using System.Diagnostics;
using System.Text;
using ExtractCLUT;
using ExtractCLUT.Games.Generic;
using ExtractCLUT.Games.PC.Delphine;
using ExtractCLUT.Games.PC.TSage;
using ExtractCLUT.Helpers;
using ExtractCLUT.Games.PC.AniMagic;
using System.Drawing;
using ExtractCLUT.Games.Sega.Saturn;
using ExtractCLUT.Games.PC.TLJ;
using ExtractCLUT.Games.PC;
using ExtractCLUT.Games.PC.HopkinsFBI;
using ExtractCLUT.Games.PC.Eradicator;
using ExtractCLUT.Games;
using ExtractCLUT.Games.PC.Anvil;
using ExtractCLUT.Games.PC.Interspective;
using ExtractCLUT.Games.PC.Prince;
using ExtractCLUT.Games.PC.JackOrlando;
using ExtractCLUT.Games.Generic.ScummVM.Decompression;
using OGLibCDi.Models;
using Rectangle = System.Drawing.Rectangle;
using SixLabors.ImageSharp;
using ExtractCLUT.Games.PC.Mario.TMD;
using SixLabors.ImageSharp.PixelFormats;
using System.Drawing.Imaging;
using ExtractCLUT.Games.PC.Bubsy;
using Color = SixLabors.ImageSharp.Color;
using ExtractCLUT.Games.PC.MADE;
using ExtractCLUT.Games.PC.AlienCabal;
using ExtractCLUT.Games.PC.AlienTrilogy;
using static ExtractCLUT.Games.PC.IconRLEDecompressor;
using ExtractCLUT.Games.PC.Cryo;
using Image = SixLabors.ImageSharp.Image;
using SixLabors.ImageSharp.Processing;
using Point = SixLabors.ImageSharp.Point;



var pcDir = @"C:\Dev\Gaming\PC\Dos\Games\PerfectAssassin\DATA";
var pcFiles = Directory.GetFiles(pcDir, "*.PC", SearchOption.TopDirectoryOnly);

foreach (var pcFile in pcFiles)
{
		try
		{
				ParsePCFile(pcFile);
		}
		catch (Exception ex)
		{
				Console.WriteLine($"An error occurred during PC processing of {pcFile}: {ex.Message}");
		}
}

// var bbDir = @"C:\Dev\Gaming\PC\Dos\DiscImages\Brudal-Baddle_DOS_EN";
// var bbFiles = Directory.GetFiles(bbDir, "*.SPR", SearchOption.AllDirectories);
// foreach (var bbFile in bbFiles)
//     ParseBBSpriteFile(bbFile);

// void ParseBBSpriteFile(string bbspriteFile)
// {
//     using var bbspriteReader = new BinaryReader(File.OpenRead(bbspriteFile));
//     var imageIndex = 0;
//     var outputFolder = Path.Combine(Path.GetDirectoryName(bbspriteFile)!, Path.GetFileNameWithoutExtension(bbspriteFile) + "_output");
//     Directory.CreateDirectory(outputFolder);
//     // Read and process the BB sprite file
//     var colourCount = bbspriteReader.ReadUInt16();
//     var paletteData = bbspriteReader.ReadBytes(colourCount * 3);
//     var palette = ColorHelper.ConvertBytesToRgbIS(paletteData, true);
//     bbspriteReader.ReadBytes(2); // skip reserved bytes
//     var check = bbspriteReader.ReadUInt16();
//     while (check != 0x01)
//         check = bbspriteReader.ReadUInt16(); // skip extra reserved bytes if needed
//     while(check == 1 && bbspriteReader.BaseStream.Position < bbspriteReader.BaseStream.Length)
//     {
//         var width = bbspriteReader.ReadUInt16()-1;
//         var height = bbspriteReader.ReadUInt16() - 1;
//         var data = bbspriteReader.ReadBytes(width * height);
//         var image = ImageFormatHelper.GenerateIMClutImage(palette, data, (int)width, (int)height, true);
//         // flip vertically
//         image.Mutate(x => x.Flip(FlipMode.Vertical));
//         var outputFile = Path.Combine(outputFolder, $"{imageIndex++}.png");
//         image.SaveAsPng(outputFile);
//         if (bbspriteReader.BaseStream.Position >= bbspriteReader.BaseStream.Length)
//             break;
//         check = bbspriteReader.ReadUInt16();
//         while (check != 0x01 && bbspriteReader.BaseStream.Position < bbspriteReader.BaseStream.Length)
//             check = bbspriteReader.ReadUInt16();
//     }
// }


// var frameDir = @"C:\Dev\Gaming\PC\Dos\Games\RequiresInvestigation\Aliens-A-Comic-Book-Adventure_DOS_EN\Aliens CD 1-2\ALIENS\DISK1\COMBAT\SPRITES";

// var fraFiles = Directory.GetFiles(frameDir, "*.FRA", SearchOption.AllDirectories);

// foreach (var fraFile in fraFiles)
// {
// 	try
// 	{
// 		ParseFraFile(fraFile);
// 		var folderToAlign = Path.Combine(Path.GetDirectoryName(fraFile)!, Path.GetFileNameWithoutExtension(fraFile) + "_output");
// 		var alignedOutputFolder = Path.Combine(Path.GetDirectoryName(fraFile)!, Path.GetFileNameWithoutExtension(fraFile) + "_aligned_output");
// 		AlignSprites(folderToAlign, alignedOutputFolder);

// 	}
// 	catch (Exception ex)
// 	{
// 		Console.WriteLine($"An error occurred during FRA processing of {fraFile}: {ex.Message}");
// 	}
// }

// var pakFiles = Directory.GetFiles(pakDir, "*.PAK", SearchOption.AllDirectories);
// foreach (var pakFile in pakFiles)
// {
//     try
//     {
//         ParsePakFile(pakFile);
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"An error occurred during PAK processing of {pakFile}: {ex.Message}");
//     }
// }

// void ParseFraFile(string fraFile)
// {
// 	var outputFolder = Path.Combine(Path.GetDirectoryName(fraFile)!, Path.GetFileNameWithoutExtension(fraFile) + "_output");
// 	Directory.CreateDirectory(outputFolder);
// 	using var fraReader = new BinaryReader(File.OpenRead(fraFile));
// 	// skip 8 byte magic
// 	fraReader.BaseStream.Seek(8, SeekOrigin.Begin);
// 	var spriteCount = fraReader.ReadByte();
// 	var paletteData = fraReader.ReadBytes(17 * 3);
// 	var palette = ColorHelper.ConvertBytesToRgbIS(paletteData, false);

// 	for (int i = 0; i < spriteCount; i++)
// 	{
// 		var index = fraReader.ReadUInt32();
// 		fraReader.ReadUInt32(); // skip
// 		var compressedSize = fraReader.ReadUInt32();
// 		var decompressedSize = fraReader.ReadUInt32();
// 		var width = fraReader.ReadUInt16();
// 		var height = fraReader.ReadUInt16();
// 		var unk1 = fraReader.ReadInt32();
// 		var unk2 = fraReader.ReadInt32();
// 		var unk3 = fraReader.ReadInt32();
// 		var unk4 = fraReader.ReadInt32();
// 		var compressedData = fraReader.ReadBytes((int)compressedSize);
// 		using var compDataReader = new BinaryReader(new MemoryStream(compressedData));
// 		var decompressedData = HLZDecoder.DecodeFrameInPlace(compDataReader, uint.MaxValue, (int)decompressedSize);
// 		var image = ImageFormatHelper.GenerateIMClutImage(palette, decompressedData, (int)width, (int)height, true, 0x10, false);
// 		var outputPng = Path.Combine(outputFolder, $"{index}_{unk1}x{unk2}_{unk3}x{unk4}.png");
// 		image.SaveAsPng(outputPng);
// 	}
// }

// void ParsePakFile(string pakFile)
// {
// 	using var hlzReader = new BinaryReader(File.OpenRead(pakFile));
// 	var binaryOutputFolder = Path.Combine(Path.GetDirectoryName(pakFile)!, "binary_output");
// 	Directory.CreateDirectory(binaryOutputFolder);
// 	hlzReader.BaseStream.Seek(0x8, SeekOrigin.Begin);
// 	var width = hlzReader.ReadUInt32();
// 	var height = hlzReader.ReadUInt32();

// 	var paletteData = hlzReader.ReadBytes(256 * 3);
// 	var palette = ColorHelper.ConvertBytesToRgbIS(paletteData, false);

// 	var decompressedData = HLZDecoder.DecodeFrameInPlace(hlzReader, uint.MaxValue, (int)(width * height));
// 	//File.WriteAllBytes(Path.Combine(binaryOutputFolder, Path.GetFileNameWithoutExtension(pakFile) + ".raw"), decompressedData);
// 	decompressedData = decompressedData.Skip(0x6).ToArray();
// 	// get the last byte and append 6 copies
// 	var lastByte = decompressedData.Last();
// 	decompressedData = decompressedData.Concat(Enumerable.Repeat(lastByte, 6)).ToArray();
// 	var image = ImageFormatHelper.GenerateIMClutImage(palette, decompressedData, (int)width, (int)height);
// 	var outputPng = Path.ChangeExtension(pakFile, ".png");
// 	image.SaveAsPng(outputPng);
// }

// /// <summary>
// /// Processes a directory of sprites and saves new, aligned versions
// /// to an output directory.
// /// </summary>
// /// <param name="inputDirectory">Path to the folder with original sprites.</param>
// /// <param name="outputDirectory">Path to save the new aligned sprites.</param>
// void AlignSprites(string inputDirectory, string outputDirectory)
// {
// 	var frameInfos = new List<FrameInfo>();

// 	// 1. Parse all filenames AND load images (we need height)
// 	Console.WriteLine("Parsing filenames and loading sprites...");
// 	foreach (var file in Directory.GetFiles(inputDirectory, "*.png"))
// 	{
// 		try
// 		{
// 			string fileName = Path.GetFileNameWithoutExtension(file);
// 			string[] parts = fileName.Split('_');
// 			if (parts.Length != 3) continue;

// 			string[] anchorParts = parts[1].Split('x');
// 			string[] drawParts = parts[2].Split('x');

// 			// Load the image *first* to get its height
// 			using (var tempSprite = Image.Load<Rgba32>(file)) // <-- CHANGED
// 			{
// 				var info = new FrameInfo
// 				{
// 					FilePath = file,
// 					Index = int.Parse(parts[0]),
// 					AnchorX = int.Parse(anchorParts[0]),
// 					AnchorY = int.Parse(anchorParts[1]),
// 					DrawX_TopLeft = int.Parse(drawParts[0]), // <-- CHANGED
// 					DrawY_Bottom = int.Parse(drawParts[1]),  // <-- CHANGED
// 					Sprite = tempSprite.Clone() // Keep a copy in memory // <-- CHANGED
// 				};
// 				frameInfos.Add(info);
// 			}
// 		}
// 		catch (Exception ex)
// 		{
// 			Console.WriteLine($"Could not parse/load file: {file}. Error: {ex.Message}");
// 		}
// 	}

// 	if (!frameInfos.Any())
// 	{
// 		Console.WriteLine("No valid sprite files found.");
// 		return;
// 	}

// 	// 2. Find the master bounding box (This logic is unchanged)
// 	Console.WriteLine("Calculating master bounding box...");
// 	int minX = int.MaxValue;
// 	int minY = int.MaxValue;
// 	int maxX = int.MinValue;
// 	int maxY = int.MinValue;

// 	foreach (var info in frameInfos)
// 	{
// 		// Now OffsetY is calculated as (DrawY_Bottom - Sprite.Height) - AnchorY
// 		// which, given your data, is just -Sprite.Height

// 		minX = Math.Min(minX, info.OffsetX);
// 		minY = Math.Min(minY, info.OffsetY); // <-- This will now be a negative number
// 		maxX = Math.Max(maxX, info.OffsetX + info.Sprite.Width);
// 		maxY = Math.Max(maxY, info.OffsetY + info.Sprite.Height); // <-- This will now be 0
// 	}

// 	/* * Let's trace the Y-axis with this new logic:
// 	 * Frame 1 (H=100): OffsetY = -100.
// 	 * Frame 4 (H=70):  OffsetY = -70.
// 	 *
// 	 * minY = min(-100, -70) = -100
// 	 * maxY = max(-100 + 100, -70 + 70) = max(0, 0) = 0
// 	 */

// 	// 3. Calculate new canvas size and anchor (This logic is unchanged)
// 	int canvasWidth = maxX - minX;
// 	int canvasHeight = maxY - minY; // <-- canvasHeight = 0 - (-100) = 100

// 	int newAnchorX = -minX;
// 	int newAnchorY = -minY; // <-- newAnchorY = -(-100) = 100

// 	Console.WriteLine($"New canvas size: {canvasWidth}x{canvasHeight}");
// 	Console.WriteLine($"New anchor (relative to top-left): ({newAnchorX}, {newAnchorY})");

// 	// 4. Create and save new images (This logic is unchanged)
// 	if (!Directory.Exists(outputDirectory))
// 	{
// 		Directory.CreateDirectory(outputDirectory);
// 	}

// 	Console.WriteLine("Generating new aligned sprites...");
// 	foreach (var info in frameInfos.OrderBy(f => f.Index))
// 	{
// 		using (var newFrame = new Image<Rgba32>(canvasWidth, canvasHeight))
// 		{
// 			// Let's trace newDrawY = newAnchorY + info.OffsetY
// 			// Frame 1 (H=100): newDrawY = 100 + (-100) = 0
// 			// Frame 4 (H=70):  newDrawY = 100 + (-70)  = 30

// 			// This is correct! Frame 1 (tall) is drawn at Y=0.
// 			// Frame 4 (short) is drawn at Y=30, aligning its bottom edge.

// 			int newDrawX = newAnchorX + info.OffsetX;
// 			int newDrawY = newAnchorY + info.OffsetY;

// 			newFrame.Mutate(ctx =>
// 					ctx.DrawImage(info.Sprite, new Point(newDrawX, newDrawY), 1f)
// 			);

// 			string newFileName = $"aligned_{info.Index:D3}.png";
// 			string outputPath = Path.Combine(outputDirectory, newFileName);
// 			newFrame.Save(outputPath);
// 		}

// 		info.Sprite.Dispose();
// 	}

// 	Console.WriteLine($"✅ Successfully aligned {frameInfos.Count} sprites in '{outputDirectory}'.");
// }

// class FrameInfo
// {
// 	public string FilePath { get; set; }
// 	public int Index { get; set; }
// 	public int AnchorX { get; set; }     // xPos1
// 	public int AnchorY { get; set; }     // yPos1
// 	public int DrawX_TopLeft { get; set; } // xPos2
// 	public int DrawY_Bottom { get; set; }  // yPos2  <-- CHANGED
// 	public Image<Rgba32> Sprite { get; set; }

// 	// We must calculate the real TopLeft Y draw position
// 	public int DrawY_TopLeft => DrawY_Bottom - Sprite.Height; // <-- CHANGED

// 	// The sprite's top-left corner relative to the anchor
// 	public int OffsetX => DrawX_TopLeft - AnchorX;
// 	public int OffsetY => DrawY_TopLeft - AnchorY; // <-- This now calculates correctly!
// }


// var icon2dData = File.ReadAllBytes(inputPath);

// var (rgb565Data, width565, height565) = IconRLEDecompressor.DecompressToRGB24(icon2dData, useRGB555: false);
// string output565 = Path.ChangeExtension(inputPath, "_rgb565.raw");
// File.WriteAllBytes(output565, rgb565Data);

// var folderToResize = @"C:\Dev\Gaming\PC\Win\Extractions\Math Invaders\output\LP27\Anims\aresear\output";
// FileHelpers.ResizeImagesInFolder(folderToResize, ExpansionOrigin.BottomCenter);

// var djBin = @"C:\Dev\Gaming\Sony\PS2\Def Jam - Vendetta (Europe)\DATA\DATA.BIN";
// using var djReader = new BinaryReader(File.OpenRead(djBin));

// djReader.BaseStream.Seek(0x91B70E0, SeekOrigin.Begin);
// var testData = djReader.ReadBytes(0xc000);
// var testImage = ImageFormatHelper.ConvertRGB888(testData, 128, 128);
// testImage.Save(@"C:\Dev\Gaming\Sony\PS2\Def Jam - Vendetta (Europe)\DATA\test_rgb888.png", ImageFormat.Png);

// for (int i = 0; i < 5; i++)
// {
//     var outputDir = Path.Combine(Path.GetDirectoryName(djBin)!, $"images_output_{i}");
//     Directory.CreateDirectory(outputDir);

//     var mainDataBlock = djReader.ReadBytes(0x1f800);
//     using var mainDataReader = new BinaryReader(new MemoryStream(mainDataBlock));
//     mainDataReader.BaseStream.Seek(0x80, SeekOrigin.Begin);
//     var palette = new List<System.Drawing.Color>();
//     var imageIndex = 0;
//     while (mainDataReader.BaseStream.Position < mainDataReader.BaseStream.Length)
//     {
//         // image header first, image data next, palette header and data last
//         var header = mainDataReader.ReadBytes(0x60);
//         if (header.All(b => b == 0x00))
//             break; // end of images
//         var type = header[0x17]; // type at 0x17 - 0x13 = image 0x00 = palette?
//         // width at 0x30, height at 0x34
//         var width = BitConverter.ToUInt32(header, 0x30);
//         var height = BitConverter.ToUInt32(header, 0x34);
//         var dataSize = width * height;
//         var imageData = mainDataReader.ReadBytes((int)dataSize);
//         header = mainDataReader.ReadBytes(0x60); // read next header for 
//         var palData = mainDataReader.ReadBytes(0x400);
//         palette = ColorHelper.ConvertBytesToRGBA(palData);
//         var image = ImageFormatHelper.GenerateClutImage(palette, imageData, (int)width, (int)height);
//         var outputFile = Path.Combine(outputDir, $"image_{imageIndex++}_{width}x{height}.png");
//         image.Save(outputFile, ImageFormat.Png);
//     }
// }
// var timOutputDir = Path.Combine(Path.GetDirectoryName(djBin)!, $"tim2_output");
// Directory.CreateDirectory(timOutputDir);
// // check for TIM2 magic
// var timIndex = 0;
// var magic = Encoding.ASCII.GetString(djReader.ReadBytes(4));
// while (djReader.BaseStream.Position < djReader.BaseStream.Length)
// {
//     switch (magic)
//     {
//         case "TIM2":
//             parseTIM(djReader);
//             break;
//         default:
//             // skip back 4 bytes and check if value == 0x1
//             djReader.BaseStream.Seek(-4, SeekOrigin.Current);
//             var checkVal = djReader.ReadUInt32();
//             if (checkVal == 0x1)
//             {
//                 // likely padding, skip ahead 4 bytes
//                 djReader.BaseStream.Seek(0x77FC, SeekOrigin.Current);
//                 break;
//             }
//             else if (checkVal == 0x80)
//             {
//                 var size = (djReader.ReadUInt32() + 0x7FF) / 0x800 * 0x800;

//             }
//             Console.WriteLine($"Unknown magic: {magic} at position {djReader.BaseStream.Position - 4}");
//             djReader.ReadBytes((int)djReader.ReadUInt32());
//             break;
//     }
//     if (magic == "WAZA" || magic == "FZUI" || magic == "NAME" || magic == "WPAR" || magic == "WWAZ")
//     {
//         magic = Encoding.ASCII.GetString(djReader.ReadBytes(4)).TrimEnd('\0');
//         if (magic == "WAZA" || magic == "FZUI" || magic == "NAME" || magic == "WPAR" || magic == "WWAZ") continue;
//     }
//     // seek to next multiple of 0x800
//     var position = djReader.BaseStream.Position;
//     var nextPosition = ((position + 0x7FF) / 0x800) * 0x800;
//     djReader.BaseStream.Seek(nextPosition, SeekOrigin.Begin);
//     magic = Encoding.ASCII.GetString(djReader.ReadBytes(4)).TrimEnd('\0');
// }


// Console.WriteLine($"Current Position: {djReader.BaseStream.Position}, Remainder: {djReader.BaseStream.Length - djReader.BaseStream.Position}");

// void parseTIM(BinaryReader djReader)
// {

//     var formatRev = djReader.ReadByte();
//     var format = djReader.ReadByte();
//     var picCount = djReader.ReadUInt16();
//     djReader.ReadBytes(8); // reserved
//     for (int p = 0; p < picCount; p++)
//     {
//         var totalSize = djReader.ReadUInt32();
//         var clutSize = djReader.ReadUInt32();
//         var imageDataSize = djReader.ReadUInt32();
//         var headerSize = djReader.ReadUInt16();
//         var colourCount = djReader.ReadUInt16();
//         var picFormat = djReader.ReadByte();
//         var mipCount = djReader.ReadByte();
//         var clutType = djReader.ReadByte();
//         var imageColourType = djReader.ReadByte();
//         var width = djReader.ReadUInt16();
//         var height = djReader.ReadUInt16();
//         var reg1 = djReader.ReadInt64();
//         var reg2 = djReader.ReadInt64();
//         var flagsReg = djReader.ReadUInt32();
//         var clutReg = djReader.ReadUInt32();
//         if (mipCount > 1)
//         {
//             djReader.ReadBytes(0x30);
//         }
//         var imageData = djReader.ReadBytes((int)imageDataSize);
//         Console.WriteLine($"TIM2 Image {p}: FormatRev={formatRev}, Format={format}, Width={width}, Height={height}, ColourCount={colourCount}, PicFormat={picFormat}, MipCount={mipCount}, ClutType={clutType}, ImageColourType={imageColourType}");
//         // 0   Undefined
//         // 1   16 - bit RGBA(A1B5G5R5)
//         // 2   32 - bit RGB(X8B8G8R8)
//         // 3   32 - bit RGBA(A8B8G8R8)
//         // 4   4 - bit indexed
//         // 5   8 - bit indexed
//         switch (imageColourType)
//         {
//             case 0:
//             default:
//                 Console.WriteLine($"Unsupported TIM2 image colour type: {imageColourType}");
//                 var binaryFile = Path.Combine(timOutputDir, $"tim2_image_{timIndex++}_{p}_{width}x{height}_unknown.bin");
//                 File.WriteAllBytes(binaryFile, imageData);
//                 break;
//             case 1:
//                 {
//                     var image = ImageFormatHelper.ConvertA1B5G5R5ToBitmap(imageData, (int)width, (int)height);
//                     var outputFile = Path.Combine(timOutputDir, $"tim2_image_{timIndex++}_{p}_{width}x{height}_A1B5G5R5.png");
//                     image.Save(outputFile, ImageFormat.Png);
//                     break;
//                 }
//             case 2:
//                 {
//                     var image = ImageFormatHelper.ConvertRGB888(imageData, (int)width, (int)height);
//                     var outputFile = Path.Combine(timOutputDir, $"tim2_image_{timIndex++}_{p}_{width}x{height}_X8B8G8R8.png");
//                     image.Save(outputFile, ImageFormat.Png);
//                     break;
//                 }
//         }
//     }
// }

// var pcxDir = @"C:\Dev\Gaming\PC\Dos\Games\Azrael's Tear\TEXTURES\WIRE";
// var pcxFiles = Directory.GetFiles(pcxDir, "*.PCX", SearchOption.TopDirectoryOnly);
// foreach (var pcxFile in pcxFiles)
// {
//     try
//     {
//         var image = ImageFormatHelper.ConvertPCX(File.ReadAllBytes(pcxFile), true);
//         var outputFile = Path.Combine(Path.GetDirectoryName(pcxFile)!, Path.GetFileNameWithoutExtension(pcxFile) + ".png");
//         image.Save(outputFile);
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"An error occurred during PCX conversion of {pcxFile}: {ex.Message}");
//     }
// }


