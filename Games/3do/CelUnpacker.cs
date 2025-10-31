using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ExtractCLUT.Helpers;
using Color = SixLabors.ImageSharp.Color;

namespace ExtractCLUT.Games.ThreeDO
{
    public static class CelUnpacker
    {
        // Packet type constants from 3DO CEL specification
        private const int PACK_EOL = 0b00;         // End of line
        private const int PACK_LITERAL = 0b01;     // Literal pixel data
        private const int PACK_TRANSPARENT = 0b10; // Transparent pixels
        private const int PACK_REPEAT = 0b11;      // Repeat single pixel

        /// <summary>
        /// Unpacks 3DO CEL data that has been compressed using the coded_packed format.
        /// Each row is independently compressed with an offset header followed by packets.
        /// Width and height are automatically determined from the data structure.
        /// </summary>
        /// <param name="packedData">The packed CEL data</param>
        /// <param name="bitsPerPixel">Bits per pixel (1, 2, 4, 6, 8, or 16)</param>
        /// <param name="verbose">Enable detailed packet logging</param>
        /// <returns>Unpacked pixel data with dimensions</returns>
        public static CelImageData UnpackCodedPackedCelData(byte[] packedData, int bitsPerPixel = 8, bool verbose = false, bool skipCompSize = false)
        {
            return UnpackCelData(packedData, bitsPerPixel, coded: true, packed: true, verbose: verbose, skipCompSize: skipCompSize);
        }

        /// <summary>
        /// Unpacks 3DO CEL data in coded_packed format with known dimensions.
        /// This is the preferred method when dimensions are known from CCB header.
        /// Uses packet encoding with row offset headers for each scanline.
        /// </summary>
        /// <param name="packedData">The packed CEL data</param>
        /// <param name="width">Width in pixels from CCB header</param>
        /// <param name="height">Height in pixels from CCB header</param>
        /// <param name="bitsPerPixel">Bits per pixel (1, 2, 4, 6, 8, or 16)</param>
        /// <param name="verbose">Enable detailed packet logging</param>
        /// <returns>Unpacked pixel data with dimensions</returns>
        public static CelImageData UnpackCodedPackedWithDimensions(byte[] packedData, int width, int height, int bitsPerPixel = 8, bool verbose = false, bool skipUncompSize = false)
        {
            if (verbose)
            {
                Console.WriteLine($"\n[VERBOSE] UnpackCodedPackedWithDimensions called:");
                Console.WriteLine($"  Width: {width}");
                Console.WriteLine($"  Height: {height}");
                Console.WriteLine($"  BitsPerPixel: {bitsPerPixel}");
                Console.WriteLine($"  Data size: {packedData.Length} bytes");
            }

            // Skip the first 4 bytes which appear to be a preamble/header in PDAT chunks
            // These bytes don't follow the row header structure and cause parsing errors
            byte[] actualData = packedData;
            if (packedData.Length > 4 && skipUncompSize)
            {
                actualData = packedData.Skip(4).ToArray();
                if (verbose)
                {
                    Console.WriteLine($"  Skipping 4-byte preamble, actual data size: {actualData.Length} bytes");
                }
            }

            int bytesPerPixel = (bitsPerPixel + 7) / 8;
            byte[] unpackedData = new byte[width * height * bytesPerPixel];
            bool[] transparencyMask = new bool[width * height];
            byte[]? amvData = (bitsPerPixel == 8) ? new byte[width * height] : null;

            // Initialize all pixels as transparent by default
            for (int i = 0; i < transparencyMask.Length; i++)
            {
                transparencyMask[i] = true;
            }

            BitReader bitReader = new BitReader(actualData);

            for (int row = 0; row < height; row++)
            {
                // Each row starts at a 32-bit word boundary
                bitReader.AlignToWord();

                if (!bitReader.HasMoreData())
                {
                    if (verbose) Console.WriteLine($"  Ran out of data at row {row}/{height}");
                    break;
                }

                // Save position at start of row (in words, not bits)
                int rowStartWord = bitReader.CurrentWordPosition;

                if (verbose) Console.WriteLine($"  Row {row}: Start word position = {rowStartWord}");

                // Read offset to next row
                // For 8/16 bpp: 10-bit offset in bits 25-16
                // For 1/2/4/6 bpp: 8-bit offset in bits 31-24
                int nextRowOffset;
                if (bitsPerPixel == 8 || bitsPerPixel == 16)
                {
                    bitReader.ReadBits(6); // Skip bits 31-26
                    nextRowOffset = bitReader.ReadBits(10); // Read bits 25-16
                }
                else
                {
                    nextRowOffset = bitReader.ReadBits(8); // Read bits 31-24
                }

                if (verbose) Console.WriteLine($"    Next row offset: {nextRowOffset}");

                // Calculate where next row should start (in words)
                int nextRowWord = rowStartWord + nextRowOffset + 2;

                if (verbose) Console.WriteLine($"    Next row word position: {nextRowWord}, data has {actualData.Length / 4} words total");

                // Process packets for this row
                int pixelsInRow = 0;
                bool endOfLine = false;
                int outputOffset = row * width * bytesPerPixel;
                int maskOffset = row * width;

                while (pixelsInRow < width && !endOfLine && bitReader.CurrentWordPosition < nextRowWord)
                {
                    // Read 2-bit packet type
                    int packetType = bitReader.ReadBits(2);

                    switch (packetType)
                    {
                        case PACK_EOL:
                            endOfLine = true;
                            break;

                        case PACK_LITERAL:
                            {
                                int count = bitReader.ReadBits(6) + 1;
                                for (int i = 0; i < count && pixelsInRow < width; i++)
                                {
                                    int pixelValue = bitReader.ReadBits(bitsPerPixel);

                                    if (bitsPerPixel == 8)
                                    {
                                        int plutIndex = pixelValue & 0x1F;
                                        int amv = (pixelValue >> 5) & 0x07;
                                        unpackedData[outputOffset++] = (byte)plutIndex;
                                        if (amvData != null) amvData[maskOffset] = (byte)amv;
                                    }
                                    else
                                    {
                                        WritePixel(unpackedData, outputOffset, pixelValue, bitsPerPixel);
                                        outputOffset += bytesPerPixel;
                                    }

                                    transparencyMask[maskOffset++] = false;
                                    pixelsInRow++;
                                }
                            }
                            break;

                        case PACK_TRANSPARENT:
                            {
                                int count = bitReader.ReadBits(6) + 1;
                                for (int i = 0; i < count && pixelsInRow < width; i++)
                                {
                                    unpackedData[outputOffset++] = 0;
                                    transparencyMask[maskOffset++] = true;
                                    if (amvData != null) amvData[maskOffset - 1] = 0;
                                    pixelsInRow++;
                                }
                            }
                            break;

                        case PACK_REPEAT:
                            {
                                int count = bitReader.ReadBits(6) + 1;
                                int pixelValue = bitReader.ReadBits(bitsPerPixel);

                                int plutIndex, amv;
                                if (bitsPerPixel == 8)
                                {
                                    plutIndex = pixelValue & 0x1F;
                                    amv = (pixelValue >> 5) & 0x07;
                                }
                                else
                                {
                                    plutIndex = pixelValue;
                                    amv = 0;
                                }

                                for (int i = 0; i < count && pixelsInRow < width; i++)
                                {
                                    if (bitsPerPixel == 8)
                                    {
                                        unpackedData[outputOffset++] = (byte)plutIndex;
                                        if (amvData != null) amvData[maskOffset] = (byte)amv;
                                    }
                                    else
                                    {
                                        WritePixel(unpackedData, outputOffset, plutIndex, bitsPerPixel);
                                        outputOffset += bytesPerPixel;
                                    }

                                    transparencyMask[maskOffset++] = false;
                                    pixelsInRow++;
                                }
                            }
                            break;
                    }
                }

                // Jump to next row
                bitReader.SeekToWord(nextRowWord);

                if (verbose && row < 5)
                {
                    Console.WriteLine($"  Row {row}: {pixelsInRow} pixels decoded");
                }
            }

            return new CelImageData
            {
                Width = width,
                Height = height,
                BitsPerPixel = bitsPerPixel,
                PixelData = unpackedData,
                TransparencyMask = transparencyMask,
                AlternateMultiplyValues = amvData
            };
        }

        /// <summary>
        /// Unpacks 3DO CEL data in coded_unpacked format with known dimensions.
        /// Uses packet encoding without row offset headers.
        /// </summary>
        /// <param name="celData">The CEL data</param>
        /// <param name="width">Width in pixels (TLHPCNT from preamble)</param>
        /// <param name="height">Height in pixels (VCNT from preamble)</param>
        /// <param name="bitsPerPixel">Bits per pixel</param>
        /// <returns>Unpacked pixel data with dimensions</returns>
        public static CelImageData UnpackCodedUnpackedCelData(byte[] celData, int width, int height, int bitsPerPixel = 8)
        {
            return UnpackUnpackedCelData(celData, width, height, bitsPerPixel, coded: true);
        }

