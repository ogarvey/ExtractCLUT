using System.Drawing.Imaging;
using static ExtractCLUT.Utils;
using ExtractCLUT.Helpers;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using System.Drawing;

public static class AsterixHelpers
{
  public static void ProcessChunks(string path, string outputId)
  {
    // read all bin files in the directory
    List<string> filePaths = Directory.GetFiles(path, "*.bin").OrderBy(f => Convert.ToInt32(f.Split('_').Last().Split('.').First())).ToList();
    List<Bitmap> videoFrameList = new List<Bitmap>();
    // for each file in filePaths
    foreach (var binfile in filePaths)
    {
      var testFile = File.ReadAllBytes(binfile);
      if (testFile.Length == 0) continue;
      var colors = ColorHelper.ConvertBytesToRGB(testFile.Skip(04).Take(0x180).ToArray());
      var imageBytes = testFile.Skip(0x184).ToArray();
      var rleImage = Rle7(imageBytes, 384);
      videoFrameList.Add(CreateImage(rleImage, colors, 384, 240));
    }
    if (videoFrameList.Count > 0) ConvertBitmapsToGif(videoFrameList, @$"C:\Dev\Projects\Gaming\CD-i\asterix\records\anims\video\output\{outputId}.gif");
  }

}
