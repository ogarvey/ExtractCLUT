using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using ExtractCLUT;
using ExtractCLUT.Games.PC;
using ExtractCLUT.Games.PC.ExpectNoMercy;
using ExtractCLUT.Games.PC.FamilyProductions;
using ExtractCLUT.Games.PC.Interspective;
using ExtractCLUT.Games.PC.SoftEnt;
using ExtractCLUT.Games.ThreeDO;
using ExtractCLUT.Games.ThreeDO.GuardianWar;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

var mainDir = @"C:\Dev\Gaming\PC\Win\Games\Silver Merc";
var palFiles = Directory.GetFiles(mainDir, "*.PAL", SearchOption.AllDirectories);
var fx5Files = Directory.GetFiles(mainDir, "*.FX5", SearchOption.AllDirectories);

foreach (var palFile in palFiles)
{
  var palette = ColorHelper.ConvertBytesToRgbIS(File.ReadAllBytes(palFile), translate: true);
  var palOutputDir = Path.Combine(mainDir, "Extracted", Path.GetFileNameWithoutExtension(palFile));
  Directory.CreateDirectory(palOutputDir);

  foreach (var fx5FilePath in fx5Files)
  {
    try
    {

      var fx5 = new Fx5(fx5FilePath, true);
      fx5.ParseImages(palFile);
      var fxOutputPath = Path.Combine(palOutputDir, Path.GetFileNameWithoutExtension(fx5FilePath));
      Directory.CreateDirectory(fxOutputPath);
      fx5.SaveImages(fxOutputPath);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error processing {fx5FilePath}: {ex.Message}");
    }
  }
}

// -- DISREGARD -- THIS IS ONLY EXPERIMENTAL TESTING CODE FOR VARIOUS GAMES AND FILE FORMATS, UNRELATED TO OUR CURRENT FOCUS --

// var paletteBitFilePath = @"C:\Dev\Gaming\PC\Dos\Games\Project-Paradise_DOS_EN\BIT\SYSPAL.BIT";
// var paletteBitFile = BitFile.Load(paletteBitFilePath);
// var palette = BitFile.CreateRgbPalette(paletteBitFile);
// var graphicsRoot = @"C:\Dev\Gaming\PC\Dos\Games\Project-Paradise_DOS_EN\BIT";
// var graphicsArchives = new[] { "GRAFIK.BIT", "GRAFIKB.BIT", "SGRAFIK.BIT", "SYSGRAPH.BIT" };
// foreach (var graphicsArchive in graphicsArchives)
// {
// 	var graphicsFile = BitFile.Load(Path.Combine(graphicsRoot, graphicsArchive));
// 	var pngOutputDirectory = Path.Combine(graphicsRoot, Path.GetFileNameWithoutExtension(graphicsArchive) + "_PNG");
// 	var writtenPngCount = graphicsFile.ExtractGraphicsPngs(pngOutputDirectory, palette);
// 	Console.WriteLine($"{graphicsArchive}: wrote {writtenPngCount} graphics PNGs to {pngOutputDirectory}.");
// }

// var palFileDir = @"C:\Dev\Gaming\PC\Dos\Games\Pee-Gity_DOS_KR\Extracted_HSV";
// var palFiles = Directory.GetFiles(palFileDir, "*.PAL", SearchOption.TopDirectoryOnly);
// var fbkFileDir = @"C:\Dev\Gaming\PC\Dos\Games\Pee-Gity_DOS_KR";
// var fbkFiles = Directory.GetFiles(fbkFileDir, "*.FBK", SearchOption.AllDirectories);

// var mainOutputDir = Path.Combine(fbkFileDir, "FBK");

// foreach (var palFile in palFiles)
// {
//   var palette = ColorHelper.ConvertBytesToRgbIS(File.ReadAllBytes(palFile), translate: true);
//   var palDir = Path.Combine(mainOutputDir, Path.GetFileNameWithoutExtension(palFile));
//   Directory.CreateDirectory(palDir);
//   foreach (var fbkFile in fbkFiles)
//   {
//     var fbk = FbkFile.Load(fbkFile);
//     var fbkOutputDir = fbk.Entries.Count > 1 ? Path.Combine(palDir, Path.GetFileNameWithoutExtension(fbkFile)) : palDir;
//     Directory.CreateDirectory(fbkOutputDir);
//     fbk.SaveImages(fbkOutputDir, palette, fbkFile);
//   }
// }



// var farFileDir = @"C:\Dev\Gaming\3do\Games\Royal-Pro-Wrestling-Jikkyo-Live\Royal\RP_art";
// var farFiles = Directory.GetFiles(farFileDir, "*.FAR", SearchOption.AllDirectories);
// foreach (var farFile in farFiles)
// {
//   var farOutDir = Path.Combine(Path.GetDirectoryName(farFile)!, Path.GetFileNameWithoutExtension(farFile));
//   Directory.CreateDirectory(farOutDir);
//   ExtractFARFile(farFile, farOutDir);

//   var celFiles = Directory.GetFiles(farOutDir, "*.cel", SearchOption.AllDirectories);
//   foreach (var celFile in celFiles)
//   {
//     var magic = Encoding.ASCII.GetString(File.ReadAllBytes(celFile).Take(4).ToArray());
//     if (magic != "CCB ")
//     {
//       // compressed CEL file, decompress it first
//       var decompName = Path.GetFileNameWithoutExtension(celFile) + "_decompressed.cel";
//       var decompData = DecompressRPWCelFile(celFile);
//       File.WriteAllBytes(decompName, decompData);
//     }
//     var outputFilePath = Path.Combine(Path.GetDirectoryName(celFile)!, Path.GetFileNameWithoutExtension(celFile) + ".png");
//     var celImage = CelUnpacker.UnpackAndSaveCelFile(celFile, outputFilePath);
//   }
// }

// void ExtractFARFile(string farFilePath, string outputDir)
// {
//   using var farReader = new BinaryReader(File.OpenRead(farFilePath));
//   var magic = Encoding.ASCII.GetString(farReader.ReadBytes(4));
//   if (magic != "FARY")
//   {
//     throw new Exception($"Invalid FAR file: {farFilePath}");
//   }
//   var dataStartOffset = farReader.ReadBigEndianUInt32();
//   var count = farReader.ReadBigEndianUInt32();
//   farReader.ReadBytes(8);
//   var offsetsAndLengths = new List<(uint offset, uint length)>();

