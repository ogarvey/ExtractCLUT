# Alundra — Map/Room Format

This document explains how a single room (map) in the PSX game *Alundra* is stored on disc
and assembled into a visible image. It is written for someone who has no prior knowledge of
PlayStation hardware, tile-based graphics, or binary data formats.

---

## 1. Background concepts

### What is a tile?

Old 2D games do not store each screen as a single large image. Instead, the game world is
built from a grid of small reusable pictures called **tiles**, much like a mosaic is made
from small coloured squares. In Alundra, every tile is exactly **24 pixels wide × 16 pixels
tall**. A full room is a grid of **52 columns × 60 rows** of these tiles, producing a final
image that is **1248 × 960 pixels**.

### What is a palette (CLUT)?

The PlayStation uses **indexed colour** for most 2D graphics. Instead of storing a full
red-green-blue colour for every pixel, each pixel stores a small number (an *index*) that
points to an entry in a separate table of colours called a **CLUT** (Colour Look-Up Table)
or **palette**. In Alundra the tilesets use **4 bits per pixel** (4bpp), so each pixel's
index is 0–15, and each palette holds 16 colours. The colours themselves are stored in
**BGR555** format: 5 bits each for blue, green and red, packed into a 16-bit word (with one
spare bit for semi-transparency).

This is efficient because many tiles share the same palette, and it allows the game to change
the look of a tile simply by swapping which palette it uses.

### What is VRAM?

The PlayStation has a dedicated block of video memory called **VRAM** (1024 × 512 pixels of
16-bit data). All graphics that the GPU draws must first be uploaded here. VRAM is divided
into "texture pages" — each page is 256 × 256 pixels at 4bpp. The game uploads the tileset
(the sheet of all available tile images) and the palettes to specific locations in VRAM, then
tells the GPU which page and palette to use for each tile it draws.

---

## 2. The DATAS.BIN archive

All of Alundra's game data (except audio) lives in a single file on the disc:
`DATA\DATAS.BIN` (~108 MB). It begins with a flat table of **494 little-endian 32-bit byte
offsets** occupying the first 0x7B8 bytes. Segment *i* spans from `offset[i]` to
`offset[i+1]`. All offsets are aligned to 2 KB boundaries.

Of the ~491 real segments, **483 are room containers** (the map/room archives described in
this document). The remainder are boot/title assets, a compressed global tileset, and
full-screen background images.

Each room container is a self-contained archive holding everything needed to render one room.

---

## 3. Room container structure

A room container begins with a **7-entry u32 little-endian offset table** (28 bytes). These
seven values mark the boundaries of **6 sub-resources** inside the container:

```
offset[0]  offset[1]  offset[2]  offset[3]  offset[4]  offset[5]  offset[6]
   │           │          │          │          │          │          │
   └─ sub0 ────┘─ sub1 ───┘─ sub2 ───┘─ sub3 ───┘─ sub4 ───┘─ sub5 ───┘
```

`offset[0]` is always 0x1C (= 28 bytes, the size of the header itself). `offset[1]` is
always 0x748. Sub-resource *k* is the byte range `[offset[k], offset[k+1])` within the
container.

| Sub | Content | Typical size | Purpose |
|-----|---------|-------------|---------|
| sub0 | Map header + palettes | 0x72C bytes (fixed) | 32 tile palettes + animation/scroll config |
| sub1 | Tilemap grid + overlay table | ~0x7000+ bytes | Which tile goes where, plus foreground strips |
| sub2 | Primary tileset (EZ-compressed) | variable | The sheet of tile images (compressed) |
| sub3 | Entity/sprite placement data | variable | Where objects, NPCs, and interactive items go |
| sub4 | Secondary resource | variable (often empty) | Additional data (per-frame entity resource) |
| sub5 | Sprite bank | variable | Sprite graphics + embedded 8-palette CLUT |

The three sub-resources needed to assemble a room image are **sub0** (palettes), **sub1**
(tilemap), and **sub2** (tileset). The rest are for sprites, objects, and entities which
are drawn on top of the map by a separate system.

---

## 4. Sub0 — Palettes and map header

Sub0 is exactly **0x72C bytes** (1836 bytes) in every container. Its layout:

```
Offset   Size      Content
──────   ────      ───────
0x00     16 bytes  Map header (map ID, config flags, dimensions, etc.)
0x10     0x400     32 palettes × 16 colours × 2 bytes = 1024 bytes of BGR555 colour data
0x410    remaining Animation bank config (scroll speeds, frame counts per tile-animation bank)
```

### Palette block (sub0 + 0x10)

The 32 palettes are stored consecutively. Each palette is **32 bytes**: 16 colours at
2 bytes each in BGR555 format:

