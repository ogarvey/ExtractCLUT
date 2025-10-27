using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Games.Generic.ScummVM.Decompression;

namespace ExtractCLUT.Games.PC
{
    public static class UfoHelper
    {
        public static void ExtractDataFile(string dataFile, string outputFolder)
        {
            using var datReader = new BinaryReader(File.OpenRead(dataFile));
            datReader.BaseStream.Seek(0xC, SeekOrigin.Begin);
            var count = datReader.ReadUInt32();
            datReader.ReadUInt32(); // padding
            var entries = new List<UfoDatEntry>();
            for (var i = 0; i < count; i++)
            {
                var entry = new UfoDatEntry(
                    datReader.ReadUInt32(),
                    datReader.ReadUInt32(),
                    datReader.ReadUInt32(),
                    datReader.ReadUInt32());
                entries.Add(entry);
            }

            foreach (var entry in entries)
            {
                datReader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
                var data = datReader.ReadBytes((int)entry.outSize2);
                var dclStream = new MemoryStream(data);
                var outputStream = new MemoryStream();

                var decompressor = new DecompressorDCL();
                decompressor.Unpack(dclStream, outputStream, entry.outSize1, true);
                var outputFile = Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(dataFile)}_{entry.Offset:x8}.bin");
                File.WriteAllBytes(outputFile, outputStream.ToArray());
            }
        }
    }

    public record UfoDatEntry(uint Offset, uint outSize1, uint type, uint outSize2);
}
