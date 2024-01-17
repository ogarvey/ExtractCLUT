using ExtractCLUT.Games;
using ExtractCLUT.Helpers;
using ExtractCLUT.Model;
using ExtractCLUT.Writers;
using System.Drawing.Imaging;
using System.Drawing.Text;
using static ExtractCLUT.Helpers.AudioHelper;
using static ExtractCLUT.Helpers.ColorHelper;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using static ExtractCLUT.Utils;
using static LotRSHelpers;
using ExtractCLUT;
using System.Linq;
using System.Drawing;
using Image = System.Drawing.Image;
using Color = System.Drawing.Color;
using System.Text;
using ManagedBass;
using OGLibCDi.Models;
using System.Diagnostics;

// var files = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\OutputNEW\L2_dat","*.bin");
// // sort the files by the number in the file name
// files = files.OrderBy(x => int.Parse(Path.GetFileNameWithoutExtension(x).Split('_').Last())).ToArray();

// var parsedData = new List<HMDatFile>();

// foreach (var file in files)
// {
//   parsedData.Add(HotelMarioHelper.ParseDatFile(file));
// }

// var datString = new StringBuilder();

// datString.AppendLine("Hotel Mario DAT Files - Parsed Data Output");
// datString.AppendLine($"Found {parsedData.Count} DAT files");
// datString.AppendLine("==========================================");
// datString.AppendLine();

// foreach (var dat in parsedData)
// {
//   datString.AppendLine($"File: {dat.OriginalFile}");
//   datString.AppendLine($"Internal File: {dat.Text}");
//   datString.AppendLine($"Found {dat.SpriteNames.Count} sprites");
//   datString.AppendLine($"Sprite Names: {string.Join(", ", dat.SpriteNames)}");
//   datString.AppendLine($"Sprite Name Offsets: {string.Join(", ", dat.SpriteNameOffsets)}");
//   datString.AppendLine($"Sprite Data Offsets: {string.Join(", ", dat.SpriteDataOffsets)}");
//   datString.AppendLine();
//   datString.AppendLine("==========================================");
//   datString.AppendLine();
// }

// File.WriteAllText(@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\OutputNEW\L2_dat\parsedData.txt", datString.ToString());

//MysticMidwayHelper.ParseRIPVideo(@"C:\Dev\Projects\Gaming\CD-i\Mystic Midway_ Rest in Pieces\ripvideo.rtf");

// var inputFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Treasures of the Lost Pyramid\Output\pyranim\video\pyranim.rtr_1_1_1_data.bin";

// var parsed = FileHelpers.SplitBinaryFileIntoChunks(inputFile, new byte[] { 0x00, 0x00, 0x00 }, false, true,0);

// var palBytes = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Treasures of the Lost Pyramid\Output\pyrdata\pyrdata.rtr_1_0_0.bin");

// var palette = ReadClutBankPalettes(palBytes,2);
// var images = new List<Image>();
// foreach (var (item, index) in parsed.WithIndex())
// {
//   var image = GenerateRle7Image(palette, item, 384, 240, true);
//   images.Add(image.Scale4());
// }

// var gifOutputPath = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Treasures of the Lost Pyramid\Output\pyranim\video\output\pyranim1_x4_tp.gif";

// CreateGifFromImageList(images, gifOutputPath,10);

// var input = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Alien Gate\ENEMIES\output\enemy1.dat_1_1_0.bin";
// var data = File.ReadAllBytes(input);
// var offsets = new List<int>();
// for (var i = 0; i < 0x60; i+=4)
// {
//   var bytes = data.Skip(i).Take(4).Reverse().ToArray();
//   var value = BitConverter.ToInt32(bytes, 0);
//   offsets.Add(value);
// }

// var spriteChunks = new List<byte[]>();

// foreach (var (offset, index) in offsets.WithIndex())
// {
//   var bytesToTake = index == offsets.Count - 1 ? data.Length - offset : offsets[index + 1] - offset;
//   var chunk = data.Skip(offset).Take(bytesToTake).ToArray();
//   File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Alien Gate\ENEMIES\output\sprites\sprite_{index}.bin", chunk);
//   spriteChunks.Add(chunk);
// }

// foreach (var (chunk, index) in spriteChunks.WithIndex())
// {
//   try {
//     var output = CompiledSpriteHelper.DecodeCompiledSprite(chunk);
//     File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Alien Gate\ENEMIES\output\sprites\sprite_decoded_{index}.bin", output);
//   } catch (Exception ex) {
//     Console.WriteLine($"Error decoding sprite {index}: {ex.Message}");
//   }
// }

