using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using static OGLibCDi.Helpers.ColorHelper;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using System.Drawing.Imaging;

namespace ExtractCLUT.Games
{
    public static class AliceHelper
    {
        public static void ExtractInventory()
        {
            var llInventory = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\ALICE IN WONDERLAND\Output\Alice_Rooms\alice_rooms.rtf_1_7_66.bin");

            var palBytes = llInventory.Take(0x80).ToArray();
            var palette = ReadPalette(palBytes);

            var imageBytes = llInventory.Skip(0x80).Take(0x4080).ToArray();
            var tileList = new List<byte[]>();

            for (int i = 0; i < 0x8000; i += 64)
            {
                tileList.Add(imageBytes.Skip(i).Take(64).ToArray());
            }

            for (int i = 0; i < tileList.Count; i += 64)
            {
                var listOfQuads = tileList.Skip(i).Take(64).ToList();
                // image is made up of 4 tiles, each tile is 8x8 pixels
                for (int j = 0; j < 32; j += 2)
                {
                    var quadTopLeft = listOfQuads[j];
                    var quadTopRight = listOfQuads[j + 1];
                    var quadBottomLeft = listOfQuads[j + 32];
                    var quadBottomRight = listOfQuads[j + 33];
                    var combinedQuads = new byte[256];
                    for (int x = 0; x < 16; x++)
                    {
                        for (int y = 0; y < 16; y++)
                        {
                            if (x < 8 && y < 8)
                            {
                                combinedQuads[x + y * 16] = quadTopLeft[x + y * 8];
                            }
                            else if (x >= 8 && y < 8)
                            {
                                combinedQuads[x + y * 16] = quadTopRight[x - 8 + y * 8];
                            }
                            else if (x < 8 && y >= 8)
                            {
                                combinedQuads[x + y * 16] = quadBottomLeft[x + (y - 8) * 8];
                            }
                            else if (x >= 8 && y >= 8)
                            {
                                combinedQuads[x + y * 16] = quadBottomRight[x - 8 + (y - 8) * 8];
                            }
                        }
                    }
                    var image = ImageFormatHelper.GenerateClutImage(palette, combinedQuads, 16, 16);
                    image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\ALICE IN WONDERLAND\Output\Alice_Rooms\combined\{i}_{j}.png");
                }
            }
        }


        public static void ParseNPCSpriteData(byte[] spriteData, string npcSpriteOutput, int recordIndex)
        {
            var palData = spriteData.Skip(0x20).Take(0x40).ToArray();
            var palette = ReadPalette(palData);

            var offsets = new List<int>();
            var offsetData = spriteData.Skip(0x60).Take(0x0E).ToArray();

            for (int i = 0; i < offsetData.Length; i += 2)
            {
                var offset = BitConverter.ToUInt16(offsetData.Skip(i).Take(2).Reverse().ToArray(), 0);
                offsets.Add(offset);
            }

            spriteData = spriteData.Skip(0x6E).Take(offsets[^1]).ToArray();
            offsets.RemoveAt(offsets.Count - 1);
            for (int i = 0; i < offsets.Count; i++)
            {
                var start = offsets[i];
                var end = i == offsets.Count - 1 ? spriteData.Length : offsets[i + 1];
                var chunk = spriteData.Skip(start).Take(end - start).ToArray();
                var decoded = DecodeImage(chunk);
                var image = GenerateClutImage(palette, decoded.imageData, 56, decoded.height, true);
                image.Save(Path.Combine(npcSpriteOutput, $"{recordIndex}_{i}.png"), ImageFormat.Png);
            }
        }

        public static void ParseSpriteData(byte[] spriteData, string outputDir)
        {
            var palData = spriteData.Take(0x40).ToArray();
            var palette = ColorHelper.ReadPalette(palData);

            var offsetData = spriteData.Skip(0x40).Take(0x9E).ToArray();
            var offsets = new List<int>();

            for (int i = 0; i < offsetData.Length; i += 2)
            {
                var offset = BitConverter.ToUInt16(offsetData.Skip(i).Take(2).Reverse().ToArray(), 0);
                offsets.Add(offset);
            }

            spriteData = spriteData.Skip(0x100).Take(offsets[^1]).ToArray();
            offsets.RemoveAt(offsets.Count - 1);

            var chunkData = new List<byte[]>();
            for (int i = 0; i < offsets.Count; i++)
            {
                var start = offsets[i];
                var end = i == offsets.Count - 1 ? spriteData.Length : offsets[i + 1];
                var chunk = spriteData.Skip(start).Take(end - start).ToArray();
                chunkData.Add(chunk);
            }

            var imageCount = 0;

            foreach (var chunk in chunkData)
            {
                var decoded = DecodeImage(chunk);
                var image = GenerateClutImage(palette, decoded.imageData, 56, decoded.height, true);
                image.Save(Path.Combine(outputDir, $"{imageCount++}.png"), ImageFormat.Png);
            }
        }
        
