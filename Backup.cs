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


// var data = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Steel Machine\DATA\BINs\level1_1_0_0.bin").Skip(0x800).ToArray();

// var offsets = data.Take(0x30).ToArray();

// var offsetList = new List<int>();

// for (int i = 0; i < offsets.Length; i += 4)
// {
//     var offset = BitConverter.ToInt32(offsets.Skip(i).Take(4).Reverse().ToArray(), 0);
//     offsetList.Add(offset);
// }

// var blobs = new List<byte[]>();

// for (int i = 0; i < offsetList.Count; i++)
// {
//     var start = offsetList[i];
//     var end = i == offsetList.Count - 1 ? data.Length : offsetList[i + 1];
//     var blob = data.Skip(start).Take(end - start).ToArray();
//     blobs.Add(blob);
// }

// foreach (var (blob, index) in blobs.WithIndex())
// {
//     File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Steel Machine\DATA\BINs\L1\{index}.bin", blob);
// }


// string filePath = @"C:\Program Files (x86)\GOG Galaxy\Games\Monkey Island 1 SE\Monkey1.pak";
// int minimumLength = 5; // Set your minimum string length
// List<string> asciiStrings = await FileFormatHelper.ScanForAsciiStringsAsync(filePath, minimumLength, requireNullTerminated: true);
// var dxtStrings = asciiStrings.Where(s => s.Contains("DXT")).ToList();
// Console.WriteLine($"{dxtStrings.Count} ASCII strings found:");



//----------------------------------------------------------------------------------------------//


//var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level2";


//TheApprentice.ExtractMapInfo(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\map7_1.bin");
//TheApprentice.ExtractBinaryData();
//TheApprentice.ExtractGoGfx();

//CropImageFolder(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\levelb\sprites\walk", "*.png", 0, 0, 31, 40, true);  

// var blkFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF", "*.blk");

// var cdiFiles = blkFiles.Select(f => new CdiFile(f)).ToList();

// var outputDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF\Output";
// Directory.CreateDirectory(outputDir);

// foreach (var cdiFile in cdiFiles)
// {
//     var dataSectors = cdiFile.DataSectors.OrderBy(s => s.Channel).ThenBy(s => s.SectorIndex).ToList();
//     var data = dataSectors.SelectMany(s => s.GetSectorData()).ToArray();
//     var filename = Path.GetFileNameWithoutExtension(cdiFile.FilePath);
//     File.WriteAllBytes($@"{outputDir}\{filename}.bin", data);
// }

// var binFiles = Directory.GetFiles(outputDir, "*.bin");

// var apprenticeFiles = new List<ApprenticeFile>();

// foreach (var file in binFiles)
// {
//     // if (!file.Contains("levelb"))
//     // {
//     //     continue;
//     // }
//     var aFile = new ApprenticeFile(file);
//     //apprenticeFiles.Add(aFile);
//     if (aFile.SubFiles.Count == 0)
//     {
//         continue;
//     }
//     var outputFolder = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(file));
//     Directory.CreateDirectory(outputFolder);
//     foreach (var (blob, index) in aFile.SubFiles.WithIndex())
//     {
//         File.WriteAllBytes(Path.Combine(outputFolder, $"{index}.bin"), blob);
//     }
// }

// var tileData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF\Output\gfxset6\1.bin");
// var palette = ConvertBytesToRGB(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF\Output\gfxset6\0.bin")
//                 .Take(0x180).ToArray());

// var tileImage = ImageFormatHelper.GenerateClutImage(palette, tileData, 320, 192,true);
// tileImage.Save(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF\Output\gfxset6\tiles.png", ImageFormat.Png);

//CropImageFolder(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\RTF\Output\big\sprites", "*.png", 0, 0, 16, 16, true);

//CropImageFolderRandom(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Dimo's Quest\Asset Extraction\LEvels", "*.png", 148, 125);


// var inputDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis";
// var outputDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks";
// Directory.CreateDirectory(outputDir);

// var binFiles = Directory.GetFiles(inputDir, "*.bin");

// //var apprenticeFiles = new List<VisionFactoryFile>();

// foreach (var file in binFiles)
// {
//     // if (!file.Contains("levelb"))
//     // {
//     //     continue;
//     // }
//     var aFile = new VisionFactoryFile(file);
//     //apprenticeFiles.Add(aFile);
//     if (aFile.SubFiles.Count == 0)
//     {
//         continue;
//     }
//     var outputFolder = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(file));
//     Directory.CreateDirectory(outputFolder);
//     foreach (var (blob, index) in aFile.SubFiles.WithIndex())
//     {
//         File.WriteAllBytes(Path.Combine(outputFolder, $"{index}.bin"), blob);
//     }
// }
//  var dataFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\4.bin";
//  var dat = File.ReadAllBytes(dataFile);
// // // //CropImageFolder(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\sprites\trot", "*.png", 0, 52, 126, 126, true);
// var blobs = LuckyLuke.BlockParser(dat,false);
// foreach (var (blob, index) in blobs.WithIndex())
// {
//     File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\4\{index}.bin", blob);
// }
//LuckyLuke.BlockParser(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv1s3.blk_1_0_0\0\6.bin",true);
// var mainDataFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\1.bin";
// //var offsetFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\1", "*.bin").OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f))).ToArray();
//var spriteDataList = new List<List<VFSpriteData>>();

// foreach (var bin in offsetFiles)
// {
//     var tempList = new List<SpriteData>();
//     var data = File.ReadAllBytes(bin);
//     for (int i = 0; i < data.Length; i+= 16)
//     {
//         if (i + 15 >= data.Length)
//         {
//             break;
//         }
//         var offset = BitConverter.ToInt32(data.Skip(i + 4).Take(4).Reverse().ToArray(), 0);
//         var width = data.Skip(i+13).Take(1).First();
//         var height = BitConverter.ToInt16(data.Skip(i + 14).Take(2).Reverse().ToArray(), 0);
//         tempList.Add(new SpriteData { Width = width, Height = height, Offset = offset });

//     }
//     spriteDataList.Add(tempList);
// }

// var palFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\2.bin";
// var spriteFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\5", "*.bin").OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f))).ToArray();
// //var spriteData = File.ReadAllBytes(mainDataFile);
// var paletteData = File.ReadAllBytes(palFile).Take(0x180).ToArray();

// var palette = ConvertBytesToRGB(paletteData);
// var spriteImageOutputPath = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv3s1.blk_1_0_0\0\sprites_5";
// Directory.CreateDirectory(spriteImageOutputPath);
// var combinedImagePath = Path.Combine(spriteImageOutputPath, "combined");
// Directory.CreateDirectory(combinedImagePath);

// var tempImageList = new List<Image>();
// var spriteIndex= 0;
// foreach (var mainDataFile in spriteFiles)
// {
//     var spriteData = File.ReadAllBytes(mainDataFile);
//     if (spriteData.Length == 0)
//     {
//         tempImageList.Add(GenerateTransparentImage(4, 20));
//     }
//     else
//     {
//         var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, 0, 0x180);
//         var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
//         var cropped = CropImage(image, 4, 20, 0, 1);
//         tempImageList.Add(cropped);
//     }
//     if (tempImageList.Count == 5) {
//         var combinedImage = LuckyLuke.CombineFGImages(tempImageList);
//         combinedImage.Save($@"{combinedImagePath}\{spriteIndex}.png", ImageFormat.Png);
//         tempImageList.Clear();
//         spriteIndex++;
//     }
// }

// static Image GenerateTransparentImage(int width, int height)
// {
//     var image = new Bitmap(width, height);
//     for (int i = 0; i < width; i++)
//     {
//         for (int j = 0; j < height; j++)
//         {
//             image.SetPixel(i, j, Color.FromArgb(0, 0, 0, 0));
//         }
//     }
//     return image;
// }

// foreach (var (list, lIndex) in spriteDataList.WithIndex()) {
//     foreach (var (sprite, sIndex) in list.WithIndex())
//     {
//         var data = spriteData.Skip(sprite.Offset).ToArray();
//         var output = CompiledSpriteHelper.DecodeCompiledSprite(data, 0, 0x180);
//         var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
//         if (sprite.Width <= 0 || sprite.Height <= 0 || sprite.Width > 384 || sprite.Height > 240)
//         {
//             image.Save($@"{spriteImageOutputPath}\{lIndex}_{sIndex}.png", ImageFormat.Png);
//             continue;
//         }
//         CropImage(image, sprite.Width, sprite.Height, 0, 1).Save($@"{spriteImageOutputPath}\{lIndex}_{sIndex}.png", ImageFormat.Png);
//     }
// }

// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv1s1.blk_1_0_0\0\output\4.bin";
// var fgTileImageFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Asset Extraction\lv1s1.blk_1_0_0\fgDayTiles", "*.png").OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f))).ToArray();
// var bgTileImageFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Asset Extraction\lv1s1.blk_1_0_0\bgDayTiles", "*.png").OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f))).ToArray();
// var data = File.ReadAllBytes(file).Take(0x1810).ToArray();

// var uintList = new List<uint>();

// for (int i = 0; i < data.Length; i += 4)
// {
//     var value = BitConverter.ToUInt32(data.Skip(i).Take(4).Reverse().ToArray(), 0) / 40;
//     uintList.Add(value);
// }

// var tempImageList = new List<Image>();

// for (int i = 0; i < uintList.Count; i++)
// {
//     var tileIndex = (int)uintList[i];
//     var tileImage = Image.FromFile(bgTileImageFiles[tileIndex]);
//     tempImageList.Add(tileImage);
//     if (tempImageList.Count == 10)
//     {
//         var combinedImage = LuckyLuke.CombineBGImages(tempImageList);
//         combinedImage.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Asset Extraction\lv1s1.blk_1_0_0\bgDayTiles\combined\{i / 10}.png", ImageFormat.Png);
//         tempImageList.Clear();
//     }
// }


// var mapData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv1s3.blk_1_0_0\ItemMap.bin");

// OutputTileMap(mapData, 480, 15, @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv1s3.blk_1_0_0\ItemMap.txt");




// var inDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke";
// var outDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\ExtractAllLevelDataTest";

// LuckyLuke.ExtractAllLevelData(inDir, outDir);
// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv6s1.blk_1_0_0\1.bin";
// var blobs = LuckyLuke.BlockParser(File.ReadAllBytes(file),true);
// foreach (var (blob, index) in blobs.WithIndex())
// {
//     File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv6s1.blk_1_0_0\1\{index}.bin", blob);
// }

//var fgTxt = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\ExtractAllLevelDataTest\lv4s3\fgMap.txt";
// var itemTxt = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\ExtractAllLevelDataTest\lv1s1\actionMap.txt";

// // //IncrementNumbersInFile(fgTxt, 161, 480);
// IncrementNumbersInFile(itemTxt, 873, 480);

// void IncrementNumbersInFile(string filePath, int incrementAmount, int width)
// {
//     try
//     {
//         // Read the contents of the file
//         string fileContent = File.ReadAllText(filePath);

//         // create a backup of the original file
//         File.WriteAllText(filePath + ".bak", fileContent);

//         // Split the content into individual numbers
//         string[] numberStrings = fileContent.Split(',');

//         // Increment each number by the specified amount
//         var incrementedNumbers = numberStrings
//             .Select(number => int.Parse(number.Trim()) + incrementAmount)
//             .ToList();

//         var sb = new StringBuilder();

//         for (int i = 0; i < incrementedNumbers.Count; i ++)
//         {
//             sb.Append($"{incrementedNumbers[i].ToString()},");
//             if (i  % width == 0)
//             {
//                 sb.AppendLine();
//             }
//         }

//         // Join the numbers back into a comma-separated string
//         string updatedContent = sb.ToString().Trim().TrimEnd(',');

//         // Write the updated string back to the file
//         File.WriteAllText(filePath, updatedContent);

//         Console.WriteLine("Numbers incremented successfully.");
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"An error occurred: {ex.Message}");
//     }
// }


// GenerateImages(256, @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\ExtractAllLevelDataTest\ActionTiles");

// void GenerateImages(int count, string outputDirectory)
// {
//     for (int i = 0; i < count; i++)
//     {
//         using (Bitmap bitmap = new Bitmap(20, 20))
//         {
//             // Set the image to have a transparent background
//             bitmap.MakeTransparent();

//             using (Graphics graphics = Graphics.FromImage(bitmap))
//             {
//                 graphics.Clear(Color.Transparent);

//                 // Set up the font
//                 using (Font font = new Font("Arial", 10))
//                 {
//                     // Measure the string to determine the position
//                     string hexText = i.ToString("X"); 
//                     SizeF textSize = graphics.MeasureString(hexText, font);
//                     PointF position = new PointF(
//                         (bitmap.Width - textSize.Width) / 2,
//                         (bitmap.Height - textSize.Height) / 2
//                     );

//                     // Draw the text
//                     using (Brush brush = new SolidBrush(Color.Fuchsia))
//                     {
//                         graphics.DrawString(hexText, font, brush, position);
//                     }
//                 }
//             }

//             // Save the image
//             string fileName = $"{outputDirectory}/image_{i}.png";
//             bitmap.Save(fileName, ImageFormat.Png);
//         }
//     }

//     Console.WriteLine("Images generated successfully.");
// }

// var binFiles = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv1s1.blk_1_0_0\1", "*.bin").OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f))).ToArray();
// var outputDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\blks\lv1s1.blk_1_0_0\1\audio";
// Directory.CreateDirectory(outputDir);
// foreach (var file in binFiles) 
// {
//   var newFile = Path.Combine(outputDir, Path.GetFileName(file));
//   ConvertMp2ToWavAndMp3(file, Path.ChangeExtension(newFile, ".wav"), "wav");
// }

// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Steel Machine\DATA\BINs\level1_1_0_0\0\1.bin";
// // var palFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Steel Machine\DATA\BINs\level1_1_0_0\0\11.bin";
// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Steel Machine\DATA\BINs\level1_1_0_0\0\1";

// Directory.CreateDirectory(outputFolder);
// var palData = File.ReadAllBytes(palFile).Skip(480).Take(96).ToArray();
// var palList = new List<List<Color>>();

