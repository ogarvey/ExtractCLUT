# AlignSprite Quick Reference Card

## Basic Syntax
```csharp
FileHelpers.AlignSprite(inputFolder, outputFolder, origin);
```

## Filename Format
```
{index}_{xOffset}_{yOffset}.png
```
Example: `42_-5_10.png`

## Origin Options (9 Total)

```
┌──────────────────────────────┐
│ TopLeft   TopCenter   TopRight
│    1          2           3
│
│ MiddleLeft MiddleCenter MiddleRight
│    4          5           6
│
│ BottomLeft BottomCenter BottomRight
│    7          8           9
└──────────────────────────────┘
```

## Common Usage Patterns

| Pattern | Code | Use Case |
|---------|------|----------|
| **Default** | `AlignSprite(input, output)` | Top-left alignment |
| **Character** | `AlignSprite(input, output, BottomCenter)` | Walk/run cycles |
| **UI Icons** | `AlignSprite(input, output, TopLeft)` | Interface elements |
| **Projectiles** | `AlignSprite(input, output, MiddleCenter)` | Bullets/missiles |
| **Centered** | `AlignSprite(input, output, MiddleCenter)` | Explosions/effects |

## Quick Examples

### Example 1: Character Animation
```csharp
FileHelpers.AlignSprite(
    @"C:\Sprites\Hero\Walk",
    @"C:\Sprites\Hero\Walk_Aligned",
    ExpansionOrigin.BottomCenter
);
```

### Example 2: UI Icons
```csharp
FileHelpers.AlignSprite(
    @"C:\UI\Icons",
    @"C:\UI\Icons_Aligned",
    ExpansionOrigin.TopLeft
);
```

### Example 3: Centered Effects
```csharp
FileHelpers.AlignSprite(
    @"C:\Effects\Explosions",
    @"C:\Effects\Explosions_Aligned",
    ExpansionOrigin.MiddleCenter
);
```

## Origin Selection Flowchart

```
Start
  │
  ├─ Need feet aligned? ───→ BottomCenter
  │
  ├─ Need UI positioning? ──→ TopLeft
  │
  ├─ Need centered? ────────→ MiddleCenter
  │
  ├─ Need edge aligned?
  │   ├─ Left edge ─────────→ MiddleLeft
  │   ├─ Right edge ────────→ MiddleRight
  │   ├─ Top edge ──────────→ TopCenter
  │   └─ Bottom edge ───────→ BottomCenter
  │
  └─ Custom requirement ────→ Choose manually
```

## Output Information

Console output shows:
```
Calculated canvas size: 93 x 123
Bounds: X [-8, 85], Y [0, 123]
Origin: BottomCenter
Reference point (origin): (38, 123)
Aligned sprite 0: 0_7_8.png
Aligned sprite 1: 1_-1_3.png
...
Successfully aligned 70 sprites to 'C:\Output'
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| No files found | Check input folder path |
| Files skipped | Verify filename format |
| Wrong alignment | Try different origin |
| Sprites cut off | Check offset values |

## Key Features

✅ Negative offsets supported  
✅ Auto canvas sizing  
✅ Transparent background  
✅ Detailed logging  
✅ Error handling  

## File Locations

- **Method**: `Helpers/FileHelpers.cs`
- **Documentation**: `AlignSprite_README.md`
- **Visual Guide**: `AlignSprite_Visual_Guide.md`
- **Examples**: `AlignSpriteExample.cs`

## Import Statement
```csharp
using ExtractCLUT.Helpers;
```

## Parameter Types
```csharp
string inputFolder     // Path to input sprites
string outputFolder    // Path for aligned output
ExpansionOrigin origin // Alignment reference point (optional)
```

---

💡 **Pro Tip**: Test with a small subset first to verify the origin is correct for your use case!