//   for (int i = 0; i < count; i++)
//   {
//     var offset = farReader.ReadBigEndianUInt32();
//     var length = farReader.ReadBigEndianUInt32();
//     offsetsAndLengths.Add((offset, length));
//   }

//   for (int i = 0; i < offsetsAndLengths.Count; i++)
//   {
//     var (offset, length) = offsetsAndLengths[i];
//     farReader.BaseStream.Seek(offset, SeekOrigin.Begin);
//     var data = farReader.ReadBytes((int)length);
//     var outputFilePath = Path.Combine(outputDir, $"file_{i:D4}.cel");
//     Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
//     File.WriteAllBytes(outputFilePath, data);
//   }
// }

// byte[] DecompressRPWCelFile(string filePath)
// {
//   using var reader = new BinaryReader(File.OpenRead(filePath));
//   // skip the first 4 bytes (unknown header)
//   reader.ReadBytes(3);
//   // simple compression: 
//   // if the byte value is < 0x80, it is a pixel count, and the next (value) bytes are the pixel values
//   // if the byte value is >= 0x80, the next byte is the count, and the (value & 0x7F) is the pixel value to repeat (count) times
//   var output = new List<byte>();

//   while (reader.BaseStream.Position < reader.BaseStream.Length)
//   {
//     var value = reader.ReadByte();
//     if (value < 0x80)
//     {
//       // read the next (value) bytes as pixel values
//       var pixels = reader.ReadBytes(value);
//       output.AddRange(pixels);
//     }
//     else
//     {
//       // read the next byte as the count, and repeat the pixel value (value & 0x7F) (count) times
//       var count = reader.ReadByte();
//       var pixelValue = (byte)(value & 0x7F);
//       for (int i = 0; i < count; i++)
//       {
//         output.Add(pixelValue);
//       }
//     }
//   }
//   return output.ToArray();
// }

// var actFileDir = @"C:\Dev\Gaming\3do\Games\Blue-Forest-Story-Kaze-no-Fuin\bfd\battle_chr";
// var actFiles = Directory.GetFiles(actFileDir, "*.ACT", SearchOption.AllDirectories);

// foreach (var actFileTest in actFiles)
// {
//   var actOutPutDir = Path.Combine(Path.GetDirectoryName(actFileTest)!, Path.GetFileNameWithoutExtension(actFileTest));
//   var actFile = new ExtractCLUT.Games.ThreeDO.BlueForestStory.ActFile(actFileTest);
//   actFile.ExportImages(actOutPutDir);
// }


// var v2matoFileDir = @"C:\Dev\Gaming\3do\Games\Lucienne's Quest\SS_DATA\magic";
// var v2matoFiles = Directory.GetFiles(v2matoFileDir, "*.chrs", SearchOption.TopDirectoryOnly);
// foreach (var v2matoFile in v2matoFiles)
// {
//   try
//   {
//     MatoArchive.ExtractArchiveV2(v2matoFile);
//   }
//   catch (Exception ex)
//   {
//     Console.WriteLine($"Error extracting {v2matoFile}: {ex.Message}");
//   }
// }

// var chrMatoFileDir = @"C:\Dev\Gaming\3do\Games\Guardian-War\lsdata\chrdata\";
// var chrMatoFiles = Directory.GetFiles(chrMatoFileDir, "*.chr", SearchOption.TopDirectoryOnly);
// foreach (var chrMatoFile in chrMatoFiles)
// {
//   MatoArchive.ExtractArchiveV1(chrMatoFile);
// }


// var subFileDir = @"C:\Dev\Gaming\3do\Games\Bishoujo Senshi Sailor Moon S\data\ANIM";
// var subFiles = Directory.GetFiles(subFileDir, "super*.pak", SearchOption.TopDirectoryOnly);

// foreach (var subfFile in subFiles)
// {
//   using var subfReader = new BinaryReader(File.OpenRead(subfFile));
//   var magic = Encoding.ASCII.GetString(subfReader.ReadBytes(4));
//   while (magic == "SUBF" && subfReader.BaseStream.Position < subfReader.BaseStream.Length)
//   {
//     var headerStart = subfReader.BaseStream.Position - 4;
//     var headerLength = subfReader.ReadBigEndianInt32();
//     var dataLength = subfReader.ReadBigEndianInt32();
//     var bytesToNextSubf = subfReader.ReadBigEndianInt32();
//     var name = subfReader.ReadNullTerminatedString();
//     // replace any invalid characters with _ (those outside the ascii 0-9a-zA-Z range) from the name
//     name = Path.GetFileName(name);
//     name = string.Concat(name.Select(c => (c < '0' || (c > '9' && c < 'A') || (c > 'Z' && c < 'a') || c > 'z') ? '_' : c));

//     subfReader.BaseStream.Seek(headerStart + headerLength, SeekOrigin.Begin);
//     var data = subfReader.ReadBytes(dataLength);
//     var outputFile = Path.Combine(Path.GetDirectoryName(subfFile)!, Path.GetFileNameWithoutExtension(subfFile).Replace(".", "_"), name);
//     Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
//     File.WriteAllBytes(outputFile, data);
//     subfReader.BaseStream.Seek(headerStart + headerLength + bytesToNextSubf, SeekOrigin.Begin);
//     if (subfReader.BaseStream.Position >= subfReader.BaseStream.Length)
//       break;
//     magic = Encoding.ASCII.GetString(subfReader.ReadBytes(4));
//   }
// }

// var timFolder = @"C:\Dev\Gaming\Sony\PSX\Games\SEIREIX";
// var timFiles = Directory.GetFiles(timFolder, "*.TIM", SearchOption.AllDirectories);

// foreach (var tim in timFiles)
// {
//   var timData = File.ReadAllBytes(tim);
//   var timImage = ImageFormatHelper.ExtractTIMImage(timData);
//   var outputFilePath = Path.Combine(Path.GetDirectoryName(tim)!, Path.GetFileNameWithoutExtension(tim) + ".png");
//   timImage.Save(outputFilePath);
// }