        /// <summary>
        /// Unpacks 3DO CEL data in uncoded_unpacked format with known dimensions.
        /// Raw pixel data without packets or row offset headers.
        /// </summary>
        /// <param name="celData">The CEL data</param>
        /// <param name="width">Width in pixels (TLHPCNT from preamble)</param>
        /// <param name="height">Height in pixels (VCNT from preamble)</param>
        /// <param name="bitsPerPixel">Bits per pixel (8 or 16 for uncoded)</param>
        /// <returns>Unpacked pixel data with dimensions</returns>
        public static CelImageData UnpackUncodedUnpackedCelData(byte[] celData, int width, int height, int bitsPerPixel = 8)
        {
            return UnpackUnpackedCelData(celData, width, height, bitsPerPixel, coded: false);
        }

        /// <summary>
        /// Unpacks 3DO CEL data in uncoded_packed format with known dimensions.
        /// Uncoded formats contain direct RGB pixel values (not palette indices).
        /// 8bpp uncoded: RGB332 format (3R, 3G, 2B)
        /// 16bpp uncoded: RGB555 format (5R, 5G, 5B, 1 control bit)
        /// Uses packet encoding (LITERAL, TRANSPARENT, REPEAT, EOL) but with RGB values.
        /// </summary>
        /// <param name="packedData">The packed CEL data</param>
        /// <param name="width">Width in pixels from CCB header</param>
        /// <param name="height">Height in pixels from CCB header</param>
        /// <param name="bitsPerPixel">Bits per pixel (8 or 16 for uncoded)</param>
        /// <param name="verbose">Enable detailed logging</param>
        /// <returns>Unpacked RGB pixel data with dimensions</returns>
        public static CelImageData UnpackUncodedPackedWithDimensions(byte[] packedData, int width, int height, int bitsPerPixel = 8, bool verbose = false)
        {
            if (verbose)
            {
                Console.WriteLine($"\n[VERBOSE] UnpackUncodedPackedWithDimensions called:");
                Console.WriteLine($"  Width: {width}");
                Console.WriteLine($"  Height: {height}");
                Console.WriteLine($"  BitsPerPixel: {bitsPerPixel}");
                Console.WriteLine($"  Data size: {packedData.Length} bytes");
            }

            // Uncoded formats use RGB data directly
            // We'll unpack to RGBA32 format (4 bytes per pixel)
            byte[] unpackedData = new byte[width * height * 4]; // RGBA32
            bool[] transparencyMask = new bool[width * height];

            // Initialize all pixels as transparent by default
            for (int i = 0; i < transparencyMask.Length; i++)
            {
                transparencyMask[i] = true;
            }

            BitReader bitReader = new BitReader(packedData);

            for (int row = 0; row < height; row++)
            {
                // Align to word boundary for each row
                bitReader.AlignToWord();

                if (!bitReader.HasMoreData())
                {
                    if (verbose)
                        Console.WriteLine($"  Row {row}: Ran out of data");
                    break;
                }

                // Read row offset header
                int rowStartWord = bitReader.CurrentWordPosition;
                int nextRowOffset;

                if (bitsPerPixel == 8 || bitsPerPixel == 16)
                {
                    bitReader.ReadBits(6); // Skip upper 6 bits
                    nextRowOffset = bitReader.ReadBits(10); // 10-bit offset
                }
                else
                {
                    nextRowOffset = bitReader.ReadBits(8); // 8-bit offset
                }

                int nextRowWord = rowStartWord + nextRowOffset + 2;

                if (verbose && row < 5)
                {
                    Console.WriteLine($"  Row {row}: Offset={nextRowOffset}, NextWord={nextRowWord}");
                }

                int pixelsInRow = 0;
                bool endOfLine = false;

                // Process packets
                while (pixelsInRow < width && !endOfLine && bitReader.CurrentWordPosition < nextRowWord)
                {
                    int packetType = bitReader.ReadBits(2);

                    switch (packetType)
                    {
                        case PACK_EOL:
                            endOfLine = true;
                            break;

                        case PACK_LITERAL:
                            {
                                int count = bitReader.ReadBits(6) + 1;
                                for (int i = 0; i < count && pixelsInRow < width; i++)
                                {
                                    int rgbValue = bitReader.ReadBits(bitsPerPixel);
                                    int pixelIndex = row * width + pixelsInRow;
                                    WriteRGBPixel(unpackedData, pixelIndex * 4, rgbValue, bitsPerPixel);
                                    transparencyMask[pixelIndex] = false;
                                    pixelsInRow++;
                                }
                            }
                            break;

                        case PACK_TRANSPARENT:
                            {
                                int count = bitReader.ReadBits(6) + 1;
                                pixelsInRow += count; // Leave as transparent (already initialized)
                            }
                            break;

                        case PACK_REPEAT:
                            {
                                int count = bitReader.ReadBits(6) + 1;
                                int rgbValue = bitReader.ReadBits(bitsPerPixel);
                                for (int i = 0; i < count && pixelsInRow < width; i++)
                                {
                                    int pixelIndex = row * width + pixelsInRow;
                                    WriteRGBPixel(unpackedData, pixelIndex * 4, rgbValue, bitsPerPixel);
                                    transparencyMask[pixelIndex] = false;
                                    pixelsInRow++;
                                }
                            }
                            break;
                    }
                }

                // Advance to next row
                bitReader.SeekToWord(nextRowWord);

                if (verbose && row < 5)
                {
                    Console.WriteLine($"    Pixels in row: {pixelsInRow}");
                }
            }

            return new CelImageData
            {
                Width = width,
                Height = height,
                BitsPerPixel = 32, // RGBA32 output
                PixelData = unpackedData,
                TransparencyMask = transparencyMask
            };
        }

        /// <summary>
        /// Unpacks 3DO CEL data in uncoded_unpacked format with known dimensions.
        /// Uncoded unpacked contains raw RGB pixel values word-aligned per row.
        /// 8bpp uncoded: RGB332 format (3R, 3G, 2B)
        /// 16bpp uncoded: RGB555 format (5R, 5G, 5B, 1 control bit)
        /// No packet encoding - just sequential RGB values.
        /// </summary>
        /// <param name="celData">The CEL data</param>
        /// <param name="width">Width in pixels</param>
        /// <param name="height">Height in pixels</param>
        /// <param name="bitsPerPixel">Bits per pixel (8 or 16 for uncoded)</param>
        /// <param name="verbose">Enable detailed logging</param>
        /// <returns>Unpacked RGB pixel data with dimensions</returns>
        public static CelImageData UnpackUncodedUnpackedWithDimensions(byte[] celData, int width, int height, int bitsPerPixel = 8, bool verbose = false)
        {
            if (verbose)
            {
                Console.WriteLine($"\n[VERBOSE] UnpackUncodedUnpackedWithDimensions called:");
                Console.WriteLine($"  Width: {width}, Height: {height}");
                Console.WriteLine($"  BitsPerPixel: {bitsPerPixel}");
                Console.WriteLine($"  Data size: {celData.Length} bytes");
            }

            // Uncoded formats use RGB data directly
            // We'll unpack to RGBA32 format (4 bytes per pixel)
            byte[] unpackedData = new byte[width * height * 4]; // RGBA32
            
            BitReader bitReader = new BitReader(celData);

            for (int row = 0; row < height; row++)
            {
                // Each row starts at 32-bit word boundary
                bitReader.AlignToWord();

                for (int col = 0; col < width; col++)
                {
                    int rgbValue = bitReader.ReadBits(bitsPerPixel);
                    int pixelIndex = row * width + col;
                    WriteRGBPixel(unpackedData, pixelIndex * 4, rgbValue, bitsPerPixel);
                }

                if (verbose && row < 5)
                {
                    Console.WriteLine($"  Row {row}: Processed {width} pixels");
                }
            }

            return new CelImageData
            {
                Width = width,
                Height = height,
                BitsPerPixel = 32, // RGBA32 output
                PixelData = unpackedData
            };
        }

        /// <summary>
        /// Legacy wrapper for uncoded_packed - auto-detection (deprecated)
        /// </summary>
        public static CelImageData UnpackUncodedPackedCelData(byte[] packedData, int bitsPerPixel = 8)
        {
            return UnpackCelData(packedData, bitsPerPixel, coded: false, packed: true);
        }

