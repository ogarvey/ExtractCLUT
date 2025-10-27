using System;
using System.IO;

namespace ExtractCLUT.Games.Generic
{

	public static class CwcDecoder
	{
		/// <summary>
		/// Decode a CWC-compressed file and return the decompressed bytes.
		/// </summary>
		public static byte[] DecodeCwc(string filePath)
		{
			if (filePath is null) throw new ArgumentNullException(nameof(filePath));
			var data = File.ReadAllBytes(filePath);
			if (data.Length < 12) throw new InvalidDataException("File too small for CWC header.");

			// ---- Header: 3 bytes "CWC", 1 byte Bits, 4 bytes Len, 4 bytes Size (all LE) ----
			if (data[0] != (byte)'C' || data[1] != (byte)'W' || data[2] != (byte)'C')
				throw new InvalidDataException("Missing CWC signature.");

			int bits = data[3];                         // DecC_Bits
			int expandedLen = BitConverter.ToInt32(data, 4);   // DecC_Len
			int compSize = BitConverter.ToInt32(data, 8);   // DecC_Size

			if (expandedLen < 0) throw new InvalidDataException("Negative expanded length in header.");
			if (compSize < 0) throw new InvalidDataException("Negative compressed size in header.");
			if (12 + compSize > data.Length) throw new InvalidDataException("Compressed size exceeds file length.");

			var br = new BitReader(data, 12, 12 + compSize);   // confine reads to header+payload
			var output = new byte[expandedLen];
			int outPos = 0;

			const int RepMinSize = 2;

			// Precompute mask/shifter used by the long form (matches the self-modifying Masker/Shifter in asm)
			uint lenMask = bits <= 0 ? 0u : ((1u << bits) - 1u); // when bits==0, mask=0 and "length" is fixed (then +2 below)

			try
			{
				// Initialize the 32-bit bit-buffer like the asm does (loads first dword, sets remaining=32)
				br.PrimeBitBuffer();

				while (true)
				{
					// If we've reached the declared expanded length, we allow clean termination as well.
					if (outPos == expandedLen) break;

					// ---- Top-level branch ----
					if (br.ReadBit() == 1)
					{
						// Literal byte
						byte b = br.ReadU8();
						if (outPos >= expandedLen) throw new InvalidDataException("Output overflow on literal.");
						output[outPos++] = b;
						continue;
					}

					// second-level decision (invert: 0 goes deeper to @@2)
					if (br.ReadBit() == 0)
					{
						// third-level decision (invert: 0 goes deeper to @@3)
						if (br.ReadBit() == 0)
						{
							// @@3: long form
							uint v24 = br.ReadU24LE();
							int lenField = (int)(v24 & lenMask);
							int lenLong = lenField + 2;
							int posField = (int)(v24 >> bits);

							if (posField == 0)
							{
								if (lenLong >= RepMinSize + 15 + 1)
								{
									byte val = br.ReadU8();
									int runLen = lenLong + 1;
									EnsureRoom(output, outPos, runLen);
									FillRun(output, outPos, runLen, val);
									outPos += runLen;
									continue;
								}
								if (lenLong == RepMinSize + 2) continue;     // no-op
								if (lenLong == RepMinSize + 1) continue;     // refill (no-op here)
								if (lenLong == RepMinSize + 3)
								{
									int count = br.ReadU8();
									EnsureRoom(output, outPos, count);
									br.ReadBytes(output, outPos, count);
									outPos += count;
									continue;
								}
								if (lenLong == RepMinSize) break;            // terminator
								throw new InvalidDataException("CWC: invalid special code.");
							}
							else
							{
								int distance = (posField - 1) + lenLong;
								CopyFromHistory(output, ref outPos, lenLong, distance);
								continue;
							}
						}

						// @@2: medium copy/run (12-bit pos, 4-bit len)
						ushort ax = br.ReadU16LE();
						int len2 = (ax & 0xF) + 2;
						int pos2 = (ax >> 4);
						if (pos2 == 0)
						{
							byte val = br.ReadU8();
							int runLen = len2 + 1;
							EnsureRoom(output, outPos, runLen);
							FillRun(output, outPos, runLen, val);
							outPos += runLen;
						}
						else
						{
							int distance = (pos2 - 1) + len2;
							CopyFromHistory(output, ref outPos, len2, distance);
						}
						continue;
					}

					// @@1: short copy/run (8-bit pos, 2-bit len)
					int len1 = (br.ReadBit() << 1) | br.ReadBit();
					len1 += 2;
					int posByte = br.ReadU8();
					if (posByte == 0)
					{
						byte val = br.ReadU8();
						int runLen = len1 + 1;
						EnsureRoom(output, outPos, runLen);
						FillRun(output, outPos, runLen, val);
						outPos += runLen;
					}
					else
					{
						int distance = (posByte - 1) + len1;
						CopyFromHistory(output, ref outPos, len1, distance);
					}
					continue;
				}

				// Validate that we matched the declared expanded length, if provided.
				if (outPos != expandedLen)
					throw new InvalidDataException($"CWC: decompressed length mismatch (got {outPos}, expected {expandedLen}).");

				return output;
			}
			catch (EndOfStreamException e)
			{
				throw new InvalidDataException("CWC: unexpected end of compressed stream.", e);
			}
		}