// var gxlDir = @"C:\Dev\Gaming\PC\Dos\Games\Zorro_DOS_EN";
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
//         try
//         {
//           var image = ImageFormatHelper.ConvertPCX(data, false);
//           var outputFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(name) + ".png");
//           image.Save(outputFile);
//           image = ImageFormatHelper.ConvertPCX(data, true);
//           outputFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(name) + "_t.png");
//           image.Save(outputFile);
//         }
//         catch (Exception ex)
//         {
//           Console.WriteLine($"Failed to convert {name} to PNG: {ex.Message}");
//         }
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


// var shakiiMainDir = @"C:\Dev\Gaming\PC\Dos\DiscImages\SAF-Secret-Armored-Force_DOS_KO_Disc-Image\SAF";
// var fx4Files = Directory.GetFiles(shakiiMainDir, "*.FX4", SearchOption.TopDirectoryOnly);
// var fx5Files = Directory.GetFiles(shakiiMainDir, "*.FX5", SearchOption.TopDirectoryOnly);
// var kpfFiles = Directory.GetFiles(shakiiMainDir, "*.KPF", SearchOption.TopDirectoryOnly);
// var palFiles = Directory.GetFiles(shakiiMainDir, "*.PAL", SearchOption.TopDirectoryOnly);

// foreach (var palPath in palFiles)
// {
//   var palette = ColorHelper.ConvertBytesToRgbIS(File.ReadAllBytes(palPath), translate: true);
//   var palOutputDir = Path.Combine(shakiiMainDir, "Extracted", Path.GetFileNameWithoutExtension(palPath));
//   Directory.CreateDirectory(palOutputDir);

//   foreach (var fx5FilePath in fx5Files)
//   {
//     var fx5 = new Fx5(fx5FilePath, true);
//     fx5.ParseImages(palPath);
//     var fxOutputPath = Path.Combine(palOutputDir, Path.GetFileNameWithoutExtension(fx5FilePath));
//     Directory.CreateDirectory(fxOutputPath);
//     fx5.SaveImages(fxOutputPath);
//   }

//   // Standard sprite FX4 files
//   foreach (var fx4FilePath in fx4Files)
//   {
//     try
//     {
//       var fx4 = Fx4File.Load(fx4FilePath);
//       var fx4OutputDir = Path.Combine(palOutputDir, Path.GetFileNameWithoutExtension(fx4FilePath));
//       Directory.CreateDirectory(fx4OutputDir);
//       fx4.SaveImages(fx4OutputDir, palette);
//     }
//     catch (InvalidDataException ex) when (ex.Message.Contains("headerless FX4 / KPF"))
//     {
//       // // These .FX4 files are actually the same RLE cutscene format as .KPF.
//       // try
//       // {
//       //   var kpf = KpfFile.Load(fx4FilePath);
//       //   var kpfOutputDir = Path.Combine(palOutputDir, Path.GetFileNameWithoutExtension(fx4FilePath));
//       //   Directory.CreateDirectory(kpfOutputDir);
//       //   kpf.SaveImages(kpfOutputDir, palette);
//       // }
//       // catch (InvalidDataException kpfEx)
//       // {
//       //   Console.WriteLine($"Skipped {Path.GetFileName(fx4FilePath)}: {kpfEx.Message}");
//       // }
//     }
//   }

//   // Cutscene / screen KPF files (and the matching headerless FX4 variants)
//   // foreach (var kpfFilePath in kpfFiles)
//   // {
//   //   try
//   //   {
//   //     var kpf = KpfFile.Load(kpfFilePath);
//   //     var kpfOutputDir = Path.Combine(palOutputDir, Path.GetFileNameWithoutExtension(kpfFilePath));
//   //     Directory.CreateDirectory(kpfOutputDir);
//   //     kpf.SaveImages(kpfOutputDir, palette);
//   //   }
//   //   catch (InvalidDataException ex)
//   //   {
//   //     Console.WriteLine($"Skipped {Path.GetFileName(kpfFilePath)}: {ex.Message}");
//   //   }
//   // }
// }

// var baseGraphicsDir = @"C:\Dev\Gaming\PC\Win\Games\ALLODS\ALLODS\GRAPHICS";
// var sprite16aPath = @"C:\Dev\Gaming\PC\Win\Games\ALLODS\ALLODS\GRAPHICS\sprites_0000000a.16a";
// var sprite256Path = @"C:\Dev\Gaming\PC\Win\Games\ALLODS\ALLODS\GRAPHICS\sprites_0000000a.256";


// // Palette data is the first 0x400 bytes of the .16a and .256 files, respectively.
// // RGBX format, 256 colors, 4 bytes per color (R,G,B,X), where X is unused/ignored.
// var sprite16PalData = File.ReadAllBytes(sprite16aPath).Take(0x400).ToArray();
// var sprite256PalData = File.ReadAllBytes(sprite256Path).Take(0x400).ToArray();

// using var sprite16Reader = new BinaryReader(File.OpenRead(sprite16aPath));
// using var sprite256Reader = new BinaryReader(File.OpenRead(sprite256Path));

// sprite16Reader.BaseStream.Seek(0x400, SeekOrigin.Begin);
// var spr16Width = sprite16Reader.ReadUInt32();
// var spr16Height = sprite16Reader.ReadUInt32();
// var spr16Length = sprite16Reader.ReadUInt32(); // compressed length of the sprite data
// var spr16Data = sprite16Reader.ReadBytes((int)spr16Length);

// sprite256Reader.BaseStream.Seek(0x400, SeekOrigin.Begin);
// var spr256Width = sprite256Reader.ReadUInt32();
// var spr256Height = sprite256Reader.ReadUInt32();
// var spr256Length = sprite256Reader.ReadUInt32(); // compressed length of the sprite data
// var spr256Data = sprite256Reader.ReadBytes((int)spr256Length);


// byte[] decompressData(byte[] compressedData, int expectedLength, int width)
// {
//     var output = new byte[expectedLength];
//     int srcPos = 0;
//     int dstPos = 0;

