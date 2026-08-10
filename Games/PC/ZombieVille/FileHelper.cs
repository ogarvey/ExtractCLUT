using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
namespace ExtractCLUT.Games.PC.ZombieVille
{
    public static class FileHelper
    {
        private const string GameName = "ZombieVille";
        private const string GameRootDir = @"C:\Dev\Gaming\PC\Dos\Games\Zombieville\ZOMB";
        // methods will be added here in the future as needed
        // 2 varieties of .spr file handlers (basic - uint width, uint height, 4 byte padding/unknown, 512 byte palette (RGB555), pixel data (8-bit indexed);
        // advanced - offset list, asssumed pixel data unknown format)
        public static Image<Rgba32> ParseSprFileBasic(string filePath)
        {
            using var sprReader = new BinaryReader(File.OpenRead(filePath));
            uint width = sprReader.ReadUInt32();
            uint height = sprReader.ReadUInt32();
            sprReader.ReadBytes(4); // skip 4 byte padding/unknown
            var palData = sprReader.ReadBytes(512); // read 512 byte palette (RGB555)
            var palette = ColorHelper.ReadRgb15PaletteIS(palData);
            var pixelData = sprReader.ReadBytes((int)(width * height)); // read pixel data (8-bit indexed)
            var image = ImageFormatHelper.GenerateIMClutImage(palette, pixelData, (int)width, (int)height);
            return image;
        }
        // .dat file handler
        // .ani file handler
        // .ann file handler
        // .anm file handler
        // .nvp file handler
        // .zld file handler
        // .gdf file handler
        // .lvo file handler
        // .cnv file handler
        // .unc file handler
    }
}
