using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using ExtractCLUT.Writers;
using OGLibCDi.Helpers;
using OGLibCDi.Models;
using Color = System.Drawing.Color;
using ColorHelper = OGLibCDi.Helpers.ColorHelper;
using ImageFormatHelper = ExtractCLUT.Helpers.ImageFormatHelper;

namespace ExtractCLUT.Games
{
  public class HMDatFile
  {
    public string OriginalFile { get; set; }
    public List<string> SpriteNames { get; set; }
    public List<int> SpriteNameOffsets { get; set; }
    public List<int> SpriteDataOffsets { get; set; }
    public string Text { get; set; }
  }
  public static class HotelMarioHelper
  {
    public static void ExtractAllImageData(string rtfPath)
    {

      var fileList = Directory.GetFiles(rtfPath, "*am.rtf").ToList();
      fileList.AddRange(Directory.GetFiles(rtfPath, "*av.rtf").ToList());
      fileList.Add(Path.Combine(rtfPath,"intro.rtf"));
      fileList.Add(Path.Combine(rtfPath, "exit.rtf"));

      foreach (var file in fileList)
      {
        if (!File.Exists(file)) continue;
        var cdiFile = new CdiFile(file);
        var outputDir = Path.Combine(Path.GetDirectoryName(file), "output");
        var fileOutputDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(file));
        Directory.CreateDirectory(fileOutputDir);

        // extract palettes
        var paletteOutputDir = Path.Combine(fileOutputDir, "palettes");
        Directory.CreateDirectory(paletteOutputDir);
        var paletteSectors = cdiFile.DataSectors.OrderBy(ds => ds.SectorIndex).ToList();

        var palettes = new List<Tuple<int, List<Color>>>();
        var offsets = new List<int>();

        foreach (var (sector, index) in paletteSectors.WithIndex())
        {
          var bytes = sector.GetSectorData().Take(0x180).ToArray();
          var palette = ColorHelper.ConvertBytesToRGB(bytes);
          palettes.Add(new Tuple<int, List<Color>>(sector.SectorIndex, palette));
          offsets.Add(sector.SectorIndex);
        }

        palettes = palettes.OrderByDescending(p => p.Item1).ToList();

        // extract DYUV data
        var dyuvOutputDir = Path.Combine(fileOutputDir, "dyuv");
        Directory.CreateDirectory(dyuvOutputDir);
        var dyuvSectors = cdiFile.VideoSectors.Where(ds => ds.Coding.VideoString == "DYUV").OrderBy(ds => ds.SectorIndex).ToList();

        var dyuvData = new List<byte[]>();

        foreach (var (sector, index) in dyuvSectors.WithIndex())
        {
          dyuvData.Add(sector.GetSectorData());
          if (sector.SubMode.IsEOR)
          {
            var dyuvBytes = dyuvData.SelectMany(x => x).ToArray();
            var image = ImageFormatHelper.DecodeDYUVImage(dyuvBytes, 384, 280);
            var outputName = Path.Combine(dyuvOutputDir, $"dyuv_{sector.SectorIndex}.png");
            image.Save(outputName, ImageFormat.Png);
            dyuvData.Clear();
          }
        }

        // extract rl7 data
        var rl7OutputDir = Path.Combine(fileOutputDir, "rl7");
        Directory.CreateDirectory(rl7OutputDir);
        var rl7Sectors = cdiFile.VideoSectors.Where(ds => ds.Coding.VideoString == "RL7").OrderBy(ds => ds.SectorIndex).ToList();

        var rl7Data = new List<byte[]>();

        var nextPaletteOffset = palettes[^2].Item1;

        foreach (var (sector, index) in rl7Sectors.WithIndex())
        {
          if (sector.SectorIndex > nextPaletteOffset || index == rl7Sectors.Count - 1)
          {
            var palette = palettes.FirstOrDefault(x => x.Item1 < rl7Sectors[index - 1].SectorIndex);
            var colors = palette.Item2;
            var rl7Bytes = rl7Data.SelectMany(x => x).ToArray();
            var imageBytes = ImageFormatHelper.Rle7(rl7Bytes, 384);
            var imageIndex = 0;
            while (imageBytes.Length >= 384 * 280)
            {
              var bytes = imageBytes.Take(384 * 280).ToArray();
              var image = ImageFormatHelper.GenerateClutImage(colors, bytes, 384, 280, true);
              var outputName = Path.Combine(rl7OutputDir, $"rl7_{sector.SectorIndex}_{imageIndex}.png");
              imageIndex++;
              image.Save(outputName, ImageFormat.Png);
              imageBytes = imageBytes.Skip(384 * 280).ToArray();
            }
            rl7Data.Clear();
            nextPaletteOffset = palettes.OrderBy(x => x.Item1).FirstOrDefault(x => x.Item1 > sector.SectorIndex)?.Item1 ?? palettes[^1].Item1;
          }
          var sectorBytes = sector.GetSectorData();
          var trimmedBytes = FileHelpers.RemoveTrailingZeroes(sectorBytes);
          rl7Data.Add(trimmedBytes);
        }

        // extract clut7 data
        var clut7OutputDir = Path.Combine(fileOutputDir, "clut7");
        Directory.CreateDirectory(clut7OutputDir);
        var clut7Sectors = cdiFile.VideoSectors.Where(ds => ds.Coding.VideoString == "CLUT7").OrderBy(ds => ds.SectorIndex).ToList();

        var clut7Data = new List<byte[]>();

        nextPaletteOffset = palettes[^2].Item1;

        foreach (var (sector, index) in clut7Sectors.WithIndex())
        {
          clut7Data.Add(sector.GetSectorData());
          if (sector.SubMode.IsEOR)
          {
            var palette = palettes.FirstOrDefault(x => x.Item1 < sector.SectorIndex - 1).Item2;
            var clut7Bytes = clut7Data.SelectMany(x => x).ToArray();
            var imageIndex = 0;

            var image = ImageFormatHelper.GenerateClutImage(palette, clut7Bytes, 384, 280, false);
            var outputName = Path.Combine(clut7OutputDir, $"clut7_{sector.SectorIndex}.png");
            image.Save(outputName, ImageFormat.Png);

            clut7Data.Clear();
            nextPaletteOffset = palettes.OrderBy(x => x.Item1).FirstOrDefault(x => x.Item1 > sector.SectorIndex)?.Item1 ?? palettes[^1].Item1;
          }
        }

      }

    }

