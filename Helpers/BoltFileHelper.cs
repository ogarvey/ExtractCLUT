using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Helpers
{
    public static class BoltFileHelper
    {
        public static int GetEndOfBoltData(byte[] data)
        {
            return BitConverter.ToInt32(data.Skip(0xC).Take(4).Reverse().ToArray(), 0);
        }

        public static int GetStartOfBoltData(byte[] data)
        {
            return BitConverter.ToInt32(data.Skip(0x18).Take(4).Reverse().ToArray(), 0);
        }

        public static List<int> GetBoltOffsets(byte[] data)
        {
            var offsets = new List<int>();

            var value = GetStartOfBoltData(data);
            offsets.Add(value);
            var index = 0x20;
            while (index < offsets[0])
            {
                value = BitConverter.ToInt32(data.Skip(index+8).Take(4).Reverse().ToArray(), 0);
                offsets.Add(value);
                index+=16;
            }

            return offsets;
        }
    }
}
