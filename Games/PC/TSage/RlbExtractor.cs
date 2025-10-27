using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.TSage
{
	public static class RlbExtractor
	{
		public static void ExtractRlb(string rlbPath, string outputDir)
		{
			var rlbHeaderBytes = new byte[] { 0x54, 0x4D, 0x49, 0x2D };

			using var reader = new BinaryReader(File.OpenRead(rlbPath));
			// check for RLB header
			var header = reader.ReadBytes(4);
			if (!header.SequenceEqual(rlbHeaderBytes))
			{
				Console.WriteLine("Not an RLB file");
				return;
			}
			reader.ReadByte(); // skip 1 byte
			reader.ReadByte();

			var indexEntry = new RLBResourceEntry();
			indexEntry.Id = reader.ReadUInt16();

			var size = reader.ReadUInt16();
			var uncompressedSize = reader.ReadUInt16();
			var sizeHi = reader.ReadByte();
			var Type = (byte)(reader.ReadByte() >> 5);

			indexEntry.Offset = reader.ReadUInt32();
			indexEntry.Size = (uint)(((sizeHi & 0XF) << 16) | size);
			indexEntry.UncompressedSize = (uint)(((sizeHi & 0xF0) << 12) | uncompressedSize);
			indexEntry.IsCompressed = Type != 0;

			reader.BaseStream.Seek(indexEntry.Offset, SeekOrigin.Begin);
			indexEntry.Data = reader.ReadBytes((int)indexEntry.Size);

			Directory.CreateDirectory(outputDir);

			var sections = new List<SectionEntry>();
			ushort resNum, configId, fileOffset;

			using var iReader = new BinaryReader(new MemoryStream(indexEntry.Data));

			while ((resNum = iReader.ReadUInt16()) != 0xFFFF)
			{
				configId = iReader.ReadUInt16();
				fileOffset = iReader.ReadUInt16();

				var se = new SectionEntry
				{
					ResNum = resNum,
					ResType = (ResourceType)(configId & 0x1F),
					Offset = (uint)((((configId >> 5) & 0x7ff) << 16) | fileOffset)
				};
				sections.Add(se);
			}

			foreach (var section in sections)
			{
				var entry = new RLBResourceEntry();
				reader.BaseStream.Seek(section.Offset, SeekOrigin.Begin);
				header = reader.ReadBytes(4);
				if (!header.SequenceEqual(rlbHeaderBytes))
				{
					Console.WriteLine("Not an RLB file");
					return;
				}
				reader.ReadByte(); // skip 1 byte
				var count = reader.ReadByte();

				for (int i = 0; i < count; i++)
				{
					reader.BaseStream.Seek(section.Offset + 6 + (i * 12), SeekOrigin.Begin);
					entry.Id = reader.ReadUInt16();
					size = reader.ReadUInt16();
					uncompressedSize = reader.ReadUInt16();
					sizeHi = reader.ReadByte();
					Type = (byte)(reader.ReadByte() >> 5);

					entry.Offset = reader.ReadUInt32();
					entry.Size = (uint)(((sizeHi & 0XF) << 16) | size);
					entry.UncompressedSize = (uint)(((sizeHi & 0xF0) << 12) | uncompressedSize);
					entry.IsCompressed = Type != 0;

					reader.BaseStream.Seek(entry.Offset + section.Offset, SeekOrigin.Begin);
					entry.Data = reader.ReadBytes((int)entry.Size);

					var output = Path.Combine(outputDir, $"{section.ResNum}_{section.ResType}_{entry.Id}_{(entry.IsCompressed ? $"compressed_{entry.UncompressedSize}" : "uncompressed")}.bin");
					File.WriteAllBytes(output, entry.Data);
				}
			}



		}
	}
	public class RLBResourceEntry
	{
		public byte[]? Data { get; set; }
		public ushort Id { get; set; }
		public uint Size { get; set; }
		public uint UncompressedSize { get; set; }
		public uint Offset { get; set; }
		public bool IsCompressed { get; set; }
	}

	public class SectionEntry
	{
		public uint Offset { get; set; }
		public ushort ResNum { get; set; }
		public ResourceType ResType { get; set; }
	}

	public enum ResourceType
	{
		RES_LIBRARY, RES_STRIP, RES_IMAGE, RES_PALETTE, RES_VISAGE, RES_SOUND, RES_MESSAGE,
		RES_FONT, RES_POINTER, RES_BANK, RES_SND_DRIVER, RES_PRIORITY, RES_CONTROL, RES_WALKRGNS,
		RES_BITMAP, RES_SAVE, RES_SEQUENCE,
		// Return to Ringworld specific resource types
		RT17, RT18, RT19, RT20, RT21, RT22, RT23, RT24, RT25, RT26, RT27, RT28, RT29, RT30, RT31
	};
}
