using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using OGLibCDi.Helpers;
using OGLibCDi.Models;
using Color = System.Drawing.Color;
using ColorHelper = OGLibCDi.Helpers.ColorHelper;
using ImageFormatHelper = ExtractCLUT.Helpers.ImageFormatHelper;
using static ExtractCLUT.Helpers.FileHelpers;

namespace ExtractCLUT.Games
{
  public class HMDatFile
  {
    public string OriginalFile { get; set; }
    public List<string> SpriteNames { get; set; }
    public List<uint> SpriteNameOffsets { get; set; }
    public List<uint> SpriteDataOffsets { get; set; }
    public string Text { get; set; }
  }
  public static class HotelMarioHelper
  {
    public static void ExtractSprites(string inputFolder, bool existingData = false, bool parsePreSprites = false, List<Color> mainPalette = null, List<Color> marioPalette = null, List<Color> luigiPalette = null)
    {
      var files = Directory.GetFiles(inputFolder, "*_dat.rtf").ToList();
      files.Add(Path.Combine(inputFolder, "intro.rtf"));
      //files.Add(Path.Combine(inputFolder, "exit.rtf"));
      files.Add(Path.Combine(inputFolder, "cdi_hotel"));
      var outputDirs = existingData ? Directory.GetDirectories(Path.Combine(inputFolder, "Output")).ToList() : new List<string>();

      if (!existingData) {
        foreach (var file in files)
        {
          var cdiFile = new CdiFile(file);

          var outputDir = Path.Combine(inputFolder, "Output", Path.GetFileNameWithoutExtension(file));
          Directory.CreateDirectory(outputDir);
          outputDirs.Add(outputDir);
          var dataSectors = cdiFile.DataSectors.OrderBy(s => s.SectorIndex).ToList();

          var sectorList = new List<CdiSector>();

          foreach (var sector in dataSectors)
          {
            sectorList.Add(sector);
            if (sector.SubMode.IsEOR || sector == dataSectors.Last())
            {
              var data = sectorList.SelectMany(x => x.GetSectorData()).ToArray();
              var index = sectorList.First().SectorIndex;
              var output = Path.Combine(outputDir, $"{index}.bin");
              File.WriteAllBytes(output, data);
              sectorList.Clear();
            }
          }
        }
      }

      foreach (var dir in outputDirs)
      {
        files = Directory.GetFiles(dir, "*.bin").ToList();
        foreach (var file in files)
        {
          var blobs = ExtractSpriteByteSequences(file);
         
          var outputDir = Path.Combine(dir, "Sprites", Path.GetFileNameWithoutExtension(file));
          
          Directory.CreateDirectory(outputDir);
          foreach (var (blob, index) in blobs.WithIndex())
          {
            try
            {
              var decodedBlob = CompiledSpriteHelper.DecodeCompiledSprite(blob, 0);
              var output = Path.Combine(outputDir, $"{index}.bin");
              File.WriteAllBytes(output, decodedBlob);
              if (mainPalette != null || (file.Contains("intro") && (marioPalette != null || luigiPalette != null)))
              {
                var palette = file.Contains("intro") ? marioPalette : mainPalette;
                var image = ImageFormatHelper.GenerateClutImage(palette, decodedBlob, 384, 240, true);
                Rectangle cropRect = new Rectangle(0, 0, 32, 32);

                // Crop the image
                using (Bitmap croppedImage = image.Clone(cropRect, image.PixelFormat))
                {
                  // if every pixel of image is black, skip saving
                  for (int x = 0; x < croppedImage.Width; x++)
                  {
                    for (int y = 0; y < croppedImage.Height; y++)
                    {
                      if (croppedImage.GetPixel(x, y).ToArgb() != Color.Black.ToArgb())
                      {
                        break;
                      }
                      if (x == croppedImage.Width - 1 && y == croppedImage.Height - 1)
                      {
                        return;
                      }
                    }
                  }

                  // Save the cropped image with "_icon" suffix

                  var imgOutputPath = Path.Combine(outputDir, "images_32x32");
                  Directory.CreateDirectory(imgOutputPath);
                  var outputName = Path.Combine(imgOutputPath, $"{index}.png");
                  croppedImage.Save(outputName, ImageFormat.Png);
                }
                cropRect = new Rectangle(0, 0, 64, 64);

                // Crop the image
                using (Bitmap croppedImage = image.Clone(cropRect, image.PixelFormat))
                {
                  // if every pixel of image is black, skip saving
                  for (int x = 0; x < croppedImage.Width; x++)
                  {
                    for (int y = 0; y < croppedImage.Height; y++)
                    {
                      if (croppedImage.GetPixel(x, y).ToArgb() != Color.Black.ToArgb())
                      {
                        break;
                      }
                      if (x == croppedImage.Width - 1 && y == croppedImage.Height - 1)
                      {
                        return;
                      }
                    }
                  }

                  // Save the cropped image with "_icon" suffix

                  var imgOutputPath = Path.Combine(outputDir, "images_64x64");
                  Directory.CreateDirectory(imgOutputPath);
                  var outputName = Path.Combine(imgOutputPath, $"{index}.png");
                  croppedImage.Save(outputName, ImageFormat.Png);
                }
                if (file.Contains("intro") && luigiPalette != null)
                {
                  cropRect = new Rectangle(0, 0, 32, 32);
                  image = ImageFormatHelper.GenerateClutImage(luigiPalette, decodedBlob, 384, 240, true);
                  using (Bitmap croppedImage = image.Clone(cropRect, image.PixelFormat))
                  {
                    // if every pixel of image is black, skip saving
                    for (int x = 0; x < croppedImage.Width; x++)
                    {
                      for (int y = 0; y < croppedImage.Height; y++)
                      {
                        if (croppedImage.GetPixel(x, y).ToArgb() != Color.Black.ToArgb())
                        {
                          break;
                        }
                        if (x == croppedImage.Width - 1 && y == croppedImage.Height - 1)
                        {
                          return;
                        }
                      }
                    }

                    // Save the cropped image with "_icon" suffix

                    var imgOutputPath = Path.Combine(outputDir, "luigi_images_32x32");
                    Directory.CreateDirectory(imgOutputPath);
                    var outputName = Path.Combine(imgOutputPath, $"{index}.png");
                    croppedImage.Save(outputName, ImageFormat.Png);
                  }
                }
              }
            }
            catch (Exception)
            {
              //Console.WriteLine($"Error decoding sprite {index} in {file}");
            }
          }

          if (parsePreSprites) {
            var preSpriteBlobs = ExtractPreSpriteByteSequences(file);
            var preSpriteOutputDir = Path.Combine(dir, "PreSprites", Path.GetFileNameWithoutExtension(file));
            Directory.CreateDirectory(preSpriteOutputDir);
            foreach (var (blob, index) in preSpriteBlobs.WithIndex())
            {
              try
              {
                var decodedBlob = CompiledSpriteHelper.DecodeCompiledSprite(blob, 0);
                var output = Path.Combine(preSpriteOutputDir, $"{index}.bin");
                File.WriteAllBytes(output, decodedBlob.Skip(blob.Length + 0x20).ToArray());
              }
              catch (Exception ex)
              {
                Console.WriteLine($"Error decoding pre-sprite {index} in {file}");
              }
            }
          }
        }
      }

    }
    public static void ExtractAllImageData(string rtfPath)
    {

      var fileList = Directory.GetFiles(rtfPath, "*am.rtf").ToList();
      fileList.AddRange(Directory.GetFiles(rtfPath, "*av.rtf").ToList());
      fileList.Add(Path.Combine(rtfPath,"intro.rtf"));
      fileList.Add(Path.Combine(rtfPath, "exit.rtf"));

      foreach (var file in fileList)
      {
        if (!File.Exists(file)) continue;
        var cdiFile = new CdiFile(file);
        var outputDir = Path.Combine(Path.GetDirectoryName(file), "output");
        var fileOutputDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(file));
        Directory.CreateDirectory(fileOutputDir);

        // extract palettes
        var paletteOutputDir = Path.Combine(fileOutputDir, "palettes");
        Directory.CreateDirectory(paletteOutputDir);
        var paletteSectors = cdiFile.DataSectors.OrderBy(ds => ds.SectorIndex).ToList();

        var palettes = new List<Tuple<int, List<Color>>>();
        var offsets = new List<int>();

        foreach (var (sector, index) in paletteSectors.WithIndex())
        {
          var bytes = sector.GetSectorData().Take(0x180).ToArray();
          var palette = ColorHelper.ConvertBytesToRGB(bytes);
          palettes.Add(new Tuple<int, List<Color>>(sector.SectorIndex, palette));
          offsets.Add(sector.SectorIndex);
        }

        palettes = palettes.OrderByDescending(p => p.Item1).ToList();

        // extract DYUV data
        var dyuvOutputDir = Path.Combine(fileOutputDir, "dyuv");
        Directory.CreateDirectory(dyuvOutputDir);
        var dyuvSectors = cdiFile.VideoSectors.Where(ds => ds.Coding.VideoString == "DYUV").OrderBy(ds => ds.SectorIndex).ToList();

        var dyuvData = new List<byte[]>();

        foreach (var (sector, index) in dyuvSectors.WithIndex())
        {
          dyuvData.Add(sector.GetSectorData());
          if (sector.SubMode.IsEOR)
          {
            var dyuvBytes = dyuvData.SelectMany(x => x).ToArray();
            var image = ImageFormatHelper.DecodeDYUVImage(dyuvBytes, 384, 280);
            var outputName = Path.Combine(dyuvOutputDir, $"dyuv_{sector.SectorIndex}.png");
            image.Save(outputName, ImageFormat.Png);
            dyuvData.Clear();
          }
        }

        // extract rl7 data
        var rl7OutputDir = Path.Combine(fileOutputDir, "rl7");
        Directory.CreateDirectory(rl7OutputDir);
        var rl7Sectors = cdiFile.VideoSectors.Where(ds => ds.Coding.VideoString == "RL7").OrderBy(ds => ds.SectorIndex).ToList();

        var rl7Data = new List<byte[]>();

        var nextPaletteOffset = palettes[^2].Item1;

        foreach (var (sector, index) in rl7Sectors.WithIndex())
        {
          if (sector.SectorIndex > nextPaletteOffset || index == rl7Sectors.Count - 1)
          {
            var palette = palettes.FirstOrDefault(x => x.Item1 < rl7Sectors[index - 1].SectorIndex);
            var colors = palette.Item2;
            var rl7Bytes = rl7Data.SelectMany(x => x).ToArray();
            var imageBytes = ImageFormatHelper.Rle7(rl7Bytes, 384);
            var imageIndex = 0;
            while (imageBytes.Length >= 384 * 280)
            {
              var bytes = imageBytes.Take(384 * 280).ToArray();
              var image = ImageFormatHelper.GenerateClutImage(colors, bytes, 384, 280, true);
              var outputName = Path.Combine(rl7OutputDir, $"rl7_{sector.SectorIndex}_{imageIndex}.png");
              imageIndex++;
              image.Save(outputName, ImageFormat.Png);
              imageBytes = imageBytes.Skip(384 * 280).ToArray();
            }
            rl7Data.Clear();
            nextPaletteOffset = palettes.OrderBy(x => x.Item1).FirstOrDefault(x => x.Item1 > sector.SectorIndex)?.Item1 ?? palettes[^1].Item1;
          }
          var sectorBytes = sector.GetSectorData();
          var trimmedBytes = FileHelpers.RemoveTrailingZeroes(sectorBytes);
          rl7Data.Add(trimmedBytes);
        }

        // extract clut7 data
        var clut7OutputDir = Path.Combine(fileOutputDir, "clut7");
        Directory.CreateDirectory(clut7OutputDir);
        var clut7Sectors = cdiFile.VideoSectors.Where(ds => ds.Coding.VideoString == "CLUT7").OrderBy(ds => ds.SectorIndex).ToList();

        var clut7Data = new List<byte[]>();

        nextPaletteOffset = palettes[^2].Item1;

        foreach (var (sector, index) in clut7Sectors.WithIndex())
        {
          clut7Data.Add(sector.GetSectorData());
          if (sector.SubMode.IsEOR)
          {
            var palette = palettes.FirstOrDefault(x => x.Item1 < sector.SectorIndex - 1).Item2;
            var clut7Bytes = clut7Data.SelectMany(x => x).ToArray();
            var imageIndex = 0;

            var image = ImageFormatHelper.GenerateClutImage(palette, clut7Bytes, 384, 280, false);
            var outputName = Path.Combine(clut7OutputDir, $"clut7_{sector.SectorIndex}.png");
            image.Save(outputName, ImageFormat.Png);

            clut7Data.Clear();
            nextPaletteOffset = palettes.OrderBy(x => x.Item1).FirstOrDefault(x => x.Item1 > sector.SectorIndex)?.Item1 ?? palettes[^1].Item1;
          }
        }

      }

    }

