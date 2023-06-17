using GUILib.ui.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GUILib.starcraft {
    class CHKFixer {

        private CHKFixer() {

        }

        private static byte[] TerrainOnly(Dictionary<string, List<LiteSection>> chk, CHK minCHK) {

            // need to update
            // - ISOM (can be empty)
            // - TILE (can be MTXM)
            // - DIM
            // - ERA
            // - MASK
            // - MTXM


            // DIM
            LiteSection_DIM dim = (LiteSection_DIM) chk["DIM "][0];
            Section_DIM minDim = (Section_DIM)minCHK.LoadSection("DIM ")[0];
            minDim.Width = dim.Width;
            minDim.Height = dim.Height;

            // ISOM
            Section_ISOM minIsom = (Section_ISOM)minCHK.LoadSection("ISOM")[0];
            minIsom.data = new byte[2 * ((dim.Width / 2 + 1) * (dim.Height + 1) * 4)];

            // ERA
            LiteSection_ERA era = (LiteSection_ERA)chk["ERA "][0];
            Section_ERA minEra = (Section_ERA)minCHK.LoadSection("ERA ")[0];
            minEra.Era = era.Era;

            // MASK
            byte[] newMask = new byte[dim.Width * dim.Height];
            for(int i = 0; i < newMask.Length; i++) {
                newMask[i] = 0xff;
            }
            Section_MASK mask = (Section_MASK)minCHK.LoadSection("MASK")[0];
            mask.data = newMask;

            // MTXM/TILE
            byte[] newMTXM = new byte[2 * dim.Width * dim.Height];
            foreach (LiteSection sectx in chk["MTXM"]) {
                int sectOffset = (int)sectx.GetData().Offset;
                int sectSize = (int)sectx.GetData().Length;
                int toCopy = newMTXM.Length < sectSize ? newMTXM.Length : sectSize;
                for(int xc = 0; xc < toCopy; xc++) {
                    newMTXM[xc] = sectx.GetData().At(xc);
                }
            }
            Section_MTXM mtxm = (Section_MTXM)minCHK.LoadSection("MTXM")[0];
            mtxm.data = newMTXM;
            Section_TILE minTile = (Section_TILE)minCHK.LoadSection("TILE")[0];
            minTile.data = newMTXM;
                        
            // Save result
            List<byte> result = new List<byte>();
            WriteBuffer wb = new WriteBuffer(result);
            minCHK.Write(wb);
            return result.ToArray();
        }

        public static byte[] TerrainOnly(byte[] chkRaw) {
            Dictionary<string, List<LiteSection>> tmp = null;
            unsafe {
                fixed (byte* chkRawPtr = chkRaw) {
                    ByteArray ba = new ByteArray(chkRawPtr, 0, (uint)chkRaw.Length);
                    tmp = LiteCHK.LoadSections(ba, "DIM ", "ERA ", "MTXM");
                }
            }
            CHKFixer fixer = new CHKFixer();
            if (tmp != null && tmp.ContainsKey("DIM ") && tmp.ContainsKey("ERA ") && tmp.ContainsKey("MTXM")) {
                string resourceName = fixer.GetType().Assembly.GetManifestResourceNames().Single(str => str.EndsWith("min.chk"));
                Stream s = fixer.GetType().Assembly.GetManifestResourceStream(resourceName);
                byte[] bs = new byte[s.Length];
                s.Read(bs, 0, bs.Length);
                CHK chkMin = CHK.Load(bs);
                if (chkMin != null) {
                    return TerrainOnly(tmp, chkMin);
                } else {
                    ErrorMessage.Show("Failed to parse template CHK file");
                }
            } else {
                ErrorMessage.Show("Failed to parse CHK file");
            }
            return null;
        }
    }
}
