using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text;
using DirectXTexNet;
using ExtractCLUT;
using ExtractCLUT.Helpers;
using ExtractCLUT.Model;
using ImageMagick;
using SixLabors.ImageSharp.Formats.Png;
using Color = System.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;
using Image = System.Drawing.Image;
using System.Runtime.InteropServices;
using ExtractCLUT.Games.Generic;
using ManagedBass;
using System.Text.RegularExpressions;
using ExtractCLUT.Games.PC.Activision;

// var dir = @"C:\Dev\Gamin\PC\Win\Games\Adventure-of-Tori_Win_KO_Disc-Image\bmp";

// var outputDir = Path.Combine(dir, "output");
// Directory.CreateDirectory(outputDir);
// var tpOutDir = Path.Combine(dir, "output_tp");
// Directory.CreateDirectory(tpOutDir);
// var files = Directory.GetFiles(dir, "*.bmp", SearchOption.AllDirectories);

// foreach (var file in files)
// {

// }
// foreach (var file in files)
// {
//   using var br = new BinaryReader(File.OpenRead(file));
//   var height = br.ReadUInt32();
//   br.ReadBytes(0x1c);
//   var startOffset = br.ReadUInt32();
//   br.ReadBytes(0x4);
//   var width = br.ReadUInt32();
//   br.BaseStream.Position = startOffset;
//   if (startOffset == 0x36)
//   {
//     // imaage is rgb888
//     var imageData = br.ReadBytes((int)(width * height * 3));
//     var image = ImageFormatHelper.ConvertRGB888(imageData, (int)width, (int)height);
//     var outputFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(file) + ".png");
//     image.RotateFlip(RotateFlipType.RotateNoneFlipY);
//     image.Save(outputFile, ImageFormat.Png);
//     image = ImageFormatHelper.ConvertRGB888(imageData, (int)width, (int)height, true);
//     image.RotateFlip(RotateFlipType.RotateNoneFlipY);               
//     outputFile = Path.Combine(tpOutDir, Path.GetFileNameWithoutExtension(file) + "_tp.png");    
//     image.Save(outputFile, ImageFormat.Png);
//   }
//   else if (startOffset == 0x436)
//   {
//     //image is palettized
//     br.BaseStream.Position = 0x36;
//     var paletteData = br.ReadBytes(0x400);
//     var palette = ColorHelper.ConvertBytesToARGB(paletteData);
//     var imageData = br.ReadBytes((int)(width * height));
//     var image = ImageFormatHelper.GenerateClutImage(palette, imageData, (int)width, (int)height);
//     var outputFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(file) + ".png");
//     image.RotateFlip(RotateFlipType.RotateNoneFlipY);
//     image.Save(outputFile, ImageFormat.Png);
//     image = ImageFormatHelper.GenerateClutImage(palette, imageData, (int)width, (int)height, true,255,false);
//     image.RotateFlip(RotateFlipType.RotateNoneFlipY);
//     outputFile = Path.Combine(tpOutDir, Path.GetFileNameWithoutExtension(file) + "_tp.png");
//     image.Save(outputFile, ImageFormat.Png);
//   } else {
//     Console.WriteLine($"Unknown structure for {file}");
//     continue;
//   }
// }

// var djvFile = @"C:\Dev\Gamin\PS2\Def Jam - Vendetta (USA)\Def Jam - Vendetta (USA)\DATA\DATA.BIN";
// var outputDir = Path.Combine(Path.GetDirectoryName(djvFile), "output");
// Directory.CreateDirectory(outputDir);

// var djvData = File.ReadAllBytes(djvFile);
// var thumbnailData = djvData.Skip(0x80).Take(0x9D780).ToArray();
// var thumbnailOutputDir = Path.Combine(outputDir, "thumbnails");
// Directory.CreateDirectory(thumbnailOutputDir);
// var tim2Data = djvData.Skip(0x9D800).Take(0x873C000).ToArray();
// var pakData = djvData.Skip(0x87d9800).Take(0x571F070).ToArray();

// for (int i = 0, j = 0; i < thumbnailData.Length; i += 0x14c0, j++)
// {
// 	if (i + 0x14bf > thumbnailData.Length) break;
// 	var imageData = thumbnailData.Skip(i+0x60).Take(0x1000).ToArray();
// 	var paletteData = thumbnailData.Skip(i+0x10c0).Take(0x400).ToArray();
// 	var palette = ColorHelper.ConvertBytesToARGB(paletteData);
// 	var image = ImageFormatHelper.GenerateClutImage(palette, imageData, 64, 64, true);
// 	image.Save(Path.Combine(thumbnailOutputDir, $"{j}.png"), ImageFormat.Png);
// }

// var pcxFilePath = @"C:\Dev\Gaming\PC_Windows\Extractions\Math Invaders\output\LP01\Anims\acryo";
// var pcxFiles = Directory.GetFiles(pcxFilePath, "*.pcx*", SearchOption.AllDirectories);
// var paletteFile = @"C:\Dev\Gaming\PC_Windows\Games\Mathinv\PAKS\output\Palette.act";
// var paletteData = File.ReadAllBytes(paletteFile);
// var palette = ConvertBytesToRGBReverse(paletteData);

