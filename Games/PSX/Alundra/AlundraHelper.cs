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
        public static void DecodeMapContainer(byte[] container, string outDir, int mapId, bool outputLayers = false, bool outputExtraGfx = false)
        {
            DecodeMapContainer(container, outDir, mapId, int.MaxValue, outputLayers, outputExtraGfx);
        }

        /// <summary>
        /// As <see cref="DecodeMapContainer(byte[], string, int, bool, bool)"/> but caps how many palette
        /// variants are rendered (<paramref name="maxPalettes"/>) — useful for fast batch passes.
        /// </summary>
        public static void DecodeMapContainer(byte[] container, string outDir, int mapId, int maxPalettes, bool outputLayers = false, bool outputExtraGfx = false)
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

            if (outputExtraGfx)
            {
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
            }

            // Assemble the full room using the tilemap (sub1) + tileset (sub2) + CLUT (sub0).
            RenderRoom(subs, palettes, outDir, $"map{mapId:D3}_room", outputLayers);
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
        private class SortedTile
        {
            public int Rx { get; set; }
            public int Ry { get; set; }
            public ushort TileCode { get; set; }
            public int Depth { get; set; }
        }

        private static void SavePng(int[] img, int W, int H, string path)
        {
            using var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < H; y++)
                Marshal.Copy(img, y * W, data.Scan0 + y * data.Stride, W);
            bmp.UnlockBits(data);
            bmp.Save(path, ImageFormat.Png);
        }

        public static void RenderRoom(List<byte[]> subs, List<List<Color>> palettes, string outDir, string baseName, bool outputLayers = false)
        {
            if (subs == null || subs.Count < 3) return;
            byte[] tilemap = subs[1];
            byte[] tiles0 = IsEz(subs[2]) ? DecompressEZ(subs[2]) : subs[2];
            if (tilemap == null || tiles0.Length < 0x8000) return;
            int needed = TilemapGridOff + RoomRows * TilemapRowStride;
            if (tilemap.Length < needed) return;

            var bgTiles = new List<SortedTile>();
            var fgTiles = new List<SortedTile>();

            for (int ry = 0; ry < RoomRows; ry++)
            {
                for (int rx = 0; rx < RoomCols; rx++)
                {
                    int cell = TilemapGridOff + ry * TilemapRowStride + rx * 8;
                    int heightOffset = tilemap[cell + 3];

                    // 1) Background tile
                    ushort bgCode = BitConverter.ToUInt16(tilemap, cell + 4);
                    if (bgCode != 0xFFFF)
                    {
                        int idx = bgCode & 0x3FF;
                        int page = idx / 160;
                        bgTiles.Add(new SortedTile
                        {
                            Rx = rx,
                            Ry = ry - heightOffset,
                            TileCode = bgCode,
                            Depth = ry * 16 + page
                        });
                    }

                    // 2) Overlay strip tiles
                    ushort ov = BitConverter.ToUInt16(tilemap, cell + 6);
                    if (ov != 0xFFFF)
                    {
                        int h = OverlayTableOff + ov * 2;
                        if (h + 1 < tilemap.Length)
                        {
                            int bse = (sbyte)tilemap[h];
                            int n = tilemap[h + 1];
                            for (int e = 1; e <= n; e++)
                            {
                                int to = h + e * 2;
                                if (to + 1 >= tilemap.Length) break;
                                ushort fgCode = BitConverter.ToUInt16(tilemap, to);
                                if (fgCode != 0xFFFF)
                                {
                                    int idx = fgCode & 0x3FF;
                                    int page = idx / 160;
                                    fgTiles.Add(new SortedTile
                                    {
                                        Rx = rx,
                                        Ry = ry - heightOffset + e - bse,
                                        TileCode = fgCode,
                                        Depth = ry * 16 + page + 7
                                    });
                                }
                            }
                        }
                    }
                }
            }

            int W = RoomCols * TileW, H = RoomRows * TileH;   // 1248 x 960

            if (outputLayers)
            {
                // Render and save background-only layer
                var bgImg = new int[W * H];
                foreach (var t in bgTiles)
                {
                    BlitTile(bgImg, W, t.Rx, t.Ry, t.TileCode, tiles0, palettes);
                }
                SavePng(bgImg, W, H, Path.Combine(outDir, baseName + "_bg.png"));

                // Render and save overlay-only layer (sorted by depth)
                var fgImg = new int[W * H];
                var sortedFg = fgTiles.OrderBy(t => t.Depth).ToList();
                foreach (var t in sortedFg)
                {
                    BlitTile(fgImg, W, t.Rx, t.Ry, t.TileCode, tiles0, palettes);
                }
                SavePng(fgImg, W, H, Path.Combine(outDir, baseName + "_fg.png"));
            }

            // Render and save combined depth-sorted image
            var combinedImg = new int[W * H];
            var allTiles = bgTiles.Concat(fgTiles).OrderBy(t => t.Depth).ToList();
            foreach (var t in allTiles)
            {
                BlitTile(combinedImg, W, t.Rx, t.Ry, t.TileCode, tiles0, palettes);
            }
            SavePng(combinedImg, W, H, Path.Combine(outDir, baseName + ".png"));
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
        public static void ExtractDatasBin(
            string binPath,
            string outDir,
            int renderMapSamples = 0,
            bool renderAllMaps = false,
            bool outputLayers = false,
            bool outputExtraGfx = false,
            bool outputRawAssets = false)
        {
            if (!File.Exists(binPath)) throw new FileNotFoundException(binPath);
            Directory.CreateDirectory(outDir);
            var mapsDir = Path.Combine(outDir, "maps");
            int mapsRendered = 0;

            var datas = File.ReadAllBytes(binPath);
            var segments = SplitDatasBin(datas);

            var manifest = outputRawAssets ? new System.Text.StringBuilder() : null;
            if (manifest != null)
            {
                manifest.AppendLine("index,offset,length,kind,subResources,note");
            }

            string rawDir = null;
            if (outputRawAssets)
            {
                rawDir = Path.Combine(outDir, "segments");
                Directory.CreateDirectory(rawDir);
            }

            foreach (var seg in segments)
            {
                var kind = Classify(seg.Data);
                string note = "";
                int subCount = 0;

                string baseName = $"seg_{seg.Index:D4}_off{seg.Offset:X8}";
                if (outputRawAssets && rawDir != null)
                {
                    File.WriteAllBytes(Path.Combine(rawDir, baseName + ".bin"), seg.Data);
                }

                switch (kind)
                {
                    case SegmentKind.Ez:
                        if (outputRawAssets && rawDir != null)
                        {
                            try
                            {
                                var dec = DecompressEZ(seg.Data);
                                File.WriteAllBytes(Path.Combine(rawDir, baseName + ".ez.raw"), dec);
                                note = $"decompressed 0x{dec.Length:X}";
                            }
                            catch (Exception ex) { note = "EZ decode failed: " + ex.Message; }
                        }
                        break;

                    case SegmentKind.Container:
                        var subs = SplitContainer(seg.Data);
                        subCount = subs.Count;
                        uint mapId = seg.Data.Length >= 0x20 ? BitConverter.ToUInt32(seg.Data, 0x1c) : 0;

                        if (outputRawAssets && rawDir != null)
                        {
                            var subDir = Path.Combine(rawDir, baseName);
                            Directory.CreateDirectory(subDir);
                            var subNotes = new List<string>();
                            for (int s = 0; s < subs.Count; s++)
                            {
                                File.WriteAllBytes(Path.Combine(subDir, $"sub{s}.bin"), subs[s]);
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
                            note = $"mapId={mapId} [{string.Join(" ", subNotes)}]";
                        }

                        if (renderAllMaps || mapsRendered < renderMapSamples)
                        {
                            try
                            {
                                DecodeMapContainer(seg.Data, mapsDir, (int)mapId, outputLayers, outputExtraGfx);
                                mapsRendered++;
                            }
                            catch (Exception ex)
                            {
                                note += " mapRenderFail:" + ex.Message;
                            }
                        }
                        break;

                    case SegmentKind.Raw16:
                        if (outputRawAssets)
                        {
                            note = "candidate 16bpp; width guesses: " +
                                   string.Join("/", WidthCandidates(seg.Length, 2));
                        }
                        break;

                    case SegmentKind.Raw4:
                        if (outputRawAssets)
                        {
                            note = "candidate 4bpp; width guesses: " +
                                   string.Join("/", WidthCandidates(seg.Length, 4));
                        }
                        break;
                }

                if (manifest != null)
                {
                    manifest.AppendLine($"{seg.Index},0x{seg.Offset:X8},0x{seg.Length:X},{kind},{subCount},{note}");
                }
            }

            if (manifest != null)
            {
                File.WriteAllText(Path.Combine(outDir, "manifest.csv"), manifest.ToString());
            }
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

        // ---------------------------------------------------------------------
        // sub3 entity / sprite database — structural verification (READ-ONLY)
        //
        // Layout reverse-engineered from:
        //   FUN_8002d84c  (relocation / header parser)
        //   FUN_80038af8  (track command + frame/cel-list resolver)
        //   FUN_8002db8c  (cel renderer; cel stride = 0xE = 14 bytes)
        //   FUN_80039b6c / FUN_80039d48 / FUN_80039c84 (entity-def config + pageBase/clutBase)
        //
        // This method ONLY walks and reports the structures so the file layout can be
        // confirmed before any pixel extraction is attempted. It does not decode graphics.
        //
        //   sub3 header = 12 LE u32 words. Every value is a byte offset from the sub3 base;
        //   the engine relocates them to absolute pointers at load time:
        //     w0 -> 20-byte-record table (null-terminated, <=128)
        //     w1 -> 12-byte-record table (null-terminated, <=128)
        //     w2 ->  8-byte-record table (null-terminated, <=128)
        //     w3 -> ENTITY DEFINITION table (u32 offsets; 0 = end, 0xFFFFFFFF = empty, <=256)
        //     w4 -> 256-entry u32 offset table
        //     w5 -> palette table
        //     w6..w11 -> misc pointers
        //
        //   Entity Definition (ONLY the first four dwords are relocated):
        //     +0x00 def[0] -> [animState][dir] track-offset table (14-byte = 7x u16 records)
        //     +0x04 def[1] -> track command-stream base   (track = def[1] + trackOffset)
        //     +0x08 def[2] -> aux/box record base         (6-byte records)
        //     +0x0C def[3] -> frame-data base             (frame = def[3] + frameOff*2)
        //     +0x10..0x17  -> misc config
        //     +0x18..0x1A  -> signed origin offset (x,y,z)
        //     +0x1B..0x1D  -> bounding-box size  (w,h,d)
        //
        //   Track command stream (FUN_80038af8):
        //     (cmd & 0x80) != 0  -> 5-byte FRAME command:
        //        byte0     delay  = cmd & 0x7F
        //        byte1..2  auxIdx  (LE u16) -> def[2] + auxIdx   (0xFFFF = none)
        //        byte3..4  frameOff(LE u16) -> def[3] + frameOff*2 (0xFFFF = none)
        //     cmd == 0x00 -> stop ;  cmd == 0x01 -> loop
        //
        //   Frame  (at def[3] + frameOff*2): byte0 = flags, byte1 = celCount,
        //          then celCount x 14-byte cel descriptors.
        //
        //   Cel descriptor (14 bytes): b0 flags (b0-2 page off, b3 STP, b4-5 ABR),
        //          b1 palIdx, b2 u, b3 v, b4 w, b5 h, b6..b13 = 4 (x,y) vertex pairs.
        // ---------------------------------------------------------------------

        /// <summary>
        /// Walk a raw (un-relocated) <c>sub3</c> buffer and produce a human-readable report of the
        /// entity/sprite database structures, validating the layout against the decompiled engine.
        /// Read-only: decodes no pixels and writes no files.
        /// </summary>
        public static string VerifySub3Layout(byte[] sub3, int maxEntities = 8,
                                              int maxTracksPerEntity = 4, int maxFramesPerTrack = 6)
        {
            var sb = new System.Text.StringBuilder();
            if (sub3 == null || sub3.Length < 0x30)
            {
                sb.AppendLine("sub3 is null or too small to be an entity database.");
                return sb.ToString();
            }
            if (IsEz(sub3))
            {
                sb.AppendLine("sub3 is EZ-compressed; decompressing first.");
                sub3 = DecompressEZ(sub3);
            }

            byte[] buf = sub3;
            bool InRange(long o) => o >= 0 && o < buf.Length;
            uint U32(long o) => (o >= 0 && o + 4 <= buf.Length) ? BitConverter.ToUInt32(buf, (int)o) : 0xFFFFFFFFu;
            ushort U16(long o) => (o >= 0 && o + 2 <= buf.Length) ? BitConverter.ToUInt16(buf, (int)o) : (ushort)0xFFFF;
            byte U8(long o) => InRange(o) ? buf[(int)o] : (byte)0;
            string Hex(long o, int n)
            {
                var parts = new List<string>();
                for (int i = 0; i < n; i++) parts.Add(U8(o + i).ToString("X2"));
                return string.Join(" ", parts);
            }

            int validCels = 0, totalCels = 0;
            int maxPalIdx = -1;
            sb.AppendLine($"sub3 size = 0x{buf.Length:X} ({buf.Length} bytes)");
            sb.AppendLine("--- File header (12 x u32, byte offsets from sub3 base) ---");
            uint[] hdr = new uint[12];
            for (int i = 0; i < 12; i++) hdr[i] = U32(i * 4);
            string[] names = {
                "w0  20-byte recs", "w1  12-byte recs", "w2  8-byte recs",
                "w3  ENTITY DEFS ", "w4  256-off tbl ", "w5  PALETTES    ",
                "w6  misc        ", "w7  misc        ", "w8  misc        ",
                "w9  misc        ", "w10 misc        ", "w11 misc        " };
            for (int i = 0; i < 12; i++)
                sb.AppendLine($"  [{i,2}] {names[i]} = 0x{hdr[i]:X8}{(InRange(hdr[i]) ? "" : "   <-- OUT OF RANGE")}");
            sb.AppendLine($"  ({hdr.Count(h => InRange(h))}/12 header offsets land inside sub3)");

            int defTableOff = (int)hdr[3];
            sb.AppendLine();

            // Palette table (w5): the header stores only the base; the count is bounded by the
            // distance to the next data region (the engine never parses a palette count).
            int palBase = (int)hdr[5];
            if (InRange(palBase))
            {
                int nextRegion = buf.Length;
                for (int i = 0; i < 12; i++)
                    if (i != 5 && InRange(hdr[i]) && hdr[i] > palBase && hdr[i] < nextRegion)
                        nextRegion = (int)hdr[i];
                if (defTableOff > palBase && defTableOff < nextRegion) nextRegion = defTableOff;
                int palBytes = nextRegion - palBase;
                sb.AppendLine($"--- Palette table @ 0x{palBase:X} (w5) ---");
                sb.AppendLine($"  region spans 0x{palBase:X}..0x{nextRegion:X} = 0x{palBytes:X} bytes " +
                              $"=> ~{palBytes / 0x20} palettes of 16 colours (32 bytes each).");
                sb.AppendLine($"  (40/64 = fixed VRAM CLUT reservation height, NOT this file's count.)");
                sb.AppendLine();
            }

            sb.AppendLine($"--- Entity Definition table @ 0x{defTableOff:X} (w3) ---");
            if (!InRange(defTableOff))
            {
                sb.AppendLine("  table offset out of range; aborting (sub3 may be the wrong resource).");
                return sb.ToString();
            }

            var defOffsets = new List<uint>();
            for (int i = 0; i < 256; i++)
            {
                uint e = U32(defTableOff + i * 4);
                if (e == 0) break;             // 0x00000000 terminates the table
                defOffsets.Add(e);
            }
            int emptySlots = defOffsets.Count(e => e == 0xFFFFFFFF);
            sb.AppendLine($"  {defOffsets.Count} slots before terminator ({emptySlots} empty 0xFFFFFFFF, " +
                          $"{defOffsets.Count - emptySlots} populated).");

            int shown = 0;
            for (int i = 0; i < defOffsets.Count && shown < maxEntities; i++)
            {
                uint defOff = defOffsets[i];
                if (defOff == 0xFFFFFFFF) continue;
                if (!InRange(defOff)) { sb.AppendLine($"  [{i}] def offset 0x{defOff:X} OUT OF RANGE"); continue; }
                shown++;

                uint d0 = U32(defOff + 0), d1 = U32(defOff + 4), d2 = U32(defOff + 8), d3 = U32(defOff + 12);
                sb.AppendLine();
                sb.AppendLine($"  Entity[{i}] def @0x{defOff:X}");
                sb.AppendLine($"    def[0] trackTbl=0x{d0:X}  def[1] trackStream=0x{d1:X}  " +
                              $"def[2] aux=0x{d2:X}  def[3] frames=0x{d3:X}");
                sb.AppendLine($"    +0x10..17 cfg = {Hex(defOff + 0x10, 8)}");
                sb.AppendLine($"    origin(x,y,z)=({(sbyte)U8(defOff + 0x18)},{(sbyte)U8(defOff + 0x19)}," +
                              $"{(sbyte)U8(defOff + 0x1a)})  bbox(w,h,d)=" +
                              $"({U8(defOff + 0x1b)},{U8(defOff + 0x1c)},{U8(defOff + 0x1d)})");

                if (!InRange(d0) || !InRange(d1) || !InRange(d3))
                {
                    sb.AppendLine("    (one of def[0/1/3] is out of range; skipping track walk)");
                    continue;
                }

                for (int anim = 0; anim < maxTracksPerEntity; anim++)
                {
                    ushort trackOff = U16(d0 + anim * 14);  // [animState][dir=0] first u16 of the 14-byte record
                    if (trackOff == 0 || trackOff == 0xFFFF)
                    {
                        sb.AppendLine($"    animState[{anim}] dir0 -> (empty)");
                        continue;
                    }
                    long cur = d1 + trackOff;
                    sb.AppendLine($"    animState[{anim}] dir0 -> track @0x{cur:X}");

                    int frames = 0, guard = 0;
                    var visitedInst = new HashSet<long>();
                    while (frames < maxFramesPerTrack && guard++ < 256 && InRange(cur) && visitedInst.Add(cur))
                    {
                        byte cmd = U8(cur);
                        if (cmd == 0x00)
                        {
                            byte nextByte = U8(cur + 1);
                            if ((nextByte & 0x80) != 0)
                            {
                                sb.AppendLine("      [stop]");
                                break;
                            }
                            else
                            {
                                sb.AppendLine($"      [transition] -> animState[{nextByte}]");
                                int direction = 0; // VerifySub3Layout samples dir 0
                                long targetRecord = d0 + nextByte * 14;
                                if (!InRange(targetRecord + direction * 2 + 2)) break;
                                ushort targetTrackOff = U16(targetRecord + direction * 2);
                                if (targetTrackOff == 0 || targetTrackOff == 0xFFFF) break;
                                long targetTrackStart = d1 + targetTrackOff;
                                if (!InRange(targetTrackStart)) break;
                                cur = targetTrackStart;
                                continue;
                            }
                        }
                        else if (cmd == 0x01)
                        {
                            sb.AppendLine("      [loop]");
                            break;
                        }
                        else if ((cmd & 0x80) != 0)
                        {
                            int delay = cmd & 0x7F;
                            ushort auxIdx = U16(cur + 1);
                            ushort frameOff = U16(cur + 3);
                            cur += 5;
                            string aux = auxIdx == 0xFFFF ? "none" : $"0x{auxIdx:X}";
                            if (frameOff == 0xFFFF)
                            {
                                sb.AppendLine($"      [frame] delay={delay,2} aux={aux} frame=<none>");
                            }
                            else
                            {
                                long fo = d3 + frameOff * 2;
                                byte fFlags = U8(fo), celCount = U8(fo + 1);
                                sb.AppendLine($"      [frame] delay={delay,2} aux={aux} " +
                                              $"frame@0x{fo:X} flags=0x{fFlags:X2} cels={celCount}");
                                for (int c = 0; c < celCount; c++)
                                {
                                    long co = fo + 2 + c * 14;
                                    if (!InRange(co + 13)) { sb.AppendLine($"        cel[{c}] OUT OF RANGE"); break; }
                                    byte cf = U8(co), pal = U8(co + 1), u = U8(co + 2), v = U8(co + 3),
                                         w = U8(co + 4), h = U8(co + 5);
                                    int page = cf & 7, stp = (cf >> 3) & 1, abr = (cf >> 4) & 3;
                                    totalCels++;
                                    if (pal > maxPalIdx) maxPalIdx = pal;
                                    if (w > 0 && h > 0 && w <= 255 && h <= 255) validCels++;
                                    if (c < 4) // cap per-frame cel spam in the report
                                        sb.AppendLine($"        cel[{c}] page={page} stp={stp} abr={abr} " +
                                                      $"pal={pal} uv=({u},{v}) wh=({w}x{h}) " +
                                                      $"verts={Hex(co + 6, 8)}");
                                }
                                if (celCount > 4) sb.AppendLine($"        ... ({celCount - 4} more cels)");
                            }
                            frames++;
                        }
                        else
                        {
                            sb.AppendLine($"      [unknown cmd 0x{cmd:X2} @0x{cur:X}]");
                            break;
                        }
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine($"--- Summary: {totalCels} cels parsed, {validCels} with plausible w/h " +
                          $"({(totalCels == 0 ? 0 : 100 * validCels / totalCels)}%). ---");
            sb.AppendLine($"    Highest palIdx referenced by any cel = {maxPalIdx} " +
                          $"(=> at least {maxPalIdx + 1} palettes needed; cross-check vs palette region above).");
            return sb.ToString();
        }

        // ---------------------------------------------------------------------
        // sub3 sprite EXTRACTION
        //
        // VRAM mapping proven from GameInit_LoadDATAS_OffsetTable (LUT builder) +
        // EZ_DecompressToVram (sheet upload) + FUN_8002db8c (cel renderer):
        //   * The sprite sheet EZ-decompresses to N x 0x8000-byte blocks; each block is
        //     one 256x256 4bpp VRAM page (256*256/2 = 0x8000).
        //   * A cel's 3-bit page field (flags & 7) selects the sheet block directly
        //     (pageBase cancels out for both the room and resident databases).
        //   * (u,v) is the pixel coordinate inside that 256x256 page; (w,h) the size.
        //   * palIdx selects one of the sub3 palettes directly (clutBase cancels out).
        //   * 4bpp row stride = 128 bytes; CLUT index 0 = transparent.
        //   * The four cel vertices give the on-screen quad (and encode H/V mirroring).
        // ---------------------------------------------------------------------

        public class AlundraCel
        {
            public int Page, PalIdx, U, V, W, H, Stp, Abr;
            public sbyte[] Vx = new sbyte[4];
            public sbyte[] Vy = new sbyte[4];
        }

        public class AlundraFrame
        {
            public int Entity, Track;
            public long Addr;
            public byte Flags;
            public List<AlundraCel> Cels = new();
        }

        private const int SpritePageW = 256, SpritePageH = 256, SpritePageBytes = 0x8000;

        /// <summary>
        /// Walk a (raw, un-relocated) <c>sub3</c> buffer and collect every distinct frame's cels.
        ///
        /// The walk is deliberately exhaustive and self-validating rather than relying on a fixed
        /// animation/direction table geometry:
        ///   * Every entity-def slot is scanned (0 and 0xFFFFFFFF are treated as empty, not as a
        ///     terminator) so entities defined after a gap are not missed.
        ///   * For each entity the ENTIRE track table (def[0]..def[1]) is swept; every u16 that
        ///     resolves to a valid track command stream is followed. This captures all animation
        ///     states AND all facing directions (front/back/side), not just direction 0.
        ///   * Frames are de-duplicated by file address, so a frame shared by several tracks is
        ///     emitted once. Candidate tracks/frames that fail structural sanity checks are dropped.
        /// </summary>
        public static List<AlundraFrame> CollectFrames(byte[] sub3, int maxEntities = 256)
        {
            var frames = new List<AlundraFrame>();
            if (sub3 == null || sub3.Length < 0x30) return frames;
            if (IsEz(sub3)) sub3 = DecompressEZ(sub3);
            byte[] buf = sub3;

            bool InRange(long o) => o >= 0 && o < buf.Length;
            uint U32(long o) => (o >= 0 && o + 4 <= buf.Length) ? BitConverter.ToUInt32(buf, (int)o) : 0xFFFFFFFFu;
            ushort U16(long o) => (o >= 0 && o + 2 <= buf.Length) ? BitConverter.ToUInt16(buf, (int)o) : (ushort)0xFFFF;
            byte U8(long o) => InRange(o) ? buf[(int)o] : (byte)0;

            int defTableOff = (int)U32(12);          // header word 3 = entity-def table
            if (!InRange(defTableOff)) return frames;

            // Decode + validate a single frame at file offset fo; null if it doesn't look real.
            AlundraFrame? DecodeFrame(int entity, int track, long fo)
            {
                int celCount = U8(fo + 1);
                if (celCount == 0 || celCount > 40) return null;
                long last = fo + 2 + (celCount - 1) * 14;
                if (!InRange(last + 13)) return null;

                var fr = new AlundraFrame { Entity = entity, Track = track, Addr = fo, Flags = U8(fo) };
                int sane = 0;
                for (int c = 0; c < celCount; c++)
                {
                    long co = fo + 2 + c * 14;
                    byte cf = U8(co);
                    var cel = new AlundraCel
                    {
                        Page = cf & 7,
                        Stp = (cf >> 3) & 1,
                        Abr = (cf >> 4) & 3,
                        PalIdx = U8(co + 1),
                        U = U8(co + 2),
                        V = U8(co + 3),
                        W = U8(co + 4),
                        H = U8(co + 5)
                    };
                    for (int q = 0; q < 4; q++)
                    {
                        cel.Vx[q] = (sbyte)U8(co + 6 + q * 2);
                        cel.Vy[q] = (sbyte)U8(co + 7 + q * 2);
                    }
                    if (cel.W > 0 && cel.H > 0 && cel.U + cel.W <= 256 && cel.V + cel.H <= 256) sane++;
                    fr.Cels.Add(cel);
                }
                // Require the majority of cels to address a valid region of a 256x256 VRAM page.
                return sane * 2 >= celCount ? fr : null;
            }

            for (int e = 0; e < 256 && e < maxEntities; e++)
            {
                uint defOff = U32(defTableOff + e * 4);
                if (defOff == 0 || defOff == 0xFFFFFFFF || !InRange(defOff)) continue;

                uint d0 = U32(defOff + 0);           // track table  (animState x direction -> u16)
                uint d1 = U32(defOff + 4);           // track command-stream base
                uint d3 = U32(defOff + 12);          // frame-data base
                if (!InRange(d0) || !InRange(d1) || !InRange(d3)) continue;
                if (d1 <= d0 || d1 - d0 > 0x4000) continue;   // def[0] must precede def[1] sanely

                var seenTracks = new HashSet<long>();
                for (long off = d0; off + 2 <= d1; off += 2)
                {
                    ushort t = U16(off);
                    if (t == 0 || t == 0xFFFF) continue;
                    long trackStart = d1 + t;
                    if (!InRange(trackStart)) continue;
                    if (!seenTracks.Add(trackStart)) continue;

                    var seenFrames = new HashSet<long>();
                    long cur = trackStart;
                    int guard = 0;
                    var visitedInst = new HashSet<long>();
                    while (guard++ < 1024 && InRange(cur) && visitedInst.Add(cur))
                    {
                        byte cmd = U8(cur);
                        if (cmd == 0x00)
                        {
                            byte nextByte = U8(cur + 1);
                            if ((nextByte & 0x80) != 0)
                            {
                                break;
                            }
                            else
                            {
                                int direction = (int)(((off - d0) % 14) / 2);
                                long targetRecord = d0 + nextByte * 14;
                                if (!InRange(targetRecord + direction * 2 + 2)) break;
                                ushort targetTrackOff = U16(targetRecord + direction * 2);
                                if (targetTrackOff == 0 || targetTrackOff == 0xFFFF) break;
                                long targetTrackStart = d1 + targetTrackOff;
                                if (!InRange(targetTrackStart)) break;
                                cur = targetTrackStart;
                                continue;
                            }
                        }
                        else if (cmd == 0x01)
                        {
                            break;
                        }
                        else if ((cmd & 0x80) != 0)
                        {
                            ushort frameOff = U16(cur + 3);
                            cur += 5;
                            if (frameOff == 0xFFFF) continue;
                            long fo = d3 + frameOff * 2;
                            if (!InRange(fo) || !seenFrames.Add(fo)) continue;
                            var fr = DecodeFrame(e, (int)((off - d0) / 2), fo);
                            if (fr != null) frames.Add(fr);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            return frames;
        }

        /// <summary>Split a decompressed sprite sheet into 256x256 4bpp VRAM pages.</summary>
        public static List<byte[]> SplitSpritePages(byte[] sheet)
        {
            var pages = new List<byte[]>();
            if (sheet == null) return pages;
            for (int p = 0; p * SpritePageBytes < sheet.Length; p++)
            {
                var page = new byte[SpritePageBytes];
                int avail = Math.Min(SpritePageBytes, sheet.Length - p * SpritePageBytes);
                Array.Copy(sheet, p * SpritePageBytes, page, 0, avail);
                pages.Add(page);
            }
            return pages;
        }

        /// <summary>Read the 16-colour BGR555 palettes embedded in <c>sub3</c> (header word 5).</summary>
        public static List<List<Color>> ReadSub3Palettes(byte[] sub3)
        {
            var result = new List<List<Color>>();
            if (sub3 == null || sub3.Length < 0x30) return result;
            if (IsEz(sub3)) sub3 = DecompressEZ(sub3);

            uint palBase = BitConverter.ToUInt32(sub3, 0x14);    // word 5
            if (palBase >= sub3.Length) return result;

            // Count is bounded by the gap to the nearest later header offset.
            int next = sub3.Length;
            for (int i = 0; i < 12; i++)
            {
                if (i == 5) continue;
                uint o = BitConverter.ToUInt32(sub3, i * 4);
                if (o > palBase && o < next) next = (int)o;
            }
            int count = Math.Max(0, (next - (int)palBase) / 0x20);
            for (int p = 0; p < count; p++)
            {
                var bytes = new byte[0x20];
                Array.Copy(sub3, (int)palBase + p * 0x20, bytes, 0, 0x20);
                // Index 0 is transparent; remaining indices fully opaque.
                result.Add(ColorHelper.ReadABgr15Palette(bytes, false));
            }
            return result;
        }

        private static byte[] GetVramPage(int Page, List<byte[]> sub2Pages, List<byte[]> sub4Pages, List<byte[]> sub5Pages)
        {
            if (Page < sub4Pages.Count)
                return sub4Pages[Page];
            int idx = Page - sub4Pages.Count;
            if (idx < sub5Pages.Count)
                return sub5Pages[idx];
            return null;
        }

        /// <summary>Composite one frame's cels onto <paramref name="bmp"/> using a shared origin
        /// (originX, originY = the canvas pixel that corresponds to entity-relative coord 0,0).</summary>
        private static bool BlitFrame(AlundraFrame fr, Bitmap bmp, int originX, int originY,
                                       List<byte[]> sub2Pages, List<byte[]> sub4Pages, List<byte[]> sub5Pages,
                                       List<List<Color>> palettes)
        {
            bool any = false;
            for (int i = fr.Cels.Count - 1; i >= 0; i--)
            {
                var cel = fr.Cels[i];
                byte[] page = GetVramPage(cel.Page, sub2Pages, sub4Pages, sub5Pages);
                if (page == null) continue;
                var pal = cel.PalIdx < palettes.Count ? palettes[cel.PalIdx]
                                                      : (palettes.Count > 0 ? palettes[0] : GreyPalette());

                using var celBmp = new Bitmap(cel.W, cel.H, PixelFormat.Format32bppArgb);
                int stride = SpritePageW / 2;
                for (int sv = 0; sv < cel.H; sv++)
                {
                    int srcY = cel.V + sv;
                    if (srcY < 0 || srcY >= SpritePageH) continue;
                    int rowBase = srcY * stride;
                    for (int su = 0; su < cel.W; su++)
                    {
                        int srcX = cel.U + su;
                        if (srcX < 0 || srcX >= SpritePageW) continue;
                        int bi = rowBase + (srcX >> 1);
                        if (bi < 0 || bi >= page.Length) continue;
                        int nib = (srcX & 1) == 0 ? (page[bi] & 0x0F) : (page[bi] >> 4) & 0x0F;
                        if (nib == 0) continue;  // transparent

                        Color c = nib < pal.Count ? pal[nib] : Color.Magenta;
                        celBmp.SetPixel(su, sv, c);
                    }
                }

                bool hFlip = cel.Vx[3] < cel.Vx[0];
                bool vFlip = cel.Vy[3] < cel.Vy[0];
                if (hFlip && vFlip)
                    celBmp.RotateFlip(RotateFlipType.RotateNoneFlipXY);
                else if (hFlip)
                    celBmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
                else if (vFlip)
                    celBmp.RotateFlip(RotateFlipType.RotateNoneFlipY);

                int x0 = cel.Vx[0];
                int x1 = cel.Vx[1];
                int x2 = cel.Vx[2];
                int x3 = cel.Vx[3];
                int y0 = cel.Vy[0];
                int y1 = cel.Vy[1];
                int y2 = cel.Vy[2];
                int y3 = cel.Vy[3];

                int minCelX = Math.Min(Math.Min(x0, x1), Math.Min(x2, x3));
                int maxCelX = Math.Max(Math.Max(x0, x1), Math.Max(x2, x3));
                int minCelY = Math.Min(Math.Min(y0, y1), Math.Min(y2, y3));
                int maxCelY = Math.Max(Math.Max(y0, y1), Math.Max(y2, y3));

                int destW = maxCelX - minCelX;
                int destH = maxCelY - minCelY;
                if (destW <= 0) destW = 1;
                if (destH <= 0) destH = 1;

                int destX = originX + minCelX;
                int destY = originY + minCelY;

                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(celBmp, new Rectangle(destX, destY, destW, destH));
                }
                any = true;
            }
            return any;
        }

        /// <summary>
        /// Extract every distinct sprite frame from a room/entity <c>sub3</c> + its decompressed
        /// sprite sheet. Frames are grouped per entity and rendered on a SHARED canvas (the union
        /// of every cel vertex across all of that entity's frames) so the animation stays aligned
        /// and every PNG for an entity is the same size. Index 0 of each palette is transparent.
        /// </summary>
        public static void ExtractSprites(List<byte[]> subs, string outDir, bool dumpPages = false)
        {
            Directory.CreateDirectory(outDir);
            byte[] sub2 = subs.Count > 2 ? subs[2] : new byte[0];
            byte[] sub3 = subs.Count > 3 ? subs[3] : new byte[0];
            byte[] sub4 = subs.Count > 4 ? subs[4] : new byte[0];
            byte[] sub5 = subs.Count > 5 ? subs[5] : new byte[0];

            if (sub2.Length > 0 && IsEz(sub2)) sub2 = DecompressEZ(sub2);
            if (sub3.Length > 0 && IsEz(sub3)) sub3 = DecompressEZ(sub3);
            if (sub4.Length > 0 && IsEz(sub4)) sub4 = DecompressEZ(sub4);
            if (sub5.Length > 0 && IsEz(sub5)) sub5 = DecompressEZ(sub5);

            var sub2Pages = SplitSpritePages(sub2);
            var sub4Pages = SplitSpritePages(sub4);
            var sub5Pages = SplitSpritePages(sub5);
            var palettes = ReadSub3Palettes(sub3);
            var frames = CollectFrames(sub3);

            if (dumpPages)
            {
                for (int p = 0; p < sub4Pages.Count; p++)
                {
                    for (int pi = 0; pi < Math.Max(1, palettes.Count); pi++)
                    {
                        var pal = pi < palettes.Count ? palettes[pi] : GreyPalette();
                        using var img = Render4bppLinear(sub4Pages[p], pal, SpritePageW);
                        img.Save(Path.Combine(outDir, $"_page{p}_pal{pi}.png"), ImageFormat.Png);
                    }
                }
                for (int p = 0; p < sub5Pages.Count; p++)
                {
                    int pageIdx = sub4Pages.Count + p;
                    for (int pi = 0; pi < Math.Max(1, palettes.Count); pi++)
                    {
                        var pal = pi < palettes.Count ? palettes[pi] : GreyPalette();
                        using var img = Render4bppLinear(sub5Pages[p], pal, SpritePageW);
                        img.Save(Path.Combine(outDir, $"_page{pageIdx}_pal{pi}.png"), ImageFormat.Png);
                    }
                }
            }

            // Group frames per entity so they can share one aligned canvas.
            var byEntity = frames.Where(f => f.Cels.Count > 0)
                                  .GroupBy(f => f.Entity)
                                  .OrderBy(g => g.Key);

            int written = 0, entityCount = 0;
            foreach (var grp in byEntity)
            {
                // Union bounding box of every cel vertex across ALL frames of this entity.
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                foreach (var fr in grp)
                {
                    foreach (var cel in fr.Cels)
                    {
                        for (int q = 0; q < 4; q++)
                        {
                            minX = Math.Min(minX, cel.Vx[q]); maxX = Math.Max(maxX, cel.Vx[q]);
                            minY = Math.Min(minY, cel.Vy[q]); maxY = Math.Max(maxY, cel.Vy[q]);
                        }
                    }
                }

                int w = Math.Max(1, maxX - minX), h = Math.Max(1, maxY - minY);
                if (w > 1024 || h > 1024) continue;  // sanity guard
                entityCount++;

                foreach (var fr in grp)
                {
                    using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                    // originX/Y maps entity-relative (0,0) to the same canvas pixel for every frame.
                    if (!BlitFrame(fr, bmp, -minX, -minY, sub2Pages, sub4Pages, sub5Pages, palettes)) continue;
                    string name = $"ent{fr.Entity:D2}_t{fr.Track:D2}_frm{fr.Addr:X4}.png";
                    bmp.Save(Path.Combine(outDir, name), ImageFormat.Png);
                    written++;
                }
            }
            Console.WriteLine($"ExtractSprites: {sub2Pages.Count + sub4Pages.Count + sub5Pages.Count} total VRAM pages, {palettes.Count} palettes, " +
                              $"{entityCount} entities, {frames.Count} frames -> {written} PNGs in {outDir}");
        }

        /// <summary>
        /// Segment each sheet page into connected "islands" of non-transparent pixels and export
        /// each as its own cropped PNG. This recovers art that is NOT part of the entity cel
        /// database -- dialogue portraits, effect sprites, props, loose creatures -- because that
        /// content is drawn by other engine subsystems and never appears in an entity track/frame.
        ///
        /// Pixels within <paramref name="gap"/> of each other (Chebyshev distance) are treated as
        /// one island, so a portrait whose features have small transparent gaps stays whole. The
        /// correct CLUT for loose art is NOT encoded in the pixels (it is chosen by the engine
        /// subsystem that draws it), so colour selection is intentionally not auto-guessed:
        ///   * <paramref name="forcePalette"/> &gt;= 0 -> render every island with that one palette.
        ///   * <paramref name="allPalettes"/> = true  -> render every island once PER palette into
        ///     <c>{outDir}/pal{n}/</c> subfolders so the right CLUT can be picked by eye.
        ///   * otherwise -> a single best-guess render (most distinct + saturated colours).
        /// </summary>
        public static void ExtractLooseSprites(byte[] spriteSheet, List<List<Color>> palettes,
                                               string outDir, int gap = 1, int minPixels = 24,
                                               int minDim = 5, int forcePalette = -1,
                                               bool allPalettes = false)
        {
            Directory.CreateDirectory(outDir);
            byte[] sheet = IsEz(spriteSheet) ? DecompressEZ(spriteSheet) : spriteSheet;
            var pages = SplitSpritePages(sheet);
            int W = SpritePageW, H = SpritePageH, stride = W / 2;

            int written = 0;
            for (int p = 0; p < pages.Count; p++)
            {
                byte[] page = pages[p];
                int Nib(int x, int y) => (x & 1) == 0 ? page[y * stride + (x >> 1)] & 0x0F
                                                      : (page[y * stride + (x >> 1)] >> 4) & 0x0F;

                var visited = new bool[W * H];
                var stack = new Stack<int>();
                for (int sy = 0; sy < H; sy++)
                    for (int sx = 0; sx < W; sx++)
                    {
                        if (visited[sy * W + sx] || Nib(sx, sy) == 0) continue;

                        // Flood the island, bridging gaps up to `gap` transparent pixels.
                        int minX = sx, maxX = sx, minY = sy, maxY = sy;
                        var members = new List<int>();
                        stack.Push(sy * W + sx);
                        visited[sy * W + sx] = true;
                        while (stack.Count > 0)
                        {
                            int idx = stack.Pop();
                            int x = idx % W, y = idx / W;
                            members.Add(idx);
                            if (x < minX) minX = x; if (x > maxX) maxX = x;
                            if (y < minY) minY = y; if (y > maxY) maxY = y;
                            for (int dy = -gap; dy <= gap; dy++)
                                for (int dx = -gap; dx <= gap; dx++)
                                {
                                    int nx = x + dx, ny = y + dy;
                                    if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                                    int ni = ny * W + nx;
                                    if (visited[ni] || Nib(nx, ny) == 0) continue;
                                    visited[ni] = true;
                                    stack.Push(ni);
                                }
                        }

                        int bw = maxX - minX + 1, bh = maxY - minY + 1;
                        if (members.Count < minPixels || bw < minDim || bh < minDim) continue;

                        if (allPalettes && palettes.Count > 0 && forcePalette < 0)
                        {
                            for (int pi = 0; pi < palettes.Count; pi++)
                            {
                                string sub = Path.Combine(outDir, $"pal{pi}");
                                Directory.CreateDirectory(sub);
                                SaveIsland(page, stride, W, members, minX, minY, bw, bh,
                                           palettes[pi], Path.Combine(sub,
                                               $"page{p}_loose{written:D3}_x{minX}_y{minY}.png"));
                            }
                        }
                        else
                        {
                            int palIdx = forcePalette >= 0 && forcePalette < palettes.Count
                                ? forcePalette : PickVividPalette(page, stride, members, W, palettes);
                            var pal = palIdx >= 0 && palIdx < palettes.Count ? palettes[palIdx] : GreyPalette();
                            SaveIsland(page, stride, W, members, minX, minY, bw, bh, pal,
                                       Path.Combine(outDir,
                                           $"page{p}_loose{written:D3}_pal{palIdx}_x{minX}_y{minY}.png"));
                        }
                        written++;
                    }
            }
            Console.WriteLine($"ExtractLooseSprites: {pages.Count} pages -> {written} loose images in {outDir}");
        }

        private static void SaveIsland(byte[] page, int stride, int W, List<int> members,
                                       int minX, int minY, int bw, int bh, List<Color> pal, string path)
        {
            using var bmp = new Bitmap(bw, bh, PixelFormat.Format32bppArgb);
            foreach (int idx in members)
            {
                int x = idx % W, y = idx / W;
                int nib = (x & 1) == 0 ? page[y * stride + (x >> 1)] & 0x0F
                                       : (page[y * stride + (x >> 1)] >> 4) & 0x0F;
                if (nib == 0) continue;
                bmp.SetPixel(x - minX, y - minY, nib < pal.Count ? pal[nib] : Color.Magenta);
            }
            bmp.Save(path, ImageFormat.Png);
        }

        /// <summary>Choose the palette whose mapping of an island's used 4bpp indices yields the
        /// most distinct + most saturated colours (a good proxy for the intended CLUT).</summary>
        private static int PickVividPalette(byte[] page, int stride, List<int> members, int W,
                                            List<List<Color>> palettes)
        {
            if (palettes.Count == 0) return -1;
            var used = new HashSet<int>();
            foreach (int idx in members)
            {
                int x = idx % W, y = idx / W;
                int nib = (x & 1) == 0 ? page[y * stride + (x >> 1)] & 0x0F
                                       : (page[y * stride + (x >> 1)] >> 4) & 0x0F;
                if (nib != 0) used.Add(nib);
            }
            int best = 0, bestScore = int.MinValue;
            for (int pi = 0; pi < palettes.Count; pi++)
            {
                var pal = palettes[pi];
                var distinct = new HashSet<int>();
                int sat = 0;
                foreach (int ci in used)
                {
                    if (ci >= pal.Count) continue;
                    Color c = pal[ci];
                    distinct.Add((c.R << 16) | (c.G << 8) | c.B);
                    int mx = Math.Max(c.R, Math.Max(c.G, c.B)), mn = Math.Min(c.R, Math.Min(c.G, c.B));
                    sat += mx - mn;
                }
                int score = distinct.Count * 32 + sat;
                if (score > bestScore) { bestScore = score; best = pi; }
            }
            return best;
        }

        private static List<Color> GreyPalette()
        {
            var p = new List<Color> { Color.Transparent };
            for (int i = 1; i < 16; i++) p.Add(Color.FromArgb(i * 16, i * 16, i * 16));
            return p;
        }
    }
}