// var resFile = @"C:\Dev\Gaming\PC\Dos\Games\Amulets-Armor_DOS_EN_v10\PICS.RES";
// var res = new ExtractCLUT.Games.PC.AmuletsAndArmor.ResFile(resFile);
// var outputDir = Path.Combine(Path.GetDirectoryName(resFile)!, "pics_res_output");
// Directory.CreateDirectory(outputDir);
// var count = res.PopulateEntries();
// foreach (var entry in res.Entries)
// {
//     Console.WriteLine($"Entry: {entry.Name}, Offset: {entry.Offset}, Size: {entry.Size}, TypeFlag: {entry.TypeFlag}");
//     var data = res.ExtractEntryData(entry);
//     var outputFile = Path.Combine(outputDir, entry.Name);
//     File.WriteAllBytes(outputFile, data);
// }


// var gfxDir = @"C:\Dev\Gaming\PC\Dos\Games\ALIEN_TRILOGY_PC\CD\SECT11";
// var gfxFiles = Directory.GetFiles(gfxDir, "*.B16", SearchOption.TopDirectoryOnly);
// var bndFiles = Directory.GetFiles(gfxDir, "*.BIN", SearchOption.TopDirectoryOnly);
// var allFiles = gfxFiles.Concat(bndFiles).ToList();
// foreach (var gfxFile in allFiles)
// {
//     try
//     {
//         GfxHelper.ExtractGfxFile(gfxFile);
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"An error occurred during GFX extraction of {gfxFile}: {ex.Message}");
//     }
// }

// var folderToResize = @"C:\Dev\Gaming\PC\Dos\DiscImages\Abuse_DOS_EN_ISO-Version\ABUSE\ART\JUG_Extracted\Transparency";
// FileHelpers.ResizeImagesInFolder(folderToResize, ExpansionOrigin.BottomCenter);

// var speDir = @"C:\Dev\Gaming\PC\Dos\DiscImages\Abuse_DOS_EN_ISO-Version\ABUSE";
// var speFiles = Directory.GetFiles(speDir, "*.SPE", SearchOption.AllDirectories);
// foreach (var speFile in speFiles)
// {
//     try
//     {
//         SpeHelper.ProcessSpeFile(speFile, true);
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"An error occurred during SPE processing: {ex.Message}");
//     }
// }

// var gxlDir = @"C:\Dev\Gaming\PC\Dos\Games\darkhalf";
// var gxlFiles = Directory.GetFiles(gxlDir, "*.GXL", SearchOption.TopDirectoryOnly);
// var outputDir = Path.Combine(gxlDir, "gxl_output");
// Directory.CreateDirectory(outputDir);

// foreach (var gxlFile in gxlFiles)
// {
//   try
//   {
//     using var gxlReader = new BinaryReader(File.OpenRead(gxlFile));
//     gxlReader.BaseStream.Seek(0x5e, SeekOrigin.Begin);
//     var count = gxlReader.ReadUInt16();
//     gxlReader.BaseStream.Seek(0x80, SeekOrigin.Begin);
//     var namesOffsetsLengths = new List<(string name, uint offset, uint length)>();
//     for (int i = 0; i < count; i++)
//     {
//       gxlReader.ReadByte();
//       var name = gxlReader.ReadNullTerminatedString();
//       var offset = gxlReader.ReadUInt32();
//       var length = gxlReader.ReadUInt32();
//       namesOffsetsLengths.Add((name, offset, length));
//       gxlReader.ReadBytes(4);
//     }
//     foreach (var pair in namesOffsetsLengths)
//     {
//       var (name, offset, length) = pair;
//       // Process each name, offset, and length as needed
//       gxlReader.BaseStream.Seek(offset, SeekOrigin.Begin);
//       var data = gxlReader.ReadBytes((int)length);
//       if (name.EndsWith(".PCX"))
//       {
//         var image = ImageFormatHelper.ConvertPCX(data, false);
//         var outputFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(name) + ".png");
//         image.Save(outputFile);
//         image = ImageFormatHelper.ConvertPCX(data, true);
//         outputFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(name) + "_t.png");
//         image.Save(outputFile);
//       }
//       else
//       {
//         var outputFile = Path.Combine(outputDir, name);
//         File.WriteAllBytes(outputFile, data);
//       }
//     }
//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"An error occurred during conversion: {ex.Message}");
//   }
// }

// var inputDir = @"C:\Dev\Gaming\PC\Dos\Games\Tex-Piombo-Caldo_DOS_IT\TEX1\STA";
// var matFiles = Directory.GetFiles(inputDir, "*.MAT", SearchOption.TopDirectoryOnly);
// var palFile = @"C:\Dev\Gaming\PC\Dos\Games\Tex-Piombo-Caldo_DOS_IT\TEX1\STA\ARCADE.PAL";
// var palData = File.ReadAllBytes(palFile).Skip(0x05).ToArray();
// var palette = ColorHelper.ConvertBytesToRgbIS(palData, true);

// foreach (var matFile in matFiles)
// {
//   var matData = File.ReadAllBytes(matFile);
//   var outputDir = Path.Combine(Path.GetDirectoryName(matFile)!, Path.GetFileNameWithoutExtension(matFile) + "_output");
//   Directory.CreateDirectory(outputDir);

//   var tWidth = 16;
//   var tHeight = 8;
//   var tSize = tWidth * tHeight;

//   for (int i = 0; i < matData.Length / tSize; i++)
//   {
//     var tileData = new byte[tSize];
//     Array.Copy(matData, i * tSize, tileData, 0, tSize);
//     var image = ImageFormatHelper.GenerateIMClutImage(palette, tileData, tWidth, tHeight, true);
//     var outputFile = Path.Combine(outputDir, $"{i:D3}.png");
//     image.Save(outputFile);
//   }

// }


// var lbmDir = @"C:\Dev\Gaming\PC\Dos\Games\PerfectAssassin\D";
// var lbmFiles = Directory.GetFiles(lbmDir, "*.LBM", SearchOption.TopDirectoryOnly);
// var outputDir = Path.Combine(lbmDir, "lbm_output");
// Directory.CreateDirectory(outputDir);
// foreach (var lbmFile in lbmFiles)
// {
//     var LbmConverter = new LbmConverter();
//     var outputPngPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(lbmFile) + ".png");
//     try
//     {
//         using var lbmStream = File.OpenRead(lbmFile);
//         using var pngStream = File.Create(outputPngPath);

//         await LbmConverter.ConvertAsync(lbmStream, pngStream);

//         Console.WriteLine("Conversion successful! 🎉");
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"An error occurred during conversion: {ex.Message}");
//     }
// }


// var guiltyGfxDat = @"C:\Dev\Gaming\PC\Dos\Games\GUILTY\GBG_GRAF.DAT";
// var datFiles = Directory.GetFiles(Path.GetDirectoryName(guiltyGfxDat)!, "GBG_0*.DAT", SearchOption.TopDirectoryOnly);

// var offsetLists = new List<List<uint>>();
// using var mainReader = new BinaryReader(File.OpenRead(guiltyGfxDat));
// var listIndex = 0;
// offsetLists.Add(new List<uint>());
// while (mainReader.BaseStream.Position < mainReader.BaseStream.Length)
// {
//   var offset = mainReader.ReadUInt32();
//   if (offset == 0 && offsetLists[listIndex].Count > 0)
//   {
//     listIndex++;
//     offsetLists.Add(new List<uint>());
//   }
//   offsetLists[listIndex].Add(offset);
// }

// foreach (var (offsetList, index) in offsetLists.WithIndex())
// {
//   var datFile = datFiles.ElementAtOrDefault(index);
//   if (datFile == null)
//   {
//     Console.WriteLine($"Missing DAT file for offset list {index}");
//     continue;
//   }
//   var outputDir = Path.Combine(Path.GetDirectoryName(datFile)!, $"{Path.GetFileNameWithoutExtension(datFile)}_extracted"); 

//   using var datReader = new BinaryReader(File.OpenRead(datFile));
//   foreach (var (offset, oIndex) in offsetList.WithIndex())
//   {
//     datReader.BaseStream.Seek(offset, SeekOrigin.Begin);
//     var length = (int)(oIndex < offsetList.Count - 1 ? offsetList[oIndex + 1] - offset : datReader.BaseStream.Length - offset);
//     var data = datReader.ReadBytes(length);
//     var outputFile = Path.Combine(outputDir, $"{oIndex:D3}.bin");
//     Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
//     File.WriteAllBytes(outputFile, data);
//   }
// }


// var palFile = @"C:\Dev\Gaming\PC\Dos\Games\Clif-Danger_DOS_EN_ISO-Version\ZIP\main.pal";
// var palData = File.ReadAllBytes(palFile);
// var palette = ColorHelper.ConvertBytesToArgbIS(palData);
// var inputDir = @"C:\Dev\Gaming\PC\Dos\Games\Clif-Danger_DOS_EN_ISO-Version\ZIP\output";
// var sprFiles = Directory.GetFiles(inputDir, "*.spr", SearchOption.TopDirectoryOnly);
// var ofsFiles = Directory.GetFiles(inputDir, "*.ofs", SearchOption.TopDirectoryOnly);
// var dimFiles = Directory.GetFiles(inputDir, "*.dim", SearchOption.TopDirectoryOnly);
// var posFiles = Directory.GetFiles(inputDir, "*.pos", SearchOption.TopDirectoryOnly);

// foreach (var sprFile in sprFiles)
// {
//   var baseName = Path.GetFileNameWithoutExtension(sprFile);
//   var ofsFile = ofsFiles.Where(f => Path.GetFileNameWithoutExtension(f) == baseName).FirstOrDefault();
//   var dimFile = dimFiles.Where(f => Path.GetFileNameWithoutExtension(f) == baseName).FirstOrDefault();
//   var posFile = posFiles.Where(f => Path.GetFileNameWithoutExtension(f) == baseName).FirstOrDefault();
//   if (ofsFile == null || dimFile == null || posFile == null)
//   {
//     Console.WriteLine($"Missing associated file for {sprFile}");
//     continue;
//   }
//   var outputDir = Path.Combine(Path.GetDirectoryName(sprFile)!, baseName + "_output");
//   Directory.CreateDirectory(outputDir);

//   using var ofsReader = new BinaryReader(File.OpenRead(ofsFile));
//   using var sprReader = new BinaryReader(File.OpenRead(sprFile));   
//   using var dimReader = new BinaryReader(File.OpenRead(dimFile));
//   using var posReader = new BinaryReader(File.OpenRead(posFile));
//   var sprIndex = 0;
//   while (ofsReader.BaseStream.Position < ofsReader.BaseStream.Length)
//   {
//     var sprOffset = ofsReader.ReadUInt32();
//     var sprWidth = dimReader.ReadUInt32();
//     var sprHeight = dimReader.ReadUInt32();
//     var posX = posReader.ReadInt32();
//     var posY = posReader.ReadInt32();
//     sprReader.BaseStream.Seek(sprOffset, SeekOrigin.Begin);
//     var sprData = sprReader.ReadBytes((int)(sprWidth * sprHeight));
//     var image = ImageFormatHelper.GenerateIMClutImage(palette, sprData, (int)sprWidth, (int)sprHeight, true);
//     var outputFile = Path.Combine(outputDir, $"{sprIndex++}_{sprWidth}_{sprHeight}_{posX}_{posY}.png");
//     image.SaveAsPng(outputFile);
//   }
// }

// var levelDir = @"C:\Dev\Gaming\PC\Win\Games\SUPER_BUBSY\LEVELS";
// var levelFiles = Directory.GetFiles(levelDir, "*.GAM", SearchOption.TopDirectoryOnly);

// var spriteDir = @"C:\Dev\Gaming\PC\Win\Games\SUPER_BUBSY\SPRITES\sprite_output";
// var spriteFiles = Directory.GetFiles(spriteDir, "*.bin", SearchOption.TopDirectoryOnly);
// var spriteOutputDir = Path.Combine(spriteDir, "decompressed");
// Directory.CreateDirectory(spriteOutputDir);

// var palette = new LevelFile(levelFiles.Where(f => f.Contains("W1L1.GAM")).First()).Clut;

// foreach (var spriteFile in spriteFiles)
// {
//   using var spriteReader = new BinaryReader(File.OpenRead(spriteFile));
//   var width = spriteReader.ReadUInt16();
//   var height = spriteReader.ReadUInt16();
//   var CompressedLength = spriteReader.ReadUInt16();
//   var compressionFlag = spriteReader.ReadUInt16();
//   if (compressionFlag == 3)
//   {
//     var compressedData = spriteReader.ReadBytes(CompressedLength);
//     var decompressedData = BubsyDecompress.DecompressLobitImage(compressedData, 0, width, height);

//     var image = ImageFormatHelper.GenerateIMClutImage(palette, decompressedData, width, height, true);
//     image.Save(Path.Combine(spriteOutputDir, Path.GetFileNameWithoutExtension(spriteFile) + ".png"));
//   }
//   else if (compressionFlag == 0)
//   {
//     // uncompressed
//     var pixelData = spriteReader.ReadBytes(width * height);
//     var image = ImageFormatHelper.GenerateIMClutImage(palette, pixelData, width, height, false);
//     image.Save(Path.Combine(spriteOutputDir, Path.GetFileNameWithoutExtension(spriteFile) + ".png"));
//   }
//   else
//   {
//     Console.WriteLine($"Unknown compression flag {compressionFlag} in {spriteFile}");
//   }
// }

// var bird = @"C:\Dev\Gaming\PC\Win\Games\SUPER_BUBSY\SPRITES\sprite_output\BIRD1L.BMP_compressed.bin";
// var birdData = File.ReadAllBytes(bird).Skip(8).ToArray();
// var decompressed = BubsyDecompress.DecompressLobitImage(birdData, 0, 0x4c, 0x44);
// var outputFolder = Path.Combine(Path.GetDirectoryName(bird)!, "decompressed");
// Directory.CreateDirectory(outputFolder);
// File.WriteAllBytes(Path.Combine(outputFolder, "BIRD1L.bin"), decompressed);

// var levelFileMissed = @"C:\Dev\Gaming\PC\Win\Games\SUPER_BUBSY\LEVELS\RAYON_A.GAM";
// var level = new LevelFile(levelFileMissed);
// var levelOutputDir = Path.Combine(Path.GetDirectoryName(levelFileMissed)!, Path.GetFileNameWithoutExtension(levelFileMissed));
// Directory.CreateDirectory(levelOutputDir);
// level.SaveMapImage(Path.Combine(levelOutputDir, "map.png"));



// var levelOutputDir = Path.Combine(levelDir, "level_maps");
// Directory.CreateDirectory(levelOutputDir);

// foreach (var levelFile in levelFiles)
// {
//   try
//   {
//     var level = new LevelFile(levelFile);
//     //var clutOutputFile = Path.Combine(levelOutputDir, "clut.png");
//     //level.SaveClut(clutOutputFile, 256, 128, 1);
//     //level.SaveTiles(levelOutputDir);
//     level.SaveMapImage(Path.Combine(levelOutputDir, Path.GetFileNameWithoutExtension(levelFile) + "_map.png"));
//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"Error processing {levelFile}: {ex.Message}");
//   }
// }


// var tafFile = @"C:\Dev\Gaming\PC\Dos\Games\ark_of_time\AOT0.TAF";
// var tafOutputDir = Path.Combine(Path.GetDirectoryName(tafFile)!, "output");
// Directory.CreateDirectory(tafOutputDir);
// ExtractTafFile(tafFile);

// void ExtractTafFile(string tafFile)
// {
//   var tafReader = new BinaryReader(File.OpenRead(tafFile));
//   var magic = tafReader.ReadUInt32(); // read the magic number
//   if (magic != 0x00464154) // "TAF\0"
//   {
//     throw new Exception("Invalid TAF file");
//   }
//   tafReader.ReadUInt16();
//   var count = tafReader.ReadUInt16(); // number of chunks
//   var allSize = tafReader.ReadUInt32(); // size of the file
//   var paletteData = tafReader.ReadBytes(0x300); // read the palette data
//   var palette = ColorHelper.ConvertBytesToRGB(paletteData, true);
//   var nextOffset = tafReader.ReadUInt32(); // offset to the next chunk
//   var correctionCount = tafReader.ReadUInt16(); // number of corrections

//   if (nextOffset == tafReader.BaseStream.Position + 1)
//     tafReader.ReadByte(); // skip the 0x00 byte

//   if (nextOffset != tafReader.BaseStream.Position)
//   {
//     throw new Exception("Invalid Sprite resource");
//   }

//   var outputDir = Path.Combine(Path.GetDirectoryName(tafFile)!, "output", Path.GetFileNameWithoutExtension(tafFile));
//   Directory.CreateDirectory(outputDir);

//   for (int i = 0; i < count; i++)
//   {
//     var compressionFlag = tafReader.ReadUInt16(); // 0 = uncompressed, 1 = compressed
//     var width = tafReader.ReadUInt16(); // width of the image
//     var height = tafReader.ReadUInt16(); // height of the image
//     nextOffset = tafReader.ReadUInt32(); // offset to the next chunk
//     var spriteOffset = tafReader.ReadUInt32(); // offset to the sprite data
//     tafReader.ReadByte(); // skip the 0x00 byte
//     if (tafReader.BaseStream.Position != spriteOffset)
//     {
//       throw new Exception("Invalid Sprite resource");
//     }
//     var length = nextOffset - spriteOffset; // length of the sprite data
//     var spriteData = tafReader.ReadBytes((int)length); // read the sprite data
//     if (compressionFlag == 1)
//     {
//       spriteData = DecodeRLE(spriteData); // decode the sprite data
//     }
//     var image = ImageFormatHelper.GenerateClutImage(palette, spriteData, width, height, true);
//     var outputFile = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(tafFile)}_{i}.png");
//     image.Save(outputFile, ImageFormat.Png);
//   }
// }

// byte[] DecodeRLE(byte[] data)
// {
//   var output = new List<byte>();
//   for (var i = 0; i < data.Length / 2 ; i++)
//   {
//     var count = data[i * 2];
//     var value = data[i * 2 + 1];
//     for (var j = 0; j < count; j++)
//     {
//       output.Add(value);
//     }
//   }
//   return output.ToArray();
// }

// using ExtractCLUT.Games.PC.Raptor;
// var mapFileDir = @"C:\Dev\Gaming\PC\Dos\Games\RAPTOR\glb_output\FILE0004";
// var mapFiles = Directory.GetFiles(mapFileDir, "*MAP*", SearchOption.TopDirectoryOnly);
// var tilesetsPath = @"C:\Dev\Gaming\PC\Dos\Extractions\Raptor\Maps\Tiles";
// foreach (var mapFile in mapFiles)
// {
//   var mapImage = RaptorFormats.CreateRaptorMap(mapFile, tilesetsPath);
//   var mapOutputDir = Path.Combine(Path.GetDirectoryName(mapFile)!, "map_output");
//   Directory.CreateDirectory(mapOutputDir);
//   var mapOutputFile = Path.Combine(mapOutputDir, Path.GetFileNameWithoutExtension(mapFile) + ".png");
//   mapImage.Save(mapOutputFile);
// }

// var palFile = @"C:\Dev\Gaming\PC\Dos\Games\RAPTOR\glb_output\FILE0001\PALETTE_DAT";
// var palData = File.ReadAllBytes(palFile);
// var palette = ColorHelper.ConvertBytesToRgbIS(palData, true);

// var picFilesDir = @"C:\Dev\Gaming\PC\Dos\Games\RAPTOR\glb_output\FILE0004";
// var picFiles = Directory.GetFiles(picFilesDir, "*_PIC*", SearchOption.TopDirectoryOnly);
// var blkFiles = Directory.GetFiles(picFilesDir, "*_BLK*", SearchOption.TopDirectoryOnly);
// var tileFiles = Directory.GetFiles(picFilesDir, "*TILES*", SearchOption.TopDirectoryOnly);
// var allFiles = picFiles.Concat(blkFiles).ToList();
// allFiles = allFiles.Concat(tileFiles).ToList();
// var tpOutputDir = Path.Combine(picFilesDir, "pic_output_tp");
// Directory.CreateDirectory(tpOutputDir);
// var outputDir = Path.Combine(picFilesDir, "pic_output");
// Directory.CreateDirectory(outputDir);
// var tileOutputDir = Path.Combine(picFilesDir, "tile_output");
// Directory.CreateDirectory(tileOutputDir);
// foreach (var picFile in allFiles)
// {
//   try
//   {
//     var image = ConvertRaptorPic(picFile, palette, true);
//     var outputFile = picFile.Contains("TILES") ? Path.Combine(tileOutputDir, Path.GetFileNameWithoutExtension(picFile) + ".png") : Path.Combine(tpOutputDir, Path.GetFileNameWithoutExtension(picFile) + ".png");
//     image.Save(outputFile);
//     image = ConvertRaptorPic(picFile, palette, false);
//     outputFile = picFile.Contains("TILES") ? Path.Combine(tileOutputDir, Path.GetFileNameWithoutExtension(picFile) + ".png") : Path.Combine(outputDir, Path.GetFileNameWithoutExtension(picFile) + ".png");
//     image.Save(outputFile);
//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"Error processing {picFile}: {ex.Message}");
//   }
// }



// Image<Rgba32> ConvertRaptorPic(string inputFile, List<Color> palette, bool useTransparency)
// {
//   using var fReader = new BinaryReader(new FileStream(inputFile, FileMode.Open, FileAccess.Read));

//   // Read Header
//   fReader.ReadUInt32(); // unknown1
//   fReader.ReadUInt32(); // unknown2
//   uint iLineCount = fReader.ReadUInt32();
//   int width = (int)fReader.ReadUInt32();
//   int height = (int)fReader.ReadUInt32();

//   byte[] pixelData = new byte[width * height];

//   if (iLineCount == 0)
//   {
//     // Raw 8bpp image data
//     fReader.Read(pixelData, 0, pixelData.Length);
//   }
//   else
//   {
//     // Sparse image with transparent parts, default to index 0
//     Array.Fill(pixelData, (byte)0);

//     while (fReader.BaseStream.Position < fReader.BaseStream.Length)
//     {
//       uint iPosX = fReader.ReadUInt32();
//       uint iPosY = fReader.ReadUInt32();
//       uint iLinearOffset = fReader.ReadUInt32();
//       uint iCount = fReader.ReadUInt32();

//       // Check for terminator block
//       if (iLinearOffset == 0xFFFFFFFF && iCount == 0xFFFFFFFF)
//       {
//         break;
//       }

//       if (iCount > 0)
//       {
//         byte[] pixels = fReader.ReadBytes((int)iCount);
//         int destOffset = (int)(iPosY * width + iPosX);

//         if (destOffset + iCount <= pixelData.Length)
//         {
//           Buffer.BlockCopy(pixels, 0, pixelData, destOffset, (int)iCount);
//         }
//       }
//     }
//   }

//   return ImageFormatHelper.GenerateIMClutImage(palette, pixelData, width, height, useTransparency);
// }


