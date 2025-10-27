using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using OGLibCDi.Models;
using static ExtractCLUT.Helpers.ColorHelper;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using static ExtractCLUT.Helpers.TiledExportHelper;

namespace ExtractCLUT.Games
{

    // Action Codes
    // 0x00 - No Action
    // 0x01 - Level Start
    // 0x02 - Random Health Pickup
    // 0x03 - Random Points Pickup
    // 0x04 - Fire Power
    // 0x05 - Bullets
    // 0x07 - Add Time
    // 0x08 - Rapid Fire
    // 0x09 - Dynamite?
    // 0x0a - ?
    // 0x0b - Star
    // 0x0c - Checkpoint
    // 0x0d - End of Level
    // 0x0e - Exit?
    // 0x10,0x11,0x12 - Block Above and Left, Block Above, Block Above and Right - Collision
    // 0x20,0x22 - Block Left, Block Right - Collision
    // 0x23,0x24,0x25,0x26 - Warp Actions
    // 0x29 - Babe?
    // 0x3? - Enemy/Hazard


    public static class LuckyLuke
    {
        public static List<List<Image>> BossBlockParser(byte[] data, List<Color> palette)
        {
            var imageLists = new List<List<Image>>();
            var spriteDataList = new List<List<VFSpriteData>>();

            var blobStart = BitConverter.ToUInt32(data.Take(4).Reverse().ToArray(), 0);
            var overallDataStart = blobStart;
            var blobEnd = BitConverter.ToUInt32(data.Skip(4).Take(4).Reverse().ToArray(), 0);

            var blob = data.Skip((int)blobStart).Take((int)(blobEnd - blobStart)).ToArray();


            blobStart = blobEnd;

            var shouldSeek = true;
            var headerOffsetList = new List<uint>();
            var spriteHeaderBlobs = new List<byte[]>();
            var spriteBlobs = new List<byte[]>();
            var index = 0xc;
            while (shouldSeek)
            {
                var headerOffset = BitConverter.ToUInt32(data.Skip(index).Take(4).Reverse().ToArray(), 0);

                if (headerOffset > overallDataStart)
                {
                    shouldSeek = false;
                    break;
                }
                headerOffsetList.Add(headerOffset);
                index += 4;
            }

            headerOffsetList = headerOffsetList.OrderBy(x => x).ToList();

            for (int i = 0; i < headerOffsetList.Count; i++)
            {
                var start = (int)headerOffsetList[i];
                var end = i == headerOffsetList.Count - 1 ? (int)overallDataStart : (int)headerOffsetList[i + 1];
                var spriteHeaderBlob = data.Skip(start).Take((int)(end - start)).ToArray();
                spriteHeaderBlobs.Add(spriteHeaderBlob);
            }


            foreach (var sData in spriteHeaderBlobs)
            {
                var tempList = new List<VFSpriteData>();

                for (int i = 0; i < sData.Length; i += 16)
                {
                    if (i + 15 >= sData.Length)
                    {
                        break;
                    }
                    if (sData.Skip(i + 0xc).Take(1).First() == 0x80)
                    {
                        var offset = BitConverter.ToInt32(sData.Skip(i + 4).Take(4).Reverse().ToArray(), 0);
                        var width = sData.Skip(i + 13).Take(1).First();
                        var height = BitConverter.ToInt16(sData.Skip(i + 14).Take(2).Reverse().ToArray(), 0);
                        tempList.Add(new VFSpriteData { Width = width, Height = height, Offset = offset });
                    }
                    else
                    {
                        // for (int j=0; j <sData.Length; j+=4)
                        // {
                        //     var offset = BitConverter.ToInt32(sData.Skip(j).Take(4).Reverse().ToArray(), 0);
                        //     var width = 384;
                        //     var height = 240;
                        //     tempList.Add(new VFSpriteData { Width = width, Height = height, Offset = offset });
                        // }
                    }

                }
                spriteDataList.Add(tempList);
            }

            foreach (var list in spriteDataList)
            {
                var tempImageList = new List<Image>();
                foreach (var sprite in list)
                {
                    var sData = data.Skip(sprite.Offset).ToArray();
                    var output = CompiledSpriteHelper.DecodeCompiledSprite(sData, 0, 0x180);
                    var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
                    if (sprite.Width <= 0 || sprite.Height <= 0 || sprite.Width >= 384 || sprite.Height >= 240)
                    {
                        tempImageList.Add(image);
                        continue;
                    }
                    tempImageList.Add(CropImage(image, sprite.Width, sprite.Height, 0, 1));
                }
                imageLists.Add(tempImageList);
            }

            // get lowest offset in spriteDataList
            blobEnd = (uint)spriteDataList.SelectMany(x => x).Min(x => x.Offset);
            blob = data.Skip((int)blobStart).Take((int)(blobEnd - blobStart)).ToArray();

            overallDataStart = BitConverter.ToUInt32(blob.Take(4).Reverse().ToArray(), 0);
            shouldSeek = true;
            headerOffsetList = new List<uint>();
            spriteHeaderBlobs = new List<byte[]>();
            spriteBlobs = new List<byte[]>();
            index = 0;
            while (shouldSeek)
            {
                var headerOffset = BitConverter.ToUInt32(blob.Skip(index).Take(4).Reverse().ToArray(), 0);

                if (headerOffset > 0xFFF)
                {
                    shouldSeek = false;
                    break;
                }
                headerOffsetList.Add(headerOffset);
                index += 4;
            }

            headerOffsetList = headerOffsetList.OrderBy(x => x).ToList();

            for (int i = 0; i < headerOffsetList.Count; i++)
            {
                var start = (int)headerOffsetList[i];
                var end = i == headerOffsetList.Count - 1 ? (int)overallDataStart : (int)headerOffsetList[i + 1];
                var spriteHeaderBlob = blob.Skip(start).Take((int)(end - start)).ToArray();
                spriteHeaderBlobs.Add(spriteHeaderBlob);
            }


            foreach (var sData in spriteHeaderBlobs)
            {
                var tempList = new List<VFSpriteData>();

                for (int i = 0; i < sData.Length; i += 16)
                {
                    if (i + 15 >= sData.Length)
                    {
                        break;
                    }
                    if (sData.Skip(i + 0xc).Take(1).First() == 0x80)
                    {
                        var offset = BitConverter.ToInt32(sData.Skip(i + 4).Take(4).Reverse().ToArray(), 0);
                        var width = sData.Skip(i + 13).Take(1).First();
                        var height = BitConverter.ToInt16(sData.Skip(i + 14).Take(2).Reverse().ToArray(), 0);
                        tempList.Add(new VFSpriteData { Width = width, Height = height, Offset = offset });
                    }
                    else
                    {
                        // for (int j=0; j <sData.Length; j+=4)
                        // {
                        //     var offset = BitConverter.ToInt32(sData.Skip(j).Take(4).Reverse().ToArray(), 0);
                        //     var width = 384;
                        //     var height = 240;
                        //     tempList.Add(new VFSpriteData { Width = width, Height = height, Offset = offset });
                        // }
                    }

                }
                spriteDataList.Add(tempList);
            }

            foreach (var list in spriteDataList)
            {
                var tempImageList = new List<Image>();
                foreach (var sprite in list)
                {
                    var sData = blob.Skip(sprite.Offset).ToArray();
                    var output = CompiledSpriteHelper.DecodeCompiledSprite(sData, 0, 0x180);
                    var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
                    if (sprite.Width <= 0 || sprite.Height <= 0 || sprite.Width >= 384 || sprite.Height >= 240)
                    {
                        tempImageList.Add(image);
                        continue;
                    }
                    tempImageList.Add(CropImage(image, sprite.Width, sprite.Height, 0, 1));
                }
                imageLists.Add(tempImageList);
            }



            return imageLists;
        }
        public static List<byte[]> BlockParser(byte[] data, bool isBlock1 = false, bool isTileSpriteBlock = false)
        {
            // First 4 bytes = offset of data start
            // Read 4 bytes at a time, and add to list as uint until we find 0x45 0x4E 0x44 0x21 (END!)
            // Take data from data start to first offset in list, then from first offset to second offset, etc.
            var dataStart = BitConverter.ToUInt32(data.Take(4).Reverse().ToArray(), 0);
            var offsets = new List<uint>();
            var blockOffsets = new Dictionary<int, int>();
            offsets.Add(dataStart);
            for (int i = isBlock1 ? 8 : 4; i < dataStart; i += isBlock1 ? 8 : 4)
            {
                var offset = BitConverter.ToUInt32(data.Skip(i).Take(4).Reverse().ToArray(), 0);
                if (offset == 1162757153)
                {
                    if (isBlock1)
                    {
                        for (int j = 0; j < 12; j += 4)
                        {
                            offset = (uint)(BitConverter.ToUInt32(data.Skip(i + 8 + j).Take(4).Reverse().ToArray(), 0) + (i + 4));
                            offsets.Add(offset);
                        }
                    }
                    break;
                }
                else if (offset == 0)
                {
                    continue;
                }
                else if (!isTileSpriteBlock && offset < offsets.LastOrDefault())
                {
                    blockOffsets.Add(i / 4, (int)offset);
                    continue;
                }
                offsets.Add(offset);
            }

            // var sb = new StringBuilder();
            // foreach (var (offset, index) in offsets.WithIndex())
            // {
            //     if (index > 0 && index % 4 == 0)
            //     {
            //         sb.AppendLine();
            //     }
            //     sb.Append($"{offset:X8}\t");
            // }

            // File.WriteAllText(Path.Combine(Path.GetDirectoryName(inputFile), $"{Path.GetFileNameWithoutExtension(inputFile)}.txt"), sb.ToString());

            var blobs = new List<byte[]>();

            for (int i = 0; i < offsets.Count; i++)
            {
                if (i == 0 || (i > 0 && offsets[i] != dataStart))
                {
                    var start = (int)offsets[i];
                    var nextOffset = i == offsets.Count - 1 ? data.Length : (int)offsets[i + 1];
                    if (nextOffset == dataStart || nextOffset < offsets[i] || nextOffset == offsets[i]) nextOffset = (int)GetNextValidOffset(offsets, (int)offsets[i], i + 2).Item2;
                    var end = i == offsets.Count - 1 ? data.Length : (int)nextOffset;
                    var blob = data.Skip(start).Take(end - start).ToArray();
                    if (blob.Length >= 4 && BitConverter.ToInt32(blob.Take(4).Reverse().ToArray(), 0) == 1162757153)
                    {
                        continue;
                    }
                    else
                    {
                        if (i == offsets.Count - 1)
                        {
                            for (int j = 0; j < blob.Length; j += 4)
                            {
                                if (j + 4 > blob.Length || BitConverter.ToUInt32(blob.Skip(j).Take(4).Reverse().ToArray(), 0) == 1162757153)
                                {
                                    blob = blob.Take(j).ToArray();
                                }
                            }
                            blobs.Add(blob);
                            blobs.Add(data.Skip((int)(offsets[i] + blob.Length + 4)).Take((int)(data.Length - (offsets[i] + blob.Length + 4))).ToArray());
                            break;
                        }
                        blobs.Add(blob);
                    }
                }
                else
                {
                    blobs.Add([]);
                }
            }

            // var outputPath = Path.Combine(Path.GetDirectoryName(inputFile), Path.GetFileNameWithoutExtension(inputFile)); 
            // Directory.CreateDirectory(outputPath);

            // foreach (var (blob, index) in blobs.WithIndex())
            // {
            //     File.WriteAllBytes(Path.Combine(outputPath, $"{index}.bin"), blob);
            // }

            return blobs;

        }