// var palette = ConvertBytesToRGB(palData);
// palList.Add(palette);
// var tiles = new List<byte[]>();

// var data = File.ReadAllBytes(file);

// var tileInt16List = new List<int>();
// var sb = new StringBuilder();
// for (int i = 0; i < data.Length; i += 2)
// {
//     var tile = BitConverter.ToInt16(data.Skip(i).Take(2).Reverse().ToArray(), 0);
//     tileInt16List.Add(tile);
// }

// for (int i = 0; i < tileInt16List.Count; i++)
// {
//     sb.Append($"{(tileInt16List[i] + 1).ToString()},");
//     if (i % 450 == 0)
//     {
//         sb.AppendLine();
//     }
// }

// File.WriteAllText(Path.Combine(outputFolder, "output.txt"), sb.ToString().Trim().TrimEnd(','));



//AudioHelper.ConvertIffToWav(file, @"C:\Dev\Projects\Gaming\VGR\bullfrog_utils_rnc\183.wav");

// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss4.blk_1_0_0.bin";
// var vFile = new VisionFactoryFile(file);
// var llpalData = vFile.SubFiles[0].Take(0x180).ToArray();
// var llpalette = ConvertBytesToRGB(llpalData);
// var output = LuckyLuke.BossBlockParser(vFile.SubFiles[2], llpalette);

// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss4.blk_1_0_0\sprites";

// Directory.CreateDirectory(outputFolder);

// for (int i = 0; i < output.Count; i++)
// {
//   for (int j = 0; j < output[i].Count; j++)
//   {
//     output[i][j].Save(Path.Combine(outputFolder, $"{i}_{j}.png"), ImageFormat.Png);
//   }
// }

// File.WriteAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss4.blk_1_0_0\palette.bin", llpalData);
// File.WriteAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss4.blk_1_0_0\bg.bin", vFile.SubFiles[1]);
//ImageFormatHelper.GenerateClutImage( llpalette, vFile.SubFiles[1], 512,256).Save(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss4.blk_1_0_0\bg.png", ImageFormat.Png);
// var file = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss2.blk_1_0_0.bin";

// var vFile = new VisionFactoryFile(file);

// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Lucky Luke\Analysis\boss2.blk_1_0_0\output";

// Directory.CreateDirectory(outputFolder);

// foreach (var (blob, index) in vFile.SubFiles.WithIndex())
// {
//     File.WriteAllBytes(Path.Combine(outputFolder, $"{index}.bin"), blob);
// }


// For a given folder, convert all dds files to png
// Then move the dds to a backup folder
// then create a backup of the .dae file, and move to the same folder
// then parse the replacement .dae file to replace all ".dds" references with ".png"

// var bkgdPath = @"C:\Dev\Gaming\PC_Windows\Games\Jinx\MMPOutput\Bkgnd";
// var bkgdOutput = @"C:\Dev\Gaming\PC_Windows\Games\Jinx\MMPOutput\BkgndOutput";
// Directory.CreateDirectory(bkgdOutput);
// var bkgdFiles = Directory.GetFiles(bkgdPath, "*.bin");

// foreach (var bkgs in bkgdFiles)
// {
//   var data = File.ReadAllBytes(bkgs);
//   // filename contains height and width eg: BKGD_BoatDock.PICT_480_640.bin
//   var filename = Path.GetFileNameWithoutExtension(bkgs);
//   var parts = filename.Split('_');
//   var width = int.Parse(parts[parts.Length -1]);
//   var height = int.Parse(parts[parts.Length -2]);
//   var image = ImageFormatHelper.DecodeRgba(data, width, height);
//   image.Save(Path.Combine(bkgdOutput, $"{Path.GetFileNameWithoutExtension(bkgs)}.png"), ImageFormat.Png);
// }

// var mmfwFile = @"C:\Dev\Gaming\PC_Windows\Games\Jinx\Jinx\HD.MMP";
// var mmfwOutput = @"C:\Dev\Gaming\PC_Windows\Games\Jinx\MMPOutput";
// Directory.CreateDirectory(mmfwOutput);

// using var mReader = new BinaryReader(File.OpenRead(mmfwFile));

// mReader.BaseStream.Seek(0x22, SeekOrigin.Begin);

// var numFiles = mReader.ReadBigEndianUInt16();

// var offsets = new List<uint>();
// var names = new List<string>();
// var widthAndHeights = new List<(ushort, ushort)>();

// for (int i = 0; i < numFiles + 1; i++)
// {
//   offsets.Add(mReader.ReadBigEndianUInt32());
// }


// for (int i = 0; i < numFiles; i++)
// {
//   var nameBytes = mReader.ReadBytes(0x20).Skip(2).ToArray();
//   var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
//   names.Add(name);
// }

// var unkBlock1 = mReader.ReadBytes(0x20b0); 
// var unkBlock2 = mReader.ReadBytes(0x1056);

// for (int i = 0; i < numFiles; i++)
// {
//   var width = mReader.ReadBigEndianUInt16();
//   var height = mReader.ReadBigEndianUInt16();
//   widthAndHeights.Add((width, height));
// }

// for (int i = 0; i < offsets.Count; i++)
// {
//   var offset = offsets[i];
//   var nextOffset = i == offsets.Count - 1 ? mReader.BaseStream.Length : offsets[i + 1];
//   var length = nextOffset - offset;
//   if (length == 0) continue;
//   mReader.BaseStream.Seek(offset, SeekOrigin.Begin);
//   var data = mReader.ReadBytes((int)length);
//   var name = names[i];
//   var (width, height) = widthAndHeights[i];
//   File.WriteAllBytes(Path.Combine(mmfwOutput, $"{name}_{width}_{height}.bin"), data);
// }

// var yodaDat = @"C:\Users\OGCit\Downloads\yoda_20220125\YODA\Yoda\yodesk.dta";
// var yodaOutput = @"C:\Users\OGCit\Downloads\yoda_20220125\YODA\Yoda\yodeskOutput";
// var iZonOutput = @"C:\Users\OGCit\Downloads\yoda_20220125\YODA\Yoda\yodeskOutput\iZon";
// Directory.CreateDirectory(yodaOutput);
// Directory.CreateDirectory(iZonOutput);
// using var yReader = new BinaryReader(File.OpenRead(yodaDat));
// yReader.BaseStream.Seek(0x8, SeekOrigin.Begin);

// while (yReader.BaseStream.Position < yReader.BaseStream.Length)
// {
//   var pos = yReader.BaseStream.Position;
//   var type = Encoding.ASCII.GetString(yReader.ReadBytes(4));
//   if (type == "ZONE") {
//     var izonCount = yReader.ReadUInt16();
//     for (int i = 0; i < izonCount; i++)
//     {
//       pos = yReader.BaseStream.Position;
//       var izType = yReader.ReadUInt16();
//       var izLength = yReader.ReadUInt32();
//       var izData = yReader.ReadBytes((int)izLength);
//       var id = BitConverter.ToUInt16(izData.Take(2).ToArray());
//       File.WriteAllBytes(Path.Combine(iZonOutput, $"{type}_{id}_0x{pos:X8}.bin"), izData);
//     }
//     Console.WriteLine($"Processed {type} at 0x{pos:X8}");
//     continue;
//   }
//   var bytesToRead = yReader.ReadUInt32();
//   var data = yReader.ReadBytes((int)bytesToRead);
//   File.WriteAllBytes(Path.Combine(yodaOutput, $"{type}_0x{pos:X8}.bin"), data);
// }


// var imageInputFolder = @"C:\GOGGames\Simon the Sorcerer - 25th Anniversary Edition\Simon1_data_all.bundle";
// var outputFolder = @"C:\GOGGames\Simon the Sorcerer - 25th Anniversary Edition\Simon1_data_all.bundle\VGAOutput";

// Directory.CreateDirectory(outputFolder);

// var vgaFiles = Directory.GetFiles(imageInputFolder, "*.VGA");

// var oddVgaFiles = vgaFiles.Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("1")).ToArray();
// var evenVgaFiles = vgaFiles.Where(f => Path.GetFileNameWithoutExtension(f).EndsWith("2")).ToArray();

// foreach (var vgaFile in evenVgaFiles)
// {
//   var data = File.ReadAllBytes(vgaFile);
//   var vgaOutputFolder = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(vgaFile));
//   Directory.CreateDirectory(vgaOutputFolder);
//   if (BitConverter.ToInt32(data.Take(4).Reverse().ToArray(), 0) != 0) continue;
//   // get the odd file which will be this file - 1
//   // eg: if vgaFile is 0002.VGA, then the odd file will be 0001.VGA
//   // ensure that the zero padding is correct
//   var oddFile = Path.Combine(imageInputFolder, $"{(int.Parse(Path.GetFileNameWithoutExtension(vgaFile)) - 1).ToString("D4")}.VGA");

//   var paletteData = File.ReadAllBytes(oddFile);
//   var paletteCount = BitConverter.ToUInt16(paletteData.Take(2).Reverse().ToArray(), 0);

//   var palStart = 6;

//   var palettes = new List<List<Color>>();

//   for (int i = 0; i < paletteCount; i++)
//   {
//     var palette = paletteData.Skip(palStart + (i * 0x60)).Take(0x60).ToArray();
//     palettes.Add(ColorHelper.ConvertBytesToRGB(palette,4));
//   }

//   var offsetList = new List<spraOffset>();

//   var index = 0;

//   while (data[index] == 0x00)
//   {
//     var offset = BitConverter.ToInt32(data.Skip(index).Take(4).Reverse().ToArray(), 0);
//     var height = BitConverter.ToUInt16(data.Skip(index + 4).Take(2).Reverse().ToArray(), 0);
//     var width = BitConverter.ToInt16(data.Skip(index + 6).Take(2).Reverse().ToArray(), 0);
//     offsetList.Add(new spraOffset { Length = offset, Width = width, Height = height });
//     index += 8;
//   }

//   for (int i = 0; i < offsetList.Count; i++)
//   {
//     if (offsetList[i].Width <= 0 || offsetList[i].Height <= 0 || offsetList[i].Width > 1280 || offsetList[i].Height >= 65535) continue;
//     var offset = offsetList[i];
//     if (offset.Length == 0) continue;
//     var nextOffset = i == offsetList.Count - 1 ? data.Length : offsetList[i + 1].Length;
//     var bytes = data.Skip(offset.Length).Take(nextOffset - offset.Length).ToArray();
//     var compressed = (offset.Height & 0x8000) != 0x0;
//     var actualHeight = offset.Height & 0x7FFF;
//     if (compressed)
//     {
//       bytes = AgosCompression.DecodeImage(bytes, 0, null, actualHeight, offset.Width / 2);
//     }

//     File.WriteAllBytes(Path.Combine(vgaOutputFolder, $"{i}.bin"), bytes);
//     for (int j = 0; j < palettes.Count; j++)
//     {
//       var palOutputFolder = Path.Combine(vgaOutputFolder, $"Palette_{j}");
//       var palOutputFolderTransparent = Path.Combine(vgaOutputFolder, $"Palette_{j}_Transparent");
//       Directory.CreateDirectory(palOutputFolder);
//       Directory.CreateDirectory(palOutputFolderTransparent);
//       var image = ImageFormatHelper.Decode4Bpp(bytes, palettes[j], offset.Width, actualHeight);
//       image.Save(Path.Combine(palOutputFolder, $"{i}_{j}.png"), ImageFormat.Png);
//       image = ImageFormatHelper.Decode4Bpp(bytes, palettes[j], offset.Width, actualHeight, true);
//       image.Save(Path.Combine(palOutputFolderTransparent, $"{i}_{j}.png"), ImageFormat.Png);
//     }
//   }
// }

//var aniInputDir = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\CHILL\LEVELS";
// var aniPaths = Directory.GetFiles(aniInputDir, "*.*");
// var pcxPaths = Directory.GetFiles(aniInputDir, "*.pcx");
//var labPaths = Directory.GetFiles(@"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Chill", "*.lab", SearchOption.AllDirectories);
// var imgPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Chill-Manor_DOS_EN\DATA\LEVELS\";
// var palPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Chill-Manor_DOS_EN\DATA\RES\output\RESINT\game_scr.pcx";
// var imPalPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\IM-Meen_DOS_EN\lab_output\RESINT\GAME_SCR.PCX";

// var imageImgPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Chill-Manor_DOS_EN\DATA\LEVELS\output\RES001\IMAGES.IMG";
// foreach (var labPath in labPaths)
// {
//   AniMagic.ExtractLab(labPath);
// }

// var palBytes = File.ReadAllBytes(palPath).Skip(0x5379).Take(0x300).ToArray();
// File.WriteAllBytes(@"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Chill-Manor_DOS_EN\DATA\RES\output\RESINT\game_scr.bin", palBytes);
// var imPalBytes = File.ReadAllBytes(imPalPath).Skip(0x4A09).Take(0x300).ToArray();
// var palette = ColorHelper.ConvertBytesToRGB(palBytes, 1);

// ColorHelper.WritePalette(Path.ChangeExtension(imPalPath, ".png"), palette);

// //ExtractIMG(imageImgPath, palette, true, true);

// var imgFiles = Directory.GetFiles(imgPath, "*.img", SearchOption.AllDirectories)
//   .Where(f => !f.ToLower().Contains("image")).ToArray();

// var imageImgFiles = Directory.GetFiles(imgPath, "*.img", SearchOption.AllDirectories)
//   .Where(f => f.ToLower().Contains("image")).ToArray();

// foreach (var imgFile in imageImgFiles)
// {
//   AniMagic.ExtractIMG(imgFile, palette, true, true);
// }

// foreach (var imgFile in imgFiles)
// {
//   AniMagic.ExtractIMG(imgFile, palette, true, false);
// }

// var cmpPath = @"C:\Dev\Projects\Gaming\VGR\PC\MSDOS\Chill-Manor_DOS_EN\DATA\RES\output\RESFACE\amul01.cmp";
// var cmpData = File.ReadAllBytes(cmpPath).Skip(0x4).ToArray();

