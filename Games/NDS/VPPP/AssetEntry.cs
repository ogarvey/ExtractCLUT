using System;

namespace ExtractCLUT.Games.NDS.VPPP
{
    /// <summary>
    /// A single entry in the Viva Piñata: Pocket Paradise <c>assets.bin</c> archive.
    ///
    /// Layout knowledge is derived from the game's loader/accessor code:
    ///   - <see cref="Assets_GetEntrySize"/> (FUN_02076a54) computes an entry's total size as
    ///     <c>offsets[index + 1] - offsets[index]</c>.
    ///   - The game's read path (FUN_0207610c / FUN_02076308) skips a fixed 0x10-byte header at
    ///     the start of every entry: payload size = <c>TotalSize - 0x10</c>, payload data begins
    ///     at <c>Offset + 0x10</c>.
    /// </summary>
    public sealed class AssetEntry
    {
        /// <summary>Per-entry header size the game always skips before the payload.</summary>
        public const int HeaderSize = 0x10;

        /// <summary>Asset id / key (the game stores these as u16, sorted ascending for binary search).</summary>
        public ushort Key { get; }

        /// <summary>Zero-based index of this entry within the directory.</summary>
        public int Index { get; }

        /// <summary>
        /// Start offset of the entry (including its 0x10 header). As parsed from the directory's
        /// offset array. Whether this is a file-absolute or data-region-relative offset is decided
        /// by <see cref="AssetDirectory"/> after validation.
        /// </summary>
        public long Offset { get; }

        /// <summary>Total entry size in bytes (header + payload).</summary>
        public long TotalSize { get; }

        /// <summary>Payload size in bytes (<see cref="TotalSize"/> minus the 0x10 header).</summary>
        public long PayloadSize => TotalSize - HeaderSize;

        /// <summary>Offset at which the payload begins (<see cref="Offset"/> + 0x10).</summary>
        public long PayloadOffset => Offset + HeaderSize;

        public AssetEntry(int index, ushort key, long offset, long totalSize)
        {
            Index = index;
            Key = key;
            Offset = offset;
            TotalSize = totalSize;
        }

        public override string ToString() =>
            $"[{Index}] key=0x{Key:X4} off=0x{Offset:X} size=0x{TotalSize:X} payload=0x{PayloadSize:X}";
    }
}
