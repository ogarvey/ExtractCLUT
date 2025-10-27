using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;

namespace ExtractCLUT.Games.PC.Interspective
{
    public static class ResourceHelper
    {
        public static void ExtractIUCDat(string iucFile)
        {
            var iucOutputDir = Path.Combine(Path.GetDirectoryName(iucFile)!, $"{Path.GetFileNameWithoutExtension(iucFile)}_output");
            Directory.CreateDirectory(iucOutputDir);

            var decompressedData = new List<byte>();
            using var iucReader = new BinaryReader(File.OpenRead(iucFile));
            var index = 0;
            while (iucReader.BaseStream.Position < iucReader.BaseStream.Length)
            {
                var width = iucReader.ReadUInt16();
                var height = iucReader.ReadUInt16();

                for (int i = 0; i < height; i++)
                {
                    var lineData = DecodePCXRle(iucReader, width);
                    decompressedData.AddRange(lineData);
                }

                var palIndicator = iucReader.ReadByte();
                if (palIndicator != 0x0C)
                {
                    throw new Exception("Unexpected palette indicator byte");
                }


                var iucPalData = iucReader.ReadBytes(0x300);
                var iucPal = ColorHelper.ConvertBytesToRgbIS(iucPalData);

                //File.WriteAllBytes(Path.Combine(iucOutputDir, "iuc_001.bin"), decompressedData.ToArray());
                var image = ImageFormatHelper.GenerateIMClutImage(iucPal, decompressedData.ToArray(), width, height, true);
                image.Save(Path.Combine(iucOutputDir, $"{index++:D3}.png"));
                decompressedData.Clear();
            }
        }
        
        public static byte[] DecodePCXRle(BinaryReader rleReader, int expectedSize)
        {
            List<byte> output = new List<byte>();
            int i = 0;
            while (i < rleReader.BaseStream.Length && output.Count < expectedSize)
            {
                byte b = rleReader.ReadByte();
                if (b > 0xC0)
                {
                    int count = b & 0x3f;
                    var val = rleReader.ReadByte();
                    for (int j = 0; j < count; j++)
                    {
                        if (i < rleReader.BaseStream.Length && output.Count < expectedSize)
                            output.Add(val);
                    }
                }
                else
                {
                    output.Add(b);
                }
            }
            return output.ToArray();
        }
    }
}