    public static HMDatFile ParseDatFile(string filePath)
    {
      var hmData = new HMDatFile
      {
        OriginalFile = Path.GetFileName(filePath),
        SpriteNames = new List<string>(),
        SpriteNameOffsets = new List<int>(),
        SpriteDataOffsets = new List<int>()
      };
      bool addToSpriteNameList = true;

      using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
      using (BinaryReader reader = new BinaryReader(fs))
      {
        // Read integer at offset 0x0C as big endian
        reader.BaseStream.Seek(0x0C, SeekOrigin.Begin);
        int startOffset = Utils.ReadBigEndianUInt32(reader);

        // Read integer at offset 0x05
        reader.BaseStream.Seek(0x04, SeekOrigin.Begin);
        int endOffset = Utils.ReadBigEndianUInt32(reader); 
        
        reader.BaseStream.Seek(0x3C, SeekOrigin.Begin);

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
          int offset = Utils.ReadBigEndianUInt32(reader);

          // Check for the sequence of 0x00 0x00 0x00 0x00
          if (offset == 0)
          {
            break;
          }

          if (addToSpriteNameList)
          {
            hmData.SpriteNameOffsets.Add(offset);
          }
          else
          {
            hmData.SpriteDataOffsets.Add(offset);
          }

          // Alternate between lists
          addToSpriteNameList = !addToSpriteNameList;
        }

        foreach (var offset in hmData.SpriteNameOffsets)
        {
          reader.BaseStream.Seek(offset, SeekOrigin.Begin);
          hmData.SpriteNames.Add(reader.ReadNullTerminatedString());
        }

        // Check if offsets are valid
        if (startOffset < endOffset && startOffset >= 0 && endOffset <= fs.Length)
        {
          // Read bytes from startOffset to endOffset
          reader.BaseStream.Seek(startOffset, SeekOrigin.Begin);
          byte[] textBytes = reader.ReadBytes(endOffset - startOffset);

          // Convert bytes to string and add to list
          hmData.Text = Encoding.ASCII.GetString(textBytes); // Assuming ASCII encoding
          return hmData;
        }
        else
        {
          throw new Exception("Invalid start or end offset in the file.");
        }
      }

    }
  }
}

// var palette = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\Output\L0_av.rtf_1_15_111.bin");
// var imageData = File.ReadAllBytes(@"c:\Dev\Projects\Gaming\CD-i\Hotel Mario\Output\L0_av.rtf_1_15_CLUT7_Normal_Even_112.bin");
// var colors = ConvertBytesToRGB(palette);

// var colors2 = colors.Select(c => c).ToList();

// var initialBgImage = GenerateClutImage(colors, imageData, 384, 280);
// var bgImages = new List<Bitmap>();
// var bgImages2 = new List<Bitmap>();

// bgImages.Add(initialBgImage);
// bgImages2.Add(initialBgImage);

