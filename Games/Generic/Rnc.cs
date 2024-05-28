using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.Generic
{
    public static class Rnc
    {
        private static readonly ushort[] CrcTable =
       {
            0x0000, 0xC0C1, 0xC181, 0x0140, 0xC301, 0x03C0, 0x0280, 0xC241,
            0xC601, 0x06C0, 0x0780, 0xC741, 0x0500, 0xC5C1, 0xC481, 0x0440,
            0xCC01, 0x0CC0, 0x0D80, 0xCD41, 0x0F00, 0xCFC1, 0xCE81, 0x0E40,
            0x0A00, 0xCAC1, 0xCB81, 0x0B40, 0xC901, 0x09C0, 0x0880, 0xC841,
            0xD801, 0x18C0, 0x1980, 0xD941, 0x1B00, 0xDBC1, 0xDA81, 0x1A40,
            0x1E00, 0xDEC1, 0xDF81, 0x1F40, 0xDD01, 0x1DC0, 0x1C80, 0xDC41,
            0x1400, 0xD4C1, 0xD581, 0x1540, 0xD701, 0x17C0, 0x1680, 0xD641,
            0xD201, 0x12C0, 0x1380, 0xD341, 0x1100, 0xD1C1, 0xD081, 0x1040,
            0xF001, 0x30C0, 0x3180, 0xF141, 0x3300, 0xF3C1, 0xF281, 0x3240,
            0x3600, 0xF6C1, 0xF781, 0x3740, 0xF501, 0x35C0, 0x3480, 0xF441,
            0x3C00, 0xFCC1, 0xFD81, 0x3D40, 0xFF01, 0x3FC0, 0x3E80, 0xFE41,
            0xFA01, 0x3AC0, 0x3B80, 0xFB41, 0x3900, 0xF9C1, 0xF881, 0x3840,
            0x2800, 0xE8C1, 0xE981, 0x2940, 0xEB01, 0x2BC0, 0x2A80, 0xEA41,
            0xEE01, 0x2EC0, 0x2F80, 0xEF41, 0x2D00, 0xEDC1, 0xEC81, 0x2C40,
            0xE401, 0x24C0, 0x2580, 0xE541, 0x2700, 0xE7C1, 0xE681, 0x2640,
            0x2200, 0xE2C1, 0xE381, 0x2340, 0xE101, 0x21C0, 0x2080, 0xE041,
            0xA001, 0x60C0, 0x6180, 0xA141, 0x6300, 0xA3C1, 0xA281, 0x6240,
            0x6600, 0xA6C1, 0xA781, 0x6740, 0xA501, 0x65C0, 0x6480, 0xA441,
            0x6C00, 0xACC1, 0xAD81, 0x6D40, 0xAF01, 0x6FC0, 0x6E80, 0xAE41,
            0xAA01, 0x6AC0, 0x6B80, 0xAB41, 0x6900, 0xA9C1, 0xA881, 0x6840,
            0x7800, 0xB8C1, 0xB981, 0x7940, 0xBB01, 0x7BC0, 0x7A80, 0xBA41,
            0xBE01, 0x7EC0, 0x7F80, 0xBF41, 0x7D00, 0xBDC1, 0xBC81, 0x7C40,
            0xB401, 0x74C0, 0x7580, 0xB541, 0x7700, 0xB7C1, 0xB681, 0x7640,
            0x7200, 0xB2C1, 0xB381, 0x7340, 0xB101, 0x71C0, 0x7080, 0xB041,
            0x5000, 0x90C1, 0x9181, 0x5140, 0x9301, 0x53C0, 0x5280, 0x9241,
            0x9601, 0x56C0, 0x5780, 0x9741, 0x5500, 0x95C1, 0x9481, 0x5440,
            0x9C01, 0x5CC0, 0x5D80, 0x9D41, 0x5F00, 0x9FC1, 0x9E81, 0x5E40,
            0x5A00, 0x9AC1, 0x9B81, 0x5B40, 0x9901, 0x59C0, 0x5880, 0x9841,
            0x8801, 0x48C0, 0x4980, 0x8941, 0x4B00, 0x8BC1, 0x8A81, 0x4A40,
            0x4E00, 0x8EC1, 0x8F81, 0x4F40, 0x8D01, 0x4DC0, 0x4C80, 0x8C41,
            0x4400, 0x84C1, 0x8581, 0x4540, 0x8701, 0x47C0, 0x4680, 0x8641,
            0x8201, 0x42C0, 0x4380, 0x8341, 0x4100, 0x81C1, 0x8081, 0x4040
        };

        public static RncStatus ReadRnc(Stream input, Stream output)
        {
            unsafe
            {
                if (input == null)
                    throw new ArgumentNullException(nameof(input));

                if (output == null)
                    throw new ArgumentNullException(nameof(output));

                var iBytes = new byte[input.Length];

                input.Read(iBytes, 0, iBytes.Length);

                uint oLen, iLen, oSum, iSum;

                fixed (byte* b = &iBytes[0])
                {
                    var sig = ReadU32BE(b);
                    if (sig != 0x524E4301)
                        return RncStatus.FileIsNotRnc;

                    oLen = ReadU32BE(b + 4);
                    iLen = ReadU32BE(b + 8);
                    oSum = ReadU16BE(b + 12);
                    iSum = ReadU16BE(b + 14);
                }

                var oBytes = new byte[oLen];

                fixed (byte* iBuf = &iBytes[0])
                fixed (byte* oBuf = &oBytes[0])
                {
                    return Unpack(iBuf, iLen, iSum, oBuf, oLen, oSum);
                }
            }
        }

        private static unsafe RncStatus Unpack(byte* iBuf, uint iLen, uint iSum, byte* oBuf, uint oLen, uint oSum)
        {
            var raw = new HuffmanTable();
            var dst = new HuffmanTable();
            var len = new HuffmanTable();

            var oEnd = oBuf + oLen;
            var iEnd = iBuf + 18 + iLen;

            iBuf += 18;

            if (ComputeChecksum(iBuf, (int)(iEnd - iBuf)) != iSum)
                return RncStatus.PackedCrcError;

            var stream = new BitStream();
            BitStreamInit(&stream, iBuf, iEnd);
            BitStreamAdvance(&stream, 2);

            while (oBuf < oEnd)
            {
                ReadHuffmanTable(raw, &stream);
                ReadHuffmanTable(dst, &stream);
                ReadHuffmanTable(len, &stream);

                var chunks = BitStreamRead(&stream, 0xFFFF, 16);

                while (true)
                {
                    var length = ReadHuffman(raw, &stream);
                    if (length == -1)
                        return RncStatus.HufDecodeError;

                    if (length != 0)
                    {
                        while (length-- != 0)
                            *oBuf++ = *stream.DataPos++;

                        BitStreamFix(&stream);
                    }

                    if (--chunks <= 0)
                        break;

                    var pos = ReadHuffman(dst, &stream);
                    if (pos == -1)
                        return RncStatus.HufDecodeError;

                    pos += 1;

                    length = ReadHuffman(len, &stream);

                    if (length == -1)
                        return RncStatus.HufDecodeError;

                    length += 2;

                    for (; length > 0; length--, oBuf++)
                        *oBuf = oBuf[-pos];
                }
            }

            if (oEnd != oBuf)
                return RncStatus.FileSizeMismatch;

            return ComputeChecksum(oEnd - oLen, (int)oLen) != oSum
                ? RncStatus.UnpackedCrcError
                : RncStatus.Ok;
        }

        private static unsafe void BitStreamInit(BitStream* stream, byte* dataPos, byte* dataEnd)
        {
            stream->BitBuffer = ReadU16LE(dataPos);
            stream->BitCount = 16;
            stream->DataPos = dataPos;
            stream->DataEnd = dataEnd;
        }

        private static unsafe void BitStreamFix(BitStream* stream)
        {
            stream->BitCount -= 16;
            stream->BitBuffer &= (uint)((1 << stream->BitCount) - 1);

            if (stream->DataPos < stream->DataEnd)
            {
                stream->BitBuffer |= ReadU16LE(stream->DataPos) << stream->BitCount;
                stream->BitCount += 16;
            }
            else if (stream->DataPos == stream->DataEnd)
            {
                stream->BitBuffer |= (uint)(*stream->DataPos << stream->BitCount);
                stream->BitCount += 8;
            }
        }

        private static unsafe uint BitStreamPeek(BitStream* stream, uint mask)
        {
            var peek = stream->BitBuffer & mask;

            return peek;
        }

        private static unsafe void BitStreamAdvance(BitStream* stream, int bits)
        {
            stream->BitBuffer >>= bits;
            stream->BitCount -= bits;

            if (stream->BitCount >= 16)
                return;

            stream->DataPos += 2;

            if (stream->DataPos < stream->DataEnd)
            {
                stream->BitBuffer |= ReadU16LE(stream->DataPos) << stream->BitCount;
                stream->BitCount += 16;
            }
            else if (stream->DataPos == stream->DataEnd)
            {
                stream->BitBuffer |= (uint)(*stream->DataPos << stream->BitCount);
                stream->BitCount += 8;
            }
        }

        private static unsafe uint BitStreamRead(BitStream* stream, uint mask, int bits)
        {
            var peek = BitStreamPeek(stream, mask);

            BitStreamAdvance(stream, bits);

            return peek;
        }

        private static unsafe ushort ComputeChecksum(byte* data, int length)
        {
            var val = default(ushort);

            while (length-- != 0)
            {
                val = (ushort)(val ^ *data++);
                val = (ushort)((val >> 8) ^ CrcTable[val & 0xFF]);
            }

            return val;
        }

        private static uint MirrorBits(uint value, int bits)
        {
            var top = (uint)(1 << (bits - 1));
            var bot = 1u;

            while (top > bot)
            {
                var mask = top | bot;

                var masked = value & mask;
                if (masked != 0 && masked != mask)
                    value ^= mask;

                top >>= 1;
                bot <<= 1;
            }

            return value;
        }

        private static unsafe long ReadHuffman(HuffmanTable table, BitStream* stream)
        {
            int i;

            for (i = 0; i < table.Count; i++)
            {
                var mask = (uint)((1 << table.Leaves[i].CodeLength) - 1);

                if (BitStreamPeek(stream, mask) == table.Leaves[i].Code)
                    break;
            }

            if (i == table.Count)
                return -1;

            BitStreamAdvance(stream, table.Leaves[i].CodeLength);

            var val = (uint)table.Leaves[i].Value;

            if (val < 2)
                return val;

            val = (uint)(1 << (int)(val - 1));

            val |= BitStreamRead(stream, val - 1, table.Leaves[i].Value - 1);

            return val;
        }

        private static unsafe void ReadHuffmanTable(HuffmanTable table, BitStream* stream)
        {
            int i;

            var num = (int)BitStreamRead(stream, 0x1F, 5);
            if (num == 0)
                return;

            var leafLen = new int[32];
            var leafMax = 1;

            for (i = 0; i < num; i++)
            {
                leafLen[i] = (int)BitStreamRead(stream, 0x0F, 4);

                if (leafMax < leafLen[i])
                    leafMax = leafLen[i];
            }

            var count = 0;
            var value = 0u; // code as BE

            for (i = 1; i <= leafMax; i++)
            {
                for (var j = 0; j < num; j++)
                {
                    if (leafLen[j] != i)
                        continue;

                    table.Leaves[count].Code = MirrorBits(value, i);
                    table.Leaves[count].CodeLength = i;
                    table.Leaves[count].Value = j;
                    value++;
                    count++;
                }

                value <<= 1;
            }

            table.Count = count;
        }

        private static unsafe uint ReadU16LE(byte* p)
        {
            uint n = p[1];
            n = (n << 8) + p[0];
            return n;
        }

        private static unsafe uint ReadU16BE(byte* p)
        {
            uint n = p[0];
            n = (n << 8) + p[1];
            return n;
        }

        private static unsafe uint ReadU32BE(byte* p)
        {
            uint n = p[0];
            n = (n << 8) + p[1];
            n = (n << 8) + p[2];
            n = (n << 8) + p[3];
            return n;
        }

        private unsafe struct BitStream
        {
            public uint BitBuffer;
            public int BitCount;
            public byte* DataEnd;
            public byte* DataPos;
        }

        private sealed class HuffmanTable
        {
            public readonly HuffmanLeaf[] Leaves = new HuffmanLeaf[32];
            public int Count;
        }

        private struct HuffmanLeaf
        {
            public uint Code;
            public int CodeLength;
            public int Value;
        }
    }

    public enum RncStatus
    {
        Ok,
        UnpackedCrcError,
        FileSizeMismatch,
        HufDecodeError,
        PackedCrcError,
        FileIsNotRnc
    }
}
