using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public static class DuneHSQUnpacker
    {
        public static byte[] Unpack2(byte[] src)
        {
            // Verify checksum
            if (((src[0] + src[1] + src[2] + src[3] + src[4] + src[5]) & 0xFF) != 171)
                return Array.Empty<byte>(); // Checksum failed

            int q = 1;
            int srcIndex = 6; // Start after checksum
            List<byte> dst = new List<byte>();

            while (true)
            {
                if (GetBit(ref q, src, ref srcIndex) != 0)
                {
                    // Copy literal byte
                    if (srcIndex >= src.Length)
                        throw new Exception("Unexpected end of input");

                    dst.Add(src[srcIndex++]);
                }
                else
                {
                    int count;
                    int offset;

                    if (GetBit(ref q, src, ref srcIndex) != 0)
                    {
                        if (srcIndex + 1 >= src.Length)
                            throw new Exception("Unexpected end of input");

                        count = src[srcIndex] & 7;
                        ushort wordValue = (ushort)(src[srcIndex] | (src[srcIndex + 1] << 8));
                        offset = unchecked((int)0xFFFFE000) | (wordValue >> 3);
                        srcIndex += 2;

                        if (count == 0)
                        {
                            if (srcIndex >= src.Length)
                                throw new Exception("Unexpected end of input");

                            count = src[srcIndex++];
                        }

                        if (count == 0)
                            return dst.ToArray(); // Decompression completed
                    }
                    else
                    {
                        count = GetBit(ref q, src, ref srcIndex) << 1;
                        count |= GetBit(ref q, src, ref srcIndex);

                        if (srcIndex >= src.Length)
                            throw new Exception("Unexpected end of input");

                        offset = unchecked((int)0xFFFFFF00) | src[srcIndex++];
                    }

                    count += 2;

                    int dmIndex = dst.Count + offset;

                    if (dmIndex < 0 || dmIndex >= dst.Count)
                        throw new Exception("Invalid offset in decompression");

                    for (int i = 0; i < count; i++)
                    {
                        dst.Add(dst[dmIndex++]);
                    }
                }
            }
        }

        private static int GetBit(ref int q, byte[] src, ref int srcIndex)
        {
            if (q == 1)
            {
                if (srcIndex + 1 >= src.Length)
                    throw new Exception("Unexpected end of input");

                q = 0x10000 | (src[srcIndex] | (src[srcIndex + 1] << 8));
                srcIndex += 2;
            }

            int bit = q & 1;
            q >>= 1;
            return bit;
        }

        /// <summary>
        /// Reads and decodes image data from a binary file.
        /// </summary>
        /// <param name="filePath">The path to the binary file containing the image data.</param>
        /// <returns>
        /// A tuple containing two byte arrays:
        /// - The first array is the palette data in RGB format (768 bytes for 256 colors).
        /// - The second array is the decoded image data where each byte represents a palette index.
        /// </returns>
        public static (byte[] palette, List<(int width, int height, byte[] imageData)>) ReadImages(string filePath, bool enableTransparency = false)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                // Read offset_A (2 bytes)
                ushort offset_A = br.ReadUInt16();
                int offsetCurrent = 2; // Bytes read so far

                // Initialize palette (256 colors * 3 bytes per color)
                byte[] palette = new byte[256 * 3];

                if (offset_A != 2)
                {
                    // Read palette data
                    while (true)
                    {
                        byte startColorIndex = br.ReadByte();
                        byte numColors = br.ReadByte();
                        offsetCurrent += 2;

                        // End of palette data
                        if (startColorIndex == 0xFF && numColors == 0xFF)
                            break;

                        for (int i = 0; i < numColors; i++)
                        {
                            // Read RGB components and shift left by 2 bits
                            byte r = (byte)(br.ReadByte() << 2);
                            byte g = (byte)(br.ReadByte() << 2);
                            byte b = (byte)(br.ReadByte() << 2);
                            offsetCurrent += 3;

                            int index = (startColorIndex + i) * 3;
                            palette[index] = r;
                            palette[index + 1] = g;
                            palette[index + 2] = b;
                        }
                    }
                }
                else
                {
                    // Default grayscale palette
                    for (int i = 0; i < 256; i++)
                    {
                        palette[i * 3] = (byte)i;
                        palette[i * 3 + 1] = (byte)i;
                        palette[i * 3 + 2] = (byte)i;
                    }
                }

                // Handle transparency in the palette
                if (enableTransparency)
                {
                    // Set palette index 0 to magenta (RGB(255, 0, 255))
                    palette[0] = 255;   // Red
                    palette[1] = 0;     // Green
                    palette[2] = 255;   // Blue
                }

                // Read image offsets
                fs.Seek(offset_A, SeekOrigin.Begin);
                List<ushort> offset_B = new List<ushort>();
                offsetCurrent = offset_A;

                // Read the first offset
                ushort firstOffset = br.ReadUInt16();
                offset_B.Add(firstOffset);
                offsetCurrent += 2;

                // Read remaining offsets until reaching the first image data position
                while (offsetCurrent < offset_A + offset_B[0])
                {
                    ushort imageOffset = br.ReadUInt16();
                    offset_B.Add(imageOffset);
                    offsetCurrent += 2;
                }

                // List to hold the images
                var images = new List<(int width, int height, byte[] imageData)>();

                // For each image
                for (int i = 0; i < offset_B.Count; i++)
                {
                    // Seek to the image data position
                    fs.Seek(offset_A + offset_B[i], SeekOrigin.Begin);

                    // Read image header
                    byte size_x_read = br.ReadByte();
                    byte compressionByte = br.ReadByte();
                    byte size_y = br.ReadByte();
                    byte offset_pal = br.ReadByte();

                    // Compute image dimensions
                    uint size_x = (uint)(size_x_read + ((compressionByte & 0x7F) << 8));
                    if (size_x == 0)
                        break; // No more images

                    bool compressed = (compressionByte & 0x80) != 0;

                    // Read unknown bytes if offset_A == 2
                    if (offset_A == 2)
                    {
                        br.ReadByte(); // unknown1
                        br.ReadByte(); // unknown2
                    }

                    int imageWidth = (int)size_x;
                    int imageHeight = size_y;
                    byte[] imageData = new byte[imageWidth * imageHeight];

                    int line = 0;
                    int column = 0;
                    int word = 0;

                    if (compressed)
                    {
                        // Compressed image data
                        while (line < imageHeight)
                        {
                            if (fs.Position >= fs.Length)
                                break;

                            sbyte repetition = br.ReadSByte();

                            // If repetition counter is negative, repeat the next byte (-repetition + 1) times
                            if (repetition < 0)
                            {
                                byte bipixel = br.ReadByte();
                                int repeatCount = (-repetition) + 1;

                                for (int j = 0; j < repeatCount; j++)
                                {
                                    // Write two pixels per bipixel
                                    WritePixel(imageData, imageWidth, imageHeight, line, ref column, offset_pal, bipixel, enableTransparency, ref word);
                                }
                            }
                            else
                            {
                                // Read and write (repetition + 1) bipixels
                                int count = repetition + 1;
                                for (int j = 0; j < count; j++)
                                {
                                    byte bipixel = br.ReadByte();
                                    WritePixel(imageData, imageWidth, imageHeight, line, ref column, offset_pal, bipixel, enableTransparency, ref word);
                                }
                            }

                            // Align to 4-byte boundaries if necessary
                            if (column >= imageWidth)
                            {
                                column = 0;
                                line++;
                                int skipBytes = (word % 4) != 0 ? 4 - (word % 4) : 0;
                                fs.Seek(skipBytes, SeekOrigin.Current);
                                word = 0;
                            }
                        }
                    }
                    else
                    {
                        // Uncompressed image data
                        while (line < imageHeight)
                        {
                            if (fs.Position >= fs.Length)
                                break;

                            byte bipixel = br.ReadByte();
                            byte bipixel2 = br.ReadByte();

                            // Write four pixels from two bipixels
                            WritePixel(imageData, imageWidth, imageHeight, line, ref column, offset_pal, bipixel, enableTransparency);
                            WritePixel(imageData, imageWidth, imageHeight, line, ref column, offset_pal, bipixel2, enableTransparency);

                            // Move to the next line if necessary
                            if (column >= imageWidth)
                            {
                                column = 0;
                                line++;
                            }
                        }
                    }

                    // Add the image to the list
                    images.Add((imageWidth, imageHeight, imageData));
                }

                return (palette, images);
            }
        }

        /// <summary>
        /// Writes pixels to the image data array from a bipixel value, handling transparency if enabled.
        /// </summary>
        private static void WritePixel(byte[] imageData, int width, int height, int line, ref int column, byte offset_pal, byte bipixel, bool enableTransparency, ref int word)
        {
            // First pixel (lower 4 bits)
            byte pixelValue = (byte)((bipixel & 0x0F) + offset_pal);
            if (enableTransparency && (bipixel & 0x0F) == 0)
                pixelValue = 0; // Use transparent color index

            if (line < height && column < width)
            {
                imageData[line * width + column] = pixelValue;
            }
            column++;
            word++;

            // Second pixel (upper 4 bits)
            pixelValue = (byte)((bipixel >> 4) + offset_pal);
            if (enableTransparency && (bipixel >> 4) == 0)
                pixelValue = 0; // Use transparent color index

            if (line < height && column < width)
            {
                imageData[line * width + column] = pixelValue;
            }
            column++;
            word++;
        }

        /// <summary>
        /// Writes pixels to the image data array from a bipixel value, handling transparency if enabled.
        /// Overloaded method without 'word' parameter for uncompressed data.
        /// </summary>
        private static void WritePixel(byte[] imageData, int width, int height, int line, ref int column, byte offset_pal, byte bipixel, bool enableTransparency)
        {
            // First pixel (lower 4 bits)
            byte pixelValue = (byte)((bipixel & 0x0F) + offset_pal);
            if (enableTransparency && (bipixel & 0x0F) == 0)
                pixelValue = 0; // Use transparent color index

            if (line < height && column < width)
            {
                imageData[line * width + column] = pixelValue;
            }
            column++;

            // Second pixel (upper 4 bits)
            pixelValue = (byte)((bipixel >> 4) + offset_pal);
            if (enableTransparency && (bipixel >> 4) == 0)
                pixelValue = 0; // Use transparent color index

            if (line < height && column < width)
            {
                imageData[line * width + column] = pixelValue;
            }
            column++;
        }
    }

}
