# Shadow Warrior Frame Processor

## Overview
This tool processes frame list files and creates aligned sprite images and animation sequences for the Shadow Warrior game on 3DO.

## Features

### 1. Frame Alignment (`ProcessFrames`)
Processes frame list files and aligns sprite images according to metadata.

### 2. Move Animation Extraction (`ProcessMoves`)
Extracts and creates animation sequences for each move defined in a moves file.

### 3. Move Animation with Alignment (`ProcessMovesWithAlignment`)
**Most Advanced**: Combines frame alignment data with move-specific position offsets to create fully aligned animation sequences. This allows the same source frame to be reused at different positions within different animations.

## Usage

### Frame Alignment

```csharp
using ExtractCLUT.Games.ThreeDO.ShadowWarrior;

// Process frames from a frame list file
string frameListPath = @"c:\path\to\ALVIN.FRAMES.txt";
string imageFolder = @"c:\path\to\images\folder";

FrameListParser.ProcessFrames(frameListPath, imageFolder);
```

### Move Animation Extraction

```csharp
using ExtractCLUT.Games.ThreeDO.ShadowWarrior;

// Extract move animations from a moves file
string movesFilePath = @"c:\path\to\ALVIN.MOVES.txt";
string imageFolder = @"c:\path\to\images\folder";

FrameListParser.ProcessMoves(movesFilePath, imageFolder);
```

### Move Animation with Full Alignment (Recommended)

```csharp
using ExtractCLUT.Games.ThreeDO.ShadowWarrior;

// Process moves with full frame alignment
string framesFilePath = @"c:\path\to\ALVIN.FRAMES.txt";
string movesFilePath = @"c:\path\to\ALVIN.MOVES.txt";
string imageFolder = @"c:\path\to\images\folder";

FrameListParser.ProcessMovesWithAlignment(framesFilePath, movesFilePath, imageFolder);
```

## Input Requirements

### Frame List File (ALVIN.FRAMES.txt)
A text file with the following structure:
- Each frame is enclosed between `StartAnimation` and `EndAnimation` markers
- Frame properties include:
  - `FrameCode`: Numeric code (e.g., 100001, 100002, etc.)
  - `SizeX`, `SizeY`: Overall frame dimensions
  - `MinSizeX`, `MinSizeY`: Actual image dimensions
  - `MinOffsetX`, `MinOffsetY`: Position offset for the image
  - `Rotated`: 1 if the image should be rotated 90° clockwise, 0 otherwise

### Moves File (ALVIN.MOVES.txt)
A text file with the following structure:
- Each move block ends with `EndMove` marker
- Move properties include:
  - `Name`: The name of the move (used for output folder naming)
  - `Fr` lines: Frame definitions with format `Fr NN Anim XX SubFr XX Ang XXXX Rep NN ...`
    - Frame index (Fr): Sequential frame number
    - Anim index: References the source image (Anim 00 → _000.png, Anim 01 → _001.png, etc.)
    - Rep: Number of times to repeat this frame in the animation

### Image Folder
Contains PNG images named with a pattern like `basename_000.png`, `basename_001.png`, etc.
- FrameCode 100001 / Anim 00 maps to `_000.png`
- FrameCode 100002 / Anim 01 maps to `_001.png`
- And so on...

## Output

### Frame Alignment Output
- Processed images are saved to an `aligned_output` subfolder within the input image folder
- Output images maintain the same naming convention as input images
- All output images have transparent backgrounds
- Images are positioned and aligned according to the frame metadata

### Move Animation Output
- Animation sequences are saved to a `moves_output` subfolder within the input image folder
- Each move gets its own subfolder named after the move (e.g., `STAND`, `WALK.R`, `JUMP.V`)
- Frames are named sequentially: `movename_000.png`, `movename_001.png`, etc.
- Frames are repeated according to the `Rep` count in the moves file

### Move Animation with Alignment Output
- Animation sequences are saved to a `moves_aligned_output` subfolder within the input image folder
- Each move gets its own subfolder named after the move
- Frames are fully processed with:
  - Transparent canvas of `SizeX x SizeY` (from FRAMES file)
  - Image rotated 90° clockwise if needed (from FRAMES file)
  - Base position from `MinOffsetX` and `MinOffsetY` (from FRAMES file)
  - Additional offset from `XM` and `YM` (from MOVES file)
  - Final position = `MinOffsetX + XM`, `MinOffsetY + YM`
- Frames are repeated according to the `Rep` count in the moves file

## Example - Frame Alignment

Given:
- Frame list: `ALVIN.FRAMES.txt`
- Images: `alvin_frame_000.png`, `alvin_frame_001.png`, etc.

The processor will:
1. Parse each frame definition from the text file
2. Load the corresponding input image
3. Rotate it 90° clockwise if `Rotated` is 1
4. Create a transparent canvas of size `SizeX x SizeY`
5. Place the image at position `(MinOffsetX, MinOffsetY)` with size `MinSizeX x MinSizeY`
6. Save the result to `aligned_output/alvin_frame_000.png`, etc.

## Example - Move Animation

Given:
- Moves file: `ALVIN.MOVES.txt` with a move named "WALK.R"
- Images: `alvin_frame_000.png`, `alvin_frame_001.png`, etc.
- Move definition:
  ```
  Name WALK.R
  Fr  00 Anim 06 SubFr 00 Ang 0000 Rep 04 ...
  Fr  01 Anim 07 SubFr 00 Ang 0000 Rep 04 ...
  ```

The processor will:
1. Create folder `moves_output/WALK.R/`
2. Copy `alvin_frame_006.png` 4 times as `WALK.R_000.png` through `WALK.R_003.png`
3. Copy `alvin_frame_007.png` 4 times as `WALK.R_004.png` through `WALK.R_007.png`
4. Continue for all frames in the move

## Example - Move Animation with Alignment

Given:
- Frames file: `ALVIN.FRAMES.txt` with frame data for Anim 06:
  ```
  FrameCode 100007
  SizeX 268
  SizeY 200
  MinSizeX 70
  MinSizeY 120
  MinOffsetX 54
  MinOffsetY 45
  Rotated 1
  ```
- Moves file: `ALVIN.MOVES.txt` with a move named "WALK.R":
  ```
  Name WALK.R
  Fr  00 Anim 06 SubFr 00 Ang 0000 Rep 04 L 10 XM 5 YM -2
  Fr  01 Anim 07 SubFr 00 Ang 0000 Rep 04 L 06 XM 4 YM 00
  ```
- Images: `alvin_frame_000.png`, `alvin_frame_001.png`, etc.

The processor will:
1. Create folder `moves_aligned_output/WALK.R/`
2. For each frame:
   - Load source image `alvin_frame_006.png`
   - Rotate 90° clockwise (Rotated = 1)
   - Resize to 70x120 (MinSizeX x MinSizeY)
   - Create transparent 268x200 canvas (SizeX x SizeY)
   - Calculate position: X = 54 + 5 = 59, Y = 45 + (-2) = 43
   - Place image at position (59, 43)
   - Repeat 4 times (Rep = 04)
3. Continue for all frames in the move
4. Result: Fully aligned animation with proper positioning for gameplay

This approach allows the same base frame to be repositioned dynamically for different animations, enabling smooth character movement and attacks.
