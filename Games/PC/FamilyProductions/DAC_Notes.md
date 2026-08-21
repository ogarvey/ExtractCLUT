# Family Productions DAC format investigation notes

## DOS file-I/O wrapper baseline

The current Ghidra project is `FableDOS/PEE.EXE`. The file access path uses these far-call wrappers:

- `FUN_34bf_0002` invokes DOS `INT 21h`, `AH=3Dh` (open file). On success it records the returned handle in the table at `0x22f8 + handle * 2`, combining the caller's `param_3` with `0x8000` after masking with `0xf8ff`.
- `FUN_3555_0000` invokes `INT 21h`, `AH=3Fh` (read file). Carry-flag failures are passed to the common error mapper `FUN_3426_0007`.
- `FUN_357e_000b` invokes `INT 21h`, `AH=42h` (move file pointer / `LSEEK`). It clears bit `0x0200` in the per-handle table entry before the DOS call.
- `FUN_3426_0007` maps DOS errors into the game's globals at `3865:007f` and `3865:1fec`.

Incoming call sites currently identified:

- Open: `FUN_34a5_0035` (`34a5:0102`).
- Read: `FUN_34d8_00f8` (`34d8:01e2`), `FUN_352f_000d` (`352f:0093`), and `caseD_9a` in `FUN_3548_000b` (`3548:003a`, `3548:008b`).
- Seek: `FUN_2ffc_00a9` (`2ffc:00f2`), `FUN_2ffc_0320` (`2ffc:0341`, `2ffc:0375`), `FUN_34c3_005d` (`34c3:00cf`), `FUN_34c3_00eb` (`34c3:011b`), `caseD_9a` in `FUN_3548_000b` (`3548:00b0`), and `FUN_3576_0000` (`3576:001b`).

These callers are shared loader candidates; no `.DAC`-specific decoding logic has been assigned yet.

## DAC path and decoder findings

`FUN_2db2_0007` opens the executable string `"DAC.DAT"` (`3865:1524`) and repeatedly drives `FUN_2ffc_0384`. That loader seeks through the indexed data, calls `FUN_2ffc_0160` to process a record, and `FUN_2ffc_047f` copies a 32000-byte page to VGA segment `A000`.

`FUN_2ffc_0160` reads a 16-byte block header, subtracts `0x10` from the first 32-bit size field, and reads the remaining block bytes into an allocated buffer. The payload is then consumed as six-byte subrecords: a 32-bit subrecord size followed by a 16-bit type. The observed handlers include types `0x0b`, `0x0c`, `0x0f`, and `0x10`.

Two useful subrecord operations are identified:

- `type 0x0b` is a palette update. Its content starts with a 16-bit command count, followed by `(skip, count)` byte pairs. A non-zero count advances the palette index by `skip`, writes `count` RGB triples from the stream, and advances by `count * 3` source bytes. A zero count is the special full-palette form: it writes indices `start .. 0xff` and consumes `0x300` RGB bytes. `FUN_2ffc_043b` performs the hardware write by sending the start index to port `0x3c8` and RGB bytes to port `0x3c9`.
- The unlabelled routine at `2ffc:0009`, called by type `0x0f`, is a full-frame RLE decoder. The caller supplies 200 rows. Each row starts with a command count; each command is a signed length byte. A positive length repeats the following byte that many times; a negative length copies the following `-length` literal bytes. Rows use a `0x140` (320-byte) destination stride.
- `FUN_2ffc_004f` is the type `0x0c` sparse image/delta decoder. Its content begins with a 16-bit start row and 16-bit row count. Each row starts with a command count; each command advances by an X skip, then uses the same signed length convention: positive copies literal bytes and negative repeats the following byte. It writes only the specified patches at a 320-byte row stride.
- The original `FUN_2ffc_0160` does not require these decoders to consume every byte in a subrecord. After dispatching a handler, it advances by the declared subrecord payload size (`2ffc:02f5`) and continues. The model therefore ignores trailing subrecord bytes after a valid palette, full-frame, or delta decode; extracted DAC members use this space for record padding/alignment.

## Sample observations

`Samples/file_0000.DAC` is `0x1b10f` bytes. It begins with `(C) 1993 Family Pro.` followed by null padding, then a `0x97`-byte member header. The first frame starts at `0x97`:

- Frame header: size `0xA53e`, marker `0xF1FA`, value `2`, then eight zero bytes.
- `type 0x0b` subrecord at `0xA7`, size `0x30a`. Its content starts with one palette command (`01 00 00 00`) followed by `0x300` bytes of RGB data at `0xb1`.
- `type 0x0f` subrecord at `0x3b1`, size `0xA224`. Its RLE content starts at `0x3b7` and decodes exactly 200 rows x 320 pixels, ending at `0xA5d5`.

The remaining sample is a sequence of `FA F1` frames. The sample contains 52 top-level frames: 15 empty 16-byte timing frames, one palette block, one full-frame type `0x0f` block, and 36 type `0x0c` delta blocks. The final frame ends exactly at the file length `0x1b10f`.

The complete sample stream still needs its outer block/subrecord boundaries assigned, but the palette update interpretation and the separate rectangle RLE operation are now grounded in the original binary.
