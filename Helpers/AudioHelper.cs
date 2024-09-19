using System.Text;
using System;
using System.IO;
using System.Diagnostics;

namespace ExtractCLUT.Helpers
{

	public static class AudioHelper
	{
		public static void ConvertPcmToWav(byte[] pcmData, string wavFilePath, int sampleRate, int channels, int bitsPerSample)
		{
			using (var wavStream = new FileStream(wavFilePath, FileMode.Create, FileAccess.Write))
			using (var writer = new BinaryWriter(wavStream))
			{
				// RIFF header
				writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
				writer.Write(36 + pcmData.Length); // File size minus first 8 bytes
				writer.Write(new char[4] { 'W', 'A', 'V', 'E' });

				// fmt subchunk
				writer.Write(new char[4] { 'f', 'm', 't', ' ' });
				writer.Write(16); // Subchunk size (16 for PCM)
				writer.Write((short)1); // Audio format (1 = PCM)
				writer.Write((short)channels); // Number of channels
				writer.Write(sampleRate); // Sample rate
				writer.Write(sampleRate * channels * bitsPerSample / 8); // Byte rate
				writer.Write((short)(channels * bitsPerSample / 8)); // Block align
				writer.Write((short)bitsPerSample); // Bits per sample

				// data subchunk
				writer.Write(new char[4] { 'd', 'a', 't', 'a' });
				writer.Write(pcmData.Length); // Data chunk size
				writer.Write(pcmData); // Write PCM data to wav file
			}
		}
		private static string ffmpegPath = "ffmpeg";
		public static void ConvertIffToWav(string inputFilePath, string outputFilePath)
		{
			// Create a new process to run ffmpeg
			Process ffmpegProcess = new Process();

			// Set up the process start info
			ffmpegProcess.StartInfo.FileName = "ffmpeg";
			ffmpegProcess.StartInfo.Arguments = $"-i \"{inputFilePath}\" \"{outputFilePath}\"";
			ffmpegProcess.StartInfo.UseShellExecute = false;
			ffmpegProcess.StartInfo.RedirectStandardOutput = true;
			ffmpegProcess.StartInfo.RedirectStandardError = true;
			ffmpegProcess.StartInfo.CreateNoWindow = true;

			// Set up event handlers to capture output and error data
			ffmpegProcess.OutputDataReceived += (sender, e) =>
			{
				if (!string.IsNullOrEmpty(e.Data))
				{
					Console.WriteLine($"Output: {e.Data}");
				}
			};
			ffmpegProcess.ErrorDataReceived += (sender, e) =>
			{
				if (!string.IsNullOrEmpty(e.Data))
				{
					Console.WriteLine($"Error: {e.Data}");
				}
			};

			// Start the process
			ffmpegProcess.Start();

			// Begin asynchronous read of the output and error streams
			ffmpegProcess.BeginOutputReadLine();
			ffmpegProcess.BeginErrorReadLine();

			// Wait for the process to exit
			ffmpegProcess.WaitForExit();

			// Check if the process exited successfully
			if (ffmpegProcess.ExitCode != 0)
			{
				throw new Exception($"ffmpeg exited with code {ffmpegProcess.ExitCode}");
			}
		}
		
		public static void ConvertMp2ToWavAndMp3(string inputFilePath, string outputPath, string format = "wav")
		{
			try
			{
				if (!File.Exists(inputFilePath))
				{
					throw new FileNotFoundException("The input file does not exist.");
				}

				if (format != "wav" && format != "mp3")
				{
					throw new ArgumentException("The output format must be either 'wav' or 'mp3'.");
				}

				if (format == "wav")
				{
					// Convert to WAV
					string wavArguments = $"-i \"{inputFilePath}\" \"{outputPath}\"";
					ExecuteFFmpegCommand(wavArguments);
				}
				else
				{
					// Convert to MP3
					string mp3Arguments = $"-i \"{inputFilePath}\" \"{outputPath}\"";
					ExecuteFFmpegCommand(mp3Arguments);
				}
				Console.WriteLine("Conversion completed successfully.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An error occurred: {ex.Message}");
			}
		}

		private static void ExecuteFFmpegCommand(string arguments)
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = ffmpegPath,
				Arguments = arguments,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using (Process process = new Process { StartInfo = startInfo })
			{
				process.Start();
				string output = process.StandardOutput.ReadToEnd();
				string error = process.StandardError.ReadToEnd();
				process.WaitForExit();

				if (process.ExitCode != 0)
				{
					throw new Exception($"FFmpeg error: {error}");
				}
				else
				{
					Console.WriteLine(output);
				}
			}
		}
		private static readonly int[] K0 = { 0, 240, 460, 392 };
		private static readonly int[] K1 = { 0, 0, -208, -220 };

		private static int lk0 = 0;
		private static int rk0 = 0;
		private static int lk1 = 0;
		private static int rk1 = 0;

		public static void OutputAudio(byte[] inputData, string outputFile, uint frequency, byte bps, bool isMono)
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

			using (var outStream = File.Create(outputFile))
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
			uint dataSize = (uint)(left.Count * 2 *wavHeader.ChannelNumber);
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
