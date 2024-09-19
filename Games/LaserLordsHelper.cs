using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using static ExtractCLUT.Utils;
using static ExtractCLUT.Helpers.ColorHelper;
using ExtractCLUT.Models;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;
using static ExtractCLUT.Helpers.ImageFormatHelper;

namespace ExtractCLUT.Games
{
  public static class LaserLordsHelper
  {
    static void TileBehaviour(int baseIndexX, int baseIndexY)
    {
      var indexXMinus1 = baseIndexX + -1;
      var _TILE_TO_LEFT_OF_PLAYER = GetTileAtCoords(indexXMinus1, baseIndexY);
      var CURRENT_TILE = GetTileAtCoords(baseIndexX, baseIndexY);
      var indexXPlus1 = baseIndexX + 1;
      var TILE_TO_RIGHT_OF_PLAYER = GetTileAtCoords(indexXPlus1, baseIndexY);
      var indexYPlus1 = baseIndexY + 1;
      var _TILE_DIAGONALLY_UP_LEFT_OF_PLAYER = GetTileAtCoords(indexXMinus1, indexYPlus1);
      var TILE_BELOW = GetTileAtCoords(baseIndexX, indexYPlus1);
      var _TILE_DIAGONALLY_DOWN_RIGHT_OF_PLAYER = GetTileAtCoords(indexXPlus1, indexYPlus1);
      var yPosition = baseIndexY + -4;
      var _TILE_TO_LEFT_AND_4_ABOVE_PLAYER = GetTileAtCoords(indexXMinus1, yPosition);
      var _TILE_4_ABOVE_PLAYER = GetTileAtCoords(baseIndexX, yPosition);
      var _TILE_TO_RIGHT_AND_4_ABOVE_PLAYER = GetTileAtCoords(indexXPlus1, yPosition);
      var _TILE_ABOVE_PLAYER = GetTileAtCoords(baseIndexX, baseIndexY + -1);
      var TILE_2_ABOVE_PLAYER = GetTileAtCoords(baseIndexX, baseIndexY + -2);
    }

    private static object GetTileAtCoords(int indexXMinus1, int baseIndexY)
    {
      throw new NotImplementedException();
    }

    public class ImageLine
    {
      public int LeftOffset { get; set; }
      public List<byte> Pixels { get; set; }
      public int RemainingPixels { get; set; }
    }

    public static List<ImageLine> DecodeImage(byte[] data)
    {
      List<ImageLine> imageLines = new List<ImageLine>();
      int i = 0;

      while (i < data.Length - 1)
      {

        ImageLine line = new ImageLine();
        if (data[i] == 0x00 && data[i + 1] != 0x38)
        {
          line.LeftOffset = data[i + 1];
          i += 2;
        }
        else if (data[i] == 0x00 && data[i + 1] == 0x38)
        {
          line.LeftOffset = 0;
          line.Pixels = Enumerable.Repeat((byte)0x00, 56).ToList();
        }
        else
        {
          line.LeftOffset = 0;
        }
        line.Pixels = new List<byte>();

        while (i < data.Length - 1)
        {
          if (line.LeftOffset + line.Pixels.Count == 56)
          {
            imageLines.Add(line);
            break;
          }
          if (data[i] == 0x00)
          {
            if (line.LeftOffset + line.Pixels.Count == 56)
            {
              imageLines.Add(line);
              break;
            }
            if (i + 2 < data.Length && data[i + 2] == 0x00)
            {
              line.RemainingPixels = data[i + 1];
              i += 2;
              if (line.LeftOffset + line.Pixels.Count == 56)
              {
                imageLines.Add(line);
                break;
              }
              break;
            }
            else
            {
              int numberOfNullBytes = data[i + 1];
              for (int j = 0; j < numberOfNullBytes; j++)
              {
                line.Pixels.Add(0x00);
                if (line.LeftOffset + line.Pixels.Count == 56)
                {
                  break;
                }
              }
              i += 2;
            }
          }
          else
          {
            line.Pixels.Add(data[i]);
            i++;
            if (line.LeftOffset + line.Pixels.Count == 56)
            {
              imageLines.Add(line);
              break;
            }
          }

          if (i >= data.Length - 1 || ((line.LeftOffset + line.Pixels.Count) == 56))
          {
            imageLines.Add(line);
            if (i >= data.Length - 1) return imageLines;
            break;
          }
        }

        imageLines.Add(line);

      }
      // if (imageLines.Count > 48) imageLines.RemoveRange(48, imageLines.Count - 48);
      return imageLines;
    }

