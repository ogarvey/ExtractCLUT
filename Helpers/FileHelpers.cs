using System.IO;
using System.Text;
using ExtractCLUT;
using ExtractCLUT.Model;

public static class FileHelpers
{
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
  public static void StripCdiData(List<SectorInfo> sectors, string path, string file)
  {
    var outputPath = Path.Combine(path, $@"NewRecords/{Path.GetFileNameWithoutExtension(file)}/output/stripped");
    Directory.CreateDirectory(outputPath);
    var strippedFile = Path.Combine(outputPath, Path.GetFileNameWithoutExtension(file) + "_stripped.bin");
    var strippedSectors = sectors.Select(x =>
    {
      if (x.IsData)
      {
        var chunk = x.Data.Skip(24).Take(2048).ToArray();
        return chunk;
      }
      else if (x.IsVideo)
      {
        var chunk = x.Data.Skip(24).Take(x.IsForm2 ? 2324 : 2048).ToArray();
        return chunk;
      }
      else if (x.IsAudio)
      {
        var chunk = x.Data.Skip(24).Take(2304).ToArray();
        return chunk;
      }
      else
      {
        return new byte[0];
      }
    }).ToList();
    var strippedData = strippedSectors.SelectMany(x => x).ToArray();
    File.WriteAllBytes(strippedFile, strippedData);
  }

  public static void ParseMonoAudioSectorsByEOR(List<SectorInfo> sectors, string baseDir, string filename)
  {
    var outputPath = $@"{baseDir}\NewRecords\{Path.GetFileNameWithoutExtension(filename)}\audio-mono-eor\output";
    Directory.CreateDirectory(outputPath);

    Dictionary<string, List<byte[]>> byteArrays = new Dictionary<string, List<byte[]>>();
    List<byte[]> recordArray = new List<byte[]>();

    foreach (var (sector, i) in sectors.WithIndex())
    {
      var chunk = sector.Data.Skip(24).Take(2304).ToArray();
      if (sector.IsEmptySector)
        continue;

      if (!sector.IsAudio)
        continue;

      if (!sector.IsMono)
        continue;

      var bps = sector.BitsPerSampleString.Replace(" ", "_");
      var sampleFreq = sector.SamplingFrequencyString.Replace(" ", "_");
      var channel = sector.Channel;
      var key = $"{sector.FileNumber}_{channel}_{bps}_{sampleFreq}";

      if (!byteArrays.ContainsKey(key))
      {
        byteArrays[key] = new List<byte[]>();
      }
      byteArrays[key].Add(chunk);
      if (sector.IsEOR)
      {
        foreach (var baKey in byteArrays.Keys)
        {
          byte[] recordData = byteArrays[baKey].SelectMany(a => a).ToArray();
          recordArray.Add(recordData);
          // write record data to file
          var recordFileName = Path.GetFileNameWithoutExtension(filename) + $"_mono_a_{baKey}_{recordArray.Count}.bin";

          var recordFilePath = Path.Combine(outputPath, recordFileName);
          File.WriteAllBytes(recordFilePath, recordData);
        }
        byteArrays.Clear();
      }
    }
  }

  public static void ParseMonoAudioSectorsByChannel(List<SectorInfo> sectors, string baseDir, string filename)
  {
    var outputPath = $@"{baseDir}\NewRecords\{Path.GetFileNameWithoutExtension(filename)}\audio-mono-channel";
    Directory.CreateDirectory(outputPath);

    Dictionary<byte, List<byte[]>> byteArrays = new Dictionary<byte, List<byte[]>>();
    List<byte[]> recordArray = new List<byte[]>();

    foreach (var (sector, i) in sectors.WithIndex())
    {
      var chunk = sector.Data.Skip(24).Take(2304).ToArray();

      if (!byteArrays.ContainsKey(sector.Channel))
      {
        byteArrays[sector.Channel] = new List<byte[]>();
      }
      byteArrays[sector.Channel].Add(chunk);
    }
    foreach (var channel in byteArrays.Keys)
    {
      byte[] recordData = byteArrays[channel].SelectMany(a => a).ToArray();
      recordArray.Add(recordData);
      // write record data to file
      var recordFileName = Path.GetFileNameWithoutExtension(filename) + $"_a_{recordArray.Count}.bin";
      var recordFilePath = Path.Combine(outputPath, recordFileName);
      File.WriteAllBytes(recordFilePath, recordData);
    }
  }

