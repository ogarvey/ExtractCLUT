using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.JackOrlando
{
    /// <summary>
    /// PBM (Compressed Bitmap) Decompressor for Jack Orlando
    /// Based on reverse engineering of decompressPbmRleData function at 0x0046c7a0
    /// 
    /// PBM Format Structure:
    /// - uint16 width (image width in pixels)
    /// - uint16 height (image height in pixels) 
    /// - byte[] compressedData (RLE compressed RGB16 pixel data)
    /// 
    /// RLE Algorithm:
    /// - 0xFF byte = control marker, followed by 2-byte control word
    /// - Control word: [repeatValue:8][repeatCount:2][scale:6]  
    /// - Non-0xFF bytes = literal pixel data
    /// 
    /// Expected Output: RGB16 pixel array (width * height * 2 bytes)
    /// </summary>
    public static class PbmDecompressor
    {
        /// <summary>
        /// Decompresses a PBM file loaded from Jack Orlando's PAK archives
        /// </summary>
        /// <param name="pbmData">Raw PBM data from loadPbmAsset</param>
        /// <returns>Decompressed RGB16 pixel data or null on error</returns>
        public static PbmImage? DecompressPbm(byte[] pbmData)
        {
            if (pbmData == null || pbmData.Length < 4)
                return null;

            try
            {
                using (var stream = new MemoryStream(pbmData))
                using (var reader = new BinaryReader(stream))
                {
                    // Read PBM header
                    ushort width = reader.ReadUInt16();
                    ushort height = reader.ReadUInt16();

                    if (width == 0 || height == 0 || width > 4096 || height > 4096)
                        return null;

                    // Calculate expected output size
                    int expectedPixels = width * height;
                    int expectedBytes = expectedPixels * 2; // RGB16 = 2 bytes per pixel

                    // Remaining data is compressed
                    byte[] compressedData = reader.ReadBytes((int)(stream.Length - stream.Position));

                    // Try different decompression methods
                    byte[] decompressedPixels = TryDecompression(compressedData, expectedBytes);

                    if (decompressedPixels != null && decompressedPixels.Length == expectedBytes)
                    {
                        return new PbmImage
                        {
                            Width = width,
                            Height = height,
                            Rgb16Data = decompressedPixels
                        };
                    }

                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static byte[] TryDecompression(byte[] compressedData, int expectedSize)
        {
            // Try RLE decompression (most common for bitmaps)
            byte[] result = TryRleDecompression(compressedData, expectedSize);
            if (result != null) return result;

            return Array.Empty<byte>();
        }

        private static byte[] TryRleDecompression(byte[] compressedData, int expectedSize)
        {
            try
            {
                var output = new List<byte>();
                int pos = 0;

                // Implement Jack Orlando's RLE algorithm (from decompressPbmRleData @ 0x0046c840)
                while (pos < compressedData.Length && output.Count < expectedSize)
                {
                    byte currentByte = compressedData[pos++];

                    if (currentByte == 0xFF) // Control byte for RLE
                    {
                        if (pos + 1 >= compressedData.Length) break;

                        // Read 2-byte control word (little endian)
                        ushort controlWord = (ushort)(compressedData[pos] | (compressedData[pos + 1] << 8));
                        pos += 2;

                        byte repeatValue = (byte)(controlWord & 0xFF); // Lower 8 bits = byte to repeat
                        int repeatCount = (controlWord >> 8) & 0xFF;   // Upper 8 bits = repeat count

                        // Repeat the byte value
                        for (int i = 0; i < repeatCount && output.Count < expectedSize; i++)
                        {
                            output.Add(repeatValue);
                        }
                    }
                    else // Literal byte
                    {
                        output.Add(currentByte);
                    }
                }
                return output.Count == expectedSize ? output.ToArray() : Array.Empty<byte>();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }


        /// <summary>
        /// Converts RGB16 data to RGB24 for easier viewing/processing
        /// </summary>
        public static byte[] ConvertRgb16ToRgb24(byte[] rgb16Data)
        {
            if (rgb16Data == null || rgb16Data.Length % 2 != 0)
                return Array.Empty<byte>();

            byte[] rgb24 = new byte[rgb16Data.Length / 2 * 3];
            int outPos = 0;

            for (int i = 0; i < rgb16Data.Length; i += 2)
            {
                ushort rgb16 = (ushort)(rgb16Data[i] | (rgb16Data[i + 1] << 8));

                // Extract RGB components (assuming 5-6-5 format)
                byte r = (byte)((rgb16 & 0xF800) >> 8); // Top 5 bits -> 8 bits
                byte g = (byte)((rgb16 & 0x07E0) >> 3); // Middle 6 bits -> 8 bits  
                byte b = (byte)((rgb16 & 0x001F) << 3); // Bottom 5 bits -> 8 bits

                rgb24[outPos++] = r;
                rgb24[outPos++] = g;
                rgb24[outPos++] = b;
            }

            return rgb24;
        }
    }

    /// <summary>
    /// Represents a decompressed PBM image
    /// </summary>
    public class PbmImage
    {
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public byte[] Rgb16Data { get; set; }

        /// <summary>
        /// Gets the image data as RGB24 for easier processing
        /// </summary>
        public byte[] GetRgb24Data()
        {
            return PbmDecompressor.ConvertRgb16ToRgb24(Rgb16Data);
        }
    }
}
