using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using ExtractCLUT;
using ExtractCLUT.Games;
using ExtractCLUT.Games.Generic;
using ExtractCLUT.Games.PC;
using ExtractCLUT.Helpers;
using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Color = System.Drawing.Color;
using Image = SixLabors.ImageSharp.Image;
using Rectangle = System.Drawing.Rectangle;

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
//     var image = ImageFormatHelper.GenerateClutImage(palette, tile, 16, 16);
//     image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Joe Guard demo\Asset Extraction\Tiles\1\{i / 256}.png", ImageFormat.Png);
// }

// for (int i = 0; i < tileData2.Length; i += 256)
// {
//   var tile = tileData2.Skip(i).Take(256).ToArray();
//   var image = ImageFormatHelper.GenerateClutImage(palette, tile, 16, 16);
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
//     var image = ImageFormatHelper.GenerateClutImage(defaultPalette, decodedBlob, 384, 240, true);
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
//         var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
//         CropImage(image, 16, 16, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
//         spriteIndex++;
//         startIndex = i + 2;
//     }
// }

// var tileImage = ImageFormatHelper.GenerateClutImage(palette, tileData, 320, 192,true);
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
// 		var image = ImageFormatHelper.GenerateClutImage(palette1, output, 384, 240, true);
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
//         var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
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
//         var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
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
//     var clutImage = ImageFormatHelper.GenerateClutImage(palette, image, 32, 32, true);
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
//   var image = ImageFormatHelper.GenerateClutImage(palette, imageData, 384, 240);
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
//     var image = ImageFormatHelper.GenerateClutImage(palette, output, width, height, true);
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
//   var imagePart1 = ImageFormatHelper.GenerateClutImage(palList[0], tilePart1, 4, 16, true);
//   var imagePart2 = ImageFormatHelper.GenerateClutImage(palList[0], tilePart2, 4, 16, true);
//   var imagePart3 = ImageFormatHelper.GenerateClutImage(palList[0], tilePart3, 4, 16, true);
//   var imagePart4 = ImageFormatHelper.GenerateClutImage(palList[0], tilePart4, 4, 16, true);
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
//     var image = ImageFormatHelper.GenerateClutImage(palList[i], output, 384, 240, true);
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

// var directories = Directory.GetDirectories(@"C:\RetroArch-Win64\system\Amiga\CommodoreAmigaRomset1\BeneathASteelSky_v2.0_CD32\data");

// foreach (var directory in directories)
// {
//     var files = Directory.GetFiles(directory);

//     foreach (var file in files)
//     {
//       var fileData = File.ReadAllBytes(file);

//       // check if first 4 bytes are "FORM"
//       if (fileData[0] == 0x46 && fileData[1] == 0x4f && fileData[2] == 0x52 && fileData[3] == 0x4d)
//       {
//         var formType = fileData.Skip(8).Take(4).ToArray();
//         var formSize = BitConverter.ToInt32(fileData.Skip(4).Take(4).Reverse().ToArray(), 0);
//         var formTypeString = Encoding.ASCII.GetString(formType);
//         Console.WriteLine($"FORM Type: {formTypeString}, Size: {formSize}");
//       }
//     }
// }


//AudioHelper.ConvertIffToWav(file, @"C:\Dev\Projects\Gaming\VGR\bullfrog_utils_rnc\183.wav");

// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss4.blk_1_0_0.bin";
// var vFile = new VisionFactoryFile(file);
// var llpalData = vFile.SubFiles[0].Take(0x180).ToArray();
// var llpalette = ConvertBytesToRGB(llpalData);
// var output = LuckyLuke.BossBlockParser(vFile.SubFiles[2], llpalette);

// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss4.blk_1_0_0\sprites";

// Directory.CreateDirectory(outputFolder);

// for (int i = 0; i < output.Count; i++)
// {
//   for (int j = 0; j < output[i].Count; j++)
//   {
//     output[i][j].Save(Path.Combine(outputFolder, $"{i}_{j}.png"), ImageFormat.Png);
//   }
// }

// File.WriteAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss4.blk_1_0_0\palette.bin", llpalData);
// File.WriteAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss4.blk_1_0_0\bg.bin", vFile.SubFiles[1]);
//ImageFormatHelper.GenerateClutImage( llpalette, vFile.SubFiles[1], 512,256).Save(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss4.blk_1_0_0\bg.png", ImageFormat.Png);
// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss2.blk_1_0_0.bin";

// var vFile = new VisionFactoryFile(file);

// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss2.blk_1_0_0\output";

// Directory.CreateDirectory(outputFolder);

// foreach (var (blob, index) in vFile.SubFiles.WithIndex())
// {
//     File.WriteAllBytes(Path.Combine(outputFolder, $"{index}.bin"), blob);
// }

// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\SLUS_00747\PSXUSA\DATA\BIO.TIM";

// var data = File.ReadAllBytes(file);

// var paletteData = data.Skip(0x14).Take(0x20).ToArray();
// var imageData = data.Skip(0x40).ToArray();
// var palette = ConvertA1B5G5R5ToColors(paletteData);
// var image = Decode4BitImage(imageData,palette,256,68);
// image.Save(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\SLUS_00747\PSXUSA\DATA\BIO.png", ImageFormat.Png);


// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\SLUS_00747\PSXUSA\DATA\GWARNIN1.TIM";
// var fileData = File.ReadAllBytes(file).Skip(0x14).ToArray();
// var image = ConvertA1B5G5R5ToBitmap(fileData, 320,240);
// image.Save(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\SLUS_00747\PSXUSA\DATA\GWARNIN1.png", ImageFormat.Png);

// var sb = new StringBuilder();

// sb.AppendLine("Parsing PSX TIM files");

// var files = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\SLUS_00747\PSXUSA\DATA", "*.TIM");

// foreach (var file in files)
// {
//   var fileData = File.ReadAllBytes(file);
//   var type = BitConverter.ToUInt32(fileData.Skip(4).Take(4).ToArray(), 0);
//   switch (type)
//   {
//     case 0x8:
//       {
//         sb.AppendLine($"Parsing TIM file: {file}, with type: {type}");
//         var palLength = BitConverter.ToUInt16(fileData.Skip(0x10).Take(2).ToArray(), 0);
//         var palCount = BitConverter.ToUInt16(fileData.Skip(0x12).Take(2).ToArray(), 0);
//         var palBytes = palLength * 2 * palCount;
//         var paletteData = fileData.Skip(0x14).Take(palBytes).ToArray();
//         var imageOffset = 0x14 + palBytes + 0xc;
//         var imageData = fileData.Skip(imageOffset).ToArray();
//         var palette = ConvertA1B5G5R5ToColors(paletteData);
//         var width = BitConverter.ToUInt16(fileData.Skip(imageOffset-4).Take(2).ToArray(), 0) * 4;
//         var height = BitConverter.ToUInt16(fileData.Skip(imageOffset-2).Take(2).ToArray(), 0);
//         var image = Decode4BitImage(imageData, palette, width, height);
//         image.Save(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".png"), ImageFormat.Png);
//         break;
//       }
//     case 0x2:
//       {
//         sb.AppendLine($"Parsing TIM file: {file}");
//         var fileData2 = fileData.Skip(0x14).ToArray();
//         var width = BitConverter.ToUInt16(fileData.Skip(0x10).Take(2).ToArray(), 0);
//         var height = BitConverter.ToUInt16(fileData.Skip(0x12).Take(2).ToArray(), 0);
//         var image2 = ConvertA1B5G5R5ToBitmap(fileData2, width, height);
//         image2.Save(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".png"), ImageFormat.Png);
//         break;
//       }
//     default:
//       {
//         sb.AppendLine($"Parsing TIM file: {file} with unknown TIM type: {type}"); 
//         var palLength = BitConverter.ToUInt16(fileData.Skip(0x10).Take(2).ToArray(), 0);
//         var palCount = BitConverter.ToUInt16(fileData.Skip(0x12).Take(2).ToArray(), 0);
//         var palBytes = palLength * 2 * palCount;
//         var paletteData = fileData.Skip(0x14).Take(palBytes).ToArray();
//         var imageOffset = 0x14 + palBytes + 0xc;
//         var imageData = fileData.Skip(imageOffset).ToArray();
//         var palette = ConvertA1B5G5R5ToColors(paletteData);
//         var width = BitConverter.ToUInt16(fileData.Skip(imageOffset - 4).Take(2).ToArray(), 0) * 2;
//         var height = BitConverter.ToUInt16(fileData.Skip(imageOffset - 2).Take(2).ToArray(), 0);
//         var image = Decode8BitImage(imageData, palette, width, height);
//         image.Save(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".png"), ImageFormat.Png);
//         break;
//       }
//   }
// }

