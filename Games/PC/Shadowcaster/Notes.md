# Shadowcaster Animation Format (.DAT) Analysis

This document details the file format, chunk commands, and compression algorithms used by the DOS game *Shadowcaster* for playing cinematic animations (`TITLE.DAT`, `INTRO.DAT`, `BOARLORD.DAT`, etc.). The analysis is based on reverse-engineering the animation player executable `ADPLAY.EXE`.

---

## 1. File Structure Overview

All animation `.DAT` files follow a structured format starting with a 64-byte (`0x40`) header, followed by a sequence of chunks containing commands and payload data.

### Header (64 bytes)
| Offset | Type | Field | Description / Value |
|--------|------|-------|---------------------|
| `0x00` | `uint16` | Magic Number | Must be `0x0105` (stored as bytes `05 01`) |
| `0x02` | `uint16` | Width | Width of animation frame (typically `320`) |
| `0x04` | `uint16` | Height | Height of animation frame (typically `200`) |
| `0x06` | `uint16` | Num Palettes? | In `TITLE.DAT`, this is `6`. Possibly count of palette sets |
| `0x08`-`0x3F` | `byte[56]` | Reserved | Zero-padded padding up to 64 bytes |

### Chunk Layout
Following the 64-byte header, chunks are stored sequentially until the end of the file. Each chunk has the following layout:
- **Command ID** (`2 bytes`, `uint16`): The operation to execute.
- **Data Length** (`4 bytes`, `uint32`): The length of the payload data.
- **Payload Data** (`Data Length` bytes): The raw bytes processed by the command handler.

---

## 2. Animation Chunk Commands

The player maintains a dispatch table of handlers (up to Command ID `0x32` / 50). The following handlers have been reverse-engineered:

### Command 1: Draw Frame (RLE-compressed Scanlines)
Draws a bounding-box-constrained frame to the destination screen/buffer using row-by-row RLE decompression.
- **Payload Header**:
  - `flags` (`1 byte`): Target screen buffer index (`0` or `1`).
  - `X` (`2 bytes`, `uint16`): Top-left X destination offset.
  - `Y` (`2 bytes`, `uint16`): Top-left Y destination offset.
  - `Width` (`2 bytes`, `uint16`): Width of frame bounding box.
  - `Height` (`2 bytes`, `uint16`): Height of frame bounding box.
- **Payload Body**: Bounded-box RLE-compressed pixels. Decompressed row-by-row:
  - For each row from `0` to `Height - 1`:
    - Set `current_x = X`.
    - While `current_x < X + Width`:
      - Read 1 signed byte `T` from the stream.
      - **If `T > 0`**:
        - Read next `1 byte` as color value `V`.
        - Fill `T` pixels with color `V` starting at `(current_x, Y + row)`.
        - `current_x += T`.
      - **If `T < 0`**:
        - Copy next `-T` raw bytes directly from the stream to pixels starting at `(current_x, Y + row)`.
        - `current_x += -T`.

### Command 2: Draw Frame (LZSS compressed)
Similar to Command 1, but the entire payload is compressed using LZSS.
- **Payload Header**:
  - `Compressed Size` (`4 bytes`, `uint32`): Total size of the compressed block (excluding these 4 bytes).
- **Decompression**: The block is decompressed using the LZSS algorithm (described below) into a temporary buffer, which is then parsed exactly like a Command 1 payload.

### Command 3: Full Screen Frame (Raw or LZSS)
Updates the entire screen buffer (`320x200 = 64000` bytes).
- **Payload Header**:
  - `flags` (`1 byte`): Target screen buffer index (`0` or `1`).
- **If remaining data length is exactly 64000**:
  - The remaining 64000 bytes are copied directly to the target screen buffer.
- **Otherwise**:
  - The remaining payload is treated as LZSS-compressed. It is decompressed into a temporary buffer, and the resulting 64000 raw bytes are copied to the screen buffer.

### Command 4: Palette Update (Custom Runs)
Updates a subset (or all) of the colors in the internal palette buffer.
- **Payload Layout**:
  - `start_index` (`1 byte`): Target palette block index (determines destination address prefix).
  - `num_runs` (`1 byte`): Number of palette color runs to read.
  - For each run:
    - `run_offset` (`1 byte`): Index offset of the run. Adds `run_offset * 3` to the destination offset.
    - `run_length` (`1 byte`): Number of colors to update. If `0`, it is treated as `256`.
    - `RGB data` (`run_length * 3` bytes): 8-bit R, G, B triplets.

### Command 16: Play Sound
Reads a sound/sample command from the stream.
- Plays digital sample or MIDI data (e.g. from sound files like `SAMP.DAT` or CD music track).

### Command 21: Individual Palette Color Update
Performs individual palette/color index updates.
- **Payload**:
  - A sequence of `[count, colors...]` pairs used to update individual palette registers.

### Command 22: Apply/Set Palette
Applies a previously loaded palette to the VGA hardware.
- **Payload**:
  - `palette_index` (`1 byte`): Index of the palette buffer to load and write to the VGA DAC registers via PORT I/O.

### Command 32: Fade Out / Clear Screen
Clears the screen and fades the current VGA palette to black.
- Resets palette buffer values to 0 and clears the target screen buffer.

### Command 33: Step Palette Fade In
Fades the screen palette one step closer to a target palette index.
- **Payload**:
  - `target_palette_index` (`1 byte`).
  - Increments or decrements each RGB component of the active hardware palette by 1 towards the target palette, updating the VGA DAC.

### Command 35: Set Engine Param / Wait
Sets global variables or waits for a specified time frame.
- **Payload**:
  - `param` (`1 byte`): Stored in a global variable to control timing or sync.

---

## 3. Compression Algorithms

### LZSS Decompression
Used in Command 2 and Command 3 to compress large graphics chunks.
- **Ring Buffer (Dictionary)**: `4096` bytes.
  - Initialized to space character `0x20` from index `0` to `4077` (`0xfee`).
  - Ring buffer pointer starts at `0xfee` (index 4078).
- **Control Byte & Bits**:
  - Read a control flag word/byte.
  - Shift control bits right to check individual bits. If the bit counter runs out, read a new control byte from the stream.
  - **If bit == 1**:
    - Read `1 byte` directly from stream, write to output, and write to ring buffer at current index (incrementing index modulo 4096).
  - **If bit == 0**:
    - Read `2 bytes` from stream: `B1` and `B2`.
    - Calculate 12-bit offset: `offset = B1 | ((B2 & 0xF0) << 4)`.
    - Calculate length: `length = (B2 & 0x0F) + 3`.
    - Copy `length` bytes from ring buffer at `offset` to output, also writing them to the ring buffer at the current index (incrementing modulo 4096).

### Lump "RLE0" Compression
Found in low-level resource managers (e.g. `CA_CacheLump` for standard game lumps), though not directly inside `.DAT` chunks.
- **Header**:
  - `4 bytes`: Magic string `"RLE0"`.
  - `4 bytes`: Decompressed size (`uint32`).
- **Decompression Loop**:
  - Read a tag byte `T`.
  - If `T == 0`, exit (decompression complete).
  - Calculate count: `count = T >> 1`.
  - **If `(T & 1) == 0`**:
    - Read `1 byte` value `V` from stream.
    - Write `V` to output `count` times.
  - **If `(T & 1) == 1`**:
    - Copy next `count` bytes directly from the stream to output.
