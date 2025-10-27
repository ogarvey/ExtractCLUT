using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    /// <summary>
    /// Decompressor for Fugger 2 icon DAT files using Run-Length Encoding
    /// Based on reverse engineering analysis of function DrawRLESprite (0x00013a56)
    /// 
    /// IMPORTANT UPDATE: Icon DAT files store DIRECT RGB16 values, not palette indices!
    /// 
    /// The decompressed data format depends on color depth:
    /// - RGB16Bit (most icons): Direct RGB565/RGB555 color values (2 bytes per pixel)
    /// - Palette8Bit (rare): VGA palette indices requiring hardware palette lookup
    /// 
    /// For RGB16 icons:
    /// 1. Decompress with ColorDepth.RGB16Bit (this gives you raw RGB565/555 data)
    /// 2. Convert to RGB888 using ConvertRGB565ToRGB888() or ConvertRGB555ToRGB888()
    /// 3. No palette lookup needed!
    /// 
    /// The VGA palette at 0x0008383c is for the hardware VGA DAC, not for icon decoding.
    /// See ICON_FORMAT_CLARIFIED.md for detailed explanation.
    /// </summary>
    public class IconRLEDecompressor
    {
        // RLE control bytes
        private const byte RLE_END_OF_SPRITE = 0xFF;
        private const byte RLE_END_OF_SCANLINE = 0xFE;
        private const byte RLE_MAX_SKIP = 0xFD;

        /// <summary>
        /// Color depth modes supported by the engine
        /// </summary>
        public enum ColorDepth
        {
            Palette8Bit = 1,    // 1 byte per pixel (palette index)
            RGB16Bit = 2,       // 2 bytes per pixel (RGB565 or RGB555)
            RGB32Bit = 4        // 4 bytes per pixel (RGBA)
        }

        /// <summary>
        /// Auto-detect sprite dimensions by analyzing RLE structure
        /// </summary>
        /// <param name="compressedData">RLE compressed sprite data</param>
        /// <param name="colorDepth">Bytes per pixel (1, 2, or 4)</param>
        /// <returns>Tuple of (width, height, scanlineCount)</returns>
        public static (int width, int height, int scanlineCount) DetectDimensions(byte[] compressedData, ColorDepth colorDepth)
        {
            int bytesPerPixel = (int)colorDepth;
            int srcPos = 0;
            int maxPixelsPerLine = 0;
            int currentLinePixels = 0;
            int scanlineCount = 0;

            while (srcPos < compressedData.Length)
            {
                byte controlByte = compressedData[srcPos++];

                if (controlByte == RLE_END_OF_SPRITE)
                {
                    // End of sprite - update max if current line has pixels
                    if (currentLinePixels > 0)
                    {
                        maxPixelsPerLine = Math.Max(maxPixelsPerLine, currentLinePixels);
                        scanlineCount++;
                    }
                    break;
                }
                else if (controlByte == RLE_END_OF_SCANLINE)
                {
                    // End of scanline
                    maxPixelsPerLine = Math.Max(maxPixelsPerLine, currentLinePixels);
                    currentLinePixels = 0;
                    scanlineCount++;
                }
                else if (controlByte <= RLE_MAX_SKIP)
                {
                    // Skip pixels
                    currentLinePixels += controlByte;

                    // Read pixel count
                    if (srcPos >= compressedData.Length)
                        break;

                    byte pixelCount = compressedData[srcPos++];
                    currentLinePixels += pixelCount;

                    // Skip the actual pixel data
                    srcPos += pixelCount * bytesPerPixel;
                }
            }

            return (maxPixelsPerLine, scanlineCount, scanlineCount);
        }

        /// <summary>
        /// Decompress RLE-encoded icon data with auto-detected dimensions
        /// </summary>
        /// <param name="compressedData">RLE compressed sprite data</param>
        /// <param name="colorDepth">Bytes per pixel (1, 2, or 4)</param>
        /// <returns>Tuple of (decompressed data, width, height)</returns>
        public static (byte[] data, int width, int height) DecompressAuto(byte[] compressedData, ColorDepth colorDepth)
        {
            var (width, height, _) = DetectDimensions(compressedData, colorDepth);

            if (width == 0 || height == 0)
            {
                throw new InvalidDataException("Could not detect valid dimensions from RLE data");
            }

            byte[] data = Decompress(compressedData, width, height, colorDepth);
            return (data, width, height);
        }

        /// <summary>
        /// Decompress RLE-encoded icon data
        /// </summary>
        /// <param name="compressedData">RLE compressed sprite data</param>
        /// <param name="width">Sprite width in pixels</param>
        /// <param name="height">Sprite height in pixels</param>
        /// <param name="colorDepth">Bytes per pixel (1, 2, or 4)</param>
        /// <returns>Decompressed pixel data</returns>
        public static byte[] Decompress(byte[] compressedData, int width, int height, ColorDepth colorDepth)
        {
            int bytesPerPixel = (int)colorDepth;
            int scanlineWidth = width * bytesPerPixel;
            byte[] output = new byte[width * height * bytesPerPixel];

            int srcPos = 0;
            int destPos = 0;
            int currentY = 0;

            while (srcPos < compressedData.Length && currentY < height)
            {
                byte controlByte = compressedData[srcPos++];

                if (controlByte == RLE_END_OF_SPRITE)
                {
                    // End of sprite
                    break;
                }
                else if (controlByte == RLE_END_OF_SCANLINE)
                {
                    // Move to next scanline
                    currentY++;
                    destPos = currentY * scanlineWidth;
                }
                else if (controlByte <= RLE_MAX_SKIP)
                {
                    // Skip 'controlByte' transparent pixels
                    destPos += controlByte * bytesPerPixel;

                    // Read pixel count for the run
                    if (srcPos >= compressedData.Length)
                        break;

                    byte pixelCount = compressedData[srcPos++];

                    // Copy 'pixelCount' pixels
                    for (int i = 0; i < pixelCount; i++)
                    {
                        if (srcPos + bytesPerPixel > compressedData.Length)
                            break;

                        // Copy pixel data based on color depth
                        for (int b = 0; b < bytesPerPixel; b++)
                        {
                            if (destPos < output.Length)
                            {
                                output[destPos++] = compressedData[srcPos++];
                            }
                            else
                            {
                                srcPos++;
                            }
                        }
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// Decompress RLE icon and convert to RGB24 (all-in-one method)
        /// </summary>
        /// <param name="compressedData">RLE compressed sprite data</param>
        /// <param name="useRGB555">If true, use RGB555 format; if false, use RGB565 (default)</param>
        /// <returns>Tuple of (RGB24 data, width, height)</returns>
        public static (byte[] rgb24Data, int width, int height) DecompressToRGB24(byte[] compressedData, bool useRGB555 = false)
        {
            // Decompress as RGB16
            var (rgb16Data, width, height) = DecompressAuto(compressedData, ColorDepth.RGB16Bit);

            // Convert to RGB24
            byte[] rgb24Data = useRGB555
                ? ConvertRGB555ToRGB888(rgb16Data)
                : ConvertRGB565ToRGB888(rgb16Data);

            return (rgb24Data, width, height);
        }

        /// <summary>
        /// Decompress and save as raw RGB file for analysis (auto-detect dimensions)
        /// </summary>
        public static void DecompressToRawAuto(string inputPath, string outputPath, ColorDepth colorDepth)
        {
            byte[] compressedData = File.ReadAllBytes(inputPath);
            var (decompressedData, width, height) = DecompressAuto(compressedData, colorDepth);
            File.WriteAllBytes(outputPath, decompressedData);

            Console.WriteLine($"Decompressed {inputPath}");
            Console.WriteLine($"  Input size: {compressedData.Length} bytes");
            Console.WriteLine($"  Output size: {decompressedData.Length} bytes");
            Console.WriteLine($"  Auto-detected dimensions: {width}x{height}");
            Console.WriteLine($"  Color depth: {colorDepth}");
            Console.WriteLine($"  Compression ratio: {(double)compressedData.Length / decompressedData.Length:P1}");
        }

        /// <summary>
        /// Decompress and save as raw RGB file for analysis
        /// </summary>
        public static void DecompressToRaw(string inputPath, string outputPath, int width, int height, ColorDepth colorDepth)
        {
            byte[] compressedData = File.ReadAllBytes(inputPath);
            byte[] decompressedData = Decompress(compressedData, width, height, colorDepth);
            File.WriteAllBytes(outputPath, decompressedData);

            Console.WriteLine($"Decompressed {inputPath}");
            Console.WriteLine($"  Input size: {compressedData.Length} bytes");
            Console.WriteLine($"  Output size: {decompressedData.Length} bytes");
            Console.WriteLine($"  Dimensions: {width}x{height}");
            Console.WriteLine($"  Color depth: {colorDepth}");
            Console.WriteLine($"  Compression ratio: {(double)compressedData.Length / decompressedData.Length:P1}");
        }

        /// <summary>
        /// Apply a palette to convert indices to RGB values
        /// </summary>
        /// <param name="indexedData">Palette-indexed data (one index per pixel)</param>
        /// <param name="palette">RGB palette (3 bytes per entry: R, G, B)</param>
        /// <param name="paletteSize">Number of palette entries</param>
        /// <returns>RGB data (3 bytes per pixel)</returns>
        public static byte[] ApplyPalette(byte[] indexedData, byte[] palette, int paletteSize = 256)
        {
            if (palette.Length < paletteSize * 3)
            {
                throw new ArgumentException($"Palette must contain at least {paletteSize * 3} bytes (RGB for {paletteSize} colors)");
            }

            byte[] rgbData = new byte[indexedData.Length * 3];

            for (int i = 0; i < indexedData.Length; i++)
            {
                int paletteIndex = indexedData[i];
                if (paletteIndex < paletteSize)
                {
                    int paletteOffset = paletteIndex * 3;
                    rgbData[i * 3] = palette[paletteOffset];         // R
                    rgbData[i * 3 + 1] = palette[paletteOffset + 1]; // G
                    rgbData[i * 3 + 2] = palette[paletteOffset + 2]; // B
                }
            }

            return rgbData;
        }

        /// <summary>
        /// Apply a 16-bit palette to convert indices to RGB565/555 values
        /// </summary>
        /// <param name="indexedData">Palette-indexed data (one index per pixel)</param>
        /// <param name="palette16">16-bit palette (2 bytes per entry, little-endian RGB565/555)</param>
        /// <param name="paletteSize">Number of palette entries</param>
        /// <returns>RGB16 data (2 bytes per pixel)</returns>
        public static byte[] ApplyPalette16(byte[] indexedData, byte[] palette16, int paletteSize = 256)
        {
            if (palette16.Length < paletteSize * 2)
            {
                throw new ArgumentException($"16-bit palette must contain at least {paletteSize * 2} bytes");
            }

            byte[] rgb16Data = new byte[indexedData.Length * 2];

            for (int i = 0; i < indexedData.Length; i++)
            {
                int paletteIndex = indexedData[i];
                if (paletteIndex < paletteSize)
                {
                    int paletteOffset = paletteIndex * 2;
                    rgb16Data[i * 2] = palette16[paletteOffset];         // Low byte
                    rgb16Data[i * 2 + 1] = palette16[paletteOffset + 1]; // High byte
                }
            }

            return rgb16Data;
        }

        /// <summary>
        /// Convert RGB565 to RGB888
        /// </summary>
        public static byte[] ConvertRGB565ToRGB888(byte[] rgb565Data)
        {
            byte[] rgb888 = new byte[(rgb565Data.Length / 2) * 3];
            int outPos = 0;

            for (int i = 0; i < rgb565Data.Length; i += 2)
            {
                ushort rgb565 = (ushort)(rgb565Data[i] | (rgb565Data[i + 1] << 8));

                byte r = (byte)(((rgb565 >> 11) & 0x1F) << 3);
                byte g = (byte)(((rgb565 >> 5) & 0x3F) << 2);
                byte b = (byte)((rgb565 & 0x1F) << 3);

                rgb888[outPos++] = r;
                rgb888[outPos++] = g;
                rgb888[outPos++] = b;
            }

            return rgb888;
        }

        /// <summary>
        /// Convert RGB555 to RGB888
        /// </summary>
        public static byte[] ConvertRGB555ToRGB888(byte[] rgb555Data)
        {
            byte[] rgb888 = new byte[(rgb555Data.Length / 2) * 3];
            int outPos = 0;

            for (int i = 0; i < rgb555Data.Length; i += 2)
            {
                ushort rgb555 = (ushort)(rgb555Data[i] | (rgb555Data[i + 1] << 8));

                byte r = (byte)(((rgb555 >> 10) & 0x1F) << 3);
                byte g = (byte)(((rgb555 >> 5) & 0x1F) << 3);
                byte b = (byte)((rgb555 & 0x1F) << 3);

                rgb888[outPos++] = r;
                rgb888[outPos++] = g;
                rgb888[outPos++] = b;
            }

            return rgb888;
        }
    }
}