        /// <summary>
        /// Unpacks unpacked (non-packed) 3DO CEL data with known dimensions.
        /// For unpacked formats, pixels are stored sequentially without run-length encoding.
        /// </summary>
        /// <param name="celData">The CEL data</param>
        /// <param name="width">Width in pixels</param>
        /// <param name="height">Height in pixels</param>
        /// <param name="bitsPerPixel">Bits per pixel</param>
        /// <param name="coded">True for coded (uses packets), false for uncoded (raw pixels)</param>
        /// <returns>Unpacked pixel data with dimensions</returns>
        private static CelImageData UnpackUnpackedCelData(byte[] celData, int width, int height, int bitsPerPixel, bool coded, bool verbose = false)
        {
            if (verbose)
            {
                Console.WriteLine($"\n[VERBOSE] UnpackUnpackedCelData:");
                Console.WriteLine($"  Width: {width}, Height: {height}");
                Console.WriteLine($"  BitsPerPixel: {bitsPerPixel}, Coded: {coded}");
                Console.WriteLine($"  Data size: {celData.Length} bytes");
                Console.WriteLine($"  First 16 bytes: {BitConverter.ToString(celData.Take(16).ToArray())}");
            }

            int bytesPerPixel = (bitsPerPixel + 7) / 8;
            byte[] unpackedData = new byte[width * height * bytesPerPixel];
            int outputOffset = 0;

            BitReader bitReader = new BitReader(celData);

            // IMPORTANT: According to 3DO documentation, UNPACKED data (both coded and uncoded)
            // is sent directly to the pixel decoder WITHOUT packet encoding.
            // "Unpacked data is sent directly to the pixel decoder without running through 
            // the data unpacker; each pixel is represented by an equal number of bits."
            // This means NO PACK_LITERAL, PACK_REPEAT, etc. - just raw pixel values!

            for (int row = 0; row < height; row++)
            {
                // Each row starts at a 32-bit word boundary
                bitReader.AlignToWord();

                if (verbose && row < 5)
                {
                    int bitPos = bitReader.CurrentWordPosition * 32;
                    Console.WriteLine($"  Row {row}: Word position = {bitReader.CurrentWordPosition}, Bit position ~{bitPos}");
                    Console.Write($"    First 8 pixels:");
                }

                // For BOTH coded and uncoded unpacked data: just read raw pixels
                // The only difference is that coded pixels go through PLUT lookup later
                for (int col = 0; col < width; col++)
                {
                    int pixelValue = bitReader.ReadBits(bitsPerPixel);

                    if (verbose && row < 5 && col < 8)
                    {
                        Console.Write($" {pixelValue:X2}");
                    }

                    WritePixel(unpackedData, outputOffset, pixelValue, bitsPerPixel);
                    outputOffset += bytesPerPixel;
                }

                if (verbose && row < 5)
                {
                    Console.WriteLine();
                }
            }

            return new CelImageData
            {
                Width = width,
                Height = height,
                BitsPerPixel = bitsPerPixel,
                PixelData = unpackedData
            };
        }

        /// <summary>
        /// Unpacks 3DO CEL data - unified method that handles all formats.
        /// </summary>
        /// <param name="celData">The CEL data</param>
        /// <param name="bitsPerPixel">Bits per pixel (1, 2, 4, 6, 8, or 16)</param>
        /// <param name="coded">True if data uses packet encoding (LITERAL, TRANSPARENT, REPEAT, EOL)</param>
        /// <param name="packed">True if data has row offset headers</param>
        /// <returns>Unpacked pixel data with dimensions</returns>
        private static CelImageData UnpackCelData(byte[] celData, int bitsPerPixel = 8, bool coded = true, bool packed = true, bool verbose = false, bool skipCompSize = false)
        {
            // For unpacked formats, we need width/height from preamble or cannot auto-detect
            // Packed formats can auto-detect dimensions by reading through the data

            if (!packed)
            {
                // Unpacked formats require known dimensions - cannot auto-detect width
                throw new NotSupportedException(
                    "Unpacked CEL formats (coded_unpacked, uncoded_unpacked) require width and height to be known in advance. " +
                    "These formats store raw pixel data without row structure markers. " +
                    "Use UnpackCelFile() which reads dimensions from CCB header, or provide dimensions explicitly.");
            }

            if (verbose)
            {
                Console.WriteLine($"\n[VERBOSE] UnpackCelData called:");
                Console.WriteLine($"  BitsPerPixel: {bitsPerPixel}");
                Console.WriteLine($"  Coded: {coded}");
                Console.WriteLine($"  Packed: {packed}");
                Console.WriteLine($"  Data size: {celData.Length} bytes");
            }

            // PACKED FORMAT: Process data with row offset headers
            List<List<byte>> rows = new List<List<byte>>();
            List<List<bool>> transparencyRows = new List<List<bool>>(); // Track transparency per pixel
            List<List<byte>> amvRows = new List<List<byte>>(); // Track AMV values for 8-bit coded
            int maxWidth = 0;

            BitReader bitReader = new BitReader(celData);
            int bytesPerPixel = (bitsPerPixel + 7) / 8;

            // Process all rows
            while (bitReader.HasMoreData())
            {
                // Each row starts at a 32-bit word boundary
                bitReader.AlignToWord();

                // Check if we're at the end of data or all zeros
                if (!bitReader.HasMoreData() || bitReader.PeekWord() == 0)
                    break;

                // Save position at start of row (in words, not bits)
                int rowStartWord = bitReader.CurrentWordPosition;

                // Read offset to next row (this is always present in packed formats)
                // For 1/2/4/6 bpp: 8-bit offset in bits 31-24
                // For 8/16 bpp: 10-bit offset in bits 25-16
                int nextRowOffset;
                if (bitsPerPixel == 8 || bitsPerPixel == 16)
                {
                    // Skip bits 31-26 (6 bits), then read bits 25-16 (10 bits)
                    bitReader.ReadBits(6); // Skip the upper 6 bits
                    nextRowOffset = bitReader.ReadBits(10);
                }
                else
                {
                    // Read bits 31-24 (8 bits)
                    nextRowOffset = bitReader.ReadBits(8);
                }

                // Calculate where next row should start (in words)
                // The offset is the distance from current word to next word, minus 2
                int nextRowWord = rowStartWord + nextRowOffset + 2;

                // Process row data based on coded/uncoded format
                List<byte> rowPixels = new List<byte>();
                List<bool> rowTransparency = new List<bool>(); // Track which pixels are transparent
                List<byte> rowAMV = new List<byte>(); // Track AMV values for 8-bit coded pixels

                // Packet tracking for verbose mode
                int packetsInRow = 0;
                Dictionary<int, int> packetTypeCounts = new Dictionary<int, int>
                {
                    { PACK_EOL, 0 },
                    { PACK_LITERAL, 0 },
                    { PACK_TRANSPARENT, 0 },
                    { PACK_REPEAT, 0 }
                };

                if (coded)
                {
                    // CODED PACKED: Process packets (LITERAL, TRANSPARENT, REPEAT, EOL)
                    bool endOfLine = false;
                    int safetyCounter = 0;
                    int maxPacketsPerRow = 1000; // Safety limit

                    while (!endOfLine && bitReader.CurrentWordPosition < nextRowWord && safetyCounter++ < maxPacketsPerRow)
                    {
                        // Read 2-bit packet type
                        int packetType = bitReader.ReadBits(2);
                        packetTypeCounts[packetType]++;
                        packetsInRow++;

                        switch (packetType)
                        {
                            case PACK_EOL:
                                // End of line marker
                                endOfLine = true;
                                break;

                            case PACK_LITERAL:
                                {
                                    int count = bitReader.ReadBits(6) + 1;
                                    for (int i = 0; i < count; i++)
                                    {
                                        int pixelValue = bitReader.ReadBits(bitsPerPixel);

                                        // For 8-bit coded: upper 3 bits are AMV, lower 5 bits are PLUT index
                                        if (bitsPerPixel == 8)
                                        {
                                            int plutIndex = pixelValue & 0x1F; // Lower 5 bits (0-31)
                                            int amv = (pixelValue >> 5) & 0x07; // Upper 3 bits (0-7)
                                            AddPixelToRow(rowPixels, plutIndex, bitsPerPixel);
                                            rowAMV.Add((byte)amv);
                                        }
                                        else
                                        {
                                            AddPixelToRow(rowPixels, pixelValue, bitsPerPixel);
                                        }

                                        AddTransparencyToRow(rowTransparency, false, bitsPerPixel); // Opaque
                                    }
                                }
                                break;

                            case PACK_TRANSPARENT:
                                {
                                    int count = bitReader.ReadBits(6) + 1;
                                    for (int i = 0; i < count; i++)
                                    {
                                        AddPixelToRow(rowPixels, 0, bitsPerPixel); // Write 0 as placeholder
                                        if (bitsPerPixel == 8) rowAMV.Add(0); // AMV=0 for transparent
                                        AddTransparencyToRow(rowTransparency, true, bitsPerPixel); // Mark as transparent
                                    }
                                }
                                break;

                            case PACK_REPEAT:
                                {
                                    int count = bitReader.ReadBits(6) + 1;
                                    int pixelValue = bitReader.ReadBits(bitsPerPixel);

                                    // For 8-bit coded: split into PLUT index and AMV
                                    int plutIndex, amv;
                                    if (bitsPerPixel == 8)
                                    {
                                        plutIndex = pixelValue & 0x1F;
                                        amv = (pixelValue >> 5) & 0x07;
                                    }
                                    else
                                    {
                                        plutIndex = pixelValue;
                                        amv = 0;
                                    }

                                    for (int i = 0; i < count; i++)
                                    {
                                        AddPixelToRow(rowPixels, plutIndex, bitsPerPixel);
                                        if (bitsPerPixel == 8) rowAMV.Add((byte)amv);
                                        AddTransparencyToRow(rowTransparency, false, bitsPerPixel); // Opaque
                                    }
                                }
                                break;
                        }
                    }
                }
                else
                {
                    // UNCODED PACKED: Still uses packet encoding (LITERAL, TRANSPARENT, REPEAT, EOL)
                    // but pixel values are direct color values (not PLUT indices)
                    // The packet structure is the same as coded_packed
                    bool endOfLine = false;
                    int safetyCounter = 0;
                    int maxPacketsPerRow = 1000; // Safety limit

                    while (!endOfLine && bitReader.CurrentWordPosition < nextRowWord && safetyCounter++ < maxPacketsPerRow)
                    {
                        // Read 2-bit packet type
                        int packetType = bitReader.ReadBits(2);

                        switch (packetType)
                        {
                            case PACK_EOL:
                                // End of line marker
                                endOfLine = true;
                                break;

                            case PACK_LITERAL:
                                {
                                    int count = bitReader.ReadBits(6) + 1;
                                    for (int i = 0; i < count; i++)
                                    {
                                        int pixelValue = bitReader.ReadBits(bitsPerPixel);
                                        AddPixelToRow(rowPixels, pixelValue, bitsPerPixel);
                                        AddTransparencyToRow(rowTransparency, false, bitsPerPixel); // Opaque
                                    }
                                }
                                break;

                            case PACK_TRANSPARENT:
                                {
                                    int count = bitReader.ReadBits(6) + 1;
                                    for (int i = 0; i < count; i++)
                                    {
                                        AddPixelToRow(rowPixels, 0, bitsPerPixel); // Write 0 as placeholder
                                        AddTransparencyToRow(rowTransparency, true, bitsPerPixel); // Mark as transparent
                                    }
                                }
                                break;

                            case PACK_REPEAT:
                                {
                                    int count = bitReader.ReadBits(6) + 1;
                                    int pixelValue = bitReader.ReadBits(bitsPerPixel);
                                    for (int i = 0; i < count; i++)
                                    {
                                        AddPixelToRow(rowPixels, pixelValue, bitsPerPixel);
                                        AddTransparencyToRow(rowTransparency, false, bitsPerPixel); // Opaque
                                    }
                                }
                                break;
                        }
                    }
                }

                // Jump to next row using the offset
                bitReader.SeekToWord(nextRowWord);

                if (rowPixels.Count > 0)
                {
                    rows.Add(rowPixels);
                    transparencyRows.Add(rowTransparency);
                    if (coded && bitsPerPixel == 8)
                    {
                        amvRows.Add(rowAMV);
                    }
                    maxWidth = Math.Max(maxWidth, rowPixels.Count / bytesPerPixel);
                }
            }

            int height = rows.Count;
            int width = maxWidth;

            // Second pass: create final image buffer and transparency mask
            byte[] unpackedData = new byte[width * height * bytesPerPixel];
            bool[] transparencyMask = new bool[width * height]; // One bool per pixel
            byte[]? amvData = (coded && bitsPerPixel == 8) ? new byte[width * height] : null; // AMV data for 8-bit coded

            // Initialize ALL pixels as transparent by default
            for (int i = 0; i < transparencyMask.Length; i++)
            {
                transparencyMask[i] = true; // Default to transparent
            }

            for (int row = 0; row < height; row++)
            {
                List<byte> rowData = rows[row];
                List<bool> rowTransp = transparencyRows[row];
                int destOffset = row * width * bytesPerPixel;
                int maskOffset = row * width;

                // Copy pixel data
                for (int i = 0; i < rowData.Count; i++)
                {
                    unpackedData[destOffset + i] = rowData[i];
                }

                // Copy transparency data - this will overwrite the default 'true' for actual pixels
                for (int i = 0; i < rowTransp.Count && i < width; i++)
                {
                    transparencyMask[maskOffset + i] = rowTransp[i];
                }

                // Copy AMV data for 8-bit coded
                if (amvData != null && row < amvRows.Count)
                {
                    List<byte> rowAmv = amvRows[row];
                    for (int i = 0; i < rowAmv.Count && i < width; i++)
                    {
                        amvData[maskOffset + i] = rowAmv[i];
                    }
                }
                // Note: Pixels beyond rowTransp.Count remain as 'true' (transparent), which is correct
                // for variable-width rows where the right side should be transparent
            }

            return new CelImageData
            {
                Width = width,
                Height = height - 1,
                BitsPerPixel = bitsPerPixel,
                PixelData = unpackedData,
                TransparencyMask = transparencyMask,
                AlternateMultiplyValues = amvData
            };
        }