// File.WriteAllText(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\SLUS_00747\PSXUSA\DATA\TIM_Parsing.txt", sb.ToString());

// var dir = @"C:\Dev\Projects\Gaming\VGR\PC\Hunter\exported_files\images\DireHorse";

// var ddsFiles = Directory.GetFiles(dir, "*.dds");
// var output = Path.Combine(dir, "_converted");
// Directory.CreateDirectory(output);

// Parallel.ForEach (ddsFiles, (ddsFile) => DDSHelper.ConvertDDSImageToPNG(ddsFile, output));



//var inputDir = @"C:\Dev\Projects\Gaming\VGR\PC\Thimbleweed\raw\Delores\DeloresDiggingSheet_output";
// Parallel.ForEach(files, file =>
// {
//     ExpandImage(file, expandWidth, expandHeight, ExpansionOrigin.BottomCenter, false, outputFolder);
// });
// var imagesToResize = Directory.GetFiles(inputDir, "*.png");

// // get max width and height
// var maxDimensions = FindMaxDimensions(inputDir);

// var expandWidth = maxDimensions.maxWidth;
// var expandHeight = maxDimensions.maxHeight;

// var expandedOutputFolder = Path.Combine(Path.GetDirectoryName(imagesToResize[0]), "expanded");
// Directory.CreateDirectory(expandedOutputFolder);

// foreach (var image in imagesToResize)
// {
//   var origin = ExpansionOrigin.BottomCenter;
//   ExpandImage(image, expandWidth, expandHeight, origin, false, expandedOutputFolder);
// }

// For a given folder, convert all dds files to png
// Then move the dds to a backup folder
// then create a backup of the .dae file, and move to the same folder
// then parse the replacement .dae file to replace all ".dds" references with ".png"
// var inputDir = @"C:\Dev\Projects\Gaming\VGR\PC\FO4\Exported\Yangtzee";
// PackageForTMR(inputDir);

// ConvertTgaToPng(inputDir, inputDir);

// static void ConvertTgaToPng(string inputDirectory, string outputDirectory)
// {
//   string[] tgaFiles = Directory.GetFiles(inputDirectory, "*.tga", SearchOption.TopDirectoryOnly);

//   foreach (string tgaFilePath in tgaFiles)
//   {
//     string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(tgaFilePath);
//     string pngFilePath = Path.Combine(outputDirectory, fileNameWithoutExtension + ".png");

//     // Load TGA file using Pfim
//     using (var tgaImage = Pfimage.FromFile(tgaFilePath))
//     {
//       if (tgaImage == null)
//       {
//         Console.WriteLine($"Failed to load TGA file: {tgaFilePath}");
//         continue;
//       }

//       // Convert Pfim image to ImageSharp image
//       var image = ConvertToImageSharp(tgaImage);

//       // Save as PNG
//       image.Save(pngFilePath, new PngEncoder());

//       Console.WriteLine($"Converted {tgaFilePath} to {pngFilePath}");
//     }
//   }

//   Console.WriteLine("Conversion completed.");
// }

// static Image? ConvertToImageSharp(IImage tgaImage)
// {
//   int width = tgaImage.Width;
//   int height = tgaImage.Height;
//   byte[] data = tgaImage.Data;

//   switch (tgaImage.Format)
//   {
//     case Pfim.ImageFormat.Rgba32:
//       {
//         Rgba32[] pixelData = new Rgba32[width * height];
//         for (int i = 0; i < pixelData.Length; i++)
//         {
//           int dataIndex = i * 4;
//           // order is BGRA
//           pixelData[i] = new Rgba32(data[dataIndex + 2], data[dataIndex + 1], data[dataIndex + 0], data[dataIndex + 3]);
//         }
//         return Image.LoadPixelData<Rgba32>(pixelData, width, height);
//       }
//     case Pfim.ImageFormat.Rgb24:
//       {
//         Rgb24[] pixelData = new Rgb24[width * height];
//         for (int i = 0; i < pixelData.Length; i++)
//         {
//           int dataIndex = i * 3;
//           pixelData[i] = new Rgb24(data[dataIndex + 0], data[dataIndex + 1], data[dataIndex + 2]);
//         }
//         return Image.LoadPixelData<Rgb24>(pixelData, width, height);
//       }

//     // Add more cases for other formats if necessary

//     default:
//       throw new NotSupportedException($"Unsupported pixel format: {tgaImage.Format}");
//   }
//   return null;
// }

// static void PackageForTMR(string inputDir)
// {
//   var ddsFiles = Directory.GetFiles(inputDir, "*.dds");
//   var pngOutput = inputDir;
//   var ddsBackup = Path.Combine(Directory.GetParent(inputDir).Name, "dds_backup");
//   var daeBackup = Path.Combine(Directory.GetParent(inputDir).Name, "dae_backup");

//   var zipFile = Path.Combine(inputDir, "tmr_package.zip");
//   var zip = ZipFile.Open(zipFile, ZipArchiveMode.Create);

//   Directory.CreateDirectory(ddsBackup);
//   Directory.CreateDirectory(daeBackup);

//   Parallel.ForEach(ddsFiles, ddsFile =>
//   {
//     var pngFile = Path.Combine(pngOutput, inputDir);
//     DDSHelper.ConvertDDSImageToPNG(ddsFile, pngFile);
//     File.Move(ddsFile, Path.Combine(ddsBackup, Path.GetFileName(ddsFile)));
//   });

//   var daeFiles = Directory.GetFiles(inputDir, "*.dae");

//   Parallel.ForEach(daeFiles, daeFile =>
//   {
//     var backupFile = Path.Combine(daeBackup, Path.GetFileName(daeFile));
//     File.Copy(daeFile, backupFile);
//     var daeData = File.ReadAllText(daeFile);
//     var updatedData = daeData.Replace(".dds", ".png");
//     File.WriteAllText(daeFile, updatedData);
//   });

//   var pngFiles = Directory.GetFiles(pngOutput, "*.png");
//   daeFiles = Directory.GetFiles(inputDir, "*.dae");

