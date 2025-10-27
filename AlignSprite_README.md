# AlignSprite Method Documentation

## Overview
The `AlignSprite` method processes a folder of sprite images that contain offset information in their filenames, and generates correctly sized and aligned images in a new output folder. This is particularly useful for sprite sheets from games where each sprite has different positioning offsets.

## Method Signature
```csharp
public static void AlignSprite(
    string inputFolder, 
    string outputFolder, 
    ExpansionOrigin origin = ExpansionOrigin.TopLeft)
```

## Parameters

### inputFolder
- **Type:** `string`
- **Description:** Path to the folder containing the source sprite PNG files
- **Example:** `@"C:\Sprites\Input"`

### outputFolder
- **Type:** `string`
- **Description:** Path to the folder where aligned sprites will be saved
- **Example:** `@"C:\Sprites\Output"`

### origin (Optional)
- **Type:** `ExpansionOrigin`
- **Default:** `ExpansionOrigin.TopLeft`
- **Description:** The origin point used for alignment calculations

## Filename Format
The method expects PNG files with the following naming convention:

```
{index}_{xOffset}_{yOffset}.png
```

### Components:
- **index:** Numeric index of the sprite (e.g., 0, 1, 2, ...)
- **xOffset:** X-axis offset (can be negative)
- **yOffset:** Y-axis offset (can be negative)

### Examples:
- `0_7_8.png` - Sprite index 0 at offset (7, 8)
- `1_-1_3.png` - Sprite index 1 at offset (-1, 3)
- `2_6_0.png` - Sprite index 2 at offset (6, 0)
- `40_8_6.png` - Sprite index 40 at offset (8, 6)

## ExpansionOrigin Options

The `origin` parameter determines the reference point for sprite alignment:

### Vertical Alignment
- **TopLeft** - Align to top-left corner
- **TopCenter** - Align to top-center
- **TopRight** - Align to top-right corner

### Centered Alignment
- **MiddleLeft** - Align to middle-left
- **MiddleCenter** - Align to center (both axes)
- **MiddleRight** - Align to middle-right

### Bottom Alignment
- **BottomLeft** - Align to bottom-left
- **BottomCenter** - Align to bottom-center
- **BottomRight** - Align to bottom-right

## How It Works

1. **File Discovery:** Scans the input folder for PNG files matching the naming pattern
2. **Offset Parsing:** Extracts index, xOffset, and yOffset from each filename
3. **Bounds Calculation:** Determines the bounding box needed to contain all sprites when placed at their offsets relative to a common origin point (0,0)
4. **Canvas Creation:** Creates a unified canvas size based on the calculated bounds
5. **Origin Placement:** Positions the common origin point (0,0) on the canvas according to the ExpansionOrigin parameter
6. **Sprite Alignment:** Places each sprite on the canvas at `originPosition + (xOffset, yOffset)`
7. **Output:** Saves all aligned sprites to the output folder with the same filenames

**Key Concept:** All sprite offsets are interpreted as positions relative to a common origin point (0,0). The `ExpansionOrigin` parameter determines where this origin point is placed on the output canvas. For example:
- `BottomCenter` places the origin at the bottom-center of the canvas (perfect for character feet)
- `TopLeft` places the origin at the top-left of the canvas
- `MiddleCenter` places the origin at the center of the canvas

This ensures that when you flip through the aligned sprites (like in an animation), they maintain their relative positions and don't "jump around".

## Usage Example

```csharp
using ExtractCLUT.Helpers;

// Basic usage with default TopLeft origin
var inputFolder = @"C:\Dev\Gaming\SpriteData\Raw";
var outputFolder = @"C:\Dev\Gaming\SpriteData\Aligned";
FileHelpers.AlignSprite(inputFolder, outputFolder);

// Using MiddleCenter origin for centered alignment
FileHelpers.AlignSprite(inputFolder, outputFolder, ExpansionOrigin.MiddleCenter);

// Using BottomCenter for character sprites aligned at the bottom
FileHelpers.AlignSprite(inputFolder, outputFolder, ExpansionOrigin.BottomCenter);
```

