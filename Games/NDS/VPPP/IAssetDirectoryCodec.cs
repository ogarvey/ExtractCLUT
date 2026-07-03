using System;

namespace ExtractCLUT.Games.NDS.VPPP
{
    /// <summary>
    /// Decoder seam for the on-disk <c>assets.bin</c> directory block.
    ///
    /// IMPORTANT — current status of the reverse engineering effort:
    /// The physical <c>assets.bin</c> shipped with the game is ENCODED. This was proven empirically
    /// against the real ~37&#160;MB file:
    ///   * the u16 key array is not sorted (≈1900 inversions; the game binary-searches it, so it
    ///     MUST be sorted once decoded);
    ///   * the u32 offset array is non-monotonic and mostly out of bounds;
    ///   * directory/data entropy is ≈7.1–7.3 bits/byte (compression territory, not plain data, and
    ///     too high for a simple XOR cipher which preserves entropy);
    ///   * standard Nintendo codecs (LZ10/LZ11) fail immediately and a 480-variant byte-LZSS
    ///     brute force found no valid decode;
    ///   * the on-disk byte at offset 4 is 0x70, not a stock NDS compression tag.
    ///
    /// The decode is performed at runtime by a custom NitroSDK archive read-proc (dispatched via
    /// <c>archive+0x54</c> in FUN_0208db28). That proc pointer is only assigned at runtime, so the
    /// exact algorithm still has to be recovered. Until then, no codec here can produce valid data.
    ///
    /// Implement this interface once the codec is identified; <see cref="AssetDirectory"/> exposes
    /// validation so a candidate codec can be verified directly against the real file.
    /// </summary>
    public interface IAssetDirectoryCodec
    {
        /// <summary>
        /// Decode the on-disk directory block into the in-RAM layout the game uses:
        /// <c>[u16 keys[count]] [pad to 4] [u32 offsets[count + 1]]</c>.
        /// </summary>
        /// <param name="raw">
        /// The raw bytes read from the file for the directory region. The caller passes the bytes
        /// starting at file offset 4 (immediately after the u32 count).
        /// </param>
        /// <param name="count">Entry count (the u32 at file offset 0).</param>
        /// <returns>The decoded directory bytes (key array followed by offset array).</returns>
        byte[] DecodeDirectory(ReadOnlySpan<byte> raw, int count);
    }

    /// <summary>
    /// Pass-through codec: returns the raw bytes unchanged.
    ///
    /// This is the default so the rest of the pipeline can be exercised, but it WILL produce an
    /// invalid directory for the shipped (encoded) <c>assets.bin</c>. Check
    /// <see cref="AssetDirectory.IsValid"/> after parsing to confirm whether a real codec is needed.
    /// </summary>
    public sealed class RawDirectoryCodec : IAssetDirectoryCodec
    {
        public byte[] DecodeDirectory(ReadOnlySpan<byte> raw, int count) => raw.ToArray();
    }
}
