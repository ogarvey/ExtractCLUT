using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using ExtractCLUT.Games.PSX.Alundra;
using ExtractCLUT.Games.PC.EoL;
using ExtractCLUT.Helpers;
using ExtractCLUT.Models.AniMagic;
using SixLabors.ImageSharp;

var alundraDataFile = @"C:\Dev\Gaming\Sony\PSX\Games\Alundra\DATA\DATAS.BIN";
var alundraOutDir = @"C:\Dev\Gaming\Sony\PSX\Games\Alundra\Extracted";
AlundraHelper.ExtractDatasBin(alundraDataFile, alundraOutDir, renderMapSamples: 12);
Console.WriteLine("Alundra DATAS.BIN extracted");




// Nothing below this line is currently being used, but it may be useful for future reference.



// var rscFile = @"C:\Dev\Gaming\PC\Win\Games\JSKGM\JSKGM\NERO003\NERO003.RSC";
// var outputDir = @"C:\Dev\Gaming\PC\Win\Games\JSKGM\JSKGM\NERO003\Extracted";
// Directory.CreateDirectory(outputDir);


// var rscFileV2 = new RscFileV2(rscFile);
// rscFileV2.ProcessBmpTable();
// rscFileV2.ExportBmpImages(outputDir);

// var pcxHeader = new byte[] { 0x0A, 0x05, 0x01, 0x08 };
// var wavHeader = new byte[] { 0x52, 0x49, 0x46, 0x46 };

// var rsfFile = @"C:\Dev\Gaming\PC\Dos\Games\Into-the-Sun-Projected-Distruction_DOS_EN\I2TS.RSF";
// using var rsfReader = new BinaryReader(File.OpenRead(rsfFile));
// rsfReader.BaseStream.Position = 0xC08018;
// var outputFolder = @"C:\Dev\Gaming\PC\Dos\Games\Into-the-Sun-Projected-Distruction_DOS_EN\Extracted";
// Directory.CreateDirectory(outputFolder);

// var pcxFolder = Path.Combine(outputFolder, "PCX");
// Directory.CreateDirectory(pcxFolder);

// var wavFolder = Path.Combine(outputFolder, "WAV");
// Directory.CreateDirectory(wavFolder);

// var palFolder = Path.Combine(outputFolder, "PAL");
// Directory.CreateDirectory(palFolder);

// var unknownFolder = Path.Combine(outputFolder, "Unknown");
// Directory.CreateDirectory(unknownFolder);

// var count = 0xed;
// var fileSizes = new List<int>();
// for (int i = 0; i < count; i++)
// {
//     rsfReader.ReadBytes(13); // Skip 13 bytes
//     var fileSize = rsfReader.ReadInt32();
//     fileSizes.Add(fileSize);
// }

// rsfReader.BaseStream.Position = 0x22;

// for (int i = 0; i < count; i++)
// {
//     var fileSize = fileSizes[i];
//     var fileData = rsfReader.ReadBytes(fileSize);
//     var first4Bytes = fileData.Take(4).ToArray();
//     switch (first4Bytes)
//     {
//         case var _ when first4Bytes.SequenceEqual(pcxHeader):
//         var image = ImageFormatHelper.ConvertPCXToImageSharp(fileData);
//             var pcxFilePath = Path.Combine(pcxFolder, $"file_{i:D3}.png");
//             image.SaveAsPng(pcxFilePath);
//             break;
//         case var _ when first4Bytes.SequenceEqual(wavHeader):
//             var wavFilePath = Path.Combine(wavFolder, $"file_{i:D3}.wav");
//             File.WriteAllBytes(wavFilePath, fileData);
//             break;
//         default:
//             if (fileSize == 0x300)
//             {
//                 var palFilePath = Path.Combine(palFolder, $"file_{i:D3}.pal");
//                 File.WriteAllBytes(palFilePath, fileData);
//                 break;
//             }
//             var unknownFilePath = Path.Combine(unknownFolder, $"file_{BitConverter.ToUInt32(first4Bytes, 0):X8}_{i:D3}.bin");
//             File.WriteAllBytes(unknownFilePath, fileData);
//             break;
//     }
// }

// var fx5Path = @"C:\Dev\Gaming\PC\Dos\Games\Eol-ui_Moheom_1995\EoluiM\STAGE1_1.IMG";
// var palPath = @"C:\Dev\Gaming\PC\Dos\Games\Eol-ui_Moheom_1995\EoluiM\STAGE1_1.PAL";
// var mapPath = @"C:\Dev\Gaming\PC\Dos\Games\Eol-ui_Moheom_1995\EoluiM\STAGE1_1.MAP";
// using var mapReader = new BinaryReader(File.OpenRead(mapPath));
// mapReader.BaseStream.Position = 0x64;
// ushort width = mapReader.ReadUInt16();
// ushort height = mapReader.ReadUInt16();
// mapReader.BaseStream.Position = 0x100;
// ushort[] mapShorts = new ushort[width * height];
// for (int i = 0; i < width * height; i++)
// {
//     mapShorts[i] = (ushort)(mapReader.ReadUInt16() & 0xFFF);
// }

// var fx5 = new Fx5(fx5Path);
// fx5.ParseImages(palPath, false);

// var images = fx5.Images;

// var screenImage = ImageFormatHelper.CreateScreenImage(images, mapShorts, width, height, 16, 16);
// screenImage.Save(@"C:\Dev\Gaming\PC\Dos\Games\Eol-ui_Moheom_1995\EoluiM\Extracted\Stage1_1FX5\screen.png");
