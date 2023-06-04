using GUILib.data;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GUILib.libs._7zip {

    class LZMA {

        private class OffsetMemoryStream : Stream {

            private MemoryStream ms;
            private int offset;

            public OffsetMemoryStream(int offset, MemoryStream ms) {
                this.ms = ms;
                this.offset = offset;
                Position = 0;
            }
            
            public virtual int Capacity { 
                get {
                    return ms.Capacity - offset;
                } set {
                    ms.Capacity = value + offset;
                } 
            }
            
            public override bool CanWrite { get => ms.CanWrite; }
            
            public override bool CanSeek { get => ms.CanSeek; }
            
            public override bool CanRead { get => ms.CanRead; }
            
            public override long Length { get => ms.Length - offset; }
            
            public override long Position {
                get {
                    return ms.Position - offset;
                }
                set {
                    ms.Position = value + offset;
                }
            }

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) {
                throw new Exception("Not like this smh");
                //return ms.CopyToAsync(destination, bufferSize, cancellationToken);
            }
            
            public override void Flush() {
                ms.Flush();
            }

            public override Task FlushAsync(CancellationToken cancellationToken) {
                return ms.FlushAsync(cancellationToken);
            }

            public virtual byte[] GetBuffer() {
                throw new Exception("Not like this smh");
            }
            
            public override int Read(byte[] buffer, int offset, int count) {
                return ms.Read(buffer, offset, count);
            }
            
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
                return ms.ReadAsync(buffer, offset, count, cancellationToken);
            }
            
            public override int ReadByte() {
                return ms.ReadByte();
            }

            public override long Seek(long offset, SeekOrigin loc) {
                offset += this.offset;
                return ms.Seek(offset, loc);
            }

            public override void SetLength(long value) {
                ms.SetLength(value + offset);
            }

            public virtual byte[] ToArray() {
                throw new Exception("not like this smh");
            }

            public override void Write(byte[] buffer, int offset, int count) {
                ms.Write(buffer, offset, count);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
                return ms.WriteAsync(buffer, offset, count, cancellationToken);
            }

            public override void WriteByte(byte value) {
                ms.WriteByte(value);
            }

            public virtual void WriteTo(Stream stream) {
                throw new Exception("Not like this smh");
            }

            protected override void Dispose(bool disposing) {
                ms.Dispose();
            }
        }

        public static byte[] Decode(byte[] input) {
            try {
                uint rawLength = (uint)((((uint)input[0]) << 24) | (((uint)input[1]) << 16) | (((uint)input[2]) << 8)) | ((((uint)input[3]) << 0));
                byte[] result = new byte[rawLength];
                byte[] props = new byte[5];
                Array.Copy(input, 4, props, 0, props.Length);
                MemoryStream dataStream = new MemoryStream(result);
                SevenZip.Compression.LZMA.Decoder coder = new SevenZip.Compression.LZMA.Decoder();
                coder.SetDecoderProperties(props);
                Stream trimmedData = new OffsetMemoryStream(9, new MemoryStream(input));
                coder.Code(trimmedData, dataStream, input.Length, result.Length, null);
                return result;
            } catch(Exception e) {
                Debugger.Log(e);
            }
            return null;
        }

        public static byte[] Encode(byte[] input) {
            try {
                int len = input.Length;
                SevenZip.Compression.LZMA.Encoder coder = new SevenZip.Compression.LZMA.Encoder();

                MemoryStream dataStream = new MemoryStream();
                coder.Code(new MemoryStream(input), dataStream, input.Length, -1, null);

                MemoryStream propertiesStream = new MemoryStream();
                coder.WriteCoderProperties(propertiesStream);
                byte[] properties= propertiesStream.ToArray();

                byte[] data = dataStream.ToArray();
                byte[] result = new byte[4 + properties.Length + data.Length];

                Array.Copy(properties, 0, result, 4, properties.Length);
                Array.Copy(data, 0, result, 4 + properties.Length, data.Length);
                result[0] = (byte)((len >> 24) & 0xff);
                result[1] = (byte)((len >> 16) & 0xff);
                result[2] = (byte)((len >> 8) & 0xff);
                result[3] = (byte)((len >> 0) & 0xff);

                return result;
            } catch (Exception e) {
                Debugger.Log(e);
            }
            return null;
        }
    }
}
