using ExtractCLUT.Games;
using ExtractCLUT.Helpers;
using ExtractCLUT.Model;
using System.Drawing.Imaging;
using System.Drawing.Text;
using static ExtractCLUT.Helpers.AudioHelper;
using static ExtractCLUT.Helpers.ColorHelper;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using static ExtractCLUT.Utils;
using ExtractCLUT;
using System.Linq;
using System.Drawing;
using Image = System.Drawing.Image;
using Color = System.Drawing.Color;
using System.Text;
using ManagedBass;
using System.Diagnostics;
using System.Text.Json;
using OGLibCDi.Models;
using Newtonsoft.Json;
using OGLibCDi.Enums;
using static ExtractCLUT.Games.LaserLordsHelper;
using ExtractCLUT.Games.PC;
using Pfim;
using System.Runtime.InteropServices;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using ExtractCLUT.Games.Generic;

// var tileFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Joe Guard demo\Analysis\Tiles.bin";
// var dataFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Joe Guard demo\Analysis\common.dat_1_1_0.bin";

// var data = File.ReadAllBytes(dataFile);

// var offsetData = data.Take(0xd8).ToArray();
// var offsets = new List<int>();

// for (int i = 0; i < offsetData.Length; i += 4)
// {
//     var offset = BitConverter.ToInt32(offsetData.Skip(i).Take(4).Reverse().ToArray(), 0);
//     offsets.Add(offset);
// }

// foreach (var (offset, oIndex) in offsets.WithIndex())
// {
//   var bytesToTake = oIndex == offsets.Count - 1 ? data.Length - offset : offsets[oIndex + 1] - offset;
//   var chunk = data.Skip(offset).Take(bytesToTake).ToArray();
//   File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Joe Guard demo\Analysis\Common\{oIndex}.bin", chunk);
// }

// var tileData1 = File.ReadAllBytes(tileFile).Take(0x2ab00).ToArray();
// var tileData2 = File.ReadAllBytes(tileFile).Skip(0x2ce00).ToArray();
// var paletteData = File.ReadAllBytes(paletteFile).Skip(240).Take(520).ToArray();

// var palette = ReadClutBankPalettes(paletteData, 2);

// // create 16 * 16 pixel image for each tile
// for (int i = 0; i < tileData1.Length; i += 256)
// {
//     var tile = tileData1.Skip(i).Take(256).ToArray();
//     var image = GenerateClutImage(palette, tile, 16, 16);
//     image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Joe Guard demo\Asset Extraction\Tiles\1\{i / 256}.png", ImageFormat.Png);
// }

// for (int i = 0; i < tileData2.Length; i += 256)
// {
//   var tile = tileData2.Skip(i).Take(256).ToArray();
//   var image = GenerateClutImage(palette, tile, 16, 16);
//   image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Joe Guard demo\Asset Extraction\Tiles\2\{i / 256}.png", ImageFormat.Png);
// }

//var palette = ConvertBytesToRGB(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\Sprite Work\Palettes\palette_9.bin").Take(0x180).ToArray());
//var marioPalette = ConvertBytesToRGB(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\Output_bak\L1\palettes\L1_av.rtf_1_15_113.bin").Take(0x180).ToArray());
// var luigiPalette = ConvertBytesToRGB(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\Sprite Work\LuigiPalette.bin").Take(0x180).ToArray());
//HotelMarioHelper.ExtractSprites(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\930610", true, false, palette);

//HotelMarioHelper.ExtractSprites(@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario", true, false, palette);

//
// var palFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Treasures of the Lost Pyramid\Output_BAK\Pyrdata\pyrdata.rtr_1_0_0.bin";
// var palData = File.ReadAllBytes(palFile);
// var palette = ReadClutBankPalettes(palData, 2);
// PyramidHelper.ExtractSprites(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Treasures of the Lost Pyramid", palette: palette);



//CropImageFolder(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Steel Machine\Output_0x70_0x13", "*.png", 0, 1, 12,13, true);

// DimosQuest.ExtractSprites(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF");

//AlienGate.ExtractSprites(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Alien Gate\ENEMIES", "*.dat");

