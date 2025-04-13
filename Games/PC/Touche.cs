using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC
{
    public static class Touche
    {
        static Dictionary<string, int> Offsets = new Dictionary<string, int>()
        {
            {"RoomImages",      0x048},
            {"RoomInfo",        0x6B0},
            {"SpriteImages",    0x2A0},
            {"IconImages",      0x38c},
            {"Sequences",       0x224}
        };



        public static void ExtractFiles(string datFile)
        {
            using var fs = new FileStream(datFile, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            var roomInfoOffsets = new List<uint>();
            var roomImageOffsets = new List<uint>();
            var spriteImageOffsets = new List<uint>();
            var iconImageOffsets = new List<uint>();
            var sequenceOffsets = new List<uint>();

            var allOffsets = new List<uint>();

            var roomImageOutputFolder = Path.Combine(Path.GetDirectoryName(datFile), "output", "RoomImages");
            Directory.CreateDirectory(roomImageOutputFolder);
            var paletteOutputFolder = Path.Combine(roomImageOutputFolder, "Palettes");
            Directory.CreateDirectory(paletteOutputFolder);

            var spriteImageOutputFolder = Path.Combine(Path.GetDirectoryName(datFile), "output", "SpriteImages");
            Directory.CreateDirectory(spriteImageOutputFolder);

            var roomInfoStart = Offsets["RoomInfo"] + 4;
            reader.BaseStream.Seek(roomInfoStart, SeekOrigin.Begin);
            // read room info offsets while offset is not 0
            var index = 0;
            while (true)
            {
                var position = roomInfoStart + (index * 4);
                reader.BaseStream.Seek(position, SeekOrigin.Begin);
                var offset = reader.ReadUInt32();
                if (offset == 0)
                {
                    break;
                }

                roomInfoOffsets.Add(offset);
                index++;
            }
            allOffsets.AddRange(roomInfoOffsets);

            var roomImageStart = Offsets["RoomImages"] + 4;
            reader.BaseStream.Seek(roomImageStart, SeekOrigin.Begin);
            // read room image offsets while offset is not 0
            index = 0;
            while (true)
            {
                var position = roomImageStart + (index * 4);
                reader.BaseStream.Seek(position, SeekOrigin.Begin);
                var offset = reader.ReadUInt32();

                if (offset == 0)
                {
                    break;
                }
                roomImageOffsets.Add(offset);
                index++;
            }
            allOffsets.AddRange(roomImageOffsets);

            var spriteImageStart = Offsets["SpriteImages"] + 4;
            reader.BaseStream.Seek(spriteImageStart, SeekOrigin.Begin);
            // read sprite image offsets while offset is not 0
            index = 0;
            while (true)
            {
                var position = spriteImageStart + (index * 4);
                reader.BaseStream.Seek(position, SeekOrigin.Begin);
                var offset = reader.ReadUInt32();
                if (offset == 0)
                {
                    break;
                }
                spriteImageOffsets.Add(offset);
                index++;
            }
            allOffsets.AddRange(spriteImageOffsets);

            var iconImageStart = Offsets["IconImages"] + 4;
            reader.BaseStream.Seek(iconImageStart, SeekOrigin.Begin);
            // read icon image offsets while offset is not 0
            index = 0;
            while (true)
            {
                var position = iconImageStart + (index * 4);
                reader.BaseStream.Seek(position, SeekOrigin.Begin);
                var offset = reader.ReadUInt32();
                if (offset == 0)
                {
                    break;
                }
                iconImageOffsets.Add(offset);
                index++;
            }
            allOffsets.AddRange(iconImageOffsets);

            var sequenceStart = Offsets["Sequences"] + 4;
            reader.BaseStream.Seek(sequenceStart, SeekOrigin.Begin);
            // read sequence offsets while offset is not 0
            index = 0;
            while (true)
            {
                var position = sequenceStart + (index * 4);
                reader.BaseStream.Seek(position, SeekOrigin.Begin);
                var offset = reader.ReadUInt32();
                if (offset == 0)
                {
                    break;
                }
                sequenceOffsets.Add(offset);
                index++;
            }
            allOffsets.AddRange(sequenceOffsets);
            // sort all offsets
            allOffsets.Sort();

            // for (int i = 0; i < roomInfoOffsets.Count; i++)
            // {
            //     var offset = roomInfoOffsets[i];
            //     reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            //     reader.BaseStream.Seek(2, SeekOrigin.Current);
            //     var RoomNumber = reader.ReadUInt16();
            //     reader.BaseStream.Seek(2, SeekOrigin.Current);
            //     // next 768 bytes are the room palette
            //     var paletteBytes = reader.ReadBytes(768);
            //     File.WriteAllBytes(Path.Combine(paletteOutputFolder, $"Room_{RoomNumber}_pal.bin"), paletteBytes);
            //     var palette = ColorHelper.ConvertBytesToRGB(paletteBytes, 1);
            //     var paletteImage = ColorHelper.CreateLabelledPalette(palette);
            //     paletteImage.Save(Path.Combine(paletteOutputFolder, $"Room_{RoomNumber}_pal.png"));
            // }

            // for (int i = 0; i < roomImageOffsets.Count; i++)
            // {
            //     var offset = roomImageOffsets[i];
            //     uint nextOffset = 0;
            //     if (i + 1 < roomImageOffsets.Count ) {
            //         nextOffset = roomImageOffsets[i + 1];
            //     } else {
            //         // get the next highest offset from allOffsets
            //         nextOffset = allOffsets.FirstOrDefault(x => x > offset);
            //     }
            //     var length = nextOffset - offset;
            //     reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            //     var width = reader.ReadUInt16();
            //     var height = reader.ReadUInt16();
            //     //var data = reader.ReadBytes((int)(length - 4));
            //     var imageLines = new List<byte[]>();
            //     for (int j = 0; j < height; j++)
            //     {
            //         var line = DecodeRLE(reader, width);
            //         imageLines.Add(line);
            //     }
            //     var imageBytes = imageLines.SelectMany(x => x).ToArray();
            //     File.WriteAllBytes(Path.Combine(roomImageOutputFolder, $"Room_{i+1}.bin"), imageBytes);
            //     var paletteFile = Path.Combine(paletteOutputFolder, $"Room_{i+1}_pal.bin");
            //     if (File.Exists(paletteFile))
            //     {
            //         var paletteBytes = File.ReadAllBytes(paletteFile);
            //         var palette = ColorHelper.ConvertBytesToRGB(paletteBytes, 1);
            //         var image =ImageFormatHelper.GenerateClutImage(palette,imageBytes, width, height,true);
            //         image.Save(Path.Combine(roomImageOutputFolder, $"Room_{i+1}.png"));
            //     }
            // }

            for (int i = 0; i < spriteImageOffsets.Count; i++)
            {
                var offset = spriteImageOffsets[i];
                uint nextOffset = 0;
                if (i + 1 < spriteImageOffsets.Count)
                {
                    nextOffset = spriteImageOffsets[i + 1];
                }
                else
                {
                    // get the next highest offset from allOffsets
                    nextOffset = allOffsets.FirstOrDefault(x => x > offset);
                }
                var length = nextOffset - offset;
                reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                var width = reader.ReadUInt16();
                var height = reader.ReadUInt16();
                //var data = reader.ReadBytes((int)(length - 4));
                var imageLines = new List<byte[]>();
                for (int j = 0; j < height; j++)
                {
                    var line = DecodeRLE(reader, width);
                    imageLines.Add(line);
                }
                var palette = @"C:\Dev\Gaming\PC_DOS\Games\Touche-The-Adventures-of-the-Fifth-Musketeer_DOS_EN_RIP-Version\Touche\output\RoomImages\Palettes\Room_2_pal.bin";
                var palData = File.ReadAllBytes(palette).Skip(576).ToArray();
                var pal = ColorHelper.ConvertBytesToRGB(palData);
                var imageBytes = imageLines.SelectMany(x => x).ToArray();
                var image = ImageFormatHelper.GenerateClutImage(pal, imageBytes, width, height, true);
                image.Save(Path.Combine(spriteImageOutputFolder, $"Sprite_{i + 1}.png"));
            }
        }
        public static byte[] DecodeRLE(BinaryReader reader, int lineWidth)
        {
            var decoded = new List<byte>();
            int w = 0;
            while (w < lineWidth)
            {
                var code = reader.ReadByte();
                if ((code & 0xC0) == 0xC0)
                {
                    var len = code & 0x3F;
                    var color = reader.ReadByte();
                    decoded.AddRange(Enumerable.Repeat(color, len));
                }
                else
                {
                    decoded.Add(code);
                    w++;
                }
            }
            return decoded.ToArray();
        }
    }
}