// var rl7InputDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Zombie Dinos from Planet Zeltoid\output\ZDAssets\RL7";
// var dataInputDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Zombie Dinos from Planet Zeltoid\output\ZDAssets\Palette with Sector Counts";

// var rl7InputFiles = Directory.GetFiles(rl7InputDir, "*.bin").OrderBy(x => int.Parse(Path.GetFileNameWithoutExtension(x).Split('_').Last())).ToArray();
// var dataInputFiles = Directory.GetFiles(dataInputDir, "*.bin").OrderBy(x => int.Parse(Path.GetFileNameWithoutExtension(x).Split('_').Last())).ToArray();

// var outputPath = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Zombie Dinos from Planet Zeltoid\output\ZDAssets\Decoded\";

// foreach (var (file,index) in dataInputFiles.WithIndex())
// {
//   ZombieDinoHelper.ParseDataFile(file);
//   var images = ZombieDinoHelper.ParseAnimFile(rl7InputFiles[index], true);
//   var gifOutputPath = Path.Combine(outputPath, $"RLV_{index}.gif");

//   CreateGifFromImageList(images, gifOutputPath, 10);
// }

// var hmDatInput = @"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\Sprite Work\L8\L8_dat.rtf_1_16_4.bin";

// var hmDat = HotelMarioHelper.ParseDatFile(hmDatInput);


// var datString = new StringBuilder();
// datString.AppendLine("Hotel Mario DAT File - Parsed Data Output");
// datString.AppendLine("==========================================");
// datString.AppendLine();

//   datString.AppendLine($"File: {hmDat.OriginalFile}");
//   datString.AppendLine($"Internal File: {hmDat.Text}");
//   datString.AppendLine($"Found {hmDat.SpriteNames.Count} sprites");
//   datString.AppendLine($"Sprite Names: {string.Join(", ", hmDat.SpriteNames)}");
//   datString.AppendLine($"Sprite Name Offsets: {string.Join(", ", hmDat.SpriteNameOffsets)}");
//   datString.AppendLine($"Sprite Data Offsets: {string.Join(", ", hmDat.SpriteDataOffsets)}");
//   datString.AppendLine();
//   datString.AppendLine("==========================================");
//   datString.AppendLine();

// File.WriteAllText(@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\Sprite Work\L8\parsedData.txt", datString.ToString());
var sb = new StringBuilder();

// var rtrFile = @"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\BurnCycleCloned.rtr";

// var bcCdiFile = new CdiFile(rtrFile);

// //BurnCycle.ExtractClutChannel(bcCdiFile,8);
// var clut7Sectors = bcCdiFile.VideoSectors.Where(x => x.Coding.VideoString == "CLUT7" && x.Channel == 1).OrderBy(x => x.Channel)
//   .ThenBy(x => x.SectorIndex).ToList();

// var dataSectors = bcCdiFile.Sectors.Where(x => x.SubMode.IsData && x.Channel == 1).OrderBy(x => x.Channel)
//   .ThenBy(x => x.SectorIndex).ToList();



// var images = new List<Image>();

// var sectorGroups = new List<List<CdiSector>>();
// var sectorCounts = new List<int>();

// var bcDataList = new List<BurnCycleSectorData>();

// byte?[] mask = [0x00, 0x09, 0x01, 0x8C, null, null, null, null, 0x02, 0x00, 0x80, 0x80];
// BurnCyclePaletteData? mostRecentPalette = null;
// foreach (var (sector, index) in dataSectors.WithIndex())
// {
//   //File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\Exploration\Palettes w Data\BurnCycleCloned.rtr_1_1_{sector.SectorIndex}.bin", sector.GetSectorData());
//   var bytes = sector.GetSectorData();
//   var paletteList = new List<BurnCyclePaletteData>();
//   var offsetList = new List<BurnCycleOffsetData>();
//   List<int> positions = BurnCycle.FindPaletteSequence(bytes, mask);
//   if (positions.Count > 0)
//   {
//     foreach (var position in positions)
//     {
//       var palBytes = bytes.Skip(position + 12).Take(0x180).ToArray();
//       var frameIndexBytes = bytes.Skip(position + 6).Take(2).Reverse().ToArray();
//       var frameIndex = BitConverter.ToInt16(frameIndexBytes, 0);
//       var pal = ConvertBytesToRGB(palBytes);
//       var palette = new BurnCyclePaletteData()
//       {
//         FirstFrame = frameIndex,
//         Palette = pal,
//         Offset = position + 12,
//         Sector = sector.SectorIndex
//       };
//       paletteList.Add(palette);
//     }
//   }

//   var counts = BurnCycle.ParseSectorCounts(bytes);

//   var offsets = BurnCycle.ParseOffsetData(bytes);

//   foreach (var (offset, oIndex) in offsets.WithIndex())
//   {

