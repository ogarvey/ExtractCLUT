using ExtractCLUT.Games.Generic;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using System.Text;

namespace ExtractCLUT.Games.PC
{
    public static class PerfectAssassin
    {

        public static void ParsePCFile(string pcFile)
        {
            var outputFolder = Path.Combine(Path.GetDirectoryName(pcFile)!, Path.GetFileNameWithoutExtension(pcFile) + "_output");

            var fileData = File.ReadAllBytes(pcFile);
            var magic = Encoding.ASCII.GetString(fileData, 0, 3);
            if (magic == "CWC")
            {
                var outputFile = Path.GetFileNameWithoutExtension(pcFile) + "_decompressed.bin";
                CausewayDecompressor.DecompressFile(pcFile, outputFile);
                fileData = File.ReadAllBytes(outputFile);
                File.Delete(outputFile);
            }
            using var pcReader = new BinaryReader(new MemoryStream(fileData));
            var count = pcReader.ReadByte() * pcReader.ReadByte();
            if (count > 1) Directory.CreateDirectory(outputFolder);
            pcReader.ReadBytes(2); // skip reserved bytes
            var paletteData = pcReader.ReadBytes(256 * 3);
            var palette = ColorHelper.ConvertBytesToRgbIS(paletteData, true);

            var offsetList = new List<uint>();
            for (int i = 0; i < count; i++)
            {
                offsetList.Add(pcReader.ReadUInt32());
            }

            for (int i = 0; i < count; i++)
            {
                pcReader.BaseStream.Seek(offsetList[i], SeekOrigin.Begin);
                var header = new PCSpriteHeader
                {
                    XOffset = pcReader.ReadInt16(),
                    YOffset = pcReader.ReadInt16(),
                    Unk1 = pcReader.ReadUInt32(),
                    Unk2 = pcReader.ReadUInt32(),
                    Width = pcReader.ReadUInt32(),
                    Height = pcReader.ReadUInt32()
                };
                var dataSize = header.Width * header.Height;
                var imageData = pcReader.ReadBytes((int)dataSize);
                var image = ImageFormatHelper.GenerateIMClutImage(palette, imageData, (int)header.Width, (int)header.Height, true);
                var outputPng = count > 1 ? Path.Combine(outputFolder, $"{i}_{header.XOffset}_{header.YOffset}.png") : $"{Path.GetFileNameWithoutExtension(pcFile)}.png";
                image.SaveAsPng(outputPng);
            }

            if (count > 1)
            {
                var outputDir = Path.Combine(outputFolder, "aligned_output");
                Directory.CreateDirectory(outputDir);
                FileHelpers.AlignSprite(outputFolder, outputDir, ExpansionOrigin.BottomCenter);
                // move aligned files back to main output folder
                foreach (var alignedFile in Directory.GetFiles(outputDir, "*.png"))
                {
                    var destFile = Path.Combine(outputFolder, Path.GetFileName(alignedFile));
                    File.Move(alignedFile, destFile, true);
                }
                Directory.Delete(outputDir);
                FileHelpers.RenameIndexedImages(outputFolder);
            }
        }

    }

    public class PCSpriteHeader
    {
        public short XOffset { get; set; }
        public short YOffset { get; set; }
        public uint Unk1 { get; set; }
        public uint Unk2 { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }
    }
}
