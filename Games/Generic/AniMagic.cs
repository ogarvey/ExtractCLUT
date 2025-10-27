using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using ImageMagick;

namespace ExtractCLUT.Games.Generic
{
	public class AniMagic
	{
		public static void ExtractIMG(string imgPath, List<Color> palette, bool isChill = false, bool isImages = false)
		{
			var outputDirectory = Path.Combine(Path.GetDirectoryName(imgPath), "img_output", Path.GetFileNameWithoutExtension(imgPath));
			Directory.CreateDirectory(outputDirectory);
			var imgFileData = File.ReadAllBytes(imgPath);

			var unmaskedOffsetCount = 0;
			var switchOffsetCount = 0;
			var maskedOffsetCount = 0;

			var unmaskedOffsets = new List<uint>();
			var switchOffsets = new List<uint>();
			var maskedOffsets = new List<uint>();

			if (isImages)
			{
				unmaskedOffsetCount = BitConverter.ToUInt16(imgFileData.Skip(2).Take(2).ToArray(), 0);
				switchOffsetCount = BitConverter.ToUInt16(imgFileData.Skip(4).Take(2).ToArray(), 0);
				maskedOffsetCount = BitConverter.ToUInt16(imgFileData.Skip(6).Take(2).ToArray(), 0);
				// find next 4 bytes which match the sequence 0x00 0x10 0x00 0x00
				var start = 0xa;
				while (start < imgFileData.Length)
				{
					if (imgFileData[start] == 0x00 && imgFileData[start + 1] == 0x10 && imgFileData[start + 2] == 0x00 && imgFileData[start + 3] == 0x00)
					{
						break;
					}
					start++;
				}
				for (int i = 0; i < unmaskedOffsetCount; i++)
				{
					var offset = BitConverter.ToUInt32(imgFileData.Skip(start + (i * 4)).Take(4).ToArray(), 0);
					unmaskedOffsets.Add(offset);
				}
				for (int i = 0; i < switchOffsetCount; i++)
				{
					var offset = BitConverter.ToUInt32(imgFileData.Skip(start + (unmaskedOffsetCount * 4) + (i * 4)).Take(4).ToArray(), 0);
					switchOffsets.Add(offset);
				}
				for (int i = 0; i < maskedOffsetCount; i++)
				{
					var offset = BitConverter.ToUInt32(imgFileData.Skip(start + (unmaskedOffsetCount * 4) + (switchOffsetCount * 4) + (i * 4)).Take(4).ToArray(), 0);
					maskedOffsets.Add(offset);
				}
			}
			else
			{
				unmaskedOffsetCount = BitConverter.ToUInt16(imgFileData.Skip(2).Take(2).ToArray(), 0);
				for (int i = 0; i < unmaskedOffsetCount; i++)
				{
					var offset = BitConverter.ToUInt32(imgFileData.Skip(4 + (i * 4)).Take(4).ToArray(), 0);
					unmaskedOffsets.Add(offset);
				}
			}



			for (int i = 0; i < unmaskedOffsetCount; i++)
			{
				var start = unmaskedOffsets[i];
				var end = (i == unmaskedOffsetCount - 1) ? (switchOffsetCount == 0 ? imgFileData.Length : (int)switchOffsets[0]) : (int)unmaskedOffsets[i + 1];
				var length = end - start;
				var data = imgFileData.Skip((int)start).Take((int)length).ToArray();
				if (isChill)
				{
					var subOffsets = new List<uint>();
					for (int j = 0; j < 0x1c; j += 4)
					{
						var subOffset = BitConverter.ToUInt32(data.Skip(j).Take(4).ToArray(), 0);
						subOffsets.Add(subOffset);
					}
					for (int j = 0; j < subOffsets.Count; j++)
					{
						var subStart = subOffsets[j];
						var subEnd = j == subOffsets.Count - 1 ? data.Length : (int)subOffsets[j + 1];
						var subLength = subEnd - subStart;
						var subData = data.Skip((int)subStart).Take((int)subLength).ToArray();
						var width = subLength switch
						{
							0x1 => 1,
							0x2 => 2,
							0x4 => 2,
							0x8 => 4,
							0x10 => 4,
							0x20 => 8,
							0x40 => 8,
							0x80 => 16,
							0x100 => 16,
							0x200 => 32,
							0x400 => 32,
							0x800 => 64,
							0x1000 => 64,
							0x2000 => 128,
							_ => 0
						};
						var height = subLength / width;
						//File.WriteAllBytes(Path.Combine(outputDirectory, $"{i}_{j}.bin"), subData);
						var image = ImageFormatHelper.GenerateClutImage(palette, subData, (int)width, (int)height);
						image.RotateFlip(RotateFlipType.Rotate90FlipNone);
						image.Save(Path.Combine(outputDirectory, $"{i}_{j}.png"), ImageFormat.Png);
					}
				}
				else
				{
					var width = Math.Sqrt(length);
					var image = ImageFormatHelper.GenerateClutImage(palette, data, (int)width, (int)width);
					// rotate image 90 degrees clockwise
					image.RotateFlip(RotateFlipType.Rotate90FlipNone);
					image.Save(Path.Combine(outputDirectory, $"{i}.png"), ImageFormat.Png);
				}
			}

			for (int i = 0; i < switchOffsetCount; i++)
			{
				var start = switchOffsets[i];
				var end = (i == switchOffsetCount - 1) ? (maskedOffsetCount == 0 ? imgFileData.Length : (int)maskedOffsets[0]) : (int)switchOffsets[i + 1];
				var length = end - start;
				var data = imgFileData.Skip((int)start).Take((int)length).ToArray();
				if (isChill)
				{
					var subOffsets = new List<uint>();
					for (int j = 0; j < 0x1c; j += 4)
					{
						var subOffset = BitConverter.ToUInt32(data.Skip(j).Take(4).ToArray(), 0);
						subOffsets.Add(subOffset);
					}
					for (int j = 0; j < subOffsets.Count; j++)
					{
						var subStart = subOffsets[j];
						var subEnd = j == subOffsets.Count - 1 ? data.Length : (int)subOffsets[j + 1];
						var subLength = subEnd - subStart;
						var subData = data.Skip((int)subStart).Take((int)subLength).ToArray();
						var width = subLength switch
						{
							0x2 => 2,
							0x8 => 4,
							0x20 => 8,
							0x80 => 16,
							0x100 => 16,
							0x200 => 32,
							0x400 => 32,
							0x800 => 64,
							0x1000 => 64,
							0x2000 => 128,
							_ => 0
						};
						var height = subLength / width;
						var image = ImageFormatHelper.GenerateClutImage(palette, subData, (int)width, (int)height);
						image.RotateFlip(RotateFlipType.Rotate90FlipNone);
						image.Save(Path.Combine(outputDirectory, $"inter_{i}_{j}.png"), ImageFormat.Png);
					}
				}
				else
				{
					var width = Math.Sqrt(length);
					var image = ImageFormatHelper.GenerateClutImage(palette, data, (int)width, (int)width);
					// rotate image 90 degrees clockwise
					image.RotateFlip(RotateFlipType.Rotate90FlipNone);
					image.Save(Path.Combine(outputDirectory, $"inter_{i}.png"), ImageFormat.Png);
				}
			}

			for (int i = 0; i < maskedOffsetCount; i++)
			{
				var start = maskedOffsets[i];
				var end = (i == maskedOffsetCount - 1) ? imgFileData.Length : (int)maskedOffsets[i + 1];
				var length = end - start;
				var imgData = imgFileData.Skip((int)start).Take((int)length).ToArray();
				if (isChill)
				{
					var subOffsets = new List<uint>();
					for (int j = 0; j < 0x1c; j += 4)
					{
						var subOffset = BitConverter.ToUInt32(imgData.Skip(j).Take(4).ToArray(), 0);
						subOffsets.Add(subOffset);
					}
					for (int j = 0; j < subOffsets.Count; j++)
					{
						var subStart = subOffsets[j];
						var subEnd = j == subOffsets.Count - 1 ? imgData.Length : (int)subOffsets[j + 1];
						var subLength = subEnd - subStart;
						var subData = imgData.Skip((int)subStart).Take((int)subLength).ToArray();
						// if (j == 0) File.WriteAllBytes(Path.Combine(outputDirectory, "bin", $"masked_{i}_{j}.bin"), subData);
						ExtractIMGSpriteChill(palette, outputDirectory, i, j, subData);
					}
				}
				else
				{
					try
					{
						ExtractIMGSpriteMeen(palette, outputDirectory, i, imgData, (int)start);
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Error extracting sprite {i} @ {start:X8}: {ex.Message}");
						// File.WriteAllBytes(Path.Combine(outputDirectory, "bin", $"masked_{i}.bin"), imgData);
						// throw;
					}
				}
			}
		}


