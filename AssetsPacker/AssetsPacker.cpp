#include <string>
#include <cstdlib>
#include <vector>
#include <map>
#include <algorithm>
#include "png.h"
#include "lzma.h"
#include "json.h"

const char* eras[] = { "badlands", "platform", "install", "ashworld", "jungle", "desert", "ice", "twilight" };
const int ERAS = sizeof(eras) / sizeof(eras[0]);

#define LOG_ERROR_NOEND(x, ...) fprintf(stderr, x, __VA_ARGS__); fflush(stderr)
#define LOG_ERROR(x, ...) fprintf(stderr, x "\n", __VA_ARGS__); fflush(stderr)


#define LOG_INFO_NOEND(x, ...) fprintf(stdout, x, __VA_ARGS__); fflush(stdout)
#define LOG_INFO(x, ...) fprintf(stdout, x "\n", __VA_ARGS__); fflush(stdout)

//#define CHECK_COMPRESSION_RESULTS

class Progress {

public:

    Progress(const char* p) {
        txt = p;
        previous = current;
        current = this;
        LOG_INFO_NOEND("Processing: ");
        Write();
        LOG_INFO("");
    }

    ~Progress() {
        current = previous;
    }

private:

    void Write() {
        if (previous) {
            previous->Write();
            LOG_INFO_NOEND(" > ");
        }
        LOG_INFO_NOEND("%s", txt);
    }

    const char* txt = nullptr;

    static Progress* current;

    Progress* previous = nullptr;

};

Progress* Progress::current = nullptr;

template<typename T>
class Memory {

public:

    Memory(Memory&) = delete;
    Memory(Memory const&) = delete;
    Memory& operator=(Memory&) = delete;
    Memory& operator=(Memory const&) = delete;
 
    Memory(Memory&& another) {
        this->ptr = (T*)another.ptr;
        this->size = another.size;
        this->release = another.release;
        another.ptr = nullptr;
    }

    Memory(int count) {
        size = sizeof(T) * count;
        ptr = (T*)calloc(count, sizeof(T));
        if (!Valid()) {
            LOG_ERROR("Failed to allocate %d bytes", (int)(count * sizeof(T)));
        }
    }

    Memory(T* data, unsigned int dataSize, bool releaseAfter=true) {
        this->size = dataSize;
        this->ptr = data;
        this->release = releaseAfter;
    }

    ~Memory() {
        if (ptr != nullptr && release) {
            free(ptr);
            ptr = nullptr;
        }
    }

    void Release() {
        if (ptr) {
            free(ptr);
            ptr = nullptr;
        }
    }

    T* Get() {
        return (T*)ptr;
    }

    operator T* () {
        return Get();
    }

    bool Valid() {
        return ptr != nullptr;
    }

    unsigned int GetSize() {
        return size;
    }

    unsigned int GetCount() {
        return size / sizeof(T);
    }

private:
    unsigned int size = 0;
    T* ptr = nullptr;
    bool release = true;

};

using ByteBuffer = Memory<unsigned char>;

class TilesetFileGuard {

public:

    TilesetFileGuard() {
        for (int i = 0; i < ERAS * 3; i++) {
            f[i] = nullptr;
        }
    }

    ~TilesetFileGuard() {
        for (int i = 0; i < ERAS * 3; i++) {
            if (f[i] != nullptr) {
                fclose(f[i]);
                f[i] = nullptr;
            }
        }
    }

    FILE*& Map(int eraIdx) {
        return f[eraIdx];
    }
    
    FILE*& Image(int eraIdx) {
        return f[ERAS + eraIdx];
    }
    
    FILE*& Out(int eraIdx) {
        return f[ERAS + ERAS + eraIdx];
    }

private:
    FILE* f[ERAS * 3] = { 0 };
};

class SpritesFileGuard {

public:

    SpritesFileGuard() {
        for (int i = 0; i < 4; i++) {
            f[i] = nullptr;
        }
    }

    ~SpritesFileGuard() {
        for (int i = 0; i < 4; i++) {
            if (f[i] != nullptr) {
                fclose(f[i]);
                f[i] = nullptr;
            }
        }
    }

    FILE*& Sprites() {
        return f[0];
    }
    
    FILE*& Map() {
        return f[1];
    }
    
    FILE*& Json() {
        return f[2];
    }
    
    FILE*& Out() {
        return f[3];
    }

private:
    FILE* f[4] = { 0 };
};

class FileContents {

public:

    FileContents(FILE* f, int padding = 0) {
        Progress p("Reading file contents");
        size = FileSize(f);
        m = new Memory<unsigned char>(size + padding);
        if (!m->Valid()) {
            return;
        }
        unsigned int rd = 0;
        unsigned char* buff = m->Get();
        while (rd != size) {
            int r = (int)fread(&(buff[rd]), 1, size - rd, f);
            if (r > 0) {
                rd += r;
            } else {
                LOG_ERROR("Failed to read file");
                return;
            }
        }
        for (int i = 0; i < padding; i++) {
            buff[size + i] = 0;
        }
        valid = true;
    }

    ~FileContents() {
        if (m != nullptr) {
            delete m;
            m = nullptr;
        }
    }

    bool Valid() {
        return m->Valid() && valid;
    }

    unsigned char* Get() {
        return m->Get();
    }

    operator unsigned char* () {
        return Get();
    }

    unsigned int GetSize() {
        return size;
    }

    operator ByteBuffer* () {
        return m;
    }

    void Release() {
        if (m != nullptr) {
            delete m;
            m = nullptr;
        }
    }

private:

    unsigned int FileSize(FILE* f) {
        fseek(f, 0L, SEEK_END);
        unsigned int sz = (unsigned int)ftell(f);
        rewind(f);
        return sz;
    }

