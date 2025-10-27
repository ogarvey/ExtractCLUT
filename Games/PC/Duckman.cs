using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC
{
	public static class Duckman
	{
		public static void ConvertDuckManBg(string bgFile)
		{
			var palettesDir = Path.Combine(Path.GetDirectoryName(bgFile)!, "palettes");
			Directory.CreateDirectory(palettesDir);
			using var br = new BinaryReader(File.OpenRead(bgFile));

			// load bg pixels
			br.BaseStream.Seek(0xA, SeekOrigin.Begin);
			var bgInfoCount = br.ReadUInt16(); // number of background info entries
			br.BaseStream.Seek(0x20, SeekOrigin.Begin);
			var bgInfoOffset = br.ReadUInt32(); // offset to background info entries
			br.BaseStream.Seek(bgInfoOffset, SeekOrigin.Begin);
			var bgInfoList = new List<BgInfo>();

			for (int i = 0; i < bgInfoCount; i++)
			{
				br.BaseStream.Seek(bgInfoOffset + i * 0x1c, SeekOrigin.Begin);
				var bgInfo = new BgInfo();
				bgInfo.Flags = br.ReadUInt32(); // flags
				br.ReadUInt16(); // unknown
				bgInfo.Priority = br.ReadInt16(); // priority
				var surfInfo = new SurfaceInfo
				{
					PixelSize = br.ReadUInt32(), // pixel size
					Width = br.ReadInt16(), // width of the background
					Height = br.ReadInt16() // height of the background
				};
				bgInfo.Info = surfInfo;
				bgInfo.PanPoint = new Point(br.ReadInt16(), br.ReadInt16()); // pan point (x, y)
				var tMapOffset = br.ReadUInt32(); // offset to the tile map
				var tPixOffset = br.ReadUInt32(); // offset to the tile pixels
				br.BaseStream.Seek(tMapOffset, SeekOrigin.Begin);
				var tMapWidth = br.ReadInt16(); // width of the tile map
				var tMapHeight = br.ReadInt16(); // height of the tile map
				br.ReadUInt32(); // unknown
				var mapBytes = br.ReadBytes(tMapWidth * tMapHeight * 2); // read the tile map entries
				var tMap = new short[tMapWidth * tMapHeight];
				for (int j = 0; j < tMap.Length; j++)
				{
					tMap[j] = BitConverter.ToInt16(mapBytes, j * 2); // read the tile map entries
				}
				var tileMap = new TileMap
				{
					Width = tMapWidth,
					Height = tMapHeight,
					Tiles = tMap // assign the tile map entries
				};
				bgInfo.TileMap = tileMap;
				br.BaseStream.Seek(tPixOffset, SeekOrigin.Begin);
				// calculate remaining bytes for the tile pixels
				var remainingBytes = (uint)(br.BaseStream.Length - br.BaseStream.Position);
				if (remainingBytes > 0)
				{
					bgInfo.Pixels = br.ReadBytes((int)remainingBytes); // read the tile pixels
				}
				bgInfoList.Add(bgInfo); // add the background info to the list
			}


			var palettes = new List<List<Color>>();

			br.BaseStream.Seek(0x18, SeekOrigin.Begin);
			var palCount = br.ReadUInt16(); // number of palettes
			br.BaseStream.Seek(0x3c, SeekOrigin.Begin);
			var palOffset = br.ReadUInt32(); // offset to the palettes]
			for (int i = 0; i < palCount; i++)
			{
				br.BaseStream.Seek(palOffset + i * 8, SeekOrigin.Begin);
				var count = br.ReadUInt16(); // number of colors in the palette
				br.ReadUInt16(); // unknown
				palOffset = br.ReadUInt32(); // offset to the palette data
				br.BaseStream.Seek(palOffset, SeekOrigin.Begin);
				var paletteData = br.ReadBytes(count * 4); // read the palette data
				var palFileName = $"{Path.GetFileNameWithoutExtension(bgFile)}_palette_{i}.pal";
				var palFilePath = Path.Combine(palettesDir, palFileName);
				File.WriteAllBytes(palFilePath, paletteData); // save the palette data to a file
				var palette = ColorHelper.ConvertBytesToARGB(paletteData); // convert the palette data to a palette
				palettes.Add(palette);
			}

			var tilePixels = new List<byte[]>();

			var tileData = bgInfoList.FirstOrDefault()?.Pixels;
			if (tileData == null)
			{
				Console.WriteLine("No tile data found.");
				return;
			}

			using var tileReader = new BinaryReader(new MemoryStream(tileData));

			while (tileReader.BaseStream.Position < tileReader.BaseStream.Length - 4)
			{
				var tile = tileReader.ReadBytes(32 * 8); // read the tile pixel data, assuming each tile is 32x8 pixels
				tilePixels.Add(tile);
			}

			foreach (var (palette, pIndex) in palettes.WithIndex())
			{
				foreach (var (bgInfo, bIndex) in bgInfoList.WithIndex())
				{
					// Now we can generate the image from the tile map and tile pixels
					var image = ImageFormatHelper.CreateScreenImage(tilePixels, bgInfo.TileMap.Tiles, bgInfo.TileMap.Width, bgInfo.TileMap.Height, 32, 8, palette, true);
					var outputImageFile = Path.ChangeExtension(bgFile, $"_{pIndex}_{bIndex}.png");
					image.Save(outputImageFile, ImageFormat.Png);
				}
			}

		}
		public static void ExtractAllGamFiles(string gamFilePath, string outputDirectory)
		{
			var gamArchive = new GamArchive(gamFilePath);
			foreach (var group in gamArchive.Groups)
			{
				var folderName = $"{group.Id:D3}";
				var outputGroupDirectory = Path.Combine(outputDirectory, folderName);
				if (!Directory.Exists(outputGroupDirectory))
				{
					Directory.CreateDirectory(outputGroupDirectory);
				}
				foreach (var file in group.Files)
				{
					var fileData = gamArchive.GetFile(group.Id, file.Id);
					var outputFilePath = Path.Combine(outputGroupDirectory, $"{file.Id}.bin");
					File.WriteAllBytes(outputFilePath, fileData);
					Console.WriteLine($"Extracted {outputFilePath}");
				}
			}
		}
	}

	public class GamArchive
	{
		public uint GroupCount { get; set; }
		public List<GamGroupEntry> Groups { get; set; } = new List<GamGroupEntry>();
		public FileInfo GFile { get; set; }

		public GamArchive(string filePath)
		{
			GFile = new FileInfo(filePath);
			if (!GFile.Exists)
				throw new FileNotFoundException("The specified GAM file does not exist.", filePath);

			using var stream = GFile.OpenRead();
			using var reader = new BinaryReader(stream);
			GroupCount = reader.ReadUInt32();
			var offsets = new List<uint>();

			for (int i = 0; i < GroupCount; i++)
			{
				var group = new GamGroupEntry { Id = reader.ReadUInt32() };
				Groups.Add(group);
				offsets.Add(reader.ReadUInt32());
			}


			for (int i = 0; i < GroupCount; i++)
			{
				reader.BaseStream.Seek(offsets[i], SeekOrigin.Begin);
				Groups[i].FileCount = reader.ReadUInt32();
				Groups[i].Files = new List<GamFileEntry>();
				for (int j = 0; j < Groups[i].FileCount; j++)
				{
					var fileEntry = new GamFileEntry
					{
						Id = reader.ReadUInt32(),
						FileOffset = reader.ReadUInt32(),
						FileSize = reader.ReadUInt32()
					};
					Groups[i].Files.Add(fileEntry);
				}
			}

			Console.WriteLine($"Loaded GAM archive with {GroupCount} groups.");
		}

		public byte[] GetFile(uint sceneId, uint fileId)
		{
			var gamFileEntry = GetGroupFileEntry(sceneId, fileId);
			using (var stream = GFile.OpenRead())
			{
				stream.Seek(gamFileEntry.FileOffset, SeekOrigin.Begin);
				var buffer = new byte[gamFileEntry.FileSize];
				stream.Read(buffer, 0, (int)gamFileEntry.FileSize);
				return buffer;
			}
		}

		private GamGroupEntry GetGroupEntry(uint sceneId)
		{
			return Groups.Where(g => g.Id == sceneId).FirstOrDefault()
				?? throw new KeyNotFoundException($"No group found with ID {sceneId}.");
		}

		private GamFileEntry GetFileEntry(GamGroupEntry groupEntry, uint fileId)
		{
			return groupEntry.Files.Where(f => f.Id == fileId).FirstOrDefault()
				?? throw new KeyNotFoundException($"No file found with ID {fileId} in group {groupEntry.Id}.");
		}

		private GamFileEntry GetGroupFileEntry(uint sceneId, uint fileId)
		{
			var groupEntry = GetGroupEntry(sceneId);
			return GetFileEntry(groupEntry, fileId);
		}
	}

	public class GamFileEntry
	{
		public uint Id { get; set; }
		public uint FileOffset { get; set; }
		public uint FileSize { get; set; }
	}

	public class GamGroupEntry
	{
		public uint Id { get; set; }
		public uint FileCount { get; set; }
		public List<GamFileEntry> Files { get; set; } = new List<GamFileEntry>();
	}


	public class BgInfo
	{
		public uint Flags { get; set; }
		public short Priority { get; set; }
		public SurfaceInfo? Info { get; set; }
		public Point PanPoint { get; set; }
		public byte[]? Pixels { get; set; }
		public TileMap? TileMap { get; set; }
	}

	public class SurfaceInfo
	{
		public short Width { get; set; }
		public short Height { get; set; }
		public uint PixelSize { get; set; }
	}

	public class TileMap
	{
		public short Width { get; set; }
		public short Height { get; set; }
		public short[]? Tiles { get; set; } // array of tile indices
	}

	public class ActorType
	{
		public uint ActorTypeId;
		public SurfaceInfo? Info { get; set; }
		public byte[]? PointsConfig;
		public Color AColor;
		public byte Scale;
		public byte Priority;
		public ushort Value1E;
		public ushort PathWalkPointsIndex;
		public ushort ScaleLayerIndex;
		public ushort PathWalkRectIndex;
		public ushort PriorityLayerIndex;
		public ushort RegionLayerIndex;
		public ushort Flags;
	}

	public class Sequence
	{
		public uint SequenceId { get; set; }
		public uint Unknown { get; set; }
		byte[]? SequenceCode { get; set; }
	}

	public class Frame
	{
		public uint Offset { get; set; }
		public ushort Flags { get; set; }
		public byte[]? PointsConfig { get; set; }
		public byte[]? CompressedPixels { get; set; }
		public SurfaceInfo? Info { get; set; }
	}
}