		public static void ExtractLab(string inputPath)
		{
			var outputPath = Path.Combine(Path.GetDirectoryName(inputPath), "lab_output", Path.GetFileNameWithoutExtension(inputPath));
			Directory.CreateDirectory(outputPath);
			var data = File.ReadAllBytes(inputPath);
			if (data[0] == 0x0A || data[1] == 0x05 || data[2] == 0x01) return;
			// read 16 byte file details in loop until we get 16bytes of 0x00
			var index = 0;
			var offsets = new Dictionary<string, int>();
			while (data.Skip(index).Take(4).All(b => b != 0x00))
			{
				var fileDetails = data.Skip(index).Take(16).ToArray();
				var fileName = Encoding.ASCII.GetString(fileDetails.Take(12).ToArray()).TrimEnd('\0');
				var fiOffset = BitConverter.ToUInt32(fileDetails.Skip(12).Take(4).ToArray(), 0);
				// if filename exists, then we have a duplicate, so add a suffix
				if (offsets.ContainsKey(fileName))
				{
					var suffix = 1;
					while (offsets.ContainsKey($"{fileName}_{suffix}"))
					{
						suffix++;
					}
					fileName = $"{fileName}_{suffix}";
				}
				offsets.Add(fileName, (int)fiOffset);
				index += 16;
			}

			foreach (var offset in offsets)
			{
				var fileName = offset.Key;
				var fileOffset = offset.Value;
				var nextOffset = offsets.SkipWhile(o => o.Key != fileName).Skip(1).FirstOrDefault().Value;
				// if this is the last offset, then next offset is the end of the file
				if (nextOffset == 0)
				{
					nextOffset = data.Length;
				}
				var fileData = data.Skip(fileOffset).Take(nextOffset - fileOffset).ToArray();
				File.WriteAllBytes(Path.Combine(outputPath, fileName), fileData);
			}
		}
		// Reverse of DecodeMaskedV1 - encodes raw pixel data into I.M Meen's masked format
		public static byte[] EncodeMaskedV1(byte[] rawImageData, byte left, byte top, byte right, byte bottom)
		{
			// Validate input dimensions (64x64 expected)
			if (rawImageData.Length != 64 * 64)
				throw new ArgumentException("Raw image data must be 64x64 pixels", nameof(rawImageData));

			// Create result with dynamically sized list
			List<byte> result = new List<byte>();

			// 1. Add the header (left, top, right, bottom)
			result.Add(left);
			result.Add(top);
			result.Add(right);
			result.Add(bottom);

			// 2. Allocate space for footer offsets (will fill in later)
			int footerOffsetCount = (right + 1) - left;
			int pixelDataOffset = 4 + (footerOffsetCount * 2);

			// Reserve space for footer offsets
			for (int i = 0; i < footerOffsetCount; i++)
			{
				result.Add(0);
				result.Add(0);
			}

			// 3. Process the image line by line (vertical lines)
			List<ushort> footerOffsets = new List<ushort>();
			List<byte> pixelData = new List<byte>();
			List<byte> footer = new List<byte>();

			// Image data needs to be processed in columns, not rows
			// We start from 'left' and go to 'right' (vertical column processing)
			for (int x = left; x <= right; x++)
			{
				// Record the offset where this column's footer data will start
				footerOffsets.Add((ushort)result.Count);

				// Process this column
				List<(int start, int end, List<byte> pixels)> segments = new List<(int, int, List<byte>)>();
				int currentStart = -1;
				List<byte> currentPixels = new List<byte>();

				// Scan top to bottom in this column
				for (int y = 0; y < 64; y++)
				{
					byte pixel = rawImageData[y * 64 + x];

					if (pixel != 0) // Non-transparent pixel
					{
						if (currentStart == -1)
							currentStart = y;

						currentPixels.Add(pixel);
					}
					else if (currentStart != -1) // Transparent pixel after non-transparent segment
					{
						segments.Add((currentStart, currentStart + currentPixels.Count, new List<byte>(currentPixels)));
						currentStart = -1;
						currentPixels.Clear();
					}
				}

				// Handle the case where the column ends with non-transparent pixels
				if (currentStart != -1)
				{
					segments.Add((currentStart, currentStart + currentPixels.Count, new List<byte>(currentPixels)));
				}

				// Add footer entries for this column
				for (int i = 0; i < segments.Count; i++)
				{
					var segment = segments[i];

					// Add pixel data to the main pixel data section
					ushort pixelOffset = (ushort)pixelData.Count;
					pixelData.AddRange(segment.pixels);

					// Add footer entry
					result.AddRange(BitConverter.GetBytes((ushort)segment.end));
					result.AddRange(BitConverter.GetBytes((ushort)segment.start));
					result.AddRange(BitConverter.GetBytes(pixelOffset));

					// Last segment gets terminator, others get continue flag
					if (i == segments.Count - 1)
					{
						// End of column marker
						result.AddRange(BitConverter.GetBytes((ushort)0));
					}
					else
					{
						// Continue with next segment
						result.AddRange(BitConverter.GetBytes((ushort)segments[i + 1].end));
					}
				}

				// Empty column case
				if (segments.Count == 0)
				{
					// Add zeros for end marker
					result.AddRange(BitConverter.GetBytes((ushort)0));
				}
			}

			// 4. Add pixel data after all footer entries
			int pixelDataStartOffset = result.Count;
			result.AddRange(pixelData);

			// 5. Go back and fill in the footer offset values
			for (int i = 0; i < footerOffsets.Count; i++)
			{
				byte[] offsetBytes = BitConverter.GetBytes(footerOffsets[i]);
				result[4 + (i * 2)] = offsetBytes[0];
				result[5 + (i * 2)] = offsetBytes[1];
			}

			return result.ToArray();
		}

