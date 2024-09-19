using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using Color = System.Drawing.Color;

namespace ExtractCLUT.Games
{
  public static class LinkFOEHelper
  {
    
  }
}

// var file = @"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\Exploration\ldata\PAC\ldata.rtr_1_2_176_PAC.bin";
// var data = File.ReadAllBytes(file);
// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\Exploration\ldata\PacChunks";
// Directory.CreateDirectory(outputFolder);

// var blobs = FileHelpers.ExtractSpriteByteSequences(null, data, [0x20, 0x2f], [0x4e, 0x75]);

// var defaultPalette = ReadClutBankPalettes(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\Exploration\ldata\Palettes\ldata.rtr_1_1_175_CLUT_BANKS.bin").Take(520).ToArray(),2);


// foreach (var (blob, index) in blobs.WithIndex())
// {
//     var decodedBlob = CompiledSpriteHelper.DecodeCompiledSprite(blob, 0, 416);
//     //File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\Exploration\ldata\Spriting\{index}.bin", decodedBlob);
//     var image = ImageFormatHelper.GenerateClutImage(defaultPalette, decodedBlob, 416, 92, true);
//     // if the entire image is FULLY transparent, skip it, if not save it
//     if (!IsImageFullyTransparent(image))
//     {
//         var outputName = Path.Combine(outputFolder, $"{index}.png");
//         if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
//         {
//             image.Save(outputName, ImageFormat.Png);
//         }
//     }
// }

// CropImageFolder(outputFolder, "*.png", 0, 0, 72, 56, true);

// var dataSize = BitConverter.ToInt32(data.Skip(0x4).Take(0x4).Reverse().ToArray(), 0);
// var dataOffset = BitConverter.ToInt32(data.Skip(0x8).Take(0x4).Reverse().ToArray(), 0);

// List<int> offsets = [0];

// for (int i = 0x10; i < dataOffset; i += 4)
// {
//     var offset = BitConverter.ToInt32(data.Skip(i).Take(4).Reverse().ToArray(), 0);
//     offsets.Add(offset);
// }

// var chunks = new List<byte[]>();

// for (int i = 0; i < offsets.Count; i++)
// {
//     var start = dataOffset + offsets[i];
//     var end = (i == offsets.Count - 1 ? dataSize : offsets[i + 1]) + dataOffset;
//     var chunk = data.Skip(start).Take(end - start).ToArray();
//     chunks.Add(chunk);
// }

// foreach (var (chunk, index) in chunks.WithIndex())
// {
//     File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\Exploration\ldata\PacChunks\{Path.GetFileNameWithoutExtension(file)}_{index}.bin", chunk);
// }
