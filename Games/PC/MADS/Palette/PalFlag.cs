namespace ExtractCLUT.Games.PC.MADS.Palette
{
    public enum PalFlag
    {
        BACKGROUND = 0x8000,  // Loading initial background
        RESERVED = 0x4000,  // Enable mapping reserved colors
        ANY_TO_CLOSEST = 0x2000,  // Any color can map to closest
        ALL_TO_CLOSEST = 0x1000,  // Any colors that can map must map
        TOP_COLORS = 0x0800,  // Allow mapping to high four colors
        DEFINE_RESERVED = 0x0400,  // Define initial reserved color
        MASK = 0xfc00   // Mask for all the palette flags
    }
}
