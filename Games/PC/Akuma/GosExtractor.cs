using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.Akuma
{
	public sealed class GosFrame
	{
		public byte DurationTicks { get; init; }
		public byte[] SpanData { get; init; } = Array.Empty<byte>();
	}

	public sealed class GosSequence
	{
		public int Index { get; init; }
		public byte FrameCount { get; init; }
		public ushort Width { get; init; }
		public ushort Height { get; init; }
		public ushort AnchorX { get; init; }
		public ushort AnchorY { get; init; }
		public IReadOnlyList<GosFrame> Frames { get; init; } = Array.Empty<GosFrame>();
	}

	/// <summary>
	/// Parser/extractor for Akuma .gos sprite files.
	///
	/// Inferred format highlights:
	/// - byte at offset +2 starts a chain of sequence records
	/// - each sequence record has a 0x1B-byte header
	/// - header fields used by renderer: frameCount, width, height, anchorX, anchorY
	/// - each frame payload is span-RLE rows terminated with 0xFF per row
	/// </summary>
	public static class AkumaGosExtractor
	{
		private const int SequenceHeaderSize = 0x1B;

		public static IReadOnlyList<GosSequence> Parse(string gosPath)
		{
			var data = File.ReadAllBytes(gosPath);
			if (data.Length < 4)
			{
				throw new InvalidDataException("File too small to be a valid .gos file.");
			}

			var sequences = new List<GosSequence>();
			int sequenceHeaderOffset = 3;
			byte frameCount = data[2];
			int sequenceIndex = 0;

			while (frameCount != 0xFF)
			{
				if (sequenceHeaderOffset + SequenceHeaderSize > data.Length)
				{
					throw new InvalidDataException("Truncated sequence header in .gos file.");
				}

				ushort width = ReadUInt16LE(data, sequenceHeaderOffset);
				ushort height = ReadUInt16LE(data, sequenceHeaderOffset + 2);
				ushort anchorX = ReadUInt16LE(data, sequenceHeaderOffset + 4);
				ushort anchorY = ReadUInt16LE(data, sequenceHeaderOffset + 6);

				int cursor = sequenceHeaderOffset + SequenceHeaderSize;
				var frames = new List<GosFrame>(frameCount);

				for (int i = 0; i < frameCount; i++)
				{
					if (cursor + 5 > data.Length)
					{
						throw new InvalidDataException("Truncated frame entry in .gos file.");
					}

					ushort payloadLength = ReadUInt16LE(data, cursor);
					byte duration = data[cursor + 4];
					int spanDataOffset = cursor + 5;

					if (spanDataOffset + payloadLength > data.Length)
					{
						throw new InvalidDataException("Frame payload exceeds file bounds.");
					}

					var spanData = new byte[payloadLength];
					Buffer.BlockCopy(data, spanDataOffset, spanData, 0, payloadLength);

					frames.Add(new GosFrame
					{
						DurationTicks = duration,
						SpanData = spanData
					});

					cursor = spanDataOffset + payloadLength;
				}

				if (cursor >= data.Length)
				{
					throw new InvalidDataException("Missing next sequence marker byte.");
				}

				sequences.Add(new GosSequence
				{
					Index = sequenceIndex,
					FrameCount = frameCount,
					Width = width,
					Height = height,
					AnchorX = anchorX,
					AnchorY = anchorY,
					Frames = frames
				});

				frameCount = data[cursor];
				sequenceHeaderOffset = cursor + 1;
				sequenceIndex++;
			}

			return sequences;
		}

		public static Image<Rgba32> DecodeFrameToImage(GosSequence sequence, GosFrame frame, bool rgb565 = true)
		{
			var image = new Image<Rgba32>(sequence.Width, sequence.Height);

			int p = 0;
			for (int y = 0; y < sequence.Height && p < frame.SpanData.Length; y++)
			{
				int x = 0;

				while (p < frame.SpanData.Length)
				{
					byte skip = frame.SpanData[p++];
					if (skip == 0xFF)
					{
						break;
					}

					if (p >= frame.SpanData.Length)
					{
						break;
					}

					byte pixelCount = frame.SpanData[p++];
					x += skip;

					for (int i = 0; i < pixelCount && p + 1 < frame.SpanData.Length; i++)
					{
						ushort packed = (ushort)(frame.SpanData[p] | (frame.SpanData[p + 1] << 8));
						p += 2;

						int px = x + i;
						if ((uint)px < sequence.Width)
						{
							image[px, y] = ConvertPacked16ToColor(packed, rgb565);
						}
					}

					x += pixelCount;
				}
			}

			return image;
		}

		public static void ExportAllFrames(string gosPath, string outputDir, bool rgb565 = true)
		{
			Directory.CreateDirectory(outputDir);
			string baseName = Path.GetFileNameWithoutExtension(gosPath);

			var sequences = Parse(gosPath);
			foreach (var sequence in sequences)
			{
				for (int i = 0; i < sequence.Frames.Count; i++)
				{
					using var image = DecodeFrameToImage(sequence, sequence.Frames[i], rgb565);
					string outPath = Path.Combine(
							outputDir,
							$"{baseName}_seq{sequence.Index:D3}_frm{i:D3}_dur{sequence.Frames[i].DurationTicks:D3}.png");
					image.SaveAsPng(outPath);
				}
			}
		}

		private static ushort ReadUInt16LE(byte[] data, int offset)
				=> (ushort)(data[offset] | (data[offset + 1] << 8));

		private static Rgba32 ConvertPacked16ToColor(ushort pixel, bool rgb565)
		{
			if (rgb565)
			{
				int r = ((pixel >> 11) & 0x1F) * 255 / 31;
				int g = ((pixel >> 5) & 0x3F) * 255 / 63;
				int b = (pixel & 0x1F) * 255 / 31;
				return new Rgba32((byte)r, (byte)g, (byte)b, 255);
			}
			else
			{
				int r = ((pixel >> 10) & 0x1F) * 255 / 31;
				int g = ((pixel >> 5) & 0x1F) * 255 / 31;
				int b = (pixel & 0x1F) * 255 / 31;
				return new Rgba32((byte)r, (byte)g, (byte)b, 255);
			}
		}
	}
}
