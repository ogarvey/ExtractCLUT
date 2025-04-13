using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PSX
{
    public class Exhumed
    {
        
    }
}

// var paletteDir = @"C:\Dev\Gaming\psx\Games\Files\EXHUMED\LEVELS\cav_palettes";
// //FileHelpers.DeduplicateBinaryFilesAsync(paletteDir,512);

// var paletteFiles = Directory.GetFiles(paletteDir, "*.bin");

// var palettes = paletteFiles.Select(p => ColorHelper.ReadABgr15Palette(File.ReadAllBytes(p))).ToList();

// var exhumedLevelFile = @"C:\Dev\Gaming\psx\Games\Files\EXHUMED\LEVELS\CAVERN.ZED";

// var exhumedLevelData = File.ReadAllBytes(exhumedLevelFile);

// var decompTest = testDecompress(exhumedLevelData.Skip(0x192950).Take(0x78F8).ToArray());
// File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(exhumedLevelFile), "cavern", "decompoverall.bin"), decompTest);

// var tileData64x64 = exhumedLevelData.Skip(0x20000).Take(0x28000).ToArray();
// var tileData32x32 = exhumedLevelData.Skip(0x48000).Take(0x400).ToArray();
// tileData64x64 = tileData64x64.Concat(exhumedLevelData.Skip(0x48400).Take(64*384).ToArray()).ToArray(); //180BAC
// tileData64x64 = tileData64x64.Concat(exhumedLevelData.Skip(0x180BAC).Take(64 * 704).ToArray()).ToArray();
// tileData32x32 = tileData32x32.Concat(exhumedLevelData.Skip(0x4e400).Take(32 * 512).ToArray()).ToArray();

// var poutputDir = Path.Combine(Path.GetDirectoryName(exhumedLevelFile), "cavern", "tiles");
// Directory.CreateDirectory(poutputDir);

// foreach (var (pal, pIndex) in palettes.WithIndex())
// {
//     var palOutputDir = Path.Combine(poutputDir, pIndex.ToString());
//     Directory.CreateDirectory(palOutputDir);
//     // tiles are 64*64 pixels
//     var tileId = 0;
//     var tileOutputDir = Path.Combine(palOutputDir, "64x64");
//     Directory.CreateDirectory(tileOutputDir);
//     for (int i = 0; i < tileData64x64.Length; i+= 64*64)
//     {
//         var tile = tileData64x64.Skip(i).Take(64*64).ToArray();
//         var image = ImageFormatHelper.GenerateClutImage(pal, tile, 64, 64);
//         image.Save(Path.Combine(tileOutputDir, $"{tileId++}.png"), ImageFormat.Png);
//     }
//     tileId = 0;
//     tileOutputDir = Path.Combine(palOutputDir, "32x32");
//     Directory.CreateDirectory(tileOutputDir);
//     for (int i = 0; i < tileData32x32.Length; i += (32 * 32))
//     {
//         var tile = tileData32x32.Skip(i).Take(32 * 32).ToArray();
//         var image = ImageFormatHelper.GenerateClutImage(pal, tile, 32, 32);
//         image.Save(Path.Combine(tileOutputDir, $"{tileId++}.png"), ImageFormat.Png);
//     }
// }


// byte[] testDecompress(byte[] data)
// {
//     // byte1 is count
//     // byte2 is length of data to repeat
//     // byte3 - byte3+length is data to repeat
//     var output = new List<byte>();
//     for (int i = 0; i+1 < data.Length;)
//     {
//         var count = data[i];
//         var length = data[i + 1];
//         var repeatData = Enumerable.Repeat((byte)0, count).ToList();
//         var pixelData = data.Skip(i + 2).Take(length).ToArray();
//         output.AddRange(repeatData);
//         output.AddRange(pixelData);
//         i += 2 + length;
//     }
//     return output.ToArray();
// }


//  var exhumedFile = @"C:\Dev\Gaming\psx\Games\Files\EXHUMED\WARNING.OWT";
