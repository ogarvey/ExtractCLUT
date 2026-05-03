using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.NeoGeo
{
    public static class NeoGeo
    {
        // Convert Sprite Format to CLUT data
        public static byte[] ConvertSpriteDataToCLUT(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                return Array.Empty<byte>();
            }

            // Find C_ROM files ending in c*.rom
            var files = Directory.GetFiles(folderPath, "*c*.rom")
                                 .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                 .ToList();

            if (files.Count == 0)
            {
                // Fallback to .bin just in case
                files = Directory.GetFiles(folderPath, "*c*.bin")
                                 .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                 .ToList();
                if (files.Count == 0) return Array.Empty<byte>();
            }

            // Neo Geo C-ROMs are paired: Odd (C1, C3, ...) and Even (C2, C4, ...)
            // Sorting files should typically put them in order c1, c2, c3, c4...
            var oddFiles = files.Where((f, i) => i % 2 == 0).ToList();
            var evenFiles = files.Where((f, i) => i % 2 == 1).ToList();

            if (oddFiles.Count == 0 || evenFiles.Count == 0)
            {
                return Array.Empty<byte>();
            }

            long totalOddSize = oddFiles.Sum(f => new FileInfo(f).Length);
            long totalEvenSize = evenFiles.Sum(f => new FileInfo(f).Length);
            int commonSize = (int)Math.Min(totalOddSize, totalEvenSize);

            if (commonSize == 0) return Array.Empty<byte>();

            byte[] oddBuffer = new byte[commonSize];
            byte[] evenBuffer = new byte[commonSize];

            int offset = 0;
            foreach (var file in oddFiles)
            {
                byte[] data = File.ReadAllBytes(file);
                int toCopy = Math.Min(data.Length, commonSize - offset);
                Buffer.BlockCopy(data, 0, oddBuffer, offset, toCopy);
                offset += toCopy;
                if (offset >= commonSize) break;
            }

            offset = 0;
            foreach (var file in evenFiles)
            {
                byte[] data = File.ReadAllBytes(file);
                int toCopy = Math.Min(data.Length, commonSize - offset);
                Buffer.BlockCopy(data, 0, evenBuffer, offset, toCopy);
                offset += toCopy;
                if (offset >= commonSize) break;
            }

            // Each tile is 16x16, 4bpp planar.
            // 4 blocks of 8x8 pixels per tile.
            // One block is 16 bytes in Odd ROM (bp0, bp1) and 16 bytes in Even ROM (bp2, bp3).
            // Total bytes per tile: 64 from Odd + 64 from Even = 128 bytes.
            int numTiles = commonSize / 64;
            byte[] clutData = new byte[numTiles * 256];

            for (int tileIdx = 0; tileIdx < numTiles; tileIdx++)
            {
                int srcTileOffset = tileIdx * 64;
                int dstTileOffset = tileIdx * 256;

                for (int b = 0; b < 4; b++)
                {
                    int xOfs = 0, yOfs = 0;
                    switch (b)
                    {
                        case 0: xOfs = 8; yOfs = 0; break;
                        case 1: xOfs = 8; yOfs = 8; break;
                        case 2: xOfs = 0; yOfs = 0; break;
                        case 3: xOfs = 0; yOfs = 8; break;
                        default: Debugger.Break(); break;
                    }

                    int blockOffset = srcTileOffset + b * 16;
                    for (int r = 0; r < 8; r++)
                    {
                        int rowOffset = blockOffset + r * 2;

                        // Neo Geo sprite data is "backwards" and big-endian interleaved
                        // Plane 1 is in byte 0, Plane 0 is in byte 1 of Odd ROMs
                        // Plane 3 is in byte 0, Plane 2 is in byte 1 of Even ROMs
                        byte bp1 = oddBuffer[rowOffset];
                        byte bp0 = oddBuffer[rowOffset + 1];
                        byte bp3 = evenBuffer[rowOffset];
                        byte bp2 = evenBuffer[rowOffset + 1];

                        for (int p = 0; p < 8; p++)
                        {
                            // "backwards" means bit 0 is pixel 0
                            int bitPos = p;
                            int pixel = ((bp0 >> bitPos) & 1) |
                                        (((bp1 >> bitPos) & 1) << 1) |
                                        (((bp2 >> bitPos) & 1) << 2) |
                                        (((bp3 >> bitPos) & 1) << 3);

                            int x = xOfs + p;
                            int y = yOfs + r;
                            clutData[dstTileOffset + y * 16 + x] = (byte)pixel;
                        }
                    }
                }
            }

            return clutData;
        }
    }
}