    public static Bitmap CreateBitmapFromImageLines(List<ImageLine> imageLines, List<Color> colorPalette, bool isplayer = false)
    {
      try
      {
        var height = imageLines.Count;
        int width = imageLines.Max(x => x.LeftOffset + x.Pixels.Count + x.RemainingPixels);
        Bitmap bitmap = new Bitmap(width, height);
        int currentLine = 0;

        using (Graphics g = Graphics.FromImage(bitmap))
        {
          g.Clear(Color.Transparent);
        }

        foreach (ImageLine line in imageLines)
        {
          int x = line.LeftOffset;

          for (int i = 0; i < line.Pixels.Count; i++)
          {
            byte currentByte = line.Pixels[i];

            // transparent where bytes are null bytes
            if (currentByte == 0x00)
            {
              bitmap.SetPixel(x, currentLine, Color.Transparent);
            }
            else if (!isplayer && ((currentByte & 0xF0) == 0xD0) || isplayer && (currentByte & 0xF0) == 0xC0)
            {
              byte colorIndex = (byte)(currentByte & 0x0F);
              Color color = colorPalette[colorIndex];
              bitmap.SetPixel(x, currentLine, color);
            }

            x++;
          }

          currentLine++;
        }

        return bitmap;
      }
      catch (Exception)
      {

        return null;
      }
    }
    public static Tuple<Image, byte[]> CreateScreenImage(List<byte[]> _tiles, byte[] _mapData, List<Color> _colors)
    {
      var mapTiles = new List<byte[]>();
      var flatArray = new byte[320 * 160];
      for (int i = 0; i < _mapData.Length; i += 2)
      {
        int index = (_mapData[i] << 8) + _mapData[i + 1];
        byte[] tile = new byte[64];
        Array.Copy(_tiles[index], tile, 64);
        mapTiles.Add(tile);
      }
      var tempScreenBitmap = new Bitmap(320, 160);
      for (int y = 0; y < 160; y++)
      {
        for (int x = 0; x < 320; x++)
        {
          int tileX = x / 8;
          int tileY = y / 8;
          int tileIndex = tileX + (tileY * 40);
          int tilePixelX = x % 8;
          int tilePixelY = y % 8;
          int tilePixelIndex = tilePixelX + (tilePixelY * 8);
          int colorIndex = mapTiles[tileIndex][tilePixelIndex];
          flatArray[x + (y * 320)] = (byte)colorIndex;
          Color color = _colors[colorIndex % 128];
          tempScreenBitmap.SetPixel(x, y, color);
        }
      }
      return new Tuple<Image, byte[]>(tempScreenBitmap, flatArray);
    }

    public static List<byte[]> ReadScreenTiles(byte[] data)
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

    public static List<byte[]> ReadSpriteTiles(byte[] data)
    {
      const int ChunkSize = 64;
      const int NumChunks = 32;
      List<byte[]> byteArrayList = new List<byte[]>();

      int offset = 0x1e800;
      while (offset < 0x27000)
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
    public static List<byte[]> GetAllScreensBytes(string planetFolderPath)
    {
      var screenBytes = new List<byte[]>();
      var screenFiles = Directory.GetFiles(planetFolderPath, "*.bin");
      screenFiles = screenFiles.Where(f => Convert.ToInt32(f.Split('_').Last().Split('.').First()) > 5).OrderBy(f => Convert.ToInt32(f.Split('_').Last().Split('.').First())).ToArray();
      foreach (var screenFile in screenFiles)
      {
        var bytes = File.ReadAllBytes(screenFile);
        var screen = bytes.Take(1600).ToArray();
        screenBytes.Add(screen);
      }
      return screenBytes;
    }
    public static byte[] GetScreenBytes(string binFile)
    {
      var bytes = File.ReadAllBytes(binFile);
      var screenBytes = bytes.Take(0x1600).ToArray();
      return screenBytes;
    }
    public static void ExtractSlidesBin(string slidesBinFile, string outputFolder)
    {
      var chunks = ReadSlideBytes(slidesBinFile);

      Console.WriteLine(chunks.Count);
      List<Image> images = new();
      List<Image> scaledImages = new();
      foreach (var chunk in chunks)
      {
        if (chunk.Length >= 92160)
        {
          var image = DecodeDYUVImage(chunk, 384, 240);
          var scaledImage = BitmapHelper.Scale4(image);
          images.Add(image);
          scaledImages.Add(scaledImage);
        }
      }

      CreateGifFromImageList(images, Path.Combine(outputFolder, "slides.gif"), 20);
      CreateGifFromImageList(scaledImages, Path.Combine(outputFolder, "slidesX4.gif"), 40);

    }
    public static List<byte[]> ReadSlideBytes(string filePath)
    {
      const int skipSize = 0x320;
      bool skipped = false;
      const int readSize = 0x16800;

      List<byte[]> byteArrays = new List<byte[]>();

      using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
      {
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
          // Skip the specified number of bytes
          if (skipped)
          {
            reader.BaseStream.Seek(skipSize, SeekOrigin.Current);
          }
          else
          {
            reader.BaseStream.Seek(0, SeekOrigin.Current);
            skipped = true;
          }

          // Read the specified number of bytes into a byte array
          byte[] buffer = new byte[readSize];
          int bytesRead = reader.Read(buffer, 0, readSize);

          // If we reached the end of the file, resize the buffer accordingly
          if (bytesRead < readSize)
          {
            Array.Resize(ref buffer, bytesRead);
          }

          // Add the byte array to the list
          byteArrays.Add(buffer);

          // Check if we reached the end of the file
          if (bytesRead < readSize)
          {
            break;
          }
        }
      }

      return byteArrays;
    }