    public static List<byte[]> ExtractIDATPalettes(string filePath)
    {
      // Byte sequences to find
      byte[] endSequence = [0x49, 0x44, 0x41, 0x54];

      // Read all bytes from the file
      byte[] fileContent = File.ReadAllBytes(filePath);

      List<byte[]> extractedSequences = new List<byte[]>();
      int currentIndex = 0;

      while (currentIndex < fileContent.Length)
      {
        // Find the end sequence starting from where the start sequence was found
        int endIndex = FindSequence(fileContent, endSequence, currentIndex);

        if (endIndex == -1) // No end sequence found after the start
          break;

        // Calculate length to copy (including the end sequence)
        int length = 0x180;

        // Copy the sequence from start to end (including the end sequence)
        byte[] extracted = new byte[length];
        Array.Copy(fileContent, endIndex-0x180, extracted, 0, length);
        extractedSequences.Add(extracted);

        // Move the current index to the byte after the current end sequence
        currentIndex =  endIndex+1;
      }

      return extractedSequences;
    }

    public static List<byte[]> ExtractSpriteByteSequences(string filePath)
    {
      // Byte sequences to find
      byte[] startSequence = [0x2f, 0x09];
      byte[] endSequence = [0x4e, 0x75];

      // Read all bytes from the file
      byte[] fileContent = File.ReadAllBytes(filePath);

      List<byte[]> extractedSequences = new List<byte[]>();
      int currentIndex = 0;

      while (currentIndex < fileContent.Length)
      {
        // Find the start sequence
        int startIndex = FindSequence(fileContent, startSequence, currentIndex);

        if (startIndex == -1) // No more start sequences found
          break;

        // Find the end sequence starting from where the start sequence was found
        int endIndex = FindSequence(fileContent, endSequence, startIndex);

        if (endIndex == -1) // No end sequence found after the start
          break;

        // Calculate length to copy (including the end sequence)
        int length = endIndex - startIndex + endSequence.Length;

        // Copy the sequence from start to end (including the end sequence)
        byte[] extracted = new byte[length];
        Array.Copy(fileContent, startIndex, extracted, 0, length);
        extractedSequences.Add(extracted);

        // Move the current index to the byte after the current end sequence
        currentIndex = endIndex + endSequence.Length;
      }

      return extractedSequences;
    }

