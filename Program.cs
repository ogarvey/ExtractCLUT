using System.Drawing.Imaging;
using static LotRSHelpers;
using ExtractCLUT.Helpers;
using static ExtractCLUT.Utils;
using Color = System.Drawing.Color;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using ExtractCLUT.Games;
using System.Drawing;
using ExtractCLUT.Writers;
using ExtractCLUT.Model;
using System.Drawing.Text;
using static ExtractCLUT.Helpers.AudioHelper;

// static void Rle7_AllBytes(byte[] dataRLE, List<Color> palette, int width, List<Bitmap> images)
// {
//   //initialize variables
//   int nrRLEData = dataRLE.Count();
//   byte[] dataDecoded = new byte[0x16800];
//   int posX = 1;
//   int outputIndex = 0;
//   int inputIndex = 0;
//   int initialIndex = 0;

//   //decode RLE7
//   while ((inputIndex < nrRLEData))
//   {
//     initialIndex = inputIndex;
//     //get run count
//     byte byte1 = @dataRLE[inputIndex++];
//     if (inputIndex >= nrRLEData) { break; }
//     if (byte1 >= 128)
//     {
//       //draw multiple times
//       byte colorNr = (byte)(byte1 - 128);

//       //get runlength
//       byte rl = @dataRLE[inputIndex++];

//       //draw x times
//       for (int i = 0; i < rl; i++)
//       {
//         if (outputIndex >= dataDecoded.Length)
//         {
//           break;
//         }
//         var index = outputIndex++;
//         if (index >= dataDecoded.Length)
//         {
//           break;
//         }
//         dataDecoded[index] = @colorNr;
//         posX++;
//       }

//       //draw until end of line
//       if (rl == 0)
//       {
//         while (posX <= width)
//         {
//           if (outputIndex >= dataDecoded.Length)
//           {
//             break;
//           }
//           dataDecoded[outputIndex++] = @colorNr;
//           posX++;
//         }
//       }
//     }
//     else
//     {
//       //draw once
//       dataDecoded[outputIndex++] = @byte1;
//       posX++;
//     }

//     //reset x to 1 if end of line is reached
//     if (posX >= width) { posX = 1; }
//     if (outputIndex >= 0x16800)
//     {
//       var offsets = $"{initialIndex:X8}_{inputIndex:X8}";
//       var image = ImageFormatHelper.GenerateRle7Image(palette, dataDecoded, width, outputIndex / width);
//       images.Add(image);
//       image.Save($@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\records\BurnCycle\video\output\Normal_6_{offsets}.png");
//       File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\records\BurnCycle\video\output\bins\Normal_6_{offsets}.bin", dataDecoded);
//       dataDecoded = new byte[0x16800];
//       outputIndex = 0;
//       posX = 1;
//     }
//   }
//   /* int requiredSize = dataDecoded.Length - 1;
//   while (dataDecoded[requiredSize] == 0x00)
//   {
//     requiredSize--;
//   }
//   byte[] dataDecoded2 = new byte[requiredSize + 1];
//   Array.Copy(dataDecoded, dataDecoded2, requiredSize + 1); */
//   //decode CLUT to bitmap
//   //return dataDecoded2;
// }

/* var input = @"C:\Dev\Projects\Gaming\CD-i\Lords of the Rising Sun\records\RLV\video\RLV_v_1_0_RL7_Normal_91.bin";
var output = @"C:\Dev\Projects\Gaming\CD-i\Lords of the Rising Sun\records\RLV\video\output\";
var palette = ColorHelper.ReadPalette(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Lords of the Rising Sun\records\RLV\data\cluts\RLV_91.clut"));

var data = File.ReadAllBytes(input);

var chunks = FileHelpers.SplitBinaryFileIntoChunks(input, new byte[] { 0x00, 0x00 }, true, true,0);
var images = new List<Bitmap>();
foreach (var (chunk, index) in chunks.WithIndex())
{
  var image = ImageFormatHelper.GenerateRle7Image(palette, chunk, 384, 240);
  images.Add(image);
}

using (var gifWriter = new GifWriter($"{output}Normal_91.gif", 100, -1))
{
  foreach (var image in images)
  {
    gifWriter.WriteFrame(image);
  }
}
 */

// var framesFile = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\space\video\space_v_1_0_QHY_Normal_3.bin";
// var originalImage = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\space\video\space_v_1_0_QHY_Normal_2.bin";

// LaserLordsHelper.ReadDyuvFramesFile(framesFile, originalImage);

// var fontfile = @"C:\Dev\Projects\Gaming\CD-i\AIW\ALICE IN WONDERLAND\records\atnc24cl\data\atnc24cl.ai1";

// var bytes = File.ReadAllBytes(fontfile).Skip(0x44).Take(0x24).ToArray();

// var fontFileData = new CdiFontFile(bytes);

// Console.WriteLine($"Found file data: ");

//EscapeFromCCHelper.ExtractDYUVs();

// var luxor9 = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\luxor\data\luxor_d_9.bin";

// var screen = LaserLordsHelper.GetScreenBytes(luxor9);

