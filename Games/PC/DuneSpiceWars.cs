using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
	public static class DuneSpiceWars
	{
		private const string IdString = "PAK\0";
		private static long baseOffset;
		private static long dataSize;
		private static FileStream fs;
		private static BinaryReader br;
		private static MemoryStream memoryFile;

		public static void ProcessFile(string inputFilePath, string outputDirectory)
		{
			if(!Directory.Exists(outputDirectory))
			{
				Directory.CreateDirectory(outputDirectory);
			}
			using (fs = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read))
			using (br = new BinaryReader(fs))
			{
				// Read the idstring "PAK\0"
				string idString = new string(br.ReadChars(4));
				if (idString != IdString)
				{
					throw new InvalidDataException("Invalid file format.");
				}

				// Get the base offset and data size
				baseOffset = br.ReadInt32();
				dataSize = br.ReadInt32();

				// Initialize memory file stream
				memoryFile = new MemoryStream();

				Extract(outputDirectory);
			}
		}

		private static void Extract(string path)
		{
			int nameLength = br.ReadByte();
			string name = new string(br.ReadChars(nameLength));
			byte type = br.ReadByte();

			if (type == 1)
			{
				path = Path.Combine(path, name);
				int ncount = br.ReadInt32();
				for (int i = 0; i < ncount; i++)
				{
					Extract(path);
				}
			}
			else if (type == 0)
			{
				string fullPath = Path.Combine(path, name);
				long offset = br.ReadInt32() + baseOffset;
				int size = br.ReadInt32();
				int fileCrc = br.ReadInt32(); // Not used in this example

				// Extract file
				ExtractFile(fullPath, offset, size);

				UpdateMemoryFile(offset + size);
			}
			else if (type == 2)
			{
				string fullPath = Path.Combine(path, name);
				double dummyOffset = br.ReadDouble(); // Actually a double
				int size = br.ReadInt32();
				int fileCrc = br.ReadInt32(); // Not used in this example

				long offset = ReadMemoryFileLongLong();

				// Extract file
				ExtractFile(fullPath, offset, size);

				UpdateMemoryFile(offset + size);
			}
		}

		private static void ExtractFile(string path, long offset, int size)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));

			fs.Seek(offset, SeekOrigin.Begin);
			byte[] data = new byte[size];
			fs.Read(data, 0, size);

			using (FileStream outFs = new FileStream(path, FileMode.Create, FileAccess.Write))
			{
				outFs.Write(data, 0, size);
			}
		}

		private static void UpdateMemoryFile(long value)
		{
			memoryFile.Seek(0, SeekOrigin.Begin);
			using (BinaryWriter bw = new BinaryWriter(memoryFile, Encoding.Default, true))
			{
				bw.Write(value);
			}
		}

		private static long ReadMemoryFileLongLong()
		{
			memoryFile.Seek(0, SeekOrigin.Begin);
			using (BinaryReader brMemory = new BinaryReader(memoryFile, Encoding.Default, true))
			{
				return brMemory.ReadInt64();
			}
		}
	}
}
