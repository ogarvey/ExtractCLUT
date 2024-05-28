using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using ExtractCLUT.Model;
using Color = System.Drawing.Color;
using OGLibCDi.Models;
using OGLibCDi.Helpers;
using ImageFormatHelper = ExtractCLUT.Helpers.ImageFormatHelper;
using ColorHelper = OGLibCDi.Helpers.ColorHelper;
using AudioHelper = ExtractCLUT.Helpers.AudioHelper;

namespace ExtractCLUT.Games
{
    public static class BurnCycle
    {
        // 0ffset 0x00 (2byte value) in the file is assumed to be the width of the image
        // Offset 0x02 (2byte value) in the file is assumed to be the height of the image
        // Offset ?? 0x06/0x08 (4byte/2byte)?? in the file seems to be a flag to indicate the type of data in the file
        // 0x0308 = CLUT7 - Byte Count Encoded
        // 0x2308 = CLUT7 - Offset Encoded
        // 0x0508 = DYUV - Byte Count Encoded

        // Offset 0x0a (4byte value) in the file indicates start of byte counts or offsets, so far only 0x1e has been seen
        // Offset 0x0e (4byte value) in the file indicates total sector count in overall "file"
        // Offset 0x16 (4byte value) in the file unknown, so far either 4bytes of 0x00, or 3 bytes of 0x00 followed by 0x80
        // - Only appears in CLUT7 files - count of colours in palette?

        // Offset 0x1e in the file is the start of the 4 byte counts or offsets (240 records of 4 bytes each)
        // Offset 0x3de in the file is the start of the ?? line ?? data (240 records of 2 bytes each)
        // Offset 0x5be in the file is the start of the palette data (384 bytes unindexed)
        // Offset 0x73e in the file is the start of the image data

        private const string MAIN_DATA_FILE = @"BurnCycle.rtr";
        private const string MAIN_OUTPUT_FOLDER = @"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\output";