//     BurnCyclePaletteData? palette = null;
//     if (paletteList.Count == 0 || offset.Item1 == 0)
//     {
//       palette = mostRecentPalette;
//     }
//     else if (paletteList.Count == 1)
//     {
//       palette = paletteList[0];
//     }
//     else if (paletteList.Count > 1)
//     {
//       // if (offset.Item3 >= 303) {
//       //   Debugger.Break();
//       // }
//       var nextPalette = paletteList.Where(x => x.FirstFrame - 1 <= offset.Item3).ToList();
//       palette = nextPalette.Count > 0 ? paletteList.Where(x => x.FirstFrame - 1 <= offset.Item3)?.OrderByDescending(x => x.FirstFrame)?.FirstOrDefault() : mostRecentPalette;
//     }
//     offsetList.Add(new BurnCycleOffsetData()
//     {
//       Offset = offset.Item1,
//       OffsetIndex = offset.Item3,
//       Sector = sector.SectorIndex,
//       Palette = palette
//     });
//     mostRecentPalette = palette;
//   }


//   var bcsd = new BurnCycleSectorData()
//   {
//     Index = sector.SectorIndex,
//     Palettes = paletteList,
//     Offsets = offsetList,
//     SectorCounts = counts,
//     RleByteGroups = new List<byte[]>()
//   };
//   bcDataList.Add(bcsd);
// }

// var currentOffset = 0;
// var byteGroupIndex = 0;

// foreach (var (bcs, index) in bcDataList.WithIndex())
// {
//   foreach (var sc in bcs.SectorCounts)
//   {
//     var group = clut7Sectors.Take(sc).ToList();
//     var bytes = group.SelectMany(x => x.GetSectorData()).ToArray();
//     bcs.RleByteGroups.Add(bytes);
//     clut7Sectors.RemoveRange(0, sc);
//   }

//   //if (bcs.RleByteGroups.Count > 0 ) File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\Output\output\binary\clut7_{bcs.Index}.bin", bcs.RleByteGroups.SelectMany(x => x).ToArray());
// }

// var allByteGroups = bcDataList.SelectMany(x => x.RleByteGroups).ToList();
// var currentPaletteFrame = 1;
// var imageCounter = 1;
// foreach (var (bcs, index) in bcDataList.WithIndex())
// {
//   try
//   {
//     foreach (var (offset, oIndex) in bcs.Offsets.WithIndex())
//     {
//       if (offset.Offset == currentOffset)
//       {
//         continue;
//       }
//       if ((oIndex + 1) < bcs.Offsets.Count && bcs.Offsets[oIndex + 1].Offset < currentOffset)
//       {
//         currentOffset = 0;
//         byteGroupIndex++;
//         continue;
//       }

//       var bytesToTake = 0;
//       if (oIndex == bcs.Offsets.Count - 1)
//       {
//         bytesToTake = allByteGroups[byteGroupIndex].Length - offset.Offset;
//       }
//       else if (currentOffset == 0)
//       {
//         bytesToTake = offset.Offset;
//       }
//       else
//       {
//         bytesToTake = bcs.Offsets[oIndex + 1].Offset - offset.Offset;
//       }

//       var bytes = currentOffset == 0 ? allByteGroups[byteGroupIndex].Take(bytesToTake).ToArray() : allByteGroups[byteGroupIndex].Skip(offset.Offset).Take(bytesToTake).ToArray();
//       var palette = offset.Palette;
//       if (palette.FirstFrame != currentPaletteFrame)
//       {
//         var gifOutputPath = Path.Combine(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\Output\output\gifs", $"BC_{index}_{bcs.Index}_{currentPaletteFrame:x4}.gif");
//         CreateGifFromImageList(images, gifOutputPath, 10);
//         images.Clear();
//         currentPaletteFrame = palette.FirstFrame;
//       }
//       var image = GenerateRle7Image(palette.Palette, bytes, 384, 240, false);
//       images.Add(image);
//       currentOffset = offset.Offset;
//     }

//   }
//   catch (System.Exception)
//   {
//     continue;
//   }
// }


var boltFile1 = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Defender of the Crown\DOC0.rtb_1_16_0.bin");
var boltFile2 = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\LABYRINTH OF CRETE\outputt\boltlib0.blt_1_16_CLUT8_Normal_Even_0.bin");

var boltData1Size = BoltFileHelper.GetEndOfBoltFile(boltFile1);
var boltData2Size = BoltFileHelper.GetEndOfBoltFile(boltFile2);

Console.WriteLine($"Bolt File 1 Size: {boltData1Size}");
Console.WriteLine($"Bolt File 2 Size: {boltData2Size}");
