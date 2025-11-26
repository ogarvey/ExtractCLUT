using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
	public enum PIDFlags
	{
		WAP_PID_FLAG_TRANSPARENCY = 1 << 0,
		WAP_PID_FLAG_VIDEO_MEMORY = 1 << 1,
		WAP_PID_FLAG_SYSTEM_MEMORY = 1 << 2,
		WAP_PID_FLAG_MIRROR = 1 << 3,
		WAP_PID_FLAG_INVERT = 1 << 4,
		WAP_PID_FLAG_COMPRESSION = 1 << 5,
		WAP_PID_FLAG_LIGHTS = 1 << 6,
		WAP_PID_FLAG_EMBEDDED_PALETTE = 1 << 7,
	};

	public class PIDFile
	{
		public uint FileDesc { get; set; } // Always 0x0a000000
		public uint Flags { get; set; }
		public uint Width { get; set; }
		public uint Height { get; set; }
		public int OffsetX { get; set; }
		public int OffsetY { get; set; }
		public uint Unknown1 { get; set; } // Always 0
		public uint Unknown2 { get; set; } // Always 0
		public byte[] Data { get; set; }

		public bool HasPalette => (Flags & (uint)PIDFlags.WAP_PID_FLAG_EMBEDDED_PALETTE) != 0;
		public bool IsCompressed => (Flags & (uint)PIDFlags.WAP_PID_FLAG_COMPRESSION) != 0;
		public bool HasTransparency => (Flags & (uint)PIDFlags.WAP_PID_FLAG_TRANSPARENCY) != 0;
		public bool IsMirrored => (Flags & (uint)PIDFlags.WAP_PID_FLAG_MIRROR) != 0;
		public bool IsInverted => (Flags & (uint)PIDFlags.WAP_PID_FLAG_INVERT) != 0;

		public PIDFile(string filePath)
		{
			using var reader = new BinaryReader(File.OpenRead(filePath));
			FileDesc = reader.ReadUInt32();
			Flags = reader.ReadUInt32();
			Width = reader.ReadUInt32();
			Height = reader.ReadUInt32();
			OffsetX = reader.ReadInt32();
			OffsetY = reader.ReadInt32();
			Unknown1 = reader.ReadUInt32();
			Unknown2 = reader.ReadUInt32();
			Data = PIDDecompressor.Decompress(reader, this);
		}
		public override string ToString()
		{
			return $"PID File: {Width}x{Height}, Offset: ({OffsetX},{OffsetY}), Flags: 0x{Flags:X8}, Data Length: {Data.Length}";
		}
	}
	public static class PIDDecompressor
	{
		public static byte[] Decompress(BinaryReader reader, PIDFile pidFile)
		{
			var expectedOutputSize = pidFile.Width * pidFile.Height;
			var x = 0;
			var y = 0;
			var controlByte = 0;
			var output = new List<byte>();

			try
			{
				if (pidFile.IsCompressed)
				{
					while (y < pidFile.Height)
					{
						controlByte = reader.ReadByte();

						if(controlByte > 128)
            {
              var i = controlByte - 128;
							while ((i > 0) && y < pidFile.Height)
              {
                output.Add(0);
								x++;
								if (x == pidFile.Width)
								{
									x = 0;
									y++;
								}
								i--;
              }
            } 
						else
            {
              var i = controlByte;
							while (i >0 && y < pidFile.Height)
              {
                output.Add(reader.ReadByte());
								x++;
								if (x == pidFile.Width)
								{
									x = 0;
									y++;
								}
								i--;
              }
            }
					}
				}
				else
				{
					while (y < pidFile.Height)
					{
						var i = 1;
						controlByte = reader.ReadByte();

						if (controlByte > 192)
						{
							i = controlByte - 192;
							controlByte = reader.ReadByte();
						}

						while ((i > 0) && y < pidFile.Height)
						{
							output.Add((byte)controlByte);
							x++;
							if (x == pidFile.Width)
							{
								x = 0;
								y++;
							}
							i--;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error during PID decompression: {ex.Message}");
			}
			Console.WriteLine($"Decompressed PID data size: {output.Count} bytes (expected: {expectedOutputSize} bytes)");
			return output.ToArray();
		}

	}
}
