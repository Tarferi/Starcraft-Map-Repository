using System.Collections.Generic;
using System.IO;

namespace GUILib.starcraft {

    public class Writable {

        private List<byte> data = null;

        private Stream os = null;

        public Writable(Stream outputStream) {
            this.os = outputStream;
        }

        public Writable(List<byte> data) {
            this.data = data;
        }

        public void WriteByte(byte b) {
            if (data != null) {
                data.Add(b);
            } else if (os != null) {
                os.WriteByte(b);
            }
        }


    }

    public class WriteBuffer {

        private Writable wr = null;

        public WriteBuffer(Stream outputStream) {
            this.wr = new Writable(outputStream);
        }

        public WriteBuffer(List<byte> data) {
            this.wr = new Writable(data);
        }
        
        public WriteBuffer(Writable wr) {
            this.wr = wr;
        }

        public void WriteByte(byte b) {
            WriteByte(b, wr);
        }

        public static void WriteByte(byte b, Writable wr) {
            wr.WriteByte(b);
        }
        
        public void WriteShort(ushort s) {
            WriteShort(s, wr);
        }

        public static void WriteShort(ushort s, Writable wr) {
            WriteByte((byte)((s >> 0) & 0xff), wr);
            WriteByte((byte)((s >> 8) & 0xff), wr);
        }
        
        public void WriteInt(uint i) {
            WriteInt(i, wr);
        }
         
        public void WriteInt(int i) {
            WriteInt((uint)i, wr);
        }

        public static void WriteInt(uint i, Writable wr) {
            WriteShort((ushort)((i >> 0) & 0xffff), wr);
            WriteShort((ushort)((i >> 16) & 0xffff), wr);
        }
        
        public void WriteData(byte[] data) {
            WriteData(data, wr);
        }

        public static void WriteData(byte[] data, Writable wr) {
            for(int i = 0; i < data.Length; i++) {
                WriteByte(data[i], wr);
            }
        }

        public void WriteData(ByteArray data) {
            WriteData(data, wr);
        }

        public static void WriteData(ByteArray data, Writable wr) {
            for(int i = 0; i < data.Length; i++) {
                WriteByte(data.At(i), wr);
            }
        }
    }
}