// var data = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Steel Machine\DATA\BINs\level1_1_0_0.bin").Skip(0x800).ToArray();

// var offsets = data.Take(0x30).ToArray();

// var offsetList = new List<int>();

// for (int i = 0; i < offsets.Length; i += 4)
// {
//     var offset = BitConverter.ToInt32(offsets.Skip(i).Take(4).Reverse().ToArray(), 0);
//     offsetList.Add(offset);
// }

// var blobs = new List<byte[]>();

// for (int i = 0; i < offsetList.Count; i++)
// {
//     var start = offsetList[i];
//     var end = i == offsetList.Count - 1 ? data.Length : offsetList[i + 1];
//     var blob = data.Skip(start).Take(end - start).ToArray();
//     blobs.Add(blob);
// }

// foreach (var (blob, index) in blobs.WithIndex())
// {
//     File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Steel Machine\DATA\BINs\L1\{index}.bin", blob);
// }

// var data = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\NAMCO compilation\GALAXIAN\Spriting\galax.spr_1_1_0.bin");

// var blobs = FileHelpers.ExtractSpriteByteSequences(null, data, [0x32, 0x3c], [0x4e, 0x75]);

// var defaultPalette = ConvertBytesToRGB(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\NAMCO compilation\GALAXIAN\Spriting\sprites.pal_1_1_0.bin").Take(0xa8).ToArray());

// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\NAMCO compilation\GALAXIAN\Spriting\Output_0x32_0x3c_200";
// Directory.CreateDirectory(outputFolder);

// foreach (var (blob, index) in blobs.WithIndex())
// {
//     var decodedBlob = CompiledSpriteHelper.DecodeCompiledSprite(blob, 0, 0x200);
//     var image = GenerateClutImage(defaultPalette, decodedBlob, 384, 240, true);
//     var outputName = Path.Combine(outputFolder, $"{index}.png");
//     if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
//     {
//         image.Save(outputName, ImageFormat.Png);
//     }
// }

// var folder = @"C:\Dev\Projects\Gaming\VGR\PC\DF\WAX\ILIFE";
// var outputFolder = @"C:\Dev\Projects\Gaming\VGR\PC\DF\WAX\ILIFE\Output";
// Directory.CreateDirectory(outputFolder);
// var maxDimensions = FindMaxDimensions(folder);

// var expandWidth = maxDimensions.maxWidth;
// var expandHeight = maxDimensions.maxHeight;

// string[] files = Directory.GetFiles(folder, "*.png");
// Parallel.ForEach(files, file =>
// {
//     ExpandImage(file, expandWidth, expandHeight, ExpansionOrigin.BottomCenter, false, outputFolder);
// });



// string filePath = @"C:\Program Files (x86)\GOG Galaxy\Games\Monkey Island 1 SE\Monkey1.pak";
// int minimumLength = 5; // Set your minimum string length
// List<string> asciiStrings = await FileFormatHelper.ScanForAsciiStringsAsync(filePath, minimumLength, requireNullTerminated: true);
// var dxtStrings = asciiStrings.Where(s => s.Contains("DXT")).ToList();
// Console.WriteLine($"{dxtStrings.Count} ASCII strings found:");



//----------------------------------------------------------------------------------------------//


//var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level2";


//TheApprentice.ExtractMapInfo(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\map7_1.bin");
//TheApprentice.ExtractBinaryData();
//TheApprentice.ExtractGoGfx();

//CropImageFolder(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\levelb\sprites\walk", "*.png", 0, 0, 31, 40, true);  

// var blkFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF", "*.blk");

// var cdiFiles = blkFiles.Select(f => new CdiFile(f)).ToList();

// var outputDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF\Output";
// Directory.CreateDirectory(outputDir);

// foreach (var cdiFile in cdiFiles)
// {
//     var dataSectors = cdiFile.DataSectors.OrderBy(s => s.Channel).ThenBy(s => s.SectorIndex).ToList();
//     var data = dataSectors.SelectMany(s => s.GetSectorData()).ToArray();
//     var filename = Path.GetFileNameWithoutExtension(cdiFile.FilePath);
//     File.WriteAllBytes($@"{outputDir}\{filename}.bin", data);
// }

