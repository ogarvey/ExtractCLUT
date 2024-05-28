using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ExtractCLUT.Model;
using static ExtractCLUT.Utils;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;
using SLImage = SixLabors.ImageSharp.Image;
using Rectangle = System.Drawing.Rectangle;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using static ExtractCLUT.Games.LaserLordsHelper;
using System.Collections.Concurrent;

namespace ExtractCLUT.Helpers
{
  public static class ImageFormatHelper
  {

    public static void UpdateImageAtCoords(byte[] largerImage, int largerWidth, int largerHeight, byte[] smallerImage, int smallerWidth, int smallerHeight, int x, int y)
    {
      if (x + smallerWidth > largerWidth || y + smallerHeight > largerHeight)
      {
        throw new ArgumentException("The smaller image does not fit within the bounds of the larger image at the specified coordinates.");
      }

      int bytesPerPixel = 4; // Assuming 32-bit RGBA image

      for (int j = 0; j < smallerHeight; j++)
      {
        int largerIndex = ((y + j) * largerWidth + x) * bytesPerPixel;
        int smallerIndex = j * smallerWidth * bytesPerPixel;

        // Copy one row from the smaller image to the larger image
        Array.Copy(smallerImage, smallerIndex, largerImage, largerIndex, smallerWidth * bytesPerPixel);
      }
    }

    public static Bitmap CombineImages(List<Image> images, int imageWidth, int imageHeight, int finalWidth, int finalHeight)
    {
      if (images == null || images.Count == 0)
      {
        throw new ArgumentException("The list of images cannot be null or empty.");
      }

      // Create a new bitmap with the desired final dimensions
      Bitmap finalImage = new Bitmap(finalWidth, finalHeight);

      using (Graphics g = Graphics.FromImage(finalImage))
      {
        // Clear the bitmap with a transparent background
        g.Clear(Color.Transparent);

        // Calculate how many images fit horizontally and vertically
        int imagesPerRow = finalWidth / imageWidth;
        int imagesPerColumn = finalHeight / imageHeight;

        // Check if there are enough images to fill the final image
        if (images.Count > imagesPerRow * imagesPerColumn)
        {
          throw new ArgumentException("The list contains more images than can fit in the final image dimensions.");
        }

        // Draw each image into the corresponding position
        for (int i = 0; i < images.Count; i++)
        {
          // Calculate the X and Y position for each image
          int xPos = (i % imagesPerRow) * imageWidth;
          int yPos = (i / imagesPerRow) * imageHeight;

          if (images[i].Width != imageWidth || images[i].Height != imageHeight)
          {
            throw new ArgumentException($"All images must be {imageWidth} pixels wide and {imageHeight} pixels tall.");
          }

          // Draw the image onto the final image
          g.DrawImage(images[i], xPos, yPos, imageWidth, imageHeight);
        }
      }

      return finalImage;
    }

    public static Bitmap CombineImages(List<Image> images, int width, int heightPerImage, int finalHeight)
    {
      if (images == null || images.Count != 4)
      {
        throw new ArgumentException("The list must contain exactly 4 images.");
      }

      // Create a new bitmap with the desired final dimensions
      Bitmap finalImage = new Bitmap(width, finalHeight);

      using (Graphics g = Graphics.FromImage(finalImage))
      {
        // Clear the bitmap with a transparent background
        g.Clear(Color.Transparent);

        // Draw each image into the corresponding position
        for (int i = 0; i < images.Count; i++)
        {
          if (images[i].Width != width || images[i].Height != heightPerImage)
          {
            throw new ArgumentException("All images must be 20 pixels wide and 5 pixels tall.");
          }

          // Calculate the Y position for each image
          int yPos = i * heightPerImage;

          // Draw the image onto the final image
          g.DrawImage(images[i], 0, yPos, width, heightPerImage);
        }
      }

      return finalImage;
    }

