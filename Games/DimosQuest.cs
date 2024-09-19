using System.Drawing.Imaging;
using ExtractCLUT.Helpers;
using OGLibCDi.Models;
using static ExtractCLUT.Helpers.ColorHelper;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using System.Xml.Linq;
using System.Text;

namespace ExtractCLUT.Games
{
    public class DimosQuest
    {
        public static void ExtractLevelMaps(){
            var cdiFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF", "*.rtf")
                .Select(f => new CdiFile(f)).ToList();

            foreach (var level in cdiFiles)
            {
                var data = level.DataSectors.OrderBy(s => s.Channel).ThenBy(s => s.SectorIndex).SelectMany(s => s.GetSectorData()).ToArray();
                var tileShorts = new List<ushort>();
                var tileData = data.Skip(0x20).Take(0x18c0).ToArray();
                for (int i = 0; i < tileData.Length; i += 2)
                {
                    var s = BitConverter.ToUInt16(tileData.Skip(i).Take(2).Reverse().ToArray(), 0);
                    tileShorts.Add(s);
                }

                var levelId = int.Parse(Path.GetFileNameWithoutExtension(level.FilePath).Substring(3, 2));

                OutputTileMap(tileData, levelId);
            }

            static void OutputTileMap(byte[] data, int level)
            {
                var sb = new StringBuilder();
                var outputFolder = @"C:\Dev\Projects\Gaming\TiledProjects\Dimo\Automated";

                var outputName = Path.Combine(outputFolder, $"Level {level}.tmx");
                if (File.Exists(outputName))
                {
                    File.Delete(outputName);
                }

                var xmlString = """
	<?xml version="1.0" encoding="UTF-8"?>
	<map version="1.10" tiledversion="1.10.2" orientation="orthogonal" renderorder="right-down" width="72" height="44" tilewidth="16" tileheight="16" infinite="0" nextlayerid="2" nextobjectid="1">
		<tileset firstgid="1" source="../Tiles TILES_ID.tsx"/>
		<layer id="1" name="Tile Layer 1" width="72" height="44">
		<data encoding="csv">
		</data>
		</layer>
	</map>
	""";
                var TILE_ID = level / 10 + 1;
                xmlString = xmlString.Replace("TILES_ID", TILE_ID.ToString());

                var xmlDoc = XDocument.Parse(xmlString);
                var dataElement = xmlDoc.Descendants("data").FirstOrDefault();
                if (dataElement == null)
                {
                    Console.WriteLine("No <data> element found in the XML file.");
                    return;
                }
                // Assuming each tile index takes 2 bytes and the array is stored in row-major order.
                int width = 72;  // Original width is considered as the height due to mirroring.
                int height = 44; // Original height is considered as the width due to mirroring.

                // Calculate number of tiles based on data length
                if (data.Length != width * height * 2)
                {
                    throw new ArgumentException("Data length does not match expected size for a 72x44 map with 2 bytes per tile.");
                }

                // Create a 2D array to hold the tile indices
                ushort[,] map = new ushort[height, width];

                // Fill the map array with the tile indices
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        // Calculate the index for the byte array, note the swapping of x and y
                        int index = (x * height + y) * 2;
                        // Convert two bytes to one ushort (considering little-endian format here)
                        ushort tileIndex = BitConverter.ToUInt16(data.Skip(index).Take(2).Reverse().ToArray(), 0);
                        map[y, x] = (ushort)(tileIndex + 1);  // Assign to the transposed positions
                    }
                }

                // Output the map to the console or any other display mechanism
                for (int i = 0; i < height; i++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        sb.Append($"{map[i, j].ToString()},");
                    }
                    sb.AppendLine();
                }

                dataElement.Value = sb.ToString().Trim().TrimEnd(',');
                xmlDoc.Save(outputName);
            }

        }
        public static void ExtractSprites(string inputFolder)
        {
            var digits = new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

            var files = Directory.GetFiles(inputFolder, "*.blk");
            var paletteFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Dimo's Quest\RTF\Output\Palettes", "*.bin");

            var outputFolder = $@"{inputFolder}\Spriting";
            Directory.CreateDirectory(outputFolder);

            var defaultPaletteData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Dimo's Quest\RTF\Output\Palettes\gfxset.bin")
                .Take(0x300);
            var defaultPalette = ConvertBytesToRGB(defaultPaletteData.ToArray());

            foreach (var file in files)
            {
                var cdiFile = new CdiFile(file);
                var dataSectors = cdiFile.DataSectors.OrderBy(s => s.Channel).ThenBy(s => s.SectorIndex).ToList();

                var data = dataSectors.SelectMany(s => s.GetSectorData()).ToArray();
                var blobs = FileHelpers.ExtractSpriteByteSequences(null, data, [0x30, 0x3c], [0x4e, 0x75]);

                blobs.AddRange(FileHelpers.ExtractSpriteByteSequences(null, data, [0x20, 0x3c], [0x4e, 0x75]));

                blobs.AddRange(FileHelpers.ExtractSpriteByteSequences(null, data, [0x70, 0x02], [0x4e, 0x75]));
                blobs.AddRange(FileHelpers.ExtractSpriteByteSequences(null, data, [0x70, 0x01], [0x4e, 0x75]));

                var fileName = Path.GetFileNameWithoutExtension(file);
                var filenameNoDigits = fileName.TrimEnd(digits);
                var palette = paletteFiles.Where(f => f.Contains(filenameNoDigits)).Select(f => ConvertBytesToRGB(File.ReadAllBytes(f).Take(0x300).ToArray())).FirstOrDefault() ?? defaultPalette;

                var outputDir = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file));
                Directory.CreateDirectory(outputDir);
                var blobDir = Path.Combine(outputDir, "blobs");
                Directory.CreateDirectory(blobDir);

                foreach (var (blob, index) in blobs.WithIndex())
                {
                    var decodedBlob = CompiledSpriteHelper.DecodeCompiledSprite(blob, 0, 0x180);
                    // File.WriteAllBytes(Path.Combine(blobDir, $"{index}.bin"), blob);
                    // File.WriteAllBytes(Path.Combine(blobDir, $"{index}_decoded.bin"), decodedBlob);
                    var image = ImageFormatHelper.GenerateClutImage(palette, decodedBlob, 384, 240, true);
                    var outputName = Path.Combine(outputDir, $"{index}.png");
                    if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                    {
                        if (!file.Contains("set") && !file.Contains("big")) image.Save(outputName, ImageFormat.Png);
                        if (file.Contains("set") || file.Contains("big")) CropImage(image, 16, 16, 0, 1).Save(outputName, ImageFormat.Png);
                    }
                }
            }

        }
    }
}

// If, in your digging, you can find out what is underneath the icecubes in (for example) level 46 (https://shikotei.com/?p=DimosQuest see the black tiles), that'd be awesome.
// Same goes for the datablob that contains the monster data and switch data per level. They're at the end of each level's tile data and in an unknown format.
// I know that underneath some of them is a bomb (like in the case of the tile on the far right of level 8)
// But in some cases there is a key (like in level 44, center top, and center-left side). Level 44 also has bombs underneath the two stones (black tiles) at the right-center (between the orange and blue walls)
// But I would be very greatful if your digging reveals the abyss animations.
