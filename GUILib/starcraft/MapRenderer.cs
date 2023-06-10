using System;
using System.Collections.Generic;
using System.Windows.Media;
using static GUILib.starcraft.Section_ERA;
using GUILib.data;
using System.Drawing;
using System.IO;
using GUILib.ui.utils;
using GUILib.ui.AssetPackerWnd;
using System.Windows;
using System.Windows.Media.Imaging;
using PixelFormat = System.Windows.Media.PixelFormat;
using System.Runtime.InteropServices;

namespace GUILib.starcraft {

    class Tileset {

        private readonly EraImage img;

        public int TileSize { get => img.TileSize; }

        private Tileset(EraImage img) {
            this.img = img;
        }

        public static Tileset Get(EraType era, string resourceInput) {
            Model model = Model.Create();
            string src = model.WorkingDir + "\\resources\\" + resourceInput;
            if (!File.Exists(src)) {
                ErrorMessage.Show("Tileset file " + resourceInput + " does not exist.\nDownload it in asset manager tab.");
                return null;
            }
            try {
                using (FileStream fs = File.OpenRead(src)) {
                    EraImage img = ImageEncoder.ReadImageForEra(fs, era, true);
                    if (img != null) {
                        return new Tileset(img);
                    }
                }
            } catch (Exception e) {
                Debugger.Log(e);
            }

            return null;
        }

        public void RnderTile(ushort tileID, Bitmap bm, int x, int y) {
            img.CopyTile(tileID, bm, x, y);
        }

        public void RnderTile(ushort tileID, Action<int, int, int> bm, int x, int y) {
            img.CopyTile(tileID, bm, x, y);
        }
    }

    class Sprites {

        private Sprites() {

        }

        public void RenderSprite(ushort spriteID,  Action<int, int, int> bm, int x, int y) {

        }

        public static Sprites Get(string spritesName) {
            throw new NotImplementedException();
        }
    }

    class MapRenderer {

        public static bool IgnoreInvalidTiles = true;
        public static bool IgnoreInvalidSprites = true;

        private static X GetOrNull<X>(Dictionary<string, List<Section>> sections, string name) {
            List<Section> s = null;
            if (sections.TryGetValue(name, out s)) {
                if (s.Count > 0) {
                    Section sec = s[s.Count - 1];
                    object o0 = sec;
                    return (X)o0;
                }
            }
            object o = null;
            return (X)o;
        }

        private static BitmapSource RenderSmall(Tileset tileset, Section_DIM DIM, byte[] mtxmBuffer, Progresser progresser, int firstRowOfTilesToRender, int lastRowOfTilesToRender) {

            int tileRows = (lastRowOfTilesToRender - firstRowOfTilesToRender) + 1;

            ulong imgWidth = (ulong)(tileset.TileSize * DIM.Width);
            ulong imgHeight = (ulong)(tileset.TileSize * tileRows);

            PixelFormat pf = PixelFormats.Rgb24;
            ulong rawStride = (ulong)((imgWidth * (ulong)pf.BitsPerPixel + 7) / 8);

            ulong bytesPerPixel = (uint)(pf.BitsPerPixel / 8);

            ulong ulRawStride = (ulong)(rawStride);
            ulong ulImgHeight = (ulong)(imgHeight);
            ulong ulBytesPerPixel = (ulong)(bytesPerPixel);
            ulong pixelsLength = ulRawStride * ulImgHeight;
            unsafe {
                IntPtr sz = (IntPtr)pixelsLength;
                IntPtr pixelsPtr = Marshal.AllocHGlobal(sz);
                byte[] pixels = new byte[pixelsLength];
                //byte* pixels = (byte*)pixelsPtr;

                ulong baseY = (ulong)(firstRowOfTilesToRender * tileset.TileSize);

                Action<int, int, int> pixelSetter = (int x, int y, int color) => {
                    ulong ux = (ulong)x;
                    ulong uy = (ulong)y;
                    uy -= baseY;
                    ulong resultOffset = (uy * ulRawStride) + (ux * ulBytesPerPixel);
                    pixels[resultOffset + 0] = (byte)((color >> 16) & 0xff); // R
                    pixels[resultOffset + 1] = (byte)((color >> 8) & 0xff); // G
                    pixels[resultOffset + 2] = (byte)((color >> 0) & 0xff); // B
                };

                int firstI = DIM.Width * firstRowOfTilesToRender * 2;
                for (int y = firstRowOfTilesToRender, i = firstI; y <= lastRowOfTilesToRender; y++) {
                    for (int x = 0; x < DIM.Width; x++, i += 2) {
                        ushort t1 = i < mtxmBuffer.Length ? mtxmBuffer[i] : (byte)0;
                        ushort t2 = i + 1 < mtxmBuffer.Length ? mtxmBuffer[i + 1] : (byte)0;
                        ushort tileID = (ushort)((t1 << 0) + (t2 << 8));
                        tileset.RnderTile(tileID, pixelSetter, x, y);
                    }
                    progresser.Tick(y);
                }

                int iImgWidth = (int)imgWidth;
                int iImgHeight = (int)imgHeight;
                //int iSz = (int)sz;
                int iRawStride = (int)rawStride;
                BitmapSource img = BitmapSource.Create(iImgWidth, iImgHeight, 96, 96, pf, null, pixels, iRawStride);
                if (img == null) {
                    ErrorMessage.Show("Failed to create new bitmap image.\nMost likely due to not enough memory.");
                    return null;
                }

                return img;
            }
        }