// var paletteBytes = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\luxor\data\cluts\luxor_5.clut");

// var palette = ColorHelper.ReadPalette(paletteBytes);

// var datBytes = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords\records\luxor\data\luxor_d_5.bin");

// var tiles = LaserLordsHelper.ReadScreenTiles(datBytes);

// var animationFrames = new List<Bitmap>();

// var screenImage = LaserLordsHelper.CreateScreenImage(tiles, screen, palette);

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
void ProcessAudioData(byte[] data, List<short> left, List<short> right, int bps, bool isMono)
{
  // Call DecodeAudioSector here with the data parameter, left, and right
  // ...
  try
  {
    DecodeAudioSector(data, left, right, bps == 8, !isMono);
  }
  catch
  {

    return;
  }
}

bool ExportAudio(List<byte[]> chunks, string filename, bool isMono, uint frequency, int bps)
{
  List<short> left = new List<short>();
  List<short> right = new List<short>();

  foreach (var chunk in chunks)
  {
    ProcessAudioData(chunk, left, right, bps, isMono);
  }

  WAVHeader wavHeader = new WAVHeader
  {
    ChannelNumber = (ushort)(isMono ? 1 : 2), // Mono
    Frequency = frequency, // 18.9 kHz
  };
  Directory.CreateDirectory(Path.GetDirectoryName(filename) + "\\wav_files");
  var outputPath = Path.Combine(Path.GetDirectoryName(filename) + "\\wav_files", Path.GetFileNameWithoutExtension(filename) + ".wav");

  using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
  {
    WriteWAV(fileStream, wavHeader, left, right);
  }
  return true;
}

var subHeaderOffset = 16;

var baseDir = @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\Laser Lords";
var bcBaseDir = @"C:\Dev\Projects\Gaming\CD-i\Burn Cycle";
var foeBaseDir = @"C:\Dev\Projects\Gaming\CD-i\LINK - The Faces of Evil";
var marioBaseDir = @"C:\Dev\Projects\Gaming\CD-i\MARIO";
var DataFile = Path.Combine(foeBaseDir, "TITLE.JBR");

var SectorInfos = new List<SectorInfo>();
var Chunks = FileHelpers.SplitBinaryFileintoSectors(DataFile, 2352);

foreach (var (chunk, index) in Chunks.WithIndex())
{
  if (chunk.Length < 2352)
  {
    continue;
  }
  var sectorInfo = new SectorInfo(DataFile, chunk)
  {
    SectorIndex = index,
    OriginalOffset = index * 2352,
    FileNumber = chunk[subHeaderOffset],
    Channel = chunk[subHeaderOffset + 1],
    SubMode = chunk[subHeaderOffset + 2],
    CodingInformation = chunk[subHeaderOffset + 3]
  };
  SectorInfos.Add(sectorInfo);
}

var dataSectors = SectorInfos.Where(x => x.IsData && !x.IsEmptySector).ToList();
var videoSectors = SectorInfos.Where(x => x.IsVideo && !x.IsEmptySector).ToList();
var monoAudioSectors = SectorInfos.Where(x => x.IsAudio && x.IsMono && !x.IsEmptySector).ToList();
var stereoAudioSectors = SectorInfos.Where(x => x.IsAudio && !x.IsMono && !x.IsEmptySector).ToList();

//FileHelpers.StripCdiData(bcSectorInfos, bcBaseDir, "BurnCycle.rtr");
FileHelpers.ParseDataSectors(dataSectors, marioBaseDir, "TITLE.JBR");
FileHelpers.ParseVideoSectors(videoSectors, bcBaseDir, "TITLE.JBR");
//FileHelpers.ParseSectorsByEOR(bcSectorInfos, bcBaseDir, "BurnCycle.rtr");

var outputDir = Path.Combine(marioBaseDir, $"individual/{Path.GetFileNameWithoutExtension(DataFile)}");
Directory.CreateDirectory(outputDir);
FileHelpers.WriteIndividualSectorsToFolder(SectorInfos, outputDir);

void OutputAudioData(List<IGrouping<AudioSectorGroupKey, SectorInfo>> audioGroup, bool isMono, bool includeFileNo)
{
  foreach (var group in audioGroup)
  {
    var channel = group.Key.Channel;
    var bitsPerSample = group.Key.BitsPerSample;
    var samplingFrequency = group.Key.SamplingFrequency;
    var FileNumber = group.Key.FileNumber;
    var bpsString = bitsPerSample == 0 ? "4 bits" : "8 bits";
    var filename = "";
    var audioSectors = group.ToList();

    var audioData = new List<byte[]>();

    foreach (var sector in audioSectors)
    {
      filename = Path.GetFileNameWithoutExtension(sector.CdiFile);
      var sectorData = sector.Data;
      var sectorAudioData = sectorData.Skip(24).Take(2304).ToArray();
      audioData.Add(sectorAudioData);
    }
    var audioType = isMono ? "mono-audio" : "stereo-audio";
    var audioPath = $@"{baseDir}\NewRecords\{filename}\{audioType}\output";

    Directory.CreateDirectory(audioPath);

    var audioFile = includeFileNo 
      ? $@"{audioPath}\{FileNumber}\{channel}_{bpsString}_{samplingFrequency}.wav" 
      : $@"{audioPath}\{channel}_{bpsString}_{samplingFrequency}.wav";

    ExportAudio(audioData, audioFile, false, (uint)samplingFrequency, bitsPerSample);
  }
}