// var binFiles = Directory.GetFiles(outputDir, "*.bin");

// var apprenticeFiles = new List<ApprenticeFile>();

// foreach (var file in binFiles)
// {
//     // if (!file.Contains("levelb"))
//     // {
//     //     continue;
//     // }
//     var aFile = new ApprenticeFile(file);
//     //apprenticeFiles.Add(aFile);
//     if (aFile.SubFiles.Count == 0)
//     {
//         continue;
//     }
//     var outputFolder = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(file));
//     Directory.CreateDirectory(outputFolder);
//     foreach (var (blob, index) in aFile.SubFiles.WithIndex())
//     {
//         File.WriteAllBytes(Path.Combine(outputFolder, $"{index}.bin"), blob);
//     }
// }

// var spriteData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF\Output\gfxset6\2.bin").Skip(0x300).Take(0x6f40).ToArray();
// var tileData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF\Output\gfxset6\1.bin");
// var palette = ConvertBytesToRGB(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF\Output\gfxset6\0.bin")
//                 .Take(0x180).ToArray());

// var startIndex = 0;
// var spriteIndex = 0;
// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF\Output\gfxset6\sprites";
// Directory.CreateDirectory(outputFolder);

// for (int i = 0; i < spriteData.Length; i++)
// {
//     if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
//     {
//         var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
//         var image = GenerateClutImage(palette, output, 384, 240, true);
//         CropImage(image, 16, 16, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
//         spriteIndex++;
//         startIndex = i + 2;
//     }
// }

// var tileImage = GenerateClutImage(palette, tileData, 320, 192,true);
// tileImage.Save(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF\Output\gfxset6\tiles.png", ImageFormat.Png);

//CropImageFolder(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF\Output\big\sprites", "*.png", 0, 0, 16, 16, true);

//CropImageFolderRandom(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\Asset Extraction\LEvels", "*.png", 148, 125);



// var palData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Golden Oldies volume I\APPL03\gdata\Palette.bin");
// var sprData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Golden Oldies volume I\APPL03\gdata\2.bin");

// var palette1 = ConvertBytesToRGB(palData.Take(0x180).ToArray());
// var startIndex = 0;
// var spriteIndex = 0;

// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Golden Oldies volume I\APPL03\gdata\output";

// //Directory.CreateDirectory(outputFolder);

// for (int i = 0; i < sprData.Length; i++)
// {
// 	if (sprData[i] == 0x4e && sprData[i + 1] == 0x75)
// 	{
// 		var output = CompiledSpriteHelper.DecodeCompiledSprite(sprData, startIndex, 0x180);
// 		//File.WriteAllBytes($@"{outputFolder}\{spriteIndex}.bin", output);
// 		var image = GenerateClutImage(palette1, output, 384, 240, true);
// 		if (IsImageFullyTransparent(image))
// 		{
// 			startIndex = i + 2;
// 			continue;
// 		}
// 		CropImage(image, 33, 33, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
// 		spriteIndex++;
// 		startIndex = i + 2;
// 	}
// }
//CropImageFolder(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Golden Oldies volume I\APPL03\gdata\output\ships", "*.png", 0, 0, 19, 19, true);


// var inputDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis";
// var outputDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks";
// Directory.CreateDirectory(outputDir);

// var binFiles = Directory.GetFiles(inputDir, "*.bin");

// //var apprenticeFiles = new List<VisionFactoryFile>();

// foreach (var file in binFiles)
// {
//     // if (!file.Contains("levelb"))
//     // {
//     //     continue;
//     // }
//     var aFile = new VisionFactoryFile(file);
//     //apprenticeFiles.Add(aFile);
//     if (aFile.SubFiles.Count == 0)
//     {
//         continue;
//     }
//     var outputFolder = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(file));
//     Directory.CreateDirectory(outputFolder);
//     foreach (var (blob, index) in aFile.SubFiles.WithIndex())
//     {
//         File.WriteAllBytes(Path.Combine(outputFolder, $"{index}.bin"), blob);
//     }
// }
//  var dataFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\4.bin";
//  var dat = File.ReadAllBytes(dataFile);
// // // //CropImageFolder(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\sprites\trot", "*.png", 0, 52, 126, 126, true);
// var blobs = LuckyLuke.BlockParser(dat,false);
// foreach (var (blob, index) in blobs.WithIndex())
// {
//     File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\4\{index}.bin", blob);
// }
//LuckyLuke.BlockParser(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv1s3.blk_1_0_0\0\6.bin",true);
// var mainDataFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\1.bin";
// //var offsetFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\1", "*.bin").OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f))).ToArray();
//var spriteDataList = new List<List<VFSpriteData>>();

