using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using static ExtractCLUT.Utils;
using static ExtractCLUT.Helpers.ColorHelper;
using Color = System.Drawing.Color;

public static class LotRSHelpers
{
  public static byte[] GetRLEBytes(string path, int offset, int size)
  {
    byte[] fileBytes;
    byte[] imageBytes;

    // Read file bytes using FileStream and close the file when done
    using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
    {
      fileBytes = new byte[fileStream.Length];
      imageBytes = new byte[size];
      fileStream.Read(fileBytes, 0, fileBytes.Length);
      imageBytes = fileBytes.Skip(offset).Take(size).ToArray();
    }
    
    return imageBytes;
  }

  public static void ProcessFilesInRange(string directoryPath, int start, int end)
  {
    string[] filePaths = Directory.GetFiles(directoryPath, "*.bin");
    List<Color> colors = new List<Color>();

    for (int i = start; i <= end; i++)
    {
      string fileName = i < 10 ? Path.Combine(directoryPath, $"0{i}.bin") : Path.Combine(directoryPath, $"{i}.bin");

      if (!File.Exists(fileName))
      {
        Console.WriteLine($"File with number {i} not found");
        continue;
      }

      if (i % 2 == 1)
      {
        byte[] paletteBytes = GetPaletteBytes(fileName, 0x5a);
        colors = ReadPalette(paletteBytes);
        WritePalette(Path.Combine(directoryPath, $"palette{i}.png"), colors);
      }
      else
      {
        byte[] imageBytes;
        using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
        {
          imageBytes = new byte[fileStream.Length];
          fileStream.Read(imageBytes, 0, imageBytes.Length);
          var image = CreateImage(imageBytes, colors,384,240);
          image.Save(Path.Combine(directoryPath, $"{i}.png"));
        }
      }
    }
  }
}