        /// <summary>
        /// Unpacks 3DO CEL data with known dimensions
        /// </summary>
        public static byte[] UnpackCelData(byte[] packedData, int width, int height, int bitsPerPixel)
        {
            // Calculate output buffer size
            int bytesPerPixel = (bitsPerPixel + 7) / 8;
            byte[] unpackedData = new byte[width * height * bytesPerPixel];
            int outputOffset = 0;

            BitReader bitReader = new BitReader(packedData);

            for (int row = 0; row < height; row++)
            {
                // Each row starts at a 32-bit word boundary
                bitReader.AlignToWord();

                // Read offset to next row
                // 8-bit offset for 1, 2, 4, 6 bpp
                // 10-bit offset for 8, 16 bpp
                int offsetBits = (bitsPerPixel == 8 || bitsPerPixel == 16) ? 10 : 8;
                int nextRowOffset = bitReader.ReadBits(offsetBits);

                // For 10-bit offset, skip padding bits (bits 31-26 are 0)
                // For 8-bit offset (bits 31-24), data starts immediately after

                // Process packets for this row
                int pixelsInRow = 0;
                bool endOfLine = false;

                while (pixelsInRow < width && !endOfLine)
                {
                    // Read 2-bit packet type
                    int packetType = bitReader.ReadBits(2);

                    switch (packetType)
                    {
                        case PACK_EOL:
                            // End of line marker
                            endOfLine = true;
                            break;

                        case PACK_LITERAL:
                            {
                                // Literal pixels: count field + individual pixel values
                                int count = bitReader.ReadBits(6) + 1; // Count is stored as count-1
                                for (int i = 0; i < count && pixelsInRow < width; i++)
                                {
                                    int pixelValue = bitReader.ReadBits(bitsPerPixel);
                                    WritePixel(unpackedData, outputOffset, pixelValue, bitsPerPixel);
                                    outputOffset += bytesPerPixel;
                                    pixelsInRow++;
                                }
                            }
                            break;

                        case PACK_TRANSPARENT:
                            {
                                // Transparent pixels: just a count, no pixel data
                                int count = bitReader.ReadBits(6) + 1;
                                for (int i = 0; i < count && pixelsInRow < width; i++)
                                {
                                    // Write transparent pixel (typically 0)
                                    WritePixel(unpackedData, outputOffset, 0, bitsPerPixel);
                                    outputOffset += bytesPerPixel;
                                    pixelsInRow++;
                                }
                            }
                            break;

                        case PACK_REPEAT:
                            {
                                // Repeating pixel: count field + single pixel value
                                int count = bitReader.ReadBits(6) + 1;
                                int pixelValue = bitReader.ReadBits(bitsPerPixel);
                                for (int i = 0; i < count && pixelsInRow < width; i++)
                                {
                                    WritePixel(unpackedData, outputOffset, pixelValue, bitsPerPixel);
                                    outputOffset += bytesPerPixel;
                                    pixelsInRow++;
                                }
                            }
                            break;
                    }
                }
            }

            return unpackedData;
        }

        /// <summary>
        /// Helper method to add a pixel to a row buffer
        /// </summary>
        private static void AddPixelToRow(List<byte> rowPixels, int pixelValue, int bitsPerPixel)
        {
            switch (bitsPerPixel)
            {
                case 1:
                case 2:
                case 4:
                case 6:
                    rowPixels.Add((byte)pixelValue);
                    break;

                case 8:
                    rowPixels.Add((byte)pixelValue);
                    break;

                case 16:
                    rowPixels.Add((byte)(pixelValue & 0xFF));
                    rowPixels.Add((byte)((pixelValue >> 8) & 0xFF));
                    break;
            }
        }

        private static void AddTransparencyToRow(List<bool> rowTransparency, bool isTransparent, int bitsPerPixel)
        {
            // Add transparency mask value(s) for this pixel
            // For 8bpp and lower: 1 bool per pixel
            // For 16bpp: 1 bool per pixel (not per byte)
            rowTransparency.Add(isTransparent);
        }

        /// <summary>
        /// Extracts palette data from a PLUT chunk
        /// </summary>
        /// <param name="plutData">Raw PLUT chunk data (without magic and size header)</param>
        /// <param name="verbose">Enable verbose logging</param>
        /// <returns>Palette as list of ImageSharp Colors</returns>
        private static List<Color> ExtractPaletteFromPLUT(byte[] plutData, bool verbose = false)
        {
            if (verbose) Console.WriteLine($"Extracting palette from PLUT data ({plutData.Length} bytes)");

            // Convert to ImageSharp Color list using ReadRgb15PaletteIS
            var palette = ColorHelper.ReadRgb15PaletteIS(plutData);

            if (verbose) Console.WriteLine($"Extracted {palette.Count} colors from PLUT chunk");
            return palette;
        }