//   foreach (var pngFile in pngFiles)
//   {
//     zip.CreateEntryFromFile(pngFile, Path.GetFileName(pngFile));
//   }

//   foreach (var daeFile in daeFiles)
//   {
//     zip.CreateEntryFromFile(daeFile, Path.GetFileName(daeFile));
//   }

//   zip.Dispose();
// }

// var jsonFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\VGR\PC\Thimbleweed\raw", "*sheet*.json");

// Parallel.ForEach(jsonFiles, file =>
// {
//   var outputFolder = Path.Combine(Path.GetDirectoryName(file), $"{Path.GetFileNameWithoutExtension(file)}_output");
//   TexturePacker.ExtractSprites(file, outputFolder);
// });



// var bm16dir = @"C:\Program Files (x86)\GOG Galaxy\Games\Broken Sword DC\Output";

// var bm16Files = Directory.GetFiles(bm16dir, "*.bm16");
// var outputDir = Path.Combine(bm16dir, "Output");
// Directory.CreateDirectory(outputDir);

// foreach (var imageFile in bm16Files)
// {
//   var data = File.ReadAllBytes(imageFile);
//   var width = BitConverter.ToUInt16(data.Skip(4).Take(2).ToArray(), 0);
//   var height = BitConverter.ToUInt16(data.Skip(6).Take(2).ToArray(), 0);
//   var imageData = data.Skip(8).ToArray();
//   var image = DecodeRgb16(imageData, width, height);
//   image.Save(Path.Combine(outputDir, Path.GetFileNameWithoutExtension(imageFile) + ".png"), ImageFormat.Png);
// }


//BrokenSword.ExtractAll();


// var folderToResize= @"C:\Dev\Projects\Gaming\VGR\PC\Simon\1\temp";

// var imagesToResize = Directory.GetFiles(folderToResize, "*.png");

// // get max width and height
// var maxDimensions = ImageFormatHelper.FindMaxDimensions(folderToResize);

// var expandWidth = maxDimensions.maxWidth;
// var expandHeight = maxDimensions.maxHeight;

// var expandedOutputFolder = Path.Combine(Path.GetDirectoryName(imagesToResize[0]), "expanded");
// Directory.CreateDirectory(expandedOutputFolder);

// foreach (var image in imagesToResize)
// {
//   var origin = ExpansionOrigin.BottomLeft;
//   ImageFormatHelper.ExpandImage(image, expandWidth, expandHeight, origin, false, expandedOutputFolder);
// }


// var imageInputFolder = @"C:\Program Files (x86)\GOG Galaxy\Games\Simon the Sorcerer - 25th Anniversary Edition\Simon1_data_all.bundle";
// var outputFolder = @"C:\Program Files (x86)\GOG Galaxy\Games\Simon the Sorcerer - 25th Anniversary Edition\Simon1_data_all.bundle\VGAOutput";

// Directory.CreateDirectory(outputFolder);

// var vgaFiles = Directory.GetFiles(imageInputFolder, "*.VGA");

// var oddVgaFiles = vgaFiles.Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("1")).ToArray();
// var evenVgaFiles = vgaFiles.Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("2")).ToArray();

// foreach (var vgaFile in evenVgaFiles)
// {
//   var data = File.ReadAllBytes(vgaFile);
//   var vgaOutputFolder = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(vgaFile));
//   Directory.CreateDirectory(vgaOutputFolder);
//   if (BitConverter.ToInt32(data.Take(4).Reverse().ToArray(), 0) != 0) continue;
//   // get the odd file which will be this file - 1
//   // eg: if vgaFile is 0002.VGA, then the odd file will be 0001.VGA
//   // ensure that the zero padding is correct
//   var oddFile = Path.Combine(imageInputFolder, $"{(int.Parse(Path.GetFileNameWithoutExtension(vgaFile)) - 1).ToString("D4")}.VGA");

//   var paletteData = File.ReadAllBytes(oddFile);
//   var paletteCount = BitConverter.ToUInt16(paletteData.Take(2).Reverse().ToArray(), 0);

//   var palStart = 6;

//   var palettes = new List<List<Color>>();

//   for (int i = 0; i < paletteCount; i++)
//   {
//     var palette = paletteData.Skip(palStart + (i * 0x60)).Take(0x60).ToArray();
//     palettes.Add(ColorHelper.ConvertBytesToRGB(palette,4));
//   }

//   var offsetList = new List<spraOffset>();

//   var index = 0;

//   while (data[index] == 0x00)
//   {
//     var offset = BitConverter.ToInt32(data.Skip(index).Take(4).Reverse().ToArray(), 0);
//     var height = BitConverter.ToUInt16(data.Skip(index + 4).Take(2).Reverse().ToArray(), 0);
//     var width = BitConverter.ToInt16(data.Skip(index + 6).Take(2).Reverse().ToArray(), 0);
//     offsetList.Add(new spraOffset { Length = offset, Width = width, Height = height });
//     index += 8;
//   }

//   for (int i = 0; i < offsetList.Count; i++)
//   {
//     if (offsetList[i].Width <= 0 || offsetList[i].Height <= 0 || offsetList[i].Width > 1280 || offsetList[i].Height >= 65535) continue;
//     var offset = offsetList[i];
//     if (offset.Length == 0) continue;
//     var nextOffset = i == offsetList.Count - 1 ? data.Length : offsetList[i + 1].Length;
//     var bytes = data.Skip(offset.Length).Take(nextOffset - offset.Length).ToArray();
//     var compressed = (offset.Height & 0x8000) != 0x0;
//     var actualHeight = offset.Height & 0x7FFF;
//     if (compressed)
//     {
//       bytes = AgosCompression.DecodeImage(bytes, 0, null, actualHeight, offset.Width / 2);
//     }

//     File.WriteAllBytes(Path.Combine(vgaOutputFolder, $"{i}.bin"), bytes);
//     for (int j = 0; j < palettes.Count; j++)
//     {
//       var palOutputFolder = Path.Combine(vgaOutputFolder, $"Palette_{j}");
//       Directory.CreateDirectory(palOutputFolder);
//       var image = ImageFormatHelper.Decode4Bpp(bytes, palettes[j], offset.Width, actualHeight);
//       image.Save(Path.Combine(palOutputFolder, $"{i}_{j}.png"), ImageFormat.Png);
//     }
//   }
// }

// var folderToResize = @"C:\Dev\Projects\Gaming\VGR\PC\Brutal-Paws-of-Fury_DOS_EN\gfx\Image_Output\Sprites\ivnspr\Shock";

// var imagesToResize = Directory.GetFiles(folderToResize, "*.png");

// //get max width and height
// var maxDimensions = ImageFormatHelper.FindMaxDimensions(folderToResize);

// var expandWidth = maxDimensions.maxWidth;
// var expandHeight = maxDimensions.maxHeight;

// var expandedOutputFolder = Path.Combine(Path.GetDirectoryName(imagesToResize[0]), "expanded");

// Directory.CreateDirectory(expandedOutputFolder);

// foreach (var image in imagesToResize)
// {
//   var origin = ExpansionOrigin.BottomLeft;
//   ImageFormatHelper.ExpandImage(image, expandWidth, expandHeight, origin, false, expandedOutputFolder);
// }

//var inputDir = @"C:\Dev\Projects\Gaming\VGR\PC\Brutal-Paws-of-Fury_DOS_EN\gfx";