## Output

The method creates:
- A new folder at the specified `outputFolder` path (created if it doesn't exist)
- PNG files with the same names as the input files
- All sprites aligned to a common canvas size based on their offset information
- Console output showing:
  - Calculated canvas size
  - Bounding box coordinates
  - Selected origin point
  - Reference point coordinates
  - Progress for each aligned sprite

### Console Output Example:
```
Calculated canvas size: 93 x 123
Bounds: X [-8, 85], Y [0, 123]
Origin: TopLeft
Reference point (origin): (-8, 0)
Aligned sprite 0: 0_7_8.png
Aligned sprite 1: 1_-1_3.png
Aligned sprite 2: 2_6_0.png
...
Successfully aligned 70 sprites to 'C:\Dev\Gaming\SpriteData\Aligned'
```

## Features

✅ Supports negative offsets  
✅ Automatic canvas size calculation  
✅ Transparent background preservation  
✅ Flexible origin point selection  
✅ Detailed console logging  
✅ Automatic sprite sorting by index  
✅ Validates filename format  

## Error Handling

The method gracefully handles:
- Invalid filename formats (skips with warning)
- Empty folders (returns with message)
- Missing files (skips with notification)
- Automatic cleanup of bitmap resources

## Best Practices

1. **Backup Original Files:** Always keep a copy of your original sprite files
2. **Verify Filenames:** Ensure all sprites follow the naming convention
3. **Choose Appropriate Origin:** Select the origin that matches your use case:
   - Character sprites: `BottomCenter` or `BottomLeft`
   - UI elements: `TopLeft` or `MiddleCenter`
   - Projectiles/Effects: `MiddleCenter`
4. **Check Console Output:** Review the calculated canvas size and bounds for accuracy
5. **Test with Sample:** Test with a small subset of sprites first

## Common Use Cases

### Game Character Animations
```csharp
// Character walking animation with feet aligned
FileHelpers.AlignSprite(
    @"C:\GameAssets\Characters\Walk", 
    @"C:\GameAssets\Characters\Walk_Aligned", 
    ExpansionOrigin.BottomCenter);
```

### UI Icons
```csharp
// UI icons aligned from top-left
FileHelpers.AlignSprite(
    @"C:\GameAssets\UI\Icons", 
    @"C:\GameAssets\UI\Icons_Aligned", 
    ExpansionOrigin.TopLeft);
```

### Projectiles/Bullets
```csharp
// Projectiles aligned at center
FileHelpers.AlignSprite(
    @"C:\GameAssets\Weapons\Projectiles", 
    @"C:\GameAssets\Weapons\Projectiles_Aligned", 
    ExpansionOrigin.MiddleCenter);
```

## Troubleshooting

### "No PNG files found in input folder"
- Verify the input folder path is correct
- Ensure the folder contains PNG files

### "Skipping file with invalid naming format"
- Check that filenames follow the `{index}_{xOffset}_{yOffset}.png` format
- Ensure all components are numeric (except negative signs are allowed for offsets)

### Sprites appear in wrong positions
- Verify the offset values in the filenames are correct
- Try a different `ExpansionOrigin` value
- Check that the original sprite dimensions are correct

## Related Methods

- `ResizeImagesInFolder()` - Resize images to match maximum dimensions
- `AlignSprites()` - Alternative alignment method with different offset format
- `AlignSpriteSequences()` - Align sprites by sequence groups
- `PositionSprites()` - Position sprites on a fixed-size canvas

## Technical Notes

- Canvas size is calculated dynamically based on sprite bounds
- All sprites are rendered with transparent backgrounds
- Bitmaps are properly disposed to prevent memory leaks
- Sprites are processed in order of their index values
- The reference point is calculated based on the bounding box and selected origin
