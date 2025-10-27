using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ExtractCLUT;
using ExtractCLUT.Model;

namespace ExtractCLUT.Helpers
{
  public static class FileHelpers
  {
    private const int subHeaderOffset = 16;
    public static byte[] RemoveConsecutiveZeros(byte[] input)
    {
      if (input == null || input.Length == 0)
      {
        return new byte[0];
      }

      List<byte> output = new List<byte>();
      int zeroCount = 0;

      foreach (byte b in input)
      {
        if (b == 0x00)
        {
          zeroCount++;
        }
        else
        {
          if (zeroCount < 4)
          {
            output.AddRange(Enumerable.Repeat((byte)0x00, zeroCount));
          }

          zeroCount = 0;
          output.Add(b);
        }
      }

      // Handle trailing zeros
      if (zeroCount < 4)
      {
        output.AddRange(Enumerable.Repeat((byte)0x00, zeroCount));
      }

      return output.ToArray();
    }

    public static List<byte[]> SplitBinaryFileByNullBytes(string filePath, int? offset)
    {
      var chunks = new List<byte[]>();
      var buffer = new List<byte>();
      using (var fs = new FileStream(filePath, FileMode.Open))
      {
        int currentByte = 0;
        bool isPreviousByteZero = false;
        if (offset.HasValue)
        {
          fs.Seek(offset.Value, SeekOrigin.Begin);
        }
        while ((currentByte = fs.ReadByte()) != -1)
        {
          if (currentByte == 0x00)
          {
            if (isPreviousByteZero)
            {
              // remove the previous 0x00 byte from buffer
              buffer.RemoveAt(buffer.Count - 1);

              // add chunk to list and clear buffer
              if (buffer.Count > 0)
              {
                if (buffer.Last() != 0x00) buffer.Add(0x00);
                chunks.Add(buffer.ToArray());
              }
              buffer.Clear();

              // skip the rest of the zeros
              while ((currentByte = fs.ReadByte()) == 0x00) { }
              if (currentByte == -1) break;
            }
            else
            {
              isPreviousByteZero = true;
            }
          }
          else
          {
            isPreviousByteZero = false;
          }

          buffer.Add((byte)currentByte);
        }
        if (buffer.Count > 0)
        {
          while (buffer.Count > 0 && buffer.Last() == 0x00)
          {
            buffer.RemoveAt(buffer.Count - 1);
          }
          if (buffer.Count > 0)
          {
            if (buffer.Last() != 0x00) buffer.Add(0x00);
            chunks.Add(buffer.ToArray());
          }
        }
      }
      return chunks;
    }
    public static List<byte[]> SplitBinaryFileBy0xFFFF(string filePath)
    {
      List<byte[]> chunks = new List<byte[]>();
      using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
      {
        List<byte> currentChunk = new List<byte>();
        byte? previousByte = null;

        while (reader.BaseStream.Position != reader.BaseStream.Length)
        {
          byte currentByte = reader.ReadByte();

          if (previousByte == 0xFF && currentByte == 0xFF)
          {

            // Skip the 0xFF 0xFF bytes
            currentChunk.RemoveAt(currentChunk.Count - 1);

            // Remove trailing 0x00 bytes and add chunk to list
            while (currentChunk.Count > 0 && currentChunk.Last() == 0x00)
            {
              currentChunk.RemoveAt(currentChunk.Count - 1);
            }
            // Remove trailing 0x00 bytes and add chunk to list
            while (currentChunk.Count > 0 && currentChunk.First() == 0x00)
            {
              currentChunk.RemoveAt(0);
            }
            if (currentChunk.Count > 0)
            {
              if (currentChunk.Last() == 0x80) currentChunk.Add(0x00);
              chunks.Add(currentChunk.ToArray());
            }
            currentChunk.Clear();
          }
          else
          {
            currentChunk.Add(currentByte);
          }

          previousByte = currentByte;
        }
      }

      return chunks;
    }
    public static List<byte[]> SplitBinaryFileIntoChunks(string filePath, byte[] separator, bool removeTrailing0x00, bool removeLeading0x00, int? offset)
    {
      List<byte[]> chunks = new List<byte[]>();
      List<byte> currentChunk = new List<byte>();

      using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
      {
        Queue<byte> separatorQueue = new Queue<byte>(separator);
        Queue<byte> matchingQueue = new Queue<byte>();

        if (offset.HasValue)
        {
          reader.BaseStream.Seek(offset.Value, SeekOrigin.Begin);
        }
        while (reader.BaseStream.Position != reader.BaseStream.Length)
        {
          byte currentByte = reader.ReadByte();

          // Check if currentByte matches the next byte in the separator sequence
          if (separatorQueue.Count > 0 && currentByte == separatorQueue.Peek())
          {
            matchingQueue.Enqueue(currentByte);
            separatorQueue.Dequeue();

            // If we've matched the entire separator sequence
            if (!separatorQueue.Any())
            {
              if (currentChunk.Count == 0)
              {
                // If we haven't added any bytes to the current chunk yet, we can skip this separator sequence
                separatorQueue = new Queue<byte>(separator);
                continue;
              }
              // Remove the separator sequence from the end of the current chunk

              if (removeTrailing0x00)
              {
                // Remove trailing 0x00 bytes
                while (currentChunk.Count > 0 && currentChunk.Last() == 0x00)
                {
                  currentChunk.RemoveAt(currentChunk.Count - 1);
                }
              }

              if (removeLeading0x00)
              {
                // Remove leading 0x00 bytes
                while (currentChunk.Count > 0 && currentChunk.First() == 0x00)
                {
                  currentChunk.RemoveAt(0);
                }
              }

              // Add chunk to list and start a new one

              if (currentChunk.Count > 0)
              {
                if (currentChunk.Last() == 0x80) currentChunk.Add(0x00);
                chunks.Add(currentChunk.ToArray());
              }
              currentChunk.Clear();

              // Reset the separator queue
              separatorQueue = new Queue<byte>(separator);
            }
          }
          else
          {
            // If we were in the middle of matching a separator sequence, we need to add the matched bytes back into the chunk
            while (matchingQueue.Any())
            {
              currentChunk.Add(matchingQueue.Dequeue());
            }

            // Reset the separator queue
            separatorQueue = new Queue<byte>(separator);

            // Add currentByte to chunk
            currentChunk.Add(currentByte);
          }
        }
      }

      // Add any remaining bytes as the last chunk
      if (currentChunk.Any())
      {
        chunks.Add(currentChunk.ToArray());
      }

      return chunks;
    }
    
