using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using Color = System.Drawing.Color;

namespace ExtractCLUT.Games
{
  public static class PinballHelper
  {
		public static string _rtfPath = @"C:\Dev\Projects\Gaming\CD-i\CDI Pinball Volume 1";
    public static string _basePath = @"C:\Dev\Projects\Gaming\CD-i\CDI Pinball Volume 1\records\pin\video";
    public static string _outputPath = @"C:\Dev\Projects\Gaming\CD-i\CDI Pinball Volume 1\output";
    public static int _palette1Offset = 0x1DE77D8;
    public static int _palette2Offset = 0x2389ED8;
    public static int _palette3Offset = 0x26E05D8;
    public static int _palette4Offset = 0x2AC09D8;
    public static int[] _paletteOffsets = new int[] { _palette1Offset, _palette2Offset, _palette3Offset, _palette4Offset };

    public static int[] _flipperOffsets = new int[] { 0xC3BF4 };

    public static string _tables = Path.Combine(_basePath, "Tables.bin");
		public static string _rtf = Path.Combine(_rtfPath, "pin.rtf");
    public static byte[] GetTableBytes(int offset, int height)
    {
      var bytes = new byte[height * 0x16800];
      using (var fs = new FileStream(_tables, FileMode.Open, FileAccess.Read))
      {
        fs.Seek(offset, SeekOrigin.Begin);
        fs.Read(bytes, 0, bytes.Length);
      }

      return bytes;
    }
    public static List<Bitmap> GetFlipperImages()
    {
      var bytes = new byte[0x44b0];
      var palette = new List<Color>();
      var paletteBytes = new byte[0x180];
      var images = new List<Bitmap>();
      for (var i = 0; i < 1; i++)
      {
        using (var fs = new FileStream(_tables, FileMode.Open, FileAccess.Read))
        {
          fs.Seek(_flipperOffsets[i], SeekOrigin.Begin);
          fs.Read(bytes, 0, bytes.Length);
        }
        using (var fs = new FileStream(_rtf, FileMode.Open, FileAccess.Read))
        {
          fs.Seek(_paletteOffsets[i+1], SeekOrigin.Begin);
          fs.Read(paletteBytes, 0, paletteBytes.Length);
				}

        palette = ColorHelper.ConvertBytesToRGB(paletteBytes);
        var image = ImageFormatHelper.GenerateClutImage(palette.ToList(), bytes, 112, 158);
        images.Add(image);
      }
      return images;
    }
    public static Bitmap GetTableBitmap(int offset, int height, bool useGrayscale, int tableId)
    {
      var rtfBytes = File.ReadAllBytes(_rtf);
      var palette = new List<Color>();
      var paletteBytes = new byte[0x180];
      var actualHeight = height * 240;
      var bytes = GetTableBytes(offset, height);

      paletteBytes = rtfBytes.Skip(_paletteOffsets[tableId - 1]).Take(0x180).ToArray();
      palette = ColorHelper.ConvertBytesToRGB(paletteBytes);

      return ImageFormatHelper.GenerateClutImage(palette.ToList(), bytes, 384, actualHeight);

    }
  }
}
