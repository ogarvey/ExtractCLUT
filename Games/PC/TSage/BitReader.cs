using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.TSage
{
    public class BitReader : BinaryReader
    {

        private byte _remainder;
        private byte _bitsLeft;
        public int NumBits { get; set; }
        private ushort[] _bitMasks = { 0x1ff, 0x3ff, 0x7ff, 0xfff };

        public BitReader(Stream input) : base(input)
        {
            NumBits = 9;
            _remainder = 0;
            _bitsLeft = 0;
        }

        public BitReader(Stream input, Encoding encoding) : base(input, encoding)
        {
            NumBits = 9;
            _remainder = 0;
            _bitsLeft = 0;
        }

        public BitReader(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
        {
            NumBits = 9;
            _remainder = 0;
            _bitsLeft = 0;
        }

        public override byte ReadByte()
        {
            // if eos return 0
            if (BaseStream.Position == BaseStream.Length)
                return 0;

            return base.ReadByte();
        }

        public ushort ReadToken()
        {
            if ((NumBits >= 9) && NumBits <= 12)
            {
                ushort result = _remainder;
                int bitsLeft = NumBits - _bitsLeft;
                int bitOffset = _bitsLeft;
                _bitsLeft = 0;

                while (bitsLeft > 0)
                {
                    _remainder = ReadByte();
                    result |= (ushort)(_remainder << bitOffset);
                    bitsLeft -= 8;
                    bitOffset += 8;
                }
                _bitsLeft = (byte)-bitsLeft;
                _remainder >>= (8 - _bitsLeft);
                return (ushort)(result & _bitMasks[NumBits - 9]);
            }

            throw new InvalidOperationException("Invalid number of bits");
        }
    }
}