        private static BitmapSource RenderWritableBitmap(Tileset tileset, Section_DIM DIM, byte[] mtxmBuffer, Progresser progresser, int firstRowOfTilesToRender, int lastRowOfTilesToRender) {
        
            if (tileset != null) {
                int tileRows = (lastRowOfTilesToRender - firstRowOfTilesToRender) + 1;

                int width = DIM.Width * tileset.TileSize;
                int height = tileRows * tileset.TileSize;

                WriteableBitmap bm = null;
                long bmBackBuffer = 0;
                int BackBufferStride = 0;
                try {
                    bm = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
                    bmBackBuffer = (long)bm.BackBuffer;
                    BackBufferStride = bm.BackBufferStride;
                } catch (Exception) {
                    ErrorMessage.Show("Not enough memory to create preview of this map");
                }
                if (bm == null) {
                    return null;
                }
                try {
                    bm.Lock();
                    int baseY = firstRowOfTilesToRender * tileset.TileSize;

                    Action runRaw = () => {
                        // Operator on raw pixel array
                        bool shownErrorMem = false;
                        Action<int, int, int> setColor = (int x, int y, int color) => {
                            unsafe {
                                y -= baseY;
                                long backBuffer = bmBackBuffer;
                                backBuffer += y * BackBufferStride;
                                backBuffer += x * 3;
                                try {
                                    *((byte*)(backBuffer + 0)) = (byte)((color >> 16) & 0xff);
                                    *((byte*)(backBuffer + 1)) = (byte)((color >> 8) & 0xff);
                                    *((byte*)(backBuffer + 2)) = (byte)((color >> 0) & 0xff);
                                } catch (Exception ex) {
                                    if (!shownErrorMem) {
                                        shownErrorMem = true;
                                        Debugger.Log(ex);
                                        ErrorMessage.Show("Memory error. This may indicate that result bitmap is not allocated.\nSome tiles will not be rendered.");
                                    }
                                }
                            };

                        };
                        int firstI = DIM.Width * firstRowOfTilesToRender * 2;
                        for (int y = firstRowOfTilesToRender, i = firstI; y <= lastRowOfTilesToRender; y++) {
                            for (int x = 0; x < DIM.Width && !shownErrorMem; x++, i += 2) {
                                ushort t1 = i < mtxmBuffer.Length ? mtxmBuffer[i] : (byte)0;
                                ushort t2 = i + 1 < mtxmBuffer.Length ? mtxmBuffer[i + 1] : (byte)0;
                                ushort tileID = (ushort)((t1 << 0) + (t2 << 8));
                                tileset.RnderTile(tileID, setColor, x, y);
                            }
                            progresser.Tick(y);
                        }
                        bm.AddDirtyRect(new Int32Rect(0, 0, width, height));
                    };

                    Action runPixelated = () => {
                        int bufferWidth = width;
                        int bufferHeight = tileset.TileSize;
                        int bytesPerPixel = 3;
                        int bufferStride = 4 * ((bufferWidth * bytesPerPixel + 3) / 4);

                        byte[] buffer = new byte[bufferStride * bufferHeight];

                        Action<int, int, int> pixelLineSetter = (int x, int y, int color) => {
                            y %= bufferHeight;
                            int idx = (y * bufferStride) + (x * bytesPerPixel);
                            buffer[idx + 0] = (byte)((color >> 16) & 0xff);
                            buffer[idx + 1] = (byte)((color >> 8) & 0xff);
                            buffer[idx + 2] = (byte)((color >> 0) & 0xff);
                        };

                        int firstI = DIM.Width * firstRowOfTilesToRender * 2;
                        for (int y = firstRowOfTilesToRender, i = firstI; y <= lastRowOfTilesToRender; y++) {
                            for (int x = 0; x < DIM.Width; x++, i += 2) {
                                ushort t1 = i < mtxmBuffer.Length ? mtxmBuffer[i] : (byte)0;
                                ushort t2 = i + 1 < mtxmBuffer.Length ? mtxmBuffer[i + 1] : (byte)0;
                                ushort tileID = (ushort)((t1 << 0) + (t2 << 8));
                                tileset.RnderTile(tileID, pixelLineSetter, x, y);
                            }
                            Int32Rect rect = new Int32Rect(0, (y - baseY) * tileset.TileSize, bufferWidth, bufferHeight);
                            int rectStride = bufferWidth * 4;
                            try {
                                bm.WritePixels(rect, buffer, bufferStride, 0);
                                bm.AddDirtyRect(rect);
                            } catch (Exception e) {
                                Debugger.Log(e);
                                ErrorMessage.Show("Failed to render map tile.\nMost likely due to not enough memory.\nSome tiles will not be rendered");
                                break;
                            }
                            progresser.Tick(y);
                        }
                    };

                    runRaw();

                } finally {
                    bm.Unlock();
                }
                return bm;
            }
            return null;
        }

