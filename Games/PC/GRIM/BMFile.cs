using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.GRIM
{
	public class BMFile
	{
		public string FileName { get; set; }
		public int ImageCount { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public int X { get; set; }
		public int Y { get; set; }
		public int Format { get; set; }
		public int TextureCount { get; set; }
		public int Bpp { get; set; }
		public bool HasTransparency { get; set; }
		public List<byte[]> ImageArrays { get; set; } = new List<byte[]>();

		public BMFile(string fileName)
		{
			FileName = fileName;

			using var reader = new BinaryReader(File.OpenRead(fileName));
			if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "BM  ")
				throw new Exception("Invalid BM file");


			if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "F\0\0\0")
				throw new Exception("Invalid BM file");

			var codec = reader.ReadUInt32();

			reader.ReadUInt32();

			ImageCount = reader.ReadInt32();
			X = reader.ReadInt32();
			Y = reader.ReadInt32();

			reader.ReadUInt32();

			Format = reader.ReadInt32();
			Bpp = reader.ReadInt32();

			reader.BaseStream.Seek(0x80, SeekOrigin.Begin);
			Width = reader.ReadInt32();
			Height = reader.ReadInt32();

			reader.BaseStream.Seek(0x80, SeekOrigin.Begin);
			for (int i = 0; i < ImageCount; i++)
			{
				reader.BaseStream.Seek(0x8, SeekOrigin.Current);
				if (codec == 0)
				{
					var dsize = Bpp / 8 * Width * Height;
					var data = reader.ReadBytes(dsize);
					ImageArrays.Add(data);
				}
				else if (codec == 3)
				{
					var compressedSize = reader.ReadUInt32();
					var compressedData = reader.ReadBytes((int)compressedSize);
					var decompressedData = Decompress(compressedData, Bpp / 8 * Width * Height);
					ImageArrays.Add(decompressedData);
				}
				else
				{
					throw new Exception("Unknown codec");
				}
			}
		}

		private byte[] Decompress(byte[] compressed, int maxBytes)
		{
			int compressedIndex = 0;

			// Helper to read a 16-bit little-endian value from 'compressed' and advance compressedIndex
			int ReadLeUInt16()
			{
				if (compressedIndex + 1 >= compressed.Length)
					throw new EndOfStreamException("Not enough data to read UInt16");

				int val = compressed[compressedIndex] | (compressed[compressedIndex + 1] << 8);
				compressedIndex += 2;
				return val;
			}

			// Initialize bit stream
			int bitstr_value = ReadLeUInt16();
			int bitstr_len = 16;

			bool GetBit()
			{
				bool bit = (bitstr_value & 1) != 0;
				bitstr_len--;
				bitstr_value >>= 1;
				if (bitstr_len == 0)
				{
					bitstr_value = ReadLeUInt16();
					bitstr_len = 16;
				}
				return bit;
			}

			// The output buffer
			byte[] result = new byte[maxBytes];
			int resultIndex = 0; // Same as byteIndex in the original code

			// Begin decompression loop
			while (true)
			{
				bool bit = GetBit();
				if (bit)
				{
					// Direct copy of one byte
					if (resultIndex >= maxBytes)
					{
						throw new InvalidOperationException("Buffer overflow: attempted to write past the end of the output buffer.");
					}
					if (compressedIndex >= compressed.Length)
					{
						throw new EndOfStreamException("Not enough input data to read a byte.");
					}

					result[resultIndex++] = compressed[compressedIndex++];
				}
				else
				{
					// Compressed sequence
					bit = GetBit();
					int copy_len, copy_offset;

					if (!bit)
					{
						// copy_len and copy_offset are encoded in a smaller form
						bool b = GetBit();
						copy_len = 2 * (b ? 1 : 0); // bit was stored in 'b'
						b = GetBit();
						copy_len += (b ? 1 : 0) + 3;

						if (compressedIndex >= compressed.Length)
							throw new EndOfStreamException("Not enough input data to read offset byte.");

						// Casting to int because original code treats offset as signed
						copy_offset = (int)compressed[compressedIndex++] - 0x100;
					}
					else
					{
						// Larger offset form
						if (compressedIndex + 1 >= compressed.Length)
							throw new EndOfStreamException("Not enough input data to read offset bytes.");

						int c0 = compressed[compressedIndex];
						int c1 = compressed[compressedIndex + 1];
						copy_offset = (c0 | ((c1 & 0xF0) << 4)) - 0x1000;
						copy_len = (c1 & 0x0F) + 3;
						compressedIndex += 2;

						if (copy_len == 3)
						{
							if (compressedIndex >= compressed.Length)
								throw new EndOfStreamException("Not enough input data to read extended length.");

							copy_len = compressed[compressedIndex++] + 1;
							if (copy_len == 1)
							{
								// Done decompressing
								// Return only the actual decompressed data (trim if needed)
								if (resultIndex < maxBytes)
								{
									byte[] finalResult = new byte[resultIndex];
									Array.Copy(result, finalResult, resultIndex);
									return finalResult;
								}
								return result;
							}
						}
					}

					// Copy previously output data using the offset
					while (copy_len > 0)
					{
						if (resultIndex >= maxBytes)
						{
							throw new InvalidOperationException("Buffer overflow: attempted to write past the end of the output buffer.");
						}

						int sourceIndex = resultIndex + copy_offset;
						if (sourceIndex < 0 || sourceIndex >= maxBytes)
						{
							throw new InvalidOperationException($"Invalid offset: sourceIndex={sourceIndex}, copy_offset={copy_offset}, resultIndex={resultIndex}, maxBytes={maxBytes}");
						}

						// Since we're referencing previously decompressed data
						result[resultIndex] = result[sourceIndex];
						resultIndex++;
						copy_len--;
					}
				}
			}
		}
	}
}