// foreach (var pcx in pcxFiles)
// {
//   var outputFolder = Path.Combine(Path.GetDirectoryName(pcx), "output");
//   Directory.CreateDirectory(outputFolder);
//   var pcxData = File.ReadAllBytes(pcx);
//   var width = BitConverter.ToUInt32(pcxData.Take(4).ToArray(), 0);
//   var height = BitConverter.ToUInt32(pcxData.Skip(4).Take(4).ToArray(), 0);
//   if (width == 0 || height == 0) continue;
//   var imageData = pcxData.Skip(8).Take((int)(width * height)).ToArray();
//   var image = ImageFormatHelper.GenerateClutImage(palette, imageData, (int)width, (int)height, true);
//   image.Save(Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(pcx) + (Path.GetExtension(pcx).Contains("F") ? "_F" : "") +  ".png"), ImageFormat.Png);
// }

// static List<Color> ConvertBytesToRGBReverse(byte[] bytes)
// {
//   List<Color> colors = new List<Color>();

//   // Start at the last RGB triplet and move backwards
//   for (int i = bytes.Length - 3; i >= 0; i -= 3)
//   {
//     byte red = bytes[i];
//     byte green = bytes[i + 1];
//     byte blue = bytes[i + 2];

//     Color color = Color.FromArgb(red, green, blue);
//     colors.Add(color);
//   }

//   return colors;
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

// var e3Pal = @"C:\Dev\Gaming\PC_Windows\Games\NUKEPC\worlds\delta.pal";
// var e3Terrain = @"C:\Dev\Gaming\PC_Windows\Games\NUKEPC\worlds\delta.tpc";

// var palette = ColorHelper.ConvertBytesToRGB(File.ReadAllBytes(e3Pal));
// var output = Path.Combine(Path.GetDirectoryName(e3Pal), "output", Path.GetFileNameWithoutExtension(e3Pal));
// Directory.CreateDirectory(output);
// var terrainChunk = 0;
// using var br = new BinaryReader(File.OpenRead(e3Terrain));
// br.BaseStream.Position = 0x10;
// while (br.BaseStream.Position < br.BaseStream.Length)
// {
//   // first read 0x4000 byttes to get the tile data
//   var tileData = br.ReadBytes(0x4000);
//   // then create 16 * 16 pixel (256 byte) tile images
//   for (var i = 0; i < 0x4000; i += 256)
//   {
//     var tile = new byte[256];
//     Array.Copy(tileData, i, tile, 0, 256);
//     var tileImage = ImageFormatHelper.GenerateClutImage(palette, tile, 16, 16);
//     tileImage.Save(Path.Combine(output, $"{terrainChunk}_{i / 256}.png"), ImageFormat.Png);
//   }
//   terrainChunk++;
//   br.BaseStream.Seek(0xC000, SeekOrigin.Current);
// }

// var smkDir = @"C:\Dev\Gaming\PC_Windows\Games\PRAGEIV122\RAGE.S16";
// var smkFiles = Directory.GetFiles(smkDir, "*.smk");

// foreach (var smk in smkFiles)
// {
//     var outputDir = Path.Combine(Path.GetDirectoryName(smk), "output", Path.GetFileNameWithoutExtension(smk));
//     Directory.CreateDirectory(outputDir);

//     // use ffmpeg to convert the smk to a series of pngs
//     var process = new Process
//     {
//         StartInfo = new ProcessStartInfo
//         {
//             FileName = "ffmpeg",
//             Arguments = $"-i \"{smk}\" \"{outputDir}\\%04d.png\"",
//             UseShellExecute = false,
//             RedirectStandardOutput = true,
//             CreateNoWindow = true
//         }
//     };
//     process.Start();
//     process.WaitForExit();
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

//  var riptideDir = @"C:\Dev\Gaming\PC\Win\Apps\NebulaFD\NebulaFD\build\Dumps\Super Smashed Bros\Images\Martinio";
// FileHelpers.ResizeImagesInFolder(riptideDir, ExpansionOrigin.BottomCenter);



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

// Files in Raptor Animated Graphics format are single frames which collectively make up a short animated sequence. 
// Each AGX entry in the FAT table makes up a single frame. Each frame in an AGX sequence is drawn on top of the previous frame. 
// The frames are found sequentially in the FAT table, with the same filename, but at different offsets.

// Each file consists of several blocks of data, which are drawn into a 64000-byte (320x200) all-black buffer to create the final frame image.
// Note: The very first frame of a sequence has an extra byte at the beginning.

// Block Format
// Each block entry consists of the following values:

