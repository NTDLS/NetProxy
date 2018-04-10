using System;

namespace NetProxy.Library.Routing
{
    public static class CRC16
    {
        private const UInt16 polynomial = 0xAB01;
        private static readonly UInt16[] table = new UInt16[256];

        public static UInt16 ComputeChecksum(byte[] bytes)
        {
            UInt16 crc = 0;
            for (int i = 0; i < bytes.Length; ++i)
            {
                byte index = (byte)(crc ^ bytes[i]);
                crc = (UInt16)((crc >> 8) ^ table[index]);
            }
            return crc;
        }

        public static UInt16 ComputeChecksum(byte[] bytes, int offset, int length)
        {
            UInt16 crc = 0;
            for (int i = offset; i < length + offset; ++i)
            {
                byte index = (byte)(crc ^ bytes[i]);
                crc = (UInt16)((crc >> 8) ^ table[index]);
            }
            return crc;
        }

        static CRC16()
        {
            UInt16 value;
            UInt16 temp;
            for (UInt16 i = 0; i < table.Length; ++i)
            {
                value = 0;
                temp = i;
                for (byte j = 0; j < 8; ++j)
                {
                    if (((value ^ temp) & 0x0001) != 0)
                    {
                        value = (UInt16)((value >> 1) ^ polynomial);
                    }
                    else
                    {
                        value >>= 1;
                    }
                    temp >>= 1;
                }
                table[i] = value;
            }
        }
    }
}