// var v2lTest = @"C:\Dev\Gaming\PC\Dos\Games\MARIO_DELUXE_DOS\MARI";
// var v2lOutputDir = Path.Combine(Path.GetDirectoryName(v2lTest)!, "mari_output");
// FormatHelper.ExtractV2LFile(v2lTest, v2lOutputDir);
// var bbmDir = @"C:\Dev\Gaming\PC\Dos\Games\teenage-mutant-ninja-turtles-ii-the-arcade-game";
// var bbmFiles = Directory.GetFiles(bbmDir, "*.LBM", SearchOption.AllDirectories);
// var outputDir = Path.Combine(bbmDir, "lbm_output");
// Directory.CreateDirectory(outputDir);
// foreach (var bbmFile in bbmFiles)
// {
//   var LbmConverter = new LbmConverter();
//   var outputPngPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(bbmFile) + ".png");
//   try
//   {
//     using var lbmStream = File.OpenRead(bbmFile);
//     using var pngStream = File.Create(outputPngPath);

//     await LbmConverter.ConvertAsync(lbmStream, pngStream);

//     Console.WriteLine("Conversion successful! 🎉");
//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"An error occurred during conversion: {ex.Message}");
//   }
// }

// var graphLib = @"C:\Dev\Gaming\PC\Dos\Games\Crazy-Drake_DOS_EN_Registered\output\EPISODE2\EPISODE2\7\GRAPHLIB.7";
// var palFile = @"C:\Dev\Gaming\PC\Dos\Games\Crazy-Drake_DOS_EN_Registered\output\EPISODE2\EPISODE2\7\PLATFORM.BBM";
// var palData = File.ReadAllBytes(palFile).Skip(0x30).Take(0x300).ToArray();
// var defaultPalette = ColorHelper.ConvertBytesToRGB(palData);

// ExtractLibFile(graphLib, defaultPalette, 200, 207);

// var tilesFolder = @"C:\Dev\Gaming\PC\Dos\Games\Crazy-Drake_DOS_EN_Registered\output\EPISODE2\EPISODE2\7\output\GRAPHLIB\pal7_output";
// var tilesImages = Directory.GetFiles(tilesFolder, "*.png").OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f))).Select(f => Image.FromFile(f)).ToList();
// var mapFile = @"C:\Dev\Gaming\PC\Dos\Games\Crazy-Drake_DOS_EN_Registered\output\EPISODE2\EPISODE2\7\MAP.71";
// var mapReader = new BinaryReader(File.OpenRead(mapFile));
// mapReader.BaseStream.Seek(0x45, SeekOrigin.Begin);
// var mapWidth = mapReader.ReadInt32();
// var mapHeight = mapReader.ReadInt32();
// mapReader.ReadBytes(0x8);
// var mapDataLength = mapReader.ReadInt32();
// var mapData = mapReader.ReadBytes(mapDataLength);
// var mapShorts = new short[mapDataLength / 2];
// Buffer.BlockCopy(mapData, 0, mapShorts, 0, mapDataLength);
// var mapImage = ImageFormatHelper.CreateScreenImage(tilesImages, mapShorts, mapWidth, mapHeight, 32, 32);
// mapImage.Save(Path.Combine(tilesFolder, "..", "map71_output_7.png"), ImageFormat.Png);


// var libDir = @"C:\Dev\Gaming\PC\Dos\Games\Crazy-Drake_DOS_EN_Registered\output\";
// var libFiles = Directory.GetFiles(libDir, "*.lib", SearchOption.AllDirectories);


// foreach (var libFile in libFiles)
// {
//   ExtractLibFile(libFile);
// }

// static void ExtractLibFile(string libFile, List<Color>? defaultPalette = null, int palRotateStart = 0, int palRotateEnd = 0)
// {
//   var libReader = new BinaryReader(File.OpenRead(libFile));

//   libReader.BaseStream.Seek(0x19, SeekOrigin.Begin);
//   var count = libReader.ReadUInt32();

//   var chunkName = Encoding.ASCII.GetString(libReader.ReadBytes(4)).TrimEnd('\0');
//   var palette = new List<Color>();
//   var width = 0;
//   var height = 0;
//   var index = 0;
//   while (chunkName != "LIBE")
//   {
//     switch (chunkName)
//     {
//       case "PAL ":
//         var palLength = libReader.ReadUInt32();
//         var palData = libReader.ReadBytes((int)palLength);
//         palette = palData.Sum(a => a) > 0 ? ColorHelper.ConvertBytesToRGB(palData, true) : defaultPalette ?? new List<Color>();
//         break;
//       case "BMPH":
//         var headerLength = libReader.ReadUInt32();
//         width = (int)libReader.ReadUInt32();
//         height = (int)libReader.ReadUInt32();
//         libReader.ReadBytes((int)headerLength - 8);
//         break;
//       case "BMP ":
//         var bmpLength = libReader.ReadUInt32();
//         var bmpData = libReader.ReadBytes((int)bmpLength);
//         if (palRotateEnd > 0)
//         {
//           var outputDir = Path.Combine(Path.GetDirectoryName(libFile)!, "output", Path.GetFileNameWithoutExtension(libFile), "pal0_output");
//           Directory.CreateDirectory(outputDir);
//           var colorsToRotate = palRotateEnd - palRotateStart + 1;
//           var outputFile = Path.Combine(outputDir, $"{index}.png");
//           for (int i = 0; i < colorsToRotate; i++)
//           {
//             var image = ImageFormatHelper.GenerateClutImage(palette, bmpData, width, height, true, 0);
//             image.Save(outputFile, ImageFormat.Png);
//             var color = palette[palRotateStart];
//             palette.RemoveAt(palRotateStart);
//             palette.Insert(palRotateEnd, color);
//             outputDir = Path.Combine(Path.GetDirectoryName(libFile)!, "output", Path.GetFileNameWithoutExtension(libFile), $"pal{i + 1}_output");
//             Directory.CreateDirectory(outputDir);
//             outputFile = Path.Combine(outputDir, $"{index}.png");
//           }
//           index++;
//         }
//         else
//         {
//           var outputDir = Path.Combine(Path.GetDirectoryName(libFile)!, "output", Path.GetFileNameWithoutExtension(libFile), "pal0_output");
//           Directory.CreateDirectory(outputDir);
//           var outputFile = Path.Combine(outputDir, $"{index++}.png");
//           var image = ImageFormatHelper.GenerateClutImage(palette, bmpData, width, height, true, 0);
//           image.Save(outputFile, ImageFormat.Png);
//         }
//         break;
//       default:
//         Console.WriteLine($"Unknown chunk: {chunkName}");
//         break;
//     }
//     chunkName = Encoding.ASCII.GetString(libReader.ReadBytes(4)).TrimEnd('\0');
//   }
// }

// var resDir = @"C:\Dev\Gaming\PC\Dos\Games\Crazy-Drake_DOS_EN_Registered";
// var resFiles = Directory.GetFiles(resDir, "*.res", SearchOption.TopDirectoryOnly);

// foreach (var resFile in resFiles)
// {
//   var outputDir = Path.Combine(resDir, "output", Path.GetFileNameWithoutExtension(resFile));
//   Directory.CreateDirectory(outputDir);
//   var resReader = new BinaryReader(File.OpenRead(resFile));
//   resReader.BaseStream.Seek(0x19, SeekOrigin.Begin);
//   var count = resReader.ReadUInt32();
//   var entries = new List<(string path, uint offset, uint length)>();
//   for (int i = 0; i < count; i++)
//   {
//     var pathBytes = resReader.ReadBytes(0x28);
//     var path = Encoding.ASCII.GetString(pathBytes).TrimEnd('\0');
//     var offset = resReader.ReadUInt32();
//     var length = resReader.ReadUInt32();
//     entries.Add((path, length, offset));
//   }

//   foreach (var (path, offset, length) in entries)
//   {
//     resReader.BaseStream.Seek(offset, SeekOrigin.Begin);
//     var data = resReader.ReadBytes((int)length);
//     var outputFile = Path.Combine(outputDir, path);
//     Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
//     File.WriteAllBytes(outputFile, data);
//   }
// }


// var itkFile = @"C:\Dev\Gaming\PC\Win\Games\The-Bizarre-Adventures-of-Woodruff-and-the-Schnibble\intro.sTK";
// var outputDir = Path.Combine(Path.GetDirectoryName(itkFile)!, "i_output");
// Directory.CreateDirectory(outputDir);

// using var itkReader = new BinaryReader(File.OpenRead(itkFile));
// var count = itkReader.ReadUInt16();

// var entries = new List<(string name, uint length, uint offset)>();

// for (int i = 0; i < count; i++)
// {
//   var nameBytes = itkReader.ReadBytes(0xD);
//   var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
//   var length = itkReader.ReadUInt32();
//   var offset = itkReader.ReadUInt32();
//   entries.Add((name, length, offset));
//   itkReader.ReadByte();
// }

// foreach (var (name, length, offset) in entries)
// {
//   itkReader.BaseStream.Seek(offset, SeekOrigin.Begin);
//   var data = itkReader.ReadBytes((int)length);
//   var outputFile = Path.Combine(outputDir, name);
//   File.WriteAllBytes(outputFile, data);
// }

// BoltFileHelper.ExtractBoltData(@"C:\Dev\Gaming\CD-i\Games\MERLINS APPRENTICE\out\boltlib0.bin", @"C:\Dev\Gaming\CD-i\Games\MERLINS APPRENTICE\out\bolt0");

// var spriteFile = @"C:\Dev\Gaming\CD-i\Games\Alien Gate\GFX\out\signs.dat_1_1_0.bin";
// var sprReader = new BinaryReader(File.OpenRead(spriteFile));

// var palFile = @"C:\Dev\Gaming\CD-i\Games\Alien Gate\CMDS\out\cdi_gate_1_1_0.bin";
// var palData = File.ReadAllBytes(palFile).Skip(0x245E4).Take(0x300).ToArray();
// var palette = ColorHelper.ConvertBytesToRGB(palData);
// var unk = sprReader.ReadBigEndianInt16();
// var tableOffset = sprReader.ReadBigEndianUInt32() + 0x1c;

// sprReader.BaseStream.Seek(tableOffset, SeekOrigin.Begin);
// var namesAndOffsets = new List<(string name, uint offset)>();

// var name = Encoding.UTF8.GetString(sprReader.ReadBytes(8));
// while (!string.IsNullOrEmpty(name))
// {
//   sprReader.ReadBytes(2);
//   var offset = sprReader.ReadBigEndianUInt32() + 0x1c;
//   //sprReader.ReadBytes(0xE);
//   namesAndOffsets.Add((name, offset));
//   name = Encoding.ASCII.GetString(sprReader.ReadBytes(8)).Replace("\0", "");
// }

// namesAndOffsets = namesAndOffsets.OrderBy(i => i.offset).Where(i => i.offset >= 0x30).ToList();

// var outputDir = Path.Combine(Path.GetDirectoryName(spriteFile)!, Path.GetFileNameWithoutExtension(spriteFile) + "_output");
// Directory.CreateDirectory(outputDir);

// foreach (var ((fname, offset), index) in namesAndOffsets.WithIndex())
// {
//   sprReader.BaseStream.Seek(offset, SeekOrigin.Begin);
//   var nextOffset = (index < namesAndOffsets.Count - 1) ? namesAndOffsets[index + 1].offset : (uint)sprReader.BaseStream.Length;
//   var length = nextOffset - offset;
//   // Process sprite data at offset
//   var spriteData = sprReader.ReadBytes((int)length); // Adjust size as needed
//   try
//   {
//     var decodedData = CompiledSpriteHelper.DecodeCompiledSprite(spriteData);
//     var outputFile = Path.Combine(outputDir, $"{fname}_{offset:X8}.png");
//     var image = ImageFormatHelper.GenerateClutImage(palette, decodedData, 384, 200, true, 0);
//     // crop to 48x48 starting 1 pixel from the top (system.drawing)
//     var cropped = new Bitmap(48, 48);
//     using (var g = Graphics.FromImage(cropped))
//     {
//       g.DrawImage(image, new Rectangle(0, 0, 48, 48), new Rectangle(0, 1, 48, 48), GraphicsUnit.Pixel);
//     }

//     image.Save(outputFile, ImageFormat.Png);
//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"Error processing {fname} at offset {offset:X8}: {ex.Message}");
//   }
// }

// var mbmResFile = @"C:\Dev\Gaming\CD-i\Games\MBMWIN\SLW\SLEUTH.RES";
// var outputDir = Path.Combine(Path.GetDirectoryName(mbmResFile)!, "output");

// var mbmReader = new BinaryReader(File.OpenRead(mbmResFile));
// mbmReader.BaseStream.Seek(0x14, SeekOrigin.Begin);

// var offsetTableOffset = mbmReader.ReadUInt32();
// var offsetTableLength = mbmReader.ReadUInt32();

// mbmReader.ReadBytes(0x20);

// var nameTableOffset = mbmReader.ReadUInt32();
// var nameTableLength = mbmReader.ReadUInt32();

// var inputLbmDir = @"C:\Dev\Gaming\PC\Dos\Pac-in-Time_DOS_EN_Pac-in-Time-Version\PAC\spr";
// var lbmFiles = Directory.GetFiles(inputLbmDir, "*.LBM", SearchOption.TopDirectoryOnly);

// foreach (var lbmFile in lbmFiles)
// {
//   var outputPngPath = Path.ChangeExtension(lbmFile, ".png");
//   try
//   {
//     var converter = new LbmConverter();

//     // Use streams to handle file I/O
//     await using var lbmStream = File.OpenRead(lbmFile);
//     await using var pngStream = File.Create(outputPngPath);

//     await converter.ConvertAsync(lbmStream, pngStream);

//     Console.WriteLine("Conversion successful! 🎉");
//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"An error occurred during conversion: {ex.Message}");
//   }
// }


// var datDir = @"C:\Dev\Gaming\PC\Dos\Pac-in-Time_DOS_EN\dat";
// var datFiles = Directory.GetFiles(datDir, "*.bin");
// var outputDir = Path.Combine(datDir, "decompressed");
// if (!Directory.Exists(outputDir))
// {
//   Directory.CreateDirectory(outputDir);
// }
// foreach (var datFile in datFiles)
// {
//   try
//   {
//     var decompressed = FuryFormats.DecompressRle(datFile);
//     if (decompressed.Length == 0) continue;
//     File.WriteAllBytes(Path.Combine(outputDir, Path.GetFileNameWithoutExtension(datFile) + ".dc.bin"), decompressed);
//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"Error decompressing {datFile}: {ex.Message}");
//   }
// }

//var tileDir = @"C:\Dev\Gaming\PC\Dos\Pac-in-Time_DOS_EN\dec";
// var tileFiles = Directory.GetFiles(tileDir, "*.png");
// foreach (var tileFile in tileFiles)
// {
//   var filename = Path.GetFileNameWithoutExtension(tileFile);
//   // get last two chars as int
//   var id = int.Parse(filename.Substring(filename.Length - 2)) - 1;
//   var tileOutputDir = Path.Combine(Path.GetDirectoryName(tileFile)!, $"{id:00}_tiles_output");
//   FuryFormats.ExtractTilesAsPng(tileFile, tileOutputDir);
// }
// var spriteFolder = @"C:\Dev\Gaming\PC\Dos\Pac-in-Time_DOS_EN\SPR";
// var levelOutputDir = Path.Combine(@"C:\Dev\Gaming\PC\Dos\Pac-in-Time_DOS_EN\", "levels_output_new");
// if (!Directory.Exists(levelOutputDir))
// {
//   Directory.CreateDirectory(levelOutputDir);
// }
// var decompDatFiles = Directory.GetFiles(outputDir, "*.dc.bin");
// foreach (var decompDatFile in decompDatFiles)
// {
//   var df = new DataFile(decompDatFile);
//   var decId = df.TileSetId;
//   var tileFolder = Path.Combine(tileDir, $"{decId:00}_tiles_output");
//   var levelFolder = Path.Combine(levelOutputDir, $"{decId:00}_output");
//   Directory.CreateDirectory(levelFolder);
//   FuryFormats.AssembleLevelFromTileMap(df, tileFolder, spriteFolder, Path.Combine(levelFolder, $"{Path.GetFileNameWithoutExtension(decompDatFile)}.png"));
// }





// var inputLbmDir = @"C:\Dev\Gaming\PC\Dos\Pac-in-Time_DOS_EN\dec";
// var lbmFiles = Directory.GetFiles(inputLbmDir, "*.LBM", SearchOption.TopDirectoryOnly);

// foreach (var lbmFile in lbmFiles)
// {
//   var outputPngPath = Path.ChangeExtension(lbmFile, ".png");
//   try
//   {
//     var converter = new LbmConverter();

//     // Use streams to handle file I/O
//     await using var lbmStream = File.OpenRead(lbmFile);
//     await using var pngStream = File.Create(outputPngPath);

//     await converter.ConvertAsync(lbmStream, pngStream);

//     Console.WriteLine("Conversion successful! 🎉");
//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"An error occurred during conversion: {ex.Message}");
//   }
// }


// var testBin = @"C:\Dev\Gaming\PC\Dos\Games\The-Gene-Machine_DOS_EN_v103-Installer\Scratch\test2.bin";
// var testData = File.ReadAllBytes(testBin);
// var decoded = ResourceHelper.DecodeImage(testData);
// File.WriteAllBytes(Path.ChangeExtension(testBin, ".dc.bin"), decoded);

// var mainDat = @"C:\Dev\Gaming\PC\Dos\Games\GUILTY\GBG_MAIN.DAT";
// using var mainDataReader = new BinaryReader(File.OpenRead(mainDat));

// var dataLength = mainDataReader.ReadUInt16();
// var data = mainDataReader.ReadBytes(dataLength - 2);
// // xor each byte with 0x6F
// for (int i = 0; i < data.Length; i++)
// {
//   data[i] ^= 0x6F;
// }
// File.WriteAllBytes(Path.ChangeExtension(mainDat, ".dc.dat"), data);

// var aniFilesDir = @"C:\Dev\Gaming\PC\Win\Games\ThomasNewLine\Anis";
// var aniFiles = Directory.GetFiles(aniFilesDir, "*.ani");

// foreach (var aniFile in aniFiles)
// {
//   var aniReader = new BinaryReader(File.OpenRead(aniFile));
//   var count = aniReader.ReadUInt32();

//   var outputDir = Path.Combine(Path.GetDirectoryName(aniFile)!, "output", Path.GetFileNameWithoutExtension(aniFile));
//   Directory.CreateDirectory(outputDir);
//   for (int i = 0; i < count; i++)
//   {
//     aniReader.ReadInt16();
//     var length = aniReader.ReadUInt32();
//     aniReader.BaseStream.Seek(-6, SeekOrigin.Current);
//     var bmpData = aniReader.ReadBytes((int)length);
//     // create image sharp image from bmp data
//     try
//     {
//       using var image = Image.Load<Rgb24>(bmpData);
//       // convert to rgba32
//       using var rgbaImage = image.CloneAs<Rgba32>();
//       // make color 255,0,255 transparent
//       for (int y = 0; y < rgbaImage.Height; y++)
//       {
//         for (int x = 0; x < rgbaImage.Width; x++)
//         {
//           var pixel = rgbaImage[x, y];
//           if (pixel.R == 255 && pixel.G == 0 && pixel.B == 255)
//           {
//             pixel.A = 0;
//             rgbaImage[x, y] = pixel;
//           }
//         }
//       }
//       var outputFile = Path.Combine(outputDir, $"{i}.png");
//       rgbaImage.Save(outputFile);
//     }
//     catch (Exception ex)
//     {
//       Console.WriteLine($"Error processing {aniFile} frame {i}: {ex.Message}");
//     }
//   }
// }

// var dclDir = @"C:\Dev\Gaming\PC\Win\Games\UFOs\UFOS\";
// var dclOutputDir = Path.Combine(dclDir, "output");
// Directory.CreateDirectory(dclOutputDir);

// var datFiles = Directory.GetFiles(dclDir, "*.dat");

// foreach (var datFile in datFiles)
// {
//   dclOutputDir = Path.Combine(dclDir, "output", Path.GetFileNameWithoutExtension(datFile));
//   Directory.CreateDirectory(dclOutputDir);
//   UfoHelper.ExtractDataFile(datFile, dclOutputDir);
// }

// var daXFile = @"C:\Dev\Gaming\PC\Win\Games\TotoSapore\artematica\artematica.daX";
// var daXOutputDir = Path.Combine(Path.GetDirectoryName(daXFile)!, "output");
// Directory.CreateDirectory(daXOutputDir);
// using var daXReader = new BinaryReader(File.OpenRead(daXFile));
// daXReader.ReadBytes(4); // magic
// var count = daXReader.ReadUInt32();
// var fileList = new List<(uint id, string name, uint length, uint offset)>();
// for (int i = 0; i < count; i++)
// {
//   var id = daXReader.ReadUInt32();
//   var nameLen = daXReader.ReadUInt16();
//   var name = Encoding.UTF8.GetString(daXReader.ReadBytes(nameLen));
//   var length = daXReader.ReadUInt32();
//   var offset = daXReader.ReadUInt32();
//   Console.WriteLine($"ID: {id}, Name: {name}, Offset: 0x{offset:X8}, Length: 0x{length:X8}");
//   fileList.Add((id, name, length, offset));
// }

// var dataPos = daXReader.BaseStream.Position;

// foreach (var (id, name, length, offset) in fileList)
// {
//   daXReader.BaseStream.Seek(dataPos + offset, SeekOrigin.Begin);
//   var data = daXReader.ReadBytes((int)length);
//   var outputFile = Path.Combine(daXOutputDir, $"{name}");
//   Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
//   File.WriteAllBytes(outputFile, data);
// }

// var tabDir = @"C:\GOGGames\Magic Carpet Plus\CARPET.CD\game\CARPET\DATA\output\Images";
// var tabFiles = Directory.GetFiles(tabDir, "*.tab");

// var palFile0 = @"C:\GOGGames\Magic Carpet Plus\CARPET.CD\game\CARPET\DATA\output\Images\PAL0-0_decompressed.DAT";
// var palData0 = File.ReadAllBytes(palFile0);
// var palette0 = ColorHelper.ConvertBytesToRGB(palData0, true);

// var palFile1 = @"C:\GOGGames\Magic Carpet Plus\CARPET.CD\game\CARPET\DATA\output\Images\PAL1-0_decompressed.DAT";
// var palData1 = File.ReadAllBytes(palFile1);
// var palette1 = ColorHelper.ConvertBytesToRGB(palData1, true);

// foreach (var tabFile in tabFiles)
// {
//   using var tabReader = new BinaryReader(File.OpenRead(tabFile));
//   var offsetsWidthHeight = new List<(uint offset, byte width, byte height)>();
//   while (tabReader.BaseStream.Position < tabReader.BaseStream.Length)
//   {
//     var offset = tabReader.ReadUInt32();
//     var width = tabReader.ReadByte();
//     var height = tabReader.ReadByte();
//     offsetsWidthHeight.Add((offset, width, height));
//   }

//   var datFile = Path.ChangeExtension(tabFile, ".dat");
//   using var datReader = new BinaryReader(File.OpenRead(datFile));
//   var outputDir = Path.Combine(Path.GetDirectoryName(tabFile)!, "output", Path.GetFileNameWithoutExtension(tabFile));
//   Directory.CreateDirectory(outputDir);

