using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.SCI
{
    using System;
    using System.IO;

    public class Decompressor
    {
        protected Stream _src;
        protected byte[] _dest;
        protected uint _szPacked;
        protected uint _szUnpacked;
        protected uint _nBits;
        protected uint _dwRead;
        protected uint _dwWrote;
        protected uint _dwBits;

        public void Init(Stream src, byte[] dest, uint nPacked, uint nUnpacked)
        {
            _src = src;
            _dest = dest;
            _szPacked = nPacked;
            _szUnpacked = nUnpacked;
            _nBits = 0;
            _dwRead = _dwWrote = 0;
            _dwBits = 0;
        }

        protected void FetchBitsMSB()
        {
            while (_nBits <= 24)
            {
                int nextByte = _src.ReadByte();
                if (nextByte == -1)
                    throw new EndOfStreamException("Unexpected end of stream");

                _dwBits |= ((uint)nextByte) << (int)(24 - _nBits);
                _nBits += 8;
                _dwRead++;
            }
        }

        protected uint GetBitsMSB(int n)
        {
            if (_nBits < n)
                FetchBitsMSB();
            uint ret = _dwBits >> (32 - n);
            _dwBits <<= n;
            _nBits -= (uint)n;
            return ret;
        }

        protected byte GetByteMSB()
        {
            return (byte)GetBitsMSB(8);
        }

        protected void PutByte(byte b)
        {
            if (_dwWrote >= _dest.Length)
                throw new IndexOutOfRangeException("Destination buffer overflow");
            _dest[_dwWrote++] = b;
        }

        protected bool IsFinished()
        {
            return (_dwWrote == _szUnpacked) && (_dwRead >= _szPacked);
        }
    }

    public class DecompressorLZS : Decompressor
    {
        public byte[] Unpack(Stream src, uint nPacked, uint nUnpacked)
        {
            byte[] dest = new byte[nUnpacked];
            Init(src, dest, nPacked, nUnpacked);
            UnpackLZS();
            return dest;
        }

        private void UnpackLZS()
        {
            ushort offs = 0;
            uint clen;

            while (!IsFinished())
            {
                if (GetBitsMSB(1) != 0)
                { // Compressed bytes follow
                    if (GetBitsMSB(1) != 0)
                    { // Seven bit offset follows
                        offs = (ushort)GetBitsMSB(7);
                        if (offs == 0) // This is the end marker - a 7-bit offset of zero
                            break;
                        clen = GetCompLen();
                        if (clen == 0)
                        {
                            Console.WriteLine("lzsDecomp: length mismatch");
                            return; // Error code
                        }
                        CopyComp(offs, clen);
                    }
                    else
                    { // Eleven bit offset follows
                        offs = (ushort)GetBitsMSB(11);
                        clen = GetCompLen();
                        if (clen == 0)
                        {
                            Console.WriteLine("lzsDecomp: length mismatch");
                            return; // Error code
                        }
                        CopyComp(offs, clen);
                    }
                }
                else // Literal byte follows
                {
                    PutByte(GetByteMSB());
                }
            }
        }

        private uint GetCompLen()
        {
            uint clen;
            int nibble;
            switch (GetBitsMSB(2))
            {
                case 0:
                    return 2;
                case 1:
                    return 3;
                case 2:
                    return 4;
                default:
                    switch (GetBitsMSB(2))
                    {
                        case 0:
                            return 5;
                        case 1:
                            return 6;
                        case 2:
                            return 7;
                        default:
                            clen = 8;
                            do
                            {
                                nibble = (int)GetBitsMSB(4);
                                clen += (uint)nibble;
                            } while (nibble == 0xF);
                            return clen;
                    }
            }
        }

        private void CopyComp(int offs, uint clen)
        {
            int hpos = (int)_dwWrote - offs;
            if (hpos < 0)
                throw new InvalidOperationException("Invalid offset in decompression");

            while (clen-- > 0)
            {
                if (hpos >= _dest.Length)
                    throw new IndexOutOfRangeException("Decompression output exceeds destination buffer size");
                PutByte(_dest[hpos++]);
            }
        }
    }

}
