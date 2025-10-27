using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using ImageFormatHelper = ExtractCLUT.Helpers.ImageFormatHelper;
using static ExtractCLUT.Helpers.FileHelpers;
using OGLibCDi.Models;
using System.Drawing.Imaging;
using System.Drawing;
using OGLibCDi.Enums;

namespace ExtractCLUT.Games
{
    public class SpriteHeader
    {
        public short Width { get; set; }
        public short Height { get; set; }
        public short OffsetX { get; set; }
        public short OffsetY { get; set; }
    }

    public static class PyramidHelper
    {
        public static void ExtractAnims(string inputFolder)
        {
            var animFile = Path.Combine(inputFolder, "pyranim.rtr");
            var dataFile = Path.Combine(inputFolder, "pyrdata.rtr");
            var dataCdiFile = new CdiFile(dataFile);
            var animCdiFile = new CdiFile(animFile);

            var paletteData = dataCdiFile.DataSectors.First().GetSectorData();
            var palette = ColorHelper.ReadClutBankPalettes(paletteData, 2);

            var outputDir = Path.Combine(inputFolder, "Anims_Output");
            Directory.CreateDirectory(outputDir);

            var animSectors = animCdiFile.Sectors;
            var animSectorList = new List<CdiSector>();
            foreach (var animSector in animSectors)
            {
                if (animSector.GetSectorType() == CdiSectorType.Data) animSectorList.Add(animSector);
                if (animSector.SubMode.IsEOR || animSector == animSectors.Last())
                {
                    if (animSectorList.Count == 0) continue;
                    var animOutputDir = Path.Combine(outputDir, $"{animSectorList.First().Channel}_{animSectorList.First().SectorIndex}");
                    Directory.CreateDirectory(animOutputDir);
                    var frames = animSectorList.Count / 7;
                    for (int i = 0; i < frames; i++)
                    {
                        var frameSectors = animSectorList.Skip(i * 7).Take(7).ToList();
                        var frameData = frameSectors.SelectMany(x => x.GetSectorData()).ToArray();
                        var frameImage = ImageFormatHelper.GenerateRle7Image(palette, frameData, 384, 240, true);
                        var output = Path.Combine(animOutputDir, $"{i}.png");
                        frameImage.Save(output, ImageFormat.Png);
                    }
                    animSectorList.Clear();
                }
            }
        }

        public static void ExtractSprites(string inputFolder, List<Color> palette)
        {
            var file = Path.Combine(inputFolder, "pyrdata.rtr");

            var cdiFile = new CdiFile(file);

            var outputDir = Path.Combine(inputFolder, "Output", Path.GetFileNameWithoutExtension(file));
            Directory.CreateDirectory(outputDir);

            var dataSectors = cdiFile.DataSectors.OrderBy(s => s.SectorIndex).ToList();
            var currentChannel = dataSectors.First().Channel;
            var sectorList = new List<CdiSector>();

            foreach (var sector in dataSectors)
            {
                if (sector.Channel != currentChannel || sector == dataSectors.Last())
                {
                    var data = sectorList.SelectMany(x => x.GetSectorData()).ToArray();
                    var sIndex = sectorList.First().SectorIndex;
                    // check first three bytes of data for CPL magic number
                    if (data.Length > 3 && data[0] == 0x43 && data[1] == 0x50 && data[2] == 0x4c)
                    {
                        var spriteOutputDir = Path.Combine(outputDir, $"{currentChannel}_{sIndex}");
                        Directory.CreateDirectory(spriteOutputDir);
                        using var spriteReader = new BinaryReader(new MemoryStream(data));
                        spriteReader.ReadUInt32(); // skip header

                        var count = spriteReader.ReadBigEndianUInt32();
                        var offsetsOffset = spriteReader.ReadBigEndianUInt32();
                        spriteReader.ReadBytes(4);
                        var headers = new List<SpriteHeader>();
                        for (int i = 0; i < count; i++)
                        {
                            spriteReader.ReadBytes(4);
                            var header = new SpriteHeader
                            {
                                OffsetX = spriteReader.ReadBigEndianInt16(),
                                OffsetY = spriteReader.ReadBigEndianInt16(),
                                Width = spriteReader.ReadBigEndianInt16(),
                                Height = (short)(spriteReader.ReadBigEndianInt16() + 1)
                            };
                            headers.Add(header);
                            spriteReader.ReadBytes(0x10);
                        }

                        // using the header offsets and width/height calculate the max width and height
                        var maxWidth = headers.Max(h => h.OffsetX + h.Width);
                        var maxHeight = headers.Max(h => h.OffsetY + h.Height);

                        spriteReader.BaseStream.Seek(offsetsOffset, SeekOrigin.Begin);
                        var offsets = new uint[count];
                        for (int i = 0; i < count; i++)
                        {
                            offsets[i] = spriteReader.ReadBigEndianUInt32();
                        }

                        for (int i = 0; i < count; i++)
                        {
                            spriteReader.BaseStream.Seek(offsets[i], SeekOrigin.Begin);
                            var nextOffset = (i < count - 1) ? offsets[i + 1] : (uint)spriteReader.BaseStream.Length;
                            var length = nextOffset - offsets[i];
                            var spriteData = spriteReader.ReadBytes((int)length);
                            var decodedSprite = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, 0x10);
                            var image = ImageFormatHelper.GenerateClutImage(palette, decodedSprite, 384, 240, true);
                            var outputFile = Path.Combine(spriteOutputDir, $"{i}.png");
                            // crop image to max width and height
                            image = ImageFormatHelper.CropImage(image, maxWidth, maxHeight);
                            image.Save(outputFile, ImageFormat.Png);
                        }
                    }
                    currentChannel = sector.Channel;
                    sectorList.Clear();
                }
                sectorList.Add(sector);
            }
        }
    }
}