//   foreach (var ((offset, width, height), index) in offsetsWidthHeight.WithIndex())
//   {
//     if (offset == 0 || width == 0 || height == 0) continue;
//     datReader.BaseStream.Seek(offset, SeekOrigin.Begin);
//     var imageData = new List<byte>();
//     while (imageData.Count < width * height)
//     {
//       var byteCount = datReader.ReadByte();
//       byteCount = byteCount >= 0xF0 ? datReader.ReadByte() : byteCount;
//       var bytes = datReader.ReadBytes(byteCount);
//       imageData.AddRange(bytes);
//       if (datReader.BaseStream.Position >= datReader.BaseStream.Length) break;
//       datReader.ReadByte(); // skip the 0 byte
//     }
//     var palette = tabFile.Contains("0") ? palette0 : palette1;
//     var image = ImageFormatHelper.GenerateClutImage(palette, imageData.ToArray(), width, height);
//     var outputFile = Path.Combine(outputDir, $"{index}.png");
//     image.Save(outputFile, ImageFormat.Png);
//   }
// }

// var ihnmaimsRes = @"C:\GOGGames\Inherit the Earth\ite.rsc";
// var outputDir = @"C:\GOGGames\Inherit the Earth\output";
// Directory.CreateDirectory(outputDir);

// using var br = new BinaryReader(File.OpenRead(ihnmaimsRes));

// // skip to filelength - 8
// br.BaseStream.Seek(-8, SeekOrigin.End);

// var listOffset = br.ReadUInt32();
// var count = br.ReadUInt32();

// Console.WriteLine($"List Offset: {listOffset:X8}, Count: {count}");

// br.BaseStream.Seek(listOffset, SeekOrigin.Begin);
// var fileEntries = new List<(uint Offset, uint Length)>();

// for (int i = 0; i < count; i++)
// {
//   var offset = br.ReadUInt32();
//   var length = br.ReadUInt32();
//   fileEntries.Add((offset, length));
// }

// foreach (var ((offset, length),index) in fileEntries.WithIndex())
// {
//   br.BaseStream.Seek(offset, SeekOrigin.Begin);
//   var data = br.ReadBytes((int)length);
//   var firstFour = data.Length > 4 ? BitConverter.ToUInt32(data.Take(4).ToArray()) : 0;
//   var fileName = $"file_{index}_{offset:X8}.bin";
//   var outputFile = Path.Combine(outputDir, fileName);
//   File.WriteAllBytes(outputFile, data);
//   Console.WriteLine($"Extracted {fileName} - {length:X8} bytes");
// }


// var resFile = @"C:\GOGGames\War Wind\Data\RES.004";
// var outputDir = Path.Combine(Path.GetDirectoryName(resFile)!, "RES004_output");
// Directory.CreateDirectory(outputDir);
// ResourceReader.ExtractResourceFile(resFile, outputDir);

// var palFiles = new string[] {
//   @"C:\GOGGames\War Wind\Data\RES004_output\015.bin",
//   @"C:\GOGGames\War Wind\Data\RES004_output\016.bin",
//   @"C:\GOGGames\War Wind\Data\RES004_output\017.bin",
//   @"C:\GOGGames\War Wind\Data\RES004_output\018.bin",
//   @"C:\GOGGames\War Wind\Data\RES004_output\023.bin",
//   @"C:\GOGGames\War Wind\Data\RES004_output\024.bin",
//   @"C:\GOGGames\War Wind\Data\RES004_output\025.bin",
//   @"C:\GOGGames\War Wind\Data\RES004_output\026.bin",
//   @"C:\GOGGames\War Wind\Data\RES004_output\090.bin",
//   @"C:\GOGGames\War Wind\Data\RES004_output\093.bin",
// };
// var palettes = new List<List<Color>>();
// foreach (var palFile in palFiles)
// {
//   var palData = File.ReadAllBytes(palFile).Skip(0x20).Take(0x300).ToArray();
//   var palette = ColorHelper.ConvertBytesToRGB(palData, true);
//   palettes.Add(palette);
// }


// var d3grDir = @"C:\GOGGames\War Wind\Data\RES004_output";
// var d3grFiles = Directory.GetFiles(d3grDir, "*.bin");

// foreach (var d3grFile in d3grFiles)
// {
//   foreach (var (defaultPalette, index) in palettes.WithIndex())
//   {
//     try
//     {
//       var outputDir = Path.Combine(Path.GetDirectoryName(d3grFile)!, "output", Path.GetFileNameWithoutExtension(d3grFile) + $"_pal_{index}_output");
//       D3grExtractor.ExtractD3grFiles(d3grFile, outputDir, defaultPalette, true);
//     }
//     catch (Exception ex)
//     {
//       Console.WriteLine($"Error processing {d3grFile}:\n {ex.Message}");
//     }
//   }
// }



// var ptcDir = @"C:\GOGGames\Galador";
// var ptcFiles = Directory.GetFiles(ptcDir, "*.ptc", SearchOption.AllDirectories);
// var outputDir = Path.Combine(ptcDir, "output_new");
// Directory.CreateDirectory(outputDir);

// foreach (var (ptc, index) in ptcFiles.WithIndex())
// {
//   var ptcFile = new PtcFile(ptc);
//   Console.WriteLine($"Processing {ptc} with {ptcFile.Entries.Count} entries, index {index}");
//   var ptcOutputDir = Path.Combine(outputDir, $"{index}");
//   Directory.CreateDirectory(ptcOutputDir);
//   ptcFile.ExtractEntries(ptcOutputDir);
//}                   

// var gargIndexFile = @"C:\GOG Games\Gargoyles Remastered\ATD_win32.mtc";
// var gargIndexData = File.ReadAllBytes(gargIndexFile).Skip(0x1F28).Take(0x31c0).ToArray();
// var gargIndexReader = new BinaryReader(new MemoryStream(gargIndexData));
// var offsetsAndLengths = new List<(uint offset, uint length)>();

// while (gargIndexReader.BaseStream.Position < gargIndexReader.BaseStream.Length)
// {
//   gargIndexReader.ReadBytes(8); // skip unknown 8 bytes
//   var offset = gargIndexReader.ReadUInt32();
//   var length = gargIndexReader.ReadUInt32();

//   offsetsAndLengths.Add((offset, length));
//   gargIndexReader.ReadBytes(16); // skip unknown 16 bytes
// }

// var gargDataFile = @"C:\GOG Games\Gargoyles Remastered\ATD_win32.mdf";
// var gargDataReader = new BinaryReader(File.OpenRead(gargDataFile));
// var gargOutputDir = Path.Combine(Path.GetDirectoryName(gargDataFile)!, "output");
// Directory.CreateDirectory(gargOutputDir);

// foreach (var (offset, length) in offsetsAndLengths)
// {
//   gargDataReader.BaseStream.Seek(offset, SeekOrigin.Begin);
//   var gargData = gargDataReader.ReadBytes((int)length);
//   var decryptedData = Gargoyles.DecryptAsset(gargData);
//   // check the magic bytes to see if it's a known format
//   var magic = Encoding.ASCII.GetString(decryptedData.Take(4).ToArray());
//   var outputFile = Path.Combine(gargOutputDir, $"{offset}.bin");
//   if (magic == "DDS ") outputFile = Path.ChangeExtension(outputFile, ".dds");
//   else if (magic == "D3DF") outputFile = Path.ChangeExtension(outputFile, ".d3d");
//   else if (magic == "D3DX") outputFile = Path.ChangeExtension(outputFile, ".d3dx");
//   else if (magic == "BM") outputFile = Path.ChangeExtension(outputFile, ".bmp");
//   else if (magic == "GIF8") outputFile = Path.ChangeExtension(outputFile, ".gif");
//   else if (magic == "JFIF") outputFile = Path.ChangeExtension(outputFile, ".jpg");
//   else if (magic == "RIFF" && decryptedData.Skip(8).Take(4).SequenceEqual(Encoding.ASCII.GetBytes("WAVE"))) Path.ChangeExtension(outputFile, ".wav");
//   else if (magic == "RIFF") continue;
//   // png
//   else if (decryptedData[0] == 0x89 && decryptedData[1] == 0x50 && decryptedData[2] == 0x4E && decryptedData[3] == 0x47) continue;
//   File.WriteAllBytes(outputFile, decryptedData);
// }

// var indexFile = @"C:\Dev\Gaming\PC\Dos\Extractions\IndyFOA\ATLANTIS_000_Unxored.bin";
// var dataFile = @"C:\Dev\Gaming\PC\Dos\Extractions\IndyFOA\ATLANTIS_001_Unxored.bin";

// var scummIndex = new ScummIndexFile(indexFile);
// var scummData = new ScummDataFile(scummIndex, dataFile);
// var mainOutDir = Path.Combine(Path.GetDirectoryName(indexFile)!, "output");

// foreach (var room in scummIndex.Rooms)
// {
//   scummData.ParseRoomData(room.RoomNumber, Path.Combine(mainOutDir, $"Room_{room.RoomNumber}_{room.RoomName}"));
// }









// var pcxDir = @"C:\Dev\Gaming\PC\Dos\Games\RequiresInvestigation\HOCUS_POCUS\HPINST\output";
// var pcxFiles = Directory.GetFiles(pcxDir, "*.PCX");
// var pcxOutputDir = Path.Combine(pcxDir, "pcx_output");
// Directory.CreateDirectory(pcxOutputDir);
// foreach (var pcxFile in pcxFiles)
// {
//   var pcxData = File.ReadAllBytes(pcxFile);
//   var image = ImageFormatHelper.ConvertPCX(pcxData);
//   var outputFile = Path.Combine(pcxOutputDir, $"{Path.GetFileNameWithoutExtension(pcxFile)}.png");
//   image.Save(outputFile, ImageFormat.Png);
//   image = ImageFormatHelper.ConvertPCX(pcxData, true);
//   outputFile = Path.Combine(pcxOutputDir, $"{Path.GetFileNameWithoutExtension(pcxFile)}_trans.png");
//   image.Save(outputFile, ImageFormat.Png);
// }

// var mapFile = @"C:\Dev\Gaming\PC\Dos\Games\RequiresInvestigation\HOCUS_POCUS\HPINST\output\E1L1.011";
// var mapReader = new BinaryReader(File.OpenRead(mapFile));
// var mapWidth = 0xf0;
// var mapHeight = 0x3c;

// var mapData = mapReader.ReadBytes(mapWidth * mapHeight);

// // each byte is a tile index, starting at 1, 0 is empty
// var mapString = new StringBuilder();
// // for each row in the map (0 to mapHeight-1) 
// for (int y = 0; y < mapHeight; y++)
// {
//   for (int x = 0; x < mapWidth; x++)
//   {
//     var index = mapData[y * mapWidth + x] +1;
//     mapString.Append(index.ToString().PadLeft(3) + ",");
//   }
//   mapString.AppendLine();
// }

// var mapOutputFile = @"C:\Dev\Gaming\PC\Dos\Games\RequiresInvestigation\HOCUS_POCUS\HPINST\output\E1L1.MAP3.txt";
// File.WriteAllText(mapOutputFile, mapString.ToString());

// var palFile = @"C:\Dev\Gaming\PC\Dos\Games\OCEAN\OCEAN.CD\SLEEP\output\ESLEEP\out\_CITY2_D.SPK";
// var palData = File.ReadAllBytes(palFile).Skip(0x18d).Take(0x2d0).ToArray();
// var palette = ColorHelper.ConvertBytesToRGB(palData, true);

// var tileFile = @"C:\Dev\Gaming\PC\Dos\Games\OCEAN\OCEAN.CD\SLEEP\output\ESLEEP\out\CITY2_D.BLK";

// var tWidth = 32;
// var tHeight = 32;

// var tileReader = new BinaryReader(File.OpenRead(tileFile));
// var outputDir = Path.Combine(Path.GetDirectoryName(tileFile)!, Path.GetFileNameWithoutExtension(tileFile) + "_tiles_output");
// Directory.CreateDirectory(outputDir);

// var tileIndex = 0;
// while (tileReader.BaseStream.Position < tileReader.BaseStream.Length)
// {
//   var tileData = tileReader.ReadBytes(tWidth * tHeight);
//   if (tileData.Length < tWidth * tHeight) break;
//   var image = ImageFormatHelper.GenerateClutImage(palette, tileData, tWidth, tHeight);
//   var outputFile = Path.Combine(outputDir, $"tile_{tileIndex}.png");
//   image.Save(outputFile, ImageFormat.Png);
//   tileIndex++;
// }

// var rncDir = @"C:\Dev\Gaming\PC\Dos\Games\OCEAN\OCEAN.CD\SLEEP";
// var rncFiles = Directory.GetFiles(rncDir, "*.RNC");

// foreach (var rncFile in rncFiles)
// {
//   var rncReader = new BinaryReader(File.OpenRead(rncFile));
//   rncReader.BaseStream.Seek(0x08, SeekOrigin.Begin);
//   var dataStart = rncReader.ReadBigEndianUInt16();
//   rncReader.ReadByte();
//   var nameAndOffsets = new List<(string name, uint offset)>();
//   while (rncReader.BaseStream.Position < dataStart-1)
//   {
//     var name = rncReader.ReadNullTerminatedString();
//     var offset = rncReader.ReadBigEndianUInt32();
//     nameAndOffsets.Add((name, offset));
//   }

//   nameAndOffsets = nameAndOffsets.OrderBy(n => n.offset).ToList();
//   var outputDir = Path.Combine(rncDir, "output", Path.GetFileNameWithoutExtension(rncFile));
//   Directory.CreateDirectory(outputDir);
//   for (int i = 0; i < nameAndOffsets.Count; i++)
//   {
//     var entry = nameAndOffsets[i];
//     var nextOffset = (i < nameAndOffsets.Count - 1) ? nameAndOffsets[i + 1].offset : (uint)rncReader.BaseStream.Length;
//     rncReader.BaseStream.Seek(entry.offset, SeekOrigin.Begin);
//     var length = nextOffset - entry.offset;
//     var data = rncReader.ReadBytes((int)length);
//     File.WriteAllBytes(Path.Combine(outputDir, entry.name), data);
//   }
// }

// var delTest = @"C:\Dev\Gaming\PC\Dos\Games\Nomad\Nomad93\anim\output\ar0001.del";
// var delData = File.ReadAllBytes(delTest);
// var decoded = NomadHelpers.DecodeDel(delData);
// File.WriteAllBytes(Path.ChangeExtension(delTest, ".raw"), decoded);


// var artFile = @"C:\Dev\Gaming\PC\Dos\Games\fate\TILES011.ART";
// var artReader = new BinaryReader(File.OpenRead(artFile));
// var outputDir = Path.Combine(Path.GetDirectoryName(artFile)!, "output_011");
// Directory.CreateDirectory(outputDir);

// var palFile = @"C:\Dev\Gaming\PC\Dos\Games\fate\PALETTE.DAT";
// var palData = File.ReadAllBytes(palFile).Take(0x300).ToArray();
// var palette = ColorHelper.ConvertBytesToRGB(palData, true);

// artReader.BaseStream.Seek(0x10, SeekOrigin.Begin);
// var heights = new List<ushort>();
// var widths = new List<ushort>();
// var unk1s = new List<ushort>();
// var unk2s = new List<ushort>();
// for (int i = 0; i < 255; i++)
// {
//   heights.Add(artReader.ReadUInt16());
// }
// for (int i = 0; i < 255; i++)
// {
//   widths.Add(artReader.ReadUInt16());
// }
// for (int i = 0; i < 255; i++)
// {
//   unk1s.Add(artReader.ReadUInt16());
// }
// for (int i = 0; i < 255; i++)
// {
//   unk2s.Add(artReader.ReadUInt16());
// }

// for (int i = 0; i < 255; i++)
// {
//   var data = artReader.ReadBytes(widths[i] * heights[i]);
//   if (data.Length == 0) continue;
//   var image = ImageFormatHelper.GenerateClutImage(palette, data, widths[i], heights[i], true, palette.Count - 1, false);
//   // flip vertically (System.Drawing)
//   image.RotateFlip(RotateFlipType.RotateNoneFlipY);
//   // rotate 90 degrees clockwise
//   image.RotateFlip(RotateFlipType.Rotate90FlipNone);
//   image.Save(Path.Combine(outputDir, $"image_{i}.png"), ImageFormat.Png);
// }

//FileHelpers.ResizeImagesInFolder(@"C:\Dev\Gaming\PC\Dos\Games\ANGST\angsthi\output\BioMedic", ExpansionOrigin.BottomCenter);


// var defaultPalFile = @"C:\Dev\Gaming\PC\Dos\Extractions\SQV\21.pal";
// var defaultPalData = File.ReadAllBytes(defaultPalFile).Skip(0x25).Take(0x400).ToArray();
// var defaultPalette = ColorHelper.ReadPalette(defaultPalData, false);
// SCIUtils.ExtractV56Files(@"C:\Dev\Gaming\PC\Dos\Extractions\SQV\v56", defaultPalette);

// var seqDir = @"C:\Dev\Gaming\PC\Dos\Extractions\SQV\out\images_v56_offset\4";
// FileHelpers.AlignSpriteSequences(seqDir, Path.Combine(seqDir, "aligned"));

// var inputDir = @"C:\Dev\Gaming\PC\Dos\Extractions\Ringworld\rlb_output\bitmaps";
// var files = Directory.GetFiles(inputDir, "*.bin");
// var palFiles = Directory.GetFiles(@"C:\Dev\Gaming\PC\Dos\Extractions\Ringworld\rlb_output\Palettes\Combined", "*.bin");

// var palFile = @"C:\Dev\Gaming\PC\Dos\Extractions\Ringworld\rlb_output\Palettes\0_RES_PALETTE_0_uncompressed.bin";
// var palData = File.ReadAllBytes(palFile).Skip(6).ToArray();
// var palette = ColorHelper.ConvertBytesToRGB(palData);
// var outputDir = Path.Combine(inputDir, "output_new");
// Directory.CreateDirectory(outputDir);

// foreach (var file in files)
// {
//   if (file.Contains("9999")) continue; var id = Path.GetFileName(file).Split('_')[0] + "_";
//   var fPalFile = palFiles.FirstOrDefault(p => Path.GetFileName(p).StartsWith(id));
//   if (fPalFile != null)
//   {
//     palData = File.ReadAllBytes(fPalFile).ToArray();
//     palette = ColorHelper.ConvertBytesToRGB(palData);
//   }
//   var fileData = File.ReadAllBytes(file);
//   var image = ImageFormatHelper.GenerateClutImage(palette, fileData.ToArray(), 160, 100);
//   var outputFile = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(file)}.png");
//   image.Save(outputFile, ImageFormat.Png);
// }

// foreach (var file in files)
// {
//   if (file.Contains("9999")) continue;
//   // extract int from filename, before the first underscore
//   var id = Path.GetFileName(file).Split('_')[0] + "_";
//   var fPalFile = palFiles.FirstOrDefault(p => Path.GetFileName(p).StartsWith(id));
//   if (fPalFile != null)
//   {
//     palData = File.ReadAllBytes(fPalFile).ToArray();
//     var newPalette = ColorHelper.ConvertBytesToRGB(palData);
//     palette.AddRange(newPalette);
//   }
//   using var br = new BinaryReader(File.OpenRead(file));
//   var count = br.ReadUInt16();
//   var fileOutputFolder = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(file));
//   Directory.CreateDirectory(fileOutputFolder);
//   var offsets = new List<uint>();
//   for (int i = 0; i < count; i++)
//   {
//     offsets.Add(br.ReadUInt32());
//   }

//   for (int i = 0; i < offsets.Count; i++)
//   {
//     var nextOffset = (i < offsets.Count - 1) ? offsets[i + 1] : (uint)br.BaseStream.Length;
//     br.BaseStream.Position = offsets[i];
//     // Process tileData...
//     var width = br.ReadUInt16();
//     var height = br.ReadUInt16();
//     var xOffset = br.ReadInt16();
//     var yOffset = br.ReadInt16();
//     var transIndex = br.ReadByte();
//     var flag = br.ReadByte();
//     var length = nextOffset - br.BaseStream.Position;
//     var rleData = br.ReadBytes((int)length);
//     var decodedData = (flag & 0x02) != 0 ? TsageImageFormats.DecodeRle(rleData, width, height, transIndex) : rleData;
//     var image = ImageFormatHelper.GenerateClutImage(palette, decodedData, width, height, true, transIndex, false, true);
//     var outputFile = Path.Combine(fileOutputFolder, $"{i}_{xOffset}_{yOffset}.png");
//     image.Save(outputFile, ImageFormat.Png);
//   }
//   //FileHelpers.AlignSprites(fileOutputFolder, Path.Combine(fileOutputFolder, "aligned"));  
// }

// CombineRingworldBG(outputDir);


// void CombineRingworldBG(string inputDir)
// {
//   // get all png files in the input dir
//   var files = Directory.GetFiles(inputDir, "*.png");
//   files = files.OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f).Split('_')[0])).ToArray();
//   // combine every 4 images into one. The order is top-left, bottom-left, top-right, bottom-right
//   var outputDir = Path.Combine(inputDir, "combined");
//   Directory.CreateDirectory(outputDir);
//   for (int i = 0; i < files.Length; i += 4)
//   {
//     var img1 = Image.Load<Rgba32>(files[i]);
//     var img2 = Image.Load<Rgba32>(files[i + 1]);
//     var img3 = Image.Load<Rgba32>(files[i + 2]);
//     var img4 = Image.Load<Rgba32>(files[i + 3]);

//     var combinedWidth = img1.Width + img3.Width;
//     var combinedHeight = img1.Height + img2.Height;

//     using var combinedImage = new Image<Rgba32>(combinedWidth, combinedHeight);

//     combinedImage.Mutate(ctx =>
//     {
//       ctx.DrawImage(img1, new Point(0, 0), 1f);
//       ctx.DrawImage(img2, new Point(0, img1.Height), 1f);
//       ctx.DrawImage(img3, new Point(img1.Width, 0), 1f);
//       ctx.DrawImage(img4, new Point(img1.Width, img1.Height), 1f);
//     });

//     var outputFileName = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(files[i])}_combined.png");
//     combinedImage.Save(outputFileName);
//   }
// }



// var plnFileDir = @"C:\Dev\Gaming\PC\Dos\Games\RequiresInvestigation\Daughter-of-Serpents_DOS_EN\daughter-of-serpents\DANTIS\DAUGHTER\GRAPHICS\";
// var outputDir = Path.Combine(Path.GetDirectoryName(plnFileDir)!, "output");
// Directory.CreateDirectory(outputDir);

// var plnFiles = Directory.GetFiles(plnFileDir, "*.PLN", SearchOption.TopDirectoryOnly);
// foreach (var plnFile in plnFiles)
// {

//   var plnData = File.ReadAllBytes(plnFile);
//   var palData = plnData.Take(0x300).ToArray();
//   var palette = ColorHelper.ConvertBytesToRGB(palData, true);

//   var planeList = new List<byte[]>();
//   for (int i = 0; i < 4; i++)
//   {
//     var planeData = plnData.Skip(0x300 + (i * 80 * 200)).Take(80 * 200).ToArray();
//     planeList.Add(planeData);
//   }

//   // Merge planes into a single image data array
//   var outputData = new List<byte>();
//   for (int i = 0; i < 80 * 200; i++)
//   {
//     var pixel1 = planeList[0][i];
//     var pixel2 = planeList[1][i];
//     var pixel3 = planeList[2][i];
//     var pixel4 = planeList[3][i];
//     outputData.Add(pixel1);
//     outputData.Add(pixel2);
//     outputData.Add(pixel3);
//     outputData.Add(pixel4);
//   }