        public static void ExtractAudio()
        {

            var BurnCycleFile = @"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\BurnCycle.rtf";
            var BurnCycle = new CdiFile(BurnCycleFile);

            var audioSectors = BurnCycle.Sectors.Where(x => x.Coding.VideoString == "DYUV" && x.Channel == 17)
              .OrderBy(x => x.SectorIndex).ToList();

            var audioData = new List<byte[]>();

            foreach (var (sector, index) in BurnCycle.Sectors.WithIndex())
            {
                if (sector.Coding.VideoString == "DYUV" && sector.Channel == 17)
                {
                    audioData.Add(sector.GetSectorData().Take(0x900).ToArray());
                }
                if (sector.SubMode.IsEOR && audioData.Count > 0)
                {
                    var errorCount = 0;
                    var outputFileName = Path.Combine(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\Output\output\audio", $"audio_18900_4bps_stereo_{sector.SectorIndex}.bin");
                    var bytes = audioData.SelectMany(x => x).ToArray();
                    try
                    {
                        AudioHelper.OutputAudio(bytes, outputFileName, 18900, 4, false);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Console.WriteLine($"Error decoding audio (18900, 4, false): {ex.Message}");
                    }
                    outputFileName = Path.Combine(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\Output\output\audio", $"audio_18900_4bps_mono_{sector.SectorIndex}.bin");
                    try
                    {
                        AudioHelper.OutputAudio(bytes, outputFileName, 18900, 4, true);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Console.WriteLine($"Error decoding audio (18900, 4, true): {ex.Message}");
                    }
                    outputFileName = Path.Combine(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\Output\output\audio", $"audio_18900_8bps_stereo_{sector.SectorIndex}.bin");
                    try
                    {
                        AudioHelper.OutputAudio(bytes, outputFileName, 18900, 8, false);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Console.WriteLine($"Error decoding audio (18900, 8, false): {ex.Message}");
                    }
                    outputFileName = Path.Combine(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\Output\output\audio", $"audio_18900_8bps_mono_{sector.SectorIndex}.bin");
                    try
                    {
                        AudioHelper.OutputAudio(bytes, outputFileName, 18900, 8, true);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Console.WriteLine($"Error decoding audio (18900, 8, false): {ex.Message}");
                    }
                    outputFileName = Path.Combine(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\Output\output\audio", $"audio_38700_4bps_stereo_{sector.SectorIndex}.bin");
                    try
                    {
                        AudioHelper.OutputAudio(bytes, outputFileName, 38700, 4, false);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Console.WriteLine($"Error decoding audio (38700, 4, false): {ex.Message}");
                    }
                    outputFileName = Path.Combine(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\Output\output\audio", $"audio_38700_4bps_mono_{sector.SectorIndex}.bin");
                    try
                    {
                        AudioHelper.OutputAudio(bytes, outputFileName, 38700, 4, true);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Console.WriteLine($"Error decoding audio (38700, 4, true): {ex.Message}");
                    }
                    outputFileName = Path.Combine(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\Output\output\audio", $"audio_38700_8bps_stereo_{sector.SectorIndex}.bin");
                    try
                    {
                        AudioHelper.OutputAudio(bytes, outputFileName, 38700, 8, false);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Console.WriteLine($"Error decoding audio (38700, 8, false): {ex.Message}");
                    }
                    outputFileName = Path.Combine(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\Output\output\audio", $"audio_38700_8bps_mono_{sector.SectorIndex}.bin");
                    try
                    {
                        AudioHelper.OutputAudio(bytes, outputFileName, 38700, 8, true);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Console.WriteLine($"Error decoding audio (38700, 8, true): {ex.Message}");
                    }
                    if (errorCount == 8)
                    {
                        File.WriteAllBytes(Path.Combine(@"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\Output\output\audio", $"audio_{sector.SectorIndex}_ERROR.bin"), bytes);
                    }
                    audioData.Clear();
                }
            }

        }
        public static void ExtractClutChannel(CdiFile bcDataFile, int channel)
        {
            var clut7Sectors = bcDataFile.VideoSectors
                .Where(x => x.Coding.VideoString == "CLUT7" && x.Channel == channel).OrderBy(x => x.SectorIndex).ToList();
            var counter = 0;
            while (clut7Sectors.Count > 0)
            {
                var initialSectorData = clut7Sectors.First().GetSectorData();
                var folder = Path.Combine(MAIN_OUTPUT_FOLDER, $"Channel{channel}\\initial");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, $"Channel{channel}_{clut7Sectors.First().SectorIndex}.bin"), initialSectorData);
                var width = BitConverter.ToInt16(initialSectorData.Take(2).Reverse().ToArray(), 0);
                var height = BitConverter.ToInt16(initialSectorData.Skip(2).Take(2).Reverse().ToArray(), 0);
                var flags = BitConverter.ToInt16(initialSectorData.Skip(8).Take(2).Reverse().ToArray(), 0);
                var offsetStart = BitConverter.ToInt16(initialSectorData.Skip(0xc).Take(2).Reverse().ToArray(), 0);
                var sectorCount = BitConverter.ToInt16(initialSectorData.Skip(0x10).Take(2).Reverse().ToArray(), 0);
                var palette = ColorHelper.ConvertBytesToRGB(initialSectorData.Skip(0x3de).Take(0x384).ToArray());
                if(flags == 8968)
                {
                    var offsetLists = new List<List<int>>();
                    var offsetList = new List<int>();

                    for (int i = 0x1e; i < 0x3de; i += 4)
                    {
                        var offset = BitConverter.ToInt32(initialSectorData.Skip(i).Take(4).Reverse().ToArray(), 0);
                        if (offset == 0)
                        {
                            offsetLists.Add(offsetList);
                            offsetList = new List<int>();
                        }
                        else
                        {
                            offsetList.Add(offset);
                        }
                    }
                    offsetLists.Add(offsetList);
                    var imageData = clut7Sectors.Take(sectorCount).Select(s => FileHelpers.RemoveTrailingZeroes(s.GetSectorData())).ToList();
                    var chunks = new List<byte[]>();
                    foreach (var (offsets, olIndex) in offsetLists.WithIndex())
                    {
                        foreach (var (offset, oIndex) in offsets.WithIndex())
                        {
                            var bytesToTake = (oIndex == offsets.Count - 1) ? imageData[olIndex].Length - offset : offsets[oIndex + 1] - offset;
                            var chunkData = imageData[olIndex].Skip(offset).Take(bytesToTake).ToArray();
                            chunks.Add(chunkData);
                        }
                    }

                    var data = chunks.SelectMany(x => x).ToArray();
                    //File.WriteAllBytes(Path.Combine(Path.Combine(MAIN_OUTPUT_FOLDER, "Channel2"), $"Channel2_{counter}.bin"), data);
                    var image = ImageFormatHelper.GenerateRle7Image(palette, data, width, height);
                    folder = Path.Combine(MAIN_OUTPUT_FOLDER, $"Channel{channel}\\images");
                    Directory.CreateDirectory(folder);
                    image.Save(Path.Combine(folder, $"Channel{channel}_{counter}.png"), ImageFormat.Png);
                }
                clut7Sectors = clut7Sectors.Skip(sectorCount).ToList();
                counter++;
            }

        }

        public static List<(int, int,int)> ParseOffsetData(byte[] offsetData)
        {
            var offsets = new List<(int,int,int)>();

            byte[] sequence = [0x00, 0x05, 0x00, 0x14];
            int index = 0;

            while (index < offsetData.Length)
            {
                index = FindOffsetSequence(offsetData, sequence, index);

                if (index == -1)
                {
                    break; // No more sequences found
                }

                while (index + 20 <= offsetData.Length && offsetData.Skip(index).Take(4).SequenceEqual(sequence))
                {
                    // Extract the last 4 bytes of the 20-byte chunk as an integer (little endian)
                    var value = BitConverter.ToInt32(offsetData.Skip(index + 16).Take(4).Reverse().ToArray(), 0);
                    var fIndex = BitConverter.ToInt16(offsetData.Skip(index + 6).Take(2).Reverse().ToArray(), 0);

                    offsets.Add((value,index,fIndex));

                    index += 20;
                }

                index++; // Skip a byte and continue looking for the sequence
            }
            return offsets;
        }

        private static int FindOffsetSequence(byte[] data, byte[] sequence, int startIndex)
        {
            for (int i = startIndex; i <= data.Length - sequence.Length; i++)
            {
                if (data.Skip(i).Take(sequence.Length).SequenceEqual(sequence))
                {
                    return i;
                }
            }

            return -1; // Sequence not found
        }

        public static List<int> ParseSectorCounts(byte[] data)
        {
            var length = BitConverter.ToInt16(data.Take(2).Reverse().ToArray(), 0);
            if (length == 2) { return new List<int>(); }

            var sectorCounts = new List<int>();

            if (length == 4) {
                var value = BitConverter.ToInt16(data.Skip(2).Take(2).Reverse().ToArray(), 0);
                sectorCounts.Add(value);
                return sectorCounts;
            }

            for (int i = 2; i < length - 2; i += 2)
            {
                var bytes = data.Skip(i).Take(2).Reverse().ToArray();
                var value = BitConverter.ToInt16(bytes, 0);
                sectorCounts.Add(value);
            }
            //if (sectorCounts.Count > 0) sectorCounts[0] = sectorCounts[0] - 1; // first sector count is always 1 less than the value in the file
            return sectorCounts;
        }

        public static void ParseOffsetsFile(string dataFile, bool isCLUT7 = true)
        {
            var fileData = File.ReadAllBytes(dataFile);

            var fileChunks = new List<byte[]>();

            for (int i = 0; i < fileData.Length; i += 0x914)
            {
                fileChunks.Add(fileData.Skip(i).Take(0x914).ToArray());
            }

            var palette = ColorHelper.ConvertBytesToRGB(fileData.Skip(0x3de).Take(0x180).ToArray());

            var offsets = new List<int>();

            for (int i = 0x1e; i < 0x3de; i += 4)
            {
                offsets.Add(BitConverter.ToInt32(fileData.Skip(i).Take(4).Reverse().ToArray(), 0));
            }
            var outputDir = Path.Combine(Path.GetDirectoryName(dataFile), "ParsedOffsetData");
            Directory.CreateDirectory(outputDir);
            var currentFileChunk = 0;
            var byteList = new List<byte[]>();
            foreach (var (offset, oIndex) in offsets.WithIndex())
            {
                if (offset == 0) currentFileChunk++;
                
                var bytesToTake = (oIndex == offsets.Count - 1 || offsets[oIndex + 1] == 0)? 0x914 - offset : offsets[oIndex + 1] - offset;
                var bytes = (oIndex == offsets.Count - 1 || offsets[oIndex + 1] == 0) ? FileHelpers.RemoveTrailingZeroes(fileChunks[currentFileChunk].Skip(offset).Take(bytesToTake).ToArray()) : fileChunks[currentFileChunk].Skip(offset).Take(bytesToTake).ToArray();
                File.WriteAllBytes(Path.Combine(outputDir, $"offset_{oIndex}_{offset}.bin"), bytes);
                
                if (bytes[^1] == 0x80) {
                    // add an extra byte to the end of the file
                    bytes = bytes.Concat(new byte[] { 0x00 }).ToArray();
                }
                byteList.Add(bytes);
            }

            var image = ImageFormatHelper.GenerateRle7Image(palette, byteList.SelectMany(s => s).ToArray(), 384, 240, false);
            
            image.Save(Path.Combine(outputDir, Path.GetFileNameWithoutExtension(dataFile) + ".png"), ImageFormat.Png);
        }

        public static void ParseByteCountFile(string dataFile, bool isCLUT7 = true)
        {
            var byteCountStart = 0x1e;
            var byteCounts = new List<uint>();

            var dataStart = isCLUT7 ? 0x73e : 0x5be;
            var lineNumsStart = 0x3de;
            var data = File.ReadAllBytes(dataFile);

            for (int i = 0; i < 240; i++)
            {
                var byteCount = BitConverter.ToUInt32(data.Skip(byteCountStart + (i * 4)).Take(4).Reverse().ToArray(), 0);
                byteCounts.Add(byteCount);
            }

            var lineNums = new List<int>();

            for (int i = 0; i < 240; i++)
            {
                var lineNum = data.Skip(lineNumsStart + (i * 2) + 1).Take(1).First();
                lineNums.Add(lineNum);
            }

            var palette = ColorHelper.ConvertBytesToRGB(data.Skip(0x5be).Take(0x180).ToArray());

            data = data.Skip(0x5be).ToArray();
            var lines = new List<byte[]>();
            var orderedLines = new List<byte[]>();
            foreach (var (byteCount, index) in byteCounts.WithIndex())
            {
                var totalLines = byteCount / 384;
                if (byteCount == 0)
                {
                    lines.Add([]);
                    continue;
                }
                for (int i = 0; i < totalLines; i++)
                {
                    var bytes = data.Take((int)(byteCount / totalLines)).ToArray();
                    lines.Add(bytes);
                    data = data.Skip((int)(byteCount / totalLines)).ToArray();
                    while (data.Length > 0 && data[0] == 0x00)
                    {
                        data = data.Skip(1).ToArray();
                    }
                }
            }

            var outputDir = @"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\Output\output\binary";
            Directory.CreateDirectory(outputDir);

            foreach (var (line, index) in lineNums.WithIndex())
            {
                orderedLines.Add(lines[line]);
            }

            File.WriteAllBytes(Path.Combine(outputDir, Path.GetFileNameWithoutExtension(dataFile)+".bin"), orderedLines.SelectMany(x => x).ToArray());

        }

        public static List<int> FindPaletteSequence(byte[] data, byte?[] mask)
        {
            byte[] pattern = [0x00, 0x09, 0x01, 0x8C, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x80, 0x80];

            List<int> positions = [];
            int patternLength = pattern.Length;

            for (int i = 0; i <= data.Length - patternLength; i++)
            {
                bool isMatch = true;
                for (int j = 0; j < patternLength; j++)
                {
                    // Skip comparison for bytes that are wildcards (null in the mask)
                    if (mask[j].HasValue && data[i + j] != pattern[j])
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    positions.Add(i);
                }
            }

            return positions;
        }
    }

    public class BurnCycleOffsetData
    {
        public int Offset { get; set; }
        public int Sector { get; set; }
        public int OffsetIndex { get; set; }
        public required BurnCyclePaletteData Palette { get; set; }
    }

    public class BurnCyclePaletteData
    {
        public int Offset { get; set; }
        public int Sector { get; set; }
        public int FirstFrame { get; set; }
        public required List<Color> Palette { get; set; }
    }

    public class BurnCycleSectorData
    {
        public int Index { get; set; }
        public List<BurnCycleOffsetData> Offsets { get; set; }
        public List<BurnCyclePaletteData>? Palettes { get; set; }
        public List<int> SectorCounts { get; set; }
        public List<byte[]>? RleByteGroups { get; set; }
    }
}


