using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.Youngblood
{
    public static class GraExtractor
    {

        public static List<Image<Rgba32>> DecodeGraImages(
            byte[] chunk,
            bool useRgb555 = false,
            bool swapRedBlue = false)
        {
            var images = new List<Image<Rgba32>>();
            if (chunk == null || chunk.Length < 0x24) return images;

            uint type = ReadUInt32(chunk, 0x00); // 0=simple, 1=complex
            int pixelBase = ReadInt32(chunk, 0x18);
            int tableOffset = ReadInt32(chunk, 0x20);

            int count = type == 1 ? (int)ReadUInt32(chunk, 0x1C) : 1;
            if (count <= 0) count = 1;

            for (int i = 0; i < count; i++)
            {
                int entry = tableOffset+ i * 0x24;
                if (entry + 0x10 > chunk.Length) break;

                int dataRel = ReadInt32(chunk, entry + 0x08);
                int dataOffset = pixelBase + dataRel;

                int sizeBytes = ReadInt32(chunk, entry + 0x04);
                int width = ReadUInt16(chunk, entry + 0x0C);
                int height = ReadUInt16(chunk, entry + 0x0E);

                if (width <= 0 || height <= 0) continue;
                int needed = width * height * 2;
                if (sizeBytes < needed) sizeBytes = needed;
                if (dataOffset < 0 || dataOffset + sizeBytes > chunk.Length) continue;

                images.Add(Decode16BitImage(chunk, dataOffset, width, height, useRgb555, swapRedBlue));
            }

            return images;
        }

        private static Image<Rgba32> Decode16BitImage(
            byte[] data, int offset, int width, int height,
            bool useRgb555, bool swapRedBlue)
        {
            var image = new Image<Rgba32>(width, height);
            int src = offset;

            for (int y = 0; y < height; y++)
            {
                Span<Rgba32> row = image.DangerousGetPixelRowMemory(y).Span;
                for (int x = 0; x < width; x++)
                {
                    ushort v = (ushort)(data[src] | (data[src + 1] << 8));
                    src += 2;

                    int r, g, b;
                    if (useRgb555)
                    {
                        r = (v >> 10) & 0x1F;
                        g = (v >> 5) & 0x1F;
                        b = v & 0x1F;
                        r = (r * 255 + 15) / 31;
                        g = (g * 255 + 15) / 31;
                        b = (b * 255 + 15) / 31;
                    }
                    else
                    {
                        r = (v >> 11) & 0x1F;
                        g = (v >> 5) & 0x3F;
                        b = v & 0x1F;
                        r = (r * 255 + 15) / 31;
                        g = (g * 255 + 31) / 63;
                        b = (b * 255 + 15) / 31;
                    }

                    if (swapRedBlue)
                    {
                        int tmp = r; r = b; b = tmp;
                    }

                    row[x] = new Rgba32((byte)r, (byte)g, (byte)b, (r == 0 && g == 0 && b == 0) ? (byte)0 : (byte)255);
                }
            }

            return image;
        }

        private static ushort ReadUInt16(byte[] b, int o) => (ushort)(b[o] | (b[o + 1] << 8));
        private static uint ReadUInt32(byte[] b, int o) => (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
        private static int ReadInt32(byte[] b, int o) => (int)ReadUInt32(b, o);
    }
}
