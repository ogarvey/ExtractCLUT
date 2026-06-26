# Alundra — DATAS.BIN / file-I/O / graphics reverse engineering notes

Ghidra project: `CD-i`. Two binaries analyzed:
- `SLES_011.35` — retail main executable (loaded at `0x80010000`).
- `ALUN_CD.EXE` — **debug/prototype build** (loaded at `0x80010000`); contains the same file API
  plus many extra loose-file strings (e.g. `taki\screen\wind.tx`). This build is the Rosetta stone:
  its loose filenames + load code reveal the structure retail packs into DATAS.BIN.

---

# ALUN_CD.EXE — `.tx` image format (CONFIRMED) + GPU/graphics API

## TL;DR — `.tx` files are headerless raw PSX VRAM blocks

`.tx` = **raw 16-bit PSX VRAM pixel data, no header**. The image is uploaded to VRAM verbatim via
`LoadImage(RECT, data)`; the destination RECT (x,y,w,h) and the pixel bit-depth/CLUT are supplied by
the **caller**, not stored in the file. To decode a `.tx` you must know its RECT + bit-depth from the
code (or brute-force by trying 4bpp/8bpp/16bpp at plausible widths).

### Proof (function `SetupScreen_wind_tx` @ `0x80044864`)

```
LoadFile("taki\screen\wind.tx", DAT_800cb3a0, 0, 0x8000);   // read up to 0x8000 bytes
local_rect = { x=0x380, y=0x100, w=0x40, h=0x100 };          // hard-coded dest RECT
LoadImage(&local_rect, DAT_800cb3a0);                        // upload to VRAM
```
- Load size `0x8000` bytes **exactly equals** `w(0x40) * h(0x100) * 2` → the file is pure 16-bit
  VRAM data with no header/padding.
- RECT w is in **16-bit VRAM words**: `w=0x40` words = 64 halfwords. At `tp=0` (4bpp) that is
  `64*4 = 256` px wide; `h=0x100 = 256` px tall → `wind.tx` is a **256×256 4bpp** texture page region
  (a screen background tile sheet). CLUTs are set up separately (see below).

## GPU / sprite API (renamed in ALUN_CD.EXE)

| Address | New name | Role / signature |
|---|---|---|
| `0x80084cdc` | `LoadImage` | Sony `LoadImage(RECT*, u32* data)` — DMA upload to VRAM (string "LoadImage") |
| `0x80083d6c` | `GetTPage` | `GetTPage(tp, abr, x, y)` — classic PSX tpage-id bit formula |
| `0x80085de4` | `GetClut` | `GetClut(...)` — packs CLUT id (15-bit) |
| `0x800858ac` | `SetSprite_TPage_Clut` | Stores tpage(+4) & clut(+8) into a sprite/texture descriptor (stride `0xc`) |
| `0x80044864` | `SetupScreen_wind_tx` | Loads wind.tx, LoadImages it, builds tpage/clut handles |

`GetTPage(tp,abr,x,y)`: `tp` 0=4bpp,1=8bpp,2=16bpp; `(x>>6)` & `(y>>8)` give the 64×256 VRAM page
grid — matching the `wind.tx` RECT granularity. Several CLUTs are allocated at VRAM y=0x100/0x1c0/
0x380/0x3c0 via `GetTPage(0,0/2,vramX,vramY)`.

## File API in ALUN_CD.EXE == retail (same code)

`LoadFile` (`0x800813e8`) → `LoadFile_impl` (`0x8004e984`) is **byte-identical** to the retail
`LoadFile_impl` (`0x800292c0`): builds `\NAME;1`, requires offset 2KB-aligned (`offset & 0x7ff==0`),
seeks sector `offset>>11`, `CdRead`s into dest. So in this debug build `.tx`/`.R` assets are **loose
named files on the disc**; in retail the same assets are concatenated into DATAS.BIN and addressed by
the boot offset table. The debug filenames therefore name the DATAS.BIN segments.

## Useful leads in ALUN_CD.EXE

- `FUN_80057928(idx)` = object/sprite metadata lookup: `*(*(idx*4 + DAT_80126214) + 0xc) + 2`. Used
  to size a dynamic CLUT (`*0x40 + 0x140`). `DAT_80126214` is an array of object-definition pointers —
  worth following to map sprite/CLUT dimensions.
