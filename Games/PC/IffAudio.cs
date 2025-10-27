using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public class IffAudio
    {
        public static void ConvertIff8SvxToWav(string inputPath, string outputPath)
        {
            using var reader = new BinaryReader(File.OpenRead(inputPath));

            // Read and validate FORM header
            var formSignature = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (formSignature != "FORM")
                throw new InvalidDataException("Not a valid IFF file - missing FORM signature");

            var formSize = reader.ReadBigEndianUInt32();
            var format = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (format != "8SVX")
                throw new InvalidDataException("Not a valid 8SVX file");

            // Parse chunks to find VHDR and BODY
            VoiceHeader? vhdr = null;
            byte[]? audioData = null;

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                if (reader.BaseStream.Position + 8 > reader.BaseStream.Length)
                    break;

                var chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
                var chunkSize = reader.ReadBigEndianUInt32();

                switch (chunkId)
                {
                    case "VHDR":
                        vhdr = ReadVoiceHeader(reader);
                        break;

                    case "BODY":
                        audioData = reader.ReadBytes((int)chunkSize);
                        break;

                    default:
                        // Skip unknown chunks
                        reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                        break;
                }

                // Align to even byte boundary (IFF requirement)
                if (chunkSize % 2 == 1 && reader.BaseStream.Position < reader.BaseStream.Length)
                    reader.ReadByte();
            }

            if (vhdr == null)
                throw new InvalidDataException("VHDR chunk not found");
            if (audioData == null)
                throw new InvalidDataException("BODY chunk not found");

            // Convert audio data if compressed
            if (vhdr.Compression == 1)
            {
                audioData = DecompressFibonacciDelta(audioData);
            }

            // Convert signed bytes to 16-bit PCM
            var pcmData = ConvertSignedBytesToPcm16(audioData);

            // Write WAV file
            WriteWavFile(outputPath, pcmData, vhdr.SamplesPerSecond, 1, 16);
        }

        private static VoiceHeader ReadVoiceHeader(BinaryReader reader)
        {
            return new VoiceHeader
            {
                OneShotSamples = reader.ReadBigEndianUInt32(),
                RepeatSamples = reader.ReadBigEndianUInt32(),
                SamplesPerCycle = reader.ReadBigEndianUInt32(),
                SamplesPerSecond = reader.ReadBigEndianUInt16(),
                Octaves = reader.ReadByte(),
                Compression = reader.ReadByte(),
                Volume = reader.ReadBigEndianUInt32()
            };
        }

        private static byte[] DecompressFibonacciDelta(byte[] compressedData)
        {
            var output = new List<byte>();
            sbyte lastValue = 0;

            // Fibonacci delta encoding decompression
            foreach (var delta in compressedData)
            {
                lastValue += (sbyte)delta;
                output.Add((byte)lastValue);
            }

            return output.ToArray();
        }

        private static byte[] ConvertSignedBytesToPcm16(byte[] signedBytes)
        {
            var pcmData = new byte[signedBytes.Length * 2];

            for (int i = 0; i < signedBytes.Length; i++)
            {
                // Convert signed byte (-128 to +127) to 16-bit PCM
                sbyte signedByte = (sbyte)signedBytes[i];
                short pcmValue = (short)(signedByte * 256); // Scale to 16-bit range

                // Write as little-endian 16-bit
                pcmData[i * 2] = (byte)(pcmValue & 0xFF);
                pcmData[i * 2 + 1] = (byte)((pcmValue >> 8) & 0xFF);
            }

            return pcmData;
        }

        private static void WriteWavFile(string outputPath, byte[] pcmData, ushort sampleRate, ushort channels, ushort bitsPerSample)
        {
            using var writer = new BinaryWriter(File.Create(outputPath));

            var bytesPerSample = bitsPerSample / 8;
            var blockAlign = (ushort)(channels * bytesPerSample);
            var byteRate = sampleRate * blockAlign;

            // RIFF header
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write((uint)(36 + pcmData.Length)); // File size - 8
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write((uint)16); // fmt chunk size
            writer.Write((ushort)1); // PCM format
            writer.Write(channels);
            writer.Write((uint)sampleRate);
            writer.Write((uint)byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            // data chunk
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write((uint)pcmData.Length);
            writer.Write(pcmData);
        }

    }
    public class VoiceHeader
    {
        public uint OneShotSamples { get; set; }
        public uint RepeatSamples { get; set; }
        public uint SamplesPerCycle { get; set; }
        public ushort SamplesPerSecond { get; set; }
        public byte Octaves { get; set; }
        public byte Compression { get; set; }
        public uint Volume { get; set; }
    }
}