    public static void ReadDyuvFramesFile(string filePath, string originalImage)
    {
      var containerList = new List<DyuvFrameContainer>();
      string fileName = Path.GetFileNameWithoutExtension(filePath);
      int chunkSize = fileName.ToLower().Contains("space", StringComparison.OrdinalIgnoreCase) ? 0x3678 : (fileName.Contains("anime", StringComparison.OrdinalIgnoreCase) ? 0x3f8C : 0);

      List<DyuvFrame> chunkDataList = new List<DyuvFrame>();
      var arrays = new List<byte[]>();
      using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
      {
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
          var container = new DyuvFrameContainer();
          container.Frames = new List<DyuvFrame>();
          // Read chunkSize bytes into a byte array
          byte[] chunkBuffer = new byte[chunkSize];
          int bytesRead = reader.Read(chunkBuffer, 0, chunkSize);

          if (bytesRead < chunkSize)
          {
            continue;
          }

          container.Frames.AddRange(ProcessChunk(container, chunkBuffer));
          containerList.Add(container);
        }
      }
      CreateDyuvImages(containerList, originalImage);
    }

    public static void CreateDyuvImages(List<DyuvFrameContainer> containers, string originalImage)
    {
      var isSpace = originalImage.Contains("space", StringComparison.OrdinalIgnoreCase);
      var height = isSpace ? 240 : 144;
      var width = isSpace ? 384 : 316;
      var imageList = new List<Image>();

      // read originalImage into a byte array
      byte[] originalImageBytes = File.ReadAllBytes(originalImage).Take(92160).ToArray();
      var outputDirectory = @$"{Path.GetDirectoryName(originalImage)}\output\{Path.GetFileNameWithoutExtension(originalImage)}\";

      if (!Directory.Exists(outputDirectory))
      {
        Directory.CreateDirectory(outputDirectory);
      }

      Image initialImage = ImageFormatHelper.DecodeDYUVImage(originalImageBytes, width, originalImageBytes.Length / width, 16);
      imageList.Add(initialImage);
      initialImage.Save(@$"{outputDirectory}\{Path.GetFileNameWithoutExtension(originalImage)}_initial.png");

      foreach (var (container, index) in containers.WithIndex())
      {
        byte[] newImageBytes = new byte[originalImageBytes.Length];
        Buffer.BlockCopy(originalImageBytes, 0, newImageBytes, 0, originalImageBytes.Length);
        foreach (var frame in container.Frames)
        {
          var x = isSpace ? frame.Offset * 4 % width : frame.Offset % width;
          var y = isSpace ? frame.Offset * 4 / width : frame.Offset / width;

          var test2 = (int)GetOffset((ushort)(ushort)y, (ushort)width);

          var offset = test2 + x;
          // Create a new byte array with the size of the original image
          // Copy the original image bytes into the new byte array
          // Copy the frame bytes into the new byte array
          if (offset >= newImageBytes.Length)
          {
            continue;
          }
          Buffer.BlockCopy(frame.Bytes, 0, newImageBytes, offset, frame.Bytes.Length);
        }
        originalImageBytes = newImageBytes;
        Image newImage = ImageFormatHelper.DecodeDYUVImage(newImageBytes, width, newImageBytes.Length / width, 16);
        imageList.Add(newImage);
        newImage.Save(@$"{outputDirectory}\{(isSpace ? "space" : $"{Path.GetFileNameWithoutExtension(originalImage)}")}_{index}.png");
      }
      ImageFormatHelper.CreateGifFromImageList(imageList, @$"{outputDirectory}\{(isSpace ? "space" : $"{Path.GetFileNameWithoutExtension(originalImage)}")}.gif");
    }

