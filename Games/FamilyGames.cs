using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using OGLibCDi.Models;
using static ExtractCLUT.Helpers.ColorHelper;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using static ExtractCLUT.Utils;

namespace ExtractCLUT.Games
{
    public class FamilyGames
    {
        public static void ExtractStuff()
        {
            var file = @"C:\Dev\Projects\Gaming\CD-i\Junkfood Jive\RTF\BUZZOFF\choice.rtf";
            var cdiFile = new CdiFile(file);

            var clut7Sectors = cdiFile.VideoSectors
                .Where(s => s.Coding.VideoString == "RL7")
                .OrderBy(s => s.SectorIndex).ToList();
            var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Junkfood Jive\RTF\BUZZOFF\AssetExtraction\choice";
            Directory.CreateDirectory(outputFolder);

            //var countSector = cdiFile.DataSectors.FirstOrDefault().GetSectorData();
            var sectorCounts = new List<byte>() { 15 };
            // for (int i = 0; i < 0x2; i++)
            // {
            //     sectorCounts.Add(countSector[i]);
            // }

            var allImages = new List<Bitmap>();
            for (int i = 0; i < sectorCounts.Count; i++)
            {
                var rleData = clut7Sectors.Take(sectorCounts[i]).SelectMany(s => s.GetSectorData()).ToArray();
                clut7Sectors.RemoveRange(0, sectorCounts[i]);
                var data = cdiFile.DataSectors.FirstOrDefault().GetSectorData();
                var paletteList = new List<List<Color>>() {
                    ReadClutBankPalettes(data.Take(0x208).ToArray(), 2),
                    // ReadClutBankPalettes(data.Skip(0x208).Take(0x208).ToArray(), 2),
                    // ReadClutBankPalettes(data.Skip(0x410).Take(0x208).ToArray(), 2),
                    // ReadClutBankPalettes(data.Skip(0x618).Take(0x208).ToArray(), 2)
                };

                //var rleData = data;
                var images = new List<Bitmap>();
                var imageData = OGLibCDi.Helpers.ImageFormatHelper.DecodeRle(rleData, 384);
                const int imageSize = 384 * 280;
                var imageCount = imageData.Length / imageSize;
                for (int j = 0; j < imageCount; j++)
                {
                    var image = GenerateClutImage(paletteList[0], imageData.Skip(j * imageSize).Take(imageSize).ToArray(), 384, 280, true);
                    image.Save(Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(file)}_{i}_{j}.png"), ImageFormat.Png);
                    // images.Add(image);
                    allImages.Add(image);
                }

                // var imageList = images.Select(x => (Image)x).ToList();
                //CreateGifFromImageList(imageList, Path.Combine(imageFolder, $"{Path.GetFileNameWithoutExtension(file)}_i.gif"), 25);
            }
            var bgFile1 = @"C:\Dev\Projects\Gaming\CD-i\Junkfood Jive\RTF\BUZZOFF\AssetExtraction\choice.png";
            // var bgFile2 = @"C:\Dev\Projects\Gaming\CD-i\Junkfood Jive\RTF\BUZZOFF\Asset Extraction\in back 2.png";
            // var bfFile3 = @"C:\Dev\Projects\Gaming\CD-i\Junkfood Jive\RTF\BUZZOFF\Asset Extraction\in back 3.png";
            // // // // Read bgFile png as separate image
            var bgImage1 = Image.FromFile(bgFile1);
            // var bgImage2 = Image.FromFile(bgFile2);
            // var bgImage3 = (Bitmap)Bitmap.FromFile(bfFile3);
            // var bgImages = new List<Image>() { bgImage1, bgImage2, bgImage1 };

            // allImages.AddRange(Enumerable.Repeat(bgImage3, 10).Select(x => x));

            CreateGifFromImageList(allImages
                .Select(x => (Image)x)
                .ToList(), Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(file)}_all.gif"), 50, 0, bgImage1);

        }
    }
}

// var clutData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Family Games I - Release\RTF\Yokosan\CLUT Palette.bin").Skip(0x10).ToArray();

// var palette = ReadClutBankPalettes(clutData, 2);

// var tileData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Family Games I - Release\RTF\Yokosan\Yokosan Tiles.bin");
// var tileOutputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Family Games I - Release\RTF\Yokosan\TilesTP";
// Directory.CreateDirectory(tileOutputFolder);
// for (int i = 0; i < tileData.Length; i += 256)
// {
//     var tile = tileData.Skip(i).Take(256).ToArray();
//     var image = GenerateClutImage(palette, tile, 16, 16, true);
//     image.Save(Path.Combine(tileOutputFolder, $"{i / 256}.png"), ImageFormat.Png);
// }


// var mapData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Family Games I - Release\RTF\Yokosan\yokosan map.bin");

// var tileIdList = new List<int>();

// for (int i = 0; i < mapData.Length; i += 2)
// {
//     var tileId = BitConverter.ToUInt16(mapData.Skip(i).Take(2).Reverse().ToArray(), 0) + 1;
//     if (tileId > 0xff) tileId = mapData[i + 1] + 1;
//     tileIdList.Add(tileId);
// }

// var tileIdsAsCommaSeparatedString = string.Join(",", tileIdList.Select(t => t.ToString()));

// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Family Games I - Release\RTF\Yokosan\Maps";
// Directory.CreateDirectory(outputFolder);

// // write the tile ids to a text file, 32 ids per line
// var tileIdTextFile = Path.Combine(outputFolder, "tileIds.txt");
// var sb = new StringBuilder();
// for (int i = 0; i < tileIdList.Count; i += 189)
// {
//     var ids = tileIdList.Skip(i).Take(189);
//     sb.AppendLine(string.Join(",", ids) + ",");
// }
// File.WriteAllText(tileIdTextFile, sb.ToString());
