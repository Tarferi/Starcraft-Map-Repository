using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace GUILib.starcraft {

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe class Pixel {
        byte r;
        byte g;
        byte b;
    }

    public unsafe class ObjectHelper<T> {

        public readonly Func<IntPtr, T> valueGetter;
        public readonly Action<IntPtr, T> valueSetter;

        public ObjectHelper(Func<IntPtr, T> valueGetter, Action<IntPtr, T> valueSetter) {
            this.valueGetter = valueGetter;
            this.valueSetter = valueSetter;
        }

    }

    public unsafe class Object<T> {

        private ObjectBuffer buffer = null;
        uint itemsCount = 0;
        uint totalOffset = 0;
        uint itemsSize = 0;

        ObjectHelper<T> helper;

        public Object(ObjectBuffer buffer, uint itemsCount, uint itemsSize, uint totalOffset, ObjectHelper<T> helper) {
            this.buffer = buffer;
            this.totalOffset = totalOffset;
            this.itemsCount = itemsCount;
            this.itemsSize = itemsSize;
            this.helper = helper;
        }

        public T Get(uint idx) {
            IntPtr ptr = Ptr(idx);
            T value = helper.valueGetter(ptr);
            return value;
        }

        public void Set(uint idx, T value) {
            IntPtr ptr = Ptr(idx);
            helper.valueSetter(ptr, value);
        }

        public IntPtr Ptr(uint idx) {
            return buffer.At(totalOffset + (idx * itemsSize));
        }
    }

    public unsafe class ObjectBuffer {

        private byte* raw = null;

        private uint size = 0;

        public ObjectBuffer() {

        }

        public Object<T> Reserve<T>(int count, ObjectHelper<T> helper) {
            return Reserve<T>((uint)count, helper);
        }

        public Object<T> Reserve<T>(uint count, ObjectHelper<T> helper) {
            if (raw != null) {
                throw new Exception("Cannot reserve more space. It's already allocated");
            }
            uint sz = (uint)Marshal.SizeOf(typeof(T));
            Object<T> obj = new Object<T>(this, count, sz, size, helper);
            size += count * sz;
            return obj;
        }

        public void Allocate() {
            if (raw != null) {
                throw new Exception("Cannot reserve more space. It's already allocated");
            }
            IntPtr p = Marshal.AllocHGlobal((int)size);
            raw = (byte*)p;
        }

        public void Reset() {
            if (raw != null) {
                IntPtr p = (IntPtr)raw;
                Marshal.FreeHGlobal(p);
            }
        }

        public IntPtr At(uint totalOffset) {
            return (IntPtr)(raw + totalOffset);
        }

        public static ObjectHelper<uint> UINT = new ObjectHelper<uint>((e) => ((uint*)e)[0], (e, v)=>{ ((uint*)e)[0] = v; });
        public static ObjectHelper<int> INT = new ObjectHelper<int>((e) => ((int*)e)[0], (e, v)=>{ ((int*)e)[0] = v; });
        public static ObjectHelper<ushort> USHORT = new ObjectHelper<ushort>((e) => ((ushort*)e)[0], (e, v)=>{ ((ushort*)e)[0] = v; });
        public static ObjectHelper<short> SHORT = new ObjectHelper<short>((e) => ((short*)e)[0], (e, v)=>{ ((short*)e)[0] = v; });
    }

    public unsafe class ByteArray {

        private byte* raw = null;

        public readonly uint Offset = 0;

        public readonly uint Length;

        public ByteArray(byte* raw, uint offset, uint size) {
            this.raw = raw;
            this.Offset = offset;
            this.Length = size;
        }

        public ByteArray(ByteArray another, uint offset, uint size) {
            this.raw = another.raw;
            this.Offset = another.Offset + offset;
            this.Length = size;
        }

        public byte* GetUnsafe() {
            return raw;
        }

        public byte At(uint idx) {
            return raw[Offset + idx];
        }
        
        public byte At(int idx) {
            return raw[Offset + idx];
        }

        public byte[] GetData() {
            byte[] b = new byte[Length];
            for (uint i = 0; i < Length; i++) {
                b[i] = At(i);
            }
            return b;
        }

        public String GetString(Encoding encoder) {
            return encoder.GetString(GetData());
        }

    }

    class ReadBuffer {

        private ByteArray data;

        private uint position = 0;

        public bool IsDone() {
            return position == data.Length;
        }

        public static bool IsDone(ByteArray data, ref uint position) {
            return position == data.Length;
        }

        public uint GetDataSize() {
            return (uint)data.Length;
        }
        
        public static uint GetDataSize(ByteArray data, uint position) {
            return (uint)data.Length;
        }
        
        public uint GetPosition() {
            return position;
        }
        
        public static uint GetPosition(ByteArray data, uint position) {
            return position;
        }

        public byte Read(ref bool error) {
            return Read(data, ref position, ref error);
        }
        
        public static byte Read(ByteArray data, ref uint position, ref bool error) {
            if (position >= data.Length) {
                error = true;
                return 0;
            }
            position++;
            return data.At(position - 1);
        }

        public ByteArray ReadData(uint length, ref bool error) {
            return ReadData(data, ref position, length, ref error);
        }

        public static ByteArray ReadData(ByteArray data, ref uint position, uint length, ref bool error) {
            return ReadData(data, ref position, (int)length, ref error);
        }

        public ByteArray ReadData(int length, ref bool error) {
            return ReadData(data, ref position, length, ref error);
        }
        
        public static ByteArray ReadData(ByteArray data, ref uint position, int length, ref bool error) {
            if(position + length > data.Length) {
                error = true;
                position = (uint)data.Length + 1; // Any further call to cause an error
                return null;
            } else {
                position += (uint)length;
                return new ByteArray(data, position - (uint)length, (uint)length);
            }

        }

        public ushort ReadShort(ref bool error) {
            return ReadShort(data, ref position, ref error);
        }
        
        public static ushort ReadShort(ByteArray data, ref uint position, ref bool error) {
            byte b1 = Read(data, ref position, ref error);
            if (!error) {
                byte b2 = Read(data, ref position, ref error);
                if (!error) {
                    ushort x1 = (ushort)b1;
                    ushort x2 = (ushort)b2;
                    return (ushort)((x2 << 8) + x1);
                }
            }
            return 0;
        }

        public uint ReadInt(ref bool error) {
            return ReadInt(data, ref position, ref error);
        }

        public static uint ReadInt(ByteArray data, ref uint position, ref bool error) {
            ushort b1 = ReadShort(data, ref position, ref error);
            if (!error) {
                ushort b2 = ReadShort(data, ref position, ref error);
                if (!error) {
                    uint x1 = (uint)b1;
                    uint x2 = (uint)b2;
                    return (uint)((x2 << 16) + x1);
                }
            }
            return 0;
        }

        public void Skip(int length, ref bool error) {
            Skip(data, ref position, length, ref error);
        }
        
        public void Skip(uint length, ref bool error) {
            Skip(data, ref position, length, ref error);
        }

        public static void Skip(ByteArray data, ref uint position, int length, ref bool error) {
            Skip(data, ref position, (uint)length, ref error);
        }

        public static void Skip(ByteArray data, ref uint position, uint length, ref bool error) {
            if (position + length > data.Length) {
                error = true;
                position = (uint)data.Length + 1; // Any further call to cause an error
            } else {
                position += length;
            }
        }

        public string ReadFixedLengthString(int length, ref bool error) {
            return ReadFixedLengthString(data, ref position, length, ref error);
        }
        
        public static string ReadFixedLengthString(ByteArray data, ref uint position, int length, ref bool error) {
            ByteArray newData = ReadData(data, ref position, length, ref error);
            if (!error) {
                return newData.GetString(Encoding.UTF8);
            }
            return null;
        }

        public ReadBuffer(ByteArray data) {
            this.data = data;
        }

    }
}