    private static uint GetOffset(ushort param_1, ushort param_2)
    {
      uint uVar1 = (uint)param_1 * (uint)param_2;
      uint result = (uint)(((uVar1 >> 16) + (param_2 * (param_1 >> 16)) + (param_1 * (param_2 >> 16))) << 16) | (uVar1 & 0xFFFF);

      return result;
    }


    private static List<DyuvFrame> ProcessChunk(DyuvFrameContainer cont, byte[] chunkBuffer)
    {
      List<DyuvFrame> chunkDataList = new List<DyuvFrame>();

      byte[] bytePair = new byte[] { chunkBuffer[1], chunkBuffer[0] };
      var lines = BitConverter.ToUInt16(bytePair, 0);
      cont.Lines = lines;
      chunkBuffer = chunkBuffer.Skip(4).ToArray();
      for (int i = 0; i < cont.Lines; i++)
      {
        if (chunkBuffer.Length < 4)
        {
          break;
        }
        DyuvFrame chunkData = new DyuvFrame();
        bytePair = new byte[] { chunkBuffer[1], chunkBuffer[0] };
        chunkData.Offset = BitConverter.ToUInt16(bytePair, 0);
        // Skip the next 2 bytes
        chunkBuffer = chunkBuffer.Skip(2).ToArray();
        bytePair = new byte[] { chunkBuffer[1], chunkBuffer[0] };
        chunkData.Sets = BitConverter.ToUInt16(bytePair, 0);

        chunkBuffer = chunkBuffer.Skip(2).ToArray();
        // Read 4 * Sets bytes into a byte array
        chunkData.Bytes = chunkBuffer.Take(chunkData.Sets * 4).ToArray();
        chunkDataList.Add(chunkData);
        chunkBuffer = chunkBuffer.Skip(chunkData.Sets * 4).ToArray();
      }

      return chunkDataList;
    }

    public static void ExtractCockpitAnimation()
    {
      var spaceDataFilesPath = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\space\data";
      var spaceDataFilesOutPath = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\space\data\output";

      var imageBinFile = "space_2352_108192_d_2.bin";
      var paletteBinFile = "space_0_2352_d_1.bin";

      var cockpitScreenImageBytes = File.ReadAllBytes(Path.Combine(spaceDataFilesPath, imageBinFile)).Take(0x10e00).ToArray();
      var cockpitControlImageBytes = File.ReadAllBytes(Path.Combine(spaceDataFilesPath, imageBinFile)).Skip(0x10e00).ToArray();
      var paletteBytes = File.ReadAllBytes(Path.Combine(spaceDataFilesPath, paletteBinFile));

      var palette = ConvertBytesToRGB(paletteBytes.Take(0x300).ToArray());

      var cockpitScreenImageList = new List<Image>();
      var cockpitControlsImageList = new List<Image>();
      var paletteImageList = new List<Image>();

      var cockpitScreenImage = new Bitmap(384, 180);
      var cockpitControlsImage = new Bitmap(384, 60);

      for (int y = 0; y < 180; y++)
      {
        for (int x = 0; x < 384; x++)
        {
          var index = y * 384 + x;
          var color = palette[cockpitScreenImageBytes[index]];
          cockpitScreenImage.SetPixel(x, y, color);
        }
      }
      cockpitScreenImageList.Add(cockpitScreenImage);

      for (int y = 0; y < 60; y++)
      {
        for (int x = 0; x < 384; x++)
        {
          var index = y * 384 + x;
          var color = palette[cockpitControlImageBytes[index]];
          cockpitControlsImage.SetPixel(x, y, color);
        }
      }
      cockpitScreenImage.Save(Path.Combine(spaceDataFilesOutPath, "cockpitScreenImage_Initial.png"));
      cockpitControlsImage.Save(Path.Combine(spaceDataFilesOutPath, "cockpitControlsImage_Initial.png"));
      cockpitControlsImageList.Add(cockpitControlsImage);

      for (int i = 0; i <= 12; i++)
      {
        RotateSubset(palette, 102, 113, 1);
        ReverseRotateSubset(palette, 10, 15, 1);
        RotateSubset(palette, 16, 21, 1);
        RotateSubset(palette, 22, 27, 1);
        ReverseRotateSubset(palette, 28, 33, 1);
        ReverseRotateSubset(palette, 34, 39, 1);
        if (i < 6) { ReverseRotateSubset(palette, 114, 125, 1); } else { RotateSubset(palette, 114, 125, 1); }
        if (i < 6) { RotateSubset(palette, 87, 101, 2); } else { ReverseRotateSubset(palette, 87, 101, 2); }
        cockpitScreenImage = new Bitmap(384, 180);
        cockpitControlsImage = new Bitmap(384, 60);

        for (int y = 0; y < 180; y++)
        {
          for (int x = 0; x < 384; x++)
          {
            var index = y * 384 + x;
            var color = palette[cockpitScreenImageBytes[index]];
            cockpitScreenImage.SetPixel(x, y, color);
          }
        }
        cockpitScreenImageList.Add(cockpitScreenImage);

        for (int y = 0; y < 60; y++)
        {
          for (int x = 0; x < 384; x++)
          {
            var index = y * 384 + x;
            var color = palette[cockpitControlImageBytes[index]];
            cockpitControlsImage.SetPixel(x, y, color);
          }
        }
        cockpitScreenImage.Save(Path.Combine(spaceDataFilesOutPath + "\\separates\\", $"cockpitScreenImage_{i + 1}.png"));
        cockpitControlsImage.Save(Path.Combine(spaceDataFilesOutPath + "\\separates\\", $"cockpitControlsImage_{i + 1}.png"));
        cockpitControlsImageList.Add(cockpitControlsImage);
      }

      CreateGifFromImageList(cockpitScreenImageList, Path.Combine(spaceDataFilesOutPath, "cockpitScreenImageList.gif"), 50, 0);
      CreateGifFromImageList(cockpitControlsImageList, Path.Combine(spaceDataFilesOutPath, "cockpitControlsImageList.gif"), 50, 0);
    }

