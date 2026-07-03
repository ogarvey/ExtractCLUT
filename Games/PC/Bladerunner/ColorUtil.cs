namespace BladeRunnerSliceExporter;

// Byte-exact port of BladeRunner::Color::get8BitColorFrom5Bit
// (engines/bladerunner/color.cpp lines 30-39).
// The table is ((int)i * 255) / 31 for i in [0,31] (integer division).
public static class ColorUtil
{
    private static readonly byte[] Map5To8 =
    {
        0, 8, 16, 24, 32, 41, 49, 57, 65, 74, 82, 90, 98, 106, 115, 123,
        131, 139, 148, 156, 164, 172, 180, 189, 197, 205, 213, 222, 230, 238, 246, 255
    };

    public static byte Get8BitFrom5Bit(byte col5b)
    {
        if (col5b > 31)
            return 255;
        return Map5To8[col5b];
    }
}