        static Tuple<int, uint> GetNextValidOffset(List<uint> offsets, int currentOffset, int startIndex)
        {
            for (int i = startIndex; i < offsets.Count; i++)
            {
                if (offsets[i] != offsets[0] && offsets[i] > currentOffset && offsets[i] != currentOffset)
                {
                    return new Tuple<int, uint>(i, offsets[i]);
                }
            }
            return new Tuple<int, uint>(-1, 0);
        }

        public static Image CombineFGImages(List<Image> images)
        {
            int tileWidth = 4; // Width of each tile
            int tileHeight = 20; // Height of each tile
            int numTilesPerRow = 5; // Number of tiles per row

            // Calculate the width and height for the final combined image
            int width = numTilesPerRow * tileWidth;
            int height = (images.Count / numTilesPerRow) * tileHeight;

            // Create a new bitmap with the calculated width and height
            Bitmap combinedImage = new Bitmap(width, height);

            using (Graphics g = Graphics.FromImage(combinedImage))
            {
                g.Clear(Color.Transparent); // Optional: Fill the background

                // Draw each image in its respective position
                for (int i = 0; i < images.Count; i++)
                {
                    int x = (i % numTilesPerRow) * tileWidth;
                    int y = (i / numTilesPerRow) * tileHeight;
                    g.DrawImage(images[i], x, y, tileWidth, tileHeight);
                }
            }

            return combinedImage;
        }
        public static Image CombineBGImages(List<Image> images)
        {
            int tileWidth = 2; // Width of each tile
            int tileHeight = 20; // Height of each tile
            int numTilesPerRow = 10; // Number of tiles per row

            // Calculate the width and height for the final combined image
            int width = numTilesPerRow * tileWidth;
            int height = (images.Count / numTilesPerRow) * tileHeight;

            // Create a new bitmap with the calculated width and height
            Bitmap combinedImage = new Bitmap(width, height);

            using (Graphics g = Graphics.FromImage(combinedImage))
            {
                g.Clear(Color.Transparent); // Optional: Fill the background

                // Draw each image in its respective position
                for (int i = 0; i < images.Count; i++)
                {
                    int x = (i % numTilesPerRow) * tileWidth;
                    int y = (i / numTilesPerRow) * tileHeight;
                    g.DrawImage(images[i], x, y, tileWidth, tileHeight);
                }
            }

            return combinedImage;
        }

