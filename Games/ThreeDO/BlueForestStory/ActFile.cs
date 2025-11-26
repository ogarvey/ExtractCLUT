using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.ThreeDO.BlueForestStory
{
	public class ActFile
	{
		public List<byte[]> DataBlocks { get; set; } = [];
		public List<byte[]> DataBlocks2 { get; set; } = [];
		public byte[] PlutData { get; set; } = [];
		public List<(byte[] CCBData, byte[] PdatData)> CelEntries { get; set; } = [];

		public ActFile(string filePath)
		{
			using var actReader = new BinaryReader(File.OpenRead(filePath));
			actReader.BaseStream.Seek(0x14, SeekOrigin.Begin);
			var firstOffset = actReader.ReadBigEndianUInt32() + 0x14;
			var offsets = new List<uint>();
			offsets.Add(firstOffset);
			while (actReader.BaseStream.Position < firstOffset)
			{
				var offset = actReader.ReadBigEndianUInt32() + 0x14;
				offsets.Add(offset);
			}

			for (int i = 0; i < offsets.Count; i++)
			{
				actReader.BaseStream.Seek(offsets[i], SeekOrigin.Begin);
				var blockData = new List<byte[]>();
				// Add bytes until we reach 4 consecutive 0xFF bytes
				while (true)
				{
					var chunk = actReader.ReadBytes(4);
					if (chunk.Length < 4)
						break;
					if (chunk.SequenceEqual(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }))
						break;
					blockData.Add(chunk);
				}
				var blockBytes = blockData.SelectMany(b => b).ToArray();
				DataBlocks.Add(blockBytes);
			}

			Console.WriteLine($"Read {DataBlocks.Count} data blocks from ACT file.");
			Console.WriteLine($"Current File Position: {actReader.BaseStream.Position:X8}");

			var db2Offset = actReader.BaseStream.Position;
			firstOffset = (uint)(actReader.ReadBigEndianUInt32() + db2Offset);
			offsets = new List<uint>();
			offsets.Add(firstOffset);
			while (actReader.BaseStream.Position < firstOffset)
			{
				var offset = actReader.ReadBigEndianUInt32() + db2Offset;
				offsets.Add((uint)offset);
			}
			var lastOffset = offsets.Last() + 0x20;
			offsets.Add(lastOffset);
			for (int i = 0; i < offsets.Count - 1; i++)
			{
				actReader.BaseStream.Seek(offsets[i], SeekOrigin.Begin);
				var length = (int)(offsets[i + 1] - offsets[i]);
				var blockBytes = actReader.ReadBytes(length);
				DataBlocks2.Add(blockBytes);
			}
			Console.WriteLine($"Read {DataBlocks2.Count} data blocks from ACT file.");
			Console.WriteLine($"Current File Position: {actReader.BaseStream.Position:X8}");

			var count = actReader.ReadBigEndianUInt32();

			var celOffset = actReader.BaseStream.Position;

			offsets = [];
			for (int i = 0; i < count; i++)
			{
				var offset = actReader.ReadBigEndianUInt32() + (uint)celOffset;
				offsets.Add(offset);
			}

			Console.WriteLine($"Found {offsets.Count} CEL entries.");
			Console.WriteLine($"Current File Position: {actReader.BaseStream.Position:X8}");

			var plutMagic = Encoding.ASCII.GetString(actReader.ReadBytes(4));
			if (plutMagic != "PLUT")
				throw new Exception("Expected PLUT magic not found.");
			var plutLength = actReader.ReadBigEndianUInt32();
			actReader.BaseStream.Seek(-8, SeekOrigin.Current);
			PlutData = actReader.ReadBytes((int)plutLength);

			// we should now be at the start of the CEL entries
			Console.WriteLine($"Current File Position: {actReader.BaseStream.Position:X8}");

			foreach (var offset in offsets)
			{
				actReader.BaseStream.Seek(offset, SeekOrigin.Begin);
				var celMagic = Encoding.ASCII.GetString(actReader.ReadBytes(4));
				if (celMagic != "CCB ")
					throw new Exception("Expected CCB  magic not found.");
				actReader.BaseStream.Seek(-4, SeekOrigin.Current);
				var ccbData = actReader.ReadBytes(0x50); // CCB is always 0x50 bytes

				var pdatMagic = Encoding.ASCII.GetString(actReader.ReadBytes(4));
				if (pdatMagic != "PDAT")
					throw new Exception("Expected PDAT magic not found.");
				var pdatLength = actReader.ReadBigEndianUInt32();
				actReader.BaseStream.Seek(-8, SeekOrigin.Current);
				var pdatData = actReader.ReadBytes((int)pdatLength);

				CelEntries.Add((ccbData, pdatData));
			}
			Console.WriteLine($"Read {CelEntries.Count} CEL entries.");
			Console.WriteLine($"Current File Position: {actReader.BaseStream.Position:X8}");
		}
	
		public bool ExportImages(string outputDir)
		{
			Directory.CreateDirectory(outputDir);
			var combinedData = new List<byte>();
			for (int i = 0; i < CelEntries.Count; i++)
			{
				var (ccbData, pdatData) = CelEntries[i];
				combinedData.AddRange(ccbData);
				combinedData.AddRange(PlutData);
				combinedData.AddRange(pdatData);
			}
			File.WriteAllBytes(Path.Combine(outputDir, "combined.cel"), combinedData.ToArray());
			CelUnpacker.UnpackAndSaveCelFile(Path.Combine(outputDir, "combined.cel"), Path.Combine(outputDir, "output.png"), verbose: true);
			File.Delete(Path.Combine(outputDir, "combined.cel"));
			return true;
		}
	}
}