    public static List<ImageLine> DecodeImage(byte[] data, byte lineMarker, int width)
    {
      List<ImageLine> imageLines = new List<ImageLine>();
      int i = 0;

      while (i < data.Length - 1)
      {

        ImageLine line = new ImageLine();
        if (data[i] == 0x00 && data[i + 1] != lineMarker)
        {
          line.LeftOffset = data[i + 1];
          i += 2;
        }
        else if (data[i] == 0x00 && data[i + 1] == lineMarker)
        {
          line.LeftOffset = 0;
          line.Pixels = new List<byte>();
          for (int j = 0; j < width; j++)
          {
            line.Pixels.Add(0x00);
            if (line.LeftOffset + line.Pixels.Count == width)
            {
              break;
            }
          }
        }
        else
        {
          line.LeftOffset = 0;
        }
        line.Pixels = new List<byte>();

        while (i < data.Length - 1)
        {
          if (line.LeftOffset + line.Pixels.Count == width)
          {
            imageLines.Add(line);
            break;
          }
          if (data[i] == 0x00)
          {
            if (line.LeftOffset + line.Pixels.Count == width)
            {
              imageLines.Add(line);
              break;
            }
            if (i + 2 < data.Length && data[i + 2] == 0x00)
            {
              line.RemainingPixels = data[i + 1];
              i += 2;
              if (line.LeftOffset + line.Pixels.Count == width)
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
                if (line.LeftOffset + line.Pixels.Count == width)
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
            if (line.LeftOffset + line.Pixels.Count == width)
            {
              imageLines.Add(line);
              break;
            }
          }

          if (i >= data.Length - 1 || ((line.LeftOffset + line.Pixels.Count) == width))
          {
            imageLines.Add(line);
            break;
          }
        }

        imageLines.Add(line);

      }

      return imageLines;
    }
    public static void CreateMultiBgGifFromImageList(List<Image> images, string outputPath, int delay = 10, int repeat = 0, List<Image>? backgroundFrames = null, List<int>? frameStarts = null)
    {
      Image<Rgba32>? background = null;
      int backgroundIndex = 0;

      using SLImage gif = new Image<Rgba32>(backgroundFrames?.FirstOrDefault()?.Width ?? images[0].Width, backgroundFrames?.FirstOrDefault()?.Height ?? images[0].Height);
      var gifMetaData = gif.Metadata.GetGifMetadata();
      gifMetaData.RepeatCount = (ushort)repeat;

      GifFrameMetadata firstFrameMetadata = gif.Frames.RootFrame.Metadata.GetGifMetadata();
      firstFrameMetadata.FrameDelay = delay;
      firstFrameMetadata.DisposalMethod = GifDisposalMethod.NotDispose;

      for (int i = 0; i < images.Count; i++)
      {
        var image = images[i];

        // Determine which background to use
        if (backgroundFrames != null && frameStarts != null && backgroundIndex < frameStarts.Count && i >= frameStarts[backgroundIndex])
        {
          if (backgroundIndex < backgroundFrames.Count)
          {
            var bytes = backgroundFrames[backgroundIndex].ImageToBytes(); // Assuming ImageToBytes() correctly converts the image to a byte array
            background = SLImage.Load<Rgba32>(bytes);
          }
          backgroundIndex++;
        }

        var imageBytes = image.ImageToBytes();
        using (SLImage frameImage = SLImage.Load<Rgba32>(imageBytes))
        {
          using (SLImage frame = new SixLabors.ImageSharp.Image<Rgba32>(backgroundFrames?.FirstOrDefault()?.Width ?? images[0].Width, backgroundFrames?.FirstOrDefault()?.Height ?? images[0].Height))
          {
            if (background != null)
            {
              frame.Mutate(ctx => ctx.DrawImage(background, 1f));
            }
            frame.Mutate(ctx => ctx.DrawImage(frameImage, 1f));

            GifFrameMetadata frameMetadata = frame.Frames.RootFrame.Metadata.GetGifMetadata();
            frameMetadata.FrameDelay = delay;
            frameMetadata.DisposalMethod = GifDisposalMethod.RestoreToBackground;

            gif.Frames.AddFrame(frame.Frames.RootFrame);
          }
        }
      }

      gif.Frames.RemoveFrame(0); // Remove the initial empty frame
      gif.SaveAsGif(outputPath); // Save the final result
    }

    public static void CreateGifFromImageList(List<Image> images, string outputPath, int delay = 10, int repeat = 0, Image? backgroundFrame = null)
    {
      Image<Rgba32>? background = null;
      if (backgroundFrame != null)
      {
        var bytes = backgroundFrame.ImageToBytes();
        background = SLImage.Load<Rgba32>(bytes);
      }

      using SLImage gif = new SixLabors.ImageSharp.Image<Rgba32>(backgroundFrame?.Width ?? images[0].Width, backgroundFrame?.Height ?? images[0].Height);
      var gifMetaData = gif.Metadata.GetGifMetadata();
      gifMetaData.RepeatCount = (ushort)repeat;

      GifFrameMetadata firstFrameMetadata = gif.Frames.RootFrame.Metadata.GetGifMetadata();
      firstFrameMetadata.FrameDelay = delay;

      // If the first frame is to be used as background, set its disposal method accordingly.
      firstFrameMetadata.DisposalMethod = backgroundFrame != null ? GifDisposalMethod.NotDispose : GifDisposalMethod.RestoreToBackground;


      foreach (var (image, index) in images.WithIndex())
      {
        var bytes = image.ImageToBytes();
        using (SLImage frameImage = SLImage.Load<Rgba32>(bytes))
        {
          // Create a new frame by compositing the current image over the background.
          using (SLImage frame = new SixLabors.ImageSharp.Image<Rgba32>(backgroundFrame?.Width ?? images[0].Width, backgroundFrame?.Height ?? images[0].Height))
          {
            if (background != null)
            {
              frame.Mutate(ctx => ctx.DrawImage(background, 1f));
            }
            frame.Mutate(ctx => ctx.DrawImage(frameImage, 1f));

            // Set the delay and disposal method for each frame.
            GifFrameMetadata frameMetadata = frame.Frames.RootFrame.Metadata.GetGifMetadata();
            frameMetadata.FrameDelay = delay;
            frameMetadata.DisposalMethod = GifDisposalMethod.RestoreToBackground;

            // Add the frame to the gif.
            gif.Frames.AddFrame(frame.Frames.RootFrame);
          }
        }
      }

      gif.Frames.RemoveFrame(0);
      // Save the final result.
      gif.SaveAsGif(outputPath);

    }

    public static byte[] ImageToBytes(this Image img)
    {
      using (var stream = new MemoryStream())
      {
        img.Save(stream, ImageFormat.Png);
        return stream.ToArray();
      }
    }
    public static void Rle7_AllBytes(byte[] rleData, List<Color> palette, int width, int heightPerImage, List<Bitmap> images)
    {
      List<byte[]> lines = new List<byte[]>();
      List<byte> currentLine = new List<byte>();
      //initialize variables
      int nrRLEData = rleData.Length;
      int inputIndex = 0;
      int initialIndex;

      //decode RLE7
      while (inputIndex < nrRLEData)
      {
        initialIndex = inputIndex;
        //get run count
        if (inputIndex >= nrRLEData) { break; }
        byte firstByte = rleData[inputIndex++];
        bool isRun = (firstByte & 0x80) != 0; // Check if the MSB is set
        byte colorIndex = (byte)(firstByte & 0x7F); // Extract color index (7 bits)

        if (isRun)
        {
          if (inputIndex + 1 >= rleData.Length)
          {
            break;
          }

          byte runLength = rleData[inputIndex + 1];
          inputIndex += 2;

          if (runLength == 1)
          {
            //throw new Exception("Invalid RLE data: Run length of 1 is forbidden.");
            continue;
          }

          int addLength = (runLength == 0) ? (width - currentLine.Count) : runLength;

          if (currentLine.Count + addLength > width)
          {
            addLength = width - currentLine.Count;
          }

          currentLine.AddRange(Enumerable.Repeat(colorIndex, addLength));
        }
        else // Single pixel
        {
          currentLine.Add(colorIndex);
          inputIndex++;
        }

        if (currentLine.Count == width)
        {
          lines.Add(currentLine.ToArray());
          currentLine.Clear();
        }
      }

      // Add the last line if not empty
      if (currentLine.Count > 0)
      {
        lines.Add(currentLine.ToArray());
      }
      if (lines.Count == heightPerImage)
      {
        var image = GenerateRle7Image(palette, lines.SelectMany(l => l).ToArray(), width, heightPerImage, true);
        images.Add(image);
        lines.Clear();
      }
    }

    private readonly static int[] dequantizer = { 0, 1, 4, 9, 16, 27, 44, 79, 128, 177, 212, 229, 240, 247, 252, 255 };

    public static Bitmap DecodeDYUVImage(byte[] encodedData, int Width, int Height, uint Y = 128, uint U = 128, uint V = 128, bool useArray = false, byte[]? yuvArray = null)
    {
      yuvArray = yuvArray ?? new byte[0];
      int encodedIndex = 0;                               //reader index
      int width = Width, height = Height;                      //output dimensions
      byte[] decodedImage = new byte[width * height * 4]; //decoded image array
      uint initialY = Y;    //initial Y value (per line)
      uint initialU = U;    //initial U value (per line)
      uint initialV = V;    //initial V value (per line)

      //loop through all output lines
      for (int y = 0; y < height; y++)
      {
        if (useArray && height * 3 > yuvArray.Length)
        {
          break;
        }
        //re-initialize previous YUV value
        uint prevY = useArray ? yuvArray[y * 3] : initialY;
        uint prevU = useArray ? yuvArray[y * 3 + 1] : initialU;
        uint prevV = useArray ? yuvArray[y * 3 + 2] : initialV;

        //loop through each pixel in line
        for (int x = 0; x < width; x += 2)
        {
          if (encodedIndex >= encodedData.Length || encodedIndex + 1 >= encodedData.Length)
          {
            break;
          }
          //read DYUV value from source
          int encodedPixel = ((encodedData[encodedIndex] << 8) | encodedData[encodedIndex + 1]);

          //parse encoded pixel to each delta value
          byte dU1 = (byte)((encodedPixel & 0xF000) >> 12);
          byte dY1 = (byte)((encodedPixel & 0x0F00) >> 8);
          byte dV1 = (byte)((encodedPixel & 0x00F0) >> 4);
          byte dY2 = (byte)(encodedPixel & 0x000F);

          //dequantize dYUV to YUV
          var Yout1 = (prevY + dequantizer[dY1]) % 256;
          var Uout2 = (prevU + dequantizer[dU1]) % 256;   //Uout2 is the output when dequantizing
          var Vout2 = (prevV + dequantizer[dV1]) % 256;   //Vout2 is the output when dequantizing
          var Yout2 = (Yout1 + dequantizer[dY2]) % 256;   //Yout2 is based on You1, not prevY

          //interpolate U and V to double resolution and determine UVout1
          var Uout1 = (prevU + Uout2) / 2;
          var Vout1 = (prevV + Vout2) / 2;

          //store latest YUV values for next iteration
          prevY = (uint)Yout2;
          prevU = (uint)Uout2;
          prevV = (uint)Vout2;

          //decode each YUV set to RGB
          (int R1, int G1, int B1) = YUVtoRGB((int)Yout1, (int)Uout1, (int)Vout1);
          (int R2, int G2, int B2) = YUVtoRGB((int)Yout2, (int)Uout2, (int)Vout2);

          //write RGB to output array
          int decodedIndex = (y * width + x) * 4; //each iteration there are 2 pixels decoded, therefor the index moves 8 steps
          decodedImage[decodedIndex + 0] = (byte)B1;
          decodedImage[decodedIndex + 1] = (byte)G1;
          decodedImage[decodedIndex + 2] = (byte)R1;
          decodedImage[decodedIndex + 3] = 0xff;
          decodedImage[decodedIndex + 4] = (byte)B2;
          decodedImage[decodedIndex + 5] = (byte)G2;
          decodedImage[decodedIndex + 6] = (byte)R2;
          decodedImage[decodedIndex + 7] = 0xff;

          //increment reader
          encodedIndex += 2;
        }
      }

      //create bitmap from RGB array
      Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
      BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
      Marshal.Copy(decodedImage, 0, bitmapData.Scan0, decodedImage.Length);
      bitmap.UnlockBits(bitmapData);

      return bitmap;
    }

    private static (int R, int G, int B) YUVtoRGB(int Y, int U, int V)
    {
      //added additional parenthesis to ensure "/256" is done last
      int R = Clamp((Y * 256 + 351 * (V - 128)) / 256);
      int G = Clamp(((Y * 256) - (86 * (U - 128) + 179 * (V - 128))) / 256);
      int B = Clamp((Y * 256 + 444 * (U - 128)) / 256);

      return (R, G, B);
    }

    private static int Clamp(int value)
    {
      if (value < 0) { return 0; }
      if (value > 255) { return 255; }
      return value;
    }

    public static Image CropImage(Image img, int width, int height, int originX = 0, int originY = 0)
    {
      Rectangle cropRect = new Rectangle(originX, originY, width, height);
      Bitmap bmpImage = new Bitmap(img);
      Bitmap bmpCrop = bmpImage.Clone(cropRect, bmpImage.PixelFormat);
      return bmpCrop;
    }
    public static void CropImageFolder(string folderPath, string extension, int originX, int originY, int width, int height, bool createOutputFolder = true, bool renameFiles = false)
    {
      string[] imageFiles = Directory.GetFiles(folderPath, extension); // Change the extension as required
      var outputFolder = createOutputFolder ? Path.Combine(folderPath, "Cropped") : folderPath;
      Directory.CreateDirectory(outputFolder);
      foreach (string imagePath in imageFiles)
      {
        // Load the image
        using (Bitmap originalImage = new Bitmap(imagePath))
        {
          // Create rectangle for cropping
          Rectangle cropRect = new Rectangle(originX, originY, width, height);

          // Crop the image
          using (Bitmap croppedImage = originalImage.Clone(cropRect, originalImage.PixelFormat))
          {
            // Save the cropped image with "_icon" suffix
            string fileName = Path.GetFileNameWithoutExtension(imagePath);
            string fileExtension = Path.GetExtension(imagePath);
            string newFileName = renameFiles ? $"{fileName}_icon{fileExtension}" : Path.GetFileName(imagePath);
            string savePath = Path.Combine(outputFolder, newFileName);

            croppedImage.Save(savePath);
          }
        }
      }
    }
    public static bool IsImageFullyTransparent(Bitmap image)
    {
      for (int x = 0; x < image.Width; x++)
      {
        for (int y = 0; y < image.Height; y++)
        {
          if (image.GetPixel(x, y).A != 0)
          {
            return false;
          }
        }
      }
      return true;
    }

    public static (int maxWidth, int maxHeight) FindMaxDimensions(string folderPath)
    {
      ConcurrentBag<int> maxWidths = new ConcurrentBag<int>();
      ConcurrentBag<int> maxHeights = new ConcurrentBag<int>();

      // Ensure the directory exists
      if (!Directory.Exists(folderPath))
      {
        Console.WriteLine("Directory does not exist.");
        return (0, 0);
      }

      Parallel.ForEach(Directory.GetFiles(folderPath, "*.png"), (file) =>
      {
        try
        {
          using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
          using (BinaryReader br = new BinaryReader(fs))
          {
            // Skip to 16 bytes where the width is located
            br.BaseStream.Seek(16, SeekOrigin.Begin);

            // Read width and height from the file
            int width = ReadBigEndianInt32(br);
            int height = ReadBigEndianInt32(br);

            // Store in concurrent collections
            maxWidths.Add(width);
            maxHeights.Add(height);
          }
        }
        catch (Exception ex)
        {
          // Handle potential exceptions
          Console.WriteLine($"Failed to read dimensions from {file}: {ex.Message}");
        }
      });

      // Determine the maximum dimensions from the concurrent collections
      int maxWidth = maxWidths.Max();
      int maxHeight = maxHeights.Max();

      return (maxWidth, maxHeight);
    }

    private static int ReadBigEndianInt32(BinaryReader reader)
    {
      var bigEndianBytes = reader.ReadBytes(4);
      Array.Reverse(bigEndianBytes); // Convert from big-endian to little-endian
      return BitConverter.ToInt32(bigEndianBytes, 0);
    }

    public static Bitmap CropTransparentEdges(this Bitmap source)
    {
      int width = source.Width;
      int height = source.Height;
      int minX = width;
      int minY = height;
      int maxX = 0;
      int maxY = 0;

      bool isTransparent(Color color)
      {
        return color.A == 0;
      }

      // Find the bounding box of the non-transparent pixels
      for (int y = 0; y < height; y++)
      {
        for (int x = 0; x < width; x++)
        {
          if (!isTransparent(source.GetPixel(x, y)))
          {
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
          }
        }
      }

      // If the entire image is transparent, return a 1x1 pixel transparent image
      if (minX > maxX || minY > maxY)
      {
        return new Bitmap(1, 1, source.PixelFormat);
      }

      // Crop the image to the bounding box
      Rectangle cropArea = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
      return source.Clone(cropArea, source.PixelFormat);
    }

    public static Bitmap ExpandImage(Bitmap image, int expandWidth, int expandHeight, ExpansionOrigin origin)
    {
      int originX = 0;
      int originY = 0;

      // Determine the origin point based on the specified expansion origin
      switch (origin)
      {
        case ExpansionOrigin.TopLeft:
          break;
        case ExpansionOrigin.TopCenter:
          originX = (expandWidth - image.Width) / 2;
          originY = 0;
          break;
        case ExpansionOrigin.TopRight:
          originX = expandWidth - image.Width;
          originY = 0;
          break;
        case ExpansionOrigin.MiddleLeft:
          originX = 0;
          originY = (expandHeight - image.Height) / 2;
          break;
        case ExpansionOrigin.MiddleCenter:
          originX = (expandWidth - image.Width) / 2;
          originY = (expandHeight - image.Height) / 2;
          break;
        case ExpansionOrigin.MiddleRight:
          originX = expandWidth - image.Width;
          originY = (expandHeight - image.Height) / 2;
          break;
        case ExpansionOrigin.BottomLeft:
          originX = 0;
          originY = expandHeight - image.Height;
          break;
        case ExpansionOrigin.BottomCenter:
          originX = (expandWidth - image.Width) / 2;
          originY = expandHeight - image.Height;
          break;
        case ExpansionOrigin.BottomRight:
          originX = expandWidth - image.Width;
          originY = expandHeight - image.Height;
          break;
      }

      // Create a new bitmap with larger dimensions
      Bitmap expandedImage = new Bitmap(expandWidth, expandHeight);

      using (Graphics graphics = Graphics.FromImage(expandedImage))
      {
        // Set the background as transparent
        graphics.Clear(Color.Transparent);
        // Draw the original image on the new canvas
        graphics.DrawImage(image, new Rectangle(originX, originY, image.Width, image.Height));
      }

      return expandedImage;
    }

    public static void ExpandImage(string imagePath, int expandWidth, int expandHeight, ExpansionOrigin origin, bool renameFiles, string outputFolder)
    {
      using (Bitmap originalImage = new Bitmap(imagePath))
      {
        int originX = 0;
        int originY = 0;

        // Determine the origin point based on the specified expansion origin
        switch (origin)
        {
          case ExpansionOrigin.TopLeft:
            break;
          case ExpansionOrigin.TopCenter:
            originX = (expandWidth - originalImage.Width) / 2;
            originY = 0;
            break;
          case ExpansionOrigin.TopRight:
            originX = expandWidth - originalImage.Width;
            originY = 0;
            break;
          case ExpansionOrigin.MiddleLeft:
            originX = 0;
            originY = (expandHeight - originalImage.Height) / 2;
            break;
          case ExpansionOrigin.MiddleCenter:
            originX = (expandWidth - originalImage.Width) / 2;
            originY = (expandHeight - originalImage.Height) / 2;
            break;
          case ExpansionOrigin.MiddleRight:
            originX = expandWidth - originalImage.Width;
            originY = (expandHeight - originalImage.Height) / 2;
            break;
          case ExpansionOrigin.BottomLeft:
            originX = 0;
            originY = expandHeight - originalImage.Height;
            break;
          case ExpansionOrigin.BottomCenter:
            originX = (expandWidth - originalImage.Width) / 2;
            originY = expandHeight - originalImage.Height;
            break;
          case ExpansionOrigin.BottomRight:
            originX = expandWidth - originalImage.Width;
            originY = expandHeight - originalImage.Height;
            break;
        }
        // Create a new bitmap with larger dimensions
        using (Bitmap expandedImage = new Bitmap(expandWidth, expandHeight))
        {
          using (Graphics graphics = Graphics.FromImage(expandedImage))
          {
            // Set the background as transparent
            graphics.Clear(Color.Transparent);
            // Draw the original image on the new canvas
            graphics.DrawImage(originalImage, new Rectangle(originX, originY, originalImage.Width, originalImage.Height));
          }

          // Save the expanded image with "_expanded" suffix
          string fileName = Path.GetFileNameWithoutExtension(imagePath);
          string fileExtension = Path.GetExtension(imagePath);
          string newFileName = renameFiles ? $"expanded_{fileName}{fileExtension}" : Path.GetFileName(imagePath);
          string savePath = Path.Combine(outputFolder, newFileName);

          expandedImage.Save(savePath);
        }
      }
    }

    public static void CropImageFolderRandom(string folderPath, string extension, int width, int height)
    {
      string[] imageFiles = Directory.GetFiles(folderPath, extension); // Change the extension as required
      var outputFolder = Path.Combine(folderPath, "Cropped");
      Directory.CreateDirectory(outputFolder);
      foreach (string imagePath in imageFiles)
      {
        // Load the image
        using (Bitmap originalImage = new Bitmap(imagePath))
        {
          // Generate random coordinates for cropping
          Random rnd = new Random();
          int x = rnd.Next(0, originalImage.Width - width);
          int y = rnd.Next(0, originalImage.Height - height);

          // Create rectangle for cropping
          Rectangle cropRect = new Rectangle(x, y, width, height);

          // Crop the image
          using (Bitmap croppedImage = originalImage.Clone(cropRect, originalImage.PixelFormat))
          {
            // Save the cropped image with "_icon" suffix
            string fileName = Path.GetFileNameWithoutExtension(imagePath);
            string fileExtension = Path.GetExtension(imagePath);
            string newFileName = $"{fileName}_icon{fileExtension}";
            string savePath = Path.Combine(outputFolder, newFileName);

            croppedImage.Save(savePath);
          }
        }
      }
    }
    public static Bitmap GenerateClutImage(List<Color> palette, KingdomFileData file, byte[] clut7Bytes)
    {
      var clutImage = new Bitmap(file.Width, file.Height);
      for (int y = 0; y < file.Height; y++)
      {
        for (int x = 0; x < file.Width; x++)
        {
          var i = y * file.Width + x;
          var paletteIndex = clut7Bytes[i];
          var color = paletteIndex < palette.Count ? palette[paletteIndex] : Color.Transparent;
          clutImage.SetPixel(x, y, color);
        }
      }

      return clutImage;
    }

    public static byte[] GenerateClutBytes(List<Color> palette, byte[] clut7Bytes, int Width, int Height)
    {
      var clutImage = new byte[Width * Height * 4];

      try
      {

        for (int y = 0; y < Height; y++)
        {
          for (int x = 0; x < Width; x++)
          {
            var i = y * Width + x;
            var paletteIndex = clut7Bytes[i];
            var color = paletteIndex < palette.Count ? palette[paletteIndex] : Color.Transparent;
            clutImage[i * 4 + 0] = color.R;
            clutImage[i * 4 + 1] = color.G;
            clutImage[i * 4 + 2] = color.B;
            clutImage[i * 4 + 3] = color.A;
          }
        }
      }
      catch (System.Exception)
      {
        return clutImage;
      }

      return clutImage;
    }
    public static Bitmap GenerateClutImage(List<Color> palette, byte[] clut7Bytes, int Width, int Height, bool useTransparency = false, int transparencyIndex = 0, bool lowerIndexes = true)
    {
      var clutImage = new Bitmap(Width, Height);

      try
      {

        for (int y = 0; y < Height; y++)
        {
          for (int x = 0; x < Width; x++)
          {
            var i = y * Width + x;
            var paletteIndex = clut7Bytes[i];
            var color = paletteIndex < palette.Count ?
            (useTransparency && ((lowerIndexes && paletteIndex <= transparencyIndex) || (!lowerIndexes && paletteIndex >= transparencyIndex) || paletteIndex == 0)) ?
            Color.Transparent : palette[paletteIndex] : palette[paletteIndex % palette.Count];
            clutImage.SetPixel(x, y, color);
          }
        }
      }
      catch (System.Exception)
      {
        return clutImage;
      }

      return clutImage;
    }

    public static Bitmap GenerateClut4Image(List<Color> palette, byte[] clut7Bytes, int Width, int Height)
    {
      var clutImage = new Bitmap(Width, Height);
      try
      {
        for (int y = 0; y < Height; y++)
        {
          for (int x = 0; x < Width; x += 2)
          {
            var i = y * Width + x;
            var paletteIndex = clut7Bytes[i];
            var paletteIndex1 = paletteIndex >> 4;
            var paletteIndex2 = paletteIndex & 0x0F;
            var color = paletteIndex1 < palette.Count ? palette[paletteIndex1] : Color.Transparent;
            var color2 = paletteIndex2 < palette.Count ? palette[paletteIndex2] : Color.Transparent;
            clutImage.SetPixel(x, y, color);
            clutImage.SetPixel(x + 1, y, color);
          }
        }
      }
      catch (System.Exception)
      {
        return clutImage;
      }

      return clutImage;
    }

    public static Bitmap GenerateRleImage(List<Color> palette, KingdomFileData file, byte[] rl7Bytes)
    {
      var rleImage = Rle7(rl7Bytes, file.Width);
      var rleBitmap = CreateImage(rleImage, palette, file.Width, file.Height);
      return rleBitmap;
    }

    public static Bitmap GenerateRle3Image(List<Color> palette, int width, int height, byte[] rl7Bytes)
    {
      var rleImage = Rle3(rl7Bytes, width, height);
      var rleBitmap = CreateImage(rleImage, palette, width, height);
      return rleBitmap;
    }

    public static Bitmap GenerateRle7Image(List<Color> palette, byte[] rl7Bytes, int width, int height, bool useTransparency = false)
    {
      var rleImage = Rle7(rl7Bytes, width);
      var rleBitmap = CreateImage(rleImage, palette, width, height, useTransparency);
      return rleBitmap;
    }


    public static byte[] Rle3(byte[] dataRLE, int width, int height)
    {
      //initialize variables
      int nrRLEData = dataRLE.Count();
      byte[] dataDecoded = new byte[width * height];
      int posX = 1;
      int outputIndex = 0;
      int inputIndex = 0;

      //decode RLE3
      while ((inputIndex < nrRLEData) && (outputIndex < (width * height)))
      {
        //get run count
        byte byte1 = @dataRLE[inputIndex++];
        if (inputIndex >= nrRLEData) { break; }
        if (byte1 >= 128)
        {
          //draw multiple times
          byte colorNr = (byte)((byte1 - 128) >> 4 & 0x07);
          byte colorNr2 = (byte)((byte1 - 128) & 0x07);

          //get runlength
          byte rl = @dataRLE[inputIndex++];

          //draw x times
          for (int i = 0; i < rl; i++)
          {
            var index = outputIndex += 2;
            if (index >= dataDecoded.Length)
            {
              break;
            }
            dataDecoded[index] = @colorNr;
            dataDecoded[index + 1] = @colorNr2;
            posX += 2;
          }

          //draw until end of line
          if (rl == 0)
          {
            while (posX <= width)
            {
              if (outputIndex >= dataDecoded.Length)
              {
                break;
              }
              dataDecoded[outputIndex++] = @colorNr;
              dataDecoded[outputIndex++] = @colorNr2;
              posX += 2;
            }
          }
        }
        else
        {
          //draw once
          dataDecoded[outputIndex++] = (byte)((byte1 - 128) >> 4);
          dataDecoded[outputIndex++] = (byte)((byte1 - 128) & 0x0F);
          posX += 2;
        }

        //reset x to 1 if end of line is reached
        if (posX >= width) { posX = 1; }
      }

      //decode CLUT to bitmap
      return dataDecoded;
    }

    public static byte[] Rle7(byte[] rleData, int lineWidth)
    {
      List<byte[]> lines = new List<byte[]>();
      List<byte> currentLine = new List<byte>();

      try
      {

        int i = 0;
        while (i < rleData.Length)
        {
          byte firstByte = rleData[i];
          bool isRun = (firstByte & 0x80) != 0; // Check if the MSB is set
          byte colorIndex = (byte)(firstByte & 0x7F); // Extract color index (7 bits)

          if (isRun)
          {
            if (i + 1 >= rleData.Length)
            {
              break;
            }

            byte runLength = rleData[i + 1];
            i += 2;

            if (runLength == 1)
            {
              //throw new Exception("Invalid RLE data: Run length of 1 is forbidden.");
              continue;
            }

            int addLength = (runLength == 0) ? (lineWidth - currentLine.Count) : runLength;

            if (currentLine.Count + addLength > lineWidth)
            {
              addLength = lineWidth - currentLine.Count;
            }

            currentLine.AddRange(Enumerable.Repeat(colorIndex, addLength));
          }
          else // Single pixel
          {
            currentLine.Add(colorIndex);
            i++;
          }

          if (currentLine.Count == lineWidth)
          {
            lines.Add(currentLine.ToArray());
            currentLine.Clear();
          }
        }

        // Add the last line if not empty
        if (currentLine.Count > 0)
        {
          lines.Add(currentLine.ToArray());
        }
      }
      catch (Exception ex)
      {
        //MessageBox.Show($"Error at line {lines.Count}, returning image so far: {ex}");
        return lines.SelectMany(l => l).ToArray();
      }

      return lines.SelectMany(l => l).ToArray();
    }

    // requires file to contain iff headers
    public static ( byte[] palette, byte[] image) ExtractPaletteAndImageBytes(string? filePath, byte[] data = null)
    {
      byte[] plteSequence = "PLTE"u8.ToArray();
      byte[] idatSequence = [0x49, 0x44, 0x41, 0x54];
      byte[] plteBytes = null;
      byte[] idatBytes = null;

      if (data != null)
      {
        using (BinaryReader reader = new BinaryReader(new MemoryStream(data)))
        {
          while (reader.BaseStream.Position != reader.BaseStream.Length)
          {
            byte currentByte = reader.ReadByte();

            if (currentByte == plteSequence[0] && MatchesSequence(reader, plteSequence.Skip(1).ToArray()))
            { // skip two bytes
              var bytes = reader.ReadBytes(4);
              uint numberOfBytesToRead = BitConverter.ToUInt32(bytes.Reverse().ToArray())-4;
              reader.ReadBytes(4); // skip two bytes
              plteBytes = reader.ReadBytes((int)numberOfBytesToRead);
            }
            else if (currentByte == idatSequence[0] && MatchesSequence(reader, idatSequence.Skip(1).ToArray()))
            {
              var bytes = reader.ReadBytes(4);
              uint numberOfBytesToRead = BitConverter.ToUInt32(bytes.Reverse().ToArray());
              idatBytes = reader.ReadBytes((int)numberOfBytesToRead);
              break; // no need to continue reading after this
            }
          }
        }
      }
      else if (filePath != null)
      {
        using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
        {
          while (reader.BaseStream.Position != reader.BaseStream.Length)
          {
            byte currentByte = reader.ReadByte();

            if (currentByte == plteSequence[0] && MatchesSequence(reader, plteSequence.Skip(1).ToArray()))
            {
              reader.BaseStream.Position += 2; // skip two bytes
              var bytes = reader.ReadBytes(2);
              ushort numberOfBytesToRead = BitConverter.ToUInt16(bytes.Reverse().ToArray());
              plteBytes = reader.ReadBytes(numberOfBytesToRead);
            }
            else if (currentByte == idatSequence[0] && MatchesSequence(reader, idatSequence.Skip(1).ToArray()))
            {
              reader.BaseStream.Position += 4;
              idatBytes = reader.ReadBytes(0x1a400);
              break; // no need to continue reading after this
            }
          }
        }
      }
      // return named Tuple
      return (palette: plteBytes, image: idatBytes);
    }


    public static List<byte[]> ExtractPngFiles(byte[] data)
    {
      var pngs = new List<byte[]>();
      byte[] pngStartSeq = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
      byte[] pngEndSeq = new byte[] { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };

      int startIndex = 0;
      while ((startIndex = FindSequence(data, pngStartSeq, startIndex)) != -1)
      {
        int endIndex = FindSequence(data, pngEndSeq, startIndex);
        if (endIndex != -1)
        {
          int length = (endIndex + pngEndSeq.Length) - startIndex;
          byte[] png = new byte[length];
          Array.Copy(data, startIndex, png, 0, length);
          pngs.Add(png);
          startIndex = endIndex + pngEndSeq.Length;
        }
        else
        {
          break;
        }
      }

      return pngs;
    }

    static int FindSequence(byte[] data, byte[] sequence, int start)
    {
      for (int i = start; i < data.Length - sequence.Length + 1; i++)
      {
        bool match = true;
        for (int j = 0; j < sequence.Length; j++)
        {
          if (data[i + j] != sequence[j])
          {
            match = false;
            break;
          }
        }
        if (match)
        {
          return i;
        }
      }
      return -1;
    }

    public static void ConvertILBMToPNG(string ilbmFilePath, string outputPngPath)
    {
      using (FileStream fileStream = new FileStream(ilbmFilePath, FileMode.Open, FileAccess.Read))
      using (BinaryReader reader = new BinaryReader(fileStream))
      {
        // Validate IFF FORM header
        string formType = new string(reader.ReadChars(4));
        if (formType != "FORM")
          throw new InvalidDataException("Not a valid IFF FORM.");

        // Read the size of the FORM
        uint formSize = (uint)ReadBigEndianUInt32(reader);

        // Read the type of FORM, should be ILBM
        string ilbmType = new string(reader.ReadChars(4));
        if (ilbmType != "ILBM")
          throw new InvalidDataException("Not a valid ILBM file.");

        // Variables to hold ILBM chunks
        BitmapHeader bitmapHeader = null;
        ColorMap colorMap = null;
        byte[] bodyData = null;

        // Read chunks within the ILBM FORM
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
          string chunkId = new string(reader.ReadChars(4));
          uint chunkSize = (uint)ReadBigEndianUInt32(reader);
          long nextChunkPosition = reader.BaseStream.Position + chunkSize;

          switch (chunkId)
          {
            case "BMHD":
              bitmapHeader = ReadBitmapHeader(reader);
              break;
            case "CMAP":
              colorMap = ReadColorMap(reader, chunkSize);
              break;
            case "BODY":
              bodyData = reader.ReadBytes((int)chunkSize);
              break;
            default:
              reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
              break;
          }

          // Align to even boundary
          if (nextChunkPosition % 2 != 0)
            nextChunkPosition++;

          reader.BaseStream.Seek(nextChunkPosition, SeekOrigin.Begin);
        }

        if (bitmapHeader == null || colorMap == null || bodyData == null)
          throw new InvalidDataException("Incomplete ILBM file.");

        // Decode bitplanes
        Bitmap bitmap = DecodeILBM(bodyData, bitmapHeader, colorMap);

        // Save as PNG
        bitmap.Save(outputPngPath, ImageFormat.Png);
      }
    }

