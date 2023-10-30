using System.Drawing.Imaging;
using ExtractCLUT.Helpers;
using static ExtractCLUT.Helpers.ColorHelper;
using Color = System.Drawing.Color;

namespace ExtractCLUT.Games
{
  public static class StrikerHelper
  {
    private static string _base = @"C:\Dev\Projects\Gaming\CD-i\Striker";
    private static string _titleScreen = Path.Combine(_base, @"SCREENS\records\title\data\title_0_124656_d_EOF.bin");
    private static string _titleNTSCScreen = Path.Combine(_base, @"SCREENS\records\titntsc\data\titntsc_0_124656_d_EOF.bin");
    private static string _statScreen = Path.Combine(_base, @"SCREENS\records\stats\data\stats_0_124656_d_EOF.bin");
    private static string _output = Path.Combine(_base, @"Output");
    public static List<Color> GetStrikerDefaultPalette()
    {
      var paletteFile = @"C:\Dev\Projects\Gaming\CD-i\Striker\PALETTES\records\clut7pal\data\clut7pal_0_2352_d_EOF.bin";
      var paletteBytes = File.ReadAllBytes(paletteFile).Take(0x180).ToArray();
      return ConvertBytesToRGB(paletteBytes);
    }

    public static void ExtractStrikerScreens()
    {
      if (!Directory.Exists(_output))
      {
        Directory.CreateDirectory(_output);
      }
      var defaultPalette = GetStrikerDefaultPalette();
      var title = ImageFormatHelper.ExtractPaletteAndImageBytes(_titleScreen);
      var titleNTSC = ImageFormatHelper.ExtractPaletteAndImageBytes(_titleNTSCScreen);
      var stats = ImageFormatHelper.ExtractPaletteAndImageBytes(_statScreen);
      var titlePalette = ConvertBytesToRGB(title.Item1);
      var titleNTSCPalette = ConvertBytesToRGB(titleNTSC.Item1);
      var statsPalette = ConvertBytesToRGB(stats.Item1);
      CreateLabelledPalette(titlePalette).Save(Path.Combine(_output, "titlePalette3.png"));
      CreateLabelledPalette(titleNTSCPalette).Save(Path.Combine(_output, "titleNTSCPalette3.png"));
      CreateLabelledPalette(statsPalette).Save(Path.Combine(_output, "statsPalette3.png"));
      var titleImage = ImageFormatHelper.GenerateClutImage(titlePalette, title.Item2, 384, 280);
      var titleNTSCImage = ImageFormatHelper.GenerateClutImage(titleNTSCPalette, titleNTSC.Item2, 384, 280);
      var statsImage = ImageFormatHelper.GenerateClutImage(statsPalette, stats.Item2, 384, 280);
      titleImage.Save(Path.Combine(_output, "title3.png"), ImageFormat.Png);
      titleNTSCImage.Save(Path.Combine(_output, "titleNTSC3.png"), ImageFormat.Png);
      statsImage.Save(Path.Combine(_output, "stats3.png"), ImageFormat.Png);

    }
  }
}
