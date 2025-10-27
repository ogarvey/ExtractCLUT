using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;

namespace ExtractCLUT.Games.PC.AlienTrilogy
{
    public static class GfxHelper
    {
        public static void ExtractGfxFile(string gfxFilePath)
        {
            using var gfxReader = new BinaryReader(File.OpenRead(gfxFilePath));
            gfxReader.ReadUInt32(); // Skip FORM
            var fileSize = gfxReader.ReadUInt32();
            var psxtInfo = Encoding.ASCII.GetString(gfxReader.ReadBytes(8));
            if (psxtInfo != "PSXTINFO")
                throw new InvalidDataException("Not a valid GFX file.");

            var infoSize = gfxReader.ReadBigEndianUInt32();
            if (infoSize != 0x10)
                throw new InvalidDataException("Unexpected PSXTINFO size.");

            var imgWidth = gfxReader.ReadUInt16();
            var imgHeight = gfxReader.ReadUInt16();

            gfxReader.ReadBytes(0x4); // Skip unknown data
            var fileCount = gfxReader.ReadUInt16();
            var fileCountConfirm = gfxReader.ReadUInt16();
            if (fileCount != fileCountConfirm)
                throw new InvalidDataException("File count mismatch.");

            var outputDir = Path.Combine(Path.GetDirectoryName(gfxFilePath)!, Path.GetFileName(gfxFilePath).Replace(".", "_"));
            Directory.CreateDirectory(outputDir);
            var transparencyDir = Path.Combine(outputDir, "Transparency");
            Directory.CreateDirectory(transparencyDir);

            gfxReader.ReadBytes(0x4); // Skip unknown data

            for (int i = 0; i < fileCount; i++)
            {
                var imageHeader = Encoding.ASCII.GetString(gfxReader.ReadBytes(4));
                if (!imageHeader.StartsWith("TP"))
                    throw new InvalidDataException("Expected IMG header.");

                var imageSize = gfxReader.ReadBigEndianUInt32();
                var imageData = gfxReader.ReadBytes((int)imageSize);
                
                var colorHeader = Encoding.ASCII.GetString(gfxReader.ReadBytes(4));
                if (!colorHeader.StartsWith("CL"))
                    throw new InvalidDataException("Expected CLUT header.");

                var colorSize = gfxReader.ReadBigEndianUInt32();
                gfxReader.ReadBytes(4); // Skip unknown data
                var colorData = gfxReader.ReadBytes((int)colorSize - 4);
                var pal = ColorHelper.ReadABgr15PaletteIS(colorData);

                var boxHeader = Encoding.ASCII.GetString(gfxReader.ReadBytes(4));
                if (!boxHeader.StartsWith("BX"))
                    throw new InvalidDataException("Expected BOX header.");
                
                var boxSize = gfxReader.ReadBigEndianUInt32();
                gfxReader.ReadBytes((int)boxSize); // Skip box data

                var image = ImageFormatHelper.GenerateIMClutImage(pal, imageData, imgWidth, imgHeight);
                var imagePath = Path.Combine(outputDir, $"Image_{i:D4}.png");
                image.SaveAsPng(imagePath);
                image = ImageFormatHelper.GenerateIMClutImage(pal, imageData, imgWidth, imgHeight, true);
                var transparencyImagePath = Path.Combine(transparencyDir, $"Image_{i:D4}_Transparency.png");
                image.SaveAsPng(transparencyImagePath);
            }
        }
    }
}