  public static void ParseStereoAudioSectors(List<SectorInfo> sectors, string baseDir, string filename)
  {
    var outputPath = $@"{baseDir}\NewRecords\{Path.GetFileNameWithoutExtension(filename)}\audio-stereo-eor\output";
    Directory.CreateDirectory(outputPath);

    Dictionary<string, List<byte[]>> byteArrays = new Dictionary<string, List<byte[]>>();
    List<byte[]> recordArray = new List<byte[]>();

    foreach (var (sector, i) in sectors.WithIndex())
    {
      var chunk = sector.Data.Skip(24).Take(2304).ToArray();
      if (sector.IsEmptySector)
        continue;

      if (!sector.IsAudio)
        continue;

      if (sector.IsMono)
        continue;

      var bps = sector.BitsPerSampleString.Replace(" ", "_");
      var sampleFreq = sector.SamplingFrequencyString.Replace(" ", "_");
      var channel = sector.Channel;
      var key = $"{sector.FileNumber}_{channel}_{bps}_{sampleFreq}";

      if (!byteArrays.ContainsKey(key))
      {
        byteArrays[key] = new List<byte[]>();
      }
      byteArrays[key].Add(chunk);
      if (sector.IsEOR)
      {
        foreach (var baKey in byteArrays.Keys)
        {
          byte[] recordData = byteArrays[baKey].SelectMany(a => a).ToArray();
          recordArray.Add(recordData);
          // write record data to file
          var recordFileName = Path.GetFileNameWithoutExtension(filename) + $"_stereo_a_{baKey}_{recordArray.Count}.bin";

          var recordFilePath = Path.Combine(outputPath, recordFileName);
          File.WriteAllBytes(recordFilePath, recordData);
        }
        byteArrays.Clear();
      }
    }
  }


  public static void ParseStereoAudioSectorsByChannel(List<SectorInfo> sectors, string baseDir, string filename)
  {
    var outputPath = $@"{baseDir}\NewRecords\{Path.GetFileNameWithoutExtension(filename)}\audio-stereo-channel\output";
    Directory.CreateDirectory(outputPath);

    Dictionary<byte, List<byte[]>> byteArrays = new Dictionary<byte, List<byte[]>>();
    List<byte[]> recordArray = new List<byte[]>();

    foreach (var (sector, i) in sectors.WithIndex())
    {
      var chunk = sector.Data.Skip(24).Take(2304).ToArray();

      if (!byteArrays.ContainsKey(sector.Channel))
      {
        byteArrays[sector.Channel] = new List<byte[]>();
      }
      byteArrays[sector.Channel].Add(chunk);
      if (sector.IsEOR)
      {
        foreach (var baKey in byteArrays.Keys)
        {
          byte[] recordData = byteArrays[baKey].SelectMany(a => a).ToArray();
          recordArray.Add(recordData);
          // write record data to file
          var recordFileName = Path.GetFileNameWithoutExtension(filename) + $"_stereo_a_{recordArray.Count}.bin";
          var recordFilePath = Path.Combine(outputPath, recordFileName);
          File.WriteAllBytes(recordFilePath, recordData);
        }
        byteArrays.Clear();
      }
    }
  }
  public static void ParseVideoSectors(List<SectorInfo> sectors, string baseDir, string filename)
  {
    var videoPath = $@"{baseDir}\NewRecords\{Path.GetFileNameWithoutExtension(filename)}\video\output";
    Directory.CreateDirectory(videoPath);

    Dictionary<string, List<byte[]>> byteArrays = new Dictionary<string, List<byte[]>>();
    List<byte[]> recordArray = new List<byte[]>();

    foreach (var (sector, i) in sectors.WithIndex())
    {
      var chunk = sector.Data.Skip(24).Take(sector.IsForm2 ? 2324 : 2048).ToArray();
      var channel = sector.Channel;
      var imageType = sector.VideoString.Replace(" ", "_");
      var imageRes = sector.ResolutionString.Replace(" ", "_");
      var key = $"{sector.FileNumber}_{channel}_{imageType}_{imageRes}";

      if (!byteArrays.ContainsKey(key))
      {
        byteArrays[key] = new List<byte[]>();
      }
      byteArrays[key].Add(chunk);
      if (sector.IsEOR)
      {
        foreach (var baKey in byteArrays.Keys)
        {
          byte[] recordData = byteArrays[baKey].SelectMany(a => a).ToArray();
        recordArray.Add(recordData);
        // write record data to file
        var recordFileName = $"{Path.GetFileNameWithoutExtension(filename)}_v_{baKey}_{recordArray.Count}.bin";
        var recordFilePath = Path.Combine(videoPath, recordFileName);
        File.WriteAllBytes(recordFilePath, recordData);
        }
        byteArrays.Clear();
      }
    }
  }
  public static void ParseVideoSectorsByEOR(List<SectorInfo> sectors, string baseDir, string filename)
  {
    var videoPath = $@"{baseDir}\NewRecords\{Path.GetFileNameWithoutExtension(filename)}\video-eor\output";
    Directory.CreateDirectory(videoPath);

    List<byte[]> byteArrays = new List<byte[]>();
    List<byte[]> recordArray = new List<byte[]>();

    foreach (var (sector, i) in sectors.WithIndex())
    {
      var chunk = sector.Data.Skip(24).Take(sector.IsForm2 ? 2324 : 2048).ToArray();
      byteArrays.Add(chunk);
      if (sector.IsEOR)
      {
        byte[] recordData = byteArrays.SelectMany(a => a).ToArray();
        recordArray.Add(recordData);
        // write record data to file
        var recordFileName = $"{Path.GetFileNameWithoutExtension(filename)}_v_{sector.VideoString}_{sector.ResolutionString}_{recordArray.Count}.bin";
        var recordFilePath = Path.Combine(videoPath, recordFileName);
        File.WriteAllBytes(recordFilePath, recordData);
        byteArrays.Clear();
      }
    }
  }
  public static void ParseDataSectors(List<SectorInfo> sectors, string baseDir, string filename)
  {
    var videoPath = $@"{baseDir}\NewRecords\{Path.GetFileNameWithoutExtension(filename)}\data-eor\output";
    Directory.CreateDirectory(videoPath);

    List<byte[]> byteArrays = new List<byte[]>();
    List<byte[]> recordArray = new List<byte[]>();

    foreach (var (sector, i) in sectors.WithIndex())
    {
      var chunk = sector.Data.Skip(24).Take(2048).ToArray();
      byteArrays.Add(chunk);
      if (sector.IsEOR)
      {
        byte[] recordData = byteArrays.SelectMany(a => a).ToArray();
        recordArray.Add(recordData);
        // write record data to file
        var recordFileName = $"{Path.GetFileNameWithoutExtension(filename)}_d_{recordArray.Count}.bin";
        var recordFilePath = Path.Combine(videoPath, recordFileName);
        File.WriteAllBytes(recordFilePath, recordData);
        byteArrays.Clear();
      }
    }
  }
  public static void ParseSectorsByEOR(List<SectorInfo> sectors, string baseDir, string filename)
  {
    var videoPath = $@"{baseDir}\NewRecords\{Path.GetFileNameWithoutExtension(filename)}\eor\output";
    Directory.CreateDirectory(videoPath);

    List<byte[]> byteArrays = new List<byte[]>();
    List<byte[]> recordArray = new List<byte[]>();
    byte[] chunk = new byte[0];
    foreach (var (sector, i) in sectors.WithIndex())
    {
      if (sector.IsData)
      {
        chunk = sector.Data.Skip(24).Take(2048).ToArray();
      }
      else if (sector.IsVideo)
      {
        chunk = sector.Data.Skip(24).Take(sector.IsForm2 ? 2324 : 2048).ToArray();
      }
      else if (sector.IsAudio)
      {
        chunk = sector.Data.Skip(24).Take(2304).ToArray();
      }
      byteArrays.Add(chunk);
      if (sector.IsEOR)
      {
        byte[] recordData = byteArrays.SelectMany(a => a).ToArray();
        recordArray.Add(recordData);
        // write record data to file
        var recordFileName = $"{Path.GetFileNameWithoutExtension(filename)}_eor_{sector.SectorIndex}_{sector.OriginalOffset}_{recordArray.Count}.bin";
        var recordFilePath = Path.Combine(videoPath, recordFileName);
        File.WriteAllBytes(recordFilePath, recordData);
        byteArrays.Clear();
      }
    }
  }