// var offsetList = new List<ushort>();
// var offsetData = cmpData.Skip(0x56).Take(0x32).ToArray();

// for (int i = 0; i < offsetData.Length; i += 2)
// {
//   var offset = BitConverter.ToUInt16(offsetData.Skip(i).Take(2).ToArray(), 0);
//   offsetList.Add(offset);
// }

// var imageDataLines = new List<byte[]>();

// for (int i = 0; i < offsetList.Count; i++)
// {
//   var start = offsetList[i];
//   var end = i == offsetList.Count - 1 ? cmpData.Length : offsetList[i + 1];
//   var line = cmpData.Skip(start).Take(end - start).ToArray();
//   imageDataLines.Add(line);
// }

// var imageLines = new List<byte[]>();

// foreach (var line in imageDataLines)
// {
//   var lineData = new byte[128];
//   var lineIndex = 0;
//   for (int i = 0; i < line.Length; i++)
//   {
//     if (lineIndex >= lineData.Length-1)
//     {
//       break;
//     }
//     var b = line[i];
//     if ((b & 0x80) == 0x80)
//     {
//       var count = b & 0x7F;
//       for (int j = 0; j < count && lineIndex < lineData.Length; j++)
//       {
//         lineData[lineIndex] = 0x0;
//         lineIndex++;
//       }
//     }
//     else
//     {
//       lineData[lineIndex] = b;
//       lineIndex++;
//     }
//   }
//   imageLines.Add(lineData);
// }

// var image = ImageFormatHelper.GenerateClutImage(palette, imageLines.SelectMany(l => l).ToArray(), 128, 64, true);
// image.Save(Path.ChangeExtension(cmpPath, ".png"), ImageFormat.Png);
// File.WriteAllBytes(Path.ChangeExtension(cmpPath, ".bin"), imageLines.SelectMany(l => l).ToArray());

// foreach (var pcxPath in pcxPaths)
// {
//   // check first three bytes are 0A 05 01
//   var pcxData = File.ReadAllBytes(pcxPath);
//   if (pcxData[0] != 0x0A || pcxData[1] != 0x05 || pcxData[2] != 0x01) continue;
//   var pcxOutputDirectory = Path.Combine(Path.GetDirectoryName(pcxPath), "output", Path.GetFileNameWithoutExtension(pcxPath));
//   Directory.CreateDirectory(pcxOutputDirectory);
//   var pngPath = Path.Combine(pcxOutputDirectory, Path.GetFileNameWithoutExtension(pcxPath) + ".png");
//   ConvertPcxToPng(pcxPath, pngPath);
// }

// foreach (var aniPath in aniPaths)
// {
//   var aniData = File.ReadAllBytes(aniPath);
//   // check that first 4 bytes are "ANI "
//   if (aniData[0] != 0x41 || aniData[1] != 0x4E || aniData[2] != 0x49 || aniData[3] != 0x20) continue;
//   Console.WriteLine($"Processing {aniPath}");
//   var aniOutputDirectory = Path.Combine(Path.GetDirectoryName(aniPath), "output", Path.GetFileNameWithoutExtension(aniPath));
//   Directory.CreateDirectory(aniOutputDirectory);
//   var version = BitConverter.ToUInt16(aniData.Skip(0x10).Take(2).ToArray(), 0);
//   // FPS -> 2 bytes @ 0x14
//   var fps = BitConverter.ToUInt16(aniData.Skip(0x14).Take(2).ToArray(), 0);
//   // audio sample rate -> 4 bytes @ 0x16
//   var audioSampleRate = BitConverter.ToUInt32(aniData.Skip(0x16).Take(4).ToArray(), 0);
//   // width -> 2 bytes @ 0x1A if v1, @ 0x1e if v2
//   var wOffset = version == 1 ? 0x1A : 0x1e;
//   var width = BitConverter.ToUInt16(aniData.Skip(wOffset).Take(2).ToArray(), 0);
//   // height -> 2 bytes @ 0x1C if v1, @ 0x20 if v2
//   var hOffset = version == 1 ? 0x1C : 0x20;
//   var height = BitConverter.ToUInt16(aniData.Skip(hOffset).Take(2).ToArray(), 0);
//   // total frames -> 4 bytes @ 0x1E if v1, @ 0x22 if v2
//   var totalFramesOffset = version == 1 ? 0x1E : 0x22;
//   var totalFrames = BitConverter.ToUInt32(aniData.Skip(totalFramesOffset).Take(4).ToArray(), 0);
//   // audio chunk size ->4 bytes @ 0x26 if v1, @ 0x2A if v2
//   var audioChunkSizeOffset = version == 1 ? 0x26 : 0x2A;
//   var audioChunkSize = BitConverter.ToUInt32(aniData.Skip(audioChunkSizeOffset).Take(4).ToArray(), 0);
//   // offset data length -> 4 bytes @ 0x2A if v1, @ 0x2E if v2
//   var offsetDataLengthOffset = version == 1 ? 0x2A : 0x2E;
//   var offsetDataLength = BitConverter.ToUInt32(aniData.Skip(offsetDataLengthOffset).Take(4).ToArray(), 0);

//   // offset data -> offsetDataLength bytes @ 0x2E if v1, @ 0x32 if v2
//   var offsetData = aniData.Skip(version == 1 ? 0x2E : 0x32).Take((int)offsetDataLength).ToArray();

//   var offsets = new List<int>();

//   for (int i = 0; i < offsetData.Length; i += 4)
//   {
//     var offset = BitConverter.ToInt32(offsetData.Skip(i).Take(4).ToArray(), 0);
//     offsets.Add(offset);
//   }

//   var paletteOffset = (version == 1 ? 0x2E : 0x32) + offsetDataLength + 0x18;
//   var paletteLength = 4 * BitConverter.ToUInt16(aniData.Skip((int)paletteOffset - 2).Take(2).ToArray(), 0);
//   var paletteData = aniData.Skip((int)paletteOffset).Take(paletteLength).ToArray();
//   var palette = ColorHelper.ConvertBytesToARGB(paletteData, 1);
//   var bodyOffset = paletteOffset + paletteLength;
//   var bodyData = aniData.Skip((int)bodyOffset).ToArray();

//   var imageCount = 0;
//   var audioBytes = new List<byte>();
//   foreach (var offset in offsets)
//   {
//     var imageDataLength = BitConverter.ToUInt32(bodyData.Skip(offset).Take(4).ToArray(), 0);
//     var imageData = bodyData.Skip(offset + 4).Take((int)imageDataLength).ToArray();
//     var image = ImageFormatHelper.GenerateRle7Image(palette, imageData, width, height, true);
//     image.Save(Path.Combine(aniOutputDirectory, $"{imageCount++}.png"), ImageFormat.Png);
//     var audioDataLength = BitConverter.ToUInt32(bodyData.Skip((int)(offset + 4 + imageDataLength)).Take(4).ToArray(), 0);
//     var audioData = bodyData.Skip((int)(offset + 8 + imageDataLength)).Take((int)audioDataLength).ToArray();
//     audioBytes.AddRange(audioData);
//   }

//   var audioFile = Path.Combine(aniOutputDirectory, "audio.wav");
//   var audio = audioBytes.ToArray();
//   AudioHelper.ConvertPcmToWav(audio, audioFile, (int)audioSampleRate, 1, 8);

// }


// static void DecompressTitus(string fileIn, string fileOut)
// {
//   int unknown;
//   int decompressedSizeInteger;
//   long decompressedSize;
//   int huffmanTreeSize;
//   int node;
//   int bitPosition = 7;
//   int bit;
//   long i = 0;

//   byte byteIn =0;
//   byte byteOut;

//   using (BinaryReader reader = new BinaryReader(File.Open(fileIn, FileMode.Open)))
//   using (BinaryWriter writer = new BinaryWriter(File.Open(fileOut, FileMode.Create)))
//   {
//     // Read header
//     unknown = reader.ReadUInt16(); // always zero?
//     decompressedSizeInteger = reader.ReadUInt16();
//     decompressedSize = (uint)decompressedSizeInteger; // Convert to unsigned long
//     huffmanTreeSize = reader.ReadUInt16();

//     // Read Huffman tree
//     int[] huffmanTree = new int[huffmanTreeSize / 2];
//     for (int j = 0; j < huffmanTreeSize / 2; j++)
//     {
//       huffmanTree[j] = reader.ReadInt16(); // 16-bit signed integers
//     }

//     // Decompress data
//     node = 0;
//     while (i < decompressedSize)
//     {
//       if (bitPosition == 7)
//       {
//         byteIn = reader.ReadByte();
//       }

//       bit = (byteIn >> bitPosition) & 1;
//       bitPosition--;

//       if (bitPosition < 0)
//       {
//         bitPosition = 7;
//       }

//       node += bit;
//       if ((huffmanTree[node] & 0x8000) != 0)
//       {
//         // Leaf node
//         byteOut = (byte)(huffmanTree[node] & 0xFF);
//         writer.Write(byteOut);
//         i++;
//         node = 0; // Reset to the root of the tree
//       }
//       else
//       {
//         // Non-leaf node
//         node = huffmanTree[node] / 2;
//       }
//     }
//   }
// }

// static byte[][] DecodePlanarEGA(string inputFile, int Planes)
// {
//   using (BinaryReader reader = new BinaryReader(File.Open(inputFile, FileMode.Open)))
//   {
//     // Read the number of images
//     int imageCount = reader.ReadUInt16();

//     // Create an array to hold all the decoded images
//     byte[][] decodedImages = new byte[imageCount][];

//     // Iterate over each image in the file
//     for (int imageIndex = 0; imageIndex < imageCount; imageIndex++)
//     {
//       // Read the image dimensions (2 bytes for height, 2 bytes for width)
//       int height = reader.ReadUInt16();
//       int width = reader.ReadUInt16();

//       // Calculate the size for each image's planar data
//       int bytesPerPlane = (width * height) / 8; // Each plane contains bytes for 8 pixels per row

//       byte[,] pixels = new byte[height, width];  // Temporary 2D array for each image
//       byte[] colorIndices = new byte[width * height];  // The output byte array for this image

//       // Read and decode the image's planar data
//       for (int plane = 0; plane < Planes; plane++)
//       {
//         for (int y = 0; y < height; y++)
//         {
//           for (int x = 0; x < width; x += 8)
//           {
//             // Read the byte that contains the next 8 pixels for this row in this plane
//             int byteIndex = (int)reader.BaseStream.Position;
//             byte planeByte = reader.ReadByte();

//             // Process each bit in the byte (corresponding to 8 pixels horizontally)
//             for (int bit = 0; bit < 8; bit++)
//             {
//               // Extract the bit for this pixel
//               int bitValue = (planeByte >> (7 - bit)) & 1;

//               // Shift the bit into the correct position for this plane and add to the pixel value
//               pixels[y, x + bit] |= (byte)(bitValue << plane);
//             }
//           }
//         }
//       }

//       // Flatten the 2D pixel array into a 1D byte array of color indices
//       for (int y = 0; y < height; y++)
//       {
//         for (int x = 0; x < width; x++)
//         {
//           colorIndices[y * width + x] = pixels[y, x]; // Store each pixel's color index
//         }
//       }

//       // Store the decoded color indices for this image
//       decodedImages[imageIndex] = colorIndices;
//     }

//     return decodedImages;
//   }
// }

// var palFile = @"C:\bassPals\palette_60110_8370.bin";
// var palData = File.ReadAllBytes(palFile);
// var palette = ColorHelper.ConvertBytesToRGB(palData, 1);
// var FosterSprites = @"C:\Dev\Gaming\PC_DOS\Extractions\BASS\output\6168.bin";
// var FosterSpritesData = File.ReadAllBytes(FosterSprites);

// // loop through the data, and extract each sprite (32x56)
// var spriteWidth = 16;
// var spriteHeight = 8;
// var spriteCount = FosterSpritesData.Length / (spriteWidth * spriteHeight);

// var outputFolder = Path.Combine(Path.GetDirectoryName(FosterSprites), "output", "Tiles_6168");
// Directory.CreateDirectory(outputFolder);

// for (int i = 0; i < spriteCount; i++)
// {
//   var spriteData = FosterSpritesData.Skip(i * spriteWidth * spriteHeight).Take(spriteWidth * spriteHeight).ToArray();
//   var sprite = ImageFormatHelper.GenerateClutImage(palette, spriteData, spriteWidth, spriteHeight);
//   sprite.Save(Path.Combine(outputFolder, $"{i}.png"), ImageFormat.Png);
// }

// var pakFilePath = @"C:\Dev\Gaming\PC_Windows\Games\Mathinv\PAKS\LEVELS";
// var datFiles = Directory.GetFiles(pakFilePath, "*.pak", SearchOption.AllDirectories);

// foreach (var datFile in datFiles)
// {
//   var outputFolder = Path.Combine(Path.GetDirectoryName(datFile), "output");
//   Directory.CreateDirectory(outputFolder);

//   var datData = File.ReadAllBytes(datFile);

//   var fileCount = BitConverter.ToUInt32(datData.Take(4).ToArray(), 0);

//   var fileOffsetData = datData.Skip(4).Take((int)fileCount * 0x44).ToArray();
//   var fileOffset = new List<(string, uint)>();

//   for (int i = 0; i < fileOffsetData.Length; i += 0x44)
//   {
//     // File name is 0x40 bytes, padded with 0x00, offset to file is the remaining 4 bytes
//     var fileName = Encoding.ASCII.GetString(fileOffsetData.Skip(i).Take(0x40).ToArray()).TrimEnd('\0');
//     // if there are any null bytes remaining, remove them and the following bytes
//     if (fileName.Contains('\0'))
//     {
//       fileName = fileName.Substring(0, fileName.IndexOf('\0'));
//     }
//     var offset = BitConverter.ToUInt32(fileOffsetData.Skip(i + 0x40).Take(4).ToArray(), 0);
//     fileOffset.Add((fileName, offset));
//   }

