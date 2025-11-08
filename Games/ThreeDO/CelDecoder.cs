using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;

namespace ExtractCLUT.Games.ThreeDO
{
    /// <summary>
    /// Modern 3DO CEL decoder with proper separation of concerns.
    /// Each cel format has its own dedicated decoding method.
    /// </summary>
    public static class CelDecoder
    {
        // Packet type constants from 3DO CEL specification
        private const int PACK_EOL = 0b00;         // End of line
        private const int PACK_LITERAL = 0b01;     // Literal pixel data
        private const int PACK_TRANSPARENT = 0b10; // Transparent pixels
        private const int PACK_REPEAT = 0b11;      // Repeat single pixel

        /// <summary>
        /// Decodes a 3DO CEL image based on the CCB header format flags.
        /// Routes to the appropriate decoder based on coded/uncoded and packed/unpacked flags.
        /// </summary>
        public static CelImageData Decode(byte[] pixelData, CcbHeader ccb, int bitsPerPixel = 0, bool verbose = false)
        {
            bool isCoded = ccb.IsCoded;
            bool isPacked = ccb.IsPacked;
            int bpp = bitsPerPixel > 0 ? bitsPerPixel : ccb.GetBitsPerPixel();
            int width = ccb.GetWidth();
            int height = ccb.GetHeight();

            if (verbose)
            {
                Console.WriteLine($"\n[CelDecoder] Decoding: {width}x{height}, {bpp}bpp");
                Console.WriteLine($"  Format: {(isCoded ? "Coded" : "Uncoded")} + {(isPacked ? "Packed" : "Unpacked")}");
                Console.WriteLine($"  CCB Flags: {ccb}");
            }

            if (isCoded && isPacked)
            {
                return DecodeCodedPacked(pixelData, ccb, width, height, bpp, verbose);
            }
            else if (isCoded && !isPacked)
            {
                return DecodeCodedUnpacked(pixelData, ccb, width, height, bpp, verbose);
            }
            else if (!isCoded && isPacked)
            {
                return DecodeUncodedPacked(pixelData, ccb, width, height, bpp, verbose);
            }
            else // !isCoded && !isPacked
            {
                return DecodeUncodedUnpacked(pixelData, ccb, width, height, bpp, verbose);
            }
        }

        /// <summary>
        /// Decodes CODED PACKED format: palette indices with packet encoding and row headers
        /// </summary>
        private static CelImageData DecodeCodedPacked(byte[] data, CcbHeader ccb, int width, int height, int bpp, bool verbose)
        {
            if (verbose)
                Console.WriteLine($"[DecodeCodedPacked] {width}x{height}, {bpp}bpp");

            int bytesPerPixel = (bpp + 7) / 8;
            byte[] pixels = new byte[width * height * bytesPerPixel];
            bool[] transparencyMask = new bool[width * height];
            byte[]? amvData = (bpp == 8) ? new byte[width * height] : null;

            // Initialize all pixels as transparent
            Array.Fill(transparencyMask, true);

            var bitReader = new CelUnpacker.BitReader(data);

            for (int row = 0; row < height; row++)
            {
                bitReader.AlignToWord();
                
                if (!bitReader.HasMoreData())
                    break;

                int rowStartWord = bitReader.CurrentWordPosition;

                // Read row offset header
                int nextRowOffset;
                if (bpp == 8 || bpp == 16)
                {
                    bitReader.ReadBits(6);
                    nextRowOffset = bitReader.ReadBits(10);
                }
                else
                {
                    nextRowOffset = bitReader.ReadBits(8);
                }

                int nextRowWord = rowStartWord + nextRowOffset + 2;

                // Process packets for this row
                int pixelsInRow = 0;
                bool endOfLine = false;
                int outputOffset = row * width * bytesPerPixel;
                int maskOffset = row * width;

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
                                    int pixelValue = bitReader.ReadBits(bpp);

                                    if (bpp == 8)
                                    {
                                        // 8bpp coded: lower 5 bits = palette index, upper 3 bits = AMV
                                        int plutIndex = pixelValue & 0x1F;
                                        int amv = (pixelValue >> 5) & 0x07;
                                        pixels[outputOffset++] = (byte)plutIndex;
                                        if (amvData != null) amvData[maskOffset] = (byte)amv;
                                    }
                                    else
                                    {
                                        // For other bpp: apply PLUTA padding if needed
                                        if (bpp < 5)
                                        {
                                            pixelValue = (ccb.Pluta << bpp) | pixelValue;
                                        }
                                        
                                        WritePixel(pixels, outputOffset, pixelValue, bpp);
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
                                    outputOffset += bytesPerPixel;
                                    transparencyMask[maskOffset++] = true;
                                    pixelsInRow++;
                                }
                            }
                            break;

                        case PACK_REPEAT:
                            {
                                int count = bitReader.ReadBits(6) + 1;
                                int pixelValue = bitReader.ReadBits(bpp);

                                int plutIndex, amv;
                                if (bpp == 8)
                                {
                                    plutIndex = pixelValue & 0x1F;
                                    amv = (pixelValue >> 5) & 0x07;
                                }
                                else
                                {
                                    if (bpp < 5)
                                    {
                                        pixelValue = (ccb.Pluta << bpp) | pixelValue;
                                    }
                                    plutIndex = pixelValue;
                                    amv = 0;
                                }

                                for (int i = 0; i < count && pixelsInRow < width; i++)
                                {
                                    if (bpp == 8)
                                    {
                                        pixels[outputOffset++] = (byte)plutIndex;
                                        if (amvData != null) amvData[maskOffset] = (byte)amv;
                                    }
                                    else
                                    {
                                        WritePixel(pixels, outputOffset, plutIndex, bpp);
                                        outputOffset += bytesPerPixel;
                                    }

                                    transparencyMask[maskOffset++] = false;
                                    pixelsInRow++;
                                }
                            }
                            break;
                    }
                }

