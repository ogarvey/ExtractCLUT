using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
	public static class NomadHelpers
	{
		// ...existing code...

		public static byte[] DecodeDel(byte[] compressedData)
		{
			using (var compressedStream = new MemoryStream(compressedData))
			{
				return DecodeDel(compressedStream);
			}
		}

		public static byte[] DecodeDel(Stream compressedStream)
		{
			using (var reader = new BinaryReader(compressedStream))
			using (var decompressedStream = new MemoryStream())
			{
				// Read header - width and height as 16-bit little-endian values
				ushort width = reader.ReadUInt16();
				ushort height = reader.ReadUInt16();

				// Delta table from the specification
				int[] deltaTable = { 0, 1, 2, 3, 4, 5, 6, 7, -8, -7, -6, -5, -4, -3, -2, -1 };

				byte? storedByte = null; // Store the current byte for nibble processing

				// Process data chunks
				while (reader.BaseStream.Position < reader.BaseStream.Length)
				{
					byte commandByte = reader.ReadByte();
					int commandType = commandByte & 0x03; // Lowest two bits

					switch (commandType)
					{
						case 0x00: // Delta encoded
							{
								// Determine sequence length by isolating top six bits
								int sequenceLength = commandByte >> 2;

								if (sequenceLength == 0)
									break;

								// First byte is copied directly from input stream
								if (reader.BaseStream.Position >= reader.BaseStream.Length)
									return decompressedStream.ToArray();

								byte firstByte = reader.ReadByte();
								decompressedStream.WriteByte(firstByte);

								// For each of the following bytes, use delta encoding
								int remainingBytes = sequenceLength - 1;

								for (int i = 0; i < remainingBytes; i++)
								{
									int deltaIndex;

									if (i % 2 == 0)
									{
										// Even index - read new byte, use high nibble
										if (reader.BaseStream.Position >= reader.BaseStream.Length)
											return decompressedStream.ToArray();

										storedByte = reader.ReadByte();
										deltaIndex = (storedByte.Value >> 4) & 0x0F;
									}
									else
									{
										// Odd index - use low nibble from stored byte
										if (storedByte.HasValue)
										{
											deltaIndex = storedByte.Value & 0x0F;
											storedByte = null;
										}
										else
										{
											return decompressedStream.ToArray();
										}
									}

									// Get delta value from table and add to last output byte
									int deltaValue = deltaTable[deltaIndex];
									byte lastByte = decompressedStream.GetBuffer()[decompressedStream.Length - 1];
									byte resultByte = (byte)(lastByte + deltaValue);

									decompressedStream.WriteByte(resultByte);
								}
								break;
							}

						case 0x01: // Repeat byte
							{
								int repeatCount = commandByte >> 2;
								if (reader.BaseStream.Position >= reader.BaseStream.Length)
									return decompressedStream.ToArray();

								byte valueToRepeat = reader.ReadByte();

								for (int i = 0; i < repeatCount; i++)
								{
									decompressedStream.WriteByte(valueToRepeat);
								}
								break;
							}

						case 0x02: // Advance output (0b10 binary = 2 decimal)
							{
								// Shift command byte right by two to isolate top six bits
								int advanceCount = commandByte >> 2;

								if (advanceCount != 0)
								{
									// Add advance count to output buffer (creates transparency)
									for (int i = 0; i < advanceCount; i++)
									{
										decompressedStream.WriteByte(0);
									}
								}
								else
								{
									// If top six bits are zero, read next byte for advance count
									if (reader.BaseStream.Position >= reader.BaseStream.Length)
										return decompressedStream.ToArray();

									byte nextByte = reader.ReadByte();
									for (int i = 0; i < nextByte; i++)
									{
										decompressedStream.WriteByte(0);
									}
								}
								break;
							}

						case 0x03: // Single copy (0b11 binary = 3 decimal)
							{
								if (reader.BaseStream.Position >= reader.BaseStream.Length)
									return decompressedStream.ToArray();

								decompressedStream.WriteByte(reader.ReadByte());
								break;
							}
					}
				}

				return decompressedStream.ToArray();
			}
		}

		// ...existing code...
		public static byte[] DecodeLZ(byte[] compressedData)
		{
			// Use a MemoryStream for easier reading
			using (var compressedStream = new MemoryStream(compressedData))
			{
				return DecodeLZ(compressedStream);
			}
		}

		/// <summary>
		/// Decodes a stream compressed with the specified modified 8-bit LZ algorithm.
		/// </summary>
		/// <param name="compressedStream">The stream to read compressed data from.</param>
		/// <returns>A byte array containing the decompressed data.</returns>
		public static byte[] DecodeLZ(Stream compressedStream)
		{
			// 1. Initialize the 4096-byte ring buffer
			const int bufferSize = 4096; // 0x1000
			var ringBuffer = new byte[bufferSize];
			Array.Fill(ringBuffer, (byte)0x20); // Initialize with 20h

			// 2. Initialize the offset pointer
			int ringBufferOffset = 0xFEE;

			using (var reader = new BinaryReader(compressedStream))
			using (var decompressedStream = new MemoryStream())
			{
				// Main decompression loop
				while (reader.BaseStream.Position < reader.BaseStream.Length)
				{
					// 3. Read the flag byte for the next 8 chunks
					byte flags = reader.ReadByte();

					// Process 8 chunks based on the flag byte
					for (int i = 0; i < 8; i++)
					{
						// Exit if we've reached the end of the input stream prematurely
						if (reader.BaseStream.Position >= reader.BaseStream.Length)
							break;

						// Check bits from LSB to MSB
						bool isLiteral = (flags & (1 << i)) != 0;

						if (isLiteral)
						{
							// Flag bit is 1: Chunk is a literal byte
							byte literal = reader.ReadByte();

							// Copy to output stream
							decompressedStream.WriteByte(literal);

							// Copy to ring buffer
							ringBuffer[ringBufferOffset] = literal;
							ringBufferOffset = (ringBufferOffset + 1) % bufferSize;
						}
						else
						{
							// Flag bit is 0: Chunk is a two-byte back-reference
							if (reader.BaseStream.Position + 1 >= reader.BaseStream.Length)
								break; // Not enough data for a back-reference

							ushort backRef = reader.ReadUInt16(); // Reads 2 bytes as little-endian

							// Decode the length and offset from the back-reference
							int length = (backRef >> 12) + 3; // Top 4 bits are length-3
							int sourceOffset = backRef & 0x0FFF; // Bottom 12 bits are offset

							// Copy data from the ring buffer to the output stream
							for (int j = 0; j < length; j++)
							{
								byte value = ringBuffer[(sourceOffset + j) % bufferSize];

								// Copy to output stream
								decompressedStream.WriteByte(value);

								// Copy to ring buffer at the current write position
								ringBuffer[ringBufferOffset] = value;
								ringBufferOffset = (ringBufferOffset + 1) % bufferSize;
							}
						}
					}
				}
				return decompressedStream.ToArray();
			}
		}

		public static byte[] DecodeRle(byte[] compressedData)
		{
			using (var compressedStream = new MemoryStream(compressedData))
			{
				return DecodeRle(compressedStream);
			}
		}

		public static byte[] DecodeRle(Stream compressedStream)
		{
			// Implementation of RLE decoding
			using (var reader = new BinaryReader(compressedStream))
			using (var decompressedStream = new MemoryStream())
			{
				while (reader.BaseStream.Position < reader.BaseStream.Length)
				{
					var controlByte = reader.ReadByte();
					var bit6 = controlByte.CheckBitStateEx(6);
					var bit7 = controlByte.CheckBitStateEx(7);
					if (bit7)
					{
						// transparency - insert 0x00 for hte count specified by the lower 7 bits
						var count = controlByte & 0x7F;
						decompressedStream.Write(new byte[count], 0, count);
					}
					else if (bit6)
					{
						// copy the next byte for the count specified by the lower 6 bits
						var count = controlByte & 0x3F;
						var value = reader.ReadByte();
						for (int i = 0; i < count; i++)
						{
							decompressedStream.WriteByte(value);
						}
					}
					else
					{
						// copy the next n bytes as is
						for (int i = 0; i < controlByte; i++)
						{
							if (reader.BaseStream.Position >= reader.BaseStream.Length)
								break; // Prevent reading beyond the end of the stream
							decompressedStream.WriteByte(reader.ReadByte());
						}
					}
				}
				return decompressedStream.ToArray();
			}
		}
	}

	public class NomadDatRecord
	{
		public bool IsCompressed { get; set; }
		public bool IsHeaderCompressed { get; set; }
		public uint UncompressedLength { get; set; }
		public uint CompressedLength { get; set; }
		public string Name { get; set; }
		public uint Offset { get; set; }

		public NomadDatRecord(BinaryReader reader)
		{
			var flags = reader.ReadUInt16();
			IsCompressed = flags.CheckBitState(8);
			IsHeaderCompressed = flags.CheckBitState(2);
			UncompressedLength = reader.ReadUInt32();
			CompressedLength = reader.ReadUInt32();
			var nameBytes = reader.ReadBytes(0xE);
			Name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
			Offset = reader.ReadUInt32();
		}
	}
}