// Data type	Name	Description
// UINT32LE	moreBlocks	0 or 1, indicating if there are more blocks left in the frame
// UINT16LE	destOffset	The offset in the destination buffer to write the block bytes
// UINT16LE	srcBytes	Number of bytes in the block
// UINT8	data[srcBytes] Block bytes as 8bpp raw VGA data
// var agxTestFile = @"C:\Dev\Gaming\PC\Dos\Games\raptor\output\SHIPSD1_AGX";
// var agxOutputDir = Path.Combine(Path.GetDirectoryName(agxTestFile), "output_agx", Path.GetFileNameWithoutExtension(agxTestFile));
// Directory.CreateDirectory(agxOutputDir);
// var palFile = @"C:\Dev\Gaming\PC\Dos\Games\raptor\output\PALETTE_DAT";
// var palData = File.ReadAllBytes(palFile);
// var palette = ColorHelper.ConvertBytesToRGB(palData);
// using var agxReader = new BinaryReader(File.OpenRead(agxTestFile));
// var moreBlocks = agxReader.ReadUInt32();
// var imageArray = new byte[320 * 200];
// var index = 0;
// while (moreBlocks == 1)
// {
//   var destOffset = agxReader.ReadUInt16();
//   var srcBytes = agxReader.ReadUInt16();
//   var blockData = agxReader.ReadBytes(srcBytes);
//   // create a new image with the size of the destination buffer
//   // copy the block data to the image array at the destination offset
//   Array.Copy(blockData, 0, imageArray, destOffset, srcBytes);
//   var image = ImageFormatHelper.GenerateClutImage(palette, imageArray, 320, 200, true);
//   // save the image to a file
//   var outputFile = Path.Combine(agxOutputDir, $"{Path.GetFileNameWithoutExtension(agxTestFile)}_{index++}.png");
//   image.Save(outputFile, ImageFormat.Png);
//   // read the next block
//   moreBlocks = agxReader.ReadUInt32();
// }

// The first two values are unknown. Possibly they might be hotspot coordinates. 
// If iLineCount is 0 then the two values are always 1.
// If iLineCount is 0, then the rest of the file are the raw pixel values (width*height bytes total) 
// and the image has no transparent parts.
// If iLineCount is NOT 0, then the rest of the file consists of multiple sprite layout blocks
// The iLinearOffset value can be used to speed up the drawing process to a 320x200 pixel VGA video buffer. 
//The iLinearOffset value could also be calculated like this:

//iLinearOffset = iPosX + 320 * iPosY
//This formula could also be used as a validity check.

//The actual value of iLineCount should not be used to detect the end of the image data. 
// The PIC files appear to always end with a terminating data block where 
// both iLinearOffset and iCount are set to -1 (0xFFFFFFFF) with no additional pixel data following that block.
// var glb0 = @"C:\Dev\Gaming\PC\Dos\Games\raptor\FILE0000.GLB";
// var glb1 = @"C:\Dev\Gaming\PC\Dos\Games\raptor\FILE0001.GLB";
// var outputDir = @"C:\Dev\Gaming\PC\Dos\Games\raptor\output\FILE0000";
//var outputDir1 = @"C:\Dev\Gaming\PC\Dos\Games\raptor\output\FILE0001";
// GLBExtractor.ExtractGLB(glb0, outputDir);
// GLBExtractor.ExtractGLB(glb1, outputDir1);

// var picFiles = Directory.GetFiles(outputDir1, "*_blk*", SearchOption.AllDirectories);

// foreach (var testPicFile in picFiles)
// {
//   var testPicOutputDir = Path.Combine(Path.GetDirectoryName(testPicFile), "output_blk");
//   Directory.CreateDirectory(testPicOutputDir);

//   using var picReader = new BinaryReader(File.OpenRead(testPicFile));
//   var picFile = new PicFile();
//   picFile.Unknown1 = picReader.ReadUInt32(); // always 1 when iLineCount is 0
//   picFile.Unknown2 = picReader.ReadUInt32(); // always 1 when iLineCount is 0 
//   picFile.LineCount = picReader.ReadUInt32(); // number of non-transparent image lines?
//   picFile.Width = picReader.ReadUInt32(); // width of the image
//   picFile.Height = picReader.ReadUInt32(); // height of the image