        /// <summary>
        /// Reads a CEL file, parses the CCB header, and automatically unpacks the pixel data
        /// using the appropriate method based on the header information.
        /// </summary>
        /// <param name="celFile">Full path to the CEL file</param>
        /// <param name="verbose">If true, displays CCB header information</param>
        /// <param name="bitsPerPixel">Optional: Override auto-detected bits per pixel (1, 2, 4, 6, 8, or 16). If 0, auto-detect.</param>
        /// <returns>Unpacked CEL image data with dimensions and pixel data</returns>
        public static CelImageData? UnpackCelFile(string celFile, bool verbose = false, int bitsPerPixel = 0, bool skipUncompSize = false)
        {
            if (!File.Exists(celFile))
            {
                Console.WriteLine($"File not found: {celFile}");
                return null;
            }

            byte[] data = File.ReadAllBytes(celFile);

            if (data.Length < 0x20)
            {
                Console.WriteLine($"File too small to contain a CCB header (minimum 32 bytes, got {data.Length})");
                return null;
            }

            if (verbose)
            {
                Console.WriteLine($"File size: {data.Length} bytes");
                // Show first 64 bytes as hex dump
                Console.WriteLine("First 64 bytes:");
                for (int i = 0; i < Math.Min(64, data.Length); i += 16)
                {
                    Console.Write($"{i:X4}: ");
                    for (int j = 0; j < 16 && i + j < data.Length && i + j < 64; j++)
                    {
                        Console.Write($"{data[i + j]:X2} ");
                    }
                    Console.Write(" ");
                    for (int j = 0; j < 16 && i + j < data.Length && i + j < 64; j++)
                    {
                        char c = (char)data[i + j];
                        Console.Write(char.IsControl(c) ? '.' : c);
                    }
                    Console.WriteLine();
                }
            }

            // Use chunk-based parsing with BinaryReader
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            List<Color> palette = new List<Color>();
            byte[]? pixelData = null;
            int ccbWidth = 0, ccbHeight = 0;
            uint ccbFlags = 0;
            uint pre0 = 0, pre1 = 0;

            // Parse all chunks until end of file
            while (stream.Position < data.Length - 8) // Need at least 8 bytes for magic + size
            {
                // Read magic bytes (4 bytes)
                long chunkStart = stream.Position;
                byte[] magic = reader.ReadBytes(4);

                if (magic.Length < 4) break; // End of file

                string magicStr = System.Text.Encoding.ASCII.GetString(magic);

                // Read chunk size (4 bytes, big-endian)
                uint chunkSize = ReadBigEndianUInt32(data, (int)stream.Position);
                stream.Position += 4;

                if (verbose)
                {
                    Console.WriteLine($"Found chunk '{magicStr}' at offset 0x{chunkStart:X}, size: {chunkSize} bytes");
                }

                if (magicStr == "CCB ")
                {
                    // Process CCB chunk - read width and height from fixed offsets
                    if (chunkSize < 0x50)
                    {
                        Console.WriteLine($"CCB chunk too small: {chunkSize} bytes (expected at least 80)");
                        return null;
                    }

                    long ccbDataStart = stream.Position;

                    // Read CCB header fields
                    stream.Position = ccbDataStart + 0x04; // Skip version field (4 bytes)
                    ccbFlags = ReadBigEndianUInt32(data, (int)stream.Position);

                    // Skip to PRE0 and PRE1 (offsets 0x38 and 0x3C in CCB data, absolute 0x40 and 0x44)
                    stream.Position = ccbDataStart + 0x38;
                    pre0 = ReadBigEndianUInt32(data, (int)stream.Position);
                    stream.Position = ccbDataStart + 0x3C;
                    pre1 = ReadBigEndianUInt32(data, (int)stream.Position);

                    // Read width and height from fixed file offsets 0x48 and 0x4C as user specified
                    // These are relative to file start, not CCB data start
                    ccbWidth = ReadBigEndianInt32(data, 0x48);
                    ccbHeight = ReadBigEndianInt32(data, 0x4C);

                    if (verbose)
                    {
                        Console.WriteLine($"CCB: Width={ccbWidth}, Height={ccbHeight}, Flags=0x{ccbFlags:X8}");
                        Console.WriteLine($"CCB: PRE0=0x{pre0:X8}, PRE1=0x{pre1:X8}");
                    }

                    // Skip past the entire CCB chunk
                    stream.Position = chunkStart + chunkSize;
                }
                else if (magicStr == "PLUT")
                {
                    // Process PLUT chunk - extract palette data
                    uint plutDataSize = chunkSize - 12; // Subtract magic (4) + size (4) + entries count (4)

                    // Skip entries count (4 bytes)
                    stream.Position += 4;

                    // Read palette data
                    byte[] plutData = reader.ReadBytes((int)plutDataSize);
                    palette = ExtractPaletteFromPLUT(plutData, verbose);

                    // Skip to next chunk (should already be at correct position)
                }
                else if (magicStr == "PDAT")
                {
                    // Process PDAT chunk - extract pixel data
                    uint pdatDataSize = chunkSize - 8; // Subtract magic (4) + size (4)
                    pixelData = reader.ReadBytes((int)pdatDataSize);

                    if (verbose)
                    {
                        Console.WriteLine($"PDAT: Extracted {pixelData.Length} bytes of pixel data");

                        // Show first 32 bytes of PDAT for debugging
                        int bytesToShow = Math.Min(32, pixelData.Length);
                        Console.Write($"First {bytesToShow} bytes: ");
                        for (int i = 0; i < bytesToShow; i++)
                        {
                            Console.Write($"{pixelData[i]:X2} ");
                        }
                        Console.WriteLine();
                    }

                    // Skip to next chunk (should already be at correct position)
                }
                else
                {
                    // Unknown chunk - skip using its size
                    uint skipSize = chunkSize - 8; // Subtract magic (4) + size (4) already read
                    stream.Position += skipSize;

                    if (verbose)
                    {
                        Console.WriteLine($"Skipping unknown chunk '{magicStr}' ({skipSize} bytes)");
                    }
                }
            }

            // Check if we have a valid CCB chunk
            if (ccbWidth <= 0 || ccbHeight <= 0)
            {
                Console.WriteLine($"No valid CCB chunk found or invalid dimensions (width={ccbWidth}, height={ccbHeight})");
                return null;
            }

            // If we only have palette data (no pixel data), return palette-only result
            if (pixelData == null && palette.Count > 0)
            {
                if (verbose)
                {
                    Console.WriteLine($"File contains palette-only data with {palette.Count} colors");
                }

                return new CelImageData
                {
                    Width = 0,
                    Height = 0,
                    BitsPerPixel = 0,
                    PixelData = Array.Empty<byte>(),
                    Palette = palette
                };
            }

            // If we have pixel data, process it
            if (pixelData == null)
            {
                Console.WriteLine("No pixel data found in file");
                return null;
            }

            if (verbose)
            {
                Console.WriteLine($"CCB FLAGS: 0x{ccbFlags:X8}");
                Console.WriteLine($"PACKED: {((ccbFlags & 0x00000200) != 0)}");
                Console.WriteLine($"CCBPRE: {((ccbFlags & 0x00400000) != 0)}");
                Console.WriteLine($"PRE0: 0x{pre0:X8}, PRE1: 0x{pre1:X8}");
                Console.WriteLine($"Width: {ccbWidth}, Height: {ccbHeight}");
                Console.WriteLine($"Pixel data size: {pixelData.Length} bytes");
            }

            // Detect format from FLAGS and PRE0/PRE1
            bool isCoded = true;
            bool isPacked = false;
            int bpp = bitsPerPixel; // Use override if provided

            // Check if we have PRE0 from CCB header
            if ((ccbFlags & 0x00400000) != 0 && pre0 != 0) // CCBPRE flag set and PRE0 exists
            {
                bool linear = (pre0 & 0x10) != 0;    // Bit 4: 1 = linear (uncoded), 0 = coded
                bool packed = (pre0 & 0x80) != 0;    // Bit 7: 1 = packed, 0 = unpacked
                isCoded = !linear;
                isPacked = packed;

                if (verbose)
                {
                    Console.WriteLine($"Format detected from CCB PRE0:");
                    Console.WriteLine($"  Linear (bit 4): {linear} (Coded: {isCoded})");
                    Console.WriteLine($"  Packed (bit 7): {packed}");
                }
            }

            // Also check PACKED flag from CCB FLAGS word
            if ((ccbFlags & 0x00000200) != 0) // PACKED flag - bit 9
            {
                isPacked = true;
                if (verbose)
                {
                    Console.WriteLine($"PACKED flag set in CCB FLAGS");
                }
            }

            // Auto-detect BPP if not specified
            if (bpp == 0 && pre0 != 0)
            {
                // PRE0 bits 2-0 encode BPP as: 0=default, 1=1bpp, 2=2bpp, 3=4bpp, 4=6bpp, 5=8bpp, 6=16bpp
                int bppCode = (int)(pre0 & 0x07);
                int[] bppMap = { 0, 1, 2, 4, 6, 8, 16 };

                if (bppCode > 0 && bppCode < bppMap.Length)
                {
                    bpp = bppMap[bppCode];
                    Console.WriteLine($"[PRE0={pre0:X8}] BPP code {bppCode} -> {bpp} bpp");
                }
                else
                {
                    bpp = 6; // Default to 6bpp
                    Console.WriteLine($"[PRE0={pre0:X8}] Unknown BPP code {bppCode}, defaulting to {bpp} bpp");
                }
            }

            if (bpp == 0)
            {
                bpp = 6; // Safe default
            }

            // Heuristic: If file size is much smaller than expected unpacked size, it's likely packed
            // even if the CCB header flags say otherwise (some files have incorrect PRE0 bits)
            if (!isPacked && ccbWidth > 0 && ccbHeight > 0 && bpp > 0)
            {
                // For unpacked format, each row is word-aligned (32-bit boundary)
                // Calculate bits per row, then round up to nearest word (4 bytes)
                int bitsPerRow = ccbWidth * bpp;
                int bytesPerRow = ((bitsPerRow + 31) / 32) * 4; // Round up to word boundary
                int expectedUnpackedSize = ccbHeight * bytesPerRow;
                int actualDataSize = pixelData.Length;

                // If actual data is less than 75% of expected size, assume it's packed (compressed)
                if (actualDataSize < expectedUnpackedSize * 0.75)
                {
                    if (verbose)
                    {
                        Console.WriteLine($"  File size heuristic: Expected {expectedUnpackedSize} bytes for unpacked, got {actualDataSize} bytes");
                        Console.WriteLine($"  Overriding format to PACKED (CCB header may have incorrect flags)");
                    }
                    isPacked = true;
                }
            }

            if (verbose)
            {
                Console.WriteLine($"\nAuto-detected parameters:");
                Console.WriteLine($"  Format: {(isCoded ? "Coded" : "Uncoded")} {(isPacked ? "Packed" : "Unpacked")}");
                Console.WriteLine($"  Bits per pixel: {bpp}");
                Console.WriteLine($"  CCB Width: {ccbWidth}");
                Console.WriteLine($"  CCB Height: {ccbHeight}");
            }

            try
            {
                // Unpack the pixel data using the detected format
                CelImageData result;

                // Route to the appropriate unpacking method based on format
                if (isCoded && isPacked)
                {
                    // CODED PACKED format - use dedicated method with known dimensions
                    if (ccbWidth <= 0 || ccbHeight <= 0)
                    {
                        // Fall back to auto-detection if dimensions invalid
                        result = UnpackCodedPackedCelData(pixelData, bpp, verbose: verbose);
                    }
                    else
                    {
                        result = UnpackCodedPackedWithDimensions(pixelData, ccbWidth, ccbHeight, bpp, verbose: verbose, skipUncompSize: skipUncompSize);
                    }
                }
                else if (isCoded && !isPacked)
                {
                    // CODED UNPACKED format
                    if (ccbWidth <= 0 || ccbHeight <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Coded unpacked format requires valid dimensions (width={ccbWidth}, height={ccbHeight})");
                    }
                    result = UnpackCodedUnpackedCelData(pixelData, ccbWidth, ccbHeight, bpp);
                }
                else if (!isCoded && isPacked)
                {
                    // UNCODED PACKED format - contains direct RGB values with packet encoding
                    if (ccbWidth <= 0 || ccbHeight <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Uncoded packed format requires valid dimensions (width={ccbWidth}, height={ccbHeight})");
                    }
                    result = UnpackUncodedPackedWithDimensions(pixelData, ccbWidth, ccbHeight, bpp, verbose: verbose);
                }
                else
                {
                    // UNCODED UNPACKED format - contains raw RGB values word-aligned per row
                    if (ccbWidth <= 0 || ccbHeight <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Uncoded unpacked format requires valid dimensions (width={ccbWidth}, height={ccbHeight})");
                    }
                    result = UnpackUncodedUnpackedWithDimensions(pixelData, ccbWidth, ccbHeight, bpp, verbose: verbose);
                }

                // Add palette data to the result if we have it
                if (palette.Count > 0)
                {
                    result.Palette = palette;
                }

                if (verbose)
                {
                    Console.WriteLine($"\nUnpacked image:");
                    Console.WriteLine($"  Width: {result.Width}");
                    Console.WriteLine($"  Height: {result.Height}");
                    Console.WriteLine($"  BPP: {result.BitsPerPixel}");
                    Console.WriteLine($"  Pixel data size: {result.PixelData.Length} bytes");
                    if (result.Palette != null)
                    {
                        Console.WriteLine($"  Palette colors: {result.Palette.Count}");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error unpacking CEL data: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parses and displays CCB (CEL Control Block) header information
        /// </summary>
        /// <param name="celFile">Full path to the CEL file</param>
        public static void ParseAndDisplayCCBHeader(string celFile)
        {
            if (!File.Exists(celFile))
            {
                Console.WriteLine($"File not found: {celFile}");
                return;
            }

            byte[] data = File.ReadAllBytes(celFile);
            ParseAndDisplayCCBHeader(data);
        }

        /// <summary>
        /// Parses and displays CCB (CEL Control Block) header information
        /// </summary>
        /// <param name="data">CEL file data including CCB header</param>
        public static void ParseAndDisplayCCBHeader(byte[] data)
        {
            if (data.Length < 0x50)
            {
                Console.WriteLine("Data too small to contain a CCB header (minimum 0x50 bytes)");
                return;
            }

            // Check for CCB magic
            if (data[0] != 0x43 || data[1] != 0x43 || data[2] != 0x42 || data[3] != 0x20)
            {
                Console.WriteLine($"Invalid CCB magic. Expected 'CCB ', got: {(char)data[0]}{(char)data[1]}{(char)data[2]}{(char)data[3]}");
                return;
            }

            Console.WriteLine("=== CEL Control Block (CCB) Header ===");
            Console.WriteLine($"Magic: CCB (0x{data[0]:X2} {data[1]:X2} {data[2]:X2} {data[3]:X2})");

            // Read fields according to CCC structure (3DO is big-endian)
            uint chunkSize = ReadBigEndianUInt32(data, 0x04);
            uint ccbVersion = ReadBigEndianUInt32(data, 0x08);
            uint ccbFlags = ReadBigEndianUInt32(data, 0x0C);
            uint ccbNextPtr = ReadBigEndianUInt32(data, 0x10);
            uint ccbCelData = ReadBigEndianUInt32(data, 0x14);  // SourcePtr
            uint ccbPLUTPtr = ReadBigEndianUInt32(data, 0x18);

            int ccbX = ReadBigEndianInt32(data, 0x1C);
            int ccbY = ReadBigEndianInt32(data, 0x20);
            uint ccbHdx = ReadBigEndianUInt32(data, 0x24);
            uint ccbHdy = ReadBigEndianUInt32(data, 0x28);
            uint ccbVdx = ReadBigEndianUInt32(data, 0x2C);
            uint ccbVdy = ReadBigEndianUInt32(data, 0x30);

            uint ccbDdx = ReadBigEndianUInt32(data, 0x34);
            uint ccbDdy = ReadBigEndianUInt32(data, 0x38);

            uint ccbPPMPC = ReadBigEndianUInt32(data, 0x3C);

            uint ccbPRE0 = ReadBigEndianUInt32(data, 0x40);  // Preamble Word 0
            uint ccbPRE1 = ReadBigEndianUInt32(data, 0x44);  // Preamble Word 1

            int ccbWidth = ReadBigEndianInt32(data, 0x48);
            int ccbHeight = ReadBigEndianInt32(data, 0x4C);

            Console.WriteLine($"\nChunk Info:");
            Console.WriteLine($"  Chunk Size: 0x{chunkSize:X8} ({chunkSize} bytes)");
            Console.WriteLine($"  CCB Version: {ccbVersion}");

            Console.WriteLine($"\nFlags (ccb_Flags): 0x{ccbFlags:X8}");

            // Parse flags
            bool ccbSkip = (ccbFlags & 0x80000000) != 0;
            bool ccbLast = (ccbFlags & 0x40000000) != 0;
            bool ccbNpAbs = (ccbFlags & 0x20000000) != 0;
            bool ccbSpAbs = (ccbFlags & 0x10000000) != 0;
            bool ccbPpAbs = (ccbFlags & 0x08000000) != 0;
            bool ccbLdSize = (ccbFlags & 0x04000000) != 0;
            bool ccbLdPrs = (ccbFlags & 0x02000000) != 0;
            bool ccbLdPpmp = (ccbFlags & 0x01000000) != 0;
            bool ccbLdPlut = (ccbFlags & 0x00800000) != 0;
            bool ccbCcbLdp = (ccbFlags & 0x00400000) != 0;
            bool ccbYoxy = (ccbFlags & 0x00200000) != 0;
            bool ccbAcsc = (ccbFlags & 0x00100000) != 0;
            bool ccbAlsc = (ccbFlags & 0x00080000) != 0;
            bool ccbAcw = (ccbFlags & 0x00040000) != 0;
            bool ccbAcc = (ccbFlags & 0x00020000) != 0;
            bool ccbTwd = (ccbFlags & 0x00010000) != 0;
            bool ccbLce = (ccbFlags & 0x00008000) != 0;
            bool ccbAce = (ccbFlags & 0x00004000) != 0;
            int ccbPre = (int)((ccbFlags >> 6) & 0x3F);
            bool ccbNoblk = (ccbFlags & 0x00000010) != 0;
            bool ccbBgnd = (ccbFlags & 0x00000008) != 0;
            int ccbPpmp = (int)(ccbFlags & 0x00000007);

            Console.WriteLine($"  CCBSKIP (Skip CEL): {ccbSkip}");
            Console.WriteLine($"  CCBLAST (Last CEL): {ccbLast}");
            Console.WriteLine($"  CCBNPABS (Next ptr absolute): {ccbNpAbs}");
            Console.WriteLine($"  CCBSPABS (Source ptr absolute): {ccbSpAbs}");
            Console.WriteLine($"  CCBPPABS (PLUT ptr absolute): {ccbPpAbs}");
            Console.WriteLine($"  CCBLDSIZE (Load CEL size): {ccbLdSize}");
            Console.WriteLine($"  CCBLDPRS (Load preamble): {ccbLdPrs}");
            Console.WriteLine($"  CCBLDPPMP (Load PPMP): {ccbLdPpmp}");
            Console.WriteLine($"  CCBLDPLUT (Load PLUT): {ccbLdPlut}");
            Console.WriteLine($"  CCBCCBLDP (CCB load P): {ccbCcbLdp}");
            Console.WriteLine($"  CCBYOXY (Use XY offsets): {ccbYoxy}");
            Console.WriteLine($"  CCBACSC (AC SuperClip): {ccbAcsc}");
            Console.WriteLine($"  CCBALSC (AL SuperClip): {ccbAlsc}");
            Console.WriteLine($"  CCBACW (AC width): {ccbAcw}");
            Console.WriteLine($"  CCBACC (AC corner): {ccbAcc}");
            Console.WriteLine($"  CCBTWD (Two word desc): {ccbTwd}");
            Console.WriteLine($"  CCBLCE (Line control enable): {ccbLce}");
            Console.WriteLine($"  CCBACE (Arith comp enable): {ccbAce}");
            Console.WriteLine($"  CCBPRE (Preamble offset): {ccbPre}");
            Console.WriteLine($"  CCBNOBLK (No black): {ccbNoblk}");
            Console.WriteLine($"  CCBBGND (Background): {ccbBgnd}");
            Console.WriteLine($"  CCBPPMP (Pixel proc mode): {ccbPpmp}");

            Console.WriteLine($"\nPointers:");
            Console.WriteLine($"  Next CCB: 0x{ccbNextPtr:X8} {(ccbNpAbs ? "(absolute)" : "(relative)")}");
            Console.WriteLine($"  CelData (Source): 0x{ccbCelData:X8} {(ccbSpAbs ? "(absolute)" : "(relative)")}");
            Console.WriteLine($"  PLUT: 0x{ccbPLUTPtr:X8} {(ccbPpAbs ? "(absolute)" : "(relative)")}");

            Console.WriteLine($"\nPosition & Projection:");
            Console.WriteLine($"  X Position: {ccbX}");
            Console.WriteLine($"  Y Position: {ccbY}");
            Console.WriteLine($"  HDX (horizontal dX): 0x{ccbHdx:X8} ({ConvertFixedPoint(ccbHdx):F4})");
            Console.WriteLine($"  HDY (horizontal dY): 0x{ccbHdy:X8} ({ConvertFixedPoint(ccbHdy):F4})");
            Console.WriteLine($"  VDX (vertical dX): 0x{ccbVdx:X8} ({ConvertFixedPoint(ccbVdx):F4})");
            Console.WriteLine($"  VDY (vertical dY): 0x{ccbVdy:X8} ({ConvertFixedPoint(ccbVdy):F4})");
            Console.WriteLine($"  DDX (delta dX): 0x{ccbDdx:X8}");
            Console.WriteLine($"  DDY (delta dY): 0x{ccbDdy:X8}");

            Console.WriteLine($"\nPixel Processing:");
            Console.WriteLine($"  PPMPC: 0x{ccbPPMPC:X8}");

            Console.WriteLine($"\nPreamble & Dimensions:");
            Console.WriteLine($"  PRE0: 0x{ccbPRE0:X8}");
            Console.WriteLine($"  PRE1: 0x{ccbPRE1:X8}");
            Console.WriteLine($"  Width: {ccbWidth}");
            Console.WriteLine($"  Height: {ccbHeight}");

            // Decode PRE0 and PRE1 if present
            if (ccbPRE0 != 0 || ccbPRE1 != 0)
            {
                DecodePreamble(ccbPRE0, ccbPRE1);
            }

            Console.WriteLine($"\n=== End of CCB Header ===\n");
        }

        /// <summary>
        /// Decode and display CEL preamble information
        /// </summary>
        private static void DecodePreamble(uint pre0, uint pre1)
        {
            Console.WriteLine($"\n  Decoded Preamble Info:");

            // PRE0 contains bits per pixel and format flags  
            int bpp = (int)((pre0 >> 24) & 0xFF);
            bool packed = (pre0 & 0x80) != 0;      // Bit 7: Packed format
            bool linear = (pre0 & 0x40) != 0;       // Bit 6: Linear (uncoded) format

            Console.WriteLine($"    Bits Per Pixel (PRE0): {bpp}");
            Console.WriteLine($"    Packed (PRE0 bit 7): {packed}");
            Console.WriteLine($"    Linear/Uncoded (PRE0 bit 6): {linear}");
            Console.WriteLine($"    Format: {(linear ? "Uncoded" : "Coded")} {(packed ? "Packed" : "Unpacked")}");

            // PRE1 might contain width/height related info depending on format
            // This varies by CEL type, so just show the raw value for now
            Console.WriteLine($"    PRE1 Additional Info: 0x{pre1:X8}");
        }

        /// <summary>
        /// Read a big-endian 32-bit unsigned integer
        /// </summary>
        private static uint ReadBigEndianUInt32(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) |
                   ((uint)data[offset + 1] << 16) |
                   ((uint)data[offset + 2] << 8) |
                   data[offset + 3];
        }

        /// <summary>
        /// Read a big-endian 32-bit signed integer
        /// </summary>
        private static int ReadBigEndianInt32(byte[] data, int offset)
        {
            return (int)ReadBigEndianUInt32(data, offset);
        }

        /// <summary>
        /// Convert 16.16 fixed point to float
        /// </summary>
        private static float ConvertFixedPoint(uint value)
        {
            int intPart = (int)(value >> 16);
            float fracPart = (value & 0xFFFF) / 65536.0f;
            return intPart + fracPart;
        }

        /// <summary>
        /// Writes a pixel value to the output buffer
        /// </summary>
        private static void WritePixel(byte[] buffer, int offset, int pixelValue, int bitsPerPixel)
        {
            switch (bitsPerPixel)
            {
                case 1:
                case 2:
                case 4:
                case 6:
                    // For sub-byte pixels, pack them into bytes
                    // For simplicity, we're writing each as a full byte
                    // A more optimized version would pack these properly
                    buffer[offset] = (byte)pixelValue;
                    break;

                case 8:
                    buffer[offset] = (byte)pixelValue;
                    break;

                case 16:
                    // 16-bit pixel stored as little-endian
                    buffer[offset] = (byte)(pixelValue & 0xFF);
                    buffer[offset + 1] = (byte)((pixelValue >> 8) & 0xFF);
                    break;

                default:
                    throw new ArgumentException($"Unsupported bits per pixel: {bitsPerPixel}");
            }
        }

        /// <summary>
        /// Writes an RGB pixel value to the output buffer in RGBA32 format
        /// </summary>
        /// <param name="buffer">Output buffer (RGBA32 format - 4 bytes per pixel)</param>
        /// <param name="offset">Byte offset in buffer (should be pixelIndex * 4)</param>
        /// <param name="rgbValue">RGB value to write</param>
        /// <param name="bitsPerPixel">Source bits per pixel (8 for RGB332, 16 for RGB555)</param>
        private static void WriteRGBPixel(byte[] buffer, int offset, int rgbValue, int bitsPerPixel)
        {
            byte r, g, b, a = 255; // Alpha always 255 (opaque)

            if (bitsPerPixel == 8)
            {
                // RGB332 format: 3 bits red, 3 bits green, 2 bits blue
                int r3 = (rgbValue >> 5) & 0x07; // Bits 7-5
                int g3 = (rgbValue >> 2) & 0x07; // Bits 4-2
                int b2 = rgbValue & 0x03;        // Bits 1-0

                // Expand to 8-bit values
                r = (byte)((r3 * 255) / 7); // 3 bits -> 8 bits
                g = (byte)((g3 * 255) / 7); // 3 bits -> 8 bits
                b = (byte)((b2 * 255) / 3); // 2 bits -> 8 bits
            }
            else if (bitsPerPixel == 16)
            {
                // RGB555 format: 5 bits red, 5 bits green, 5 bits blue, 1 control bit
                int r5 = (rgbValue >> 10) & 0x1F; // Bits 14-10
                int g5 = (rgbValue >> 5) & 0x1F;  // Bits 9-5
                int b5 = rgbValue & 0x1F;         // Bits 4-0

                // Expand to 8-bit values
                r = (byte)((r5 * 255) / 31); // 5 bits -> 8 bits
                g = (byte)((g5 * 255) / 31); // 5 bits -> 8 bits
                b = (byte)((b5 * 255) / 31); // 5 bits -> 8 bits
            }
            else
            {
                throw new ArgumentException($"WriteRGBPixel: Unsupported bits per pixel: {bitsPerPixel}");
            }

            // Write RGBA32 (R, G, B, A)
            buffer[offset] = r;
            buffer[offset + 1] = g;
            buffer[offset + 2] = b;
            buffer[offset + 3] = a;
        }

        /// <summary>
        /// Bit-level reader for packed CEL data
        /// Reads bits MSB-first as per 3DO specification
        /// </summary>
        private class BitReader
        {
            private readonly byte[] data;
            private int bitPosition;

            public BitReader(byte[] data)
            {
                this.data = data;
                this.bitPosition = 0;
            }

            /// <summary>
            /// Get current position in 32-bit words
            /// </summary>
            public int CurrentWordPosition => bitPosition / 32;

            /// <summary>
            /// Check if there's more data to read
            /// </summary>
            public bool HasMoreData()
            {
                return bitPosition < data.Length * 8;
            }

            /// <summary>
            /// Peek at the next 32-bit word without advancing position
            /// Returns the word as stored (big-endian)
            /// </summary>
            public uint PeekWord()
            {
                AlignToWord();
                int byteIndex = bitPosition / 8;
                if (byteIndex + 4 <= data.Length)
                {
                    // Read as big-endian (MSB first)
                    return ((uint)data[byteIndex] << 24) |
                           ((uint)data[byteIndex + 1] << 16) |
                           ((uint)data[byteIndex + 2] << 8) |
                           data[byteIndex + 3];
                }
                return 0;
            }

            /// <summary>
            /// Seek to a specific word position
            /// </summary>
            public void SeekToWord(int wordPosition)
            {
                bitPosition = wordPosition * 32;
            }

            /// <summary>
            /// Read the specified number of bits from the stream
            /// Reads bits in MSB-first order within each byte
            /// </summary>
            public int ReadBits(int count)
            {
                int result = 0;

                for (int i = 0; i < count; i++)
                {
                    int byteIndex = bitPosition / 8;
                    int bitIndex = 7 - (bitPosition % 8); // MSB first within byte

                    if (byteIndex < data.Length)
                    {
                        int bit = (data[byteIndex] >> bitIndex) & 1;
                        result = (result << 1) | bit;
                    }

                    bitPosition++;
                }

                return result;
            }

            /// <summary>
            /// Align the bit position to the next 32-bit word boundary
            /// </summary>
            public void AlignToWord()
            {
                int bitsIntoWord = bitPosition % 32;
                if (bitsIntoWord != 0)
                {
                    bitPosition += (32 - bitsIntoWord);
                }
            }
        }

        /// <summary>
        /// Saves CEL image data to a PNG file with proper transparency and AMV support
        /// </summary>
        /// <param name="celOutput">The unpacked CEL image data</param>
        /// <param name="outputPath">Path where to save the PNG file</param>
        /// <param name="palette">Color palette to use for rendering (optional for 32bpp RGBA data)</param>
        public static void SaveCelImage(CelImageData celOutput, string outputPath, List<Color>? palette = null)
        {
            Image<Rgba32> image;

            // Handle 32bpp RGBA32 data first (from uncoded formats) - transparency already in alpha channel
            if (celOutput.BitsPerPixel == 32)
            {
                // 32bpp RGBA32 data - direct pixel copy
                image = new Image<Rgba32>(celOutput.Width, celOutput.Height);
                for (int y = 0; y < celOutput.Height; y++)
                {
                    for (int x = 0; x < celOutput.Width; x++)
                    {
                        int pixelIndex = y * celOutput.Width + x;
                        int byteOffset = pixelIndex * 4;
                        byte r = celOutput.PixelData[byteOffset];
                        byte g = celOutput.PixelData[byteOffset + 1];
                        byte b = celOutput.PixelData[byteOffset + 2];
                        byte a = celOutput.PixelData[byteOffset + 3];
                        image[x, y] = new Rgba32(r, g, b, a);
                    }
                }
            }
            // Check if we need custom rendering (transparency or AMV data) for palette-based formats
            else if (celOutput.TransparencyMask != null || celOutput.AlternateMultiplyValues != null)
            {
                // Palette-based formats with transparency/AMV require a palette
                if (palette == null)
                {
                    throw new ArgumentNullException(nameof(palette), "Palette is required for CEL images with transparency or AMV data");
                }

                // Create RGBA image with transparency and/or AMV support
                image = new Image<Rgba32>(celOutput.Width, celOutput.Height);
                int bytesPerPixel = (celOutput.BitsPerPixel + 7) / 8;

                for (int y = 0; y < celOutput.Height; y++)
                {
                    for (int x = 0; x < celOutput.Width; x++)
                    {
                        int pixelIndex = y * celOutput.Width + x;

                        // Read palette index based on bits per pixel
                        int plutIndex;
                        if (celOutput.BitsPerPixel == 16)
                        {
                            // For 16bpp, read 2 bytes as a 16-bit index
                            int byteOffset = pixelIndex * 2;
                            plutIndex = celOutput.PixelData[byteOffset] | (celOutput.PixelData[byteOffset + 1] << 8);
                        }
                        else
                        {
                            // For 8bpp and lower, read single byte
                            plutIndex = celOutput.PixelData[pixelIndex];
                        }

                        // Check transparency if mask exists
                        if (celOutput.TransparencyMask != null && celOutput.TransparencyMask[pixelIndex])
                        {
                            // Transparent pixel - set alpha to 0
                            image[x, y] = new Rgba32(0, 0, 0, 0);
                        }
                        else
                        {
                            // Opaque pixel - use palette color
                            Rgba32 baseColor = plutIndex < palette.Count ? palette[plutIndex] : palette[plutIndex % palette.Count];

                            // Apply AMV (Alternate Multiply Value) for brightness modulation if available
                            if (celOutput.AlternateMultiplyValues != null)
                            {
                                byte amv = celOutput.AlternateMultiplyValues[pixelIndex];
                                // AMV provides 8 brightness levels (0-7)
                                // 0 = darkest (black), 7 = full brightness
                                // Scale color by (amv / 7.0)
                                float multiplier = amv / 7.0f;

                                baseColor = new Rgba32(
                                    (byte)(baseColor.R * multiplier),
                                    (byte)(baseColor.G * multiplier),
                                    (byte)(baseColor.B * multiplier),
                                    baseColor.A
                                );
                            }

                            image[x, y] = baseColor;
                        }
                    }
                }
            }
            else
            {
                // Palette-based rendering (coded formats)
                if (palette == null)
                {
                    throw new ArgumentNullException(nameof(palette), "Palette is required for palette-indexed CEL images");
                }

                if (celOutput.BitsPerPixel == 8)
                {
                    // For 8bpp coded formats, pixel values contain both PLUT index and AMV
                    // Format: [AMV:3][PLUT:5] - we need to extract the lower 5 bits (PLUT index)
                    byte[] cleanedPixelData = new byte[celOutput.PixelData.Length];
                    for (int i = 0; i < celOutput.PixelData.Length; i++)
                    {
                        cleanedPixelData[i] = (byte)(celOutput.PixelData[i] & 0x1F); // Extract lower 5 bits (PLUT index)
                    }
                    image = ImageFormatHelper.GenerateIMClutImage(palette, cleanedPixelData, celOutput.Width, celOutput.Height);
                }
                else if (celOutput.BitsPerPixel < 8)
                {
                    // For sub-byte formats (1, 2, 4, 6 bpp), data is already clean palette indices
                    image = ImageFormatHelper.GenerateIMClutImage(palette, celOutput.PixelData, celOutput.Width, celOutput.Height);
                }
                else // 16bpp
                {
                    image = ImageFormatHelper.GenerateIM16BitImage(palette, celOutput.PixelData, celOutput.Width, celOutput.Height);
                }
            }

            image.SaveAsPng(outputPath);
        }

        /// <summary>
        /// Convenience method that unpacks a CEL file and saves it directly to PNG
        /// </summary>
        /// <param name="celFilePath">Path to the CEL file</param>
        /// <param name="outputPath">Path where to save the PNG file</param>
        /// <param name="palette">Color palette to use for rendering</param>
        /// <param name="bitsPerPixel">Optional: Override auto-detected bits per pixel (1, 2, 4, 6, 8, or 16). If 0, auto-detect.</param>
        /// <param name="verbose">If true, displays diagnostic information</param>
        /// <returns>True if successful, false if failed</returns>
        public static bool UnpackAndSaveCelFile(string celFilePath, string outputPath, List<Color> palette, int bitsPerPixel = 0, bool verbose = false)
        {
            var celData = UnpackCelFile(celFilePath, verbose, bitsPerPixel);
            if (celData == null)
            {
                if (verbose) Console.WriteLine($"Failed to unpack CEL file: {celFilePath}");
                return false;
            }

            try
            {
                SaveCelImage(celData, outputPath, palette);
                if (verbose) Console.WriteLine($"Successfully saved CEL image to: {outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                if (verbose) Console.WriteLine($"Failed to save CEL image: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Result data structure for unpacked CEL images
    /// </summary>
    public class CelImageData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int BitsPerPixel { get; set; }
        public byte[] PixelData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Transparency mask: true = transparent pixel (from PACK_TRANSPARENT packet), false = opaque pixel.
        /// Only populated for packed CEL formats. Length matches PixelData length.
        /// </summary>
        public bool[]? TransparencyMask { get; set; } = null;

        /// <summary>
        /// RGB Alternate Multiply Values for 8-bit coded pixels.
        /// For 8bpp coded: upper 3 bits are AMV (0-7), lower 5 bits are PLUT index (0-31).
        /// Each AMV value (0-7) represents a brightness/shade multiplier.
        /// Only populated for 8-bit coded formats. null for other formats.
        /// </summary>
        public byte[]? AlternateMultiplyValues { get; set; } = null;

        /// <summary>
        /// Palette data extracted from PLUT chunks.
        /// Only populated when the file contains palette data instead of image data.
        /// null for image files.
        /// </summary>
        public List<Color>? Palette { get; set; } = null;
    }
}