// foreach (var bin in offsetFiles)
// {
//     var tempList = new List<SpriteData>();
//     var data = File.ReadAllBytes(bin);
//     for (int i = 0; i < data.Length; i+= 16)
//     {
//         if (i + 15 >= data.Length)
//         {
//             break;
//         }
//         var offset = BitConverter.ToInt32(data.Skip(i + 4).Take(4).Reverse().ToArray(), 0);
//         var width = data.Skip(i+13).Take(1).First();
//         var height = BitConverter.ToInt16(data.Skip(i + 14).Take(2).Reverse().ToArray(), 0);
//         tempList.Add(new SpriteData { Width = width, Height = height, Offset = offset });

//     }
//     spriteDataList.Add(tempList);
// }

// var palFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\2.bin";
// var spriteFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\5", "*.bin").OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f))).ToArray();
// //var spriteData = File.ReadAllBytes(mainDataFile);
// var paletteData = File.ReadAllBytes(palFile).Take(0x180).ToArray();

// var palette = ConvertBytesToRGB(paletteData);
// var spriteImageOutputPath = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\sprites_5";
// Directory.CreateDirectory(spriteImageOutputPath);
// var combinedImagePath = Path.Combine(spriteImageOutputPath, "combined");
// Directory.CreateDirectory(combinedImagePath);

// var tempImageList = new List<Image>();
// var spriteIndex= 0;
// foreach (var mainDataFile in spriteFiles)
// {
//     var spriteData = File.ReadAllBytes(mainDataFile);
//     if (spriteData.Length == 0)
//     {
//         tempImageList.Add(GenerateTransparentImage(4, 20));
//     }
//     else
//     {
//         var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, 0, 0x180);
//         var image = GenerateClutImage(palette, output, 384, 240, true);
//         var cropped = CropImage(image, 4, 20, 0, 1);
//         tempImageList.Add(cropped);
//     }
//     if (tempImageList.Count == 5) {
//         var combinedImage = LuckyLuke.CombineFGImages(tempImageList);
//         combinedImage.Save($@"{combinedImagePath}\{spriteIndex}.png", ImageFormat.Png);
//         tempImageList.Clear();
//         spriteIndex++;
//     }
// }

// static Image GenerateTransparentImage(int width, int height)
// {
//     var image = new Bitmap(width, height);
//     for (int i = 0; i < width; i++)
//     {
//         for (int j = 0; j < height; j++)
//         {
//             image.SetPixel(i, j, Color.FromArgb(0, 0, 0, 0));
//         }
//     }
//     return image;
// }

// foreach (var (list, lIndex) in spriteDataList.WithIndex()) {
//     foreach (var (sprite, sIndex) in list.WithIndex())
//     {
//         var data = spriteData.Skip(sprite.Offset).ToArray();
//         var output = CompiledSpriteHelper.DecodeCompiledSprite(data, 0, 0x180);
//         var image = GenerateClutImage(palette, output, 384, 240, true);
//         if (sprite.Width <= 0 || sprite.Height <= 0 || sprite.Width > 384 || sprite.Height > 240)
//         {
//             image.Save($@"{spriteImageOutputPath}\{lIndex}_{sIndex}.png", ImageFormat.Png);
//             continue;
//         }
//         CropImage(image, sprite.Width, sprite.Height, 0, 1).Save($@"{spriteImageOutputPath}\{lIndex}_{sIndex}.png", ImageFormat.Png);
//     }
// }

// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv1s1.blk_1_0_0\0\output\4.bin";
// var fgTileImageFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Asset Extraction\lv1s1.blk_1_0_0\fgDayTiles", "*.png").OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f))).ToArray();
// var bgTileImageFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Asset Extraction\lv1s1.blk_1_0_0\bgDayTiles", "*.png").OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f))).ToArray();
// var data = File.ReadAllBytes(file).Take(0x1810).ToArray();

// var uintList = new List<uint>();

// for (int i = 0; i < data.Length; i += 4)
// {
//     var value = BitConverter.ToUInt32(data.Skip(i).Take(4).Reverse().ToArray(), 0) / 40;
//     uintList.Add(value);
// }

// var tempImageList = new List<Image>();

// for (int i = 0; i < uintList.Count; i++)
// {
//     var tileIndex = (int)uintList[i];
//     var tileImage = Image.FromFile(bgTileImageFiles[tileIndex]);
//     tempImageList.Add(tileImage);
//     if (tempImageList.Count == 10)
//     {
//         var combinedImage = LuckyLuke.CombineBGImages(tempImageList);
//         combinedImage.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Asset Extraction\lv1s1.blk_1_0_0\bgDayTiles\combined\{i / 10}.png", ImageFormat.Png);
//         tempImageList.Clear();
//     }
// }


// var mapData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv1s3.blk_1_0_0\ItemMap.bin");

// OutputTileMap(mapData, 480, 15, @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv1s3.blk_1_0_0\ItemMap.txt");




// var inDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke";
// var outDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\ExtractAllLevelDataTest";

// LuckyLuke.ExtractAllLevelData(inDir, outDir);
// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv6s1.blk_1_0_0\1.bin";
// var blobs = LuckyLuke.BlockParser(File.ReadAllBytes(file),true);
// foreach (var (blob, index) in blobs.WithIndex())
// {
//     File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv6s1.blk_1_0_0\1\{index}.bin", blob);
// }

//var fgTxt = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\ExtractAllLevelDataTest\lv4s3\fgMap.txt";
// var itemTxt = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\ExtractAllLevelDataTest\lv1s1\actionMap.txt";

// // //IncrementNumbersInFile(fgTxt, 161, 480);
// IncrementNumbersInFile(itemTxt, 873, 480);

// void IncrementNumbersInFile(string filePath, int incrementAmount, int width)
// {
//     try
//     {
//         // Read the contents of the file
//         string fileContent = File.ReadAllText(filePath);

//         // create a backup of the original file
//         File.WriteAllText(filePath + ".bak", fileContent);

//         // Split the content into individual numbers
//         string[] numberStrings = fileContent.Split(',');

//         // Increment each number by the specified amount
//         var incrementedNumbers = numberStrings
//             .Select(number => int.Parse(number.Trim()) + incrementAmount)
//             .ToList();

//         var sb = new StringBuilder();

//         for (int i = 0; i < incrementedNumbers.Count; i ++)
//         {
//             sb.Append($"{incrementedNumbers[i].ToString()},");
//             if (i  % width == 0)
//             {
//                 sb.AppendLine();
//             }
//         }

//         // Join the numbers back into a comma-separated string
//         string updatedContent = sb.ToString().Trim().TrimEnd(',');

//         // Write the updated string back to the file
//         File.WriteAllText(filePath, updatedContent);

//         Console.WriteLine("Numbers incremented successfully.");
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"An error occurred: {ex.Message}");
//     }
// }


// GenerateImages(256, @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\ExtractAllLevelDataTest\ActionTiles");

// void GenerateImages(int count, string outputDirectory)
// {
//     for (int i = 0; i < count; i++)
//     {
//         using (Bitmap bitmap = new Bitmap(20, 20))
//         {
//             // Set the image to have a transparent background
//             bitmap.MakeTransparent();

//             using (Graphics graphics = Graphics.FromImage(bitmap))
//             {
//                 graphics.Clear(Color.Transparent);

//                 // Set up the font
//                 using (Font font = new Font("Arial", 10))
//                 {
//                     // Measure the string to determine the position
//                     string hexText = i.ToString("X"); 
//                     SizeF textSize = graphics.MeasureString(hexText, font);
//                     PointF position = new PointF(
//                         (bitmap.Width - textSize.Width) / 2,
//                         (bitmap.Height - textSize.Height) / 2
//                     );