//     while (srcPos < compressedData.Length && dstPos < expectedLength)
//     {
//         byte op = compressedData[srcPos++];
//         int count = op & 0x3F;

//         if ((op & 0xC0) == 0x00)
//         {
//             // 0x00-0x3F: draw a run of opaque pixels (palette indices)
//             for (int i = 0; i < count; i++)
//             {
//                 output[dstPos++] = compressedData[srcPos++];
//             }
//         }
//         else if ((op & 0xC0) == 0x40)
//         {
//             // 0x40-0x7F: skip a number of scanlines (vertical transparent run)
//             dstPos += count * width;
//         }
//         else
//         {
//             // 0x80-0xFF: skip a number of pixels (horizontal transparent run)
//             dstPos += count;
//         }
//     }

//     return output;
// }

// Image<Rgba32> Decode16a(byte[] compressedData, int width, int height)
// {
//     var image = new Image<Rgba32>(width, height);
//     int src = 0;
//     int x = 0;
//     int y = 0;

//     while (src + 1 < compressedData.Length && y < height)
//     {
//         ushort cmd = (ushort)(compressedData[src] | (compressedData[src + 1] << 8));
//         src += 2;
//         int count = cmd & 0x3FFF;

//         if ((cmd & 0x4000) != 0)
//         {
//             // 0x4000: vertical skip (whole scanlines)
//             y += count;
//             x = 0;
//             if (y >= height)
//                 break;
//         }
//         else if ((cmd & 0x8000) != 0)
//         {
//             // 0x8000: horizontal skip (transparent pixels on current line)
//             x += count;
//         }
//         else
//         {
//             // 0x0000-0x3FFF: draw a run of RGB565 pixels
//             for (int i = 0; i < count; i++)
//             {
//                 if (src + 1 >= compressedData.Length)
//                     break;
//                 ushort p = (ushort)(compressedData[src] | (compressedData[src + 1] << 8));
//                 src += 2;
//                 if (x < width && y < height)
//                 {
//                     image[x, y] = Rgb565ToRgba32(p);
//                 }
//                 x++;
//             }
//         }

//         if (x >= width)
//         {
//             x = 0;
//             y++;
//         }
//     }

//     return image;
// }

// Rgba32 Rgb565ToRgba32(ushort v)
// {
//     int r = (v >> 11) & 0x1F;
//     int g = (v >> 5) & 0x3F;
//     int b = v & 0x1F;
//     r = (r * 255 + 15) / 31;
//     g = (g * 255 + 31) / 63;
//     b = (b * 255 + 15) / 31;
//     return new Rgba32((byte)r, (byte)g, (byte)b, 255);
// }

// var outputDir = Path.Combine(baseGraphicsDir, "Extracted");
// Directory.CreateDirectory(outputDir);

// var palette256 = ColorHelper.ConvertRgbxIS(sprite256PalData);
// var decompressed256 = decompressData(spr256Data, (int)(spr256Width * spr256Height), (int)spr256Width);
// var image256 = ImageFormatHelper.GenerateIMClutImage(palette256, decompressed256, (int)spr256Width, (int)spr256Height, true, new int[] { 0 });
// image256.SaveAsPng(Path.Combine(outputDir, "sprites_0000000a_256.png"));
// Console.WriteLine($"Saved sprites_0000000a_256.png ({spr256Width}x{spr256Height})");

// var image16 = Decode16a(spr16Data, (int)spr16Width, (int)spr16Height);
// image16.SaveAsPng(Path.Combine(outputDir, "sprites_0000000a_16a.png"));
// Console.WriteLine($"Saved sprites_0000000a_16a.png ({spr16Width}x{spr16Height})");



// var pidFilesPath = @"C:\Dev\Gaming\PC\Win\Games\Gruntz_Win_EN_RIP-Version\Gruntz\GRUNTZ\";
// var pidFiles = Directory.GetFiles(pidFilesPath, "*.PID", SearchOption.AllDirectories);

// foreach (var pidFile in pidFiles)
// {
//     var pid = new PIDFile(pidFile);
//     Console.WriteLine(pid.ToString());
//     var outputDir = Path.Combine(Path.GetDirectoryName(pidFile)!, "Extracted");
//     Directory.CreateDirectory(outputDir);
//     var outputFilePath = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(pidFile)}_{pid.OffsetX}_{pid.OffsetY}.png");
//     // palette is last 0x300 bytes of the data
//     var palData = File.ReadAllBytes(pidFile);   
//     palData = palData.Skip(palData.Length - 0x300).Take(0x300).ToArray();
//     var palette = ColorHelper.ConvertBytesToRgbIS(palData);
//     var image = ImageFormatHelper.GenerateIMClutImage(palette, pid.Data, (int)pid.Width, (int)pid.Height, true, transparencyColor: new Rgba32(255, 0, 132, 255));
//     image.SaveAsPng(outputFilePath);
// }


// var datFile = @"C:\Dev\Gaming\PC\Win\Games\Little-Bombers-Returns_Win_EN_Shareware-version-15\resource.dat";
// using var dataReader = new BinaryReader(File.OpenRead(datFile));
// dataReader.BaseStream.Seek(0x22, SeekOrigin.Begin);
// var zlibData = dataReader.ReadBytes(0x12ea);
// var decompressedData = new ZLibStream(new MemoryStream(zlibData), CompressionMode.Decompress);
// // decompressedData now contains the uncompressed data from the .dat file, 
// // this is comma separated string data, that we need to read line by line and extract the file info from in one of two formats.
// // 1.) Text/data entry (4 fields):
// // name,filename,offset,compressed_size
// // 2.) Image/sprite entry (9 fields):
// // name,filename,sprite_type,has_transparency,tile_height,frame_width,frame_height,offset,compressed_size
// var offsetAdjustment = dataReader.BaseStream.Position;
// var stringReader = new StreamReader(decompressedData);
// var outputDir = Path.Combine(Path.GetDirectoryName(datFile)!, "Extracted");
// var transparentPngDir = Path.Combine(outputDir, "TransparentPng");
// Directory.CreateDirectory(outputDir);
// Directory.CreateDirectory(transparentPngDir);
// var lineIndex = 0;
// while (!stringReader.EndOfStream)
// {
//     var line = stringReader.ReadLine();
//     lineIndex++;
//     if (string.IsNullOrWhiteSpace(line))
//     {
//         continue;
//     }
//     var fields = line.Split(',');
//     if (fields.Length == 4)
//     {
//         var name = fields[0];
//         var filename = fields[1];
//         var offset = int.Parse(fields[2]) + (int)offsetAdjustment;
//         var compressedSize = int.Parse(fields[3]);
//         dataReader.BaseStream.Seek(offset, SeekOrigin.Begin);
//         var compressedData = dataReader.ReadBytes(compressedSize);
//         var outputFilePath = Path.Combine(outputDir, filename);
//         var decompressedDataEntry = new ZLibStream(new MemoryStream(compressedData), CompressionMode.Decompress);

