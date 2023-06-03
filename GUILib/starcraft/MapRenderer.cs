using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Media;
using static GUILib.starcraft.Section_ERA;
using GUILib.libs._7zip;
using GUILib.data;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;

namespace GUILib.starcraft {

    class StarcraftResources {

        private static Dictionary<string, ByteArray> resources = new Dictionary<string, ByteArray>();

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static ByteArray GetResource(string key, bool compressed) {
            lock (resources);
            ByteArray b = null;
            if (resources.TryGetValue(key, out b)) {
                return b;
            } else {
                Assembly ass = Assembly.GetAssembly(Model.Create().GetType());
                string n = ass.GetName().Name;
                var tilesetStream = ass.GetManifestResourceStream(n + ".resources." + key);

                byte[] tilesetBuffer = new byte[tilesetStream.Length];
                tilesetStream.Read(tilesetBuffer, 0, tilesetBuffer.Length);

                if (compressed) {
                    // Decompress
                    tilesetBuffer = LZMA.Decode(tilesetBuffer);
                    if (tilesetBuffer == null) {
                        return null;
                    }
                }
                                
                unsafe {
                    IntPtr pointer = Marshal.AllocHGlobal(tilesetBuffer.Length);
                    Marshal.Copy(tilesetBuffer, 0, pointer, tilesetBuffer.Length);
                    byte* bf = (byte*)pointer;
                    ByteArray ar = new ByteArray(bf, 0, (uint)tilesetBuffer.Length);
                    resources[key] = ar;
                    return ar;
                }
            }
        }

