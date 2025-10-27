using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ExtractCLUT.Games.PC.AlienCabal
{
    public static class GobExtractor
    {
        public static void ExtractGob(string filePath)
        {
            using var gobReader = new BinaryReader(File.OpenRead(filePath));

            var mainOutDir = Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath) + "_Extracted");
            Directory.CreateDirectory(mainOutDir);

            var wavOutDir = Path.Combine(mainOutDir, "WAVs");
            Directory.CreateDirectory(wavOutDir);

            var imgOutDir = Path.Combine(mainOutDir, "Images");
            Directory.CreateDirectory(imgOutDir);

            var otherOutDir = Path.Combine(mainOutDir, "Other");
            Directory.CreateDirectory(otherOutDir);

            gobReader.ReadInt16();
            var palOffset = gobReader.ReadUInt32();

            gobReader.BaseStream.Seek(palOffset + 6, SeekOrigin.Begin);
            var palData = gobReader.ReadBytes(0x300);
            var palette = ConvertCabalPal(palData);

            var otherIndex = 0;
            while (gobReader.BaseStream.Position < gobReader.BaseStream.Length)
            {
                var blockStart = gobReader.BaseStream.Position;
                var typeFlag = gobReader.ReadUInt16();
                var blockLength = gobReader.ReadUInt32();

                switch (typeFlag)
                {
                    case 0x0E:
                        var headerLength = gobReader.ReadUInt16();
                        var name = gobReader.ReadNullTerminatedString();
                        gobReader.BaseStream.Seek(blockStart + 0x16, SeekOrigin.Begin);
                        var width = gobReader.ReadUInt16();
                        var height = gobReader.ReadUInt16();
                        gobReader.ReadUInt16();
                        var imgData = gobReader.ReadBytes((int)(blockLength - headerLength));
                        var image = ImageFormatHelper.GenerateIMClutImage(palette, imgData, width, height, true);
                        image.Mutate(x => x.Flip(FlipMode.Vertical));
                        var imgPath = Path.Combine(imgOutDir, name + ".png");
                        image.Save(imgPath);
                        break;
                    case 0x1D: // headerless wav data
                        var wavData = gobReader.ReadBytes((int)(blockLength - 6));
                        // convert to wav file
                        break;
                    case 0x0F: // some kind of data, fall through for now
                    default:
                        var otherData = gobReader.ReadBytes((int)(blockLength - 6));
                        var otherFilePath = Path.Combine(otherOutDir, $"other_{otherIndex:D4}.bin");
                        File.WriteAllBytes(otherFilePath, otherData);
                        otherIndex++;
                        break;
                }
            }
        }

        public static List<Color> ConvertCabalPal(byte[] bytes)
        {
            var rBytes = bytes.Take(0x100).ToArray();
            var gBytes = bytes.Skip(0x100).Take(0x100).ToArray();
            var bBytes = bytes.Skip(0x200).Take(0x100).ToArray();
            var colors = new List<Color>();

            for (int i = 0; i < 256; i++)
            {
                var r = rBytes[i];
                var g = gBytes[i];
                var b = bBytes[i];
                Color color = Color.FromRgb(r, g, b);
                colors.Add(color);
            }

            return colors;
        }
    }
}