    private static BitmapHeader ReadBitmapHeader(BinaryReader reader)
    {
      BitmapHeader header = new BitmapHeader
      {
        Width = (ushort)ReadBigEndianUInt16(reader),
        Height = (ushort)ReadBigEndianUInt16(reader),
        X = (short)ReadBigEndianInt16(reader),
        Y = (short)ReadBigEndianInt16(reader),
        Planes = reader.ReadByte(),
        Masking = reader.ReadByte(),
        Compression = reader.ReadByte(),
        Pad1 = reader.ReadByte(),
        TransparentColor = (ushort)ReadBigEndianUInt16(reader),
        XAspect = reader.ReadByte(),
        YAspect = reader.ReadByte(),
        PageWidth = (short)ReadBigEndianInt16(reader),
        PageHeight = (short)ReadBigEndianInt16(reader)
      };
      return header;
    }

    private static ColorMap ReadColorMap(BinaryReader reader, uint chunkSize)
    {
      int colorCount = (int)(chunkSize / 3);
      Color[] colors = new Color[colorCount];
      for (int i = 0; i < colorCount; i++)
      {
        byte r = reader.ReadByte();
        byte g = reader.ReadByte();
        byte b = reader.ReadByte();
        colors[i] = Color.FromArgb(r, g, b);
      }
      return new ColorMap { Colors = colors };
    }

