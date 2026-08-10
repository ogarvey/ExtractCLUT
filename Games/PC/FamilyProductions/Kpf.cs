using System;
using System.Collections.Generic;
using System.IO;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.FamilyProductions
{
    /// <summary>
    /// Reader for the .KPF / headerless .FX4 cutscene image format used by Family Productions.
    /// </summary>
    /// <remarks>
    /// These files are not the standard sprite FX4 format. They start with the little-endian
    /// DWORD 0x00000001, followed by a 0x40-byte header, then RLE-compressed VGA frames.
    /// The RLE scheme matches the full-screen loader in SH_GAME.EXE (FUN_2acb_0004):
    /// a byte >= 0xC0 encodes a run of (byte &amp; 0x3F) pixels using the next byte as the
    /// palette index; all other bytes are literal palette indices.
    /// </remarks>
    public class KpfEntry
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Pixels { get; set; } = Array.Empty<byte>();
    }

    public class KpfFile
    {
        public List<KpfEntry> Entries { get; } = new List<KpfEntry>();

        public static KpfFile Load(string path)
        {
            var data = File.ReadAllBytes(path);
            if (data.Length < 0x40)
                throw new InvalidDataException($"KPF file too small: {path}");

            // Verified signature: first DWORD must be 0x00000001.
            uint signature = BitConverter.ToUInt32(data, 0);
            if (signature != 0x00000001)
                throw new InvalidDataException($"{path} is not a KPF file (signature 0x{signature:X8}).");

            // Verified field: image width is a little-endian uint16 at offset 0x3C.
            int width = BitConverter.ToUInt16(data, 0x3C);
            if (width <= 0)
                throw new InvalidDataException($"{path} has invalid KPF width {width}.");

            // Decode the whole RLE stream into a flat pixel buffer. Most observed files store
            // one or more full 200-row VGA frames (e.g. SH_OPEN.KPF = 120x200, SH-PIC.FX4 has
            // two 132x200 frames). Smaller files such as HIGH.KPF appear to be short overlays.
            // Verified: pixel data begins at offset 0x40 (the 0x20-0x3F region is additional
            // header fields, including the width at 0x3C).
            int offset = 0x40;
            var decoded = new List<byte>();
            while (offset < data.Length)
            {
                byte op = data[offset++];
                if ((op & 0xC0) == 0xC0)
                {
                    int count = op & 0x3F;
                    if (offset >= data.Length)
                        break;

                    byte value = data[offset++];
                    while (count-- > 0)
                    {
                        decoded.Add(value);
                    }
                }
                else
                {
                    decoded.Add(op);
                }
            }

            if (decoded.Count == 0)
                throw new InvalidDataException($"{path} did not contain any decodable KPF pixels.");

            const int FrameHeight = 200;
            var file = new KpfFile();
            int position = 0;

            while (position < decoded.Count)
            {
                int remaining = decoded.Count - position;
                int frameHeight;

                if (remaining >= width * FrameHeight)
                {
                    // A full 200-row VGA frame.
                    frameHeight = FrameHeight;
                }
                else if (remaining >= width)
                {
                    // A shorter overlay/frame; keep only complete scanlines and
                    // drop the trailing partial scanline (it is padding).
                    frameHeight = remaining / width;
                }
                else
                {
                    // Less than one scanline of padding data; stop decoding.
                    break;
                }

                int framePixelCount = frameHeight * width;
                var pixels = new byte[framePixelCount];
                decoded.CopyTo(position, pixels, 0, framePixelCount);

                file.Entries.Add(new KpfEntry
                {
                    Width = width,
                    Height = frameHeight,
                    Pixels = pixels
                });

                position += framePixelCount;
            }

            return file;
        }

        public void SaveImages(string outputDirectory, List<SixLabors.ImageSharp.Color> palette)
        {
            Directory.CreateDirectory(outputDirectory);
            for (int i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                using var image = new Image<Rgba32>(entry.Width, entry.Height);
                for (int y = 0; y < entry.Height; y++)
                {
                    for (int x = 0; x < entry.Width; x++)
                    {
                        int idx = y * entry.Width + x;
                        int palIdx = entry.Pixels[idx];
                        if (palIdx < palette.Count)
                            image[x, y] = (Rgba32)palette[palIdx];
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
