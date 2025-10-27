using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public static class JPHelper
    {
        private const ushort XorKey = 0x7c2b;

        /// <summary>
        /// Helper class to read bits from a byte array.
        /// </summary>
        private class BitReader
        {
            private readonly byte[] _data;
            private int _bytePosition;
            private byte _bitPosition;

            public BitReader(byte[] data)
            {
                _data = data;
                _bytePosition = 0;
                _bitPosition = 0;
            }

            public ushort ReadWord()
            {
                if (_bytePosition + 1 >= _data.Length)
                {
                    // Not enough data for a full word, might happen at the end
                    return 0;
                }
                ushort value = BitConverter.ToUInt16(_data, _bytePosition);
                _bytePosition += 2;
                return value;
            }

            public uint ReadBits(int count)
            {
                uint value = 0;
                for (int i = 0; i < count; i++)
                {
                    if (_bytePosition >= _data.Length)
                    {
                        break; // End of stream
                    }

                    uint bit = (uint)(_data[_bytePosition] >> (7 - _bitPosition)) & 1;
                    value = (value << 1) | bit;

                    _bitPosition++;
                    if (_bitPosition == 8)
                    {
                        _bitPosition = 0;
                        _bytePosition++;
                    }
                }
                return value;
            }
        }

        /// <summary>
        /// Decompresses a Jurassic Park asset file.
        /// </summary>
        /// <param name="filePath">The path to the compressed file.</param>
        public static void Decompress(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File not found at {filePath}");
                return;
            }

            byte[] compressedData = File.ReadAllBytes(filePath);
            byte[] decompressedData = DecompressData(compressedData);

            string outputFilePath = filePath + ".decompressed";
            File.WriteAllBytes(outputFilePath, decompressedData);

            Console.WriteLine($"Decompression successful. Output written to {outputFilePath}");
        }

        private static byte[] DecompressData(byte[] compressedData)
        {
            var reader = new BitReader(compressedData);

            // The first word seems to be the size of the output buffer.
            ushort decompressedSize = (ushort)(reader.ReadWord() ^ XorKey);

            // The second word is skipped/unused according to the disassembly.
            reader.ReadWord();

            var output = new byte[decompressedSize];
            int outputPos = decompressedSize - 1;

            uint bitStream = (uint)(reader.ReadWord() ^ XorKey);

            while (outputPos >= 0)
            {
                uint controlBit = bitStream & 1;
                bitStream >>= 1;
                if (bitStream == 0)
                {
                    bitStream = (uint)(reader.ReadWord() ^ XorKey) | 0x8000;
                }

                if (controlBit == 0) // Literal copy
                {
                    uint type = ReadNextBits(ref bitStream, reader, 1);
                    int count;
                    if (type == 1)
                    {
                        count = (int)ReadNextBits(ref bitStream, reader, 3) + 1;
                    }
                    else
                    {
                        count = (int)ReadNextBits(ref bitStream, reader, 8) + 1;
                    }

                    for (int i = 0; i < count && outputPos >= 0; i++)
                    {
                        output[outputPos] = (byte)ReadNextBits(ref bitStream, reader, 8);
                        outputPos--;
                    }
                }
                else // Copy from dictionary (previously decompressed data)
                {
                    uint type = ReadNextBits(ref bitStream, reader, 2);
                    int length;
                    int offset;

                    if (type == 3)
                    {
                        length = (int)ReadNextBits(ref bitStream, reader, 8);
                        offset = (int)ReadNextBits(ref bitStream, reader, 12) + 1;
                    }
                    else if (type < 2)
                    {
                        length = (int)type + 2;
                        offset = (int)ReadNextBits(ref bitStream, reader, (int)type + 9) + 1;
                    }
                    else // type == 2
                    {
                        length = (int)ReadNextBits(ref bitStream, reader, 8) + 1;
                        offset = (int)ReadNextBits(ref bitStream, reader, 8) + 1;
                    }

                    for (int i = 0; i < length && outputPos >= 0; i++)
                    {
                        output[outputPos] = output[outputPos + offset];
                        outputPos--;
                    }
                }
            }

            return output;
        }

        private static uint ReadNextBits(ref uint bitStream, BitReader reader, int count)
        {
            uint value = 0;
            for (int i = 0; i < count; i++)
            {
                value |= (bitStream & 1) << i;
                bitStream >>= 1;
                if (bitStream == 0)
                {
                    bitStream = (uint)(reader.ReadWord() ^ XorKey) | 0x8000;
                }
            }
            return value;
        }
    }
}
