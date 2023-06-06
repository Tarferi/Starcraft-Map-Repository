using GUILib.data;
using GUILib.ui.utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace GUILib.ui.AssetManagerWnd {

    public class EraImage {

        private readonly Bitmap bm;
        ushort[] mapping;
        private readonly int tileSize;

        private readonly int tilesX;
        private readonly int tilesY;

        private bool GetTileCoord(int idx, out int x, out int y) {
            ushort tileI = mapping[idx];
            if(tileI > 0) { 
                y = tileI / tilesX;
                x = tileI % tilesX;
                return true;
            }
            x = 0;
            y = 0;
            return false;
        }

        public void CopyTile(int tileIdx, Bitmap dst, int dstX, int dstY) {

        }

        public EraImage(Bitmap bm, ushort[] mapping, int tileSize) {
            this.bm = bm;
            this.mapping = mapping;
            this.tileSize = tileSize;
            this.tilesX = bm.Width / tileSize;
            this.tilesY = bm.Height / tileSize;
        }

        public int GetTileSize() {
            return tileSize;
        }
    }

    public class ImageEncoder {

        private static void IntToBytes(int data, ref byte[] bytes, int offset) {
            bytes[offset + 0] = (byte)((data >> 24) & 0xff);
            bytes[offset + 1] = (byte)((data >> 16) & 0xff);
            bytes[offset + 2] = (byte)((data >> 8) & 0xff);
            bytes[offset + 3] = (byte)((data >> 0) & 0xff);
        }

        private static Color[] GetPallete<Color, PalColor>(ref List<Color> pal, ref PalColor[] data, Func<int, PalColor> f1) {

            // Convert pal to dictionary
            Dictionary<PalColor, Color> palDict = new Dictionary<PalColor, Color>();
            Dictionary<Color, PalColor> palBack = new Dictionary<Color, PalColor>();
            for (int i = 0; i < pal.Count; i++) {
                PalColor to = f1(i);
                Color pc = pal[i];
                palDict[to] = pc;
                palBack[pc] = to;
            }

            // Count number of color occurances
            Dictionary<PalColor, int> counter = new Dictionary<PalColor, int>();
            foreach (PalColor d in data) {
                int cnt = 0;
                counter.TryGetValue(d, out cnt);
                cnt++;
                counter[d] = cnt;
            }

            // Unmap colors from current pallete
            Color[] dataUnmapped = new Color[data.Length];
            for (int i = 0; i < data.Length; i++) {
                PalColor pc = data[i];
                Color c = palDict[pc];
                dataUnmapped[i] = c;
            }

            // Sort and recreate mapping of colors to pallete
            Dictionary<Color, PalColor> remap = new Dictionary<Color, PalColor>();
            pal.Sort((Color x, Color y) => {
                int cx = counter[palBack[x]];
                int cy = counter[palBack[y]];
                return cx - cy;
            });
            for (int i = 0; i < pal.Count; i++) {
                Color c = pal[i];
                PalColor pc = f1(i);
                remap[c] = pc;
            }
            for (int i = 0; i < dataUnmapped.Length; i++) {
                data[i] = remap[dataUnmapped[i]];
            }
            return pal.ToArray();
        }

        private static void WriteInt(int v, Stream ms) {
            byte[] tmp = new byte[4];
            IntToBytes(v, ref tmp, 0);
            ms.Write(tmp, 0, tmp.Length);
        }

        private static int ReadInt(Stream ms) {
            int b1 = (byte)ms.ReadByte();
            int b2 = (byte)ms.ReadByte();
            int b3 = (byte)ms.ReadByte();
            int b4 = (byte)ms.ReadByte();
            return (b1 << 24) + (b2 << 16) + (b3 << 8) + b4;
        }

        private static void WriteArray(ref byte[] b, Stream ms) {
            WriteInt(b.Length, ms);
            ms.Write(b, 0, b.Length);
        }

        private static byte[] ReadArray(Stream ms) {
            int res = ReadInt(ms);
            byte[] b = new byte[res];
            ms.Read(b, 0, b.Length);
            return b;
        }

        private static byte[] EncodeSeparateChannels(int w, int h, Func<int, int, uint> colors, bool includeAlpha) {

            List<byte> palleteR = new List<byte>();
            List<byte> palleteG = new List<byte>();
            List<byte> palleteB = new List<byte>();
            List<byte> palleteA = new List<byte>();
            Dictionary<byte, byte> palleteIdxR = new Dictionary<byte, byte>();
            Dictionary<byte, byte> palleteIdxG = new Dictionary<byte, byte>();
            Dictionary<byte, byte> palleteIdxB = new Dictionary<byte, byte>();
            Dictionary<byte, byte> palleteIdxA = new Dictionary<byte, byte>();

            byte[] r = new byte[w * h];
            byte[] g = new byte[w * h];
            byte[] b = new byte[w * h];
            byte[] a = null;
            if (includeAlpha) {
                a = new byte[w * h];
            }

            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    int i = (y * w) + x;

                    uint color = colors(x, y);
                    if (!includeAlpha) {
                        color = color & 0xffffff;
                    }
                    r[i] = (byte)((color >> 16) & 0xff);
                    g[i] = (byte)((color >> 8) & 0xff);
                    b[i] = (byte)((color >> 0) & 0xff);
                    if (includeAlpha) {
                        a[i] = (byte)((color >> 24) & 0xff);
                    }

                    byte idxR = 0;
                    if (!palleteIdxR.TryGetValue(r[i], out idxR)) {
                        idxR = (byte)palleteR.Count;
                        palleteR.Add(r[i]);
                        palleteIdxR.Add(r[i], idxR);
                    }
                    r[i] = idxR;

                    byte idxG = 0;
                    if (!palleteIdxG.TryGetValue(g[i], out idxG)) {
                        idxG = (byte)palleteG.Count;
                        palleteG.Add(g[i]);
                        palleteIdxG.Add(g[i], idxG);
                    }
                    g[i] = idxG;

                    byte idxB = 0;
                    if (!palleteIdxB.TryGetValue(b[i], out idxB)) {
                        idxB = (byte)palleteB.Count;
                        palleteB.Add(b[i]);
                        palleteIdxB.Add(b[i], idxB);
                    }
                    b[i] = idxB;

                    if (includeAlpha) {
                        byte idxA = 0;
                        if (!palleteIdxA.TryGetValue(a[i], out idxA)) {
                            idxA = (byte)palleteA.Count;
                            palleteA.Add(a[i]);
                            palleteIdxA.Add(a[i], idxA);
                        }
                        a[i] = idxA;
                    }
                }
            }

            Func<int, byte> unampper = (int x) => (byte)x;

            byte[] palR = GetPallete(ref palleteR, ref r, unampper);
            byte[] palG = GetPallete(ref palleteG, ref g, unampper);
            byte[] palB = GetPallete(ref palleteB, ref b, unampper);
            byte[] palA = null;
            if (includeAlpha) {
                palA = GetPallete(ref palleteA, ref a, unampper);
            }

            byte[] re = GUILib.libs._7zip.LZMA.Encode(r);
            byte[] ge = GUILib.libs._7zip.LZMA.Encode(g);
            byte[] be = GUILib.libs._7zip.LZMA.Encode(b);
            byte[] ae = null;
            if (includeAlpha) {
                ae = GUILib.libs._7zip.LZMA.Encode(a);
            }

            MemoryStream outs = new MemoryStream();
            WriteArray(ref palR, outs);
            WriteArray(ref palG, outs);
            WriteArray(ref palB, outs);
            if (includeAlpha) {
                WriteArray(ref palA, outs);
            }
            WriteArray(ref re, outs);
            WriteArray(ref ge, outs);
            WriteArray(ref be, outs);
            if (includeAlpha) {
                WriteArray(ref ae, outs);
            }
            return outs.ToArray();
        }

        private static void DecodeSeparateChannels(int w, int h, Stream ms, Action<int, int, uint> pixels, bool includeAlpha) {
            byte[] palR = ReadArray(ms);
            byte[] palG = ReadArray(ms);
            byte[] palB = ReadArray(ms);
            byte[] palA = null;
            if (includeAlpha) {
                palA = ReadArray(ms);
            }
            byte[] re = ReadArray(ms);
            byte[] ge = ReadArray(ms);
            byte[] be = ReadArray(ms);
            byte[] ae = null;
            if (includeAlpha) {
                ae = ReadArray(ms);
            }

            re = GUILib.libs._7zip.LZMA.Decode(re);
            ge = GUILib.libs._7zip.LZMA.Decode(ge);
            be = GUILib.libs._7zip.LZMA.Decode(be);
            if (includeAlpha) {
                ae = GUILib.libs._7zip.LZMA.Decode(ae);
            }

            for (int i = 0; i < re.Length; i++) {
                uint r = (uint)palR[re[i]];
                uint g = (uint)palG[ge[i]];
                uint b = (uint)palB[be[i]];

                uint color = (r << 16) + (g << 8) + (b);
                if (includeAlpha) {
                    uint a = palA[ae[i]];
                    color = (a << 24) + color;
                }
                int x = i % w;
                int y = i / w;
                pixels(x, y, color);
            }
        }

        private static byte[] EncodeJoinedChannels(int w, int h, Func<int, int, uint> colors, bool includeAlpha) {

            List<uint> pallete = new List<uint>();
            Dictionary<uint, int> palleteIdx = new Dictionary<uint, int>();

            int bytesPerPixel = includeAlpha ? 4 : 3;
            int[] rgb = new int[w * h];

            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    int i = ((y * w) + x);

                    uint color = colors(x, y);
                    if (!includeAlpha) {
                        color = color & 0xffffff;
                    }

                    int idx = 0;
                    if (!palleteIdx.TryGetValue(color, out idx)) {
                        idx = pallete.Count;
                        pallete.Add(color);
                        palleteIdx.Add(color, idx);
                    }
                    rgb[i] = idx;
                }
            }

            Func<int, int> unampper = (int x) => (int)x;

            uint[] rawPallete = GetPallete(ref pallete, ref rgb, unampper);
            byte[] rawPalleteC = new byte[rawPallete.Length * bytesPerPixel];

            for (int i = 0; i < rawPallete.Length; i++) {
                int idx = i * bytesPerPixel;
                rawPalleteC[idx + 0] = (byte)((rawPallete[i] >> 16) & 0xff);
                rawPalleteC[idx + 1] = (byte)((rawPallete[i] >> 8) & 0xff);
                rawPalleteC[idx + 2] = (byte)((rawPallete[i] >> 0) & 0xff);
                if (includeAlpha) {
                    rawPalleteC[idx + 3] = (byte)((rawPallete[i] >> 24) & 0xff);
                }
            }

            byte palBytesPerPixel;
            if (rawPallete.Length <= 0xff) {
                palBytesPerPixel = 1;
            } else if (rawPallete.Length <= 0xffff) {
                palBytesPerPixel = 2;
            } else if (rawPallete.Length <= 0xffffff) {
                palBytesPerPixel = 3;
            } else {
                palBytesPerPixel = 4;
            }

            byte[] rgbb = new byte[rgb.Length * palBytesPerPixel];

            for (int i = 0; i < rgb.Length; i++) {
                int palColor = rgb[i];
                switch (palBytesPerPixel) {
                    case 4:
                        rgbb[(i * palBytesPerPixel) + 3] = (byte)((palColor >> 24) & 0xff);
                        goto case 3;

                    case 3:
                        rgbb[(i * palBytesPerPixel) + 2] = (byte)((palColor >> 16) & 0xff);
                        goto case 2;

                    case 2:
                        rgbb[(i * palBytesPerPixel) + 1] = (byte)((palColor >> 8) & 0xff);
                        goto case 1;

                    case 1:
                        rgbb[(i * palBytesPerPixel) + 0] = (byte)((palColor >> 0) & 0xff);
                        break;
                }
            }

            byte[] rgbe = GUILib.libs._7zip.LZMA.Encode(rgbb);

            MemoryStream outs = new MemoryStream();
            outs.WriteByte(palBytesPerPixel);
            WriteArray(ref rawPalleteC, outs);
            WriteArray(ref rgbe, outs);
            return outs.ToArray();
        }

        private static void DecodeJoinedChannels(int w, int h, Stream ms, Action<int, int, uint> colors, bool includeAlpha) {
            int bytesPerPixel = includeAlpha ? 4 : 3;
            byte palBytesPerPixel = (byte)ms.ReadByte();
            byte[] rawPalleteC = ReadArray(ms);
            byte[] rgbe = ReadArray(ms);

            byte[] rgb = GUILib.libs._7zip.LZMA.Decode(rgbe);

            uint[] pallete = new uint[rawPalleteC.Length / bytesPerPixel];
            for (int i = 0; i < pallete.Length; i++) {
                int idx = i * bytesPerPixel;
                pallete[i] = 0;
                pallete[i] += (uint)rawPalleteC[idx + 0] << 16;
                pallete[i] += (uint)rawPalleteC[idx + 1] << 8;
                pallete[i] += (uint)rawPalleteC[idx + 2] << 0;
                if (includeAlpha) {
                    pallete[i] += (uint)rawPalleteC[idx + 3] << 24;
                }
            }

            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    int i = ((y * w) + x) * palBytesPerPixel;

                    uint palColor = 0;
                    switch (palBytesPerPixel) {
                        case 4:
                            palColor += ((uint)rgb[i + 3]) << 24;
                            goto case 3;

                        case 3:
                            palColor += ((uint)rgb[i + 2]) << 16;
                            goto case 2;

                        case 2:
                            palColor += ((uint)rgb[i + 1]) << 8;
                            goto case 1;

                        case 1:
                            palColor += ((uint)rgb[i + 0]) << 0;
                            break;

                    }
                    uint color = pallete[palColor];
                    colors(x, y, color);
                }
            }
        }

        public static bool AsyncWriteProcessFile(Stream inStream, Stream mapping, Stream outStream, out uint resultSize) {
            Bitmap bm = new Bitmap(inStream);
            bool includeAlpha = false;
            resultSize = 0;

            if (bm == null) {
                return false;
            }
            int w = bm.Width;
            int h = bm.Height;
            Func<int, int, uint> colors = (x, y) => (uint)bm.GetPixel(x, y).ToArgb();

            bool error = false;
            Action<int, int, uint> pixChecker = (int px, int py, uint pcolor) => {
                uint rcolor = colors(px, py);
                if (!includeAlpha) {
                    rcolor = rcolor & 0xffffff;
                }
                if (!error) {
                    error |= pcolor != rcolor;
                    if (error) {
                        ErrorMessage.Show("Encoding failed");
                    }
                }
            };

            byte[] c1 = EncodeSeparateChannels(w, h, colors, includeAlpha);
            DecodeSeparateChannels(w, h, new MemoryStream(c1), pixChecker, includeAlpha);
            byte[] c2 = EncodeJoinedChannels(w, h, colors, includeAlpha);
            DecodeJoinedChannels(w, h, new MemoryStream(c2), pixChecker, includeAlpha);
            byte[] c1e = GUILib.libs._7zip.LZMA.Encode(c1);
            byte[] c2e = GUILib.libs._7zip.LZMA.Encode(c2);

            List<Pair<byte[], byte>> data = new List<Pair<byte[], byte>>();
            data.Add(new Pair<byte[], byte>(c1, 0));
            data.Add(new Pair<byte[], byte>(c2, 1));
            data.Add(new Pair<byte[], byte>(c1e, 2));
            data.Add(new Pair<byte[], byte>(c2e, 3));


            Pair<byte[], byte> minData = data[0];
            foreach (Pair<byte[], byte> d in data) {
                if (d.Left.Length < minData.Left.Length) {
                    minData = d;
                }
            }

            byte[] mappingC = new byte[mapping.Length];
            mapping.Read(mappingC, 0, mappingC.Length);
            WriteArray(ref mappingC, outStream);
            WriteInt(w, outStream);
            WriteInt(h, outStream);
            outStream.WriteByte(minData.right);
            outStream.WriteByte(includeAlpha ? (byte)1 : (byte)0);
            byte[] br = minData.Left;
            WriteArray(ref br, outStream);
            resultSize = (uint)(3 + minData.Left.Length);
            return true;
        }

        public static EraImage AsyncReadProcessFile(string output, string path, Stream inStream) {
            try {
                byte[] mappingC = ReadArray(inStream);
                int w = ReadInt(inStream);
                int h = ReadInt(inStream);
                bool includeAlpha = inStream.ReadByte() == 1;
                byte alg = (byte)inStream.ReadByte();
                Bitmap b = new Bitmap(w, h);

                Action<int, int, uint> pixSetter = (int px, int py, uint pcolor) => {
                    if (!includeAlpha) {
                        pcolor += (uint)0xff << 24;
                    }
                    b.SetPixel(px, py, Color.FromArgb((int)pcolor));
                };


                switch (alg) {
                    case 0:
                        DecodeSeparateChannels(w, h, inStream, pixSetter, includeAlpha);
                        break;

                    case 1:
                        DecodeJoinedChannels(w, h, inStream, pixSetter, includeAlpha);
                        break;

                    case 2:
                        byte[] tmp = new byte[inStream.Length - inStream.Position];
                        inStream.Read(tmp, 0, tmp.Length);
                        byte[] dec = GUILib.libs._7zip.LZMA.Decode(tmp);
                        inStream = new MemoryStream(dec);
                        if (alg == 2) {
                            goto case 0;
                        } else {
                            goto case 1;
                        }
                }

                ushort[] mappingS = new ushort[mappingC.Length / 2];
                for(int i = 0; i < mappingS.Length; i++) {
                    ushort b1 = mappingC[(i * 2)];
                    ushort b2 = mappingC[(i * 2) + 1];
                    ushort bx = (ushort)((b1 << 8) + b2);
                    mappingS[i] = bx;
                }
                return new EraImage(b, mappingS, b.Width / 64);
            } catch (Exception e) {
                Debugger.Log(e);
                return null;
            }
        }

    }
}