// var celFiles = Directory.GetFiles(inputDir, "*.cel");
// var celOutputDirectory = Path.Combine(inputDir, "Image_Output", "Cel");
// Directory.CreateDirectory(celOutputDirectory);

// foreach (var cel in celFiles) {
//   var data = File.ReadAllBytes(cel);
//   var width = BitConverter.ToInt16(data.Skip(2).Take(2).ToArray(), 0);
//   var height = BitConverter.ToInt16(data.Skip(4).Take(2).ToArray(), 0);
//   var paletteData = data.Skip(32).Take(768).ToArray();
//   var palette = ColorHelper.ConvertBytesToRGB(paletteData, 4);
//   var imageData = data.Skip(800).ToArray();
//   var image = ImageFormatHelper.GenerateClutImage(palette, imageData, width, height, true);
//   image.Save(Path.Combine(celOutputDirectory, Path.GetFileNameWithoutExtension(cel) + ".png"), ImageFormat.Png);
// }

// var palFiles = Directory.GetFiles(inputDir, "*.pal");
// var colFiles = Directory.GetFiles(inputDir, "*.col");
// var spriteFiles = Directory.GetFiles(inputDir, "*.bin");
// var dcdFiles = Directory.GetFiles(inputDir, "*.dcd");
// var dclFiles = Directory.GetFiles(inputDir, "*.dcl");
// var offsetFiles = Directory.GetFiles(inputDir, "*.off");

// var mainSpriteOutputDirectory = Path.Combine(inputDir, "Image_Output", "Sprites");

// foreach (var dclFile in dclFiles)
// {
//   var spriteOutputDirectory = Path.Combine(mainSpriteOutputDirectory, Path.GetFileNameWithoutExtension(dclFile));
//   var name = Path.GetFileNameWithoutExtension(dclFile);

//   var data = File.ReadAllBytes(dclFile);

//   var colFile = name.Contains("wave") ? colFiles.FirstOrDefault(f => f.Contains("water")) : colFiles.FirstOrDefault(f => f.Contains(name.Substring(0,3)));
//   var paletteData1 = File.ReadAllBytes(colFile).Skip(8).ToArray();
//   var palette1 = ColorHelper.ConvertBytesToRGB(paletteData1, 1);

//   var offsetList = new List<int>();

//   try {
//     var image = RenderDclSprite(data, palette1);
//     Directory.CreateDirectory(spriteOutputDirectory);
//     image.Save(Path.Combine(spriteOutputDirectory, $"{name}_pal1.png"), ImageFormat.Png);
//   } catch (Exception ex) {
//     Console.WriteLine($"Error rendering {dclFile}: {ex.Message}");
//   }
// }

// foreach (var offsetFile in offsetFiles)
// {
//   var spriteOutputDirectory = Path.Combine(mainSpriteOutputDirectory, Path.GetFileNameWithoutExtension(offsetFile));
//   var name = Path.GetFileNameWithoutExtension(offsetFile);
//   var data = File.ReadAllBytes(offsetFile);

//   var offsetList = new List<int>();
//   for (int i = 0; i < data.Length; i += 4)
//   {
//     var offset = BitConverter.ToInt32(data.Skip(i).Take(4).ToArray(), 0);
//     offsetList.Add(offset);
//   }

//   var spriteData = File.ReadAllBytes(spriteFiles.First(f => f.Contains(name)));
//   var palFile = palFiles.FirstOrDefault(f => f.Contains(name)) ?? "";
//   if (string.IsNullOrEmpty(palFile))
//   {
//     palFile = palFiles.FirstOrDefault(f => f.Contains(name.Substring(0, 4))) ?? "";
//     if (string.IsNullOrEmpty(palFile))
//     {
//       palFile = palFiles.FirstOrDefault(f => f.Contains(name.Substring(0, 3)));

//       if (string.IsNullOrEmpty(palFile))
//       {
//         palFile = palFiles.FirstOrDefault(f => f.Contains(name.Substring(0, 2)));

//         if (string.IsNullOrEmpty(palFile))
//         {
//           return;
//         }
//       }
//     }
//   }
//   Directory.CreateDirectory(spriteOutputDirectory);
//   var palData = File.ReadAllBytes(palFile);

//   var palette = ColorHelper.ConvertBytesToRGB(palData, 4);

//   var sprites = new List<byte[]>();

//   for (int i = 0; i < offsetList.Count; i++)
//   {
//     var start = offsetList[i];
//     var end = i == offsetList.Count - 1 ? spriteData.Length : offsetList[i + 1];
//     var sprite = spriteData.Skip(start).Take(end - start).ToArray();
//     var spriteImage = RenderSprite(sprite, palette);
//     spriteImage.Save(Path.Combine(spriteOutputDirectory, $"{i}.png"), ImageFormat.Png);
//   }
// }

// foreach (var dcdFile in dcdFiles)
// {
//   var dcdOutputDirectory = Path.Combine(mainSpriteOutputDirectory, Path.GetFileNameWithoutExtension(dcdFile));
//   var name = Path.GetFileNameWithoutExtension(dcdFile);
//   var data = File.ReadAllBytes(dcdFile);

//   var palFile = palFiles.FirstOrDefault(f => f.Contains(name)) ?? "";
//   if (string.IsNullOrEmpty(palFile))
//   {
//     palFile = palFiles.FirstOrDefault(f => f.Contains(name.Substring(0, 4))) ?? "";
//     if (string.IsNullOrEmpty(palFile))
//     {
//       palFile = palFiles.FirstOrDefault(f => f.Contains(name.Substring(0, 3)));

//       if (string.IsNullOrEmpty(palFile))
//       {
//         palFile = palFiles.FirstOrDefault(f => f.Contains(name.Substring(0, 2)));

//         if (string.IsNullOrEmpty(palFile))
//         {
//           return;
//         }
//       }
//     }
//   }
//   var palData = File.ReadAllBytes(palFile);
//   var palette = ColorHelper.ConvertBytesToRGB(palData, 4);

//   try{
//     var sprite = RenderCamSprite(data, palette);
//     Directory.CreateDirectory(dcdOutputDirectory);
//     sprite.Save(Path.Combine(dcdOutputDirectory, $"{name}.png"), ImageFormat.Png);
//   } catch (Exception ex) {
//     Console.WriteLine(ex.Message);
//   }
// }

//var aniInputDir = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\CHILL\LEVELS";
// var aniPaths = Directory.GetFiles(aniInputDir, "*.*");
// var pcxPaths = Directory.GetFiles(aniInputDir, "*.pcx");
//var labPaths = Directory.GetFiles(@"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Chill", "*.lab", SearchOption.AllDirectories);
var imgPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Chill-Manor_DOS_EN\DATA\LEVELS\";
var palPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Chill-Manor_DOS_EN\DATA\RES\output\RESINT\game_scr.pcx";
var imPalPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\IM-Meen_DOS_EN\lab_output\RESINT\GAME_SCR.PCX";

// var imageImgPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Chill-Manor_DOS_EN\DATA\LEVELS\output\RES001\IMAGES.IMG";
// foreach (var labPath in labPaths)
// {
//   AniMagic.ExtractLab(labPath);
// }

var palBytes = File.ReadAllBytes(palPath).Skip(0x5379).Take(0x300).ToArray();
// File.WriteAllBytes(@"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Chill-Manor_DOS_EN\DATA\RES\output\RESINT\game_scr.bin", palBytes);
var imPalBytes = File.ReadAllBytes(imPalPath).Skip(0x4A09).Take(0x300).ToArray();
var palette = ColorHelper.ConvertBytesToRGB(palBytes, 1);

