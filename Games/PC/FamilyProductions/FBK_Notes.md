# Family Productions FBK format investigation notes

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

These callers are shared loader candidates; no `.FBK`-specific decoding logic has been assigned yet.

## Verified FBK decoder

The FBK path is confirmed in the Ghidra project:

- `FUN_28fa_0134` opens the caller-supplied filename in binary mode (`"rb"`), reads a 32-bit offset from the executable table at `3865:00ae + index * 4`, adds `0x1e`, and seeks to that record payload.
- It calls `FUN_28fa_0099` 200 times. Each decoded row is written at `output + row * 0x140`, so the output is 320 x 200 (64,000 bytes).
- `FUN_28fa_0099` reads one control byte at a time. If `(control & 0xc0) == 0xc0`, it reads one value byte and writes that value `(control & 0x3f)` times. Otherwise it writes the control byte as one literal pixel. Each row stops after exactly 320 output bytes; there is no row terminator.
- The two-plane variant `FUN_28fa_01cf` uses the same row decoder for two records and places 100 decoded rows into alternating even/odd output rows, producing two 320 x 200 pages.

### Sample confirmation

`Samples/file_0000.FBK` is `0x4122` bytes. Its first 30 bytes are the fixed record header, beginning with the ASCII signature `[ Background Data (C) C S H ]`; decoding from offset `0x1e` consumes the remaining `0x4104` bytes and produces exactly 200 rows / 64,000 pixels. The first payload controls include `C6 B9` (six `B9` pixels), `C4 BA` (four `BA` pixels), and `C2 B9` (two `B9` pixels), matching the routine exactly.

The game’s known FBK filenames include `FBK.DAT` at `3865:0b8b`, `3865:0ba3`, `3865:0xbb3`, `3865:0x0bc3`, and `3865:0619`. The observed calls use record indices including `4` and `0x2b`-`0x2f`.
