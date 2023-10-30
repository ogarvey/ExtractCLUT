using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Xml;
using ExtractCLUT;
using SixLabors.ImageSharp.Formats.Gif;
using Color = System.Drawing.Color;
using Image = SixLabors.ImageSharp.Image;
using Rectangle = System.Drawing.Rectangle;


/* 
static Color[] ConvertBytesToRGB(byte[] bytes)
{
  Color[] colors = new Color[bytes.Length / 3];
  int colorIndex = 0;

  for (int i = 0; i < bytes.Length; i += 3)
  {
    byte red = bytes[i];
    byte green = bytes[i + 1];
    byte blue = bytes[i + 2];

    Color color = Color.FromArgb(red, green, blue);
    colors[colorIndex] = color;

    colorIndex++;
  }

  return colors;
}

static Bitmap ConvertByteArrayToBitmap(byte[] byteArray, Color[] palette, int width, int height)
{
  Bitmap bitmap = new Bitmap(width, height);
  Color color;
  for (int y = 0; y < height; y++)
  {
    for (int x = 0; x < width; x++)
    {
      byte pixelValue = byteArray[x + y * width];
      int paletteIndex = pixelValue;
      if (paletteIndex >= palette.Length)
      {
         color = Color.Transparent;
      } else {
         color = palette[paletteIndex];
      }
      bitmap.SetPixel(x, y, color);
    }
  }

  return bitmap;
}

static Color[] RotateColors(Color[] colors, int startIndex, int subsetSize)
{
  Color[] rotatedColors = new Color[colors.Length];

  // Copy the original colors to the rotated colors array
  Array.Copy(colors, rotatedColors, colors.Length);

  // Rotate the subset of colors
  var temp = rotatedColors[startIndex - subsetSize];
  for (int i = 1; i < subsetSize; i++)
  {
    rotatedColors[startIndex - i] = rotatedColors[startIndex - i - 1];
  }
  rotatedColors[startIndex] = temp;

  return rotatedColors;
}

static Color[] RotateSubsetOfColors(Color[] colors, int startIndex, int subsetSize)
{
  Color[] rotatedColors = new Color[colors.Length];

  // Copy the original colors to the rotated colors array
  Array.Copy(colors, rotatedColors, colors.Length);

  // Rotate the subset of colors
  rotatedColors[startIndex] = colors[startIndex + (subsetSize-1)];

  for (int i = 0; i < subsetSize; i++)
  {
    rotatedColors[startIndex + i] = colors[startIndex + i];
  }

  return rotatedColors;
}

static void ConvertBitmapsToGif(List<Bitmap> bitmaps, string filePath)
{
  // Set the encoder parameters for the GIF animation
  EncoderParameters encoderParams = new EncoderParameters(1);
  encoderParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);

  // Get the codec for the GIF file format
  ImageCodecInfo gifCodec = GetEncoderInfo("image/gif");

  // Save the first frame of the GIF animation
  bitmaps[0].Save(filePath, gifCodec, encoderParams);

  // Set the encoder parameters for the subsequent frames
  encoderParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);

  // Save the remaining frames of the GIF animation
  for (int i = 1; i < bitmaps.Count; i++)
  {
    bitmaps[0].SaveAdd(bitmaps[i], encoderParams);
  }

  // Set the encoder parameters for the final frame of the GIF animation
  encoderParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);

  // Save the final frame of the GIF animation
  bitmaps[0].SaveAdd(encoderParams);
}

static ImageCodecInfo GetEncoderInfo(string mimeType)
{
  // Get the codec information for the specified MIME type
  ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

  foreach (ImageCodecInfo codec in codecs)
  {
    if (codec.MimeType == mimeType)
    {
      return codec;
    }
  }

  return null;
}

static Bitmap ConvertByteArrayToBitmap2(byte[] bytes, Color[] palette)
{
  int width = 384;
  int height = bytes.Length / width;

  Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

  int stride = bitmap.Width * 4;
  byte[] pixelData = new byte[bitmap.Height * stride];

  for (int y = 0; y < bitmap.Height; y++)
  {
    int lineStart = y * width;
    int pixelOffset = y * stride;

    for (int x = 0; x < bitmap.Width; x++)
    {
      int index = bytes[lineStart + x];
      Color color = palette[index];

      // Add the color to the pixel data array
      pixelData[pixelOffset + x * 4] = color.B;
      pixelData[pixelOffset + x * 4 + 1] = color.G;
      pixelData[pixelOffset + x * 4 + 2] = color.R;
      pixelData[pixelOffset + x * 4 + 3] = color.A;
    }

    // Fill any remaining space in the line with transparent pixels
    for (int x = width; x < bitmap.Width; x++)
    {
      pixelData[pixelOffset + x * 4 + 3] = 0;
    }
  }

  // Copy the pixel data array to the bitmap
  var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
  Marshal.Copy(pixelData, 0, bitmapData.Scan0, pixelData.Length);
  bitmap.UnlockBits(bitmapData);

  return bitmap;
}
void CreateLoopingGif(List<System.Drawing.Bitmap> bitmaps, string outputPath, int frameDelayMilliseconds = 200)
{
  // Convert System.Drawing.Bitmap to ImageSharp.Image<Rgba32>
  var imageSharpImages = bitmaps.Select(bitmap =>
  {
    using var memoryStream = new MemoryStream();
    bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
    memoryStream.Position = 0;
    return Image.Load<Rgba32>(memoryStream);
  }).ToList();

  // Create the GIF encoder with looping enabled
  var gifEncoder = new GifEncoder
  {
    ColorTableMode = GifColorTableMode.Global, // Use a global color table for all frames
  };
  // Set looping metadata
  var gifMetadata = new GifMetadata { RepeatCount = 0 }; // 0 means infinite looping

  // Save the images as a looping GIF
  using (var outputStream = File.OpenWrite(outputPath))
  {
    using var imageCollection = imageSharpImages[0]; // Initialize imageCollection with the first frame
    imageSharpImages[0].Frames.RootFrame.Metadata.GetFormatMetadata(GifFormat.Instance).FrameDelay = frameDelayMilliseconds / 10; // FrameDelay is in hundredths of a second

    for (int i = 1; i < imageSharpImages.Count; i++)
    {
      var image = imageSharpImages[i];
      image.Frames.RootFrame.Metadata.GetFormatMetadata(GifFormat.Instance).FrameDelay = frameDelayMilliseconds / 10;
      imageCollection.Frames.AddFrame(image.Frames[0]);
    }

    imageCollection.Metadata.GetFormatMetadata(GifFormat.Instance).RepeatCount = 0; // Enable looping
    imageCollection.Save(outputStream, gifEncoder);
  }

  // Dispose the ImageSharp.Image<Rgba32> objects
  foreach (var image in imageSharpImages)
  {
    image.Dispose();
  }
}

Bitmap CreatePaletteBitmap(List<Color> palette)
{
  int width = 256;
  int height = 256;
  var bitmap = new Bitmap(width, height);

  for (int row = 0; row < height; row++)
  {
    for (int col = 0; col < width; col++)
    {
      bitmap.SetPixel(col, row, palette[col]);
    }
  }

  return bitmap;
}

static List<byte[]> ReadScreenTiles(byte[] data)
{
  const int ChunkSize = 64;
  const int NumChunks = 32;
  List<byte[]> byteArrayList = new List<byte[]>();

  int offset = 0;
  while (offset < 0x10000)
  {
    for (int i = 0; i < NumChunks; i++)
    {
      byte[] chunk = new byte[ChunkSize];
      Array.Copy(data, offset + i * ChunkSize, chunk, 0, ChunkSize);
      byteArrayList.Add(chunk);
    }
    offset += ChunkSize * NumChunks;
  }
  return byteArrayList;
}

 void ExportTiles(List<byte[]> tiles, List<Color> colors, string filename)
{
  foreach (var (tile, index) in tiles.WithIndex())
  {
    var tileColors = new List<Color>();
    foreach (var item in tile)
    {
      var colorIndex = (int)item;
      tileColors.Add(colors[colorIndex]);
    }
    var file = Path.GetFileNameWithoutExtension(filename);
    var path = Path.GetDirectoryName(filename) + "\\output\\tiles\\" + file;
    var outputName = $"{index}.png";
    Directory.CreateDirectory(path);
    SaveTile(Path.Combine(path, outputName), tileColors);
  }
}

void SaveTile(string outputPath, List<Color> colors)
{
  if (colors.Count != 64)
  {
    throw new ArgumentException("The color list must contain exactly 64 colors.");
  }

  Bitmap image = new Bitmap(8, 8);

  for (int y = 0; y < 8; y++)
  {
    for (int x = 0; x < 8; x++)
    {
      int colorIndex = y * 8 + x;
      image.SetPixel(x, y, colors[colorIndex]);
    }
  }

  image.Save(outputPath, ImageFormat.Png);
}

static int ConvertHexToDecimal(string hexValue)
{
  // Convert the hex string to uppercase and reverse it
  string reversedHex = new string(hexValue.ToUpper().Reverse().ToArray());

  int decimalValue = 0;
  for (int i = 0; i < reversedHex.Length; i++)
  {
    // Get the decimal value of the current hex digit
    int digitValue = "0123456789ABCDEF".IndexOf(reversedHex[i]);

    // Multiply the digit value by 16 raised to the power of its position
    decimalValue += digitValue * (int)Math.Pow(16, i);
  }

  return decimalValue;
}

static void MergeXmlFragments(string directoryPath, string outputFile)
{
  // Create a new XML document to hold the merged chunks
  XmlDocument mergedDoc = new XmlDocument();
  XmlNode rootNode = mergedDoc.CreateElement("data");
  mergedDoc.AppendChild(rootNode);

  // Get all files in the directory
  string[] files = Directory.GetFiles(directoryPath, "*.xml");
  files = files.OrderBy(f => Convert.ToInt32(f.Split('_').Last().Split('.').First())).ToArray();

  // Loop through each file in the directory
  foreach (string file in files)
  {
    // Load the XML file
    XmlDocument xmlFile = new XmlDocument();
    xmlFile.Load(file);

    // Find the <chunk> element and import it into the merged document
    XmlNode chunkNode = xmlFile.SelectSingleNode("//chunk");
    XmlNode importedChunk = mergedDoc.ImportNode(chunkNode, true);
    rootNode.AppendChild(importedChunk);
  }

  // Save the merged XML document to a file
  mergedDoc.Save(Path.Combine(directoryPath, outputFile));
}

static void GenerateIntegerListFromFolder(string folderPath)
{
  // delete existing xml files 
  var xmlFiles = Directory.GetFiles(folderPath, "*.xml");
  foreach (var file in xmlFiles)
  {
    File.Delete(file);
  }
  // Get a list of all files in the specified folder
  var files = Directory.GetFiles(folderPath, "*.bin");
  // sort files by number that is after the last underscore in the filename
  files = files.OrderBy(f => Convert.ToInt32(f.Split('_').Last().Split('.').First())).ToArray();

  // Loop through each file in the folder
  foreach (var (file, index) in files.WithIndex())
  {
    // Read the first 1600 bytes of the file
    var buffer = new byte[1600];
    using var stream = new FileStream(file, FileMode.Open);
    stream.Read(buffer, 0, buffer.Length);

    // Convert every two bytes to an integer and add it to the list
    var integers = new List<int>();
    for (int i = 0; i < buffer.Length; i += 2)
    {
      string hexValue = $"{buffer[i]:X2}{buffer[i + 1]:X2}";
      int value = ConvertHexToDecimal(hexValue);
      integers.Add(1+value);
    }

    // Write the list of integers to a file, separated by commas
    if (index > 4) {
      var output = string.Join(",", integers);
      output = @"<chunk x=""0"" y=""0"" width=""40"" height=""20"">" + output + @"</chunk>";
      File.WriteAllText($"{folderPath}{Path.GetFileNameWithoutExtension(file)}.xml", output);
    }
  }
  MergeXmlFragments(folderPath, "combined_output.xml");
}

List<Color> ReadPalette(byte[] data)
{
  var length = (int)data.Length;
  List<Color> colors = new List<Color>();
  for (int i = 0; i < length; i+=4)
  {
    var color = Color.FromArgb(255, data[i+1], data[i + 2], data[i + 3]);
    colors.Add(color);
  }
  if (colors.Count < 256)
  {
    var remaining = 256 - colors.Count;
    for (int i = 0; i < remaining; i++)
    {
      colors.Add(Color.FromArgb(255, 0, 0, 0));
    }
  }
  return colors;
} */

