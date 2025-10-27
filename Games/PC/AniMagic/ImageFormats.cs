using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.AniMagic
{
    public static class ImageFormats
    {
        public static byte[] DecompressSpp32(BinaryReader sppReader, int size, int width, int height)
        {
            var outSize = width * height;
            byte[] lookup = new byte[16];
            var lookupData = sppReader.ReadBytes(16);
            Array.Copy(lookupData, lookup, lookupData.Length);
            var decompressedData = new List<byte>();
            while (size-- > 0)
            {
                byte ins = sppReader.ReadByte();
                byte loBits = (byte)(ins & 0x0F);
                byte hiBits = (byte)((ins & 0xF0) >> 4);

                if (hiBits == 0xF)
                {
                    // run of a single color
                    var count = sppReader.ReadByte() + 3;
                    size--;
                    if (outSize < count)
                    {
                        Console.WriteLine($"Decompressed data is too small: {outSize} < {count}");
                        break;
                    }
                    outSize -= count;
                    decompressedData.AddRange(Enumerable.Repeat(loBits, count));
                }
                else
                {
                    // two pixels
                    if (outSize <= 0)
                    {
                        Console.WriteLine($"Decompressed data is too small: {outSize} <= 0");
                        break;
                    }
                    decompressedData.Add(lookup[hiBits]);
                    outSize--;
                    if (outSize > 0)
                    {
                        decompressedData.Add(lookup[hiBits]);
                        outSize--;
                    }
                }
            }
            return decompressedData.ToArray();
        }
        public static byte[] DecompressSlw8(BinaryReader slReader, int size, int width, int height)
        {
            // populate decompressed data with 0x00 bytes
            var decompressedData = new List<byte>(new byte[width * height]);
            var bufferIndex = 0;
            while (size-- > 0)
            {
                byte val = slReader.ReadByte(); // read the next byte
                if (val != 0xFF)
                {
                    decompressedData[bufferIndex++] = val; // if the byte is not 0xFF, add it to the decompressed data
                    continue; // continue to the next byte
                }
                uint count = slReader.ReadByte(); // read the count
                size--;
                ushort step = 0;
                if ((count & 0x80) == 0)
                {
                    step = slReader.ReadByte();
                    size--;
                }
                else
                {
                    count = count ^ 0x80;
                    step = slReader.ReadUInt16();
                    size -= 2;
                }
                count += 4;

                for (int i = 0; i < count; i++)
                {
                    if (decompressedData.Count < step + 1)
                    {
                        Console.WriteLine($"Decompressed data is too small: {decompressedData.Count} < {step + 1}");
                        break;
                    }
                    decompressedData[bufferIndex++] = decompressedData[decompressedData.Count - step - 1]; // add the byte at the offset to the decompressed data
                }
            }
            return decompressedData.ToArray(); // return the decompressed data as an array
        }

        public static byte[] DecompressSLWM(byte[] input)
        {
            uint bitsLeft = 0;
            ushort lastBits = 0;
            byte currBit;
            var output = new List<byte>();

            using var br = new BinaryReader(new MemoryStream(input));
            while (true)
            {
                if (bitsLeft == 0) { bitsLeft = 16; lastBits = br.ReadUInt16(); }
                currBit = (byte)(lastBits & 1); lastBits >>= 1; bitsLeft--;

                if (currBit == 1)
                {
                    output.Add(br.ReadByte());
                    continue;
                }

                if (bitsLeft == 0) { bitsLeft = 16; lastBits = br.ReadUInt16(); }
                currBit = (byte)(lastBits & 1); lastBits >>= 1; bitsLeft--;

                uint start;
                uint count;

                if (currBit > 0)
                {
                    uint orMask = br.ReadByte();
                    uint ins = br.ReadByte();
                    count = ins & 7;
                    start = (uint)(((ins & ~7) << 5) | orMask);
                    if (count == 0)
                    {
                        count = br.ReadByte();
                        if (count == 0)
                            break;
                        count -= 2;
                    }
                }
                else
                {
                    // count encoded in the next two bits
                    count = 0;

                    if (bitsLeft == 0) { bitsLeft = 16; lastBits = br.ReadUInt16(); }
                    currBit = (byte)(lastBits & 1); lastBits >>= 1; bitsLeft--;

                    count = (count << 1) | currBit;

                    if (bitsLeft == 0) { bitsLeft = 16; lastBits = br.ReadUInt16(); }
                    currBit = (byte)(lastBits & 1); lastBits >>= 1; bitsLeft--;

                    count = (count << 1) | currBit;

                    start = br.ReadByte();
                }

                count += 2;
                start++;
                for (uint i = 0; i < count; i++)
                {
                    // *buffer = *(buffer - start);
                    // buffer++;
                    output.Add(output[(int)(output.Count - start)]);
                }
            }
            return output.ToArray();
        }
    }
}
