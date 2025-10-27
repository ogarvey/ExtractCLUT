using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Prince
{
	public static class Decompressor
	{
		static byte _bitBuffer;

		static BinaryReader? _src;

		static readonly ushort[] table1 = new ushort[] {
			0x8000, 0x0002,
			0x4000, 0x0004,
			0x2000, 0x0008,
			0x1000, 0x0010,
			0x0800, 0x0020,
			0x0400, 0x0040,
			0x0200, 0x0080,
			0x0100, 0x0100,
			0x0080, 0x0200,
			0x0040, 0x0400
		};
		static readonly uint[] table2 = new uint[] { 
			0x0000F000,
			0x0020FC00,
			0x00A0FF00,
			0x02A0FF80,
			0x06A0FFC0,
			0x0EA0FFE0,
			0x1EA0FFF0,
			0x3EA0FFF8
		};
		static readonly ushort[] table3 = new ushort[] {
			0x8000, 0x0000,
			0x4000, 0x0002,
			0x2000, 0x0006,
			0x1000, 0x000E,
			0x0800, 0x001E,
			0x0400, 0x003E,
			0x0200, 0x007E,
			0x0100, 0x00FE,
			0x0080, 0x01FE,
			0x0040, 0x03FE,
			0x0020, 0x07FE,
			0x0010, 0x0FFE,
			0x0008, 0x1FFE,
			0x0004, 0x3FFE,
			0x0002, 0x7FFE,
			0x0001, 0xFFFE
		};

		public static byte[] Decompress(byte[] input, int outputSize)
		{
			_src = new BinaryReader(new MemoryStream(input));
			_bitBuffer = 0x80;

			byte[] dest = new byte[outputSize];
			int destPos = 0;

			while (destPos < outputSize-1)
			{
				uint ebp;
				ushort offset, length;

				if (ReadBit() != 0)
				{
					if (ReadBit() != 0)
					{
						if (ReadBit() != 0)
						{
							if (ReadBit() != 0)
							{
								if (ReadBit() != 0)
								{
									if (ReadBit() != 0)
									{
										uint tableIndex = 0;
										while (ReadBit() != 0)
											tableIndex++;

										length = table3[tableIndex * 2 + 0];
										bool more;
										do
										{
											more = (length & 0x8000) == 0;
											length = (ushort)((length << 1) | ReadBit());
										} while (more);

										length += table3[tableIndex * 2 + 1];
										length++;

										// Copy literal bytes
										for (int i = 0; i < length && destPos < outputSize; i++)
										{
											dest[destPos++] = _src.ReadByte();
										}
									}
									if (destPos < outputSize)
									{
										dest[destPos++] = _src.ReadByte();
									}
								}
								if (destPos < outputSize)
								{
									dest[destPos++] = _src.ReadByte();
								}
							}
							if (destPos < outputSize)
							{
								dest[destPos++] = _src.ReadByte();
							}
						}
						if (destPos < outputSize)
						{
							dest[destPos++] = _src.ReadByte();
						}
					}
					if (destPos < outputSize)
					{
						dest[destPos++] = _src.ReadByte();
					}
				}

				if (ReadBit() == 0)
				{
					if (ReadBit() != 0)
					{
						uint tableIndex = (uint)ReadBit();
						tableIndex = (tableIndex << 1) | (uint)ReadBit();
						tableIndex = (tableIndex << 1) | (uint)ReadBit();
						ebp = table2[tableIndex];
						length = 1;
					}
					else
					{
						ebp = 0x0000FF00;
						length = 0;
					}
				}
				else
				{
					uint tableIndex = 0;
					while (ReadBit() != 0)
						tableIndex++;

					length = table1[tableIndex * 2 + 0];
					bool more;
					do
					{
						more = (length & 0x8000) == 0;
						length = (ushort)((length << 1) | ReadBit());
					} while (more);

					length += table1[tableIndex * 2 + 1];
					tableIndex = (uint)ReadBit();
					tableIndex = (tableIndex << 1) | (uint)ReadBit();
					tableIndex = (tableIndex << 1) | (uint)ReadBit();
					ebp = table2[tableIndex];
				}

				offset = (ushort)(ebp & 0xFFFF);
				bool more2;
				do
				{
					if (_bitBuffer == 0x80)
					{
						if (offset >= 0xFF00)
						{
							offset = (ushort)((offset << 8) | _src.ReadByte());
						}
					}
					more2 = (offset & 0x8000) != 0;
					offset = (ushort)((offset << 1) | ReadBit());
				} while (more2);

				offset += (ushort)(ebp >> 16);
				length += 2;

				while (length-- > 0)
				{
					if (destPos >= outputSize)
						return dest;

					if (destPos - offset >= 0)
						dest[destPos] = dest[destPos - offset];
					destPos++;
				}
			}

			return dest;
		}

		private static int ReadBit()
		{
			int bit = (_bitBuffer & 0x80) >> 7;
			_bitBuffer <<= 1;
			if (_bitBuffer == 0)
			{
				_bitBuffer = _src!.ReadByte();
				bit = (_bitBuffer & 0x80) >> 7;
				_bitBuffer <<= 1;
				_bitBuffer |= 0x01;
			}
			return bit;
		}
	}
}
