using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Voodoo
{
	public static class KidExtractor
	{
		public static void Extract(string kidFile, string? outputDirectory = null)
		{
			// First 4 chars should be equal to `Burp`
			using var reader = new BinaryReader(File.OpenRead(kidFile));

			var header = new string(reader.ReadChars(4));
			if (header != "Burp")
			{
				throw new InvalidDataException("Invalid KID file");
			}

			var entryCount = reader.ReadUInt32();
			var currentPos = 0;
			var entries = new List<KidEntry>();
			for (int i = 0; i < entryCount; i++)
			{
				var entry = new KidEntry
				{
					UncompressedLength = reader.ReadUInt32(),
					CompressedLength = reader.ReadUInt32(),
					NameOffset = reader.ReadUInt32(),
					DataOffset = reader.ReadUInt32(),
					Type = reader.ReadUInt32()
				};
				// store current position
				currentPos = (int)reader.BaseStream.Position;

				// Read the name
				reader.BaseStream.Seek(entry.NameOffset, SeekOrigin.Begin);
				reader.ReadByte();
				var nameLength = reader.ReadByte();
				entry.Name = new string(reader.ReadChars(nameLength));

				entries.Add(entry);
				reader.BaseStream.Seek(currentPos, SeekOrigin.Begin);
			}

		}
	}

	public class KidEntry
	{
		public uint UncompressedLength { get; set; }
		public uint CompressedLength { get; set; }
		public uint NameOffset { get; set; }
		public uint DataOffset { get; set; }
		public uint Type { get; set; }
		public string Name { get; set; } = string.Empty;
	}
}