//   for (int i = 0; i < fileCount; i++)
//   {
//     var (fileName, offset) = fileOffset[i];
//     var nextOffset = (i == fileCount - 1) ? datData.Length : (int)fileOffset[i + 1].Item2;
//     var fileData = datData.Skip((int)offset).Take((int)(nextOffset - offset)).ToArray();
//     // if filename contains folder structure, create the folder
//     var filePath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(datFile), fileName);
//     Directory.CreateDirectory(Path.GetDirectoryName(filePath));
//     // if file exists, add a number to the end

//     File.WriteAllBytes(filePath, fileData);
//   }
// }

// var pcxFilePath = @"C:\Dev\Gaming\PC_Windows\Games\Mathinv\PAKS\LEVELS\output";
// var pcxFiles = Directory.GetFiles(pcxFilePath, "*.pc*", SearchOption.AllDirectories);
// var paletteFile = @"C:\Dev\Gaming\PC_Windows\Games\Mathinv\PAKS\output\Palette.act";
// var paletteData = File.ReadAllBytes(paletteFile);
// var palette = ConvertBytesToRGBReverse(paletteData);

// foreach (var pcx in pcxFiles)
// {
//   var outputFolder = Path.Combine(Path.GetDirectoryName(pcx), "output");
//   Directory.CreateDirectory(outputFolder);
//   var pcxData = File.ReadAllBytes(pcx);
//   var width = BitConverter.ToUInt32(pcxData.Take(4).ToArray(), 0);
//   var height = BitConverter.ToUInt32(pcxData.Skip(4).Take(4).ToArray(), 0);
//   if (width == 0 || height == 0) continue;
//   var imageData = pcxData.Skip(8).Take((int)(width * height)).ToArray();
//   var image = ImageFormatHelper.GenerateClutImage(palette, imageData, (int)width, (int)height, true);
//   image.Save(Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(pcx) + ".png"), ImageFormat.Png);
// }

// static List<Color> ConvertBytesToRGBReverse(byte[] bytes)
// {
//   List<Color> colors = new List<Color>();

//   // Start at the last RGB triplet and move backwards
//   for (int i = bytes.Length - 3; i >= 0; i -= 3)
//   {
//     byte red = bytes[i];
//     byte green = bytes[i + 1];
//     byte blue = bytes[i + 2];

//     Color color = Color.FromArgb(red, green, blue);
//     colors.Add(color);
//   }

//   return colors;
// }

// var planetFolder = @"C:\Dev\Gaming\CD-i\Games\Laser Lords";
// var outputFolder = @"C:\Dev\Gaming\CD-i\Extractions\Laser Lords";

// var spaceFile = Path.Combine(planetFolder, "space.rtf");
// var spaceOutputFolder = Path.Combine(outputFolder, "Space");
// Directory.CreateDirectory(spaceOutputFolder);

// LaserLordsHelper.ExtractCockpitAnimation(spaceFile, spaceOutputFolder);

// var planetFiles = new List<string>(){
//   "argos.rtf",
//   "luxor.rtf",
//   "fornax.rtf",
//   "hive.rtf",
//   "tekton.rtf",
//   "woo.rtf",
//   "ravanna.rtf",
// };

// foreach (var planet in planetFiles)
// { 
//   var planetName = Path.GetFileNameWithoutExtension(planet);
//   var planetPath = Path.Combine(planetFolder, planet);
//   var outputPlanetFolder = Path.Combine(outputFolder, planetName);
//   Directory.CreateDirectory(outputPlanetFolder);
//   LaserLordsHelper.ExtractPlanet(planetPath, outputPlanetFolder);
// }

// ------------------------------------------------------------------------------------------------------------


// var mainPalFile = @"C:\Dev\Gaming\PC_DOS\Games\FABLE\INSTALL\Extracted\PALETTE.RAW";
// var mainPalData = File.ReadAllBytes(mainPalFile);
// var mainPalette = ColorHelper.ConvertBytesToRGB(mainPalData, 4);

// var closeupFile = @"C:\Dev\Gaming\PC_DOS\Games\FABLE\INSTALL\Extracted\CLOSEUP.SPR";
// var closeupData = File.ReadAllBytes(closeupFile);
// var palOffsetsStart = BitConverter.ToUInt32(closeupData.Skip(0x10).Take(4).ToArray(), 0);
// var palCount = BitConverter.ToUInt16(closeupData.Skip(0x14).Take(2).ToArray(), 0);

// Console.WriteLine($"Palette Offsets Start: {palOffsetsStart:X8}");
// Console.WriteLine($"Palette Count: {palCount}");

// var palOffsets = new List<uint>();
// for (int i = 0; i < palCount; i++)
// {
//   var offset = BitConverter.ToUInt32(closeupData.Skip((int)palOffsetsStart + (i * 4)).Take(4).ToArray(), 0);
//   palOffsets.Add(offset);
// }

// var outputFolder = Path.Combine(Path.GetDirectoryName(closeupFile), "output");
// Directory.CreateDirectory(outputFolder);
// var palettesFolder = Path.Combine(outputFolder, "Palettes");
// Directory.CreateDirectory(palettesFolder);

// var previousPalette = new byte[480];
// for (int i = 0; i < palOffsets.Count; i++)
// {
//   var pal = new byte[768];
//   // first 288 bytes we take from the main palette
//   Array.Copy(mainPalData, 0, pal, 0, 288);
//   var offset = palOffsets[i];
//   if (offset == 0) {
//     // copy the previous palette to fill the remaining 480 bytes
//     Array.Copy(previousPalette, 0, pal, 288, 480);
//     File.WriteAllBytes(Path.Combine(palettesFolder, $"{i}_pal.bin"), pal);
//     continue;
//   }
//   var palData = closeupData.Skip((int)offset).Take(0x1e0).ToArray();
//   Array.Copy(palData, 0, pal, 288, 480);
//   File.WriteAllBytes(Path.Combine(palettesFolder, $"{i}_pal.bin"), pal);
//   previousPalette = palData;
// }

// var file = @"C:\Dev\Gaming\PC_DOS\Extractions\MI2\monkey2.001";
// var fileBytes = File.ReadAllBytes(file);

// var unxoredBytes = fileBytes.Select(x => (byte)(x ^ 0x69)).ToArray();
// var unxoutputFolder = Path.Combine(Path.GetDirectoryName(file), "output");
// Directory.CreateDirectory(unxoutputFolder);

// File.WriteAllBytes(Path.Combine(unxoutputFolder, "monkey2.001.bin"), unxoredBytes);

//var atlantisIndexFile = @"C:\Dev\Gaming\PC_DOS\Extractions\MI2\output\monkey2.000.bin";

//var scummIndex = new ScummIndexFile(atlantisIndexFile);

// foreach (var room in scummIndex.Rooms)
// {
//   var roomCostumes = scummIndex.Costumes.Where(x => x.RoomNumber == room.RoomNumber).ToList();
// }

// var atlantisDataFile = @"C:\Dev\Gaming\PC_DOS\Extractions\MI2\output\monkey2.001.bin";
// var scummData = new ScummDataFile(scummIndex,atlantisDataFile);

// Console.WriteLine($"Data File has {scummData.Table.NumOfRooms} rooms");

// var outputFolder = Path.Combine(Path.GetDirectoryName(atlantisDataFile), "Room_backgrounds");
// Directory.CreateDirectory(outputFolder);
// outputFolder = Path.Combine(Path.GetDirectoryName(atlantisDataFile), "Room_backgrounds", "PALs");
// Directory.CreateDirectory(outputFolder);
// outputFolder = Path.Combine(Path.GetDirectoryName(atlantisDataFile), "Room_Costumes");
// Directory.CreateDirectory(outputFolder);
// outputFolder = Path.Combine(Path.GetDirectoryName(atlantisDataFile), "Room_Objects");
// Directory.CreateDirectory(outputFolder);

// // var rff = scummData.ParseRoomData(1);

// for (int i = 0; i < scummData.Table.NumOfRooms; i++)
// {
//   var room = scummIndex.Rooms[i];
//   var roomFile = Path.Combine(outputFolder, $"{room.RoomNumber}_{room.RoomName}_RoomFile.bin");
//   //scummData.DumpRoomData(room.RoomNumber, roomFile);
//   var rf = scummData.ParseRoomData(room.RoomNumber);
// }

// static void ExtractHqr(string hqrFile, string outputFolder)
// {
//   var hqrData = File.ReadAllBytes(hqrFile);
//   var offsets = new List<uint>();
//   var headerSize = BitConverter.ToUInt32(hqrData.Take(4).ToArray(), 0);
//   using var ms = new BinaryReader(new MemoryStream(hqrData));
//   while (ms.BaseStream.Position < headerSize)
//   {
//       var offset = ms.ReadUInt32();
//       if (offset == 0) continue;
//       offsets.Add(offset);
//   }
//   // iterate through offsets and get data, last offset is the file size
//   for (var i = 0; i < offsets.Count - 1; i++)
//   {
//       var offset = offsets[i];
//       var length = offsets[i + 1] - offset;
//       var realSize = ms.ReadUInt32();
//       var compSize = ms.ReadUInt32();
//       var mode = ms.ReadUInt16();
//       var data = ms.ReadBytes((int)length-10);
//       if (mode == 0)
//       {
//           File.WriteAllBytes(Path.Combine(outputFolder, $"{i}.bin"), data);
//       } else {
//           var decompressed = DecompressHqr(data, (int)realSize, mode);
//           File.WriteAllBytes(Path.Combine(outputFolder, $"{i}_dc.bin"), decompressed);
//       }
//   }
// }

// static byte[] DecompressHqr(byte[] dat, int decompressedSize, int mode) 
// {
//   var output = new List<byte>();
//   using (var ms = new BinaryReader(new MemoryStream(dat)))
//   do {
//     var b = ms.ReadByte();
//     for (int i = 0; i < 8; i++)
//     {
//       if ((b & (1 << i)) == 0)
//       {
//         var offset = ms.ReadUInt16();
//         var length = (offset & 0x1F) + mode + 1;
//         var lookbackOffset = output.Count - (offset >> 4) - 1;
//         for (var j = 0; j < length; j++)
//         {
//           output.Add(output[lookbackOffset++]);
//         }
//       } else {
//         output.Add(ms.ReadByte());
//       }
//       if (output.Count >= decompressedSize) return output.ToArray(); 
//     }
//   } while (output.Count < decompressedSize);
//   return output.ToArray();
// }

//https://discord.com/channels/581224060529148060/711242520415174666/1231714467461464164

// var indexFile = @"C:\Dev\Gaming\PC_DOS\Games\STJRITES\DATA.DIR";
// var dataFile = @"C:\Dev\Gaming\PC_DOS\Games\STJRITES\DATA.001";
// var runFile = @"C:\Dev\Gaming\PC_DOS\Games\STJRITES\DATA.RUN";
// StarTrek.ExtractST(indexFile, dataFile, runFile);

// var bmpDir = @"C:\Dev\Gaming\PC_DOS\Games\STJRITES\output\data";
// var bmpFiles = Directory.GetFiles(bmpDir, "*.bmp", SearchOption.AllDirectories);

// var bridgePal = @"C:\Dev\Gaming\PC_DOS\Games\TREKCD\TREKCD\ENGLISH\output\BRIDGE.PAL";
// var bridgePalData = File.ReadAllBytes(bridgePal);
// var bridgePalette = ColorHelper.ConvertBytesToRGB(bridgePalData, 4);

// var outputFolder = Path.Combine(bmpDir, "bmp_output");
// Directory.CreateDirectory(outputFolder);

// foreach (var bFile in bmpFiles)
// {
//   var data = File.ReadAllBytes(bFile);
//   // check if first 4 bytes == "FORM"
//   if (Encoding.ASCII.GetString(data.Take(4).ToArray()) == "FORM") continue;
//   var width = BitConverter.ToUInt16(data.Skip(0x4).Take(2).ToArray(), 0);
//   var height = BitConverter.ToUInt16(data.Skip(0x6).Take(2).ToArray(), 0);
//   if (width == 0 || height == 0 || width >= 640 || height >= 400) continue;
//   var imageData = data.Skip(0x8).ToArray();
//   var image = ImageFormatHelper.GenerateClutImage(bridgePalette, imageData, width, height, true);
//   image.Save(Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(bFile) + ".png"), ImageFormat.Png);
// }


// var inputDir = @"C:\Dev\Gaming\PC_DOS\Games\Nippon-Safes-Inc_DOS_EN\nippon-safes-inc";
// NipponSafes.ExtractBackgroundImages(inputDir);
// NipponSafes.ExtractSpriteFiles(inputDir);


// var rlbHeaderBytes = new byte[] { 0x54, 0x4D, 0x49, 0x2D };

// var ringRLB = @"C:\Dev\Gaming\PC_DOS\Blue-Force_DOS_EN\blue.rlb";

// using var reader = new BinaryReader(File.OpenRead(ringRLB));
// // check for RLB header
// var header = reader.ReadBytes(4);
// if (!header.SequenceEqual(rlbHeaderBytes))
// {
//   Console.WriteLine("Not an RLB file");
//   return;
// }
// reader.ReadByte(); // skip 1 byte
// reader.ReadByte();

// var indexEntry = new RLBResourceEntry();
// indexEntry.Id = reader.ReadUInt16();

// var size = reader.ReadUInt16();
// var uncompressedSize = reader.ReadUInt16();
// var sizeHi = reader.ReadByte();
// var Type = (byte)(reader.ReadByte() >> 5);

// indexEntry.Offset = reader.ReadUInt32();
// indexEntry.Size = (uint)(((sizeHi & 0XF) << 16) | size);
// indexEntry.UncompressedSize = (uint)(((sizeHi & 0xF0) << 12) | uncompressedSize);
// indexEntry.IsCompressed = Type != 0;

// reader.BaseStream.Seek(indexEntry.Offset, SeekOrigin.Begin);
// indexEntry.Data = reader.ReadBytes((int)indexEntry.Size);


// var outputFolder = Path.Combine(Path.GetDirectoryName(ringRLB), "rlb_output");
// Directory.CreateDirectory(outputFolder);

// var sections = new List<SectionEntry>();
// ushort resNum, configId, fileOffset;