        private static Dictionary<EraType, ByteArray> tilesets = new Dictionary<EraType, ByteArray>();

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static ByteArray GetTileSetBuffer(EraType type) {
            lock (tilesets) ;
            ByteArray b = null;
            if (tilesets.TryGetValue(type, out b)) {
                return b;
            } else {
                ByteArray tilesetArray = GetResource("tileset.bin", false); ;

                int eraI = (int)type;
                ReadBuffer rb = new ReadBuffer(tilesetArray);
                bool error = false;

                // Skip previous sections
                for (int i = 0; i < eraI; i++) {
                    uint tileSizeC = rb.ReadInt(ref error);
                    if (error) { return null; }
                    rb.Skip(tileSizeC, ref error);
                    if (error) { return null; }
                }
                
                // Extract our section
                uint tileSize = rb.ReadInt(ref error);
                if (error) { return null; }
                ByteArray data = rb.ReadData(tileSize, ref error);
                if (error) { return null; }

                byte[] tmp = new byte[tileSize];
                for (int i = 0; i < tileSize; i++) {
                    tmp[i] = data.At(i);
                }

                // Decompress
                tmp = LZMA.Decode(tmp);
                if (tmp == null) {
                    return null;
                }
                unsafe {
                    IntPtr pointer = Marshal.AllocHGlobal(tmp.Length);
                    Marshal.Copy(tmp, 0, pointer, tmp.Length);
                    byte* bf = (byte*)pointer;
                    ByteArray ar = new ByteArray(bf, 0, (uint)tmp.Length);
                    tmp = null;
                    tilesets[type] = ar;
                    return ar;
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void FlushCache() {
            {
                lock (resources) ;
                unsafe {
                    foreach (string key in resources.Keys) {
                        ByteArray b = resources[key];
                        byte* ptr = b.GetUnsafe();
                        IntPtr p = (IntPtr)ptr;
                        Marshal.FreeHGlobal(p);
                    }
                    resources.Clear();
                }
            }
            {
                lock (tilesets) ;
                unsafe {
                    foreach (EraType key in tilesets.Keys) {
                        ByteArray b = tilesets[key];
                        byte* ptr = b.GetUnsafe();
                        IntPtr p = (IntPtr)ptr;
                        Marshal.FreeHGlobal(p);
                    }
                    tilesets.Clear();
                }
            }
        }
    }

    class TilesetData {

        ObjectBuffer tileDataBuffer;

        private Object<uint> data;
        private uint dataLength;
        private Object<ushort> map;
        private uint mapLength;

        private bool valid = false;
     
        public void Reset() {
            if (tileDataBuffer != null) {
                tileDataBuffer.Reset();
            }
        }

        public TilesetData(ByteArray ts) {
            ReadBuffer rb = new ReadBuffer(ts);

            bool error = false;

            this.mapLength = rb.ReadShort(ref error);
            if (error) { return; }
            ByteArray mappingC = rb.ReadData(this.mapLength, ref error);
            if (error) { return; }
            ushort palleteLength = rb.ReadShort(ref error);
            if (error) { return; }
            ByteArray rawPallete = rb.ReadData(palleteLength * 3, ref error);
            if (error) { return; }


            // Get total tiles
            this.dataLength = 0;
            for (uint i = 0; i < this.mapLength; i++) {
                if (mappingC.At(i) == 1) {
                    this.dataLength++;
                }
            }

            tileDataBuffer = new ObjectBuffer();

            // Decode data
            const uint tileSize = 32;
            
            this.data = tileDataBuffer.Reserve<uint>(this.dataLength * tileSize * tileSize, ObjectBuffer.UINT);
            this.map = tileDataBuffer.Reserve<ushort>(this.mapLength, ObjectBuffer.USHORT);
            tileDataBuffer.Allocate();


            // Read all pixels
            ByteArray pixelData = rb.ReadData(this.dataLength * tileSize * tileSize, ref error);
            if (error) { return; }


            // Decode colors
            for (uint i = 0; i < this.dataLength * tileSize * tileSize; i++) {
                uint pixel = pixelData.At(i);
                if (pixel >= palleteLength) {
                    return;
                }

                uint basec = pixel * 3;
                uint r = rawPallete.At(basec + 2);
                uint g = rawPallete.At(basec + 1);
                uint b = rawPallete.At(basec + 0);
                uint color = (r << 16) | (g << 8) | (b << 0);
                this.data.Set(i, color);
            }
            //pallete = null;
            pixelData = null;


            // Fix mapping
            ushort totalPresent = 0;
            for (uint i = 0; i < this.mapLength; i++) {
                if (mappingC.At(i) == 0) {
                    this.map.Set(i, 0xffff);
                } else {
                    this.map.Set(i, totalPresent);
                    totalPresent++;
                }
            }

            
            valid = true;
        }

        public static TilesetData GetForEra(EraType type) {
            ByteArray data = StarcraftResources.GetTileSetBuffer(type);
            if (data == null) {
                return null;
            }
            TilesetData td = new TilesetData(data);
            if (td.valid) {
                return td;
            }
            return null;
        }

        public bool DrawTileSet(uint MapWidth, uint MapHeight, ByteArray mtxm,  Action<uint, uint, uint> pixelSetter) {
            const uint tileSize = 32;

            for (uint tileIndex = 0; tileIndex < MapWidth * MapHeight; tileIndex++) {
                uint t1 = (mtxm.At((tileIndex * 2)));
                uint t2 = mtxm.At((tileIndex * 2) + 1);
                t2 <<= 8;
                uint tile = t1 + t2;
                if (tile >= this.mapLength) { // No such tile
                    if (MapRenderer.IgnoreInvalidTiles) {
                        continue;
                    } else {
                        return false;
                    }
                }

                uint remapIndex = this.map.Get(tile);
                if (remapIndex >= this.dataLength) { // Image for tile doesn't exists (or is null)
                    continue;
                }


                uint rgbTileIdx = remapIndex * tileSize * tileSize;


                uint resultBaseX = (tileIndex % MapWidth) * tileSize;
                uint resultBaseY = (tileIndex / MapHeight) * tileSize;

                // Copy RGB values
                for (uint pixIndex = 0; pixIndex < tileSize * tileSize; pixIndex++) {
                    uint color = this.data.Get(rgbTileIdx + pixIndex);
                    uint resultPX = pixIndex % tileSize;
                    uint resultPY = pixIndex / tileSize;

                    uint resultOffsetX = resultBaseX + resultPX;
                    uint resultOffsetY = resultBaseY + resultPY;

                    pixelSetter(resultOffsetX, resultOffsetY, (color << 8) | 0xff);
                }
            }
            return true;
        }

    }

    /*

    class SpriteData {

        public struct SpriteImage {
            public ushort id;
            public ushort width;
            public ushort height;
            public uint[] data;
            public uint dataLength;
        }

        public struct SpriteImageList {
            public SpriteImage[] data;
            public uint totalItems;

            public ushort[] unitMapping;
            public uint totalUnits;

            public ushort[] spriteMapping;
            public uint totalSprites;
        }

        public SpriteImageList images = new SpriteImageList();

        private bool valid = false;

        private SpriteData(ByteArray ts) {



            ReadBuffer rb = new ReadBuffer(ts);

            bool error = false;


            // Read total images
            uint totalImages = rb.ReadShort(ref error);
            if (error) { return; }
            images.totalItems = totalImages;


            // Read pallete
            uint palleteDataLength = rb.ReadShort(ref error);
            if (error) { return; }
            ByteArray rawPallete = rb.ReadData(palleteDataLength * 4, ref error);
            if (error) { return; }
            uint[] pallete = new uint[palleteDataLength];


            // Decode pallete
            for (uint i = 0; i < palleteDataLength; i++) {
                uint baser = i * 4;
                uint r = rawPallete.At(baser + 0);
                uint g = rawPallete.At(baser + 1);
                uint b = rawPallete.At(baser + 2);
                uint a = rawPallete.At(baser + 3);
                uint color = (r << 24) | (g << 16) | (b << 8) | (a << 0);
                pallete[i] = color;
            }
            rawPallete = null;

            
            // Read sprites
            uint totalSprites = rb.ReadShort(ref error);
            if (error) { return; }
            ushort[] resSprites = new ushort[totalSprites];
            for (uint i = 0; i < totalSprites; i++) {
                ushort spriteImageID = rb.ReadShort(ref error);
                if (error) { return; }
                resSprites[i] = spriteImageID;
            }

            
            // Units
            uint totalUnits = rb.ReadShort(ref error);
            if (error) { return; }
            ushort[] resUnits = new ushort[totalUnits];
            for (uint i = 0; i < totalUnits; i++) {
                ushort unitImageID = rb.ReadShort(ref error);
                if (error) { return; }
                resUnits[i] = unitImageID;
            }


            // Allocate result image array
            SpriteImage[] resImages = new SpriteImage[totalImages];
            images.data = resImages;
            images.totalSprites = totalSprites;
            images.spriteMapping = resSprites;
            images.totalUnits = totalUnits;
            images.unitMapping = resUnits;

            for (ushort imageIndex = 0; imageIndex < totalImages; imageIndex++) {
                byte type = rb.Read(ref error);
                if (error) { return; }

                ushort width = rb.ReadShort(ref error);
                if (error) { return; }
                
                ushort height = rb.ReadShort(ref error);
                if (error) { return; }
                
                uint totalSize = ((uint) width) *((uint) height);
                ByteArray imageData = rb.ReadData(totalSize, ref error);
                if (error) { return; }

                uint[] nwInt = new uint[totalSize];


                for (uint i = 0; i < totalSize; i++) {
                    byte colorPalleteIndex = imageData.At(i);
                    if (colorPalleteIndex >= palleteDataLength) { // Invalid color?
                        return;
                    }
                    uint color = pallete[colorPalleteIndex];
                    nwInt[i] = color;
                }
                
                resImages[imageIndex].data = nwInt;
                resImages[imageIndex].dataLength = totalSize;
                resImages[imageIndex].id = imageIndex;
                resImages[imageIndex].width = width;
                resImages[imageIndex].height = height;

            }

            valid = true;
        }

        public static SpriteData Load() {
            ByteArray data = StarcraftResources.GetResource("sprites.bin", true);
            if (data == null) {
                return null;
            }
            SpriteData td = new SpriteData(data);
            if (td.valid) {
                return td;
            }
            return null;
        }
    }

    */

    class MapRenderer {

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        public static bool IgnoreInvalidTiles = true;
        public static bool IgnoreInvalidSprites = true;

        private static X GetOrNull<X>(Dictionary<string, List<Section>> sections, string name) {
            List<Section> s = null;
            if (sections.TryGetValue(name, out s)) {
                if (s.Count > 0) {
                    Section sec = s[0];
                    object o0 = sec;
                    return (X)o0;
                }
            }
            object o = null;
            return (X)o;
        }

        /*

        private static bool DrawImageAt(SpriteData.SpriteImageList images, ushort imageID, uint x, uint y, Action<uint, uint, uint> pixelSetter) {
            if (imageID >= images.totalItems) {
                return false;
            }
            SpriteData.SpriteImage image = images.data[imageID];
            uint[] bitmap = image.data;
            uint w = image.width;
            uint h = image.height;

            uint offX = (w / 2);
            uint offY = (h / 2);

            x -= offX;
            y -= offY;

            int pxx = 0;
            for (uint py = 0; py < h; py++) {
                for (uint px = 0; px < w; px++) {
                    uint color = bitmap[(py * w) + px];
                    byte a = (byte)(color & 0xff);
                    if (a != 0) {
                        pxx++;
                        if (pxx >= 51 && pxx <= 50) {
                            pixelSetter(x + px, y + py, color);
                        }
                    }
                }
            }
            return true;
        }

        private static bool DrawSprites(Section_THG2 THG2, Action<uint, uint, uint> pixelSetter) {
            SpriteData sData = SpriteData.Load();
            if (sData == null) {
                return false;
            }

            SpriteData.SpriteImageList images = sData.images;
            for (uint i = 0; i < THG2.Sprites.Count; i++) {
                Section_THG2.Section_THG2_STRUCTURE sprite = THG2.Sprites[(int)i];
                uint spriteID = sprite.id;
                if (spriteID >= images.totalSprites) {
                    if (IgnoreInvalidSprites) {
                        continue;
                    }
                    return false;
                }
                if (!DrawImageAt(images, images.spriteMapping[spriteID], sprite.x, sprite.y, pixelSetter)) {
                    return false;
                }
            }
            return true;
        }

        */
        
        public static ImageSource RenderMap(byte[] data) {
            try {
                GC.Collect();
                unsafe {
                    fixed (byte* dataRaw = &data[0]) {
                        ByteArray ba = new ByteArray(dataRaw, 0, (uint)data.Length);

                        Dictionary<string, List<Section>> sections = CHK.LoadSections(ba, "DIM ", "ERA ", "MTXM", "THG2");
                        Section_DIM DIM = GetOrNull<Section_DIM>(sections, "DIM ");
                        Section_ERA ERA = GetOrNull<Section_ERA>(sections, "ERA ");
                        Section_MTXM MTXM = GetOrNull<Section_MTXM>(sections, "MTXM");
                        Section_THG2 THG2 = GetOrNull<Section_THG2>(sections, "THG2");
                        if (DIM == null || ERA == null || MTXM == null || THG2 == null) {
                            return null;
                        }
                        EraType era = ERA.Era;
                        if (era == EraType.INVALID) {
                            return null;
                        }

                        uint tileSize = 32;

                        int imgWidth = (int)(tileSize * DIM.Width);
                        int imgHeight = (int)(tileSize * DIM.Height);

                        ByteArray mtxmData = null;

                        List<Section> mtxms = sections.ContainsKey("MTXM") ? sections["MTXM"] : new List<Section>();
                        foreach (Section mtxmsec in mtxms) {
                            MTXM = (Section_MTXM)mtxmsec;
                            mtxmData = MTXM.GetData();
                        }


                        Func<Bitmap, bool> process = (Bitmap img) => {
                            Rectangle lockRegion = new Rectangle(0, 0, imgWidth, imgHeight);
                            BitmapData bmData = img.LockBits(lockRegion, ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                            Action<uint, uint, uint> pixelSetter = (uint x, uint y, uint color) => {
                                if (x < imgWidth && y < imgHeight) {
                                    byte* p = (byte*)bmData.Scan0 + (y * bmData.Stride) + (x * 3);
                                    p[0] = (byte)((color >> 8) & 0xff); // B
                                    p[1] = (byte)((color >> 16) & 0xff); // G
                                    p[2] = (byte)((color >> 24) & 0xff); // R
                                }
                            };

                            TilesetData tileset = TilesetData.GetForEra(era);
                            if (tileset != null) {
                                try {
                                    if (!tileset.DrawTileSet(DIM.Width, DIM.Height, mtxmData, pixelSetter)) {
                                        return false;
                                    }
                                } finally {
                                    tileset.Reset();
                                }
                            }


                            //if (!DrawSprites(THG2, pixelSetter)) {
                            //    return null;
                            //}


                            img.UnlockBits(bmData);
                            return true;
                        };
                        
                        using (Bitmap img = new Bitmap(imgWidth, imgHeight)) {
                            if (process(img)) {
                                IntPtr hBmp = img.GetHbitmap();
                                ImageSource imgs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBmp, IntPtr.Zero, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                                imgs.Freeze();
                                DeleteObject(hBmp); //Clean up original bitmap
                                if (imgs == null) {
                                    return null;
                                }
                                return imgs;
                            }
                            return null;
                        }
                    }
                }
            } finally {
                StarcraftResources.FlushCache();
                GC.Collect();
            }
        }
    }
}