    private static Bitmap DecodeILBM(byte[] bodyData, BitmapHeader header, ColorMap colorMap)
    {
      int width = header.Width;
      int height = header.Height;
      int planes = header.Planes;
      Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed);

      BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
      int bytesPerRow = (width + 7) / 8;

      byte[] pixelData = new byte[height * bitmapData.Stride];
      byte[] decompressedBodyData = header.Compression == 1 ? DecompressByteRun1(bodyData, bytesPerRow * height * planes) : bodyData;
      int rowIndex = 0;

      for (int y = 0; y < height; y++)
      {
        byte[] rowBytes = new byte[bytesPerRow * planes];
        Array.Copy(decompressedBodyData, rowIndex, rowBytes, 0, rowBytes.Length);
        rowIndex += rowBytes.Length;

        for (int x = 0; x < width; x++)
        {
          byte colorIndex = 0;
          for (int p = 0; p < planes; p++)
          {
            int byteIndex = (x / 8) + (p * bytesPerRow);
            byte bit = (byte)(1 << (7 - (x % 8)));
            if ((rowBytes[byteIndex] & bit) != 0)
              colorIndex |= (byte)(1 << p);
          }
          pixelData[y * bitmapData.Stride + x] = colorIndex;
        }
      }

      Marshal.Copy(pixelData, 0, bitmapData.Scan0, pixelData.Length);
      bitmap.UnlockBits(bitmapData);