        public static void ExtractAllLevelData(string blkFolder, string outputFolder)
        {
            Directory.CreateDirectory(outputFolder);

            var blkFiles = Directory.GetFiles(blkFolder, "*.blk").Where(x => x.Contains("lv")).ToList();
            var cdiFiles = blkFiles.Select(x => new CdiFile(x)).ToList();

            foreach (var file in cdiFiles)
            {

                int levelIndex = int.Parse(file.FileName[file.FileName.IndexOf("lv") + 2].ToString());
                //if (levelIndex!=6) continue;
                var data = file.DataSectors.OrderBy(s => s.Channel)
                    .ThenBy(s => s.SectorIndex)
                    .SelectMany(s => s.GetSectorData())
                    .ToArray();

                var vFile = new VisionFactoryFile(file.FilePath, data);

                // vFile contains 2 "blocks" of data,
                // Block 0 contains 7, 8 or 9 sub-blocks of data (9 for levels 1,2, and 4, 8 for levels 3 and 5, and 7 for level 6)
                // - Sub Block 0 contains some sort of setup data for the level
                // - Sub Block 1 contains Luke Sprites and UI sprites with offsets
                // - Sub Block 2 contains Enemy Sprites with offsets
                // - Sub Block 3 contains fg day/night palettes
                // - Sub Block 4 contains bg day/night palettes
                // - Sub Block 5 contains contains (1,2,4) background tiles with offsets, (3,5,6) foreground tiles with offsets
                // - Sub Block 6 (1,2,4) foreground tiles with offsets, (3,5,6) item tile sprites with offsets
                // - Sub Block 7 contains (1,2,4 only) item tile sprite offsets
                // - Sub Block 8 contains (1,2,4 only) item tile sprite data
                // Block 1 contains varying number of mpeg blocks, and a final 4 blocks containing map data
                var block0 = BlockParser(vFile.SubFiles[0]);
                var block1 = BlockParser(vFile.SubFiles[1], true);

                // foreach (var (block, index) in  block0.WithIndex()) {
                //     File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv1s1.blk_1_0_0\0\{index}.bin", block);
                // }

                // Handle Block 0 - Palette 1
                var fgDayPalette = levelIndex == 6 ? ConvertBytesToRGB(block0[2].Skip(0).Take(0x180).ToArray()) : ConvertBytesToRGB(block0[3].Skip(0).Take(0x180).ToArray());
                var fgNightPalette = levelIndex == 6 ? ConvertBytesToRGB(block0[2].Skip(0).Take(0x180).ToArray()) : ConvertBytesToRGB(block0[3].Skip(0x180).Take(0x180).ToArray());

                // Handle Block 0 - Palette 2
                var bgDayPalette = levelIndex == 6 ? ConvertBytesToRGB(block0[3].Skip(0).Take(0x180).ToArray()) : ConvertBytesToRGB(block0[4].Skip(0).Take(0x180).ToArray());
                var bgNightPalette = levelIndex == 6 ? ConvertBytesToRGB(block0[3].Skip(0).Take(0x180).ToArray()) : ConvertBytesToRGB(block0[4].Skip(0x180).Take(0x180).ToArray());

                // Handle Block 0 -  Luke and UI Sprites
                var lukeAndUISpriteListsDay = ParseSpriteBlock(block0[1], fgDayPalette); // Returns a list of lists of images
                var lukeAndUISpriteListsNight = ParseSpriteBlock(block0[1], fgNightPalette); // Returns a list of lists of images

                // Handle Block 0 -  Enemy Sprites
                var enemySpriteListsDay = levelIndex == 6 ? new List<List<Image>>() : ParseSpriteBlock(block0[2], fgDayPalette); // Returns a list of lists of images
                var enemySpriteListsNight = levelIndex == 6 ? new List<List<Image>>() : ParseSpriteBlock(block0[2], fgNightPalette); // Returns a list of lists of images

                var bgTilesDay = new List<Image>();
                var bgTilesNight = new List<Image>();
                var fgTilesDay = new List<Image>();
                var fgTilesNight = new List<Image>();
                // Handle Block 0 -  Sub Block 4
                // if (LV1,2,4) - Background Tiles, if (LV3,5,6) - Foreground Tiles
                // Sub Block 5 (LV1,2,4)
                if (levelIndex == 1 || levelIndex == 2 || levelIndex == 4)
                {
                    bgTilesDay = ParseBGTileBlock(block0[5], bgDayPalette);
                    bgTilesNight = ParseBGTileBlock(block0[5], bgNightPalette);
                    fgTilesDay = ParseFGTileBlock(block0[6], fgDayPalette);
                    fgTilesNight = ParseFGTileBlock(block0[6], fgNightPalette);
                }
                else if (levelIndex == 6)
                {
                    fgTilesDay = ParseFGTileBlock(block0[4], fgDayPalette);
                    fgTilesNight = ParseFGTileBlock(block0[4], fgNightPalette);
                }
                else
                {
                    fgTilesDay = ParseFGTileBlock(block0[5], fgDayPalette);
                    fgTilesNight = ParseFGTileBlock(block0[5], fgNightPalette);
                }

                var tileSpriteData = levelIndex switch
                {
                    1 => block0[7].Concat(new byte[4] { 0x45, 0x4e, 0x44, 0x21 }).Concat(block0[8]).ToArray(),
                    2 => block0[7].Concat(new byte[4] { 0x45, 0x4e, 0x44, 0x21 }).Concat(block0[8]).ToArray(),
                    4 => block0[7].Concat(new byte[4] { 0x45, 0x4e, 0x44, 0x21 }).Concat(block0[8]).ToArray(),
                    6 => block0[5].Concat(new byte[4] { 0x45, 0x4e, 0x44, 0x21 }).Concat(block0[6]).ToArray(),
                    _ => block0[6].Concat(new byte[4] { 0x45, 0x4e, 0x44, 0x21 }).Concat(block0[7]).ToArray(),
                };
                // Handle Block 0 -  Sub Block 5 (LV3,5,6)/Sub Block 6(LV1,2,4)
                var tileSpriteDayList = ParseTileSpriteBlock(tileSpriteData, fgDayPalette);
                var tileSpriteNightList = ParseTileSpriteBlock(tileSpriteData, fgNightPalette);

                var spriteOutputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file.FilePath), "sprites");
                var bgOutputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file.FilePath), "bgTiles");
                var fgOutputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file.FilePath), "fgTiles");
                var tileSpriteOutputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file.FilePath), "tileSprites");

                var nightOutputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file.FilePath), "night");
                Directory.CreateDirectory(nightOutputPath);
                var spriteNightOutputPath = Path.Combine(nightOutputPath, "sprites");
                var bgNightOutputPath = Path.Combine(nightOutputPath, "bgTiles");
                var fgNightOutputPath = Path.Combine(nightOutputPath, "fgTiles");
                var tileSpriteNightOutputPath = Path.Combine(nightOutputPath, "tileSprites");

                Directory.CreateDirectory(spriteOutputPath);
                Directory.CreateDirectory(bgOutputPath);
                Directory.CreateDirectory(fgOutputPath);
                Directory.CreateDirectory(tileSpriteOutputPath);
                Directory.CreateDirectory(spriteNightOutputPath);
                Directory.CreateDirectory(bgNightOutputPath);
                Directory.CreateDirectory(fgNightOutputPath);
                Directory.CreateDirectory(tileSpriteNightOutputPath);

                for (int i = 0; i < tileSpriteDayList.Count; i++)
                {
                    tileSpriteDayList[i].Save(Path.Combine(tileSpriteOutputPath, $"TileSprite_{i}.png"), ImageFormat.Png);
                    tileSpriteNightList[i].Save(Path.Combine(tileSpriteNightOutputPath, $"TileSprite_{i}.png"), ImageFormat.Png);

                }

                for (int i = 0; i < lukeAndUISpriteListsDay.Count; i++)
                {
                    for (int j = 0; j < lukeAndUISpriteListsDay[i].Count; j++)
                    {
                        lukeAndUISpriteListsDay[i][j].Save(Path.Combine(spriteOutputPath, $"Luke_{i}_{j}.png"), ImageFormat.Png);
                        lukeAndUISpriteListsNight[i][j].Save(Path.Combine(spriteNightOutputPath, $"Luke_{i}_{j}.png"), ImageFormat.Png);
                    }
                }

                for (int i = 0; i < enemySpriteListsDay.Count; i++)
                {
                    for (int j = 0; j < enemySpriteListsDay[i].Count; j++)
                    {
                        enemySpriteListsDay[i][j].Save(Path.Combine(spriteOutputPath, $"Enemy_{i}_{j}.png"), ImageFormat.Png);
                        enemySpriteListsNight[i][j].Save(Path.Combine(spriteNightOutputPath, $"Enemy_{i}_{j}.png"), ImageFormat.Png);
                    }
                }

                for (int i = 0; i < bgTilesDay.Count; i++)
                {
                    bgTilesDay[i].Save(Path.Combine(bgOutputPath, $"BG_{i}.png"), ImageFormat.Png);
                    bgTilesNight[i].Save(Path.Combine(bgNightOutputPath, $"BG_{i}.png"), ImageFormat.Png);
                }

                for (int i = 0; i < fgTilesDay.Count; i++)
                {
                    fgTilesDay[i].Save(Path.Combine(fgOutputPath, $"FG_{i}.png"), ImageFormat.Png);
                    fgTilesNight[i].Save(Path.Combine(fgNightOutputPath, $"FG_{i}.png"), ImageFormat.Png);
                }

                var bgMapData = block1[^5].Skip(0x8).Take(0x1c20).ToArray();
                var fgMapData = block1[^4].Skip(0x8).Take(0x3840).ToArray();
                var itemMapData = block1[^3].Skip(0x8).Take(0x3840).ToArray();
                var actionMapData = block1[^2].Skip(0x788).Take(0x3840).ToArray();

                var bgMap = GenerateTileMapString(bgMapData, 240, 15);
                var fgMap = GenerateTileMapString(fgMapData, 480, 15);
                var itemMap = GenerateTileMapString(itemMapData, 480, 15);
                var actionMap = GenerateTileMapString(actionMapData, 480, 15);

                var bgMapPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file.FileName), "bgMap.txt");
                var fgMapPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file.FileName), "fgMap.txt");
                var itemMapPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file.FileName), "itemMap.txt");
                var actionMapPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file.FileName), "actionMap.txt");

                File.WriteAllText(bgMapPath, bgMap);
                File.WriteAllText(fgMapPath, fgMap);
                File.WriteAllText(itemMapPath, itemMap);
                File.WriteAllText(actionMapPath, actionMap);

            }
        }

        private static List<Image> ParseBGTileBlock(byte[] bytes, List<Color> palette)
        {
            var tileImages = new List<Image>();

            // tileBlobs[0] will be the offsets to the tiles,
            // tileBlobs[1] will be the actual tile data
            var tileBlobs = BlockParser(bytes);

            var offsetData = tileBlobs[0];
            var tileData = tileBlobs[1];

            var uintList = new List<uint>();

            for (int i = 0; i < offsetData.Length; i += 4)
            {
                var value = BitConverter.ToUInt32(offsetData.Skip(i).Take(4).Reverse().ToArray(), 0) / 40;
                uintList.Add(value);
            }

            var tempImageList = new List<Image>();

            for (int i = 0; i < uintList.Count; i++)
            {
                var tileOffset = (int)uintList[i] * 40;
                var tileBytes = tileData.Skip(tileOffset).Take(40).ToArray();
                var tileImage = ImageFormatHelper.GenerateClutImage(palette, tileBytes, 2, 20);
                tempImageList.Add(tileImage);
                if (tempImageList.Count == 10)
                {
                    tileImages.Add(CombineBGImages(tempImageList));

                    tempImageList.Clear();
                }
            }
            return tileImages;
        }

        private static List<Image> ParseFGTileBlock(byte[] bytes, List<Color> palette)
        {
            var tileImages = new List<Image>();

            // tileBlobs[0] will be the offsets to the tiles,
            // tileBlobs[1] will be the actual tile data
            var tileBlobs = BlockParser(bytes);

            var offsetData = tileBlobs[0];
            var tileData = tileBlobs[1];

            var uintList = new List<uint>();

            for (int i = 0; i < offsetData.Length; i += 4)
            {
                var value = BitConverter.ToUInt32(offsetData.Skip(i).Take(4).Reverse().ToArray(), 0) / 80;
                uintList.Add(value);
            }

            var tempImageList = new List<Image>();

            for (int i = 0; i < uintList.Count; i++)
            {
                var tileOffset = (int)uintList[i] * 80;
                var tileBytes = tileData.Skip(tileOffset).Take(80).ToArray();
                var tileImage = ImageFormatHelper.GenerateClutImage(palette, tileBytes, 4, 20, true);
                tempImageList.Add(tileImage);
                if (tempImageList.Count == 5)
                {
                    tileImages.Add(CombineFGImages(tempImageList));

                    tempImageList.Clear();
                }
            }

            return tileImages;
        }

        public static List<Image> ParseTileSpriteBlock(byte[] blockData, List<Color> palette)
        {
            var imageList = new List<Image>();
            var spriteBlobs = BlockParser(blockData, isTileSpriteBlock: true);
            var tempImageList = new List<Image>();
            foreach (var spriteData in spriteBlobs)
            {
                if (spriteData.Length == 0)
                {
                    tempImageList.Add(GenerateTransparentImage(4, 20));
                }
                else
                {
                    var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, 0, 0x180);
                    var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
                    var cropped = CropImage(image, 4, 20, 0, 1);
                    tempImageList.Add(cropped);
                }
                if (tempImageList.Count == 5)
                {
                    var combinedImage = CombineFGImages(tempImageList);
                    imageList.Add(combinedImage);
                    tempImageList.Clear();
                }
            }
            return imageList;
        }

        static Image GenerateTransparentImage(int width, int height)
        {
            var image = new Bitmap(width, height);
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    image.SetPixel(i, j, Color.FromArgb(0, 0, 0, 0));
                }
            }
            return image;
        }

        static List<List<Image>> ParseSpriteBlock(byte[] blockData, List<Color> palette)
        {
            var imageLists = new List<List<Image>>();
            var spriteDataList = new List<List<VFSpriteData>>();
            var offsetBlobs = BlockParser(blockData, false);
            offsetBlobs.RemoveAt(offsetBlobs.Count - 1);
            foreach (var data in offsetBlobs)
            {
                var tempList = new List<VFSpriteData>();

                for (int i = 0; i < data.Length; i += 16)
                {
                    if (i + 15 >= data.Length)
                    {
                        break;
                    }
                    var offset = BitConverter.ToInt32(data.Skip(i + 4).Take(4).Reverse().ToArray(), 0);
                    var width = data.Skip(i + 13).Take(1).First();
                    var height = BitConverter.ToInt16(data.Skip(i + 14).Take(2).Reverse().ToArray(), 0);
                    tempList.Add(new VFSpriteData { Width = width, Height = height, Offset = offset });

                }
                spriteDataList.Add(tempList);
            }

            foreach (var list in spriteDataList)
            {
                var tempImageList = new List<Image>();
                foreach (var sprite in list)
                {
                    var data = blockData.Skip(sprite.Offset).ToArray();
                    var output = CompiledSpriteHelper.DecodeCompiledSprite(data, 0, 0x180);
                    var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
                    if (sprite.Width <= 0 || sprite.Height <= 0 || sprite.Width > 384 || sprite.Height > 240)
                    {
                        tempImageList.Add(image);
                        continue;
                    }
                    tempImageList.Add(CropImage(image, sprite.Width, sprite.Height, 0, 1));
                }
                imageLists.Add(tempImageList);
            }

            return imageLists;
        }
    }
}


// <@540228612268490754 > "You need to create a Float var (Ball Speed) in the projectile class. 
// Set it as instance editable and exposed on spawn. Next set its default value to your absolute minimum speed. Save.

// Open the construction tab. Drag the projectile movement component (PMC) into the graph. 
// From it drag out and Set Initial Speed and Max Speed. Use your Ball speed as the value.

// Now you can dynamically set your ball speed on spawn. 
// The Spawn Actor of Class (your projectile) will now have a "Ball Speed" pin. 
// So calc the speed and pipe that value to the Spawn actor node."

// ~Rev0verDrive