//   var image = ImageFormatHelper.GenerateClutImage(palette, outputData.ToArray(), 320, 200, false);
//   var outputFile = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(plnFile)}.png");
//   image.Save(outputFile, ImageFormat.Png);
// }



// var defaultPalFile = @"C:\Dev\Gaming\PC\Dos\Games\ANVIL_10E\ANVIL\DATA\RES001_output\62.bin";
// var defaultPalData = File.ReadAllBytes(defaultPalFile).Skip(0x98).Take(0x300).ToArray();
// var defaultPalette = ColorHelper.ConvertBytesToRGB(defaultPalData, true);

// var dfgDir = @"C:\Dev\Gaming\PC\Dos\Games\ANVIL_10E\ANVIL\DATA\RES007_output";
// var dfgFiles = Directory.GetFiles(dfgDir, "*.bin");

// foreach (var dfgFile in dfgFiles)
// {
//   try
//   {
//     var outputDir = Path.Combine(Path.GetDirectoryName(dfgFile)!, "output", Path.GetFileNameWithoutExtension(dfgFile) + "_output");
//     D3grExtractor.ExtractD3grFiles(dfgFile, outputDir, defaultPalette, true);
//     FileHelpers.CropSpritesToOptimalSize(outputDir, Path.Combine(outputDir, "aligned"), 320, 200);
//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"Error processing {dfgFile}:\n {ex.Message}");
//   }
// }


// var resFile = @"C:\Dev\Gaming\PC\Dos\Games\ANVIL_10E\ANVIL\DATA\RES.009";
// var outputDir = Path.Combine(Path.GetDirectoryName(resFile)!, "RES009_output");
// Directory.CreateDirectory(outputDir);

// var resReader = new BinaryReader(File.OpenRead(resFile));
// var count = resReader.ReadUInt32();
// var offsets = new List<uint>();

// for (int i = 0; i < count; i++)
// {
//   offsets.Add(resReader.ReadUInt32());
// }

// for (int i = 0; i < count; i++)
// {
//   resReader.BaseStream.Seek(offsets[i], SeekOrigin.Begin);
//   var nextOffset = (i < count - 1) ? offsets[i + 1] : (uint)resReader.BaseStream.Length;
//   var length = nextOffset - offsets[i];
//   var data = resReader.ReadBytes((int)length);
//   File.WriteAllBytes(Path.Combine(outputDir, $"{i}.bin"), data);
// }

// //ZeldasAdventure.ExtractAll();

// // --- 1. CONFIGURATION ---
// // Define the dimensions of a single tile image.
// const int tileWidth = 384;
// const int tileHeight = 240;

// // Set the path to your folder of PNG tiles and where to save the output.
// // IMPORTANT: Replace these with your actual folder paths.
// string tilesFolderPath = @"C:\Dev\Gaming\CD-i\Games\Zelda\Analysis\Data\under\screens";
// string outputImagePath = @"C:\Dev\Gaming\CD-i\Games\Zelda\Analysis\Data\under\screens\water_shrine.png";

// // --- 2. IMAGE DATA ARRAY ---
// // This is your layout data, transcribed from the image you provided.
// // A jagged array (int[][]) is used here.
// int[][] imageData =
// [
//  new int[] {91, 92, 93,  0,  0,  0,  0,  0,  0,  0,  0,  0, 0},
//  new int[] {0,  0, 94,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0},
//  new int[] {0, 96, 95,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0},
//  new int[] {0, 97,  0,  0,  0,  0,108,109,110,  0,  0,  0,  0},
//  new int[] {0, 98, 99,100,  0,106,107,  0,111,112,113,  0,  0},
//  new int[] {0,  0,  0,101,102,105,  0,  0,  0,  0,114,115,116},
//  new int[] {0,  0,  0,  0,103,104,  0,  0,  0,  0,  0,  0,  0},
// ];

// // --- 3. LOAD TILE FILE PATHS ---
// // Get all .png files from the specified folder.
// // It's CRITICAL to sort them to ensure index 1 maps to the first file correctly.
// // Assumes files are named in a way that alphabetical sorting works (e.g., tile_001.png, tile_002.png).
// Console.WriteLine($"Loading tiles from: {tilesFolderPath}");
// var tileFiles = Directory.GetFiles(tilesFolderPath, "*.png")
//                          .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f))) // Custom sort based on numeric suffix
//                          .ToArray();

// if (tileFiles.Length == 0)
// {
//   Console.WriteLine("Error: No PNG files found in the specified directory.");
//   return;
// }
// Console.WriteLine($"Found {tileFiles.Length} tile images.");

// // --- 4. CREATE THE FINAL CANVAS ---
// // Calculate the dimensions of the final combined image.
// int numRows = imageData.Length;
// int numCols = imageData.Max(row => row.Length); // Handles non-rectangular arrays safely
// int finalWidth = numCols * tileWidth;
// int finalHeight = numRows * tileHeight;

// // Create a new blank image with a transparent background.
// // The 'using' statement ensures memory is properly managed.
// using var finalImage = new Image<Rgba32>(finalWidth, finalHeight);
// Console.WriteLine($"Creating final canvas of size {finalWidth}x{finalHeight} pixels.");

// // --- 5. STITCH THE IMAGES ---
// // Iterate over each cell in our imageData map.
// for (int row = 0; row < numRows; row++)
// {
//   for (int col = 0; col < imageData[row].Length; col++)
//   {
//     // Get the tile number from our map.
//     int tileNumber = imageData[row][col];

//     // If the number is 0, it's an empty space, so we skip it.
//     if (tileNumber == 0)
//     {
//       continue;
//     }

//     // The array is 1-based, but our file list is 0-based.
//     // So, we subtract 1 to get the correct file index.
//     int fileIndex = tileNumber - 1;

//     // Safety check: ensure the required tile exists.
//     if (fileIndex >= 0 && fileIndex < tileFiles.Length)
//     {
//       // Load the individual tile image.
//       using var tileImage = Image.Load(tileFiles[fileIndex]);

//       // Calculate the position to draw this tile on the final canvas.
//       var location = new Point(col * tileWidth, row * tileHeight);

//       // Use Mutate to draw the tile onto the canvas.
//       // The '1f' means full opacity.
//       finalImage.Mutate(ctx => ctx.DrawImage(tileImage, location, 1f));
//     }
//     else
//     {
//       Console.WriteLine($"Warning: Tile number {tileNumber} is out of bounds and was skipped.");
//     }
//   }
// }

// // --- 6. SAVE THE RESULT ---
// // Save the composed image to the specified output path.
// Console.WriteLine($"Saving final image to: {outputImagePath}");
// finalImage.Save(outputImagePath);
// Console.WriteLine("Done!");



//var lbaSpriteFile = @"C:\Dev\Gaming\PC\Dos\Games\LBA\LBA\sprites_output_2\0_dc.bin";
// var lbaSpriteData = File.ReadAllBytes(lbaSpriteFile);
// var decodedSprite = DecodeLBASprite(lbaSpriteData, 0x8);
// File.WriteAllBytes(Path.ChangeExtension(lbaSpriteFile, ".raw"), decodedSprite);

// var hqrFile = @"C:\Dev\Gaming\PC\Dos\Games\LBA\LBA\SPRITES.HQR";
// var outputFolder = Path.Combine(Path.GetDirectoryName(hqrFile)!, "sprites_output_2");
// Directory.CreateDirectory(outputFolder);
// ExtractHqr(hqrFile, outputFolder);

// static void ExtractHqr(string hqrFile, string outputFolder)
// {
//   var hqrData = File.ReadAllBytes(hqrFile);
//   var offsets = new List<uint>();
//   var headerSize = BitConverter.ToUInt32(hqrData.Take(4).ToArray(), 0);
//   using var ms = new BinaryReader(new MemoryStream(hqrData));
//   while (ms.BaseStream.Position < headerSize)
//   {
//     var offset = ms.ReadUInt32();
//     if (offset == 0) continue;
//     offsets.Add(offset);
//   }
//   // iterate through offsets and get data, last offset is the file size
//   for (var i = 0; i < offsets.Count - 1; i++)
//   {
//     var offset = offsets[i];
//     var length = offsets[i + 1] - offset;
//     var realSize = ms.ReadUInt32();
//     var compSize = ms.ReadUInt32();
//     var mode = ms.ReadUInt16();
//     var data = ms.ReadBytes((int)length - 10);
//     if (mode == 0)
//     {
//       File.WriteAllBytes(Path.Combine(outputFolder, $"{i}.bin"), data);
//     }
//     else
//     {
//       var decompressed = DecompressHqr(data, (int)realSize, mode);
//       File.WriteAllBytes(Path.Combine(outputFolder, $"{i}_dc.bin"), decompressed);
//     }
//   }
// }

// static byte[] DecompressHqr(byte[] dat, int decompressedSize, int mode)
// {
//   var output = new List<byte>();
//   using (var ms = new BinaryReader(new MemoryStream(dat)))
//     do
//     {
//       var b = ms.ReadByte();
//       for (int i = 0; i < 8; i++)
//       {
//         if ((b & (1 << i)) == 0)
//         {
//           var offset = ms.ReadUInt16();
//           var length = (offset & 0x0F) + mode + 1;
//           var lookbackOffset = output.Count - (offset >> 4) - 1;
//           for (var j = 0; j < length; j++)
//           {
//             output.Add(output[lookbackOffset++]);
//           }
//         }
//         else
//         {
//           output.Add(ms.ReadByte());
//         }
//         if (output.Count >= decompressedSize) return output.ToArray();
//       }
//     } while (output.Count < decompressedSize);
//   return output.ToArray();
// }

// static byte[] DecodeLBASprite(byte[] compressedData, uint offset)
// {
//   using var reader = new BinaryReader(new MemoryStream(compressedData));
//   reader.BaseStream.Seek(offset, SeekOrigin.Begin);

//   var width = reader.ReadByte();
//   var height = reader.ReadByte();
//   var offsetX = reader.ReadByte();
//   var offsetY = reader.ReadByte();

//   var outputSize = height * width;
//   var output = new byte[outputSize];
//   var lastPosition = (height - 1) * width + width;// This matches getBasePtr(width, height-1) in C++

//   for (int y = 0; y < height; ++y)
//   {
//     byte numRuns = reader.ReadByte();
//     int x = 0;
//     for (byte run = 0; run < numRuns; ++run)
//     {
//       byte runSpec = reader.ReadByte();
//       byte runLength = (byte)(Utils.GetBitsFromByte(runSpec, 0, 6) + 1);
//       byte type = (byte)Utils.GetBitsFromByte(runSpec, 6, 2);

//       if (type == 1)
//       {
//         // Read individual bytes for each pixel in the run
//         int startPos = y * width + x;
//         for (byte j = 0; j < runLength; ++j)
//         {
//           int currentPos = startPos + j;
//           if (currentPos >= lastPosition)
//           {
//             return output; // Bounds check equivalent to C++ pointer check
//           }
//           output[currentPos] = reader.ReadByte();
//         }
//       }
//       else if (type != 0)
//       {
//         // Fill run with a single repeated byte
//         int startPos = y * width + x;
//         int endPos = y * width + x + runLength; // One past the last position to write

//         if (endPos > lastPosition)
//         {
//           return output; // Bounds check equivalent to C++ pointer check
//         }

//         byte fillValue = reader.ReadByte();
//         for (int j = 0; j < runLength; ++j)
//         {
//           output[startPos + j] = fillValue;
//         }
//       }
//       x += runLength;
//     }
//   }
//   return output;
// }


// var algFile = @"C:\Dev\Gaming\PC\Dos\Games\DRASCULA\Packet\ag.bin";
// var algReader = new BinaryReader(File.OpenRead(algFile));
// var count = algReader.ReadUInt32();
// for (int i = 0; i < count; i++)
// {
//   var length = algReader.ReadUInt32();
//   var algData = algReader.ReadBytes((int)length);
//   var decodedData = DecodeDrasculaRLE(algData);
//   var palData = algReader.ReadBytes(0x300);
//   var palette = ColorHelper.ConvertBytesToRGB(palData, true);
//   var image = ImageFormatHelper.GenerateClutImage(palette, decodedData, 320, 200, false);
//   var outputDir = Path.Combine(Path.GetDirectoryName(algFile)!, Path.GetFileNameWithoutExtension(algFile) + "_output");
//   Directory.CreateDirectory(outputDir);
//   var outputFile = Path.Combine(outputDir, $"image_{i}.png");
//   image.Save(outputFile, ImageFormat.Png);
// }

// byte[] DecodeDrasculaRLE(byte[] compressedData, ushort pitch = 320)
// {
//   bool stopProcessing = false;
//   byte pixel;
//   uint repeat;
//   int curByte = 0, curLine = 0;
//   pitch -= 320;
//   var output = new List<byte>();
//   using var reader = new BinaryReader(new MemoryStream(compressedData));

//   while (!stopProcessing)
//   {
//     pixel = reader.ReadByte();
//     repeat = 1;
//     if ((pixel & 192) == 192)
//     {
//       repeat = (uint)(pixel & 63);
//       pixel = reader.ReadByte();
//     }
//     for (uint j = 0; j < repeat; j++)
//     {
//       output.Add(pixel);
//       if (++curByte >= 320)
//       {
//         curByte = 0;
//         output.AddRange(Enumerable.Repeat((byte)0, pitch));
//         if (++curLine >= 200)
//         {
//           stopProcessing = true;
//           break;
//         }
//       }
//     }
//   }
//   return output.ToArray();
// }



// var folderToResize = @"C:\Dev\Gaming\Apps\unwrs\hades_wrs\output\tu";
// FileHelpers.ResizeImagesInFolder(folderToResize, ExpansionOrigin.BottomCenter);

// var testFile = @"C:\Dev\Gaming\PC\Win\Games\VOODOO\Test\test.bin";

// var testData = File.ReadAllBytes(testFile);

// var decodedData = VoodooAssetSystem.BitwiseDecompressor.Decompress(testData, 0x94ef);

// var outputFile = @"C:\Dev\Gaming\PC\Win\Games\VOODOO\Test\output.bin";
// File.WriteAllBytes(outputFile, decodedData);

// var resPath = @"C:\Dev\Gaming\PC\Win\Games\HopkinsUK\Hopkins4\BUFFER\PIC.RES";

// ResExtractor.ExtractResources(resPath, Path.Combine(Path.GetDirectoryName(resPath)!, Path.GetFileNameWithoutExtension(resPath) + "output"));

// // var pcxBytes = File.ReadAllBytes(pcxPath);
// // var imageO = ImageFormatHelper.ConvertPCX(pcxBytes);

// var indexFile = @"C:\Dev\Gaming\PC\Dos\Games\DIG1\DIG\DIG.LA0";

// var scummIndex = new ScummIndexFile(indexFile);

// var mainFile = @"C:\Dev\Gaming\PC\Dos\Games\DIG1\DIG\DIG.LA1";

// var outputFolder = @"C:\Dev\Gaming\PC\Dos\Games\DIG1\DIG\Output";
// Directory.CreateDirectory(outputFolder);

// var scummFile = new ScummDataFile(scummIndex, mainFile);

// for (int i = 0; i < scummFile.Table.NumOfRooms; i++)
// {
//   var room = scummFile.Index.Rooms[i];
//   scummFile.ParseRoomData(room.RoomNumber, outputFolder);
// }



// var folderToResize = @"C:\GOG Games\Take No Prisoners\output\SPRITES\output\SECURITY";
// var tOutputDir = Path.Combine(folderToResize, "output");
// FileHelpers.AlignSprites(folderToResize, tOutputDir);

// var folderToResize = @"C:\Dev\Gaming\PC\Dos\Games\Chill-Manor_DOS_EN\DATA\RES\output\RESFACE\output\New folder";

// FileHelpers.ResizeImagesInFolder(folderToResize, ExpansionOrigin.BottomCenter);

// Debugger.Break();


// var rleDir = @"C:\GOG Games\Take No Prisoners\output\FONTS";

// var outputDir = @"C:\GOG Games\Take No Prisoners\output\STATBAR\FONTS\out";
// Directory.CreateDirectory(outputDir);

// var rleFiles = Directory.GetFiles(rleDir, "*.SFD");
// var palFile = @"C:\GOG Games\Take No Prisoners\output\VAMPIRE.PAL";
// var palData = File.ReadAllBytes(palFile);
// var pal = ColorHelper.ConvertBytesToRGB(palData);

// foreach (var relFile in rleFiles)
// {
//   try
//   {
//     var fileOutputDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(relFile));
//     Directory.CreateDirectory(fileOutputDir);
//     var compressedData = File.ReadAllBytes(relFile);
//     var decompressedData = RleDecompress(compressedData);
//     using var reader = new BinaryReader(new MemoryStream(decompressedData));
//     var count = reader.ReadUInt32();
//     var offsets = new uint[count];
//     for (int i = 0; i < count; i++)
//     {
//       offsets[i] = reader.ReadUInt32();
//     }

//     for (int i = 0; i < count; i++)
//     {
//       reader.BaseStream.Seek(offsets[i], SeekOrigin.Begin);
//       var width = reader.ReadUInt16();
//       var height = reader.ReadUInt16();
//       var xOffset = reader.ReadInt16();
//       var yOffset = reader.ReadInt16();
//       var imageData = reader.ReadBytes(width * height);
//       var image = ImageFormatHelper.GenerateClutImage(pal, imageData, width, height, true);
//       var outputFile = Path.Combine(fileOutputDir, $"{Path.GetFileNameWithoutExtension(relFile)}_{i}_{xOffset}_{yOffset}.png");
//       image.Save(outputFile, ImageFormat.Png);
//     }
//     if (count <= 1) continue;
//     var outDir = Path.Combine(fileOutputDir, "resized");
//     Directory.CreateDirectory(outDir);

//     FileHelpers.AlignSprites(fileOutputDir, outDir);

//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"Error processing {relFile}: {ex.Message}");
//   }
// }

// /// <summary>
// /// Decompresses data using a specific RLE algorithm.
// /// </summary>
// /// <param name="compressedData">The byte array containing the compressed data.</param>
// /// <returns>A byte array with the decompressed data.</returns>
// /// <exception cref="InvalidDataException">Thrown if the compressed data is malformed.</exception>
// static byte[] RleDecompress(byte[] compressedData)
// {
//   if (compressedData == null || compressedData.Length < 6)
//   {
//     throw new InvalidDataException("Compressed data is null or too short.");
//   }

//   byte compressionType = compressedData[0];
//   if (compressedData[1] == 0)
//   {
//     // not compressed
//     return compressedData;
//   }
//   // Check for a specific compression signature
//   if (compressionType >= 8 || compressedData[1] != 1)
//   {
//     throw new InvalidDataException("Invalid compression signature.");
//   }

//   // Get the decompressed size from the next 4 bytes
//   int decompressedSize = BitConverter.ToInt32(compressedData, 2);
//   if (decompressedSize == 0)
//   {
//     return Array.Empty<byte>();
//   }

//   var decompressedData = new byte[decompressedSize];
//   int outputIndex = 0;
//   int inputOffset = 6;

//   // Main decompression loop
//   while (outputIndex < decompressedSize)
//   {
//     // Check for input buffer overflow
//     if (compressedData.Length <= inputOffset)
//     {
//       throw new InvalidDataException("Unexpected end of compressed data stream.");
//     }

//     // Read the control byte
//     byte controlByte = compressedData[inputOffset];

//     // If the top 3 bits match the compression type, it's a run
//     if ((controlByte >> 5) == compressionType)
//     {
//       // Check if there's enough data for the run value
//       if (compressedData.Length <= inputOffset + 1)
//       {
//         throw new InvalidDataException("Unexpected end of stream when reading a run.");
//       }

//       byte runValue = compressedData[inputOffset + 1];
//       // The lower 5 bits of the control byte are the run length
//       int runLength = controlByte & 0x1F;

//       // Check for output buffer overflow before writing
//       if (outputIndex + runLength > decompressedSize)
//       {
//         throw new InvalidDataException("Run would exceed decompressed size.");
//       }

//       // Write the run to the output
//       for (int i = 0; i < runLength; i++)
//       {
//         decompressedData[outputIndex++] = runValue;
//       }

//       // Advance the input offset by 2 (control byte + value byte)
//       inputOffset += 2;
//     }
//     else
//     {
//       // It's a literal, so just copy the byte
//       decompressedData[outputIndex++] = controlByte;
//       // Advance the input offset by 1
//       inputOffset++;
//     }
//   }

//   // Optional: Check for a specific end-of-stream marker for validation
//   if (inputOffset < compressedData.Length && compressedData[inputOffset] == 0x97 && outputIndex == decompressedSize)
//   {
//     // Success
//   }
//   else if (outputIndex != decompressedSize)
//   {
//     throw new InvalidDataException("Decompression resulted in a different size than expected.");
//   }

//   return decompressedData;
// }




// var vpkFile = @"C:\GOG Games\Take No Prisoners\TNP.VPK";

// using var vReader = new BinaryReader(File.OpenRead(vpkFile));

// vReader.BaseStream.Seek(0x28, SeekOrigin.Begin);

// var fileTableOffset = vReader.ReadUInt32();

// vReader.BaseStream.Seek(fileTableOffset, SeekOrigin.Begin);

// while (vReader.BaseStream.Position < vReader.BaseStream.Length)
// {
//   var entry = new VpkEntry
//   {
//     Name = new string(vReader.ReadChars(0x38)).TrimEnd((char)0x01).TrimEnd('\0'),
//     Offset = vReader.ReadUInt32(),
//     Size = vReader.ReadUInt32()
//   };
//   if (string.IsNullOrEmpty(entry.Name)) break;
//   Console.WriteLine($"{entry.Name} - {entry.Offset:X8} - {entry.Size:X8}");
//   var outputDir = Path.Combine(Path.GetDirectoryName(vpkFile)!, "output");
//   var outputPath = Path.Combine(outputDir, entry.Name);
//   // ensure full dir path exists
//   Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

//   var currentPos = vReader.BaseStream.Position;

//   vReader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
//   var data = vReader.ReadBytes((int)entry.Size);
//   File.WriteAllBytes(outputPath, data);
//   vReader.BaseStream.Seek(currentPos, SeekOrigin.Begin);
// }

// class VpkEntry
// {
//   public string Name { get; set; } = string.Empty;
//   public uint Offset { get; set; }
//   public uint Size { get; set; }
// }

// var xarcFile = @"C:\Dev\Gaming\PC\Win\Games\TLJ\install\1a\1a.xarc";

// var xarc = new Xarc(xarcFile);

// Console.WriteLine($"XARC File: {xarc.FilePath}");
// Console.WriteLine($"Entry Count: {xarc.Entries.Count}");
// foreach (var entry in xarc.Entries)
// {
//   Console.WriteLine($" - {entry.Name} (Offset: {entry.Offset}, Size: {entry.Size})");
//   var outputDir = Path.Combine(Path.GetDirectoryName(xarcFile)!, "output");
//   Directory.CreateDirectory(outputDir);
//   File.WriteAllBytes(Path.Combine(outputDir, entry.Name), entry.Data);
// }

// var folderToResize = @"C:\Dev\Gaming\Sega\Saturn\Games\BLAZING_DRAGONS\SYSTEM\actor_output\S00H2";

// FileHelpers.ResizeImagesInFolder(folderToResize, ExpansionOrigin.TopRight);


// var actorDir = @"C:\Dev\Gaming\Sega\Saturn\Games\BLAZING_DRAGONS\SYSTEM";
// var actorFiles = Directory.GetFiles(actorDir, "*.ACT", SearchOption.TopDirectoryOnly);