//   if (picFile.LineCount == 0)
//   {
//     // read the raw pixel data
//     picFile.Data = picReader.ReadBytes((int)(picFile.Width * picFile.Height));
//     var image = ImageFormatHelper.GenerateClutImage(palette, picFile.Data, (int)picFile.Width, (int)picFile.Height, true);
//     var outputFile = Path.Combine(testPicOutputDir, $"{Path.GetFileNameWithoutExtension(testPicFile)}.png");
//     image.Save(outputFile, ImageFormat.Png);
//   }
//   else
//   {
//     // read the sprite layout blocks
//     while (true)
//     {
//       var block = new PicSpriteLayoutBlock();
//       block.PosX = picReader.ReadUInt32(); // relative to left edge of image
//       block.PosY = picReader.ReadUInt32(); // relative to top edge of image
//       block.LinearOffset = picReader.ReadUInt32(); // relative to top-left pixel of image
//       block.PixelCount = picReader.ReadUInt32(); // number of pixels in this block
//       if (block.PixelCount == uint.MaxValue) break; // end of file marker
//       block.Pixels = picReader.ReadBytes((int)block.PixelCount); // 8bpp raw VGA data, one byte per pixel
//       picFile.SpriteLayoutBlocks.Add(block);
//     }
//     // create overall image from the sprite layout blocks
//     var imageArray = new byte[320 * 200];
//     foreach (var block in picFile.SpriteLayoutBlocks)
//     {
//       // copy the block data to the image array at the destination offset
//       Array.Copy(block.Pixels, 0, imageArray, block.LinearOffset, block.PixelCount);
//     }
//     var image = ImageFormatHelper.GenerateClutImage(palette, imageArray, 320, 200, true);
//     // crop the image to the width and height of the pic file
//     var croppedImage = ImageFormatHelper.CropImage(image, (int)picFile.Width, (int)picFile.Height);
//     // save the image to a file
//     var outputFile = Path.Combine(testPicOutputDir, $"{Path.GetFileNameWithoutExtension(testPicFile)}.png");
//     croppedImage.Save(outputFile, ImageFormat.Png);
//   }

// }


// class PicFile {
//   public uint Unknown1 { get; set; } // Always 1 when iLineCount is 0
//   public uint Unknown2 { get; set; } // Always 1 when iLineCount is 0
//   public uint LineCount { get; set; } // Number of non-transparent image lines?
//   public uint Width { get; set; }
//   public uint Height { get; set; }
//   public byte[]? Data { get; set; } // 8bpp raw VGA data, one byte per pixel; or sprite layout blocks
//   public List<PicSpriteLayoutBlock> SpriteLayoutBlocks { get; set; } = new List<PicSpriteLayoutBlock>();
// }

// class PicSpriteLayoutBlock {
//   public uint PosX { get; set; } // relative to left edge of image
//   public uint PosY { get; set; } // relative to top edge of image
//   public uint LinearOffset { get; set; } // relative to top-left pixel of image
//   public uint PixelCount { get; set; } // number of pixels in this block
//   public byte[] Pixels { get; set; } = Array.Empty<byte>(); // 8bpp raw VGA data, one byte per pixel
// }

// var rncDir = @"C:\Dev\Gaming\PC\Dos\Games\Magic-Pockets_DOS_EN\magic-pockets\Magic Pockets\DATA";
// var allFiles = Directory.GetFiles(rncDir, "*.*", SearchOption.AllDirectories);
// var outputDir = Path.Combine(rncDir, "output");
// Directory.CreateDirectory(outputDir);

// // for each file, call the rnc_propack_x64.exe with the arguments ("u " + file + " " + file + ".unpacked")
// foreach (var file in allFiles)
// {
//   var outputFile = $"{Path.GetFileNameWithoutExtension(file)}_unpacked.{Path.GetExtension(file)}";
//   outputFile = Path.Combine(outputDir, outputFile);
//   var process = new Process
//   {
//     StartInfo = new ProcessStartInfo
//     {
//       FileName = @"rnc_propack_x64.exe",
//       Arguments = $"u \"{file}\" \"{outputFile}\"",
//       UseShellExecute = false,
//       RedirectStandardOutput = true,
//       CreateNoWindow = true
//     }
//   };
//   process.Start();
//   process.WaitForExit();
// }

// var dictPath = @"C:\GOGGames\Bio Menace\EGADICT.BM1";
// var compressedPath = @"C:\GOGGames\Bio Menace\Workspace\EGAGRAPH_BM1_6.bin";
// var outputDir = @"C:\GOGGames\Bio Menace\Workspace\output";
// Directory.CreateDirectory(outputDir);

// byte[] dictionaryData = File.ReadAllBytes(dictPath);
// HuffmanTree huffmanTree = HuffmanDecoder.ReadHuffmanTree(dictionaryData);

// byte[] compressedData = File.ReadAllBytes(compressedPath);
// byte[] decompressedData = HuffmanDecoder.Decompress(huffmanTree, compressedData);

// var converted = ConvertToRgba(decompressedData, 88, 16, 4, 88, 16);
// // Save the image to a file
// using (var bitmap = new Bitmap(converted.Width, converted.Height))
// {
//     for (int y = 0; y < converted.Height; y++)
//     {
//         for (int x = 0; x < converted.Width; x++)
//         {
//             bitmap.SetPixel(x, y, converted.Data[x + y * converted.Width]);
//         }
//     }
//     bitmap.Save(Path.Combine(outputDir, "test.png"), ImageFormat.Png);
// }

// static (Color[] Data, int Width, int Height) ConvertToRgba(byte[] fileContents, int tileWidth, int tileHeight, int numBitPlanes, int imageWidth, int imageHeight)
// {
//   const byte oneThird = 85;
//   const byte twoThirds = 170;
//   var _palette16Colour = new Color[]
//   {
//                 Color.FromArgb(0, 0, 0),
//                 Color.FromArgb(0, 0, twoThirds),
//                 Color.FromArgb(0, twoThirds, 0),
//                 Color.FromArgb(0, twoThirds, twoThirds),