// ColorHelper.WritePalette(Path.ChangeExtension(imPalPath, ".png"), palette);

// //ExtractIMG(imageImgPath, palette, true, true);

// var imgFiles = Directory.GetFiles(imgPath, "*.img", SearchOption.AllDirectories)
//   .Where(f => !f.ToLower().Contains("image")).ToArray();

// var imageImgFiles = Directory.GetFiles(imgPath, "*.img", SearchOption.AllDirectories)
//   .Where(f => f.ToLower().Contains("image")).ToArray();

// foreach (var imgFile in imageImgFiles)
// {
//   AniMagic.ExtractIMG(imgFile, palette, true, true);
// }

// foreach (var imgFile in imgFiles)
// {
//   AniMagic.ExtractIMG(imgFile, palette, true, false);
// }

// var cmpPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Chill-Manor_DOS_EN\DATA\RES\output\RESFACE\amul01.cmp";
// var cmpData = File.ReadAllBytes(cmpPath).Skip(0x4).ToArray();

// var offsetList = new List<ushort>();
// var offsetData = cmpData.Skip(0x56).Take(0x32).ToArray();

// for (int i = 0; i < offsetData.Length; i += 2)
// {
//   var offset = BitConverter.ToUInt16(offsetData.Skip(i).Take(2).ToArray(), 0);
//   offsetList.Add(offset);
// }

// var imageDataLines = new List<byte[]>();

// for (int i = 0; i < offsetList.Count; i++)
// {
//   var start = offsetList[i];
//   var end = i == offsetList.Count - 1 ? cmpData.Length : offsetList[i + 1];
//   var line = cmpData.Skip(start).Take(end - start).ToArray();
//   imageDataLines.Add(line);
// }

// var imageLines = new List<byte[]>();

// foreach (var line in imageDataLines)
// {
//   var lineData = new byte[128];
//   var lineIndex = 0;
//   for (int i = 0; i < line.Length; i++)
//   {
//     if (lineIndex >= lineData.Length-1)
//     {
//       break;
//     }
//     var b = line[i];
//     if ((b & 0x80) == 0x80)
//     {
//       var count = b & 0x7F;
//       for (int j = 0; j < count && lineIndex < lineData.Length; j++)
//       {
//         lineData[lineIndex] = 0x0;
//         lineIndex++;
//       }
//     }
//     else
//     {
//       lineData[lineIndex] = b;
//       lineIndex++;
//     }
//   }
//   imageLines.Add(lineData);
// }

// var image = ImageFormatHelper.GenerateClutImage(palette, imageLines.SelectMany(l => l).ToArray(), 128, 64, true);
// image.Save(Path.ChangeExtension(cmpPath, ".png"), ImageFormat.Png);
// File.WriteAllBytes(Path.ChangeExtension(cmpPath, ".bin"), imageLines.SelectMany(l => l).ToArray());

// foreach (var pcxPath in pcxPaths)
// {
//   // check first three bytes are 0A 05 01
//   var pcxData = File.ReadAllBytes(pcxPath);
//   if (pcxData[0] != 0x0A || pcxData[1] != 0x05 || pcxData[2] != 0x01) continue;
//   var pcxOutputDirectory = Path.Combine(Path.GetDirectoryName(pcxPath), "output", Path.GetFileNameWithoutExtension(pcxPath));
//   Directory.CreateDirectory(pcxOutputDirectory);
//   var pngPath = Path.Combine(pcxOutputDirectory, Path.GetFileNameWithoutExtension(pcxPath) + ".png");
//   ConvertPcxToPng(pcxPath, pngPath);
// }

// foreach (var aniPath in aniPaths)
// {
//   var aniData = File.ReadAllBytes(aniPath);
//   // check that first 4 bytes are "ANI "
//   if (aniData[0] != 0x41 || aniData[1] != 0x4E || aniData[2] != 0x49 || aniData[3] != 0x20) continue;
//   Console.WriteLine($"Processing {aniPath}");
//   var aniOutputDirectory = Path.Combine(Path.GetDirectoryName(aniPath), "output", Path.GetFileNameWithoutExtension(aniPath));
//   Directory.CreateDirectory(aniOutputDirectory);
//   var version = BitConverter.ToUInt16(aniData.Skip(0x10).Take(2).ToArray(), 0);
//   // FPS -> 2 bytes @ 0x14
//   var fps = BitConverter.ToUInt16(aniData.Skip(0x14).Take(2).ToArray(), 0);
//   // audio sample rate -> 4 bytes @ 0x16
//   var audioSampleRate = BitConverter.ToUInt32(aniData.Skip(0x16).Take(4).ToArray(), 0);
//   // width -> 2 bytes @ 0x1A if v1, @ 0x1e if v2
//   var wOffset = version == 1 ? 0x1A : 0x1e;
//   var width = BitConverter.ToUInt16(aniData.Skip(wOffset).Take(2).ToArray(), 0);
//   // height -> 2 bytes @ 0x1C if v1, @ 0x20 if v2
//   var hOffset = version == 1 ? 0x1C : 0x20;
//   var height = BitConverter.ToUInt16(aniData.Skip(hOffset).Take(2).ToArray(), 0);
//   // total frames -> 4 bytes @ 0x1E if v1, @ 0x22 if v2
//   var totalFramesOffset = version == 1 ? 0x1E : 0x22;
//   var totalFrames = BitConverter.ToUInt32(aniData.Skip(totalFramesOffset).Take(4).ToArray(), 0);
//   // audio chunk size ->4 bytes @ 0x26 if v1, @ 0x2A if v2
//   var audioChunkSizeOffset = version == 1 ? 0x26 : 0x2A;
//   var audioChunkSize = BitConverter.ToUInt32(aniData.Skip(audioChunkSizeOffset).Take(4).ToArray(), 0);
//   // offset data length -> 4 bytes @ 0x2A if v1, @ 0x2E if v2
//   var offsetDataLengthOffset = version == 1 ? 0x2A : 0x2E;
//   var offsetDataLength = BitConverter.ToUInt32(aniData.Skip(offsetDataLengthOffset).Take(4).ToArray(), 0);

//   // offset data -> offsetDataLength bytes @ 0x2E if v1, @ 0x32 if v2
//   var offsetData = aniData.Skip(version == 1 ? 0x2E : 0x32).Take((int)offsetDataLength).ToArray();

//   var offsets = new List<int>();

//   for (int i = 0; i < offsetData.Length; i += 4)
//   {
//     var offset = BitConverter.ToInt32(offsetData.Skip(i).Take(4).ToArray(), 0);
//     offsets.Add(offset);
//   }

//   var paletteOffset = (version == 1 ? 0x2E : 0x32) + offsetDataLength + 0x18;
//   var paletteLength = 4 * BitConverter.ToUInt16(aniData.Skip((int)paletteOffset - 2).Take(2).ToArray(), 0);
//   var paletteData = aniData.Skip((int)paletteOffset).Take(paletteLength).ToArray();
//   var palette = ColorHelper.ConvertBytesToARGB(paletteData, 1);
//   var bodyOffset = paletteOffset + paletteLength;
//   var bodyData = aniData.Skip((int)bodyOffset).ToArray();