/* var argosDataFile = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\argos\data\argos_261072_458640_d_5.bin";
var fornaxDataFile = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\fornax\data\fornax_261072_458640_d_5.bin";
var hiveDataFile = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\hive\data\hive_261072_458640_d_5.bin";
var luxorDataFile = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\luxor\data\luxor_261072_458640_d_5.bin";
var ravannaDataFile = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\ravanna\data\ravanna_261072_458640_d_5.bin";
var tektonDataFile = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\tekton\data\tekton_261072_458640_d_5.bin";
var wooDataFile = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\woo\data\woo_261072_458640_d_5.bin";
 */
 
// GenerateIntegerListFromFolder(argosDataFile);
// GenerateIntegerListFromFolder(fornaxDataFile);
// GenerateIntegerListFromFolder(hiveDataFile);
// GenerateIntegerListFromFolder(luxorDataFile);
// GenerateIntegerListFromFolder(ravannaDataFile);
// GenerateIntegerListFromFolder(tektonDataFile);
// GenerateIntegerListFromFolder(wooDataFile);

// Utils.SplitCsvFiles(argosDataFile, argosDataFile + "maps.txt");
// Utils.SplitCsvFiles(fornaxDataFile, fornaxDataFile + "maps.txt");
// Utils.SplitCsvFiles(hiveDataFile, hiveDataFile + "maps.txt");
// Utils.SplitCsvFiles(luxorDataFile, luxorDataFile + "maps.txt");
// Utils.SplitCsvFiles(ravannaDataFile, ravannaDataFile + "maps.txt");
// Utils.SplitCsvFiles(tektonDataFile, tektonDataFile + "maps.txt");
// Utils.SplitCsvFiles(wooDataFile, wooDataFile + "maps.txt");
/* 
var argosData = File.ReadAllBytes(argosDataFile);
var fornaxData = File.ReadAllBytes(fornaxDataFile);
var hiveData = File.ReadAllBytes(hiveDataFile);
var luxorData = File.ReadAllBytes(luxorDataFile);
var ravannaData = File.ReadAllBytes(ravannaDataFile);
var tektonData = File.ReadAllBytes(tektonDataFile);
var wooData = File.ReadAllBytes(wooDataFile);

var argosTiles = ReadScreenTiles(argosData);
var fornaxTiles = ReadScreenTiles(fornaxData);
var hiveTiles = ReadScreenTiles(hiveData);
var luxorTiles = ReadScreenTiles(luxorData);
var ravannaTiles = ReadScreenTiles(ravannaData);
var tektonTiles = ReadScreenTiles(tektonData);
var wooTiles = ReadScreenTiles(wooData);

var argosPalette = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\argos\data\cluts\argos_5.clut");
var fornaxPalette = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\fornax\data\cluts\fornax_5.clut");
var hivePalette = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\hive\data\cluts\hive_5.clut");
var luxorPalette = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\luxor\data\cluts\luxor_5.clut");
var ravannaPalette = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\ravanna\data\cluts\ravanna_5.clut");
var tektonPalette = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\tekton\data\cluts\tekton_5.clut");
var wooPalette = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\woo\data\cluts\woo_5.clut");

var argosColors = ReadPalette(argosPalette);
var fornaxColors = ReadPalette(fornaxPalette);
var hiveColors = ReadPalette(hivePalette);
var luxorColors = ReadPalette(luxorPalette);
var ravannaColors = ReadPalette(ravannaPalette);
var tektonColors = ReadPalette(tektonPalette);
var wooColors = ReadPalette(wooPalette);

ExportTiles(argosTiles, argosColors, argosDataFile);
ExportTiles(fornaxTiles, fornaxColors, fornaxDataFile);
ExportTiles(hiveTiles, hiveColors, hiveDataFile);
ExportTiles(luxorTiles, luxorColors, luxorDataFile);
ExportTiles(ravannaTiles, ravannaColors, ravannaDataFile);
ExportTiles(tektonTiles, tektonColors, tektonDataFile);
ExportTiles(wooTiles, wooColors, wooDataFile);
 */

