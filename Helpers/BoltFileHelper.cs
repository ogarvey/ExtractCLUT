using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Helpers
{
    public static class BoltFileHelper
    {
        public static int GetEndOfBoltFile(byte[] data)
        {
            return BitConverter.ToInt32(data.Skip(0xC).Take(4).Reverse().ToArray(), 0);
        }
    }
}