    public static List<byte[]> ExtractPreSpriteByteSequences(string filePath)
    {
      // Byte sequences to find
      byte[] startSequence = [0x4e, 0x55];
      byte[] endSequence = [0x4e, 0x75];

      // Read all bytes from the file
      byte[] fileContent = File.ReadAllBytes(filePath);

      List<byte[]> extractedSequences = new List<byte[]>();
      int currentIndex = 0;

      while (currentIndex < fileContent.Length)
      {
        // Find the start sequence
        int startIndex = FindSequence(fileContent, startSequence, currentIndex);

        if (startIndex == -1) // No more start sequences found
          break;

        // Find the end sequence starting from where the start sequence was found
        int endIndex = FindSequence(fileContent, endSequence, startIndex);

        if (endIndex == -1) // No end sequence found after the start
          break;

        // Calculate length to copy (including the end sequence)
        int length = endIndex - startIndex + endSequence.Length;

        // Copy the sequence from start to end (including the end sequence)
        byte[] extracted = new byte[length];
        Array.Copy(fileContent, startIndex, extracted, 0, length);
        extractedSequences.Add(extracted);

        // Move the current index to the byte after the current end sequence
        currentIndex = endIndex + endSequence.Length;
      }

      return extractedSequences;
    }

    public static HMDatFile ParseDatFile(string filePath)
    {
      var hmData = new HMDatFile
      {
        OriginalFile = Path.GetFileName(filePath),
        SpriteNames = new List<string>(),
        SpriteNameOffsets = new List<uint>(),
        SpriteDataOffsets = new List<uint>()
      };
      bool addToSpriteNameList = true;

      using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
      using (BinaryReader reader = new BinaryReader(fs))
      {
        // Read integer at offset 0x0C as big endian
        reader.BaseStream.Seek(0x0C, SeekOrigin.Begin);
        uint startOffset = Utils.ReadBigEndianUInt32(reader);

        // Read integer at offset 0x05
        reader.BaseStream.Seek(0x04, SeekOrigin.Begin);
        uint endOffset = Utils.ReadBigEndianUInt32(reader); 
        
        reader.BaseStream.Seek(0x3C, SeekOrigin.Begin);

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
          uint offset = Utils.ReadBigEndianUInt32(reader);

          // Check for the sequence of 0x00 0x00 0x00 0x00
          if (offset == 0)
          {
            break;
          }

          if (addToSpriteNameList)
          {
            hmData.SpriteNameOffsets.Add(offset);
          }
          else
          {
            hmData.SpriteDataOffsets.Add(offset);
          }

          // Alternate between lists
          addToSpriteNameList = !addToSpriteNameList;
        }

        foreach (var offset in hmData.SpriteNameOffsets)
        {
          reader.BaseStream.Seek(offset, SeekOrigin.Begin);
          hmData.SpriteNames.Add(reader.ReadNullTerminatedString());
        }

        // Check if offsets are valid
        if (startOffset < endOffset && startOffset >= 0 && endOffset <= fs.Length)
        {
          // Read bytes from startOffset to endOffset
          reader.BaseStream.Seek(startOffset, SeekOrigin.Begin);
          byte[] textBytes = reader.ReadBytes((int)(endOffset - startOffset));

          // Convert bytes to string and add to list
          hmData.Text = Encoding.ASCII.GetString(textBytes); // Assuming ASCII encoding
          return hmData;
        }
        else
        {
          throw new Exception("Invalid start or end offset in the file.");
        }
      }

    }
  }
}

