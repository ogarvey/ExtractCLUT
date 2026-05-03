using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Arcturus
{
    public static class ArcturusDecompressor
    {
        public static byte[] DecompressLzssLike(byte[] packed, int expectedSize)
        {
            if (expectedSize < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedSize));

            var output = new byte[expectedSize];
            var ring = new byte[0x2000];

            int src = 0;
            int dst = 0;

            while (dst < expectedSize && src < packed.Length)
            {
                byte flags = packed[src++];

                for (int bit = 0; bit < 8 && dst < expectedSize; bit++)
                {
                    if ((flags & 1) == 0)
                    {
                        if (src >= packed.Length)
                            break;

                        byte literal = packed[src++];
                        output[dst] = literal;
                        ring[dst & 0x1FFF] = literal;
                        dst++;
                    }
                    else
                    {
                        if (src + 1 >= packed.Length)
                            break;

                        int token = packed[src] | (packed[src + 1] << 8);
                        src += 2;

                        int distance = token & 0x0FFF;
                        int length = (token >> 12) + 2;
                        int copyPos = dst - distance;

                        for (int i = 0; i < length && dst < expectedSize; i++)
                        {
                            byte value = ring[(copyPos + i) & 0x1FFF];
                            output[dst] = value;
                            ring[dst & 0x1FFF] = value;
                            dst++;
                        }
                    }

                    flags >>= 1;
                }
            }

            if (dst != expectedSize)
                throw new InvalidDataException($"Decompression size mismatch: got {dst}, expected {expectedSize}");

            return output;
        }
    }
}
