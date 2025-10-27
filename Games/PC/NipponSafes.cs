using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC
{
    public static class NipponSafes
    {
        public static void ExtractNSDiskFiles(string diskFolder)
        {
            var diskFiles = Directory.GetFiles(diskFolder, "DISK*");

            var namesOffset = 0x16;
            var nameSize = 0x20;
            var fileCount = 0x180;
            var filesOffset = 0x3016;
            foreach (var disk in diskFiles)
            {
                uint dataOffset = 0x4000;
                var diskOffsetData = new List<(uint, uint)>();
                using var reader = new BinaryReader(new FileStream(disk, FileMode.Open));
                reader.BaseStream.Seek(namesOffset, SeekOrigin.Begin);
                var names = new List<string>();
                for (int i = 0; i < fileCount; i++)
                {
                    var name = Encoding.ASCII.GetString(reader.ReadBytes(nameSize)).TrimEnd('\0');
                    names.Add(name);
                }
                reader.BaseStream.Seek(filesOffset, SeekOrigin.Begin);
                for (int i = 0; i < fileCount; i++)
                {
                    var offset = dataOffset;
                    var length = reader.ReadBigEndianUInt32();
                    diskOffsetData.Add((offset, length));
                    dataOffset += length;
                }
                var outputFolder = Path.Combine(diskFolder, "output", Path.GetFileNameWithoutExtension(disk));
                Directory.CreateDirectory(outputFolder);

                for (int i = 0; i < diskOffsetData.Count; i++)
                {
                    var (offset, length) = diskOffsetData[i];
                    if (length == 0) continue;
                    reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                    var data = reader.ReadBytes((int)length);
                    File.WriteAllBytes(Path.Combine(outputFolder, names[i]), data);
                }
            }
        }

        public static void ExtractBackgroundImages(string disksFolder)
        {

            var slideFiles = Directory.GetFiles(disksFolder, "*.dyn", SearchOption.AllDirectories).ToList();
            slideFiles.AddRange(Directory.GetFiles(disksFolder, "*.slide", SearchOption.AllDirectories).ToList());
            foreach (var slideFile in slideFiles)
            {

                using (var reader = new BinaryReader(File.OpenRead(slideFile)))
                {
                    var palData = reader.ReadBytes(0x60);
                    File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(slideFile), Path.GetFileNameWithoutExtension(slideFile) + "_pal.bin"), palData);
                    var palette = ColorHelper.ConvertBytesToRGB(palData, true);
                    var layer0 = reader.ReadByte();
                    var layer1 = reader.ReadByte();
                    var layer2 = reader.ReadByte();
                    var layer3 = reader.ReadByte();
                    var ranges = new List<PaletteFxRange>();
                    for (int i = 0; i < 6; i++)
                    {
                        var range = new PaletteFxRange()
                        {
                            Timer = reader.ReadBigEndianUInt16(),
                            Step = reader.ReadBigEndianUInt16(),
                            Flags = reader.ReadBigEndianUInt16(),
                            First = reader.ReadByte(),
                            Last = reader.ReadByte()
                        };
                        ranges.Add(range);
                    }
                    byte[] storage = new byte[0x80];
                    uint storageLength = 0;
                    uint length = 0;

                    var output = new List<byte>();
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        do
                        {
                            length = reader.ReadByte();
                            if (reader.BaseStream.Position >= reader.BaseStream.Length) break;

                            if (length == 0x80)
                            {
                                storageLength = 0;
                            }
                            else if (length <= 0x7f)
                            {
                                length++;
                                for (int i = 0; i < length; i++)
                                {
                                    storage[i] = reader.ReadByte();
                                }
                                storageLength = length;
                            }
                            else
                            {
                                length = (256 - length) + 1;
                                var v = reader.ReadByte();
                                for (int i = 0; i < length; i++)
                                {
                                    storage[i] = v;
                                }
                                storageLength = length;
                            }
                        } while (storageLength == 0);

                        // unpack the bits
                        for (int i = 0; i < storageLength; i++)
                        {
                            byte b = (byte)(storage[i] & 0x1f);
                            output.Add(b);
                        }
                    }
                    var imageData = output.ToArray();
                    var image = ImageFormatHelper.GenerateClutImage(palette, imageData, 320, 200);
                    var outputDir = Path.Combine(Path.GetDirectoryName(slideFile), "output_dyn");
                    Directory.CreateDirectory(outputDir);
                    image.Save(Path.Combine(outputDir, Path.GetFileNameWithoutExtension(slideFile) + ".png"), ImageFormat.Png);
                }
            }
        }

        public static void ExtractSpriteFiles(string inputDir)
        {
            var inputFiles = Directory.GetFiles(inputDir, "*.cnv", SearchOption.AllDirectories).ToList();
            inputFiles.AddRange(Directory.GetFiles(inputDir, "*.pp", SearchOption.AllDirectories));
            foreach (var file in inputFiles)
            {
                var testPackedData = File.ReadAllBytes(file);
                var testPal = @"C:\Dev\Gaming\PC_DOS\Games\Nippon-Safes-Inc_DOS_EN\nippon-safes-inc\output\DISK1\grottaint_pal.bin";
                var testPalData = File.ReadAllBytes(testPal).ToArray();
                var testPalette = ColorHelper.ConvertBytesToRGB(testPalData, true);
                var numFrames = testPackedData[0];
                var width = testPackedData[1];
                var height = testPackedData[2];
                var outputFolder = Path.Combine(inputDir, "output_pp", Path.GetFileNameWithoutExtension(file));
                Directory.CreateDirectory(outputFolder);
                var imageData = testPackedData.Skip(3).ToArray();
                var decompressedImage = new byte[width * height * numFrames];
                if (imageData.Length != width * height * numFrames)
                {
                    decompressedImage = DecompressPackBits(imageData, numFrames, width, height);
                }
                else
                {
                    decompressedImage = imageData;
                }
                for (int i = 0; i < numFrames; i++)
                {
                    var image = ImageFormatHelper.GenerateClutImage(testPalette, decompressedImage.Skip(i * width * height).Take(width * height).ToArray(), width, height, true);
                    image.Save(Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(file)}_{i}.png"), ImageFormat.Png);
                }
            }
        }

        static byte[] DecompressPackBits(byte[] compressed, byte numFrames, byte width, byte height)
        {
            uint left = (uint)(numFrames * width * height);
            uint lenR = 0;
            uint lenW = 0;

            using var reader = new BinaryReader(new MemoryStream(compressed));
            var output = new List<byte>();
            while (left > 0 && reader.BaseStream.Position < reader.BaseStream.Length)
            {
                lenR = reader.ReadByte();

                if (lenR == 0x80)
                {
                    lenW = 0;
                }
                else if (lenR <= 0x7f)
                {
                    lenR++;
                    lenW = Math.Min(lenR, left);
                    for (int i = 0; i < lenW; i++)
                    {
                        output.Add(reader.ReadByte());
                    }
                    for (; lenR > lenW; lenR--)
                    {
                        reader.ReadByte();
                    }
                }
                else
                {
                    lenW = Math.Min((256 - lenR) + 1, left);
                    var v = reader.ReadByte();
                    for (int i = 0; i < lenW; i++)
                    {
                        output.Add(v);
                    }
                }
                left -= lenW;
            }
            return output.ToArray();
        }
    }
    
    class PaletteFxRange
    {
        public ushort Timer { get; set; }
        public ushort Step { get; set; }
        public ushort Flags { get; set; }
        public Byte First { get; set; }
        public Byte Last { get; set; }
    }

}