		/// <summary>
		/// Encodes an 8-bit CLUT image into I.M. Meen's masked image format
		/// </summary>
		/// <param name="imageData">Raw image data (8-bit CLUT format)</param>
		/// <param name="width">Width of the image (should be 64)</param>
		/// <param name="height">Height of the image (should be 64)</param>
		/// <returns>Encoded masked image data</returns>
		public static byte[] EncodeMaskedV1(byte[] image, int width, int height)
		{
			if (width != 64)
				throw new ArgumentException("Width must be 64 for I.M. Meen masked images", nameof(width));

			if (image.Length != width * height)
				throw new ArgumentException($"Image data must be {width * height} bytes for a {width}x{height} image", nameof(image));


			// image: 64*64 = 4096 bytes, row-major order
			const int size = 64;
			byte[,] img = new byte[size, size];
			for (int i = 0; i < size * size; i++)
				img[i / size, i % size] = image[i];

			// 1. Find bounds
			int left = size, right = -1, top = size, bottom = -1;
			for (int y = 0; y < size; y++)
				for (int x = 0; x < size; x++)
					if (img[y, x] != 0)
					{
						if (x < left) left = x;
						if (x > right) right = x;
						if (y < top) top = y;
						if (y > bottom) bottom = y;
					}
			if (left > right || top > bottom)
				left = top = 0; right = bottom = 0; // fully transparent

			int footerOffsetCount = (right + 1) - left;
			var footerOffsets = new List<ushort>();
			var pixelData = new List<byte>();
			var footers = new List<byte>();

			// Placeholder for header and footer offsets
			int headerSize = 4 + footerOffsetCount * 2;
			int pixelDataOffset = headerSize;

			// For each column
			for (int x = left; x <= right; x++)
			{
				footerOffsets.Add((ushort)(headerSize + pixelData.Count + footers.Count));
				int y = top;
				while (y <= bottom)
				{
					// Find start of run
					while (y <= bottom && img[y, x] == 0) y++;
					if (y > bottom) break;
					int runStart = y;
					// Find end of run
					while (y <= bottom && img[y, x] != 0) y++;
					int runEnd = y;
					// Write footer entry
					ushort end = (ushort)runEnd;
					ushort start = (ushort)runStart;
					ushort dataOffset = (ushort)(headerSize + pixelData.Count);
					ushort command = 0; // 0 = end of this column
					if (y <= bottom)
						command = (ushort)runEnd; // not used by decoder, but matches format
					footers.AddRange(BitConverter.GetBytes(end));
					footers.AddRange(BitConverter.GetBytes(start));
					footers.AddRange(BitConverter.GetBytes(dataOffset));
					footers.AddRange(BitConverter.GetBytes(command));
					// Write pixel data
					for (int yy = runStart; yy < runEnd; yy++)
						pixelData.Add(img[yy, x]);
				}
				// End of column: add 0 end marker if needed
				if (footers.Count == 0 || BitConverter.ToUInt16(footers.Skip(footers.Count - 8).Take(2).ToArray(), 0) != 0)
				{
					footers.AddRange(new byte[2]); // end marker (0)
				}
			}

			// Assemble output
			var output = new List<byte>();
			output.Add((byte)left);
			output.Add((byte)top);
			output.Add((byte)right);
			output.Add((byte)bottom);
			foreach (var offset in footerOffsets)
				output.AddRange(BitConverter.GetBytes(offset));
			output.AddRange(pixelData);
			output.AddRange(footers);

			return output.ToArray();
		}

