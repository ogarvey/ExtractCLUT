using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.ThreeDO.EoT
{
    /// <summary>
    /// Decompresses sprite data from Eye of Typhoon CHA files.
    /// The sprites use RLE compression designed for VGA Mode X (planar 4-bit graphics).
    /// </summary>
    public class CHASpriteDecompressor
    {
        /// <summary>
        /// Decompresses a single sprite from CHA file data.
        /// </summary>
        /// <param name="compressedData">The compressed sprite data (starting with width/height header)</param>
        /// <returns>Decompressed 8-bit indexed pixel data (0 = transparent)</returns>
        public static byte[] DecompressSprite(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length < 4)
                throw new ArgumentException("Invalid sprite data");

            using (var reader = new BinaryReader(new MemoryStream(compressedData)))
            {
                return DecompressSprite(reader);
            }
        }

        /// <summary>
        /// Decompresses a single sprite from a BinaryReader.
        /// </summary>
        /// <param name="reader">BinaryReader positioned at sprite header</param>
        /// <returns>Decompressed 8-bit indexed pixel data (0 = transparent)</returns>
        public static byte[] DecompressSprite(BinaryReader reader)
        {
            // Read sprite dimensions from header
            ushort width = reader.ReadUInt16();  // Width stored divided by 4 (for planar mode)
            ushort height = reader.ReadUInt16();
            reader.ReadBytes(6);
            int totalPixels = width * height;

            byte[] outputBuffer = new byte[totalPixels];
            int pixelIndex = 0;

            // Decompress RLE data
            while (pixelIndex < totalPixels)
            {
                byte controlByte = reader.ReadByte();

                if (controlByte < 0x80)
                {
                    // Single literal pixel
                    // Low nibble (0x0F) contains the palette index
                    outputBuffer[pixelIndex++] = controlByte;
                }
                else
                {
                    // Run of pixels
                    byte runLength = (byte)(controlByte & 0x7F);  // Bits 0-6 contain run length
                    byte pixelValue = reader.ReadByte();

                    // Write the run
                    for (int i = 0; i < runLength && pixelIndex < totalPixels; i++)
                    {
                        outputBuffer[pixelIndex++] = pixelValue;
                    }
                }
            }

            return outputBuffer;
        }

        /// <summary>
        /// Decompresses a sprite and returns it with dimension information.
        /// </summary>
        public static SpriteData DecompressSpriteWithInfo(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length < 4)
                throw new ArgumentException("Invalid sprite data");

            using (var ms = new MemoryStream(compressedData))
            using (var reader = new BinaryReader(ms))
            {
                ushort width = reader.ReadUInt16();
                ushort height = reader.ReadUInt16();

                // Reset to beginning to read full sprite
                ms.Position = 0;
                byte[] pixels = DecompressSprite(reader);

                return new SpriteData
                {
                    Width = width,
                    Height = height,
                    Pixels = pixels
                };
            }
        }

        /// <summary>
        /// Extracts individual sprites from a CHA file using a PID (Picture Index Data) file.
        /// </summary>
        /// <param name="chaData">Complete CHA file data</param>
        /// <param name="pidData">Complete PID file data (array of DWORDs indicating sprite sizes)</param>
        /// <returns>Array of decompressed sprites</returns>
        public static SpriteData[] ExtractAllSprites(byte[] chaData, byte[] pidData)
        {
            if (chaData == null || pidData == null)
                throw new ArgumentException("Invalid file data");

            using (var pidReader = new BinaryReader(new MemoryStream(pidData)))
            {
                // First DWORD in PID is often special/header, read and skip it
                uint firstEntry = pidReader.ReadUInt32();

                // Calculate number of sprites (remaining bytes / 4)
                int spriteCount = (int)((pidData.Length - 4) / 4);
                var sprites = new SpriteData[spriteCount];

                int currentOffset = 0;

                for (int i = 0; i < spriteCount; i++)
                {
                    uint spriteSize = pidReader.ReadUInt32();

                    if (currentOffset + spriteSize > chaData.Length)
                        break;

                    // Extract sprite data
                    byte[] spriteData = new byte[spriteSize];
                    Array.Copy(chaData, currentOffset, spriteData, 0, spriteSize);

                    // Decompress
                    sprites[i] = DecompressSpriteWithInfo(spriteData);

                    currentOffset += (int)spriteSize;
                }

                return sprites;
            }
        }
    }

    /// <summary>
    /// Represents a decompressed sprite with dimension information.
    /// </summary>
    public class SpriteData
    {
        /// <summary>Width in pixels</summary>
        public int Width { get; set; }

        /// <summary>Height in pixels</summary>
        public int Height { get; set; }

        /// <summary>8-bit indexed pixel data (0 = transparent, 1-15 = palette indices)</summary>
        public byte[] Pixels { get; set; }
        private static byte[] ConvertPlanarToLinear(byte[] planarData, int width, int height)
        {
            if (height % 4 != 0)
                throw new ArgumentException("Height must be divisible by 4 for planar mode");

            int totalPixels = width * height;
            byte[] linearData = new byte[totalPixels];
            int rowsPerPlane = height / 4;  // Each plane contains height/4 rows
            int bytesPerRow = width;

            // Interleave the 4 planes
            for (int plane = 0; plane < 4; plane++)
            {
                int planeOffset = plane * rowsPerPlane * bytesPerRow;

                for (int row = 0; row < rowsPerPlane; row++)
                {
                    int srcOffset = planeOffset + (row * bytesPerRow);
                    int dstRow = (row * 4) + plane;  // Interleave: plane 0 row 0 -> output row 0, plane 1 row 0 -> output row 1, etc.
                    int dstOffset = dstRow * bytesPerRow;

                    Array.Copy(planarData, srcOffset, linearData, dstOffset, bytesPerRow);
                }
            }

            return linearData;
        }
        /// <summary>
        /// Loads all animations from IDX and ACT files.
        /// </summary>

        /// <summary>
        /// Converts the sprite to a simple text representation for debugging.
        /// </summary>
        public string ToDebugString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Sprite: {Width}x{Height}");

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    byte pixel = Pixels[y * Width + x];
                    sb.Append(pixel == 0 ? "." : pixel.ToString("X"));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Saves the sprite as a raw 8-bit indexed bitmap.
        /// </summary>
        public void SaveAsRaw(string filename)
        {
            File.WriteAllBytes(filename, Pixels);
        }

        public byte[] GetPixels()
        {
            return ConvertPlanarToLinear(Pixels, Height, Width);
        }
    }

}