                bitReader.SeekToWord(nextRowWord);
            }

            // Check if transparency is actually used
            bool hasTransparency = Array.Exists(transparencyMask, t => t);

            return new CelImageData
            {
                Width = width,
                Height = height,
                BitsPerPixel = bpp,
                PixelData = pixels,
                TransparencyMask = hasTransparency ? transparencyMask : null,
                AlternateMultiplyValues = amvData
            };
        }

        /// <summary>
        /// Decodes CODED UNPACKED format: palette indices without packet encoding
        /// </summary>
        private static CelImageData DecodeCodedUnpacked(byte[] data, CcbHeader ccb, int width, int height, int bpp, bool verbose)
        {
            if (verbose)
                Console.WriteLine($"[DecodeCodedUnpacked] {width}x{height}, {bpp}bpp");

            // For coded unpacked, pixels are stored sequentially, word-aligned per row
            int bytesPerPixel = (bpp + 7) / 8;
            byte[] pixels = new byte[width * height * bytesPerPixel];
            
            var bitReader = new CelUnpacker.BitReader(data);

            for (int row = 0; row < height; row++)
            {
                bitReader.AlignToWord();
                
                for (int col = 0; col < width; col++)
                {
                    int pixelValue = bitReader.ReadBits(bpp);
                    
                    // Apply PLUTA padding if needed
                    if (bpp < 5)
                    {
                        pixelValue = (ccb.Pluta << bpp) | pixelValue;
                    }
                    
                    int offset = (row * width + col) * bytesPerPixel;
                    WritePixel(pixels, offset, pixelValue, bpp);
                }
            }

            return new CelImageData
            {
                Width = width,
                Height = height,
                BitsPerPixel = bpp,
                PixelData = pixels
            };
        }

        /// <summary>
        /// Decodes UNCODED PACKED format: direct RGB values with packet encoding and row headers
        /// 8bpp: RGB332 (3R, 3G, 2B)
        /// 16bpp: RGB555 (5R, 5G, 5B, 1 control bit)
        /// </summary>
        private static CelImageData DecodeUncodedPacked(byte[] data, CcbHeader ccb, int width, int height, int bpp, bool verbose)
        {
            if (verbose)
                Console.WriteLine($"[DecodeUncodedPacked] {width}x{height}, {bpp}bpp RGB");

            int bytesPerPixel = 4; // Store as RGBA32
            byte[] pixels = new byte[width * height * bytesPerPixel];
            bool[] transparencyMask = new bool[width * height];

            Array.Fill(transparencyMask, true);

            var bitReader = new CelUnpacker.BitReader(data);

            for (int row = 0; row < height; row++)
            {
                bitReader.AlignToWord();
                
                if (!bitReader.HasMoreData())
                    break;

                int rowStartWord = bitReader.CurrentWordPosition;

                // Read row offset
                int nextRowOffset;
                if (bpp == 16)
                {
                    bitReader.ReadBits(6);
                    nextRowOffset = bitReader.ReadBits(10);
                }
                else
                {
                    nextRowOffset = bitReader.ReadBits(8);
                }

                int nextRowWord = rowStartWord + nextRowOffset + 2;

                int pixelsInRow = 0;
                bool endOfLine = false;
                int outputOffset = row * width * bytesPerPixel;
                int maskOffset = row * width;

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
                                    int pixelValue = bitReader.ReadBits(bpp);
                                    
                                    // Check BGND flag for 000 values in uncoded formats
                                    if (pixelValue == 0 && !ccb.Bgnd)
                                    {
                                        // BGND=0: treat 000 as transparent
                                        transparencyMask[maskOffset] = true;
                                    }
                                    else
                                    {
                                        // Apply NOBLK for 000 values
                                        if (pixelValue == 0 && !ccb.NoBlk)
                                        {
                                            pixelValue = 1; // Change 000 to 100
                                        }
                                        
                                        var (r, g, b, a) = ConvertToRgba(pixelValue, bpp);
                                        pixels[outputOffset++] = r;
                                        pixels[outputOffset++] = g;
                                        pixels[outputOffset++] = b;
                                        pixels[outputOffset++] = a;
                                        transparencyMask[maskOffset] = false;
                                    }

                                    maskOffset++;
                                    pixelsInRow++;
                                }
                            }
                            break;

                        case PACK_TRANSPARENT:
                            {
                                int count = bitReader.ReadBits(6) + 1;
                                for (int i = 0; i < count && pixelsInRow < width; i++)
                                {
                                    outputOffset += bytesPerPixel;
                                    transparencyMask[maskOffset++] = true;
                                    pixelsInRow++;
                                }
                            }
                            break;

                        case PACK_REPEAT:
                            {
                                int count = bitReader.ReadBits(6) + 1;
                                int pixelValue = bitReader.ReadBits(bpp);
                                
                                bool isTransparent = false;
                                byte r = 0, g = 0, b = 0, a = 255;

                                if (pixelValue == 0 && !ccb.Bgnd)
                                {
                                    isTransparent = true;
                                }
                                else
                                {
                                    if (pixelValue == 0 && !ccb.NoBlk)
                                    {
                                        pixelValue = 1;
                                    }
                                    (r, g, b, a) = ConvertToRgba(pixelValue, bpp);
                                }

                                for (int i = 0; i < count && pixelsInRow < width; i++)
                                {
                                    if (isTransparent)
                                    {
                                        transparencyMask[maskOffset] = true;
                                    }
                                    else
                                    {
                                        pixels[outputOffset++] = r;
                                        pixels[outputOffset++] = g;
                                        pixels[outputOffset++] = b;
                                        pixels[outputOffset++] = a;
                                        transparencyMask[maskOffset] = false;
                                    }

                                    maskOffset++;
                                    pixelsInRow++;
                                }
                            }
                            break;
                    }
                }

                bitReader.SeekToWord(nextRowWord);
            }

            bool hasTransparency = Array.Exists(transparencyMask, t => t);

            return new CelImageData
            {
                Width = width,
                Height = height,
                BitsPerPixel = 32, // RGBA32
                PixelData = pixels,
                TransparencyMask = hasTransparency ? transparencyMask : null
            };
        }

        /// <summary>
        /// Decodes UNCODED UNPACKED format: direct RGB values without packet encoding
        /// </summary>
        private static CelImageData DecodeUncodedUnpacked(byte[] data, CcbHeader ccb, int width, int height, int bpp, bool verbose)
        {
            if (verbose)
                Console.WriteLine($"[DecodeUncodedUnpacked] {width}x{height}, {bpp}bpp RGB");

            int bytesPerPixel = 4; // Store as RGBA32
            byte[] pixels = new byte[width * height * bytesPerPixel];
            
            var bitReader = new CelUnpacker.BitReader(data);

            for (int row = 0; row < height; row++)
            {
                bitReader.AlignToWord();
                
                for (int col = 0; col < width; col++)
                {
                    int pixelValue = bitReader.ReadBits(bpp);
                    
                    // Apply NOBLK for 000 values in uncoded formats
                    if (pixelValue == 0 && !ccb.NoBlk)
                    {
                        pixelValue = 1;
                    }
                    
                    var (r, g, b, a) = ConvertToRgba(pixelValue, bpp);
                    int offset = (row * width + col) * bytesPerPixel;
                    pixels[offset] = r;
                    pixels[offset + 1] = g;
                    pixels[offset + 2] = b;
                    pixels[offset + 3] = a;
                }
            }

            return new CelImageData
            {
                Width = width,
                Height = height,
                BitsPerPixel = 32, // RGBA32
                PixelData = pixels
            };
        }

        /// <summary>
        /// Converts uncoded pixel value to RGBA based on bits per pixel
        /// </summary>
        private static (byte r, byte g, byte b, byte a) ConvertToRgba(int pixelValue, int bpp)
        {
            if (bpp == 16)
            {
                // RGB555: 5R, 5G, 5B, 1 control bit
                int r = ((pixelValue >> 10) & 0x1F) << 3; // Scale 5 bits to 8 bits
                int g = ((pixelValue >> 5) & 0x1F) << 3;
                int b = (pixelValue & 0x1F) << 3;
                return ((byte)r, (byte)g, (byte)b, 255);
            }
            else // 8bpp
            {
                // RGB332: 3R, 3G, 2B
                int r = ((pixelValue >> 5) & 0x07) * 255 / 7; // Scale 3 bits to 8 bits
                int g = ((pixelValue >> 2) & 0x07) * 255 / 7;
                int b = (pixelValue & 0x03) * 255 / 3; // Scale 2 bits to 8 bits
                return ((byte)r, (byte)g, (byte)b, 255);
            }
        }

        /// <summary>
        /// Writes a pixel value to the output buffer
        /// </summary>
        private static void WritePixel(byte[] buffer, int offset, int value, int bitsPerPixel)
        {
            switch (bitsPerPixel)
            {
                case 1:
                case 2:
                case 4:
                case 6:
                case 8:
                    buffer[offset] = (byte)value;
                    break;

                case 16:
                    buffer[offset] = (byte)(value & 0xFF);
                    buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
                    break;
            }
        }
    }
}