//                 Color.FromArgb(twoThirds, 0, 0),
//                 Color.FromArgb(twoThirds, 0, twoThirds),
//                 Color.FromArgb(twoThirds, oneThird, 0),
//                 Color.FromArgb(twoThirds, twoThirds, twoThirds),

//                 Color.FromArgb(oneThird, oneThird, oneThird),
//                 Color.FromArgb(oneThird, oneThird, 255),
//                 Color.FromArgb(oneThird, 255, oneThird),
//                 Color.FromArgb(oneThird, 255, 255),

//                 Color.FromArgb(255, oneThird, oneThird),
//                 Color.FromArgb(255, oneThird, 255),
//                 Color.FromArgb(255, 255, oneThird),
//                 Color.FromArgb(255, 255, 255),
//   };

//   var _palette4Colour = new Color[]
//   {
//                 Color.FromArgb(0, 0, 0),
//                 Color.FromArgb(0, 255, 255),
//                 Color.FromArgb(255, 0, 255),
//                 Color.FromArgb(255, 255, 255)
//   };

//  var  _palette2Colour = new Color[]
//   {
//                 Color.FromArgb(0, 0, 0),
//                 Color.FromArgb(255, 255, 255)
//   };
//   int NumPixelsPerByte = 8;
//   var planeLengthInBytes = (tileWidth * tileHeight) / NumPixelsPerByte;
//   var imageWidthInTiles = imageWidth / tileWidth;
//   var imageHeightInTiles = imageHeight / tileHeight;

//   var onScreenTextureData = new Color[imageWidthInTiles * tileWidth * imageHeightInTiles * tileHeight];
//   for (var i = 0; i < onScreenTextureData.Length; i++)
//   {
//     onScreenTextureData[i] = Color.Magenta;
//   }

//   for (var y = 0; y < imageHeightInTiles * tileHeight; y++)
//   {
//     for (var x = 0; x < imageWidthInTiles * tileWidth; x++)
//     {
//       var pixelIdx = (x) + (y * imageWidthInTiles * tileWidth);
//       if (pixelIdx > onScreenTextureData.Length - 1)
//       {
//         onScreenTextureData[pixelIdx] = Color.Cyan;
//         continue;
//       }

//       var tileX = x / tileWidth;
//       var tileY = y / tileHeight;
//       var tileIdx = tileX + tileY * imageWidthInTiles;

//       var numBytesInTile = (tileWidth * tileHeight / NumPixelsPerByte * numBitPlanes);
//       var byteIdx = tileIdx * numBytesInTile
//                     + ((x % tileWidth) / NumPixelsPerByte)
//                     + ((y % tileHeight) * (tileWidth) / NumPixelsPerByte);

//       var palleteIdx = 0;
//       for (var i = 0; i < numBitPlanes; i++)
//       {
//         var lookupAddress = byteIdx + (planeLengthInBytes * i);

//         if (lookupAddress > fileContents.Length - 1)
//         {
//           onScreenTextureData[pixelIdx] = Color.Magenta;
//           break;
//         }

//         var relevantBitPosition = pixelIdx % NumPixelsPerByte;

//         var val = (fileContents[lookupAddress] >> (7 - relevantBitPosition)) & 1;
//         palleteIdx = palleteIdx | (val << i);
//       }

//       if (pixelIdx > onScreenTextureData.Length - 1)
//       {
//         Console.WriteLine("Tried to set pixel out of bounds");
//         break;
//       }

//       var paletteToUse = numBitPlanes == 1 ? _palette2Colour
//           : numBitPlanes == 2 ? _palette4Colour : _palette16Colour;
//       onScreenTextureData[pixelIdx] = paletteToUse[palleteIdx];
//     }
//   }

//   return (onScreenTextureData, imageWidthInTiles * tileWidth, imageHeightInTiles * tileHeight);
// }

// var palFile = @"C:\Dev\Gaming\PC\Dos\Games\NAM\NAM\NAM\PALETTE.DAT";
// var paletteData = File.ReadAllBytes(palFile).Take(0x300).ToArray();
// var palette = ColorHelper.ConvertBytesToRGB(paletteData);

// var artFile = @"C:\Dev\Gaming\PC\Dos\Games\NAM\NAM\NAM\TILES000.ART";
// var outputDir = Path.Combine(Path.GetDirectoryName(artFile), "output", Path.GetFileNameWithoutExtension(artFile));
// Directory.CreateDirectory(outputDir);

// using var br = new BinaryReader(File.OpenRead(artFile));
// br.ReadBytes(8); // skip version and unused count;
// var firstIndex = br.ReadUInt32(); // first index
// var lastIndex = br.ReadUInt32(); // last index

