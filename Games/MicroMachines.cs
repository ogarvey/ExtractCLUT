
using static ExtractCLUT.Helpers.ColorHelper;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using System.Drawing;
using System.Drawing.Imaging;
using OGLibCDi.Models;

namespace ExtractCLUT.Games
{
    public class MicroMachines
    {
        public static void ExtractSprites(string spriteFile, bool useTransparency, int transparencyIndex, bool lowerIndexes = true)
        {
            var data = File.ReadAllBytes(spriteFile);
            var paletteOffset = 12;
            var paletteBytes = data.Skip(paletteOffset).Take(0x180).ToArray();

            var palette = ConvertBytesToRGB(paletteBytes);

            var offsetData = data.Skip(0x18e).Take(0x1a0).ToArray();


            var spriteOffsets = new List<int>();

            for (var i = 0; i < 0x1a0; i += 4)
            {
                var bytes = offsetData.Skip(i).Take(4).Reverse().ToArray();
                var value = BitConverter.ToInt32(bytes, 0) - 0x200;
                if (value < 0)
                {
                    break;
                }
                spriteOffsets.Add(value);
            }

            var spriteData = data.Skip(0x38e).ToArray();

            var outputDirectory = Path.Combine(Path.GetDirectoryName(spriteFile), "Output", Path.GetFileNameWithoutExtension(spriteFile));
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
            for (var i = 0; i < spriteOffsets.Count; i++)
            {
                var width = spriteData[spriteOffsets[i]];
                if (width == 0)
                {
                    return;
                }
                var height = spriteData[spriteOffsets[i] + 1];
                var bytesToTake = width*height;
                var sprite = spriteData.Skip(spriteOffsets[i] + 2).Take(bytesToTake).ToArray();
                var image = GenerateClutImage(palette, sprite, width, height, useTransparency, transparencyIndex, lowerIndexes);
                image.Save($@"{outputDirectory}\sprite_{i}{(useTransparency ? "tp" : "")}.png", ImageFormat.Png);
            }

        }

        public static void ExtractTilesAndBlocks()
        {
            var files = new List<Tuple<string, string, int>>() {
                new Tuple<string, string, int>( "Bath",  @"C:\Dev\Projects\Gaming\CD-i\Micro Machines\BLOCKS\Output\Bath\BATH.BLK_1_1_0.bin", 0x30000),
                new Tuple<string, string, int>( "Bedroom",  @"C:\Dev\Projects\Gaming\CD-i\Micro Machines\BLOCKS\Output\Bedroom\BEDROOM.BLK_1_1_0.bin", 0x1c100),
                new Tuple<string, string, int>( "BREAKFST",  @"C:\Dev\Projects\Gaming\CD-i\Micro Machines\BLOCKS\Output\BREAKFST\BREAKFST.BLK_1_1_0.bin", 0x27900),
                new Tuple<string, string, int>( "GARAGE",  @"C:\Dev\Projects\Gaming\CD-i\Micro Machines\BLOCKS\Output\GARAGE\GARAGE.BLK_1_1_0.bin", 0x35200),
                new Tuple<string, string, int>( "GARDEN",  @"C:\Dev\Projects\Gaming\CD-i\Micro Machines\BLOCKS\Output\GARDEN\GARDEN.BLK_1_1_0.bin", 0x30400),
                new Tuple<string, string, int>( "PATIO",  @"C:\Dev\Projects\Gaming\CD-i\Micro Machines\BLOCKS\Output\PATIO\PATIO.BLK_1_1_0.bin", 0x18b00),
                new Tuple<string, string, int>( "POOL",  @"C:\Dev\Projects\Gaming\CD-i\Micro Machines\BLOCKS\Output\POOL\POOL.BLK_1_1_0.bin", 0x30200),
                new Tuple<string, string, int>( "SAND",  @"C:\Dev\Projects\Gaming\CD-i\Micro Machines\BLOCKS\Output\SAND\SAND.BLK_1_1_0.bin", 0x35200),
                new Tuple<string, string, int>( "SCHOOL",  @"C:\Dev\Projects\Gaming\CD-i\Micro Machines\BLOCKS\Output\SCHOOL\SCHOOL.BLK_1_1_0.bin", 0x33600)
            };
            // get system directory for User documents
            var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var outputPath = Path.Combine(docsPath, "CD-i", "Micro Machines", "BLOCKS", "Output");
            foreach (var file in files)
            {
                ExtractTilesAndBlocks(File.ReadAllBytes(file.Item2), file.Item1, file.Item3, outputPath);
            }

        }

