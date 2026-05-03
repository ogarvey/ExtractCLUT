using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public static class AnemoneSpriteDecompressor
    {
        public static byte[] Decompress(byte[] compressedData, int width, int height, uint flags)
        {
            // Output buffer: 16-bit pixels (2 bytes per pixel)
            byte[] output = new byte[width * height * 2];

            // If flags is 0, it's raw data.
            if (flags == 0)
            {
                if (compressedData.Length > 0)
                {
                    int copyLength = Math.Min(compressedData.Length, output.Length);
                    Array.Copy(compressedData, output, copyLength);
                }

                // Convert 555 to 565 in-place
                Convert555To565(output, 0, output.Length);
                return output;
            }

            // Compressed format (Line-based RLE/Skip)
            using (var reader = new BinaryReader(new MemoryStream(compressedData)))
            {
                // Read Line Count (2 bytes)
                if (reader.BaseStream.Length < 2) return output;
                ushort lineCount = reader.ReadUInt16();

                // Process each line
                for (int i = 0; i < lineCount; i++)
                {
                    if (reader.BaseStream.Position >= reader.BaseStream.Length) break;

                    // Read Segment Count (1 byte)
                    byte segmentCount = reader.ReadByte();

                    // Calculate the start offset for this line in the output buffer
                    // Assuming lines correspond to Y rows.
                    int lineStartOffset = i * width * 2;
                    int currentLineOffset = 0; // Offset in bytes from the start of the line

                    for (int s = 0; s < segmentCount; s++)
                    {
                        if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) break;

                        // Read Skip (2 bytes) - Number of bytes to skip (transparent)
                        ushort skip = reader.ReadUInt16();

                        // Read Run Size (2 bytes) - Number of bytes of pixel data
                        ushort runSize = reader.ReadUInt16();

                        // Advance by Skip
                        currentLineOffset += skip;

                        // Check bounds
                        if (lineStartOffset + currentLineOffset + runSize > output.Length)
                        {
                            // Prevent buffer overflow
                            break;
                        }

                        // Read Pixels
                        if (reader.BaseStream.Position + runSize > reader.BaseStream.Length) break;
                        byte[] pixels = reader.ReadBytes(runSize);

                        // Copy to output
                        Array.Copy(pixels, 0, output, lineStartOffset + currentLineOffset, pixels.Length);

                        // Convert these pixels 555->565
                        //Convert555To565(output, lineStartOffset + currentLineOffset, runSize);

                        // Advance by Run Size
                        currentLineOffset += runSize;
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// Converts 16-bit pixels from RGB 555 to RGB 565 in-place.
        /// </summary>
        private static void Convert555To565(byte[] data, int offset, int length)
        {
            for (int i = 0; i < length; i += 2)
            {
                if (offset + i + 1 >= data.Length) break;

                ushort pixel = BitConverter.ToUInt16(data, offset + i);

                // Formula from decompilation: ((pixel & 0x7fe0) << 1) | (pixel & 0x1f)
                // 0x7FE0 = 0111 1111 1110 0000 (Bits 5-14)
                // Shifts Green and Red left by 1 to make room for the extra Green bit (which stays 0).
                ushort newPixel = (ushort)(((pixel & 0x7fe0) << 1) | (pixel & 0x1f));

                // Write back
                data[offset + i] = (byte)(newPixel & 0xFF);
                data[offset + i + 1] = (byte)(newPixel >> 8);
            }
        }

        /// <summary>
        /// Converts 16-bit pixels from RGB 555 to BGR 565 in-place.
        /// </summary>
        private static void Convert555ToBGR565(byte[] data, int offset, int length)
        {
            for (int i = 0; i < length; i += 2)
            {
                if (offset + i + 1 >= data.Length) break;

                ushort pixel = BitConverter.ToUInt16(data, offset + i);

                // Convert RGB555 to RGB565
                ushort rgb565 = (ushort)(((pixel & 0x7fe0) << 1) | (pixel & 0x1f));

                // Now convert RGB565 to BGR565
                ushort bgr565 = (ushort)(((rgb565 & 0xF800) >> 11) | (rgb565 & 0x07E0) | ((rgb565 & 0x001F) << 11));

                // Write back
                data[offset + i] = (byte)(bgr565 & 0xFF);
                data[offset + i + 1] = (byte)(bgr565 >> 8);
            }
        }
    }
}
