# ZombieVille Research Notes

## 2026-07-23: FUN_00010318 initial trace

### Binary context

- The active Ghidra program is `ZOMB.EXE` from project `FableDOS`.
- The program is analyzed as 32-bit x86 Borland C++ code.

## 2026-07-23: On-disk sample inventory

The game files are under the `GameRootDir` configured in `FileHelper.cs`: `C:\Dev\Gaming\PC\Dos\Games\Zombieville\ZOMB`. A recursive inventory found 926 files in the subdirectories, including 94 `.spr`, 31 `.lbm`, 28 `.dat`, 24 `.nvp`, 24 `.gdf`, 24 `.lvo`, 47 `.zld`, 58 `.cnv`, 8 `.ani`, 14 `.anm`, 5 `.unc`, and 1 `.ann` file.

The actual samples refine the format classifications:

- 86 of the 94 `.spr` files have a file length of exactly `524 + width * height`, where the first eight bytes are little-endian width/height and the parser's existing `8 + 4 + 512` byte pre-pixel layout accounts for the 524-byte prefix. These are confirmed fixed-header indexed images on disk.
- Three `.spr` files (`CONSOLE\DESK01.SPR`, `DATA\CONSOLE.SPR`, and `DATA\INV.SPR`) retain plausible width/height values and the same prefix shape but are shorter than the uncompressed pixel count. They are a separate compressed-payload variant; the compression rule is not yet proven.
- Four `.spr` files (`DATA\ALTICON.SPR`, `DATA\ICONS.SPR`, `DATA\ANI.SPR`, and `DATA\BLOOD.SPR`) begin with monotonic little-endian offsets. Their first offset is `0x4b0`, consistent with a 300-entry offset table before the referenced data. These are sprite collections rather than the fixed one-image form.
- `DATA\HAND.SPR` has a distinct header and does not match either of the two observations above; it remains unresolved.
- Representative `.lbm` files begin `FORM ... PBM` and contain `BMHD` and `CMAP` chunks. Level samples also contain `DPPS`, repeated `CRNG`, `TINY`, and `BODY` chunks. This confirms the traced IFF/PBM image path against real files, not only decompiler behavior.
- Representative `.cnv` files contain embedded animation resource names such as `MATT_n00.anm` and `ANGL_NUA.ANM`, supporting their conversation/resource-record classification.
- `.unc` is not a simple extension alias for `.anm`: `BLOOD.UNC` and `PUSS.UNC` have same-named `.anm` files with matching sizes but different bytes, while other `.unc` files have no same-named `.anm` file. Their standalone encoding remains unresolved.

### Observed loader behavior

`FUN_00010318` is a level-data/resource loader. Its direct file-related flow is:

1. It calls `FUN_0005b308` with the three incoming parameters and `0x128`, then formats a `leveldat\\%s\\%s` path.
2. It formats `%s.lbm` and passes that path to `FUN_0001c538`.
3. It formats `%s.dat` and opens it with mode `rb`, storing the resulting stream in global `DAT_000d9184`. A null stream is sent to an error/reporting routine.
4. It formats `%s%s.ann` and calls `FUN_00052a04` before opening the path with mode `rb` when that result is nonzero. A failed open is also sent to an error/reporting routine.
5. When the `.ann` path is accepted, it calls `FUN_00020dd8` with the value returned by `FUN_0005b308` and state values associated with the opened resource files.
6. It stores the `FUN_0005b308` result in `DAT_000d91a4`, calls `FUN_00027110`, and returns the result from `FUN_00052a04`.

### Current conclusions

- `.lbm`, `.dat`, and `.ann` belong to one level-data loading path.
- `.dat` is required by this path because the loader opens it unconditionally and reports a null stream.
- `.ann` is conditional: the loader first calls `FUN_00052a04`, and only enters the open/processing block when that result is nonzero.
- `FUN_0001c41c`, used by the `.lbm` handler, opens a candidate path and returns true only when the open returns a non-null stream. `FUN_0001c09c` then reads that stream into memory and closes it.
- `FUN_0001c538` searches the loaded `.lbm` bytes for the IFF chunk IDs `BMHD`, `CMAP`, and `BODY`. It reads the BMHD width/height fields and decodes the BODY data with a byte-run style loop before passing the result to `FUN_0005a3cc` under the resource name `iffload_to`.
- `FUN_00020dd8` formats a `leveldat\\%s\\%s` base path and calls `FUN_0001b8e8` with the extension string `gdf`. This makes the `.ann` branch a trigger for numbered GDF resources, not a demonstrated image decoder.
- `FUN_0001b8e8` probes names formatted as `%s%03d.%s`, so the numbered resource convention is `<base>001.gdf`, `<base>002.gdf`, and so on. Its extension comparison accepts `gdf`, `lvo`, and `zld` case-insensitively.
- The GDF result is passed to `FUN_000209d4`. That routine reads a four-byte value with `FUN_0001b658` and allocates/initializes that many record objects through `FUN_0004f9b0`, indicating a serialized object/resource stream rather than a direct bitmap.
- `FUN_000209d4` calls `FUN_0001ff8c` after its GDF work, so the `.ann` path reaches both the GDF and LVO loaders.
- `FUN_0001ff8c` requests the same numbered loader with extension `lvo`, then reads a fixed table of record-group counts from the result and allocates `0xb4`-byte records for each group. This is also a structured data stream; no image signature or pixel decode is present in this path.
- `FUN_00027110` calls `FUN_00026d38` after the `.ann`/GDF/LVO work. `FUN_00026d38` requests the numbered loader with extension `zld`, calls `FUN_00026b2c`, reads a four-byte count into `DAT_000d9194`, reads `0xb0`-byte table entries, and then reads repeated records through `FUN_0004f9b0` with range checks. ZLD is therefore a structured level/game-state stream, not a pixel stream.
- The required `.dat` stream is consumed by `FUN_000126e4` and helper `FUN_000324a4`. The latter seeks/reads internal resource names `fra%05d.zzv` and `fra%05d.mzv`. The same runtime area separately reads from `DAT_000d9164`, the optional `.ann` stream, and then requests `OBJECT10.ANM`. This makes `.dat` a packed game-resource source, not an image format on the evidence currently available.
- The exact field layout of the `.ann`, `.gdf`, `.lvo`, and `.zld` records is not proven yet. The real game tree now confirms that `.spr`, `.ani`, and `.unc` samples exist; the next discriminating work is to decode the non-basic `.spr` variants and connect the animation/resource samples to their direct runtime consumers.