//         using (var outputFileStream = File.Create(outputFilePath))
//         {
//             decompressedDataEntry.CopyTo(outputFileStream);
//         }
//         Console.WriteLine($"Extracted {filename} to {outputFilePath}");
//     }
//     else if (fields.Length == 9)
//     {
//         var name = fields[0];
//         var filename = fields[1];
//         var spriteType = fields[2];
//         var hasTransparency = fields[3] == "1";
//         var tileHeight = int.Parse(fields[4]);
//         var frameWidth = int.Parse(fields[5]);
//         var frameHeight = int.Parse(fields[6]);
//         var offset = int.Parse(fields[7]) + (int)offsetAdjustment;
//         var compressedSize = int.Parse(fields[8]);
//         dataReader.BaseStream.Seek(offset, SeekOrigin.Begin);
//         var compressedData = dataReader.ReadBytes(compressedSize);
//         var decompressedDataEntry = new ZLibStream(new MemoryStream(compressedData), CompressionMode.Decompress);
//         var outputFilePath = Path.Combine(outputDir, filename);
//         using (var outputFileStream = File.Create(outputFilePath))
//         {
//             decompressedDataEntry.CopyTo(outputFileStream);
//         }
//         if (hasTransparency && Path.GetExtension(filename).Equals(".bmp", StringComparison.OrdinalIgnoreCase))
//         {
//             var pngFilePath = Path.Combine(transparentPngDir, Path.ChangeExtension(filename, ".png"));
//             SaveColorKeyPng(outputFilePath, pngFilePath);
//         }
//         Console.WriteLine($"Extracted sprite {filename} to {outputFilePath}");
//     }
//     else
//     {
//         Console.WriteLine($"Warning: Line {lineIndex} has unexpected number of fields ({fields.Length}): {line}");
//     }
// }

// static void SaveColorKeyPng(string bmpFilePath, string pngFilePath)
// {
//     using var image = Image.Load<Rgba32>(bmpFilePath);
//     var keyPixel = image[0, image.Height - 1];

//     for (var y = 0; y < image.Height; y++)
//     {
//         for (var x = 0; x < image.Width; x++)
//         {
//             var pixel = image[x, y];
//             if (pixel.R == keyPixel.R && pixel.G == keyPixel.G && pixel.B == keyPixel.B)
//             {
//                 image[x, y] = new Rgba32(pixel.R, pixel.G, pixel.B, 0);
//             }
//         }
//     }

//     image.SaveAsPng(pngFilePath);
// }


// var csfFileDir = @"C:\Dev\Gaming\PC\Win\DiscImages\Expect-No-Mercy_Win-3x_EN_Win3xO-release\DUDES";
// var csfFiles = Directory.GetFiles(csfFileDir, "*.CSF", SearchOption.TopDirectoryOnly);
// foreach (var csfFile in csfFiles)
// {
//     var csfChunks = FileHelper.ExtractCsFile(csfFile);
//     Console.WriteLine($"Extracted {csfChunks.Count} chunks from {Path.GetFileName(csfFile)}");
//     var csfOutputDir = Path.Combine(Path.GetDirectoryName(csfFile)!, "Extracted", Path.GetFileName(csfFile).Replace(".", "_"));
//     foreach (var (chunk, index) in csfChunks.WithIndex())
//     {
//         var image = FileHelper.ConvertCsFileChunkToImage(chunk);
//         Directory.CreateDirectory(csfOutputDir);
//         var outputFilePath = Path.Combine(csfOutputDir, $"chunk_{index}.png");
//         image.SaveAsPng(outputFilePath);
//         Console.WriteLine($"Saved chunk {index} as {outputFilePath}");
//     }

// }

// var fx5Dir = @"C:\Dev\Gaming\PC\Dos\DiscImages\Rebel-Runner---Operation-Digital-Code_DOS_EN";
// var fx5Files = Directory.GetFiles(fx5Dir, "*.FX5", SearchOption.TopDirectoryOnly);

// var palFiles = Directory.GetFiles(fx5Dir, "*.PAL", SearchOption.TopDirectoryOnly);

// foreach (var fx5File in fx5Files)
// {
//     foreach (var palFile in palFiles)
//     {
//         var fx5 = new Fx5(fx5File, true);
//         fx5.ParseImages(palFile);
//         var fxOutputPath = Path.Combine(Path.GetDirectoryName(fx5File)!, "FX5", $"{Path.GetFileNameWithoutExtension(palFile)}", $"{Path.GetFileNameWithoutExtension(fx5File)}_output");
//         Directory.CreateDirectory(fxOutputPath);
//         fx5.SaveImages(fxOutputPath);
//     }
// }


// var sprPath = @"C:\Dev\Gaming\PC\Dos\DiscImages\Zombie-Wars_Win_EN_RIP-Version\zombiewars\Extracted\GFX\FACES.SPR";
// var rawPath = @"C:\Dev\Gaming\PC\Dos\DiscImages\Zombie-Wars_Win_EN_RIP-Version\zombiewars\Extracted\GFX\ANI1.RAW";

// var sprOutputDir = Path.Combine(Path.GetDirectoryName(sprPath)!, "Extracted", Path.GetFileNameWithoutExtension(sprPath));
// Directory.CreateDirectory(sprOutputDir);