        public static (byte[] imageData, int height) DecodeImage(byte[] data)
        {
            List<byte[]> imageLines = new List<byte[]>();
            var imageLine = new byte[0x38];
            var imageLineIndex = 0;
            int i = 0;

            while (i < data.Length - 1)
            {
                if (data[i] == 0x00 && data[i + 1] == 0x38)
                {
                    // blank line
                    imageLines.Add(new byte[0x38]);
                    i += 2;
                    imageLineIndex = 0;
                }
                else if (data[i] == 0x00)
                {
                    // repeat 0x00 for data[i+1] bytes
                    for (int j = 0; j < data[i + 1]; j++)
                    {
                        imageLine[imageLineIndex] = 0x00;
                        imageLineIndex++;
                    }
                    i += 2;
                }
                else
                {
                    // use value of data[i] & 0x0F
                    imageLine[imageLineIndex] = (byte)(data[i] & 0x0F);
                    imageLineIndex++;
                    i++;
                }
                if (imageLineIndex == 0x38)
                {
                    imageLines.Add(imageLine);
                    imageLine = new byte[0x38];
                    imageLineIndex = 0;
                }
            }
            return (imageData: imageLines.SelectMany(x => x).ToArray(), height: imageLines.Count);
        }
    }
}
// var aliceRooms = @"C:\Dev\Gaming\CD-i\Games\ALICE IN WONDERLAND\alice_rooms.rtf";
// var aliceCdiFile = new CdiFile(aliceRooms);

// var mainOutputFolder = @"C:\Dev\Gaming\CD-i\Extractions\ALICE IN WONDERLAND\Output_Main";
// Directory.CreateDirectory(mainOutputFolder);

// var npcSpriteFolder = Path.Combine(mainOutputFolder, "NPC_Sprites");
// Directory.CreateDirectory(npcSpriteFolder);

// var playerSpriteFolder = Path.Combine(mainOutputFolder, "Player_Sprites_1");
// Directory.CreateDirectory(playerSpriteFolder);
// var playerSpriteFolder2 = Path.Combine(mainOutputFolder, "Player_Sprites_2");
// Directory.CreateDirectory(playerSpriteFolder2);
// var playerSpriteFolder3 = Path.Combine(mainOutputFolder, "Player_Sprites_3");
// Directory.CreateDirectory(playerSpriteFolder3);

// var sectors = aliceCdiFile.Sectors;

// var channel1Sectors = sectors.Where(x => x.Channel == 1).ToList();
// var channel1Data = channel1Sectors.Where(x => x.SubMode.IsData).Select(x => x.GetSectorData()).SelectMany(x => x).ToArray();
// AliceHelper.ParseSpriteData(channel1Data, playerSpriteFolder);
// var channel2Sectors = sectors.Where(x => x.Channel == 2).ToList();
// var channel2Data = channel2Sectors.Where(x => x.SubMode.IsData).Select(x => x.GetSectorData()).SelectMany(x => x).ToArray();
// AliceHelper.ParseSpriteData(channel2Data, playerSpriteFolder2);
// var channel3Sectors = sectors.Where(x => x.Channel == 3).ToList();
// var channel3Data = channel3Sectors.Where(x => x.SubMode.IsData).Select(x => x.GetSectorData()).SelectMany(x => x).ToArray();
// AliceHelper.ParseSpriteData(channel3Data, playerSpriteFolder3);

// var sectorList = new List<CdiSector>();

// foreach (var sector in sectors)
// {
//     sectorList.Add(sector);
//     if (sector.SubMode.IsEOR)
//     {
//         var channel5Sectors = sectorList.Where(x => x.Channel == 5).ToList();
//         if (channel5Sectors.Count > 0)
//         {
//             var dataBytes = channel5Sectors.Where(x => x.SubMode.IsData).Select(x => x.GetSectorData()).SelectMany(x => x).ToArray();
//             AliceHelper.ParseNPCSpriteData(dataBytes, npcSpriteFolder, channel5Sectors.First().SectorIndex);
//         }
//         sectorList.Clear();
//     }
// }
