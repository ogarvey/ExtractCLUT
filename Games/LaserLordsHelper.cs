using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using static ExtractCLUT.Utils;
using static ExtractCLUT.Helpers.ColorHelper;
using ExtractCLUT.Models;
using ExtractCLUT.Writers;
using Color = System.Drawing.Color;

namespace ExtractCLUT.Games
{
  public static class LaserLordsHelper
  {

    public static Bitmap CreateScreenImage(List<byte[]> _tiles, byte[] _mapData, List<Color> _colors)
    {
      var mapTiles = new List<byte[]>();
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
          Color color = _colors[colorIndex];
          tempScreenBitmap.SetPixel(x, y, color);
        }
      }
      return tempScreenBitmap;
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
    public static byte[] GetScreenBytes(string binFile) {
      var bytes = File.ReadAllBytes(binFile);
      var screenBytes = bytes.Take(0x1600).ToArray();
      return screenBytes;
    }
    public static void ExtractSlidesBin(string slidesBinFile, string outputFolder)
    {
      var chunks = ReadSlideBytes(slidesBinFile);

      Console.WriteLine(chunks.Count);
      List<Bitmap> images = new();
      List<Bitmap> scaledImages = new();
      foreach (var chunk in chunks)
      {
        if (chunk.Length >= 92160)
        {
          var image = ImageFormatHelper.DecodeDYUVImage(chunk, 384, 240);
          var scaledImage = BitmapHelper.Scale4(image);
          images.Add(image);
          scaledImages.Add(scaledImage);
        }
      }

      ConvertBitmapsToGif(images, @$"{outputFolder}\slides.gif");
      ConvertBitmapsToGif(scaledImages, @$"{outputFolder}\slides4x.gif");

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
      var height = originalImage.Contains("space", StringComparison.OrdinalIgnoreCase) ? 240 : 144;
      var width = originalImage.Contains("space", StringComparison.OrdinalIgnoreCase) ? 384 : 316;
      var imageList = new List<Bitmap>();
      var imageListX4 = new List<Bitmap>();
      // read originalImage into a byte array
      byte[] originalImageBytes = File.ReadAllBytes(originalImage).Take(92160).ToArray();

      Bitmap initialImage = ImageFormatHelper.DecodeDYUVImage(originalImageBytes, width, originalImageBytes.Length / width);
      imageList.Add(initialImage);
      initialImage.Save(@$"{Path.GetDirectoryName(originalImage)}\output\{Path.GetFileNameWithoutExtension(originalImage)}_initial.png");

      Bitmap initialImageX4 = BitmapHelper.Scale4(ImageFormatHelper.DecodeDYUVImage(originalImageBytes, width, originalImageBytes.Length / width));
      imageListX4.Add(initialImageX4);
      initialImageX4.Save(@$"{Path.GetDirectoryName(originalImage)}\output\x4\{Path.GetFileNameWithoutExtension(originalImage)}_initial_X4.png");

      foreach (var (container, index) in containers.WithIndex())
      {
        byte[] newImageBytes = new byte[originalImageBytes.Length];
        Buffer.BlockCopy(originalImageBytes, 0, newImageBytes, 0, originalImageBytes.Length);
        foreach (var frame in container.Frames)
        {
          var x = originalImage.Contains("space", StringComparison.OrdinalIgnoreCase) ? (frame.Offset * 4) % width : (frame.Offset) % width;
          var y = originalImage.Contains("space", StringComparison.OrdinalIgnoreCase) ? (frame.Offset * 4) / width : (frame.Offset) / width;

          var test2 = (int)GetOffset((ushort)((ushort)y), (ushort)width);

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
        Bitmap newImage = ImageFormatHelper.DecodeDYUVImage(newImageBytes, width, newImageBytes.Length / width);
        imageList.Add(newImage);
        newImage.Save(@$"{Path.GetDirectoryName(originalImage)}\output\space_{index}.png");
        Bitmap newImageX4 = BitmapHelper.Scale4(ImageFormatHelper.DecodeDYUVImage(newImageBytes, width, newImageBytes.Length / width));
        imageListX4.Add(newImageX4);
        newImageX4.Save(@$"{Path.GetDirectoryName(originalImage)}\output\x4\space_{index}_X4.png");
      }
      using (var gifWriter = new GifWriter(@$"{Path.GetDirectoryName(originalImage)}\output\space.gif", 100, -1))
      {
        foreach (var image in imageList)
        {
          gifWriter.WriteFrame(image);
        }
      }
      using (var gifWriter = new GifWriter(@$"{Path.GetDirectoryName(originalImage)}\output\x4\space.gif", 100, -1))
      {
        foreach (var image in imageListX4)
        {
          gifWriter.WriteFrame(image);
        }
      }
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

      var palette = ColorHelper.ConvertBytesToRGB(paletteBytes.Take(0x300).ToArray());

      var cockpitScreenImageList = new List<Bitmap>();
      var cockpitControlsImageList = new List<Bitmap>();
      var paletteImageList = new List<Bitmap>();

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

      using (var gifWriter = new GifWriter(Path.Combine(spaceDataFilesOutPath, "cockpitScreenImageList.gif"), 500, 0))
      {
        foreach (var cockpitImage in cockpitScreenImageList)
        {
          gifWriter.WriteFrame(cockpitImage);
        }
      }


    }
  }
}