    public static Image CreateTileImage(byte[] tile, List<Color> palette, bool idOverlay = false)
    {
      if (palette.Count < 256)
      {
        var remaining = 256 - palette.Count;
        for (int i = 0; i < remaining; i++)
        {
          palette.Add(Color.FromArgb(255, 0, 0, 0));
        }
      }
      // Create an 8*8 bitmap from the tile bytes
      // Each byte is an index into the palette
      var tileImage = new Bitmap(8, 8);
      for (int y = 0; y < 8; y++)
      {
        for (int x = 0; x < 8; x++)
        {
          var index = y * 8 + x;
          var color = palette[tile[index]];
          tileImage.SetPixel(x, y, color);
        }
      }
      return tileImage;
    }

    public static Color DetermineTileBehaviour(int tileIndex)
    {

      if (((tileIndex == 0) || (0x1f < tileIndex)) && (tileIndex < 0x58 || (0x5f < tileIndex)))
      {

        if ((0x1f < tileIndex) && (tileIndex < 0x40))
        {
          // COLLISION = 1;
          return Color.Red;
        }
        if ((tileIndex == 0x40) || (tileIndex == 0x43) || (tileIndex == 0x46) ||
           tileIndex == 0x49 || (tileIndex == 0x4c))
        {
          //TRANSFORMLOOKUPSTATUS = 1;
          //CLIMBABLE = 1;
          //_DAT_800029a4 = 1;
          return Color.LimeGreen;
        }
        if ((tileIndex == 0x41) || (tileIndex == 0x44) ||
           tileIndex == 0x47 || tileIndex == 0x4a || (tileIndex == 0x4d))
        {
          //TRANSFORMLOOKUPSTATUS = 1;
          //CLIMBABLE = 1;
          //_DAT_800029a0 = 1;
          return Color.LimeGreen;
        }
        if ((tileIndex == 0x42) || (tileIndex == 0x45) || (tileIndex == 0x48) ||
           tileIndex == 0x4b || (tileIndex == 0x4e))
        {
          //TRANSFORMLOOKUPSTATUS = 1;
          //CLIMBABLE = 1;
          return Color.LimeGreen;
        }
        if ((0x5f < tileIndex) && (tileIndex < 0x63))
        {
          //_DAT_80002990 = 1;
          return Color.HotPink;
        }
        if ((0x62 < tileIndex) && (tileIndex < 0x66))
        {
          //_DAT_80002990 = 1;
          return Color.HotPink;
        }
        if ((0x65 < tileIndex) && (tileIndex < 0x69))
        {
          //_DAT_8000298c = 2;
          //_DAT_80002990 = 1;
          return Color.HotPink;
        }
        if ((tileIndex == 0x69) || (tileIndex == 0x6d))
        {
          //TRANSFORMLOOKUPSTATUS = 1;
          //_DAT_8000297c = 1;
          return Color.Orange;
        }
        if ((tileIndex == 0x6a) || (tileIndex == 0x6e))
        {
          //TRANSFORMLOOKUPSTATUS = 1;
          //_DAT_80002978 = 1;
          return Color.Orange;
        }
        if ((tileIndex == 0x6b) || (tileIndex == 0x6f))
        {
          //TRANSFORMLOOKUPSTATUS = 1;
          return Color.Orange;
        }
        if ((tileIndex == 0x6c) || (tileIndex == 0x4f))
        {
          //TRANSFORMLOOKUPSTATUS = 1;
          return Color.RoyalBlue;
        }
        if ((0x4f < tileIndex) && (tileIndex < 0x56))
        {
          //TRANSFORMLOOKUPSTATUS = 1;
          //CONDITION2 = 1;
          return Color.RoyalBlue;
        }
        if ((0x55 < tileIndex) && (tileIndex < 0x58))
        {
          //DAT_80002980 = 1;
          return Color.RoyalBlue;
        }
        if ((tileIndex == 0x70) || (tileIndex == 0x71))
        {
          //TRANSFORMLOOKUPSTATUS = 1;
          return Color.Orange;
        }
        if ((tileIndex == 0x72) || (tileIndex == 0x73))
        {
          //TRANSFORMLOOKUPSTATUS = 1;
          //_DAT_80002968 = 1;
          return Color.RoyalBlue;
        }
        if ((tileIndex == 0x78) || (tileIndex == 0x79))
        {
          //TRANSFORMLOOKUPSTATUS = 1;
          return Color.RoyalBlue;
        }
        if ((0x73 < tileIndex) && (tileIndex < 0x77))
        {
          return Color.RoyalBlue;
        }
        if (tileIndex == 0x77)
        {
          return Color.RoyalBlue;
        }
        else
        {
          if ((0x7a < tileIndex) && (tileIndex < 0x80))
          {
            return Color.RoyalBlue;
          }
          if (tileIndex != 0x7a)
          {
            return Color.Coral;
          }
        }
      }
      return Color.Cyan;
    }