### Secondary format traces

#### `.nvp`

- `FUN_000108f4` formats `%s.nvp` after the main level load and passes the path to `FUN_0001ae68`.
- `FUN_0001ae68` opens the file, reads a four-byte count with `FUN_0001b658`, allocates `(count * 2 + 3) * 4` bytes, fills paired entries through `FUN_00013848` and `FUN_000138fc`, and reads two terminal values through `FUN_000139a4`.
- This proves a compact counted/indexed numeric table. No bitmap signature, palette read, pixel loop, or image registration is present. The table's exact game meaning is not proven, so navigation/viewport terminology should remain a hypothesis.

#### `.cnv`

- `FUN_00012e94` probes `conv\\cage.cnv` and then up to four `conv\\cage_%s.cnv` variants. It records which variant was found, passes the opened stream to `FUN_00050438`, and closes it; this routine does not show the body parser.
- `FUN_0002a328` opens `conv\\%s_%s.cnv`, reads one-byte and four-byte scalar fields, conditionally deserializes repeated subrecords through `FUN_0004f9b0`, and then reads counted tables with element sizes `4`, `0x40`, `0x1cc`, and a final byte buffer.
- The path names strongly associate `.cnv` with conversation data, and the body is demonstrably structured serialized data. No image signature or pixel decoder is present in the traced handler.

#### `.wbl`

- `FUN_0001e9e0` opens the caller-provided path, reads a one-byte record count, then reads a one-byte index for each entry, allocates a `0xb4`-byte object, and deserializes it with `FUN_0004f9b0`.
- `FUN_00048e20` initializes the cursor resource `leveldat\\dicph.cur` and then loads `leveldat\\chars\\Invdel.wbl` through the WBL path. `FUN_000494b0` has a related header/version path for the same cursor and WBL resources.
- `FUN_00048af0` reads the selected file in `0x1000`-byte blocks after obtaining its size, but this is transport/file handling rather than an image decoder. The current evidence supports `.wbl` as indexed structured game data, not a direct bitmap stream.

#### `.ann` and `.anm`

- `.ann` remains an optional auxiliary input: `FUN_00010318` opens it only after `FUN_00052a04` succeeds, and the stream then participates in the numbered GDF/LVO/ZLD loading path.
- Later code also passes `DAT_000d9164` to the generic whole-entry reader `FUN_0001ba30` and requests `OBJECT10.ANM` through `FUN_0005ac90`. This means `.ann` is not proven to be only a boolean manifest; it may also serve as a resource stream or container source. Its exact format and relationship to named entries remain unresolved.
- `.anm` is evidenced by resource names such as `OBJECT10.ANM`, `MATT_n00.anm`, `Object%d.anm`, `zap.anm`, `DOOR%02d.ANM`, and `time.anm`. These names make the resources animation-related, but no standalone `.anm` parser or pixel decoder has been established. The current result is a resource-purpose classification, not a format-layout classification.

### Current format status

| Extension | Evidence-backed status | PNG extraction relevance |
| --- | --- | --- |
| `.lbm` | Confirmed IFF/ILBM-style image: `BMHD`, `CMAP`, `BODY`, dimensions, palette, and byte-run BODY decoding | Highest-priority candidate |
| `.dat` | Required packed/resource stream with named entries such as `fra%05d.zzv` and `fra%05d.mzv` | Container investigation may expose assets; not a direct bitmap on current evidence |
| `.ann` | Optional auxiliary stream feeding level resources; possible named-resource/container source, exact role unproven | Indirect; inspect entries before extraction work |
| `.gdf` | Counted serialized records initialized through `FUN_0004f9b0` | Not a direct image stream on current evidence |
| `.lvo` | Fixed groups of `0xb4`-byte serialized records | Not a direct image stream on current evidence |
| `.zld` | Counted level data with `0xb0`-byte table entries and repeated generic records | Not a direct image stream on current evidence |
| `.wbl` | One-byte count/index table of `0xb4`-byte serialized records | Not a direct image stream on current evidence |
| `.cnv` | Structured scalar, subrecord, and table data under `conv\\...` paths | Not a direct image stream on current evidence |
| `.nvp` | Counted/indexed numeric table consumed during level initialization | Not a direct image stream on current evidence |
| `.anm` | 14 real files exist, and resource names are used by the runtime; standalone format remains unknown | Possible indirect asset source; parser still needed |
| `.spr` | 86 real files match the fixed indexed layout; 3 have shorter compressed payloads, 4 begin with offset tables, and 1 has a distinct header | High for the 86 fixed files; variant-specific work remains |
| `.ani` | 8 real files exist under `DATA`; no standalone parser or image decoder is established in the current trace | Possible animation source; layout and extraction path unknown |
| `.unc` | 5 real files exist under `DATA\GORE`; same-name `.anm` comparisons are byte-different or absent | Possible alternate animation/resource encoding; parser still needed |
