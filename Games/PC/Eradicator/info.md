var palFile = @"C:\Dev\Gaming\PC\Dos\Games\ERADE\output\PALETTE_extracted\LAB_extracted\PALETTE";
var palData = File.ReadAllBytes(palFile);
var pal = ColorHelper.ConvertBytesToRGB(palData);

var spriteTestFileDir = @"C:\Dev\Gaming\PC\Dos\Games\ERADE\output\SPR_ACTIONS_extracted\";
var spriteTestFiles = Directory.GetFiles(spriteTestFileDir, "*.*");

foreach (var spriteTestFile in spriteTestFiles)
{
  var outputDir = Path.Combine(Path.GetDirectoryName(spriteTestFile)!, Path.GetFileNameWithoutExtension(spriteTestFile) + "_output");
  Directory.CreateDirectory(outputDir);
  using var spriteReader = new BinaryReader(File.OpenRead(spriteTestFile));

  var count1 = spriteReader.ReadByte();
  var count2 = spriteReader.ReadByte();

  var actualCount = count1 * count2;

  spriteReader.ReadBytes(2); // skip 2 bytes

  var spriteHeaders = new List<SpriteHeader>();

  for (int i = 0; i < actualCount; i++)
  {
    var header = new SpriteHeader
    {
      YOffset = spriteReader.ReadByte(),
      XOffset = spriteReader.ReadByte(),
      Flag1 = spriteReader.ReadByte(),
      Flag2 = spriteReader.ReadByte(),
      DataOffset = spriteReader.ReadUInt32()
    };
    spriteHeaders.Add(header);
  }

  foreach (var (header, index) in spriteHeaders.WithIndex())
  {
    spriteReader.BaseStream.Seek(header.DataOffset, SeekOrigin.Begin);
    var height = spriteReader.ReadInt32();
    var width = spriteReader.ReadInt32();
    spriteReader.ReadBytes(4); // skip 4 bytes
    var imageData = spriteReader.ReadBytes((int)(width * height));
    var image = ImageFormatHelper.GenerateClutImage(pal, imageData, width, height, true);
    var imageName = $"{Path.GetFileNameWithoutExtension(spriteTestFile)}_{index}_{header.XOffset}_{header.YOffset}.png";
    var outputPath = Path.Combine(outputDir, imageName);
    if (header.Flag1 == 1)
    {
      // flip image vertically
      image.RotateFlip(RotateFlipType.RotateNoneFlipY); 
    }
    image.Save(outputPath, ImageFormat.Png);
  }

  FileHelpers.AlignSprites(outputDir, Path.Combine(outputDir, "aligned"));
}

class SpriteHeader
{
  public byte YOffset { get; set; }
  public byte XOffset { get; set; }
  public byte Flag1 { get; set; }
  public byte Flag2 { get; set; }
  public uint DataOffset { get; set; }
};