    public static void ExtractInventory()
    {
      var llInventory = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\Exploration\Inventory\argos.rtf_1_4_172.bin");

      var palBytes = llInventory.Take(0x80).ToArray();
      var palette = ReadPalette(palBytes);

      var imageBytes = llInventory.Skip(0x80).Take(0x8000).ToArray();
      var tileList = new List<byte[]>();

      for (int i = 0; i < 0x8000; i += 64)
      {
        tileList.Add(imageBytes.Skip(i).Take(64).ToArray());
      }

      for (int i = 0; i < tileList.Count; i += 64)
      {
        if (i <= 320)
        {
          var listOfQuads = tileList.Skip(i).Take(64).ToList();
          // image is made up of 4 tiles, each tile is 8x8 pixels
          for (int j = 0; j < 32; j += 2)
          {
            var quadTopLeft = listOfQuads[j];
            var quadTopRight = listOfQuads[j + 1];
            var quadBottomLeft = listOfQuads[j + 32];
            var quadBottomRight = listOfQuads[j + 33];
            var combinedQuads = new byte[256];
            for (int x = 0; x < 16; x++)
            {
              for (int y = 0; y < 16; y++)
              {
                if (x < 8 && y < 8)
                {
                  combinedQuads[x + y * 16] = quadTopLeft[x + y * 8];
                }
                else if (x >= 8 && y < 8)
                {
                  combinedQuads[x + y * 16] = quadTopRight[x - 8 + y * 8];
                }
                else if (x < 8 && y >= 8)
                {
                  combinedQuads[x + y * 16] = quadBottomLeft[x + (y - 8) * 8];
                }
                else if (x >= 8 && y >= 8)
                {
                  combinedQuads[x + y * 16] = quadBottomRight[x - 8 + (y - 8) * 8];
                }
              }
            }
            var image = ImageFormatHelper.GenerateClutImage(palette, combinedQuads, 16, 16, true);
            image.Save($@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\Exploration\Inventory\output\combined\{i}_{j}.png");
          }
        }
        else if (i > 320)
        {
          // Song Icons which are 16 tiles, each icon is 4x4 tiles, each tile is 8x8 pixels
          var listOfQuads = tileList.Skip(i).Take(256).ToList();
        }
      }
    }
  }
}