//   var imageCount = 0;
//   var audioBytes = new List<byte>();
//   foreach (var offset in offsets)
//   {
//     var imageDataLength = BitConverter.ToUInt32(bodyData.Skip(offset).Take(4).ToArray(), 0);
//     var imageData = bodyData.Skip(offset + 4).Take((int)imageDataLength).ToArray();
//     var image = ImageFormatHelper.GenerateRle7Image(palette, imageData, width, height, true);
//     image.Save(Path.Combine(aniOutputDirectory, $"{imageCount++}.png"), ImageFormat.Png);
//     var audioDataLength = BitConverter.ToUInt32(bodyData.Skip((int)(offset + 4 + imageDataLength)).Take(4).ToArray(), 0);
//     var audioData = bodyData.Skip((int)(offset + 8 + imageDataLength)).Take((int)audioDataLength).ToArray();
//     audioBytes.AddRange(audioData);
//   }

//   var audioFile = Path.Combine(aniOutputDirectory, "audio.wav");
//   var audio = audioBytes.ToArray();
//   AudioHelper.ConvertPcmToWav(audio, audioFile, (int)audioSampleRate, 1, 8);

// }

// var palFile = @"C:\bassPals\BassPal_Death.bin";
// var palData = File.ReadAllBytes(palFile);
// var palette = ColorHelper.ConvertBytesToRGB(palData, 1);
// var FosterSprites = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\BASS\output\180.bin";
// var FosterSpritesData = File.ReadAllBytes(FosterSprites);

// // loop through the data, and extract each sprite (32x56)
// var spriteWidth = 16;
// var spriteHeight = 8;
// var spriteCount = FosterSpritesData.Length / (spriteWidth * spriteHeight);

// var outputFolder = Path.Combine(Path.GetDirectoryName(FosterSprites), "output", "Tiles_180");
// Directory.CreateDirectory(outputFolder);

// for (int i = 0; i < spriteCount; i++)
// {
//   var spriteData = FosterSpritesData.Skip(i * spriteWidth * spriteHeight).Take(spriteWidth * spriteHeight).ToArray();
//   var sprite = ImageFormatHelper.GenerateClutImage(palette, spriteData, spriteWidth, spriteHeight);
//   sprite.Save(Path.Combine(outputFolder, $"{i}.png"), ImageFormat.Png);
// }

// var main = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Chill-Manor_DOS_EN\DATA\LEVELS\output";

// // get all folders in the main directory
// var folders = Directory.GetDirectories(main);

// foreach (var folder in folders)
// {
//   var folderName = Path.GetFileName(folder);
//   // append \img_output\FLOOR to the folder
//   var imgFolder = Path.Combine(folder, "img_output", "SKY");
//   // Get all files, and rename so they are prefixed with the folder name,
//   // ie, if folder is RES001, then all files will be prefixed with RES001_
//   var files = Directory.GetFiles(imgFolder);
//   foreach (var file in files)
//   {
//     var newFile = Path.Combine(imgFolder, $"{folderName}_{Path.GetFileName(file)}");
//     File.Move(file, newFile);
//   }
// }

// var testPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Wayne-s-World_DOS_EN_ISO_CD\WW\";
// var gxlFiles = Directory.GetFiles(testPath, "*.gxl");

// foreach (var gxlFile in gxlFiles)
// {
//   var testOutputPath = Path.Combine(testPath, "output", Path.GetFileNameWithoutExtension(gxlFile));
//   Directory.CreateDirectory(testOutputPath);
//   ExtractGLX(gxlFile, testOutputPath);

//   var pcxFiles = Directory.GetFiles(testOutputPath, "*.pcx", SearchOption.AllDirectories);

//   foreach (var pcxFile in pcxFiles)
//   {
//     var pcxOutputDirectory = Path.Combine(testOutputPath, "Parsed");
//     Directory.CreateDirectory(pcxOutputDirectory);
//     var pngPath = Path.ChangeExtension(pcxFile, ".png");
//     var pcxData = File.ReadAllBytes(pcxFile);
//     var xMin = BitConverter.ToUInt16(pcxData.Skip(4).Take(2).ToArray(), 0);
//     var yMin = BitConverter.ToUInt16(pcxData.Skip(6).Take(2).ToArray(), 0);
//     var xMax = BitConverter.ToUInt16(pcxData.Skip(8).Take(2).ToArray(), 0);
//     var yMax = BitConverter.ToUInt16(pcxData.Skip(10).Take(2).ToArray(), 0);
//     var width = xMax - xMin + 1;
//     var adjusted = false;
//     if (width % 2 != 0)
//     {
//       width++;
//       adjusted = true;
//     }
//     var height = yMax - yMin + 1;
//     var paletteData = pcxData.Skip(pcxData.Length - 0x300).Take(0x300).ToArray();
//     var pcxPalette = ColorHelper.ConvertBytesToRGB(paletteData, 1);
//     var imageData = pcxData.Skip(128).Take(pcxData.Length - 0x301 - 128).ToArray();
//     File.WriteAllBytes(Path.ChangeExtension(pcxFile, "_uc.bin"), imageData);
//     imageData = DecompressPCX(imageData);
//     File.WriteAllBytes(Path.ChangeExtension(pcxFile, ".bin"), imageData);
//     File.WriteAllBytes(Path.ChangeExtension(pcxFile, ".pal"), paletteData);
//     var image = ImageFormatHelper.GenerateClutImage(pcxPalette, imageData, width, height, true);
//     if (adjusted)
//     {
//       // trim one pixel from the right using SixLabors.ImageSharp
//       var cropRect = new Rectangle(0, 0, width - 1, height);
//       image = image.Clone(cropRect, image.PixelFormat);
//     }
//     image.Save(Path.Combine(pcxOutputDirectory, "TP_" + Path.GetFileNameWithoutExtension(pcxFile).Replace(" ", "") + ".png"), ImageFormat.Png);
//     image = ImageFormatHelper.GenerateClutImage(pcxPalette, imageData, width, height, false);
//     if (adjusted)
//     {
//       // trim one pixel from the right using SixLabors.ImageSharp
//       var cropRect = new Rectangle(0, 0, width - 1, height);
//       image = image.Clone(cropRect, image.PixelFormat);
//     }
//     image.Save(Path.Combine(pcxOutputDirectory, Path.GetFileNameWithoutExtension(pcxFile).Replace(" ", "") + ".png"), ImageFormat.Png);
//   }
// }




// static void ExtractGLX(string inputFilePath, string outputDirectory)
// {
//   using (BinaryReader reader = new BinaryReader(File.Open(inputFilePath, FileMode.Open)))
//   {
//     // Read GX Library header
//     GXHeader header = new GXHeader
//     {
//       Id = reader.ReadUInt16(),
//       Copyright = Encoding.ASCII.GetString(reader.ReadBytes(50)).TrimEnd('\0'),
//       Version = reader.ReadUInt16(),
//       Label = Encoding.ASCII.GetString(reader.ReadBytes(40)).TrimEnd('\0'),
//       Entries = reader.ReadUInt16(),
//       Reserved = reader.ReadBytes(32)
//     };

//     if (header.Id != 0xCA01)
//     {
//       Console.WriteLine("Invalid GX Library ID.");
//       return;
//     }