    public static byte[] RemoveTrailingZeroes(byte[] array)
    {
      int lastIndex = array.Length - 1;

      // Find the index of the last non-zero byte
      while (lastIndex >= 0 && array[lastIndex] == 0)
      {
        lastIndex--;
      }

      // Include one additional zero if the original number of trailing zeroes was odd
      if ((array.Length - lastIndex) == 0)
      {
        lastIndex++;
      }

      // Create a new array without the trailing zeroes
      byte[] trimmedArray = new byte[lastIndex + 1];
      Array.Copy(array, trimmedArray, lastIndex + 1);

      return trimmedArray;
    }
    public static byte[] DecompressLZSS(byte[] encodedData)
    {
      const int N = 0x1000;  // History buffer size
      List<byte> decodedData = new List<byte>();
      byte[] histbuff = new byte[N];  // History buffer (sliding window)
      int bufpos = 0;  // Position in the sliding window

      using (MemoryStream inStream = new MemoryStream(encodedData))
      using (BinaryReader reader = new BinaryReader(inStream))
      {
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
          byte flagbyte = reader.ReadByte();

          for (int i = 0; i < 8; i++)
          {
            if ((flagbyte & (1 << i)) == 0)
            {
              // Read offset-length pair
              if (reader.BaseStream.Position + 2 > reader.BaseStream.Length)
                break;  // End of stream check

              ushort offsetlen = reader.ReadUInt16();
              int length = (offsetlen & 0xF) + 3;
              int offset = (bufpos - (offsetlen >> 4)) & (N - 1);

              for (int j = 0; j < length; j++)
              {
                byte tempa = histbuff[(offset + j) & (N - 1)];
                decodedData.Add(tempa);
                histbuff[bufpos] = tempa;
                bufpos = (bufpos + 1) & (N - 1);
              }
            }
            else
            {
              // Read literal byte
              if (reader.BaseStream.Position >= reader.BaseStream.Length)
                break;

              byte tempa = reader.ReadByte();
              decodedData.Add(tempa);
              histbuff[bufpos] = tempa;
              bufpos = (bufpos + 1) & (N - 1);
            }
          }
        }
      }

      return decodedData.ToArray();
    }
    public static Bitmap ParseFont(byte[] data)
    {
      // var fontFileData = new CdiFontFile(data);

      // Console.WriteLine($"Found file data: {fontFileData}");
      var Width = 280 * 8;
      var Height = 15;
      var clutImage = new Bitmap(Width, Height, PixelFormat.Format1bppIndexed);
      try {
        for (int y = 0; y < Height; y++)
        {
          for (int x = 0; x < Width;)
          {
            var i = y * Width + x;
            var paletteByte = data[i];
            // for each bit in paletteByte
            for (int j = 7; j >= 0; j--)
            {
              var bit = (paletteByte >> j) & 1;
              var paletteIndex = bit;
              var color = paletteIndex == 0 ? Color.Black : Color.White;
              clutImage.SetPixel(x++, y, color);
            }
          }
        }
      } catch (Exception ex) {
        Console.WriteLine($"Error: {ex.Message}");
        return clutImage;
      }
      return clutImage;
    }
     public static List<byte[]> SplitBinaryFileintoSectors(string filePath, int sectorSize)
    {
      List<byte[]> sectors = new List<byte[]>();
      using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
      {
        while (reader.BaseStream.Position != reader.BaseStream.Length)
        {
          byte[] sector = reader.ReadBytes(sectorSize);
          sectors.Add(sector);
        }
      }
      return sectors;
    }

    public static List<long> FindSequenceOffsets(string filePath, byte[] sequence)
    {
      if (sequence == null || sequence.Length == 0)
        throw new ArgumentException("Sequence must not be null or empty.");

      List<long> offsets = new List<long>();

      using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
      {
        byte[] buffer = new byte[sequence.Length]; // Buffer to hold bytes read from the file
        int sequenceIndex = 0; // Index of the current byte in the sequence

        while (stream.Position < stream.Length)
        {
          int b = stream.ReadByte();
          if (b == -1) break; // End of file

          if (b == sequence[sequenceIndex])
          {
            sequenceIndex++; // Move to the next byte in the sequence
            if (sequenceIndex == sequence.Length)
            {
              // Complete sequence found, add offset to list
              long sequenceStart = stream.Position - sequence.Length;
              offsets.Add(sequenceStart);

              // Reset sequence index and adjust position to continue search
              sequenceIndex = 0;
              stream.Position = sequenceStart + 1;
            }
          }
          else
          {
            if (sequenceIndex > 0)
            {
              // Partial match found, but current byte does not continue the sequence
              // Reset sequence index and adjust position to continue search
              stream.Position -= sequenceIndex;
              sequenceIndex = 0;
            }
          }
        }
      }

      return offsets;
    }
    public static int FindSequence(byte[] data, byte[] sequence, int startIndex)
    {
      int maxFirstCharIndex = data.Length - sequence.Length + 1;
      for (int i = startIndex; i < maxFirstCharIndex; i++)
      {
        bool matchFound = true;
        for (int j = 0; j < sequence.Length; j++)
        {
          if (data[i + j] != sequence[j])
          {
            matchFound = false;
            break;
          }
        }

        if (matchFound)
          return i;
      }
      return -1; // Sequence not found
    }

    public static List<long> FindByteSequenceMemoryMapped(string filePath, byte[] pattern)
    {
      var offsets = new List<long>();
      using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, "search", 0);
      using var accessor = mmf.CreateViewAccessor();

      long fileSize = new FileInfo(filePath).Length;
      int patternLength = pattern.Length;

      // Boyer-Moore bad character table
      var badCharTable = new int[256];
      for (int i = 0; i < 256; i++)
        badCharTable[i] = patternLength;
      for (int i = 0; i < patternLength - 1; i++)
        badCharTable[pattern[i]] = patternLength - 1 - i;

      long pos = 0;
      while (pos <= fileSize - patternLength)
      {
        int i = patternLength - 1;
        while (i >= 0 && accessor.ReadByte(pos + i) == pattern[i])
          i--;

        if (i < 0)
        {
          offsets.Add(pos);
          pos++;
        }
        else
        {
          byte currentByte = accessor.ReadByte(pos + i);
          pos += Math.Max(1, badCharTable[currentByte] - (patternLength - 1 - i));
        }
      }

