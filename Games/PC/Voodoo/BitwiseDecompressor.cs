using System;
using System.Collections.Generic;
using System.IO.Compression;

namespace VoodooAssetSystem
{
    /// <summary>
    /// C# implementation of the Voodoo game's bitwise decompression algorithm.
    /// Ghidra analysis confirms this is standard DEFLATE (RFC 1951).
    /// </summary>
    public static class BitwiseDecompressor
    {
        /// <summary>
        /// Decompresses DEFLATE-compressed data.
        /// </summary>
        /// <param name="compressedData">Input compressed byte array</param>
        /// <param name="expectedSize">Expected output size (optional)</param>
        /// <returns>Decompressed byte array, or null on error</returns>
        public static byte[] Decompress(byte[] compressedData, int expectedSize = 0)
        {
            if (compressedData == null || compressedData.Length == 0)
                return null;

            try
            {
                using (var input = new MemoryStream(compressedData))
                using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream(expectedSize > 0 ? expectedSize : compressedData.Length * 4))
                {
                    deflate.CopyTo(output);
                    return output.ToArray();
                }
            }
            catch (Exception)
            {
                // If standard DeflateStream fails, it might be because of zlib header (RFC 1950)
                // or raw DEFLATE (RFC 1951). DeflateStream usually expects raw DEFLATE.
                // If the data has a zlib header (0x78 0x9C etc), we might need to skip 2 bytes.

                // Try skipping 2 bytes (ZLIB header)
                if (compressedData.Length > 2 && compressedData[0] == 0x78)
                {
                    try
                    {
                        using (var input = new MemoryStream(compressedData, 2, compressedData.Length - 2))
                        using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                        using (var output = new MemoryStream(expectedSize > 0 ? expectedSize : compressedData.Length * 4))
                        {
                            deflate.CopyTo(output);
                            return output.ToArray();
                        }
                    }
                    catch
                    {
                        return null;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Decompresses data into a pre-allocated buffer.
        /// </summary>
        public static int DecompressToBuffer(byte[] compressedData, byte[] outputBuffer, int outputOffset = 0)
        {
            var result = Decompress(compressedData);
            if (result == null) return -1;

            if (outputOffset + result.Length > outputBuffer.Length)
                return -1;

            Array.Copy(result, 0, outputBuffer, outputOffset, result.Length);
            return result.Length;
        }
    }
}