// var palData = File.ReadAllBytes(rawPath).Skip(0x20).Take(0x300).ToArray();
// var palette = ColorHelper.ConvertBytesToRgbIS(palData);

// using var sprReader = new BinaryReader(File.OpenRead(sprPath));

// sprReader.BaseStream.Seek(0x06, SeekOrigin.Begin);
// var sprIndex = 0;
// while (sprReader.BaseStream.Position < sprReader.BaseStream.Length)
// {
//     var width = sprReader.ReadUInt16();
//     var height = sprReader.ReadUInt16();
//     var spriteData = sprReader.ReadBytes(width * height);
//     var image = ImageFormatHelper.GenerateIMClutImage(palette, spriteData, width, height, true, [0]);
//     image.SaveAsPng(Path.Combine(sprOutputDir, $"sprite_{sprIndex}.png"));
//     sprIndex++;
//     sprReader.ReadBytes(4);
// }

// var sb0Path = @"C:\Dev\Gaming\PC\Dos\DiscImages\Zombie-Wars_Win_EN_RIP-Version\zombiewars\LOCAL.SB0";
// var outputDir = Path.Combine(Path.GetDirectoryName(sb0Path)!, "Extracted", Path.GetFileNameWithoutExtension(sb0Path));
// ExtractSb0File(sb0Path, outputDir);

// void ExtractSb0File(string sb0Path, string outputDir)
// {
//     Directory.CreateDirectory(outputDir);

//     using var sb0Reader = new BinaryReader(File.OpenRead(sb0Path));

//     var strLength = sb0Reader.ReadByte();
//     var magic = Encoding.ASCII.GetString(sb0Reader.ReadBytes(strLength));
//     if (magic != "SUB0FILE10")
//     {
//         throw new Exception($"Invalid SB0 file: {sb0Path}");
//     }

//     var sb0Entries = new List<Sb0Entry>();

//     strLength = sb0Reader.ReadByte();
//     var sb0Name = Encoding.ASCII.GetString(sb0Reader.ReadBytes(strLength));
//     var skipCount = 0xC - strLength;
//     sb0Reader.ReadBytes(skipCount);
//     var offset = sb0Reader.ReadUInt32();
//     var size = sb0Reader.ReadUInt32();
//     sb0Entries.Add(new Sb0Entry
//     {
//         Name = sb0Name,
//         Offset = offset,
//         Size = size
//     });

//     while (sb0Reader.BaseStream.Position < offset)
//     {
//         strLength = sb0Reader.ReadByte();
//         var name = Encoding.ASCII.GetString(sb0Reader.ReadBytes(strLength));
//         skipCount = 0xC - strLength;
//         sb0Reader.ReadBytes(skipCount);
//         offset = sb0Reader.ReadUInt32();
//         size = sb0Reader.ReadUInt32();
//         if (offset == 0 || size == 0)
//         {
//             break;
//         }
//         sb0Entries.Add(new Sb0Entry
//         {
//             Name = name,
//             Offset = offset,
//             Size = size
//         });
//     }

//     foreach (var entry in sb0Entries)
//     {
//         sb0Reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
//         var data = sb0Reader.ReadBytes((int)entry.Size);
//         var outputFilePath = Path.Combine(outputDir, entry.Name);
//         File.WriteAllBytes(outputFilePath, data);
//     }
// }

// class Sb0Entry
// {
//     public string Name { get; set; }
//     public uint Offset { get; set; }
//     public uint Size { get; set; }
// }


// var testData = File.ReadAllBytes(testBin);
// var decompressedData = ParseSprite(testData);
// File.WriteAllBytes(@"C:\Dev\Gaming\PC\Dos\DiscImages\L-A-Blaster_DOS_EN\DATA\LASPRITE\Testing\test2_decompressed.bin", decompressedData);

// byte[] ParseSprite(byte[] data)
// {
//   using var dReader = new BinaryReader(new MemoryStream(data));
//   var width = dReader.ReadUInt32();
//   var height = dReader.ReadUInt32();
//   var xPivot = dReader.ReadUInt32();
//   var yPivot = dReader.ReadUInt32();
//   var lineOffsets = new List<uint>();
//   for (int i = 0; i < height + 1; i++)
//   {
//     lineOffsets.Add(dReader.ReadUInt32() + 0x10);
//   }

//   var decompressedData = new byte[width * height];
//   for (int y = 0; y < height; y++)
//   {
//     var lineOffset = lineOffsets[y];
//     var nextLineOffset = lineOffsets[y + 1];
//     var lineLength = nextLineOffset - lineOffset-1;
//     dReader.BaseStream.Seek(lineOffset, SeekOrigin.Begin);
//     var startPixel = dReader.ReadByte();
//     var pixels = dReader.ReadBytes((int)lineLength);
//     // insert pixel data into decompressedData at the correct position
//     Array.Copy(pixels, 0, decompressedData, y * width + startPixel, pixels.Length);
//   }
//   return decompressedData;
// }

// var objFile = @"C:\Dev\Gaming\PC\Dos\DiscImages\Radix-Beyond-the-Void_DOS_EN\Extracted\ObjectBitmaps";
// var palFile = @"C:\Dev\Gaming\PC\Dos\DiscImages\Radix-Beyond-the-Void_DOS_EN\Extracted\Palette[1]";
// var outputDir = @"C:\Dev\Gaming\PC\Dos\DiscImages\Radix-Beyond-the-Void_DOS_EN\Extracted\ObjectBitmaps_Extracted";
// Directory.CreateDirectory(outputDir);
// var failedOutputDir = Path.Combine(outputDir, "Failed");
// Directory.CreateDirectory(failedOutputDir);
// var decompressedOutputDir = Path.Combine(outputDir, "Decompressed");
// Directory.CreateDirectory(decompressedOutputDir);
// var palData = File.ReadAllBytes(palFile);
// var palette = ColorHelper.ConvertBytesToRgbIS(palData, true);
// using var objReader = new BinaryReader(File.OpenRead(objFile));
// var objCount = objReader.ReadUInt16();
// var objEntries = new List<ObjEntry>();
// var tableOffset = objReader.ReadUInt32() - 0x76919B;
// objReader.BaseStream.Seek(tableOffset, SeekOrigin.Begin);
// for (int i = 0; i < objCount; i++)
// {
//   var nameBytes = objReader.ReadBytes(32);
//   var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
//   var offset = objReader.ReadUInt32() - 0x76919B;
//   var width = objReader.ReadUInt16();
//   var height = objReader.ReadUInt16();
//   // check if name already exists in the list, if so, append a number to the name to make it unique
//   var originalName = name;
//   int nameIndex = 1;
//   while (objEntries.Exists(e => e.Name == name))
//   {
//     name = $"{originalName}_{nameIndex}";
//     nameIndex++;
//   }
//   objEntries.Add(new ObjEntry
//   {
//     Name = name,
//     Offset = offset,
//     Width = width,
//     Height = height
//   });
// }

