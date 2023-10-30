using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using ExtractCLUT.Writers;

namespace ExtractCLUT.Games
{
  public static class PecosBillHelper
  {
    private static string _storyFolder = @"C:\Dev\Projects\Gaming\CD-i\Pecos Bill\records\story\video";
    private static string[] _storyFiles = new string[] {
        Path.Combine(_storyFolder, "story_v_1_16_DYUV_Normal_1.bin"),
        Path.Combine(_storyFolder, "story_v_1_17_DYUV_Normal_3.bin"),
        Path.Combine(_storyFolder, "story_v_1_19_DYUV_Normal_2.bin"),
        Path.Combine(_storyFolder, "story_v_1_20_DYUV_Normal_4.bin") };
    public static void ExtractStory()
    {
      foreach (var file in _storyFiles)
      {
        var bytes = File.ReadAllBytes(file);
        var images = new List<Bitmap>();
        var scaledImages = new List<Bitmap>();
        var bytesToSkip = 0x1aaac;
        var bytesToRead = 0x1a400;
        for (int i = 0; i < (bytes.Length - bytesToSkip - 1); i += bytesToSkip)
        {
          switch (file)
          {
            case var f when f.Contains("story_v_1_16_DYUV_Normal_1.bin"):
              bytesToSkip = 0x16b20;
              bytesToRead = 0x16800;
              break;
            case var f when f.Contains("story_v_1_17_DYUV_Normal_3.bin"):
              bytesToSkip = 0x882c;
              bytesToRead = 0x8700;
              break;
            case var f when f.Contains("story_v_1_19_DYUV_Normal_2.bin"):
              bytesToSkip = 0x1aaac;
              bytesToRead = 0x1a400;
              break;
            case var f when f.Contains("story_v_1_20_DYUV_Normal_4.bin"):
              bytesToSkip = 0xa368;
              bytesToRead = 0x9c00;
              break;
            default:
              bytesToSkip = 0x1a400;
              break;
          }
          var imageBytes = bytes.Skip(i).Take(bytesToRead).ToArray();
          var image = ImageFormatHelper.DecodeDYUVImage(imageBytes, 384, bytesToRead / 384);
          images.Add(image);
          //scaledImages.Add(BitmapHelper.Scale4(image));
        }
        using (var gifWriter = new GifWriter(Path.Combine(_storyFolder, $"output\\{Path.GetFileNameWithoutExtension(file)}_2000.gif"), 3640, 0))
        {
          foreach (var image in images)
          {
            gifWriter.WriteFrame(image);
          }
        }
        /* using (var gifWriter = new GifWriter(Path.Combine(_storyFolder, $"output\\{Path.GetFileNameWithoutExtension(file)}_500_scaled.gif"), 500, 0))
        {
          foreach (var image in scaledImages)
          {
            gifWriter.WriteFrame(image);
          }
        } */
      }
    }
  }
}
