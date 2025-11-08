# 3DO CEL Decoder Refactoring

## Overview

The 3DO CEL decoding system has been refactored to provide better separation of concerns and proper handling of CCB (Cel Control Block) flags. This document explains the new architecture.

## Problem with Original Code

The original `CelUnpacker.cs` implementation had CCB flag handling (BGND, NOBLK, PLUTA) incorrectly applied to **coded formats** (palette-indexed images). These flags are only relevant for **uncoded formats** (direct RGB images).

**Issue:** The BGND and NOBLK flags were checking for `000` values in palette indices and treating them as transparent or modifying them, which broke previously working 8bpp images.

**Root cause:** Coded formats use palette indices (0-31 for 8bpp), not RGB values. A palette index of 0 is a valid color entry, not a black pixel.

## New Architecture

###  1. **CcbHeader.cs** - CCB Header Model

A comprehensive class representing all CCB header fields and flags as properties:

```csharp
public class CcbHeader
{
    // Raw fields
    public uint Flags { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public uint Pre0 { get; set; }
    public uint Pre1 { get; set; }
    
    // Computed properties
    public bool Packed => (Flags & 0x00000200) != 0;     // Bit 9
    public bool Bgnd => (Flags & 0x00000020) != 0;       // Bit 5
    public bool NoBlk => (Flags & 0x00000010) != 0;      // Bit 4
    public byte Pluta => (byte)((Flags >> 1) & 0x07);    // Bits 3-1
    public bool IsCoded => (Pre0 & 0x10) != 0;           // PRE0 bit 4
    // ... many more flags
}
```

**Benefits:**
- Self-documenting code
- Type-safe flag access
- All CCB documentation in one place
- Easy to extend

### 2. **CelDecoder.cs** - Format-Specific Decoders

A new static class with dedicated methods for each of the 4 cel formats:

```csharp
public static class CelDecoder
{
    // Main entry point - routes to correct decoder
    public static CelImageData Decode(byte[] pixelData, CcbHeader ccb, ...);
    
    // Format-specific decoders
    private static CelImageData DecodeCodedPacked(...);
    private static CelImageData DecodeCodedUnpacked(...);
    private static CelImageData DecodeUncodedPacked(...);
    private static CelImageData DecodeUncodedUnpacked(...);
}
```

**Format Matrix:**

| Format | Coded/Uncoded | Packed/Unpacked | Description |
|--------|---------------|-----------------|-------------|
| **Coded Packed** | Coded (palette indices) | Packed (row headers + packets) | Most common 3DO format |
| **Coded Unpacked** | Coded (palette indices) | Unpacked (raw sequential) | Less common, word-aligned rows |
| **Uncoded Packed** | Uncoded (RGB values) | Packed (row headers + packets) | Direct color with compression |
| **Uncoded Unpacked** | Uncoded (RGB values) | Unpacked (raw sequential) | Direct color, no compression |

### 3. **CelUnpacker.cs** - Legacy Compatibility

The original `CelUnpacker` class remains **unchanged** (except reverted flag handling) for backward compatibility:

```csharp
public static class CelUnpacker
{
    // Existing public methods preserved
    public static CelImageData UnpackCodedPackedWithDimensions(...);
    public static CelImageData UnpackCodedUnpackedCelData(...);
    public static CelImageData UnpackUncodedPackedWithDimensions(...);
    public static CelImageData UnpackUncodedUnpackedWithDimensions(...);
    
    // BitReader made public for CelDecoder access
    public class BitReader { ... }
}
```

## CCB Flag Handling - Correct Implementation

### Flags for CODED formats (palette indices):

- **PLUTA (bits 3-1)**: For pixels < 5bpp, pad high bits before PLUT lookup
  - Example: 4bpp pixel `0xF` with PLUTA=`0x3` → `(0x3 << 4) | 0xF` = `0x3F` → lookup PLUT[0x3F]
- **BGND**: NOT applicable (no RGB 000 values, only palette indices)
- **NOBLK**: NOT applicable (palette index 0 is valid, not "black")

### Flags for UNCODED formats (direct RGB):

- **BGND (bit 5)**: Controls 000 value treatment
  - `BGND=0`: Treat RGB 000 as **transparent** (skip pixel)
  - `BGND=1`: Treat RGB 000 as **black** (render it)
- **NOBLK (bit 4)**: Controls 000 output value
  - `NOBLK=0`: Write 000 as **100** (slightly brighten to avoid pure black)
  - `NOBLK=1`: Write 000 as **000** (keep pure black)
- **PLUTA**: NOT typically used (uncoded formats have full RGB values)

### Implementation in CelDecoder:

**DecodeCodedPacked:**
```csharp
// Only apply PLUTA for <5bpp pixels
if (bpp < 5)
{
    pixelValue = (ccb.Pluta << bpp) | pixelValue;
}
// BGND and NOBLK NOT checked - these are palette indices!
```

**DecodeUncodedPacked:**
```csharp
// Check BGND for 000 RGB values
if (pixelValue == 0 && !ccb.Bgnd)
{
    // Mark as transparent
    transparencyMask[maskOffset] = true;
}
else
{
    // Apply NOBLK for 000 values
    if (pixelValue == 0 && !ccb.NoBlk)
    {
        pixelValue = 1; // Change 000 to 100
    }
    var (r, g, b, a) = ConvertToRgba(pixelValue, bpp);
    // ...
}
```

## Migration Path

### Current Code (still works):
```csharp
var result = CelUnpacker.UnpackCodedPackedWithDimensions(data, width, height, 8);
```

### New Code (recommended for new features):
```csharp
var ccb = new CcbHeader 
{
    Flags = ccbFlags,
    Width = width,
    Height = height,
    Pre0 = pre0,
    Pre1 = pre1
};

var result = CelDecoder.Decode(data, ccb, bitsPerPixel: 8, verbose: true);
```

## Benefits of New Architecture

1. **Correct Flag Handling**: BGND/NOBLK only applied to uncoded formats
2. **Better Organization**: Each format has its own dedicated method
3. **Type Safety**: CcbHeader provides strongly-typed flag access
4. **Maintainability**: Clear separation makes bugs easier to find
5. **Extensibility**: Easy to add new formats or flags
6. **Documentation**: Code is self-documenting with explicit flag names
7. **Backward Compatibility**: Old code continues to work

## Testing Checklist

- [ ] 8bpp coded packed images decode correctly (no incorrect transparency)
- [ ] 16bpp uncoded packed images apply BGND/NOBLK correctly
- [ ] 4bpp coded images apply PLUTA padding
- [ ] Transparency masks only created when pixels are actually transparent
- [ ] AMV (Alternate Multiply Values) preserved for 8bpp coded formats
- [ ] Concatenated CEL files with multiple PDATs still work

## Next Steps

1. Test with previously broken image to verify fix
2. Gradually migrate `CelUnpacker` call sites to use `CelDecoder`
3. Add unit tests for each format type
4. Document which games use which formats
5. Consider deprecating old `CelUnpacker` methods once migration complete