    bool valid = false;
    unsigned int size = 0;
    ByteBuffer* m = nullptr;
};

class Bitmap {

public:

    Bitmap(FILE* f, bool includeAlpha) {
        Progress p("Loading PNG image");
        this->includeAlpha = includeAlpha;
        FileContents fc(f);
        if (!fc.Valid()) {
            return;
        }
        if (!decode_png(&pixels, fc.Get(), fc.GetSize(), &w, &h, includeAlpha)) {
            LOG_ERROR("Failed to decode png");
            return;
        }

        valid = true;
    }

    bool Valid() {
        return valid;
    }

    unsigned int GetWidth() {
        return w;
    }

    unsigned int GetHeight() {
        return h;
    }

    void PixelAt(unsigned int x, unsigned int y, unsigned char& r, unsigned char& g, unsigned char& b, unsigned char& a) {
        if (includeAlpha) {
            unsigned int idx = ((y * w) + x) * 4;
            r = (unsigned char)pixels[idx + 0];
            g = (unsigned char)pixels[idx + 1];
            b = (unsigned char)pixels[idx + 2];
            a = (unsigned char)pixels[idx + 3];
        } else {
            unsigned int idx = ((y * w) + x) * 3;
            r = (unsigned char)pixels[idx + 0];
            g = (unsigned char)pixels[idx + 1];
            b = (unsigned char)pixels[idx + 2];
            a = 0xff;
        }
    }

private:

    static bool decode_png(std::vector<unsigned char>* pixelsOut, unsigned char* bdata, unsigned int dataLength, unsigned int* wOut, unsigned int* hOut, bool includeAlpha) {
        int w = 0, h = 0;
        int d = 0;
        unsigned char* data = stbi_load_from_memory(bdata, dataLength, &w, &h, &d, includeAlpha ? 4 : 3);
        if (data == nullptr) {
            LOG_ERROR("Failed to decode a png file");
            return false;
        } else if (d != 4 && d != 3) {
            stbi_image_free(data);
            LOG_ERROR("Unknown palette: %d", d);
            return false;
        }
        *pixelsOut = std::vector<unsigned char>(data, data + ((includeAlpha ? 4 : 3) * w * h));
        *wOut = w;
        *hOut = h;
        return true;
    }

    static bool encode_png(std::vector<unsigned char>* in, std::vector<unsigned char>* out, unsigned int w, unsigned int h) {
        struct tmp {
            std::vector<unsigned char>* out;
        } tmpI{ out };

        auto func = [](void* context, void* data, int size) {
            struct tmp* tmpI = (struct tmp*)context;
            unsigned char* pixels = (unsigned char*)data;
            *(tmpI->out) = std::vector<unsigned char>(pixels, pixels + size);
        };
        unsigned char* buffer = &(in->front());
        int res = stbi_write_png_to_func(func, &tmpI, w, h, 4, buffer, 4 * w);
        return res != 0;
    }

    bool valid = false;
    unsigned int w = 0;
    unsigned int h = 0;
    bool includeAlpha = false;

    std::vector<unsigned char> pixels;

};

class LZMA {
private:
    
    LZMA() {}

public:

    static ByteBuffer Decode(ByteBuffer& buffer) {
        char* output = nullptr;
        unsigned int outputLength = 0;
        bool error = false;
        lzma_decompress((char*)buffer.Get(), buffer.GetSize(), &output, &outputLength, &error);
        if (error || output == nullptr) {
            LOG_ERROR("LZMA decompression failed");
            if (output) {
                free(output);
            }
            ByteBuffer b(nullptr, 0);
            return std::move(b);
        }
        ByteBuffer b((unsigned char*)output, outputLength);
        return std::move(b);
    }
    
    static ByteBuffer Encode(ByteBuffer& buffer) {
        char* output = nullptr;
        unsigned int outputLength = 0;
        bool error = false;
        lzma_compress((char*)buffer.Get(), buffer.GetSize(), &output, &outputLength, &error);
        if (error || output == nullptr) {
            LOG_ERROR("LZMA cecompression failed");
            if (output) {
                free(output);
            }
            ByteBuffer b(nullptr, 0);
            return std::move(b);
        }
        ByteBuffer b((unsigned char*)output, outputLength);
#ifdef CHECK_COMPRESSION_RESULTS
        {
            ByteBuffer check = LZMA::Decode(b);
            if (!check.Valid() || check.GetSize() != buffer.GetSize()) {
                LOG_ERROR("LZMA compression failed: failed to validate compressed data");
                ByteBuffer b(nullptr, 0);
                return std::move(b);
            } else {
                unsigned char* src = check.Get();
                unsigned char* dst = buffer.Get();
                if (memcmp(src, dst, check.GetSize())) {
                    LOG_ERROR("LZMA compression failed: failed to validate compressed data");
                    ByteBuffer b(nullptr, 0);
                    return std::move(b);
                }
            }
        }
#endif
        return std::move(b);
    }

};

class ImageEncoder {
    
private:

    ImageEncoder() {}

public:

    template<typename Color, typename PalColor>
    static void GetPallete(Memory<Color>& pal, Memory<PalColor>& data) {

        // Convert pal to dictionary
        std::map<PalColor, Color> palDict;
        std::map<Color, PalColor> palBack;
        for (unsigned int i = 0; i < pal.GetCount(); i++) {
            PalColor to = (PalColor)i;
            Color pc = pal[i];
            palDict[to] = pc;
            palBack[pc] = to;
        }

        // Count number of color occurances
        std::map<PalColor, int> counter;
        for (unsigned int i = 0, o = data.GetCount(); i < o; i++) {
            PalColor& d = data[i];
            int cnt = 0;
            const auto& counterit = counter.find(d);
            if (counterit == counter.end()) {
                counter[d] = 1;
            } else {
                int& cc = counterit->second;
                cc++;
            }
        }

        // Unmap colors from current pallete
        Memory<Color> dataUnmapped(data.GetCount());
        for (int i = 0, o = data.GetCount(); i < o; i++) {
            PalColor pc = data[i];
            Color c = palDict[pc];
            dataUnmapped[i] = c;
        }

        // Sort and recreate mapping of colors to pallete
        std::map<Color, PalColor> remap;
  
        auto cmp = [&](const Color& a, const Color& b) -> bool {
            int cx = counter[palBack[a]];
            int cy = counter[palBack[b]];
            return cx > cy;
        };

        Color* first = &(pal[0]);
        Color* end = &(pal[pal.GetCount()]);
        {
            Progress p("Sorting pallete indexes");
            std::sort(first, end, cmp);
        }

        for (unsigned int i = 0; i < pal.GetCount(); i++) {
            Color c = pal[i];
            PalColor pc = (PalColor)i;
            remap[c] = pc;
        }
        for (unsigned int i = 0; i < dataUnmapped.GetCount(); i++) {
            data[i] = remap[dataUnmapped[i]];
        }
        
    }

