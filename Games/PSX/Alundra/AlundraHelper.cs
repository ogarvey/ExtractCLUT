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
            RenderRoom(subs, envPalettes, outDir, $"map{mapId:D3}_room");

            // Extract individual sprites from sub4 (tiles1) if present
            if (subs[4] != null && subs[4].Length > 0)
            {
                try
                {
                    byte[] tiles1Pixels = IsEz(subs[4]) ? DecompressEZ(subs[4]) : subs[4];
                    if (tiles1Pixels.Length > 0)
                    {
                        // Use tileset palettes (palettes 16 to 31 are object palettes)
                        var objPalettes = palettes.Skip(16).Take(16).ToList();
                        ExtractIndividualSprites(tiles1Pixels, objPalettes, outDir, $"map{mapId:D3}_tiles1_sprites");
                    }
                }
                catch (Exception)
                {
                    // Ignore decompression/extraction errors
                }
            }

            // Extract sprites from sub5 (sprite bank) if present
            if (subs[5] != null && subs[5].Length >= 0x20)
            {
                try
                {
                    var bank = ParseSpriteBank(subs[5]);
                    if (bank.Pixels4bpp != null && bank.Pixels4bpp.Length > 0 && bank.Clut != null && bank.Clut.Count > 0)
                    {
                        var bankPalettes = new List<List<Color>>();
                        for (int p = 0; p < 8; p++)
                        {
                            var palette = bank.Clut.Skip(p * 16).Take(16).ToList();
                            if (palette.Count == 16) bankPalettes.Add(palette);
                        }

                        // Render and save the full sheets first
                        string spritesDir = Path.Combine(outDir, "sprites");
                        Directory.CreateDirectory(spritesDir);
                        for (int p = 0; p < bankPalettes.Count; p++)
                        {
                            var renderPalette = bankPalettes[p].Select((c, idx) => idx == 0 ? Color.Transparent : c).ToList();
                            using var sheet = Render4bppLinear(bank.Pixels4bpp, renderPalette, 256);
                            sheet.Save(Path.Combine(spritesDir, $"map{mapId:D3}_spritesheet_pal{p}.png"), ImageFormat.Png);
                        }

                        // Extract individual cropped cels using Connected Component Bounding Boxes
                        ExtractIndividualSprites(bank.Pixels4bpp, bankPalettes, outDir, $"map{mapId:D3}_bank_sprites");
                    }
                }
                catch (Exception)
                {
                    // Ignore malformed sprite banks
                }
            }
        }

        /// <summary>
        /// Scans a 4bpp linear pixel block and uses Connected Component Analysis (CCA) with recursive
        /// projection-based splitting to find the bounding boxes of all sprites (non-transparent pixel clusters).
        /// Extracts each sprite individually, cropped, under all distinct palettes with index-0 transparency.
        /// </summary>
        public static void ExtractIndividualSprites(byte[] pixels, List<List<Color>> palettes, string outDir, string baseName)
        {
            if (pixels == null || pixels.Length == 0 || palettes == null || palettes.Count == 0) return;

            int width = 256;
            int height = (pixels.Length * 2) / width;
            if (height <= 0) return;

            // Find all sprite bounding boxes in the 4bpp VRAM sheet
            var bounds = FindSpriteBounds4bpp(pixels, width, height);
            if (bounds.Count == 0) return;

            string targetDir = Path.Combine(outDir, "sprites", baseName);
            Directory.CreateDirectory(targetDir);

            for (int i = 0; i < bounds.Count; i++)
            {
                var rect = bounds[i];
                int w = rect.Width;
                int h = rect.Height;

                // Find all unique non-zero color indices (nibbles) used in this sprite cel
                var usedIndices = new HashSet<int>();
                for (int cy = 0; cy < h; cy++)
                {
                    int py = rect.Y + cy;
                    int baseIdx = py * width + rect.X;
                    for (int cx = 0; cx < w; cx++)
                    {
                        int idx = baseIdx + cx;
                        int byteOff = idx / 2;
                        if (byteOff < pixels.Length)
                        {
                            int b = pixels[byteOff];
                            int nib = (idx & 1) == 0 ? b & 0x0F : (b >> 4) & 0x0F;
                            if (nib != 0) usedIndices.Add(nib);
                        }
                    }
                }

                // If the sprite contains only transparent pixels, skip it
                if (usedIndices.Count == 0) continue;

                // For each palette, check if the colors for the used indices are unique.
                // This prevents exporting identical/duplicate images for different palettes.
                var renderedSignatures = new List<string>();

                for (int p = 0; p < palettes.Count; p++)
                {
                    var palette = palettes[p];
                    if (palette.Count < 16) continue;

                    // Build a unique signature of the colors actually used by this sprite in this palette.
                    var sigParts = new List<string>();
                    foreach (int idx in usedIndices.OrderBy(x => x))
                    {
                        var c = palette[idx];
                        sigParts.Add($"{idx}:{c.R},{c.G},{c.B}");
                    }
                    string signature = string.Join(";", sigParts);

                    // Skip if we've already rendered an identical image for this sprite
                    if (renderedSignatures.Contains(signature)) continue;
                    renderedSignatures.Add(signature);

                    using var celBmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                    var bd = celBmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        int[] pal = palette.Select((c, idx) => idx == 0 ? 0 : c.ToArgb()).ToArray();
                        var row = new int[w];
                        IntPtr scan = bd.Scan0;
                        for (int cy = 0; cy < h; cy++)
                        {
                            int py = rect.Y + cy;
                            int baseIdx = py * width + rect.X;
                            for (int cx = 0; cx < w; cx++)
                            {
                                int idx = baseIdx + cx;
                                int byteOff = idx / 2;
                                if (byteOff < pixels.Length)
                                {
                                    int b = pixels[byteOff];
                                    int nib = (idx & 1) == 0 ? b & 0x0F : (b >> 4) & 0x0F;
                                    row[cx] = pal[nib];
                                }
                                else
                                {
                                    row[cx] = 0; // transparent
                                }
                            }
                            System.Runtime.InteropServices.Marshal.Copy(row, 0, scan + cy * bd.Stride, w);
                        }
                    }
                    finally
                    {
                        celBmp.UnlockBits(bd);
                    }

                    string celPath = Path.Combine(targetDir, $"sprite_{i:D3}_pal{p}.png");
                    celBmp.Save(celPath, ImageFormat.Png);
                }
            }
        }

        /// <summary>
        /// Perform a 4-connectivity Breadth-First Search (BFS) directly on raw 4bpp pixels to find the bounding boxes
        /// of all connected non-transparent pixel regions. Uses a recursive projection-based splitting algorithm
        /// to segment merged sprite sheets and tilesets cleanly.
        /// </summary>
        public static List<Rectangle> FindSpriteBounds4bpp(byte[] pixels, int width, int height)
        {
            bool[] visited = new bool[width * height];
            var initialRects = new List<Rectangle>();

            // Convert to 2D grid of nibbles for fast access
            byte[] grid = new byte[width * height];
            for (int i = 0; i < width * height; i++)
            {
                int byteOff = i / 2;
                if (byteOff < pixels.Length)
                {
                    int b = pixels[byteOff];
                    grid[i] = (byte)((i & 1) == 0 ? b & 0x0F : (b >> 4) & 0x0F);
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    if (visited[idx] || grid[idx] == 0)
                    {
                        visited[idx] = true;
                        continue;
                    }

                    // Start BFS for connected component (4-connectivity)
                    int minX = x, maxX = x, minY = y, maxY = y;
                    var queue = new Queue<int>();
                    queue.Enqueue(idx);
                    visited[idx] = true;

                    while (queue.Count > 0)
                    {
                        int curr = queue.Dequeue();
                        int cx = curr % width;
                        int cy = curr / width;

                        if (cx < minX) minX = cx;
                        if (cx > maxX) maxX = cx;
                        if (cy < minY) minY = cy;
                        if (cy > maxY) maxY = cy;

                        // Check 4-connected neighbors
                        int[] dx = { -1, 1, 0, 0 };
                        int[] dy = { 0, 0, -1, 1 };
                        for (int i = 0; i < 4; i++)
                        {
                            int nx = cx + dx[i];
                            int ny = cy + dy[i];
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                int nIdx = ny * width + nx;
                                if (!visited[nIdx])
                                {
                                    visited[nIdx] = true;
                                    if (grid[nIdx] != 0)
                                    {
                                        queue.Enqueue(nIdx);
                                    }
                                }
                            }
                        }
                    }

                    int w = maxX - minX + 1;
                    int h = maxY - minY + 1;
                    // Keep boxes of reasonable size (at least 3x3 to filter out single-pixel stray lines/noise)
                    if (w >= 3 && h >= 3)
                    {
                        initialRects.Add(new Rectangle(minX, minY, w, h));
                    }
                }
            }

            var finalRects = new List<Rectangle>();

            // Local helper function for recursive splitting
            void SplitComponent(Rectangle rect)
            {
                // 1. Trim transparent edges
                var trimmed = GetBBox(grid, width, height, rect);
                if (trimmed == null) return;
                rect = trimmed.Value;

                int x1 = rect.X;
                int y1 = rect.Y;
                int w = rect.Width;
                int h = rect.Height;

                // If size is reasonable, keep it
                if (w <= 48 && h <= 48)
                {
                    finalRects.Add(rect);
                    return;
                }

                // 2. Attempt to split horizontally
                int[] rowCounts = new int[h];
                for (int cy = 0; cy < h; cy++)
                {
                    int rowY = y1 + cy;
                    int count = 0;
                    for (int cx = 0; cx < w; cx++)
                    {
                        if (grid[rowY * width + (x1 + cx)] != 0) count++;
                    }
                    rowCounts[cy] = count;
                }

                int splitY = -1;
                // Avoid splitting too close to the edges (minimum 8 pixels)
                for (int i = 8; i < h - 8; i++)
                {
                    if (rowCounts[i] <= 1)
                    {
                        splitY = y1 + i;
                        break;
                    }
                }

                if (splitY != -1)
                {
                    SplitComponent(new Rectangle(x1, y1, w, splitY - y1));
                    SplitComponent(new Rectangle(x1, splitY + 1, w, y1 + h - (splitY + 1)));
                    return;
                }

                // 3. Attempt to split vertically
                int[] colCounts = new int[w];
                for (int cx = 0; cx < w; cx++)
                {
                    int colX = x1 + cx;
                    int count = 0;
                    for (int cy = 0; cy < h; cy++)
                    {
                        if (grid[(y1 + cy) * width + colX] != 0) count++;
                    }
                    colCounts[cx] = count;
                }

                int splitX = -1;
                for (int i = 8; i < w - 8; i++)
                {
                    if (colCounts[i] <= 1)
                    {
                        splitX = x1 + i;
                        break;
                    }
                }

                if (splitX != -1)
                {
                    SplitComponent(new Rectangle(x1, y1, splitX - x1, h));
                    SplitComponent(new Rectangle(splitX + 1, y1, x1 + w - (splitX + 1), h));
                    return;
                }

                // 4. Fallback: if it is still too large, split by a fixed boundary
                if (w > 48)
                {
                    int sx = x1 + ((w / 2) & ~15);
                    if (sx <= x1 + 8 || sx >= x1 + w - 8)
                    {
                        sx = x1 + w / 2;
                    }
                    SplitComponent(new Rectangle(x1, y1, sx - x1, h));
                    SplitComponent(new Rectangle(sx, y1, x1 + w - sx, h));
                    return;
                }

                if (h > 48)
                {
                    int sy = y1 + ((h / 2) & ~15);
                    if (sy <= y1 + 8 || sy >= y1 + h - 8)
                    {
                        sy = y1 + h / 2;
                    }
                    SplitComponent(new Rectangle(x1, y1, w, sy - y1));
                    SplitComponent(new Rectangle(x1, sy, w, y1 + h - sy));
                    return;
                }

                finalRects.Add(rect);
            }

            foreach (var comp in initialRects)
            {
                SplitComponent(comp);
            }

            return finalRects;
        }

        private static Rectangle? GetBBox(byte[] grid, int width, int height, Rectangle rect)
        {
            int tx1 = rect.X + rect.Width;
            int tx2 = rect.X;
            int ty1 = rect.Y + rect.Height;
            int ty2 = rect.Y;
            bool hasPixels = false;

            for (int y = rect.Y; y < rect.Y + rect.Height; y++)
            {
                for (int x = rect.X; x < rect.X + rect.Width; x++)
                {
                    if (grid[y * width + x] != 0)
                    {
                        hasPixels = true;
                        if (x < tx1) tx1 = x;
                        if (x > tx2) tx2 = x;
                        if (y < ty1) ty1 = y;
                        if (y > ty2) ty2 = y;
                    }
                }
            }

            if (hasPixels)
            {
                return new Rectangle(tx1, ty1, tx2 - tx1 + 1, ty2 - ty1 + 1);
            }
            return null;
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
            byte[] tiles = IsEz(subs[2]) ? DecompressEZ(subs[2]) : subs[2];
            if (tilemap == null || tiles.Length < 0x8000) return;
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
                    BlitTile(img, W, rx, ry, BitConverter.ToUInt16(tilemap, cell + 4), tiles, palettes);
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
                    for (int e = 1; e <= n; e++)
                    {
                        int to = h + e * 2;
                        if (to + 1 >= tilemap.Length) break;
                        BlitTile(img, W, rx, ry + e - bse, BitConverter.ToUInt16(tilemap, to), tiles, palettes);
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