// var xSizes = new List<ushort>();
// var ySizes = new List<ushort>();
// var tileAttributes = new List<TileAttribute>();
// // INT16LE[localtileend - localtilestart + 1]  tilesizx array of the x-dimensions of all of the tiles in the file
// // INT16LE[localtileend-localtilestart + 1] tilesizy array of the y-dimensions of all of the tiles in the file
// // INT32LE[localtileend-localtilestart + 1] picanm array of attributes for all the tiles

// for (var i = 0; i < lastIndex - firstIndex + 1; i++)
// {
//   var xSize = br.ReadUInt16();
//   xSizes.Add(xSize);
// }
// for (var i = 0; i < lastIndex - firstIndex + 1; i++)
// {
//   var ySize = br.ReadUInt16();
//   ySizes.Add(ySize);
// }

// for (var i = 0; i < lastIndex - firstIndex + 1; i++)
// {
//   var tileAttribute = br.ReadInt32();
//   var tileAttr = new TileAttribute(tileAttribute);
//   tileAttributes.Add(tileAttr);
//   Console.WriteLine(tileAttr.ToString());
// }

// Console.WriteLine($"Current position: {br.BaseStream.Position}");

// for (int i = (int)firstIndex; i <= lastIndex; i++)
// {
//   var tileData = br.ReadBytes(xSizes[i] * ySizes[i]);
//   var tileImage = ImageFormatHelper.GenerateClutImage(palette, tileData, xSizes[i], ySizes[i], true);
//   var outputFile = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(artFile)}_{i}.png");
//   tileImage.Save(outputFile, ImageFormat.Png);
// }

// class TileAttribute
// {
//   // bits 31-28	| bits 27-24 (4 bit unsigned integer)	| bits 23-16 (8 bit signed integer)	| bits 15-8 (8 bit signed integer)	| bits 7 and 6 (2 bit enumeration)	| bits 5-0 (6-bit unsigned integer)
//   // unused?       Animation speed                       Y-center offset                     X-center offset                     Animation type:                     Number of frames
//   //                                                                                                                             00 = no animation
//   //                                                                                                                             01 = oscillating animation
//   //                                                                                                                             10 = animate forward
//   //                                                                                                                             11 = animate backward
//   public uint AnimationSpeed { get; set; } // bits 27-24 (4 bit unsigned integer)
//   public int YCenterOffset { get; set; } // bits 23-16 (8 bit signed integer)
//   public int XCenterOffset { get; set; } // bits 15-8 (8 bit signed integer)
//   public uint AnimationType { get; set; } // bits 7 and 6 (2 bit enumeration)
//   public uint NumberOfFrames { get; set; } // bits 5-0 (6-bit unsigned integer)

//   public TileAttribute(int tileAttribute)
//   {
//     AnimationSpeed = (uint)((tileAttribute >> 24) & 0x0F);
//     YCenterOffset = (sbyte)((tileAttribute >> 16) & 0xFF);
//     XCenterOffset = (sbyte)((tileAttribute >> 8) & 0xFF);
//     AnimationType = (uint)((tileAttribute >> 6) & 0x03);
//     NumberOfFrames = (uint)(tileAttribute & 0x3F);
//   }
//   public override string ToString()
//   {
//     return $"AnimationSpeed: {AnimationSpeed}, YCenterOffset: {YCenterOffset}, XCenterOffset: {XCenterOffset}, AnimationType: {AnimationType}, NumberOfFrames: {NumberOfFrames}";
//   }
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

var v4vFiles = Directory.GetFiles(@"C:\Dev\Gaming\PC\Win\Games\PITFALL\ASSETS", "*4_V4*.bin", SearchOption.AllDirectories);
foreach (var v4vFile in v4vFiles)
{
  var sOutputDir = Path.Combine(Path.GetDirectoryName(v4vFile), "output", Path.GetFileNameWithoutExtension(v4vFile));
  Directory.CreateDirectory(sOutputDir);
  var sData = File.ReadAllBytes(v4vFile);
  try
  {
    var sprites = V4VSpriteParser.ParseAlignedSpriteSet(sData);
    foreach (var (sprite, index) in sprites.WithIndex())
    {
      var outputFile = Path.Combine(sOutputDir, $"{Path.GetFileNameWithoutExtension(v4vFile)}_{index}.png");
      sprite.Save(outputFile, ImageFormat.Png);
    }
    //File.Move(v4vFile, Path.Combine(doneFolder, Path.GetFileName(v4vFile)));
  }
  catch (Exception ex)
  {
    Console.WriteLine($"Error parsing {v4vFile}: {ex.Message}");
  }
}


var ewFiles = Directory.GetFiles(@"C:\Dev\Gaming\PC\Win\Games\PITFALL\ASSETS", "*.ph");

