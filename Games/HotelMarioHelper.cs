using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using ExtractCLUT.Writers;
using Color = System.Drawing.Color;

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
    static string _baseDir = @"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\records";
    static string _introDir = $@"{_baseDir}\intro\video";
    static string _introOutputDir = $@"{_baseDir}\intro\video\output";
    static int[] _introRL7_2_Offsets = new int[] { 0x0, 0x5ac8, 0x6cf0, 0xbea4, 0xf51c, 0x158f8 };
    static int[] _introRL7_2_OffsetLengths = new int[] { 0x51df, 0x9c1, 0x495c, 0x2ec2, 0x51ca, 0x2888 };
    static int[] _introPaletteOffsets = new int[] { 0x2df18, 0x34428, 0x35fb8, 0x3bb98, 0x3fbe8, 0x47358 };
    static int[] _introRL7_3_Offsets = new int[] {
      0x0, 0x914, 0x1228, 0x2450, 0x3678, 0x48a0, 0x5ac8, 0x6cf0,0x7f18, 0x9140,0xa368,0xb590,0xc7b8,0xd9e0,
      0xec08,0xfe30,0x11058,0x1196c,0x12280,0x12b94,0x134a8,0x13dbc,0x146d0,0x14fe4,0x1620c
      };
    static int[] _introRL7_3_OffsetLengths = new int[] {
      0x63a, 0x8f5, 0xb66, 0xc17, 0xce8, 0xc8e, 0xd41, 0xd08 ,0xd43,0xcb6,0xc27,0xa9f,0xb52,0x9f3,
      0x9c6,0x980,0x89c,0x766,0x75d,0x6c0,0x6db,0x6b3,0x5dc,0xe8e,0x11e8
      };
    static int[] _introPalette2Offsets = new int[] { 0x5a288 };
    public static void ExtractIntroFiles()
    {
      var introPalette = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\intro.rtf");

      var file = Path.Combine(_introDir, "intro_v_1_15_RL7_Normal_2.bin");
      var image = File.ReadAllBytes(file);
      for (int i = 0; i < _introRL7_2_Offsets.Length; i++)
      {
        var palette1 = ColorHelper.ConvertBytesToRGB(introPalette.Skip(_introPaletteOffsets[i]).Take(0x180).ToArray()).ToList();
        var offset = _introRL7_2_Offsets[i];
        var length = _introRL7_2_OffsetLengths[i];
        var imageBytes = image.Skip(offset).Take(length).ToArray();
        var img = ImageFormatHelper.GenerateRle7Image(palette1, imageBytes, 384, 280);
        img.Save(Path.Combine(_introOutputDir, $"intro_RL7_2_{i}.png"), ImageFormat.Png);
      }

      file = Path.Combine(_introDir, "intro_v_1_15_RL7_Normal_3.bin");
      var palette = ColorHelper.ConvertBytesToRGB(introPalette.Skip(_introPalette2Offsets[0]).Take(0x180).ToArray()).ToList();
      var anim = new List<Bitmap>();
      palette[0] = Color.Transparent;
      var chunks = FileHelpers.SplitBinaryFileIntoChunks(file, new byte[] { 0x00, 0x00, 0x00 }, true, true, null);
      foreach (var (chunk, index) in chunks.WithIndex())
      {
        var lChunk = chunk.ToList();
        while (lChunk.Count > 0 && lChunk.First() == 0x00)
        {
          lChunk.RemoveAt(0);
        }
        if (lChunk.Count == 0)
        {
          continue;
        }
        var img = ImageFormatHelper.GenerateRle7Image(palette, lChunk.ToArray(), 384, 280);
        //img.Save(Path.Combine(_introOutputDir, $"intro_RL7_3_{index}.png"), ImageFormat.Png);
        anim.Add(img);
      }
      using (var gifWriter = new GifWriter(Path.Combine(_introOutputDir, $"intro_RL7_3.gif"), 100))
      {
        foreach (var img in anim)
        {
          gifWriter.WriteFrame(img);
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
