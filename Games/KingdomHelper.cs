using ExtractCLUT.Model;
using System.IO;
using System.Text;

public static class KingdomHelper {
  public static List<byte[]> GetPotentialCluts(byte[] bytes) {
    List<byte[]> cluts = new List<byte[]>();
    for (int i = 0; i < bytes.Length; i += 0x800) {
      byte[] palette = new byte[0x180];
      int chunkSize = Math.Min(0x180, bytes.Length - i);
      Array.Copy(bytes, i+4, palette, 0, chunkSize);
      cluts.Add(palette);
    }
    return cluts;
  }

  public static List<KingdomFileData> AnalyzeFiles(string directoryPath, string outputPath)
  {
    var kingdomFileData = new List<KingdomFileData>();
    // Create a new CSV file and write the header
    using StreamWriter csvWriter = new StreamWriter(outputPath, false, Encoding.UTF8);
    csvWriter.WriteLine("Filename,Filenumber,BytesToRead,Filesize,Width,Height,PotentialSubFileCount");

    // Get all files in the directory
    string[] files = Directory.GetFiles(directoryPath);

    foreach (string filePath in files)
    {
      // Open the file and read the first 32 bytes
      using FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
      using BinaryReader reader = new BinaryReader(fileStream);

      byte[] buffer = reader.ReadBytes(32);

      // Interpret the data
      ushort filenumber = BitConverter.ToUInt16(BitConverter.IsLittleEndian ? buffer.Skip(4).Take(2).Reverse().ToArray() : buffer.Skip(4).Take(2).ToArray(), 0);
      uint bytesToRead = BitConverter.ToUInt32(BitConverter.IsLittleEndian ? buffer.Skip(16).Take(4).Reverse().ToArray() : buffer.Skip(16).Take(4).ToArray(), 0);
      ushort width = BitConverter.ToUInt16(BitConverter.IsLittleEndian ? buffer.Skip(24).Take(2).Reverse().ToArray() : buffer.Skip(24).Take(2).ToArray(), 0);
      ushort height = BitConverter.ToUInt16(BitConverter.IsLittleEndian ? buffer.Skip(26).Take(2).Reverse().ToArray() : buffer.Skip(26).Take(2).ToArray(), 0);

      // Write the data to the CSV file
      csvWriter.WriteLine($"{Path.GetFileName(filePath)},{filenumber},{bytesToRead},{reader.BaseStream.Length},{width},{height},{reader.BaseStream.Length / bytesToRead}");
      kingdomFileData.Add(new KingdomFileData
      {
        BytesToRead = (int)bytesToRead,
        Filenumber = filenumber,
        Filename = Path.GetFileName(filePath),
        Filesize = (int)reader.BaseStream.Length,
        Height = height,
        PotentialSubFileCount = (int)(reader.BaseStream.Length / bytesToRead),
        Width = width
      });
    }
    return kingdomFileData;
  }

}

/* var potentialPalette = @"C:\Dev\Projects\Gaming\CD-i\kingdom\records\gats\data\gats_17082576_17106096_d_6.bin";
var potentialPaletteBytes = File.ReadAllBytes(potentialPalette);
var cluts = KingdomHelper.GetPotentialCluts(potentialPaletteBytes);

var clutList = new List<List<Color>>();
var clutList2 = new List<List<Color>>();

foreach (var clut in cluts)
{
  clutList.Add(ColorHelper.ConvertBytesToRGB(clut));
  clutList2.Add(ReadPalette(clut));
}

var rl7File = @"C:\Dev\Projects\Gaming\CD-i\kingdom\records\gats\video\gats_v_1_16_RL7_Normal_4.bin";
var clut7File = @"C:\Dev\Projects\Gaming\CD-i\kingdom\records\gats\video\gats_v_1_16_CLUT7_Normal_3.bin";
var outputPath = @"C:\Dev\Projects\Gaming\CD-i\kingdom\records\gats\video\output\";

var rl7Bytes = File.ReadAllBytes(rl7File).Skip(0x20).Take(0x7d4).ToArray();
var clut7Bytes = File.ReadAllBytes(clut7File).Skip(0x20).Take(0x8a00).ToArray();

var colors = ColorHelper.ConvertBytesToRGB(rl7Bytes.Skip(04).Take(0x180).ToArray());


foreach (var (clut, index) in clutList.WithIndex())
{
  var rleImage = RLE(rl7Bytes, 384, 280);
  var relBitmap = CreateImage(rleImage, clut);
  relBitmap.Save(Path.Combine(outputPath + "\\RL7", $"rleImage_palette_{index}.png"));
  var clutImage = new Bitmap(384, 92);
  for (int y = 0; y < 92; y++)
  {
    for (int x = 0; x < 384; x++)
    {
      var i = y * 384 + x;
      var paletteIndex = clut7Bytes[i];
      var color = clut[paletteIndex];
      clutImage.SetPixel(x, y, color);
    }
  }
  clutImage.Save(Path.Combine(outputPath + "\\CLUT7", $"clutImage_palette_{index}.png"));
}

foreach (var (clut, index) in clutList2.WithIndex())
{
  var rleImage = RLE(rl7Bytes, 384, 280);
  var relBitmap = CreateImage(rleImage, clut);
  relBitmap.Save(Path.Combine(outputPath + "\\RL7", $"rleImage_palette2_{index}.png"));
  var clutImage = new Bitmap(384, 92);
  for (int y = 0; y < 92; y++)
  {
    for (int x = 0; x < 384; x++)
    {
      var i = y * 384 + x;
      var paletteIndex = clut7Bytes[i];
      var color = clut[paletteIndex];
      clutImage.SetPixel(x, y, color);
    }
  }
  clutImage.Save(Path.Combine(outputPath + "\\CLUT7", $"clutImage_palette2_{index}.png"));
} */
