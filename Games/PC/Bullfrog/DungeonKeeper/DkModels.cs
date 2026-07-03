using System.Collections.Generic;

namespace ExtractCLUT.Games.PC.Bullfrog.DungeonKeeper
{
    /// <summary>
    /// A single record from a Dungeon Keeper "picture table" (.tab) file.
    /// Each record is 6 bytes long and points at a chunk within the matching
    /// picture data (.dat) file.
    /// </summary>
    public class DkPictureEntry
    {
        /// <summary>Offset of the picture within the data file (bytes 0-3, little-endian).</summary>
        public uint Offset { get; set; }

        /// <summary>Horizontal size of the picture (byte 4).</summary>
        public byte Width { get; set; }

        /// <summary>Vertical size of the picture (byte 5).</summary>
        public byte Height { get; set; }

        /// <summary>True when the record has no pixel data (a placeholder/count entry).</summary>
        public bool IsEmpty => Width == 0 || Height == 0;
    }

    /// <summary>
    /// A single 16-byte "KeepSprite" element record from a Dungeon Keeper sprite
    /// table (the table the game indexes as <c>DAT_005e6210</c>; it accompanies a
    /// <c>creature.jty</c> / <c>.dat</c> pixel-data file).
    ///
    /// The layout below was recovered from the game binary:
    ///   * <c>FUN_0043c4f0</c> - sprite loader (bytes 0-3 are a data offset; the data
    ///     size of a record is the next record's offset minus this one's)
    ///   * <c>FUN_00454930</c> - draw-metadata getter (width/height/offset/origin fields)
    ///   * <c>FUN_0043c3e0</c> - group loader (bytes 8-9 = type and frame count)
    ///   * <c>FUN_00453ba0</c> - blitter (bytes 12-15 = signed origins)
    ///
    /// There is NO 32-bit "animation number" (an older fan-page misreading). Frames
    /// are grouped by walking consecutive records, using <see cref="Type"/> and
    /// <see cref="FrameCount"/> to size each group.
    /// </summary>
    public class DkAnimationEntry
    {
        /// <summary>Offset of this frame's RLE pixel data within the data file (bytes 0-3).
        /// The data size equals the next record's offset minus this one's.</summary>
        public uint DataOffset { get; set; }

        /// <summary>Bitmap width of this frame's RLE pixel data (byte 4). Used for both
        /// static and directional sprites.</summary>
        public byte Width { get; set; }

        /// <summary>Bitmap height of this frame's RLE pixel data (byte 5). Used for both
        /// static and directional sprites.</summary>
        public byte Height { get; set; }

        /// <summary>Horizontal anchor/bounding extent (byte 6). Used by the game for
        /// draw positioning (e.g. horizontal-flip alignment), not for decoding pixels.</summary>
        public byte Width2 { get; set; }

        /// <summary>Vertical anchor/bounding extent (byte 7).</summary>
        public byte Height2 { get; set; }

        /// <summary>Sprite type / group-header flag (byte 8): 0 = static, 2 = directional
        /// (5 facings stored, 3 mirrored). Only meaningful on a group's first record.</summary>
        public byte Type { get; set; }

        /// <summary>Frame count (byte 9). Type 0: total frames. Type 2: frames per
        /// direction (total records = FrameCount * 5). Only meaningful on a group header.</summary>
        public byte FrameCount { get; set; }

        /// <summary>Horizontal draw offset of this frame (byte 10).</summary>
        public byte OffsetX { get; set; }

        /// <summary>Vertical draw offset of this frame (byte 11).</summary>
        public byte OffsetY { get; set; }

        /// <summary>Signed X-origin, added to the draw position (bytes 12-13).</summary>
        public short OriginX { get; set; }

        /// <summary>Signed Y-origin, added to the draw position (bytes 14-15).</summary>
        public short OriginY { get; set; }

        /// <summary>True when this group header describes a directional (8-facing) sprite.</summary>
        public bool IsDirectional => Type == 2;

        /// <summary>Number of records this group spans, when this record is a group header.</summary>
        public int GroupRecordCount => Type == 2 ? FrameCount * 5 : FrameCount;

        /// <summary>True when the record carries no pixel dimensions (e.g. a placeholder).</summary>
        public bool IsEmpty => Width == 0 && Height == 0 && Width2 == 0 && Height2 == 0;
    }

    /// <summary>
    /// A decoded Dungeon Keeper sprite: palette indices plus a per-pixel opacity
    /// mask (transparent pixels are the run-length "skip" runs in the source data).
    /// </summary>
    public class DkSprite
    {
        public int Width { get; }
        public int Height { get; }

        /// <summary>Palette index for each pixel, row-major (length = Width * Height).</summary>
        public byte[] Pixels { get; }

        /// <summary>True where a pixel is visible, false where it is transparent.</summary>
        public bool[] Opaque { get; }

        public DkSprite(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height];
            Opaque = new bool[width * height];
        }
    }
}
