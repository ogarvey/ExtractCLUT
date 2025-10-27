using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Delphine
{
    public static class CineUnpack
    {
        private static uint _crc;
        private static uint _chunk32b;
        private static uint _srcPos;
        private static int _dstPos;
        private static BinaryReader? _source;
        private static byte[]? _destination;

        public static byte[] unpack(byte[] source, uint outputLen)
        {
            _source = new BinaryReader(new MemoryStream(source));
            _destination = new byte[outputLen];
            _srcPos = 0;
            _dstPos = (int)(outputLen - 1);

            _source?.BaseStream.Seek(source.LongLength - 4, SeekOrigin.Begin);
            uint unpackedLength = readSource();
            _crc = readSource();
            _chunk32b = readSource();
            Console.WriteLine($"Unpacked Length: {unpackedLength}, CRC: {_crc:X8}, First Chunk: {_chunk32b:X8}");
            _crc ^= _chunk32b;
            Console.WriteLine($"First Chunk after CRC: {_chunk32b:X8}");

            while (_dstPos >= 0)
            {
                if (nextBit() == 0)
                { // 0...
                    if (nextBit() == 0)
                    { // 0 0
                        uint numBytes = getBits(3) + 1;
                        unpackRawBytes(numBytes);
                    }
                    else
                    { // 0 1
                        uint numBytes = 2;
                        uint offset = getBits(8);
                        copyRelocatedBytes(offset, numBytes);
                    }
                }
                else
                { // 1...
                    uint c = getBits(2);
                    if (c == 3)
                    { // 1 1 1
                        uint numBytes = getBits(8) + 9;
                        unpackRawBytes(numBytes);
                    }
                    else if (c < 2)
                    { // 1 0 x
                        uint numBytes = c + 3;
                        uint offset = getBits(c + 9);
                        copyRelocatedBytes(offset, numBytes);
                    }
                    else
                    { // 1 1 0
                        uint numBytes = getBits(8) + 1;
                        uint offset = getBits(12);
                        copyRelocatedBytes(offset, numBytes);
                    }
                }
            }

            return _destination!;
        }

        private static uint readSource()
        {
            if (_source?.BaseStream.Position + 4 <= _source?.BaseStream.Length)
            {
                var val = _source.ReadBigEndianUInt32();
                if (_source.BaseStream.Position - 8 >= 0)
                {
                    _source.BaseStream.Position -= 8;
                }
                else
                {
                    _source.BaseStream.Position = 0;
                }
                return val;
            }

            throw new InvalidOperationException("Attempt to read past end of source buffer");
        }

        private static uint rcr(bool inputCarry)
        {
            uint outputCarry = (_chunk32b & 1);
            _chunk32b >>= 1;
            if (inputCarry)
            {
                _chunk32b |= 0x80000000;
            }
            return outputCarry;
        }

        private static uint nextBit()
        {
            uint carry = rcr(false);

            if (_chunk32b == 0)
            {
                _chunk32b = readSource();
                _crc ^= _chunk32b;
                carry = rcr(true);
            }
            return carry;
        }

        private static uint getBits(uint count)
        {
            uint c = 0;
            while (count-- > 0)
            {
                c <<= 1;
                c |= nextBit();
            }
            return c;
        }

        private static void unpackRawBytes(uint count)
        {
            if (_dstPos >= _destination?.Length || _dstPos - count + 1 < 0)
            {
                throw new InvalidOperationException("Destination pointer is out of bounds for this operation");
            }

            while (count-- > 0)
            {
                _destination![_dstPos] = (byte)getBits(8);
                _dstPos--;
            }
        }

        private static void copyRelocatedBytes(uint offset, uint count)
        {
            // if (_dst + offset >= _dstEnd || _dst - numBytes + 1 < _dstBegin)
            // {
            //     _error = true;
            //     return; // Destination pointer is out of bounds for this operation
            // }
            if (_dstPos + offset >= _destination?.Length || _dstPos - count + 1 < 0)
            {
                throw new InvalidOperationException("Destination pointer is out of bounds for this operation");
            }

            // while (numBytes--)
            // {
            //     *_dst = *(_dst + offset);
            //     --_dst;
            // }
            while (count-- > 0)
            {
                _destination![_dstPos] = _destination![_dstPos + offset];
                _dstPos--;
            }
        }
    }
}