    static ByteBuffer EncodeSeparateChannels(int w, int h, Bitmap* bm, bool includeAlpha) {
        Progress p("Encoding separate channels");
        Memory<unsigned char> palleteR(0xff + 1);
        Memory<unsigned char> palleteG(0xff + 1);
        Memory<unsigned char> palleteB(0xff + 1);
        Memory<unsigned char> palleteA(includeAlpha ? 1 : 0xff + 1);

        Memory<unsigned char> palleteIdxR(0xff + 1);
        Memory<unsigned char> palleteIdxG(0xff + 1);
        Memory<unsigned char> palleteIdxB(0xff + 1);
        Memory<unsigned char> palleteIdxA(0xff + 1);

        bool palleteHasR[0xff + 1] = { 0 };
        bool palleteHasG[0xff + 1] = { 0 };
        bool palleteHasB[0xff + 1] = { 0 };
        bool palleteHasA[0xff + 1] = { 0 };

        unsigned char palleteCntR = 0;
        unsigned char palleteCntG = 0;
        unsigned char palleteCntB = 0;
        unsigned char palleteCntA = 0;

        ByteBuffer r(w * h);
        ByteBuffer g(w * h);
        ByteBuffer b(w * h);
        ByteBuffer a(includeAlpha ? w * h : 1);

        {

            auto checkC = [](bool* palleteHas, Memory<unsigned char>& palleteIdx, Memory<unsigned char>& pallete, Memory<unsigned char>& colors, unsigned char& palleteCnt, int i) {

                unsigned char idx = 0;
                if (palleteHas[colors[i]]) {
                    idx = palleteIdx[colors[i]];
                } else {
                    palleteHas[colors[i]] = true;
                    idx = palleteCnt;
                    palleteIdx[colors[i]] = idx;
                    pallete[idx] = colors[i];
                    palleteCnt++;
                }
                colors[i] = idx;
            };

            Progress p("Indexing colors");
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    int i = (y * w) + x;
                    unsigned char pa = 0;
                    bm->PixelAt(x, y, r[i], g[i], b[i], pa);
                    if (includeAlpha) {
                        a[i] = pa;
                    }

                    checkC(palleteHasR, palleteIdxR, palleteR, r, palleteCntR, i);
                    checkC(palleteHasG, palleteIdxG, palleteG, g, palleteCntG, i);
                    checkC(palleteHasB, palleteIdxB, palleteB, b, palleteCntB, i);
                    if (includeAlpha) {
                        checkC(palleteHasA, palleteIdxA, palleteA, a, palleteCntA, i);
                    }
                }
            }
        }
      

        {
            Progress p("Converting palletes (R)");
            GetPallete(palleteR, r);
        }
        {
            Progress p("Converting palletes (G)");
            GetPallete(palleteG, g);
        }
        {
            Progress p("Converting palletes (B)");
            GetPallete(palleteB, b);
        }
        if (includeAlpha) {
            Progress p("Converting palletes (A)");
            GetPallete(palleteA, a);
        }
        
        Progress p0("Encoding pixel data");
        ByteBuffer re = LZMA::Encode(r);
        r.Release();
        if (!re.Valid()) {
            LOG_ERROR("Failed to encode R channel");
        }
        ByteBuffer ge = LZMA::Encode(g);
        if (!ge.Valid()) {
            LOG_ERROR("Failed to encode G channel");
        }
        g.Release();
        ByteBuffer be = LZMA::Encode(b);
        if (!be.Valid()) {
            LOG_ERROR("Failed to encode B channel");
        }
        b.Release();
        ByteBuffer ae = LZMA::Encode(a);
        if (!ae.Valid()) {
            LOG_ERROR("Failed to encode A channel");
        }
        a.Release();

        unsigned int resultSize = 24 + (includeAlpha ? 8 : 0);
        resultSize += palleteR.GetSize() + palleteG.GetSize() + palleteB.GetSize() + (includeAlpha ? palleteA.GetSize() : 0);
        resultSize += re.GetSize() + ge.GetSize() + be.GetSize() + (includeAlpha ? ae.GetSize() : 0);

        ByteBuffer outs(resultSize);
        if (!outs.Valid()) {
            LOG_ERROR("Failed to create output buffer");
            return false;
        }

        unsigned int pos = 0;
        bool error = false;
        error |= !WriteArray(outs, palleteR, pos);
        error |= !WriteArray(outs, palleteG, pos);
        error |= !WriteArray(outs, palleteB, pos);
        if (includeAlpha) {
            error |= !WriteArray(outs, palleteA, pos);
        }
        error |= !WriteArray(outs, re, pos);
        error |= !WriteArray(outs, ge, pos);
        error |= !WriteArray(outs, be, pos);
        if (includeAlpha) {
            error |= !WriteArray(outs, ae, pos);
        }

        error |= pos != outs.GetSize();
        if (error) {
            ByteBuffer bf(nullptr, 0);
            return std::move(bf);
        }
        return std::move(outs);
    }

    static bool CheckEncodedSeparateChannels(int w, int h, Bitmap* bm, ByteBuffer& input, bool includeAlpha) {
        Progress p("Checking separate channels encoding");
        unsigned int pos = 0;
        ByteBuffer dummy(nullptr, 0);

        ByteBuffer palR = ReadArray(input, pos);
        ByteBuffer palG = ReadArray(input, pos);
        ByteBuffer palB = ReadArray(input, pos);
        ByteBuffer palA = includeAlpha ? ReadArray(input, pos) : std::move(dummy);
        if (!palR.Valid() || !palG.Valid() || !palB.Valid() || (includeAlpha && !palA.Valid())) {
            LOG_ERROR("Failed to read pallete");
            return false;
        }
        
        ByteBuffer re = ReadArray(input, pos);
        ByteBuffer ge = ReadArray(input, pos);
        ByteBuffer be = ReadArray(input, pos);
        ByteBuffer ae = includeAlpha ? ReadArray(input, pos) : std::move(dummy);
        if (!re.Valid() || !ge.Valid() || !be.Valid() || (includeAlpha && !ae.Valid())) {
            LOG_ERROR("Failed to read pallete");
            return false;
        }

        ByteBuffer dre = LZMA::Decode(re);
        re.Release();
        ByteBuffer dge = LZMA::Decode(ge);
        ge.Release();
        ByteBuffer dbe = LZMA::Decode(be);
        be.Release();
        ByteBuffer dae = includeAlpha ? LZMA::Decode(ae) : std::move(dummy);
        ae.Release();
    
        if (!dre.Valid() || !dge.Valid() || !dbe.Valid() || (includeAlpha && !dae.Valid())) {
            LOG_ERROR("Failed to decrypt pixel data");
            return false;
        }

        if (dre.GetSize() != w * h || dre.GetSize() != dbe.GetSize() || dbe.GetSize() != dge.GetSize() || (includeAlpha && dae.GetSize() != dre.GetSize())) {
            LOG_ERROR("Invalid dimensional data");
            return false;
        }
        for (unsigned int i = 0; i < dre.GetSize(); i++) {
            unsigned char r = palR[dre[i]];
            unsigned char g = palG[dge[i]];
            unsigned char b = palB[dbe[i]];
            unsigned char a = includeAlpha ? palA[dae[i]] : 0xff;

            int x = i % w;
            int y = i / w;
            
            unsigned char pr = 0;
            unsigned char pg = 0;
            unsigned char pb = 0;
            unsigned char pa = 0;
            bm->PixelAt(x, y, pr, pg, pb, pa);
            if (pr != r || pg != g && pb != b && (includeAlpha && pa != a)) {
                LOG_ERROR("Pixel checking failed at %d", i);
                return false;
            }
        }

        return true;
    }

    static bool CheckEncodedJoinedChannels(int w, int h, Bitmap* bm, ByteBuffer& input, bool includeAlpha) {
        Progress p("Checking joined channels encoding");

        int bytesPerPixel = includeAlpha ? 4 : 3;
        unsigned char palBytesPerPixel = 0;
        unsigned int pos = 0;
        if (!ReadByte(input, pos, palBytesPerPixel)) {
            return false;
        }
        ByteBuffer rawPalleteC = ReadArray(input, pos);
        if (!rawPalleteC.Valid()) {
            return false;
        }
        ByteBuffer rgbe = ReadArray(input, pos);
        if (!rgbe.Valid()) {
            return false;
        }

        ByteBuffer rgb = LZMA::Decode(rgbe);
        rgbe.Release();
        if (!rgb.Valid()) {
            return false;
        }
        
        Memory<unsigned int> pallete (rawPalleteC.GetCount() / bytesPerPixel);
        for (unsigned int i = 0; i < pallete.GetCount(); i++) {
            int idx = i * bytesPerPixel;
            pallete[i] = 0;
            pallete[i] += (unsigned int)rawPalleteC[idx + 0] << 16;
            pallete[i] += (unsigned int)rawPalleteC[idx + 1] << 8;
            pallete[i] += (unsigned int)rawPalleteC[idx + 2] << 0;
            if (includeAlpha) {
                pallete[i] += (unsigned int)rawPalleteC[idx + 3] << 24;
            }
        }
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                int i = ((y * w) + x) * palBytesPerPixel;

                unsigned int palColor = 0;
                switch (palBytesPerPixel) {
                case 4:
                    palColor += ((unsigned int)rgb[i + 3]) << 24;

                case 3:
                    palColor += ((unsigned int)rgb[i + 2]) << 16;

                case 2:
                    palColor += ((unsigned int)rgb[i + 1]) << 8;

                case 1:
                    palColor += ((unsigned int)rgb[i + 0]) << 0;
                    break;

                }
                unsigned int color = pallete[palColor];

                unsigned char pr = 0;
                unsigned char pg = 0;
                unsigned char pb = 0;
                unsigned char pa = 0;

                bm->PixelAt(x, y, pr, pg, pb, pa);

                unsigned int pcolor = (pa << 24) + (pr << 16) + (pg << 8) + pb;

                if (!includeAlpha) {
                    pcolor &= 0xffffff;
                    color &= 0xffffff;
                }
                if (pcolor != color) {
                    LOG_ERROR("Pixel checking failed at %d", i);
                    return false;
                }
            }
        }

        return true;
    }

    static ByteBuffer EncodeJoinedChannels(int w, int h, Bitmap* bm, bool includeAlpha) {
        Progress p("Encoding joined channels");

        std::vector<unsigned int> pallete;
        std::map<unsigned int, int> palleteIdx;
        unsigned int palleteCount = 0;

        int bytesPerPixel = includeAlpha ? 4 : 3;
        Memory<unsigned int> rgb(w * h);

        if (!rgb.Valid()) {
            LOG_ERROR("Failed to allocate rpb buffer");
        }

        {
            Progress p("Indexing colors");
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    int i = ((y * w) + x);

                    unsigned char r = 0;
                    unsigned char g = 0;
                    unsigned char b = 0;
                    unsigned char a = 0;
                    bm->PixelAt(x, y, r, g, b, a);
                    if (!includeAlpha) {
                        a = 0xff;
                    }

                    unsigned ri = r;
                    unsigned gi = g;
                    unsigned bi = b;
                    unsigned ai = a;

                    unsigned int color = (ai << 24) + (ri << 16) + (gi << 8) + bi;
                    if (!includeAlpha) {
                        color = color & 0xffffff;
                    }

                    auto findit = palleteIdx.find(color);
                    if (findit == palleteIdx.end()) {
                        unsigned int idx = palleteCount;
                        palleteCount++;
                        pallete.push_back(color);
                        palleteIdx[color] = idx;
                        rgb[i] = idx;
                    } else {
                        unsigned int idx = findit->second;
                        rgb[i] = idx;
                    }
                }
            }
        }

        {
            Progress p("Converting palletes");
            Memory<unsigned int> palleteTmp(&pallete.at(0), (int)pallete.size() * (int)sizeof(unsigned int), false);
            GetPallete(palleteTmp, rgb);
        }

        ByteBuffer rawPalleteC((int)pallete.size() * bytesPerPixel);
        if (!rawPalleteC.Valid()) {
            LOG_ERROR("Failed to allocate rpb data pallete buffer");
            return false;
        }

        for (unsigned int i = 0; i < pallete.size(); i++) {
            int idx = i * bytesPerPixel;
            rawPalleteC[idx + 0] = (unsigned char)((pallete[i] >> 16) & 0xff);
            rawPalleteC[idx + 1] = (unsigned char)((pallete[i] >> 8) & 0xff);
            rawPalleteC[idx + 2] = (unsigned char)((pallete[i] >> 0) & 0xff);
            if (includeAlpha) {
                rawPalleteC[idx + 3] = (unsigned char)((pallete[i] >> 24) & 0xff);
            }
        }


        unsigned char palBytesPerPixel;
        if (pallete.size() <= 0xff) {
            palBytesPerPixel = 1;
        } else if (pallete.size() <= 0xffff) {
            palBytesPerPixel = 2;
        } else if (pallete.size() <= 0xffffff) {
            palBytesPerPixel = 3;
        } else {
            palBytesPerPixel = 4;
        }

        ByteBuffer rgbb(rgb.GetCount() * palBytesPerPixel);
        if (!rgbb.Valid()) {
            LOG_ERROR("Failed to allocate rpb data buffer");
            return false;
        }
        {
            Progress p("Processing pixel data");

            for (unsigned int i = 0; i < rgb.GetCount(); i++) {
                int palColor = rgb[i];
                switch (palBytesPerPixel) {
                case 4:
                    rgbb[(i * palBytesPerPixel) + 3] = (unsigned char)((palColor >> 24) & 0xff);

                case 3:
                    rgbb[(i * palBytesPerPixel) + 2] = (unsigned char)((palColor >> 16) & 0xff);

                case 2:
                    rgbb[(i * palBytesPerPixel) + 1] = (unsigned char)((palColor >> 8) & 0xff);

                case 1:
                    rgbb[(i * palBytesPerPixel) + 0] = (unsigned char)((palColor >> 0) & 0xff);
                    break;
                }
            }
        }

        ByteBuffer rgbe = LZMA::Encode(rgbb);
        rgbb.Release();
        if (!rgbe.Valid()) {
            LOG_ERROR("Failed to compress rpb data");
            return false;
        }

        unsigned int resultSize = 9 + rawPalleteC.GetSize() + rgbe.GetSize();
        ByteBuffer outs(resultSize);
        if (!outs.Valid()) {
            LOG_ERROR("Failed to create output buffer");
            return false;
        }

        unsigned int pos = 0;
        bool error = false;

        error |= !WriteByte(outs, palBytesPerPixel, pos);
        error |= !WriteArray(outs, rawPalleteC, pos);
        error |= !WriteArray(outs, rgbe, pos);

        error |= pos != outs.GetSize();
        if (error) {
            ByteBuffer bf(nullptr, 0);
            return std::move(bf);
        }
        return std::move(outs);
    }

    static bool ReadByte(ByteBuffer& buffer, unsigned int& position, unsigned char& b) {
        if (position < buffer.GetSize()) {
            b = buffer[position];
            position++;
            return true;
        }
        LOG_ERROR("Failed to read byte from byte buffer");
        return false;
    }

    static ByteBuffer ReadArray(ByteBuffer& buffer, unsigned int& position) {
        unsigned int sz = 0;
        if (ReadInt(buffer, position, sz)) {
            if (position + sz <= buffer.GetSize()) {
                ByteBuffer b(sz);
                if (!b.Valid()) {
                    LOG_ERROR("Failed to create new buffer");
                    return false;
                }
                unsigned char* src = &(buffer[position]);
                unsigned char* dst = b.Get();
                memcpy(dst, src, sz);
                position += sz;
                return std::move(b);
            } else {
                LOG_ERROR("Failed to read buffer from byte buffer");
            }
        }
        ByteBuffer b(nullptr, 0);
        return std::move(b);
    }
    
    static bool ReadInt(ByteBuffer& buffer, unsigned int& position, unsigned int& i) {
        unsigned char b1 = 0;
        unsigned char b2 = 0;
        unsigned char b3 = 0;
        unsigned char b4 = 0;

        if (ReadByte(buffer, position, b1)) {
            if (ReadByte(buffer, position, b2)) {
                if (ReadByte(buffer, position, b3)) {
                    if (ReadByte(buffer, position, b4)) {
                        unsigned int i1 = b1;
                        unsigned int i2 = b2;
                        unsigned int i3 = b3;
                        unsigned int i4 = b4;
                        i = (i1 << 24) + (i2 << 16) + (i3 << 8) + i4;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    static bool WriteArray(FILE* f, ByteBuffer* buffer) {
        if (WriteInt(f, buffer->GetSize())) {
            size_t sz = fwrite(buffer->Get(), 1, buffer->GetSize(), f);
            if (sz == buffer->GetSize()) {
                return true;
            }
            LOG_ERROR("Failed to write file");
        }
        return false;
    }

    static bool WriteInt(FILE* f, int value) {
        unsigned int v = (unsigned int)value;
        if (WriteByte(f, (unsigned char)((v >> 24) & 0xff))) {
            if (WriteByte(f, (unsigned char)((v >> 16) & 0xff))) {
                if (WriteByte(f, (unsigned char)((v >> 8) & 0xff))) {
                    if (WriteByte(f, (unsigned char)((v >> 0) & 0xff))) {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    static bool WriteByte(FILE* f, unsigned char b) {
        size_t sz = fwrite(&b, 1, 1, f);
        if (sz != 1) {
            LOG_ERROR("Failed to write file");
            return false;
        }
        return true;
    }

    static bool WriteArray(ByteBuffer& f, ByteBuffer& buffer, unsigned int& position) {
        if (WriteInt(f, buffer.GetSize(), position)) {
            if (position + buffer.GetSize() <= f.GetSize()) {
                unsigned char* src = buffer.Get();
                unsigned char* dst = &(f.Get()[position]);
                memcpy(dst, src, buffer.GetSize());
                position += buffer.GetSize();
                return true;
            } else {
                LOG_ERROR("Failed to write data buffer to byte buffer");
                return false;
            }
        }
        return false;
    }

    static bool WriteInt(ByteBuffer& f, int value, unsigned int& position) {
        unsigned int v = (unsigned int)value;
        if (WriteByte(f, (unsigned char)((v >> 24) & 0xff), position)) {
            if (WriteByte(f, (unsigned char)((v >> 16) & 0xff), position)) {
                if (WriteByte(f, (unsigned char)((v >> 8) & 0xff), position)) {
                    if (WriteByte(f, (unsigned char)((v >> 0) & 0xff), position)) {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    static bool WriteByte(ByteBuffer& f, unsigned char b, unsigned int& position) {
        if (position < f.GetSize()) {
            f[position] = b;
            position++;
        } else{
            LOG_ERROR("Failed to write file to byte buffer");
            return false;
        }
        return true;
    }

    static bool AsyncWriteProcessFile(FILE* inStream, FILE* mapping, FILE* outStream, unsigned int* resultSize) {
        bool includeAlpha = false;

        resultSize[0] = 0;

        Bitmap bm(inStream, includeAlpha);
        if (!bm.Valid()) {
            LOG_ERROR("Failed to parse bitmap");
            return false;
        }
        int w = bm.GetWidth();
        int h = bm.GetHeight();

        
        ByteBuffer c1 = EncodeSeparateChannels(w, h, &bm, includeAlpha);
        if (!c1.Valid()) {
            LOG_ERROR("Failed to encode C1");
            return false;
        }
        if (!CheckEncodedSeparateChannels(w, h, &bm, c1, includeAlpha)) {
            LOG_ERROR("Checking C1 encoding failed");
            return false;
        }

        ByteBuffer c1e = LZMA::Encode(c1);
        if (!c1e.Valid()) {
            LOG_ERROR("Failed to encode C1E");
            return false;
        }
        
        ByteBuffer* r1 = nullptr;
        unsigned char a1 = 0;
        if (c1e.GetSize() < c1.GetSize()) {
            c1.Release();
            r1 = &c1e;
            a1 = 2;
        } else {
            c1e.Release();
            r1 = &c1;
            a1 = 0;
        }
        
        ByteBuffer c2 = EncodeJoinedChannels(w, h, &bm, includeAlpha);
        if (!c2.Valid()) {
            LOG_ERROR("Failed to encode C2");
            return false;
        }
        if (!CheckEncodedJoinedChannels(w, h, &bm, c2, includeAlpha)) {
            LOG_ERROR("Checking C2 encoding failed");
            return false;
        }

        ByteBuffer c2e = LZMA::Encode(c2);
        if (!c2e.Valid()) {
            LOG_ERROR("Failed to encode C2E");
            return false;
        }
        ByteBuffer* r2 = nullptr;
        unsigned char a2;
        if (c2e.GetSize() < c2.GetSize()) {
            c2.Release();
            r2 = &c2e;
            a2 = 3;
        } else {
            c2e.Release();
            r2 = &c2;
            a2 = 1;
        }
        
        ByteBuffer* m = r1->GetSize() < r2->GetSize() ? r1 : r2;
        unsigned char alg = r1->GetSize() < r2->GetSize() ? a1 : a2;

        FileContents mappingBuffer(mapping);
        bool error = false;
        if (!mappingBuffer.Valid()) {
            LOG_ERROR("Failed to read mapping file");
            return false;
        }
        error |= !WriteArray(outStream, mappingBuffer);
        error |= !WriteInt(outStream, w);
        error |= !WriteInt(outStream, h);
        error |= !WriteByte(outStream, alg);
        error |= !WriteByte(outStream, includeAlpha ? 1 : 0);
        error |= !WriteArray(outStream, m);
        resultSize[0] = m->GetSize() + mappingBuffer.GetSize() + 18;
        return !error;
    }

};

class JsonGuard {

public:

    JsonGuard(JsonValue* value) {
        this->value = value;
    }

    ~JsonGuard() {
        if (value) {
            delete value;
            value = nullptr;
        }
    }

    operator JsonValue*() {
        return value;
    }

private:
    JsonValue* value = nullptr;
};

class JsonImage {

public:
    
    JsonImage(JsonValue* value) {
        if (!value->IsObject()) {
            LOG_ERROR("Expected object");
            return;
        }
        JsonObject* obj = value->AsObject();
        bool error = false;
        x = obj->GetRawInt("x", error);
        y = obj->GetRawInt("y", error);
        w = obj->GetRawInt("w", error);
        h = obj->GetRawInt("h", error);
        shiftx = obj->GetRawInt("shiftx", error);
        shifty = obj->GetRawInt("shifty", error);
        if (error) {
            LOG_ERROR("Missing field");
            return;
        }

        valid = true;
    }

    int GetX() {
        return x;
    }
    
    int GetY() {
        return y;
    }
    
    int GetWidth() {
        return w;
    }
    
    int GetHeight() {
        return h;
    }

    int GetShiftX() {
        return shiftx;
    }

    int GetShiftY() {
        return shifty;
    }

    bool IsValid() {
        return valid;
    }

private:
    bool valid = false;
    
    int x = 0;
    int y = 0;
    int w = 0;
    int h = 0;
    int shiftx = 0;
    int shifty = 0;

};

class JsonSprite {

public:
    
    JsonSprite(JsonValue* value) {
        if (!value->IsObject()) {
            LOG_ERROR("Expected object");
            return;
        }
        JsonObject* obj = value->AsObject();
        overlays = obj->GetArray("overlays");
        if (overlays == nullptr) {
            LOG_ERROR("Missing field overlays");
            return;
        }
        for (unsigned int i = 0; i < overlays->GetSize(); i++) {
            JsonValue* ov = overlays->GetValueAt(i);
            if (!ov->IsObject()) {
                LOG_ERROR("Expected object");
                return;
            }
            obj = ov->AsObject();
            JsonArray* ar = obj->AsObject()->GetArray("image");
            if (!ar) {
                LOG_ERROR("Expected array of images");
                return;
            }
            for (unsigned int o = 0; o < ar->GetSize(); o++) {
                JsonValue* val = ar->GetValueAt(o);
                if (!val->IsNumber()) {
                    LOG_ERROR("Expected number");
                    return;
                }
            }
            
        }

        valid = true;
    }

    bool IsValid() {
        return valid;
    }

    int GetOverlaysCount() {
        return overlays->GetSize();
    }
    
    int GetOverlaysImagesCount(int overlayID) {
        JsonObject* obj = overlays->GetValueAt(overlayID)->AsObject();
        return obj->GetArray("image")->GetSize();
    }

    int GetOverlayImageID(int overlayID, int imageIDx) {
        JsonValue* ov = overlays->GetValueAt(overlayID);
        return ov->AsObject()->GetArray("image")->GetValueAt(imageIDx)->AsNumber()->IntValue();
    }

    bool IsShadow(int overlayID) {
        JsonObject* ov = overlays->GetValueAt(overlayID)->AsObject();
        bool error = false;
        bool b = ov->GetRawBoolean("shadow", error);
        if (!error) {
            return b;
        }
        return false;
    }
    
    bool IsHidingIfCloaked(int overlayID) {
        JsonObject* ov = overlays->GetValueAt(overlayID)->AsObject();
        bool error = false;
        bool b = ov->GetRawBoolean("hideIfCloaked", error);
        if (!error) {
            return b;
        }
        return false;
    }
      
    int GetOffsetY(int overlayID) {
        JsonObject* ov = overlays->GetValueAt(overlayID)->AsObject();
        bool error = false;
        int off = ov->GetRawInt("y", error);
        if (!error) {
            return off;
        }
        return 0;
    }

private:
    bool valid = false;
    
    JsonArray* overlays = nullptr;

};

static int loadTilesetFiles(TilesetFileGuard*fs, const char* input, const char* output) {

    for (int i = 0; i < ERAS; i++) {
        const char* era = eras[i];
        FILE*& fsImage = fs->Image(i);
        FILE*& fsMap = fs->Map(i);
        FILE*& fsOut = fs->Out(i);

        char buffer[1024];
        sprintf(buffer, "%s/%s.png", input, era);
        fsImage = fopen(buffer, "rb");
        if (!fsImage) {
            LOG_ERROR("Failed to open %s for reading", buffer);
            return 1;
        }

        sprintf(buffer, "%s/%s.map", input, era);
        fsMap = fopen(buffer, "rb");
        if (!fsImage) {
            LOG_ERROR("Failed to open %s for reading", buffer);
            return 1;
        }

        sprintf(buffer, "%s/%s.bin", output, era);
        fsOut = fopen(buffer, "wb");
        if (!fsImage) {
            LOG_ERROR("Failed to open %s for writing", buffer);
            return 1;
        }
    }
    return 0; 
}

static int loadSpriteFiles(SpritesFileGuard* fs, const char* input, const char* output) {

    FILE*& fsImage = fs->Sprites();
    FILE*& fsMap = fs->Map();
    FILE*& fsJsn = fs->Json();
    FILE*& fsOut = fs->Out();

    char buffer[1024];
    sprintf(buffer, "%s/sprites.png", input);
    fsImage = fopen(buffer, "rb");
    if (!fsImage) {
        LOG_ERROR("Failed to open %s for reading", buffer);
        return 1;
    }

    sprintf(buffer, "%s/spritesmask.png", input);
    fsMap = fopen(buffer, "rb");
    if (!fsImage) {
        LOG_ERROR("Failed to open %s for reading", buffer);
        return 1;
    }

    sprintf(buffer, "%s/data.json", output);
    fsJsn = fopen(buffer, "rb");
    if (!fsImage) {
        LOG_ERROR("Failed to open %s for writing", buffer);
        return 1;
    }
    
    sprintf(buffer, "%s/sprites.bin", output);
    fsOut = fopen(buffer, "wb");
    if (!fsImage) {
        LOG_ERROR("Failed to open %s for writing", buffer);
        return 1;
    }
    
    return 0; 
}

static int processFiles(TilesetFileGuard* fs) {

    for (int i = 0; i < ERAS; i++) {
        const char* era = eras[i];
        Progress p(era);
        FILE*& fsImage = fs->Image(i);
        FILE*& fsMap = fs->Map(i);
        FILE*& fsOut = fs->Out(i);

        unsigned int resultSize = 0;
        if (!ImageEncoder::AsyncWriteProcessFile(fsImage, fsMap, fsOut, &resultSize)) {
            LOG_ERROR("Failed to process");
            return 1;
        }
        
    }
    return 0;
}

static int processFiles(SpritesFileGuard* fs) {
    FileContents jsn(fs->Json(), 1);
    if (!jsn.Valid()) {
        return 1;
    }

    char* rawJson = (char*)jsn.Get();
    JsonGuard jg(JsonValue::Parse(rawJson, jsn.GetSize()));
    JsonValue* val = jg;
    if (!val) {
        LOG_ERROR("Failed to parse JSON data");
        return 1;
    }
    if (!val->IsObject()) {
        LOG_ERROR("Invalid JSON");
        return 1;
    }
    if (!val->AsObject()->GetArray("sprites") || !val->AsObject()->GetArray("images")) {
        LOG_ERROR("Invalid JSON");
        return 1;
    }

    JsonArray* sprites = val->AsObject()->GetArray("sprites");
    JsonArray* images = val->AsObject()->GetArray("images");
    
    Memory<bool> usedImages(images->GetSize());
    if (!usedImages.Valid()) {
        return 1;
    }

    int maxW = 0;
    int maxH = 0;

    int below255 = 0;

    for (unsigned int spriteID = 0; spriteID < sprites->GetSize(); spriteID++) {
        JsonSprite sprite(sprites->GetValueAt(spriteID));
        if (!sprite.IsValid()) {
            LOG_ERROR("Sprite %d is invalid", spriteID);
            return 1;
        }

        for (int spriteOverlayID = 0; spriteOverlayID < sprite.GetOverlaysCount(); spriteOverlayID++) {
            bool isShadow = sprite.IsShadow(spriteOverlayID);
            int offsetY = sprite.GetOffsetY(spriteOverlayID);

            int spriteOverlayImagesCount = sprite.GetOverlaysImagesCount(spriteOverlayID);
            for (int imageIDx = 0; imageIDx < spriteOverlayImagesCount; imageIDx++) {
                int imageID = sprite.GetOverlayImageID(spriteOverlayID, imageIDx);

                JsonImage img(images->GetValueAt(imageID));
                if (!img.IsValid()) {
                    LOG_ERROR("Invalid image");
                    return 1;
                }

                if (img.GetWidth() > maxW) {
                    maxW = img.GetWidth();
                }
                if (img.GetHeight() > maxH) {
                    maxH = img.GetHeight();
                }

                

                usedImages[imageID] = true;
            }
        }
    }

    int usedImagesTotal = 0;
    for (int i = 0; i < usedImages.GetSize(); i++) {
        JsonImage img(images->GetValueAt(i));
        if (img.GetWidth() < 0b1111111 && img.GetHeight() < 0b1111111) {
            below255++;
        }
        if (usedImages[i]) {
            usedImagesTotal++;
        }
    }
        
    return 0;
}

int main(int argc, char** argv) {
    setvbuf(stdout, NULL, _IONBF, 0);
    setvbuf(stderr, NULL, _IONBF, 0);

    if (argc != 4) {
        LOG_ERROR("Expected 3 arguments, got %d", argc - 1);
        return 1;
    }

    char type = argv[1][0] - '0';
    char* input = argv[2];
    char* output = argv[3];

    LOG_INFO("Input folder: %s\nOutput folder:%s", input, output);

    if (type == 0) {
        // Tileset
        TilesetFileGuard files;
        int res = 0;

        // Load files
        res = loadTilesetFiles(&files, input, output);
        if (res != 0) {
            return res;
        }

        // Process files
        res = processFiles(&files);
        if (res != 0) {
            return res;
        }
    } else if (type == 1) {
        // Sprites
        SpritesFileGuard files;
        
        int res = 0;

        res = loadSpriteFiles(&files, input, output);
        if (res != 0) {
            return res;
        }
        // Process files
        res = processFiles(&files);
        if (res != 0) {
            return res;
        }
    }

    return 0;
}

#define DllExport   __declspec( dllexport )

DllExport int main2(int argc, char** argv) {
    return main(argc, argv);
}