//     // Read image entries
//     GXImageEntry[] entries = new GXImageEntry[header.Entries];
//     for (int i = 0; i < header.Entries; i++)
//     {
//       entries[i] = new GXImageEntry
//       {
//         PackingType = reader.ReadByte(),
//         Name = Encoding.ASCII.GetString(reader.ReadBytes(13)).TrimEnd('\0'),
//         Offset = (int)reader.ReadUInt32(),
//         Size = (int)reader.ReadUInt32(),
//         Date = reader.ReadUInt16(),
//         Time = reader.ReadUInt16()
//       };
//     }

//     // Extract each file
//     foreach (GXImageEntry entry in entries)
//     {
//       reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
//       byte[] fileData = reader.ReadBytes(entry.Size);

//       string outputFilePath = Path.Combine(outputDirectory, entry.Name.TrimEnd(' '));
//       Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));
//       File.WriteAllBytes(outputFilePath, fileData);

//       Console.WriteLine($"Extracted: {entry.Name}");
//     }
//   }
// }

// public class GXHeader
// {
//   public ushort Id { get; set; }
//   public string Copyright { get; set; }
//   public ushort Version { get; set; }
//   public string Label { get; set; }
//   public ushort Entries { get; set; }
//   public byte[] Reserved { get; set; }
// }

// // GX Image Entry Structure
// public class GXImageEntry
// {
//   public byte PackingType { get; set; }
//   public string Name { get; set; }
//   public int Offset { get; set; }
//   public int Size { get; set; }
//   public ushort Date { get; set; }
//   public ushort Time { get; set; }
// }


var filesInPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\The-Blues-Brothers_DOS_EN\blues-brothers\";

// var filesIn = Directory.GetFiles(filesInPath, "*.ck*");

// foreach (var fileIn in filesIn)
// {
//   var fileOut = Path.ChangeExtension(fileIn, "_out.bin");

//   if (File.Exists(fileOut))
//   {
//     fileOut = Path.ChangeExtension(fileIn, "_out1.bin");
//   }

//   DecompressTitus(fileIn, fileOut);

//   ImageFormatHelper.ConvertILBMToPNG(fileOut, Path.ChangeExtension(fileOut, ".png"), true);

// }

// var sqzFiles = Directory.GetFiles(filesInPath, "*.sqz");

// foreach (var sqzFile in sqzFiles)
// {
//   var outPath = Path.ChangeExtension(sqzFile, "_sqz.bin");
//   DecompressTitus(sqzFile, outPath);
// }

// var sqvFiles = Directory.GetFiles(filesInPath, "*.sqv");

// foreach (var sqvFile in sqvFiles)
// {
//   var sqvBin = Path.ChangeExtension(sqvFile, ".bin");
//   DecompressTitus(sqvFile, sqvBin);
//   var decodedImages = DecodePlanarEGA(sqvBin,4);
//   var binaryOutputFolder = Path.Combine(filesInPath, "output", Path.GetFileNameWithoutExtension(sqvFile));
//   Directory.CreateDirectory(binaryOutputFolder);
//   for (int i = 0; i < decodedImages.Length; i++)
//   {
//     var bytes = decodedImages[i];
//     File.WriteAllBytes(Path.Combine(binaryOutputFolder, $"{i}.bin"), bytes);
//   }
// }

// static void DecompressTitus(string fileIn, string fileOut)
// {
//   int unknown;
//   int decompressedSizeInteger;
//   long decompressedSize;
//   int huffmanTreeSize;
//   int node;
//   int bitPosition = 7;
//   int bit;
//   long i = 0;

//   byte byteIn =0;
//   byte byteOut;

//   using (BinaryReader reader = new BinaryReader(File.Open(fileIn, FileMode.Open)))
//   using (BinaryWriter writer = new BinaryWriter(File.Open(fileOut, FileMode.Create)))
//   {
//     // Read header
//     unknown = reader.ReadUInt16(); // always zero?
//     decompressedSizeInteger = reader.ReadUInt16();
//     decompressedSize = (uint)decompressedSizeInteger; // Convert to unsigned long
//     huffmanTreeSize = reader.ReadUInt16();

//     // Read Huffman tree
//     int[] huffmanTree = new int[huffmanTreeSize / 2];
//     for (int j = 0; j < huffmanTreeSize / 2; j++)
//     {
//       huffmanTree[j] = reader.ReadInt16(); // 16-bit signed integers
//     }

//     // Decompress data
//     node = 0;
//     while (i < decompressedSize)
//     {
//       if (bitPosition == 7)
//       {
//         byteIn = reader.ReadByte();
//       }

//       bit = (byteIn >> bitPosition) & 1;
//       bitPosition--;

//       if (bitPosition < 0)
//       {
//         bitPosition = 7;
//       }

//       node += bit;
//       if ((huffmanTree[node] & 0x8000) != 0)
//       {
//         // Leaf node
//         byteOut = (byte)(huffmanTree[node] & 0xFF);
//         writer.Write(byteOut);
//         i++;
//         node = 0; // Reset to the root of the tree
//       }
//       else
//       {
//         // Non-leaf node
//         node = huffmanTree[node] / 2;
//       }
//     }
//   }
// }

// static byte[][] DecodePlanarEGA(string inputFile, int Planes)
// {
//   using (BinaryReader reader = new BinaryReader(File.Open(inputFile, FileMode.Open)))
//   {
//     // Read the number of images
//     int imageCount = reader.ReadUInt16();

//     // Create an array to hold all the decoded images
//     byte[][] decodedImages = new byte[imageCount][];

//     // Iterate over each image in the file
//     for (int imageIndex = 0; imageIndex < imageCount; imageIndex++)
//     {
//       // Read the image dimensions (2 bytes for height, 2 bytes for width)
//       int height = reader.ReadUInt16();
//       int width = reader.ReadUInt16();

//       // Calculate the size for each image's planar data
//       int bytesPerPlane = (width * height) / 8; // Each plane contains bytes for 8 pixels per row

//       byte[,] pixels = new byte[height, width];  // Temporary 2D array for each image
//       byte[] colorIndices = new byte[width * height];  // The output byte array for this image

//       // Read and decode the image's planar data
//       for (int plane = 0; plane < Planes; plane++)
//       {
//         for (int y = 0; y < height; y++)
//         {
//           for (int x = 0; x < width; x += 8)
//           {
//             // Read the byte that contains the next 8 pixels for this row in this plane
//             int byteIndex = (int)reader.BaseStream.Position;
//             byte planeByte = reader.ReadByte();

//             // Process each bit in the byte (corresponding to 8 pixels horizontally)
//             for (int bit = 0; bit < 8; bit++)
//             {
//               // Extract the bit for this pixel
//               int bitValue = (planeByte >> (7 - bit)) & 1;

//               // Shift the bit into the correct position for this plane and add to the pixel value
//               pixels[y, x + bit] |= (byte)(bitValue << plane);
//             }
//           }
//         }
//       }

//       // Flatten the 2D pixel array into a 1D byte array of color indices
//       for (int y = 0; y < height; y++)
//       {
//         for (int x = 0; x < width; x++)
//         {
//           colorIndices[y * width + x] = pixels[y, x]; // Store each pixel's color index
//         }
//       }

//       // Store the decoded color indices for this image
//       decodedImages[imageIndex] = colorIndices;
//     }

//     return decodedImages;
//   }
// }

// var testOutputPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Jack-the-Ripper_DOS_EN\ANIM";

// var pcxFiles = Directory.GetFiles(testOutputPath, "*.pcx", SearchOption.AllDirectories);

