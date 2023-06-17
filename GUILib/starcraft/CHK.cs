using GUILib.ui.utils;
using System.Collections.Generic;
using System.Text;

namespace GUILib.starcraft {

    public class Section {

        private string name;
        private byte[] data = null;

        protected Section(string name) {
            this.name = name;
            this.data = null;
        }

        public Section(string name, byte[] data) {
            this.name = name;
            this.data = data;
        }

        public virtual bool IsValid() {
            return valid;
        }

        protected void SetInvalid() {
            valid = false;
        }

        private bool valid = true;

        protected byte ByteAt(byte[] data, int position) {
            if(position < data.Length) {
                return data[position];
            }
            SetInvalid();
            return 0;
        }

        protected ushort ShortAt(byte[] data, int position) {
            int b1 = ByteAt(data, position);
            int b2 = ByteAt(data, position + 1);
            if (valid) {
                return (ushort)(b1 + (b2 << 8));
            }
            return 0;
        }

        protected uint IntAt(byte[] data, int position) {
            int b1 = ShortAt(data, position);
            int b2 = ShortAt(data, position + 2);
            if (valid) {
                return (uint)(b1 + (b2 << 16));
            }
            return 0;
        }
  
        public void Write(WriteBuffer wb) {
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            wb.WriteData(nameBytes);

            List<byte> bytes = new List<byte>();
            WriteBuffer tmp = new WriteBuffer(bytes);
            WriteContent(tmp);

            wb.WriteInt(bytes.Count);
            wb.WriteData(bytes.ToArray());
        }

        protected virtual void WriteContent(WriteBuffer wb) {
            if (data != null) {
                wb.WriteData(data);
            } else {
                ErrorMessage.Show("No data available when some data was expected to be available.");
            }
        }
    }

    public class Section_DIM : Section {

        public ushort Width;
        public ushort Height;

        public Section_DIM(byte[] data) : base("DIM ") {
            Width = ShortAt(data, 0);
            Height = ShortAt(data, 2);
        }

        public Section_DIM(ushort width, ushort height) : base("DIM ") {
            this.Width = width;
            this.Height = height;
        }
        
        protected override void WriteContent(WriteBuffer wb) {
            wb.WriteShort(Width);
            wb.WriteShort(Height);
        }
    }
    
    public class Section_ERA : Section {

        public EraType Era;

        public Section_ERA(byte[] data) : base("ERA ") {
            ushort era = (ushort)(ShortAt(data, 0) & 0b111);
            if(era >= 0 && era < (ushort)EraType.INVALID) {
                this.Era = (EraType)era;
            } else {
                this.Era = EraType.INVALID;
                SetInvalid();
            }
        }

        public Section_ERA(EraType era) : base("ERA ") {
            this.Era = era;
        }

        protected override void WriteContent(WriteBuffer wb) {
            wb.WriteShort((ushort)Era);
        }
    }
    
    public class Section_MTXM : Section {

        public byte[] data;

        public Section_MTXM(byte[] data) : base("MTXM") {
            this.data = data;
        }

        protected override void WriteContent(WriteBuffer wb) {
            wb.WriteData(data);
        }
    }
    
    public class Section_MASK: Section {

        public byte[] data;

        public Section_MASK(byte[] data) : base("MASK") {
            this.data = data;
        }

        protected override void WriteContent(WriteBuffer wb) {
            wb.WriteData(data);
        }
    }
    
    public class Section_ISOM: Section {

        public byte[] data;

        public Section_ISOM(byte[] data) : base("ISOM") {
            this.data = data;
        }

        protected override void WriteContent(WriteBuffer wb) {
            wb.WriteData(data);
        }
    }
    
    public class Section_TILE: Section {

        public byte[] data;

        public Section_TILE(byte[] data) : base("TILE") {
            this.data = data;
        }

        protected override void WriteContent(WriteBuffer wb) {
            wb.WriteData(data);
        }
    }

    public class CHK {

        private readonly Dictionary<string, List<Section>> sections;

        private CHK(Dictionary<string, List<Section>> sections) {
            this.sections = sections;
        }

        public void Write(WriteBuffer wb) {
            foreach(string key in sections.Keys) {
                foreach(Section section in sections[key]) {
                    section.Write(wb);
                }
            }
        }

        public static CHK Load(byte[] data) {
            Dictionary<string, List<Section>> sections = new Dictionary<string, List<Section>>();
            unsafe {
                fixed (byte* dataPtr = data) {
                    ByteArray dataArray = new ByteArray(dataPtr, 0, (uint)data.Length);
                    ReadBuffer rb = new ReadBuffer(dataArray);
                    while (!rb.IsDone()) {
                        bool error = false;
                        string name = rb.ReadFixedLengthString(4, ref error);
                        if (error) {
                            return null;
                        }
                        uint size = rb.ReadInt(ref error);
                        if (error) {
                            return null;
                        }
                        uint restLength = rb.GetDataSize() - rb.GetPosition();
                        if (size > restLength) {
                            return null;
                        }
                       Section section = null;
                        uint end = rb.GetPosition();

                        byte[] sectionData = rb.ReadData(size, ref error).GetData();
                        if (error) {
                            return null;
                        }
                        if (name == "DIM ") {
                            section = new Section_DIM(sectionData);
                        } else if (name == "ERA ") {
                            section = new Section_ERA(sectionData);
                        } else if (name == "MTXM") {
                            section = new Section_MTXM(sectionData);
                        } else if (name == "MASK") {
                            section = new Section_MASK(sectionData);
                        } else if (name == "ISOM") {
                            section = new Section_ISOM(sectionData);
                        } else if (name == "TILE") {
                            section = new Section_TILE(sectionData);
                        } else {
                            section = new Section(name, sectionData);
                        }
                        if (section.IsValid()) {
                            if (!sections.ContainsKey(name)) {
                                sections[name] = new List<Section>();
                            }
                            sections[name].Add(section);
                        }
                    }
                    return new CHK(sections);
                }
            }
            return null;
        }

        public List<Section> LoadSection(string name) {
            if (!sections.ContainsKey(name)) {
                sections[name] = new List<Section>();
            }
            return sections[name];
        }

    }
}