		/// Gemini
		///  /// <summary>
		/// Encodes a 64x64 8-bit CLUT raw pixel data array into the Masked V1 format.
		/// </summary>
		/// <param name="rawPixelData">A byte array of 4096 bytes representing the 64x64 image.
		/// Value 0 is assumed to be transparent.</param>
		/// <returns>A byte array in the Masked V1 encoded format.</returns>
		public static byte[] EncodeMaskedV1(byte[] rawPixelData)
		{
			if (rawPixelData == null || rawPixelData.Length != 4096)
			{
				throw new ArgumentException("Input rawPixelData must be 4096 bytes (64x64).");
			}

			// 1. Determine true sprite boundaries
			byte minCol = 63, maxCol = 0;
			byte minRow = 63, maxRow = 0;
			bool hasPixels = false;

			for (int r = 0; r < 64; r++)
			{
				for (int c = 0; c < 64; c++)
				{
					if (rawPixelData[r * 64 + c] != 0) // Assuming 0 is transparent
					{
						hasPixels = true;
						if (c < minCol) minCol = (byte)c;
						if (c > maxCol) maxCol = (byte)c;
						if (r < minRow) minRow = (byte)r;
						if (r > maxRow) maxRow = (byte)r;
					}
				}
			}

			byte trueLeft, trueTop, trueRight, trueBottom;
			int footerOffsetCount;

			if (!hasPixels)
			{
				trueLeft = 0; trueTop = 0; trueRight = 0; trueBottom = 0;
				footerOffsetCount = 1; // One "empty" column to describe
			}
			else
			{
				trueLeft = minCol; trueTop = minRow;
				trueRight = maxCol; trueBottom = maxRow;
				footerOffsetCount = (trueRight + 1) - trueLeft;
			}

			List<byte> headerPortionBytes = new List<byte>();
			List<byte> consolidatedPixelDataBytes = new List<byte>();
			List<List<byte>> footerCommandsPerColumn = new List<List<byte>>();

			// File offset where the consolidated pixel data block will begin.
			int pixelDataBlockStartFileOffset = 4 + (footerOffsetCount * 2);
			int currentLocalOffsetInPixelBlock = 0;

			// 2. Process each column within the true sprite bounds
			for (int c = trueLeft; c <= trueRight; c++)
			{
				List<byte> currentColumnCommandListBytes = new List<byte>();
				var segmentsInColumn = new List<(ushort startRow, ushort endRow, List<byte> pixels)>();

				bool inSegment = false;
				ushort currentSegmentStartRow = 0;
				List<byte> currentSegmentPixelValues = new List<byte>();

				for (ushort r = 0; r < 64; r++) // Iterate down the column
				{
					byte pixelValue = rawPixelData[r * 64 + c];
					if (pixelValue != 0)
					{
						if (!inSegment)
						{
							inSegment = true;
							currentSegmentStartRow = r;
						}
						currentSegmentPixelValues.Add(pixelValue);
					}
					else
					{
						if (inSegment)
						{
							inSegment = false;
							segmentsInColumn.Add((currentSegmentStartRow, r, new List<byte>(currentSegmentPixelValues)));
							currentSegmentPixelValues.Clear();
						}
					}
				}
				if (inSegment) // If column ends with a non-transparent segment
				{
					segmentsInColumn.Add((currentSegmentStartRow, (ushort)64, new List<byte>(currentSegmentPixelValues)));
				}

				if (!segmentsInColumn.Any())
				{
					// Entirely transparent column within true bounds
					currentColumnCommandListBytes.AddRange(BitConverter.GetBytes((ushort)0)); // AA (pixelEnd)
					currentColumnCommandListBytes.AddRange(BitConverter.GetBytes((ushort)0)); // BB (pixelStart)
																																										// CC points to start of pixel block, but count will be 0 based on AA/BB
					currentColumnCommandListBytes.AddRange(BitConverter.GetBytes((ushort)pixelDataBlockStartFileOffset));
					currentColumnCommandListBytes.AddRange(BitConverter.GetBytes((ushort)0)); // XX (end of column commands)
				}
				else
				{
					for (int i = 0; i < segmentsInColumn.Count; i++)
					{
						var segment = segmentsInColumn[i];
						ushort aa_pixelEnd = segment.endRow;
						ushort bb_pixelStart = segment.startRow;

						ushort cc_pixelDataFileOffset = (ushort)(pixelDataBlockStartFileOffset + currentLocalOffsetInPixelBlock);
						consolidatedPixelDataBytes.AddRange(segment.pixels);
						currentLocalOffsetInPixelBlock += segment.pixels.Count;

						ushort xx_triggerCommand = (ushort)((i == segmentsInColumn.Count - 1) ? 0x0000 : 0xFFFF);

						currentColumnCommandListBytes.AddRange(BitConverter.GetBytes(aa_pixelEnd));
						currentColumnCommandListBytes.AddRange(BitConverter.GetBytes(bb_pixelStart));
						currentColumnCommandListBytes.AddRange(BitConverter.GetBytes(cc_pixelDataFileOffset));
						currentColumnCommandListBytes.AddRange(BitConverter.GetBytes(xx_triggerCommand));
					}
				}
				footerCommandsPerColumn.Add(currentColumnCommandListBytes);
			}

			// If !hasPixels, the loop (c=0 to 0) runs once, segmentsInColumn is empty, 
			// and the "Entirely transparent column" block adds one 8-byte command set.
			// This correctly populates footerCommandsPerColumn with one entry for the single "empty" column.

			// 3. Construct the header portion (Dimensions + Footer Offset Pointers)
			headerPortionBytes.Add(trueLeft);
			headerPortionBytes.Add(trueTop);
			headerPortionBytes.Add(trueRight);
			headerPortionBytes.Add(trueBottom);

			int currentFooterCommandBlockFileOffset = pixelDataBlockStartFileOffset + consolidatedPixelDataBytes.Count;

			for (int i = 0; i < footerOffsetCount; i++)
			{
				headerPortionBytes.AddRange(BitConverter.GetBytes((ushort)currentFooterCommandBlockFileOffset));
				// Ensure there's a corresponding command block (should always be true if footerOffsetCount matches processed columns)
				if (i < footerCommandsPerColumn.Count)
				{
					currentFooterCommandBlockFileOffset += footerCommandsPerColumn[i].Count;
				}
				else
				{
					// Should not happen: means footerOffsetCount is larger than the number of column command blocks generated.
					// This might imply an issue if an image was non-empty but resulted in footerOffsetCount=0,
					// but that's guarded by setting it to 1 for !hasPixels.
				}
			}

			// 4. Assemble the final byte array
			List<byte> finalEncodedData = new List<byte>();
			finalEncodedData.AddRange(headerPortionBytes);
			finalEncodedData.AddRange(consolidatedPixelDataBytes);
			foreach (var columnCommands in footerCommandsPerColumn)
			{
				finalEncodedData.AddRange(columnCommands);
			}

			return finalEncodedData.ToArray();
		}

