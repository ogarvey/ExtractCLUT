using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Processing;
using Path = System.IO.Path;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;

namespace ExtractCLUT.Games.PC.Bubsy
{
	public class MapEntry
	{
		public ushort TileIndex { get; set; }
		public byte Flag1 { get; set; }
		public byte Flag2 { get; set; }
	}
	
	public class LevelFile
	{

		public List<Color> Clut { get; set; } = new List<Color>();
		public List<Image> Tiles { get; set; } = new List<Image>();
		public List<MapEntry> MapData { get; set; } = new List<MapEntry>();
		public uint MapWidth { get; set; }
		public uint MapHeight { get; set; }
		public uint TileWidth { get; private set; }
		public uint TileHeight { get; private set; }

		public LevelFile(string filePath)
		{
			using var lvlReader = new BinaryReader(File.OpenRead(filePath));
			lvlReader.BaseStream.Seek(0x150, SeekOrigin.Begin);
			// Check for "PAL " magic
			var magic = lvlReader.ReadUInt32();
			if (magic != 0x206C6150) // "PAL "
			{
				throw new Exception("Invalid level file");
			}
			lvlReader.BaseStream.Seek(0x160, SeekOrigin.Begin);
			// Read CLUT RGBA format
			for (int i = 0; i < 256; i++)
			{
				byte r = lvlReader.ReadByte();
				byte g = lvlReader.ReadByte();
				byte b = lvlReader.ReadByte();
				byte a = (byte)(i == 0 ? 0 : 255);
				lvlReader.ReadByte();
				Clut.Add(Color.FromRgba(b, g, r, a));
			}

			// We should be at the "Map " magic now
			magic = lvlReader.ReadUInt32();
			if (magic != 0x2070614D && magic != 0x70614D53) // "Map " or "SMap"
			{
				throw new Exception("Invalid level file");
			}
			var mapSize = lvlReader.ReadUInt32() - 16;
			MapWidth = lvlReader.ReadUInt32();
			MapHeight = lvlReader.ReadUInt32();
			var mapRecordSize = filePath.Contains("RAYON") || filePath.Contains("FINALE") ? 12 : magic == 0x2070614D ? 8 : 4;
			for (int i = 0; i < mapSize / mapRecordSize; i++)
			{
				MapData.Add(new MapEntry
				{
					TileIndex = lvlReader.ReadUInt16(),
					Flag1 = lvlReader.ReadByte(),
					Flag2 = lvlReader.ReadByte()
				});
				if (magic == 0x2070614D && !filePath.Contains("RAYON") && !filePath.Contains("FINALE")) // "Map "
				{
					lvlReader.ReadBytes(4);
				}
				if (filePath.Contains("RAYON") || filePath.Contains("FINALE")) // "SMap"
				{
					lvlReader.ReadBytes(8);
				}
			}

			if (magic == 0x70614D53 && !filePath.Contains("FINALE.GAM") &&!filePath.Contains("RAYON.GAM"))
			{
				// 41 53 6C 6F
				magic = lvlReader.ReadUInt32();
				if (magic != 0x6F6C5341) // "ASlo"
				{
					throw new Exception("Invalid level file");
				}
				var asloSize = lvlReader.ReadUInt32() - 8;
				lvlReader.BaseStream.Seek(asloSize, SeekOrigin.Current); // Skip ASlo data
				magic = lvlReader.ReadUInt32();
				// 41 46 6C 61
				if (magic != 0x616C4641) // "AFla"
				{
					throw new Exception("Invalid level file");
				}
				var aflaSize = lvlReader.ReadUInt32() - 8;
				lvlReader.BaseStream.Seek(aflaSize, SeekOrigin.Current); // Skip AFla data
			}

			// We should be at the "Tile" magic now
			magic = lvlReader.ReadUInt32();
			// 43 54 69 6C
			if (magic != 0x656C6954 && magic != 0x6C695443) // "Tile" or "CTil
			{
				throw new Exception("Invalid level file");
			}
			var tileSize = lvlReader.ReadUInt32() - 16;
			TileWidth = lvlReader.ReadUInt32();
			TileHeight = lvlReader.ReadUInt32();

			lvlReader.ReadUInt32(); // Unknown, always 0x00000001
			var numTiles = lvlReader.ReadUInt32();
			if (magic == 0x656C6954) // "Tile"
			{
				for (int t = 0; t < numTiles; t++)
				{
					var tileData = new byte[TileWidth * TileHeight];

					tileData = lvlReader.ReadBytes(tileData.Length);

					// use the CLUT to create an image
					var img = new Image<Rgba32>((int)TileWidth, (int)TileHeight);
					for (int y = 0; y < TileHeight; y++)
					{
						for (int x = 0; x < TileWidth; x++)
						{
							var colorIndex = tileData[y * TileWidth + x];
							img[x, y] = Clut[colorIndex].ToPixel<Rgba32>();
						}
					}
					//img.Mutate(ctx => ctx.Rotate(-90).Flip(FlipMode.Horizontal));
					Tiles.Add(img);
				}
			}
			else
			{
				var compressedData = lvlReader.ReadBytes((int)tileSize);
				while (Tiles.Count < numTiles)
				{

					List<byte> output = new List<byte>();
					int compressedOffset = 0;

					for (int line = 0; line < 32; line++)
					{
						var lineData = new byte[32];
						int bytesConsumed = BubsyDecompress.UncRLELobit((uint)32, compressedData, lineData);
						output.AddRange(lineData);
						compressedData = compressedData.Skip(bytesConsumed).ToArray();
						compressedOffset += bytesConsumed;
					}
					var imageData = output.ToArray();
					var img = new Image<Rgba32>((int)TileWidth, (int)TileHeight);
					for (int y = 0; y < TileHeight; y++)
					{
						for (int x = 0; x < TileWidth; x++)
						{
							var colorIndex = imageData[y * TileWidth + x];
							img[x, y] = Clut[colorIndex].ToPixel<Rgba32>();
						}
					}
					Tiles.Add(img);
				}
			}
		}