        public static void ExtractFMV(string fmvPath)
        {
            var files = Directory.GetFiles(fmvPath, "*.GRN*.bin");

            foreach (var file in files)
            {
                var cdiFile = new CdiFile(file);
                var initialSector = cdiFile.Sectors.First(x => x.SubMode.IsData);
                var data = initialSector.GetSectorData().Take(0x180).ToArray();
                var palette = ConvertBytesToRGB(data);

                var remainingSectors = cdiFile.Sectors.Where(x => x.SubMode.IsData).Skip(1).ToList();

                var byteArrayList = new List<byte[]>();
                var images = new List<Image>();
                foreach (var sector in remainingSectors)
                {
                    byteArrayList.Add(sector.GetSectorData());
                    if (sector.SubMode.IsTrigger)
                    {
                        var imageBytes = byteArrayList.SelectMany(x => x).ToArray();
                        var image = GenerateRle7Image(palette, imageBytes, 192, 140);
                        images.Add(image);
                        byteArrayList.Clear();
                    }
                }
                var fmvOutputPath = Path.Combine(fmvPath,"\\Output");
                var gifOutputPath = Path.Combine(fmvOutputPath, $"{Path.GetFileNameWithoutExtension(file)}.gif");
                CreateGifFromImageList(images, gifOutputPath, 10);
            }

        }
        public static void ExtractTilesAndBlocks(byte[] data, string blockName, int tileByteCount, string outputPath)
        {
            var paletteOffset = 12;
            var tilesOffset = 0x138e;

            var tileBytes = data.Skip(tilesOffset).Take(tileByteCount).ToArray();
            //var screenBytes = data.Skip(0x7).Take(screenByteCount).ToArray();
            var paletteBytes = data.Skip(paletteOffset).Take(0x180).ToArray();

            var palette = ConvertBytesToRGB(paletteBytes);

            var tiles = GetScreenTiles(tileBytes);
            var tileOutputPath = Path.Combine(outputPath, "tiles");
            var blockOutputPath = Path.Combine(outputPath, "blocks");
            foreach (var (tile, index) in tiles.WithIndex())
            {
                var image = GenerateClutImage(palette, tile, 16, 16);
                image.Save($@"{tileOutputPath}\{blockName}_{index}.png", ImageFormat.Png);
            }

            var blockBytes = data.Skip(0x18e).Take(0x1200).ToArray();

            for (int i = 0; i < blockBytes.Length; i += 0x48)
            {
                var block = blockBytes.Skip(i).Take(0x48).ToArray();
                // create a 6 * 6 short array
                var blockArray = new short[6, 6];

                for (int j = 0; j < 6; j++)
                {
                    for (int l = 0; l < 6; l++)
                    {
                        var tileIndex = BitConverter.ToInt16(block.Skip((j * 6 + l) * 2).Take(2).Reverse().ToArray(), 0);
                        blockArray[j, l] = tileIndex;
                    }
                }

                // create a 96 * 96 image
                var blockImage = new Bitmap(96, 96);
                for (int j = 0; j < 6; j++)
                {
                    for (int l = 0; l < 6; l++)
                    {
                        var tile = tiles[blockArray[j, l]];
                        var image = GenerateClutImage(palette, tile, 16, 16);
                        for (int m = 0; m < 16; m++)
                        {
                            for (int n = 0; n < 16; n++)
                            {
                                blockImage.SetPixel(l * 16 + n, j * 16 + m, image.GetPixel(n, m));
                            }
                        }
                    }
                }
                blockImage.Save($@"{blockOutputPath}\{blockName}\block_{i}.png", ImageFormat.Png);
            }

        }
        public static List<byte[]> GetScreenTiles(byte[] data)
        {
            var tiles = new List<byte[]>();
            for (int i = 0; i < data.Length; i += 0x100)
            {
                var tile = new byte[0x100];
                Array.Copy(data, i, tile, 0, 0x100);
                tiles.Add(tile);
            }
            return tiles;
        }

        public static Image CreateScreenImage(int width, int height, List<byte[]> _tiles, byte[] _mapData, List<Color> _colors)
        {
            var mapTiles = new List<byte[]>();

            for (int i = 0; i < _mapData.Length; i += 2)
            {
                int index = (_mapData[i] << 8) + _mapData[i + 1];
                byte[] tile = new byte[256];
                Array.Copy(_tiles[index], tile, 256);
                mapTiles.Add(tile);
            }

            var tempScreenBitmap = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int tileX = x / 16;
                    int tileY = y / 16;
                    int tileIndex = tileX + (tileY * (width / 16));
                    int tilePixelX = x % 16;
                    int tilePixelY = y % 16;
                    int tilePixelIndex = tilePixelX + (tilePixelY * 16);
                    int colorIndex = mapTiles[tileIndex][tilePixelIndex];
                    Color color = _colors[colorIndex % 128];
                    tempScreenBitmap.SetPixel(x, y, color);
                }
            }
            return tempScreenBitmap;
        }
    }
}
