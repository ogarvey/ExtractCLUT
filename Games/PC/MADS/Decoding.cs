using System.Drawing;
using System.Drawing.Imaging;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.MADS
{
	public static class Decoding
	{
		public static byte[] ReadSprite(SpriteHeader ti, byte[] data, bool verbose = false)
		{
			if (ti.Width == 0 || ti.Height == 0)
			{
				throw new ArgumentException("Invalid sprite dimensions: Width and Height must be non-zero.");
			}

			var output = new byte[ti.Width * ti.Height];

			int bg = 0xFD; // Transparent background color

			if (verbose)
			{
				Console.WriteLine($"Sprite size = {ti.Width} x {ti.Height}");
			}

			int i = 0, j = 0, k = 0; // Track current positions

			void WritePixel(byte color, int length = 1)
			{
				while (length > 0)
				{
					if (color >= 0xfd)
					{
						//Console.WriteLine($"Writing pixel at ({i}, {j}) with color {color}");
						color=0xff;
					}

					output[j * ti.Width + i] = color;
					i++;
					length--;
				}
			}

			void NextLine()
			{
				i = 0;
				j++;
			}

			byte ReadByte()
			{
				if (k >= data.Length)
				{
					throw new IndexOutOfRangeException("Attempted to read beyond the data array.");
				}

				return data[k++];
			}

			byte ReadLineMode()
			{
				byte mode = ReadByte();
				if (verbose)
				{
					Console.WriteLine($"Line mode = {mode:X2}");
				}

				return mode;
			}

			byte lineMode = ReadLineMode();
			while (true)
			{
				if (lineMode == 0xFF)
				{
					// Fill the rest of the line with the background color
					WritePixel((byte)bg, ti.Width - i);
					NextLine();
					lineMode = ReadLineMode();
				}
				else if (lineMode == 0xFC)
				{
					// End of image
					break;
				}
				else
				{
					// Handle pixel commands
					byte x = ReadByte();

					if (x == 0xFF)
					{
						// Fill the rest of the line with the background color
						WritePixel((byte)bg, ti.Width - i);
						NextLine();
						lineMode = ReadLineMode();
					}
					else
					{
						if (lineMode == 0xFE)
						{
							// Pixel mode
							if (x == 0xFE)
							{
								int length = ReadByte();
								byte color = ReadByte();
								WritePixel(color, length);
							}
							else
							{
								WritePixel(x);
							}
						}
						else if (lineMode == 0xFD)
						{
							// Multipixel mode
							byte color = ReadByte();
							WritePixel(color, x);
						}
						else
						{
							throw new InvalidOperationException($"Unknown line mode: {lineMode:X2}");
						}
					}
				}
			}

			return output;
		}
		
		public static byte[] DecodeSprite(byte[] data, SpriteHeader sh, bool isCompressed = false)
		{
			var result = new byte[sh.Width * sh.Height];
			int x = 0;
			int y = 0;
			int bytesRead = 0;
			int bytesToRead = (int)sh.Length;
			using var reader = new BinaryReader(new MemoryStream(data));
			var lm = reader.ReadByte();
			bytesRead++;
			while (y < sh.Height && bytesRead < bytesToRead)
			{
				if (lm == 0xFF)
				{
					// fill with bg color (0xFD) to the end of this line
					while (x < sh.Width)
					{
						result[y * sh.Width + x] = 0xFD;
						x++;
					}
					x = 0;
					y++;
					lm = reader.ReadByte();
					bytesRead++;
				}
				else if (lm == 0xFC)
				{
					// end of sprite
					break;
				}
				else
				{
					var b = reader.ReadByte();
					bytesRead++;

					if (b == 0xFF)
					{
						// fill with bg color (0xFD) to the end of this line
						while (x < sh.Width)
						{
							result[y * sh.Width + x] = 0xFD;
							x++;
						}
						x = 0;
						y++;
						lm = reader.ReadByte();
						bytesRead++;
					}
					else
					{
						if (lm == 0xFE)
						{
							if (b == 0xFE)
							{
								var len = reader.ReadByte();
								bytesRead++;
								var colorIndex = reader.ReadByte();
								bytesRead++;
								for (int i = 0; i < len; i++)
								{
									result[y * sh.Width + x] = colorIndex;
									x++;
									if (x >= sh.Width)
									{
										x = 0;
										y++;
									}
								}
							}
							else
							{
								if (x >= sh.Width)
								{
									x = 0;
									y++;
								}
								result[y * sh.Width + x] = b;
								x++;
							}
						}
						else if (lm == 0xFD)
						{
							var colorIndex = reader.ReadByte();
							bytesRead++;
							result[y * sh.Width + x] = colorIndex;
							x++;
						}
						else
						{
							throw new Exception($"Unknown line mode: {lm}; offset: {reader.BaseStream.Position}");
						}
					}
				}
			}
			return result;
		}
	
		public static void ExtractV2BackgroundImage(string tileMapFile, string tileDataFile, string outputDir)
		{
			// Check output directory exists
			Directory.CreateDirectory(outputDir);
			var tileDataFileInfo = new FileInfo(tileDataFile);

			var tileMapPack = new MadsPackFile(tileMapFile);
			var tileDataPack = new MadsPackFile(tileDataFile);

			var mapStream = tileMapPack.GetEntryDataReader(0);
			mapStream.ReadInt32();
			var tileCountX = mapStream.ReadInt16();
			var tileCountY = mapStream.ReadInt16();
			var tileWidthMap = mapStream.ReadInt16();
			var tileHeightMap = mapStream.ReadInt16();
			var screenWidth = mapStream.ReadInt16();
			var screenHeight = mapStream.ReadInt16();
			var tileCountMap = tileCountX * tileCountY;

			var tileMap = new ushort[tileCountMap];
			mapStream = tileMapPack.GetEntryDataReader(1);
			for (int i = 0; i < tileCountMap; i++)
			{
				tileMap[i] = mapStream.ReadUInt16();
			}

			var tileDataUncomp = tileDataPack.GetEntryDataReader(0);
			var tileCount = tileDataUncomp.ReadInt16();
			var tileWidth = tileDataUncomp.ReadInt16();
			var tileHeight = tileDataUncomp.ReadInt16();

			// confirm that this data matches that from the mapStream
			if (tileWidth != tileWidthMap || tileHeight != tileHeightMap || tileCount != tileCountMap
				|| screenWidth != 320 || screenHeight > 156)
			{
				Console.WriteLine("Tile dimensions do not match");
			}

			tileDataUncomp = tileDataPack.GetEntryDataReader(1);
			var tiles = new List<byte[]>();
			uint compressedTileDataSize = 0;
			var tdfReader = new BinaryReader(File.OpenRead(tileDataFile));
			for (int i = 0; i < tileCountMap; i++)
			{
				tileDataUncomp.BaseStream.Seek(i * 4, SeekOrigin.Begin);
				var tileOffset = tileDataUncomp.ReadUInt32();

				if (i == tileCount - 1)
				{
					compressedTileDataSize = (uint)(tileDataFileInfo.Length - tileOffset);
				}
				else
				{
					compressedTileDataSize = tileDataUncomp.ReadUInt32() - tileOffset;
				}

				tdfReader.BaseStream.Seek(tileDataPack.DataOffset + tileOffset, SeekOrigin.Begin);
				var compressedTileData = tdfReader.ReadBytes((int)compressedTileDataSize);
				var uncompressedTileData = FabDecompressor.ReadFab(new BinaryReader(new MemoryStream(compressedTileData)), tileWidth * tileHeight);
				tiles.Add(uncompressedTileData.ToArray());
			}

			using var palStream = tileDataPack.GetEntryDataReader(2);
			var palette = new List<Rgba32>();
			var count = palStream.ReadUInt16();
			for (int i = 0; i < count; i++)
			{
				var r = palStream.ReadByte() * 255 / 63;
				var g = palStream.ReadByte() * 255 / 63;
				var b = palStream.ReadByte() * 255 / 63;

				var fR = r / 255f;
				var fG = g / 255f;
				var fB = b / 255f;

				var color = new Rgba32(fR, fG, fB);
				palette.Add(color);
				palStream.ReadBytes(3);
				Console.WriteLine(i);
			}

			var imageOutputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(tileMapFile) + "a.png");
			var imgOutputStream = new FileStream(imageOutputPath, FileMode.Create);
			var imgEncoder = new PngEncoder()
			{
					BitDepth = PngBitDepth.Bit8,
					ColorType = PngColorType.RgbWithAlpha,
					CompressionLevel = PngCompressionLevel.NoCompression,
					FilterMethod = PngFilterMethod.Adaptive,
					InterlaceMethod = PngInterlaceMode.Adam7,
					Threshold = 0xFF,
			};

			if (tileCount == 1)
			{
				var image = ImageFormatHelper.GenerateClutImage(palette, tiles[0], tileWidth, tileHeight);
				image.Save(imgOutputStream, imgEncoder);
				// var image = ImageFormatHelper.GenerateClutImage(palette, tiles[0], tileWidth, tileHeight);
				// image.Save(imageOutputPath, ImageFormat.Png);
				//File.WriteAllBytes(@"C:\Dev\Gaming\PC_DOS\Extractions\Dragonsphere\SECTION1\TT\RM101\decoded.bin", tiles[0]);
			}
			else
			{
				var tmIndex = 0;
				var imageBytes = new byte[tileWidth * tileHeight * tileCount];
				for (int y = 0; y < tileCountY; y++)
				{
					for (int x = 0; x < tileCountX; x++)
					{
						var tIndex = tileMap[tmIndex++];
						var tile = tiles[tIndex];
						// place the tile in the image at the correct position
						// the image is tileWidth * tileCountX wide and tileHeight * tileCountY high
						// x and y are the tile coordinates in the map, so the top left corner of the tile is at x * tileWidth, y * tileHeight
						for (int ty = 0; ty < tileHeight; ty++)
						{
							for (int tx = 0; tx < tileWidth; tx++)
							{
								var imageIndex = ((y * tileHeight) + ty) * (tileWidth * tileCountX) + ((x * tileWidth) + tx);
								var tileIndex = (ty * tileWidth) + tx;
								imageBytes[imageIndex] = tile[tileIndex];
							}
						}
					}
				}
				var image = ImageFormatHelper.GenerateClutImage(palette, imageBytes, tileWidth * tileCountX, tileHeight * tileCountY);
				image.Save(imgOutputStream, imgEncoder);

				// var image = ImageFormatHelper.GenerateClutImage(palette, imageBytes, tileWidth, tileHeight);
				// image.Save(imageOutputPath, ImageFormat.Png);
			}
		}

	}
}
