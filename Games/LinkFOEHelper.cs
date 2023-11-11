using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using ExtractCLUT.Writers;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using Color = System.Drawing.Color;

namespace ExtractCLUT.Games
{
  public static class LinkFOEHelper
  {
    
  }
}

// lanim/zanim data-eor folder has the anims, read through a file, split into chunks of 0x3800 bytes, parse each chunk as an rle frame

// var file = @"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\records\ldata\data\ldata_d_1.bin";
// var paletteBytes = LinkFOEHelper.FindSequenceAndGetPriorBytes(file, new byte[] { 0x49, 0x44, 0x41, 0x54 }, 384);
// var palette = ColorHelper.ConvertBytesToRGB(paletteBytes);

// byte[] sequenceToFind = new byte[] { 0x00, 0x00, 0x00, 0x80, 0xFF, 0xFF, 0xFF };
// List<long> offsets = FileHelpers.FindSequenceOffsets(DataFile, sequenceToFind);

// var bytes = File.ReadAllBytes(DataFile);

// foreach (var offset in offsets)
// {
//   var paletteBytes = bytes.Skip((int)offset + 0x4).Take(0x188).ToArray();
//   File.WriteAllBytes(Path.Combine(baseDir, $"NewRecords\\ldata\\output\\palettes\\{offset}.bin"), paletteBytes);
// } 