      // Apply the color palette
      ColorPalette palette = bitmap.Palette;
      for (int i = 0; i < colorMap.Colors.Length; i++)
      {
        palette.Entries[i] = colorMap.Colors[i];
      }
      bitmap.Palette = palette;

      return bitmap;
    }
    private static byte[] DecompressByteRun1(byte[] compressedData, int expectedSize)
    {
      List<byte> decompressedData = new List<byte>(expectedSize);
      int i = 0;

      while (i < compressedData.Length)
      {
        sbyte n = (sbyte)compressedData[i++];
        if (n >= 0)
        {
          // Copy the next n+1 bytes literally
          for (int j = 0; j <= n; j++)
          {
            decompressedData.Add(compressedData[i++]);
          }
        }
        else if (n != -128)
        {
          // Repeat the next byte -n+1 times
          byte b = compressedData[i++];
          for (int j = 0; j < -n + 1; j++)
          {
            decompressedData.Add(b);
          }
        }
      }

      return decompressedData.ToArray();
    }

  }

  public class BitmapHeader
  {
    public ushort Width { get; set; }
    public ushort Height { get; set; }
    public short X { get; set; }
    public short Y { get; set; }
    public byte Planes { get; set; }
    public byte Masking { get; set; }
    public byte Compression { get; set; }
    public byte Pad1 { get; set; }
    public ushort TransparentColor { get; set; }
    public byte XAspect { get; set; }
    public byte YAspect { get; set; }
    public short PageWidth { get; set; }
    public short PageHeight { get; set; }
  }

  public class ColorMap
  {
    public Color[] Colors { get; set; }
  }

  public enum ExpansionOrigin
  {
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight
  }
}
