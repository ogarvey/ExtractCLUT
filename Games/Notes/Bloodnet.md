
var blFile = @"C:\Program Files (x86)\GOG Galaxy\Games\Bloodnet\Bloodnet\BLOODNET\ART\intrface.PL";

var bloodnetPlFile = new BloodnetPlFile(blFile);

// write details to console
Console.WriteLine($"Name: {bloodnetPlFile.Name}");
Console.WriteLine($"Name Table Offset: {bloodnetPlFile.NameTableOffset}");
Console.WriteLine($"Image Count: {bloodnetPlFile.ImageCount}");

// populate the image blocks
bloodnetPlFile.PopulateImageBlocks();

// output the image block data
bloodnetPlFile.OutputImageBlockData(@"C:\Dev\Projects\Gaming\VGR\PC\Bloodnet\Output");

class BloodnetPlFile
{
  public string Name { get; set; }
  public int NameTableOffset { get; set; }
  public int ImageCount { get; set; }
  public byte[] Data { get; set; }
  public List<BloodnetImageHeader> ImageBlocks { get; set; }

  public BloodnetPlFile(string path)
  {
    var name = Path.GetFileNameWithoutExtension(path);
    if (!File.Exists(path))
    {
      throw new FileNotFoundException($"File not found: {path}");
    }
    Data = File.ReadAllBytes(path);
    var imageCount = BitConverter.ToInt16(Data.Take(2).ToArray(), 0);
    NameTableOffset = BitConverter.ToInt32(Data.Skip(2).Take(4).ToArray());
    Name = name;
    ImageCount = imageCount;
    ImageBlocks = new List<BloodnetImageHeader>();
  }

  public void OutputImageBlockData(string outputPath)
  {
    if (ImageBlocks == null || ImageBlocks.Count == 0)
    {
      throw new InvalidOperationException("ImageBlocks is null or empty");
    }

    if (!Directory.Exists(outputPath))
    {
      Directory.CreateDirectory(outputPath);
    }

    var subDir = Path.Combine(outputPath, Name);
    if (!Directory.Exists(subDir))
    {
      Directory.CreateDirectory(subDir);
    }

    foreach (var imageBlock in ImageBlocks)
    {
      // go to the offset, take data until the next offset
      // save the data to a file,if the last offset take data until the NameTableOffset
      var nextOffset = ImageBlocks.IndexOf(imageBlock) == ImageBlocks.Count - 1 ? (uint)NameTableOffset : ImageBlocks[ImageBlocks.IndexOf(imageBlock) + 1].Offset;
      var imageData = Data.Skip((int)imageBlock.Offset).Take((int)(nextOffset - (int)imageBlock.Offset)).ToArray();
      File.WriteAllBytes(Path.Combine(subDir, $"{imageBlock.Name}.bin"), imageData);
    }
  }

  public void PopulateImageBlocks()
  {
    if (Data == null || Data.Length == 0)
    {
      throw new InvalidOperationException("Data is null or empty");
    }
    var imageHeaderData = Data.Skip(NameTableOffset).Take(ImageCount * 0xC).ToArray();

    for (int i = 0; i < imageHeaderData.Length; i += 0xC)
    {
      var imageHeader = new BloodnetImageHeader
      {
        Offset = BitConverter.ToUInt32(imageHeaderData.Skip(i).Take(4).ToArray(), 0),
        Name = Encoding.ASCII.GetString(imageHeaderData.Skip(i + 4).Take(8).ToArray()).TrimEnd('\0')
      };
      Console.WriteLine($"Image: {imageHeader.Name}, Offset: {imageHeader.Offset:X8}");
      ImageBlocks?.Add(imageHeader);
    }
  }
}
public class BloodnetImageHeader
{
  public uint Offset { get; set; }
  public string Name { get; set; }
}