```
Bit 15      Bit 14-10    Bit 9-5      Bit 4-0
─────       ─────────    ───────      ───────
STP         Blue (5b)    Green (5b)   Red (5b)
```

The first palette entry (index 0) of every palette is treated as **transparent** by the
PlayStation GPU — regardless of what colour value is stored there. This is a hardware
convention for 4bpp textures, not a software choice. Any pixel whose nibble value is 0 is
simply not drawn, allowing the background or layer below to show through.

The tile field in the tilemap (see §5) encodes which of these 32 palettes to use for each
tile. In practice, background tiles use palettes 0–15 (the top nibble of the tile word),
while palettes 16–31 can be used by objects and overlay elements.

### Animation config (sub0 + 0x420)

Starting at offset 0x420 within sub0 is a small table of **6 animation bank descriptors**,
each 2 bytes (12 bytes total). These control tile-scrolling effects such as flowing water
or flickering torches. For a static extraction (a single frame), these can be ignored —
the "frame 0" tiles are already correct in the tileset.

---

## 5. Sub1 — Tilemap grid and overlay strip table

Sub1 is the map itself — the blueprint that says which tile goes where and what is drawn
on top. It has two regions:

### 5a. Tilemap grid (sub1 + 0x604)

The first 0x604 bytes of sub1 are a secondary header (map dimensions, collision zones,
height data). The actual tilemap grid begins at **offset 0x604**.

The grid is a flat array of **52 columns × 60 rows** of **8-byte cells**, laid out in
row-major order with a row stride of **0x1A0 bytes** (= 52 cells × 8 bytes). Each cell
describes one tile position:

```
Offset   Type    Name       Meaning
──────   ────    ────       ───────
@0       u16     type       Collision / zone info (not used for rendering)
@2       u16     height     Byte @3 = vertical tile offset; byte @2 = height zone
@4       u16     tile       Background tile to draw (see below)
@6       u16     overlay    Foreground strip index, or 0xFFFF for none
```

#### The tile field (@4)

This 16-bit value encodes both the tile index and the palette to use:

```
Bits 15-12    Bits 11-10    Bits 9-0
──────────    ──────────    ────────
Palette       (unused)      Tile index
(0–15)                      (0–959)
```

- **Tile index** (`tile & 0x3FF`): a value from 0 to 959 identifying which 24×16 tile
  to draw from the tileset. The special value **0xFFFF** means "empty — draw nothing."
  Valid indices must be less than 960 (0x3C0).

- **Palette** (`(tile >> 12) & 0xF`): which of the 16 palettes (from sub0) to use when
  colouring this tile. Different tiles can use different palettes, which is how a single
  tileset can produce varied colours across the map (e.g. green grass, brown dirt, and
  grey stone can all come from the same tile shapes with different palettes).

#### The overlay field (@6)

When this value is not 0xFFFF, it indexes into the **overlay strip table** (see §5b) to
draw a vertical stack of foreground tiles on top of the background at this column. This is
used for things like tree canopies, pillar tops, and wall caps that need to appear in front
of the player character.

### 5b. Overlay strip table (sub1 + 0x6784)

Starting at offset 0x6784 within sub1 is a variable-length table of **foreground tile
strips**. When a cell's overlay field is *v* (not 0xFFFF), the entry is at:

```
address = sub1 + 0x6784 + v × 2
```

Each entry has the following structure:

```
Byte 0:   signed base offset (sbyte)
Byte 1:   N — number of tiles in the strip
Bytes 2.. N × 2 bytes — N tile words (same format as the background tile field)
```

The strip draws a vertical column of *N* foreground tiles. Element *e* (counting from 1 to
N) is drawn at map row `(cell_row + e − base)`, in the same column as the cell. The tiles
in the strip use the same palette-and-index encoding as background tiles.

Both the background layer and the overlay layer sample their tile images from the **same
tileset** (sub2). The game does not use a separate tileset for foreground tiles.

> **Note for implementors:** The game engine draws the overlay strip tiles from bottom to
> top (the `do { ... puVar17 = puVar17 + -1; }` loop counts downward), but the visual
> result is the same because each tile overwrites only non-transparent pixels.

---

## 6. Sub2 — Primary tileset (EZ-compressed)

Sub2 contains the actual tile images. It is almost always **EZ-compressed** (identified by
the ASCII bytes `"EZ"` at the start). After decompression, the result is a flat **4bpp
bitmap that is 256 pixels wide** — a vertical stack of up to 6 VRAM pages, each 256 × 256
pixels, giving a total height of up to 1536 pixels.

### The EZ compression format

EZ is a simple LZ77/RLE scheme with escape byte **0xAD**:

| Sequence | Meaning |
|----------|---------|
| Any byte ≠ 0xAD | Emit that byte literally |
| `AD 00` | Emit a literal 0xAD byte |
| `AD dist 00` | End of stream (stop decompressing) |
| `AD dist count` | Copy *count* bytes from output position *(current − dist)* (overlap allowed, acting as RLE) |

The first 6 bytes of the compressed data are a header (`"EZ"` + 4 bytes) and are skipped.

### Tileset layout — 4bpp linear bitmap

After decompression, the data is a **linear 4bpp image, 256 pixels wide**. Each byte
contains two pixels: the low nibble (bits 0–3) is the left pixel, the high nibble
(bits 4–7) is the right pixel. Each nibble is an index (0–15) into whichever palette is
selected for that tile.

The tileset is NOT arranged as discrete 24×16 tile blocks in memory. Instead, it is a
continuous flat bitmap, and the game calculates where each tile's pixels are using the
**procedural tile dictionary** (see §7).

---

## 7. The procedural tile dictionary

The game does **not** store a lookup table mapping tile index to pixel coordinates. Instead,
the function `FUN_8002caf8` generates the dictionary at startup using a fixed formula.
There are **960 tiles** (indices 0–959), arranged as **6 pages × 16 rows × 10 columns**:

```
Given a tile index (0–959):
    page = index / 160          (0–5: which 256×256 page)
    remainder = index % 160
    tile_row = remainder / 10   (0–15: which row of tiles within the page)
    tile_col = remainder % 10   (0–9: which column of tiles within the page)

    srcU = tile_col × 24        (pixel X offset within the 256px-wide bitmap)
    srcV = page × 256 + tile_row × 16   (pixel Y offset in the stacked bitmap)
```

Each tile occupies a **24 × 16 pixel** rectangle starting at `(srcU, srcV)` in the
decompressed tileset bitmap. The 256-pixel width accommodates `10 × 24 = 240` pixels of
tile data per row, with 16 pixels of unused space on the right of each page.

```
        0       24      48      72      ...     216     240  256
        ├───────┼───────┼───────┼───────       ─┼───────┤····│
   0    │ Tile 0│ Tile 1│ Tile 2│ Tile 3  ...   │ Tile 9│pad │  ← row 0, page 0
  16    │Tile 10│Tile 11│Tile 12│ ...           │Tile 19│pad │  ← row 1, page 0
  32    │Tile 20│ ...                                         │  ← row 2, page 0
  ...   │  ...                                                │
 240    │T. 150 │ ...                           │T. 159 │pad │  ← row 15, page 0
 ───────┼───────────────────────────────────────────────┼─────  ← page boundary (Y=256)
 256    │T. 160 │T. 161 │ ...                   │T. 169 │pad │  ← row 0, page 1
  ...   │  ...                                                │
```

### Animated tiles

The game supports tile animation (e.g. flowing water, flickering flames) via 6 animation
banks configured in sub0 at offset 0x420. Each bank has a frame counter that periodically
shifts the tile dictionary pointer by a fixed offset, swapping in alternate tile art.
For a static extraction, the base (frame 0) dictionary is always correct.

---

## 8. Putting it all together — assembling a room image

Here is the step-by-step process to render a complete room image:

### Step 1: Extract and prepare the data

1. **Read the container header** (7 × u32) to find the 6 sub-resource boundaries.
2. **Parse sub0**: read the 32 palettes from offset 0x10 (each 32 bytes of BGR555).
3. **Decompress sub2**: apply EZ decompression to get the flat 4bpp tileset bitmap.
4. **Read sub1**: this is the tilemap grid + overlay strip table (used as-is, no
   decompression needed).

### Step 2: Draw the background layer

For each cell in the 52×60 grid:

1. Read the **vertical tile offset** (`heightOffset`) at `sub1 + 0x604 + row × 0x1A0 + col × 8 + 3` (an unsigned byte).
2. Read the **tile field** at `sub1 + 0x604 + row × 0x1A0 + col × 8 + 4` (a u16).
3. If the tile field is 0xFFFF, skip this cell (it is empty).
4. Extract the **tile index** (`tile & 0x3FF`) and **palette number** (`(tile >> 12) & 0xF`).
5. Use the procedural formula (§7) to compute `srcU` and `srcV` — the pixel coordinates of this tile within the decompressed tileset.
6. Copy the 24×16 pixel rectangle from the tileset to the output image at position `(col × 24, (row - heightOffset) * 16)`.
7. Skip any pixel whose index is **0** (leave it transparent).

### Step 3: Draw the foreground/overlay layer

For each cell in the 52×60 grid:

