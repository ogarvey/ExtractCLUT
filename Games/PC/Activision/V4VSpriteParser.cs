using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Activision
{
	public static class PHEWHelper
	{

		public static Bitmap ParseLevelBackground(byte[] levelFileData, bool isPitfall = false)
		{
			var tileWidth = 8;
			var tileHeight = 8;

			ArgumentNullException.ThrowIfNull(levelFileData);

			if (levelFileData.Length < 20) // Minimum size for header
			{
				throw new ArgumentException("Level file data is too short to contain a valid header.", nameof(levelFileData));
			}

			int dataCursor = 0;

			// Read layer header data
			// Assuming Little Endian format like most Windows systems
			uint tileCCount = BitConverter.ToUInt16(levelFileData, dataCursor);
			dataCursor += isPitfall ? 4 : 2;
			uint tileRCount = BitConverter.ToUInt16(levelFileData, dataCursor);
			dataCursor += isPitfall ? 4 : 6;
			// Offsets are relative to the start of the segment (layerStart, which is 0 here)
			uint tilePoolOffset = BitConverter.ToUInt32(levelFileData, dataCursor);
			dataCursor += 4;
			uint layerPaletteOffset = BitConverter.ToUInt32(levelFileData, dataCursor);
			dataCursor += 4;
			uint paletteColorCount = BitConverter.ToUInt32(levelFileData, dataCursor);
			dataCursor += 4;
			int tileRefsOffset = dataCursor; // Position where tile references start

			// Basic offset validation
			if (layerPaletteOffset >= levelFileData.Length || tilePoolOffset >= levelFileData.Length || tilePoolOffset >= layerPaletteOffset)
			{
				throw new ArgumentException("Invalid offsets detected in header.", nameof(levelFileData));
			}

			// --- Load Palette ---
			// C# List uses 0-based indexing. We'll add transparent as the first color explicitly.
			List<Color> palette = new List<Color>();
			palette.Add(Color.FromArgb(0, 0, 0, 0)); // Index 0 is fully transparent

			dataCursor = (int)layerPaletteOffset; // Move cursor to palette data

			for (int p = 0; p < paletteColorCount; p++)
			{
				if (dataCursor + 3 > levelFileData.Length)
					throw new ArgumentException("Reached end of file while reading palette.", nameof(levelFileData));

				byte r = levelFileData[dataCursor];
				byte g = levelFileData[dataCursor + 1];
				byte b = levelFileData[dataCursor + 2];
				palette.Add(Color.FromArgb(255, r, g, b)); // Add as fully opaque RGB color
				dataCursor += 3;
			}

			// --- Load Tile Pool ---
			dataCursor = (int)tilePoolOffset; // Move cursor to tile pool data
			int tileSizeInBytes = tileWidth * tileHeight; // Each byte is a palette index
			int tilePoolSizeBytes = (int)(layerPaletteOffset - tilePoolOffset);

			if (tilePoolSizeBytes < 0 || tileSizeInBytes == 0)
			{
				throw new ArgumentException("Invalid tile pool size calculation based on offsets.", nameof(levelFileData));
			}
			if (tilePoolSizeBytes % tileSizeInBytes != 0)
			{
				// This might indicate an issue with the assumed tileWidth/tileHeight or the file format
				Console.WriteLine($"Warning: Tile pool size ({tilePoolSizeBytes}) is not an exact multiple of calculated tile data size ({tileSizeInBytes}).");
				// Allow continuing, but it might lead to errors later. A stricter check could throw here.
			}

			int tileCount = tilePoolSizeBytes / tileSizeInBytes;
			List<Bitmap> tilePool = new List<Bitmap>(tileCount);

			for (int t = 0; t < tileCount; t++)
			{
				// Create a bitmap for the tile with Alpha channel support
				Bitmap tileBitmap = new Bitmap(tileWidth, tileHeight, PixelFormat.Format32bppArgb);

				for (int r = 0; r < tileHeight; r++) // Tile row (y)
				{
					for (int c = 0; c < tileWidth; c++) // Tile column (x)
					{
						if (dataCursor >= levelFileData.Length)
							throw new ArgumentException("Reached end of file while reading tile pool data.", nameof(levelFileData));

						byte paletteIndex = levelFileData[dataCursor];

						if (paletteIndex >= palette.Count)
							throw new ArgumentException($"Invalid palette index {paletteIndex} encountered in tile data (Palette size: {palette.Count}).", nameof(levelFileData));

						// Get color from palette (index 0 is transparent, others map directly)
						Color pixelColor = palette[paletteIndex];
						tileBitmap.SetPixel(c, r, pixelColor); // SetPixel uses (x, y) order

						dataCursor++;
					}
				}
				tilePool.Add(tileBitmap);
			}

			// --- Build Composite Image ---
			int compositeWidth = (int)tileCCount * tileWidth;
			int compositeHeight = (int)tileRCount * tileHeight;

			if (compositeWidth <= 0 || compositeHeight <= 0)
			{
				throw new ArgumentException("Invalid composite image dimensions calculated.", nameof(levelFileData));
			}

			Bitmap compositeImage = new Bitmap(compositeWidth, compositeHeight, PixelFormat.Format32bppArgb);

			// Use Graphics object to draw tiles onto the composite image
			using (Graphics graphics = Graphics.FromImage(compositeImage))
			{
				// Optional: Set background to transparent or a default color if needed
				graphics.Clear(Color.Transparent);

				dataCursor = tileRefsOffset; // Move cursor to tile reference data

				for (int r = 0; r < tileRCount; r++) // Tile grid row
				{
					for (int c = 0; c < tileCCount; c++) // Tile grid column
					{
						if (dataCursor + 2 > levelFileData.Length)
							throw new ArgumentException("Reached end of file while reading tile references.", nameof(levelFileData));

						// Get tile entry (16 bits)
						ushort fullVal = BitConverter.ToUInt16(levelFileData, dataCursor);
						dataCursor += 2;

						// Extract index (lower 12 bits) - Assuming 0-based index in file
						int index = fullVal & 0x0FFF; // Mask for lower 12 bits

						if (index >= tilePool.Count)
							throw new ArgumentException($"Invalid tile index {index} encountered in map data (Tile pool size: {tilePool.Count}).", nameof(levelFileData));


						// Extract Flags (upper 4 bits)
						// Bit 13 -> flagsBits & 1
						// Bit 14 -> flagsBits & 2
						// Bit 15 -> flagsBits & 4
						// Bit 16 -> flagsBits & 8 (Sprite Priority - Ignored here)
						int flagsBits = (fullVal >> 12) & 0x0F; // Shift down 12, mask lowest 4
																										// bool fTilePriority = (flagsBits & 0b0001) != 0; // Ignored
						bool fVertFlip = (flagsBits & 0b0010) != 0;
						bool fHorzFlip = (flagsBits & 0b0100) != 0;
						// bool fSpritePriority = (flagsBits & 0b1000) != 0; // Ignored

						// Get referenced tile
						Bitmap baseTile = tilePool[index];
						Bitmap tileToDraw = baseTile; // Start with the original reference

						// --- Modify tile if needed ---
						// Clone the bitmap *only* if we need to flip it,
						// otherwise RotateFlip modifies the original in the pool.
						if (fVertFlip || fHorzFlip)
						{
							tileToDraw = (Bitmap)baseTile.Clone(); // Work on a copy

							if (fVertFlip && fHorzFlip)
								tileToDraw.RotateFlip(RotateFlipType.RotateNoneFlipXY);
							else if (fVertFlip)
								tileToDraw.RotateFlip(RotateFlipType.RotateNoneFlipY);
							else if (fHorzFlip)
								tileToDraw.RotateFlip(RotateFlipType.RotateNoneFlipX);
						}

						// Calculate position to draw the tile
						int destX = c * tileWidth;
						int destY = r * tileHeight;

						// Stitch tile to picture
						graphics.DrawImage(tileToDraw, destX, destY, tileWidth, tileHeight);

						// If we cloned the tile, dispose the clone after drawing
						if (tileToDraw != baseTile)
						{
							tileToDraw.Dispose();
						}
					}
				}
			} // Graphics object is disposed here

			// --- Cleanup Note ---
			// The bitmaps in tilePool are not explicitly disposed here.
			// They will be garbage collected. If this function were called
			// extremely frequently with large tile pools, managing their
			// disposal might be necessary, but usually GC is sufficient.

			return compositeImage; // Return the final assembled image
		}

		// Constants matching MATLAB hex2dec
		private const byte ROW_END_MARKER = 0x7F; // 127
		private const byte ROW_INC_MARKER = 0x7D; // 125
		private const byte ENTRY_END_MARKER = 0x7E; // 126
		private struct SpriteHeaderInfo
		{
			public uint Offset;
			public int Columns;
			public int Rows;
			public int XOffset;
			public int YOffset;
		}
		/// <summary>
		/// Parses sprite set data from a byte array, handling two potential formats.
		/// Returns a list of all sprites found as true-color Bitmaps.
		/// </summary>
		/// <param name="spriteFileData">Raw byte data for the sprite set.</param>
		/// <returns>A List of Bitmap objects, each representing a sprite.</returns>
		/// <exception cref="ArgumentNullException">Thrown if spriteFileData is null.</exception>
		/// <exception cref="ArgumentException">Thrown if file data is too short or contains invalid/inconsistent data.</exception>
		public static List<Bitmap> ParseSpriteSet(byte[] spriteFileData)
		{
			if (spriteFileData == null)
			{
				throw new ArgumentNullException(nameof(spriteFileData));
			}
			if (spriteFileData.Length < 16) // Absolute minimum for any header
			{
				throw new ArgumentException("Sprite file data is too short.", nameof(spriteFileData));
			}

			List<Bitmap> truecolorImages = new List<Bitmap>();
			int dataCursor = 0;
			List<Color> palette = new List<Color>();

			// Determine format version by checking for the ROW_END_MARKER (0x7F)
			bool isAdvancedFormat = spriteFileData.Contains(ROW_END_MARKER);

			// --- Common Palette Loading Logic ---
			void LoadPalette(int paletteOffset, ushort paletteColorCount)
			{
				palette.Clear();
				// Add transparent color at index 0
				palette.Add(Color.FromArgb(0, 0, 0, 0));

				dataCursor = paletteOffset + 3; // Move cursor to palette data in file

				for (int c = 1; c < paletteColorCount; c++)
				{
					if (dataCursor + 3 > spriteFileData.Length)
						throw new ArgumentException("Reached end of file while reading palette.", nameof(spriteFileData));

					byte r = spriteFileData[dataCursor];
					byte g = spriteFileData[dataCursor + 1];
					byte b = spriteFileData[dataCursor + 2];
					palette.Add(Color.FromArgb(255, r, g, b)); // Add as fully opaque RGB
					dataCursor += 3;
				}
			}

			// ================================================================
			// === Branch 1: Simpler Format (No 0x7F marker found)          ===
			// ================================================================
			if (!isAdvancedFormat)
			{
				// --- Parse Header (Simpler Format) ---
				dataCursor = 0;
				dataCursor += 4; // Skip segment signature (assuming 4 bytes)
				if (dataCursor + 12 > spriteFileData.Length) throw new ArgumentException("File too short for simple header.", nameof(spriteFileData));

				// Read offsets/counts - Assuming Little Endian
				uint paletteOffsetRaw = BitConverter.ToUInt32(spriteFileData, dataCursor);
				dataCursor += 4;
				ushort paletteColorCount = BitConverter.ToUInt16(spriteFileData, dataCursor);
				dataCursor += 2;
				ushort entryByteSize = BitConverter.ToUInt16(spriteFileData, dataCursor); // Size of one sprite's pixel data
				dataCursor += 2;
				ushort columns = BitConverter.ToUInt16(spriteFileData, dataCursor);
				dataCursor += 2;
				ushort rows = BitConverter.ToUInt16(spriteFileData, dataCursor);
				dataCursor += 2; // Header reading done, cursor is at 0x10 (16)

				// Validate Header Info
				if (paletteOffsetRaw >= spriteFileData.Length)
					throw new ArgumentException("Invalid palette offset in simple header.", nameof(spriteFileData));
				if (columns == 0 || rows == 0)
					throw new ArgumentException("Sprite dimensions cannot be zero in simple header.", nameof(spriteFileData));
				if (entryByteSize != (columns * rows))
				{
					throw new ArgumentException($"Entry byte size ({entryByteSize}) in simple header does not match columns*rows ({columns * rows})!", nameof(spriteFileData));
				}

				// --- Load Palette ---
				LoadPalette((int)paletteOffsetRaw, paletteColorCount);

				// --- Calculate Sprite Count & Process Entries ---
				int spriteDataStartOffset = 0x10; // Header size is fixed at 16 bytes
				int spriteDataEndOffset = (int)paletteOffsetRaw;
				int totalSpriteDataBytes = spriteDataEndOffset - spriteDataStartOffset;

				if (totalSpriteDataBytes < 0 || entryByteSize == 0)
					throw new ArgumentException("Invalid sprite data size calculated.", nameof(spriteFileData));
				if (totalSpriteDataBytes % entryByteSize != 0)
					Console.WriteLine($"Warning: Total sprite data size ({totalSpriteDataBytes}) is not an exact multiple of entry byte size ({entryByteSize}).");


				int entryCount = totalSpriteDataBytes / entryByteSize;
				dataCursor = spriteDataStartOffset; // Move cursor to start of first sprite's data

				for (int i = 0; i < entryCount; i++)
				{
					Bitmap spriteBitmap = new Bitmap(columns, rows, PixelFormat.Format32bppArgb);

					for (int r = 0; r < rows; r++)
					{
						for (int c = 0; c < columns; c++)
						{
							if (dataCursor >= spriteDataEndOffset || dataCursor >= spriteFileData.Length)
								throw new ArgumentException($"Reached end of sprite data unexpectedly at sprite {i}, pixel ({c},{r}).", nameof(spriteFileData));

							byte pixelIndex = spriteFileData[dataCursor];

							if (pixelIndex >= palette.Count)
								throw new ArgumentException($"Invalid palette index {pixelIndex} encountered (Palette size: {palette.Count}).", nameof(spriteFileData));

							// Index 0 is transparent (already added to palette list at index 0)
							// Other indices map directly
							spriteBitmap.SetPixel(c, r, palette[pixelIndex]);

							dataCursor++;
						}
					}
					truecolorImages.Add(spriteBitmap);
				}

				// Optional: Sanity check cursor position
				if (dataCursor != spriteDataEndOffset)
				{
					Console.WriteLine($"Warning: Cursor position ({dataCursor}) after reading simple format sprites does not match expected palette offset ({spriteDataEndOffset}).");
				}
			}
			// ================================================================
			// === Branch 2: Advanced Format (0x7F marker found)            ===
			// ================================================================
			else
			{
				// --- Parse Header (Advanced Format) ---
				dataCursor = 0;
				dataCursor += 4; // Skip segment signature
				if (dataCursor + 8 > spriteFileData.Length) throw new ArgumentException("File too short for advanced header base.", nameof(spriteFileData));

				uint paletteOffsetRaw = BitConverter.ToUInt32(spriteFileData, dataCursor); dataCursor += 4;
				ushort paletteColorCount = BitConverter.ToUInt16(spriteFileData, dataCursor); dataCursor += 2;
				ushort entryCount = BitConverter.ToUInt16(spriteFileData, dataCursor); dataCursor += 2;

				if (paletteOffsetRaw >= spriteFileData.Length)
					throw new ArgumentException("Invalid palette offset in advanced header.", nameof(spriteFileData));

				// --- Get Entry Offsets ---
				if (dataCursor + (entryCount * 4) > spriteFileData.Length)
					throw new ArgumentException("File too short to read all entry offsets.", nameof(spriteFileData));

				List<uint> entryOffsets = new List<uint>(entryCount);
				for (int e = 0; e < entryCount; e++)
				{
					entryOffsets.Add(BitConverter.ToUInt32(spriteFileData, dataCursor));
					dataCursor += 4;
				}

				// --- Load Palette ---
				LoadPalette((int)paletteOffsetRaw, paletteColorCount);

				// --- Read Each Entry ---
				for (int i = 0; i < entryCount; i++)
				{
					dataCursor = (int)entryOffsets[i];
					int entryStartCursor = dataCursor;

					// Read Entry Header
					if (dataCursor + 16 > spriteFileData.Length) // Need at least 4(w)+4(h)+8(unknown)
						throw new ArgumentException($"Entry {i} data is too short for header.", nameof(spriteFileData));

					uint columns = BitConverter.ToUInt32(spriteFileData, dataCursor); dataCursor += 4;
					uint rows = BitConverter.ToUInt32(spriteFileData, dataCursor); dataCursor += 4;
					dataCursor += 8; // Skip 8 unknown bytes

					if (columns == 0 || rows == 0)
					{
						Console.WriteLine($"Warning: Sprite entry {i} has zero dimensions ({columns}x{rows}). Skipping.");
						continue; // Skip processing this potentially invalid entry
					}

					Bitmap spriteBitmap = new Bitmap((int)columns, (int)rows, PixelFormat.Format32bppArgb);

					// Process Rows using RLE logic
					for (int r = 0; r < rows; r++)
					{
						int rowStartCursor = dataCursor;

						// Find end of row data (byte before ROW_END_MARKER or ENTRY_END_MARKER)
						int nextRowEndMarker = -1;
						int nextEntryEndMarker = -1;

						// Find next ROW_END_MARKER at or after current cursor
						for (int k = rowStartCursor; k < spriteFileData.Length; k++)
						{
							if (spriteFileData[k] == ROW_END_MARKER) { nextRowEndMarker = k; break; }
						}
						// Find next ENTRY_END_MARKER at or after current cursor
						for (int k = rowStartCursor; k < spriteFileData.Length; k++)
						{
							if (spriteFileData[k] == ENTRY_END_MARKER) { nextEntryEndMarker = k; break; }
						}

						int rowDataEndCursor = -1; // Index of the last byte of actual row data

						if (nextRowEndMarker != -1 && nextEntryEndMarker != -1)
							rowDataEndCursor = Math.Min(nextRowEndMarker, nextEntryEndMarker) - 1;
						else if (nextRowEndMarker != -1)
							rowDataEndCursor = nextRowEndMarker - 1;
						else if (nextEntryEndMarker != -1)
							rowDataEndCursor = nextEntryEndMarker - 1;
						else
							throw new ArgumentException($"Could not find row/entry end marker for sprite {i}, row {r}.", nameof(spriteFileData));

						// Check if row is empty
						if (rowDataEndCursor < rowStartCursor)
						{
							// Empty row, advance cursor past the marker
							dataCursor = rowDataEndCursor + 2;
							// Fill bitmap row with transparent
							for (int p = 0; p < columns; p++)
							{
								spriteBitmap.SetPixel(p, r, palette[0]); // palette[0] is Transparent
							}
							continue; // Move to next row
						}


						// Parse row data using RLE logic
						// Use List<byte?> where null represents a transparent pixel
						List<byte?> parsedRowPixels = new List<byte?>((int)columns);
						int pixOffsetMult = 1; // For ROW_INC_MARKER (0x7D)
						int alphaIndex = 0; // Index within the raw row data where next alpha control byte is expected

						for (int j = rowStartCursor; j <= rowDataEndCursor; j++)
						{
							byte currentByte = spriteFileData[j];

							if (currentByte == ROW_INC_MARKER) // 0x7D
							{
								int targetLength = ROW_INC_MARKER * pixOffsetMult; // 125, 250, ...
								int paddingNeeded = targetLength - parsedRowPixels.Count;
								if (paddingNeeded > 0)
								{
									for (int k = 0; k < paddingNeeded; k++) parsedRowPixels.Add(null); // Pad with transparency
								}
								pixOffsetMult++;
								if (j == rowStartCursor) // If 0x7D is the very first byte
								{
									alphaIndex = 1; // Alpha check still needs to happen on the next byte
								}
							}
							else
							{
								if (j == rowStartCursor + alphaIndex) // Is this the alpha control byte?
								{
									int alphaOffset = (int)currentByte - (int)ROW_END_MARKER; // Check relative to 0x7F (127)

									if (alphaOffset < 0) // Value < 127: This byte IS the count of transparent pixels
									{
										int transparentCount = currentByte;
										for (int k = 0; k < transparentCount; k++) parsedRowPixels.Add(null);
										alphaIndex++; // Next byte might also be alpha control
									}
									else // Value >= 127: This byte indicates the offset to the NEXT alpha control byte
									{
										// The +1 is because the offset is relative to the current byte's position *within the row data*
										alphaIndex = (j - rowStartCursor) + alphaOffset + 1;
									}
								}
								else // Regular pixel index byte
								{
									if (currentByte >= palette.Count)
										throw new ArgumentException($"Invalid palette index {currentByte} in sprite {i}, row {r} RLE data (Palette size: {palette.Count}).", nameof(spriteFileData));
									parsedRowPixels.Add(currentByte);
								}
							}
						} // End processing raw row bytes

						// Fill the bitmap row from parsedRowPixels, padding with transparency if needed
						for (int p = 0; p < columns; p++)
						{
							byte? colorIndex = (p < parsedRowPixels.Count) ? parsedRowPixels[p] : null;
							Color pixelColor = (colorIndex == null) ? palette[0] : palette[colorIndex.Value];
							spriteBitmap.SetPixel(p, r, pixelColor);
						}


						// Advance file cursor past the row data and its end marker
						dataCursor = rowDataEndCursor + 2;

					} // End row loop

					truecolorImages.Add(spriteBitmap);

					// Optional: Handle padding if entries need to be aligned (MATLAB code commented this out)
					// int entryLength = dataCursor - entryStartCursor;
					// int remainder = entryLength % 4;
					// if (remainder != 0) dataCursor += (4 - remainder);

				} // End entry loop
			}

			return truecolorImages;
		}

		/// <summary>
		/// Parses sprite set data from a byte array, handling two potential formats
		/// and applying X/Y offsets for correct alignment in the advanced format.
		/// Returns a list of all sprites found as true-color Bitmaps.
		/// </summary>
		/// <param name="spriteFileData">Raw byte data for the sprite set.</param>
		/// <returns>A List of Bitmap objects, each representing an aligned sprite.</returns>
		/// <exception cref="ArgumentNullException">Thrown if spriteFileData is null.</exception>
		/// <exception cref="ArgumentException">Thrown if file data is too short or contains invalid/inconsistent data.</exception>
		public static List<Bitmap> ParseAlignedSpriteSet(byte[] spriteFileData)
		{
			if (spriteFileData == null) throw new ArgumentNullException(nameof(spriteFileData));
			if (spriteFileData.Length < 16) throw new ArgumentException("Sprite file data is too short.", nameof(spriteFileData));

			List<Bitmap> truecolorImages = new List<Bitmap>();
			int dataCursor = 0;
			List<Color> palette = new List<Color>();
			bool isAdvancedFormat = spriteFileData.Contains(ROW_END_MARKER);

			// --- Common Palette Loading Logic --- (Same as before)
			void LoadPalette(int paletteOffset, ushort paletteColorCount, bool isFormat1 = false)
			{
				palette.Clear();
				//palette.Add(Color.FromArgb(0, 0, 0, 0)); // Index 0 = Transparent
				dataCursor = paletteOffset; // Use local cursor copy for safety if needed
				int initialCursor = dataCursor;
				for (int c = 0; c < paletteColorCount; c++)
				{
					if (initialCursor + (c * 3) + 3 > spriteFileData.Length) throw new ArgumentException($"Reached end of file while reading palette color {c}.", nameof(spriteFileData));
					byte r = spriteFileData[initialCursor + (c * 3)];
					byte g = spriteFileData[initialCursor + (c * 3) + 1];
					byte b = spriteFileData[initialCursor + (c * 3) + 2];
					palette.Add(Color.FromArgb(255, r, g, b));
				}
				// dataCursor is *not* advanced globally here, only used locally
			}


			// ================================================================
			// === Branch 1: Simpler Format (No Alignment Changes Needed)   ===
			// ================================================================
			if (!isAdvancedFormat)
			{
				// (Simple format parsing remains the same - no changes needed)
				dataCursor = 0;
				dataCursor += 4; // Skip segment signature
				if (dataCursor + 12 > spriteFileData.Length) throw new ArgumentException("File too short for simple header.", nameof(spriteFileData));
				uint paletteOffsetRaw = BitConverter.ToUInt32(spriteFileData, dataCursor); dataCursor += 4;
				ushort paletteColorCount = BitConverter.ToUInt16(spriteFileData, dataCursor); dataCursor += 2;
				ushort entryByteSize = BitConverter.ToUInt16(spriteFileData, dataCursor); dataCursor += 2;
				ushort columns_ushort = BitConverter.ToUInt16(spriteFileData, dataCursor); dataCursor += 2;
				ushort rows_ushort = BitConverter.ToUInt16(spriteFileData, dataCursor); dataCursor += 2;
				int columns = columns_ushort;
				int rows = rows_ushort;

				if (paletteOffsetRaw >= spriteFileData.Length) throw new ArgumentException("Invalid palette offset in simple header.", nameof(spriteFileData));
				if (entryByteSize != (columns * rows) && !(columns <= 0 || rows <= 0))
				{
					// Allow mismatch if dimensions are zero, otherwise error
					throw new ArgumentException($"Entry byte size ({entryByteSize}) in simple header does not match columns*rows ({columns * rows})!", nameof(spriteFileData));
				}

				LoadPalette((int)paletteOffsetRaw, paletteColorCount, true);

				int spriteDataStartOffset = 0x10;
				int spriteDataEndOffset = (int)paletteOffsetRaw;
				int totalSpriteDataBytes = spriteDataEndOffset - spriteDataStartOffset;
				if (totalSpriteDataBytes < 0) throw new ArgumentException("Invalid sprite data size calculated (negative).", nameof(spriteFileData));

				int entryCount = 0;
				if (columns > 0 && rows > 0 && entryByteSize > 0)
				{
					entryCount = totalSpriteDataBytes / entryByteSize;
					if (totalSpriteDataBytes % entryByteSize != 0) Console.WriteLine($"Warning: Simple format total sprite data size ({totalSpriteDataBytes}) is not exact multiple of entry size ({entryByteSize}).");
				}
				else if (totalSpriteDataBytes > 0 && (columns <= 0 || rows <= 0))
				{
					Console.WriteLine($"Warning: Simple format sprite data exists ({totalSpriteDataBytes} bytes) but calculated entry size is 0 or dimensions are 0.");
				}


				dataCursor = spriteDataStartOffset;

				for (int i = 0; i < entryCount; i++)
				{
					if (columns <= 0 || rows <= 0)
					{ // Should be caught by entryCount logic, but belt-and-braces
						// Skip data for zero-dim entry if entryByteSize was > 0
						if (entryByteSize > 0) dataCursor += entryByteSize;
						continue;
					}
					Bitmap spriteBitmap = new Bitmap(columns, rows, PixelFormat.Format32bppArgb);
					for (int r = 0; r < rows; r++)
					{
						for (int c = 0; c < columns; c++)
						{
							if (dataCursor >= spriteDataEndOffset || dataCursor >= spriteFileData.Length) throw new ArgumentException($"Reached end of simple sprite data unexpectedly at sprite {i}, pixel ({c},{r}).", nameof(spriteFileData));
							byte pixelIndex = spriteFileData[dataCursor++]; // Read and advance cursor
							if (pixelIndex >= palette.Count) pixelIndex = (byte)(palette.Count % pixelIndex);
							spriteBitmap.SetPixel(c, r, palette[pixelIndex]);
						}
					}
					truecolorImages.Add(spriteBitmap);
				}
			}
			// ================================================================
			// === Branch 2: Advanced Format (Unified Frame Implementation) ===
			// ================================================================
			else
			{
				// --- Read Base Header ---
				dataCursor = 0;
				dataCursor += 4; // Skip segment signature
				if (dataCursor + 8 > spriteFileData.Length) throw new ArgumentException("File too short for advanced header base.", nameof(spriteFileData));
				uint paletteOffsetRaw = BitConverter.ToUInt32(spriteFileData, dataCursor); dataCursor += 4;
				ushort paletteColorCount = BitConverter.ToUInt16(spriteFileData, dataCursor); dataCursor += 2;
				ushort entryCount = BitConverter.ToUInt16(spriteFileData, dataCursor); dataCursor += 2;
				if (paletteOffsetRaw >= spriteFileData.Length) throw new ArgumentException("Invalid palette offset in advanced header.", nameof(spriteFileData));

				int globalFrameMinX = 0;
				int globalFrameMinY = 0;
				int globalFrameMaxX = 0;
				int globalFrameMaxY = 0;
				List<SpriteHeaderInfo> headers = new List<SpriteHeaderInfo>(entryCount);
				int headerBaseCursor = dataCursor; // Position after base header, before offsets

				// --- Load Palette --- (Load it once upfront)
				LoadPalette((int)paletteOffsetRaw, paletteColorCount);

				// --- Pre-Pass: Read all entry headers and find global bounds ---

				if (headerBaseCursor + (entryCount * 4) > spriteFileData.Length) throw new ArgumentException("File too short to read all entry offsets.", nameof(spriteFileData));

				for (int i = 0; i < entryCount; i++)
				{
					// Read offset first
					uint entryOffset = BitConverter.ToUInt32(spriteFileData, headerBaseCursor + (i * 4));
					if (entryOffset + 16 > spriteFileData.Length) throw new ArgumentException($"Entry {i} offset {entryOffset} is invalid or too close to end of file.", nameof(spriteFileData));

					// Read individual header fields
					int entryHeaderCursor = (int)entryOffset;
					int cols = (int)BitConverter.ToUInt32(spriteFileData, entryHeaderCursor); entryHeaderCursor += 4;
					int rows = (int)BitConverter.ToUInt32(spriteFileData, entryHeaderCursor); entryHeaderCursor += 4;
					int xOff = BitConverter.ToInt32(spriteFileData, entryHeaderCursor); entryHeaderCursor += 4;
					int yOff = BitConverter.ToInt32(spriteFileData, entryHeaderCursor); entryHeaderCursor += 4; // Cursor now after offset y

					headers.Add(new SpriteHeaderInfo { Offset = entryOffset, Columns = cols, Rows = rows, XOffset = xOff, YOffset = yOff });

					// Update global bounds
					globalFrameMinX = Math.Min(globalFrameMinX, xOff);
					globalFrameMinY = Math.Min(globalFrameMinY, yOff);
					globalFrameMaxX = Math.Max(globalFrameMaxX, xOff + cols);
					globalFrameMaxY = Math.Max(globalFrameMaxY, yOff + rows);
				}

				// --- Calculate Unified Frame Dimensions ---
				// Add 1x1 minimum size to handle cases where all sprites might have 0 dimension
				int unifiedFrameWidth = Math.Max(1, globalFrameMaxX - globalFrameMinX);
				int unifiedFrameHeight = Math.Max(1, globalFrameMaxY - globalFrameMinY);

				// --- Processing Pass: Process each entry using stored header info ---
				for (int i = 0; i < entryCount; i++)
				{
					SpriteHeaderInfo header = headers[i];
					dataCursor = (int)header.Offset + 16; // Start cursor after W, H, XOff, YOff

					// Create bitmap with UNIFIED dimensions
					Bitmap spriteBitmap = new Bitmap(unifiedFrameWidth, unifiedFrameHeight, PixelFormat.Format32bppArgb);
					using (Graphics g = Graphics.FromImage(spriteBitmap)) { g.Clear(Color.Transparent); }

					// Process only if sprite has dimensions
					if (header.Columns > 0 && header.Rows > 0)
					{
						// Process Rows using RLE logic
						for (int r = 0; r < header.Rows; r++)
						{
							int rowStartCursor = dataCursor;
							// Find end of row data (same logic as before)
							int nextRowEndMarker = -1, nextEntryEndMarker = -1;
							for (int k = rowStartCursor; k < spriteFileData.Length; k++) { if (spriteFileData[k] == ROW_END_MARKER) { nextRowEndMarker = k; break; } }
							for (int k = rowStartCursor; k < spriteFileData.Length; k++) { if (spriteFileData[k] == ENTRY_END_MARKER) { nextEntryEndMarker = k; break; } }
							int rowDataEndCursor = -1;
							if (nextRowEndMarker != -1 && nextEntryEndMarker != -1) rowDataEndCursor = Math.Min(nextRowEndMarker, nextEntryEndMarker) - 1;
							else if (nextRowEndMarker != -1) rowDataEndCursor = nextRowEndMarker - 1;
							else if (nextEntryEndMarker != -1) rowDataEndCursor = nextEntryEndMarker - 1;
							else throw new ArgumentException($"Could not find row/entry end marker for sprite {i}, row {r}.", nameof(spriteFileData));

							List<byte?> parsedRowPixels = new List<byte?>((int)header.Columns);
							if (rowDataEndCursor >= rowStartCursor)
							{ // Parse RLE (same logic as before)
								int pixOffsetMult = 1; 
								int alphaIndex = 0;
								for (int j = rowStartCursor; j <= rowDataEndCursor; j++)
								{
									byte currentByte = spriteFileData[j];
									if (currentByte == ROW_INC_MARKER)
									{
										int targetLength = ROW_INC_MARKER * pixOffsetMult; 
										int paddingNeeded = targetLength - parsedRowPixels.Count;
										if (paddingNeeded > 0) 
										{ 
											for (int k = 0; k < paddingNeeded; k++) parsedRowPixels.Add(null);
										}
										pixOffsetMult++; if (j == rowStartCursor) alphaIndex = 1;
									}
									else
									{
										if ((j - rowStartCursor) == alphaIndex) 
										{ 
											int alphaOffset = (int)currentByte - (int)ROW_END_MARKER; 
											if (alphaOffset < 0) 
											{ 
												int transparentCount = currentByte; 
												for (int k = 0; k < transparentCount; k++) parsedRowPixels.Add(null); 
												alphaIndex++; 
											} 
											else 
											{ 
												alphaIndex = (j - rowStartCursor) + alphaOffset + 1; 
											}
										}
										else 
										{ 
											if (currentByte >= palette.Count) throw new ArgumentException($"Invalid palette index {currentByte} in sprite {i}, row {r} RLE (Palette size: {palette.Count}).", nameof(spriteFileData)); parsedRowPixels.Add(currentByte); 
										}
									}
								}
							}

							// --- Fill the UNIFIED Bitmap Row applying offsets RELATIVE TO GLOBAL FRAME ---
							for (int p = 0; p < header.Columns; p++)
							{
								byte? colorIndex = (p < parsedRowPixels.Count) ? parsedRowPixels[p] : null;
								Color pixelColor; // Default to transparent

								// --- CHANGE: Determine color based on RLE result ---
								if (colorIndex == null)
								{
									// Null from RLE explicitly means transparent
									pixelColor = Color.Transparent; // Use Color.Transparent struct directly
								}
								else
								{
									// Non-null index from RLE maps directly to the loaded palette
									if (colorIndex.Value >= palette.Count)
									{
										throw new ArgumentException($"Invalid palette index {colorIndex.Value} derived from RLE for sprite {i}, pixel ({p},{r}) (Palette size: {palette.Count}).", nameof(spriteFileData));
									}
									// Index 0 now correctly maps to palette[0] (first color from file)
									pixelColor = palette[colorIndex.Value];
								}

								// Calculate destination coordinates within the UNIFIED bitmap frame
								// Destination = (Pixel's logical position) - (Global Frame's logical top-left corner)
								int destX = (header.XOffset + p) - globalFrameMinX;
								int destY = (header.YOffset + r) - globalFrameMinY;

								// Draw the pixel only if it's within the frame bounds
								if (destX >= 0 && destX < unifiedFrameWidth && destY >= 0 && destY < unifiedFrameHeight)
								{
									spriteBitmap.SetPixel(destX, destY, pixelColor);
								}
								else
								{
									// This case should ideally not happen if global bounds were calculated correctly, but log if it does.
									Console.WriteLine($"Warning: Calculated pixel ({destX},{destY}) out of unified bounds ({unifiedFrameWidth}x{unifiedFrameHeight}) for sprite {i}, pixel({p},{r}).");
								}
							}
							// Advance file cursor past the row data and its end marker
							dataCursor = rowDataEndCursor + 2;
						} // End row loop
					}
					else
					{
						// Handle case where sprite had 0 columns or rows - skip RLE parsing entirely
						// Advance cursor by finding the ENTRY_END_MARKER
						int endMarkerPos = -1;
						for (int k = dataCursor; k < spriteFileData.Length; k++)
						{
							if (spriteFileData[k] == ENTRY_END_MARKER) { endMarkerPos = k; break; }
						}
						if (endMarkerPos != -1)
						{
							dataCursor = endMarkerPos + 1; // Move past the end marker
						}
						else
						{
							Console.WriteLine($"Warning: Could not find ENTRY_END_MARKER after zero-dim sprite {i}. Subsequent parsing may fail.");
							// Optionally break or throw
						}
					}


					truecolorImages.Add(spriteBitmap);

				} // End entry loop
			}

			return truecolorImages;
		}
	}
}