// foreach (var actorFile in actorFiles)
// {
//   try
//   {
//     var images = BlazingDragons.ParseActorFile(actorFile);
//     var outputDir = Path.Combine(Path.GetDirectoryName(actorFile)!, "actor_output_2", Path.GetFileNameWithoutExtension(actorFile));
//     Directory.CreateDirectory(outputDir);
//     foreach (var (image, index) in images.WithIndex())
//     {
//       image.Save(Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(actorFile)}_{index}.png"), ImageFormat.Png);
//     }
//   }
//   catch (Exception ex)
//   {
//     Debug.WriteLine($"Error processing actor file {actorFile}: {ex.Message}");
//   }
// }

// var test = @"C:\Dev\Gaming\Sega\Saturn\Games\BLAZING_DRAGONS\SYSTEM\S02b.SCR";
// var testData = File.ReadAllBytes(test);
// // var outputDir = @"C:\Dev\Gaming\Sega\Saturn\Games\BLAZING_DRAGONS\SYSTEM\S02b_output";
// // Directory.CreateDirectory(outputDir);

// var palData = testData.Take(0x300).ToArray();
// palData[0] = 0; // ensure first color is transparent
// palData[1] = 0;
// var mainPal = ColorHelper.ReadRgb15Palette(palData.Take(0x200).ToArray());
// var subPal = ColorHelper.ReadRgb15Palette(palData.Skip(0x200).Take(0x100).ToArray());

// var tileData = testData.Skip(0x2264).ToArray();
// var tiles = new List<byte[]>();
// for (int i = 0, j = 0; i < tileData.Length; i += 64, j++)
// {
//   var tile = tileData.Skip(i).Take(64).ToArray();
//   tiles.Add(tile);
// }

// var map1data = testData.Skip(0x324).Take(0x7d0).ToArray();
// var map2data = testData.Skip(0xaf4).Take(0x7d0).ToArray();
// var map3data = testData.Skip(0x12c4).Take(0x7d0).ToArray();
// var map4data = testData.Skip(0x1a94).Take(0x7d0).ToArray();

// // convert to short array from big endian byte array
// var map1shorts = map1data.Select((b, i) => new { b, i })
//     .GroupBy(x => x.i / 2)
//     .Select(g => (short)((g.ElementAt(0).b << 8) | g.ElementAt(1).b))
//     .ToArray();

// var map2shorts = map2data.Select((b, i) => new { b, i })
//     .GroupBy(x => x.i / 2)
//     .Select(g => (short)((g.ElementAt(0).b << 8) | g.ElementAt(1).b))
//     .ToArray();

// var map3shorts = map3data.Select((b, i) => new { b, i })
//     .GroupBy(x => x.i / 2)
//     .Select(g => (short)((g.ElementAt(0).b << 8) | g.ElementAt(1).b))
//     .ToArray();

// var map4shorts = map4data.Select((b, i) => new { b, i })
//     .GroupBy(x => x.i / 2)
//     .Select(g => (short)((g.ElementAt(0).b << 8) | g.ElementAt(1).b))
//     .ToArray();

// var image = ImageFormatHelper.CreateScreenImage(tiles, map1shorts, 0x28, 0x19, 8, 8, mainPal);
// image.Save(Path.Combine(outputDir, "S02b_map1.png"), ImageFormat.Png);

// image = ImageFormatHelper.CreateScreenImage(tiles, map2shorts, 0x28, 0x19, 8, 8, mainPal);
// image.Save(Path.Combine(outputDir, "S02b_map2.png"), ImageFormat.Png);

// image = ImageFormatHelper.CreateScreenImage(tiles, map3shorts, 0x28, 0x19, 8, 8, mainPal,true);
// image.Save(Path.Combine(outputDir, "S02b_map3.png"), ImageFormat.Png);

// image = ImageFormatHelper.CreateScreenImage(tiles, map4shorts, 0x28, 0x19, 8, 8, mainPal);
// image.Save(Path.Combine(outputDir, "S02b_map4.png"), ImageFormat.Png);

// var folderToResize = @"C:\Dev\Gaming\PC\Win\Games\DARBY\DATA\bmp_output\PAGE03\dragon";

// FileHelpers.ResizeImagesInFolder(folderToResize, ExpansionOrigin.BottomCenter);

// var rscDir = @"C:\Dev\Gaming\PC\Win\Games\lotc\W4";
// var rscPaths = Directory.GetFiles(rscDir, "*.RSC", SearchOption.AllDirectories);

// foreach (var rscPath in rscPaths)
// {
//   try
//   {
//     var rsc = new RscFile(rscPath);

//     var rscReader = new BinaryReader(File.OpenRead(rscPath));

//     var anims = rsc.AnimResources;
//     var sb = new StringBuilder();
//     foreach (var (anim, index) in anims.WithIndex())
//     {
//       rscReader.BaseStream.Seek(anim.Offset + 0x10, SeekOrigin.Begin);
//       var remainingFileSize = rscReader.BaseStream.Length - rscReader.BaseStream.Position;
//       if (anim.Size > remainingFileSize) anim.Size = (uint)remainingFileSize;
//       if (anim.Size == remainingFileSize) anim.Size -= 1;
//       var animData = rscReader.ReadBytes((int)anim.Size);
//       sb.AppendLine($"Parsing animation {index}");
//       sb.AppendLine();
//       ComposerAnimationParser.ReadAnimation(animData, sb);
//       sb.AppendLine();
//       sb.AppendLine($"Parsed animation {index}");
//     }

//     // write sb to text file
//     var logOutputPath = Path.Combine(Path.GetDirectoryName(rscPath)!, $"{Path.GetFileNameWithoutExtension(rscPath)}_animations.txt");
//     File.WriteAllText(logOutputPath, sb.ToString());

//     continue;

//     var palRes = rsc.PaletteResources.FirstOrDefault();
//     var palOffset = palRes?.Offset + 0x10;

//     if (palOffset != null)
//     {
//       rscReader.BaseStream.Seek(palOffset.Value, SeekOrigin.Begin);
//     }

//     var palLength = rscReader.ReadUInt16();
//     var palData = rscReader.ReadBytes(palLength * 3);

//     var palette = ColorHelper.ConvertBytesToRGB(palData);

//     var bmpResources = rsc.BmpResources;
//     var outputDir = Path.Combine(Path.GetDirectoryName(rscPath)!, Path.GetFileNameWithoutExtension(rscPath));
//     Directory.CreateDirectory(outputDir);
//     foreach (var (bmpRes, index) in bmpResources.WithIndex())
//     {
//       rscReader.BaseStream.Seek(bmpRes.Offset+0x10, SeekOrigin.Begin);
//       var type = rscReader.ReadUInt16();
//       var height = rscReader.ReadUInt16();
//       var width = rscReader.ReadUInt16();
//       var length = rscReader.ReadInt32();
//       switch (type & 0xFF)
//       {
//         case 0x00:
//           {
//             // uncompressed
//             var data = rscReader.ReadBytes(length);
//             var image = ImageFormatHelper.GenerateClutImage(palette, data, width, height);
//             // flip image vertically
//             image.RotateFlip(RotateFlipType.RotateNoneFlipY);
//             image.Save(Path.Combine(outputDir, $"{bmpRes.Id}.png"), ImageFormat.Png);
//             break;
//           }
//         case 0x01:
//           {
//             var decompressedData = ImageFormats.DecompressSpp32(rscReader, length, width, height);
//             var image = ImageFormatHelper.GenerateClutImage(palette, decompressedData, width, height, true);
//             image.RotateFlip(RotateFlipType.RotateNoneFlipY);
//             image.Save(Path.Combine(outputDir, $"{bmpRes.Id}.png"), ImageFormat.Png);
//             break;
//           }
//         case 0x03:
//           {
//             var decompressedData = ImageFormats.DecompressSlw8(rscReader, length, width, height);
//             var image = ImageFormatHelper.GenerateClutImage(palette, decompressedData, width, height, true);
//             image.RotateFlip(RotateFlipType.RotateNoneFlipY);
//             image.Save(Path.Combine(outputDir, $"{bmpRes.Id}.png"), ImageFormat.Png);
//             break;
//           }
//         case 0x04:
//           { // SLWM compressed
//             rscReader.ReadBytes(4);
//             try
//             {
//               var compressedData = rscReader.ReadBytes(length - 4);
//               var decompressedData = ImageFormats.DecompressSLWM(compressedData);
//               var imageData = GenerateImageData(decompressedData, width, height);
//               var image = ImageFormatHelper.GenerateClutImage(palette, imageData, width, height, true);
//               image.RotateFlip(RotateFlipType.RotateNoneFlipY);
//               image.Save(Path.Combine(outputDir, $"{bmpRes.Id}.png"), ImageFormat.Png);
//             }
//             catch (Exception ex)
//             {
//               Debug.WriteLine($"Error processing SLWM BMP {bmpRes.Id}, in file {rscPath}: {ex.Message}");
//             }
//             break;
//           }
//           case 0x05:
//           {
//             var typeFolder = Path.Combine(outputDir, "type5");
//             Directory.CreateDirectory(typeFolder);
//             var compressedData = rscReader.ReadBytes(length);
//             var decompressedData = ImageFormats.DecompressSLWM(compressedData);
//             var image = ImageFormatHelper.GenerateClutImage(palette, decompressedData, width, height, true);
//             image.RotateFlip(RotateFlipType.RotateNoneFlipY);
//             image.Save(Path.Combine(typeFolder, $"{bmpRes.Id}.png"), ImageFormat.Png);
//             break;
//           }
//         default:
//           Debug.WriteLine($"Unknown BMP type: {type:X4}");
//           continue;
//       }
//       // Process BMP data...
//     }

//   }
//   catch (Exception ex)
//   {
//     Debug.WriteLine($"Error processing RSC file {rscPath}: {ex.Message}");
//   }
// }

// // Assuming width and height are known. For PAGE03.RSC, it's 320x200.
// byte[] GenerateImageData(byte[] tempBuf, int width, int height)
// {
//   var pixelData = new byte[width * height];
//   int pixelDataIndex = 0;

//   int instrPos = tempBuf[0] + 1;
//   instrPos += BitConverter.ToUInt16(tempBuf, instrPos) + 2;
//   int instrIndex = instrPos;

//   for (int line = 0; line < height; line++)
//   {
//     int pixels = 0;
//     while (pixels < width)
//     {
//       byte data = tempBuf[instrIndex++];
//       byte color = tempBuf[(data & 0x7F) + 1];

//       if ((data & 0x80) == 0)
//       {
//         if (pixelDataIndex < pixelData.Length)
//         {
//           pixelData[pixelDataIndex++] = color;
//         }
//         pixels++;
//       }
//       else
//       {
//         byte count = tempBuf[instrIndex++];
//         if (count == 0)
//         {
//           while (pixels < width)
//           {
//             if (pixelDataIndex < pixelData.Length)
//             {
//               pixelData[pixelDataIndex++] = color;
//             }
//             pixels++;
//           }
//           break;
//         }
//         else
//         {
//           for (int i = 0; i < count; i++)
//           {
//             if (pixelDataIndex < pixelData.Length)
//             {
//               pixelData[pixelDataIndex++] = color;
//             }
//             pixels++;
//           }
//         }
//       }
//     }
//   }
//   return pixelData;
// }



// var compFile = @"c:\Dev\Gaming\PC\Dos\Games\Future-Wars-Adventures-in-Time_DOS_EN\future-wars\futurewars\output\BOITIER.ANI";
// var compData = File.ReadAllBytes(compFile);
// var unpackedData = CineUnpack.unpack(compData, 0x156);

// var outputFile = @"C:\Dev\Gaming\PC\Dos\Games\Future-Wars-Adventures-in-Time_DOS_EN\future-wars\futurewars\output\BOITIER_UNPACKED.ANI";
// File.WriteAllBytes(outputFile, unpackedData);

// var partFile = new PartFile(@"C:\Dev\Gaming\PC\Dos\Games\Future-Wars-Adventures-in-Time_DOS_EN\future-wars\futurewars\PART01");
// partFile.ParseFile();

// var outputDir = Path.Combine(Path.GetDirectoryName(partFile.FilePath)!, "output");

// foreach (var item in partFile.SubFiles)
// {
//     var outputFile = Path.Combine(outputDir, item.Name);
//     Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
//     if (item.Data != null) File.WriteAllBytes(outputFile, item.Data);
// }


// var riptideDir = @"C:\Dev\Gaming\PC_DOS\Apps\DrRiptideDissected\Game\v1.0 full\unpacked";
// var lFiles = Directory.GetFiles(riptideDir, "*.l");

// var palFile = @"C:\Dev\Gaming\PC_DOS\Apps\DrRiptideDissected\Game\v1.0 full\unpacked\1-2.M";
// var palData = File.ReadAllBytes(palFile).Skip(68772).Take(0x300).ToArray();
// var pal = ColorHelper.ConvertBytesToRGB(palData);

// var outputDir = Path.Combine(riptideDir, "spr_output");
// Directory.CreateDirectory(outputDir);
// foreach (var lFile in lFiles)
// {
//   using var br = new BinaryReader(File.OpenRead(lFile));
//   var numSprites = br.ReadByte();
//   for (int i = 0; i < numSprites; i++)
//   {
//     var width = br.ReadByte();
//     var height = br.ReadByte();
//     var imageData = br.ReadBytes(width * height);
//     var image = ImageFormatHelper.GenerateClutImage(pal, imageData, width, height,true);
//     image.Save(Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(lFile)}_{i}.png"), ImageFormat.Png);
//   }
// }

// var mFiles = Directory.GetFiles(riptideDir, "*.m");

// foreach (var mFile in mFiles)
// {
//   var outputDir = Path.Combine(Path.GetDirectoryName(mFile), "output");
//   Directory.CreateDirectory(outputDir);
//   var tiles = new List<byte[]>();
//   using var br = new BinaryReader(File.OpenRead(mFile));
//   var widthInTiles = br.ReadUInt16();
//   var heightInTiles = br.ReadUInt16();
//   var tileMapData = br.ReadBytes(widthInTiles * heightInTiles * 4);
//   var mapData = new List<ushort>();
//   for (int i = 0; i < tileMapData.Length; i += 4)
//   {
//     var tileIndex = BitConverter.ToUInt16(tileMapData.Skip(i).Take(2).ToArray());
//     mapData.Add(tileIndex);
//   }
//   var tileData = br.ReadBytes(0x8000);
//   for (int i = 0; i < tileData.Length; i += 64)
//   {
//     var tile = new byte[64];
//     Array.Copy(tileData, i, tile, 0, 64);
//     tiles.Add(tile);
//   }
//   var palette = br.ReadBytes(0x300);
//   var colors = ColorHelper.ConvertBytesToRGB(palette);
//   var screenImage = ImageFormatHelper.CreateScreenImage(tiles, mapData.ToArray(), widthInTiles, heightInTiles, 8, 8, colors);
//   screenImage.Save(Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(mFile)}.png"), ImageFormat.Png);
// }

// var datFile = @"C:\Dev\Gaming\PC_DOS\Games\Swelac\Lucas Learning Folder\Star Wars Early Learning\Data\Main.dat";
// var outputDir = Path.Combine(Path.GetDirectoryName(datFile), "main_output");
// Directory.CreateDirectory(outputDir);

// using var br = new BinaryReader(File.OpenRead(datFile));
// var tableOffset = br.ReadUInt32();

// br.BaseStream.Position = tableOffset + 0x14;
// var numFiles = br.ReadUInt32(); // maybe?

// var offsetLengthNames = new List<(uint, uint, string)>();
// for (int i = 0; i < numFiles; i++)
// {
//   var offset = br.ReadUInt32();
//   var length = br.ReadUInt32();
//   var nameLength = br.ReadByte();
//   var name = Encoding.ASCII.GetString(br.ReadBytes(nameLength));
//   // ensure position is aligned to 4 bytes
//   if (br.BaseStream.Position % 4 != 0)
//   {
//     br.BaseStream.Position += 4 - (br.BaseStream.Position % 4);
//   }
//   offsetLengthNames.Add((offset, length, name));
//   Console.WriteLine($"{name} - {offset:X8} - {length:X8}");
// }

// br.BaseStream.Position += 8;
// numFiles = br.ReadUInt32();
// for (int i = 0; i < numFiles; i++)
// {
//   var offset = br.ReadUInt32();
//   var length = br.ReadUInt32();
//   var nameLength = br.ReadByte();
//   var name = Encoding.ASCII.GetString(br.ReadBytes(nameLength));
//   // ensure position is aligned to 4 bytes
//   if (br.BaseStream.Position % 4 != 0)
//   {
//     br.BaseStream.Position += 4 - (br.BaseStream.Position % 4);
//   }
//   offsetLengthNames.Add((offset, length, name));
//   Console.WriteLine($"{name} - {offset:X8} - {length:X8}");
// }

// br.BaseStream.Position += 8;
// numFiles = br.ReadUInt32();
// for (int i = 0; i < numFiles; i++)
// {
//   var offset = br.ReadUInt32();
//   var length = br.ReadUInt32();
//   var nameLength = br.ReadByte();
//   var name = Encoding.ASCII.GetString(br.ReadBytes(nameLength));
//   // ensure position is aligned to 4 bytes
//   if (br.BaseStream.Position % 4 != 0)
//   {
//     br.BaseStream.Position += 4 - (br.BaseStream.Position % 4);
//   }
//   offsetLengthNames.Add((offset, length, name));
//   Console.WriteLine($"{name} - {offset:X8} - {length:X8}");
// }

// foreach (var (offset, length, name) in offsetLengthNames)
// {
//   br.BaseStream.Position = offset;
//   var data = br.ReadBytes((int)length);
//   File.WriteAllBytes(Path.Combine(outputDir, name), data);
// }

// var inputDir = @"C:\Dev\Gaming\PC\Win\Extractions\RE\REAsset\RE4\Items\Weapons\Magnums\Killer7\Export";
// PackageForTMR(inputDir, true);

// static void PackageForTMR(string inputDir, bool splitChannels = false)
// {
//   var ddsFiles = Directory.GetFiles(inputDir, "*.dds");
//   var pngOutput = inputDir;
//   var backup = Path.Combine(Directory.GetParent(inputDir).FullName, "backup");

//   var zipFile = Path.Combine(inputDir, "tmr_package.zip");
//   var zip = ZipFile.Open(zipFile, ZipArchiveMode.Create);

//   Directory.CreateDirectory(backup);

//   Parallel.ForEach(ddsFiles, ddsFile =>
//   {
//     var pngFile = Path.Combine(pngOutput, inputDir, $"{Path.GetFileNameWithoutExtension(ddsFile)}.png");

//     try
//     {
//       ScratchImage image = TexHelper.Instance.LoadFromDDSFile(ddsFile, DDS_FLAGS.NONE);
//       var metadata = image.GetMetadata();

//       // Decompress compressed formats (e.g., BC7, BC1-5, etc.)
//       if (IsCompressedFormat(metadata.Format))
//       {
//         image = image.Decompress(DXGI_FORMAT.R8G8B8A8_UNORM_SRGB);
//       }

//       // Convert non-RGBA formats to RGBA for compatibility with PNG
//       else if (!IsRGBA(metadata.Format))
//       {
//         image = image.Convert(DXGI_FORMAT.R8G8B8A8_UNORM_SRGB, TEX_FILTER_FLAGS.DEFAULT, 0.5f);
//       }

//       var tgaPath = Path.ChangeExtension(pngFile, ".tga");
//       // Save to PNG
//       image.SaveToTGAFile(0, tgaPath);
//       using (MagickImage im = new MagickImage(tgaPath))
//       {
//         im.Write(pngFile);
//       }
//       File.Delete(tgaPath);
//       File.Move(ddsFile, Path.Combine(backup, Path.GetFileName(ddsFile)));
//     }
//     catch
//     {
//       Console.WriteLine($"Error processing {ddsFile}");
//     }
//   });

//   var daeFiles = Directory.GetFiles(inputDir, "*.dae");

//   Parallel.ForEach(daeFiles, daeFile =>
//   {
//     var backupFile = Path.Combine(backup, Path.GetFileName(daeFile));
//     File.Copy(daeFile, backupFile);
//     var daeData = File.ReadAllText(daeFile);
//     var updatedData = daeData.Replace(".dds", ".png");
//     File.WriteAllText(daeFile, updatedData);
//   });

//   var pngFiles = Directory.GetFiles(pngOutput, "*.png");
//   daeFiles = Directory.GetFiles(inputDir, "*.dae");

//   if (splitChannels)
//   {
//     foreach (var pngFile in pngFiles)
//     {
//       var image = new MagickImage(pngFile);
//       var channels = image.Separate(Channels.RGBA);
//       for (int i = 0; i < channels.Count; i++)
//       {
//         var c = i switch
//         {
//           0 => "R",
//           1 => "G",
//           2 => "B",
//           3 => "A",
//           _ => "R"
//         };
//         channels[i].Write(Path.Combine(Path.GetDirectoryName(pngFile), $"{Path.GetFileNameWithoutExtension(pngFile)}_{c}.png"));
//       }
//     }
//   }

//   pngFiles = Directory.GetFiles(pngOutput, "*.png");
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

// static bool IsCompressedFormat(DXGI_FORMAT format)
// {
//   switch (format)
//   {
//     case DXGI_FORMAT.BC1_UNORM:
//     case DXGI_FORMAT.BC1_UNORM_SRGB:
//     case DXGI_FORMAT.BC2_UNORM:
//     case DXGI_FORMAT.BC2_UNORM_SRGB:
//     case DXGI_FORMAT.BC3_UNORM:
//     case DXGI_FORMAT.BC3_UNORM_SRGB:
//     case DXGI_FORMAT.BC4_UNORM:
//     case DXGI_FORMAT.BC4_SNORM:
//     case DXGI_FORMAT.BC5_UNORM:
//     case DXGI_FORMAT.BC5_SNORM:
//     case DXGI_FORMAT.BC6H_UF16:
//     case DXGI_FORMAT.BC6H_SF16:
//     case DXGI_FORMAT.BC7_UNORM:
//     case DXGI_FORMAT.BC7_UNORM_SRGB:
//       return true;
//     default:
//       return false;
//   }
// }

// static bool IsRGBA(DXGI_FORMAT format)
// {
//   return format == DXGI_FORMAT.R8G8B8A8_UNORM ||
//          format == DXGI_FORMAT.R8G8B8A8_UNORM_SRGB ||
//          format == DXGI_FORMAT.B8G8R8A8_UNORM ||
//          format == DXGI_FORMAT.B8G8R8A8_UNORM_SRGB;
// }

// var dataFile = @"C:\Dev\Gaming\PC\Win\Games\Valhalla\GAME\gfx\common.gfx";
// var outputDir = Path.Combine(Path.GetDirectoryName(dataFile), "output");
// Directory.CreateDirectory(outputDir);

// using var dReader = new BinaryReader(File.OpenRead(dataFile));
// dReader.BaseStream.Position = 0x88;
// var numFiles = dReader.ReadUInt32();

// var fileEntries = new List<ValhallaFileEntry>();
// for (int i = 0; i < numFiles; i++)
// {
//   var entry = new ValhallaFileEntry
//   {
//     Offset = dReader.ReadUInt32(),
//     Id = dReader.ReadUInt32(),
//     Flags = dReader.ReadUInt32(),
//     NameLength = dReader.ReadUInt32()
//   };
//   entry.Name = Encoding.ASCII.GetString(dReader.ReadBytes((int)entry.NameLength-1));
//   dReader.ReadByte(); // null terminator
//   fileEntries.Add(entry);
//   Console.WriteLine($"{entry.Name} - {entry.Offset:X8} - {entry.Id:X8} - {entry.Flags:X8} - {entry.NameLength:X8}");
// }

