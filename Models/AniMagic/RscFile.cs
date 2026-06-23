using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ExtractCLUT.Models.AniMagic
{
	public class RscFileBase
	{
		protected string _filePath;
		protected uint _headerSize;
		protected ushort _headerCount;
		protected uint _headerOffsetAdjustment;
		protected List<(string HeaderType, uint HeaderOffset, uint HeaderSize)> _headerTypeAndOffset = [];
		protected byte _version;
		protected uint _bmpTableOffset;
		protected uint _bmpTableSize;
		protected uint _colTableOffset;
		protected uint _colTableSize;
		protected uint _wavTableOffset;
		protected uint _wavTableSize;
		protected uint _scrTableOffset;
		protected uint _scrTableSize;

		protected List<Image<Rgba32>> _bmpImages = [];
		protected List<Color> _palette = [];

		public RscFileBase(string filepath)
		{
			_filePath = filepath;
		}

		protected void ReadTableOffsets(BinaryReader rscReader)
		{
			for (var i = 0; i < _headerCount; i++)
			{

				var headerType = Encoding.UTF8.GetString(rscReader.ReadBytes(4)); // type of the header
				uint headerOffset = (uint)(rscReader.ReadUInt32() + _headerOffsetAdjustment); // offset of the header
				var prevHeader = _headerTypeAndOffset.LastOrDefault();
				var prevSize = (prevHeader != default) ? headerOffset - prevHeader.Item2 : 0;
				if (prevHeader != default && prevSize > 0)
					_headerTypeAndOffset[_headerTypeAndOffset.Count - 1] = (prevHeader.Item1, prevHeader.Item2, prevSize); // update the size of the previous header
				_headerTypeAndOffset.Add((headerType, headerOffset, 0));
			}

			_bmpTableOffset = _headerTypeAndOffset.FirstOrDefault(x => x.HeaderType == "BMAP").HeaderOffset; // offset of the BMP table
			_colTableOffset = _headerTypeAndOffset.FirstOrDefault(x => x.HeaderType == "CTBL").HeaderOffset; // offset of the color table
			_wavTableOffset = _headerTypeAndOffset.FirstOrDefault(x => x.HeaderType == "WAVT").HeaderOffset; // offset of the WAV table
			_scrTableOffset = _headerTypeAndOffset.FirstOrDefault(x => x.HeaderType == "SCRP").HeaderOffset; // offset of the SCR table
			_bmpTableSize = _headerTypeAndOffset.FirstOrDefault(x => x.HeaderType == "BMAP").HeaderSize; // size of the BMP table
			_colTableSize = _headerTypeAndOffset.FirstOrDefault(x => x.HeaderType == "CTBL").HeaderSize; // size of the color table
			_wavTableSize = _headerTypeAndOffset.FirstOrDefault(x => x.HeaderType == "WAVT").HeaderSize; // size of the WAV table
			_scrTableSize = _headerTypeAndOffset.FirstOrDefault(x => x.HeaderType == "SCRP").HeaderSize; // size of the SCR table
		}


		protected void ProcessColorTable()
		{
			// Implement color table processing logic here
			using var rscReader = new BinaryReader(File.OpenRead(_filePath));
			rscReader.BaseStream.Position = _colTableOffset;
			var paletteOffset = rscReader.ReadUInt32() + 0x12; // offset of the palette
			rscReader.BaseStream.Position = paletteOffset;
			var palData = rscReader.ReadBytes(0x300); // read the palette data
			_palette = ColorHelper.ConvertBytesToRgbIS(palData);
		}

		protected void ProcessWavTable()
		{
			// Implement WAV table processing logic here
		}

		protected void ProcessScrTable()
		{
			// Implement SCR table processing logic here
		}

		protected byte[] Decompress(Stream inputStream, int compressedDataSize, int expectedOutputSize)
		{
			if (inputStream == null)
				throw new ArgumentNullException(nameof(inputStream));
			if (!inputStream.CanRead)
				throw new ArgumentException("Stream must be readable.", nameof(inputStream));
			if (compressedDataSize < 0)
				throw new ArgumentOutOfRangeException(nameof(compressedDataSize), "Compressed data size cannot be negative.");
			if (expectedOutputSize < 0)
				throw new ArgumentOutOfRangeException(nameof(expectedOutputSize), "Expected output size cannot be negative.");
			if (expectedOutputSize == 0 && compressedDataSize == 0)
				return Array.Empty<byte>();


			byte[] outputBuffer = new byte[expectedOutputSize];
			int outputIndex = 0;
			int bytesToRead = compressedDataSize;

			using var reader = new BinaryReader(inputStream);

			// --- Main Decompression Loop ---
			while (bytesToRead > 0)
			{
				if (outputIndex >= expectedOutputSize && bytesToRead > 0)
				{
					throw new InvalidDataException($"Output buffer overrun. Exceeded expected size {expectedOutputSize} while {bytesToRead} input bytes remain.");
				}

				byte val;
				try { val = reader.ReadByte(); }
				catch (EndOfStreamException ex) { throw new InvalidDataException($"Stream ended unexpectedly while reading control byte. Expected {bytesToRead} more bytes.", ex); }
				bytesToRead--;

				if (val != 0xff)
				{
					// --- Literal Byte ---
					if (outputIndex >= expectedOutputSize)
					{
						throw new InvalidDataException($"Output buffer overrun on literal write. Exceeded expected size {expectedOutputSize}.");
					}
					outputBuffer[outputIndex++] = val;
				}
				else // val == 0xff : Compressed sequence marker
				{
					// --- Compressed Block ---
					byte countByte;
					try { countByte = reader.ReadByte(); }
					catch (EndOfStreamException ex) { throw new InvalidDataException($"Stream ended unexpectedly while reading count byte. Expected {bytesToRead} more bytes.", ex); }
					bytesToRead--;

					ushort step;
					bool stepIsTwoBytes = (countByte & 0x80) != 0; // Check MSB

					if (!stepIsTwoBytes) // MSB is 0: step is 1 byte
					{
						try { step = reader.ReadByte(); }
						catch (EndOfStreamException ex) { throw new InvalidDataException($"Stream ended unexpectedly while reading 1-byte step. Expected {bytesToRead} more bytes.", ex); }
						bytesToRead--;
					}
					else // MSB is 1: step is 2 bytes (Little Endian)
					{
						countByte = (byte)(countByte ^ 0x80); // Clear the MSB
						try { step = reader.ReadUInt16(); }
						catch (EndOfStreamException ex) { throw new InvalidDataException($"Stream ended unexpectedly while reading 2-byte step. Expected {bytesToRead} more bytes.", ex); }
						bytesToRead -= 2;
					}

					if (bytesToRead < 0)
					{
						throw new InvalidDataException($"Input stream read error. Consumed more bytes ({compressedDataSize - bytesToRead}) than expected ({compressedDataSize}).");
					}

					int copyLength = countByte + 4; // Calculate the actual copy length

					if (step + 1 > outputIndex)
					{
						throw new InvalidDataException($"Invalid back-reference. Step {step + 1} at output index {outputIndex} points before the start of the buffer.");
					}
					if (outputIndex + copyLength > expectedOutputSize)
					{
						throw new InvalidDataException($"Decompression error. Copying {copyLength} bytes at output index {outputIndex} would exceed the expected output size of {expectedOutputSize}.");
					}

					// --- Perform Back-Reference Copy ---
					for (int i = 0; i < copyLength; i++)
					{
						int sourceIndex = outputIndex - step - 1;
						outputBuffer[outputIndex] = outputBuffer[sourceIndex];
						outputIndex++;
					}
				}
			}

			if (bytesToRead != 0)
			{
				throw new InvalidDataException($"Decompression logic error or incorrect compressed size. Expected to read {compressedDataSize} bytes, but {Math.Abs(bytesToRead)} bytes {(bytesToRead < 0 ? "over-read" : "remain unread")}.");
			}

			if (outputIndex != expectedOutputSize)
			{
				throw new InvalidDataException($"Decompression finished, but produced {outputIndex} output bytes instead of the expected {expectedOutputSize}.");
			}



			return outputBuffer;
		}

		protected byte[] DecompressType45(Stream inputStream, int compressedDataSize, int expectedOutputSize)
		{
			if (inputStream == null)
				throw new ArgumentNullException(nameof(inputStream));
			if (!inputStream.CanRead)
				throw new ArgumentException("Stream must be readable.", nameof(inputStream));
			if (compressedDataSize < 2)
				throw new ArgumentOutOfRangeException(nameof(compressedDataSize), "Compressed data size must be at least 2 bytes for type 4/5 data.");
			if (expectedOutputSize < 0)
				throw new ArgumentOutOfRangeException(nameof(expectedOutputSize), "Expected output size cannot be negative.");
			if (expectedOutputSize == 0)
				return Array.Empty<byte>();

			byte[] outputBuffer = new byte[expectedOutputSize];
			int outputIndex = 0;
			int bytesRemaining = compressedDataSize;

			using var reader = new BinaryReader(inputStream);

			ushort bitBuffer;
			try { bitBuffer = reader.ReadUInt16(); }
			catch (EndOfStreamException ex)
			{
				throw new InvalidDataException("Stream ended unexpectedly while reading the initial bit buffer.", ex);
			}
			bytesRemaining -= 2;
			int bitsRemaining = 16;

			bool ReadBit()
			{
				if (bitsRemaining == 0)
				{
					if (bytesRemaining < 2)
						throw new InvalidDataException("Stream ended unexpectedly while refilling the bit buffer.");

					try { bitBuffer = reader.ReadUInt16(); }
					catch (EndOfStreamException ex)
					{
						throw new InvalidDataException("Stream ended unexpectedly while refilling the bit buffer.", ex);
					}

					bytesRemaining -= 2;
					bitsRemaining = 16;
				}

				bool bit = (bitBuffer & 1) != 0;
				bitBuffer >>= 1;
				bitsRemaining--;
				return bit;
			}

			byte ReadDataByte(string context)
			{
				if (bytesRemaining <= 0)
					throw new InvalidDataException($"Stream ended unexpectedly while reading {context}.");

				try
				{
					bytesRemaining--;
					return reader.ReadByte();
				}
				catch (EndOfStreamException ex)
				{
					throw new InvalidDataException($"Stream ended unexpectedly while reading {context}.", ex);
				}
			}

			while (outputIndex < expectedOutputSize)
			{
				if (!ReadBit())
				{
					outputBuffer[outputIndex++] = ReadDataByte("literal byte");
					continue;
				}

				if (!ReadBit())
				{
					int shortCopyLength = ((ReadBit() ? 1 : 0) << 1) | (ReadBit() ? 1 : 0);
					shortCopyLength += 2;
					byte offsetLow = ReadDataByte("short back-reference offset");
					int shortBackReferenceDistance = offsetLow + 1;

					if (shortBackReferenceDistance > outputIndex)
						throw new InvalidDataException($"Invalid back-reference. Distance {shortBackReferenceDistance} at output index {outputIndex} points before the start of the buffer.");
					if (outputIndex + shortCopyLength > expectedOutputSize)
						throw new InvalidDataException($"Decompression error. Copying {shortCopyLength} bytes at output index {outputIndex} would exceed the expected output size of {expectedOutputSize}.");

					int shortSourceIndex = outputIndex - shortBackReferenceDistance;
					for (int i = 0; i < shortCopyLength; i++)
					{
						outputBuffer[outputIndex++] = outputBuffer[shortSourceIndex++];
					}
					continue;
				}

				byte referenceLow = ReadDataByte("back-reference low byte");
				byte referenceHigh = ReadDataByte("back-reference high byte");
				int backReferenceDistance = (((referenceHigh & 0xF8) << 5) | referenceLow) + 1;
				int copyLength = referenceHigh & 0x07;

				if (copyLength == 0)
				{
					byte terminatorOrLength = ReadDataByte("back-reference length byte");
					if (terminatorOrLength == 0)
						break;

					copyLength = terminatorOrLength;
				}
				else
				{
					copyLength += 2;
				}

				if (backReferenceDistance > outputIndex)
					throw new InvalidDataException($"Invalid back-reference. Distance {backReferenceDistance} at output index {outputIndex} points before the start of the buffer.");
				if (outputIndex + copyLength > expectedOutputSize)
					throw new InvalidDataException($"Decompression error. Copying {copyLength} bytes at output index {outputIndex} would exceed the expected output size of {expectedOutputSize}.");

				int sourceIndex = outputIndex - backReferenceDistance;
				for (int i = 0; i < copyLength; i++)
				{
					outputBuffer[outputIndex++] = outputBuffer[sourceIndex++];
				}
			}

			if (outputIndex != expectedOutputSize)
			{
				throw new InvalidDataException($"Decompression finished, but produced {outputIndex} output bytes instead of the expected {expectedOutputSize}.");
			}

			return outputBuffer;
		}

		protected byte[] DecodeBmpData(byte compressionType, byte[] compressedData, int expectedOutputSize)
		{
			if (compressedData == null)
				throw new ArgumentNullException(nameof(compressedData));

			return compressionType switch
			{
				0 => compressedData,
				3 => Decompress(new MemoryStream(compressedData, writable: false), compressedData.Length, expectedOutputSize),
				4 => DecompressType45(new MemoryStream(compressedData, writable: false), compressedData.Length, expectedOutputSize),
				5 => DecompressType45(new MemoryStream(compressedData, writable: false), compressedData.Length, expectedOutputSize),
				_ => throw new InvalidDataException($"Unsupported BMP compression type {compressionType}.")
			};
		}

		public void ExportBmpImages(string outputDir)
		{
			if (!Directory.Exists(outputDir))
			{
				Directory.CreateDirectory(outputDir);
			}

			for (int i = 0; i < _bmpImages.Count; i++)
			{
				var image = _bmpImages[i];
				var outputFilePath = Path.Combine(outputDir, $"image_{i:D4}.png");
				image.Mutate(x => x.Flip(FlipMode.Vertical)); // Flip the image vertically
				image.SaveAsPng(outputFilePath);
				Console.WriteLine($"Saved image {i} to {outputFilePath}");
			}
		}
	}
}
