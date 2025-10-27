using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using Microsoft.VisualBasic;

namespace ExtractCLUT.Games.PC
{
    public static class DeathGate
    {

        public static void ExtractAll(string picDir)
        {
            var picFiles = Directory.GetFiles(picDir, "*.PIC");

            var outputDir = Path.Combine(picDir, "output");
            Directory.CreateDirectory(outputDir);

            var allPaletteOutputDir = Path.Combine(outputDir, "palettes");
            Directory.CreateDirectory(allPaletteOutputDir);

            var FlagCoordinates = (1 << 0);
            var FlagPalette = (1 << 12);

            foreach (var picFile in picFiles)
            {
                var picFileOutputDir = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(picFile));
                Directory.CreateDirectory(picFileOutputDir);
                var headers = new List<DeathGatePicHeader>();
                using var reader = new BinaryReader(File.OpenRead(picFile));
                var offset = reader.ReadUInt32();
                while (offset != 0 && reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    var header = new DeathGatePicHeader
                    {
                        Offset = offset,
                        Flags = reader.ReadUInt16(),
                        Width = reader.ReadUInt16(),
                        Height = reader.ReadUInt16(),
                        ImageOffset = 0
                    };
                    if ((header.Flags & FlagCoordinates) != 0)
                    {
                        header.ImageOffset += 4;
                    }
                    if ((header.Flags & FlagPalette) != 0)
                    {
                        header.ImageOffset += 768;
                    }
                    reader.ReadBytes(2);
                    headers.Add(header);
                    offset = reader.ReadUInt32();
                }
                var paletteList = new List<List<Color>>();
                var paletteHeaders = headers.Where(x => x.HasPalette).ToList();

                // get headers without palette
                var imageHeaders = headers.Where(x => !x.HasPalette).ToList();

                foreach (var paletteHeader in paletteHeaders)
                {
                    var paletteOutputDir = Path.Combine(picFileOutputDir, $"palette_{paletteHeader.Offset}");
                    Directory.CreateDirectory(paletteOutputDir);

                    reader.BaseStream.Seek(paletteHeader.Offset, SeekOrigin.Begin);
                    var paletteData = reader.ReadBytes(768);
                    File.WriteAllBytes(Path.Combine(allPaletteOutputDir, $"{Path.GetFileNameWithoutExtension(picFile)}_palette_{paletteHeader.Offset:X8}.bin"), paletteData);
                    var palette = ColorHelper.ConvertBytesToRGB(paletteData, true);

                    if (paletteHeader.Width != 0 && paletteHeader.Height != 0)
                    {
                        var imageData = reader.ReadBytes(paletteHeader.Width * paletteHeader.Height);
                        //File.WriteAllBytes(Path.Combine(paletteBinaryOutputDir, $"{(paletteHeader.Offset + paletteHeader.ImageOffset):X8}_{paletteHeader.Flags:X4}.bin"), imageData);
                        var image = ImageFormatHelper.GenerateClutImage(palette, imageData, paletteHeader.Width, paletteHeader.Height);
                        // create filename for image using offset and flags in hex
                        var filename = $"{paletteHeader.Offset:X8}_{paletteHeader.Flags:X2}.png";
                        image.Save(Path.Combine(paletteOutputDir, filename), ImageFormat.Png);
                    }
                    foreach (var iHeader in imageHeaders)
                    {
                        if (iHeader.Width == 0 || iHeader.Height == 0) continue;
                        reader.BaseStream.Seek(iHeader.Offset + iHeader.ImageOffset, SeekOrigin.Begin);
                        var imageData = reader.ReadBytes(iHeader.Width * iHeader.Height);
                        //File.WriteAllBytes(Path.Combine(paletteBinaryOutputDir, $"{(iHeader.Offset + iHeader.ImageOffset):X8}_{iHeader.Flags:X4}.bin"), imageData);
                        var image = ImageFormatHelper.GenerateClutImage(palette, imageData, iHeader.Width, iHeader.Height);
                        var filename = $"{iHeader.Offset + iHeader.ImageOffset:X8}_{iHeader.Flags:X4}.png";
                        image.Save(Path.Combine(paletteOutputDir, filename), ImageFormat.Png);
                    }
                }
            }

        }
        class DeathGatePicHeader
        {
            public uint Offset { get; set; }
            public ushort Flags { get; set; }
            public ushort Width { get; set; }
            public ushort Height { get; set; }
            public int ImageOffset { get; set; }
            public bool HasPalette => (Flags & (1 << 12)) != 0;
            public bool HasCoordinates => (Flags & (1 << 0)) != 0;
        }


    }
}
