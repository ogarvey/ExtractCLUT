using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Bubsy
{
	public static class SpriteFile
	{
		public static void ExtractSpriteFile(string spriteFile)
		{
			var spriteOutputDir = Path.Combine(Path.GetDirectoryName(spriteFile)!, "sprite_output");
			Directory.CreateDirectory(spriteOutputDir);

			using var spriteReader = new BinaryReader(File.OpenRead(spriteFile));
			var spriteCount = spriteReader.ReadUInt32();
			var offsetAndNames = new List<(uint offset, string name)>();
			for (int i = 0; i < spriteCount; i++)
			{
				var offset = spriteReader.ReadUInt32();
				var nameBytes = spriteReader.ReadBytes(13);
				var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
				offsetAndNames.Add((offset, name));
			}

			offsetAndNames = offsetAndNames.OrderBy(o => o.offset).ToList();

			for (int i = 0; i < offsetAndNames.Count - 1; i++)
			{
				var (offset, name) = offsetAndNames[i];
				var nextOffset = (i < offsetAndNames.Count - 1) ? offsetAndNames[i + 1].offset : (uint)spriteReader.BaseStream.Length;
				var length = nextOffset - offset;
				spriteReader.BaseStream.Seek(offset, SeekOrigin.Begin);
				var spriteData = spriteReader.ReadBytes((int)length);
				File.WriteAllBytes(Path.Combine(spriteOutputDir, $"{name}_compressed.bin"), spriteData);
			}
		}
	}
}