      return offsets;
    }

    public static List<long> FindByteSequenceBuffered(string filePath, byte[] pattern)
    {
      var offsets = new List<long>();
      const int bufferSize = 1024 * 1024; // 1MB buffer

      // Build KMP failure function
      var failure = new int[pattern.Length];
      for (int i = 1, j = 0; i < pattern.Length; i++)
      {
        while (j > 0 && pattern[i] != pattern[j])
          j = failure[j - 1];
        if (pattern[i] == pattern[j])
          j++;
        failure[i] = j;
      }

      using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
      var buffer = new byte[bufferSize + pattern.Length - 1];
      long fileOffset = 0;
      int patternIndex = 0;
      int bytesRead;

      while ((bytesRead = fs.Read(buffer, 0, bufferSize)) > 0)
      {
        for (int i = 0; i < bytesRead; i++)
        {
          while (patternIndex > 0 && buffer[i] != pattern[patternIndex])
            patternIndex = failure[patternIndex - 1];

          if (buffer[i] == pattern[patternIndex])
            patternIndex++;

          if (patternIndex == pattern.Length)
          {
            offsets.Add(fileOffset + i - pattern.Length + 1);
            patternIndex = failure[patternIndex - 1];
          }
        }

        fileOffset += bytesRead;

        // Handle pattern spanning buffer boundaries
        if (bytesRead == bufferSize)
        {
          Array.Copy(buffer, bufferSize, buffer, 0, pattern.Length - 1);
          fileOffset -= pattern.Length - 1;
          fs.Seek(fileOffset, SeekOrigin.Begin);
        }
      }

      return offsets;
    }

    public static List<long> FindByteSequenceSpan(string filePath, byte[] pattern)
    {
      var offsets = new List<long>();
      var fileBytes = File.ReadAllBytes(filePath);
      var span = fileBytes.AsSpan();

      int index = 0;
      while (index <= span.Length - pattern.Length)
      {
        int found = span.Slice(index).IndexOf(pattern);
        if (found == -1)
          break;

        offsets.Add(index + found);
        index += found + 1;
      }

      return offsets;
    }

    public static void CropSpritesToOptimalSize(string inputFolder, string outputFolder, int frameWidth = 320, int frameHeight = 200)
    {
      var regex = new Regex(@"(\d+)_(-?\d+)_(-?\d+)\.png");
      var files = Directory.GetFiles(inputFolder, "*.png");

      if (files.Length == 0)
      {
        Console.WriteLine("No PNG files found in input folder.");
        return;
      }

      int minX = int.MaxValue, minY = int.MaxValue;
      int maxX = int.MinValue, maxY = int.MinValue;

      // First pass: calculate bounds
      foreach (var file in files)
      {
        var match = regex.Match(Path.GetFileName(file));
        if (!match.Success) continue;

        int originX = int.Parse(match.Groups[2].Value);
        int originY = int.Parse(match.Groups[3].Value);

        using (var bmp = new Bitmap(file))
        {
          int spriteLeft = originX;
          int spriteTop = originY;
          int spriteRight = originX + bmp.Width;
          int spriteBottom = originY + bmp.Height;

          minX = Math.Min(minX, spriteLeft);
          minY = Math.Min(minY, spriteTop);
          maxX = Math.Max(maxX, spriteRight);
          maxY = Math.Max(maxY, spriteBottom);
        }
      }

      // Calculate crop dimensions
      int totalWidth = maxX - minX;
      int totalHeight = maxY - minY;
      int cropWidth = Math.Min(totalWidth, frameWidth);
      int cropHeight = Math.Min(totalHeight, frameHeight);

      // Ensure minimum reasonable size
      cropWidth = Math.Max(cropWidth, 32);
      cropHeight = Math.Max(cropHeight, 32);

      // Calculate centered crop area
      int centerX = (minX + maxX) / 2;
      int centerY = (minY + maxY) / 2;
      int cropMinX = centerX - cropWidth / 2;
      int cropMinY = centerY - cropHeight / 2;

      Directory.CreateDirectory(outputFolder);

      // Second pass: create cropped images
      foreach (var file in files)
      {
        var match = regex.Match(Path.GetFileName(file));
        if (!match.Success) continue;

        int originX = int.Parse(match.Groups[2].Value);
        int originY = int.Parse(match.Groups[3].Value);
        int index = int.Parse(match.Groups[1].Value);

        using (var sourceBmp = new Bitmap(file))
        {
          var croppedImage = new Bitmap(cropWidth, cropHeight);
          using (var g = Graphics.FromImage(croppedImage))
          {
            g.Clear(Color.Transparent);

            // Calculate position within the centered crop area
            int drawX = originX - cropMinX;
            int drawY = originY - cropMinY;

            // Draw the sprite at its relative position within the centered crop bounds
            g.DrawImage(sourceBmp, drawX, drawY);
          }

          string filename = Path.GetFileName(file);
          string outputPath = Path.Combine(outputFolder, filename);
          croppedImage.Save(outputPath);
          croppedImage.Dispose();
        }
      }
    }


    public static void AlignAndCropSprites(string inputFolder, string outputFolder, int frameHeight, int sequenceSize = 4)
    {
      var regex = new Regex(@"(\d+)_(-?\d+)_(-?\d+)\.png");
      var files = Directory.GetFiles(inputFolder, "*.png");

      if (!files.Any())
      {
        Console.WriteLine("No PNG files found in input folder.");
        return;
      }

      var allSprites = new List<(string path, int index, int originX, int originY, Bitmap bmp)>();

      // Load all sprites and their info first
      foreach (var file in files)
      {
        var match = regex.Match(Path.GetFileName(file));
        if (!match.Success) continue;

        int index = int.Parse(match.Groups[1].Value);
        int originX = int.Parse(match.Groups[2].Value);
        int originY = int.Parse(match.Groups[3].Value);
        var bmp = new Bitmap(file);
        allSprites.Add((file, index, originX, originY, bmp));
      }

      // Sort sprites by index to ensure correct sequencing
      allSprites = allSprites.OrderBy(s => s.index).ToList();

      Directory.CreateDirectory(outputFolder);

      // Process sprites in batches based on sequenceSize
      for (int i = 0; i < allSprites.Count; i += sequenceSize)
      {
        var sequenceSprites = allSprites.Skip(i).Take(sequenceSize).ToList();
        if (!sequenceSprites.Any()) continue;

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        // First pass for the sequence: calculate combined bounding box
        foreach (var (_, _, originX, originY, bmp) in sequenceSprites)
        {
          int drawX = originX;
          int drawY = frameHeight - originY;

          minX = Math.Min(minX, drawX);
          minY = Math.Min(minY, drawY - bmp.Height);
          maxX = Math.Max(maxX, drawX + bmp.Width);
          maxY = Math.Max(maxY, drawY);
        }

        if (minX == int.MaxValue) continue;

        int cropWidth = maxX - minX;
        int cropHeight = maxY - minY;

        // Second pass for the sequence: create aligned and cropped images
        foreach (var (path, _, originX, originY, bmp) in sequenceSprites)
        {
          var croppedImage = new Bitmap(cropWidth, cropHeight);
          using (var g = Graphics.FromImage(croppedImage))
          {
            g.Clear(Color.Transparent);

            int conceptualDrawY = frameHeight - originY;
            int conceptualDrawX = originX;

            int finalDrawX = conceptualDrawX - minX;
            int finalDrawY = conceptualDrawY - minY - bmp.Height;

            g.DrawImage(bmp, finalDrawX, finalDrawY);
          }

          string filename = Path.GetFileName(path);
          string outputPath = Path.Combine(outputFolder, filename);
          croppedImage.Save(outputPath);
          croppedImage.Dispose();
        }
      }

      // Dispose of all loaded bitmaps
      foreach (var sprite in allSprites)
      {
        sprite.bmp.Dispose();
      }
    }

    public static void PositionSprites(string inputFolder, string outputFolder, int frameWidth, int frameHeight)
    {
      var regex = new Regex(@"(\d+)_(-?\d+)_(-?\d+)\.png");
      var files = Directory.GetFiles(inputFolder, "*.png");

      Directory.CreateDirectory(outputFolder);

      foreach (var file in files)
      {
        var match = regex.Match(Path.GetFileName(file));
        if (!match.Success) continue;

        int originY = int.Parse(match.Groups[3].Value);
        int originX = int.Parse(match.Groups[2].Value);
        int index = int.Parse(match.Groups[1].Value);

        using (Bitmap sourceBmp = new Bitmap(file))
        {
          Bitmap newImage = new Bitmap(frameWidth, frameHeight);
          using (Graphics g = Graphics.FromImage(newImage))
          {
            g.Clear(Color.Transparent);

            // Draw the image at the origin coordinates from top-left
            g.DrawImage(sourceBmp, originX, originY);
          }

          string filename = Path.GetFileName(file);
          string outputPath = Path.Combine(outputFolder, filename);
          newImage.Save(outputPath);
          newImage.Dispose();
        }
      }
    }

    public static void AlignSprites(string inputFolder, string outputFolder)
    {

      var regex = new Regex(@"(\d+)_(-?\d+)_(-?\d+)\.png");
      // var regex = new Regex(@"(\d+)_(-?\d+)_(-?\d+)\.png");
      var files = Directory.GetFiles(inputFolder, "*.png");

      var images = new List<(string path, int originX, int originY, Bitmap bmp)>();

      int left = int.MaxValue, top = int.MaxValue;
      int right = int.MinValue, bottom = int.MinValue;

      // Load all images and calculate frame size
      foreach (var file in files)
      {
        var match = regex.Match(Path.GetFileName(file));
        if (!match.Success) continue;

        int originX = int.Parse(match.Groups[2].Value);
        int originY = int.Parse(match.Groups[3].Value);
        Bitmap bmp = new Bitmap(file);

        int x0 = -originX;
        int y0 = -originY;
        int x1 = x0 + bmp.Width;
        int y1 = y0 + bmp.Height;

        left = Math.Min(left, x0);
        top = Math.Min(top, y0);
        right = Math.Max(right, x1);
        bottom = Math.Max(bottom, y1);

        images.Add((file, originX, originY, bmp));
      }

      int frameWidth = right - left;
      int frameHeight = bottom - top;

      int originFrameX = -left;
      int originFrameY = -top;

      // Console.WriteLine($"Calculated frame size: {frameWidth} x {frameHeight}");
      // Console.WriteLine($"All origins aligned to point: ({originFrameX}, {originFrameY})");

      Directory.CreateDirectory(outputFolder);

      foreach (var (path, originX, originY, bmp) in images)
      {
        Bitmap newImage = new Bitmap(frameWidth, frameHeight);
        using (Graphics g = Graphics.FromImage(newImage))
        {
          g.Clear(Color.Transparent);

          int offsetX = originFrameX - originX;
          int offsetY = originFrameY - originY;

          g.DrawImage(bmp, offsetX, offsetY);
        }

        string filename = Path.GetFileName(path);
        string outputPath = Path.Combine(outputFolder, filename);
        newImage.Save(outputPath);
      }
    }

    public static void AlignSpriteSequences(string inputFolder, string outputFolder)
    {
      //var regex = new Regex(@"(\d+)_(\d+)__(-?\d+)_(-?\d+)\.png");
      var regex = new Regex(@"(\d+)_(\d+)_(-?\d+)_(-?\d+)_(-?\d+)_(-?\d+)\.png");
      var files = Directory.GetFiles(inputFolder, "*.png");

      // Group files by sequence ID
      var sequenceGroups = new Dictionary<int, List<(string path, int frameId, int originX, int originY, Bitmap bmp)>>();

      foreach (var file in files)
      {
        var match = regex.Match(Path.GetFileName(file));
        if (!match.Success) continue;

        int sequenceId = int.Parse(match.Groups[1].Value);
        int frameId = int.Parse(match.Groups[2].Value);
        int originX = int.Parse(match.Groups[3].Value);
        int originY = int.Parse(match.Groups[4].Value);
        Bitmap bmp = new Bitmap(file);

        if (!sequenceGroups.ContainsKey(sequenceId))
        {
          sequenceGroups[sequenceId] = new List<(string, int, int, int, Bitmap)>();
        }

        sequenceGroups[sequenceId].Add((file, frameId, originX, originY, bmp));
      }

      Directory.CreateDirectory(outputFolder);

      // Process each sequence separately
      foreach (var kvp in sequenceGroups)
      {
        int sequenceId = kvp.Key;
        var images = kvp.Value;

        // Determine output directory for this sequence
        string sequenceOutputFolder;
        if (sequenceGroups.Count > 1)
        {
          sequenceOutputFolder = Path.Combine(outputFolder, sequenceId.ToString());
          Directory.CreateDirectory(sequenceOutputFolder);
        }
        else
        {
          sequenceOutputFolder = outputFolder;
        }

        int left = int.MaxValue, top = int.MaxValue;
        int right = int.MinValue, bottom = int.MinValue;

        // Calculate frame size for this sequence
        foreach (var (path, frameId, originX, originY, bmp) in images)
        {
          int x0 = -originX;
          int y0 = -originY;
          int x1 = x0 + bmp.Width;
          int y1 = y0 + bmp.Height;

          left = Math.Min(left, x0);
          top = Math.Min(top, y0);
          right = Math.Max(right, x1);
          bottom = Math.Max(bottom, y1);
        }

        int frameWidth = right - left;
        int frameHeight = bottom - top;

        int originFrameX = -left;
        int originFrameY = -top;

        Console.WriteLine($"Sequence {sequenceId}: frame size {frameWidth} x {frameHeight}, origin at ({originFrameX}, {originFrameY})");

        // Process each frame in the sequence
        foreach (var (path, frameId, originX, originY, bmp) in images)
        {
          Bitmap newImage = new Bitmap(frameWidth, frameHeight);
          using (Graphics g = Graphics.FromImage(newImage))
          {
            g.Clear(Color.Transparent);

            int offsetX = originFrameX - originX;
            int offsetY = originFrameY - originY;

            g.DrawImage(bmp, offsetX, offsetY);
          }

          string filename = Path.GetFileName(path);
          string outputPath = Path.Combine(sequenceOutputFolder, filename);
          newImage.Save(outputPath);

          bmp.Dispose();
          newImage.Dispose();
        }
      }
    }

    public static void AlignSpritesAlt(string inputFolder, string outputFolder)
    {
      //var regex = new Regex(@"(\d+)_(\d+)__(-?\d+)_(-?\d+)\.png");
      var regex = new Regex(@"(\d+)_(-?\d+)_(-?\d+)\.png");
      var files = Directory.GetFiles(inputFolder, "*.png");

      // Group files by sequence ID
      var sequenceGroups = new Dictionary<int, List<(string path, int frameId, int originX, int originY, Bitmap bmp)>>();

      foreach (var file in files)
      {
        var match = regex.Match(Path.GetFileName(file));
        if (!match.Success) continue;

        int sequenceId = 0;
        int frameId = int.Parse(match.Groups[1].Value);
        int originX = int.Parse(match.Groups[2].Value);
        int originY = int.Parse(match.Groups[3].Value);
        Bitmap bmp = new Bitmap(file);

        if (!sequenceGroups.ContainsKey(sequenceId))
        {
          sequenceGroups[sequenceId] = new List<(string, int, int, int, Bitmap)>();
        }

        sequenceGroups[sequenceId].Add((file, frameId, originX, originY, bmp));
      }

      Directory.CreateDirectory(outputFolder);

      // Process each sequence separately
      foreach (var kvp in sequenceGroups)
      {
        int sequenceId = kvp.Key;
        var images = kvp.Value;

        // Determine output directory for this sequence
        string sequenceOutputFolder;
        if (sequenceGroups.Count > 1)
        {
          sequenceOutputFolder = Path.Combine(outputFolder, sequenceId.ToString());
          Directory.CreateDirectory(sequenceOutputFolder);
        }
        else
        {
          sequenceOutputFolder = outputFolder;
        }

        int left = int.MaxValue, top = int.MaxValue;
        int right = int.MinValue, bottom = int.MinValue;

        // Calculate frame size for this sequence
        foreach (var (path, frameId, originX, originY, bmp) in images)
        {
          int x0 = -originX;
          int y0 = -originY;
          int x1 = x0 + bmp.Width;
          int y1 = y0 + bmp.Height;

          left = Math.Min(left, x0);
          top = Math.Min(top, y0);
          right = Math.Max(right, x1);
          bottom = Math.Max(bottom, y1);
        }

        int frameWidth = right - left;
        int frameHeight = bottom - top;

        int originFrameX = -left;
        int originFrameY = -top;

        Console.WriteLine($"Sequence {sequenceId}: frame size {frameWidth} x {frameHeight}, origin at ({originFrameX}, {originFrameY})");

        // Process each frame in the sequence
        foreach (var (path, frameId, originX, originY, bmp) in images)
        {
          Bitmap newImage = new Bitmap(frameWidth, frameHeight);
          using (Graphics g = Graphics.FromImage(newImage))
          {
            g.Clear(Color.Transparent);

            int offsetX = originFrameX + originX;
            int offsetY = originFrameY + originY;

            g.DrawImage(bmp, offsetX, offsetY);
          }

          string filename = Path.GetFileName(path);
          string outputPath = Path.Combine(sequenceOutputFolder, filename);
          newImage.Save(outputPath);

          bmp.Dispose();
          newImage.Dispose();
        }
      }
    }

    public static void ResizeImagesInFolder(string folderToResize, ExpansionOrigin origin)
    {

      var imagesToResize = Directory.GetFiles(folderToResize, "*.png");

      //get max width and height
      var maxDimensions = ImageFormatHelper.FindMaxDimensions(folderToResize);

      var expandWidth = maxDimensions.maxWidth;
      var expandHeight = maxDimensions.maxHeight;

      var expandedOutputFolder = Path.Combine(Path.GetDirectoryName(imagesToResize[0])!, "expanded");

      Directory.CreateDirectory(expandedOutputFolder);

      foreach (var image in imagesToResize)
      {
        ImageFormatHelper.ExpandImage(image, expandWidth, expandHeight, origin, false, expandedOutputFolder);
      }
    }

    /// <summary>
    /// Aligns sprites from a folder based on their embedded offset information in the filename.
    /// Filename format: {index}_{xOffset}_{yOffset}.png
    /// The offsets represent where each sprite should be positioned relative to a common origin point.
    /// The origin point's location within each sprite is determined by the ExpansionOrigin parameter.
    /// </summary>
    /// <param name="inputFolder">Folder containing the sprite images with offset information in filenames</param>
    /// <param name="outputFolder">Folder where aligned sprites will be saved</param>
    /// <param name="origin">Where the origin point is located within each sprite (e.g., BottomCenter for character feet)</param>
    public static void AlignSprite(string inputFolder, string outputFolder, ExpansionOrigin origin = ExpansionOrigin.TopLeft)
    {
      var regex = new Regex(@"(\d+)_(-?\d+)_(-?\d+)\.png");
      var files = Directory.GetFiles(inputFolder, "*.png");

      if (files.Length == 0)
      {
        Console.WriteLine("No PNG files found in input folder.");
        return;
      }

      var spriteInfos = new List<(string path, int index, int xOffset, int yOffset, Bitmap bmp)>();

      // Load all sprites and parse filename information
      foreach (var file in files)
      {
        var match = regex.Match(Path.GetFileName(file));
        if (!match.Success)
        {
          Console.WriteLine($"Skipping file with invalid naming format: {Path.GetFileName(file)}");
          continue;
        }

        int index = int.Parse(match.Groups[1].Value);
        int xOffset = int.Parse(match.Groups[2].Value);
        int yOffset = int.Parse(match.Groups[3].Value);
        var bmp = new Bitmap(file);

        spriteInfos.Add((file, index, xOffset, yOffset, bmp));
      }

      if (spriteInfos.Count == 0)
      {
        Console.WriteLine("No valid sprite files found.");
        return;
      }

      // Sort by index
      spriteInfos = spriteInfos.OrderBy(s => s.index).ToList();

      // Calculate bounding box
      // For each sprite, calculate where the origin point is within that sprite using:
      // originX = ((width / 2) + 1) - xOffset  (for BottomCenter, adjust for other origins)
      // originY = ((height) + 1) - yOffset     (for BottomCenter)
      // Then the top-left of the sprite in world space is at -originX, -originY
      int minX = int.MaxValue, minY = int.MaxValue;
      int maxX = int.MinValue, maxY = int.MinValue;

      foreach (var (_, _, xOffset, yOffset, bmp) in spriteInfos)
      {
        // Calculate where the origin point is within this sprite
        int originWithinSpriteX = 0, originWithinSpriteY = 0;
        
        switch (origin)
        {
          case ExpansionOrigin.TopLeft:
            originWithinSpriteX = 1 - xOffset;
            originWithinSpriteY = 1 - yOffset;
            break;
          case ExpansionOrigin.TopCenter:
            originWithinSpriteX = (bmp.Width / 2 + 1) - xOffset;
            originWithinSpriteY = 1 - yOffset;
            break;
          case ExpansionOrigin.TopRight:
            originWithinSpriteX = (bmp.Width + 1) - xOffset;
            originWithinSpriteY = 1 - yOffset;
            break;
          case ExpansionOrigin.MiddleLeft:
            originWithinSpriteX = 1 - xOffset;
            originWithinSpriteY = (bmp.Height / 2 + 1) - yOffset;
            break;
          case ExpansionOrigin.MiddleCenter:
            originWithinSpriteX = (bmp.Width / 2 + 1) - xOffset;
            originWithinSpriteY = (bmp.Height / 2 + 1) - yOffset;
            break;
          case ExpansionOrigin.MiddleRight:
            originWithinSpriteX = (bmp.Width + 1) - xOffset;
            originWithinSpriteY = (bmp.Height / 2 + 1) - yOffset;
            break;
          case ExpansionOrigin.BottomLeft:
            originWithinSpriteX = 1 - xOffset;
            originWithinSpriteY = (bmp.Height + 1) - yOffset;
            break;
          case ExpansionOrigin.BottomCenter:
            originWithinSpriteX = (bmp.Width / 2 + 1) - xOffset;
            originWithinSpriteY = (bmp.Height + 1) - yOffset;
            break;
          case ExpansionOrigin.BottomRight:
            originWithinSpriteX = (bmp.Width + 1) - xOffset;
            originWithinSpriteY = (bmp.Height + 1) - yOffset;
            break;
        }
        
        // The top-left of this sprite in world space (relative to origin at 0,0)
        int spriteLeft = -originWithinSpriteX;
        int spriteTop = -originWithinSpriteY;
        int spriteRight = spriteLeft + bmp.Width;
        int spriteBottom = spriteTop + bmp.Height;

        minX = Math.Min(minX, spriteLeft);
        minY = Math.Min(minY, spriteTop);
        maxX = Math.Max(maxX, spriteRight);
        maxY = Math.Max(maxY, spriteBottom);
      }

      // Calculate canvas size
      int canvasWidth = maxX - minX;
      int canvasHeight = maxY - minY;

      Console.WriteLine($"Calculated canvas size: {canvasWidth} x {canvasHeight}");
      Console.WriteLine($"Sprite bounds: X [{minX}, {maxX}], Y [{minY}, {maxY}]");
      Console.WriteLine($"Origin mode: {origin}");

      // All sprites will be shifted by -minX, -minY to fit on canvas
      int baseShiftX = -minX;
      int baseShiftY = -minY;

      Console.WriteLine($"All sprites will be shifted by: ({baseShiftX}, {baseShiftY}) to fit on canvas");

      Directory.CreateDirectory(outputFolder);

      // Process each sprite
      foreach (var (path, index, xOffset, yOffset, bmp) in spriteInfos)
      {
        // Calculate where the origin is within this sprite
        int originWithinSpriteX = 0, originWithinSpriteY = 0;
        
        switch (origin)
        {
          case ExpansionOrigin.TopLeft:
            originWithinSpriteX = 1 - xOffset;
            originWithinSpriteY = 1 - yOffset;
            break;
          case ExpansionOrigin.TopCenter:
            originWithinSpriteX = (bmp.Width / 2 + 1) - xOffset;
            originWithinSpriteY = 1 - yOffset;
            break;
          case ExpansionOrigin.TopRight:
            originWithinSpriteX = (bmp.Width + 1) - xOffset;
            originWithinSpriteY = 1 - yOffset;
            break;
          case ExpansionOrigin.MiddleLeft:
            originWithinSpriteX = 1 - xOffset;
            originWithinSpriteY = (bmp.Height / 2 + 1) - yOffset;
            break;
          case ExpansionOrigin.MiddleCenter:
            originWithinSpriteX = (bmp.Width / 2 + 1) - xOffset;
            originWithinSpriteY = (bmp.Height / 2 + 1) - yOffset;
            break;
          case ExpansionOrigin.MiddleRight:
            originWithinSpriteX = (bmp.Width + 1) - xOffset;
            originWithinSpriteY = (bmp.Height / 2 + 1) - yOffset;
            break;
          case ExpansionOrigin.BottomLeft:
            originWithinSpriteX = 1 - xOffset;
            originWithinSpriteY = (bmp.Height + 1) - yOffset;
            break;
          case ExpansionOrigin.BottomCenter:
            originWithinSpriteX = (bmp.Width / 2 + 1) - xOffset;
            originWithinSpriteY = (bmp.Height + 1) - yOffset;
            break;
          case ExpansionOrigin.BottomRight:
            originWithinSpriteX = (bmp.Width + 1) - xOffset;
            originWithinSpriteY = (bmp.Height + 1) - yOffset;
            break;
        }
        
        // Top-left position in world space, then shift to canvas space
        int drawX = -originWithinSpriteX + baseShiftX;
        int drawY = -originWithinSpriteY + baseShiftY;

        var alignedImage = new Bitmap(canvasWidth, canvasHeight);
        using (var g = Graphics.FromImage(alignedImage))
        {
          g.Clear(Color.Transparent);
          g.DrawImage(bmp, drawX, drawY);
        }

        string filename = Path.GetFileName(path);
        string outputPath = Path.Combine(outputFolder, filename);
        alignedImage.Save(outputPath);
        alignedImage.Dispose();

        Console.WriteLine($"Sprite {index}: offset ({xOffset,4},{yOffset,4}) -> drawn at canvas ({drawX,4},{drawY,4})");
      }

      // Cleanup
      foreach (var sprite in spriteInfos)
      {
        sprite.bmp.Dispose();
      }

      Console.WriteLine($"\n✓ Successfully aligned {spriteInfos.Count} sprites to '{outputFolder}'");
      Console.WriteLine($"  All sprites share the same {canvasWidth}x{canvasHeight} canvas with origin at {origin}");
    }

    public static List<byte[]> ExtractSpriteByteSequences(string? filePath, byte[]? data, byte[] startSequence, byte[] endSequence)
    {
      // Read all bytes from the file
      byte[] fileContent = filePath != null ? File.ReadAllBytes(filePath) : data ?? Array.Empty<byte>();

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
    public static List<long> FindSequenceOffsets(byte[] data, byte[] sequence)
    {
      if (sequence == null || sequence.Length == 0)
        throw new ArgumentException("Sequence must not be null or empty.");

      List<long> offsets = new List<long>();

      using (var stream = new MemoryStream(data))
      {
        byte[] buffer = new byte[sequence.Length]; // Buffer to hold bytes read from the file
        int sequenceIndex = 0; // Index of the current byte in the sequence

        while (stream.Position < stream.Length)
        {
          int b = stream.ReadByte();
          if (b == -1) break; // End of file

          if (b == sequence[sequenceIndex])
          {
            sequenceIndex++; // Move to the next byte in the sequence
            if (sequenceIndex == sequence.Length)
            {
              // Complete sequence found, add offset to list
              long sequenceStart = stream.Position - sequence.Length;
              offsets.Add(sequenceStart);

              // Reset sequence index and adjust position to continue search
              sequenceIndex = 0;
              stream.Position = sequenceStart + 1;
            }
          }
          else
          {
            if (sequenceIndex > 0)
            {
              // Partial match found, but current byte does not continue the sequence
              // Reset sequence index and adjust position to continue search
              stream.Position -= sequenceIndex;
              sequenceIndex = 0;
            }
          }
        }
      }

      return offsets;
    }
    public static async Task DeduplicateBinaryFilesAsync(string directory, int size, bool topLevelOnly = true)
    {
      if (!Directory.Exists(directory))
      {
        throw new DirectoryNotFoundException($"Directory '{directory}' not found.");
      }

      var fileHashes = new Dictionary<string, string>(); // Hash -> FilePath
      var filesToDelete = new List<string>();

      foreach (var filePath in Directory.EnumerateFiles(directory, "*.bin", topLevelOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories)) // Only top level.
      {
        if (new FileInfo(filePath).Length != size) // check size first
        {
          Console.WriteLine($"Skipping file '{filePath}' - not {size} bytes.");
          continue;
        }

        string fileHash = await CalculateFileHashAsync(filePath, size);

        if (fileHashes.ContainsKey(fileHash))
        {
          filesToDelete.Add(filePath); // Mark for deletion
          Console.WriteLine($"Duplicate found: '{filePath}' (matches '{fileHashes[fileHash]}')");
        }
        else
        {
          fileHashes.Add(fileHash, filePath);
        }
      }

      // Delete duplicates (do this *after* enumeration to avoid issues with modifying the collection).
      foreach (var fileToDelete in filesToDelete)
      {
        try
        {
          File.Delete(fileToDelete);
          Console.WriteLine($"Deleted: '{fileToDelete}'");
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Error deleting '{fileToDelete}': {ex.Message}");
        }
      }
    }


    private static async Task<string> CalculateFileHashAsync(string filePath, int size)
    {
      await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
      using var sha256 = SHA256.Create(); // Or MD5 if suitable.

      // For 32-byte files, reading all at once is likely fastest.
      var buffer = new byte[size];
      await fileStream.ReadAsync(buffer, 0, size);
      byte[] hashBytes = await sha256.ComputeHashAsync(new MemoryStream(buffer));


      return Convert.ToBase64String(hashBytes); // Or Convert.ToHexString(hashBytes) for hex.
    }
    
    public static byte[] FindSequenceAndGetPriorBytes(string filePath, byte[] sequence, int bytesToGet)
    {
      byte[] fileBytes = File.ReadAllBytes(filePath);
      byte[] sequenceBytes = sequence;

      // Convert to list to use the IndexOf function.
      List<byte> byteList = new List<byte>(fileBytes);

      for (int i = 0; i < byteList.Count; i++)
      {
        // Use SequenceEqual to check if the next few elements in the list are equal to the sequence
        if (byteList.Skip(i).Take(sequenceBytes.Length).SequenceEqual(sequenceBytes))
        {
          // We've found the sequence, now get the prior bytes.
          int startIndex = Math.Max(0, i - bytesToGet);
          return fileBytes.Skip(startIndex).Take(bytesToGet).ToArray();
        }
      }

      return Array.Empty<byte>(); // or throw an exception, or whatever you want to do if the sequence is not found.
    }

    public static byte[] RemoveOneZeroFromTripleZeroSequence(byte[] data)
    {
      if (data == null)
        throw new ArgumentNullException(nameof(data));

      List<byte> processed = new List<byte>(data.Length);

      int i = 0;
      while (i < data.Length)
      {
        // Check if we have a sequence of exactly three 0x00 bytes
        if (i < data.Length - 2 && data[i] == 0x00 && data[i + 1] == 0x00 && data[i + 2] == 0x00)
        {
          // Add two 0x00 bytes instead of three
          processed.Add(0x00);
          processed.Add(0x00);

          // Skip the three 0x00 bytes in the original array
          i += 3;
        }
        else
        {
          // Add the current byte to the processed array
          processed.Add(data[i]);
          i++;
        }
      }

      return processed.ToArray();
    }

    public static List<string> ExtractFilenames(string inputFilePath)
    {
      List<string> filenames = new List<string>();
      const int bufferSize = 1024 * 1024; // 1 MB buffer size
      byte[] buffer = new byte[bufferSize];
      string remainingData = string.Empty;

      using (FileStream fs = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read))
      using (BinaryReader reader = new BinaryReader(fs))
      {
        while (reader.BaseStream.Position < 0x52000)
        {
          int bytesRead = reader.Read(buffer, 0, bufferSize);
          string chunk = remainingData + Encoding.ASCII.GetString(buffer, 0, bytesRead);

          // Find potential filenames
          string pattern = @"[a-zA-Z0-9_\-\.]+";
          Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
          MatchCollection matches = regex.Matches(chunk);

          // Process matches
          for (int i = 0; i < matches.Count; i++)
          {
            string matchValue = matches[i].Value;
            // If it's the last match and it's incomplete, save it to remainingData
            if (i == matches.Count - 1 && reader.BaseStream.Position < reader.BaseStream.Length && !char.IsWhiteSpace(chunk[chunk.Length - 1]))
            {
              remainingData = matchValue;
            }
            else
            {
              if (IsValidFilename(matchValue))
              {
                filenames.Add(matchValue);
              }
            }
          }

          // If no matches, reset remainingData
          if (matches.Count == 0)
          {
            remainingData = string.Empty;
          }
        }
      }

      return filenames;
    }

    public static void ExtractFBXModels(string inputFilePath, string outputDirectory)
    {
      const int bufferSize = 1024 * 1024; // 1 MB buffer size
      byte[] buffer = new byte[bufferSize];

      string fbxAsciiHeader = "; FBX";
      string fbxBinaryHeader = "Kaydara FBX Binary";
      List<long> fbxStartPositions = new List<long>();

      using (FileStream fs = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read))
      using (BinaryReader reader = new BinaryReader(fs))
      {
        long fileLength = reader.BaseStream.Length;
        reader.BaseStream.Position = 0x21430000;
        while (reader.BaseStream.Position < fileLength)
        {
          int bytesRead = reader.Read(buffer, 0, bufferSize);
          string chunk = Encoding.ASCII.GetString(buffer, 0, bytesRead);
          if (fbxStartPositions.Count > 0)
          {
            break;
          }
          int index = 0;
          while (index < chunk.Length)
          {
            int asciiPos = chunk.IndexOf(fbxAsciiHeader, index);
            int binaryPos = chunk.IndexOf(fbxBinaryHeader, index);

            int foundPos = -1;
            if (asciiPos != -1 && (asciiPos < binaryPos || binaryPos == -1))
            {
              foundPos = asciiPos;
            }
            else if (binaryPos != -1 && (binaryPos < asciiPos || asciiPos == -1))
            {
              foundPos = binaryPos;
            }

            if (foundPos != -1)
            {
              fbxStartPositions.Add(reader.BaseStream.Position - bytesRead + foundPos);
              index = foundPos + 1;
              break;
            }
            else
            {
              break;
            }
          }

          // Move the reader back a bit to ensure we catch headers that might span across buffer boundaries
          reader.BaseStream.Position -= fbxAsciiHeader.Length;
        }

        for (int i = 0; i < fbxStartPositions.Count; i++)
        {
          long startPosition = fbxStartPositions[i];
          long endPosition = (i + 1 < fbxStartPositions.Count) ? fbxStartPositions[i + 1] : fileLength;

          fs.Seek(startPosition, SeekOrigin.Begin);
          long dataSize = endPosition - startPosition;
          byte[] fbxData = reader.ReadBytes((int)dataSize);

          string outputFilePath = Path.Combine(outputDirectory, $"extracted_{i}.fbx");
          File.WriteAllBytes(outputFilePath, fbxData);

          Console.WriteLine($"Extracted FBX model {i} to {outputFilePath}");
        }
      }

      Console.WriteLine("Extraction completed.");
    }
    static bool IsValidFilename(string filename)
    {
      // Simple heuristic to filter out unlikely filenames
      // Adjust the criteria based on your specific needs
      return filename.Length > 3 && filename.Contains(".");
    }

    /// <summary>
    /// Renames image files in a folder from format "{index}_{offsetX}_{offsetY}.png" 
    /// to "{FolderName}_{Index}.png" where FolderName is the parent folder name with "_output" removed.
    /// Files are sorted by index before renaming.
    /// </summary>
    /// <param name="folderPath">Path to the folder containing the images to rename</param>
    public static void RenameIndexedImages(string folderPath)
    {
      if (!Directory.Exists(folderPath))
      {
        Console.WriteLine($"Directory '{folderPath}' not found.");
        return;
      }

      var regex = new Regex(@"^(\d+)_(-?\d+)_(-?\d+)\.png$");
      var files = Directory.GetFiles(folderPath, "*.png");

      if (files.Length == 0)
      {
        Console.WriteLine("No PNG files found in folder.");
        return;
      }

      // Parse and collect file information
      var fileInfos = new List<(string path, int index)>();
      foreach (var file in files)
      {
        var fileName = Path.GetFileName(file);
        var match = regex.Match(fileName);
        
        if (!match.Success)
        {
          Console.WriteLine($"Skipping file with invalid format: {fileName}");
          continue;
        }

        int index = int.Parse(match.Groups[1].Value);
        fileInfos.Add((file, index));
      }

      if (fileInfos.Count == 0)
      {
        Console.WriteLine("No files matching the expected format found.");
        return;
      }

      // Sort by index
      fileInfos = fileInfos.OrderBy(f => f.index).ToList();

      // Get folder name and remove "_output" suffix
      string folderName = new DirectoryInfo(folderPath).Name;
      folderName = folderName.Replace("_output", "");

      // Rename files
      int renamedCount = 0;
      for (int i = 0; i < fileInfos.Count; i++)
      {
        var (originalPath, originalIndex) = fileInfos[i];
        string newFileName = $"{folderName}_{i}.png";
        string newPath = Path.Combine(folderPath, newFileName);

        // Check if target file already exists
        if (File.Exists(newPath) && newPath != originalPath)
        {
          Console.WriteLine($"Warning: Target file '{newFileName}' already exists. Skipping rename of '{Path.GetFileName(originalPath)}'");
          continue;
        }

        try
        {
          File.Move(originalPath, newPath);
          renamedCount++;
          Console.WriteLine($"Renamed: {Path.GetFileName(originalPath)} -> {newFileName}");
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Error renaming '{Path.GetFileName(originalPath)}': {ex.Message}");
        }
      }

      Console.WriteLine($"\n✓ Successfully renamed {renamedCount} of {fileInfos.Count} files in '{folderPath}'");
    }
  }


}

