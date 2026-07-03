using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.Bullfrog.DungeonKeeper
{
    /// <summary>
    /// Helper for extracting Dungeon Keeper graphics assets.
    ///
    /// Dungeon Keeper stores graphics as a set of cooperating files:
    ///   * .pal - a palette (256 RGB triples, each component 0-63)
    ///   * .tab - a table of records describing each picture/frame
    ///   * .dat - run-length encoded pixel data, indexed by the table offsets
    ///
    /// Two table flavours exist: 6-byte "picture table" records (stills) and
    /// 16-byte "animation table" records (animation frames). Both index into a
    /// .dat file using the same RLE pixel format.
    ///
    /// Format reference: https://jonskeet.uk/dk/graphics.html
    /// </summary>
    public static class DK
    {
        private const int PictureRecordSize = 6;
        private const int AnimationRecordSize = 16;

        #region Palette

        /// <summary>
        /// Reads a Dungeon Keeper palette file (256 RGB triples, components 0-63).
        /// Each component is scaled up to the full 0-255 range.
        /// </summary>
        public static List<Rgba32> ReadPaletteFile(string path)
            => ReadPalette(File.ReadAllBytes(path));

        /// <summary>
        /// Converts raw palette bytes (256 triples of 6-bit R,G,B) into 32-bit colours.
        /// Components are stored 0-63; they are scaled to 0-255 (value * 4, clamped).
        /// </summary>
        public static List<Rgba32> ReadPalette(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var palette = new List<Rgba32>(256);
            int count = Math.Min(256, data.Length / 3);
            for (int i = 0; i < count; i++)
            {
                int o = i * 3;
                palette.Add(new Rgba32(Scale6(data[o]), Scale6(data[o + 1]), Scale6(data[o + 2])));
            }

            // Pad to a full 256-entry palette so stray indices never throw.
            while (palette.Count < 256)
                palette.Add(new Rgba32(0, 0, 0));

            return palette;
        }

        private static byte Scale6(byte v) => (byte)Math.Min(255, v * 4);

        #endregion

        #region Tables

        /// <summary>Reads a 6-byte-per-record picture table (.tab) file.</summary>
        public static List<DkPictureEntry> ReadPictureTableFile(string path)
            => ReadPictureTable(File.ReadAllBytes(path));

        /// <summary>
        /// Parses a picture table. The first record is a placeholder whose "offset"
        /// field actually holds the number of entries; it carries no pixel data and
        /// is skipped.
        /// </summary>
        public static List<DkPictureEntry> ReadPictureTable(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var entries = new List<DkPictureEntry>();
            int recordCount = data.Length / PictureRecordSize;
            for (int i = 0; i < recordCount; i++)
            {
                int o = i * PictureRecordSize;
                entries.Add(new DkPictureEntry
                {
                    Offset = ReadUInt32(data, o),
                    Width = data[o + 4],
                    Height = data[o + 5],
                });
            }

            // The first record is the count placeholder (width/height == 0).
            if (entries.Count > 0 && entries[0].IsEmpty)
                entries.RemoveAt(0);

            return entries;
        }

        /// <summary>Reads a 16-byte-per-record animation table (.tab) file.</summary>
        public static List<DkAnimationEntry> ReadAnimationTableFile(string path)
            => ReadAnimationTable(File.ReadAllBytes(path));

        /// <summary>
        /// Parses an animation table. As with picture tables the leading placeholder
        /// record (width/height == 0) is skipped.
        /// </summary>
        public static List<DkAnimationEntry> ReadAnimationTable(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var entries = new List<DkAnimationEntry>();
            int recordCount = data.Length / AnimationRecordSize;
            for (int i = 0; i < recordCount; i++)
            {
                int o = i * AnimationRecordSize;
                entries.Add(new DkAnimationEntry
                {
                    DataOffset = ReadUInt32(data, o),
                    Width = data[o + 4],
                    Height = data[o + 5],
                    Width2 = data[o + 6],
                    Height2 = data[o + 7],
                    Type = data[o + 8],
                    FrameCount = data[o + 9],
                    OffsetX = data[o + 10],
                    OffsetY = data[o + 11],
                    OriginX = ReadInt16(data, o + 12),
                    OriginY = ReadInt16(data, o + 14),
                });
            }

            if (entries.Count > 0 && entries[0].IsEmpty)
                entries.RemoveAt(0);

            return entries;
        }

        #endregion

        #region Sprite decoding

        /// <summary>
        /// Decodes a single sprite from the RLE picture/animation data (.dat).
        ///
        /// The stream at <paramref name="offset"/> is a series of rows. Each row is a
        /// run of control bytes read as signed:
        ///   * positive n : n opaque pixels follow, one palette index byte each
        ///   * negative n : skip |n| transparent pixels
        ///   * zero       : end of the current row
        /// </summary>
        public static DkSprite DecodeSprite(byte[] data, long offset, int width, int height)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (width <= 0 || height <= 0) return new DkSprite(Math.Max(0, width), Math.Max(0, height));

            var sprite = new DkSprite(width, height);
            long pos = offset;
            int row = 0;
            int col = 0;

            while (row < height && pos < data.Length)
            {
                sbyte control = (sbyte)data[pos++];

                if (control == 0)
                {
                    // End of row.
                    col = 0;
                    row++;
                }
                else if (control < 0)
                {
                    // Transparent run.
                    col += -control;
                }
                else
                {
                    // Opaque run of palette indices.
                    for (int i = 0; i < control; i++)
                    {
                        if (pos >= data.Length) break;
                        byte index = data[pos++];
                        if (row < height && col < width)
                        {
                            int p = row * width + col;
                            sprite.Pixels[p] = index;
                            sprite.Opaque[p] = true;
                        }
                        col++;
                    }
                }
            }

            return sprite;
        }

        /// <summary>
        /// Renders a decoded sprite to an ImageSharp image sized exactly to the sprite.
        /// Transparent pixels are written with a fully transparent alpha; opaque pixels
        /// use the palette.
        /// </summary>
        public static Image<Rgba32> ToImage(DkSprite sprite, IReadOnlyList<Rgba32> palette)
        {
            if (sprite == null) throw new ArgumentNullException(nameof(sprite));
            return ToImage(sprite, palette, sprite.Width, sprite.Height, 0, 0);
        }

        /// <summary>
        /// Renders a decoded sprite onto a fixed-size transparent canvas, placing its
        /// top-left corner at (<paramref name="offsetX"/>, <paramref name="offsetY"/>).
        /// Used to give every frame of an animation the same dimensions with each frame
        /// positioned correctly within the shared canvas.
        /// </summary>
        public static Image<Rgba32> ToImage(DkSprite sprite, IReadOnlyList<Rgba32> palette,
            int canvasWidth, int canvasHeight, int offsetX, int offsetY)
        {
            if (sprite == null) throw new ArgumentNullException(nameof(sprite));
            if (palette == null) throw new ArgumentNullException(nameof(palette));

            // Image<Rgba32> starts fully transparent, so unwritten pixels stay clear.
            var image = new Image<Rgba32>(Math.Max(1, canvasWidth), Math.Max(1, canvasHeight));
            var transparent = new Rgba32(0, 0, 0, 0);

            image.ProcessPixelRows(accessor =>
            {
                for (int sy = 0; sy < sprite.Height; sy++)
                {
                    int cy = sy + offsetY;
                    if (cy < 0 || cy >= canvasHeight) continue;

                    var pixelRow = accessor.GetRowSpan(cy);
                    for (int sx = 0; sx < sprite.Width; sx++)
                    {
                        int cx = sx + offsetX;
                        if (cx < 0 || cx >= canvasWidth) continue;

                        int p = sy * sprite.Width + sx;
                        if (sprite.Opaque[p])
                        {
                            byte idx = sprite.Pixels[p];
                            pixelRow[cx] = idx < palette.Count ? palette[idx] : transparent;
                        }
                    }
                }
            });

            return image;
        }

        #endregion

        #region Extraction

        /// <summary>
        /// Extracts every still picture described by a .tab/.dat/.pal set and writes
        /// each one as a PNG into <paramref name="outputDirectory"/>. Returns the
        /// number of pictures written.
        /// </summary>
        public static int ExtractPictures(string tabPath, string datPath, string palPath, string outputDirectory)
        {
            var palette = ReadPaletteFile(palPath);
            var table = ReadPictureTableFile(tabPath);
            var dat = File.ReadAllBytes(datPath);
            Directory.CreateDirectory(outputDirectory);

            string baseName = Path.GetFileNameWithoutExtension(datPath);
            int written = 0;
            for (int i = 0; i < table.Count; i++)
            {
                var entry = table[i];
                if (entry.IsEmpty) continue;

                var sprite = DecodeSprite(dat, entry.Offset, entry.Width, entry.Height);
                using var image = ToImage(sprite, palette);
                image.SaveAsPng(Path.Combine(outputDirectory, $"{baseName}_{i:D4}.png"));
                written++;
            }

            return written;
        }

        /// <summary>
        /// Extracts every animation frame described by a .tab/.dat/.pal set and writes
        /// each frame as a PNG, grouped into one folder per sprite/animation.
        ///
        /// Grouping matches the game (FUN_0043c3e0): the table is walked linearly and
        /// each group header (byte 8 = Type, byte 9 = FrameCount) consumes a run of
        /// consecutive records - <c>FrameCount</c> records for a static sprite (Type 0)
        /// or <c>FrameCount * 5</c> records for a directional sprite (Type 2: 5 stored
        /// facings, each with <c>FrameCount</c> frames). Directional groups are further
        /// split into one sub-folder per facing.
        ///
        /// All frames in a group (or facing) are rendered onto a shared canvas sized to
        /// enclose every frame and positioned by each frame's OffsetX/OffsetY, so they
        /// line up and play without jitter. Returns the number of frames written.
        /// </summary>
        public static int ExtractAnimationFrames(string tabPath, string datPath, string palPath, string outputDirectory)
        {
            var palette = ReadPaletteFile(palPath);
            var table = ReadAnimationTableFile(tabPath);
            var dat = File.ReadAllBytes(datPath);
            Directory.CreateDirectory(outputDirectory);

            string baseName = Path.GetFileNameWithoutExtension(datPath);

            int written = 0;
            int groupIndex = 0;

            for (int i = 0; i < table.Count;)
            {
                var header = table[i];
                byte groupType = header.Type;
                int framesPerDir = header.FrameCount;

                // A group header must describe a known sprite type with at least one frame.
                if ((groupType != 0 && groupType != 2) || framesPerDir <= 0)
                {
                    i++;
                    continue;
                }

                int directions = groupType == 2 ? 5 : 1;
                int recordCount = framesPerDir * directions;
                recordCount = Math.Min(recordCount, table.Count - i);

                string groupFolder = Path.Combine(
                    outputDirectory,
                    $"{baseName}_{groupIndex:D4}_type{groupType}_fc{framesPerDir}");
                Directory.CreateDirectory(groupFolder);

                // Canvas large enough to hold every frame across all directions at its
                // offset, so all facings of the animation stay aligned with each other.
                int canvasWidth = 0;
                int canvasHeight = 0;
                for (int r = 0; r < recordCount; r++)
                {
                    var frame = table[i + r];
                    canvasWidth = Math.Max(canvasWidth, frame.OffsetX + frame.Width);
                    canvasHeight = Math.Max(canvasHeight, frame.OffsetY + frame.Height);
                }

                if (canvasWidth <= 0 || canvasHeight <= 0)
                {
                    i += recordCount;
                    groupIndex++;
                    continue;
                }

                for (int dir = 0; dir < directions; dir++)
                {
                    // Records are direction-major: [dir0 frame0..N][dir1 frame0..N]...
                    int dirStart = i + dir * framesPerDir;
                    int dirFrames = Math.Min(framesPerDir, table.Count - dirStart);
                    if (dirFrames <= 0) break;

                    string frameFolder = groupType == 2
                        ? Path.Combine(groupFolder, $"dir{dir}")
                        : groupFolder;
                    if (groupType == 2) Directory.CreateDirectory(frameFolder);

                    for (int f = 0; f < dirFrames; f++)
                    {
                        var frame = table[dirStart + f];
                        int w = frame.Width;
                        int h = frame.Height;
                        if (w <= 0 || h <= 0) continue;

                        var sprite = DecodeSprite(dat, frame.DataOffset, w, h);
                        using var image = ToImage(sprite, palette, canvasWidth, canvasHeight,
                            frame.OffsetX, frame.OffsetY);
                        image.SaveAsPng(Path.Combine(frameFolder, $"frame{f:D2}.png"));
                        written++;
                    }
                }

                i += recordCount;
                groupIndex++;
            }

            return written;
        }

        #endregion

        #region Little-endian readers

        private static uint ReadUInt32(byte[] data, int offset)
            => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

        private static short ReadInt16(byte[] data, int offset)
            => (short)(data[offset] | (data[offset + 1] << 8));

        #endregion
    }
}
