using System;
using System.Collections.Generic;
using System.Windows.Media;
using static GUILib.starcraft.Section_ERA;
using GUILib.data;
using System.Drawing;
using System.IO;
using GUILib.ui.utils;
using GUILib.ui.AssetPackerWnd;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Drawing.Imaging;

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
                    EraImage img = ImageEncoder.ReadImageForEra(fs, era);
                    if (img != null) {
                        return new Tileset(img);
                    }
                }
            } catch(Exception e) {
                Debugger.Log(e);
            }

            return null;
        }

        public void RnderTile(ushort tileID, Bitmap bm, int x, int y) {
            img.CopyTile(tileID, bm, x, y);
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

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);
        
        private static ImageSource ImageSourceFromBitmap(Bitmap bmp) {
            var handle = bmp.GetHbitmap();
            try {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            } finally { DeleteObject(handle); }
        }

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

                        List<Section> mtxms = sections.ContainsKey("MTXM") ? sections["MTXM"] : new List<Section>();

                        byte[] mtxmBuffer = new byte[DIM.Width * DIM.Height * 2];
                        foreach(Section sectx in mtxms) {
                            int sectOffset = (int)sectx.GetData().Offset;
                            int sectSize = (int)sectx.GetData().Length;
                            int toCopy = mtxmBuffer.Length < sectSize ? mtxmBuffer.Length : sectSize;
                            Array.Copy(data, sectOffset, mtxmBuffer, 0, toCopy);
                        }

                        Debugger.LogFun("Preparing tileset...");
                        Tileset tileset = Tileset.Get(era, "Carbot.bin");
                        Debugger.LogFun("Rendering map...");
                        if (tileset != null) {
                            Bitmap img = new Bitmap(DIM.Width * tileset.TileSize, DIM.Height * tileset.TileSize, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                            Bitmap clear = img;
                            try {
                                for (int y = 0, i = 0; y < DIM.Height; y++) {
                                    for (int x = 0; x < DIM.Width; x++, i += 2) {
                                        ushort t1 = i < mtxmBuffer.Length ? mtxmBuffer[i] : (byte)0;
                                        ushort t2 = i + 1 < mtxmBuffer.Length ? mtxmBuffer[i + 1] : (byte)0;
                                        ushort tileID = (ushort)((t1 << 0) + (t2 << 8));
                                        tileset.RnderTile(tileID, img, x, y);
                                    }
                                }
                                clear = null;
                            } finally {
                                if (clear != null) {
                                    clear.Dispose();
                                    clear = null;
                                }
                            }
                            img.Save(Model.Create().WorkingDir + "\\tmp.png", ImageFormat.Png);
                            return ImageSourceFromBitmap(img);
                        }
                        return null;
                    }
                }
            } finally {
                GC.Collect();
            }
        }
    }
}