		public void SaveTiles(string outputDir)
		{
			Directory.CreateDirectory(outputDir);
			for (int i = 0; i < Tiles.Count; i++)
			{
				var tile = Tiles[i];
				tile.SaveAsPng(Path.Combine(outputDir, $"tile_{i:D4}.png"));
			}
		}

		public void SaveClut(string outputPath, int width, int height, int scale = 4)
		{
			using var img = new Image<Rgba32>(width * scale, height * scale);
			for (int i = 0; i < Clut.Count; i++)
			{
				int x = i % width;
				int y = i / width;
				var color = Clut[i];
				for (int sy = 0; sy < scale; sy++)
				{
					for (int sx = 0; sx < scale; sx++)
					{
						img[x * scale + sx, y * scale + sy] = color.ToPixel<Rgba32>();
					}
				}
			}
			img.SaveAsPng(outputPath);
		}

		public void SaveMapImage(string mapOutputPath)
		{
			using var img = new Image<Rgba32>((int)(MapHeight * TileWidth), (int)(MapWidth * TileHeight));
			for (int my = 0; my < MapWidth; my++)
			{
				for (int mx = 0; mx < MapHeight; mx++)
				{
					var mapIndex = my * MapHeight + mx;
					var tileIndex = MapData[(int)mapIndex].TileIndex;
					var flag1 = MapData[(int)mapIndex].Flag1;
					var flag2 = MapData[(int)mapIndex].Flag2;
					if (tileIndex < Tiles.Count)
					{
						var tile = Tiles[tileIndex].Clone(ctx => ctx.Rotate(-90).Flip(FlipMode.Vertical));

						img.Mutate(ctx => ctx.DrawImage(tile, new Point((int)(mx * TileWidth), (int)(my * TileHeight)), 1f));
						tile.Dispose();
					}
				}
			}
			img.Mutate(ctx => ctx.Rotate(90).Flip(FlipMode.Horizontal));
			img.SaveAsPng(mapOutputPath);
		}
	}
}