// using var iReader = new BinaryReader(new MemoryStream(indexEntry.Data));

// while ((resNum = iReader.ReadUInt16()) != 0xFFFF)
// {
//   configId = iReader.ReadUInt16();
//   fileOffset = iReader.ReadUInt16();

//   var se = new SectionEntry
//   {
//     ResNum = resNum,
//     ResType= (ResourceType)(configId & 0x1F),
//     Offset = (uint)((((configId >> 5) & 0x7ff) << 16) | fileOffset)
//   };
//   sections.Add(se);
// }

// foreach (var section in sections)
// {
//   var entry = new RLBResourceEntry();
//   reader.BaseStream.Seek(section.Offset, SeekOrigin.Begin); 
//   header = reader.ReadBytes(4);
//   if (!header.SequenceEqual(rlbHeaderBytes))
//   {
//     Console.WriteLine("Not an RLB file");
//     return;
//   }
//   reader.ReadByte(); // skip 1 byte
//   var count = reader.ReadByte();

//   for (int i = 0; i < count; i++)
//   {
//     reader.BaseStream.Seek(section.Offset + 6 + (i * 12), SeekOrigin.Begin);
//     entry.Id = reader.ReadUInt16();
//     size = reader.ReadUInt16();
//     uncompressedSize = reader.ReadUInt16();
//     sizeHi = reader.ReadByte();
//     Type = (byte)(reader.ReadByte() >> 5);

//     entry.Offset = reader.ReadUInt32();
//     entry.Size = (uint)(((sizeHi & 0XF) << 16) | size);
//     entry.UncompressedSize = (uint)(((sizeHi & 0xF0) << 12) | uncompressedSize);
//     entry.IsCompressed = Type != 0;

//     reader.BaseStream.Seek(entry.Offset+section.Offset, SeekOrigin.Begin);
//     entry.Data = reader.ReadBytes((int)entry.Size);

//     var output = Path.Combine(outputFolder, $"{section.ResNum}_{section.ResType}_{entry.Id}_{(entry.IsCompressed ? "compressed" : "uncompressed")}.bin");
//     File.WriteAllBytes(output, entry.Data);
//   }
// }

// class RLBResourceEntry
// {
//   public byte[]? Data { get; set; }
//   public ushort Id { get; set; }
//   public uint Size { get; set; }
//   public uint UncompressedSize { get; set; }
//   public uint Offset { get; set; }
//   public bool IsCompressed { get; set; }
// }

// class SectionEntry
// {
//   public uint Offset { get; set; }
//   public ushort ResNum { get; set; }
//   public ResourceType ResType { get; set; }
// }

// enum ResourceType
// {
//   RES_LIBRARY, RES_STRIP, RES_IMAGE, RES_PALETTE, RES_VISAGE, RES_SOUND, RES_MESSAGE,
//   RES_FONT, RES_POINTER, RES_BANK, RES_SND_DRIVER, RES_PRIORITY, RES_CONTROL, RES_WALKRGNS,
//   RES_BITMAP, RES_SAVE, RES_SEQUENCE,
//   // Return to Ringworld specific resource types
//   RT17, RT18, RT19, RT20, RT21, RT22, RT23, RT24, RT25, RT26, RT27, RT28, RT29, RT30, RT31
// };


// var lkLevelDir = @"C:\Dev\Gaming\PC_DOS\Extractions\LionKing";
// var lkLevelFiles = Directory.GetFiles(lkLevelDir, "*.MAP");


// var lkSpriteFiles = Directory.GetFiles(lkLevelDir, "*.MCH");

// foreach (var lkLevelFile in lkLevelFiles)
// {

//   var lkLevelData = File.ReadAllBytes(lkLevelFile);
//   var palData = lkLevelData.Skip(0xA8).Take(0x300).ToArray();
//   var palette = ColorHelper.ConvertBytesToRGB(palData, 4);
//   foreach (var lkSpriteFile in lkSpriteFiles)
//   {
//     using var reader = new BinaryReader(File.OpenRead(lkSpriteFile));
//     var frameCount = reader.ReadUInt16();
//     var frames = new List<lkSpriteFrame>();
//     for (int i = 0; i < frameCount; i++)
//     {
//       var frame = new lkSpriteFrame
//       {
//         width = reader.ReadUInt16(),
//         height = reader.ReadUInt16(),
//         offset = reader.ReadUInt32()
//       };
//       frames.Add(frame);
//     }

//     var outputFolder = Path.Combine(Path.GetDirectoryName(lkSpriteFile), $"output_{Path.GetFileNameWithoutExtension(lkLevelFile)}", Path.GetFileNameWithoutExtension(lkSpriteFile));
//     Directory.CreateDirectory(outputFolder);

//     for (int i = 0; i < frames.Count; i++)
//     {
//       var frame = frames[i];
//       reader.BaseStream.Seek(frame.offset, SeekOrigin.Begin);
//       var imageData = new byte[frame.width * frame.height];
//       var pixelsPopulated = 0;
//       // Frame pixel data is stored in chunks representing horizontal runs of pixels. Each chunk has this format:
//       // byte 0: x position to start drawing the run
//       // byte 1: y position to start drawing the run
//       // byte 2: length of the run
//       // bytes [length]: pixel data
//       var x = reader.ReadByte();
//       while (x != 0xFF)
//       {

//         var y = reader.ReadByte();
//         var length = reader.ReadByte();
//         var data = reader.ReadBytes(length);
//         for (int j = 0; j < length; j++)
//         {
//           imageData[(y * frame.width) + x + j] = data[j];
//         }
//         x = reader.ReadByte();
//       }
//       //File.WriteAllBytes(Path.Combine(outputFolder, $"{i}.bin"), imageData);
//       var image = ImageFormatHelper.GenerateClutImage(palette, imageData, frame.width, frame.height, true);
//       image.Save(Path.Combine(outputFolder, $"{i}.png"), ImageFormat.Png);
//     }
//   }
// }

// class lkSpriteFrame {
//   public ushort width { get; set; }
//   public ushort height { get; set; }
//   public uint offset { get; set; }
// }


// var digIndexFile = @"C:\Dev\Gaming\PC_DOS\Games\DIG1\DIG\DIG.LA0";
// var outputFolder = @"C:\Dev\Gaming\PC_DOS\Extractions\DIG\output";
// var rOutputFolder = @"C:\Dev\Gaming\PC_DOS\Extractions\DIG\output_bin";

// var scummIndex = new ScummIndexFile(digIndexFile);

// foreach (var room in scummIndex.Rooms)
// {
//   var roomCostumes = scummIndex.Costumes.Where(x => x.RoomNumber == room.RoomNumber).ToList();
// }

// var digDataFile = @"C:\Dev\Gaming\PC_DOS\Games\DIG1\DIG\DIG.LA1";
// var scummData = new ScummDataFile(scummIndex,digDataFile);

// Console.WriteLine($"Data File has {scummData.Table.NumOfRooms} rooms");
// for (int i = 0; i < scummData.Table.NumOfRooms; i++)
// {
//   var room = scummIndex.Rooms[i];
//   var roomFile = Path.Combine(rOutputFolder, $"{room.RoomNumber}_{room.RoomName}_RoomFile.bin");
//   scummData.DumpRoomData(room.RoomNumber, roomFile);
//   var rf = scummData.ParseRoomData(room.RoomNumber);
// }


// var levelFiles = Directory.GetFiles(@"C:\Dev\Gaming\PC_DOS\Extractions\LionKing\","*.MAP");

// foreach (var levelFile in levelFiles)
// {
//   var levelData = File.ReadAllBytes(levelFile);
//   var levelPaletteData = levelData.Skip(0xA8).Take(0x300).ToArray();
//   var levelPalette = ColorHelper.ConvertBytesToRGB(levelPaletteData, 4);
//   var map1WidthInTiles = BitConverter.ToUInt16(levelData.Skip(0x8).Take(2).ToArray(), 0);
//   var map1HeightInTiles = BitConverter.ToUInt16(levelData.Skip(0xC).Take(2).ToArray(), 0);
//   var offset2 = BitConverter.ToUInt32(levelData.Skip(0x10).Take(4).ToArray(), 0);
//   var map2WidthInTiles = BitConverter.ToUInt16(levelData.Skip(0x14).Take(2).ToArray(), 0);
//   var map2HeightInTiles = BitConverter.ToUInt16(levelData.Skip(0x18).Take(2).ToArray(), 0);
//   var offset3 = BitConverter.ToUInt32(levelData.Skip(0x1c).Take(4).ToArray(), 0);

//   var tileData = levelData.Skip(0x3A8).Take((int)(offset2-0x3a8)).ToArray();
//   var tileWidth = 8;
//   var tileHeight = 8;

//   var tileCount = tileData.Length / (tileWidth * tileHeight);
//   var tileOutputDir = Path.Combine(Path.GetDirectoryName(levelFile), $"{Path.GetFileNameWithoutExtension(levelFile)}_tiles");
//   Directory.CreateDirectory(tileOutputDir);
//   var outputDir = Path.Combine(Path.GetDirectoryName(levelFile), $"csv_maps");
//   Directory.CreateDirectory(outputDir);

//   // for (int i = 0; i < tileCount; i++)
//   // {
//   //   var tile = tileData.Skip(i * (tileWidth * tileHeight)).Take(tileWidth * tileHeight).ToArray();
//   //   var image = ImageFormatHelper.GenerateClutImage(levelPalette, tile, tileWidth, tileHeight, true);
//   //   image.Save(Path.Combine(tileOutputDir, $"{i}.png"), ImageFormat.Png);
//   //   image.RotateFlip(RotateFlipType.RotateNoneFlipY);
//   //   image.Save(Path.Combine(tileOutputDir, $"{i + tileCount}.png"), ImageFormat.Png);
//   //   image.RotateFlip(RotateFlipType.RotateNoneFlipXY);
//   //   image.Save(Path.Combine(tileOutputDir, $"{i + (tileCount * 2)}.png"), ImageFormat.Png);
//   //   image.RotateFlip(RotateFlipType.RotateNoneFlipY);
//   //   image.Save(Path.Combine(tileOutputDir, $"{i + (tileCount * 3)}.png"), ImageFormat.Png);
//   // }

//   var mapData1 = levelData.Skip((int)offset2).Take((int)(offset3 - offset2)).ToArray();
//   var csv = string.Empty;
//   // for (int i = 0; i < mapData1.Length; i += 2)
//   // {
//   //   var value = BitConverter.ToUInt16(mapData1, i);
//   //   // value <<= 2;
//   //   // value += (byte)((controlByte / 64) + 1);
//   //   value = value switch
//   //   {
//   //     > 0xbfff => (ushort)((value & 0xfff) + (tileCount * 3)),
//   //     > 0x7fff => (ushort)((value & 0xfff) + (tileCount * 2)),
//   //     > 0x3fff => (ushort)((value & 0xfff) + tileCount),
//   //     _ => (ushort)(value & 0xfff)
//   //   };
//   //   csv += $"{value},";
//   //   if ((i + 2) % (map1WidthInTiles*2) == 0 && i < mapData1.Length - 2)
//   //   {
//   //     csv += "\r\n";
//   //   }
//   // }
//   // // trim the last comma
//   // csv = csv.TrimEnd(',');
//   // File.WriteAllText(Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(levelFile)}_fore_{map1WidthInTiles}_{map1HeightInTiles}.csv"), csv);

//   if (offset3 == 0) continue;
//   var mapData2 = levelData.Skip((int)offset3).ToArray();
//   csv = string.Empty;
//   for (int i = 0; i < mapData2.Length; i += 2)
//   {
//     var value = BitConverter.ToUInt16(mapData2, i);
//     // value <<= 2;
//     // value += (byte)((controlByte / 64) + 1);
//     value = value switch
//     {
//       > 0xbfff => (ushort)(value & 0xfff),
//       > 0x7fff => (ushort)((value & 0xfff) + (tileCount * 2)),
//       > 0x3fff => (ushort)((value & 0xfff) + tileCount),
//       _ => value
//     };
//     csv += $"{value},";
//     if ((i + 2) % (map2WidthInTiles*2) == 0 && i < mapData2.Length - 2)
//     {
//       csv += "\r\n";
//     }
//   }
//   // trim the last comma
//   csv = csv.TrimEnd(',');
//   File.WriteAllText(Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(levelFile)}_back_{map2WidthInTiles}_{map2HeightInTiles}.csv"), csv);

// }



// var dwDir = @"C:\Dev\Gaming\PC_DOS\Games\Discworld_DOS_EN_ISO-Version---Directors-Cut\Discworld_Directors_Cut_ISO\DISCWLD";

// Discworld.ExtractAll(dwDir);



// var inputFolder = @"C:\Dev\Gaming\PC_DOS\Extractions\LionKing\output\output_LEVEL7";

// // get a list of all files in the input folder
// var files = Directory.GetFiles(inputFolder, "*.png", SearchOption.AllDirectories);

// // prefix each file with the folder name
// foreach (var file in files)
// {
//   var fileName = Path.GetFileName(file);
//   var folderName = Path.GetFileName(Path.GetDirectoryName(file));
//   var newFileName = Path.Combine(Path.GetDirectoryName(file), $"{folderName}_{fileName}");
//   File.Copy(file, newFileName);
//   File.Delete(file);
// }

// string MadsCMagic = @"MADSCONCAT 1.0";

// var resourceDir = @"C:\Dev\Gaming\PC_DOS\Games\OUAF";
// var hagFiles = Directory.GetFiles(resourceDir, "*.HAG");