		/// <summary>
		/// Encodes an 8-bit CLUT image into Chill Manor's masked image format
		/// </summary>
		/// <param name="imageData">Raw image data (8-bit CLUT format)</param>
		/// <param name="width">Width of the image</param>
		/// <param name="height">Height of the image</param>
		/// <param name="leftPadding">Left padding (usually 0)</param>
		/// <returns>Encoded masked image data</returns>
		public static byte[] EncodeMaskedV2(byte[] imageData, ushort width, ushort height, ushort leftPadding = 0)
		{
			using var memoryStream = new MemoryStream();
			using var writer = new BinaryWriter(memoryStream);

			// Determine top padding (count empty lines at the top)
			ushort topPadding = 0;
			for (int y = 0; y < height; y++)
			{
				bool hasNonZeroPixel = false;
				for (int x = 0; x < width; x++)
				{
					if (imageData[y * width + x] != 0)
					{
						hasNonZeroPixel = true;
						break;
					}
				}

				if (hasNonZeroPixel)
					break;

				topPadding++;
			}

			// Write header
			writer.Write(height);                      // Height
			writer.Write(width);                       // Width
			writer.Write(leftPadding);                 // Left padding
			writer.Write((ushort)0);                   // Unused
			writer.Write(topPadding);                  // Top padding
			writer.Write((ushort)(height));            // Footer offset count + top padding

			// Placeholder for first footer offset
			writer.Write((ushort)0);

			// Calculate and reserve space for footer offsets
			long footerOffsetStartPosition = memoryStream.Position;
			for (int y = 0; y < height - topPadding; y++)
			{
				writer.Write((ushort)0);  // Placeholder
			}

			// Save position for first line data
			ushort firstLineOffset = (ushort)memoryStream.Position;

			// Go back and write first footer offset
			long savedPosition = memoryStream.Position;
			memoryStream.Seek(0x0C, SeekOrigin.Begin);
			writer.Write(firstLineOffset);
			memoryStream.Seek(savedPosition, SeekOrigin.Begin);

			// Process and write each line
			for (int y = topPadding; y < height; y++)
			{
				// Save line start position
				ushort lineOffset = (ushort)memoryStream.Position;

				// Update footer offset for this line
				memoryStream.Seek(footerOffsetStartPosition + ((y - topPadding) * 2), SeekOrigin.Begin);
				writer.Write(lineOffset);
				memoryStream.Seek(lineOffset, SeekOrigin.Begin);

				// If line is empty, write 0xFF and continue
				bool lineIsEmpty = true;
				for (int w = 0; w < width; w++)
				{
					if (imageData[y * width + w] != 0)
					{
						lineIsEmpty = false;
						break;
					}
				}

				if (lineIsEmpty)
				{
					writer.Write((byte)0xFF);
					continue;
				}

				// Encode the line
				int x = leftPadding;
				while (x < width)
				{
					// Find transparent run
					int transparentStart = x;
					while (x < width && imageData[y * width + x] == 0)
						x++;

					int transparentCount = x - transparentStart;

					// Write transparent run if any
					if (transparentCount > 0)
					{
						while (transparentCount > 0)
						{
							int runLength = Math.Min(transparentCount, 127);
							writer.Write((byte)(runLength | 0x80)); // Set high bit for transparent run
							transparentCount -= runLength;
						}
					}

					// Find non-transparent run
					int pixelStart = x;
					while (x < width && imageData[y * width + x] != 0 && x - pixelStart < 127)
						x++;

					int pixelCount = x - pixelStart;

					// Write non-transparent run if any
					if (pixelCount > 0)
					{
						// Write count
						writer.Write((byte)pixelCount);

						// Write pixel data
						for (int i = 0; i < pixelCount; i++)
						{
							writer.Write(imageData[y * width + pixelStart + i]);
						}
					}
				}
			}

			return memoryStream.ToArray();
		}

