using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC.SCI
{
    public static class SCIUtils
    {
        public static void ExtractV56Files(string srcPath, List<Color>? overridePalette = null)
        {
            var v56Files = Directory.GetFiles(srcPath, "*.v56", SearchOption.AllDirectories);
            foreach (var v56File in v56Files)
            {
                var outputDir = Path.Combine(srcPath, "out", "images_v56_offset", Path.GetFileNameWithoutExtension(v56File));
                var data = File.ReadAllBytes(v56File);
                if (data[0] == 0x80 && data[1] == 0x80) data = data.Skip(0x1a).ToArray();
                var headerSize = BitConverter.ToUInt16(data.Take(2).ToArray(), 0) + 2;
                var header = data.Take(headerSize).ToArray();
                var loopCount = data[2];
                var palOffset = BitConverter.ToUInt32(data.Skip(8).Take(4).ToArray(), 0);
                var loopSize = data[12];
                var celSize = data[13];
                var palette = new List<Color>();
                var paletteArray = new byte[256 * 3];
                if (palOffset != 0)
                {
                    var potentialPaletteData = data.Skip((int)palOffset).ToArray();
                    var palFormat = potentialPaletteData[32];
                    var palDataOffset = 37;
                    var palColorStart = potentialPaletteData[25];
                    var palColorCount = BitConverter.ToUInt16(potentialPaletteData.Skip(29).Take(2).ToArray());

                    var palData = potentialPaletteData.Skip(palDataOffset).Take(palColorCount * 4).ToArray();
                    for (int i = palColorStart, j = 0; i < (palColorStart + palColorCount); i++, j++)
                    {
                        var index = palData[j * 4];
                        var r = palData[(j * 4) + 1];
                        var g = palData[(j * 4) + 2];
                        var b = palData[(j * 4) + 3];
                        palette.Add(Color.FromArgb(r, g, b));
                        paletteArray[(j * 3)] = r;
                        paletteArray[(j * 3) + 1] = g;
                        paletteArray[(j * 3) + 2] = b;
                    }
                    var palFileName = Path.Combine(Path.GetDirectoryName(v56File), "out", "palettes", $"{Path.GetFileNameWithoutExtension(v56File)}_palette.bin");
                    // check directory exists and create if not
                    Directory.CreateDirectory(Path.GetDirectoryName(palFileName));
                    File.WriteAllBytes(palFileName, paletteArray);
                }
                var loops = new LoopInfo[loopCount];

                for (int i = 0; i < loopCount; i++)
                {
                    var loop = new LoopInfo();
                    var loopData = data.Skip(headerSize + (i * loopSize)).Take(loopSize).ToArray();
                    byte seekEntry = loopData[0];
                    if (seekEntry != 255)
                    {
                        loop.MirrorFlag = true;
                        do
                        {
                            if (seekEntry >= loopCount)
                            {
                                Console.WriteLine("Invalid seek entry");
                                break;
                            }
                            loopData = data.Skip(headerSize + (seekEntry * loopSize)).Take(loopSize).ToArray();
                        } while ((seekEntry = loopData[0]) != 255);
                    }
                    else
                    {
                        loop.MirrorFlag = false;
                    }
                    var celCount = loopData[2];
                    loop.Cels = new CelInfo[celCount];
                    UInt32 celOffset = BitConverter.ToUInt32(loopData.Skip(12).Take(4).ToArray(), 0);

                    for (int j = 0; j < celCount; j++)
                    {
                        var cel = new CelInfo();
                        cel.ScriptWidth = cel.Width = BitConverter.ToInt16(data.Skip((int)((j * celSize) + celOffset)).Take(2).ToArray(), 0);
                        cel.ScriptHeight = cel.Height = BitConverter.ToInt16(data.Skip((int)((j * celSize) + celOffset) + 2).Take(2).ToArray(), 0);
                        cel.X = BitConverter.ToInt16(data.Skip((int)((j * celSize) + celOffset) + 4).Take(2).ToArray(), 0);
                        cel.Y = BitConverter.ToInt16(data.Skip((int)((j * celSize) + celOffset) + 6).Take(2).ToArray(), 0);
                        if (cel.Y < 0) cel.Y += 255;
                        cel.ClearKey = data[celOffset + 8];
                        cel.OffsetEGA = 0;
                        cel.OffsetRLE = BitConverter.ToUInt32(data.Skip((int)((j * celSize) + celOffset) + 24).Take(4).ToArray(), 0);
                        cel.OffsetLiteral = BitConverter.ToUInt32(data.Skip((int)((j * celSize) + celOffset) + 28).Take(4).ToArray(), 0);
                        if (cel.OffsetRLE > 0 && cel.OffsetLiteral == 0)
                        {
                            // swap the values
                            cel.OffsetLiteral = cel.OffsetRLE;
                            cel.OffsetRLE = 0;
                        }
                        if (loop.MirrorFlag) cel.X = (short)-cel.X;
                        cel.RawBitmap = new byte[cel.Width * cel.Height];
                        loop.Cels[j] = cel;
                        var rleData = data.Skip((int)cel.OffsetRLE).ToArray();
                        var literalData = data.Skip((int)cel.OffsetLiteral).ToArray();
                        var currentByte = 0;
                        var runLength = 0;
                        var pixelNr = 0;
                        var pixelCount = cel.Width * cel.Height;
                        var rleIndex = 0;
                        var literalIndex = 0;
                        while (pixelNr < pixelCount)
                        {
                            currentByte = rleData[rleIndex++];
                            runLength = currentByte & 0x3F;
                            runLength = Math.Min(runLength, pixelCount - pixelNr);
                            switch (currentByte & 0xC0)
                            {
                                case 0x40:
                                    runLength += 64;
                                    // fall through
                                    goto case 0x00;
                                case 0x00:
                                    if (cel.OffsetLiteral == 0)
                                    {
                                        // copy Math.Min(runLength, pixelCount-pixelNr) bytes from rleData to cel.RawBitmap
                                        for (int k = 0; k < runLength; k++)
                                        {
                                            cel.RawBitmap[pixelNr++] = rleData[rleIndex++];
                                        }
                                    }
                                    else
                                    {
                                        // copy Math.Min(runLength, pixelCount-pixelNr) bytes from rleData to cel.RawBitmap
                                        for (int k = 0; k < runLength; k++)
                                        {
                                            cel.RawBitmap[pixelNr++] = literalData[literalIndex++];
                                        }
                                    }
                                    break;
                                case 0x80: // fill with the color of the next byte
                                    if (cel.OffsetLiteral == 0)
                                    {
                                        // copy Math.Min(runLength, pixelCount-pixelNr) bytes from rleData to cel.RawBitmap
                                        for (int k = 0; k < runLength; k++)
                                        {
                                            cel.RawBitmap[pixelNr++] = rleData[rleIndex];
                                        }
                                        rleIndex++;
                                    }
                                    else
                                    {
                                        // copy Math.Min(runLength, pixelCount-pixelNr) bytes from rleData to cel.RawBitmap
                                        for (int k = 0; k < runLength; k++)
                                        {
                                            cel.RawBitmap[pixelNr++] = literalData[literalIndex];
                                        }
                                        literalIndex++;
                                    }
                                    break;
                                case 0xC0: // transparent run, fill with cel.ClearKey
                                    for (int k = 0; k < runLength; k++)
                                    {
                                        cel.RawBitmap[pixelNr++] = cel.ClearKey;
                                    }
                                    break;
                                default:
                                    pixelNr += runLength;
                                    break;
                            }
                        }
                        if (palette.Count == 0)
                        {
                            palette = overridePalette ?? new List<Color>();
                        }
                        if (palette.Count == 0)
                        {
                            // generate greyscale palette
                            for (int k = 0; k < 256; k++)
                            {
                                palette.Add(Color.FromArgb(k, k, k));
                            }
                        }
                        var colCount = palette.Count;
                        //if (cel.ClearKey > 0) palette[cel.ClearKey > colCount ? colCount - 1 : cel.ClearKey-1] = Color.Transparent;
                        var image = ImageFormatHelper.GenerateClutImage(palette, cel.RawBitmap, cel.Width, cel.Height, true, cel.ClearKey, false, true);
                        Directory.CreateDirectory(outputDir);
                        if (loop.MirrorFlag) image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        // take into account cel.X and cel.Y, these should offset the image from the top left corner
                        // if negative, offset from the bottom right corner
                        var leftBound = cel.X - (cel.Width / 2);
                        image.Save(Path.Combine(outputDir, $"{i}_{j}__{cel.X}_{cel.Y}.png"), ImageFormat.Png);
                    }
                    loops[i] = loop;
                }

                FileHelpers.AlignSpriteSequences(outputDir, Path.Combine(outputDir, "aligned"));
            }



        }
    }

    class CelInfo
    {
        public short Width { get; set; }
        public short Height { get; set; }
        public short ScriptWidth { get; set; }
        public short ScriptHeight { get; set; }
        public short X { get; set; }
        public short Y { get; set; }
        public byte ClearKey { get; set; }
        public ushort OffsetEGA { get; set; }
        public uint OffsetRLE { get; set; }
        public uint OffsetLiteral { get; set; }
        public byte[] RawBitmap { get; set; }
    }

    class LoopInfo
    {
        public bool MirrorFlag { get; set; }
        public CelInfo[] Cels { get; set; }
    }
}