// foreach (var hag in hagFiles)
// {
//     var sb = new StringBuilder();
//     sb.AppendLine($"Processing {hag}");
//     sb.AppendLine();
//     using var reader = new BinaryReader(File.OpenRead(hag));
//     var header = Encoding.ASCII.GetString(reader.ReadBytes(14));
//     if (header != MadsCMagic)
//     {
//         Console.WriteLine("Not a MADSCONCAT file");
//         continue;
//     }
//     reader.ReadBytes(2); // skip bytes
//     var count = reader.ReadUInt16();
//     var subfiles = new List<MadsConcatSubFile>();
//     for (int i = 0; i < count; i++)
//     {
//         var subfile = new MadsConcatSubFile
//         {
//             Offset = reader.ReadInt32(),
//             Length = reader.ReadInt32()
//         };
//         var filenameBytes = reader.ReadBytes(14);
//         // find the first null byte
//         var nullIndex = Array.IndexOf(filenameBytes, (byte)0);
//         var filename = (nullIndex == -1) ? Encoding.ASCII.GetString(filenameBytes)  : Encoding.ASCII.GetString(filenameBytes, 0, nullIndex);
//         subfile.Filename = filename;
//         subfiles.Add(subfile);
//         sb.AppendLine($"Subfile {i}: {filename} Offset: {subfile.Offset} Length: {subfile.Length}");
//     }
//     var outputDir = Path.Combine(resourceDir, "EXTRACTED", Path.GetFileNameWithoutExtension(hag));
//     Directory.CreateDirectory(outputDir);

//     File.WriteAllText(Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(hag)}_listing.txt"), sb.ToString());
//     foreach (var subfile in subfiles)
//     {
//         reader.BaseStream.Seek(subfile.Offset, SeekOrigin.Begin);
//         var data = reader.ReadBytes(subfile.Length);
//         File.WriteAllBytes(Path.Combine(outputDir, subfile.Filename), data);
//     }
// }

// // string madsPMagic = @"MADSPACK 2.0";

// var testDir1 = @"C:\Dev\Gaming\PC_DOS\Games\OUAF\EXTRACTED";

// // start a stopwatch
// var stopwatch = new Stopwatch();
// stopwatch.Start();

// ImageFormats.ExtractAllSprites(testDir1);
// ImageFormats.ExtractAllBackgrounds(testDir1);

// // stop the stopwatch
// stopwatch.Stop();
// Console.WriteLine($"Elapsed time: {stopwatch.Elapsed}");


// class MadsConcatSubFile
// {
// 	public int Offset { get; set; }
// 	public int Length { get; set; }
// 	public string Filename { get; set; }
// }

// class MadsPackSubFile
// {
// 	public short Flags { get; set; }
// 	public int Size { get; set; }
// 	public int CompressedSize { get; set; }
// }

// var testLabFile = @"C:\Dev\Gaming\PC_DOS\Games\GRIM_DISC_B\GRIMDATA\DATA004.LAB";
// var testLabData = File.ReadAllBytes(testLabFile);
// var testLab = new LabFile(testLabFile);
// var outputDir = @"C:\Dev\Gaming\PC_DOS\Extractions\GrimFandango\LABs\DATA004";
// Directory.CreateDirectory(outputDir);

// foreach (var entry in testLab.Entries)
// {
//     var data = testLabData.Skip(entry.Offset).Take(entry.Length).ToArray();
//     File.WriteAllBytes(Path.Combine(outputDir, $"{entry.Name}"), data);
// }

// var testBMFile = @"C:\Dev\Gaming\PC_DOS\Extractions\GrimFandango\LABs\DATA002\cb_boat.bm";

// var testBM = new BMFile(testBMFile);
// var outputDir = Path.Combine(Path.GetDirectoryName(testBMFile), "BM_Out");
// Directory.CreateDirectory(outputDir);
// var image = ImageFormatHelper.ConvertRGB565(testBM.ImageArrays[0], testBM.Width, testBM.Height);
// var imageOutputFile = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(testBMFile)}.png");
// var imageOutputStream = new FileStream(imageOutputFile, FileMode.Create);
// var imageEncoder = new PngEncoder();

// image.Save(imageOutputStream, imageEncoder);

// var mainDir = @"C:\Dev\Gaming\PC_DOS\Extractions\GrimFandango\LABs";
// var testBMFiles = Directory.GetFiles(mainDir, "*.bm", SearchOption.AllDirectories);

// foreach (var testBMFile in testBMFiles)
// {
//     try
//     {
//         var testBM = new BMFile(testBMFile);
//         var outputDir = Path.Combine(mainDir, "BM_Out");
//         Directory.CreateDirectory(outputDir);
//         for (int i = 0; i < testBM.ImageCount; i++)
//         {

//             var image = ImageFormatHelper.ConvertRGB565(testBM.ImageArrays[i], testBM.Width, testBM.Height);
//             var imageOutputFile = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(testBMFile)}_{i}.png");
//             var imageOutputStream = new FileStream(imageOutputFile, FileMode.Create);
//             var imageEncoder = new PngEncoder();

//             image.Save(imageOutputStream, imageEncoder);
//         }
//     }
//     catch (Exception ex)
//     {
//         Console.WriteLine($"Error processing {testBMFile}: {ex.Message}");
//         continue;
//     }

// }

// var testModel = @"C:\Dev\Gaming\PC_DOS\Extractions\GrimFandango\LABs\DATA000\mannysuit.3do";

// var allVerts = new List<List<Vector3>>();
// var allTextureVerts = new List<List<Vector2>>();
// var allFaceVerts = new List<List<int>>();

// var mReader = new BinaryReader(File.OpenRead(testModel));

// if (Encoding.ASCII.GetString(mReader.ReadBytes(4)) != "LDOM")
// {
//     Console.WriteLine("Not a 3DO file");
//     return;
// }

// var matCount = mReader.ReadUInt32();
// var matNames = new List<string>();

// for (int i = 0; i < matCount; i++)
// {
//     var name = Encoding.ASCII.GetString(mReader.ReadBytes(32)).TrimEnd('\0');
//     //Console.WriteLine(name);
//     matNames.Add(name);
// }
// mReader.ReadBytes(36); // skip name (32) and Unk (4) bytes

// var geoSetCount = mReader.ReadUInt32();
// uint numMeshes = 0;
// for (int i = 0; i < geoSetCount; i++)
// {
//     numMeshes = mReader.ReadUInt32();
//     for (int j =0; j < numMeshes; j++)
//     {
//         var meshName = Encoding.ASCII.GetString(mReader.ReadBytes(32)).TrimEnd('\0');
//         Console.WriteLine(meshName);
//         mReader.ReadInt32(); // skip 4 bytes
//         var geoMode = mReader.ReadUInt32();
//         var lightMode = mReader.ReadUInt32();
//         var textureMode = mReader.ReadUInt32();
//         var numVerts = mReader.ReadUInt32();
//         var numTextureVerts = mReader.ReadUInt32();
//         var numFaces = mReader.ReadUInt32();
//         Console.WriteLine($"GeoMode: {geoMode} LightMode: {lightMode} TextureMode: {textureMode} NumVerts: {numVerts} NumTextureVerts: {numTextureVerts} NumFaces: {numFaces}");
//         var verts = new List<Vector3>();
//         var verticesI = new List<float>();
//         var vertNormals = new List<Vector3>();
//         var textureVerts = new List<Vector2>();
//         var meshFaces = new List<GrimMeshFace>();
//         var materialIds = new List<int>();
//         for (int k = 0; k < numVerts; k++)
//         {
//             var x = mReader.ReadSingle();
//             var y = mReader.ReadSingle();
//             var z = mReader.ReadSingle();
//             verts.Add(new Vector3(x, y, z));
//             //Console.WriteLine($"Vert {k}: {x} {y} {z}");
//         }
//         allVerts.Add(verts);
//         for (int k = 0; k < numTextureVerts; k++)
//         {
//             var u = mReader.ReadSingle();
//             var v = mReader.ReadSingle();
//             textureVerts.Add(new Vector2(u, v));
//            // Console.WriteLine($"TextureVert {k}: {u} {v}");
//         }
//         allTextureVerts.Add(textureVerts);
//         for (int k = 0; k < numVerts; k++)
//         {
//             verticesI.Add(mReader.ReadSingle());
//             //Console.WriteLine($"VertI {k}: {verticesI[k]}");
//         }
//         mReader.ReadBytes((int)(4 * numVerts)); // skip 4 bytes per vert
//         var meshFaceVerts = new List<int>();
//         for (int k = 0; k < numFaces; k++)
//         {
//             mReader.ReadBytes(4); // skip 4 bytes
//             var type = mReader.ReadInt32();
//             geoMode = (uint)mReader.ReadInt32();
//             lightMode = (uint)mReader.ReadInt32();
//             textureMode = (uint)mReader.ReadInt32();
//             var numVertsInFace = mReader.ReadInt32();
//             mReader.ReadBytes(4); // skip 4 bytes
//             var texPtr = mReader.ReadUInt32();
//             var matPtr = mReader.ReadUInt32();
//             mReader.ReadBytes(12); // skip 12 bytes
//             var extraLight = mReader.ReadSingle();
//             mReader.ReadBytes(12); // skip 12 bytes
//             mReader.ReadBytes(12); // skip 12 bytes normal array ?? 
//             var faceVerts = new int[numVertsInFace];
//             var faceTextureVerts = new int[numVertsInFace];
//             for (int l = 0; l < numVertsInFace; l++)
//             {
//                 faceVerts[l] = mReader.ReadInt32();
//             }
//             if (texPtr != 0)
//             {
//                 for (int l = 0; l < numVertsInFace; l++)
//                 {
//                     faceTextureVerts[l] = mReader.ReadInt32();
//                 }
//             }
//             var combinedVerts = new List<int>();
//             for (int l = 0; l < numVertsInFace; l++)
//             {
//                 combinedVerts.Add(faceVerts[l]);
//                 combinedVerts.Add(faceTextureVerts[l]);
//                 combinedVerts.Add(faceVerts[l]);
//             }
//             meshFaceVerts.AddRange(combinedVerts);
//             if (matPtr != 0)
//             {
//                 matPtr = mReader.ReadUInt32();
//                 materialIds.Add((int)matPtr);
//             }
//         }
//         allFaceVerts.Add(meshFaceVerts);
//         for (int k = 0; k < numVerts; k++)
//         {
//             var x = mReader.ReadSingle();
//             var y = mReader.ReadSingle();
//             var z = mReader.ReadSingle();
//             vertNormals.Add(new Vector3(x, y, z));
//             // Console.WriteLine($"VertNormal {k}: {x} {y} {z}");
//         }
//         var shadow = mReader.ReadUInt32();
//         mReader.ReadBytes(4); // skip 4 bytes
//         var radius = mReader.ReadSingle();
//         mReader.ReadBytes(24); // skip 24 bytes
//     }
// }

// var maxVertCount = allVerts.Max(v => v.Count);
// var maxTextureVertCount = allTextureVerts.Max(v => v.Count);
// var maxFaceVertCount = allFaceVerts.Max(v => v.Count);
// var byteArrays = new List<byte[]>();


// foreach (var (verts,vIndex) in allVerts.WithIndex())
// {
//     var byteArray = new List<byte>();
//     for (int i = 0; i < maxVertCount; i++)
//     {
//         if (i < verts.Count)
//         {
//             byteArray.AddRange(BitConverter.GetBytes(verts[i].X));
//             byteArray.AddRange(BitConverter.GetBytes(verts[i].Y));
//             byteArray.AddRange(BitConverter.GetBytes(verts[i].Z));
//         }
//         else
//         {
//             byteArray.AddRange(BitConverter.GetBytes(0.0f));
//             byteArray.AddRange(BitConverter.GetBytes(0.0f));
//             byteArray.AddRange(BitConverter.GetBytes(0.0f));
//         }
//     }
//     for (int i = 0; i < maxTextureVertCount; i++)
//     {
//         if (i < allTextureVerts[vIndex].Count)
//         {
//             byteArray.AddRange(BitConverter.GetBytes(allTextureVerts[vIndex][i].X));
//             byteArray.AddRange(BitConverter.GetBytes(allTextureVerts[vIndex][i].Y));
//         }
//         else
//         {
//             byteArray.AddRange(BitConverter.GetBytes(0.0f));
//             byteArray.AddRange(BitConverter.GetBytes(0.0f));
//         }
//     }
//     for (int i = 0; i < maxFaceVertCount; i++)
//     {
//         if (i < allFaceVerts[vIndex].Count)
//         {
//             byteArray.AddRange(BitConverter.GetBytes(allFaceVerts[vIndex][i]));
//         }
//         else
//         {
//             byteArray.AddRange(BitConverter.GetBytes(0));
//         }
//     }
//     byteArrays.Add(byteArray.ToArray());
// }

// var outputDir = Path.Combine(Path.GetDirectoryName(testModel), "3DO_Out");
// Directory.CreateDirectory(outputDir);
// File.WriteAllBytes(Path.Combine(outputDir, "verts_and_uvs_and_faces.bin"), byteArrays.SelectMany(b => b).ToArray());

// class GrimMeshFace {
//     public GrimMat Material { get; set; }
//     public int Type { get; set; }
//     public int GeoMode { get; set; }
//     public int LightMode { get; set; }
//     public int TextureMode { get; set; }
//     public int NumVerts { get; set; }
//     public float ExtraLight { get; set; }
//     public int[] TextureVerts { get; set; }
//     public int[] Vertices { get; set; }
//     public Vector3[] VertNormals { get; set; }
// }

// class GrimMat {

// }

// var sfDir = @"C:\Dev\Gaming\PC_DOS\Games\street-fighter-ii";
// var sfIndexFile = @"C:\Dev\Gaming\PC_DOS\Games\street-fighter-ii\INDEX.SF2";

// var sfReader = new BinaryReader(File.OpenRead(sfIndexFile));

// var fileList = new List<string>();

// while (sfReader.BaseStream.Position < 0x80)
// {
//    var fileName = Encoding.ASCII.GetString(sfReader.ReadBytes(16)).TrimEnd('\0');
//    if (string.IsNullOrWhiteSpace(fileName)) continue;
//    fileList.Add(fileName);
// }


// var sfList = new List<SF2File>();

// while (sfReader.BaseStream.Position < sfReader.BaseStream.Length-1)
// {
//    var sf2File = new SF2File
//    {
//       IndexAndFlag = sfReader.ReadByte(),
//       // Offset is only 3 bytes
//       Offset = BitConverter.ToUInt32(sfReader.ReadBytes(3).Append((byte)0).ToArray(), 0),
//       Length = sfReader.ReadUInt16(),
//       FileName = Encoding.ASCII.GetString(sfReader.ReadBytes(0xb)).TrimEnd('\0')
//    };
//    sfList.Add(sf2File);
// }

