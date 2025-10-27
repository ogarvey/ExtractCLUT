# AlignSprite Method - Visual Guide

## How Sprite Alignment Works

### Step 1: Input Sprites with Offsets

Each sprite has offset information in its filename:
```
0_7_8.png   → Sprite at position (7, 8)
1_-1_3.png  → Sprite at position (-1, 3)
2_6_0.png   → Sprite at position (6, 0)
```

### Step 2: Calculate Bounding Box

```
Visual representation (coordinate system):

      -1  0  1  2  3  4  5  6  7  8  9  10
    ┌──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┬──┐
  0 │  │  │  │  │  │  │  │  │  │  │  │  │
    ├──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┤
  1 │  │  │  │  │  │  │  │  │  │  │  │  │
    ├──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┤
  2 │  │  │  │  │  │  │  │  │  │  │  │  │
    ├──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┤
  3 │  │[1]│  │  │  │  │  │  │  │  │  │  │ ← Sprite 1 starts at (-1, 3)
    ├──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┤
  4 │  │  │  │  │  │  │  │  │  │  │  │  │
    ├──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┤
  5 │  │  │  │  │  │  │  │  │  │  │  │  │
    ├──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┤
  6 │  │  │  │  │  │  │  │  │  │  │  │  │
    ├──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┤
  7 │  │  │  │  │  │  │  │  │  │  │  │  │
    ├──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┼──┤
  8 │  │  │  │  │  │  │  │[0]│  │  │  │  │ ← Sprite 0 starts at (7, 8)
    └──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┴──┘
```

The method calculates:
- **minX** = -1 (leftmost position)
- **minY** = 0 (topmost position)
- **maxX** = 10 (rightmost position including sprite width)
- **maxY** = 15 (bottommost position including sprite height)

Canvas size = (maxX - minX, maxY - minY) = (11, 15)

### Step 3: ExpansionOrigin Options

The origin determines the reference point for alignment:

```
┌─────────────────────────────────────────┐
│  TopLeft    TopCenter      TopRight     │
│     ●           ●              ●        │
│                                          │
│  MiddleLeft MiddleCenter  MiddleRight   │
│     ●           ●              ●        │
│                                          │
│  BottomLeft BottomCenter  BottomRight   │
│     ●           ●              ●        │
└─────────────────────────────────────────┘
```

### Step 4: Alignment with TopLeft Origin

```
Before alignment (sprites with offsets):

  -1       6       7
   ↓       ↓       ↓
   ┌─────┐         ┌──────┐
 0 │ Spr │         │      │
   │  1  │       8 │ Spr  │
   │     │         │  0   │
   └─────┘         │      │
                   └──────┘

After alignment (TopLeft origin):
All sprites positioned relative to (minX=-1, minY=0)

   0       7       8
   ↓       ↓       ↓
   ┌─────┐         ┌──────┐
 0 │ Spr │         │      │
   │  1  │       8 │ Spr  │
   │     │         │  0   │
   └─────┘         │      │
                   └──────┘
   
Canvas size: 11 x 15
All sprites shifted by: (+1, 0) to eliminate negative coordinates
```

### Step 5: Alignment with BottomCenter Origin

Best for character sprites where feet alignment is important:

```
Before (varying heights and positions):

        ┌─────┐   
        │ Spr │   
        │  0  │   
        └─────┘   
   ┌──────┐       
   │ Spr  │       
   │  1   │       
   │      │       
   └──────┘       

After (BottomCenter origin):
All sprites aligned at bottom-center

        ┌─────┐       ┌──────┐   
        │ Spr │       │ Spr  │   
        │  0  │       │  1   │   
        └─────┘       │      │   
    ─────────────────┴──────┴── ← Bottom aligned
           Center ↑
```

### Step 6: Output

All sprites are rendered on the same canvas size:
- Transparent background
- Consistent dimensions
- Proper offset positioning
- Ready for animation or sprite sheet creation

## Real-World Example: Character Walk Cycle

Input sprites (varying sizes and offsets):
```
Frame 0: 0_10_5.png  (32x48)  - Standing
Frame 1: 1_8_7.png   (34x46)  - Foot forward
Frame 2: 2_12_3.png  (30x50)  - Mid-stride
Frame 3: 3_9_8.png   (33x45)  - Foot back
```