- Only one `.tx` string is present (`taki\screen\wind.tx`); others are likely built dynamically or
  live in the lookup tables. Search the data segment for more path fragments (`screen\`, `\`, `.tx`,
  `.R`) and for the table at `DAT_80126214`.

## Next steps for `.tx` extraction

1. Find every `LoadImage` call site (xrefs to `0x80084cdc`) — each pairs a **RECT** with a **source
   buffer** that was just `LoadFile`d. That RECT gives you `width(words), height` for the matching
   `.tx`, and nearby `GetTPage`/`GetClut` calls give bit-depth + palette VRAM location.
2. For a `.tx` of `N` bytes with known RECT `w×h` (words): `N == w*h*2`. Decode as `tp`-bpp:
   4bpp → `4*w` px wide, 8bpp → `2*w` px wide, 16bpp → `w` px wide; height `h`.
3. The CLUT for an indexed `.tx` is a separate 16-color (4bpp) or 256-color (8bpp) block of 16-bit
   BGR555 entries, usually loaded/located via the `GetClut` VRAM coords. Look for a sibling palette
   file or a CLUT region uploaded by the same setup function.
4. Reuse project helpers: `Helpers/ColorHelpers.cs` (BGR555), `Helpers/BitmapHelper.cs`,
   `Helpers/CompiledSpriteHelper.cs`.

---

# LoadImage call-site sweep (ALUN_CD.EXE) — RESULTS

Swept all 14 xrefs to `LoadImage` (`0x80084cdc`). Each call pairs a RECT `{x,y,w,h}` (w,h in 16-bit
VRAM words) with the source buffer. **Two big confirmations:**

### A. `DATA\DATAS.BIN` IS read directly by byte offset (even in this build)

Two functions stream graphics straight out of DATAS.BIN using the path string
`"DATA\DATAS.BIN"` (`0x800c52b8`, renamed `s_DATAS_BIN_byteoffset_path`) with **explicit byte
offsets** — exactly the retail mechanism. These are the concrete examples of the offset-table
splitting we've been looking for:

| Func | What it does |
|---|---|
| `0x8002be98` `LoadScreen_DATAS_320x240_16bpp` | Reads 4 segments from DATAS.BIN at offsets from an array (`param_1[i]`), each **`0x9600` bytes**; LoadImages each as RECT **w=0x140, h=0x3c** at y=0 and again at y=0xf0. `0x140*0x3c*2 = 0x9600` → a **320×240 16-bit (BGR555) full-screen image, stored as 4 strips of 60 rows**, double-buffered. |
| `0x8002c74c` `LoadTiles_DATAS_6x_4bpp` | Reads **6 segments** of `0x8000` from `DAT_800e366c + i*0x8000`; LoadImages each as **w=0x40, h=0x100** at x=0x280+i*0x40. Same geometry as `.tx` (64 words × 256). Also builds an in-RAM 16-entry gradient CLUT (`0x421`/`0x7fff`) uploaded at x=0,y=0x1e0,w=0x10,h=0x20. |

**Takeaway:** DATAS.BIN segments are loaded with byte offsets that must be 2KB-aligned
(`offset & 0x7ff == 0`), and decoded as raw VRAM blocks whose dimensions are hard-coded at the call
site — *not* stored in the file. Your header offset table gives the segment boundaries; the
dimensions come from the code (or are inferred from segment size).

### B. `.cl` = the CLUT/palette file that pairs with `.tx` (CONFIRMED)

`0x80044b8c` `SetupScreen_wind_cl`:
```
LoadFile("taki\screen\wind.cl", buf, 0, 0x200);   // 0x200 bytes
LoadImage({x=0x120, y=0x1e0, w=0x10, h=0x10}, buf); // 16x16 = 256 CLUT entries
```
- `.cl` = **raw CLUT**: `0x200` bytes = **256 × 16-bit BGR555 colors** (a full 8bpp palette), headerless.
- Pairs by basename with the `.tx`: `wind.tx` (pixels) + `wind.cl` (palette). **This is the
  extraction key for indexed `.tx` images.** For 4bpp `.tx` a `.cl` may instead be 16 colors (0x20 bytes).
- The `.tx` CLUT is uploaded to a VRAM region; `GetClut(x,y)` packs that location into the CLUT id.

## Sprite / text decoders found in the same sweep (label only)

| Func | Role |
|---|---|
| `0x800476b8` `CompositeSprite_4bpp` | Assembles a 4bpp sprite from pieces described by a **sprite table at `DAT_80099110`, stride 0x14 (20 bytes)**: fields look like `+0x00 width, +0x04 height, +0x08 srcDataPtr, +0x0c srcVramRow, +0x10 ?, +0x10/+0x14 destX/destY`. Nibble-packs into a staging buffer, then LoadImage. The real "compiled sprite" format. |
| `0x8004f1a8` `DrawText_1bpp_to_4bpp` | Font renderer: expands **1bpp glyph bitmaps → 4bpp** (each set bit → nibble `0x5`), 16 px tall, LoadImage w=4 words per char. Not a bitmap asset path. |
| `0x8005afd0` `InitSpriteBank` | Takes a loaded resource block `param_2` with a **header**: `param_2[0]` = offset to pixel data, `param_2[7]` = type/flags (checks magic `0x01010000`). LoadImages the pixel block (16×8 region) and inits per-cel tables (`DAT_800c3c00…`). This is a **structured sprite-bank resource** (unlike raw `.tx`) — a strong candidate format for many DATAS.BIN segments. |
| `0x8002e714` `UploadVRAM_strips_16w` | Generic VRAM uploader, splits tall (>0x200 row) w=0x10 blocks into strips. Used for CLUT/sprite sheets. |

---

# Deep dive: sprite-bank header, bit-depth, DATAS offset table, EZ compression

### Bit-depth helper `UploadVram_bpp` (`0x80083004`) — the bpp Rosetta

Generic `LoadImage`+`GetTPage` wrapper. `param_2` = **bit-depth mode** (= PSX `tp` field), and it
converts a pixel *byte* count to VRAM *word* width accordingly:

| mode | bpp | VRAM width (words) from `nbytes` | px width |
|---|---|---|---|
| 0 | 4bpp | `nbytes >> 2` | `width_words * 4` |
| 1 | 8bpp | `nbytes / 2` | `width_words * 2` |
| 2 | 16bpp| `nbytes` | `width_words * 1` |

Signature: `UploadVram_bpp(void* pixels, int mode, int clutId, short vramX, short vramY, int rectW, short rectH)`.

### Sprite-bank resource header (`InitSpriteBank` `0x8005afd0`)

Loaded whole from a `.BIN` (debug build: `\ARAN\DATA\BG\BG000.BIN`) or a DATAS.BIN segment, then
`InitSpriteBank(slot, bankPtr)`. Layout of `bankPtr` (32-bit LE words):

| Off | Field | Meaning |
|---|---|---|
| `0x00` | `word[0]` = **gfxOff** | byte offset from bank start to the graphics payload |
| `0x1c` | `word[7]` = **flags** | low byte must be non-zero (gate); full word `& 0xffff0000 == 0x01010000` selects a sub-type (`DAT_80185adc`) |

Graphics payload at `bank + gfxOff`:
- `[gfxOff .. gfxOff+0x100)` → **CLUT block**, uploaded as RECT **w=0x10, h=8** (128 words = 256
  bytes = 8 palettes × 16 colors, BGR555). VRAM dest from `DAT_80180f30/34`.
- `[gfxOff+0x100 .. )` → **4bpp pixel data** (mode 0), uploaded as RECT **0x100 × 0x100** (= 1024×256
  px) via `UploadVram_bpp`. VRAM dest from `DAT_8017f618/61c`.

So a sprite-bank = small word header (≥0x20) + optional cel table + `[256-byte CLUT][4bpp pixels]`
payload. The zeroed arrays `DAT_800c3c00…` are runtime cel state, not file data.

### DATAS.BIN master offset table (`GameInit_LoadDATAS_OffsetTable` `0x8002bfe0`)

```
LoadFile("DATA\DATAS.BIN", buf, 0, 0x7b8);   // first 0x7b8 bytes = the index
// then scans entries: size_of_segment(i) = u32[i+1] - u32[i]
```
**DATAS.BIN begins with a u32 little-endian offset table** (`0x7b8` bytes ≈ 494 entries scanned
~483 times). Segment `i` spans `[u32[i], u32[i+1])`. This is the master directory for splitting the
whole archive (same offset-table pattern as SOUND.BIN). Offsets are 2KB-aligned (`& 0x7ff == 0`),
matching `LoadFile`'s sector requirement.

### `EZ` compression (`EZ_DecompressToVram` `0x80080d1c` + `EZ_DecompressTile` `0x80080bc0`) — FULLY REVERSED

`.EZ` files (e.g. `\ARAN\TAKI\DEBUG.EZ`) are LZ77/RLE-compressed graphics.
- **File header:** ASCII `"EZ"` (2 bytes) + 4 more bytes = **6-byte header**, then the compressed
  stream. Decompresses into 64×256-word (`0x40×0x100`) **4bpp tiles**, each uploaded then VRAM-x += 0x40.
- **Escape byte = `0xAD`.** Stream decode (byte `b`):
  - `b != 0xAD` → emit literal `b`.
  - `b == 0xAD`, read `dist = next`:
    - `dist == 0` → escaped literal: emit a single `0xAD` (sequence `AD 00`).
    - else read `count = next`:
      - `count == 0` → **end-of-stream** marker (`AD dist 00`), stop.
      - else → **back-reference**: copy `count` bytes from `dst - dist` (byte-by-byte; overlap
        allowed → acts as RLE). 
- Output is produced in ≤`0x8000`-byte tiles (decoder yields when it has emitted `> 0x7fff`); the
  per-tile output pointer is reset to the tile buffer before each tile.

C# reference port:
```csharp
// src = bytes AFTER the 6-byte "EZ...." header
static byte[] DecompressEZ(byte[] src) {
    var outp = new List<byte>();
    int p = 0;
    while (p < src.Length) {
        byte b = src[p++];
        if (b != 0xAD) { outp.Add(b); continue; }
        if (p >= src.Length) break;
        int dist = src[p++];
        if (dist == 0) { outp.Add(0xAD); continue; }   // escaped literal 0xAD
        if (p >= src.Length) break;
        int count = src[p++];
        if (count == 0) break;                           // end marker
        int from = outp.Count - dist;
        for (int i = 0; i < count; i++) outp.Add(outp[from + i]); // overlap-safe
    }
    return outp.ToArray();
}
```

## Asset format summary (Alundra graphics)

| Ext | Format | Pairs with | Decode |
|---|---|---|---|
| `.tx` | Headerless raw 16-bit VRAM pixel block | a `.cl` | RECT from code; bpp via tpage (4/8/16). `bytes = w_words*h*2` |
| `.cl` | Headerless CLUT: N × BGR555 (256 = 0x200B, 16 = 0x20B) | a `.tx` | direct palette (`ReadABgr15Palette`) |
| `.EZ` | `"EZ"`+4-byte header, then `0xAD`-escaped LZ/RLE → 4bpp 0x40×0x100 tiles | — | `DecompressEZ` then 4bpp decode |
| DATAS segments | Same raw VRAM blocks, concatenated; 2KB-aligned byte offsets | master u32 table @ off 0 | dims hard-coded per call site / inferred from size |
| sprite-bank | Header: `[0]`=gfxOff, `[7]`=flags(magic 0x01010000); payload = 256B CLUT + 4bpp pixels | — | see `InitSpriteBank` / `CompositeSprite_4bpp` |

## Recommended extraction approach (updated)

0. **Split DATAS.BIN** using its master u32 offset table (`GameInit_LoadDATAS_OffsetTable`): read the
   first `0x7b8` bytes as `u32[]`; segment `i = [u32[i], u32[i+1])`.
1. **Decompress `.EZ`/`"EZ"`-magic segments** with `DecompressEZ`, then decode as 4bpp tiles.
2. **Pair `.tx`/DATAS pixel segments with their `.cl`/CLUT** by basename or by adjacency in the
   offset table. Decode pixels as 4/8bpp indexed and apply the BGR555 palette.
3. For headerless segments where bpp is unknown, **brute-force**: a segment of `N` bytes is likely
   `w_words*h*2`; try 4bpp at width `4*w_words`, 8bpp at `2*w_words`, 16bpp at `w_words`, with the
   `.cl` (256→8bpp, 16→4bpp) telling you which.
4. For structured **sprite-bank** segments (magic `0x01010000` at word 7), parse the header per
   `InitSpriteBank`: `word[0]` → gfxOff; payload = 256-byte CLUT (8×16 colors) + 4bpp pixels.
5. Use `CompositeSprite_4bpp`'s table layout (`DAT_80099110`, stride 0x14) as the template for the
   compiled-sprite struct when you reach those segments.

---

# Retail DATAS.BIN — VALIDATED against the real file (108 MB, 0x67E7800)

Ran the prototype extractor (`AlundraHelper.ExtractDatasBin`) on the real
`C:\Dev\Gaming\Sony\PSX\Games\Alundra\DATA\DATAS.BIN`. Results:

### Offset table (confirmed)
- DATAS.BIN opens with a **flat, consecutive u32 little-endian offset table** in the first `0x800`
  bytes — **494 entries**, segment `i = [u32[i], u32[i+1])`. Matches the Ghidra loader
  (`GameInit_LoadDATAS_OffsetTable`, `word[i+1]-word[i]`). All offsets are 2KB-aligned.
- Yields **491 real segments** (2 zero-length entries skipped; e.g. `[1]==[2]`, `[3]==[4]`).
- ⚠️ A naive *pair* reader `(start,end),(start,end)…` (reading 2 u32 per entry) **silently drops the
  gap segments** — e.g. segment #5 at `0x5B000` len `0x30000`. Use the consecutive reader.

### Segment kind distribution (491 total)
| Kind | Count | Notes |
|---|---|---|
| **Container** (map/room) | **483** | 7-u32 header, `off[0]=0x1C`, `off[1]=0x748` (both constant), 6 sub-resources |
| Raw16 (direct-colour) | 4 | `00 80 00 80…` = BGR555 black w/ STP bit; segs #6–9, each `0x9800` |
| Raw4 (indexed) | 3 | boot/UI: seg #0 (`0x38000`), #4 (`0x1800` index), #5 (`0x30000`, `11 11…` fill) |
| EZ (compressed) | 1 | seg #2 `0x21000` → **decompressed `0x40000`** (= 8× `0x8000` 4bpp tiles) ✓ |

**The EZ decompressor was validated on real data** (`0x21000` → `0x40000`, clean 2:1). The first ~10
segments are the boot/title/font assets; segments 10..492 are the **map containers** indexed by
`word[7]` = running map ID `0,1,2,…`.

### Container (map/room) format — CONFIRMED on real data
```
u32 header[7]:   off[0]=0x1C (const)  off[1]=0x748 (const)  off[2..5]=variable  off[6]=end
                 → 6 sub-resources: sub_k = [off[k], off[k+1])
sub0  [0x1C,0x748)  = 0x72C-byte map header/config; its first u32 = map ID (0,1,2,…)
sub1+ = tileset / tilemap / palette payloads (per-map sizes)
```
Sub0 begins `00 00 00 00 | 80 00 00 10 | 00 02 07 00 | FF 00 80 00 …` then an internal pointer list
(`0x0442 0x0463 0x0464 0x0884 0x0CC6 …`). Decoding sub1+ into tiles+palette is the **next milestone**
(needs the per-map CLUT + tilemap sub-format).

### Prototype extractor status (`Games/PSX/Alundra/AlundraHelper.cs`)
- `SplitDatasBin` — consecutive offset-table splitter (491 segs). ✓
- `Classify` / `LooksLikeContainer` — EZ / Container / Raw16 / Raw4 / Small. ✓
- `SplitContainer` — extracts the 6 sub-resources. ✓
- `DecompressEZ` — full `0xAD`-escape LZ/RLE, validated on real EZ segment. ✓
- `ExtractDatasBin` — dumps every segment `.bin`, container sub-resources, EZ `.raw`, + `manifest.csv`.
- Driver wired in `Program.cs`; output → `…\Alundra\Extracted\` (`segments/` + `manifest.csv`).
- **TODO:** decode Raw16/Raw4 + map sub-resources to PNG once the per-map palette/tilemap layout is
  reversed (sub0 pointer list is the entry point).

---

# SLES_011.35 (retail) — DATAS.BIN / file-I/O notes

## TL;DR — the most important finding

In the **main executable**, `DATAS.BIN` is **opened and its disc location cached at boot, but its
content is never read by any code in this binary**. Therefore the offset-table splitter and the
graphics/cel decoders are **NOT in SLES_011.35** — they live in **overlay/resource modules that are
themselves loaded from `DATAS.BIN`** (Alundra streams level/code overlays at runtime).

Proof chain (all verified via xrefs):

- The cached `DATAS.BIN` file descriptor (`DAT_801f0148`, 6 words) and its state flags
  (`DAT_801f0128/012c/0130`) are **written** by `CdInit_OpenArchives` and **read only** by
  `GetFileInfo`.
- `GetFileInfo` (`0x800291dc`) is called by **only one** function: `LoadFile_impl` (`0x800292c0`).
- `LoadFile_impl` is called by **only one** function: `LoadFile` (`0x80028ec8`).
- `LoadFile` has **7 callers — every one of them loads `data\sound.bin`** (audio).
- No code path reaches `GetFileInfo` with a name that resolves to `\DATA\DATAS.BIN;1`.

So: stop hunting for the DATAS parser inside SLES_011.35. Treat DATAS.BIN as an archive of
overlay/resource segments and reverse it by **content format**, segment-by-segment.

## Two functions originally found via the Strings panel

Both belong to the **CD / file-I/O layer** (Sony `libcd` + an ISO-9660 wrapper). Neither parses
DATAS.BIN's internal structure — they only locate/load bytes off the disc.

- `FUN_80028f38` → **`CdInit_OpenArchives`** — one-time boot init of the CD driver; opens and caches
  the disc location of `\DATA\DATAS.BIN;1` and `\DATA\SOUND.BIN;1`.
- `FUN_800291dc` → **`GetFileInfo`** — returns a file's CdlLOC (LBA) + size by name; special-cases
  the two cached archives, otherwise falls back to `CdSearchFile`.

## Function map (renamed in Ghidra)

| Address | New name | Role |
|---|---|---|
| `0x80028f38` | `CdInit_OpenArchives` | Boot CD init; caches DATAS/SOUND descriptors |
| `0x800291dc` | `GetFileInfo` | Resolve file name → LBA+size (sole reader of DATAS descriptor) |
| `0x80028ec8` | `LoadFile` | Public loader wrapper (thin) |
| `0x800292c0` | `LoadFile_impl` | `LoadFile(name, dst, byteOffset, length)`; seeks `byteOffset>>11` sector, CdReads |
| `0x80029cbc` | `strcmp` | libc strcmp |
| `0x80029e60` | `cd_debug_printf` | Debug printf (gated by `DAT_80147c34` verbosity) |
| `0x80030e84` | `CdSetDebug` | Sets `DAT_80147c34` debug level |
| `0x800311d8` | `CdControlB` | Blocking CD command |
| `0x80031ff0` | `cd_command` | Raw CD command engine (`CDROM_REG0/1/2`) |
| `0x80031aa4` | `CdSync` | Wait for CD command completion |
| `0x80032c7c` | `CdSearchFile` | ISO-9660 path search (splits on `\`) |
| `0x80032f80` | `CD_newmedia` | Reads PVD (`CD001`) + root dir; builds dir table |
| `0x80033304` | `CD_cachefile` | Caches a directory's file records |
| `0x8003325c` | `CD_searchdir` | Linear search of cached dir table |
| `0x800335a8` | `cd_read_sectors` | `CdRead` N sectors at LBA into buffer |
| `0x800314ac` | (CdlLOC → LBA extractor) | Converts BCD min:sec:frame → absolute sector |
| `0x800263e4` | (resource loader, dual backend) | Loads `DATA\ETC_RES.R` etc. |

## File-I/O architecture (verified)

```
game code
  └─ LoadFile(name, dst, byteOffset, length)        0x80028ec8
       └─ LoadFile_impl                              0x800292c0
            ├─ GetFileInfo(name) → CdlLOC + size     0x800291dc
            │     ├─ "\DATA\DATAS.BIN;1" → cached DAT_801f0148  (CACHED, never used for content)
            │     ├─ "\DATA\SOUND.BIN;1" → cached DAT_801e7cf0
            │     └─ else → CdSearchFile (ISO-9660 dir walk)
            └─ cd_read_sectors @ (baseLBA + byteOffset>>11)
```

### Generic resource loader — `0x800263e4(name, dst, size)`

Dual backend selected by global `DAT_800436b0` (dev-vs-retail switch):
- `== 1` → **PSX BIOS filesystem** syscalls: open `trap(0x103)`, read (0x8000-byte chunks via
  `FUN_800360a4`), close `trap(0x104)`. Used on a dev/host setup.
- `== 0` → **direct CD read**: builds `\%s;1`, calls `FUN_80033ce4` (CdReadFile-like).
- `== -1` → disabled.

Boot uses it for `DATA\ETC_RES.R` (a `.R` resource file, 0x3000 bytes). `.R` = Alundra "resource".

## The sub-file offset-table pattern (confirmed via SOUND.BIN loaders)

The SOUND.BIN audio loaders are structurally identical to what DATAS.BIN almost certainly uses, and
they confirm the user's offset-table theory:

- An **in-RAM table of 32-bit byte offsets** (e.g. `DAT_8012c3bc`, `DAT_8012c3c0`, `DAT_8012c3c4`…).
- Sub-file *i* spans **`[offset[i], offset[i+1])`** → `size = offset[i+1] - offset[i]`.
- Loaded via `LoadFile("data\sound.bin", dst, offset[i], size)`, **streamed in `0x10000`-byte
  chunks**, then handed to a decoder (`SsVabOpenHead` / `SsVabTransBody` / `SsSeqOpen` for audio).

Relevant SOUND loaders (parallels for any future DATAS work):
- `0x80028264` — VAB header + body upload (whole-bank)
- `0x800283b0` — SE group loader (indexed by group: `&DAT_8012c3c4 + idx*8`)
- `0x80028b38` / `0x80028c78` — SEQ + VAB pair loader (indexed: `&DAT_8012c620 + idx*0xc`)

## Useful globals / addresses

| Symbol | Meaning |
|---|---|
| `DAT_801f0148` (6 words) | Cached `DATAS.BIN` descriptor: CdlLOC(3w) + flags + size |
| `DAT_801e7cf0` (6 words) | Cached `SOUND.BIN` descriptor |
| `DAT_801ca3e0` | SOUND.BIN base LBA (`FUN_800314ac(&DAT_801e7cf0)`) |
| `DAT_800436b0` | Resource backend mode: -1 disabled / 0 CD / 1 BIOS-fs |
| `DAT_80147c34` | CD debug verbosity level |
| `DAT_80131b18` | Set to 1 once archives are open |
| Strings | `\DATA\DATAS.BIN;1` @ `0x8002033c`, `\DATA\SOUND.BIN;1` @ `0x80020350`, `data\sound.bin` @ `0x80020248`, `DATA\ETC_RES.R` @ `0x80020134` |

## Why string search can't find the DATAS parser

DATAS content access uses **base-register + offset addressing** on the cached descriptor (e.g.
`lw v1,0x148(s0)` where `s0 = 0x801f0000`). Ghidra's absolute-address xref panel does NOT resolve
these, so the relevant reader (if any existed in this EXE) wouldn't appear in xrefs to `0x801f0148`.
Combined with the proof chain above, the parser is concluded to be in overlay code, not here.

## Recommended next steps for graphics extraction

1. **Split by the header offset table** (already started): first `~0x800` bytes = array of 32-bit
   byte offsets; segment *i* = `[table[i], table[i+1])`. Watch for whether offsets are byte- or
   sector-granular (the loaders here use **byte** offsets with `>>11` only for CD seeking).
2. **Classify each segment by magic/structure**, not by code:
   - PSX `TIM` images: header `0x10 00 00 00` then type word (8/9 = 4/8-bit CLUT).
   - Cel + CLUT pairs (palette block followed by indexed pixels).
   - Compressed blobs (look for an LZ/RLE header: small size word + control bytes).
   - Overlay code segments (MIPS — start with `addiu sp,sp,-imm`); these contain the real decoders.
3. If a decoder must be reversed, **load a DATAS overlay segment into Ghidra at its runtime base**
   and analyze there — that is where the cel/CLUT logic lives, reusing the helpers in this project
   (`Helpers/ColorHelpers.cs`, `BitmapHelper.cs`, `CompiledSpriteHelper.cs`).
4. Cross-reference with `ETC_RES.R` and other `.R` files on the disc — the `.R` resource format may
   be simpler and document the cel/CLUT layout used throughout the game.

---

# Map/room container sub-resources (DECODED)

Each of the 483 `Container` segments is a self-describing room/map archive. Header at off 0x00
is `0x1C` (size of the 7-entry u32 offset table); a u32 `mapId` lives at off 0x1C. `SplitContainer`
yields 6 sub-resources:

| Sub | Content | Notes |
|---|---|---|
| sub0 | Map header + **tileset CLUT** | constant 0x72C bytes; **32 palettes × 16 BGR555 at +0x10** (the real tile CLUT) |
| sub1 | **Tilemap grid** + object table | 52×60 grid of 8-byte cells at +0x604 (stride 0x1a0); object table after 0x6784 |
| sub2 | **EZ -> 0x30000 4bpp tileset** | flat VRAM bitmap, **256px wide** = 6 stacked 256x256 pages; tiles are 24×16 |
| sub3 | Entity/sprite placement records | incrementing IDs (0x22,0x23,..) + X/Y coords, 0xFFFF padded |
| sub4 | EZ -> variable secondary tileset | often 0 length; same flat 4bpp format as sub2 |
| sub5 | Sprite bank (InitSpriteBank format) | `word[0]`=gfxOff(=0x34); CLUT 8x16 at [gfxOff,gfxOff+0x100); 4bpp pixels after |

## Tileset geometry — CONFIRMED CORRECT

sub2/sub4 decompress (EZ) to a **flat 4bpp bitmap, 256 pixels wide** (low-nibble-first), NOT a
16x16 tile sheet. Rendered linearly with `Render4bppLinear` (LockBits-optimised) the output is
clean, recognisable Alundra environment art across every sampled map (brick walls, foliage/trees,
barrels, ledges, creatures). Validated visually on maps 0,3,5,7. The 16x16-tile interpretation was
wrong (streaky) and is no longer used.

`ExtractDatasBin(bin, out, renderMapSamples:N)` now renders the first N map tilesets to
`<out>/maps/map{NNN}_tiles{0|1}_pal{p}.png` plus per-palette swatch strips.

## OPEN ITEM — tileset colour CLUT is shared/global (SUPERSEDED — see below)

> **CORRECTION (resolved):** The earlier conclusion that the tileset CLUT was "shared/global / not
> stored per-map" was **WRONG**. The CLUT *is* in the container, at **sub0 + 0x10** (32 palettes ×
> 16 BGR555 colours). The grey ramp seen earlier was a *debug-build placeholder* CLUT, not the real
> data. See the next section for the full, verified pipeline.

## Tileset CLUT + tilemap format — FULLY DECODED & VALIDATED

Reversed against the **debug build (Ghidra port 8192, ALUN_CD.EXE — has named functions)**.

### CLUT location (sub0 + 0x10)
- `LoadTiles_DATAS_6x_4bpp` (`0x8002c74c`): the retail/real branch uploads the CLUT from
  `DAT_800e367c + 0x10` to VRAM `(x=0, y=0x1e0)`, size **16 wide × 32 tall = 32 palettes × 16
  colours**. The debug branch instead builds a fake black/white ramp (`0x421`/`0x7fff`) — that was
  the grey ramp.
- `FUN_8002cc9c(CLUT_ptr, cfg, tileset_ptr)` sets `DAT_800e367c`=CLUT, `DAT_800e3678`=tileset,
  `DAT_800dcbb4`=tilemap. So **CLUT base = sub0 pointer**, and the 32 palettes live at
  `sub0 + 0x10 + pal*0x20` (0x20 bytes = 16 × u16 BGR555 each).
- `AlundraHelper.ReadContainerCluts(sub0)` reads exactly this. Verified visually: tiles render in
  correct olive/tan/green game colours.

### Tilemap grid (sub1 / `DAT_800dcbb4`), from `FUN_8002d64c` / `FUN_8002cde4`
- Grid of **8-byte cells**, row stride **0x1a0** (= 52 cells), grid base offset **0x604**,
  bounds **52 cols × 60 rows**. `52*60*8 + 0x604 ≈ 0x6784` (≈ sub1 length).
- After the grid (offset `0x6784`) is the object/overlay table indexed by the cell's `@6` field.

### 8-byte cell format (from the `FUN_8002cde4` draw loop)
```
u16 @0  type/height  collision/zone info (not graphics)
u16 @2  height2      byte@3 = vertical tile offset (usually 0); byte@2 = height/zone (not gfx)
u16 @4  tile         0xFFFF = empty; else  idx = tile & 0x3FF (valid < 0x3C0=960)
                                            palette = (tile >> 12) & 0xF
u16 @6  overlay      0xFFFF = none, else index into the foreground-strip table at sub1 + 0x6784
```

### Per-tile palette selection — how the PSX picks the CLUT (KEY)
On PSX each textured primitive carries its own **CLUT id** (packed VRAM x/y of the palette). The
background draw sets it as `clutId = clutTable[tile >> 12]`, where `clutTable = DAT_800c9438`
(`= DAT_800e3668`). `clutTable` is built in `GameInit_LoadDATAS_OffsetTable` (`0x8002bfe0`) by
`GetClut(x, y)` over `x ∈ {0,64,128,192,256}`, `y ∈ {480..511}` → the first 32 entries are
`GetClut(0, 480+N)`, i.e. **nibble N → the CLUT at VRAM (0, 480+N) → sub0 palette N**. So the
graphics palette for a background tile is simply `(tile >> 12) & 0xF` indexing the 32 sub0
palettes (only 0–15 reachable for background; objects can use 16–31 via a base offset).

### CLUT index 0 is TRANSPARENT (not the STP bit)
For 4bpp textured tiles the hardware treats **colour index 0 as fully transparent**, regardless of
the STP/alpha bit value of that palette entry. The renderer must skip nibble 0 (leave the
destination unchanged) — drawing `palette[0]` as a solid colour floods every tile's gaps and
produces a washed-out, low-contrast image. `ReadContainerCluts` therefore reads palettes as
**opaque** (`translucent: false`); transparency is handled per-pixel in `BlitTile`.

### Foreground/overlay layer (cell @6 → strip table at sub1 + 0x6784)
When `@6 != 0xFFFF` it indexes a vertical foreground strip at `sub1 + 0x6784 + @6*2`:
```
byte[0] = signed base offset
byte[1] = N (tile count)
u16[1..N] = N foreground tiles (same idx/palette packing)
```
Element `e` (1..N) draws at map row `ry + e - base`, same column — a vertical stack of foreground
tiles (tree/pillar tops, wall caps) composited **on top of** the background layer. This is the
second tile layer; both layers sample the same sub2 tileset.

### Objects/sprites are a separate path (`FUN_8002db8c`)
Movable objects/NPCs are drawn by `FUN_8002db8c` from 0xE-byte records as 4-vertex quads. **This
is where H/V flip lives** (`flag & 8` enables flip, `(flag & 0x30) >> 4` = flip mode) and it uses
the same `DAT_800c9438` CLUT table with a per-object base (so objects can reach palettes 16–31).
Background tiles themselves are never flipped. Object rendering is not yet implemented.

### Tile dictionary is PROCEDURAL (not stored), from `FUN_8002caf8`
The 960-entry tile dictionary (`DAT_800dc070`, 3 bytes/entry `{page, U, V}`) is generated in
code, **not** read from the container. Each tile is **24 × 16 px**:
```
960 tiles = 6 pages × 16 rows × 10 cols
idx -> page = idx / 160; rem = idx % 160; trow = rem / 10; tcol = rem % 10
       srcU = tcol * 24;  srcV = page * 256 + trow * 16   (in the 256px-wide stacked tileset)
```
Animated tiles add `DAT_800e363c[animBank*7]*3` to the dict pointer (the per-frame anim offset);
for a static snapshot animBank=0 → no offset. This matches the camera math in `FUN_8002cde4`
(camX clamped [0,0x39f] then /24 = column; camY clamped [0,0x2cf] then /16 = row).
Room = 52×24 × 60×16 = **1248 × 960 px**.

### Result
`AlundraHelper.RenderRoom` assembles full colour rooms from sub0 (CLUT) + sub1 (tilemap) + sub2
(tileset): background layer (cell @4) then foreground/overlay strips (cell @6), with index-0
transparency and per-tile palettes. `DecodeMapContainer` / `ExtractDatasBin(..., renderMapSamples:N)`
emit `map{NNN}_room.png`. Validated visually on maps 1, 4, 6, 7: vivid, correctly-coloured
fortresses, villages and forests with waterfalls, walls, trees, paths and buildings. Map 0 is a
debug/test room (tile samples + katakana labels).

**Known limitations / next targets:**
- A few tiles (e.g. the top strip of map 1) render as black fragments — likely animated tiles
  (non-zero `animBank`) or tiles that expect the secondary tileset `sub4` (map 1 is the first with
  a non-empty sub4). Static extraction can't show the animated frame.
- Objects/NPCs (`FUN_8002db8c`, sub3 placement records + flip) are not drawn.
- The cell `@3` vertical offset and exact scanline strip clipping are approximated by a fixed
  24×16 grid; correct for the sampled maps but may misplace rare tall/offset tiles.