// for (int i = 0; i < 12; i++)
// {
//   RotateSubset(colors2, 92, 104, 1);
//   var image = GenerateClutImage(colors2, imageData, 384, 280);
//   image.Save($@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\Output\gifs\bg{i}.png");
//   bgImages.Add(image);
//   ReverseRotateSubset(colors, 93, 104, 1);
//   image = GenerateClutImage(colors, imageData, 384, 280);
//   image.Save($@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\Output\gifs\bg2{i}.png");
//   bgImages2.Add(image);
// }

// var outputPath = @"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\Output\gifs\";
// Directory.CreateDirectory(outputPath);

// using (var gifWriter = new GifWriter(Path.Combine(outputPath, "bg.gif"), 100))
// {
//   foreach (var image in bgImages)
//   {
//     gifWriter.WriteFrame(image);
//   }
// }

// using (var gifWriter = new GifWriter(Path.Combine(outputPath, "bg2.gif"), 100))
// {
//   foreach (var image in bgImages2)
//   {
//     gifWriter.WriteFrame(image);
//   }
// }
// var palette = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\Output\L3\palettes\L3_av.rtf_1_15_116.bin").Take(384).ToArray();

// var imageData = File.ReadAllBytes(@"c:\Dev\Projects\Gaming\CD-i\Hotel Mario\Output\L3\CLUT7\Stages\L3_av.rtf_1_15_CLUT7_Normal_Even_117.bin");

// var outputPath = @"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\Output\gifs\";
// Directory.CreateDirectory(outputPath);

// var colors = ConvertBytesToRGB(palette);

// var colors2 = colors.Select(c => c).ToList();

// var initialBgImage = GenerateClutBytes(colors, imageData, 384, 280);
// var bgImages = new List<byte[]>();
// var bgImages2 = new List<byte[]>();

// var initialPalImage = CreateLabelledPalette(colors);
// var palImages  = new List<Bitmap>();
// var palImages2 = new List<Bitmap>();

// palImages.Add(initialPalImage);
// palImages2.Add(initialPalImage);

// bgImages.Add(initialBgImage);
// bgImages2.Add(initialBgImage);
// for (int i = 0; i <= 14; i++)
// {
//   RotateSubset(colors2, 82, 97, 1);
//   palImages.Add(CreateLabelledPalette(colors2));
//   var image = GenerateClutBytes(colors2, imageData, 384, 280);
//   bgImages.Add(image);

//   ReverseRotateSubset(colors, 82, 97, 1);
//   palImages2.Add(CreateLabelledPalette(colors));
//   var image2 = GenerateClutBytes(colors, imageData, 384, 280);
//   bgImages2.Add(image2);
// }


// using (var gifWriter = new GifWriter(Path.Combine(outputPath, "Level3_palette.gif"), 500, 0))
// {
//   foreach (var img in palImages)
//   {
//     gifWriter.WriteFrame(img);
//   }
// }

// // using (var gifWriter = new GifWriter(Path.Combine(outputPath, "Level3_s1_x4_500.gif"), 500, 0))
// // {
// //   foreach (var img in bgImages)
// //   {
// //     gifWriter.WriteFrame(img);
// //   }
// // }

// using (var gifWriter = new GifWriter(Path.Combine(outputPath, "Level3_palette_alt.gif"), 500,0))
// {
//   foreach (var img in palImages2)
//   {
//     gifWriter.WriteFrame(img);
//   }
// }

// using (var gifWriter = new GifWriter(Path.Combine(outputPath, "Level3_s1_alt_x4_500.gif"), 500,0))
// {
//   foreach (var img in bgImages2)
//   {
//     gifWriter.WriteFrame(img);
//   }
// }

// var binData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\Output\L1\L1_av.rtf_1_15_CLUT4_Normal_Even_236.bin");

// var byteList = new List<byte[]>();

// for (int i = 0; i < binData.Length - 0x3f; i += 0x40)
// {
//   var newData = new byte[0x80];
//   var chunk = binData.Skip(i).Take(0x40).ToArray();
//   // for the first 8 bytes,
//   for (int j = 0, k = 0; j < 8; j += 2, k++)
//   {
//     newData[k * 4] = chunk[1];
//     newData[k * 4 + 1] = chunk[3];
//     newData[k * 4 + 2] = chunk[5];
//     newData[k * 4 + 3] = chunk[7];
//   }
//   // copy the remaining 56 bytes to the new array, twice to take up the remaining 112 bytes
//   Array.Copy(chunk, 8, newData, 16, 56);
//   Array.Copy(chunk, 8, newData, 72, 56);
//   byteList.Add(newData);
// }

// var output = @"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\Output\L1\L1_av.rtf_1_15_CLUT4_Normal_Even_236_3.bin";
// File.WriteAllBytes(output, byteList.SelectMany(x => x).ToArray());