// Console.WriteLine($"Total files: {fileEntries.Count}");

// for (int i = 0; i < fileEntries.Count; i++)
// {
//   var entry = fileEntries[i];
//   var nextEntry = i + 1 < fileEntries.Count ? fileEntries[i + 1] : null;
//   var length = nextEntry != null ? nextEntry.Offset - entry.Offset : (uint)(dReader.BaseStream.Length - entry.Offset);
//   dReader.BaseStream.Position = entry.Offset;
//   var data = dReader.ReadBytes((int)length);
//   var filepath = entry.Name.Replace("Palette for: ..", "").Replace('?','a').Substring(1);
//   var outputFile = Path.Combine(outputDir, filepath);
//   if (entry.Name.Contains("Palette for: .."))
//   {
//     outputFile += ".pal";
//   }
//   Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
//   File.WriteAllBytes(outputFile, data);
//   Console.WriteLine($"Extracted {entry.Name} - {length:X8} bytes");
// }

// class ValhallaFileEntry
// {
//   public uint Offset { get; set; }
//   public uint Id { get; set; }
//   public uint Flags { get; set; }
//   public uint NameLength { get; set; }
//   public string Name { get; set; } = string.Empty;
// }


// var folderToResize = @"C:\GOGGames\Dink Smallwood HD\dink\graphics";

// var dinkFiles = Directory.GetFiles(folderToResize, "*.ff", SearchOption.AllDirectories);

// foreach (var dinkFile in dinkFiles)
// {
//   var outputDir = Path.Combine(Path.GetDirectoryName(dinkFile), "output");
//   Directory.CreateDirectory(outputDir);

//   var binaryOutputDir = Path.Combine(Path.GetDirectoryName(dinkFile), "output_binary");
//   Directory.CreateDirectory(binaryOutputDir);

//   using var br = new BinaryReader(File.OpenRead(dinkFile));
//   var count = br.ReadUInt16(); // number of frames
//   br.ReadBytes(2); // skip 2 bytes

//   // next follows a list of offsets and filenames, 17 bytes each
//   // the first 4 bytes are the offset, the next 13 bytes are the null terminated filename
//   // the last offset is the end of the file, and the filename is all nulls
//   var offsets = new List<uint>();
//   var filenames = new List<string>();
//   for (var i = 0; i < count; i++)
//   {
//     var offset = br.ReadUInt32();
//     offsets.Add(offset);
//     if (i == count - 1) break; // last offset is the end of the file
//     var filename = Encoding.ASCII.GetString(br.ReadBytes(13)).TrimEnd('\0');
//     filenames.Add(filename);
//   }

//   Console.WriteLine($"Count: {count}");

//   for (int i = 0; i < count - 1; i++)
//   {
//     var offset = offsets[i];
//     var filename = filenames[i];
//     var nextOffset = i + 1 < count ? offsets[i + 1] : (uint)br.BaseStream.Length;
//     var length = nextOffset - offset;
//     br.BaseStream.Position = offset;
//     var data = br.ReadBytes((int)length);
//     var outputBInFile = Path.Combine(binaryOutputDir, $"{filename}.bin");
//     File.WriteAllBytes(outputBInFile, data);
//     try {
//       var image = ParseBM(data);
//       var outputFile = Path.Combine(outputDir, $"{filename}.png");
//       image.RotateFlip(RotateFlipType.RotateNoneFlipY);
//       image.Save(outputFile, ImageFormat.Png);
//     } catch (Exception ex)
//     {
//       Console.WriteLine($"Error parsing {dinkFile}: {ex.Message}");
//     }
//   }
//   FileHelpers.ResizeImagesInFolder(outputDir, ExpansionOrigin.BottomLeft);
//   // var originalpngFiles = Directory.GetFiles(outputDir, "*.png");
//   // foreach (var originalpngFile in originalpngFiles)
//   // {
//   //   File.Delete(originalpngFile);
//   // }
// }

// Image ParseBM(byte[] data)
// {
//   // data starts with BM followed by 2 bytes for the length of the data (including these 4 bytes)
//   if (data[0] != 'B' || data[1] != 'M')
//   {
//     throw new Exception("Invalid BM file");
//   }
//   var length = BitConverter.ToUInt32(data, 2);
//   if (length != data.Length)
//   {
//     throw new Exception("Invalid length of BM file");
//   }
//   var actualWidth = BitConverter.ToUInt16(data, 0x12);
//   var height = BitConverter.ToUInt16(data, 0x16);
//   var paletteData = data.Skip(0x36).Take(0x400).ToArray();
//   var palette = ColorHelper.ConvertBytesToARGB(paletteData);
//   var imageData = data.Skip(0x36 + 0x400).Take(data.Length - (0x36 + 0x400)).ToArray();
//   var width = imageData.Length / height;
//   var image = ImageFormatHelper.GenerateClutImage(palette, imageData, width, height, true);
//   // crop the image to the actual width
//   var croppedImage = ImageFormatHelper.CropImage(image, actualWidth, height);
//   return croppedImage;
// }

// var levelFile = @"C:\Dev\Gaming\PC\Win\Games\WORM\ASSETS\00_output\init_4.bin";
// var levelData = File.ReadAllBytes(levelFile);
// var levelImage = ParseLevelBackground(levelData);
// levelImage.Save(@"C:\Dev\Gaming\PC\Win\Games\WORM\ASSETS\00_output\level.png", ImageFormat.Png);

// var doneFolder = @"C:\Dev\Gaming\PC\Win\Games\WORM\done";
// Directory.CreateDirectory(doneFolder);

// var v4vFiles = Directory.GetFiles(@"C:\Dev\Gaming\PC\Win\Games\WORM\ASSETS", "*4_V4*.bin", SearchOption.AllDirectories);
// foreach (var v4vFile in v4vFiles)
// {
//   var sOutputDir = Path.Combine(Path.GetDirectoryName(v4vFile)!, "output", Path.GetFileNameWithoutExtension(v4vFile));
//   Directory.CreateDirectory(sOutputDir);
//   var sData = File.ReadAllBytes(v4vFile);
//   try
//   {
//     var sprites = PHEWHelper.ParseAlignedSpriteSet(sData);
//     foreach (var (sprite, index) in sprites.WithIndex())
//     {
//       var outputFile = Path.Combine(sOutputDir, $"{Path.GetFileNameWithoutExtension(v4vFile)}_{index}.png");
//       sprite.Save(outputFile, ImageFormat.Png);
//     }
//     //File.Move(v4vFile, Path.Combine(doneFolder, Path.GetFileName(v4vFile)));
//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"Error parsing {v4vFile}: {ex.Message}");
//   }
// }


// var ewFiles = Directory.GetFiles(@"C:\Dev\Gaming\PC\Win\Games\PITFALL\ASSETS", "*.ph");

// foreach (var ewFile in ewFiles)
// {
//   var outputDir = Path.Combine(Path.GetDirectoryName(ewFile)!, Path.GetFileNameWithoutExtension(ewFile));
//   Directory.CreateDirectory(outputDir);
//   using var br = new BinaryReader(File.OpenRead(ewFile));
//   var index = 0;
//   while (br.BaseStream.Position < br.BaseStream.Length)
//   {
//     var length = br.ReadUInt32();
//     var data = br.ReadBytes((int)length);
//     var magic = data.Take(4).ToArray();
//     var magicString = Encoding.ASCII.GetString(magic);
//     // replace anything that is not a letter or number with an underscore
//     magicString = Regex.Replace(magicString, @"[^a-zA-Z0-9]", "_");
//     if (magicString == "RIFF")
//     {
//       File.WriteAllBytes(Path.Combine(outputDir, $"init_{index++}_{magicString}.wav"), data);
//     }
//     else if (magicString != "4_V4" && (index < 5 || magicString.Contains("__")))
//     {
//       try
//       {
//         var levelImage = PHEWHelper.ParseLevelBackground(data, true);
//         levelImage.Save(Path.Combine(outputDir, $"init_{index++}.png"), ImageFormat.Png);
//       }
//       catch (Exception ex)
//       {
//         Console.WriteLine($"Error parsing {ewFile} - {index}: {ex.Message}");
//         File.WriteAllBytes(Path.Combine(outputDir, $"init_{index++}.bin"), data);
//       }
//     }
//     else
//     {
//       // check for presence of 0x7e or 0x7f in file
//       // if present, save as a .bin file
//       if (data.Contains((byte)0x7f))
//       {
//         // format 2
//         File.WriteAllBytes(Path.Combine(outputDir, $"init_{index++}_{magicString}_f2.bin"), data);
//       }
//       else
//       {
//         // format 1
//         File.WriteAllBytes(Path.Combine(outputDir, $"init_{index++}_{magicString}_f1.bin"), data);
//       }
//     }
//   }
// }


// var testSpriteBlob = @"C:\Dev\Gaming\CD-i\Games\CD-i\Output\SpriteBlock9.bin";
// var pal2 = @"C:\Dev\Gaming\CD-i\Games\CD-i\Output\PAL2.bin";
// var pal2Data = File.ReadAllBytes(pal2);
// var palette2 = ColorHelper.ReadClutBankPalettes(pal2Data, 2);
// using var br = new BinaryReader(File.OpenRead(testSpriteBlob));
// // start offset is the 4 bytes at 0x8 BigEndian
// br.ReadBytes(4); // skip 4 bytes
// // read the end offset
// var endOffset = br.ReadBigEndianUInt32();
// // read the start offset
// var startOffset = br.ReadBigEndianUInt32();

// var offsetList = new List<uint>();
// var offset = 0;
// while (br.BaseStream.Position < startOffset)
// {
//   offset = (int)br.ReadBigEndianUInt32();
//   if (offset > endOffset) break; // end of file marker
//   offsetList.Add((uint)(offset + startOffset));
// }

// var outputDir = Path.Combine(Path.GetDirectoryName(testSpriteBlob)!, "output9");
// Directory.CreateDirectory(outputDir);

// for (int i = 0; i < offsetList.Count; i++)
// {
//   var sOffset = offsetList[i];
//   var nextOffset = i + 1 < offsetList.Count ? offsetList[i + 1] : endOffset;
//   if (nextOffset < sOffset || nextOffset > endOffset) break; // end of file marker
//   var length = nextOffset - sOffset;
//   br.BaseStream.Position = sOffset;
//   var blob = br.ReadBytes((int)length);
//   var decodedBlob = CompiledSpriteHelper.DecodeCompiledSprite(blob, 0);
//   var image = ImageFormatHelper.GenerateClutImage(palette2, decodedBlob, 416, 200, true);
//   var outputFile = Path.Combine(outputDir, $"sprite_{i}.png");
//   image.Save(outputFile, ImageFormat.Png);
// }
// ImageFormatHelper.CropImageFolder(outputDir, "*.png", 0, 0, 84, 72);

// var levelFile = @"C:\GOG Games\Necrodome\MAPS\Q1.RVG";
// var levelData = File.ReadAllBytes(levelFile);

// // replace bytes from offset 0x3593 for 0x18000 bytes with 0x00
// var startOffset = 0x11EB;// 0x3593;
// var length = 0x2388; // 0x18000;

// var newLevelData = new byte[length];
// Array.Copy(levelData, startOffset, newLevelData, 0, length);

// var index = 0;
// for (int i = 0; i < newLevelData.Length; i += 24)
// {
//   newLevelData[i+0xf] = (byte)(0xe7);
// }
// Array.Copy(newLevelData, 0, levelData, startOffset, length);
// File.Move(levelFile, levelFile + ".bak");
// File.WriteAllBytes(levelFile, levelData);
// Get all files from the input directory

// var meenPalFile = @"C:\Dev\Gaming\PC\Dos\Games\Chill-Manor_DOS_EN\DATA\RES\output\RESINT\game_scr.bin";
// var meenPalData = File.ReadAllBytes(meenPalFile);

// var cmpFiles = Directory.GetFiles(@"C:\Dev\Gaming\PC\Dos\Games\Chill-Manor_DOS_EN\DATA\RES\output\RESINT", "*.cmp");
// var outputDir = Path.Combine(Path.GetDirectoryName(cmpFiles[0])!, "output");
// Directory.CreateDirectory(outputDir);

// foreach (var cmpFile in cmpFiles)
// {
//   var cmpData = File.ReadAllBytes(cmpFile).Skip(0x4).ToArray();
//   if (cmpData.Length < 0x10)
//   {
//     Console.WriteLine($"Skipping {cmpFile} - not enough data");
//     continue;
//   }
//   var width = BitConverter.ToUInt16(cmpData.Take(2).ToArray(), 0);
//   var height = BitConverter.ToUInt16(cmpData.Skip(2).Take(2).ToArray(), 0);
//   var firstNonTransparentRow = BitConverter.ToUInt16(cmpData.Skip(4).Take(2).ToArray(), 0);
//   var lastNonTransparentRow = BitConverter.ToUInt16(cmpData.Skip(6).Take(2).ToArray(), 0);
//   var firstNonTransparentColumn = BitConverter.ToUInt16(cmpData.Skip(8).Take(2).ToArray(), 0);
//   var lastNonTransparentColumn = BitConverter.ToUInt16(cmpData.Skip(0xa).Take(2).ToArray(), 0);

//   var colourCount = cmpData[0xc];
//   var colourBytes = cmpData.Skip(0xd).Take(colourCount).ToArray();

//   var cmpPaletteBytes = new byte[colourCount * 3];
//   for (int i = 0; i < colourCount; i++)
//   {
//     var colour = colourBytes[i];
//     var palIndex = colour * 3;
//     cmpPaletteBytes[i * 3] = meenPalData[palIndex];
//     cmpPaletteBytes[i * 3 + 1] = meenPalData[palIndex + 1];
//     cmpPaletteBytes[i * 3 + 2] = meenPalData[palIndex + 2];
//   }
//   var offsetDataOffset = 0xd + colourCount;
//   var offsetList = new List<ushort>();
//   var offsetData = cmpData.Skip(offsetDataOffset).ToArray();

//   var offset = BitConverter.ToUInt16(offsetData.Take(2).ToArray(), 0);
//   offsetList.Add(offset);
//   var currentPos = offsetDataOffset + 2;
//   while (currentPos < offsetList[0])
//   {
//     offset = BitConverter.ToUInt16(cmpData.Skip(currentPos).Take(2).ToArray(), 0);
//     offsetList.Add(offset);
//     currentPos += 2;
//   }

//   var imageDataLines = new List<byte[]>();

//   for (int i = 0; i < offsetList.Count; i++)
//   {
//     var start = offsetList[i];
//     var end = i == offsetList.Count - 1 ? cmpData.Length : offsetList[i + 1];
//     var line = cmpData.Skip(start).Take(end - start).ToArray();
//     imageDataLines.Add(line);
//   }

//   var imageLines = new List<byte[]>();
//   if (firstNonTransparentRow > 0)
//   {
//     for (int i = 0; i < firstNonTransparentRow; i++)
//     {
//       var lineData = Enumerable.Repeat((byte)0x0, width).ToArray();
//       imageLines.Add(lineData);
//     }
//   }
//   foreach (var line in imageDataLines)
//   {
//     var lineData = new byte[width];
//     var lineIndex = 0;
//     for (int i = 0; i < line.Length; i++)
//     {
//       if (lineIndex >= lineData.Length - 1)
//       {
//         break;
//       }
//       var b = line[i];
//       if ((b & 0x80) == 0x80)
//       {
//         var count = b & 0x7F;
//         for (int j = 0; j < count && lineIndex < lineData.Length; j++)
//         {
//           lineData[lineIndex] = 0x0;
//           lineIndex++;
//         }
//       }
//       else
//       {
//         lineData[lineIndex] = b;
//         lineIndex++;
//       }
//     }
//     imageLines.Add(lineData);
//   }
//   if (lastNonTransparentRow < height - 1)
//   {
//     for (int i = lastNonTransparentRow; i < height - 1; i++)
//     {
//       var lineData = Enumerable.Repeat((byte)0x0, width).ToArray();
//       imageLines.Add(lineData);
//     }
//   }
//   var imageData = imageLines.SelectMany(x => x).ToArray();
//   var imagePal = ColorHelper.ConvertBytesToRGB(cmpPaletteBytes);
//   var image = ImageFormatHelper.GenerateClutImage(imagePal, imageData, width, height, true);
//   var outputFile = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(cmpFile)}.png");
//   image.Save(outputFile, ImageFormat.Png);
// }

// var cdiData = @"C:\Dev\Gaming\CD-i\Games\CYBER SOLDIER   SHARAKU\syaraku.rtf";
// var cdiOutputDir = Path.Combine(Path.GetDirectoryName(cdiData)!, "output");
// Directory.CreateDirectory(cdiOutputDir);
// var cdiDataFile = new CdiFile(cdiData);

// var audioOutputDir = Path.Combine(cdiOutputDir, "audio");
// Directory.CreateDirectory(audioOutputDir);
// var videoOutputDir = Path.Combine(cdiOutputDir, "video");
// Directory.CreateDirectory(videoOutputDir);
// var dataOutputDir = Path.Combine(cdiOutputDir, "data");
// Directory.CreateDirectory(dataOutputDir);

// var sectorList = new List<CdiSector>();

// foreach (var sector in cdiDataFile.Sectors)
// {
//   sectorList.Add(sector);
//   if (sector.SubMode.IsEOR)
//   {
//     var dataSectors = sectorList.Where(s => s.SubMode.IsData).ToList();
//     var audioSectors = sectorList.Where(s => s.SubMode.IsAudio).ToList();
//     var rleSectors = sectorList.Where(s => s.SubMode.IsVideo && s.Coding.VideoString == "RL7").ToList();
//     var clutSectors = sectorList.Where(s => s.SubMode.IsVideo && s.Coding.VideoString == "CLUT7").ToList();
//     var dataData = dataSectors.SelectMany(s => s.GetSectorData()).ToArray();
//     var audioData = audioSectors.SelectMany(s => s.GetSectorData()).ToArray();
//     var rleData = rleSectors.SelectMany(s => s.GetSectorData()).ToArray();
//     var clutData = clutSectors.SelectMany(s => s.GetSectorData()).ToArray();
//     Console.WriteLine($"Data sectors: {dataSectors.Count}, Audio sectors: {audioSectors.Count}, RLE sectors: {rleSectors.Count}, CLUT sectors: {clutSectors.Count}");
//     if (dataData.Length > 0)
//     {
//       var dataFile = Path.Combine(dataOutputDir, $"{sector.SectorIndex}_data.bin");
//       File.WriteAllBytes(dataFile, dataData);
//     }
//     if (audioData.Length > 0)
//     {
//       var audioFile = Path.Combine(audioOutputDir, $"{sector.SectorIndex}_audio.bin");
//       File.WriteAllBytes(audioFile, audioData);
//     }
//     if (rleData.Length > 0)
//     {
//       var rleFile = Path.Combine(videoOutputDir, $"{sector.SectorIndex}_video.rle");
//       File.WriteAllBytes(rleFile, rleData);
//     }
//     if (clutData.Length > 0)
//     {
//       var clutFile = Path.Combine(videoOutputDir, $"{sector.SectorIndex}_video.clut");
//       File.WriteAllBytes(clutFile, clutData);
//     }
//     sectorList.Clear();
//   }
// }

// var byteSequencePattern = new byte[] { 0x43, 0x50, 0x4C, 0x32 };
// var testSpriteFile = @"C:\Dev\Gaming\CD-i\Games\BODYSLAM\output\channel_1\6714_data.bin";

// var offsets = FileHelpers.FindByteSequenceBuffered(testSpriteFile, byteSequencePattern);

// foreach (var offset in offsets)
// {

//   using var br = new BinaryReader(File.OpenRead(testSpriteFile));
//   br.BaseStream.Seek(offset + 0xC, SeekOrigin.Begin); // skip the first 8 bytes
//   var sOffsets = new List<uint>();
//   var unks = new List<ushort>();
//   for (int i = 0; i < 90; i++)
//   {
//     var sOffset = br.ReadBigEndianUInt32();
//     sOffsets.Add(sOffset);
//   }

//   for (int i = 0; i < 90; i++)
//   {
//     var unk = br.ReadUInt16();
//     unks.Add(unk);
//   }

//   var dataStartOffset = br.BaseStream.Position;

//   var sOutputDir = Path.Combine(Path.GetDirectoryName(testSpriteFile)!, "sprite_output", Path.GetFileNameWithoutExtension(testSpriteFile), $"sprite_{offset:X}");
//   Directory.CreateDirectory(sOutputDir);
//   for (int i = 0; i < 90; i++)
//   {
//     var sOffset = sOffsets[i] + dataStartOffset;
//     br.BaseStream.Seek(sOffset, SeekOrigin.Begin);
//     var length = i == 89 ? (int)(br.BaseStream.Length - sOffset) : (int)(sOffsets[i + 1] + dataStartOffset - sOffset);
//     var csData = br.ReadBytes(length);
//     var decodedData = CompiledSpriteHelper.DecodeCompiledSprite(csData, 0, 0x300);
//     File.WriteAllBytes(Path.Combine(sOutputDir, $"sprite_{i}.bin"), decodedData);
//   }

// }

// var rtrFile = @"C:\Dev\Gaming\CD-i\Games\BODYSLAM\data.rtr";
// var cdiRtr = new CdiFile(rtrFile);

// var sectorList = new List<CdiSector>();

// foreach (var sector in cdiRtr.Sectors)
// {
//   sectorList.Add(sector);
//   if (sector.SubMode.IsEOR)
//   {
//     var dataSectorsByChannel = sectorList
//       .Where(s => s.SubMode.IsData)
//       .GroupBy(s => s.Channel)
//       .ToDictionary(g => g.Key, g => g.ToList());

//     foreach (var kvp in dataSectorsByChannel)
//     {
//       var channel = kvp.Key;
//       var dataSectors = kvp.Value;
//       if (dataSectors.Count == 0) continue;

//       var dataBytes = dataSectors.SelectMany(s => s.GetSectorData()).ToArray();
//       if (dataBytes.Length == 0) continue;

//       var dOutputDir = Path.Combine(Path.GetDirectoryName(rtrFile)!, "output", $"channel_{channel}");
//       Directory.CreateDirectory(dOutputDir);
//       var outputFile = Path.Combine(dOutputDir, $"{sectorList.First().SectorIndex}_data.bin");
//       File.WriteAllBytes(outputFile, dataBytes);
//     }
//     sectorList.Clear();
//   }
// }

// var rleFiles = Directory.GetFiles(@"C:\Dev\Gaming\PC\Dos\Games\Nomad\Nomad93\invent\output", "*.stp");
// var rleOutputDir = Path.Combine(Path.GetDirectoryName(rleFiles[0])!, "output_rle");
// Directory.CreateDirectory(rleOutputDir);
// foreach (var rleTest in rleFiles)
// {
//   var rleData = File.ReadAllBytes(rleTest).Skip(0x8).ToArray();

//   rleData = NomadHelpers.DecodeRle(rleData);
//   File.WriteAllBytes(Path.Combine(rleOutputDir, $"{Path.GetFileNameWithoutExtension(rleTest)}.decoded.bin"), rleData);
// }

//var testFile = @"C:\Dev\Gaming\PC\Dos\Games\Nomad\Nomad93\invent.DAT";

