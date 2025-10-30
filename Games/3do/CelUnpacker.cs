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
        public static CelImageData UnpackCodedPackedCelData(byte[] packedData, int bitsPerPixel = 8, bool verbose = false)
        {
            return UnpackCelData(packedData, bitsPerPixel, coded: true, packed: true, verbose: verbose);
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
        /// Unpacks 3DO CEL data in uncoded_packed format.
        /// Raw pixel data with row offset headers but no packet encoding.
        /// Width and height are automatically determined from the data structure.
        /// </summary>
        /// <param name="packedData">The packed CEL data</param>
        /// <param name="bitsPerPixel">Bits per pixel (1, 2, 4, 6, 8, or 16)</param>
        /// <returns>Unpacked pixel data with dimensions</returns>
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
        private static CelImageData UnpackUnpackedCelData(byte[] celData, int width, int height, int bitsPerPixel, bool coded)
        {
            int bytesPerPixel = (bitsPerPixel + 7) / 8;
            byte[] unpackedData = new byte[width * height * bytesPerPixel];
            int outputOffset = 0;
            
            BitReader bitReader = new BitReader(celData);
            
            for (int row = 0; row < height; row++)
            {
                // Each row starts at a 32-bit word boundary
                bitReader.AlignToWord();
                
                if (coded)
                {
                    // CODED UNPACKED: Process packets for this row
                    int pixelsInRow = 0;
                    bool endOfLine = false;
                    
                    while (pixelsInRow < width && !endOfLine && bitReader.HasMoreData())
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
                                    int count = bitReader.ReadBits(6) + 1;
                                    for (int i = 0; i < count && pixelsInRow < width; i++)
                                    {
                                        WritePixel(unpackedData, outputOffset, 0, bitsPerPixel);
                                        outputOffset += bytesPerPixel;
                                        pixelsInRow++;
                                    }
                                }
                                break;
                                
                            case PACK_REPEAT:
                                {
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
                else
                {
                    // UNCODED UNPACKED: Raw pixel data, just read width pixels
                    for (int col = 0; col < width; col++)
                    {
                        int pixelValue = bitReader.ReadBits(bitsPerPixel);
                        WritePixel(unpackedData, outputOffset, pixelValue, bitsPerPixel);
                        outputOffset += bytesPerPixel;
                    }
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
        private static CelImageData UnpackCelData(byte[] celData, int bitsPerPixel = 8, bool coded = true, bool packed = true, bool verbose = false)
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
                Height = height-1,
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
        /// Reads a CEL file, parses the CCB header, and automatically unpacks the pixel data
        /// using the appropriate method based on the header information.
        /// </summary>
        /// <param name="celFile">Full path to the CEL file</param>
        /// <param name="verbose">If true, displays CCB header information</param>
        /// <param name="bitsPerPixel">Optional: Override auto-detected bits per pixel (1, 2, 4, 6, 8, or 16). If 0, auto-detect.</param>
        /// <returns>Unpacked CEL image data with dimensions and pixel data</returns>
        public static CelImageData? UnpackCelFile(string celFile, bool verbose = false, int bitsPerPixel = 0)
        {
            if (!File.Exists(celFile))
            {
                Console.WriteLine($"File not found: {celFile}");
                return null;
            }
            List<Color> palette = new List<Color>();
            byte[] data = File.ReadAllBytes(celFile);
            
            if (data.Length < 0x20)
            {
                Console.WriteLine($"File too small to contain a CCB header (minimum 32 bytes, got {data.Length})");
                return null;
            }

            // Check for CCB magic
            if (data[0] != 0x43 || data[1] != 0x43 || data[2] != 0x42 || data[3] != 0x20)
            {
                Console.WriteLine($"Invalid CCB magic. Expected 'CCB ', got: {(char)data[0]}{(char)data[1]}{(char)data[2]}{(char)data[3]}");
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

            // Check for PLUT chunk within CCB file - this indicates a palette file, not an image file
            for (int i = 0; i < data.Length - 4; i++)
            {
                if (data[i] == 0x50 && data[i + 1] == 0x4C && data[i + 2] == 0x55 && data[i + 3] == 0x54) // "PLUT"
                {
                    if (verbose) Console.WriteLine($"Found PLUT chunk at offset 0x{i:X2} - extracting palette data");
                    
                    // Read PLUT chunk size (4 bytes after PLUT magic)
                    if (i + 8 >= data.Length)
                    {
                        if (verbose) Console.WriteLine("PLUT chunk too small to contain size information");
                        return null;
                    }
                    
                    uint plutSize = ReadBigEndianUInt32(data, i + 4) - 12;
                    if (verbose) Console.WriteLine($"PLUT chunk size: {plutSize} bytes");
                    var plutEntries = ReadBigEndianUInt32(data, i + 8);
                    // Calculate palette data start (after PLUT magic + size)
                    int paletteDataStart = i + 12;
                    int availableBytes = data.Length - paletteDataStart;
                    
                    if (availableBytes < plutSize)
                    {
                        if (verbose) Console.WriteLine($"Not enough data for PLUT chunk. Expected: {plutSize}, Available: {availableBytes}");
                        return null;
                    }
                    
                    // Extract palette data
                    byte[] paletteBytes = new byte[plutSize];
                    Array.Copy(data, paletteDataStart, paletteBytes, 0, (int)plutSize);
                    
                    // Convert to ImageSharp Color list using ReadRgb15PaletteIS
                    palette = ColorHelper.ReadRgb15PaletteIS(paletteBytes);
                    
                    if (verbose) Console.WriteLine($"Extracted {palette.Count} colors from PLUT chunk");

                }
            }

            // Read file header
            uint chunkSize = ReadBigEndianUInt32(data, 0x04);
            // 4 bytes padding at 0x08-0x0B
            
            // Parse CCB structure starting at 0x0C
            int ccbOffset = 0x0C;
            
            // Required CCB words (first 6 words)
            uint flags = ReadBigEndianUInt32(data, ccbOffset); ccbOffset += 4;
            uint nextPtr = ReadBigEndianUInt32(data, ccbOffset) & 0xFFFFFF; ccbOffset += 4; // 24-bit
            uint sourcePtr = ReadBigEndianUInt32(data, ccbOffset) & 0xFFFFFF; ccbOffset += 4; // 24-bit  
            uint plutPtr = ReadBigEndianUInt32(data, ccbOffset) & 0xFFFFFF; ccbOffset += 4; // 24-bit
            uint xPos = ReadBigEndianUInt32(data, ccbOffset); ccbOffset += 4; // 32-bit 16.16 format
            uint yPos = ReadBigEndianUInt32(data, ccbOffset); ccbOffset += 4; // 32-bit 16.16 format
            
            // Parse optional words based on flags
            uint hdx = 0, hdy = 0, vdx = 0, vdy = 0;
            uint hddx = 0, hddy = 0;
            uint pixc = 0;
            uint pre0 = 0, pre1 = 0;
            
            // Check flags for optional words (they must appear in this order)
            if ((flags & 0x04000000) != 0) // LDSIZE flag - bit 26
            {
                hdx = ReadBigEndianUInt32(data, ccbOffset); ccbOffset += 4;
                hdy = ReadBigEndianUInt32(data, ccbOffset); ccbOffset += 4;
                vdx = ReadBigEndianUInt32(data, ccbOffset); ccbOffset += 4;
                vdy = ReadBigEndianUInt32(data, ccbOffset); ccbOffset += 4;
            }
            
            if ((flags & 0x02000000) != 0) // LDPRS flag - bit 25
            {
                hddx = ReadBigEndianUInt32(data, ccbOffset); ccbOffset += 4;
                hddy = ReadBigEndianUInt32(data, ccbOffset); ccbOffset += 4;
            }
            
            if ((flags & 0x01000000) != 0) // LDPIXC flag - bit 24
            {
                pixc = ReadBigEndianUInt32(data, ccbOffset); ccbOffset += 4;
            }
            
            if ((flags & 0x00400000) != 0) // CCBPRE flag - bit 22
            {
                pre0 = ReadBigEndianUInt32(data, ccbOffset); ccbOffset += 4;
                if ((flags & 0x00000200) != 0) // PACKED flag - bit 9 (determines if PRE1 exists)
                {
                    // Packed format uses only PRE0
                }
                else
                {
                    // Unpacked format uses PRE0 and PRE1
                    pre1 = ReadBigEndianUInt32(data, ccbOffset); ccbOffset += 4;
                }
            }
            
            // Extract width and height from the image data header
            int ccbWidth = 0, ccbHeight = 0;
            
            if (verbose)
            {
                Console.WriteLine($"Available data for width/height at offset 0x{ccbOffset:X}: {data.Length - ccbOffset} bytes");
                if (ccbOffset + 16 <= data.Length)
                {
                    Console.WriteLine($"Next 16 bytes: {string.Join(" ", data.Skip(ccbOffset).Take(16).Select(b => $"{b:X2}"))}");
                }
            }
            
            // For standalone .cel files, width/height are typically at the start of the image data
            if (ccbOffset + 8 <= data.Length)
            {
                ccbWidth = ReadBigEndianInt32(data, ccbOffset);
                ccbHeight = ReadBigEndianInt32(data, ccbOffset + 4);
                
                if (verbose)
                {
                    Console.WriteLine($"Read width from 0x{ccbOffset:X}: {ccbWidth}");
                    Console.WriteLine($"Read height from 0x{ccbOffset + 4:X}: {ccbHeight}");
                }
                
                // Check if these values seem reasonable for dimensions
                if (ccbWidth > 0 && ccbWidth < 2048 && ccbHeight > 0 && ccbHeight < 2048)
                {
                    ccbOffset += 8; // Skip width/height
                    if (verbose)
                    {
                        Console.WriteLine($"Width/height seem valid, advancing to 0x{ccbOffset:X}");
                    }
                }
                else
                {
                    // Width/height don't look valid, maybe they're embedded differently
                    // Try looking at the PRE0/PRE1 values if available
                    if (pre0 != 0)
                    {
                        // PRE0 bits 0-9 sometimes contain width-1, bits 10-19 contain height-1
                        int pre0Width = (int)(pre0 & 0x3FF) + 1;
                        int pre0Height = (int)((pre0 >> 10) & 0x3FF) + 1;
                        
                        if (verbose)
                        {
                            Console.WriteLine($"PRE0 derived width: {pre0Width}, height: {pre0Height}");
                        }
                        
                        if (pre0Width > 0 && pre0Width < 2048 && pre0Height > 0 && pre0Height < 2048)
                        {
                            ccbWidth = pre0Width;
                            ccbHeight = pre0Height;
                            if (verbose)
                            {
                                Console.WriteLine($"Using PRE0 derived dimensions: {ccbWidth}x{ccbHeight}");
                            }
                        }
                    }
                    
                    // Don't advance ccbOffset if we didn't find valid dimensions at the start
                    if (verbose)
                    {
                        Console.WriteLine($"Width/height not found at expected location, keeping offset at 0x{ccbOffset:X}");
                    }
                }
            }
            
            // Update variable names for compatibility with existing code
            uint ccbPRE0 = pre0;
            uint ccbFlags = flags;
            
            // Display header info if verbose
            if (verbose)
            {
                Console.WriteLine($"CCB FLAGS: 0x{flags:X8}");
                Console.WriteLine($"PACKED: {((flags & 0x00000200) != 0)}");
                Console.WriteLine($"CCBPRE: {((flags & 0x00400000) != 0)}");
                Console.WriteLine($"PRE0: 0x{pre0:X8}, PRE1: 0x{pre1:X8}");
                Console.WriteLine($"Width: {ccbWidth}, Height: {ccbHeight}");
                Console.WriteLine($"Source data starts at offset: 0x{ccbOffset:X}");
            }

            // Determine format from PRE0 and CCB flags
            // PRE0 bit 6 (0x40) = Linear bit (1 = uncoded/linear, 0 = coded)
            // PRE0 bit 7 (0x80) = Packed bit (1 = packed, 0 = unpacked)
            // CCBLDPRS flag (0x02000000) indicates preamble data follows
            
            bool isCoded = true;
            // Extract pixel data starting from the current ccbOffset
            byte[] pixelData = new byte[data.Length - ccbOffset];
            Array.Copy(data, ccbOffset, pixelData, 0, pixelData.Length);

            if (verbose)
            {
                Console.WriteLine($"Pixel data size: {pixelData.Length} bytes");
            }

            // Detect format from FLAGS and PRE0/PRE1
            bool formatDetected = false;
            bool isPacked = false;
            int bpp = bitsPerPixel; // Use override if provided

            // Check if we have PRE0 from CCB header
            if ((flags & 0x00400000) != 0 && pre0 != 0) // CCBPRE flag set and PRE0 exists
            {
                bool linear = (pre0 & 0x40) != 0;    // Bit 6: 1 = linear (uncoded), 0 = coded
                bool packed = (pre0 & 0x80) != 0;    // Bit 7: 1 = packed, 0 = unpacked
                isCoded = !linear;
                isPacked = packed;
                formatDetected = true;
                
                if (verbose)
                {
                    Console.WriteLine($"Format detected from CCB PRE0:");
                    Console.WriteLine($"  Linear: {linear} (Coded: {isCoded})");
                    Console.WriteLine($"  Packed: {packed}");
                }
            }
            
            // Also check PACKED flag from CCB FLAGS word
            if ((flags & 0x00000200) != 0) // PACKED flag - bit 9
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
                // PRE0 typically has BPP in bits 31-24 for coded CELs
                bpp = (int)((pre0 >> 24) & 0xFF);
                
                // If BPP from PRE0 is 0 or doesn't make sense, try to infer
                if (bpp == 0 || (bpp != 1 && bpp != 2 && bpp != 4 && bpp != 6 && bpp != 8 && bpp != 16))
                {
                    // Try bits 6-0 of PRE0 for packed format indicator
                    int pre0_bpp_alt = (int)(pre0 & 0x3F);
                    if (pre0_bpp_alt >= 1 && pre0_bpp_alt <= 16)
                        bpp = pre0_bpp_alt;
                    else
                        bpp = 6; // Default to 6bpp for packed formats (common)
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
                int bytesPerPixel = (bpp + 7) / 8;
                int expectedUnpackedSize = ccbWidth * ccbHeight * bytesPerPixel;
                int actualDataSize = data.Length - 0x58; // Rough estimate: file size minus headers
                
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

            // Calculate pixel data offset
            // CCB header is 0x50 bytes, preamble words follow (PRE0 and PRE1 are at 0x40 and 0x44)
            // Actual pixel data typically starts after the CCB header (0x50) + preamble  
            int pixelDataOffset = (int)chunkSize; // Chunk size should point past the CCB
            
            // For packed CELs, there's often an 8-byte preamble after the CCB header
            // Check if chunk size equals CCB header size (0x50)
            if (pixelDataOffset == 0x50)
            {
                // Check for preamble words after CCB header
                if (pixelDataOffset + 8 <= data.Length)
                {
                    uint preambleCheck = ReadBigEndianUInt32(data, pixelDataOffset);
                    
                    if (verbose)
                    {
                        Console.WriteLine($"  Checking for preamble at 0x{pixelDataOffset:X}: 0x{preambleCheck:X8}");
                        Console.WriteLine($"  CCB PRE0: 0x{ccbPRE0:X8}");
                    }
                    
                    // Check if the data at 0x50 looks like a preamble (non-zero in upper byte)
                    // For packed CELs, preamble typically has format information in upper bytes
                    if ((preambleCheck & 0xFF000000) != 0)
                    {
                        pixelDataOffset += 8; // Skip preamble words
                        if (verbose)
                        {
                            Console.WriteLine($"  Found preamble, skipping to 0x{pixelDataOffset:X}");
                        }
                    }
                }
            }
            else if (pixelDataOffset < 0x50 || pixelDataOffset > data.Length)
            {
                // Invalid chunk size, default to 0x50
                pixelDataOffset = 0x50;
                
                // Check for preamble
                if (pixelDataOffset + 8 <= data.Length)
                {
                    uint preambleCheck = ReadBigEndianUInt32(data, pixelDataOffset);
                    
                    if ((preambleCheck & 0xFF000000) != 0)
                    {
                        pixelDataOffset += 8;
                    }
                }
            }

            if (verbose)
            {
                Console.WriteLine($"  Pixel data offset: 0x{ccbOffset:X}");
            }

            try
            {
                // Unpack the pixel data using the detected format
                CelImageData result;
                
                if (!isPacked)
                {
                    // Unpacked formats need width and height from CCB header
                    if (ccbWidth <= 0 || ccbHeight <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Unpacked format detected but CCB dimensions are invalid (width={ccbWidth}, height={ccbHeight}). " +
                            "Unpacked formats require valid width and height in the CCB header.");
                    }
                    
                    result = UnpackUnpackedCelData(pixelData, ccbWidth, ccbHeight, bpp, coded: isCoded);
                }
                else
                {
                    // Packed formats can auto-detect dimensions
                    result = UnpackCelData(pixelData, bpp, coded: isCoded, packed: isPacked);
                }
                
                if (verbose)
                {
                    Console.WriteLine($"\nUnpacked image:");
                    Console.WriteLine($"  Width: {result.Width}");
                    Console.WriteLine($"  Height: {result.Height}");
                    Console.WriteLine($"  BPP: {result.BitsPerPixel}");
                    Console.WriteLine($"  Pixel data size: {result.PixelData.Length} bytes");
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
        /// <param name="palette">Color palette to use for rendering</param>
        public static void SaveCelImage(CelImageData celOutput, string outputPath, List<Color> palette)
        {
            Image<Rgba32> image;
            if (celOutput.TransparencyMask != null)
            {
                // Create RGBA image with transparency
                image = new Image<Rgba32>(celOutput.Width, celOutput.Height);
                for (int y = 0; y < celOutput.Height; y++)
                {
                    for (int x = 0; x < celOutput.Width; x++)
                    {
                        int pixelIndex = y * celOutput.Width + x;
                        byte plutIndex = celOutput.PixelData[pixelIndex];

                        if (celOutput.TransparencyMask[pixelIndex])
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
                // No transparency mask - use standard method
                image = ImageFormatHelper.GenerateIMClutImage(palette, celOutput.PixelData, celOutput.Width, celOutput.Height, true);
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
