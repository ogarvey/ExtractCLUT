using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PSX
{
	public static class ChocoboHelper
	{
		public static void ExtractBlockOne(byte[] data, string outputFolder)
		{
			var workingData = data.Skip(0x18800).ToArray();
			using var dReader = new BinaryReader(new MemoryStream(workingData));
			var subBlockCount = dReader.ReadUInt32();
			var offsets = new List<uint>();
			for (var i = 0; i < subBlockCount+1; i++)
			{
				offsets.Add(dReader.ReadUInt32());
			}
			for (var i = 0; i < subBlockCount; i++)
			{
				var start = offsets[i];
				var end = offsets[i + 1];
				var blockData = workingData.Skip((int)start).Take((int)(end - start)).ToArray();
				File.WriteAllBytes(Path.Combine(outputFolder, $"block_{i}.bin"), blockData);
			}
		}
	}
	public class ChocobosDungeonTwo
	{
		// 0x0 - 0x4000 some sort of table?
		// 0x4000 - 0x5043 - Squaresoft logo
		// 0x5800 - 0x77FF - Unknown block
		// 0x7800 - 0x7FFF - Unknown block (2 byte values between 0x01 - 0x08)
		// 0x8000 - 0x17FFF - Unknown block ( 8 byte structs in 256 byte blocks?)
		// 0x18000 - 0x187FF - Unknown block (4 byte structs in 128 byte blocks?)

		// Sprite lookup table info
		// Example @0x1CFE4 in ALLBIN.BIN
		// 8 bytes per entry
		// 0x00: 2 bytes, unknown
		// 0x02: 1 byte, x position in overall frame
		// 0x03: 1 byte, y position in overall frame
		// 0x04: 2 bytes, size in bytes of sprite
		// 0x06: 1 byte, width in pixels
		// 0x07: 1 byte, height in pixels



		// Block start @ 0x18800 in ALLBIN.BIN
		//latest     block = 0x260664
	}
}
