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
using ImageFormatHelper = OGLibCDi.Helpers.ImageFormatHelper;
using ColorHelper = OGLibCDi.Helpers.ColorHelper;

namespace ExtractCLUT.Games
{
    public static class BurnCycle
    {
        private const string MAIN_DATA_FILE = @"BurnCycle.rtr";
        private const string MAIN_OUTPUT_FOLDER = @"C:\Dev\Projects\Gaming\CD-i\Burn Cycle\output";

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


