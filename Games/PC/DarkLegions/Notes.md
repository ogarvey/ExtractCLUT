# Dark Legions - Image Compression & Metadata Format Notes

We investigated the file loading and rendering methods in `DL.EXE` using Ghidra and matched them with the `.DAT`/`.DMP` and `.DAC`/`.DMC` file pairs found in the game directories.

---

## File Format Overview

Images are stored in two distinct paired formats:

### 1. Contiguous Buffer Pairs (`.DAT` / `.DMP`)
- **`.DMP`**: Contains metadata entries, where the `Offset` is a **decompressed offset** pointing into a globally decompressed buffer.
- **`.DAT`**: Contains pixel data compressed as a **single contiguous** RLE-encoded block representing the entire asset file. At load time, the game decompresses the entire `.DAT` file in one pass to memory.

### 2. Stream-Sliced Pairs (`.DAC` / `.DMC`)
- **`.DMC`**: Contains metadata entries, where the `Offset` is a **compressed offset** pointing directly into the `.DAC` file.
- **`.DAC`**: Contains pixel data where **each frame is compressed independently** as its own RLE stream.
- *Why?* This allows the game to keep the `.DAC` asset file in memory compressed and decompress only the specific required frame on-the-fly during rendering, conserving precious conventional DOS RAM.

---

## Metadata Structure (`.DMP` and `.DMC`)

Both metadata formats start with an 8-byte header, followed by a contiguous array of 10-byte entries.

### Header (8 bytes)
- **`0x00` (2 bytes)**: Number of entries (`uint16`, little-endian).
- **`0x02` (6 bytes)**: Reserved / padding (always `0x00`).

### Entry Structure (10 bytes each)
- **`0x00` (4 bytes)**: Offset (`uint32`, little-endian).
  - For `.DMP`: Offset into the **decompressed** DAT buffer.
  - For `.DMC`: Offset into the **compressed** DAC file.
- **`0x04` (1 byte)**: Image Width (`uint8`).
- **`0x05` (1 byte)**: Image Height (`uint8`).
- **`0x06` (1 byte)**: Unknown flags/coordinates (`b6`).
- **`0x07` (1 byte)**: Unknown flags/coordinates (`b7`).
- **`0x08` (1 byte)**: Pivot X coordinate / X offset (`uint8`).
- **`0x09` (1 byte)**: Pivot Y coordinate / Y offset (`uint8`).

*Note: Since each entry is exactly 10 bytes, the 32-bit dword read at offset 8 in assembly overlaps with the first 2 bytes of the next entry, which are ignored during rendering.*

---

## Decompression Logic (`FUN_00012b4c`)

The decompression routine `FUN_00012b4c` processes RLE-compressed data using a **zero-run RLE algorithm**:

- Iterate through the compressed source buffer.
- If a byte is `0x00`:
  1. The next byte is read as the `count`.
  2. Write `count` zero bytes (`0x00`) to the destination buffer.
  3. Advance the source pointer by 2 bytes.
- If a byte is not `0x00`:
  1. Write the byte directly as a literal to the destination buffer.
  2. Advance the source pointer by 1 byte.

### Python Pseudocode
```python
def decompress_rle(compressed_bytes):
    decompressed = bytearray()
    src_idx = 0
    size = len(compressed_bytes)
    while src_idx < size:
        val = compressed_bytes[src_idx]
        if val == 0:
            count = compressed_bytes[src_idx + 1]
            decompressed.extend([0] * count)
            src_idx += 2
        else:
            decompressed.append(val)
            src_idx += 1
    return bytes(decompressed)
```

---

## Bounding Box & Pivot Alignment Logic

To align multiple sprite frames of different dimensions so they draw correctly relative to one another (e.g. for character animations), the game aligns them around a common pivot coordinate.

1. **Calculate the global relative bounding box** across all valid frames:
   ```csharp
   int minX = int.MaxValue;
   int maxX = int.MinValue;
   int minY = int.MaxValue;
   int maxY = int.MinValue;

   foreach (var entry in entries)
   {
       int relLeft = -entry.PivotX;
       int relRight = entry.Width - 1 - entry.PivotX;
       int relTop = -entry.PivotY;
       int relBottom = entry.Height - 1 - entry.PivotY;

       minX = Math.Min(minX, relLeft);
       maxX = Math.Max(maxX, relRight);
       minY = Math.Min(minY, relTop);
       maxY = Math.Max(maxY, relBottom);
   }

   int canvasWidth = maxX - minX + 1;
   int canvasHeight = maxY - minY + 1;
   int canvasPivotX = -minX;
   int canvasPivotY = -minY;
   ```

2. **Position each frame** on the common canvas relative to the common pivot point:
   - Horizontal offset: `xCanvas = canvasPivotX - entry.PivotX`
   - Vertical offset: `yCanvas = canvasPivotY - entry.PivotY`

---

## Palette Format & Scaling (`SK.COL`)

Color lookup tables (e.g., `SK.COL`) contain 256 colors.
- **Header**: 8 bytes.
- **Color Entries**: 768 bytes starting at offset `0x08` (3 bytes per color: RGB).
- **VGA Translation**: 
  - Standard VGA files require translation from 6-bit (`0..63`) to 8-bit (`0..255`) color space.
  - However, `SK.COL` from *Dark Legions* is already saved in **8-bit** format (max component value is `255`), meaning no VGA translation should be applied (`translate: false` in `ColorHelper.ConvertBytesToRgbIS`).