        private static int DecideImageSizes(Tileset tileset, Section_DIM DIM) {
            List<int> lst = new List<int>();
            ulong height = (ulong)DIM.Height * (ulong)tileset.TileSize;
            ulong width = (ulong)DIM.Width * (ulong)tileset.TileSize;
            ulong rawStride = ((width * 32 + 7) / 8);

            Func<int, ulong> pixelCountByTileRows = (int tileRows) => {
                return rawStride * (ulong)tileRows * (ulong)tileset.TileSize;
            };

#if WIN64
            ulong maxLimit = int.MaxValue;
#else
            ulong maxLimit = 1024 * 1024 * 100; // 100 mb
#endif
            for(int sz = DIM.Height; sz > 0; sz--) {
                if (pixelCountByTileRows(sz) < maxLimit) {
                    return sz;
                }
            }

            return 1;
        }

        public static ImageSource[] RenderMap(byte[] data, string tilesetName, string spritesName) {
            try {
                GC.Collect();
                Debugger.LogFun("Rendering map...");
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

                        List<Section> mtxms = sections.ContainsKey("MTXM") ? sections["MTXM"] : new List<Section>();

                        byte[] mtxmBuffer = new byte[DIM.Width * DIM.Height * 2];
                        foreach (Section sectx in mtxms) {
                            int sectOffset = (int)sectx.GetData().Offset;
                            int sectSize = (int)sectx.GetData().Length;
                            int toCopy = mtxmBuffer.Length < sectSize ? mtxmBuffer.Length : sectSize;
                            Array.Copy(data, sectOffset, mtxmBuffer, 0, toCopy);
                        }

                        Debugger.LogFun("Preparing tileset...");
                        Tileset tileset = Tileset.Get(era, tilesetName);
                        Sprites sprites = Sprites.Get(spritesName);

                        if (tileset == null) {
                            ErrorMessage.Show("Failed to decode tileset.\nTry deleting and downloading it again.");
                            return null;
                        }
                        Progresser progresser = Progresser.Percentage(DIM.Height, (string val) => {
                            Debugger.LogFun("Rendering map (" + val + ")");
                        });

                        Func<BitmapSource, string, BitmapSource> saveBM = (BitmapSource bm, string name) => {
                            string resultFile = Model.Create().WorkingDir + "\\" + name;
                            using (FileStream stream5 = new FileStream(resultFile, FileMode.Create)) {
                                BitmapEncoder encoder5 = new PngBitmapEncoder();

                                encoder5.Frames.Add(BitmapFrame.Create(bm));
                                try {
                                    encoder5.Save(stream5);
                                } catch (Exception e) {
                                    Debugger.Log(e);
                                    ErrorMessage.Show("Failed to save result image");
                                    return null;
                                }
                            }
                            BitmapSource bs = null;
                            AsyncManager.OnUIThread(() => {
                                bs = new BitmapImage(new Uri(resultFile));
                            }, ExecutionOption.Blocking);
                            return bs;
                        };

                        int fragmentSize = DecideImageSizes(tileset, DIM);
                        int framentsCount = DIM.Height / fragmentSize;
                        if(DIM.Height % fragmentSize != 0) {
                            framentsCount++;
                        }

#if WIN64
                        bool convertRightAway = false;
#else
                        bool convertRightAway = true;
#endif

                        BitmapSource[] sources = new BitmapSource[framentsCount];
                        ImageSource[] imgs = new ImageSource[framentsCount];
                        for(int i = 0, pos = 0; i < imgs.Length; i++) {
                            GC.Collect();
                            int firstRendered = pos;
                            int lastRendered = firstRendered + fragmentSize - 1;
                            if (lastRendered > DIM.Height - 1) {
                                lastRendered = DIM.Height - 1;
                            }
                            pos = lastRendered + 1;
                            
                            BitmapSource bm = RenderWritableBitmap(tileset, DIM, mtxmBuffer, progresser, firstRendered, lastRendered);
                            //BitmapSource bm = RenderSmall(tileset, DIM, mtxmBuffer, progresser, firstRendered, lastRendered);
                            if (bm == null) {
                                return null;
                            } else {
                                if (convertRightAway) {
                                    imgs[i] = saveBM(bm, "out_part_" + i + ".png");
                                    if (imgs[i] == null) {
                                        ErrorMessage.Show("Failed to read bitmap partition");
                                        return null;
                                    }
                                } else {
                                    sources[i] = bm;
                                }
                            }
                        }
                        GC.Collect();
                        if (!convertRightAway) {
                            Debugger.LogFun("Preparing to display map preview...");
                            for(int i = 0; i < sources.Length; i++) {
                                imgs[i] = saveBM(sources[i], "out_part_" + i + ".png");
                            }
                        }
                        Debugger.LogFun("Displaying map preview");
                        return imgs;
                    }
                }
            } finally {
                GC.Collect();
            }
        }
    
    }
}