// Console.WriteLine($"SF2File count: {sfList.Count}");

// var outputDir = Path.Combine(Path.GetDirectoryName(sfIndexFile), "output");
// Directory.CreateDirectory(outputDir);

// foreach (var sf2File in sfList)
// {
//    var mainFile = fileList[sf2File.Index];
//    var data = File.ReadAllBytes(Path.Combine(sfDir, mainFile));
//    var sfFileData = data.Skip((int)sf2File.Offset).Take(sf2File.Length).ToArray();
//    if (sf2File.IsCompressed)
//    {
//       sfFileData = DecompressSf2(sfFileData);
//    }
//    var finalOutputDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(mainFile));
//    Directory.CreateDirectory(finalOutputDir);
//    File.WriteAllBytes(Path.Combine(finalOutputDir, sf2File.FileName), sfFileData);
// }


// byte[] DecompressSf2(byte[] data)
// {
//    var decompressedData = new List<byte>();

//    // decompression algorithm

//    return decompressedData.ToArray();
// }

// internal class SF2File
// {
//    public byte IndexAndFlag { get; set; }
//    public uint Offset { get; set; }
//    public ushort Length { get; set; }
//    public string FileName { get; set; }
//    public bool IsCompressed => (IndexAndFlag & 3) != 0;
//    public byte Index => (byte)(IndexAndFlag >> 4);
// }


// We have two log files, we need to read the contents of both
// Then compare line by line and return the first line that is different
// If the files are identical, return null


// var ddsTestInput = @"C:\Users\OGCit\AppData\Roaming\suyu\dump\0100F4300BF2C000\romfs\common\UI\master\Scenes\zukan_pokemon\zukan_pokemon-00000.nutexb";
// ImageFormatHelper.ConvertNutexbToDds(ddsTestInput, Path.GetDirectoryName(ddsTestInput));
//var binFolder = @"C:\Dev\Gaming\psx\Extractions\DarkStalkers\dedupe";

// // read all .bin files, and split them into 32byte chunks, save each chunk as a separate file
// var binFiles = Directory.GetFiles(binFolder, "*.bin", SearchOption.AllDirectories);

// //var binFile = @"C:\Dev\Gaming\psx\Extractions\DarkStalkers\DarkStalkersPaletteSet1.bin";

// foreach (var binFile in binFiles)
// {
// 	var data = File.ReadAllBytes(binFile);
// 	var chunkCount = data.Length / 32;
// 	var outputFolder = Path.Combine(binFolder, Path.GetFileNameWithoutExtension(binFile));
// 	Directory.CreateDirectory(outputFolder);
// 	for (int i = 0; i < chunkCount; i++)
// 	{
// 		var chunk = data.Skip(i * 32).Take(32).ToArray();
// 		File.WriteAllBytes(Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(binFile)}_{i}.bin"), chunk);
// 	}
// }

//await FileHelpers.DeduplicateBinaryFilesAsync(binFolder, 32, false);

// --------------- MEGA MAN X4 - PSX ---------------------- //

// await FileHelpers.DeduplicateBinaryFilesAsync(@"C:\Dev\Gaming\psx\Games\Files\SLES_01176\ARC\Palettes\Consolidated_Deduped");


// var compressedDir = @"C:\Dev\Gaming\psx\apps\BloodPill-1.1\win32\out\maps";
// var compressedFiles = Directory.GetFiles(compressedDir, "*.cmp");

// foreach (var compressedFile in compressedFiles)
// {
//     BODecompressor.DecompressFile(compressedFile, Path.Combine(compressedDir, Path.ChangeExtension(Path.GetFileName(compressedFile), ".map")));
// }

// var exhumedData = File.ReadAllBytes(exhumedFile);

// for (int i = 0; i < exhumedData.Length;)
// {
//     var width = BitConverter.ToUInt16(exhumedData.Skip(i).Take(2).ToArray(), 0);
//     if (width == 0xFFFF) break;
//     var height = BitConverter.ToUInt16(exhumedData.Skip(i + 2).Take(2).ToArray(), 0);
//     var data = exhumedData.Skip(i + 4).Take(width * 2 * height).ToArray();
//     var image = ImageFormatHelper.ConvertA1B5G5R5ToBitmap(data, width, height);
//     image.Save(Path.Combine(Path.GetDirectoryName(exhumedFile), "output",$"WARNING_{i}.png"), ImageFormat.Png);
//     i += 4 + (width * 2 * height);
// }


// var allBinFile = @"C:\Dev\Gaming\psx\Games\Files\Chocobo's Dungeon 2\ALLBIN.BIN";

// var allBinData = File.ReadAllBytes(allBinFile);
// var outf = @"C:\Dev\Gaming\psx\Games\Files\Chocobo's Dungeon 2\AutomatedOutput";
// ChocoboHelper.ExtractBlockOne(allBinData,outf);

// var spriteHeaderBytes = allBinData.Skip(0x2E301C).Take(0x48).ToArray();
// var shList = new List<CDSpriteHeader>();
// for (int i = 0; i < 0x48; i += 0xC)
// {
//     shList.Add(new CDSpriteHeader
//     {
//         XOffset = spriteHeaderBytes.Skip(i + 2).Take(1).First(),
//         YOffset = (byte)(spriteHeaderBytes.Skip(i + 3).Take(1).First()),
//         Width = (int)(spriteHeaderBytes.Skip(i + 4).Take(1).First()+1),
//         Height = (byte)(spriteHeaderBytes.Skip(i + 6).Take(1).First()+1),
//     });
// }

// var palBytes = allBinData.Skip(0x2E3070).Take(0x80).ToArray();
// var palCount = palBytes.Length / 0x20;

// var palList = new List<List<Color>>();

// for (int i = 0; i < palCount; i++)
// {
//     var pal = palBytes.Skip(i * 0x20).Take(0x20).ToArray();
//     palList.Add(ColorHelper.ReadABgr15Palette(pal));
// }

// foreach (var (pal,pIndex) in palList.WithIndex())
// {
//     var imageBytes = allBinData.Skip(0x29E548).Take(0x5c00).ToArray();
//     var mainImage = ImageFormatHelper.GenerateClutImage(pal, imageBytes, 128, 184,true,255,false);

//     // iterate through the sprite headers and extract each sprite from the image
//     var outputDir = Path.Combine(Path.GetDirectoryName(allBinFile), "choco_and_viking", pIndex.ToString());
//     Directory.CreateDirectory(outputDir);

//     using var mainImageCopy = new Bitmap(mainImage);
//     for (int i = 0; i < shList.Count; i++)
//     {
//         var sh = shList[i];
//         // extract the sprite from mainImageCopy using the info in the CDSpriteHeader
//         using (var spriteImage = new Bitmap(sh.Width, sh.Height))
//         {
//             using (var graphics = Graphics.FromImage(spriteImage))
//             {
//                 graphics.Clear(Color.Transparent);

//                 // Draw the sprite from the main image
//                 graphics.DrawImage(mainImageCopy, new Rectangle(0, 0, sh.Width, sh.Height), new Rectangle(sh.XOffset, sh.YOffset, sh.Width, sh.Height), GraphicsUnit.Pixel);
//             }

//             // Save the sprite image
//             string spritePath = Path.Combine(outputDir, $"{i}.png");
//             spriteImage.Save(spritePath, ImageFormat.Png);
//         }
//     }
// }


// var bgArcDir = @"C:\Dev\Gaming\psx\Games\Files\MARVEL_VS_CAPCOM\DAT";
// var bgArcs = Directory.GetFiles(bgArcDir, "*.arc");

// foreach (var bgArc in bgArcs)
// {
//     var bgBytes = File.ReadAllBytes(bgArc);

//     var fileCount = BitConverter.ToUInt32(bgBytes.Take(4).ToArray(), 0);

//     var fileOffsetAndLengths = new List<(uint, uint)>();

//     for (int i = 0; i < fileCount; i++)
//     {
//         var offset = BitConverter.ToUInt32(bgBytes.Skip(4 + (i * 8)).Take(4).ToArray(), 0);
//         var length = BitConverter.ToUInt32(bgBytes.Skip(8 + (i * 8)).Take(4).ToArray(), 0);
//         fileOffsetAndLengths.Add((offset, length));
//     }

//     var outputDir = Path.Combine(Path.GetDirectoryName(bgArc), "output", Path.GetFileNameWithoutExtension(bgArc));
//     Directory.CreateDirectory(outputDir);

//     for (int i = 0; i < fileCount; i++)
//     {
//         var data = bgBytes.Skip((int)fileOffsetAndLengths[i].Item1).Take((int)fileOffsetAndLengths[i].Item2).ToArray();
//         File.WriteAllBytes(Path.Combine(outputDir, $"{i}.bin"), data);
//     }
// }


// var spriteDir = @"C:\Dev\Gaming\psx\apps\BloodPill-1.1\win32\out\sprites";
// var spriteFiles = Directory.GetFiles(spriteDir, "*.sha");

// foreach (var spriteFile in spriteFiles)
// {
//     var extension = Path.GetExtension(spriteFile);
//     switch (extension)
//     {
//         case ".sdr":
//             LegacyOfKain.ConvertSDR(spriteFile);
//             break;
//         case ".sdt":
//             LegacyOfKain.ConvertSDT(spriteFile);
//             break;
//         case ".sha":
//             LegacyOfKain.ConvertSHA(spriteFile);
//             break;
//         default:
//             Console.WriteLine($"Unhandled extension: {extension}");
//             break;
//     }
// }



// var intFiles = Directory.GetFiles(@"C:\Dev\Gaming\psx\Games\Files\Parappa", "*.int", SearchOption.AllDirectories);

// foreach (var intFile in intFiles)
// {
//     using var intReader = new BinaryReader(File.OpenRead(intFile));

//     intReader.ReadInt32(); // skip 4 bytes
//     var fileCount = intReader.ReadInt32();
//     intReader.ReadInt32(); // skip 4 bytes
//     var blockLengthsAndNames = new List<(int, string)>();
//     for (int i = 0; i < fileCount; i++)
//     {
//         intReader.ReadInt32(); // skip 4 bytes
//         var length = intReader.ReadInt32();
//         var name = Encoding.ASCII.GetString(intReader.ReadBytes(12)).TrimEnd('\0');
//         blockLengthsAndNames.Add((length, name));
//     }

//     intReader.BaseStream.Seek(0x2000, SeekOrigin.Begin);

//     var outputDir = Path.Combine(Path.GetDirectoryName(intFile), Path.GetFileNameWithoutExtension(intFile));
//     Directory.CreateDirectory(outputDir);

//     for (int i = 0; i < fileCount; i++)
//     {
//         var data = intReader.ReadBytes(blockLengthsAndNames[i].Item1);
//         var image = ImageFormatHelper.ExtractTIMImage(data);
//         image.Save(Path.Combine(outputDir, $"{blockLengthsAndNames[i].Item2}.png"), ImageFormat.Png);
//     }
// }



// class CDSpriteHeader
// {
// 	public byte XOffset { get; set; }
// 	public byte YOffset { get; set; }
// 	public int Width { get; set; }
// 	public byte Height { get; set; }
// }

// var logFile1 = @"C:\Dev\Gaming\Gameboy\GBOG\bin\Debug\net6.0-windows\log.txt";
// var logFile2 = @"C:\Dev\Gaming\Gameboy\Emulators\DMG-master\WinFormsDmgRenderer\bin\Debug\net5.0-windows\log.txt";

// var log1Lines = File.ReadAllLines(logFile1);
// var log2Lines = File.ReadAllLines(logFile2);

// for (int i = 0; i < log1Lines.Length; i++)
// {
//     if (log1Lines[i] != log2Lines[i])
//     {
//         Console.WriteLine($"Difference found at line {i + 1}");
//         Console.WriteLine($"Log1: {log1Lines[i]}");
//         Console.WriteLine($"Log2: {log2Lines[i]}");
//         break;
//     }
// }

// var binFolder = @"C:\Dev\Gaming\PC_DOS\Games\BURNCYCLE\output";

// var binFiles = Directory.GetFiles(binFolder, "*.bin");

// var minfFolder = Path.Combine(binFolder, "minf");
// var vinfFolder = Path.Combine(binFolder, "vinf");

// Directory.CreateDirectory(minfFolder);
// Directory.CreateDirectory(vinfFolder);

// var latestPal = new List<Color>();

// foreach (var binFile in binFiles)
// {
// 	using var reader = new BinaryReader(File.OpenRead(binFile));
// 	var type = Encoding.ASCII.GetString(reader.ReadBytes(4));
// 	var subFileIndex = 0;
// 	var outputFolder = type == "MINF" ? Path.Combine(minfFolder, Path.GetFileNameWithoutExtension(binFile)) : vinfFolder;
// 	Directory.CreateDirectory(outputFolder);
// 	var headerOffset = reader.ReadBigEndianUInt32();
// 	reader.BaseStream.Seek(headerOffset, SeekOrigin.Current);
// 	type = Encoding.ASCII.GetString(reader.ReadBytes(4)).ToUpper();
// 	while (type != "EOR ")
// 	{
// 		var blockLength = reader.ReadBigEndianUInt32();
// 		if (blockLength % 2 != 0)
// 		{
// 			blockLength++;
// 		}
// 		switch (type)
// 		{
// 			case "PSTL":
// 			case "PLTF":
// 				{
// 					var palData = reader.ReadBytes((int)blockLength);
// 					latestPal = ColorHelper.ConvertBytesToRGB(palData.Skip(4).ToArray());
// 					break;
// 				}
// 			case "C8ST":
// 				{
// 					var c8stData = reader.ReadBytes((int)blockLength);
// 					var width = BitConverter.ToUInt16(c8stData.Take(2).Reverse().ToArray());
// 					var height = BitConverter.ToUInt16(c8stData.Skip(2).Take(2).Reverse().ToArray());
// 					var imageData = c8stData.Skip(4).ToArray();
// 					var image = ImageFormatHelper.GenerateClutImage(latestPal, imageData, width, height, true);
// 					image.Save(Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(binFile)}_{subFileIndex++}_C8ST.png"), ImageFormat.Png);
// 					break;
// 				}
// 			case "RL7F":
// 			case "RL7B":
// 			case "RL7K":
// 			{
// 				var rl7fData = reader.ReadBytes((int)blockLength);
// 				var image = ImageFormatHelper.GenerateRle7Image(latestPal, rl7fData, 320, 200, true);
// 				image.Save(Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(binFile)}_{subFileIndex++}_{type}.png"), ImageFormat.Png);
// 				break;
// 			}
// 			default:
// 				{
// 					reader.BaseStream.Seek(blockLength, SeekOrigin.Current);
// 					Console.WriteLine($"Unhandled type: {type}");
// 					break;
// 				}
// 		}
// 		type = Encoding.ASCII.GetString(reader.ReadBytes(4)).ToUpper();
// 	}
// }

