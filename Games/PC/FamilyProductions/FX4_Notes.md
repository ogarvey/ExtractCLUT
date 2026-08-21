# Family Productions FX4 format investigation notes

## Ghidra I/O primitives (SH_GAME.EXE)
- `FUN_1000_0906` – low-level file read (DOS/C runtime `read`).
  - Forward callgraph: only calls `FUN_1000_06ba` (error handling).
  - Near callers in segment 1000: `FUN_1000_2c14`, `FUN_1000_38ad`.
- `FUN_1000_07b8` – file seek (`lseek`).
  - Forward callgraph: only calls `FUN_1000_06ba`.
  - Called from many loaders in segments 153b, 1965, 24cc, 273c, 2753, etc.
- `FUN_1000_3287` / `FUN_1000_3423` – create / open file helpers.
  - Both called from `FUN_1000_32b4`, the high-level `fopen` wrapper.
- `FUN_1000_32b4` (`fopen`-like) is used by far loaders in segments 153b, 155f, 2eed, 34ee, etc.

## FX4 loader identified
- `FUN_3367_0237` is the generic FX4 loader.
  - Called from `FUN_14a5_0005` as `FUN_3367_0237(s_option_fx4, 5)`.
  - The same loader is used for all `.FX4` files (option screen and in-game sprites).
- It stores entries in a per-slot structure at `DAT_3c52_7ead + slot * 0x7de`.

## FX4 file layout (verified against `SH.FX4`)
1. 28-byte copyright string + `0x1A` terminator (`(C) Family Production. 1993\x1A`).
2. Seek to offset `0x1C` and read a little-endian word `rawCount`.
3. Number of entries = `rawCount + 1`.
4. For each entry:
   - `byte Width`
   - `byte Height`
   - `ushort CompressedSize` stored **big-endian** (`(byte3 << 8) | byte4`).
   - `CompressedSize` bytes of scanline RLE pixel data.
- Verified: `SH.FX4` has `rawCount = 0x0036` => 55 entries, first entry `166x100`, `CompressedSize = 0x094D = 2381`.

## RLE pixel encoding (verified from `FUN_3367_0d8c` / `FUN_3367_0dd9`)
- Each scanline is decoded independently.
- Loop reading bytes until end-of-line marker `0xFF`:
  1. `skip = next byte`. If `skip == 0xFF`, end of scanline.
  2. `count = next byte`. If `count == 0xFF`, end of scanline.
  3. Copy the next `count` literal bytes to the destination.
- `FUN_3367_0d8c` draws left-to-right; `FUN_3367_0dd9` draws right-to-left (horizontally flipped).
- Transparent/unwritten pixels are simply never touched by the decoder.

## Rendering pipeline
- `FUN_3367_0940(int x, int y, int slot, int entryIndex, int mode)` fetches an entry and calls one of the blitters.
- Entry table fields used at offsets relative to the per-slot base:
  - `+0x0E` = Width
  - `+0x0F` = Height
  - `+0x10` = CompressedSize
  - `+0x14` = XMS offset of the compressed pixel data

## Palette format
- `.PAL` files are 768 bytes = 256 colours * 3 bytes (R, G, B).
- Values are 6-bit VGA DAC components; multiply by `255/63` to get 8-bit RGB.
- Verified against `SH_PAL.PAL` (length 768).

## `.KPF` / headerless `.FX4` cutscene image format
- Files such as `SH-OPEN.FX4`, `SH-PIC.FX4` and `SH_OPEN.KPF` do **not** use the standard sprite FX4 format. They share the same structure and are extracted by `KpfFile`.
- Verified layout:
  - No `(C) Family Production. 1993\x1A` copyright header.
  - First DWORD is little-endian `0x00000001`.
  - 0x40-byte header (not 0x20 as initially assumed).
  - Image width is a little-endian `uint16` at header offset `0x3C`.
  - Pixel data starts at file offset `0x40`.
- Header fields (partially understood):
  - `0x00`: signature `0x00000001`
  - `0x24`: `0x04F0` for most files, `0x03F0` for a few (purpose unknown)
  - `0x2A`: little-endian uint16, likely compressed pixel data size, but decoding exactly that many bytes does not always fill the final scanline
  - `0x3A`: flag byte (`0x01` for most files, `0x00` for `SH_STORY.KPF`)
  - `0x3C`: image width (little-endian uint16)
- RLE compression (matches `FUN_2acb_0004`):
  - If the high two bits of a byte are set (`byte >= 0xC0`), the low six bits are a run count and the next byte is the palette index to repeat.
  - Otherwise the byte is a literal palette index.
- Frame splitting:
  - The decoder expands the whole RLE stream, then splits it into 200-row frames.
  - Any trailing complete scanlines form a final shorter frame; a trailing partial scanline is discarded as padding.
- The standard FX4 loader `FUN_3367_0237` cannot parse these files (word at offset `0x1C` is `0`).
- `Fx4.cs` detects the `01 00 00 00` signature and throws an informative exception; `Program.cs` catches it and falls back to `KpfFile.Load`.
- `Kpf.cs` implements a reader/renderer for the format.

