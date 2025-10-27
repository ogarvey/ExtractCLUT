using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.MADS
{
    public static class MADUtils
    {
        public static void ExtractAllBackgrounds(string path)
        {
            var tileMapFiles = Directory.GetFiles(path, "*.MM", SearchOption.AllDirectories);
            var tileDataFiles = Directory.GetFiles(path, "*.TT", SearchOption.AllDirectories);
            var outputDir = Path.Combine(path, "OUTPUT", "Backgrounds");
            Directory.CreateDirectory(outputDir);

            foreach (var tileMapFile in tileMapFiles)
            {
                var tileDataFile = tileDataFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == Path.GetFileNameWithoutExtension(tileMapFile));
                if (tileDataFile == null) continue;
                try
                {
                    Decoding.ExtractV2BackgroundImage(tileMapFile, tileDataFile, outputDir);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {tileMapFile}: {ex.Message}");
                }
            }
        }
    
        public static void ExtractAllSprites(string path)
        {
            var spriteFiles = Directory.GetFiles(path, "*.SS", SearchOption.AllDirectories);
            var outputDir = Path.Combine(path, "OUTPUT", "Sprites");
            Directory.CreateDirectory(outputDir);

            foreach (var spriteFile in spriteFiles)
            {
                var sprite = new MadsPackFile(spriteFile);
                var shReader = sprite.GetEntryDataReader(1);
                var palReader = sprite.GetEntryDataReader(2);
                var spriteFileData = sprite.GetEntryData(3);
                var spriteHeaderList = new List<SpriteHeader>();

                var palette = new List<Color>();
                var count = palReader.ReadUInt16();
                for (int i = 0; i < count; i++)
                {
                    var r = palReader.ReadByte() * 255 / 63;
                    var g = palReader.ReadByte() * 255 / 63;
                    var b = palReader.ReadByte() * 255 / 63;
                    // now we convert the byte values between 0 and 255, to float values between 0 and 1
                    var fR = r / 255f;
                    var fG = g / 255f;
                    var fB = b / 255f;
                    
                    var color = new Rgba32(fR, fG, fB);
                    palette.Add(color);
                    palReader.ReadBytes(3);
                    Console.WriteLine(i);
                } 
                
                while (shReader.BaseStream.Position < shReader.BaseStream.Length)
                {
                    var header = new SpriteHeader
                    {
                        Offset = shReader.ReadUInt32(),
                        Length = shReader.ReadUInt32(),
                        WidthPadded = shReader.ReadUInt16(),
                        HeightPadded = shReader.ReadUInt16(),
                        Width = shReader.ReadUInt16(),
                        Height = shReader.ReadUInt16(),
                    };
                    spriteHeaderList.Add(header);
                }

                foreach (var (sh, sIndex) in spriteHeaderList.WithIndex())
                {
                    if (sh.Width == 0 || sh.Height == 0) continue;
                    var decodedSpriteData = Array.Empty<byte>();    
                    var spriteData = spriteFileData.Skip((int)sh.Offset).Take((int)sh.Length).ToArray();
                    try {
                        decodedSpriteData = Decoding.ReadSprite(sh, spriteData, false);
                    } catch (Exception ex) {
                        Console.WriteLine($"Error processing sprite {sIndex} in {spriteFile}: {ex.Message}");
                        continue;
                    }
                    //File.WriteAllBytes(Path.Combine(dir, $"sprite_{sIndex}.bin"), decodedSpriteData);
                    var image = ImageFormatHelper.GenerateIMClutImage(palette, decodedSpriteData, sh.Width, sh.Height, true, 0xff, false);

                    var imageOutputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(spriteFile) + $"_{sIndex}.png");
                    var imgOutputStream = new FileStream(imageOutputPath, FileMode.Create);
                    var imgEncoder = new PngEncoder() {
                        BitDepth = PngBitDepth.Bit8,
                        ColorType = PngColorType.RgbWithAlpha,
                        CompressionLevel = PngCompressionLevel.NoCompression,
                        FilterMethod = PngFilterMethod.Adaptive,
                        InterlaceMethod = PngInterlaceMode.Adam7,
                        Threshold = 0xFF,
                    };
                    
                    image.Save(imgOutputStream, imgEncoder);
                }
            }
        }
    }
}