// var framesFile = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\space\video\space_v_1_0_QHY_Normal_3.bin";
// var originalImage = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\space\video\space_v_1_0_QHY_Normal_2.bin";

// LaserLordsHelper.ReadDyuvFramesFile(framesFile, originalImage);

// var fontfile = @"C:\Dev\Projects\Gaming\CD-i\AIW\ALICE IN WONDERLAND\records\atnc24cl\data\atnc24cl.ai1";

// var bytes = File.ReadAllBytes(fontfile).Skip(0x44).Take(0x24).ToArray();

// var fontFileData = new CdiFontFile(bytes);

// Console.WriteLine($"Found file data: ");

// var luxor9 = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\luxor\data\luxor_d_9.bin";

// var screen = LaserLordsHelper.GetScreenBytes(luxor9);

// var animationFrames = new List<Bitmap>();


// animationFrames.Add(screenImage);

// for (int i = 0; i < 3; i++)
// {
//   ColorHelper.RotateSubset(palette, 85, 88, 1);
//   screenImage = LaserLordsHelper.CreateScreenImage(tiles, screen, palette);

//   animationFrames.Add(screenImage);
// }



// using (var gifWriter = new GifWriter(@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\luxor\data\output\luxor_9_1000.gif", 1000, 0))
// {
//   foreach (var cockpitImage in animationFrames)
//   {
//     gifWriter.WriteFrame(cockpitImage);
//   }
// }

// var tileBytes = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\records\argos\data\argos_d_5.bin");
// var palettOffset1 = 0x10004;
// var paletteOffset2 = 0x10108;

// var paletteBytes = tileBytes.Skip(palettOffset1).Take(0x100).ToArray();
// paletteBytes = paletteBytes.Concat(tileBytes.Skip(paletteOffset2).Take(0x100)).ToArray();
// var palette = ColorHelper.ReadPalette(paletteBytes);
// // var screens = LaserLordsHelper.GetAllScreensBytes(@"C:\Dev\Personal\Projects\Gaming\CD-i\Extracted\Laser Lords\NewRecords\argos\data-eor\output\");
// var tiles = LaserLordsHelper.ReadScreenTiles(tileBytes);

// foreach (var (tile, index) in tiles.WithIndex())
// {
//   var tileImage = LaserLordsHelper.CreateTileImage(tile, palette);
//   tileImage.Save($@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\records\argos\data\output\tiles\argos_{index}.png");
// }

// foreach (var (screen, index) in screens.WithIndex())
// {
//   var screenImage = LaserLordsHelper.CreateScreenImage(tiles, screen, palette);
//   screenImage.Save($@"C:\Dev\Personal\Projects\Gaming\CD-i\Extracted\Laser Lords\NewRecords\argos\data-eor\output\screens\argos_{index + 5}.png");
// }


// var tileFile = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\Output\hive\hive.rtf_1_1_111.bin";
// var tileBytes = File.ReadAllBytes(tileFile);

// var screenBytes = tileBytes.Skip(0x10000).ToArray();
// var screens = new List<byte[]>();

// for (int i = 0; i < screenBytes.Length; i += 0x800)
// {
//   screens.Add(screenBytes.Skip(i).Take(0x800).ToArray());
// }

// var tiles = LaserLordsHelper.ReadScreenTiles(tileBytes);

// var paletteFile = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\Output\hive\hive.rtf_1_2_143.bin";

// var paletteBytes = File.ReadAllBytes(paletteFile).Take(0x208).ToArray();

// var palette = ReadClutBankPalettes(paletteBytes, 2);

// var outputPath = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\Output\hive\screens\slgifs\";
// var tileOutputPath = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\Output\hive\tiles\tinted\";

// Directory.CreateDirectory(outputPath);
// Directory.CreateDirectory(tileOutputPath);

// var images = new List<Image>();

// // foreach (var (screen, index) in screens.WithIndex())
// // {
// //   for (int i = 0; i < 20; i++)
// //   {
// //     var image = LaserLordsHelper.CreateScreenImage(tiles, screen, palette);
// //     images.Add(image);
// //     RotateSubset(palette, 80, 83, 1);
// //     if (i % 2 == 0) RotateSubset(palette, 84, 85, 1);
// //     RotateSubset(palette, 86, 87, 1);
// //     RotateSubset(palette, 98, 101, 1);
// //     RotateSubset(palette, 111, 120, 1);
// //   }

// //   // create gif from these images, then clear the list for the next batch
// //   CreateGifFromImageList(images, Path.Combine(outputPath, $"hive_{index}.gif"));
// //   images.Clear();
// // }