		// ---- Helpers that mirror the assembly semantics ----

		private static void CopyFromHistory(byte[] dst, ref int outPos, int len, int distance)
		{
			if (len < 0) throw new InvalidDataException("Negative copy length.");
			if (distance <= 0) throw new InvalidDataException("Invalid back-reference distance.");
			if (outPos + len > dst.Length) throw new InvalidDataException("Output overflow on copy.");
			int src = outPos - distance;
			if (src < 0) throw new InvalidDataException("Back-reference points before start of output.");

			// rep movsb semantics → forward byte-by-byte (supports overlap)
			for (int i = 0; i < len; i++)
				dst[outPos++] = dst[src + i];
		}

		private static void FillRun(byte[] dst, int dstPos, int count, byte value)
		{
			for (int i = 0; i < count; i++) dst[dstPos + i] = value;
		}

		private static void EnsureRoom(byte[] dst, int pos, int count)
		{
			if (pos < 0 || count < 0 || pos + count > dst.Length)
				throw new InvalidDataException("Output overflow.");
		}

		// ---- Bit/byte reader that matches the macro _DCD_ReadBit pattern ----
		private sealed class BitReader
		{
			private readonly byte[] _buf;
			private readonly int _end;     // exclusive
			private int _idx;              // current byte index into _buf
			private uint _bitBuf;          // 32-bit shift register (MSB-first consumption)
			private int _bitsLeft;         // remaining bits in _bitBuf (0..32)

			public BitReader(byte[] buf, int start, int endExclusive)
			{
				_buf = buf;
				_idx = start;
				_end = endExclusive;
				_bitBuf = 0;
				_bitsLeft = 0;
			}

			public void PrimeBitBuffer()
			{
				// Matches: mov ebp,[esi]; add esi,4; dl=32
				_bitBuf = ReadU32LE();
				_bitsLeft = 32;
			}

			public int ReadBit()
			{
				// Matches adc ebp, ebp + carry behavior the asm uses to take MSB as the next bit,
				// but simplified: we output MSB, then shift left.
				if (_bitsLeft == 0)
				{
					_bitBuf = ReadU32LE();
					_bitsLeft = 32;
				}

				int bit = (int)(_bitBuf >> 31) & 1;
				_bitBuf <<= 1;
				_bitsLeft--;
				return bit;
			}

			public byte ReadU8()
			{
				if (_idx >= _end) throw new EndOfStreamException();
				return _buf[_idx++];
			}

			public ushort ReadU16LE()
			{
				if (_idx + 2 > _end) throw new EndOfStreamException();
				ushort v = BitConverter.ToUInt16(_buf, _idx);
				_idx += 2;
				return v;
			}

			public uint ReadU24LE()
			{
				if (_idx + 3 > _end) throw new EndOfStreamException();
				uint v = BitConverter.ToUInt32(new byte[] { _buf[_idx], _buf[_idx + 1], _buf[_idx + 2], 0 }, 0);
				_idx += 3;
				return v;
			}

			private uint ReadU32LE()
			{
				if (_idx + 4 > _end) throw new EndOfStreamException();
				uint v = (uint)BitConverter.ToInt32(_buf, _idx);
				_idx += 4;
				return v;
			}

			public void ReadBytes(byte[] dst, int dstOffset, int count)
			{
				if (_idx + count > _end) throw new EndOfStreamException();
				Buffer.BlockCopy(_buf, _idx, dst, dstOffset, count);
				_idx += count;
			}
		}