// foreach (var pcxFile in pcxFiles)
// {
//   var pcxOutputDirectory = Path.Combine(testOutputPath, "Parsed");
//   Directory.CreateDirectory(pcxOutputDirectory);
//   var pngPath = Path.ChangeExtension(pcxFile, ".png");
//   var pcxData = File.ReadAllBytes(pcxFile);
//   var xMin = BitConverter.ToUInt16(pcxData.Skip(4).Take(2).ToArray(), 0);
//   var yMin = BitConverter.ToUInt16(pcxData.Skip(6).Take(2).ToArray(), 0);
//   var xMax = BitConverter.ToUInt16(pcxData.Skip(8).Take(2).ToArray(), 0);
//   var yMax = BitConverter.ToUInt16(pcxData.Skip(10).Take(2).ToArray(), 0);
//   var width = xMax - xMin + 1;
//   var height = yMax - yMin + 1;
//   var paletteData = pcxData.Skip(pcxData.Length - 0x300).Take(0x300).ToArray();
//   var pcxPalette = ColorHelper.ConvertBytesToRGB(paletteData, 1);
//   var imageData = pcxData.Skip(128).Take(pcxData.Length - 0x301 - 128).ToArray();
//   File.WriteAllBytes(Path.ChangeExtension(pcxFile, "_uc.bin"), imageData);
//   imageData = DecompressPCX2(imageData, width, height);
//   File.WriteAllBytes(Path.ChangeExtension(pcxFile, ".bin"), imageData);
//   File.WriteAllBytes(Path.ChangeExtension(pcxFile, ".pal"), paletteData);
//   var image = ImageFormatHelper.GenerateClutImage(pcxPalette, imageData, width, height, true);
//   image.Save(Path.Combine(pcxOutputDirectory, "TP_" + Path.GetFileNameWithoutExtension(pcxFile) + ".png"), ImageFormat.Png);
// }

// static byte[] DecompressPCX2(byte[] compressedData, int width, int height)
// {
//   List<byte> decompressedData = new List<byte>();

//   int index = 0;
//   for (int y = 0; y < height; y++)
//   {
//     int bytesInScanline = 0;
//     while (bytesInScanline < width)
//     {
//       byte currentByte = compressedData[index++];

//       if ((currentByte & 0xC0) == 0xC0) // Check if the two highest bits are set
//       {
//         // If the byte is >= 192 (0xC0), it's a repeat code
//         int count = currentByte & 0x3F; // Get the number of repetitions (lower 6 bits)
//         if (index >= compressedData.Length) throw new Exception("Invalid RLE compressed data.");

//         byte value = compressedData[index++]; // The next byte is the value to repeat
//         for (int i = 0; i < count && bytesInScanline < width; i++)
//         {
//           decompressedData.Add(value);
//           bytesInScanline++;
//         }
//       }
//       else
//       {
//         // If the byte is less than 192, it's a literal value (just copy it as is)
//         decompressedData.Add(currentByte);
//         bytesInScanline++;
//       }
//     }

//     // If the width is odd, there may be a padding byte to skip
//     if (width % 2 != 0 && index < compressedData.Length)
//     {
//       index++; // Skip the padding byte
//     }
//   }

//   return decompressedData.ToArray();
// }


// static byte[] DecompressPCX(byte[] compressedData)
// {
//   List<byte> decompressedData = new List<byte>();

//   int index = 0;
//   while (index < compressedData.Length)
//   {
//     byte currentByte = compressedData[index++];

//     if ((currentByte & 0xC0) == 0xC0) // Check if the two highest bits are set
//     {
//       // If the byte is >= 192 (0xC0), it's a repeat code
//       int count = currentByte & 0x3F; // Get the number of repetitions (lower 6 bits)
//       if (index >= compressedData.Length) throw new Exception("Invalid RLE compressed data.");

//       byte value = compressedData[index++]; // The next byte is the value to repeat
//       for (int i = 0; i < count; i++)
//       {
//         decompressedData.Add(value);
//       }
//     }
//     else
//     {
//       // If the byte is less than 192, it's a literal value (just copy it as is)
//       decompressedData.Add(currentByte);
//     }
//   }

//   return decompressedData.ToArray();
// }

// var pacPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Fury of the Furries (1993) (Puzzle Platform) (DOS)\Fury\";

// var lbmFiles = Directory.GetFiles(pacPath, "*.lbm", SearchOption.AllDirectories);

// foreach (var lbmFile in lbmFiles)
// {
//   var pngPath = Path.ChangeExtension(lbmFile, ".png");
//   ImageFormatHelper.ConvertILBMToPNG(lbmFile, pngPath, false);
//   var transparentFolder = Path.Combine(Path.GetDirectoryName(lbmFile), "Transparent");
//   Directory.CreateDirectory(transparentFolder);
//   var transparentPngPath = Path.Combine(transparentFolder, Path.GetFileName(pngPath));
//   ImageFormatHelper.ConvertILBMToPNG(lbmFile, transparentPngPath, true);
// }

// var chpFiles = Directory.GetFiles(pacPath, "*.chp", SearchOption.AllDirectories);
// var palFile = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Space-Hulk_DOS_EN_ISO-Version\Space_Hulk_ISO\HULK\LBM_DIR\PAL_A.LBM";
// var pal = File.ReadAllBytes(palFile).Skip(0x30).Take(0x300).ToArray();
// var chpPalette = ColorHelper.ConvertBytesToRGB(pal, 1);

// foreach(var chpFile in chpFiles)
// {
//   var outputDirectory = Path.Combine(Path.GetDirectoryName(chpFile), "output", Path.GetFileNameWithoutExtension(chpFile));
//   Directory.CreateDirectory(outputDirectory);
//   var imageCount = 0;
//   using (var reader = new BinaryReader(File.Open(chpFile, FileMode.Open)))
//   {
//     while (reader.BaseStream.Position < reader.BaseStream.Length)
//     {
//       var width = reader.ReadInt16();
//       var height = reader.ReadInt16();
//       var length = reader.ReadInt16();
//       var imageData = length == 0 ? reader.ReadBytes((int)(reader.BaseStream.Length-reader.BaseStream.Position)) : reader.ReadBytes(length - 6);
//       var image = ImageFormatHelper.GenerateClutImage(chpPalette, imageData, width, height, true);
//       image.Save(Path.Combine(outputDirectory, $"{imageCount++}.png"), ImageFormat.Png);
//     }
//   }
// }


var sjDat = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Space-Jam_DOS_EN\SJ.DAT";

var sjDatOutput = Path.Combine(Path.GetDirectoryName(sjDat), "output");
Directory.CreateDirectory(sjDatOutput);

// sj.dat contains a series of ilbm files, one after the other
using (var reader = new BinaryReader(File.Open(sjDat, FileMode.Open)))
{
  var imageCount = 0;
  while (reader.BaseStream.Position < reader.BaseStream.Length)
  {
    // skip FORM, and read the size of the chunk
    reader.ReadBytes(4);
    var chunkSize = reader.ReadBigEndianUInt32();
    reader.BaseStream.Position -= 8;
    var chunkData = reader.ReadBytes((int)chunkSize+8);
    var outPath = Path.Combine(sjDatOutput, $"{imageCount++}.lbm");
    File.WriteAllBytes(outPath, chunkData);
    var pngPath = Path.ChangeExtension(outPath, ".png");
    ImageFormatHelper.ConvertILBMToPNG(outPath, pngPath, false);
    File.Delete(outPath);
  }
}
