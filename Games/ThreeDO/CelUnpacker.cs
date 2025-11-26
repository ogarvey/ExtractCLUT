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
        public static CelImageData UnpackCodedPackedWithDimensions(byte[] packedData, int width, int height, int bitsPerPixel = 8, bool verbose = false, bool skipUncompSize = false, uint ccbFlags = 0)
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

                //if (verbose) Console.WriteLine($"  Row {row}: Start word position = {rowStartWord}");

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

                //if (verbose) Console.WriteLine($"    Next row offset: {nextRowOffset}");

                // Calculate where next row should start (in words)
                int nextRowWord = rowStartWord + nextRowOffset + 2;

                //if (verbose) Console.WriteLine($"    Next row word position: {nextRowWord}, data has {actualData.Length / 4} words total");

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
            }

            // Check if there are any actual transparent pixels
            bool hasTransparency = false;
            for (int i = 0; i < transparencyMask.Length; i++)
            {
                if (transparencyMask[i])
                {
                    hasTransparency = true;
                    break;
                }
            }

            return new CelImageData
            {
                Width = width,
                Height = height,
                BitsPerPixel = bitsPerPixel,
                PixelData = unpackedData,
                TransparencyMask = hasTransparency ? transparencyMask : null,
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

            }

            // Check if there are any actual transparent pixels
            bool hasTransparency = false;
            for (int i = 0; i < transparencyMask.Length; i++)
            {
                if (transparencyMask[i])
                {
                    hasTransparency = true;
                    break;
                }
            }

            return new CelImageData
            {
                Width = width,
                Height = height,
                BitsPerPixel = 32, // RGBA32 output
                PixelData = unpackedData,
                TransparencyMask = hasTransparency ? transparencyMask : null
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

                // For BOTH coded and uncoded unpacked data: just read raw pixels
                // The only difference is that coded pixels go through PLUT lookup later
                for (int col = 0; col < width; col++)
                {
                    int pixelValue = bitReader.ReadBits(bitsPerPixel);

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
            var results = UnpackCelFile_FromBytes_Multiple(data, verbose, bitsPerPixel, skipUncompSize);
            return results.Count > 0 ? results[0] : null;
        }

        /// <summary>
        /// Reads a CEL file and extracts all PDAT chunks as separate images.
        /// Some CEL files contain multiple PDAT chunks that should be extracted separately.
        /// </summary>
        /// <param name="celFile">Full path to the CEL file</param>
        /// <param name="verbose">If true, displays CCB header information</param>
        /// <param name="bitsPerPixel">Optional: Override auto-detected bits per pixel (1, 2, 4, 6, 8, or 16). If 0, auto-detect.</param>
        /// <returns>List of unpacked CEL image data (one per PDAT chunk)</returns>
        public static List<CelImageData> UnpackCelFileMultiple(string celFile, bool verbose = false, int bitsPerPixel = 0, bool skipUncompSize = false)
        {
            if (!File.Exists(celFile))
            {
                Console.WriteLine($"File not found: {celFile}");
                return new List<CelImageData>();
            }

            byte[] data = File.ReadAllBytes(celFile);
            return UnpackCelFile_FromBytes_Multiple(data, verbose, bitsPerPixel, skipUncompSize);
        }

        /// <summary>
        /// Parses CEL data from a byte array (CCB header + PDAT chunks) and automatically unpacks all PDAT chunks as separate images.
        /// This is useful for CEL files containing multiple PDAT chunks that should be extracted separately.
        /// </summary>
        /// <param name="data">CEL data bytes (must start with CCB or PDAT chunk)</param>
        /// <param name="verbose">If true, displays CCB header information</param>
        /// <param name="bitsPerPixel">Optional: Override auto-detected bits per pixel (1, 2, 4, 6, 8, or 16). If 0, auto-detect.</param>
        /// <returns>List of unpacked CEL image data (one per PDAT chunk)</returns>
        public static List<CelImageData>  UnpackCelFile_FromBytes_Multiple(byte[] data, bool verbose = false, int bitsPerPixel = 0, bool skipUncompSize = false)
        {
            var results = new List<CelImageData>();

            if (data.Length < 0x20)
            {
                Console.WriteLine($"File too small to contain a CCB header (minimum 32 bytes, got {data.Length})");
                return results;
            }

            // Use chunk-based parsing with BinaryReader
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            // Store all chunks in order, then match them up properly
            var chunks = new List<(string type, int index, object data)>();
            var ccbList = new List<(int width, int height, uint flags, uint pre0, uint pre1, uint pixc)>();
            var pdatList = new List<byte[]>();
            var plutList = new List<List<Color>>();

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
                        return results;
                    }

                    long ccbDataStart = stream.Position;

                    // Read CCB header fields
                    stream.Position = ccbDataStart + 0x04; // Skip version field (4 bytes)
                    uint ccbFlags = ReadBigEndianUInt32(data, (int)stream.Position);

                    // Skip to PRE0 and PRE1 (offsets 0x38 and 0x3C in CCB data)
                    stream.Position = ccbDataStart + 0x38;
                    uint pre0 = ReadBigEndianUInt32(data, (int)stream.Position);
                    stream.Position = ccbDataStart + 0x3C;
                    uint pre1 = ReadBigEndianUInt32(data, (int)stream.Position);

                    // Read width and height from CCB data (offsets 0x40 and 0x44 in CCB data)
                    int ccbWidth = ReadBigEndianInt32(data, (int)ccbDataStart + 0x40);
                    int ccbHeight = ReadBigEndianInt32(data, (int)ccbDataStart + 0x44);

                    // Read PIXC word (CCB word #13) if LDPIXC flag is set (bit 24)
                    uint pixc = 0;
                    bool ldpixc = (ccbFlags & 0x01000000) != 0; // LDPIXC flag - bit 24
                    if (ldpixc && chunkSize >= 0x40) // Need at least 64 bytes for PIXC at offset 0x34
                    {
                        pixc = ReadBigEndianUInt32(data, (int)ccbDataStart + 0x34);
                    }

                    // Store CCB data including PIXC
                    int ccbIndex = ccbList.Count;
                    ccbList.Add((ccbWidth, ccbHeight, ccbFlags, pre0, pre1, pixc));
                    chunks.Add(("CCB", ccbIndex, null!));

                    if (verbose)
                    {
                        Console.WriteLine($"CCB #{ccbIndex + 1}: Width={ccbWidth}, Height={ccbHeight}, Flags=0x{ccbFlags:X8}");
                        Console.WriteLine($"CCB #{ccbIndex + 1}: PRE0=0x{pre0:X8}, PRE1=0x{pre1:X8}");
                        if (ldpixc)
                        {
                            Console.WriteLine($"CCB #{ccbIndex + 1}: PIXC=0x{pixc:X8} (LDPIXC flag set)");
                        }
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

                    // Read palette data and store
                    byte[] plutData = reader.ReadBytes((int)plutDataSize);
                    List<Color> palette = ExtractPaletteFromPLUT(plutData, verbose);
                    
                    int plutIndex = plutList.Count;
                    plutList.Add(palette);
                    chunks.Add(("PLUT", plutIndex, null!));

                    if (verbose)
                    {
                        Console.WriteLine($"PLUT #{plutIndex + 1}: {palette.Count} colors");
                    }

                    // Skip to next chunk (should already be at correct position)
                }
                else if (magicStr == "PDAT")
                {
                    // Process PDAT chunk - just store the pixel data for now
                    uint pdatDataSize = chunkSize - 8; // Subtract magic (4) + size (4)
                    byte[] pixelData = reader.ReadBytes((int)pdatDataSize);
                    
                    int pdatIndex = pdatList.Count;
                    pdatList.Add(pixelData);
                    chunks.Add(("PDAT", pdatIndex, null!));

                    if (verbose)
                    {
                        Console.WriteLine($"PDAT #{pdatIndex + 1}: {pixelData.Length} bytes");
                    }

                    // Skip to next chunk (should already be at correct position)
                }
                else
                {
                    // Unknown chunk - skip using its size
                    uint skipSize = chunkSize - 8; // Subtract magic (4) + size (4) already read
                    if (stream.Position + skipSize > data.Length)
                    {
                        Console.WriteLine($"Chunk '{magicStr}' size exceeds file bounds, stopping parsing");
                        break;
                    }
                    stream.Position += skipSize;

                    if (verbose)
                    {
                        Console.WriteLine($"Skipping unknown chunk '{magicStr}' ({skipSize} bytes)");
                    }
                }
            }

            // Now match up chunks: For each PDAT, find its preceding CCB and following/preceding PLUT
            var pdatGroups = new List<(byte[] pixelData, int width, int height, uint flags, uint pre0, uint pre1, uint pixc, List<Color> palette)>();
            
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i].type == "PDAT")
                {
                    int pdatIdx = chunks[i].index;
                    byte[] pixelData = pdatList[pdatIdx];
                    
                    // Find the most recent CCB before this PDAT
                    int ccbIdx = -1;
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (chunks[j].type == "CCB")
                        {
                            ccbIdx = chunks[j].index;
                            break;
                        }
                    }
                    
                    if (ccbIdx == -1)
                    {
                        Console.WriteLine($"Warning: PDAT #{pdatIdx + 1} has no preceding CCB chunk, skipping");
                        continue;
                    }
                    
                    // Find the nearest PLUT (check after first, then before)
                    int plutIdx = -1;
                    // Check forward first (for CCB → PDAT → PLUT pattern)
                    for (int j = i + 1; j < chunks.Count; j++)
                    {
                        if (chunks[j].type == "PLUT")
                        {
                            plutIdx = chunks[j].index;
                            break;
                        }
                        // Stop if we hit another CCB or PDAT
                        if (chunks[j].type == "CCB" || chunks[j].type == "PDAT")
                            break;
                    }
                    
                    // If not found forward, check backward (for CCB → PLUT → PDAT pattern)
                    if (plutIdx == -1)
                    {
                        for (int j = i - 1; j >= 0; j--)
                        {
                            if (chunks[j].type == "PLUT")
                            {
                            plutIdx = chunks[j].index;
                                break;
                            }
                            // Stop if we hit a CCB that's not our own
                            if (chunks[j].type == "CCB" && chunks[j].index != ccbIdx)
                                break;
                        }
                    }
                    
                    var ccb = ccbList[ccbIdx];
                    List<Color> palette = plutIdx >= 0 ? plutList[plutIdx] : new List<Color>();
                    
                    pdatGroups.Add((pixelData, ccb.width, ccb.height, ccb.flags, ccb.pre0, ccb.pre1, ccb.pixc, palette));
                    
                    if (verbose)
                    {
                        Console.WriteLine($"Matched: CCB #{ccbIdx + 1} + PDAT #{pdatIdx + 1} + PLUT #{(plutIdx >= 0 ? (plutIdx + 1).ToString() : "none")} = {ccb.width}x{ccb.height}, {palette.Count} colors");
                    }
                }
            }

            // Check if we have any valid data to process
            if (ccbList.Count == 0 && pdatGroups.Count == 0)
            {
                // No CCB and no PDAT groups - check if we have palette-only data
                if (plutList.Count > 0)
                {
                    if (verbose)
                    {
                        Console.WriteLine($"File contains palette-only data with {plutList[0].Count} colors");
                    }

                    results.Add(new CelImageData
                    {
                        Width = 0,
                        Height = 0,
                        BitsPerPixel = 0,
                        PixelData = Array.Empty<byte>(),
                        Palette = plutList[0]
                    });
                    return results;
                }
                
                Console.WriteLine("No valid CCB chunk or PDAT data found in file");
                return results;
            }

            // If we have no pixel data, return empty
            if (pdatGroups.Count == 0)
            {
                Console.WriteLine("No pixel data found in file");
                return results;
            }

            if (verbose)
            {
                Console.WriteLine($"\n========================================");
                Console.WriteLine($"Processing {pdatGroups.Count} PDAT chunk(s)");
                Console.WriteLine($"========================================");
            }

            // Process each PDAT chunk separately with its associated CCB and palette
            for (int pdatIndex = 0; pdatIndex < pdatGroups.Count; pdatIndex++)
            {
                var group = pdatGroups[pdatIndex];
                byte[] pixelData = group.pixelData;
                int width = group.width;
                int height = group.height;
                uint flags = group.flags;
                uint pre0 = group.pre0;
                uint pre1 = group.pre1;
                uint pixc = group.pixc;
                List<Color> palette = group.palette;

                if (verbose)
                {
                    Console.WriteLine($"\n--- Processing PDAT chunk #{pdatIndex + 1} ---");
                    Console.WriteLine($"CCB FLAGS: 0x{flags:X8}");
                    Console.WriteLine($"PACKED: {((flags & 0x00000200) != 0)}");
                    Console.WriteLine($"CCBPRE: {((flags & 0x00400000) != 0)}");
                    Console.WriteLine($"PRE0: 0x{pre0:X8}, PRE1: 0x{pre1:X8}");
                    if (pixc != 0)
                    {
                        Console.WriteLine($"PIXC: 0x{pixc:X8}");
                    }
                    Console.WriteLine($"Width: {width}, Height: {height}");
                    Console.WriteLine($"Pixel data size: {pixelData.Length} bytes");
                    Console.WriteLine($"Palette colors: {palette.Count}");
                }

                // Process this PDAT chunk using its associated CCB header data
                var result = ProcessSinglePDAT(pixelData, width, height, flags, pre0, pre1, pixc, bitsPerPixel, skipUncompSize, verbose);
                
                if (result != null)
                {
                    // Add palette data to the result if we have it
                    if (palette.Count > 0)
                    {
                        result.Palette = palette;
                    }
                    results.Add(result);
                }
            }

            if (verbose)
            {
                Console.WriteLine($"\nSuccessfully processed {results.Count} of {pdatGroups.Count} PDAT chunks");
            }

            return results;
        }

        /// <summary>
        /// Processes a single PDAT chunk with CCB header information.
        /// Extracted as helper method to be reused for multiple PDAT chunks.
        /// </summary>
        private static CelImageData? ProcessSinglePDAT(byte[] pixelData, int ccbWidth, int ccbHeight, uint ccbFlags, uint pre0, uint pre1, uint pixc, int bitsPerPixel, bool skipUncompSize, bool verbose)
        {
            // Detect format from FLAGS and PRE0/PRE1
            bool isCoded = true;
            bool isPacked = false;
            int bpp = bitsPerPixel; // Use override if provided

            // Check CCBPRE flag to determine where preamble is located
            bool ccbpreFlag = (ccbFlags & 0x00400000) != 0; // CCBPRE flag - bit 22

            if (ccbpreFlag && pre0 != 0)
            {
                // CCBPRE=1: Preamble is in CCB (PRE0/PRE1 fields)
                // Pixel data starts immediately in PDAT
                if (verbose)
                {
                    Console.WriteLine($"CCBPRE=1: Preamble in CCB, pixel data starts at PDAT offset 0");
                }

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
                
                // Check CCB FLAGS PACKED bit - it can override PRE0 bit 7
                if ((ccbFlags & 0x00000200) != 0) // PACKED flag - bit 9
                {
                    isPacked = true;
                    if (verbose)
                    {
                        Console.WriteLine($"  PACKED flag in CCB FLAGS overrides PRE0 bit 7 -> isPacked=true");
                    }
                }
            }
            else if (!ccbpreFlag && pixelData != null && pixelData.Length >= 4)
            {
                // CCBPRE=0: Preamble is at the beginning of PDAT
                // Read PRE0 from first 4 bytes of pixel data
                uint pdatPre0 = ReadBigEndianUInt32(pixelData, 0);

                bool linear = (pdatPre0 & 0x10) != 0;    // Bit 4: 1 = linear (uncoded), 0 = coded
                bool packed = (pdatPre0 & 0x80) != 0;    // Bit 7: 1 = packed, 0 = unpacked
                isCoded = !linear;
                isPacked = packed;

                // IMPORTANT: Check if CCB FLAGS PACKED bit overrides PRE0 bit 7 BEFORE calculating preamble size
                if ((ccbFlags & 0x00000200) != 0) // PACKED flag - bit 9
                {
                    isPacked = true;
                }

                // Determine how many preamble bytes to skip based on FINAL isPacked value (after CCB override)
                int preambleBytes = isPacked ? 4 : 8; // Packed=4 bytes (PRE0 only), Unpacked=8 bytes (PRE0+PRE1)

                if (verbose)
                {
                    Console.WriteLine($"CCBPRE=0: Preamble at start of PDAT");
                    Console.WriteLine($"  PRE0 from PDAT: 0x{pdatPre0:X8}");
                    Console.WriteLine($"  Linear (bit 4): {linear} (Coded: {isCoded})");
                    Console.WriteLine($"  Packed (bit 7): {packed}");
                    if ((ccbFlags & 0x00000200) != 0)
                    {
                        Console.WriteLine($"  PACKED flag in CCB FLAGS overrides PRE0 bit 7 -> isPacked=true");
                    }
                    Console.WriteLine($"  Skipping {preambleBytes} preamble bytes from PDAT");
                }

                // Skip the preamble bytes - create new array without them
                byte[] actualPixelData = new byte[pixelData.Length - preambleBytes];
                Array.Copy(pixelData, preambleBytes, actualPixelData, 0, actualPixelData.Length);
                pixelData = actualPixelData;

                // Use PDAT's PRE0 for BPP detection
                pre0 = pdatPre0;

                if (verbose)
                {
                    Console.WriteLine($"  Pixel data size after skipping preamble: {pixelData.Length} bytes");
                }
            }
            else if ((ccbFlags & 0x00000200) != 0) // PACKED flag in CCB FLAGS (only if CCBPRE=1)
            {
                // CCBPRE=1: Check PACKED flag from CCB FLAGS word
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

            // Heuristic: Validate packed/unpacked flag against actual file size
            // if (ccbWidth > 0 && ccbHeight > 0 && bpp > 0 && pixelData != null)
            // {
            //     int bitsPerRow = ccbWidth * bpp;
            //     int bytesPerRow = ((bitsPerRow + 31) / 32) * 4;
            //     int expectedUnpackedSize = ccbHeight * bytesPerRow;
            //     int actualDataSize = pixelData.Length;
            //     int simpleUnpackedSize = ccbWidth * ccbHeight * ((bpp + 7) / 8);

            //     if (verbose)
            //     {
            //         Console.WriteLine($"\n  File size analysis:");
            //         Console.WriteLine($"    Expected unpacked (word-aligned): {expectedUnpackedSize} bytes");
            //         Console.WriteLine($"    Expected unpacked (simple): {simpleUnpackedSize} bytes");
            //         Console.WriteLine($"    Actual size: {actualDataSize} bytes");
            //     }

            //     if (!isPacked && actualDataSize < expectedUnpackedSize * 0.95)
            //     {
            //         if (verbose)
            //         {
            //             Console.WriteLine($"  ⚠ Size too small for unpacked format -> Overriding to PACKED");
            //         }
            //         isPacked = true;
            //     }
            //     else if (isPacked && (actualDataSize == expectedUnpackedSize || 
            //                           actualDataSize == simpleUnpackedSize ))
            //     {
            //         if (verbose)
            //         {
            //             Console.WriteLine($"  ⚠ Size matches unpacked format exactly -> Overriding to UNPACKED");
            //         }
            //         isPacked = false;
            //     }
            // }

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
                    if (ccbWidth <= 0 || ccbHeight <= 0)
                    {
                        result = UnpackCodedPackedCelData(pixelData, bpp, verbose: verbose);
                    }
                    else
                    {
                        result = UnpackCodedPackedWithDimensions(pixelData, ccbWidth, ccbHeight, bpp, verbose: verbose, skipUncompSize: skipUncompSize, ccbFlags: ccbFlags);
                    }
                }
                else if (isCoded && !isPacked)
                {
                    if (ccbWidth <= 0 || ccbHeight <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Coded unpacked format requires valid dimensions (width={ccbWidth}, height={ccbHeight})");
                    }
                    result = UnpackCodedUnpackedCelData(pixelData, ccbWidth, ccbHeight, bpp);
                }
                else if (!isCoded && isPacked)
                {
                    if (ccbWidth <= 0 || ccbHeight <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Uncoded packed format requires valid dimensions (width={ccbWidth}, height={ccbHeight})");
                    }
                    result = UnpackUncodedPackedWithDimensions(pixelData, ccbWidth, ccbHeight, bpp, verbose: verbose);
                }
                else
                {
                    if (ccbWidth <= 0 || ccbHeight <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Uncoded unpacked format requires valid dimensions (width={ccbWidth}, height={ccbHeight})");
                    }
                    result = UnpackUncodedUnpackedWithDimensions(pixelData, ccbWidth, ccbHeight, bpp, verbose: verbose);
                }

                if (verbose)
                {
                    Console.WriteLine($"\nUnpacked image:");
                    Console.WriteLine($"  Width: {result.Width}");
                    Console.WriteLine($"  Height: {result.Height}");
                    Console.WriteLine($"  BPP: {result.BitsPerPixel}");
                    Console.WriteLine($"  Pixel data size: {result.PixelData.Length} bytes");
                }

                // Store CCB flags and PIXC in result for later use in rendering
                result.CcbFlags = ccbFlags;
                result.Pixc = pixc;

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error unpacking PDAT: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Parses CEL data from a byte array (CCB header + PDAT chunks) and automatically unpacks the pixel data.
        /// This is useful for processing CEL data embedded in other file formats (like ANIM files).
        /// Returns only the first PDAT chunk if multiple are present.
        /// </summary>
        /// <param name="data">CEL data bytes (must start with CCB or PDAT chunk)</param>
        /// <param name="verbose">If true, displays CCB header information</param>
        /// <param name="bitsPerPixel">Optional: Override auto-detected bits per pixel (1, 2, 4, 6, 8, or 16). If 0, auto-detect.</param>
        /// <returns>Unpacked CEL image data with dimensions and pixel data</returns>
        private static CelImageData? UnpackCelFile_FromBytes(byte[] data, bool verbose = false, int bitsPerPixel = 0, bool skipUncompSize = false)
        {
            var results = UnpackCelFile_FromBytes_Multiple(data, verbose, bitsPerPixel, skipUncompSize);
            return results.Count > 0 ? results[0] : null;
        }

        /// <summary>
        /// LEGACY METHOD - Kept for compatibility
        /// Parses CEL data from a byte array (CCB header + PDAT chunks) and automatically unpacks the pixel data.
        /// This is useful for processing CEL data embedded in other file formats (like ANIM files).
        /// </summary>
        /// <param name="data">CEL data bytes (must start with CCB or PDAT chunk)</param>
        /// <param name="verbose">If true, displays CCB header information</param>
        /// <param name="bitsPerPixel">Optional: Override auto-detected bits per pixel (1, 2, 4, 6, 8, or 16). If 0, auto-detect.</param>
        /// <returns>Unpacked CEL image data with dimensions and pixel data</returns>
        private static CelImageData? UnpackCelFile_FromBytes_Legacy(byte[] data, bool verbose = false, int bitsPerPixel = 0, bool skipUncompSize = false)
        {

            if (data.Length < 0x20)
            {
                Console.WriteLine($"File too small to contain a CCB header (minimum 32 bytes, got {data.Length})");
                return null;
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
                                    // Check CCBPRE flag to determine where preamble is located
            bool ccbpreFlag = (ccbFlags & 0x00400000) != 0; // CCBPRE flag - bit 22

            if (ccbpreFlag && pre0 != 0)
            {
                // CCBPRE=1: Preamble is in CCB (PRE0/PRE1 fields)
                // Pixel data starts immediately in PDAT
                if (verbose)
                {
                    Console.WriteLine($"CCBPRE=1: Preamble in CCB, pixel data starts at PDAT offset 0");
                }

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
                
                // Check CCB FLAGS PACKED bit - it can override PRE0 bit 7
                if ((ccbFlags & 0x00000200) != 0) // PACKED flag - bit 9
                {
                    isPacked = true;
                    if (verbose)
                    {
                        Console.WriteLine($"  PACKED flag in CCB FLAGS overrides PRE0 bit 7 -> isPacked=true");
                    }
                }
            }
            else if (!ccbpreFlag && pixelData != null && pixelData.Length >= 4)
            {
                // CCBPRE=0: Preamble is at the beginning of PDAT
                // Read PRE0 from first 4 bytes of pixel data
                pre0 = ReadBigEndianUInt32(pixelData, 0);

                bool linear = (pre0 & 0x10) != 0;    // Bit 4: 1 = linear (uncoded), 0 = coded
                bool packed = (pre0 & 0x80) != 0;    // Bit 7: 1 = packed, 0 = unpacked
                isCoded = !linear;
                isPacked = packed;

                // IMPORTANT: Check if CCB FLAGS PACKED bit overrides PRE0 bit 7 BEFORE calculating preamble size
                if ((ccbFlags & 0x00000200) != 0) // PACKED flag - bit 9
                {
                    isPacked = true;
                }

                // Determine how many preamble bytes to skip based on FINAL isPacked value (after CCB override)
                int preambleBytes = isPacked ? 4 : 8; // Packed=4 bytes (PRE0 only), Unpacked=8 bytes (PRE0+PRE1)

                if (verbose)
                {
                    Console.WriteLine($"CCBPRE=0: Preamble at start of PDAT");
                    Console.WriteLine($"  PRE0 from PDAT: 0x{pre0:X8}");
                    Console.WriteLine($"  Linear (bit 4): {linear} (Coded: {isCoded})");
                    Console.WriteLine($"  Packed (bit 7): {packed}");
                    if ((ccbFlags & 0x00000200) != 0)
                    {
                        Console.WriteLine($"  PACKED flag in CCB FLAGS overrides PRE0 bit 7 -> isPacked=true");
                    }
                    Console.WriteLine($"  Skipping {preambleBytes} preamble bytes from PDAT");
                }

                // Skip the preamble bytes - create new array without them
                byte[] actualPixelData = new byte[pixelData.Length - preambleBytes];
                Array.Copy(pixelData, preambleBytes, actualPixelData, 0, actualPixelData.Length);
                pixelData = actualPixelData;

                if (verbose)
                {
                    Console.WriteLine($"  Pixel data size after skipping preamble: {pixelData.Length} bytes");
                }
            }
            else if ((ccbFlags & 0x00000200) != 0) // PACKED flag in CCB FLAGS (only if CCBPRE=1)
            {
                // CCBPRE=1: Check PACKED flag from CCB FLAGS word
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

            // Heuristic: Validate packed/unpacked flag against actual file size
            // Some files have incorrect PRE0 bit 7 in CCB headers
            if (ccbWidth > 0 && ccbHeight > 0 && bpp > 0 && pixelData != null)
            {
                // For unpacked format, each row is word-aligned (32-bit boundary)
                // Calculate bits per row, then round up to nearest word (4 bytes)
                int bitsPerRow = ccbWidth * bpp;
                int bytesPerRow = ((bitsPerRow + 31) / 32) * 4; // Round up to word boundary
                int expectedUnpackedSize = ccbHeight * bytesPerRow;
                int actualDataSize = pixelData.Length;

                // Calculate expected size for simple unpacked case (no word alignment, just raw bytes)
                int simpleUnpackedSize = ccbWidth * ccbHeight * ((bpp + 7) / 8);

                if (verbose)
                {
                    Console.WriteLine($"\n  File size analysis:");
                    Console.WriteLine($"    Expected unpacked (word-aligned): {expectedUnpackedSize} bytes");
                    Console.WriteLine($"    Expected unpacked (simple): {simpleUnpackedSize} bytes");
                    Console.WriteLine($"    Actual size: {actualDataSize} bytes");
                }

                // Case 1: Marked as UNPACKED, but size is too small -> Actually PACKED
                if (!isPacked && actualDataSize < expectedUnpackedSize * 0.75)
                {
                    if (verbose)
                    {
                        Console.WriteLine($"  ⚠ Size too small for unpacked format -> Overriding to PACKED");
                    }
                    isPacked = true;
                }
                // Case 2: Marked as PACKED, but size matches unpacked exactly -> Actually UNPACKED
                else if (isPacked && (actualDataSize == expectedUnpackedSize || 
                                      actualDataSize == simpleUnpackedSize ||
                                      Math.Abs(actualDataSize - expectedUnpackedSize) < 100))
                {
                    if (verbose)
                    {
                        Console.WriteLine($"  ⚠ Size matches unpacked format exactly -> Overriding to UNPACKED");
                    }
                    isPacked = false;
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
                        result = UnpackCodedPackedWithDimensions(pixelData, ccbWidth, ccbHeight, bpp, verbose: verbose, skipUncompSize: skipUncompSize, ccbFlags: ccbFlags);
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
        public class BitReader
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
        /// <param name="ccbFlags">CCB FLAGS word - used to check BGND flag for transparency handling</param>
        /// <param name="pixc">PIXC control word for pixel processor operations (0 = use default AMV scaling)</param>
        /// <param name="useFrameBufferBlending">If true, simulate frame buffer blending for transparent background effects</param>
        /// <param name="verbose">Enable verbose logging of pixel processor operations</param>
        public static void SaveCelImage(CelImageData celOutput, string outputPath, List<Color>? palette = null, uint ccbFlags = 0, uint pixc = 0, bool useFrameBufferBlending = false, bool verbose = false)
        {
            // Decode PIXC control word for pixel processor operations
            // P-mode 0 (lower 16 bits) and P-mode 1 (upper 16 bits)
            bool pixcProvided = pixc != 0;
            
            // P-mode 0 bits (bits 0-15)
            uint pixcP0 = pixc & 0xFFFF;
            bool p0_1S = (pixcP0 & 0x8000) != 0;        // Bit 15: Primary source (0=cel, 1=frame buffer)
            uint p0_MS = (pixcP0 >> 13) & 0x03;         // Bits 14-13: Multiplier source
            uint p0_MF = (pixcP0 >> 10) & 0x07;         // Bits 12-10: Multiplier factor (if MS=00)
            uint p0_DF = (pixcP0 >> 8) & 0x03;          // Bits 9-8: Divider factor
            uint p0_2S = (pixcP0 >> 6) & 0x03;          // Bits 7-6: Secondary source
            uint p0_AV = (pixcP0 >> 1) & 0x1F;          // Bits 5-1: Secondary source value/AV bits
            bool p0_2D = (pixcP0 & 0x01) != 0;          // Bit 0: Secondary divider (0=1, 1=2)
            
            // P-mode 1 bits (bits 16-31) - same structure
            uint pixcP1 = (pixc >> 16) & 0xFFFF;
            bool p1_1S = (pixcP1 & 0x8000) != 0;
            uint p1_MS = (pixcP1 >> 13) & 0x03;
            uint p1_MF = (pixcP1 >> 10) & 0x07;
            uint p1_DF = (pixcP1 >> 8) & 0x03;
            uint p1_2S = (pixcP1 >> 6) & 0x03;
            uint p1_AV = (pixcP1 >> 1) & 0x1F;
            bool p1_2D = (pixcP1 & 0x01) != 0;
            
            // Decode POVER bits from CCB FLAGS (bits 8-7) to determine P-mode selection
            uint pover = (ccbFlags >> 7) & 0x03;
            // POVER: 00=use P-mode from pixel, 01=reserved, 10=force P-mode 0, 11=force P-mode 1
            
            if (verbose && pixcProvided)
            {
                Console.WriteLine($"\n[PIXC Pixel Processor Configuration]");
                Console.WriteLine($"  PIXC Word: 0x{pixc:X8}");
                Console.WriteLine($"  POVER (CCB bits 8-7): {pover} ({(pover == 0 ? "From Pixel" : pover == 2 ? "Force P-mode 0" : pover == 3 ? "Force P-mode 1" : "Reserved")})");
                Console.WriteLine($"\n  P-mode 0 (bits 0-15): 0x{pixcP0:X4}");
                Console.WriteLine($"    1S (Primary Source): {(p0_1S ? "Frame Buffer" : "Cel Pixel")}");
                Console.WriteLine($"    MS (Multiplier Source): {p0_MS} ({(p0_MS == 0 ? "CCB constant" : p0_MS == 1 ? "AMV from decoder" : p0_MS == 2 ? "Both PMV+PDV from color" : "PMV from color")})");
                if (p0_MS == 0) Console.WriteLine($"    MF (Multiplier Factor): {p0_MF + 1}");
                Console.WriteLine($"    DF (Divider Factor): {(p0_DF == 0 ? 16 : p0_DF == 1 ? 2 : p0_DF == 2 ? 4 : 8)}");
                Console.WriteLine($"    2S (Secondary Source): {p0_2S} ({(p0_2S == 0 ? "Zero" : p0_2S == 1 ? "CCB AV value" : p0_2S == 2 ? "Frame Buffer" : "Cel Pixel")})");
                Console.WriteLine($"    AV (Secondary Value): {p0_AV}");
                Console.WriteLine($"    2D (Secondary Divider): {(p0_2D ? 2 : 1)}");
                Console.WriteLine($"\n  P-mode 1 (bits 16-31): 0x{pixcP1:X4}");
                Console.WriteLine($"    1S: {(p1_1S ? "Frame Buffer" : "Cel Pixel")}, MS: {p1_MS}, MF: {p1_MF + 1}, DF: {(p1_DF == 0 ? 16 : p1_DF == 1 ? 2 : p1_DF == 2 ? 4 : 8)}, 2S: {p1_2S}, AV: {p1_AV}, 2D: {(p1_2D ? 2 : 1)}");
                
                if (useFrameBufferBlending)
                {
                    Console.WriteLine($"\n  Frame Buffer Blending: ENABLED (transparent background used for secondary source)");
                }
                else
                {
                    Console.WriteLine($"\n  Frame Buffer Blending: DISABLED (use default AMV scaling)");
                }
            }

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
            // Check if we have transparency or AMV data that needs special handling
            else if (celOutput.TransparencyMask != null || celOutput.AlternateMultiplyValues != null)
            {
                // Create RGBA image with transparency and/or AMV support
                image = new Image<Rgba32>(celOutput.Width, celOutput.Height);
                int bytesPerPixel = (celOutput.BitsPerPixel + 7) / 8;

                // Determine if this is uncoded (direct RGB) or coded (palette indices)
                bool isUncoded = (celOutput.BitsPerPixel == 16 || celOutput.BitsPerPixel == 8) && palette == null;

                for (int y = 0; y < celOutput.Height; y++)
                {
                    for (int x = 0; x < celOutput.Width; x++)
                    {
                        int pixelIndex = y * celOutput.Width + x;

                        // Check transparency first
                        if (celOutput.TransparencyMask != null && celOutput.TransparencyMask[pixelIndex])
                        {
                            // Transparent pixel - set alpha to 0
                            image[x, y] = new Rgba32(0, 0, 0, 0);
                            continue;
                        }

                        Rgba32 baseColor;

                        if (isUncoded)
                        {
                            // UNCODED FORMAT: Convert RGB data directly (no palette lookup)
                            if (celOutput.BitsPerPixel == 16)
                            {
                                // 16bpp uncoded: RGB555 format (5R, 5G, 5B, 1 control bit)
                                int byteOffset = pixelIndex * 2;
                                int pixelValue = celOutput.PixelData[byteOffset] | (celOutput.PixelData[byteOffset + 1] << 8);
                                
                                int r = ((pixelValue >> 10) & 0x1F) << 3; // Scale 5 bits to 8 bits
                                int g = ((pixelValue >> 5) & 0x1F) << 3;
                                int b = (pixelValue & 0x1F) << 3;
                                
                                baseColor = new Rgba32((byte)r, (byte)g, (byte)b, 255);
                            }
                            else // 8bpp uncoded
                            {
                                // 8bpp uncoded: RGB332 format (3R, 3G, 2B)
                                byte pixelValue = celOutput.PixelData[pixelIndex];
                                
                                int r = ((pixelValue >> 5) & 0x07) * 255 / 7; // Scale 3 bits to 8 bits
                                int g = ((pixelValue >> 2) & 0x07) * 255 / 7;
                                int b = (pixelValue & 0x03) * 255 / 3; // Scale 2 bits to 8 bits
                                
                                baseColor = new Rgba32((byte)r, (byte)g, (byte)b, 255);
                            }
                        }
                        else
                        {
                            // CODED FORMAT: Use palette lookup
                            if (palette == null)
                            {
                                throw new ArgumentNullException(nameof(palette), "Palette is required for coded CEL images with transparency or AMV data");
                            }

                            // Read palette index based on bits per pixel
                            int plutIndex;
                            if (celOutput.BitsPerPixel == 16)
                            {
                                // For 16bpp coded, read 2 bytes as a 16-bit index
                                int byteOffset = pixelIndex * 2;
                                plutIndex = celOutput.PixelData[byteOffset] | (celOutput.PixelData[byteOffset + 1] << 8);
                            }
                            else
                            {
                                // For 8bpp and lower, read single byte (mask to 5 bits for PLUT index)
                                plutIndex = celOutput.PixelData[pixelIndex] & 0x1F;
                            }

                            baseColor = plutIndex < palette.Count ? palette[plutIndex] : palette[plutIndex % palette.Count];
                            
                            // Check if PLUT entry is 000 (black RGB) - this indicates transparency
                            // unless BGND flag is set (bit 5 of CCB FLAGS)
                            bool bgndFlag = (ccbFlags & 0x00000020) != 0;
                            if (!bgndFlag && baseColor.R == 0 && baseColor.G == 0 && baseColor.B == 0)
                            {
                                // PLUT entry is 000 and BGND is not set - treat as transparent
                                image[x, y] = new Rgba32(0, 0, 0, 0);
                                continue;
                            }
                        }

                        // Apply Pixel Processor operations (AMV and PIXC)
                        if (celOutput.AlternateMultiplyValues != null)
                        {
                            byte amv = celOutput.AlternateMultiplyValues[pixelIndex];
                            
                            if (pixcProvided && useFrameBufferBlending)
                            {
                                // Use P-mode 0 settings (most common for coded 8bpp with AMV)
                                // Apply full 3DO pixel processor pipeline
                                
                                // Primary Source - the cel pixel color
                                Rgba32 primarySource = baseColor;
                                
                                // Primary Multiplier Value (PMV)
                                float pmv = 1.0f;
                                if (p0_MS == 0)
                                {
                                    // Use CCB constant (MF + 1)
                                    pmv = p0_MF + 1;
                                }
                                else if (p0_MS == 1)
                                {
                                    // Use AMV from decoder (0-7 maps to 1-8)
                                    pmv = amv + 1;
                                }
                                else if (p0_MS == 3)
                                {
                                    // Use color value from decoder as PMV (top 3 bits of RGB)
                                    // For simplicity, average the R, G, B top bits
                                    float colorPmv = ((baseColor.R >> 5) + (baseColor.G >> 5) + (baseColor.B >> 5)) / 3.0f;
                                    pmv = colorPmv + 1; // 0-7 → 1-8
                                }
                                
                                // Primary Divider Value (PDV)
                                float pdv = p0_DF == 0 ? 16.0f : p0_DF == 1 ? 2.0f : p0_DF == 2 ? 4.0f : 8.0f;
                                
                                // Scale primary source
                                float primaryScale = pmv / pdv;
                                Rgba32 scaledPrimary = new Rgba32(
                                    (byte)Math.Min(255, baseColor.R * primaryScale),
                                    (byte)Math.Min(255, baseColor.G * primaryScale),
                                    (byte)Math.Min(255, baseColor.B * primaryScale),
                                    baseColor.A
                                );
                                
                                // Secondary Source
                                Rgba32 secondarySource = new Rgba32(0, 0, 0, 0);
                                if (p0_2S == 1)
                                {
                                    // CCB AV value (constant grayscale)
                                    byte avValue = (byte)((p0_AV * 255) / 31);
                                    secondarySource = new Rgba32(avValue, avValue, avValue, 255);
                                }
                                else if (p0_2S == 2)
                                {
                                    // Frame buffer pixel (transparent = black for blending)
                                    secondarySource = new Rgba32(0, 0, 0, 0);
                                }
                                else if (p0_2S == 3)
                                {
                                    // Cel pixel itself
                                    secondarySource = baseColor;
                                }
                                
                                // Secondary divider
                                float sdv = p0_2D ? 2.0f : 1.0f;
                                Rgba32 scaledSecondary = new Rgba32(
                                    (byte)Math.Min(255, secondarySource.R / sdv),
                                    (byte)Math.Min(255, secondarySource.G / sdv),
                                    (byte)Math.Min(255, secondarySource.B / sdv),
                                    secondarySource.A
                                );
                                
                                // Final math stage - add primary and secondary (most common for fire effects)
                                baseColor = new Rgba32(
                                    (byte)Math.Min(255, scaledPrimary.R + scaledSecondary.R),
                                    (byte)Math.Min(255, scaledPrimary.G + scaledSecondary.G),
                                    (byte)Math.Min(255, scaledPrimary.B + scaledSecondary.B),
                                    255 // Keep opaque
                                );
                            }
                            else
                            {
                                // Simple AMV scaling (legacy behavior when PIXC not provided)
                                // AMV provides 8 brightness levels (0-7)
                                // Map to 1-8 range for multiplication, then divide by 8 for normalization
                                // This gives: 0→0.125, 1→0.25, 2→0.375, ..., 7→1.0
                                float multiplier = (amv + 1) / 8.0f;

                                baseColor = new Rgba32(
                                    (byte)Math.Min(255, baseColor.R * multiplier),
                                    (byte)Math.Min(255, baseColor.G * multiplier),
                                    (byte)Math.Min(255, baseColor.B * multiplier),
                                    baseColor.A
                                );
                            }
                        }

                        image[x, y] = baseColor;
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

                // Manual rendering to handle PLUT 000 transparency
                image = new Image<Rgba32>(celOutput.Width, celOutput.Height);
                bool bgndFlag = (ccbFlags & 0x00000020) != 0; // BGND flag - bit 5
                
                for (int y = 0; y < celOutput.Height; y++)
                {
                    for (int x = 0; x < celOutput.Width; x++)
                    {
                        int pixelIndex = y * celOutput.Width + x;
                        int plutIndex;
                        
                        if (celOutput.BitsPerPixel == 8)
                        {
                            // For 8bpp coded: Extract lower 5 bits (PLUT index)
                            plutIndex = celOutput.PixelData[pixelIndex] & 0x1F;
                        }
                        else if (celOutput.BitsPerPixel < 8)
                        {
                            // For sub-byte formats, data is already clean palette indices
                            plutIndex = celOutput.PixelData[pixelIndex];
                        }
                        else // 16bpp
                        {
                            int byteOffset = pixelIndex * 2;
                            plutIndex = celOutput.PixelData[byteOffset] | (celOutput.PixelData[byteOffset + 1] << 8);
                        }
                        
                        // Get color from palette
                        Color paletteColor = plutIndex < palette.Count ? palette[plutIndex] : palette[plutIndex % palette.Count];
                        Rgba32 rgba = paletteColor.ToPixel<Rgba32>();
                        
                        // Check if PLUT entry is 000 (black RGB) - this indicates transparency
                        // unless BGND flag is set
                        if (!bgndFlag && rgba.R == 0 && rgba.G == 0 && rgba.B == 0)
                        {
                            // PLUT entry is 000 and BGND is not set - treat as transparent
                            image[x, y] = new Rgba32(0, 0, 0, 0);
                        }
                        else
                        {
                            // Opaque pixel
                            image[x, y] = rgba;
                        }
                    }
                }
            }

            image.SaveAsPng(outputPath);
        }

        /// <summary>
        /// Unpacks an ANIM file containing multiple CEL frames and saves them as separate PNG files.
        /// ANIM files contain an ANIM chunk followed by multiple CCB+PDAT chunks for each frame.
        /// </summary>
        /// <param name="animFile">Path to the ANIM file</param>
        /// <param name="outputDir">Directory where to save the frame images</param>
        /// <param name="verbose">If true, displays diagnostic information</param>
        /// <param name="bitsPerPixel">Optional: Override auto-detected bits per pixel. If 0, auto-detect.</param>
        /// <returns>Number of frames extracted, or -1 on error</returns>
        public static int UnpackAnimFile(string animFile, string outputDir, bool verbose = false, int bitsPerPixel = 0, bool noLoopRecords = false)
        {
            if (!File.Exists(animFile))
            {
                Console.WriteLine($"ANIM file not found: {animFile}");
                return -1;
            }

            var data = File.ReadAllBytes(animFile);
            if (data.Length < 16)
            {
                Console.WriteLine($"File too small to be valid ANIM: {animFile}");
                return -1;
            }

            // Create output directory
            Directory.CreateDirectory(outputDir);

            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            // Parse ANIM chunk header
            var magic = new string(reader.ReadChars(4));
            if (magic != "ANIM")
            {
                Console.WriteLine($"Invalid ANIM magic in {Path.GetFileName(animFile)}: expected 'ANIM', got '{magic}'");
                return -1;
            }

            var chunkSize = ReadBigEndianInt32(data, 4);
            var version = ReadBigEndianInt32(data, 8);
            var animType = ReadBigEndianInt32(data, 12);
            var numFrames = ReadBigEndianInt32(data, 16);
            var frameRate = ReadBigEndianInt32(data, 20);
            var startFrame = ReadBigEndianInt32(data, 24);
            var numLoops = ReadBigEndianInt32(data, 28);

            if (verbose)
            {
                Console.WriteLine($"\n========================================");
                Console.WriteLine($"ANIM File: {Path.GetFileName(animFile)}");
                Console.WriteLine($"========================================");
                Console.WriteLine($"ANIM Chunk:");
                Console.WriteLine($"  Version: {version}");
                Console.WriteLine($"  Animation Type: {animType} ({(animType == 0 ? "multi-CCB" : "single CCB")})");
                Console.WriteLine($"  Number of Frames: {numFrames}");
                Console.WriteLine($"  Frame Rate: {frameRate} (1/60th sec per frame = {60.0 / frameRate:F2} fps)");
                Console.WriteLine($"  Start Frame: {startFrame}");
                Console.WriteLine($"  Number of Loops: {numLoops}");
            }

            // Skip loop data (each loop is 16 bytes: start, end, repeatCount, repeatDelay)
            // Note: AnimChunk structure has loop[1] meaning at least 1 loop record exists
            // even when numLoops is 0, so we need to read max(1, numLoops) records
            int loopRecordsToSkip = Math.Max(1, numLoops);
            if (!noLoopRecords)
            {
                stream.Seek(32 + (loopRecordsToSkip * 16), SeekOrigin.Begin);
            } else
            {
                stream.Seek(32, SeekOrigin.Begin);
            }
            
            if (verbose && numLoops == 0)
            {
                Console.WriteLine($"  Note: Reading 1 loop record even though numLoops=0 (per 3DO spec)");
            }

            // Now parse CEL frames (CCB + PDAT chunks)
            int frameIndex = 0;
            List<Color>? currentPalette = null;
            byte[]? currentCCB = null;
            byte[]? sharedCCB = null; // For animType==1 (single CCB for all frames)
            int framesExtracted = 0;

            while (stream.Position < data.Length - 8)
            {
                var chunkMagic = new string(reader.ReadChars(4));
                var chunkDataSize = ReadBigEndianInt32(data, (int)stream.Position);
                stream.Seek(4, SeekOrigin.Current); // Skip size field we just read

                if (verbose)
                {
                    Console.WriteLine($"\nChunk: {chunkMagic}, Size: {chunkDataSize}");
                }

                if (chunkMagic == "CCB ")
                {
                    // Store the CCB data for pairing with the next PDAT
                    long ccbStart = stream.Position - 8; // Include magic + size
                    currentCCB = new byte[chunkDataSize];
                    stream.Seek(ccbStart, SeekOrigin.Begin);
                    stream.Read(currentCCB, 0, chunkDataSize);

                    // If this is a single CCB animation, save it for reuse
                    if (animType == 1 && sharedCCB == null)
                    {
                        sharedCCB = currentCCB;
                        if (verbose)
                        {
                            Console.WriteLine($"  Stored shared CCB for all frames (animType=1)");
                        }
                    }

                    if (verbose)
                    {
                        // Extract CCB header fields for detailed logging
                        // Note: CCB chunk size is 80 bytes total (including 8-byte header)
                        // Actual CCB data is 72 bytes, offsets are relative to start of chunk (after magic+size)
                        uint ccbFlags = (uint)ReadBigEndianInt32(currentCCB, 8);
                        int width = ReadBigEndianInt32(currentCCB, 72);
                        int height = ReadBigEndianInt32(currentCCB, 76);

                        Console.WriteLine($"  CCB for frame {frameIndex}:");
                        Console.WriteLine($"    Dimensions: {width}x{height}");
                        Console.WriteLine($"    FLAGS: 0x{ccbFlags:X8}");
                        
                        // Decode important flags
                        bool skipFlag = (ccbFlags & 0x80000000) != 0;
                        bool lastFlag = (ccbFlags & 0x40000000) != 0;
                        bool ccbpreFlag = (ccbFlags & 0x00400000) != 0;
                        bool packedFlag = (ccbFlags & 0x00000200) != 0;
                        bool bgndFlag = (ccbFlags & 0x00000020) != 0;
                        bool noblkFlag = (ccbFlags & 0x00000010) != 0;
                        int plutaValue = (int)((ccbFlags >> 1) & 0x7);
                        
                        Console.WriteLine($"    SKIP={skipFlag}, LAST={lastFlag}, CCBPRE={ccbpreFlag}, PACKED={packedFlag}");
                        Console.WriteLine($"    BGND={bgndFlag}, NOBLK={noblkFlag}, PLUTA={plutaValue}");
                    }
                    else
                    {
                        // Non-verbose: just show dimensions
                        int width = ReadBigEndianInt32(currentCCB, 72);
                        int height = ReadBigEndianInt32(currentCCB, 76);
                        Console.WriteLine($"  CCB for frame {frameIndex}: {width}x{height}");
                    }
                }
                else if (chunkMagic == "PLUT")
                {
                    // Read palette data
                    int dataSize = chunkDataSize - 0xc; // Subtract header
                    reader.ReadInt32();
                    byte[] plutData = reader.ReadBytes(dataSize);
                    currentPalette = ExtractPaletteFromPLUT(plutData, verbose);

                    if (verbose)
                    {
                        Console.WriteLine($"  Loaded palette with {currentPalette.Count} colors");
                    }
                }
                else if (chunkMagic == "PDAT")
                {
                    // For animType==1 (single CCB), reuse the shared CCB for all PDAT chunks
                    byte[]? ccbToUse = null;
                    if (animType == 1 && sharedCCB != null)
                    {
                        ccbToUse = sharedCCB;
                    }
                    else
                    {
                        ccbToUse = currentCCB;
                    }
                    
                    // This PDAT belongs to the previous CCB
                    if (ccbToUse == null)
                    {
                        Console.WriteLine($"Warning: PDAT chunk at offset {stream.Position - 8} has no preceding CCB");
                        int dataSize = chunkDataSize - 8;
                        stream.Seek(dataSize, SeekOrigin.Current);
                        continue;
                    }

                    // Read the PDAT chunk
                    long pdatStart = stream.Position - 8;
                    byte[] pdatChunk = new byte[chunkDataSize];
                    stream.Seek(pdatStart, SeekOrigin.Begin);
                    stream.Read(pdatChunk, 0, chunkDataSize);

                    // Combine CCB + PDAT into a single buffer for processing
                    byte[] celData = new byte[ccbToUse.Length + pdatChunk.Length];
                    Array.Copy(ccbToUse, 0, celData, 0, ccbToUse.Length);
                    Array.Copy(pdatChunk, 0, celData, ccbToUse.Length, pdatChunk.Length);

                    // Process this CEL frame
                    try
                    {
                        // Create a temporary stream with CCB + PDAT data
                        var celImageData = UnpackCelFile_FromBytes(celData, verbose: false, bitsPerPixel: bitsPerPixel);
                        
                        if (celImageData != null)
                        {
                            // Determine output filename
                            string frameFileName = $"{Path.GetFileNameWithoutExtension(animFile)}_frame{frameIndex:D4}.png";
                            string frameOutputPath = Path.Combine(outputDir, frameFileName);

                            // Use palette from CEL data if available, otherwise use current palette
                            var paletteToUse = celImageData.Palette ?? currentPalette;

                            // Extract CCB flags for transparency handling
                            uint ccbFlags = (uint)ReadBigEndianInt32(ccbToUse, 8);

                            // Extract PIXC word (CCB word #13) if LDPIXC flag is set (bit 24)
                            uint pixc = 0;
                            bool ldpixc = (ccbFlags & 0x01000000) != 0;
                            if (ldpixc && ccbToUse.Length >= 60) // Need at least 60 bytes for PIXC at offset 52
                            {
                                pixc = (uint)ReadBigEndianInt32(ccbToUse, 52); // PIXC is CCB word #13 (offset 52 from CCB start)
                            }

                            // Log transparency status
                            if (verbose)
                            {
                                bool hasTransparencyMask = celImageData.TransparencyMask != null;
                                int transparentPixels = 0;
                                if (hasTransparencyMask && celImageData.TransparencyMask != null)
                                {
                                    transparentPixels = celImageData.TransparencyMask.Count(t => t);
                                }
                                int totalPixels = celImageData.Width * celImageData.Height;
                                
                                Console.WriteLine($"  Frame {frameIndex} transparency: Mask={hasTransparencyMask}, " +
                                    $"Transparent pixels={transparentPixels}/{totalPixels} ({100.0 * transparentPixels / totalPixels:F1}%)");
                            }

                            // Save the frame with CCB flags and PIXC for proper transparency handling and pixel processor simulation
                            SaveCelImage(celImageData, frameOutputPath, paletteToUse, ccbFlags, pixc, useFrameBufferBlending: false, verbose);
                            
                            if (verbose)
                            {
                                Console.WriteLine($"  ✓ Saved frame {frameIndex}: {frameFileName}");
                            }
                            
                            framesExtracted++;
                        }
                        else
                        {
                            Console.WriteLine($"  ✗ Failed to unpack frame {frameIndex}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ✗ Error processing frame {frameIndex}: {ex.Message}");
                    }

                    frameIndex++;
                    
                    // Only reset CCB for multi-CCB animations (animType==0)
                    if (animType == 0)
                    {
                        currentCCB = null;
                    }
                }
                else
                {
                    // Unknown chunk - skip it
                    int dataSize = chunkDataSize - 8;
                    if (dataSize > 0 && dataSize < data.Length)
                    {
                        stream.Seek(dataSize, SeekOrigin.Current);
                    }
                    else
                    {
                        break;
                    }
                }

                // Align to 4-byte boundary
                while (stream.Position % 4 != 0 && stream.Position < stream.Length)
                {
                    stream.ReadByte();
                }
            }

            if (verbose || framesExtracted > 0)
            {
                Console.WriteLine($"\n✓ Extracted {framesExtracted} of {frameIndex} frames from ANIM file");
            }
            
            return framesExtracted;
        }

        /// <summary>
        /// Reorders pixels based on the pixelOrder field from IMAG header.
        /// The pixelOrder describes how pixels are STORED in the file.
        /// We need to read them in that order and place them in standard row-major order.
        /// 
        /// Note: Documentation uses (row,column) notation which is (y,x).
        /// pixelOrder 0: Standard row-major - pixels stored left-to-right, top-to-bottom
        /// pixelOrder 1: Sherrie LRform - within each 2x2 block, pixels stored as: (0,0), (0,1), (1,0), (1,1)
        /// pixelOrder 2: UGO LRform - within each 2x2 block, pixels stored as: (0,1), (0,0), (1,1), (1,0)
        /// </summary>
        /// <param name="source">Source image data (pixels in file order)</param>
        /// <param name="pixelOrder">Pixel order mode (0=standard, 1=Sherrie, 2=UGO)</param>
        /// <param name="verbose">Enable verbose logging</param>
        /// <returns>Reordered image data in standard row-major layout</returns>
        private static CelImageData ReorderPixels(CelImageData source, byte pixelOrder, bool verbose)
        {
            if (pixelOrder == 0)
            {
                return source; // Already in standard order
            }

            if (verbose)
            {
                Console.WriteLine($"\nReordering pixels from mode {pixelOrder} ({(pixelOrder == 1 ? "Sherrie LRform" : "UGO LRform")}) to standard row-major");
            }

            int bytesPerPixel = (source.BitsPerPixel + 7) / 8;
            byte[] reorderedPixelData = new byte[source.Width * source.Height * bytesPerPixel];
            bool[]? reorderedTransparencyMask = source.TransparencyMask != null ? new bool[source.Width * source.Height] : null;
            byte[]? reorderedAmvData = source.AlternateMultiplyValues != null ? new byte[source.Width * source.Height] : null;

            int srcPixelIndex = 0;

            // Process image in 2x2 blocks
            for (int blockY = 0; blockY < source.Height; blockY += 2)
            {
                for (int blockX = 0; blockX < source.Width; blockX += 2)
                {
                    // Define where each of the 4 sequential pixels in the file should go
                    // Using (row, column) notation from the docs, which is (y, x)
                    int[] destYOffsets, destXOffsets;
                    
                    if (pixelOrder == 1)
                    {
                        // Sherrie LRform: file has pixels in order for positions (row,col): (0,0), (0,1), (1,0), (1,1)
                        // Interpreting as: top-left, bottom-left, top-right, bottom-right (column-major within block)
                        destYOffsets = new int[] { 0, 1, 0, 1 };
                        destXOffsets = new int[] { 0, 0, 1, 1 };
                    }
                    else // pixelOrder == 2
                    {
                        // UGO LRform: file has pixels in order for positions (row,col): (0,1), (0,0), (1,1), (1,0)
                        // Interpreting as: top-right, bottom-right, top-left, bottom-left
                        destYOffsets = new int[] { 0, 1, 0, 1 };
                        destXOffsets = new int[] { 1, 1, 0, 0 };
                    }

                    // Read 4 sequential pixels from source and place them according to the pattern
                    for (int i = 0; i < 4; i++)
                    {
                        int destX = blockX + destXOffsets[i];
                        int destY = blockY + destYOffsets[i];

                        // Bounds check
                        if (destX >= source.Width || destY >= source.Height)
                        {
                            srcPixelIndex++; // Skip this pixel in source
                            continue;
                        }

                        int destPixelIndex = destY * source.Width + destX;

                        // Copy pixel data from sequential source position to calculated destination
                        for (int b = 0; b < bytesPerPixel; b++)
                        {
                            reorderedPixelData[destPixelIndex * bytesPerPixel + b] = 
                                source.PixelData[srcPixelIndex * bytesPerPixel + b];
                        }

                        // Copy transparency mask if present
                        if (reorderedTransparencyMask != null && source.TransparencyMask != null)
                        {
                            reorderedTransparencyMask[destPixelIndex] = source.TransparencyMask[srcPixelIndex];
                        }

                        // Copy AMV data if present
                        if (reorderedAmvData != null && source.AlternateMultiplyValues != null)
                        {
                            reorderedAmvData[destPixelIndex] = source.AlternateMultiplyValues[srcPixelIndex];
                        }

                        srcPixelIndex++;
                    }
                }
            }

            return new CelImageData
            {
                Width = source.Width,
                Height = source.Height,
                BitsPerPixel = source.BitsPerPixel,
                PixelData = reorderedPixelData,
                TransparencyMask = reorderedTransparencyMask,
                AlternateMultiplyValues = reorderedAmvData,
                Palette = source.Palette
            };
        }

        /// <summary>
        /// Scales image data for hvformat modes (0554h, 0554v, v554h).
        /// These formats store pixel data at reduced dimensions and need to be scaled up.
        /// </summary>
        /// <param name="source">Source image data</param>
        /// <param name="targetWidth">Target width after scaling</param>
        /// <param name="targetHeight">Target height after scaling</param>
        /// <param name="hvFormat">HV format: 1=0554h (horiz), 2=0554v (vert), 3=v554h</param>
        /// <param name="verbose">Enable verbose logging</param>
        /// <returns>Scaled image data</returns>
        private static CelImageData ScaleImageData(CelImageData source, int targetWidth, int targetHeight, byte hvFormat, bool verbose)
        {
            if (verbose)
            {
                Console.WriteLine($"\nScaling image from {source.Width}x{source.Height} to {targetWidth}x{targetHeight} (hvFormat={hvFormat})");
            }

            int bytesPerPixel = (source.BitsPerPixel + 7) / 8;
            byte[] scaledPixelData = new byte[targetWidth * targetHeight * bytesPerPixel];
            bool[]? scaledTransparencyMask = source.TransparencyMask != null ? new bool[targetWidth * targetHeight] : null;
            byte[]? scaledAmvData = source.AlternateMultiplyValues != null ? new byte[targetWidth * targetHeight] : null;

            // Simple nearest-neighbor scaling
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    // Map target coordinates back to source coordinates
                    int srcX = (x * source.Width) / targetWidth;
                    int srcY = (y * source.Height) / targetHeight;

                    // Ensure source coordinates are within bounds
                    srcX = Math.Min(srcX, source.Width - 1);
                    srcY = Math.Min(srcY, source.Height - 1);

                    int srcPixelIndex = srcY * source.Width + srcX;
                    int dstPixelIndex = y * targetWidth + x;

                    // Copy pixel data
                    for (int b = 0; b < bytesPerPixel; b++)
                    {
                        scaledPixelData[dstPixelIndex * bytesPerPixel + b] = source.PixelData[srcPixelIndex * bytesPerPixel + b];
                    }

                    // Copy transparency mask if present
                    if (scaledTransparencyMask != null && source.TransparencyMask != null)
                    {
                        scaledTransparencyMask[dstPixelIndex] = source.TransparencyMask[srcPixelIndex];
                    }

                    // Copy AMV data if present
                    if (scaledAmvData != null && source.AlternateMultiplyValues != null)
                    {
                        scaledAmvData[dstPixelIndex] = source.AlternateMultiplyValues[srcPixelIndex];
                    }
                }
            }

            return new CelImageData
            {
                Width = targetWidth,
                Height = targetHeight,
                BitsPerPixel = source.BitsPerPixel,
                PixelData = scaledPixelData,
                TransparencyMask = scaledTransparencyMask,
                AlternateMultiplyValues = scaledAmvData,
                Palette = source.Palette
            };
        }

        /// <summary>
        /// Unpacks 3DO IMAG (Image Control) format files and automatically unpacks the pixel data.
        /// IMAG files have a simpler header structure than CCB and are used for static images.
        /// Structure: IMAG chunk + PLUT chunk (optional) + PDAT chunk(s)
        /// </summary>
        /// <param name="imagFile">Full path to the IMAG file</param>
        /// <param name="verbose">If true, displays IMAG header information</param>
        /// <returns>Unpacked image data with dimensions and pixel data, or null if failed</returns>
        public static CelImageData? UnpackImagFile(string imagFile, bool verbose = false)
        {
            if (!File.Exists(imagFile))
            {
                Console.WriteLine($"File not found: {imagFile}");
                return null;
            }

            byte[] data = File.ReadAllBytes(imagFile);
            return UnpackImagFile_FromBytes(data, verbose);
        }

        /// <summary>
        /// Unpacks 3DO IMAG (Image Control) format data from a byte array.
        /// This is useful for processing IMAG data embedded in other file formats.
        /// </summary>
        /// <param name="data">IMAG data bytes (must start with IMAG chunk)</param>
        /// <param name="verbose">If true, displays IMAG header information</param>
        /// <returns>Unpacked image data with dimensions and pixel data, or null if failed</returns>
        public static CelImageData? UnpackImagFile_FromBytes(byte[] data, bool verbose = false)
        {
            if (data.Length < 0x18)
            {
                Console.WriteLine($"Data too small to contain an IMAG header (minimum 24 bytes, got {data.Length})");
                return null;
            }

            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            List<Color>? palette = null;
            byte[]? pixelData = null;
            int width = 0, height = 0, bytesPerRow = 0;
            byte bitsPerPixel = 0, numComponents = 0, numPlanes = 0;
            byte colorSpace = 0, compType = 0, hvFormat = 0, pixelOrder = 0, version = 0;

            // Parse all chunks until end of file
            while (stream.Position < data.Length - 8)
            {
                long chunkStart = stream.Position;
                byte[] magic = reader.ReadBytes(4);

                if (magic.Length < 4) break;

                string magicStr = System.Text.Encoding.ASCII.GetString(magic);
                uint chunkSize = ReadBigEndianUInt32(data, (int)stream.Position);
                stream.Position += 4;

                if (verbose)
                {
                    Console.WriteLine($"Found chunk '{magicStr}' at offset 0x{chunkStart:X}, size: {chunkSize} bytes");
                }

                if (magicStr == "IMAG")
                {
                    // Process IMAG chunk - Image Control structure
                    if (chunkSize < 0x18)
                    {
                        Console.WriteLine($"IMAG chunk too small: {chunkSize} bytes (expected at least 24)");
                        return null;
                    }

                    long imagDataStart = stream.Position;

                    // Read IMAG header fields (all big-endian)
                    width = ReadBigEndianInt32(data, (int)stream.Position);
                    stream.Position += 4;
                    height = ReadBigEndianInt32(data, (int)stream.Position);
                    stream.Position += 4;
                    bytesPerRow = ReadBigEndianInt32(data, (int)stream.Position);
                    stream.Position += 4;

                    bitsPerPixel = reader.ReadByte();
                    numComponents = reader.ReadByte();
                    numPlanes = reader.ReadByte();
                    colorSpace = reader.ReadByte();
                    compType = reader.ReadByte();
                    hvFormat = reader.ReadByte();
                    pixelOrder = reader.ReadByte();
                    version = reader.ReadByte();

                    // Apply hvformat adjustments
                    // hvformat: 0 => 0555, 1 => 0554h (horizontal double), 2 => 0554v (vertical double), 3 => v554h
                    // For 0554v (vertical), the image data is stored at half height and should be doubled
                    // For 0554h (horizontal), the image data is stored at half width and should be doubled
                    int actualWidth = width;
                    int actualHeight = height;
                    
                    if (hvFormat == 1) // 0554h - horizontal double
                    {
                        actualWidth = width * 2;
                    }
                    else if (hvFormat == 2) // 0554v - vertical double
                    {
                        actualHeight = height * 2;
                    }
                    else if (hvFormat == 3) // v554h - both doubled?
                    {
                        actualWidth = width * 2;
                        actualHeight = height * 2;
                    }

                    if (verbose)
                    {
                        Console.WriteLine($"\n=== IMAG Header ===");
                        Console.WriteLine($"Width: {width} (actual: {actualWidth})");
                        Console.WriteLine($"Height: {height} (actual: {actualHeight})");
                        Console.WriteLine($"Bytes per row: {bytesPerRow}");
                        Console.WriteLine($"Bits per pixel: {bitsPerPixel}");
                        Console.WriteLine($"Num components: {numComponents} ({(numComponents == 1 ? "coded/indexed" : "RGB/YUV")})");
                        Console.WriteLine($"Num planes: {numPlanes} ({(numPlanes == 1 ? "chunky" : "planar")})");
                        Console.WriteLine($"Color space: {colorSpace} ({(colorSpace == 0 ? "RGB" : "YCrCb")})");
                        Console.WriteLine($"Compression type: {compType} ({(compType == 0 ? "uncompressed" : compType == 1 ? "Cel bit packed" : "unknown")})");
                        Console.WriteLine($"HV format: {hvFormat} ({(hvFormat == 0 ? "0555" : hvFormat == 1 ? "0554h (horiz double)" : hvFormat == 2 ? "0554v (vert double)" : "v554h")})");
                        Console.WriteLine($"Pixel order: {pixelOrder} ({(pixelOrder == 0 ? "standard row-major" : pixelOrder == 1 ? "Sherrie LRform (2x2 blocks)" : "UGO LRform (2x2 blocks)")})");
                        Console.WriteLine($"Version: {version}");
                        Console.WriteLine($"===================\n");
                    }

                    // Update width and height to actual values
                    width = actualWidth;
                    height = actualHeight;

                    // Skip past the entire IMAG chunk
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
                }
                else if (magicStr == "PDAT")
                {
                    // Process PDAT chunk - extract pixel data
                    uint pdatDataSize = chunkSize - 8; // Subtract magic (4) + size (4)
                    pixelData = reader.ReadBytes((int)pdatDataSize);

                    if (verbose)
                    {
                        Console.WriteLine($"PDAT: Extracted {pixelData.Length} bytes of pixel data");
                    }
                }
                else
                {
                    // Unknown chunk - skip using its size
                    uint skipSize = chunkSize - 8;
                    if (stream.Position + skipSize > data.Length)
                    {
                        Console.WriteLine($"Chunk '{magicStr}' size exceeds file bounds, stopping parsing");
                        break;
                    }
                    stream.Position += skipSize;

                    if (verbose)
                    {
                        Console.WriteLine($"Skipping unknown chunk '{magicStr}' ({skipSize} bytes)");
                    }
                }
            }

            // Validate we have the necessary data
            if (width <= 0 || height <= 0)
            {
                Console.WriteLine($"No valid IMAG chunk found or invalid dimensions (width={width}, height={height})");
                return null;
            }

            if (pixelData == null)
            {
                Console.WriteLine("No pixel data found in file");
                return null;
            }

            try
            {
                CelImageData result;

                // Determine format from IMAG header
                bool isCoded = (numComponents == 1); // 1 = color index, 3 = RGB
                bool isPacked = (compType == 1);     // 0 = uncompressed, 1 = Cel bit packed

                // For hvformat scaling, we need to unpack at the stored dimensions first
                int storedWidth = width;
                int storedHeight = height;
                
                // Adjust back to stored dimensions for unpacking
                if (hvFormat == 1) // 0554h - horizontal double
                {
                    storedWidth = width / 2;
                }
                else if (hvFormat == 2) // 0554v - vertical double
                {
                    storedHeight = height / 2;
                }
                else if (hvFormat == 3) // v554h - both doubled
                {
                    storedWidth = width / 2;
                    storedHeight = height / 2;
                }

                if (verbose)
                {
                    Console.WriteLine($"\nDetected format: {(isCoded ? "Coded" : "Uncoded")} {(isPacked ? "Packed" : "Unpacked")}");
                    if (hvFormat != 0)
                    {
                        Console.WriteLine($"Unpacking at stored dimensions: {storedWidth}x{storedHeight}");
                        Console.WriteLine($"Will scale to final dimensions: {width}x{height}");
                    }
                }

                // Route to the appropriate unpacking method based on format
                // Use STORED dimensions for unpacking
                if (isCoded && isPacked)
                {
                    // Coded + Packed: Use coded packed method
                    result = UnpackCodedPackedWithDimensions(pixelData, storedWidth, storedHeight, bitsPerPixel, verbose: verbose);
                }
                else if (isCoded && !isPacked)
                {
                    // Coded + Unpacked: Use coded unpacked method
                    result = UnpackCodedUnpackedCelData(pixelData, storedWidth, storedHeight, bitsPerPixel);
                }
                else if (!isCoded && isPacked)
                {
                    // Uncoded + Packed: Use uncoded packed method
                    result = UnpackUncodedPackedWithDimensions(pixelData, storedWidth, storedHeight, bitsPerPixel, verbose: verbose);
                }
                else
                {
                    // Uncoded + Unpacked: Use uncoded unpacked method
                    result = UnpackUncodedUnpackedWithDimensions(pixelData, storedWidth, storedHeight, bitsPerPixel, verbose: verbose);
                }

                // Apply pixel reordering if needed (before scaling)
                if (pixelOrder != 0)
                {
                    result = ReorderPixels(result, pixelOrder, verbose);
                }

                // Apply hvformat scaling if needed
                if (hvFormat != 0 && (width != storedWidth || height != storedHeight))
                {
                    result = ScaleImageData(result, width, height, hvFormat, verbose);
                }

                // Add palette data to the result if we have it
                if (palette != null)
                {
                    result.Palette = palette;
                }

                if (verbose)
                {
                    Console.WriteLine($"\nUnpacked IMAG image:");
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
                Console.WriteLine($"Error unpacking IMAG data: {ex.Message}");
                if (verbose)
                {
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
                return null;
            }
        }

        /// <summary>
        /// Convenience method that unpacks an IMAG file and saves it directly to PNG.
        /// </summary>
        /// <param name="imagFilePath">Path to the IMAG file</param>
        /// <param name="outputPath">Path where to save the PNG file</param>
        /// <param name="palette">Color palette to use for rendering (optional for uncoded images)</param>
        /// <param name="verbose">If true, displays diagnostic information</param>
        /// <returns>True if successful, false if failed</returns>
        public static bool UnpackAndSaveImagFile(string imagFilePath, string outputPath, List<Color>? palette = null, bool verbose = false)
        {
            var imagData = UnpackImagFile(imagFilePath, verbose);
            if (imagData == null)
            {
                if (verbose) Console.WriteLine($"Failed to unpack IMAG file: {imagFilePath}");
                return false;
            }

            try
            {
                SaveCelImage(imagData, outputPath, palette ?? imagData.Palette, imagData.CcbFlags, imagData.Pixc, useFrameBufferBlending: false, verbose);
                if (verbose) Console.WriteLine($"Successfully saved IMAG image to: {outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                if (verbose) Console.WriteLine($"Failed to save IMAG image: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Convenience method that unpacks a CEL file and saves it directly to PNG.
        /// If the file contains multiple PDAT chunks, saves them as separate numbered files.
        /// </summary>
        /// <param name="celFilePath">Path to the CEL file</param>
        /// <param name="outputPath">Path where to save the PNG file (or base name for multiple files)</param>
        /// <param name="palette">Color palette to use for rendering</param>
        /// <param name="bitsPerPixel">Optional: Override auto-detected bits per pixel (1, 2, 4, 6, 8, or 16). If 0, auto-detect.</param>
        /// <param name="verbose">If true, displays diagnostic information</param>
        /// <returns>True if successful, false if failed</returns>
        public static bool UnpackAndSaveCelFile(string celFilePath, string outputPath, List<Color>? palette = null, int bitsPerPixel = 0, bool verbose = false)
        {
            var celDataList = UnpackCelFileMultiple(celFilePath, verbose, bitsPerPixel);
            if (celDataList.Count == 0)
            {
                if (verbose) Console.WriteLine($"Failed to unpack CEL file: {celFilePath}");
                return false;
            }

            try
            {
                if (celDataList.Count == 1)
                {
                    // Single PDAT - save with original filename
                    SaveCelImage(celDataList[0], outputPath, palette ?? celDataList[0].Palette, celDataList[0].CcbFlags, celDataList[0].Pixc, useFrameBufferBlending: false, verbose);
                    if (verbose) Console.WriteLine($"Successfully saved CEL image to: {outputPath}");
                }
                else
                {
                    // Multiple PDATs - save with numbered suffixes
                    string directory = Path.GetDirectoryName(outputPath) ?? "";
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(outputPath);
                    string extension = Path.GetExtension(outputPath);

                    for (int i = 0; i < celDataList.Count; i++)
                    {
                        string numberedPath = Path.Combine(directory, $"{fileNameWithoutExt}_{i:D4}{extension}");
                        SaveCelImage(celDataList[i], numberedPath, palette ?? celDataList[i].Palette, celDataList[i].CcbFlags, celDataList[i].Pixc, useFrameBufferBlending: true, verbose);
                        if (verbose) Console.WriteLine($"Successfully saved CEL image #{i + 1} to: {numberedPath}");
                    }
                }
                
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

        /// <summary>
        /// CCB FLAGS word value from the cel control block.
        /// Contains control bits for transparency, packing, and rendering behavior.
        /// Important for PLUT 000 transparency handling (BGND flag at bit 5).
        /// </summary>
        public uint CcbFlags { get; set; } = 0;

        /// <summary>
        /// PIXC word from CCB (word #13) - Pixel Processor Control.
        /// Controls pixel processor math operations for blending, scaling, and effects.
        /// Contains P-mode 0 (bits 0-15) and P-mode 1 (bits 16-31) settings.
        /// Only populated when LDPIXC flag (bit 24 of FLAGS) is set in CCB.
        /// </summary>
        public uint Pixc { get; set; } = 0;
    }
}
