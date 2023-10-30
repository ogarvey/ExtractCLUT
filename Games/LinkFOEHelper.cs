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

    public static int[] animPaletteOffsets = new int[] {
        0xA83AFCC,0xA83d48c,0xa83f49c,0xc9dfb1c,0xc9f73cc,0xd75501c,0xD76c8cc
    };

    public static int[] dataPaletteOffsets = new int[] {
        0x1c, 0x63ebd,0x7ae3c,0xe5b1c,0x15763c,0x1c231c,0x23071c,0x2a223c,0x31063c,
        0x37ea3c,0x3ece3c,0x457b1c,0x4c5f1c,0x53431c,0x5a5e3c,0x610b1c,0x680aac,
        0x6f0a3c,0x75ee3c,0x7c9b1c,0x83b63c,0x8a631c,0x91471c,0x98623c,0x9f2aac
    };

    public static int[] dataPaletteOffsets2 = new int[] {
        0x63ebc, 0x63fc0,0x640c4,0x64c18,0x647ec
,0x648f0,    };

    public static byte[] FindSequenceAndGetPriorBytes(string filePath, byte[] sequence, int bytesToGet)
    {
      byte[] fileBytes = File.ReadAllBytes(filePath);
      byte[] sequenceBytes = sequence;

      // Convert to list to use the IndexOf function.
      List<byte> byteList = new List<byte>(fileBytes);

      for (int i = 0; i < byteList.Count; i++)
      {
        // Use SequenceEqual to check if the next few elements in the list are equal to the sequence
        if (byteList.Skip(i).Take(sequenceBytes.Length).SequenceEqual(sequenceBytes))
        {
          // We've found the sequence, now get the prior bytes.
          int startIndex = Math.Max(0, i - bytesToGet);
          return fileBytes.Skip(startIndex).Take(bytesToGet).ToArray();
        }
      }

      return null; // or throw an exception, or whatever you want to do if the sequence is not found.
    }

    public static void WriteAnimPalettes()
    {
      var bytes = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\lanim.rtr");
      for (int i = 0; i < animPaletteOffsets.Length; i++)
      {
        var palette = ColorHelper.ReadPalette(bytes.Skip(animPaletteOffsets[i]).Take(0x180).ToArray());
        ColorHelper.CreateLabelledPalette(palette).Save($@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\palettes\lanim_rtr_RP_palette_{i}.png", ImageFormat.Png);
      }
    }

    public static void WriteDataPalettes()
    {
      var bytes = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\ldata.rtr");
      for (int i = 0; i <= (dataPaletteOffsets2.Length / 2) + 1; i += 2)
      {
        var palette = ColorHelper.ReadPalette(bytes.Skip(dataPaletteOffsets2[i]).Take(0x100).ToArray());
        palette.AddRange(ColorHelper.ReadPalette(bytes.Skip(dataPaletteOffsets2[i + 1]).Take(0x100).ToArray()));
        ColorHelper.CreateLabelledPalette(palette).Save($@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\palettes\ldata_rtr_RP_CLUT_palette_combined_{i}.png", ImageFormat.Png);
      }
    }

    public static List<List<Color>> GetPossibleCLUTPalettes()
    {
      var combined = new List<List<Color>>();
      var bytes = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\ldata.rtr");
      for (int i = 0; i <= (dataPaletteOffsets2.Length / 2) + 1; i += 2)
      {
        var colors = new List<Color>();
        colors.AddRange(ColorHelper.ReadPalette(bytes.Skip(dataPaletteOffsets2[i]).Take(0x100).ToArray()));
        colors.AddRange(ColorHelper.ReadPalette(bytes.Skip(dataPaletteOffsets2[i + 1]).Take(0x100).ToArray()));
        combined.Add(colors);
        //ColorHelper.CreateLabelledPalette(colors).Save($@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\palettes\combined_{i}.png", ImageFormat.Png);
      }
      return combined;
    }

		public static List<List<Color>> GetPossibleCLUTPalettes2()
		{
      var bytes = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\ldata.rtr");
			var palettes = new List<List<Color>>();
			for (int i = 0; i < dataPaletteOffsets.Length; i++)
			{
				var palette = ColorHelper.ConvertBytesToRGB(bytes.Skip(dataPaletteOffsets[i]).Take(0xFC).ToArray());
				palettes.Add(palette);
			}
			return palettes;
		}

    public static List<Color> GetPossibleRLEPalette()
    {

      var bytes = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\ldata.rtr");
      var palBytes = bytes.Skip(4).Take(0x180).ToArray();
      var colors = ColorHelper.ReadPalette(palBytes);
      if (colors.Count < 128)
      {
        colors.AddRange(colors);
      }
      return colors;
      //ColorHelper.CreateLabelledPalette(palette).Save($@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\palettes\rle_palete.png", ImageFormat.Png);

    }

    public static void ExtractAnimDataVideoScenesForAllPalettes()
    {
      var possiblePalettes = GetPossibleCLUTPalettes();
      possiblePalettes[0][0] = Color.Transparent;
      //ColorHelper.CreateLabelledPalette(possiblePalettes[index]).Save($@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\palettes\extractloop_{index}.png", ImageFormat.Png);

      ExtractAnimDataVideoScenes(possiblePalettes[0]);

    }

    public static void ExtractDataVideoScenes()
    {
      var possiblePalettes = GetPossibleCLUTPalettes2();
      var datafile1 = @"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\records\ldata\video\ldata_v_1_4_CLUT7_Normal_3.bin";

      var imageBytes = File.ReadAllBytes(datafile1).Skip(0x1D9328).Take(0x16800).ToArray();

     foreach (var (palette, index) in possiblePalettes.WithIndex())
			{
        var image = ImageFormatHelper.GenerateClutImage(palette, imageBytes, 1152, 80);
        image.Save($@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\records\ldata\video\output\Normal_3_Map6_a2_palette_{index}_.png", ImageFormat.Png);
			}

    }

    public static void ExtractAnimDataVideoScenes(List<Color> palette)
    {

      var chunkSize = 0x3800;

      for (int i = 1; i < 63; i++)
      {
        var rleFileBytes = File.ReadAllBytes(@$"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\records\lanim\data\lanim_d_{i}.bin");

        var chunks = new List<byte[]>();
        var trimmedChunks = new List<byte[]>();
        for (int j = 0; j < rleFileBytes.Length - 0x37ff; j += chunkSize)
        {
          byte[] chunk = new byte[chunkSize];
          Array.Copy(rleFileBytes, j, chunk, 0, chunkSize);

          chunks.Add(chunk);
        }

        foreach (var (chunk, index) in chunks.WithIndex())
        {
          int j = chunk.Length - 1;
          while (chunk[j] == 0)
            --j;
          // now foo[i] is the last non-zero byte
          byte[] bar = new byte[j + 1];
          Array.Copy(chunk, bar, j + 1);
          trimmedChunks.Add(bar);
        }
        var images = new List<Bitmap>();
        foreach (var (chunk, index) in trimmedChunks.WithIndex())
        {
          images.Add(BitmapHelper.Scale4(GenerateRle7Image(palette, chunk, 384, 240)));
        }

        using (var gifWriter = new GifWriter($@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\records\lanim\data\output\lanim_d_{i}_scaled.gif", 100, 0))
        {
          foreach (var image in images)
          {
            gifWriter.WriteFrame(image);
          }
        }

      }

    }
  }
}

/* var dataFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\records\ldata\data\", "*.bin");
var imageFile = @"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\records\ldata\video\ldata_v_1_1_CLUT7_Normal_2.bin";
var imageChunks = FileHelpers.SplitBinaryFileIntoChunks(imageFile, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, true, true, 0);
foreach (var (imageChunk, index) in imageChunks.WithIndex())
{
  File.WriteAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\records\ldata\video\output\chunks_02\" + $"Normal_2_{index}.bin", imageChunk);
// } */
// var chunkFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\records\ldata\video\output\chunks_02\", "*.bin").OrderBy(f => Convert.ToInt32(f.Split('_').Last().Split('.').First())).ToArray();
// //var paletteList = new List<List<Color>>();

// var file = @"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\records\ldata\data\ldata_d_1.bin";
// var paletteBytes = LinkFOEHelper.FindSequenceAndGetPriorBytes(file, new byte[] { 0x49, 0x44, 0x41, 0x54 }, 384);
// var palette = ColorHelper.ConvertBytesToRGB(paletteBytes);

// var labelWidths = new int[]{
//   136,
//   84,
//   96,
//   156,
//   120,
//   112,
//   124,
//   100,
//   104,
//   100,
//   148,
//   132,
//   92,
//   132,
//   124
// };

// foreach (var (chunkFile, index) in chunkFiles.WithIndex())
// {
//   var chunkBytes = File.ReadAllBytes(chunkFile);
//   var fileName = Path.GetFileNameWithoutExtension(chunkFile);
//   var saveTo = $@"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil\records\ldata\video\output\chunks_02\output";
//   Directory.CreateDirectory(saveTo);
//   var width = labelWidths[index];

//   var image = ImageFormatHelper.GenerateClutImage(palette, chunkBytes, width, chunkBytes.Length / width);
//   image.Save($@"{saveTo}\{fileName}.png");

// }
