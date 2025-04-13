using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC
{
    public class StarTrek
    {

        public static void ExtractST(string indexFile, string dataFile, string runFile)
        {
            var indexBytes = File.ReadAllBytes(indexFile);

            // index file is a list of 0xE byte entries, each entry has 8 bytes for the filename, 3 bytes for the extension, and 3 bytes for the offset
            var indexEntries = new List<(string, string, uint)>();

            for (int i = 0; i < indexBytes.Length; i += 0xE)
            {
                var fileName = Encoding.ASCII.GetString(indexBytes.Skip(i).Take(8).ToArray()).TrimEnd('\0');
                var extension = Encoding.ASCII.GetString(indexBytes.Skip(i + 8).Take(3).ToArray()).TrimEnd('\0');
                var offset = BitConverter.ToUInt32(indexBytes.Skip(i + 11).Take(3).Append((byte)0).ToArray(), 0);
                Console.WriteLine($"Found {fileName}.{extension} at {offset}");
                indexEntries.Add((fileName, extension, offset));
            }

            // iterate through the index entries and extract the data
            var dataBytes = File.ReadAllBytes(dataFile);
            var runBytes = File.ReadAllBytes(runFile);

            var outputFolder = Path.Combine(Path.GetDirectoryName(dataFile), "output", Path.GetFileNameWithoutExtension(dataFile));
            Directory.CreateDirectory(outputFolder);

            for (int i = 0; i < indexEntries.Count; i++)
            {
                var count = 1;
                var secondaryOffsets = new List<uint>();
                var (fileName, extension, offset) = indexEntries[i];
                if ((offset & 0x800000) != 0) {
                    count = (int)((offset >> 16) & 0x7F);
                    offset = offset & 0xFFFF;
                    // use offset as a pointer to the run file to get the actual offset for the data file
                    var newoffset = BitConverter.ToUInt32(runBytes.Skip((int)offset).Take(3).Append((byte)0).ToArray(), 0);
                    offset+=3;
                    for (int j = 0; j < count-1; j++) {
                        var previous = secondaryOffsets.Count > 0 ? secondaryOffsets[j - 1] : newoffset;
                        var secondaryOffset = BitConverter.ToUInt16(runBytes.Skip((int)offset + j * 2).Take(2).ToArray(), 0) + previous;
                        secondaryOffsets.Add(secondaryOffset);
                    }
                    offset = newoffset;
                }

                for (int j = 0; j < count; j++)
                {
                    var dataOffset = j == 0 ? offset : secondaryOffsets[j - 1];
                    var data = dataBytes.Skip((int)dataOffset).ToArray();
                    var outputFileName = count > 1 ? Path.Combine(outputFolder, $"{fileName}_{j}.{extension}") : Path.Combine(outputFolder, $"{fileName}.{extension}");
                    var uncompressedSize = BitConverter.ToUInt16(data.Take(2).ToArray(), 0);
                    var compressedSize = BitConverter.ToUInt16(data.Skip(2).Take(2).ToArray(), 0);
                    var compressedData = data.Skip(4).Take(compressedSize).ToArray();
                    var decompressedData = FileHelpers.DecompressLZSS(compressedData);
                    Console.WriteLine($"Sanity Check {fileName}.{extension}: {uncompressedSize == decompressedData.Length}");
                    // remove any null characters from filename
                    outputFileName = outputFileName.Replace("\0", "");
                    File.WriteAllBytes(outputFileName, decompressedData);
                }
            }
        }

    }
}
