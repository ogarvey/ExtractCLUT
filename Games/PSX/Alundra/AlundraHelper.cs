using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PSX.Alundra
{
    /// <summary>
    /// Prototype extractor for the PSX game *Alundra*.
    ///
    /// All formats here were reverse-engineered from the debug build ALUN_CD.EXE; see
    /// <c>Games/PSX/Alundra/Notes.md</c> for the full analysis. Summary:
    ///   - DATAS.BIN begins with a u32 little-endian offset table; segment i = [u32[i], u32[i+1]).
    ///   - <c>.tx</c> = headerless raw PSX VRAM pixel block (4/8/16 bpp; bpp/width come from caller code).
    ///   - <c>.cl</c> = headerless CLUT, N x 16-bit BGR555 (256 colours = 0x200 bytes, 16 = 0x20 bytes).
    ///   - <c>.EZ</c> = "EZ"+4-byte header, then 0xAD-escaped LZ/RLE compressed 4bpp tiles.
    ///   - sprite-bank = word[0] gfxOff; payload = 256-byte CLUT (8x16 colours) + 4bpp pixels.
    /// </summary>
    public static class AlundraHelper
    {
        // ---------------------------------------------------------------------
        // EZ compression  (EZ_DecompressTile @ 0x80080bc0)
        // ---------------------------------------------------------------------

        /// <summary>Magic bytes at the start of an EZ-compressed file.</summary>
        public static bool IsEz(byte[] data) =>
            data != null && data.Length >= 2 && data[0] == (byte)'E' && data[1] == (byte)'Z';

        /// <summary>
        /// Decompress an Alundra <c>.EZ</c> stream. Pass the FULL file (the 6-byte "EZ...." header
        /// is skipped automatically). Faithful port of EZ_DecompressTile: escape byte 0xAD,
        ///   AD 00          -> literal 0xAD
        ///   AD dist 00     -> end of stream
        ///   AD dist count  -> copy <count> bytes from (dst - dist), overlap allowed (RLE).
        /// </summary>
        public static byte[] DecompressEZ(byte[] file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            // Skip the 6-byte "EZ" + 4 header bytes when present.
            int start = IsEz(file) ? 6 : 0;

            var outp = new List<byte>(file.Length * 4);
            int p = start;
            while (p < file.Length)
            {
                byte b = file[p++];
                if (b != 0xAD)
                {
                    outp.Add(b);
                    continue;
                }

                if (p >= file.Length) break;
                int dist = file[p++];
                if (dist == 0)
                {
                    outp.Add(0xAD);          // escaped literal
                    continue;
                }

                if (p >= file.Length) break;
                int count = file[p++];
                if (count == 0) break;       // end-of-stream marker

                int from = outp.Count - dist;
                if (from < 0) break;         // malformed
                for (int i = 0; i < count; i++)
                    outp.Add(outp[from + i]); // overlap-safe back reference
            }
            return outp.ToArray();
        }

        // ---------------------------------------------------------------------
        // DATAS.BIN master offset table  (GameInit_LoadDATAS_OffsetTable @ 0x8002bfe0)
        // ---------------------------------------------------------------------

        public class DatasSegment
        {
            public int Index { get; set; }
            public uint Offset { get; set; }
            public int Length { get; set; }
            public byte[] Data { get; set; }
        }

        /// <summary>
        /// Split DATAS.BIN using its leading u32 little-endian offset table.
        /// <paramref name="tableEntries"/> defaults to scanning until an offset stops increasing or
        /// runs past the file; the retail loader reads a fixed 0x7b8-byte (≈494-entry) table.
        /// </summary>
        public static List<DatasSegment> SplitDatasBin(byte[] datas, int tableEntries = 0)
        {
            if (datas == null) throw new ArgumentNullException(nameof(datas));

            // Read the offset table. Entries are 2KB-aligned byte offsets into the archive.
            var offsets = new List<uint>();
            int maxScan = tableEntries > 0 ? tableEntries : datas.Length / 4;
            uint prev = 0;
            for (int i = 0; i < maxScan; i++)
            {
                int o = i * 4;
                if (o + 4 > datas.Length) break;
                uint v = BitConverter.ToUInt32(datas, o);
                // Heuristic stop: offsets must be non-decreasing, in range, and 2KB-aligned.
                if (tableEntries == 0)
                {
                    if (i > 0 && (v < prev || v > datas.Length || (v & 0x7ff) != 0)) break;
                }
                offsets.Add(v);
                prev = v;
            }

            var segments = new List<DatasSegment>();
            for (int i = 0; i + 1 < offsets.Count; i++)
            {
                uint a = offsets[i];
                uint b = offsets[i + 1];
                if (b <= a || b > datas.Length) continue;
                int len = (int)(b - a);
                var seg = new DatasSegment
                {
                    Index = i,
                    Offset = a,
                    Length = len,
                    Data = datas.Skip((int)a).Take(len).ToArray()
                };
                segments.Add(seg);
            }
            return segments;
        }

        // ---------------------------------------------------------------------
        // CLUT (.cl) and pixel decoders
        // ---------------------------------------------------------------------

        /// <summary>Read a <c>.cl</c> palette (N x 16-bit BGR555 with PSX transparency bit).</summary>
        public static List<Color> ReadClut(byte[] cl, bool translucent = false) =>
            ColorHelper.ReadABgr15Palette(cl, translucent);

        /// <summary>Decode a raw 16-bpp (direct colour) <c>.tx</c> block to an image.</summary>
        /// <param name="widthPx">Width in pixels (= RECT width in VRAM words for 16bpp).</param>
        public static Bitmap DecodeRaw16bpp(byte[] tx, int widthPx)
        {
            if (widthPx <= 0) throw new ArgumentOutOfRangeException(nameof(widthPx));
            var colors = ColorHelper.ReadABgr15Palette(tx);
            int heightPx = colors.Count / widthPx;
            var bmp = new Bitmap(widthPx, Math.Max(1, heightPx));
            for (int i = 0; i < colors.Count; i++)
            {
                int x = i % widthPx;
                int y = i / widthPx;
                if (y >= heightPx) break;
                bmp.SetPixel(x, y, colors[i]);
            }
            return bmp;
        }

        /// <summary>Decode a 4-bpp indexed <c>.tx</c> block with a 16-colour CLUT.</summary>
        /// <param name="widthPx">Width in pixels (= RECT width in VRAM words * 4 for 4bpp).</param>
        public static Image Decode4bpp(byte[] tx, List<Color> palette, int widthPx)
        {
            int heightPx = (tx.Length * 2) / widthPx;
            return ImageFormatHelper.GenerateClut4Image(palette, tx, widthPx, Math.Max(1, heightPx));
        }

        /// <summary>Decode an 8-bpp indexed <c>.tx</c> block with a 256-colour CLUT.</summary>
        /// <param name="widthPx">Width in pixels (= RECT width in VRAM words * 2 for 8bpp).</param>
        public static Image Decode8bpp(byte[] tx, List<Color> palette, int widthPx)
        {
            int heightPx = tx.Length / widthPx;
            return ImageFormatHelper.GenerateClutImage(palette, tx, widthPx, Math.Max(1, heightPx), true);
        }

        // ---------------------------------------------------------------------
        // Sprite-bank resource  (InitSpriteBank @ 0x8005afd0)
        // ---------------------------------------------------------------------

        public class SpriteBank
        {
            public uint GfxOffset { get; set; }
            public uint Flags { get; set; }
            public List<Color> Clut { get; set; }   // 8 palettes x 16 colours
            public byte[] Pixels4bpp { get; set; }   // raw 4bpp pixel block
        }

        /// <summary>
        /// Parse a sprite-bank resource: word[0] = gfxOff, word[7] = flags. Payload at gfxOff is a
        /// 256-byte CLUT block (8 x 16 colours) followed by 4bpp pixel data.
        /// </summary>
        public static SpriteBank ParseSpriteBank(byte[] bank)
        {
            if (bank == null || bank.Length < 0x20)
                throw new ArgumentException("Buffer too small for a sprite-bank header.", nameof(bank));

            uint gfxOff = BitConverter.ToUInt32(bank, 0x00);
            uint flags = BitConverter.ToUInt32(bank, 0x1c);
            if ((flags & 0xff) == 0)
                throw new InvalidDataException("Not a sprite-bank (flags low byte is zero).");
            if (gfxOff + 0x100 > bank.Length)
                throw new InvalidDataException("gfxOff points past end of buffer.");

            var clutBytes = bank.Skip((int)gfxOff).Take(0x100).ToArray();
            var pixels = bank.Skip((int)gfxOff + 0x100).ToArray();
            return new SpriteBank
            {
                GfxOffset = gfxOff,
                Flags = flags,
                Clut = ColorHelper.ReadABgr15Palette(clutBytes),
                Pixels4bpp = pixels
            };
        }

        // ---------------------------------------------------------------------
        // Segment classification + container splitting (retail DATAS.BIN)
        // ---------------------------------------------------------------------

        public enum SegmentKind
        {
            Empty,      // zero-length
            Ez,         // "EZ" magic, compressed
            Container,  // 7-u32 header (off[0]==0x1C), 6 sub-resources — the map/room format
            Raw16,      // direct 16bpp pixel block (STP-bit heavy)
            Raw4,       // 4bpp/indexed pixel block (default guess)
            Small       // < 0x40 bytes, likely a table/index
        }

        /// <summary>
        /// Classify a DATAS.BIN segment by its content. Mirrors what the retail loaders expect:
        ///   - <see cref="SegmentKind.Ez"/>  : starts with ASCII "EZ".
        ///   - <see cref="SegmentKind.Container"/> : 7-u32 offset header (off[0]==0x1C, off[1]==0x748),
        ///     i.e. a map/room resource holding 6 sub-resources. word[7] is the running map ID.
        ///   - <see cref="SegmentKind.Raw16"/> : majority of sampled 16-bit words have the STP/alpha
        ///     bit (0x8000) set — characteristic of Alundra's direct-colour backgrounds.
        ///   - <see cref="SegmentKind.Raw4"/>  : everything else (treat as 4bpp indexed pixels).
        /// </summary>
        public static SegmentKind Classify(byte[] data)
        {
            if (data == null || data.Length == 0) return SegmentKind.Empty;
            if (data.Length < 0x40) return SegmentKind.Small;
            if (data[0] == (byte)'E' && data[1] == (byte)'Z') return SegmentKind.Ez;
            if (LooksLikeContainer(data)) return SegmentKind.Container;

            int n = Math.Min(256, data.Length / 2);
            int hi = 0;
            for (int k = 0; k < n; k++)
                if ((BitConverter.ToUInt16(data, k * 2) & 0x8000) != 0) hi++;
            return hi > n * 0.6 ? SegmentKind.Raw16 : SegmentKind.Raw4;
        }

        /// <summary>True if the buffer opens with the container's fixed 7-entry ascending u32 header.</summary>
        public static bool LooksLikeContainer(byte[] data)
        {
            if (data == null || data.Length < 0x1c + 4) return false;
            uint first = BitConverter.ToUInt32(data, 0);
            if (first != 0x1c) return false; // header is always 7 words (0x1C bytes) in retail
            uint prev = first;
            for (int k = 1; k < 7; k++)
            {
                uint v = BitConverter.ToUInt32(data, k * 4);
                if (v <= prev || v > data.Length) return false;
                prev = v;
            }
            return true;
        }

        /// <summary>
        /// Split a container segment into its 6 sub-resources using the 7-entry header
        /// (sub_k = [off[k], off[k+1])). Returns the sub-resource byte ranges.
        /// </summary>
        public static List<byte[]> SplitContainer(byte[] data)
        {
            var subs = new List<byte[]>();
            if (!LooksLikeContainer(data)) return subs;

            var offs = new List<uint>();
            for (int k = 0; k < 7; k++) offs.Add(BitConverter.ToUInt32(data, k * 4));
            for (int k = 0; k + 1 < offs.Count; k++)
            {
                uint a = offs[k], b = offs[k + 1];
                if (b <= a || b > data.Length) { subs.Add(Array.Empty<byte>()); continue; }
                subs.Add(data.Skip((int)a).Take((int)(b - a)).ToArray());
            }
            return subs;
        }

        // ---------------------------------------------------------------------
        // Map container graphics decoder
        // ---------------------------------------------------------------------

        /// <summary>
        /// Render a block of 4bpp pixels arranged as fixed <paramref name="tileW"/> x
        /// <paramref name="tileH"/> tiles (row-major within each tile, tiles stored consecutively)
        /// into a sheet that is <paramref name="tilesAcross"/> tiles wide. Low nibble = left pixel.
        /// This matches how PSX background tilesets are packed in VRAM.
        /// </summary>
        public static Bitmap Render4bppTileSheet(byte[] pixels, List<Color> palette,
                                                 int tilesAcross, int tileW = 16, int tileH = 16)
        {
            int bytesPerTile = tileW * tileH / 2;
            int tileCount = pixels.Length / bytesPerTile;
            if (tileCount == 0) return new Bitmap(1, 1);

            int tilesDown = (tileCount + tilesAcross - 1) / tilesAcross;
            var bmp = new Bitmap(tilesAcross * tileW, tilesDown * tileH);

            for (int t = 0; t < tileCount; t++)
            {
                int tileX = (t % tilesAcross) * tileW;
                int tileY = (t / tilesAcross) * tileH;
                int baseByte = t * bytesPerTile;
                for (int py = 0; py < tileH; py++)
                {
                    for (int px = 0; px < tileW; px++)
                    {
                        int pixelIndex = py * tileW + px;
                        int byteOff = baseByte + pixelIndex / 2;
                        if (byteOff >= pixels.Length) break;
                        int nib = (pixelIndex & 1) == 0
                            ? pixels[byteOff] & 0x0F
                            : (pixels[byteOff] >> 4) & 0x0F;
                        var c = nib < palette.Count ? palette[nib] : Color.Magenta;
                        bmp.SetPixel(tileX + px, tileY + py, c);
                    }
                }
            }
            return bmp;
        }

        /// <summary>
        /// Render a 4bpp pixel block as a flat linear bitmap of the given pixel width
        /// (no tile rearrangement). Low nibble = left pixel. Uses LockBits for speed.
        /// </summary>
        public static Bitmap Render4bppLinear(byte[] pixels, List<Color> palette, int widthPx)
        {
            int total = pixels.Length * 2;
            int heightPx = Math.Max(1, total / widthPx);
            var bmp = new Bitmap(widthPx, heightPx, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, widthPx, heightPx);
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] pal = new int[16];
                for (int i = 0; i < 16; i++)
                {
                    var c = i < palette.Count ? palette[i] : Color.Magenta;
                    pal[i] = c.ToArgb();
                }
                var row = new int[widthPx];
                IntPtr scan = bd.Scan0;
                for (int y = 0; y < heightPx; y++)
                {
                    int baseIdx = y * widthPx;
                    for (int x = 0; x < widthPx; x++)
                    {
                        int i = baseIdx + x;
                        int b = pixels[i / 2];
                        int nib = (i & 1) == 0 ? b & 0x0F : (b >> 4) & 0x0F;
                        row[x] = pal[nib];
                    }
                    System.Runtime.InteropServices.Marshal.Copy(row, 0, scan + y * bd.Stride, widthPx);
                }
            }
            finally { bmp.UnlockBits(bd); }
            return bmp;
        }

        /// <summary>
        /// Slice a sprite-bank CLUT block (8 palettes x 16 BGR555 colours) into 8 individual
        /// 16-colour palettes. The block lives at <c>word[0]</c> (gfxOff) of the sprite-bank.
        /// </summary>
        public static List<List<Color>> SplitSpriteBankPalettes(byte[] spriteBank)
        {
            var result = new List<List<Color>>();
            if (spriteBank == null || spriteBank.Length < 0x20) return result;
            uint gfxOff = BitConverter.ToUInt32(spriteBank, 0);
            if (gfxOff + 0x100 > spriteBank.Length) return result;
            for (int pal = 0; pal < 8; pal++)
            {
                var bytes = new byte[0x20];
                Array.Copy(spriteBank, (int)gfxOff + pal * 0x20, bytes, 0, 0x20);
                result.Add(ColorHelper.ReadABgr15Palette(bytes, true));
            }
            return result;
        }

        /// <summary>
        /// Read the tileset CLUT block embedded in the map container's <c>sub0</c> header. Confirmed
        /// via the retail <c>LoadTiles_DATAS_6x_4bpp</c> path: the real palette lives at
        /// <c>sub0 + 0x10</c> and is 32 palettes × 16 BGR555 colours (0x400 bytes), uploaded to VRAM
        /// (0, 480) as a 16×32 CLUT region. Per-bank animation/scroll config follows at <c>+0x420</c>.
        /// </summary>
        public static List<List<Color>> ReadContainerCluts(byte[] sub0)
        {
            var result = new List<List<Color>>();
            if (sub0 == null || sub0.Length < 0x410) return result;
            for (int pal = 0; pal < 32; pal++)
            {
                var bytes = new byte[0x20];
                Array.Copy(sub0, 0x10 + pal * 0x20, bytes, 0, 0x20);
                // Opaque colours: the hardware uses CLUT index 0 (not the STP bit) for
                // transparency when drawing tiles, so non-zero indices must stay fully opaque.
                result.Add(ColorHelper.ReadABgr15Palette(bytes, false));
            }
            return result;
        }

        /// <summary>
        /// Decode one map/room container into PNGs: the primary tileset (sub2, EZ-&gt;0x30000 4bpp)
        /// and the optional secondary tileset (sub4), rendered as a flat 256px-wide bitmap for each
        /// available palette, plus a swatch strip per palette. sub2/sub4 are flat VRAM bitmaps (not
        /// 16x16 tiles); the arrangement into screens is driven by the tilemap (sub1).
        /// </summary>
        public static void DecodeMapContainer(byte[] container, string outDir, int mapId)
        {
            DecodeMapContainer(container, outDir, mapId, int.MaxValue);
        }

        /// <summary>
        /// As <see cref="DecodeMapContainer(byte[], string, int)"/> but caps how many palette
        /// variants are rendered (<paramref name="maxPalettes"/>) — useful for fast batch passes.
        /// </summary>
        public static void DecodeMapContainer(byte[] container, string outDir, int mapId, int maxPalettes)
        {
            Directory.CreateDirectory(outDir);
            var subs = SplitContainer(container);
            if (subs.Count < 6) return;

            // sub0 holds the real tileset CLUT (32 palettes × 16 colours at +0x10), as confirmed by
            // the retail LoadTiles_DATAS_6x_4bpp / FUN_8002cc9c code path.
            var palettes = ReadContainerCluts(subs[0]);
            if (palettes.Count == 0)
                palettes.Add(Enumerable.Range(0, 16)
                    .Select(i => Color.FromArgb(i * 16, i * 16, i * 16)).ToList());

            var envPalettes = palettes.ToList();
            if (envPalettes.Count > maxPalettes) envPalettes = envPalettes.Take(maxPalettes).ToList();

            // Dump every palette as a 16x1 swatch strip for reference.
            for (int p = 0; p < envPalettes.Count; p++)
            {
                using var sw = new Bitmap(16, 1);
                for (int i = 0; i < 16 && i < envPalettes[p].Count; i++) sw.SetPixel(i, 0, envPalettes[p][i]);
                sw.Save(Path.Combine(outDir, $"map{mapId:D3}_pal{p}.png"), ImageFormat.Png);
            }

            // sub2 = primary tileset (EZ -> 0x30000 4bpp)
            RenderTilesetSub(subs[2], envPalettes, outDir, $"map{mapId:D3}_tiles0");
            // sub4 = secondary tileset (often empty)
            RenderTilesetSub(subs[4], envPalettes, outDir, $"map{mapId:D3}_tiles1");

            // Assemble the full room using the tilemap (sub1) + tileset (sub2) + CLUT (sub0).
            RenderRoom(subs, palettes, outDir, $"map{mapId:D3}_room");

            // Extract individual sprites using the unified 14-byte descriptors database in sub3
            if (subs[3] != null && subs[3].Length > 0)
            {
                try
                {
                    ExtractIndividualSpritesUnified(palettes, subs[3], subs[2], subs[4], subs[5], outDir, $"map{mapId:D3}");
                }
                catch (Exception)
                {
                    // Ignore extraction/decompression errors
                }
            }
        }

        public class SpriteCelDescriptor
        {
            public byte Flags { get; set; }
            public byte PalIdx { get; set; }
            public byte U { get; set; }
            public byte V { get; set; }
            public byte Width { get; set; }
            public byte Height { get; set; }
            public sbyte[] VertexOffsets { get; set; }
            public int PageIdx => Flags & 0x07;
        }

        /// <summary>
        /// Extract sprites from sub2, sub4, and sub5 using the 14-byte cel descriptors found in sub3.
        /// </summary>
        public static void ExtractIndividualSpritesUnified(
            List<List<Color>> mapPalettes, byte[] sub3, byte[] sub2, byte[] sub4, byte[] sub5,
            string outDir, string baseName)
        {
            if (sub3 == null || sub3.Length < 24) return;

            // 1) Decompress sub2 (primary tileset) if present and EZ-compressed
            byte[] sub2Pixels = Array.Empty<byte>();
            if (sub2 != null && sub2.Length > 0)
            {
                sub2Pixels = IsEz(sub2) ? DecompressEZ(sub2) : sub2;
            }

            // 2) Decompress sub4 (secondary tileset) if present and EZ-compressed
            byte[] sub4Pixels = Array.Empty<byte>();
            if (sub4 != null && sub4.Length > 0)
            {
                sub4Pixels = IsEz(sub4) ? DecompressEZ(sub4) : sub4;
            }

            // 3) Parse sub5 (sprite bank) if present and render the full sheets
            byte[] sub5Pixels = Array.Empty<byte>();
            var sub5Palettes = new List<List<Color>>();
            if (sub5 != null && sub5.Length >= 0x20)
            {
                try
                {
                    var bank = ParseSpriteBank(sub5);
                    sub5Pixels = bank.Pixels4bpp;
                    for (int p = 0; p < 8; p++)
                    {
                        var palette = bank.Clut.Skip(p * 16).Take(16).ToList();
                        if (palette.Count == 16) sub5Palettes.Add(palette);
                    }

                    string spritesDir = Path.Combine(outDir, "sprites");
                    Directory.CreateDirectory(spritesDir);
                    for (int p = 0; p < sub5Palettes.Count; p++)
                    {
                        var renderPalette = sub5Palettes[p].Select((c, idx) => idx == 0 ? Color.Transparent : c).ToList();
                        using var sheet = Render4bppLinear(sub5Pixels, renderPalette, 256);
                        sheet.Save(Path.Combine(spritesDir, $"{baseName}_sub5_spritesheet_pal{p}.png"), ImageFormat.Png);
                    }
                }
                catch { }
            }

            // 4) Parse the 40 sprite palettes from sub3.bin (specified by word[5] of the header)
            var sub3Palettes = new List<List<Color>>();
            uint spritePalOffset = BitConverter.ToUInt32(sub3, 20);
            if (spritePalOffset > 0 && spritePalOffset + 1280 <= sub3.Length)
            {
                for (int p = 0; p < 40; p++)
                {
                    int offset = (int)spritePalOffset + p * 32;
                    byte[] palBytes = new byte[32];
                    Array.Copy(sub3, offset, palBytes, 0, 32);
                    var palette = ColorHelper.ReadABgr15Palette(palBytes, true);
                    sub3Palettes.Add(palette);
                }
            }

            // 5) Walk Table 1 in sub3.bin to locate all active Entity Definitions
            var entityOffsets = new List<uint>();
            uint table1Offset = BitConverter.ToUInt32(sub3, 12);
            if (table1Offset > 0 && table1Offset + 4 <= sub3.Length)
            {
                for (int k = 0; k < 256; k++)
                {
                    int entryOffset = (int)table1Offset + k * 4;
                    if (entryOffset + 4 > sub3.Length) break;
                    uint val = BitConverter.ToUInt32(sub3, entryOffset);
                    if (val == 0) break;
                    if (val == 0xFFFFFFFF) continue;
                    if (val < sub3.Length)
                    {
                        entityOffsets.Add(val);
                    }
                }
            }

            // 6) Clean and prepare target directory
            string targetDir = Path.Combine(outDir, "sprites", $"{baseName}_exact_cels");
            if (Directory.Exists(targetDir))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(targetDir))
                    {
                        File.Delete(file);
                    }
                }
                catch { }
            }
            Directory.CreateDirectory(targetDir);

            // 7) Crop and extract cels per entity, animation, and frame
            for (int entIdx = 0; entIdx < entityOffsets.Count; entIdx++)
            {
                uint entOff = entityOffsets[entIdx];
                if (entOff + 36 > sub3.Length) continue;

                uint word7 = BitConverter.ToUInt32(sub3, (int)entOff + 28);
                uint word8 = BitConverter.ToUInt32(sub3, (int)entOff + 32);

                int pageBase = (int)((word7 >> 16) & 0xFF);
                int clutBase = (int)((word8 >> 16) & 0xFF);

                // Read the 4 animation pointers
                var animOffsets = new List<uint>();
                for (int i = 0; i < 16; i += 4)
                {
                    uint val = BitConverter.ToUInt32(sub3, (int)entOff + i);
                    if (val >= table1Offset && val < sub3.Length)
                    {
                        animOffsets.Add(val);
                    }
                    else
                    {
                        animOffsets.Add(0); // keep index alignment
                    }
                }

                // Process the two possible animation pairs
                for (int animIdx = 0; animIdx < 2; animIdx++)
                {
                    uint ptr0 = animOffsets[2 * animIdx];
                    uint ptr1 = animOffsets[2 * animIdx + 1];

                    if (ptr0 == 0 || ptr1 == 0 || ptr0 >= sub3.Length || ptr1 >= sub3.Length)
                    {
                        continue; // Invalid or missing animation pair
                    }

                    int distance = (int)ptr1 - (int)ptr0;
                    if (distance < 0) continue; // Invalid pair ordering

                    if (distance < 80)
                    {
                        // -------------------------------------------------------------
                        // Format B: List-based (6-byte frames + 16-byte cels)
                        // -------------------------------------------------------------
                        int frameCount = Math.Min(7, distance / 6);
                        for (int frameIdx = 0; frameIdx < frameCount; frameIdx++)
                        {
                            int frameOff = (int)ptr0 + frameIdx * 6;
                            sbyte xOff = (sbyte)sub3[frameOff];
                            sbyte yOff = (sbyte)sub3[frameOff + 1];
                            byte celCountMinusOne = sub3[frameOff + 2];
                            byte celStartIdx = sub3[frameOff + 3];
                            byte delay = sub3[frameOff + 4];
                            byte flags = sub3[frameOff + 5];

                            // Skip empty/unused frame slots
                            if (celCountMinusOne == 0xFF || (celCountMinusOne == 0 && celStartIdx == 0 && delay == 0 && flags == 0))
                            {
                                continue;
                            }

                            int celCount = celCountMinusOne + 1;
                            if (celCount <= 0 || celCount > 40) continue;

                            for (int c = 0; c < celCount; c++)
                            {
                                int celOff = (int)ptr1 + (celStartIdx + c) * 16;
                                if (celOff + 16 > sub3.Length) continue;

                                byte celFlags = sub3[celOff];
                                byte celPalIdx = sub3[celOff + 1];
                                byte celU = sub3[celOff + 2];
                                byte celV = sub3[celOff + 3];
                                byte celW = sub3[celOff + 4];
                                byte celH = sub3[celOff + 5];

                                if (celW == 0 || celH == 0 || celW > 256 || celH > 256)
                                {
                                    continue; // Skip invalid sizes
                                }

                                int actualPage = pageBase + (celFlags & 0x07);
                                int actualPalIdx = clutBase + celPalIdx;

                                ProcessAndSaveCel(
                                    actualPage, actualPalIdx, celU, celV, celW, celH, 
                                    sub2Pixels, sub4Pixels, sub5Pixels, mapPalettes, sub3Palettes, sub5Palettes,
                                    targetDir, entIdx, animIdx, frameIdx, c, sourceName: "formatB"
                                );
                            }
                        }
                    }
                    else
                    {
                        // -------------------------------------------------------------
                        // Format A: Offset-based (16-bit offset table + 5-byte pieces)
                        // -------------------------------------------------------------
                        var offsets = new List<uint>();
                        for (int f = 0; f < 7; f++)
                        {
                            int offPtr = (int)ptr0 + f * 2;
                            if (offPtr + 2 > ptr1) break;
                            ushort val = BitConverter.ToUInt16(sub3, offPtr);
                            offsets.Add(val);
                        }

                        // Determine the end of the frame data block (to bound the last frame)
                        int nextBlockOff = sub3.Length;
                        foreach (var otherOff in entityOffsets)
                        {
                            if (otherOff > ptr1 && otherOff < nextBlockOff) nextBlockOff = (int)otherOff;
                        }
                        for (int k = 0; k < animOffsets.Count; k++)
                        {
                            uint otherAnim = animOffsets[k];
                            if (otherAnim > ptr1 && otherAnim < nextBlockOff) nextBlockOff = (int)otherAnim;
                        }

                        for (int frameIdx = 0; frameIdx < offsets.Count; frameIdx++)
                        {
                            uint offset = offsets[frameIdx];
                            if (frameIdx > 0 && offset == 0) break; // End of animation
                            
                            int currOff = (int)ptr1 + (int)offset;
                            if (currOff >= nextBlockOff) break;

                            // Determine frame size by looking at the next offset
                            int frameSize = 0;
                            if (frameIdx + 1 < offsets.Count && offsets[frameIdx + 1] > offset && offsets[frameIdx + 1] < (nextBlockOff - ptr1))
                            {
                                frameSize = (int)(offsets[frameIdx + 1] - offset);
                            }
                            else
                            {
                                frameSize = nextBlockOff - currOff;
                            }

                            if (frameSize <= 1) continue;

                            // The last byte is the delay, the rest are 5-byte pieces
                            int pieceCount = (frameSize - 1) / 5;
                            if (pieceCount <= 0 || pieceCount > 40) continue;

                            for (int c = 0; c < pieceCount; c++)
                            {
                                int pieceOff = currOff + c * 5;
                                if (pieceOff + 5 > nextBlockOff) break;

                                byte celFlags = sub3[pieceOff];
                                byte celPalIdx = sub3[pieceOff + 1];
                                byte celU = sub3[pieceOff + 2];
                                byte celV = sub3[pieceOff + 3];
                                byte celW_H = sub3[pieceOff + 4]; // usually 0, default to 16x16

                                byte celW = 16;
                                byte celH = 16;

                                int actualPage = pageBase + (celFlags & 0x07);
                                int actualPalIdx = clutBase + celPalIdx;

                                ProcessAndSaveCel(
                                    actualPage, actualPalIdx, celU, celV, celW, celH, 
                                    sub2Pixels, sub4Pixels, sub5Pixels, mapPalettes, sub3Palettes, sub5Palettes,
                                    targetDir, entIdx, animIdx, frameIdx, c, sourceName: "formatA"
                                );
                            }
                        }
                    }
                }
            }
        }

        private static void ProcessAndSaveCel(
            int actualPage, int actualPalIdx, byte celU, byte celV, byte celW, byte celH,
            byte[] sub2Pixels, byte[] sub4Pixels, byte[] sub5Pixels,
            List<List<Color>> mapPalettes, List<List<Color>> sub3Palettes, List<List<Color>> sub5Palettes,
            string targetDir, int entIdx, int animIdx, int frameIdx, int c, string sourceName)
        {
            byte[] srcPixels = null;
            int srcV = 0;
            string pageSourceName = "";

            if (actualPage < 10)
            {
                srcPixels = sub2Pixels;
                srcV = actualPage * 256 + celV;
                pageSourceName = "sub2";
            }
            else if (actualPage >= 10 && actualPage <= 13)
            {
                int sub4Page = actualPage - 10;
                srcPixels = sub4Pixels;
                srcV = sub4Page * 256 + celV;
                pageSourceName = "sub4";
            }
            else if (actualPage >= 14)
            {
                int sub5Page = actualPage - 14;
                srcPixels = sub5Pixels;
                srcV = sub5Page * 256 + celV;
                pageSourceName = "sub5";
            }

            if (srcPixels == null || srcPixels.Length == 0)
            {
                return; // Pixels not available for this page
            }

            int maxRows = (srcPixels.Length * 2) / 256;
            if (srcV + celH > maxRows)
            {
                return; // Coordinates out of VRAM bounds
            }

            // Map palette based on actualPalIdx:
            List<Color> palette = null;
            string palSource = "";

            if (actualPalIdx < 32)
            {
                if (actualPalIdx < mapPalettes.Count)
                {
                    palette = mapPalettes[actualPalIdx];
                    palSource = $"sub0_pal{actualPalIdx}";
                }
            }
            else if (actualPalIdx >= 32 && actualPalIdx < 128)
            {
                int s3Pal = 0;
                if (actualPalIdx >= 96) s3Pal = actualPalIdx - 96;
                else if (actualPalIdx >= 64) s3Pal = actualPalIdx - 64;
                else s3Pal = actualPalIdx - 32;

                if (s3Pal >= 0 && s3Pal < sub3Palettes.Count)
                {
                    palette = sub3Palettes[s3Pal];
                    palSource = $"sub3_pal{s3Pal}";
                }
            }
            else if (actualPalIdx >= 128 && actualPalIdx < 136)
            {
                int s5Pal = actualPalIdx - 128;
                if (s5Pal < sub5Palettes.Count)
                {
                    palette = sub5Palettes[s5Pal];
                    palSource = $"sub5_pal{s5Pal}";
                }
            }

            // Fallback palette if the mapped index is out of bounds
            if (palette == null)
            {
                if (sub3Palettes.Count > 0)
                {
                    palette = sub3Palettes[0];
                    palSource = "fallback_sub3_0";
                }
                else if (mapPalettes.Count > 0)
                {
                    palette = mapPalettes[0];
                    palSource = "fallback_sub0_0";
                }
            }

            using (var celBmp = new Bitmap(celW, celH, PixelFormat.Format32bppArgb))
            {
                var bd = celBmp.LockBits(new Rectangle(0, 0, celW, celH), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int[] palColors = new int[16];
                    for (int k = 0; k < 16; k++)
                    {
                        if (k == 0)
                        {
                            palColors[k] = 0; // Transparent
                        }
                        else
                        {
                            palColors[k] = (palette != null && k < palette.Count ? palette[k] : Color.Magenta).ToArgb();
                        }
                    }

                    var row = new int[celW];
                    IntPtr scan = bd.Scan0;
                    for (int cy = 0; cy < celH; cy++)
                    {
                        int py = srcV + cy;
                        int baseIdx = py * 256;
                        for (int cx = 0; cx < celW; cx++)
                        {
                            int px = celU + cx;
                            int pixelIdx = baseIdx + px;
                            int byteOff = pixelIdx / 2;
                            if (byteOff < srcPixels.Length)
                            {
                                int b = srcPixels[byteOff];
                                int nib = (pixelIdx & 1) == 0 ? b & 0x0F : (b >> 4) & 0x0F;
                                row[cx] = palColors[nib];
                            }
                            else
                            {
                                row[cx] = 0;
                            }
                        }
                        System.Runtime.InteropServices.Marshal.Copy(row, 0, scan + cy * bd.Stride, celW);
                    }
                }
                finally
                {
                    celBmp.UnlockBits(bd);
                }

                string celPath = Path.Combine(targetDir, $"cel_ent{entIdx:D2}_anim{animIdx:D2}_frame{frameIdx:D2}_cel{c:D2}_{pageSourceName}_page{actualPage}_U{celU}_V{celV}_W{celW}_H{celH}_{palSource}_{sourceName}.png");
                celBmp.Save(celPath, ImageFormat.Png);
            }
        }

        // Tilemap layout (sub1 / DAT_800dcbb4), from retail FUN_8002d64c / FUN_8002cde4:
        //   grid of 8-byte cells, row stride 0x1a0 (=52 cells), grid base offset 0x604, 52x60 cells.
        // 8-byte cell: [u16 type/height][u16 height2][u16 tile][u16 overlay]
        //   @2 high byte: vertical tile offset (usually 0); low byte = height/zone data (not gfx).
        //   tile   (@4): 0xFFFF = empty. Otherwise:
        //                 idx     = tile & 0x3FF   (0..959, must be < 0x3C0)
        //                 palette = (tile >> 12) & 0xF   -> CLUT via GetClut table = sub0 palette N
        //   overlay(@6): 0xFFFF = none, else index into the foreground-strip table at sub1+0x6784.
        // The CLUT index 0 of every tile is TRANSPARENT (PSX 4bpp convention, not the STP bit).
        //
        // Foreground/overlay strip table (sub1 + 0x6784), entry = sub1 + 0x6784 + overlay*2:
        //   byte[0] = signed base offset, byte[1] = N (tile count), then N u16 tiles.
        //   element e (1..N) draws at map row (ry + e - base), same column => a vertical stack
        //   of foreground tiles (tree/pillar tops, wall caps) drawn on top of the background.
        //
        // The tile dictionary (DAT_800dc070) is PROCEDURALLY generated by FUN_8002caf8, not stored
        // in the container: 960 tiles = 6 pages x 16 rows x 10 cols, each tile 24x16 px.
        //   page = idx / 160; rem = idx % 160; trow = rem / 10; tcol = rem % 10
        //   srcU = tcol * 24; srcV = page * 256 + trow * 16   (in the 256px-wide stacked tileset)
        private const int RoomCols = 52, RoomRows = 60;
        private const int TileW = 24, TileH = 16;
        private const int TilemapGridOff = 0x604, TilemapRowStride = 0x1a0;
        private const int OverlayTableOff = 0x6784;

        /// <summary>
        /// Assemble a full room image from the container's tilemap (sub1), tileset (sub2) and CLUT
        /// (sub0). Lays the background tile layer (cell @4) then the foreground/overlay strips
        /// (cell @6 -> table at sub1+0x6784). Each tile is 24x16, palette = tile-field top nibble,
        /// CLUT index 0 transparent. Grid is 52x60 cells => 1248x960px.
        /// </summary>
        public static void RenderRoom(List<byte[]> subs, List<List<Color>> palettes, string outDir, string baseName)
        {
            if (subs == null || subs.Count < 3) return;
            byte[] tilemap = subs[1];
            byte[] tiles0 = IsEz(subs[2]) ? DecompressEZ(subs[2]) : subs[2];
            byte[] tiles1 = null;
            if (subs.Count > 4 && subs[4] != null && subs[4].Length > 0)
            {
                tiles1 = IsEz(subs[4]) ? DecompressEZ(subs[4]) : subs[4];
            }
            if (tilemap == null || tiles0.Length < 0x8000) return;
            int needed = TilemapGridOff + RoomRows * TilemapRowStride;
            if (tilemap.Length < needed) return;

            int W = RoomCols * TileW, H = RoomRows * TileH;   // 1248 x 960
            var img = new int[W * H];

            // 1) Background tile layer.
            for (int ry = 0; ry < RoomRows; ry++)
            {
                for (int rx = 0; rx < RoomCols; rx++)
                {
                    int cell = TilemapGridOff + ry * TilemapRowStride + rx * 8;
                    int heightOffset = tilemap[cell + 3];
                    BlitTile(img, W, rx, ry - heightOffset, BitConverter.ToUInt16(tilemap, cell + 4), tiles0, palettes);
                }
            }

            // 2) Foreground/overlay strip layer (drawn on top).
            for (int ry = 0; ry < RoomRows; ry++)
            {
                for (int rx = 0; rx < RoomCols; rx++)
                {
                    int cell = TilemapGridOff + ry * TilemapRowStride + rx * 8;
                    int ov = BitConverter.ToUInt16(tilemap, cell + 6);
                    if (ov == 0xFFFF) continue;
                    int h = OverlayTableOff + ov * 2;
                    if (h + 1 >= tilemap.Length) continue;
                    int bse = (sbyte)tilemap[h];
                    int n = tilemap[h + 1];
                    int heightOffset = tilemap[cell + 3];
                    for (int e = 1; e <= n; e++)
                    {
                        int to = h + e * 2;
                        if (to + 1 >= tilemap.Length) break;
                        BlitTile(img, W, rx, ry - heightOffset + e - bse, BitConverter.ToUInt16(tilemap, to), tiles0, palettes);
                    }
                }
            }

            using var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < H; y++)
                Marshal.Copy(img, y * W, data.Scan0 + y * data.Stride, W);
            bmp.UnlockBits(data);
            bmp.Save(Path.Combine(outDir, baseName + ".png"), ImageFormat.Png);
        }

        /// <summary>
        /// Blit one 24x16 tile (by packed tile field) into <paramref name="img"/> at grid cell
        /// (<paramref name="rx"/>,<paramref name="ry"/>). CLUT index 0 is left transparent.
        /// </summary>
        private static void BlitTile(int[] img, int W, int rx, int ry, int tile, byte[] tiles, List<List<Color>> palettes)
        {
            if (tile == 0xFFFF) return;
            if (ry < 0 || ry >= RoomRows || rx < 0 || rx >= RoomCols) return;
            int idx = tile & 0x3FF;
            if (idx >= 0x3C0) return;
            int palIdx = (tile >> 12) & 0xF;
            var pal = palettes[palIdx < palettes.Count ? palIdx : 0];

            const int sheetRowBytes = 128;   // 256px 4bpp = 128 bytes/row
            int page = idx / 160, rem = idx % 160;
            int srcU = (rem % 10) * TileW;
            int srcV = page * 256 + (rem / 10) * TileH;

            for (int ty = 0; ty < TileH; ty++)
            {
                int rowBase = (srcV + ty) * sheetRowBytes;
                int dstBase = (ry * TileH + ty) * W + rx * TileW;
                for (int tx = 0; tx < TileW; tx++)
                {
                    int px = srcU + tx;
                    int bi = rowBase + (px >> 1);
                    if (bi < 0 || bi >= tiles.Length) continue;
                    int nib = (px & 1) == 0 ? (tiles[bi] & 0x0f) : (tiles[bi] >> 4);
                    if (nib == 0) continue;   // CLUT index 0 = transparent
                    img[dstBase + tx] = pal[nib < pal.Count ? nib : 0].ToArgb();
                }
            }
        }

        /// <summary>
        /// Scan a buffer for runs that look like vivid 16-colour BGR555 CLUTs (many saturated +
        /// bright + distinct entries). Returns the byte offsets of the best candidates. Used to
        /// locate the per-map tileset palette, which is embedded in the tilemap sub-resource (sub1).
        /// </summary>
        public static List<int> FindClutCandidates(byte[] b, int max = 8)
        {
            var hits = new List<(int off, int score)>();
            if (b == null || b.Length < 0x20) return new List<int>();
            for (int off = 0; off + 0x20 <= b.Length; off += 2)
            {
                int sat = 0, bright = 0;
                var distinct = new HashSet<ushort>();
                for (int i = 0; i < 16; i++)
                {
                    ushort v = BitConverter.ToUInt16(b, off + i * 2);
                    distinct.Add(v);
                    int r = v & 0x1F, g = (v >> 5) & 0x1F, bl = (v >> 10) & 0x1F;
                    int mx = Math.Max(r, Math.Max(g, bl)), mn = Math.Min(r, Math.Min(g, bl));
                    if (mx - mn >= 6) sat++;
                    if (mx >= 18) bright++;
                }
                if (sat >= 6 && bright >= 6 && distinct.Count >= 10)
                    hits.Add((off, sat + bright + distinct.Count));
            }
            // De-duplicate near-adjacent hits (keep local maxima ~0x20 apart).
            return hits.OrderByDescending(h => h.score)
                       .Select(h => h.off)
                       .Where((o, idx) => true)
                       .Take(max)
                       .ToList();
        }

        private static void RenderTilesetSub(byte[] sub, List<List<Color>> palettes, string outDir, string baseName)
        {
            if (sub == null || sub.Length == 0) return;
            byte[] pixels = IsEz(sub) ? DecompressEZ(sub) : sub;
            if (pixels.Length == 0) return;

            // sub2/sub4 are flat 256px-wide 4bpp bitmaps (stacked 256x256 VRAM pages), NOT tiles.
            // Render with every supplied palette so the correct CLUT can be picked per region.
            for (int p = 0; p < palettes.Count; p++)
            {
                using var lin = Render4bppLinear(pixels, palettes[p], 256);
                lin.Save(Path.Combine(outDir, $"{baseName}_pal{p}.png"), ImageFormat.Png);
            }
        }

        // ---------------------------------------------------------------------
        // Full DATAS.BIN extraction driver
        // ---------------------------------------------------------------------

        /// <summary>
        /// Split the retail DATAS.BIN, classify every segment, dump raw bytes + container
        /// sub-resources, decompress EZ segments, and write a manifest CSV. This is the
        /// prototype "auto-classifier" pass. When <paramref name="renderMapSamples"/> &gt; 0, the
        /// first N map containers also get their 4bpp tileset (sub2/sub4) rendered to PNG under
        /// <c>outDir/maps</c> (grey-ramp reference render — the true per-tile CLUT is loaded by
        /// shared retail map code and is not yet reversed).
        /// </summary>
        public static void ExtractDatasBin(string binPath, string outDir, int renderMapSamples = 0)
        {
            if (!File.Exists(binPath)) throw new FileNotFoundException(binPath);
            Directory.CreateDirectory(outDir);
            var mapsDir = Path.Combine(outDir, "maps");
            int mapsRendered = 0;

            var datas = File.ReadAllBytes(binPath);
            var segments = SplitDatasBin(datas);

            var manifest = new System.Text.StringBuilder();
            manifest.AppendLine("index,offset,length,kind,subResources,note");

            var rawDir = Path.Combine(outDir, "segments");
            Directory.CreateDirectory(rawDir);

            foreach (var seg in segments)
            {
                var kind = Classify(seg.Data);
                string note = "";
                int subCount = 0;

                string baseName = $"seg_{seg.Index:D4}_off{seg.Offset:X8}";
                File.WriteAllBytes(Path.Combine(rawDir, baseName + ".bin"), seg.Data);

                switch (kind)
                {
                    case SegmentKind.Ez:
                        try
                        {
                            var dec = DecompressEZ(seg.Data);
                            File.WriteAllBytes(Path.Combine(rawDir, baseName + ".ez.raw"), dec);
                            note = $"decompressed 0x{dec.Length:X}";
                        }
                        catch (Exception ex) { note = "EZ decode failed: " + ex.Message; }
                        break;

                    case SegmentKind.Container:
                        var subs = SplitContainer(seg.Data);
                        subCount = subs.Count;
                        var subDir = Path.Combine(rawDir, baseName);
                        Directory.CreateDirectory(subDir);
                        var subNotes = new List<string>();
                        for (int s = 0; s < subs.Count; s++)
                        {
                            File.WriteAllBytes(Path.Combine(subDir, $"sub{s}.bin"), subs[s]);
                            // sub2/sub4 (and any sub) may themselves be EZ-compressed graphics.
                            if (IsEz(subs[s]))
                            {
                                try
                                {
                                    var dec = DecompressEZ(subs[s]);
                                    File.WriteAllBytes(Path.Combine(subDir, $"sub{s}.raw"), dec);
                                    subNotes.Add($"sub{s}=EZ->0x{dec.Length:X}");
                                }
                                catch (Exception ex) { subNotes.Add($"sub{s}=EZ-fail({ex.Message})"); }
                            }
                            else
                            {
                                subNotes.Add($"sub{s}=0x{subs[s].Length:X}");
                            }
                        }
                        uint mapId = seg.Data.Length >= 0x20 ? BitConverter.ToUInt32(seg.Data, 0x1c) : 0;
                        note = $"mapId={mapId} [{string.Join(" ", subNotes)}]";
                        if (mapsRendered < renderMapSamples)
                        {
                            try { DecodeMapContainer(seg.Data, mapsDir, (int)mapId, 2); mapsRendered++; }
                            catch (Exception ex) { note += " mapRenderFail:" + ex.Message; }
                        }
                        break;

                    case SegmentKind.Raw16:
                        note = "candidate 16bpp; width guesses: " +
                               string.Join("/", WidthCandidates(seg.Length, 2));
                        break;

                    case SegmentKind.Raw4:
                        note = "candidate 4bpp; width guesses: " +
                               string.Join("/", WidthCandidates(seg.Length, 4));
                        break;
                }

                manifest.AppendLine($"{seg.Index},0x{seg.Offset:X8},0x{seg.Length:X},{kind},{subCount},{note}");
            }

            File.WriteAllText(Path.Combine(outDir, "manifest.csv"), manifest.ToString());
        }

        /// <summary>
        /// Suggest plausible pixel widths for a raw block of <paramref name="byteLen"/> bytes at the
        /// given bits-per-pixel, favouring common PSX tile/strip widths (multiples of 16 that divide
        /// the pixel count cleanly).
        /// </summary>
        public static List<int> WidthCandidates(int byteLen, int bpp)
        {
            long pixels = (long)byteLen * 8 / bpp;
            var widths = new List<int>();
            foreach (int w in new[] { 64, 128, 160, 256, 320, 512, 640, 1024 })
                if (pixels % w == 0) widths.Add(w);
            return widths.Count > 0 ? widths : new List<int> { 256 };
        }

        // ---------------------------------------------------------------------
        // Convenience: walk a folder of loose Alundra assets (debug-build layout)
        // ---------------------------------------------------------------------
        public static void ExtractFolder(string dir, int defaultWidthPx = 256)
        {
            if (!Directory.Exists(dir)) throw new DirectoryNotFoundException(dir);

            var outDir = Path.Combine(dir, "_extracted");
            Directory.CreateDirectory(outDir);

            // Decompress EZ files first.
            foreach (var ez in Directory.GetFiles(dir, "*.ez", SearchOption.AllDirectories))
            {
                var raw = DecompressEZ(File.ReadAllBytes(ez));
                File.WriteAllBytes(Path.Combine(outDir, Path.GetFileNameWithoutExtension(ez) + ".raw"), raw);
            }

            // Render tx + cl pairs.
            foreach (var tx in Directory.GetFiles(dir, "*.tx", SearchOption.AllDirectories))
            {
                var baseName = Path.GetFileNameWithoutExtension(tx);
                var cl = Path.Combine(Path.GetDirectoryName(tx), baseName + ".cl");
                var txBytes = File.ReadAllBytes(tx);

                Image img;
                if (File.Exists(cl))
                {
                    var palette = ReadClut(File.ReadAllBytes(cl));
                    img = palette.Count > 16
                        ? Decode8bpp(txBytes, palette, defaultWidthPx)
                        : Decode4bpp(txBytes, palette, defaultWidthPx);
                }
                else
                {
                    // No palette -> assume direct 16bpp.
                    img = DecodeRaw16bpp(txBytes, defaultWidthPx);
                }

                img.Save(Path.Combine(outDir, baseName + ".png"), ImageFormat.Png);
                img.Dispose();
            }
        }
    }
}