foreach (var ewFile in ewFiles)
{
  var outputDir = Path.Combine(Path.GetDirectoryName(ewFile), Path.GetFileNameWithoutExtension(ewFile));
  Directory.CreateDirectory(outputDir);
  using var br = new BinaryReader(File.OpenRead(ewFile));
  var index = 0;
  while (br.BaseStream.Position < br.BaseStream.Length)
  {
    var length = br.ReadUInt32();
    var data = br.ReadBytes((int)length);
    var magic = data.Take(4).ToArray();
    var magicString = Encoding.ASCII.GetString(magic);
    // replace anything that is not a letter or number with an underscore
    magicString = Regex.Replace(magicString, @"[^a-zA-Z0-9]", "_");
    if (magicString == "RIFF")
    {
      File.WriteAllBytes(Path.Combine(outputDir, $"init_{index++}_{magicString}.wav"), data);
    }
    else if (magicString != "4_V4" && (index < 5 || magicString.Contains("__")))
    {
      try
      {
        var levelImage = ParseLevelBackground(data, true);
        levelImage.Save(Path.Combine(outputDir, $"init_{index++}.png"), ImageFormat.Png);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error parsing {ewFile} - {index}: {ex.Message}");
        File.WriteAllBytes(Path.Combine(outputDir, $"init_{index++}.bin"), data);
      }
    }
    else
    {
      // check for presence of 0x7e or 0x7f in file
      // if present, save as a .bin file
      if (data.Contains((byte)0x7f))
      {
        // format 2
        File.WriteAllBytes(Path.Combine(outputDir, $"init_{index++}_{magicString}_f2.bin"), data);
      }
      else
      {
        // format 1
        File.WriteAllBytes(Path.Combine(outputDir, $"init_{index++}_{magicString}_f1.bin"), data);
      }
    }
  }
}