// foreach (var (tile, index) in tiles.WithIndex())
// {
//   var image = LaserLordsHelper.CreateTileImage(tile, palette);
//   if (index < 128)
//   {
//     var colour = LaserLordsHelper.DetermineTileBehaviour(index);
//     image = BitmapHelper.TintImage(image, colour);
//   }
//   image.Save(Path.Combine(tileOutputPath, $"hive_{index}.png"), ImageFormat.Png);
//   // images.Add(image);
//   // RotateSubset(palette, 80, 83, 1);
//   // RotateSubset(palette, 84, 85, 1);
//   // RotateSubset(palette, 86, 88, 1);
//   // RotateSubset(palette, 90, 92, 1);
//   // image = LaserLordsHelper.CreateTileImage(tile, palette);
//   // image.Save(Path.Combine(tileOutputPath, $"hive_{index + 1024}.png"), ImageFormat.Png);
//   // images.Add(image);
//   // RotateSubset(palette, 80, 83, 1);
//   // RotateSubset(palette, 84, 85, 1);
//   // RotateSubset(palette, 86, 88, 1);
//   // RotateSubset(palette, 90, 92, 1);
//   // image = LaserLordsHelper.CreateTileImage(tile, palette);
//   // image.Save(Path.Combine(tileOutputPath, $"hive_{index + 1024 * 2}.png"), ImageFormat.Png);
//   // images.Add(image);
//   // RotateSubset(palette, 80, 83, 1);
//   // RotateSubset(palette, 84, 85, 1);
//   // RotateSubset(palette, 86, 88, 1);
//   // RotateSubset(palette, 90, 92, 1);
//   // image = LaserLordsHelper.CreateTileImage(tile, palette);
//   // image.Save(Path.Combine(tileOutputPath, $"hive_{index + 1024 * 3}.png"), ImageFormat.Png);
//   // images.Add(image);

//   // // create gif from these images, then clear the list for the next batch
//   // var tileGifOutputDir = Path.Combine(tileOutputPath, "gifs");
//   // Directory.CreateDirectory(tileGifOutputDir);
//   // CreateGifFromImageList(images, Path.Combine(tileGifOutputDir, $"hive_{index}.gif"));
//   // images.Clear();
// }


// var ravannaRtf = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\tekton.rtf";

// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\Analysis\SprOutput2\tekton
// ";

// Directory.CreateDirectory(outputFolder);

// var ravannaCdi = new CdiFile(ravannaRtf);

// var dataSectors = ravannaCdi.DataSectors.Where(s => s.Channel == 2).OrderBy(s => s.SectorIndex).Skip(1).ToList();

// var tempSectorList = new List<CdiSector>();

// foreach (var (sector, index) in dataSectors.WithIndex())
// {
//   tempSectorList.Add(sector);
//   if (sector.SubMode.IsEOR)
//   {
//     var ravannaSpriteData = tempSectorList.SelectMany(s => s.GetSectorData()).ToArray();

//     var ravannaPalette = ReadPalette(ravannaSpriteData.Skip(0x20).Take(0x40).ToArray());

//     var offsetData = ravannaSpriteData.Skip(0x60).Take(0xe).ToArray();

//     var offsets = new List<int>();

//     for (int i = 0; i < offsetData.Length; i += 2)
//     {
//       var offset = BitConverter.ToUInt16(offsetData.Skip(i).Take(2).Reverse().ToArray(), 0);
//       offsets.Add(offset);
//     }

//     var data = ravannaSpriteData.Skip(0x6e).ToArray();

//     var blobs = new List<byte[]>();

//     for (int i = 0; i < offsets.Count; i++)
//     {
//       var start = offsets[i];
//       var end = i == offsets.Count - 1 ? offsets[i] : offsets[i + 1];
//       var blob = data.Skip(start).Take(end - start).ToArray();
//       blobs.Add(blob);
//     }


//     foreach (var (blob, bIndex) in blobs.WithIndex())
//     {
//       if (blob.Length == 0) continue;
//       var decodedBlob = DecodeImage(blob);
//       var image = CreateBitmapFromImageLines(decodedBlob, ravannaPalette, false);
//       var outputName = Path.Combine(outputFolder, $"{sector.SectorIndex}_{bIndex}.png");
//       if (image != null && OperatingSystem.IsWindowsVersionAtLeast(6, 1))
//       {
//         image.Save(outputName, ImageFormat.Png);
//       }
//     }
//     dataSectors = dataSectors.Skip(tempSectorList.Count).ToList();
//     tempSectorList.Clear();
//   }
// }
