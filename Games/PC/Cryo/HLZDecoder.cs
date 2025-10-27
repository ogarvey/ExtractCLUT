using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Cryo
{
    public static class HLZDecoder
    {
        private static uint ReadUInt32LE(BinaryReader reader)
        {
            byte b1 = reader.ReadByte();
            byte b2 = reader.ReadByte();
            byte b3 = reader.ReadByte();
            byte b4 = reader.ReadByte();
            return (uint)(b1 | (b2 << 8) | (b3 << 16) | (b4 << 24));
        }

        private static ushort ReadUInt16LE(BinaryReader reader)
        {
            byte b1 = reader.ReadByte();
            byte b2 = reader.ReadByte();
            return (ushort)(b1 | (b2 << 8));
        }

        public static byte[] DecodeFrameInPlace(BinaryReader reader, uint size, int expectedOutputSize)
        {
            // Pre-allocate and zero-initialize the output buffer
            var output = new List<byte>(new byte[expectedOutputSize]);
            int outputPosition = 0;

            bool eof = false;
            bool checkSize = (size != uint.MaxValue);
            uint reg = 0;
            int regBits = 0;

            while (!eof)
            {
                if (GetReg(reader, ref size, ref reg, ref regBits))
                {
                    // Copy literal byte
                    if (size < 1)
                    {
                        throw new InvalidDataException("Can't read pixel byte");
                    }

                    byte c = reader.ReadByte();
                    output[outputPosition++] = c;
                    size--;
                }
                else
                {
                    int offset, repeatCount;

                    if (GetReg(reader, ref size, ref reg, ref regBits))
                    {
                        // Long repeat
                        if (size < 2)
                        {
                            throw new InvalidDataException("Can't read repeat count/offset");
                        }

                        ushort tmp = reader.ReadUInt16();
                        size -= 2;

                        repeatCount = tmp & 0x7;
                        offset = (tmp >> 3) - 0x2000;

                        if (repeatCount == 0)
                        {
                            if (size < 1)
                            {
                                throw new InvalidDataException("Can't read long repeat count");
                            }

                            repeatCount = reader.ReadByte();
                            size--;

                            if (repeatCount == 0)
                            {
                                eof = true;
                                continue;
                            }
                        }
                    }
                    else
                    {
                        // Short repeat
                        repeatCount = GetReg(reader, ref size, ref reg, ref regBits) ? 1 : 0;
                        repeatCount <<= 1;
                        repeatCount |= GetReg(reader, ref size, ref reg, ref regBits) ? 1 : 0;

                        if (size < 1)
                        {
                            throw new InvalidDataException("Can't read offset byte");
                        }

                        offset = reader.ReadByte() - 0x100;
                        size--;
                    }

                    repeatCount += 2;

                    int sourcePos = outputPosition + offset; // offset is negative

                    if (sourcePos < 0)
                    {
                        throw new InvalidDataException(
                            $"Invalid offset {offset}, current position is {outputPosition}");
                    }

                    // Copy from previous data (offset is always negative)
                    for (int i = 0; i < repeatCount; i++)
                    {
                        output[outputPosition] = output[sourcePos + i];
                        outputPosition++;
                    }
                }
            }

            if (checkSize && size != 0)
            {
                // Skip remaining bytes
                reader.BaseStream.Seek(size, SeekOrigin.Current);
            }

            return output.ToArray();
        }

        private static bool GetReg(BinaryReader reader, ref uint size, ref uint reg, ref int regBits)
        {
            if (regBits == 0)
            {
                if (size < 4)
                {
                    throw new InvalidDataException("Can't feed register: not enough data");
                }

                reg = ReadUInt32LE(reader);
                size -= 4;
                regBits = 32;
            }

            uint ret = (reg >> 31) & 0x1;
            reg <<= 1;
            regBits--;

            return ret != 0;
        }
    }
}