static Bitmap ParseLevelBackground(byte[] levelFileData, bool isPitfall = false)
{
  var tileWidth = 8;
  var tileHeight = 8;

  ArgumentNullException.ThrowIfNull(levelFileData);

  if (levelFileData.Length < 20) // Minimum size for header
  {
    throw new ArgumentException("Level file data is too short to contain a valid header.", nameof(levelFileData));
  }

  int dataCursor = 0;

  // Read layer header data
  // Assuming Little Endian format like most Windows systems
  uint tileCCount = BitConverter.ToUInt16(levelFileData, dataCursor);
  dataCursor += isPitfall ? 4 : 2;
  uint tileRCount = BitConverter.ToUInt16(levelFileData, dataCursor);
  dataCursor += isPitfall ? 4 : 6;
  // Offsets are relative to the start of the segment (layerStart, which is 0 here)
  uint tilePoolOffset = BitConverter.ToUInt32(levelFileData, dataCursor);
  dataCursor += 4;
  uint layerPaletteOffset = BitConverter.ToUInt32(levelFileData, dataCursor);
  dataCursor += 4;
  uint paletteColorCount = BitConverter.ToUInt32(levelFileData, dataCursor);
  dataCursor += 4;
  int tileRefsOffset = dataCursor; // Position where tile references start

  // Basic offset validation
  if (layerPaletteOffset >= levelFileData.Length || tilePoolOffset >= levelFileData.Length || tilePoolOffset >= layerPaletteOffset)
  {
    throw new ArgumentException("Invalid offsets detected in header.", nameof(levelFileData));
  }

  // --- Load Palette ---
  // C# List uses 0-based indexing. We'll add transparent as the first color explicitly.
  List<Color> palette = new List<Color>();
  palette.Add(Color.FromArgb(0, 0, 0, 0)); // Index 0 is fully transparent

  dataCursor = (int)layerPaletteOffset; // Move cursor to palette data

  for (int p = 0; p < paletteColorCount; p++)
  {
    if (dataCursor + 3 > levelFileData.Length)
      throw new ArgumentException("Reached end of file while reading palette.", nameof(levelFileData));

    byte r = levelFileData[dataCursor];
    byte g = levelFileData[dataCursor + 1];
    byte b = levelFileData[dataCursor + 2];
    palette.Add(Color.FromArgb(255, r, g, b)); // Add as fully opaque RGB color
    dataCursor += 3;
  }

  // --- Load Tile Pool ---
  dataCursor = (int)tilePoolOffset; // Move cursor to tile pool data
  int tileSizeInBytes = tileWidth * tileHeight; // Each byte is a palette index
  int tilePoolSizeBytes = (int)(layerPaletteOffset - tilePoolOffset);

  if (tilePoolSizeBytes < 0 || tileSizeInBytes == 0)
  {
    throw new ArgumentException("Invalid tile pool size calculation based on offsets.", nameof(levelFileData));
  }
  if (tilePoolSizeBytes % tileSizeInBytes != 0)
  {
    // This might indicate an issue with the assumed tileWidth/tileHeight or the file format
    Console.WriteLine($"Warning: Tile pool size ({tilePoolSizeBytes}) is not an exact multiple of calculated tile data size ({tileSizeInBytes}).");
    // Allow continuing, but it might lead to errors later. A stricter check could throw here.
  }

  int tileCount = tilePoolSizeBytes / tileSizeInBytes;
  List<Bitmap> tilePool = new List<Bitmap>(tileCount);

  for (int t = 0; t < tileCount; t++)
  {
    // Create a bitmap for the tile with Alpha channel support
    Bitmap tileBitmap = new Bitmap(tileWidth, tileHeight, PixelFormat.Format32bppArgb);

    for (int r = 0; r < tileHeight; r++) // Tile row (y)
    {
      for (int c = 0; c < tileWidth; c++) // Tile column (x)
      {
        if (dataCursor >= levelFileData.Length)
          throw new ArgumentException("Reached end of file while reading tile pool data.", nameof(levelFileData));

        byte paletteIndex = levelFileData[dataCursor];

        if (paletteIndex >= palette.Count)
          throw new ArgumentException($"Invalid palette index {paletteIndex} encountered in tile data (Palette size: {palette.Count}).", nameof(levelFileData));

        // Get color from palette (index 0 is transparent, others map directly)
        Color pixelColor = palette[paletteIndex];
        tileBitmap.SetPixel(c, r, pixelColor); // SetPixel uses (x, y) order

        dataCursor++;
      }
    }
    tilePool.Add(tileBitmap);
  }

  // --- Build Composite Image ---
  int compositeWidth = (int)tileCCount * tileWidth;
  int compositeHeight = (int)tileRCount * tileHeight;

  if (compositeWidth <= 0 || compositeHeight <= 0)
  {
    throw new ArgumentException("Invalid composite image dimensions calculated.", nameof(levelFileData));
  }

  Bitmap compositeImage = new Bitmap(compositeWidth, compositeHeight, PixelFormat.Format32bppArgb);

  // Use Graphics object to draw tiles onto the composite image
  using (Graphics graphics = Graphics.FromImage(compositeImage))
  {
    // Optional: Set background to transparent or a default color if needed
    graphics.Clear(Color.Transparent);

    dataCursor = tileRefsOffset; // Move cursor to tile reference data

    for (int r = 0; r < tileRCount; r++) // Tile grid row
    {
      for (int c = 0; c < tileCCount; c++) // Tile grid column
      {
        if (dataCursor + 2 > levelFileData.Length)
          throw new ArgumentException("Reached end of file while reading tile references.", nameof(levelFileData));

        // Get tile entry (16 bits)
        ushort fullVal = BitConverter.ToUInt16(levelFileData, dataCursor);
        dataCursor += 2;

        // Extract index (lower 12 bits) - Assuming 0-based index in file
        int index = fullVal & 0x0FFF; // Mask for lower 12 bits

        if (index >= tilePool.Count)
          throw new ArgumentException($"Invalid tile index {index} encountered in map data (Tile pool size: {tilePool.Count}).", nameof(levelFileData));


        // Extract Flags (upper 4 bits)
        // Bit 13 -> flagsBits & 1
        // Bit 14 -> flagsBits & 2
        // Bit 15 -> flagsBits & 4
        // Bit 16 -> flagsBits & 8 (Sprite Priority - Ignored here)
        int flagsBits = (fullVal >> 12) & 0x0F; // Shift down 12, mask lowest 4
                                                // bool fTilePriority = (flagsBits & 0b0001) != 0; // Ignored
        bool fVertFlip = (flagsBits & 0b0010) != 0;
        bool fHorzFlip = (flagsBits & 0b0100) != 0;
        // bool fSpritePriority = (flagsBits & 0b1000) != 0; // Ignored

        // Get referenced tile
        Bitmap baseTile = tilePool[index];
        Bitmap tileToDraw = baseTile; // Start with the original reference

        // --- Modify tile if needed ---
        // Clone the bitmap *only* if we need to flip it,
        // otherwise RotateFlip modifies the original in the pool.
        if (fVertFlip || fHorzFlip)
        {
          tileToDraw = (Bitmap)baseTile.Clone(); // Work on a copy

          if (fVertFlip && fHorzFlip)
            tileToDraw.RotateFlip(RotateFlipType.RotateNoneFlipXY);
          else if (fVertFlip)
            tileToDraw.RotateFlip(RotateFlipType.RotateNoneFlipY);
          else if (fHorzFlip)
            tileToDraw.RotateFlip(RotateFlipType.RotateNoneFlipX);
        }

        // Calculate position to draw the tile
        int destX = c * tileWidth;
        int destY = r * tileHeight;

        // Stitch tile to picture
        graphics.DrawImage(tileToDraw, destX, destY, tileWidth, tileHeight);

        // If we cloned the tile, dispose the clone after drawing
        if (tileToDraw != baseTile)
        {
          tileToDraw.Dispose();
        }
      }
    }
  } // Graphics object is disposed here

  // --- Cleanup Note ---
  // The bitmaps in tilePool are not explicitly disposed here.
  // They will be garbage collected. If this function were called
  // extremely frequently with large tile pools, managing their
  // disposal might be necessary, but usually GC is sufficient.

  return compositeImage; // Return the final assembled image
}