		/// <summary>
		/// Decodes a CWC compressed file and returns the decompressed data.
		/// </summary>
		/// <param name="filePath">The path to the CWC file.</param>
		/// <returns>A byte array containing the decompressed data.</returns>
		/// <exception cref="InvalidDataException">Thrown if the file is not a valid CWC format or contains corrupt data.</exception>
		/// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
		public static byte[] Decode(string filePath)
		{
			using var fileStream = File.OpenRead(filePath);
			using var reader = new BinaryReader(fileStream);

			// 1. Read Header
			byte[] id = reader.ReadBytes(3);
			if (System.Text.Encoding.ASCII.GetString(id) != "CWC")
			{
				throw new InvalidDataException("Not a valid CWC file: Magic ID 'CWC' not found.");
			}

			byte positionBits = reader.ReadByte();
			uint uncompressedLength = reader.ReadUInt32();
			uint compressedLength = reader.ReadUInt32();

			if (uncompressedLength == 0)
			{
				return Array.Empty<byte>();
			}

			// 2. Setup Unified Buffer (This is the key change)
			byte[] inputBuffer = reader.ReadBytes((int)compressedLength);
			int bufferIndex = 0; // Our new "esi" register

			// 3. Setup Decompression
			using var outputStream = new MemoryStream((int)uncompressedLength);
			var bitStream = new BitStreamReader();

			uint lengthMask = (1U << positionBits) - 1;
			const int repMinSize = 2;

			// 4. Main Decompression Loop (now using the unified buffer)
			while (outputStream.Position < uncompressedLength)
			{
				// Read control bit
				int bit;
				(bit, bufferIndex) = bitStream.ReadBit(inputBuffer, bufferIndex);

				if (bit == 1) // Code: 1 -> Literal byte
				{
					outputStream.WriteByte(inputBuffer[bufferIndex++]);
				}
				else
				{
					(bit, bufferIndex) = bitStream.ReadBit(inputBuffer, bufferIndex);
					if (bit == 1) // Code: 01 -> Short repeat/run
					{
						int length = bitStream.ReadBits(inputBuffer, ref bufferIndex, 2) + repMinSize;
						int position = inputBuffer[bufferIndex++];

						if (position == 0) // Run
						{
							byte value = inputBuffer[bufferIndex++];
							for (int i = 0; i < length + 1; i++) outputStream.WriteByte(value);
						}
						else // Repeat
						{
							// Using the standard offset calculation as the assembly's version is problematic
							CopyRepeat(outputStream, position, length);
						}
					}
					else
					{
						(bit, bufferIndex) = bitStream.ReadBit(inputBuffer, bufferIndex);
						if (bit == 1) // Code: 001 -> Medium repeat/run
						{
							ushort block = (ushort)(inputBuffer[bufferIndex++] | inputBuffer[bufferIndex++] << 8);
							int length = (block & 0x0F) + repMinSize;
							int position = block >> 4;

							if (position == 0) // Run
							{
								byte value = inputBuffer[bufferIndex++];
								for (int i = 0; i < length + 1; i++) outputStream.WriteByte(value);
							}
							else // Repeat
							{
								CopyRepeat(outputStream, position, length);
							}
						}
						else // Code: 000 -> Long repeat/run or special
						{
							uint block = (uint)(inputBuffer[bufferIndex++] | inputBuffer[bufferIndex++] << 8 | inputBuffer[bufferIndex++] << 16);
							int length = (int)(block & lengthMask) + repMinSize;
							int position = (int)(block >> positionBits);

							if (position == 0) // Special command
							{
								switch (length)
								{
									case repMinSize: // Terminator
										goto EndOfStream;
									case repMinSize + 3: // Literal string
										int literalLength = inputBuffer[bufferIndex++];
										outputStream.Write(inputBuffer, bufferIndex, literalLength);
										bufferIndex += literalLength;
										break;
									default: // Long run
										byte value = inputBuffer[bufferIndex++];
										for (int i = 0; i < length + 1; i++) outputStream.WriteByte(value);
										break;
								}
							}
							else // Long repeat
							{
								CopyRepeat(outputStream, position, length);
							}
						}
					}
				}
			}

		EndOfStream:
			if (outputStream.Position != uncompressedLength)
			{
				throw new InvalidDataException("Decompression failed: Output size does not match expected size.");
			}

			return outputStream.ToArray();
		}

		private static void CopyRepeat(MemoryStream stream, int offset, int length)
		{
			long currentPos = stream.Position;
			long readPos = currentPos - offset;

			if (readPos < 0) throw new InvalidDataException("Invalid repeat offset: Cannot read before the stream start.");

			// Byte-by-byte copy handles overlapping cases correctly.
			for (int i = 0; i < length; i++)
			{
				stream.Position = readPos + i;
				int byteToCopy = stream.ReadByte();
				stream.Position = currentPos + i;
				stream.WriteByte((byte)byteToCopy);
			}
			stream.Position = currentPos + length;
		}

		/// <summary>
		/// A stateless bit reader that operates on a shared buffer and index.
		/// It no longer performs any I/O itself.
		/// </summary>
		private class BitStreamReader
		{
			private uint _bitBuffer;
			private int _bitsLeft;

			public (int, int) ReadBit(byte[] buffer, int index)
			{
				if (_bitsLeft == 0)
				{
					// Refill the buffer from our shared byte array, advancing the shared index
					_bitBuffer = BitConverter.ToUInt32(buffer, index);
					index += 4;
					_bitsLeft = 32;
				}

				// Read MSB first, just like "adc ebp, ebp"
				int bit = (int)(_bitBuffer >> 31);
				_bitBuffer <<= 1;
				_bitsLeft--;

				return (bit, index);
			}

			public int ReadBits(byte[] buffer, ref int index, int count)
			{
				int result = 0;
				for (int i = 0; i < count; i++)
				{
					int bit;
					(bit, index) = ReadBit(buffer, index);
					result = (result << 1) | bit;
				}
				return result;
			}
		}
	}
}
