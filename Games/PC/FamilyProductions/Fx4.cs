using System;
using System.Collections.Generic;
using System.IO;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.FamilyProductions
{
    public class Fx4Entry
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int CompressedSize { get; set; }
        public byte[] CompressedData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Decompresses this entry's scanline RLE into a Width*Height pixel array.
        /// Unwritten pixels remain 0; the caller should use the written mask for transparency.
        /// </summary>
        public byte[] DecompressPixels(out bool[] written)
        {
            var pixels = new byte[Width * Height];
            written = new bool[Width * Height];
            int src = 0;
            for (int y = 0; y < Height; y++)
            {
                int dst = y * Width;
                while (src < CompressedData.Length)
                {
                    byte skip = CompressedData[src++];
                    if (skip == 0xFF)
                        break; // end of scanline

                    dst += skip;
                    if (src >= CompressedData.Length)
                        break;

                    byte count = CompressedData[src++];
                    if (count == 0xFF)
                        break; // end of scanline after a skip

                    for (int i = 0; i < count && dst < pixels.Length && src < CompressedData.Length; i++)
                    {
                        pixels[dst] = CompressedData[src++];
                        written[dst] = true;
                        dst++;
                    }
                }
            }
            return pixels;
        }
    }

    public class Fx4File
    {
        public List<Fx4Entry> Entries { get; } = new List<Fx4Entry>();

        public static Fx4File Load(string path)
        {
            var data = File.ReadAllBytes(path);
            if (data.Length < 0x1E)
                throw new InvalidDataException($"FX4 file too small: {path}");

            // Some files use the .FX4 extension but are actually a different cutscene/script
            // format (also found with .KPF extension, e.g. SH-OPEN.FX4 / SH_OPEN.KPF).
            // Signature: first DWORD == 0x00000001 followed by a 0x20-byte header.
            if (data.Length >= 0x20 &&
                BitConverter.ToUInt32(data, 0) == 0x00000001)
            {
                throw new InvalidDataException(
                    $"{path} is a headerless FX4 / KPF cutscene script, not the standard sprite FX4 format.");
            }

            // Verified in FUN_3367_0237: fseek(fp, 0x1c, SEEK_SET); fread(&count, 2, 1, fp);
            ushort rawCount = BitConverter.ToUInt16(data, 0x1C);
            int entryCount = rawCount + 1; // loader stores count + 1

            var file = new Fx4File();
            int offset = 0x1E;
            for (int i = 0; i < entryCount; i++)
            {
                if (offset + 4 > data.Length)
                    throw new InvalidDataException($"FX4 header truncated at entry {i}");

                int width = data[offset];
                int height = data[offset + 1];
                // Verified in loader: size = (byte3 << 8) | byte4  (big-endian word)
                int compressedSize = (data[offset + 2] << 8) | data[offset + 3];

                if (offset + 4 + compressedSize > data.Length)
                    throw new InvalidDataException($"FX4 entry {i} exceeds file length");

                var compressed = new byte[compressedSize];
                Buffer.BlockCopy(data, offset + 4, compressed, 0, compressedSize);

                file.Entries.Add(new Fx4Entry
                {
                    Width = width,
                    Height = height,
                    CompressedSize = compressedSize,
                    CompressedData = compressed
                });

                offset += 4 + compressedSize;
            }
            return file;
        }

        /// <summary>
        /// Renders every entry to a PNG using the supplied 256-colour ImageSharp palette.
        /// Unwritten (transparent) areas are alpha 0.
        /// </summary>
        public void SaveImages(string outputDirectory, List<SixLabors.ImageSharp.Color> palette)
        {
            Directory.CreateDirectory(outputDirectory);
            for (int i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (entry.Width <= 0 || entry.Height <= 0)
                {
                    Console.WriteLine($"Skipping entry {i} with invalid dimensions {entry.Width}x{entry.Height}");
                    continue;
                }
                var pixels = entry.DecompressPixels(out var written);
                using var image = new Image<Rgba32>(entry.Width, entry.Height);
                for (int y = 0; y < entry.Height; y++)
                {
                    for (int x = 0; x < entry.Width; x++)
                    {
                        int idx = y * entry.Width + x;
                        if (written[idx])
                        {
                            int palIdx = pixels[idx];
                            if (palIdx < palette.Count)
                                image[x, y] = (Rgba32)palette[palIdx];
                        }
                    }
                }
                var fileName = $"{Path.GetFileNameWithoutExtension(outputDirectory)}_{i:D4}.png";
                var filePath = Path.Combine(outputDirectory, fileName);
                image.SaveAsPng(filePath);
                Console.WriteLine($"Saved {filePath} ({entry.Width}x{entry.Height})");
            }
        }
    }
}
