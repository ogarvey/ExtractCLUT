using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace ExtractCLUT.Games.NDS.VPPP
{
    /// <summary>
    /// Parsed <c>assets.bin</c> directory.
    ///
    /// Verified in-RAM layout (from Assets_LoadDirectory / FUN_020769f4 and the accessors):
    /// <code>
    /// file 0x00 : u32   count (N)
    /// file 0x04 : u16   keys[N]            // asset ids, sorted ascending; binary-searched
    ///             pad to a 4-byte boundary -> the key region occupies ((N + 1) >> 1) * 4 bytes
    ///             u32   offsets[N + 1]      // entry i spans offsets[i]..offsets[i + 1]
    /// </code>
    /// The loader reads the u32 count, then allocates and reads <c>align4(N * 6 + 4)</c> bytes for
    /// the directory block. The size accessor (FUN_02076a54) locates the offset array at
    /// <c>keyBase + ((N + 1) >> 1) * 4</c>, proving the keys are u16 and the offsets are u32.
    /// </summary>
    public sealed class AssetDirectory
    {
        /// <summary>Number of directory entries (the u32 at file offset 0).</summary>
        public int Count { get; }

        /// <summary>Asset keys, in directory order (expected sorted ascending once decoded).</summary>
        public IReadOnlyList<ushort> Keys => _keys;

        /// <summary>
        /// Raw offset values, length <see cref="Count"/> + 1. Entry i spans
        /// <c>offsets[i]..offsets[i + 1]</c>.
        /// </summary>
        public IReadOnlyList<uint> Offsets => _offsets;

        /// <summary>The parsed entries.</summary>
        public IReadOnlyList<AssetEntry> Entries => _entries;

        /// <summary>
        /// True when the directory looks structurally sane: keys sorted ascending and offsets
        /// monotonically non-decreasing. Use this to verify a candidate codec against the real file.
        /// </summary>
        public bool IsValid => KeyInversions == 0 && NonMonotonicOffsets == 0;

        /// <summary>Number of out-of-order adjacent keys (0 means fully sorted).</summary>
        public int KeyInversions { get; }

        /// <summary>Number of offsets that decrease relative to the previous one (0 means monotonic).</summary>
        public int NonMonotonicOffsets { get; }

        private readonly ushort[] _keys;
        private readonly uint[] _offsets;
        private readonly AssetEntry[] _entries;

        /// <summary>Byte size of the directory block the game allocates: <c>align4(N * 6 + 4)</c>.</summary>
        public static long DirectoryBlockSize(int count) => ((long)count * 6 + 4 + 3) & ~3L;

        /// <summary>Byte size of the u16 key region (4-byte aligned): <c>((N + 1) >> 1) * 4</c>.</summary>
        public static int KeyRegionSize(int count) => ((count + 1) >> 1) * 4;

        /// <summary>
        /// Parse a decoded directory block.
        /// </summary>
        /// <param name="decodedDirectory">
        /// The decoded directory bytes (key array followed by the offset array) — i.e. the file
        /// bytes from offset 4 onward, after running them through an <see cref="IAssetDirectoryCodec"/>.
        /// </param>
        /// <param name="count">Entry count (u32 at file offset 0).</param>
        /// <param name="dataRegionBase">
        /// File offset of the entry data region (immediately after the count + directory block).
        /// Offsets are interpreted as data-region-relative when that yields valid in-bounds entries,
        /// otherwise as file-absolute.
        /// </param>
        /// <param name="fileLength">Total file length, used for bounds checks.</param>
        public AssetDirectory(ReadOnlySpan<byte> decodedDirectory, int count, long dataRegionBase, long fileLength)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            Count = count;
            _keys = new ushort[count];
            _offsets = new uint[count + 1];

            int keyRegion = KeyRegionSize(count);
            long need = keyRegion + (long)(count + 1) * 4;
            if (decodedDirectory.Length < need)
                throw new ArgumentException(
                    $"Decoded directory too small: need {need} bytes, got {decodedDirectory.Length}.",
                    nameof(decodedDirectory));

            // keys start at the very beginning of the decoded block (file offset 4)
            for (int i = 0; i < count; i++)
                _keys[i] = BinaryPrimitives.ReadUInt16LittleEndian(decodedDirectory.Slice(i * 2));

            var offsetBase = decodedDirectory.Slice(keyRegion);
            for (int i = 0; i <= count; i++)
                _offsets[i] = BinaryPrimitives.ReadUInt32LittleEndian(offsetBase.Slice(i * 4));

            // Structural validation
            int inv = 0;
            for (int i = 1; i < count; i++)
                if (_keys[i] < _keys[i - 1]) inv++;
            KeyInversions = inv;

            int nonMono = 0;
            for (int i = 1; i <= count; i++)
                if (_offsets[i] < _offsets[i - 1]) nonMono++;
            NonMonotonicOffsets = nonMono;

            // Decide how to interpret offsets: data-region-relative vs file-absolute.
            bool absoluteFits = OffsetsFit(_offsets, 0, fileLength);
            long entryBase = absoluteFits ? 0 : dataRegionBase;

            _entries = new AssetEntry[count];
            for (int i = 0; i < count; i++)
            {
                long start = entryBase + _offsets[i];
                long size = (long)_offsets[i + 1] - _offsets[i];
                _entries[i] = new AssetEntry(i, _keys[i], start, size);
            }
        }

        private static bool OffsetsFit(uint[] offsets, long bias, long fileLength)
        {
            for (int i = 0; i < offsets.Length; i++)
                if (bias + offsets[i] > fileLength)
                    return false;
            return true;
        }

        /// <summary>
        /// Binary search for an entry by key, mirroring the game's lookup (FUN_01ffc164 over the
        /// sorted u16 key array). Returns null if not found.
        /// </summary>
        public AssetEntry? FindByKey(ushort key)
        {
            int lo = 0, hi = Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                ushort k = _keys[mid];
                if (k == key) return _entries[mid];
                if (k < key) lo = mid + 1;
                else hi = mid - 1;
            }
            return null;
        }
    }
}