// using var br = new BinaryReader(File.OpenRead(testFile));
// var fileCount = br.ReadUInt16(); // number of files

// var nomadFiles = new List<NomadDatRecord>();

// for (int i = 0; i < fileCount; i++)
// {
//   var fileRecord = new NomadDatRecord(br);
//   nomadFiles.Add(fileRecord);
// }
// var outputDir = Path.Combine(Path.GetDirectoryName(testFile)!, Path.GetFileNameWithoutExtension(testFile), "output");
// Directory.CreateDirectory(outputDir);

// foreach (var file in nomadFiles)
// {
//   br.BaseStream.Seek(file.Offset, SeekOrigin.Begin);
//   if (file.IsCompressed && file.IsHeaderCompressed)
//   {
//     var fileData = br.ReadBytes((int)file.CompressedLength);
//     var decompressedData = NomadHelpers.DecodeLZ(fileData);
//     var outputFile = Path.Combine(outputDir, $"{file.Name}");
//     File.WriteAllBytes(outputFile, decompressedData);
//   }
//   else if (file.IsCompressed && !file.IsHeaderCompressed)
//   {
//     // almost the same, but we need to take the 4 byte uncompressed header into account
//     var headerBytes = br.ReadBytes(4);
//     var fileData = br.ReadBytes((int)file.CompressedLength);
//     var decompressedData = NomadHelpers.DecodeLZ(fileData);
//     var outputFile = Path.Combine(outputDir, $"{file.Name}");
//     // combine the header and the decompressed data
//     var finalData = new byte[headerBytes.Length + decompressedData.Length];
//     Array.Copy(headerBytes, finalData, headerBytes.Length);
//     Array.Copy(decompressedData, 0, finalData, headerBytes.Length, decompressedData.Length);
//     File.WriteAllBytes(outputFile, finalData);
//   }
//   else
//   {
//     // not compressed, just read the data
//     var fileData = br.ReadBytes((int)file.UncompressedLength);
//     var outputFile = Path.Combine(outputDir, $"{file.Name}");
//     File.WriteAllBytes(outputFile, fileData);
//   }
// }

// now that we have the frames, we need to go through the fr



// var test = @"C:\Dev\Gaming\PC\Dos\Games\BABAYAGA\BABAYAGA\MENU\TitleBmp2.bin";
// using var testReader = new BinaryReader(File.OpenRead(test));

// testReader.BaseStream.Position = 0x0A;

// var decodedData = Decompress(testReader.BaseStream, 0x18ee, 0x99 * 0x7a);
// File.WriteAllBytes(test + ".unpacked2", decodedData);
// Debugger.Break();

// var rscDir = @"C:\Dev\Gaming\PC\Dos\Games\BABAYAGA\BABAYAGA\STORY";
// var outputDir = Path.Combine(rscDir, "output");
// Directory.CreateDirectory(outputDir);

// foreach (var rscFile in Directory.GetFiles(rscDir, "*.RSC"))
// {
//   try
//   {
//     extractRscFile(rscFile, outputDir);
//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"Error extracting {rscFile}: {ex.Message}");
//   }
// }

// void extractRscFile(string rscFile, string outputDir)
// {
//   using var rscReader = new BinaryReader(File.OpenRead(rscFile));
//   var headerSize = rscReader.ReadUInt32(); // size of the header

//   var headerCount = rscReader.ReadUInt16(); // number of headers
//   var headerTypeAndOffset = new List<(string, uint)>(); // list of header types and offsets
//   for (var i = 0; i < headerCount; i++)
//   {
//     var headerType = Encoding.UTF8.GetString(rscReader.ReadBytes(4)); // type of the header
//     var headerOffset = rscReader.ReadUInt32() + 6; // offset of the header
//     headerTypeAndOffset.Add((headerType, headerOffset));
//   }

//   Console.WriteLine($"Header count: {headerCount}");
//   var bmpTableOffset = headerTypeAndOffset.FirstOrDefault(x => x.Item1 == "BMAP").Item2; // offset of the BMP table
//   var colorTableOffset = headerTypeAndOffset.FirstOrDefault(x => x.Item1 == "CTBL").Item2; // offset of the color table

//   rscReader.BaseStream.Position = colorTableOffset + 2;
//   var colorsOffset = rscReader.ReadUInt32() + headerSize; // offset of the colors
//   var colorsSize = rscReader.ReadUInt32(); // size of the colors

//   rscReader.BaseStream.Position = colorsOffset + 2;
//   var colorData = rscReader.ReadBytes((int)colorsSize); // read the color data
//   var palette = ColorHelper.ConvertBytesToRGB(colorData); // convert the color data to a palette

//   rscReader.BaseStream.Position = bmpTableOffset + 2
//   ;
//   var bmpOffset = rscReader.ReadUInt32() + headerSize; // offset of the BMP data
//   var bmpSize = rscReader.ReadUInt32(); // size of the BMP data

//   rscReader.BaseStream.Position = bmpOffset + 2;
//   var height = rscReader.ReadUInt16(); // height of the BMP
//   var width = rscReader.ReadUInt16(); // width of the BMP
//   rscReader.ReadBytes(4); // skip 4 bytes
//   var bmpData = rscReader.ReadBytes((int)bmpSize); // read the BMP data
//   var image = ImageFormatHelper.GenerateClutImage(palette, bmpData, width, height); // generate the image from the BMP data
//   // fkip the image vertically
//   image.RotateFlip(RotateFlipType.RotateNoneFlipY); // flip the image vertically
//   image.Save(Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(rscFile)}.png"), ImageFormat.Png); // save the image to a file

// }

// Debugger.Break();

// byte[] Decompress(Stream inputStream, int compressedDataSize, int expectedOutputSize)
// {
//   // --- Input Validation ---
//   if (inputStream == null)
//     throw new ArgumentNullException(nameof(inputStream));
//   if (!inputStream.CanRead)
//     throw new ArgumentException("Stream must be readable.", nameof(inputStream));
//   if (compressedDataSize < 0)
//     throw new ArgumentOutOfRangeException(nameof(compressedDataSize), "Compressed data size cannot be negative.");
//   if (expectedOutputSize < 0)
//     throw new ArgumentOutOfRangeException(nameof(expectedOutputSize), "Expected output size cannot be negative.");
//   if (expectedOutputSize == 0 && compressedDataSize == 0)
//     return Array.Empty<byte>(); // Handle empty case
//                                 // Add more robust checks if needed (e.g., if compressed size > 0 but output is 0)

//   byte[] outputBuffer = new byte[expectedOutputSize];
//   int outputIndex = 0;       // Current position in outputBuffer
//   int bytesToRead = compressedDataSize; // Counter for remaining input bytes

//   // Use BinaryReader for convenience. Keep stream open.
//   using (var reader = new BinaryReader(inputStream, Encoding.ASCII, true))
//   {
//     // --- Main Decompression Loop ---
//     // Loop continues as long as there are input bytes expected to be read.
//     while (bytesToRead > 0)
//     {
//       // Check for potential output buffer overflow before writing.
//       if (outputIndex >= expectedOutputSize && bytesToRead > 0)
//       {
//         // Allow loop to finish if output buffer is full *exactly* when input is consumed.
//         // If input remains but output is full, it's an error.
//         throw new InvalidDataException($"Output buffer overrun. Exceeded expected size {expectedOutputSize} while {bytesToRead} input bytes remain.");
//       }

//       byte val;
//       try { val = reader.ReadByte(); }
//       catch (EndOfStreamException ex) { throw new InvalidDataException($"Stream ended unexpectedly while reading control byte. Expected {bytesToRead} more bytes.", ex); }
//       bytesToRead--; // Consumed 1 byte for 'val'

//       if (val != 0xff)
//       {
//         // --- Literal Byte ---
//         // Check if output buffer has space BEFORE writing
//         if (outputIndex >= expectedOutputSize)
//         {
//           throw new InvalidDataException($"Output buffer overrun on literal write. Exceeded expected size {expectedOutputSize}.");
//         }
//         outputBuffer[outputIndex++] = val;
//       }
//       else // val == 0xff : Compressed sequence marker
//       {
//         // --- Compressed Block ---
//         byte countByte;
//         try { countByte = reader.ReadByte(); }
//         catch (EndOfStreamException ex) { throw new InvalidDataException($"Stream ended unexpectedly while reading count byte. Expected {bytesToRead} more bytes.", ex); }
//         bytesToRead--; // Consumed 1 byte for 'countByte'

//         ushort step;
//         bool stepIsTwoBytes = (countByte & 0x80) != 0; // Check MSB

//         if (!stepIsTwoBytes) // MSB is 0: step is 1 byte
//         {
//           try { step = reader.ReadByte(); }
//           catch (EndOfStreamException ex) { throw new InvalidDataException($"Stream ended unexpectedly while reading 1-byte step. Expected {bytesToRead} more bytes.", ex); }
//           bytesToRead--; // Consumed 1 byte for 1-byte step
//         }
//         else // MSB is 1: step is 2 bytes (Little Endian)
//         {
//           countByte = (byte)(countByte ^ 0x80); // Clear the MSB
//           try { step = reader.ReadUInt16(); }
//           catch (EndOfStreamException ex) { throw new InvalidDataException($"Stream ended unexpectedly while reading 2-byte step. Expected {bytesToRead} more bytes.", ex); }
//           bytesToRead -= 2; // Consumed 2 bytes for 2-byte step
//         }

//         // Ensure bytesToRead hasn't gone negative (indicates bad compressedDataSize or data corruption)
//         if (bytesToRead < 0)
//         {
//           throw new InvalidDataException($"Input stream read error. Consumed more bytes ({compressedDataSize - bytesToRead}) than expected ({compressedDataSize}).");
//         }

//         int copyLength = countByte + 4; // Calculate the actual copy length (+4 bias)

//         // --- Safety Checks before Copy ---
//         // 1. Back-reference Offset Check:
//         if (step + 1 > outputIndex)
//         {
//           throw new InvalidDataException($"Invalid back-reference. Step {step + 1} at output index {outputIndex} points before the start of the buffer.");
//         }
//         // 2. Output Boundary Check:
//         if (outputIndex + copyLength > expectedOutputSize)
//         {
//           throw new InvalidDataException($"Decompression error. Copying {copyLength} bytes at output index {outputIndex} would exceed the expected output size of {expectedOutputSize}.");
//         }
//         // --- End Safety Checks ---

//         // --- Perform Back-Reference Copy ---
//         for (int i = 0; i < copyLength; i++)
//         {
//           int sourceIndex = outputIndex - step - 1;
//           outputBuffer[outputIndex] = outputBuffer[sourceIndex];
//           outputIndex++;
//         }
//         // NOTE: bytesToRead is NOT decremented here, matching C++ logic
//       }
//     } // End while (bytesToRead > 0)

//     // --- Final Validation Checks ---
//     // 1. Did we consume *exactly* the expected number of input bytes?
//     //    (bytesToRead should be 0 if the loop condition and decrements worked correctly)
//     if (bytesToRead != 0)
//     {
//       throw new InvalidDataException($"Decompression logic error or incorrect compressed size. Expected to read {compressedDataSize} bytes, but {Math.Abs(bytesToRead)} bytes {(bytesToRead < 0 ? "over-read" : "remain unread")}.");
//     }

//     // 2. Did we produce *exactly* the expected number of output bytes?
//     if (outputIndex != expectedOutputSize)
//     {
//       throw new InvalidDataException($"Decompression finished, but produced {outputIndex} output bytes instead of the expected {expectedOutputSize}.");
//     }

//   } // BinaryReader disposed

//   return outputBuffer;
// }


// var catgun = @"C:\Dev\Gaming\PC\Dos\Games\CATGUN\CATGUN.BLK";
// var catgunOutputDir = Path.Combine(Path.GetDirectoryName(catgun)!, "output");
// Directory.CreateDirectory(catgunOutputDir);

// using var cbr = new BinaryReader(File.OpenRead(catgun));
// var count = cbr.ReadUInt32(); // number of files
// var dataOffset = cbr.ReadUInt32(); // offset to the start of the data

// var catgunFiles = new List<CatgunFile>();
// for (var i = 0; i < count; i++)
// {
//   var name = Encoding.UTF8.GetString(cbr.ReadBytes(0x28)).TrimEnd('\0');
//   var offset = cbr.ReadUInt32();
//   var length = cbr.ReadUInt32();
//   var catgunFile = new CatgunFile()
//   {
//     Name = name,
//     Offset = offset + dataOffset,
//     Length = length
//   };
//   catgunFiles.Add(catgunFile);
// }

// for (int i = 0; i < catgunFiles.Count; i++)
// {
//   var catgunFile = catgunFiles[i];
//   cbr.BaseStream.Position = catgunFile.Offset;
//   var data = cbr.ReadBytes((int)catgunFile.Length);
//   var outputFile = Path.Combine(catgunOutputDir, $"{catgunFile.Name}");
//   Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
//   File.WriteAllBytes(outputFile, data);
// }


// var folderToResize = @"C:\Dev\Gaming\PC\Dos\Games\Chewy-Esc-from-F5_DOS_EN_ISO\ROOM\output\DET35\JanitorChewy";
// FileHelpers.ResizeImagesInFolder(folderToResize, ExpansionOrigin.BottomLeft);
// Debugger.Break();

// var imageFile = @"C:\Dev\Gaming\PC\Dos\Games\Speed-Racer-in-The-Challenge-of-Racer-X_DOS_EN\speed-racer-in-the-challenge-of-racer-x\ACCOBACK.TJL";

// var tileData = File.ReadAllBytes(imageFile).Skip(0xA).Take(0x1000).ToArray();
// var tileWidth = 8;
// var tileHeight = 8;

// OutputTileMap(tileData, "ACCOBACK", 64,32, tileWidth, tileHeight, @"C:\Dev\Gaming\PC\Dos\Extractions\Speed Racer\Maps");

// var outputDir = Path.Combine(Path.GetDirectoryName(imageFile)!, "output", Path.GetFileNameWithoutExtension(imageFile));
// Directory.CreateDirectory(outputDir);
// var index = 0;
// for (int i = 0; i < imageData.Length; i+=0x40)
// {
//   var frameData = imageData.Skip(i).Take(0x40).ToArray();
//   var image = ImageFormatHelper.GenerateClutImage(palette, frameData, 8, 8);
//   var outputFile = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(imageFile)}_{index++}.png");
//   image.Save(outputFile, ImageFormat.Png);
// }

// var sprFiles = Directory.GetFiles(@"C:\Dev\Gaming\PC\Win\NECRDOME_E3\NECRDOME\output\NECRO\sprites");
// var palFile = @"C:\Dev\Gaming\PC\Win\NECRDOME_E3\NECRDOME\output\NECRO\palette";
// var outputDir = Path.Combine(Path.GetDirectoryName(sprFiles[0])!, "output");
// Directory.CreateDirectory(outputDir);
// var paletteData = File.ReadAllBytes(palFile).Take(0x300).ToArray();
// var palette = ColorHelper.ConvertBytesToRGB(paletteData, true);

// foreach (var spr in sprFiles)
// {
//   var sprData = File.ReadAllBytes(spr);
//   var sprWidth = BitConverter.ToUInt16(sprData, 0x0);
//   var sprHeight = BitConverter.ToUInt16(sprData, 0x2);
//   sprData = sprData.Skip(0xa).ToArray(); // skip the first 10 bytes
//   var sprImage = ImageFormatHelper.GenerateClutImage(palette, sprData, sprWidth, sprHeight, true);
//   var outputFile = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(spr)}.png");
//   sprImage.Save(outputFile, ImageFormat.Png);
// }


// var test = @"C:\Dev\Gaming\PC\Dos\Games\Chewy-Esc-from-F5_DOS_EN_ISO\";
// var tafFiles = Directory.GetFiles(test, "*.TAF", SearchOption.AllDirectories);

// foreach (var tafFile in tafFiles)
// {
//   try
//   {
//     ExtractTafFile(tafFile);
//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"Error extracting {tafFile}: {ex.Message}");
//   }
// }





// var palFile = @"C:\Dev\Gaming\PC\Win\Games\MAGESLAY_press\MAGESLAY\MAGE\VAMPIRE.PAL";
// var paletteData = File.ReadAllBytes(palFile).Take(0x300).ToArray();
// var palette = ColorHelper.ConvertBytesToRGB(paletteData);

// var spriteDir = @"C:\Dev\Gaming\PC\Win\Games\Mageslay\MageSlay\Mage\SPRITES";
// var soutputDir = Path.Combine(spriteDir, "output");
// Directory.CreateDirectory(soutputDir);
// var spriteFiles = Directory.GetFiles(spriteDir, "demon.SFD");

// foreach (var sFile in spriteFiles)
// {
//   // Read and print first 6 bytes of the file
//   // using var br = new BinaryReader(File.OpenRead(sFile));
//   // var first6Bytes = br.ReadBytes(6);
//   // for (int i = 0; i < first6Bytes.Length; i++)
//   // {
//   //   Console.Write($"{first6Bytes[i]:X2} ");
//   // }
//   // Console.WriteLine();
//   DecodeMageSlayerSpriteFile(sFile, palette);
// }

// static void DecodeMageSlayerSpriteFile(string sFile, List<Color> palette)
// {
//   using var br = new BinaryReader(File.OpenRead(sFile));
//   var initialByte = br.ReadByte(); // read the first byte
//   br.BaseStream.Position = 0x0;
//   var initialShort = br.ReadUInt16();
//   br.BaseStream.Position = 0x4; // skip the first 4 bytes
//   var initialOffset = br.ReadUInt16(); // read the initial offset
//   var rleFlag = 0;
//   if (initialShort < 100 || sFile.Contains("_d.sfd"))
//   {
//     var outputDir = Path.Combine(Path.GetDirectoryName(sFile)!, "output", Path.GetFileNameWithoutExtension(sFile));
//     // no compression, reset to beginning of offsets
//     br.BaseStream.Position = 0x4; // skip the first 4 bytes
//     var count = initialShort;
//     var offsets = new List<uint>();
//     for (var i = 0; i < count; i++)
//     {
//       var offset = br.ReadUInt32();
//       offsets.Add(offset);
//     }
//     for (int i = 0; i < count; i++)
//     {
//       var offset = offsets[i];
//       br.BaseStream.Position = offset;
//       var width = br.ReadUInt16();
//       var height = br.ReadUInt16();
//       var xOffset = br.ReadUInt16();
//       var yOffset = br.ReadUInt16();
//       var length = width * height;
//       var data = br.ReadBytes((int)length);
//       var image = ImageFormatHelper.GenerateClutImage(palette, data, width, height, true);
//       var outputFile = Path.Combine(outputDir, $"{i}_{xOffset}_{yOffset}.png");
//       Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
//       image.Save(outputFile, ImageFormat.Png);
//     }
//   }
//   else
//   {
//     // multiple sprites, compressed
//     rleFlag = 0x20 * initialByte;
//     br.BaseStream.Position = 0x6;
//     var compressedData = br.ReadBytes((int)(br.BaseStream.Length - 0x6));
//     var decompressedData = DecompressMSData(compressedData, rleFlag);
//     var outputFile = Path.Combine(Path.GetDirectoryName(sFile)!, $"{Path.GetFileNameWithoutExtension(sFile)}_d.sfd");
//     File.WriteAllBytes(outputFile, decompressedData);
//     DecodeMageSlayerSpriteFile(outputFile, palette);
//     //File.Delete(outputFile);
//   }
// }

// static byte[] DecompressMSData(byte[] compressedData, int rleFlag)
// {
//   var decompressedData = new List<byte>();
//   for (int i = 0; i < compressedData.Length; i++)
//   {
//     var currentByte = compressedData[i];
//     if ((currentByte & rleFlag) == rleFlag && currentByte <= rleFlag + 0x1f)
//     {
//       // RLE compression
//       var repeatCount = currentByte & 0x1F;
//       byte value = 0;
//       if (i + 1 < compressedData.Length)
//       {
//         value = compressedData[i + 1];
//         i++; // Skip the next byte as it's part of the RLE command
//       }
//       else
//       {
//         break; // Avoid out of bounds access
//       }
//       for (int j = 0; j < repeatCount; j++)
//       {
//         decompressedData.Add(value);
//       }
//     }
//     else
//     {
//       // No compression, just add the byte to the output
//       decompressedData.Add(currentByte);
//     }
//   }
//   return decompressedData.ToArray();
// }

// static void OutputTileMap(byte[] data, string level, int width, int height, int tileWidth, int tileHeight, string outputFolder)
// {
//   var sb = new StringBuilder();

//   var outputName = Path.Combine(outputFolder, $"{level}.tmx");
//   if (File.Exists(outputName))
//   {
//     File.Delete(outputName);
//   }

//   var xmlString = """
// 	<?xml version="1.0" encoding="UTF-8"?>
// 	<map version="1.10" tiledversion="1.11.2" orientation="orthogonal" renderorder="right-down" width="_WIDTH_" height="_HEIGHT_" tilewidth="_TWIDTH_" tileheight="_THEIGHT_" infinite="0" nextlayerid="2" nextobjectid="1">
// 		<tileset firstgid="1" source="C:/Dev/Gaming/PC/Dos/Extractions/Speed Racer/Maps/TILES_ID_Tiles.tsx"/>
// 		<layer id="1" name="Tile Layer 1" width="_WIDTH_" height="_HEIGHT_">
// 		<data encoding="csv">
// 		</data>
// 		</layer>
// 	</map>
// 	""";
//   xmlString = xmlString.Replace("TILES_ID", level);
//   xmlString = xmlString.Replace("_WIDTH_", width.ToString());
//   xmlString = xmlString.Replace("_HEIGHT_", height.ToString());
//   xmlString = xmlString.Replace("_TWIDTH_", tileWidth.ToString());
//   xmlString = xmlString.Replace("_THEIGHT_", tileHeight.ToString());

//   var xmlDoc = XDocument.Parse(xmlString);
//   var dataElement = xmlDoc.Descendants("data").FirstOrDefault();
//   if (dataElement == null)
//   {
//     Console.WriteLine("No <data> element found in the XML file.");
//     return;
//   }
//   // Assuming each tile index takes 2 bytes and the array is stored in row-major order.

//   // Calculate number of tiles based on data length
//   if (data.Length != width * height * 2)
//   {
//     throw new ArgumentException("Data length does not match expected size for a 72x44 map with 2 bytes per tile.");
//   }

//   // Create a 2D array to hold the tile indices
//   ushort[,] map = new ushort[width, height];

//   // Fill the map array with the tile indices
//   for (int x = 0; x < width; x++)
//   {
//     for (int y = 0; y < height; y++)
//     {
//       int index = (x * height + y) * 2;
//       // Convert two bytes to one ushort (considering little-endian format here)
//       ushort tileIndex = BitConverter.ToUInt16(data.Skip(index).Take(2).ToArray(), 0);
//       map[x, y] = (ushort)(tileIndex + 1);  // Assign to the transposed positions
//     }
//   }

//   // Output the map to the console or any other display mechanism
//   for (int i = 0; i < width; i++)
//   {
//     for (int j = 0; j < height; j++)
//     {
//       sb.Append($"{map[i, j].ToString()},");
//     }
//     sb.AppendLine();
//   }

//   dataElement.Value = sb.ToString().Trim().TrimEnd(',');
//   xmlDoc.Save(outputName);
// }
// class CatgunFile
// {
//   public string Name { get; set; } = string.Empty;
//   public uint Offset { get; set; }
//   public uint Length { get; set; }
// }