//                     // Draw the text
//                     using (Brush brush = new SolidBrush(Color.Fuchsia))
//                     {
//                         graphics.DrawString(hexText, font, brush, position);
//                     }
//                 }
//             }

//             // Save the image
//             string fileName = $"{outputDirectory}/image_{i}.png";
//             bitmap.Save(fileName, ImageFormat.Png);
//         }
//     }

//     Console.WriteLine("Images generated successfully.");
// }

// var binFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv1s1.blk_1_0_0\1", "*.bin").OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f))).ToArray();
// var outputDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv1s1.blk_1_0_0\1\audio";
// Directory.CreateDirectory(outputDir);
// foreach (var file in binFiles) 
// {
//   var newFile = Path.Combine(outputDir, Path.GetFileName(file));
//   ConvertMp2ToWavAndMp3(file, Path.ChangeExtension(newFile, ".wav"), "wav");
// }

// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Atlantis_The Last Resort\game\rtfs\Spr\level1.spr_1_0_0.bin";

// var data = File.ReadAllBytes(file);

// var imageData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Atlantis_The Last Resort\game\rtfs\Spr\level1\2.bin").Skip(0xc).ToArray();
// var pal1 = data.Skip(0x4).Take(0x180).ToArray();

// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Atlantis_The Last Resort\game\rtfs\Spr\images";
// Directory.CreateDirectory(outputFolder);
// var palette = ConvertBytesToRGB(pal1);
// palette[^1] = Color.Transparent;
// var imageIndex = 0;
// for (int i =0; i < imageData.Length;)
// {
//     var image = imageData.Skip(i).Take(0x400).ToArray();
//     var clutImage = GenerateClutImage(palette, image, 32, 32, true);
//     CropImage(clutImage, 22,32,0,0).Save($@"{outputFolder}\2_{imageIndex}.png", ImageFormat.Png);
//     i += 0x41e;
//     imageIndex++;
// }

// 

//CreatureShock.ExtractData(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Creature Shock disc 2\SG2_1");

// var pngs = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Creature Shock disc 2\SG2_1\Extracted\jelly_5", "*.png");

// // get largest width and height
// var maxDimensions = FindMaxDimensions(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Creature Shock disc 2\SG2_1\Extracted\jelly_5");

// var expandWidth = maxDimensions.maxWidth;
// var expandHeight = maxDimensions.maxHeight;

// foreach (var png in pngs)
// {
//     ExpandImage(png, expandWidth, expandHeight, ExpansionOrigin.TopCenter, true, @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Creature Shock disc 2\SG2_1\Extracted\jelly_5");
// }

// var files = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Whack a Bubble\BACKGROUND", "*.rtf");

// foreach (var file in files)
// {
//   var cdiFile = new CdiFile(file);
//   var palData = cdiFile.DataSectors.OrderBy(s => s.Channel).ThenBy(s => s.SectorIndex).SelectMany(s => s.GetSectorData()).ToArray();
//   var imageData = cdiFile.VideoSectors.OrderBy(s => s.Channel).ThenBy(s => s.SectorIndex).SelectMany(s => s.GetSectorData()).ToArray();
//   var palette = ReadClutBankPalettes(palData,2);
//   var image = GenerateClutImage(palette, imageData, 384, 240);
//   image.Save(Path.Combine(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Whack a Bubble\BACKGROUND\Output", Path.GetFileNameWithoutExtension(file) + ".png"), ImageFormat.Png);
// }

// var paletteFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lost Ride\Analysis\s0_1_0_0.bin";
// var paletteData = File.ReadAllBytes(paletteFile).Skip(0x4).Take(0x180).ToArray();
// var palette = ConvertBytesToRGB(paletteData);

// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lost Ride\Analysis\s0_1_2_59.bin";

// var data = File.ReadAllBytes(file);

