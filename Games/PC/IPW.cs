using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
	public static class IPW
	{
		public static void Extract(string ipwFile, string resourceOutputDir)
		{
			var ipwData = File.ReadAllBytes(ipwFile);
			for (int i = 0xc; i < ipwData.Length; i++)
			{
				ipwData[i] = (byte)(ipwData[i] ^ 0xAC);
			}

			var xorOutputFile = Path.ChangeExtension(ipwFile, ".xor");
			
			Directory.CreateDirectory(resourceOutputDir);

			File.WriteAllBytes(xorOutputFile, ipwData);
			using var xorReader = new BinaryReader(File.OpenRead(xorOutputFile));
			xorReader.ReadBytes(0x4); // skip 4 bytes
			var fileCount = xorReader.ReadUInt32(); // 0x4
			var fileTableOffset = xorReader.ReadUInt32(); // 0xC

			var namesOffsetsAndLengths = new List<(string name, uint offset, uint length)>(); // offset, length, name

			xorReader.BaseStream.Position = fileTableOffset;
			// each entry is 24 bytes long, 16 bytes null padded name, 4 bytes offset, 4 bytes length
			for (var i = 0; i < fileCount; i++)
			{
				var name = Encoding.UTF8.GetString(xorReader.ReadBytes(16)).TrimEnd('\0');
				var offset = xorReader.ReadUInt32();
				var length = xorReader.ReadUInt32();
				namesOffsetsAndLengths.Add((name, offset, length));
			}

			foreach (var (name, offset, length) in namesOffsetsAndLengths)
			{
				Console.WriteLine($"Name: {name}, Offset: {offset}, Length: {length}");
				// read the file data
				xorReader.BaseStream.Position = offset;
				var fileData = xorReader.ReadBytes((int)length);
				// write the file data to a file
				var outputFile = Path.Combine(resourceOutputDir, name);
				File.WriteAllBytes(outputFile, fileData);
			}
		}
	}
}