		static void ExtractIMGSpriteMeen(List<Color> palette, string outputDirectory, int i, byte[] imgData, int offset)
		{
			var paddingBelow = imgData[2];
			var top = imgData[1];
			var paddingAbove = imgData[0];
			var bottom = imgData[3];

			var footerOffsetCount = (paddingBelow + 1) - paddingAbove;

			var footerOffsets = new List<ushort>();

			for (int j = 0; j < footerOffsetCount; j++)
			{
				var footerOffset = BitConverter.ToUInt16(imgData.Skip(4 + (j * 2)).Take(2).ToArray(), 0);
				footerOffsets.Add(footerOffset);
			}

			var imageLines = new List<byte[]>();

			for (int j = 0; j < paddingAbove; j++)
			{
				imageLines.Add(new byte[64]);
			}

			for (int j = 0; j < footerOffsets.Count; j++)
			{
				// ===== [Footer] =====
				// The footer is composed of the pixel lines.By which, I mean it tells the game where to put each pixel. Each section is formatted as such.

				// "AA 00 BB 00 CC 00 XX 00"

				// AA - The end of the pixel set on that vertical line.The exact spot the transparency starts again.
				// BB - The start of the pixel line.The exact spot of the first pixel in line.
				// CC - The offset of the pixels themselves.Read from Pixel Data. Add "1C000" to this byte.
				// XX - trigger / command.If it is 00, then there are no more pixels on that vertical line, move onto the next one. If it is not 00, treat it as the next command's AA and continue on the same line.
				var imageLineData = new byte[64];
				var currentOffset = footerOffsets[j];
				var nextOffset = j == footerOffsets.Count - 1 ? imgData.Length : footerOffsets[j + 1];
				var lineData = imgData.Skip(currentOffset).Take(nextOffset - currentOffset).ToArray();

				var command = -1;

				while (lineData.Length >= 6)
				{
					var pixelEnd = BitConverter.ToUInt16(lineData.Take(2).ToArray(), 0);
					if (pixelEnd == 0)
					{
						imageLines.Add(imageLineData);
						imageLineData = new byte[64];
						lineData = lineData.Skip(2).ToArray();
						continue;
					}
					var pixelStart = BitConverter.ToUInt16(lineData.Skip(2).Take(2).ToArray(), 0);
					var pixelOffset = BitConverter.ToUInt16(lineData.Skip(4).Take(2).ToArray(), 0);
					var pixelCount = pixelEnd - pixelStart;
					var pixelData = imgData.Skip(pixelOffset).Take(pixelCount).ToArray();
					// insert pixel data into imageLineData at pixelStart
					for (int k = 0; k < pixelCount; k++)
					{
						imageLineData[pixelStart + k] = pixelData[k];
					}
					command = BitConverter.ToUInt16(lineData.Skip(6).Take(2).ToArray(), 0);
					if (command == 0)
					{
						imageLines.Add(imageLineData);
						imageLineData = new byte[64];
						lineData = lineData.Skip(8).ToArray();
					}
					else
					{
						lineData = lineData.Skip(6).ToArray();
					}
				}
			}
			var imageBytes = imageLines.SelectMany(l => l).ToArray();
			var image = ImageFormatHelper.GenerateClutImage(palette, imageBytes, 64, 64, true);
			// rotate image 90 degrees clockwise
			image.RotateFlip(RotateFlipType.Rotate90FlipNone);
			image.Save(Path.Combine(outputDirectory, $"masked_{i}.png"), ImageFormat.Png);
		}