Using `ExpansionOrigin.BottomCenter`:

```
Input (different sizes):        Output (aligned at bottom):
                                
┌────┐  ┌─────┐  ┌───┐         ┌────┐  ┌─────┐  ┌───┐
│ 0  │  │  1  │  │ 2 │         │ 0  │  │  1  │  │ 2 │
│    │  │     │  │   │         │    │  │     │  │   │
│    │  └─────┘  │   │   →     │    │  │     │  │   │
└────┘           │   │         │    │  │     │  │   │
                 └───┘         └────┘  └─────┘  └───┘
                               ───────────────────────
                                  ↑ All aligned here

Result: Smooth animation with consistent ground level
```

## Origin Selection Guide

| Use Case | Recommended Origin | Reason |
|----------|-------------------|---------|
| Character sprites | BottomCenter | Feet alignment |
| Flying objects | MiddleCenter | Center of mass |
| UI icons | TopLeft | Standard UI positioning |
| Projectiles | MiddleCenter | Rotation center |
| Ground objects | BottomLeft | Shadow alignment |
| Ceiling objects | TopCenter | Hanging position |

## Coordinate System

```
(0,0) ────────────→ X (positive)
  │
  │
  │
  ↓
  Y (positive)
  
Negative offsets place sprites to the left (X) or above (Y) the origin.
```

## Common Patterns

### Pattern 1: Fixed Origin Point
All sprites positioned relative to a common point (e.g., character's feet)

### Pattern 2: Centered Alignment  
Sprites centered around a common point (e.g., explosions, UI elements)

### Pattern 3: Edge Alignment
Sprites aligned to a specific edge (e.g., ground tiles, ceiling decorations)

## Troubleshooting Visual Guide

### Problem: Sprites appear cut off

```
Cause: Negative offsets not accounted for

Wrong canvas calculation:
┌───────┐
│   ┌──X│  ← Sprite extends beyond left edge
│   │   │
└───┴───┘

Correct (AlignSprite handles this):
┌───────────┐
│ ┌───┐     │  ← Canvas includes negative offset space
│ │   │     │
└─┴───┴─────┘
```

### Problem: Too much empty space

```
Cause: Using wrong origin for sprite type

Character with TopLeft origin:
┌─────────────────┐
│ ┌───┐           │  ← Lots of empty space at bottom
│ │   │           │
│ │   │           │
│ └───┘           │
│                 │
│                 │
└─────────────────┘

Better with BottomCenter:
┌─────────────────┐
│                 │
│     ┌───┐       │  ← Minimal empty space
│     │   │       │
│     │   │       │
└─────┴───┴───────┘
```

## Technical Flow Diagram

```
┌──────────────────┐
│ Input: PNG files │
│ {i}_{x}_{y}.png  │
└────────┬─────────┘
         │
         ↓
┌────────────────────┐
│ Parse filenames    │
│ Extract: i, x, y   │
└────────┬───────────┘
         │
         ↓
┌────────────────────────┐
│ Load bitmap images     │
│ Get width/height       │
└────────┬───────────────┘
         │
         ↓
┌───────────────────────────┐
│ Calculate bounding box    │
│ minX, minY, maxX, maxY    │
└────────┬──────────────────┘
         │
         ↓
┌────────────────────────────┐
│ Determine canvas size      │
│ width = maxX - minX        │
│ height = maxY - minY       │
└────────┬───────────────────┘
         │
         ↓
┌────────────────────────────┐
│ Calculate reference point  │
│ Based on ExpansionOrigin   │
└────────┬───────────────────┘
         │
         ↓
┌────────────────────────────┐
│ For each sprite:           │
│ - Create canvas            │
│ - Position at offset       │
│ - Draw sprite              │
│ - Save to output           │
└────────┬───────────────────┘
         │
         ↓
┌──────────────────┐
│ Output: Aligned  │
│ PNG files        │
└──────────────────┘
```

## Performance Notes

- **Memory efficient**: Processes sprites one at a time
- **Automatic cleanup**: Disposes bitmaps after processing
- **Validation**: Skips invalid filenames with warnings
- **Sorted output**: Processes in index order for consistent results