  public static void WriteIndividualSectorsToFolder(List<SectorInfo> sectors, string outputPath)
  {
    foreach (var (sector, i) in sectors.WithIndex())
    {
      var sectorFileName = $"i{sector.SectorIndex}_ch{sector.Channel}";
      if (sector.IsAudio)
      {
        var audioType = sector.IsMono ? "Mono" : "Stereo";
        sectorFileName += $"_{audioType}";
      }
      if (sector.IsVideo) sectorFileName += $"_{sector.VideoString}";
      if (sector.IsEOR) sectorFileName += $"_eor";
      if (sector.IsEOF) sectorFileName += $"_eof";
      if (sector.IsTrigger) sectorFileName += $"_trigger";
      if (sector.IsForm2) sectorFileName += $"_form2";
      if (sector.IsASCF) sectorFileName += $"_ascf";

      if (sector.IsVideo)
      {
        sectorFileName += sector.HasEvenLines ? $"_even" : "_odd";
        sectorFileName += $".bin";
        var sectorFilePath = Path.Combine(outputPath, $"video/{sector.Channel}");
        Directory.CreateDirectory(sectorFilePath);
        sectorFilePath = Path.Combine(sectorFilePath, sectorFileName);
        File.WriteAllBytes(sectorFilePath, sector.Data.Skip(24).Take(sector.IsForm2 ? 2324 : 2048).ToArray());
      }
      else if (sector.IsAudio)
      {
        sectorFileName += $".bin";
        var sectorFilePath = Path.Combine(outputPath, $"audio/{sector.Channel}");
        Directory.CreateDirectory(sectorFilePath);
        sectorFilePath = Path.Combine(sectorFilePath, sectorFileName);
        File.WriteAllBytes(sectorFilePath, sector.Data.Skip(24).Take(2304).ToArray());
      }
      else if (sector.IsData)
      {
        sectorFileName += ".bin";
        var sectorFilePath = Path.Combine(outputPath, $"data/{sector.Channel}");
        Directory.CreateDirectory(sectorFilePath);
        sectorFilePath = Path.Combine(sectorFilePath, sectorFileName);
        File.WriteAllBytes(sectorFilePath, sector.Data.Skip(24).Take(2048).ToArray());
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
}
