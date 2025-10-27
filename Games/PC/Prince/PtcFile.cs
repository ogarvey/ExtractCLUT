using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC.Prince
{
    public static class PtcHelpers
    {

        public static void AlignSprites(string inputFolder, string outputFolder)
        {
            var regex = new Regex(@"(\d+)_(-?\d+)_(-?\d+)_(-?\d+)_(-?\d+)_(-?\d+)_(-?\d+)\.png");
            // var regex = new Regex(@"(\d+)_(-?\d+)_(-?\d+)\.png");
            var files = Directory.GetFiles(inputFolder, "*.png");

            var images = new List<(string path, int index, int offsetX, int offsetY, int originX, int originY, int width, int height, Bitmap bmp)>();


            // Load all images and calculate frame size
            foreach (var file in files)
            {
                var match = regex.Match(Path.GetFileName(file));
                if (!match.Success) continue;

                int unused = int.Parse(match.Groups[1].Value);
                int offsetX = int.Parse(match.Groups[2].Value);
                int offsetY = int.Parse(match.Groups[3].Value);
                int originX = int.Parse(match.Groups[4].Value);
                int originY = int.Parse(match.Groups[5].Value);
                int width = int.Parse(match.Groups[6].Value);
                int height = int.Parse(match.Groups[7].Value);
                Bitmap bmp = new Bitmap(file);

                images.Add((file, unused, offsetX, offsetY, originX, originY, width, height, bmp));
            }

            int frameWidth = images[0].width;
            int frameHeight = images[0].height;

            foreach (var img in images)
            {
                Bitmap bmp = new Bitmap(frameWidth, frameHeight);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);

                    int x0 = img.originX + img.offsetX;
                    int y0 = img.originY + img.offsetY;

                    g.DrawImage(img.bmp, x0, y0);
                }
                var outputPath = Path.Combine(outputFolder, Path.GetFileName(img.path));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                bmp.Save(outputPath);
            }
        }
    }
    public class PtcFile
    {
        private readonly uint _fileTableOffsetKey = 0x4D4F4B2D;
        private readonly uint _fileTableSizeKey = 0x534F4654;
        private List<Color> _palette = new List<Color>();
        private uint _width;
        private uint _height;
        private BinaryReader _reader;
        public List<PtcEntry> Entries { get; } = new List<PtcEntry>();
        public PtcFile(string path)
        {
            _reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read));
            ReadEntries();
        }

        private void ReadEntries()
        {
            _reader.ReadBytes(4); // Skip signature
            var fileTableOffset = _reader.ReadUInt32() ^ _fileTableOffsetKey;
            var fileTableSize = _reader.ReadUInt32() ^ _fileTableSizeKey;
            _reader.BaseStream.Seek(fileTableOffset, SeekOrigin.Begin);

            var tableData = _reader.ReadBytes((int)fileTableSize);
            tableData = Decrypt(tableData, (int)fileTableSize);

            using var tableReader = new BinaryReader(new MemoryStream(tableData));
            while (tableReader.BaseStream.Position < tableReader.BaseStream.Length)
            {
                var nameBytes = tableReader.ReadBytes(24);
                var name = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                var offset = tableReader.ReadUInt32();
                var size = tableReader.ReadUInt32();
                Entries.Add(new PtcEntry(name, offset, size));
            }

            // Read palette from room file
            var roomEntry = Entries.FirstOrDefault(e => e.name.ToUpper() == "ROOM");
            if (roomEntry != null)
            {
                _reader.BaseStream.Seek(roomEntry.Offset, SeekOrigin.Begin);
                var data = _reader.ReadBytes((int)roomEntry.Size);
                var decompressedSize = BitConverter.ToInt32(data.Skip(14).Take(4).Reverse().ToArray(), 0);
                data = Decompressor.Decompress(data.Skip(18).ToArray(), decompressedSize);
                using var roomReader = new BinaryReader(new MemoryStream(data));
                roomReader.ReadBytes(0x12); // Skip signature
                _width = roomReader.ReadUInt32();
                _height = roomReader.ReadUInt32();
                roomReader.ReadBytes(0x1c); // Skip unknown data
                var paletteData = roomReader.ReadBytes(0x400);
                _palette = ColorHelper.ConvertBytesToBGRA(paletteData);
            }
        }

        public void ExtractEntries(string outputDir)
        {
            foreach (var entry in Entries)
            {
                _reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
                var data = _reader.ReadBytes((int)entry.Size);
                var magic = System.Text.Encoding.ASCII.GetString(data.Take(4).ToArray());
                if (magic == "MASM")
                {
                    var decompressedSize = BitConverter.ToInt32(data.Skip(14).Take(4).Reverse().ToArray(), 0);
                    data = Decompressor.Decompress(data.Skip(18).ToArray(), decompressedSize);
                    magic = System.Text.Encoding.ASCII.GetString(data.Take(4).ToArray());
                    if (entry.name == "ROOM")
                    {
                        File.WriteAllBytes(Path.Combine(outputDir, $"{entry.name}.bmp"), data);
                    }
                    else if (magic == "AN\0\0" && !entry.name.EndsWith("S") && !entry.name.EndsWith(".ANI"))
                    {
                        var animOutputDir = Path.Combine(outputDir, "animations", entry.name);
                        Directory.CreateDirectory(animOutputDir);
                        using var animReader = new BinaryReader(new MemoryStream(data));
                        animReader.ReadBytes(2); // Skip magic
                        var loopCount = animReader.ReadUInt16();
                        var phaseCount = animReader.ReadUInt16();
                        var frameCount = animReader.ReadUInt16();
                        var baseX = animReader.ReadUInt16();
                        var baseY = animReader.ReadUInt16();
                        var phaseOffset = animReader.ReadUInt32();

                        var offsets = new List<uint>();
                        var phases = new List<PhaseInfo>();

                        for (int i = 0; i < frameCount; i++)
                        {
                            offsets.Add(animReader.ReadUInt32());
                        }

                        for (int i = 0; i < phaseCount; i++)
                        {
                            var offsetX = animReader.ReadInt16();
                            var offsetY = animReader.ReadInt16();
                            var toFromIndex = animReader.ReadUInt16();
                            animReader.ReadUInt16(); // Skip unknown
                            phases.Add(new PhaseInfo(offsetX, offsetY, toFromIndex));
                        }

                        foreach (var (phase, index) in phases.WithIndex())
                        {
                            animReader.BaseStream.Seek(offsets[phase.PhaseToFromIndex], SeekOrigin.Begin);
                            var width = animReader.ReadUInt16();
                            var height = animReader.ReadUInt16();
                            // confirm there are enough bytes left for marker data
                            if (animReader.BaseStream.Position + 4 >= animReader.BaseStream.Length)
                                continue;
                            var marker = animReader.ReadBigEndianUInt32();
                            if (Encoding.ASCII.GetString(BitConverter.GetBytes(marker).Reverse().ToArray()) == "masm")
                            {
                                var decompSize = animReader.ReadUInt32();
                                var length = phase.PhaseToFromIndex >= offsets.Count - 1 ? (uint)(data.Length - offsets[phase.PhaseToFromIndex]) : offsets[phase.PhaseToFromIndex + 1] - offsets[phase.PhaseToFromIndex];
                                var compData = animReader.ReadBytes((int)length - 12);
                                try
                                {
                                    var decompressed = Decompressor.Decompress(compData, (int)decompSize);
                                    var image = ImageFormatHelper.GenerateClutImage(_palette, decompressed, width, height, true, 255, false);
                                    var imageName = $"{index}_{phase.PhaseOffsetX}_{phase.PhaseOffsetY}_{baseX}_{baseY}_{_width}_{_height}.png";
                                    image.Save(Path.Combine(animOutputDir, imageName));
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to decompress animation frame {index} of {entry.name}: {ex.Message}");
                                    File.WriteAllBytes(Path.Combine(animOutputDir, $"{index}_error.bin"), compData);
                                }
                            }
                            else
                            {
                                animReader.BaseStream.Seek(-4, SeekOrigin.Current);
                                var imageData = animReader.ReadBytes(width * height);
                                var image = ImageFormatHelper.GenerateClutImage(_palette, imageData, width, height, true, 255, false);
                                var imageName = $"{index}_{phase.PhaseOffsetX}_{phase.PhaseOffsetY}_{baseX}_{baseY}_{_width}_{_height}.png";
                                image.Save(Path.Combine(animOutputDir, imageName));
                            }
                        }
                        PtcHelpers.AlignSprites(animOutputDir, Path.Combine(animOutputDir, "aligned"));
                    }
                    else if (entry.name.ToUpper() == "PATH")
                    {
                        // 80 x 480 pixel image, need to copy bytes to get correct width - copy each pixel (room.width / 80) times
                        using var imgReader = new BinaryReader(new MemoryStream(data));
                        var output = new List<byte>();

                        while (imgReader.BaseStream.Position < imgReader.BaseStream.Length)
                        {
                            var b = imgReader.ReadByte();
                            for (int i = 0; i < _width / 80; i++) // room.width / 80 
                            {
                                output.Add(b);
                            }
                        }

                        var image = ImageFormatHelper.GenerateClutImage(_palette, output.ToArray(), (int)_width, (int)_height);
                        image.Save(Path.Combine(outputDir, $"{entry.name}.png"));
                    }
                    else if ((!entry.name.EndsWith(".LST") && entry.name.StartsWith("OB")) || entry.name.StartsWith("PS"))
                    {
                        using var imgReader = new BinaryReader(new MemoryStream(data));
                        var unk1 = imgReader.ReadUInt16();
                        var unk2 = imgReader.ReadUInt16();
                        var width = imgReader.ReadUInt16();
                        var height = imgReader.ReadUInt16();

                        var imageData = imgReader.ReadBytes((int)(width * height));

                        var image = ImageFormatHelper.GenerateClutImage(_palette, imageData, width, height, true, 255, false);
                        var imageName = $"{entry.name}_w{width}_h{height}_u1_{unk1}_u2_{unk2}.png";
                        image.Save(Path.Combine(outputDir, imageName));
                    }
                    else
                    {
                        File.WriteAllBytes(Path.Combine(outputDir, $"{entry.name}.bin"), data);
                    }
                }
                else if (magic == "RIFF")
                {
                    File.WriteAllBytes(Path.Combine(outputDir, $"{entry.name}.wav"), data);
                }
            }
        }

        private byte[] Decrypt(byte[] buffer, int size)
        {
            uint key = 0xDEADF00D;
            byte[] output = new byte[size];
            // while (size--)
            // {
            //     *buffer++ += key & 0xFF;
            //     key ^= 0x2E84299A;
            //     key += MKTAG('B', 'L', 'A', 'H');
            //     key = ((key & 1) << 31) | (key >> 1);
            // }
            for (int i = 0; i < size; i++)
            {
                output[i] = (byte)(buffer[i] + (key & 0xFF));
                key ^= 0x2E84299A;
                key += 0x424C4148; // MKTAG('B', 'L', 'A', 'H')
                key = ((key & 1) << 31) | (key >> 1);
            }
            return output;
        }
    }
}


public record PtcEntry(string name, uint Offset, uint Size);

public record PhaseInfo(short PhaseOffsetX, short PhaseOffsetY, ushort PhaseToFromIndex);
