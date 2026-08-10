using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;

namespace ExtractCLUT.Games.PC.LABlaster
{
    public static class FileHelper
    {
        public static void ExtractDatFiles(string sprDatDir)
        {
            var outputDir = Path.Combine(Path.GetDirectoryName(sprDatDir)!, "Extracted");
            Directory.CreateDirectory(outputDir);
            var sprDatFiles = Directory.GetFiles(sprDatDir, "*.DAT");
            foreach (var sprDatFile in sprDatFiles)
            {
                using var sprDatReader = new BinaryReader(File.OpenRead(sprDatFile));

                while (sprDatReader.BaseStream.Position < sprDatReader.BaseStream.Length)
                {
                    var id = sprDatReader.ReadUInt32();
                    var path = Encoding.ASCII.GetString(sprDatReader.ReadBytes(32)).TrimEnd('\0');
                    if (path == "")
                    {
                        break;
                    }
                    if (Path.GetFileNameWithoutExtension(path).Contains("Surf1")
                      || Path.GetFileNameWithoutExtension(path).Contains("32mmd5a")
                      || Path.GetFileNameWithoutExtension(path).Contains("42mmd5a")
                      || Path.GetFileNameWithoutExtension(path).Contains("52mmd55")
                      || Path.GetFileNameWithoutExtension(path).Contains("52mtank")
                      || Path.GetFileNameWithoutExtension(path).Contains("53mapac")
                      || Path.GetFileNameWithoutExtension(path).Contains("53mbloa")
                      || Path.GetFileNameWithoutExtension(path).Contains("53mhnda")
                      || Path.GetFileNameWithoutExtension(path).Contains("53mwspa")
                      || Path.GetFileNameWithoutExtension(path).Contains("54mblda")
                      || Path.GetFileNameWithoutExtension(path).Contains("54mblwa")
                      || Path.GetFileNameWithoutExtension(path).Contains("42mrota")
                      || Path.GetFileNameWithoutExtension(path).Contains("34mskea")
                      || Path.GetFileNameWithoutExtension(path).Contains("42mskea")
                      || Path.GetFileNameWithoutExtension(path).Contains("61mblab")
                      || Path.GetFileNameWithoutExtension(path).Contains("61mrotb")
                      || Path.GetFileNameWithoutExtension(path).Contains("jeepgira")
                      || Path.GetFileNameWithoutExtension(path).Contains("chinaa")
                      || Path.GetFileNameWithoutExtension(path).Contains("ninjaa")
                      || Path.GetFileNameWithoutExtension(path).Contains("l2mjeta")
                      || Path.GetFileNameWithoutExtension(path).Contains("l2mwjet1")
                      )
                    {
                        sprDatReader.ReadBytes(0x48); // skip 12 bytes
                    }
                    else
                    {
                        sprDatReader.ReadBytes(0xC); // skip 72 bytes
                    }
                    var chunkSize = sprDatReader.ReadUInt32() - 0x808;

                    var rgbxPalData = sprDatReader.ReadBytes(0x400);
                    var palette1 = ColorHelper.ConvertRgbxIS(rgbxPalData, true);
                    sprDatReader.ReadBytes(0x8); // skip 8 bytes
                    var palette2 = ColorHelper.ConvertRgbxIS(sprDatReader.ReadBytes(0x400), true);

                    var currentPos = sprDatReader.BaseStream.Position;
                    var imageIndex = 0;
                    while (sprDatReader.BaseStream.Position < currentPos + chunkSize - 4)
                    {
                        // path is in format "folder\file", we want to create a subdirectory for the folder and save the image as file_{imageIndex}.png

                        var width = sprDatReader.ReadUInt32();
                        var height = sprDatReader.ReadUInt32();
                        var xPivot = sprDatReader.ReadUInt32();
                        var yPivot = sprDatReader.ReadUInt32();
                        var lineOffsets = new List<uint>();
                        var lineOffsetStartPos = sprDatReader.BaseStream.Position;
                        for (int i = 0; i < height + 1; i++)
                        {
                            lineOffsets.Add((uint)(sprDatReader.ReadUInt32() + lineOffsetStartPos));
                        }

                        var decompressedData = new byte[width * height];
                        for (int y = 0; y < height; y++)
                        {
                            var lineOffset = lineOffsets[y];
                            var nextLineOffset = lineOffsets[y + 1];
                            var lineLength = nextLineOffset - lineOffset - 1;
                            sprDatReader.BaseStream.Seek(lineOffset, SeekOrigin.Begin);
                            var startPixel = sprDatReader.ReadByte();
                            var pixels = sprDatReader.ReadBytes((int)lineLength);
                            // insert pixel data into decompressedData at the correct position
                            Array.Copy(pixels, 0, decompressedData, y * width + startPixel, pixels.Length);
                        }

                        var subDir = Path.Combine(outputDir, Path.GetDirectoryName(path)!);
                        Directory.CreateDirectory(subDir);
                        using (var image = ImageFormatHelper.GenerateIMClutImage(palette1, decompressedData, (int)width, (int)height, true, [0]))
                        {
                            image.SaveAsPng(Path.Combine(subDir, $"{Path.GetFileNameWithoutExtension(path)}_{imageIndex}.png"));
                        }
                        imageIndex++;
                    }
                    currentPos = sprDatReader.BaseStream.Position;
                    // seek to next 4-byte boundary
                    var nextBoundary = (currentPos + 3) & ~3;
                    if (nextBoundary > sprDatReader.BaseStream.Length)
                    {
                        break;
                    }
                    else if (nextBoundary == currentPos)
                    {
                        nextBoundary += 4;
                    }
                    sprDatReader.BaseStream.Seek(nextBoundary, SeekOrigin.Begin);
                }

            }

        }
    }
}
