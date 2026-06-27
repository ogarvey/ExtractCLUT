# Alundra Sprite and Entity Animation System Format

This document details the file formats and engine mechanics of the NPC, object, and monster sprite/animation systems in *Alundra* (PSX). The information has been verified directly by decompiling the game's executable (`SLES_011.35` / `ALUN_CD.EXE`) and analyzing the VRAM upload and rendering routines.

---

## 1. Architectural Overview

Every room or map in *Alundra* is stored as a multi-resource container segment in `DATAS.BIN`. When Alundra enters a room, the engine loads this container and splits it into six sub-resources:
- **`sub0`**: Room headers, map palettes, and environment parameters.
- **`sub1`**: Tile properties and collision attributes.
- **`sub2`**: Primary map tileset (compressed with the engine's custom `EZ` compression).
- **`sub3`**: Entity and sprite database. This resource contains all entity definitions, animation tables, animation tracks, frame definitions, cel descriptors, and sprite palettes loaded for the room.
- **`sub4`**: Sprite sheets (compressed with `EZ` compression) uploaded to VRAM (exact pages are resolved via the runtime page LUT — see Section 5).
- **`sub5`**: Supplemental sprite bank or cache uploaded to VRAM (page resolved via the LUT).

### Memory Relocation (`FUN_8002d84c`)
Upon loading `sub3`, the engine executes `FUN_8002d84c` to parse a **12-word (0x30-byte) file header** and relocate raw file offsets into absolute memory pointers. Every header word is a byte offset measured from the start of `sub3`:

| Word | Offset | Target | Record stride / parse rule |
| :--- | :--- | :--- | :--- |
| **0** | 0x00 | 20-byte-record table | walked in 5-dword (20-byte) steps, null-terminated, ≤128 |
| **1** | 0x04 | 12-byte-record table | walked in 3-dword (12-byte) steps, null-terminated, ≤128 |
| **2** | 0x08 | 8-byte-record table | walked in 2-dword (8-byte) steps, null-terminated, ≤128 |
| **3** | 0x0C | **Entity Definitions table** | array of u32 offsets; `0x00000000` = end, `0xFFFFFFFF` = empty slot, ≤256 |
| **4** | 0x10 | 256-entry u32 offset table | each non-empty entry relocated to absolute |
| **5** | 0x14 | **Sprite palette table** | base pointer only (no count parsed here) |
| **6–11** | 0x18–0x2C | Miscellaneous pointers | six absolute pointers |

For each populated entry in the Entity Definitions table, the relocator additionally rewrites the **first four dwords of the entity definition** (its four sub-table offsets) into absolute pointers — see Section 2.

> **Palette count is per-file, not fixed.** The relocator stores only the palette *base pointer* (word 5); it never reads a count. The number of meaningful 16-colour palettes is bounded by the gap between word 5 and the next data region — e.g. in the sample room `sub3` the palettes run `0x440 → 0x544`, i.e. **8 palettes** (0x100 bytes), which matches the fact that its cels only reference `palIdx` 0–7.
>
> The values **40** (resident DB, `FUN_8002de90`) and **64** (room DB, `FUN_8002e028`) are *not* file palette counts — they are the fixed **VRAM CLUT reservation height** passed to `UploadVRAM_strips_16w` (which uploads that many 16-wide CLUT rows, advancing the source 0x20 bytes per row). Any rows beyond the file's real palettes are simply unused VRAM. Each palette is 16 colours (32 bytes).

---

## 2. Entity Definitions (Word 3)

The Entity Definitions table is a lookup table of 32-bit offsets (relocated to absolute pointers) pointing to individual **Entity Definitions**.
- Empty/unused slots are marked with `0xFFFFFFFF`.
- The table ends when an entry with value `0x00000000` is encountered.

### Entity Definition Header
The relocator rewrites the **first four dwords** of each entity definition. These are **not** four alternate animations — they are four *parallel sub-tables* that the track/frame engine indexes into together (confirmed in `FUN_80038af8`):

| Bytes | Field | Role |
| :--- | :--- | :--- |
| 0x00–0x03 | `def[0]` **trackTable** | `[animState][direction]` → track offset. Indexed as `def[0] + animState*14`; each 14-byte record is up to 7 × u16 per-direction track offsets. |
| 0x04–0x07 | `def[1]` **trackStream** | base of the track command streams. A track starts at `def[1] + trackOffset`. |
| 0x08–0x0B | `def[2]` **auxTable** | base of 6-byte auxiliary/hit-box records (indexed by a frame command field). |
| 0x0C–0x0F | `def[3]` **frameTable** | base of frame data. A frame starts at `def[3] + frameOffset*2`. |

The remaining header bytes are **configuration and collision data** — they do *not* encode `pageBase`/`clutBase` (see Section 5 for where those really come from):

| Bytes | Field |
| :--- | :--- |
| 0x10–0x12 | misc config (24-bit value) |
| 0x13 | misc config byte |
| 0x14–0x17 | misc config (4 bytes) |
| 0x18 / 0x19 / 0x1A | signed origin offset **x / y / z** |
| 0x1B / 0x1C / 0x1D | bounding-box size **w / h / d** |

(Sources: `FUN_80039d48` sets up 0x10–0x17; `FUN_80039c84` sets up the 0x18–0x1D origin/bbox fields.)

---

## 3. Animation Tracks (`FUN_80038af8`)

### Track Selection
The engine selects a track from the entity's current animation state and facing direction:
```
record      = def[0] + animState * 14        // 14-byte record = up to 7 × u16
trackOffset = u16 at [record + direction*2]  // 0x0000 / 0xFFFF = empty
track       = def[1] + trackOffset           // start of the command stream
```

### Animation Track Command Stream
Each track is a stream of commands executed by `FUN_80038af8`. **The frame-playback command is 5 bytes** (not 3), and carries **two** 16-bit little-endian indices:

| Command Byte (`cmd`) | Length | Description |
| :--- | :--- | :--- |
| **`0x80` to `0xFF`** | **5 bytes** | **Frame Playback**:<br>• `delay = cmd & 0x7F`<br>• bytes 1–2 = `auxIdx` (LE u16) → `def[2] + auxIdx` (a 6-byte aux/hit-box record; `0xFFFF` = none)<br>• bytes 3–4 = `frameOffset` (LE u16) → `def[3] + frameOffset*2` (the frame; `0xFFFF` = none) |
| **`0x00`** | 1 byte | **Stop**: Halts animation playback on the current frame. |
| **`0x01`** | 1 byte | **Loop**: Resets the playback cursor back to the start of the track. |

> The “aux” record (`def[2]`) is not pixel data — it is a small per-frame attachment/hit-box structure (loaded into the animation instance at `+0x82..0x87` as three shifted origins and three sizes). It can be ignored for sprite extraction, but it explains why the command is 5 bytes rather than 3.

---

## 4. Frame and Cel Structures

There is a **single** frame layout (the previously-documented “Format A / Format B, distance ≥/< 80” distinction does **not** exist in the engine). A frame is located at `def[3] + frameOffset*2` and consists of a 2-byte header followed by a packed array of cels:

#### Frame Header (2 bytes)
- **Byte 0**: `flags` (loaded into the animation instance at `+0x1a8`; used by the depth/priority logic in `FUN_800397f0`).
- **Byte 1**: `celCount` — the **actual** number of cels in the frame (not minus one).

Immediately after the header come `celCount` cel descriptors.

#### Cel Descriptor (**14 bytes**)
The renderer `FUN_8002db8c` advances `0xE` (14) bytes per cel — there is **no** 2-byte padding. Each cel describes a textured quad:
- **Byte 0**: `flags`
  - Bits 0–2: VRAM page LUT offset (added to `pageBase`; see Section 5).
  - Bit 3: Semi-transparency flag (STP / blending enabled).
  - Bits 4–5: Semi-transparency mode (`ABR`): `0` = 50% average, `1` = 100% additive, `2` = 100% subtractive, `3` = 25% additive.
- **Byte 1**: `palIdx` (CLUT LUT offset, added to `clutBase`).
- **Byte 2**: `u` (VRAM U texture coordinate).
- **Byte 3**: `v` (VRAM V texture coordinate).
- **Byte 4**: `width`.
- **Byte 5**: `height`.
- **Bytes 6, 7**: `x0, y0` (signed vertex-0 offset relative to the entity origin).
- **Bytes 8, 9**: `x1, y1` (vertex 1).
- **Bytes 10, 11**: `x2, y2` (vertex 2).
- **Bytes 12, 13**: `x3, y3` (vertex 3).

Horizontal mirroring is encoded directly in the vertex layout: a left-facing cel uses `(x0<x1)` while its right-facing counterpart swaps them (`x0>x1`), reusing the same source texture.

---

## 5. VRAM Layout and Rendering (`FUN_8002db8c`)

The *Alundra* engine utilizes the PlayStation's 2D VRAM framebuffer (1024x512 pixels, organized into 16 horizontal pages of 64x256 pixels at 16-bpp, or 256x256 pixels at 4-bpp).

### `pageBase` / `clutBase` are constants, not entity-def fields
`pageBase` and `clutBase` are **not** read from the entity definition. They are constants chosen by which database owns the entity (`FUN_80039b6c`, stored into the animation instance at `+0x1b0` / `+0x1b4` by `FUN_80039d48`):

| Database | `pageBase` | `clutBase` |
| :--- | :--- | :--- |
| Resident (global) DB | `0x0B` | `0x60` |
| Room DB | `0x00` | `0x20` |

(Room entity-definition indices also have `+0x100` added before lookup — `FUN_80039f9c` / `FUN_8003a08c`.)

### Sprite Sheet Mapping (LUT-based)
The renderer does **not** use a direct `pageBase + (celFlags & 7)` → VRAM-page formula with fixed `<10 / 10–13 / ≥14` thresholds. Instead it resolves the hardware registers through two runtime lookup tables:
```
TPage = (&DAT_800db838)[ABR*0x16 + pageBase + (celFlags & 7)]
CLUT  = (&DAT_800c9438)[clutBase + palIdx]
```
So the cel's 3-bit page field is a **LUT index** (biased by the constant `pageBase`), and `palIdx` is a **CLUT-LUT index** (biased by `clutBase`). These tables are populated at load time (they live in BSS), so the exact page→VRAM-page mapping must be read from a live/loaded instance rather than statically from the file.

> The CLUT region itself is reserved at a fixed VRAM height (40 rows resident @ (192,480), 64 rows room @ (64,480)) by `UploadVRAM_strips_16w`, but only the file's actual palettes (see Section 1) hold meaningful colours.

### Primitive Generation
The rendering function `FUN_8002db8c` iterates over the cels of the active frame and writes hardware-accelerated flat-textured quad primitive packets (`POLY_FT4`) directly to the GPU display list:
- **Texture Coordinates mapping**:
  - Vertex 0 (Top-Left): `(u, v)`
  - Vertex 1 (Top-Right): `(u + width, v)`
  - Vertex 2 (Bottom-Left): `(u, v + height)`
  - Vertex 3 (Bottom-Right): `(u + width, v + height)`
- **Screen Position coordinates mapping**:
  - The vertex coordinates `(x0, y0)` to `(x3, y3)` are added to the entity's screen-space position coordinates `(entityX, entityY)` to position the quad on the screen, taking camera scrolling into account.
- **CLUT and TPage registers**:
  - The calculated hardware CLUT register value (derived from `clutBase + celPalIdx`) and the TPage register value (derived from VRAM page and transparency flags) are written to the primitive packet to configure the PlayStation GPU's texture mapping hardware.