// foreach (var (entry, index) in objEntries.WithIndex())
// {
//   objReader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
//   var dataLength = index < objEntries.Count - 1 ? (int)(objEntries[index + 1].Offset - entry.Offset) : (int)(objReader.BaseStream.Length - entry.Offset);
//   var compressedData = objReader.ReadBytes(dataLength); // read the entire compressed data for this entry
//   try
//   {
//     var decompressedData = FileHelper.DecompressRadixBitmap(compressedData, entry.Width, entry.Height);
//     using (var image = ImageFormatHelper.GenerateIMClutImage(palette, decompressedData, entry.Width, entry.Height, true, new int[] { 0,252, 253, 254, 255 }))
//     {
//       var outputFilePath = Path.Combine(outputDir, $"{entry.Name}.png");
//       image.Mutate(x => x.RotateFlip(RotateMode.Rotate90, FlipMode.Horizontal));
//       image.SaveAsPng(outputFilePath);
//       File.WriteAllBytes(Path.Combine(decompressedOutputDir, $"{entry.Name}_decompressed.bin"), decompressedData);
//     }
//   }
//   catch (Exception ex)
//   {
//     File.WriteAllBytes(Path.Combine(failedOutputDir, $"{entry.Name}_error.bin"), compressedData);
//     Console.WriteLine($"Error processing {entry.Name}: {ex.Message}");
//   }
// }



// namespace ExtractCLUT
// {
//     class Program
//     {
//         static void Main(string[] args)
//         {
//             Console.WriteLine("Dark Legions Asset Decompressor and Extractor (DAT/DMP & DAC/DMC)");
//             Console.WriteLine("===============================================================");

//             const string samplesDir = @"C:\Dev\Gaming\PC\Dos\Games\RequiresInvestigation\DARKLEGIONS\DLEGIONS";
//             string skColPath = Path.Combine(samplesDir, "SK.COL");

//             if (!File.Exists(skColPath))
//             {
//                 Console.WriteLine($"Error: Palette file not found at: {skColPath}");
//                 return;
//             }

//             // 1. Load and Translate Palette (768 bytes starting at offset 0x08)
//             Console.WriteLine("Loading and translating palette from SK.COL...");
//             byte[] colBytes = File.ReadAllBytes(skColPath);
//             if (colBytes.Length < 8 + 768)
//             {
//                 Console.WriteLine("Error: SK.COL file is too small.");
//                 return;
//             }
//             byte[] paletteBytes = new byte[768];
//             Array.Copy(colBytes, 8, paletteBytes, 0, 768);

//             // Convert to IS palette (SK.COL is already 8-bit, so translate is false)
//             List<SixLabors.ImageSharp.Color> palette = ColorHelper.ConvertBytesToRgbIS(paletteBytes, translate: false);
//             Console.WriteLine($"Loaded {palette.Count} colors.");

//             // Find all .DAT and .DAC files
//             var files = new List<string>();
//             files.AddRange(Directory.GetFiles(samplesDir, "*.DAT"));
//             files.AddRange(Directory.GetFiles(samplesDir, "*.DAC"));

//             foreach (var filePath in files)
//             {
//                 string baseName = Path.GetFileNameWithoutExtension(filePath);
//                 string ext = Path.GetExtension(filePath).ToUpperInvariant();
//                 bool isDac = ext == ".DAC";
//                 string dmpPath = Path.Combine(samplesDir, baseName + (isDac ? ".DMC" : ".DMP"));

//                 if (!File.Exists(filePath) || !File.Exists(dmpPath))
//                 {
//                     continue;
//                 }

//                 Console.WriteLine($"\nProcessing {baseName} ({ext})...");

//                 // 2. Read and Parse DMP/DMC
//                 List<DmpEntry> entries = ParseDmp(dmpPath);
//                 Console.WriteLine($"File contains {entries.Count} entries.");

//                 // Load source bytes
//                 byte[] srcBytes = File.ReadAllBytes(filePath);

//                 // For .DAT files, we decompress the entire file as a single contiguous stream first
//                 byte[] decompressedBytes = null;
//                 if (!isDac)
//                 {
//                     decompressedBytes = DecompressRle(srcBytes);
//                     Console.WriteLine($"DAT size: {srcBytes.Length} bytes -> Decompressed: {decompressedBytes.Length} bytes");
//                 }

//                 // 3. Calculate alignment bounding box over all valid frames
//                 int minX = int.MaxValue;
//                 int maxX = int.MinValue;
//                 int minY = int.MaxValue;
//                 int maxY = int.MinValue;
//                 int validCount = 0;

//                 foreach (var entry in entries)
//                 {
//                     if (entry.Width == 0 || entry.Height == 0) continue;
//                     validCount++;

//                     int relLeft = -entry.PivotX;
//                     int relRight = entry.Width - 1 - entry.PivotX;
//                     int relTop = -entry.PivotY;
//                     int relBottom = entry.Height - 1 - entry.PivotY;

//                     minX = Math.Min(minX, relLeft);
//                     maxX = Math.Max(maxX, relRight);
//                     minY = Math.Min(minY, relTop);
//                     maxY = Math.Max(maxY, relBottom);
//                 }

//                 if (validCount == 0)
//                 {
//                     Console.WriteLine("No valid frames to extract.");
//                     continue;
//                 }