// List<byte[]> resultList = new List<byte[]>();
// int i = 0;
// int spriteIndex = 0;
// while (i < data.Length)
// {
//   var unk1 = BitConverter.ToInt16(data.Skip(i).Take(2).Reverse().ToArray(), 0);
//   var unk2 = BitConverter.ToInt16(data.Skip(i + 2).Take(2).Reverse().ToArray(), 0);
//   var width = BitConverter.ToInt16(data.Skip(i + 4).Take(2).Reverse().ToArray(), 0);
//   var height = BitConverter.ToInt16(data.Skip(i + 6).Take(2).Reverse().ToArray(), 0)+1;

//   // Skip the first 8 bytes
//   i += 20;
//   if (i >= data.Length) break;

//   List<byte> currentArray = new List<byte>();
//   bool foundSequence = false;

//   while (i < data.Length)
//   {
//     // Read the current byte
//     byte currentByte = data[i];
//     currentArray.Add(currentByte);
//     i++;

//     // Check if we have reached the 0x4e 0x75 sequence
//     if (currentArray.Count >= 2 && currentArray[currentArray.Count - 2] == 0x4e && currentArray[currentArray.Count - 1] == 0x75)
//     {
//       foundSequence = true;
//       break;
//     }
//   }

//   if (foundSequence)
//   {
//     var blob = currentArray.ToArray();
//     var output = CompiledSpriteHelper.DecodeCompiledSprite(blob, 0, 0x180);
//     var image = GenerateClutImage(palette, output, width, height, true);
//     image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lost Ride\Analysis\Output\{spriteIndex}_{unk1}_{unk2}.png", ImageFormat.Png);
//     spriteIndex++;
//   }

//   // Check for 8 consecutive 0x00 bytes to terminate the process
//   if (i < data.Length - 7 && IsEightZeroBytesSequence(data, i))
//   {
//     break;
//   }
// }

// foreach (var (blob,index) in resultList.WithIndex())
// {
//   }

// static bool IsEightZeroBytesSequence(byte[] input, int startIndex)
// {
//   for (int j = 0; j < 8; j++)
//   {
//     if (input[startIndex + j] != 0x00)
//     {
//       return false;
//     }
//   }
//   return true;
// }

// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Steel Machine\DATA\BINs\level1_1_0_0\0\1.bin";
// // var palFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Steel Machine\DATA\BINs\level1_1_0_0\0\11.bin";
// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Steel Machine\DATA\BINs\level1_1_0_0\0\1";

// Directory.CreateDirectory(outputFolder);
// var palData = File.ReadAllBytes(palFile).Skip(480).Take(96).ToArray();
// var palList = new List<List<Color>>();

// var palette = ConvertBytesToRGB(palData);
// palList.Add(palette);
// var tiles = new List<byte[]>();

// var data = File.ReadAllBytes(file);

// var tileInt16List = new List<int>();
// var sb = new StringBuilder();
// for (int i = 0; i < data.Length; i += 2)
// {
//     var tile = BitConverter.ToInt16(data.Skip(i).Take(2).Reverse().ToArray(), 0);
//     tileInt16List.Add(tile);
// }

// for (int i = 0; i < tileInt16List.Count; i++)
// {
//     sb.Append($"{(tileInt16List[i] + 1).ToString()},");
//     if (i % 450 == 0)
//     {
//         sb.AppendLine();
//     }
// }

// File.WriteAllText(Path.Combine(outputFolder, "output.txt"), sb.ToString().Trim().TrimEnd(','));

// var inputFolder = @"C:\RetroArch-Win64\system\Amiga\CommodoreAmigaRomset1\Civilization_v1.0_AGA_CD\aga";
// var ilbmFiles = Directory.GetFiles(inputFolder, "*.lbm");

// var outputFolder = @"C:\RetroArch-Win64\system\Amiga\CommodoreAmigaRomset1\Civilization_v1.0_AGA_CD\Sheets\Output";
// Directory.CreateDirectory(outputFolder);

// foreach (var ilbmFile in ilbmFiles)
// {
//   var outputFileName = Path.GetFileNameWithoutExtension(ilbmFile) + ".png";
//   try {

//     ConvertILBMToPNG(ilbmFile, Path.Combine(outputFolder, outputFileName));
//   } catch (Exception ex) {
//     Console.WriteLine($"Error converting {ilbmFile}: {ex.Message}");
//   }
// }

