# Dungeon Keeper 2 — WAD & KMF File Formats

Findings from reverse engineering `DKII.EXE` (v1.7, Ghidra project `CD-i`, port 8193), cross-checked
against the OpenKeeper project and **verified empirically** against the GOG install at
`C:\GOG Games\Dungeon Keeper 2`.

All values are **little-endian** unless stated otherwise.

---

## 1. Binary reference map (DKII.EXE)

| Address | Role |
|---|---|
| `FUN_0055c020` | Resource system init: builds HD path from `GetCommandLineA`, registers 9 resource sources — 6 WADs (`Meshes.Wad`, `K:\DK2\Dev\Data\Meshes.Wad` (dev leftover), `EngineTextures.wad`, `Sprite.Wad`, `FrontEnd.wad`, `Paths.wad`) via `FUN_0055be80` and 4 loose directories (`data\editor`, `data\text\`, `data\Texture`, `data\palette`) via `FUN_0055bf40` |
| `FUN_0055be80` | Open one WAD: `FUN_005baad0` → alloc 0xE0 `TbWadFileStorage` (RTTI `.?AVTbWadFileStorage@@`), register into storage list (`FUN_00556c90`) |
| `FUN_005d8370` | `TbWadFileStorage` constructor (vtable `0x00672740`) |
| `FUN_005d8560` | `TbWadFileStorage::Open` — reads **0x58-byte header**, checks magic `0x42465744` (`"DWFB"`), version `< 3` (flag passed for `== 2`) |
| `FUN_00556a20` | Resource lookup — iterates registered storages, virtual find/open per storage |
| `FUN_00556ba0` | Load whole resource into a growable global buffer (`DAT_006d6438`: capacity/size/dataPtr) |
| `FUN_005d8b10` | Open file inside WAD: directory lookup, re-reads 0x28-byte entry, creates substream; entry `type` bit0 ⇒ error, bit1 ⇒ decompressor A (`FUN_005ffad0`), bit2 ⇒ decompressor B (`FUN_005ff9b0`, vtable `0x00672f28`); `FUN_005ffb10` allocates the full uncompressed-size output buffer |
| `FUN_0043e030` | KMF resource load — formats `"%s%s.kmf"` / `"%s.kmf"` and pulls the file through the resource system (raw bytes; parsing happens in the mesh-resource classes: RTTI `CMeshResourceBase`, `CAnimMeshResource`, `CPolyMeshResource`, `CMeshGroup`) |

Decompiled sources saved in [_re/](_re/) alongside [verify_wad.ps1](_re/verify_wad.ps1) which validates
everything below against the real `Meshes.WAD`.

---

## 2. WAD container format (`DWFB`)

### 2.1 Header (0x58 bytes, at offset 0)

| Offset | Size | Field | Notes |
|---|---|---|---|
| 0x00 | 4 | magic | `"DWFB"` (`0x42465744` as u32) |
| 0x04 | 4 | version | `2` (engine accepts ≤ 2; version 2 selects the current directory layout) |
| 0x08 | 0x40 | unknown | all zero in retail files |
| 0x48 | 4 | fileCount | e.g. Meshes.WAD = 2015 |
| 0x4C | 4 | nameBlobOffset | absolute file offset of the filename blob |
| 0x50 | 4 | nameBlobSize | blob runs to end-of-file (`nameBlobOffset + nameBlobSize == fileSize`) |
| 0x54 | 4 | unknown | 0 |

### 2.2 Directory (immediately at 0x58, `fileCount` × 0x28 bytes)

| Offset | Size | Field | Notes |
|---|---|---|---|
| 0x00 | 4 | unknown1 | varies; not needed for extraction |
| 0x04 | 4 | nameOffset | **absolute** offset into the name blob |
| 0x08 | 4 | nameSize | includes NUL terminator |
| 0x0C | 4 | dataOffset | **absolute** offset of the file payload |
| 0x10 | 4 | compressedSize | payload size as stored |
| 0x14 | 4 | type | `0` = stored, `4` = compressed (bit1 = alternate codec, unused in retail; bit0 = invalid) |
| 0x18 | 4 | uncompressedSize | |
| 0x1C | 12 | unknown2 | zero |

Names may contain relative paths (e.g. `WORKSHOP\...`); duplicate bare names are only unique with path.

Verified sample (Meshes.WAD, entry 0): `g_Maiden-Torture.kmf`, dataOffset 0x13B30, compressed 0x160F,
type 4, uncompressed 0x5327.

### 2.3 Compression (type 4)

LZ77 variant with a leading pseudo-header, control-flag bytes, and back-references into the output:

```
i = 0
if (src[0] & 1) skip 3 extra bytes    # src[0] observed = 0x10
i++                                    # skip one byte (0xFB observed)
decompressedSize = src[i]<<16 | src[i+1]<<8 | src[i+2]   # 24-bit BIG-endian
i += 3

loop:
  flag = src[i++]
  if (flag & 0x80) == 0:              # short backref, 1 extra byte
      tmp = src[i++]
      copy (flag & 3) literal bytes
      k = out_pos - ((flag & 0x60) << 3) - tmp - 1
      copy ((flag >> 2) & 7) + 3 bytes from out[k...]     # do/while: counter+1 copies
  elif (flag & 0x40) == 0:            # medium backref, 2 extra bytes
      tmp, tmp2 = src[i++], src[i++]
      copy (tmp >> 6) literal bytes
      k = out_pos - ((tmp & 0x3F) << 8) - tmp2 - 1
      copy (flag & 0x3F) + 4 bytes from out[k...]
  elif (flag & 0x20) == 0:            # long backref, 3 extra bytes
      t1, t2, t3 = src[i++], src[i++], src[i++]
      copy (flag & 3) literal bytes
      k = out_pos - ((flag & 0x10) << 12) - (t1 << 8) - t2 - 1
      copy t3 + ((flag & 0x0C) << 6) + 5 bytes from out[k...]
  else:                               # literal run / terminator
      count = (flag & 0x1F) * 4 + 4
      if count > 0x70: count = flag & 3; FINISH after copy
      copy count literal bytes
```

(The "+3/+4/+5" totals account for the original's post-decrement `do{}while(counter--)` copying one
extra byte over the stated base counts.)

Verified: entry 0 of Meshes.WAD decompresses to exactly 0x5327 bytes beginning `KMSH`.

---

## 3. KMF model format (`KMSH`)

Chunked format. Every chunk = 4-char tag + u32 size (size generally includes the whole chunk; safe
practice is to parse sequentially rather than trusting sizes). Strings are NUL-terminated,
varying-length. File version observed: `0x11` at offset 8 (after `KMSH` + size).

```
KMSH
 ├─ HEAD                 u32 type (1 = MESH, 2 = ANIM, 3 = GROP), u32 unknown (=1)
 ├─ MATL                 (absent for GROP)   u32 materialCount, then per material:
 │   └─ MAT2             name (cstr), u32 textureCount, textureCount × cstr (alternative textures),
 │                       u32 flags, f32 brightness, f32 shininess (gamma), cstr environmentMapTexture
 ├─ MESH                 (type 1)
 │   ├─ HEAD             name (cstr), u32 sprsCount, u32 geomCount, 3×f32 pos, f32 scale (cull cube), u32 lodCount
 │   ├─ CTRL             u32 count, count × { u32, u32 } (unknown)
 │   ├─ SPRS
 │   │   ├─ sprsCount × SPHD { lodCount × u32 triangleCount, u32 vertexCount, f32 mmFactor }
 │   │   └─ sprsCount × SPRS {
 │   │         u32 materialIndex,
 │   │         per LOD: triangleCount[lod] × { u8 a, u8 b, u8 c },      # vertex indices, ≤255/sprite
 │   │         vertexCount × { u16 geomIndex, u16 u, u16 v, 3×f32 normal }
 │   │      }
 │   └─ GEOM             geomCount × { 3×f32 xyz }
 ├─ ANIM                 (type 2)
 │   ├─ HEAD             name (cstr), u32 sprsCount, u32 frameCount, u32 indexCount, u32 geomCount,
 │   │                   u32 frameFactorFunction (0=CLAMP, 1=WRAP), 3×f32 pos, f32 cubeScale, f32 scale, u32 lodCount
 │   ├─ CTRL             u32 count, count × { u16, u16, u32 } (unknown)
 │   ├─ SPRS
 │   │   ├─ sprsCount × SPHD { lodCount × u32 triangleCount, u32 vertexCount, f32 mmFactor }
 │   │   └─ sprsCount × SPRS {
 │   │         u32 materialIndex,
 │   │         POLY  per LOD: triangleCount[lod] × { u8 a, u8 b, u8 c },
 │   │         VERT  vertexCount × { u16 u, u16 v, 3×f32 normal, u16 itabIndex }
 │   │      }
 │   ├─ ITAB             ceil(frameCount / 128) blocks × indexCount × u32
 │   ├─ GEOM             geomCount × { u32 packedCoords, u8 frameBase }
 │   │                   x = ((packed >> 20) % 1024 − 512) / 511 × scale
 │   │                   y = ((packed >> 10) % 1024 − 512) / 511 × scale
 │   │                   z = ((packed >>  0) % 1024 − 512) / 511 × scale
 │   └─ VGEO             indexCount × frameCount × u8 geomOffset
 └─ GROP                 (type 3)
     ├─ HEAD             u32 elementCount
     └─ elementCount × ELEM { name (cstr, mesh KMF name w/o extension), 3×f32 pos }
```

### Notes

- **UVs**: u16, normalize by /32768 (or /65536 depending on wrap handling — validate visually).
- **Triangles** index into the sprite's vertex list; each sprite ("sub-mesh") has ≤ 256 vertices.
- **LODs**: only triangle lists vary per LOD; vertices are shared. LOD 0 = full detail.
- **Static mesh** vertices reference `GEOM` positions via `geomIndex`.
- **Animation** is per-vertex morph animation (no skeleton):
  - For vertex with `itabIndex i` at frame `f`:
    `geomIndex = ITAB[f / 128][i] + VGEO[i][f]`, then the `GEOM` entry gives the position.
  - `frameBase` (u8 in GEOM) marks which key/pose frame the geometry belongs to; the engine
    interpolates between pose frames (OpenKeeper reproduces this with pose tracks).
  - `frameFactorFunction` controls looping (WRAP) vs clamping (CLAMP) between frames.
- **Coordinate system**: DK2 is left-handed with Z up; Blender/glTF conversion needs axis swap
  (OpenKeeper swaps Z & Y for jMonkeyEngine).
- **Textures**: material texture names reference the **texture cache** at
  `DK2TextureCache\EngineTextures.dat` + `.dir` (see section 3.1) — these are the model
  textures. `EngineTextures.WAD` also contains plain PNGs, but those serve other purposes.

### 3.1 Texture cache (`DK2TextureCache\EngineTextures.dir` / `.dat`)

`.dir` — index file:

| Offset | Size | Field |
|---|---|---|
| 0x00 | 4 | magic `"TCHC"` |
| 0x04 | 4 | file size |
| 0x08 | 4 | version (4) |
| 0x0C | 4 | entryCount (retail: 5767) |
| 0x10 | — | entries: NUL-terminated name (may contain sub-paths), u32 offset into `.dat` |

`.dat` — record at each offset:

| Offset | Size | Field |
|---|---|---|
| 0x00 | 4 | width |
| 0x04 | 4 | height |
| 0x08 | 4 | size (of the record from offset 0x0C onward) |
| 0x0C | 2 | sourceWidth |
| 0x0E | 2 | sourceHeight |
| 0x10 | 4 | flags — bit 7 = has alpha |
| 0x14 | size−8 | compressed data, read as u32 LE words |

Compression is a DCT-based 8×8-block codec (per-channel delta-coded DC + Huffman-coded AC
coefficients, custom inverse transform). Ported from OpenKeeper's `Dk2TextureDecoder` /
`EngineTextureDecoder` (original C decoding code by George Gensure) — see
`DKIITextureCache.cs`. Texture names carry `MM0`/`MM1`/`MM2`... suffixes = mip levels
(MM0 largest). Verified: all 5,767 retail textures decode cleanly.


---

## 4. Implementation plan

Target: C# (this repo), namespace `ExtractCLUT.Games.PC.Bullfrog.DungeonKeeper2`.

### Phase 1 — WAD extractor
1. `WadFile` class: parse header + directory + name blob (section 2.1–2.2).
2. `WadDecompressor`: port of the LZ77 variant (section 2.3) — already validated in
   [verify_wad.ps1](_re/verify_wad.ps1).
3. Extract-all CLI: dump every WAD to `<out>\<wadname>\<path>\<file>`; preserve embedded subpaths.
   - Acceptance: all 2015 files of Meshes.WAD extract; every `.kmf` starts with `KMSH`;
     stored (type 0) files copied verbatim.
   - **DONE** (implemented in `DKII.cs`): 2,667 files across 5 WADs, 0 failures;
     all 2,015 KMFs valid; EngineTextures = 102 valid PNGs.

### Phase 2 — KMF parser
1. `KmfFile` reader per section 3 (types MESH / ANIM / GROP), chunk-tag validation with clear errors.
2. Model classes: `KmfMaterial`, `KmfMesh`, `KmfSprite`, `KmfAnim`, `KmfGrop`.
3. Sanity harness: batch-parse all KMFs in Meshes.WAD + FrontEnd.WAD, report failures/unknown fields.
   - **DONE** (implemented in `DKIIKmf.cs`): 2,015/2,015 parsed, every byte consumed
     (0 trailing bytes), all material/geom indices valid.
     Breakdown: 862 MESH, 1,145 ANIM, 8 GROP.

### Phase 3 — Conversion to glTF 2.0 (best Blender ingestion path)

**DONE** (implemented in `DKIIGltf.cs`, using SharpGLTF.Toolkit): 2,007/2,007 MESH+ANIM models
converted to .glb (8 GROP skipped), textures embedded from the texture cache.

1. **Static meshes**: sprites → glTF primitives (one material each), positions from GEOM,
   normals + UVs from vertices, LOD 0 triangles. Axis conversion `(x, y, z) → (x, −z, y)`
   with reversed triangle winding; UV = u16 / 32768 (matches OpenKeeper).
2. **Animations**: per-vertex morphing → one morph target per frame (absolute positions,
   deltas computed by SharpGLTF), weight track at 30 fps. Some ANIM files are actually
   static (torture/electrocute variants use texture effects, not vertex motion) — these
   export without morph targets.
3. **GROP**: skipped for now (8 files; group nodes referencing other meshes at offsets —
   trivial to recreate in Blender if needed).
4. **Materials**: base-color texture wired from extracted texture-cache PNGs (`<name>MM0.png`),
   with OpenKeeper's known texture-name fixes (Goblinbak/Goblin2).

Remaining polish (validate visually in Blender first):
- UV wrap handling and alpha/transparency material flags.
- Optional: key-pose-only morph targets (smaller files, interpolated playback).
- Optional: GROP → glTF scene with node instances.

### Open questions / risks
- MESH `CTRL` and ANIM `CTRL` semantics unknown (unused by OpenKeeper too) — ignore initially.
- UV scale factor (32768 vs 65536) — confirm visually on a textured model.
- `mmFactor` (mip/LOD distance factor) — metadata only, not needed for export.
- Some FrontEnd/Paths WAD content is not KMF; extractor must not assume.
