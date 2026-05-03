using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Voodoo
{
	public static class VoodooSpriteExtractor
	{
		private static StringBuilder _log = new StringBuilder();
		public static string GetLog() => _log.ToString();
		public static void ClearLog() => _log.Clear();

		private static void Log(string message)
		{
			_log.AppendLine(message);
			try { File.AppendAllText("sprite_debug.log", message + Environment.NewLine); } catch { }
		}

		public class SpriteFrame
		{
			public int Width { get; set; }
			public int Height { get; set; }
			public int OffsetX { get; set; }
			public int OffsetY { get; set; }
			public byte[] PixelData { get; set; } // 8-bit indexed
			public List<SubFrame> SubFrames { get; set; } = new List<SubFrame>();
		}

		public class SubFrame
		{
			public byte Type { get; set; }
			public int CoordinateCount { get; set; }
			public List<Point> Coordinates { get; set; } = new List<Point>();
			public byte[] RawData { get; set; }
			public byte[] DecodedPixels { get; set; }
			public int Width { get; set; }
			public int Height { get; set; }
		}

		public struct Point
		{
			public short X;
			public short Y;
		}

		/// <summary>
		/// Extracts a sprite frame from the decompressed Type 4 asset data.
		/// </summary>
		public static SpriteFrame ExtractFrame(byte[] assetData, int frameIndex)
		{
			if (assetData == null || assetData.Length < 4)
				throw new ArgumentException("Invalid asset data");

			using (var reader = new BinaryReader(new MemoryStream(assetData)))
			{
				// 1. Read Offset Table Offset
				int headerOffsetTableOffset = reader.ReadInt32();
				int dataOffsetTableOffset = reader.ReadInt32();
				// 2. Read Frame Offset
				reader.BaseStream.Seek(headerOffsetTableOffset + frameIndex * 4, SeekOrigin.Begin);
				int frameOffset = reader.ReadInt32();

				// 3. Go to Frame Data
				// The formula in ProcessSpriteData is: Base + OffsetTable[Index] + OffsetTableOffset
				// Wait, let's re-verify the formula:
				// puVar7 = (int)spriteData + *(int *)((int)spriteData + Index*4 + *spriteData) + *spriteData
				// Base + ReadInt32(Base + Index*4 + TableOffset) + TableOffset
				// So the offset in the table is relative to (Base + TableOffset).

				int absoluteFrameOffset = headerOffsetTableOffset + frameOffset;
				reader.BaseStream.Seek(absoluteFrameOffset, SeekOrigin.Begin);

				var frame = new SpriteFrame();

				// 4. Read Frame Header
				// uVar6 = CONCAT11(*puVar7, (char)((uint)in_EAX >> 8)) & 0xff80;
				// frameType = (byte)(uVar6 >> 8);
				// This part in ProcessSpriteData seems to calculate a global frame type or flags?
				// But the loop uses *pbVar1 for sub-frame type.

				// Let's look at the loop initialization:
				// frameCount = (uint)(byte)puVar7[1];
				// pbVar1 = puVar7 + 2;

				byte headerByte0 = reader.ReadByte(); // puVar7[0]
				byte frameCount = reader.ReadByte();  // puVar7[1]

				// Sub-frames start at offset 2

				for (int i = 0; i < frameCount; i++)
				{
					var subFrame = new SubFrame();

					// Sub-frame Header
					subFrame.Type = reader.ReadByte();
					subFrame.CoordinateCount = reader.ReadByte();

					// Skip 2 unknown bytes (pbVar1 + 4 is where coordinates start, so 2 bytes skipped)
					reader.ReadUInt16();

					// Read Coordinates
					for (int c = 0; c < subFrame.CoordinateCount; c++)
					{
						short x = reader.ReadInt16();
						short y = reader.ReadInt16();
						subFrame.Coordinates.Add(new Point { X = x, Y = y });
						Log($"    Coord {c}: {x}, {y}");
					}

					// The rest is Pixel Data.
					// get the offset from the dataOffsetTableOffset
					long currentPos = reader.BaseStream.Position;
					reader.BaseStream.Seek(dataOffsetTableOffset + frameIndex * 4, SeekOrigin.Begin);
					int dataOffset = reader.ReadInt32() + dataOffsetTableOffset;

					// Read Flags (2 bytes before DataOffset)
					reader.BaseStream.Seek(dataOffset - 2, SeekOrigin.Begin);
					ushort flags = reader.ReadUInt16();
					bool isMirrored = (flags & 0x8000) != 0;
					bool isTranslucent = (flags & 0x4000) != 0;

					Log($"Frame {frameIndex} SubFrame {i}: Offset={dataOffset:X} Flags={flags:X4} Mirrored={isMirrored} Translucent={isTranslucent}");

					reader.BaseStream.Seek(dataOffset, SeekOrigin.Begin);

					try
					{
						// Peek Width/Height
						short width = reader.ReadInt16();
						short height = reader.ReadInt16();
						Log($"  Dimensions: {width}x{height}");

						if (width > 0 && height > 0 && width < 2000 && height < 2000)
						{
							subFrame.Width = width;
							subFrame.Height = height;

							// Decode based on Type and Flags
							// Type 0: Simple (FUN_00426c18) - Blended/Shadow (Multi-Segment)
							// Type 1: Complex (FUN_0042666c) - Direct Copy (Multi-Segment)
							// Flag 0x4000: Translucent (FUN_00426914) - Raw Bitmap

							if (isTranslucent)
							{
								subFrame.DecodedPixels = DecodeRawFrame(reader, width, height);
							}
							else if (subFrame.Type == 1 || isMirrored)
							{
								subFrame.DecodedPixels = DecodeComplexFrame(reader, width, height, isMirrored);
							}
							else
							{
								subFrame.DecodedPixels = DecodeSimpleFrame(reader, width, height);
							}
						}
						else
						{
							// Not a standard bitmap header?
							throw new Exception("Invalid dimensions");
						}
					}
					catch
					{
						// Decoding failed, reset
						throw new Exception("Decoding failed");
					}

					frame.SubFrames.Add(subFrame);
				}

				return frame;
			}
		}

		private struct Segment
		{
			public byte Skip;
			public byte[] Data;
		}

		private static byte[] DecodeRawFrame(BinaryReader reader, int width, int height)
		{
			// FUN_00426914 (Translucent/Raw)
			// Raw bitmap data, 0 is transparent
			byte[] pixels = new byte[width * height];

			// Data is stored as raw bytes, row by row
			// 0x00 bytes are skipped (transparent)

			for (int row = 0; row < height; row++)
			{
				for (int col = 0; col < width; col++)
				{
					byte b = reader.ReadByte();
					if (b != 0)
					{
						pixels[row * width + col] = b;
					}
				}
			}
			return pixels;
		}

		private static byte[] DecodeSimpleFrame(BinaryReader reader, int width, int height)
		{
			// FUN_00426c18 (Simple/Blended)
			// Actually supports MULTIPLE segments per row, just like Complex!
			// The difference is that it blends pixels (FUN_00420d50) instead of direct copy.
			// For extraction, we treat it as direct copy.

			return DecodeComplexFrame(reader, width, height, false);
		}

		private static byte[] DecodeComplexFrame(BinaryReader reader, int width, int height, bool isMirrored)
		{
			// FUN_0042666c (Complex)
			// Format: [Count] ([Skip] [Run4] [Run1] [Data])... [EndSkip]
			// Ghidra analysis confirms:
			// - Skip byte simply advances the output pointer (pbVar15 += *pbVar12).
			// - This means it skips pixels, leaving them transparent (or whatever was in the buffer).
			// - It does NOT repeat the previous pixel.
			// - EndSkip also simply advances the pointer.

			byte[] pixels = new byte[width * height];
			Log($"  Decoding Complex Frame: {width}x{height} Mirrored={isMirrored}");

			for (int row = 0; row < height; row++)
			{
				byte segmentCount = reader.ReadByte();
				int currentX = 0;
				if (isMirrored) currentX = width;

				// Log($"    Row {row}: Segments={segmentCount} StartX={currentX}");

				for (int s = 0; s < segmentCount; s++)
				{
					byte skip = reader.ReadByte();
					byte run4 = reader.ReadByte();
					byte run1 = reader.ReadByte();

					int totalPixels = run4 * 4 + run1;
					byte[] data = reader.ReadBytes(totalPixels);

					// Log($"      Seg {s}: Skip={skip} Run4={run4} Run1={run1} Total={totalPixels}");
					if (skip > 0 && skip < 5) Log($"      Row {row} Seg {s}: Small Skip {skip} at X={currentX}");

					if (isMirrored)
					{
						// Right-to-Left Logic
						currentX -= skip;
						currentX -= totalPixels;

						if (currentX < 0) Log($"      ERROR: Row {row} Seg {s} Underflow! X={currentX}");

						Array.Reverse(data);
						WritePixels(pixels, width, height, currentX, row, data);
					}
					else
					{
						// Left-to-Right Logic
						currentX += skip;

						// Check for 0s in data
						// for(int k=0; k<data.Length; k++)
						//    if(data[k] == 0) Log($"      Row {row} Seg {s} contains 0 at index {k}");

						WritePixels(pixels, width, height, currentX, row, data);
						currentX += totalPixels;

						if (currentX > width) Log($"      ERROR: Row {row} Seg {s} Overflow! X={currentX}");
					}
				}

				byte endSkip = reader.ReadByte();
				int finalX = currentX;
				if (!isMirrored) finalX += endSkip;
				else finalX -= endSkip;

				// Check for alignment issues
				if (!isMirrored && finalX != width)
					Log($"    Row {row} Mismatch: EndX={finalX} Expected={width} EndSkip={endSkip}");
				else if (isMirrored && finalX != 0)
					Log($"    Row {row} Mismatch: EndX={finalX} Expected=0 EndSkip={endSkip}");
			}

			return pixels;
		}

		private static void WritePixels(byte[] buffer, int width, int height, int x, int y, byte[] data)
		{
			for (int i = 0; i < data.Length; i++)
			{
				if (x + i < width && y < height)
				{
					buffer[y * width + (x + i)] = data[i];
				}
			}
		}
	}
}