List<IGrouping<AudioSectorGroupKey, SectorInfo>> OrderAndGroupAudioSectors(List<SectorInfo> sectors, bool includeFileNo)
{
  if (!includeFileNo)
  {
    var orderedMonoAudioSectors = sectors
    .OrderBy(x => x.Channel)
    .ThenBy(x => x.BitsPerSample)
    .ThenBy(x => x.SamplingFrequency)
    .ThenBy(x => x.SectorIndex)
    .ToList();

    var eorSectors = orderedMonoAudioSectors.Where(x => x.IsEOR).ToList();

    return orderedMonoAudioSectors
      .GroupBy(x => new AudioSectorGroupKey() { Channel = x.Channel, BitsPerSample = x.BitsPerSample, SamplingFrequency = x.SamplingFrequencyValue })
      .ToList();
  }
  else
  {
    var orderedMonoAudioSectors = sectors
    .OrderBy(x => x.FileNumber)
    .ThenBy(x => x.Channel)
    .ThenBy(x => x.BitsPerSample)
    .ThenBy(x => x.SamplingFrequency)
    .ThenBy(x => x.SectorIndex)
    .ToList();

    return orderedMonoAudioSectors
      .GroupBy(x => new AudioSectorGroupKey() { FileNumber = x.FileNumber, Channel = x.Channel, BitsPerSample = x.BitsPerSample, SamplingFrequency = x.SamplingFrequencyValue })
      .ToList();
  }
}

void OrderAndGroupVideoSectors(List<SectorInfo> sectors, bool includeFileNo)
{
  if (!includeFileNo)
  {
    var orderedVideoSectors = sectors
    .OrderBy(x => x.Channel)
    .ThenBy(x => x.Coding)
    .ThenBy(x => x.Resolution)
    .ThenBy(x => x.SectorIndex)
    .ToList();

    var eorSectors = orderedVideoSectors.Where(x => x.IsEOR).ToList();

    var grouped = orderedVideoSectors
      .GroupBy(x => new { x.Channel, x.VideoString, x.ResolutionString })
      .ToList();
    
    foreach (var group in grouped)
    {
      var channel = group.Key.Channel;
      var videoType = group.Key.VideoString;
      var resolution = group.Key.ResolutionString;
      var filename = "";

      var videoData = new List<byte[]>();

      foreach (var sector in group)
      {
        filename = Path.GetFileNameWithoutExtension(sector.CdiFile);
        var sectorData = sector.Data;
        var sectorVideoData = sectorData.Skip(24).Take(sector.IsForm2 ? 2324 : 2048).ToArray();
        videoData.Add(sectorVideoData);
      }
      var videoPath = $@"{baseDir}\NewRecords\{filename}\video\output";

      Directory.CreateDirectory(videoPath);

      var videoFile = $@"{videoPath}\{channel}_{videoType}_{resolution}.bin";

      File.WriteAllBytes(videoFile, videoData.SelectMany(x => x).ToArray());
    }
  }
  else
  {
    var orderedVideoSectors = sectors
    .OrderBy(x => x.FileNumber)
    .ThenBy(x => x.Channel)
    .ThenBy(x => x.Coding)
    .ThenBy(x => x.Resolution)
    .ThenBy(x => x.SectorIndex)
    .ToList();

    var grouped = orderedVideoSectors
      .GroupBy(x => new { x.FileNumber, x.Channel, x.VideoString, x.ResolutionString })
      .ToList();

    foreach (var group in grouped)
    {
      var FileNumber = group.Key.FileNumber;
      var channel = group.Key.Channel;
      var videoType = group.Key.VideoString;
      var resolution = group.Key.ResolutionString;
      var filename = "";

      var videoData = new List<byte[]>();

      foreach (var sector in group)
      {
        filename = Path.GetFileNameWithoutExtension(sector.CdiFile);
        var sectorData = sector.Data;
        var sectorVideoData = sectorData.Skip(24).Take(sector.IsForm2 ? 2324: 2048).ToArray();
        videoData.Add(sectorVideoData);
      }
      var videoPath = $@"{baseDir}\NewRecords\{filename}\video-with-fileno\output";

      Directory.CreateDirectory(videoPath);

      var videoFile = $@"{videoPath}\{FileNumber}_{channel}_{videoType}_{resolution}.bin";

      File.WriteAllBytes(videoFile, videoData.SelectMany(x => x).ToArray());
    }
  }
}



internal class AudioSectorGroupKey
{
  public byte Channel { get; set; }
  public int BitsPerSample { get; set; }
  public int SamplingFrequency { get; set; }
  public int? FileNumber { get; set; }
}

internal class VideoSectorGroupKey
{
  public byte Channel { get; set; }
  public int? FileNumber { get; set; }
  public string VideoType { get; set; }
  public string Resolution { get; set; }
}
