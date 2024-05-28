using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games
{
    public class WhackABubble
    {

//         var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Whack a Bubble\sprites.dat";

//         var cdiFile = new CdiFile(file);
//         var data = cdiFile.DataSectors.OrderBy(s => s.Channel).ThenBy(s => s.SectorIndex).SelectMany(s => s.GetSectorData()).ToArray();

//         var spriteOffsetList = new List<uint>();
//         var clutOffsetList = new List<uint>();

//         var spriteOffsetData = data.Skip(12).Take(0x200).ToArray();
//         var clutOffsetData = data.Skip(0x20c).Take(0x250).ToArray();

//         var palette1Data = data.Skip(0x4a0).Take(0x208).ToArray();
//         var palette2Data = data.Skip(0x6a8).Take(0x208).ToArray();

// for (int i = 0; i<spriteOffsetData.Length; i += 4)
// {
//   var offset = BitConverter.ToUInt32(spriteOffsetData.Skip(i).Take(4).Reverse().ToArray(), 0);
//         spriteOffsetList.Add(offset);
// }

// for (int i = 0; i<clutOffsetData.Length; i += 4)
// {
//   var offset = BitConverter.ToUInt32(clutOffsetData.Skip(i).Take(4).Reverse().ToArray(), 0);
//     clutOffsetList.Add(offset);
// }

// var palette1 = ReadClutBankPalettes(palette1Data, 2);
// var palette2 = ReadClutBankPalettes(palette2Data, 2);

// var spriteOutput = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Whack a Bubble\Analysis\sprites";
// Directory.CreateDirectory(spriteOutput);
// var blobOutput = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Whack a Bubble\Analysis\blobs";
// Directory.CreateDirectory(blobOutput);

// var spriteIndex = 0;
// for (int i = 0; i < spriteOffsetList.Count; i++)
// {
//     var width = BitConverter.ToUInt16(data.Skip((int)spriteOffsetList[i]).Take(2).Reverse().ToArray(), 0);
//     var height = BitConverter.ToUInt16(data.Skip((int)spriteOffsetList[i] + 2).Take(2).Reverse().ToArray(), 0);

//     var start = spriteOffsetList[i] + 12;
//     var end = i == spriteOffsetList.Count - 1 ? data.Length : (int)spriteOffsetList[i + 1];
//     var blob = data.Skip((int)start).Take((int)(end - start)).ToArray();
//     File.WriteAllBytes($@"{blobOutput}\{spriteOffsetList[i]}_{i}.bin", blob);
//     var startIndex = 0;
//     for (int j = 0; j < blob.Length; j++)
//     {
//         if (blob[j] == 0x4e && blob[j + 1] == 0x75)
//         {
//             var output = CompiledSpriteHelper.DecodeCompiledSprite(blob, startIndex, 0x180);
//             //File.WriteAllBytes($@"{outputFolder}\{spriteIndex}.bin", output);
//             var image = GenerateClutImage(palette1, output, 384, 240, true);
//             if (IsImageFullyTransparent(image))
//             {
//                 startIndex = j + 2;
//                 continue;
//             }
//             CropImage(image, width, height, 0, 1).Save($@"{spriteOutput}\{spriteOffsetList[i]}_{spriteIndex}.png", ImageFormat.Png);
//             // image = GenerateClutImage(palette2, output, 384, 240, true);
//             // CropImage(image,192,120,0,1).Save($@"{outputFolder}\{spriteIndex}_2.png", ImageFormat.Png);
//             spriteIndex++;
//             startIndex = j + 2;
//         }
//     }
// }

// // var clutOutput = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Whack a Bubble\Analysis\images";
// // Directory.CreateDirectory(clutOutput);

// // var imageIndex = 0;
// // for (int i = 0; i < clutOffsetList.Count; i++)
// // {
// //   var width = BitConverter.ToUInt16(data.Skip((int)clutOffsetList[i]).Take(2).Reverse().ToArray(), 0);
// //   var height = BitConverter.ToUInt16(data.Skip((int)clutOffsetList[i] + 2).Take(2).Reverse().ToArray(), 0);
// //   var start = clutOffsetList[i] + 4;
// //   var end = i == clutOffsetList.Count - 1 ? data.Length : (int)clutOffsetList[i + 1];
// //   var blob = data.Skip((int)start).Take((int)(end - start)).ToArray();

// //   var image = GenerateClutImage(palette1, blob, width, height, true);
// //   image.Save($@"{clutOutput}\{clutOffsetList[i]}_{imageIndex}.png", ImageFormat.Png);
// //   image = GenerateClutImage(palette2, blob, width, height, true);
// //   image.Save($@"{clutOutput}\{clutOffsetList[i]}_{imageIndex}_2.png", ImageFormat.Png);

// //   imageIndex++;

// // }
    }
}
