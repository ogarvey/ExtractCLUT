using System.Text;
using System;
using System.IO;

namespace ExtractCLUT.Helpers
{

	public static class AudioHelper
	{
		private static readonly int[] K0 = { 0, 240, 460, 392 };
		private static readonly int[] K1 = { 0, 0, -208, -220 };

		private static int lk0 = 0;
		private static int rk0 = 0;
		private static int lk1 = 0;
		private static int rk1 = 0;

		public static void OutputAudio(byte[] inputData, string inputFile, uint frequency, byte bps, bool isMono)
		{
			List<short> left = new List<short>();
			List<short> right = new List<short>();

			for (int i = 0; i < inputData.Length; i += 2304)
			{
				byte[] chunk = new byte[2304];
				Array.Copy(inputData, i, chunk, 0, 2304);
				var isLevelA = bps == 8 && frequency == 37800;
				DecodeAudioSector(chunk, left, right, isLevelA, !isMono);
			}

			Directory.CreateDirectory(Path.GetDirectoryName(inputFile) + "\\wav_files");
			var outputPath = Path.Combine(Path.GetDirectoryName(inputFile) + "\\wav_files", Path.GetFileNameWithoutExtension(inputFile) + ".wav");
			using (var outStream = File.Create(outputPath))
			{
				WriteWAV(outStream, new WAVHeader { ChannelNumber = (ushort)(isMono ? 1 : 2), Frequency = frequency }, left, right);
			}
		}

		public static void ResetAudioFiltersDelay()
		{
			lk0 = 0;
			rk0 = 0;
			lk1 = 0;
			rk1 = 0;
		}

		private static short Lim16(int num)
		{
			return num > short.MaxValue ? short.MaxValue : num < short.MinValue ? short.MinValue : (short)num;
		}
		private static byte DecodeADPCM(int su, int gain, sbyte[][] sd, ref byte[] ranges, ref byte[] filters, bool stereo, List<short> left, List<short> right)
		{
			byte index = 0;

			for (int i = 0; i < su; i++)
			{
				ushort curGain = (ushort)(2 << (gain - ranges[i]));
				for (byte ss = 0; ss < 28; ss++)
				{
					if (stereo && (i & 1) == 1)
					{
						short sample = Lim16((sd[i][ss] * curGain) + ((rk0 * K0[filters[i]] + rk1 * K1[filters[i]]) / 256));
						rk1 = rk0;
						rk0 = sample;
						right.Add(sample);
						index++;
					}
					else
					{
						short sample = Lim16((sd[i][ss] * curGain) + ((lk0 * K0[filters[i]] + lk1 * K1[filters[i]]) / 256));
						lk1 = lk0;
						lk0 = sample;
						left.Add(sample);
						index++;
					}
				}
			}

			return index;
		}
		public struct WAVHeader
		{
			public ushort ChannelNumber;
			public uint Frequency;
		}

		public static void WriteWAV(Stream outStream, WAVHeader wavHeader, List<short> left, List<short> right)
		{
			ushort bytePerBloc = (ushort)(wavHeader.ChannelNumber * 2);
			uint bytePerSec = wavHeader.Frequency * bytePerBloc;
			uint dataSize = (uint)(left.Count * 2 + right.Count * 2);
			uint wavSize = 36 + dataSize;

			using (BinaryWriter writer = new BinaryWriter(outStream, Encoding.ASCII, leaveOpen: true))
			{
				writer.Write("RIFF".ToCharArray());
				writer.Write(wavSize);
				writer.Write("WAVE".ToCharArray());
				writer.Write("fmt ".ToCharArray());
				writer.Write(0x10);
				writer.Write((ushort)1); // audio format

				writer.Write(wavHeader.ChannelNumber);
				writer.Write(wavHeader.Frequency);
				writer.Write(bytePerSec);
				writer.Write(bytePerBloc);
				writer.Write((ushort)0x10);
				writer.Write("data".ToCharArray());
				writer.Write(dataSize);

				if (right.Count > 0) // stereo
				{
					for (int i = 0; i < left.Count && i < right.Count; i++)
					{
						writer.Write(left[i]);
						writer.Write(right[i]);
					}
				}
				else // mono
				{
					foreach (short value in left)
					{
						writer.Write(value);
					}
				}
			}
		}

		public static ushort DecodeAudioSector(byte[] data, List<short> left, List<short> right, bool levelA, bool stereo)
		{
			ushort index = 0;
			if (levelA) // Level A (8 bits per sample)
			{
				for (byte sg = 0; sg < 18; sg++)
				{
					index += DecodeLevelASoundGroup(stereo, data.AsSpan(128 * sg, 128).ToArray(), left, right);
				}
			}
			else // Level B and C (4 bits per sample)
			{
				for (byte sg = 0; sg < 18; sg++)
				{
					index += DecodeLevelBCSoundGroup(stereo, data.AsSpan(128 * sg, 128).ToArray(), left, right);
				}
			}
			return index;
		}
		public static byte DecodeLevelASoundGroup(bool stereo, byte[] data, List<short> left, List<short> right)
		{
			byte index = 16;
			byte[] range = new byte[4];
			byte[] filter = new byte[4];
			sbyte[][] SD = new sbyte[4][];
			for (int i = 0; i < 4; i++)
			{
				SD[i] = new sbyte[28];
			}

			for (byte i = 0; i < 4; i++)
			{
				range[i] = (byte)(data[i] & 0x0F);
				filter[i] = (byte)(data[i] >> 4);
			}

			for (byte ss = 0; ss < 28; ss++) // sound sample
			{
				for (byte su = 0; su < 4; su++) // sound unit
				{
					SD[su][ss] = (sbyte)data[index++];
				}
			}

			index = DecodeADPCM(4, 8, SD, ref range, ref filter, stereo, left, right);
			return index;
		}

		private static byte DecodeLevelBCSoundGroup(bool stereo, byte[] data, List<short> left, List<short> right)
		{
			byte index = 4;
			byte[] range = new byte[8];
			byte[] filter = new byte[8];
			sbyte[][] SD = new sbyte[8][];

			for (int i = 0; i < 8; i++)
			{
				SD[i] = new sbyte[28];
				range[i] = (byte)(data[i + index] & 0x0F);
				filter[i] = (byte)(data[i + index] >> 4);
			}

			index = 16;
			for (byte ss = 0; ss < 28; ss++)
			{
				for (byte su = 0; su < 8; su += 2)
				{
					byte SB = data[index++];
					SD[su][ss] = (sbyte)(SB & 0x0F);
					if (SD[su][ss] >= 8) SD[su][ss] -= 16;
					SD[su + 1][ss] = (sbyte)(SB >> 4);
					if (SD[su + 1][ss] >= 8) SD[su + 1][ss] -= 16;
				}
			}

			index = DecodeADPCM(8, 12, SD, ref range, ref filter, stereo, left, right);
			return index;
		}

	}

}