		static void ExtractIMGSpriteChill(List<Color> palette, string outputDirectory, int i, int i2, byte[] imgData)
		{
			if (imgData.Length <= 0xc || i2 > 0) return;
			var height = BitConverter.ToUInt16(imgData.Take(2).ToArray(), 0);
			if (height <= 1) return;
			var width = BitConverter.ToUInt16(imgData.Skip(2).Take(2).ToArray(), 0);
			var leftPadding = BitConverter.ToUInt16(imgData.Skip(4).Take(2).ToArray(), 0);
			var topPadding = BitConverter.ToUInt16(imgData.Skip(8).Take(2).ToArray(), 0);
			var footerOffsetCount = BitConverter.ToUInt16(imgData.Skip(0xa).Take(2).ToArray(), 0) - topPadding;
			var footerOffset = BitConverter.ToUInt16(imgData.Skip(0xc).Take(2).ToArray(), 0);

			var footerBytes = imgData.Skip(0xe).Take((footerOffsetCount) * 2).ToArray();
			var footerOffsets = new List<ushort>
						{
								footerOffset
						};

			for (int j = 0; j < footerOffsetCount; j++)
			{
				var offset = BitConverter.ToUInt16(footerBytes.Skip(j * 2).Take(2).ToArray(), 0);
				footerOffsets.Add(offset);
			}

			var imageLines = new List<byte[]>();

			if (topPadding > 0)
			{
				for (int j = 0; j < topPadding; j++)
				{
					imageLines.Add(new byte[width]);
				}
			}

			//var sb = new StringBuilder();
			for (int j = 0; j < footerOffsets.Count; j++)
			{
				var imageLineData = new byte[width];
				var currentOffset = footerOffsets[j];
				var nextOffset = j == footerOffsets.Count - 1 ? imgData.Length : footerOffsets[j + 1];
				var lineData = imgData.Skip(currentOffset).Take(nextOffset - currentOffset).ToArray();
				// // convert lineData to a string of bytes 
				// foreach (var b in lineData)
				// {
				//     sb.Append($"{b:X2} ");
				// }
				// sb.AppendLine();

				if (lineData[0] == 0xFF)
				{
					continue;
				}

				var lineDataIndex = 0;
				var imageLineDataIndex = leftPadding;

				var pixelsRemaining = 0;

				if (lineData[0] == 0)
				{
					pixelsRemaining = 128;
					lineDataIndex++;
				}

				while (lineDataIndex < lineData.Length && imageLineDataIndex < width)
				{
					if (((lineData[lineDataIndex] & 0x80) > 0) && pixelsRemaining == 0)
					{
						var count = (lineData[lineDataIndex] & 0x7f);
						// insert (lineData[index] & 0x7f) transparent pixels
						for (int k = 0; k < count; k++)
						{
							if (imageLineDataIndex >= width) break;
							imageLineData[imageLineDataIndex] = 0;
							imageLineDataIndex++;
						}
						lineDataIndex++;
					}
					else if (lineData[lineDataIndex] < 0x80 && pixelsRemaining == 0)
					{
						// the number of pixels following this byte
						pixelsRemaining = lineData[lineDataIndex] > 0 ? lineData[lineDataIndex] : width - imageLineDataIndex;
						lineDataIndex++;
					}
					else
					{
						// insert as pixel
						imageLineData[imageLineDataIndex++] = lineData[lineDataIndex++];
						pixelsRemaining--;
					}
				}
				imageLines.Add(imageLineData);
			}
			var imageBytes = imageLines.SelectMany(l => l).ToArray();
			// write sb to file
			//File.WriteAllText(Path.Combine(outputDirectory, $"masked_{i}_{i2}.txt"), sb.ToString());
			var image = ImageFormatHelper.GenerateClutImage(palette, imageBytes, width, height, true);
			// rotate image 90 degrees clockwise
			image.RotateFlip(RotateFlipType.Rotate90FlipNone);
			image.Save(Path.Combine(outputDirectory, $"masked_{i}_{i2}.png"), ImageFormat.Png);
		}

		public static void ExtractCMP(string cmpPath)
		{
			var cmpBytes = File.ReadAllBytes(cmpPath);
			// first four bytes is the filesize remaining
			var fileSize = BitConverter.ToUInt32(cmpBytes.Take(4).ToArray(), 0);
			cmpBytes = cmpBytes.Skip(4).ToArray();

		}
	}
}
