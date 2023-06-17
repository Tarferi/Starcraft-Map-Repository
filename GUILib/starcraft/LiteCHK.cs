using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GUILib.starcraft {

    public enum EraType {
        Badlands = 0,
        SpacePlatform = 1,
        Installation = 2,
        Ashworld = 3,
        Jungle = 4,
        Desert = 5,
        Arctic = 6,
        Twilight = 7,
        INVALID = 8
    };

    public class LiteSection {

        private readonly string name;
        private readonly ByteArray data;

        private bool valid = true;

        public LiteSection(String name, ByteArray data) {
            this.name = name;
            this.data = data;
        }

        protected ushort GetShortAt(int idx) {
            uint position = (uint)idx;
            bool error = false;
            ushort value = ReadBuffer.ReadShort(data, ref position, ref error);
            return error ? (ushort)0 : value;
        }

        protected uint GetIntAt(int idx) {
            uint position = (uint)idx;
            bool error = false;
            uint value = ReadBuffer.ReadInt(data, ref position, ref error);
            return error ? (uint)0 : value;
        }

        protected ByteArray ReadDataAt(uint length, int idx) {
            uint position = (uint)idx;
            bool error = false;
            ByteArray value = ReadBuffer.ReadData(data, ref position, length, ref error);
            return error ? null : value;
        }

        protected byte GetByteAt(int idx) {
            uint position = (uint)idx;
            bool error = false;
            byte value = ReadBuffer.Read(data, ref position, ref error);
            return error ? (byte)0 : value;
        }

        public uint GetSize() {
            return (uint)data.Length;
        }

        public ByteArray GetData() {
            return data;
        }

        protected void AssertSize(int size) {
            if (data.Length != size) {
                valid = false;
            }
        }

        public bool IsValid() {
            return valid;
        }

    }

    public class LiteSection_DIM : LiteSection {

        public ushort Width { get => GetShortAt(0); }

        public ushort Height { get => GetShortAt(2); }

        public LiteSection_DIM(ByteArray data) : base("DIM ", data) {

        }
    }

    public class LiteSection_ERA : LiteSection {

        public ushort RawEra { get => (ushort)(GetShortAt(0) & 0b111); }

        public EraType Era {
            get {
                ushort e = RawEra;
                if (e < (ushort)EraType.INVALID) {
                    return (EraType)e;
                }
                return EraType.INVALID;
            }
        }

        public LiteSection_ERA(ByteArray data) : base("ERA ", data) {
            AssertSize(2);
        }

    }

    public class LiteSection_MTXM : LiteSection {

        public LiteSection_MTXM(ByteArray data) : base("MTXM ", data) {

        }

    }

    public class LiteSection_THG2 : LiteSection {

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Section_THG2_STRUCTURE {
            public ushort id;
            public ushort x;
            public ushort y;
            public byte owner;
            public byte unused;
            public ushort flags;
        };

        public List<Section_THG2_STRUCTURE> Sprites = new List<Section_THG2_STRUCTURE>();

        public LiteSection_THG2(ByteArray data) : base("THG2", data) {
            // TODO: add eror checking
            uint szSection_THG2_STRUCTURE = (uint)Marshal.SizeOf(typeof(Section_THG2_STRUCTURE));
            uint totalSprites = GetSize() / szSection_THG2_STRUCTURE;
            for (uint i = 0; i < totalSprites; i++) {
                int pos = (int)(i * szSection_THG2_STRUCTURE);
                Section_THG2_STRUCTURE sprite = new Section_THG2_STRUCTURE();
                sprite.id = GetShortAt(pos + 0);
                sprite.x = GetShortAt(pos + 2);
                sprite.y = GetShortAt(pos + 4);
                sprite.owner = GetByteAt(pos + 6);
                sprite.unused = GetByteAt(pos + 7);
                sprite.flags = GetShortAt(pos + 8);
                Sprites.Add(sprite);
            }

            AssertSize((int)(totalSprites * szSection_THG2_STRUCTURE));
        }

    }

    public class LiteCHK {

        private bool valid = false;

        Dictionary<string, List<LiteSection>> sections = new Dictionary<string, List<LiteSection>>();

        private LiteCHK(ByteArray data) {

            ReadBuffer rb = new ReadBuffer(data);
            while (!rb.IsDone()) {
                bool error = false;
                string name = rb.ReadFixedLengthString(4, ref error);
                if (error) {
                    return;
                }
                uint size = rb.ReadInt(ref error);
                if (error) {
                    return;
                }
                uint restLength = rb.GetDataSize() - rb.GetPosition();
                if (size > restLength) {
                    return;
                }
                LiteSection section = null;
                uint end = rb.GetPosition();

                ByteArray sectionData = rb.ReadData(size, ref error);
                if (error) {
                    return;
                }
                if (name == "DIM ") {
                    section = new LiteSection_DIM(sectionData);
                } else if (name == "ERA ") {
                    section = new LiteSection_ERA(sectionData);
                } else if (name == "MTXM") {
                    section = new LiteSection_MTXM(sectionData);
                } else if (name == "THG2") {
                    section = new LiteSection_THG2(sectionData);
                } else {
                    section = new LiteSection(name, sectionData);
                }
                if (section.IsValid()) {
                    if (!sections.ContainsKey(name)) {
                        sections[name] = new List<LiteSection>();
                    }
                    sections[name].Add(section);
                }
            }

            valid = true;
        }

        public static LiteCHK Load(ByteArray data) {
            LiteCHK chk = new LiteCHK(data);
            if (chk.valid) {
                return chk;
            }
            return null;
        }

        public static Dictionary<string, List<LiteSection>> LoadSections(ByteArray data, params string[] sections) {
            LiteCHK chk = new LiteCHK(data);
            Dictionary<string, List<LiteSection>> res = new Dictionary<string, List<LiteSection>>();
            foreach (string section in sections) {
                if (chk.sections.ContainsKey(section)) {
                    res[section] = chk.sections[section];
                }
            }
            return res;
        }

    }
}