// var dataFile = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\space\data\space_2352_108192_d_2.bin";
// byte[] byteArray = File.ReadAllBytes(dataFile);

// var paletteFile = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\space\data\space_0_2352_d_1.bin";
// var paletteBytes = File.ReadAllBytes(paletteFile).Take(0x300).ToArray();

// var palette = ConvertBytesToRGB(paletteBytes);



// Color[] originalColors = palette; // Original set of colors
// List<Bitmap> bitmaps = new List<Bitmap>(); // List of bitmaps to generate
// List<Bitmap> paletteBitmaps = new List<Bitmap>(); // List of bitmaps to generate
// List<Bitmap> altBitmaps = new List<Bitmap>(); // List of bitmaps to generate
// int subsetSize = 16; // Size of the subset to rotate
// int width = 384;
// int height = 240;
// var startIndex = 120;
// Color[] rotatedColors = RotateSubsetOfColors(originalColors, startIndex, subsetSize);
// for (int i = startIndex+1; i < startIndex + subsetSize; i++)
// {
//   Bitmap paletteBitmap = CreatePaletteBitmap(rotatedColors.ToList());
//   Bitmap grayscaleBitmap = ConvertByteArrayToBitmap(byteArray, rotatedColors, width, height);
//   Bitmap bitmap = ConvertByteArrayToBitmap2(byteArray, rotatedColors);
//   bitmaps.Add(grayscaleBitmap);// Convert byte array to bitmap
//   altBitmaps.Add(bitmap);// Convert byte array to bitmap
//   paletteBitmaps.Add(paletteBitmap);
//   rotatedColors = RotateColors(rotatedColors, startIndex, subsetSize);
// }


// CreateLoopingGif(bitmaps, @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\space\data\output\test_120_16.gif");

// CreateLoopingGif(paletteBitmaps, @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\space\data\output\palette_120_16.gif");