// var hagFile = @"C:\Dev\Gaming\PC_DOS\Games\DBURGER\RESOURCE\SECTION1.HAG";
// var hagData = File.ReadAllBytes(hagFile);

// var palBytes = hagData.Skip(0x495D820).Take(0x400).ToArray();
// var pal = ColorHelper.ConvertBytesToARGB(palBytes);

// var imageData = hagData.Skip(0x495DC20).Take(0x80 * 0x780).ToArray();

// var imageLength = 0x80 * 0x80;
// var imageCount = imageData.Length / imageLength;

// var outputDir = Path.Combine(Path.GetDirectoryName(hagFile), "section1_0x495D820");
// Directory.CreateDirectory(outputDir);

// for (int i = 0; i < imageCount; i++)
// {
//     var image = ImageFormatHelper.GenerateClutImage(pal, imageData.Skip(i * imageLength).Take(imageLength).ToArray(), 0x80, 0x80, true);
//     image.Save(Path.Combine(outputDir, $"{i}.png"), ImageFormat.Png);
// }

// var smkDir = @"C:\Dev\Gaming\PC_Windows\Games\SPIDERMAN\SPIDER";
// var smkFiles = Directory.GetFiles(smkDir, "*.smk", SearchOption.AllDirectories);

// foreach (var smk in smkFiles)
// {
//     var outputDir = Path.Combine(Path.GetDirectoryName(smk), "output", Path.GetFileNameWithoutExtension(smk));
//     Directory.CreateDirectory(outputDir);

//     // use ffmpeg to convert the smk to a series of pngs
//     var process = new Process
//     {
//         StartInfo = new ProcessStartInfo
//         {
//             FileName = "ffmpeg",
//             Arguments = $"-i \"{smk}\" \"{outputDir}\\%04d.png\"",
//             UseShellExecute = false,
//             RedirectStandardOutput = true,
//             CreateNoWindow = true
//         }
//     };
//     process.Start();
//     process.WaitForExit();
// }


// var folderToResize = @"C:\Dev\Gaming\PC_DOS\Korean Game\MON2-1";

// FileHelpers.ResizeImagesInFolder(folderToResize, ExpansionOrigin.MiddleCenter);

// var inDir = @"C:\Dev\Gaming\PC_DOS\Games\Dokkaebi-ga-Ganda_DOS_EN_Alt-1";

// var palFiles = Directory.GetFiles(inDir, "*.plt");
// var palList = new List<List<Color>>();

// foreach (var pFile in palFiles)
// {
//     var pData = File.ReadAllBytes(pFile);
//     palList.Add(ColorHelper.ConvertBytesToARGB(pData));
// }

// var sprFiles = Directory.GetFiles(inDir, "*.spr");

// foreach (var spr in sprFiles)
// {
//     var pal = new List<Color>();
//     // first we check the spr filename for the first instance of an integer
//     var digitIndex = Path.GetFileNameWithoutExtension(spr).Where(char.IsDigit)?.FirstOrDefault();
//     if (digitIndex != null && digitIndex > 0)
//     {
//         var palIndex = Path.GetFileNameWithoutExtension(spr).IndexOf((char)digitIndex);

//         if (palIndex > 0)
//         {
//             // get the number from the filename
//             var palNumber = int.Parse(Path.GetFileNameWithoutExtension(spr).Substring(palIndex, 1));
//             // find the index of the pal file that contains the number in the spr filename
//             var palFileName = palFiles.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).Contains(palNumber.ToString()));
//             var palFileIndex = palFiles.ToList().IndexOf(palFileName);
//             if (palFileIndex > -1)
//             {
//                 pal = palList[palFileIndex];
//             }
//         }
//     }
//     else
//     {
//         // check the first 4 characters of the spr filename against the first 4 characters of the pal filename
//         var sprName = Path.GetFileNameWithoutExtension(spr).Substring(0, 4);
//         var palFile = palFiles.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).StartsWith(sprName));
//         if (palFile != null)
//         {
//             var pData = File.ReadAllBytes(palFile);
//             pal = ColorHelper.ConvertBytesToARGB(pData);
//         } else {
//             pal = palList[0];
//         }
//     }
//     var sprData = File.ReadAllBytes(spr);

//     var count = BitConverter.ToUInt16(sprData.Take(2).ToArray(), 0);
//     var dataLength = BitConverter.ToUInt16(sprData.Skip(2).Take(2).ToArray(), 0);
//     var offsetDataLength = sprData.Length - dataLength - 4;
//     var sizeData = sprData.Skip(4).Take(offsetDataLength).ToArray();

//     var widthAndHeightAndOffset = new List<(int, int, int)>();
//     for (int i = 0; i < sizeData.Length; i += 10)
//     {
//         var width = BitConverter.ToUInt16(sizeData.Skip(i + 4).Take(2).ToArray(), 0);
//         var height = BitConverter.ToUInt16(sizeData.Skip(i + 6).Take(2).ToArray(), 0);
//         var offset = BitConverter.ToUInt16(sizeData.Skip(i + 8).Take(2).ToArray(), 0);
//         widthAndHeightAndOffset.Add((width, height, offset));
//     }

//     var index = 4 + offsetDataLength;
//     var spriteImageData = sprData.Skip(index).ToArray();
//     var outputDir = Path.Combine(Path.GetDirectoryName(spr), "output", Path.GetFileNameWithoutExtension(spr));
//     Directory.CreateDirectory(outputDir);


//     for (int i = 0; i < widthAndHeightAndOffset.Count; i++)
//     {
//         var leftPadding = 0;
//         var rightPadding = 0;
//         var outputData = new List<byte>();
//         var skipLeft = false;

//         var pixelsInCurrentLine = 0;

//         var width = widthAndHeightAndOffset[i].Item1;
//         var height = widthAndHeightAndOffset[i].Item2;
//         var expectedSize = width * height;

//         var spriteLength = i == widthAndHeightAndOffset.Count - 1 ? dataLength - widthAndHeightAndOffset[i].Item3 : widthAndHeightAndOffset[i + 1].Item3 - widthAndHeightAndOffset[i].Item3;
//         var spriteData = spriteImageData.Skip(widthAndHeightAndOffset[i].Item3).Take(spriteLength).ToArray();
//         index = 0;
//         while (outputData.Count < expectedSize && index < spriteData.Length)
//         {
//             if (!skipLeft)
//             {
//                 leftPadding = spriteData[index++];
//                 outputData.AddRange(Enumerable.Repeat((byte)0, leftPadding));
//                 if (leftPadding != 0 && leftPadding != width) index++;
//                 pixelsInCurrentLine = leftPadding;
//                 if (pixelsInCurrentLine == width)
//                 {
//                     skipLeft = false;
//                     pixelsInCurrentLine = 0;
//                     continue;
//                 }
//             }
//             var pixelCount = spriteData[index++];
//             var pixels = spriteData.Skip(index).Take(pixelCount).ToArray();
//             outputData.AddRange(pixels);
//             index += pixelCount;
//             pixelsInCurrentLine += pixelCount;
//             if (index >= spriteData.Length)
//             {
//                 break;
//             }
//             if (pixelsInCurrentLine < width)
//             {
//                 rightPadding = spriteData[index];
//                 outputData.AddRange(Enumerable.Repeat((byte)0, rightPadding));
//                 index++;
//                 pixelsInCurrentLine += rightPadding;
//                 if (index >= spriteData.Length)
//                 {
//                     break;
//                 }
//                 if (spriteData[index] == 0 && pixelsInCurrentLine < width)
//                 {
//                     skipLeft = true;
//                     index++;
//                 }
//                 else
//                 {
//                     skipLeft = false;
//                     pixelsInCurrentLine = 0;
//                 }
//             }
//             else if (pixelsInCurrentLine == width)
//             {
//                 skipLeft = false;
//                 pixelsInCurrentLine = 0;
//             }
//         }
//         var outputArray = outputData.ToArray();
//         //File.WriteAllBytes(Path.Combine(outputDir, $"output_{i}.bin"), outputArray);
//         var image = ImageFormatHelper.GenerateClutImage(pal, outputArray, width, height, true);
//         image.Save(Path.Combine(outputDir, $"output_{i}.png"), ImageFormat.Png);
//         outputData.Clear();
//     }

// }

// var inputFolder = @"C:\Dev\Gaming\PC_DOS\Games\DEATH_GATE\DGATEVGA";
// var testPIC = @"C:\Dev\Gaming\PC_DOS\Games\DEATH_GATE\DGATEVGA\DGATE000.PIC";

// var picFiles = Directory.GetFiles(inputFolder, "*.PIC");

// var picData = File.ReadAllBytes(testPIC);
// var initialOffset = BitConverter.ToUInt32(picData.Take(4).ToArray(), 0);

// var dgHeaderData = picData.Take((int)initialOffset - 12).ToArray();
// var dgHeaderCount = dgHeaderData.Length / 12;

// var dgHeaders = new List<DGatePICHeader>();

// for (int i = 0; i < dgHeaderCount; i++)
// {
//     var offset = BitConverter.ToUInt32(dgHeaderData.Skip(i * 12).Take(4).ToArray(), 0);
//     var flags = BitConverter.ToUInt16(dgHeaderData.Skip(i * 12 + 4).Take(2).ToArray(), 0);
//     var width = BitConverter.ToUInt16(dgHeaderData.Skip(i * 12 + 6).Take(2).ToArray(), 0);
//     var height = BitConverter.ToUInt16(dgHeaderData.Skip(i * 12 + 8).Take(2).ToArray(), 0);
//     var unused = BitConverter.ToUInt16(dgHeaderData.Skip(i * 12 + 10).Take(2).ToArray(), 0);
//     dgHeaders.Add(new DGatePICHeader { Offset = offset, Flags = flags, Width = width, Height = height, Unused = unused });
// }

// var outputDir = Path.Combine(Path.GetDirectoryName(testPIC), "PIC_output");
// Directory.CreateDirectory(outputDir);

// var mostRecentPalette = new List<Color>();
// // find the first palette, in case it's needed for the first image
// var firstPalette = dgHeaders.FirstOrDefault(d => d.Flags == 0x1300);
// if (firstPalette != null)
// {
//     var firstPaletteData = picData.Skip((int)firstPalette.Offset).Take(0x300).ToArray();
//     mostRecentPalette = ColorHelper.ConvertBytesToARGB(firstPaletteData);
// }

// for (int i = 0; i < dgHeaders.Count; i++)
// {
//     var dgHeader = dgHeaders[i];
//     var nextOffset = (int)(i == dgHeaders.Count - 1 ? picData.Length : (int)dgHeaders[i + 1].Offset);
//     var chunk = picData.Skip((int)dgHeader.Offset).Take((int)(nextOffset - dgHeader.Offset)).ToArray();
//     if (dgHeader.Flags == 0x1300)
//     {
//         mostRecentPalette = ColorHelper.ConvertBytesToARGB(chunk.Take(0x300).ToArray());
//         chunk = chunk.Skip(0x300).ToArray();
//     }
// }

// class DGatePICHeader {
//     public uint Offset { get; set; }
//     public ushort Flags { get; set; }
//     public ushort Width { get; set; }
//     public ushort Height { get; set; }
//     public ushort Unused { get; set; }
// }

// var dunFile = @"C:\Dev\Gaming\PC_Windows\Games\fhd\globals.dun";

// using var dReader = new BinaryReader(File.OpenRead(dunFile));
// var dunOutputDir = @"C:\Dev\Gaming\PC_Windows\Games\fhd\globals";
// Directory.CreateDirectory(dunOutputDir);

// dReader.BaseStream.Seek(0x1AAF69D4, SeekOrigin.Begin);

// while (dReader.BaseStream.Position < dReader.BaseStream.Length)
// {
//   // read until we find the next 0x0A
//   var name = "";
//   var nameBytes = new List<byte>();
//   while (true)
//   {
//     var b = dReader.ReadByte();
//     if (b == 0x0A)
//     {
//       break;
//     }
//     nameBytes.Add(b);
//   }
//   var offset = dReader.ReadInt32();
//   var length = dReader.ReadInt32();
//   var currentPos = dReader.BaseStream.Position;
//   var nameBytesArr = nameBytes.ToArray();
//   name = Encoding.ASCII.GetString(nameBytesArr);
//   var fileName = name.Split('\\').Last();
//   var folders = name.Substring(2, name.Length - 2).Split('\\').Take(name.Split('\\').Length - 1);
//   var folderPath = Path.Combine(dunOutputDir, Path.Combine(folders.ToArray()));
//   Directory.CreateDirectory(folderPath);
//   Console.WriteLine($"FolderPath: {folderPath} Name: {name}, Offset: {offset}, Length: {length}");
//   dReader.BaseStream.Seek(offset, SeekOrigin.Begin);
//   var data = dReader.ReadBytes(length);
//   var output = Path.Combine(folderPath, fileName);
//   File.WriteAllBytes(output, data);
//   dReader.BaseStream.Seek(currentPos, SeekOrigin.Begin);
// }
