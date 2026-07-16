# Antman (Korean DOS Game) Graphics & Resource Investigation

During our investigation of the method `FUN_00019e88` and its call tree in Ghidra for the executable `KMAN.EXE`, we uncovered the game's sprite and tile drawing mechanism.

---

## 1. Summary of Findings: The "Compiled Sprite" Architecture
Contrary to typical retro games that use bitmap decompression algorithms (such as RLE, LZW, or Huffman), **Antman does not use an image decompression algorithm**.

Instead, it employs **Compiled Sprites** (and compiled background tiles). The graphics files (`CSPRITE.DAT` and `TILE.DAT`) contain raw x86 machine instructions. The game loads these instructions directly into memory and executes them to render the graphics.

---

## 2. Resource Loading (`FUN_00019e88`)
The method `FUN_00019e88` acts as a resource loader for `CSPRITE.DAT` (retrieved from `0x00032f78`):

1. **Header & Index Table**: 
   - The first 4 bytes of `CSPRITE.DAT` contain the total number of resource records.
   - An index table follows at offset `4`, consisting of 8-byte descriptors for each resource: `[uint32_t offset, uint32_t size]`.
2. **Resource Block Reading**:
   - The function seeks to the offset of the requested resource.
   - It reads `num_elements_A` (4 bytes), and allocates memory for `num_elements_A * 0x4c` bytes. It then reads these elements directly.
   - It reads `num_elements_B` (4 bytes), and allocates memory for `num_elements_B * 8` bytes.
   - For each element in `elements_B`, the first 4 bytes specify a size. The function allocates a buffer of that size and reads the raw data directly from the file into the buffer:
     ```c
     puVar4 = malloc(element_B_size);
     fread(puVar4, element_B_size, 1, file);
     ```
At no point during the loading process is any decompression or translation performed. The bytes are loaded into memory exactly as they exist in the file.

---

## 3. Sprite Rendering (`FUN_0001cc14`)
The drawing function `FUN_0001cc14` demonstrates how these loaded buffers are used:

1. It determines the source block from `elements_B` using metadata inside `elements_A`.
2. It sets up the destination pointer in register `EDI`, pointing to the backbuffer/screen:
   ```assembly
   MOV EDI, dword ptr [0x00035fd8] ; Backbuffer base
   ADD EDI, EDX                   ; X offset
   ADD EDI, [EBP + -0xc]          ; Y * ScreenPitch
   ```
3. It performs a **far indirect call** directly to the loaded buffer data:
   ```assembly
   CALLF [EBP + -0x28]            ; Calling elements_B[idx].data
   ```
This execution of raw loaded bytes confirms that each block in `CSPRITE.DAT` contains executable machine code designed to write pixel values directly to the address in `EDI` and then return via `RETF` / `RET`.

---

## 4. Tile Loading & Rendering (`FUN_00019fe8` & `FUN_0001ccec`)
We observed a matching pattern for background tiles:
- `FUN_00019fe8` loads `"TILE.DAT"` (retrieved from `0x00032f80`) directly into memory and populates the pointer array at `DAT_0003501c`.
- `FUN_0001ccec` renders the background map by calling these tile addresses directly:
  ```c
  local_4c = (&DAT_0003501c)[tile_index];
  (*(code *)&local_4c)(0x150, iVar2 + 4, iVar1 + 0x10, in_CS);
  ```

---

## 5. Map Loading & Level Rendering (`MAP.DAT`)
The level layout is loaded from `"MAP.DAT"` (retrieved from `0x00032f84`):
1. **Map Loading (`FUN_0001a114`)**:
   - The first 4 bytes of `MAP.DAT` contain `num_maps` (9 maps total).
   - An index table follows at offset `4`, consisting of 8-byte descriptors for each map: `[uint32_t offset, uint32_t size]`.
   - The function seeks to the offset and reads:
     - `width` (4 bytes)
     - `height` (4 bytes)
     - `cells` (an array of `width * height` uint32_t cell descriptors).
2. **Cell Interpretation**:
   - `tileIndex = cell & 0xFFFF` (lower 16 bits).
   - `isSolid = (cell & 0x1000000) != 0` (bit 24 dictates collision/wall status).
   - Object/Enemy type index is specified by bits 16-21 and 26-27 (read in `FUN_00013bb0`).
3. **Map Tileset Resource Association**:
   - Level background tiles are drawn from `TILE.DAT` resources based on the level index `L`:
     - `L < 5` -> `resourceIndex = L`
     - `L >= 5` -> `resourceIndex = L - 4`

---

## 6. Tile Transparency and Instruction Emulation
During map rendering, compiled tile blocks use standard x86 block-copy instructions to draw pixels:
- **`MOVSD` (`0xA5`)**: Copies 4 pixels.
- **`MOVSW` (`0x66 0xA5`)**: Copies 2 pixels.
- **`MOVSB` (`0xA4`)**: Copies 1 pixel.

These instructions copy pixels from the source index pointer `ESI` to the destination pointer `EDI`.
- **Solid Pixels**: Written using immediate values, e.g. `MOV [EDI], imm` or `MOV [EDI + displacement], imm`.
- **Transparent Pixels**: Emulated by copying from the background buffer pointer in `ESI` (meaning the background shows through).
- When extracting tiles as separate images with transparent backgrounds, we:
  1. Treat all bytes copied via `MOVSB`/`MOVSW`/`MOVSD` (where `ESI` is a background pointer) as transparent (alpha = 0).
  2. Implement operand size prefix (`0x66`) and displacement-based `MOV` instructions (ModR/M bytes `0x47` and `0x87` for `[EDI + disp8]` and `[EDI + disp32]`).
  3. Support `ADD ESI, EBX` (`01 DE`) where `EBX` is the background pitch.

---

## 7. Conclusion
There is no decompression algorithm to reverse-engineer. To extract the assets and recreate the maps, we:
1. Parse the x86 machine instructions in the compiled sprite and tile blocks (including block copy and memory displacement instructions) to emulate their drawing operations on a virtual screen buffer.
2. Align sprite frames by computing their global bounding boxes based on relative pixel offsets to the anchor point `(0, 0)`.
3. Parse the map cell grid in `MAP.DAT` and paste the reconstructed 16x16 tiles onto large PNG layout canvases.
