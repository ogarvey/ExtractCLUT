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
  public static class MerlinsApprenticeHelper
  {
    private static List<Color> GetPalette(byte[] data)
    {
      var palette = new List<Color>();
      var paletteBytes = data.Skip(8).Take(0x100).ToArray();
      palette.AddRange(ColorHelper.ReadPalette(paletteBytes));
      paletteBytes = data.Skip(0x10c).Take(0x100).ToArray();
      palette.AddRange(ColorHelper.ReadPalette(paletteBytes));
      return palette;
    }

    public static void Extract(string filePath, string outputDir)
    {
      var data = File.ReadAllBytes(filePath);
      var palette = GetPalette(data);
      var width = 384;
      var height = 240;
      var images = new List<Bitmap>();
      var dataRLE = FileHelpers.SplitBinaryFileIntoChunks(filePath,new byte[] {0x00, 0x00, 0x00}, true, true, 0x20c);
      foreach (var (chunk, index) in dataRLE.WithIndex())
      {
        if (chunk.Length == 0)
        {
          continue;
        }
        if (chunk[0] != 0x80)
        {
          var image = ImageFormatHelper.GenerateRle7Image(palette, chunk.Skip(2).ToArray(), width, height);
          images.Add(image);
        } else {
          var image = ImageFormatHelper.GenerateRle7Image(palette, chunk, width, height);
          images.Add(image);
        }
      }
      using (var gifWriter = new GifWriter(outputDir + "MerlinsApprentice.gif", 100, -1))
      {
        foreach (var (image,index) in images.WithIndex())
        {
          image.Save(outputDir + "MerlinsApprentice_" + index + ".png");
          gifWriter.WriteFrame(image);
        }
      }
    }
  }
}