## Verified extraction results for signature `0x00000001` files
| File | Width | Saved frames |
|------|-------|--------------|
| `HIGH.KPF` | 128 | 128×41 |
| `S-STOUT.KPF` | 180 | 180×21 |
| `SH_CLEAR.KPF` | 120 | 120×27 |
| `SH_END.KPF` | 120 | 120×122 |
| `SH_FINAL.KPF` | 120 | 120×200 + 120×63 |
| `SH_MDBOS.KPF` | 120 | 120×200 + 120×3 |
| `SH_OPEN.KPF` | 120 | 120×199 |
| `SH_OVER.KPF` | 120 | 120×114 |
| `SH_ST1.KPF` | 132 | 132×200 + 132×105 |
| `SH_ST2.KPF` | 120 | 120×179 |
| `SH_ST3.KPF` | 140 | 140×200 + 140×2 |
| `SH_ST4.KPF` | 120 | 120×200 + 120×134 |
| `SH_ST5.KPF` | 155 | 155×128 |
| `SH_ST6.KPF` | 120 | 2×120×200 + 120×98 |
| `SH_ST7.KPF` | 120 | 120×200 + 120×70 |
| `SH_STORY.KPF` | 180 | 180×18 |
| `SH-OPEN.FX4` | 120 | 120×196 |
| `SH-PIC.FX4` | 132 | 2×132×200 + 132×32 |

*Note:* Exact frame boundaries for multi-frame files are inferred; the final partial frame may include trailer metadata. The rendered PNGs were visually verified with PIL previews to be coherent images, not random noise.

## Implementation / verification
- Parser + renderer added to `Games/PC/FamilyProductions/Fx4.cs`.
- Test driver added to `Program.cs` using `SH.FX4` and `SH_PAL.PAL`.
- Running the extractor produced 55 PNG frames (`166x100`) in `C:\Dev\Gaming\PC\Dos\DiscImages\Shakii-the-Wolf_DOS_EN\Extracted\FX4`, confirming the header count and RLE decoder.

## `.KPF` extension is overloaded
- The cutscene-image variant is **only** the files whose first DWORD is `0x00000001` and whose width at offset `0x3C` matches the decoded pixel count in a sensible way.
- Other `.KPF` files use unrelated formats and are rejected by signature:
  - `BACK.KPF`, `BACK2.KPF`: `0x0801050A`
  - `MLOCK.KPF`: `0xB1F5B1FF`
  - `ST1.KPF` … `ST7.KPF`: `0x2045474B`
  - `STG1_B1.KPF`: `0xD1FFD1FF`
  - `STG1_B2.KPF`: `0xF4C1F6C1`
  - `STG3_BG.KPF`: `0xE2FFE2FF`
  - `STG5_B1.KPF`: `0xF6C1FAC2`
  - `STG5_B2.KPF`: `0xF6C5FAC1`
  - `STG6_BG.KPF`: `0xA3FFA3FF`
  - `STG7_BG.KPF`: `0x9EC39FC6`
- Standard sprite FX4 equivalents exist for some of these (e.g. `MLOCK.FX4`, `ST1.FX4`).

## Updated KPF / headerless FX4 decoder
- `KpfFile.Load` now decodes the entire RLE stream first, then splits it into frames.
- A frame is 200 rows when enough pixels remain; otherwise the remaining complete scanlines form a shorter overlay.
- Any trailing partial scanline (< width) is treated as padding and discarded.
- `Program.cs` falls back to `KpfFile.Load` when `Fx4File.Load` reports a headerless FX4 / KPF file.

## Verified extraction results for signature `0x00000001` files
| File | Width | Decoded rows | Saved frames |
|------|-------|--------------|--------------|
| `HIGH.KPF` | 128 | 42 | 1 × 128×42 |
| `S-STOUT.KPF` | 180 | 22 | 1 × 180×22 |
| `SH_CLEAR.KPF` | 120 | 28 | 1 × 120×28 |
| `SH_END.KPF` | 120 | 123 | 1 × 120×123 |
| `SH_FINAL.KPF` | 120 | 264 | 1 × 120×200 + 1 × 120×64 |
| `SH_MDBOS.KPF` | 120 | 204 | 1 × 120×200 + 1 × 120×4 |
| `SH_OPEN.KPF` | 120 | 200 | 1 × 120×200 |
| `SH_OVER.KPF` | 120 | 115 | 1 × 120×115 |
| `SH_ST1.KPF` | 132 | 306 | 1 × 132×200 + 1 × 132×106 |
| `SH_ST2.KPF` | 120 | 180 | 1 × 120×180 |
| `SH_ST3.KPF` | 140 | 203 | 1 × 140×200 + 1 × 140×3 |
| `SH_ST4.KPF` | 120 | 335 | 1 × 120×200 + 1 × 120×135 |
| `SH_ST5.KPF` | 155 | 129 | 1 × 155×129 |
| `SH_ST6.KPF` | 120 | 498 | 2 × 120×200 + 1 × 120×98 |
| `SH_ST7.KPF` | 120 | 271 | 1 × 120×200 + 1 × 120×71 |
| `SH_STORY.KPF` | 180 | 19 | 1 × 180×19 |
| `SH-OPEN.FX4` | 120 | 197 | 1 × 120×197 |
| `SH-PIC.FX4` | 132 | 433 | 2 × 132×200 + 1 × 132×33 |

*Notes:*
- Exact frame boundaries for multi-frame files are inferred from the decoded pixel count; the final partial frame may be an artifact of trailing metadata.
- `SH_OPEN.KPF` and `SH-OPEN.FX4` are different assets (200 vs 197 rows) despite the similar name.
