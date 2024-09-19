using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
        return input;
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

    private static void ParseFont(byte[] data)
    {

      // var fontfile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\ALICE IN WONDERLAND\Output\atnc24cl.ai1_0_0_0.bin";

      // var bytes = File.ReadAllBytes(fontfile).Skip(0x44).Take(0x24).ToArray();

      var fontFileData = new CdiFontFile(data);

      Console.WriteLine($"Found file data: {fontFileData}");
      var Width = 280 * 8;
      var Height = 15;
      var clutImage = new Bitmap(Width, Height, PixelFormat.Format1bppIndexed);
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


    public static List<byte[]> ExtractSpriteByteSequences(string? filePath, byte[]? data, byte[] startSequence, byte[] endSequence)
    {
      // Read all bytes from the file
      byte[] fileContent = filePath != null ? File.ReadAllBytes(filePath) : data;

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

      return null; // or throw an exception, or whatever you want to do if the sequence is not found.
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
  }


}