// data = data.Take(data.Length - 4).ToArray();

// for (int i = 0; i < data.Length; i += 0x40)
// {
//   var tile = data.Skip(i).Take(0x40).ToArray();
//   tiles.Add(tile);
// }

// for (int i = 0; i < tiles.Count; i+=4)
// {
//   var tilePart1 = tiles[i];
//   var tilePart2 = tiles[i + 1];
//   var tilePart3 = tiles[i + 2];
//   var tilePart4 = tiles[i + 3];
//   var imagePart1 = GenerateClutImage(palList[0], tilePart1, 4, 16, true);
//   var imagePart2 = GenerateClutImage(palList[0], tilePart2, 4, 16, true);
//   var imagePart3 = GenerateClutImage(palList[0], tilePart3, 4, 16, true);
//   var imagePart4 = GenerateClutImage(palList[0], tilePart4, 4, 16, true);
//   var image = CombineImages(new List<Image> { imagePart1, imagePart2, imagePart3, imagePart4 }, 4, 16,16,16);
//   image.Save($@"{ outputFolder}\{i/4}.png", ImageFormat.Png);
// }
// var offsetData = data.Take(0x348).ToArray();

// var offsetList = new List<int>();

// for (int i = 0; i < offsetData.Length; i += 4)
// {
//     var offset = BitConverter.ToInt32(offsetData.Skip(i).Take(4).Reverse().ToArray(), 0);
//     offsetList.Add(offset);
// }

// var blobs = new List<byte[]>();

// for (int i = 0; i < offsetList.Count; i++)
// {
//     var start = offsetList[i];
//     var end = i == offsetList.Count - 1 ? data.Length : offsetList[i + 1];
//     var blob = data.Skip(start).Take(end - start).ToArray();
//     blobs.Add(blob);
// }

// foreach (var (blob, index) in blobs.WithIndex())
// {
//   var output = CompiledSpriteHelper.DecodeCompiledSprite(blob, 0, 0x180);
//   for (int i = 0; i < palList.Count; i++)
//   {
//     var palOutputFolder = Path.Combine(outputFolder, $"Palette_{i}");
//     var image = GenerateClutImage(palList[i], output, 384, 240, true);
//     image.CropTransparentEdges().Save($@"{palOutputFolder}\{index}_{i}.png", ImageFormat.Png);
//   }
// }

// var pngs = Directory.GetFiles(@"C:\Dev\Projects\Gaming\VGR\PC\Neon\sprites\Dancer", "*.png");

// var maxDimensions = FindMaxDimensions(@"C:\Dev\Projects\Gaming\VGR\PC\Neon\sprites\Dancer");

// var expandWidth = maxDimensions.maxWidth;
// var expandHeight = maxDimensions.maxHeight;

// foreach (var png in pngs)
// {
//     ExpandImage(png, expandWidth, expandHeight, ExpansionOrigin.BottomCenter, false, @"C:\Dev\Projects\Gaming\VGR\PC\Neon\sprites\Dancer\expanded");
// }

var directories = Directory.GetDirectories(@"C:\RetroArch-Win64\system\Amiga\CommodoreAmigaRomset1\BeneathASteelSky_v2.0_CD32\data");

foreach (var directory in directories)
{
    var files = Directory.GetFiles(directory);

    foreach (var file in files)
    {
      var fileData = File.ReadAllBytes(file);

      // check if first 4 bytes are "FORM"
      if (fileData[0] == 0x46 && fileData[1] == 0x4f && fileData[2] == 0x52 && fileData[3] == 0x4d)
      {
        var formType = fileData.Skip(8).Take(4).ToArray();
        var formSize = BitConverter.ToInt32(fileData.Skip(4).Take(4).Reverse().ToArray(), 0);
        var formTypeString = Encoding.ASCII.GetString(formType);
        Console.WriteLine($"FORM Type: {formTypeString}, Size: {formSize}");
      }
    }
}


//AudioHelper.ConvertIffToWav(file, @"C:\Dev\Projects\Gaming\VGR\bullfrog_utils_rnc\183.wav");
