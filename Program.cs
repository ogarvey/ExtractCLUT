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

var subHeaderOffset = 16;

var baseDir = @"C:\Dev\Projects\Gaming\CD-i\LTFOE";

var DataFile = Path.Combine(baseDir, "_lanim.rtr");

byte[] sequenceToFind = new byte[] { 0x00, 0x00, 0x00, 0x80, 0xFF, 0xFF, 0xFF };
List<long> offsets = FileHelpers.FindSequenceOffsets(DataFile, sequenceToFind);

var bytes = File.ReadAllBytes(DataFile);

foreach (var offset in offsets)
{
  var paletteBytes = bytes.Skip((int)offset + 0x4).Take(0x188).ToArray();
  var palette = ColorHelper.ConvertBytesToRGB(paletteBytes);
  var paletteBitmap = ColorHelper.CreateLabelledPalette(palette);
  paletteBitmap.Save(Path.Combine(baseDir, $"NewRecords\\_lanim\\output\\palettes\\{offset}.png"));
}

// var files = Directory.GetFiles(baseDir, "*.rtr");

// foreach (var file in files)
// {
//   var SectorInfos = new List<SectorInfo>();
//   var Chunks = FileHelpers.SplitBinaryFileintoSectors(file, 2352);

//   foreach (var (chunk, index) in Chunks.WithIndex())
//   {
//     if (chunk.Length < 2352)
//     {
//       continue;
//     }
//     var sectorInfo = new SectorInfo(file, chunk)
//     {
//       SectorIndex = index,
//       OriginalOffset = index * 2352,
//       FileNumber = chunk[subHeaderOffset],
//       Channel = chunk[subHeaderOffset + 1],
//       SubMode = chunk[subHeaderOffset + 2],
//       CodingInformation = chunk[subHeaderOffset + 3]
//     };
//     SectorInfos.Add(sectorInfo);
//   }
//   var dataSectors = SectorInfos.Where(x => x.IsData && !x.IsEmptySector).ToList();
//   var videoSectors = SectorInfos.Where(x => x.IsVideo && !x.IsEmptySector).ToList();
//   var monoAudioSectors = SectorInfos.Where(x => x.IsAudio && x.IsMono && !x.IsEmptySector).ToList();
//   var stereoAudioSectors = SectorInfos.Where(x => x.IsAudio && !x.IsMono && !x.IsEmptySector).ToList();

  // FileHelpers.StripCdiData(SectorInfos, baseDir, file);
  // FileHelpers.ParseDataSectors(dataSectors, baseDir, file);
  // FileHelpers.ParseVideoSectors(SectorInfos, baseDir, file);
  // FileHelpers.ParseMonoAudioSectorsByChannel(monoAudioSectors, baseDir, file);
  // FileHelpers.ParseMonoAudioSectorsByEOR(monoAudioSectors, baseDir, file);
  // FileHelpers.ParseStereoAudioSectors(stereoAudioSectors, baseDir, file);
  // FileHelpers.ParseStereoAudioSectorsByChannel(stereoAudioSectors, baseDir, file);
  // FileHelpers.ParseSectorsByEOR(SectorInfos, baseDir, file);
//}
//LaserLordsHelper.ExtractSlidesBin(@"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\records\slides\video\slides_v_1_16_DYUV_Normal_1.bin", @"C:\Dev\Projects\Gaming\CD-i\LLExtractRaw\records\slides\video\output");
// var sectorInfo = new SectorInfo("music.rtr", new byte[2352])
// {
//   SectorIndex = 0,
//   OriginalOffset = 0,
//   FileNumber = 1,
//   Channel = 1,
//   SubMode = 64,
//   CodingInformation = 5
// };

// Console.WriteLine($"SectorInfo: {sectorInfo}");

// BurnCycle.ExtractIndividualSectors(SectorInfos);

// var dataSectors = SectorInfos.Where(x => x.IsData && !x.IsEmptySector).ToList();
// var videoSectors = SectorInfos.Where(x => x.IsVideo && !x.IsEmptySector).ToList();
// var monoAudioSectors = SectorInfos.Where(x => x.IsAudio && x.IsMono && !x.IsEmptySector).ToList();
// var stereoAudioSectors = SectorInfos.Where(x => x.IsAudio && !x.IsMono && !x.IsEmptySector).ToList();


// FileHelpers.StripCdiData(SectorInfos, baseDir, DataFile);
// FileHelpers.ParseDataSectors(dataSectors, baseDir, DataFile);
// FileHelpers.ParseVideoSectors(videoSectors, baseDir, DataFile);
// FileHelpers.ParseMonoAudioSectorsByChannel(SectorInfos, baseDir, DataFile);
// FileHelpers.ParseMonoAudioSectorsByEOR(monoAudioSectors, baseDir, DataFile);
// FileHelpers.ParseStereoAudioSectors(stereoAudioSectors, baseDir, DataFile);
// FileHelpers.ParseSectorsByEOR(SectorInfos, baseDir, DataFile);

// var paletteFileBytes = File.ReadAllBytes(@"C:\Dev\Personal\Projects\Gaming\CD-i\Extracted\Laser Lords\NewRecords\argos\data-eor\output\argos_d_5.bin");


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


// var audioFile = @"C:\Dev\Personal\Projects\Gaming\CD-i\Extracted\Laser Lords\NewRecords\argos_v\audio-mono-channel\argos_v_a_16.bin";

// var audioBytes = File.ReadAllBytes(audioFile);

// OutputAudio(audioBytes, audioFile, 18900, 4, true);


// var file = @"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\NewRecords\BurnCycle\eor\output\BurnCycle.rtr_eor_166873_392485296_1055.bin";

// var fileBytes = File.ReadAllBytes(file);
// var palette = File.ReadAllBytes(file).Skip(0x4a).Take(0x180).ToArray();
// var colors = ColorHelper.ConvertBytesToRGB(palette);

// var images = new List<Bitmap>();

// var initialBytes = fileBytes.Skip(0x478c).Take(0x6cf0).ToArray();
// var secondaryBytes = fileBytes.Skip(0xbd90).Take(0x9140).ToArray();
// var tertiaryBytes = fileBytes.Skip(0x157e4).Take(0x6cf0).ToArray();
// var quaternaryBytes = fileBytes.Skip(0x1cde8).ToArray();

// var combinedBytes = initialBytes.Concat(secondaryBytes).ToArray();
// combinedBytes = combinedBytes.Concat(tertiaryBytes).ToArray();
// combinedBytes = combinedBytes.Concat(quaternaryBytes).ToArray();

// ImageFormatHelper.Rle7_AllBytes(combinedBytes, colors, 384, images);

// using (var gifWriter = new GifWriter(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\NewRecords\BurnCycle\eor\output\output\BurnCycle.rtr_eor_1055_loop_250ms.bin.gif", 250, 0))
// {
//   foreach (var image in images)
//   {
//     gifWriter.WriteFrame(image);
//   }
// }

