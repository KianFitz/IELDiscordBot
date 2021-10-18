using System;
using System.Collections.Generic;
using System.Text;

namespace Shared
{
    public class ByteBuffer
    {
        private readonly List<byte> buffer = new List<byte>();
        private readonly byte[] readBuffer = new byte[4096];
        private int readPos = 0;

        public ByteBuffer(int opcode, int length)
        {
            WriteInt(opcode);
            WriteInt(length + 8);
        }

        public ByteBuffer(byte[] bytes)
        {
            readBuffer = bytes;
        }

        public void WriteString(string s)
        {
            byte[] b = Encoding.UTF8.GetBytes(s);
            WriteInt(s.Length);
            buffer.AddRange(b);
        }

        public void WriteInt(int i)
        {
            byte[] b = BitConverter.GetBytes(i);
            buffer.AddRange(b);
        }

        public void WriteBool(bool bo)
        {
            byte[] b = BitConverter.GetBytes(bo);
            buffer.AddRange(b);
        }

        public string ReadString()
        {
            int length = ReadInt();
            if (length == 0)
                return "";

            byte[] bytes = new byte[length];
            Array.Copy(readBuffer, readPos, bytes, 0, length);

            readPos += length;

            return Encoding.UTF8.GetString(bytes);
        }

        public int ReadInt()
        {
            int retVal;
            byte[] bytes = new byte[4];
            Array.Copy(readBuffer, readPos, bytes, 0, 4);

            retVal = BitConverter.ToInt32(bytes);

            readPos += 4;

            return retVal;
        }

        public bool ReadBool()
        {
            byte[] bytes = new byte[1];
            Array.Copy(readBuffer, readPos, bytes, 0, 1);
            readPos += 1;
            return BitConverter.ToBoolean(bytes, 0);
        }

        public byte[] ToByteArray()
        {
            return buffer.ToArray();
        }
    }
}