//                 int canvasWidth = maxX - minX + 1;
//                 int canvasHeight = maxY - minY + 1;
//                 int canvasPivotX = -minX;
//                 int canvasPivotY = -minY;

//                 Console.WriteLine($"Calculated Aligned Canvas Size: {canvasWidth}x{canvasHeight} (Pivot: {canvasPivotX}, {canvasPivotY})");

//                 // 4. Extract, decompress, and align each frame
//                 string outputDir = Path.Combine(samplesDir, isDac ? "Extracted_DAC" : "Extracted_DAT", baseName);
//                 Directory.CreateDirectory(outputDir);

//                 int extractedCount = 0;
//                 for (int i = 0; i < entries.Count; i++)
//                 {
//                     var entry = entries[i];
//                     if (entry.Width == 0 || entry.Height == 0) continue;

//                     byte[] pixelData;
//                     int expectedSize = entry.Width * entry.Height;

//                     if (isDac)
//                     {
//                         // DMC contains compressed offsets. Each entry is compressed independently.
//                         int compOffset = (int)entry.Offset;
//                         int nextOffset = srcBytes.Length;
//                         for (int j = i + 1; j < entries.Count; j++)
//                         {
//                             if (entries[j].Width > 0 && entries[j].Height > 0)
//                             {
//                                 nextOffset = (int)entries[j].Offset;
//                                 break;
//                             }
//                         }
//                         int compSize = nextOffset - compOffset;

//                         if (compOffset < 0 || compOffset >= srcBytes.Length || compSize <= 0 || compOffset + compSize > srcBytes.Length)
//                         {
//                             Console.WriteLine($"Warning: Entry {i} has invalid compressed offset/size (offset: {compOffset}, size: {compSize})");
//                             continue;
//                         }

//                         byte[] compSlice = new byte[compSize];
//                         Array.Copy(srcBytes, compOffset, compSlice, 0, compSize);

//                         byte[] decompSlice = DecompressRle(compSlice);
//                         if (decompSlice.Length < expectedSize)
//                         {
//                             Console.WriteLine($"Warning: Entry {i} decompressed size is too small (got {decompSlice.Length}, expected {expectedSize})");
//                             continue;
//                         }
//                         pixelData = decompSlice;
//                     }
//                     else
//                     {
//                         // DMP contains decompressed offsets into the globally decompressed DAT buffer.
//                         if (entry.Offset + expectedSize > decompressedBytes.Length)
//                         {
//                             Console.WriteLine($"Warning: Entry {i} goes out of bounds of decompressed DAT buffer (offset: {entry.Offset}, expected: {expectedSize})");
//                             continue;
//                         }
//                         pixelData = new byte[expectedSize];
//                         Array.Copy(decompressedBytes, entry.Offset, pixelData, 0, expectedSize);
//                     }

//                     // Position the sprite relative to the common canvas pivot
//                     int xCanvas = canvasPivotX - entry.PivotX;
//                     int yCanvas = canvasPivotY - entry.PivotY;

//                     // Create canvas pixel array (default initialized to 0, which is the transparent index)
//                     byte[] canvasPixels = new byte[canvasWidth * canvasHeight];

//                     for (int y = 0; y < entry.Height; y++)
//                     {
//                         for (int x = 0; x < entry.Width; x++)
//                         {
//                             byte val = pixelData[y * entry.Width + x];
//                             int destX = xCanvas + x;
//                             int destY = yCanvas + y;
//                             canvasPixels[destY * canvasWidth + destX] = val;
//                         }
//                     }

//                     // Use GenerateIMClutImage to create the colored transparent image
//                     using (Image<Rgba32> image = ImageFormatHelper.GenerateIMClutImage(
//                         palette,
//                         canvasPixels,
//                         canvasWidth,
//                         canvasHeight,
//                         useTransparency: true,
//                         transparencyIndex: 0,
//                         lowerIndexes: true,
//                         fixedIndex: true))
//                     {
//                         string outPath = Path.Combine(outputDir, $"{baseName}_{i:D3}.png");
//                         image.SaveAsPng(outPath);
//                     }
//                     extractedCount++;
//                 }
//                 Console.WriteLine($"Extracted {extractedCount} aligned images to: {outputDir}");
//             }
//         }

//         static byte[] DecompressRle(byte[] src)
//         {
//             List<byte> dest = new List<byte>();
//             int srcIdx = 0;
//             int size = src.Length;

//             while (srcIdx < size)
//             {
//                 byte val = src[srcIdx];
//                 if (val == 0)
//                 {
//                     if (srcIdx + 1 >= size) break;
//                     byte count = src[srcIdx + 1];
//                     for (int i = 0; i < count; i++)
//                     {
//                         dest.Add(0);
//                     }
//                     srcIdx += 2;
//                 }
//                 else
//                 {
//                     dest.Add(val);
//                     srcIdx += 1;
//                 }
//             }

//             return dest.ToArray();
//         }

//         struct DmpEntry
//         {
//             public uint Offset;
//             public byte Width;
//             public byte Height;
//             public byte B6;
//             public byte B7;
//             public byte PivotX;
//             public byte PivotY;
//         }

//         static List<DmpEntry> ParseDmp(string path)
//         {
//             List<DmpEntry> entries = new List<DmpEntry>();
//             using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
//             {
//                 if (reader.BaseStream.Length < 8) return entries;
//                 ushort count = reader.ReadUInt16();
//                 reader.BaseStream.Seek(8, SeekOrigin.Begin); // Skip 8-byte header

//                 for (int i = 0; i < count; i++)
//                 {
//                     if (reader.BaseStream.Position + 10 > reader.BaseStream.Length) break;
//                     DmpEntry entry = new DmpEntry
//                     {
//                         Offset = reader.ReadUInt32(),
//                         Width = reader.ReadByte(),
//                         Height = reader.ReadByte(),
//                         B6 = reader.ReadByte(),
//                         B7 = reader.ReadByte(),
//                         PivotX = reader.ReadByte(),
//                         PivotY = reader.ReadByte()
//                     };
//                     entries.Add(entry);
//                 }
//             }
//             return entries;
//         }
//     }
// }