1. Read the **vertical tile offset** (`heightOffset`) at `sub1 + 0x604 + row × 0x1A0 + col × 8 + 3` (an unsigned byte).
2. Read the **overlay field** at `sub1 + 0x604 + row × 0x1A0 + col × 8 + 6` (a u16).
3. If the overlay field is 0xFFFF, skip this cell.
4. Look up the overlay strip entry at `sub1 + 0x6784 + overlay × 2`.
5. Read `base` (signed byte) and `N` (unsigned byte).
6. For each element *e* from 1 to N:
   - Read the tile word at `entry_address + e × 2`.
   - Compute the destination row: `row - heightOffset + e − base`.
   - Draw this tile (same process as Step 2) at the computed row, same column.
   - Because index-0 pixels are transparent, only the non-transparent parts of the foreground tile overwrite the background.

### Result

The output is a **1248 × 960 pixel** RGBA image with the background and foreground layers
composited together. Transparent areas (tile index 0 or empty cells) can be left as
transparent or filled with a background colour.

---

## 9. Remaining sub-resources (not needed for map rendering)

### Sub3 — Entity and sprite placement

Sub3 contains data that defines where objects, NPCs, treasure chests, and other interactive
entities are placed in the room. It has its own internal header with pointers to several
tables:

- **Entity table**: a list of entity definitions, each containing animation pointers,
  page/palette base indices, and positional data.
- **Sprite palettes**: up to 40 additional 16-colour palettes for entity sprites, stored
  at an offset given by word[5] of the sub3 header.

Entity/sprite rendering uses a different code path (`FUN_8002db8c`) that supports
per-sprite H/V flipping and uses quad primitives rather than SPRT packets. This is not
part of the map tile rendering.

### Sub4 — Dynamic entity resource

Sub4 is loaded per-frame by a separate function (`FUN_8002e028`) and appears to be used
for dynamic entity-related data (e.g. scripted events, animated object state). It is
**not** a secondary tileset for the map and should not be used for tile rendering.

### Sub5 — Sprite bank

Sub5 follows the `InitSpriteBank` format: a small header where `word[0]` is the byte
offset to the graphics payload and `word[7]` holds type/flags. The payload consists of
a **256-byte CLUT block** (8 palettes × 16 BGR555 colours) followed by **4bpp pixel data**
for the sprite sheet. This is used for the room's movable sprites (enemies, items, effects).

---

## 10. Key constants

| Constant | Value | Meaning |
|----------|-------|---------|
| Room grid size | 52 × 60 cells | Width × height of the tilemap |
| Tile size | 24 × 16 pixels | Width × height of each tile |
| Room pixel size | 1248 × 960 pixels | Total rendered room dimensions |
| Tilemap grid offset | 0x604 | Byte offset of the first cell within sub1 |
| Tilemap row stride | 0x1A0 (416) | Bytes per row in the tilemap (52 × 8) |
| Cell size | 8 bytes | Size of one tilemap cell |
| Overlay table offset | 0x6784 | Byte offset of the strip table within sub1 |
| Palette offset in sub0 | 0x10 | Where the 32 BGR555 palettes begin |
| Palette count | 32 | Total palettes (16 reachable by background tiles) |
| Colours per palette | 16 | 4bpp = 16 possible indices |
| Tileset width | 256 pixels | Width of the decompressed 4bpp bitmap |
| Tiles per page | 160 | 10 columns × 16 rows per 256×256 page |
| Total tile slots | 960 | 6 pages × 160 tiles |
| Tile index mask | 0x3FF | Bits 0–9 of the tile field |
| Palette mask | 0xF000 | Bits 12–15 of the tile field (shift right by 12) |
| Transparent index | 0 | Pixel index 0 = fully transparent (hardware rule) |

---

## 11. Known issues and caveats

1. **Tile animation**: some tiles (water, fire, torch flicker) cycle through multiple
   frames stored in adjacent tile dictionary entries. A static extraction only captures
   frame 0. The animation bank config at sub0 + 0x420 describes the cycle parameters.

2. **No tile flipping or rotation**: unlike many tile engines, Alundra's background tiles
   are drawn as PSX `SPRT` (sprite) packets, which do not support hardware H-flip or
   V-flip. If a tile appears flipped in-game, it is stored as a separate tile in the
   tileset. Object/entity sprites (drawn via `FUN_8002db8c`) *do* support flipping, but
   those are not part of the tilemap.

3. **BGR555 colour order**: the palette colours are in PSX-native BGR555 format, not RGB.
   When decoding, the 5-bit fields must be mapped correctly: bits 0–4 = Red, bits 5–9 =
   Green, bits 10–14 = Blue. Swapping red and blue is a common mistake that produces
   images with a blue/teal tint instead of the correct warm browns and greens.

4. **Both layers use the same tileset**: the game engine uses a single tileset (sub2) for
   both the background tile layer and the foreground overlay strip layer. Sub4 is not a
   "secondary tileset" — it is an entity resource loaded by a different subsystem.
