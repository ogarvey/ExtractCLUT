using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ExtractCLUT.Games.PC.ExpectNoMercy
{
    public static class FileHelper
    {
        public static List<byte[]> ExtractCsFile(string filePath)
        {
            var csFileData = new List<byte[]>();

            using var br = new BinaryReader(File.OpenRead(filePath));
            br.BaseStream.Seek(0x1A, SeekOrigin.Begin);
            
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                var IdString = Encoding.ASCII.GetString(br.ReadBytes(4));
                var dataSize = br.ReadUInt32();
                var data = br.ReadBytes((int)dataSize);
                if (dataSize == 0x08)
                {
                    continue;
                }
                csFileData.Add(data);
            }
            return csFileData;
        }

        public static Image<Rgba32> ConvertCsFileChunkToImage(byte[] chunkData)
        {
            using var chunkReader = new BinaryReader(new MemoryStream(chunkData));
            var chunkLength = chunkReader.BaseStream.Length -0x50;
            chunkReader.BaseStream.Seek(0x04, SeekOrigin.Begin);
            var width = chunkReader.ReadUInt32();
            var height = chunkReader.ReadUInt32();
            chunkReader.BaseStream.Seek(0x1c, SeekOrigin.Current);
            var paletteData = chunkReader.ReadBytes(0x400);
            var palette = ColorHelper.ConvertRgbxIS(paletteData, false);
            

            var imageData = chunkReader.ReadBytes((int)chunkLength);
            var decodedData = DecodeRle(imageData, (int)width, (int)height);
            var image = ImageFormatHelper.GenerateIMClutImage(palette, decodedData, (int)width, (int)height, true, [0x0, 0xA]);
            image.Mutate(x => x.Flip(FlipMode.Vertical));
            return image;
        }


        public static byte[] DecodeRle(byte[] data, int width, int height)
        {
            byte[] indices = new byte[checked(width * height)];
            int offset = 0;
            int x = 0;
            int y = 0;

            while (offset < data.Length)
            {
                if (data.Length - offset < 2)
                {
                    throw new InvalidDataException("RLE8 data ends in the middle of a command");
                }

                int count = data[offset++];
                int value = data[offset++];

                if (count != 0)
                {
                    EnsureRunFits(x, y, count, width, height);
                    for (int i = 0; i < count; i++)
                    {
                        indices[y * width + x++] = (byte)value;
                    }

                    continue;
                }

                switch (value)
                {
                    case 0:
                        if (y >= height)
                        {
                            throw new InvalidDataException("RLE8 end-of-line is outside the image");
                        }

                        x = 0;
                        y++;
                        break;

                    case 1:
                        return indices;

                    case 2:
                        if (data.Length - offset < 2)
                        {
                            throw new InvalidDataException("RLE8 delta command is truncated");
                        }

                        int deltaX = data[offset++];
                        int deltaY = data[offset++];
                        x = checked(x + deltaX);
                        y = checked(y + deltaY);
                        if (x > width || y > height)
                        {
                            throw new InvalidDataException("RLE8 delta moves outside the image");
                        }
                        break;

                    default:
                        int literalCount = value;
                        if (data.Length - offset < literalCount)
                        {
                            throw new InvalidDataException("RLE8 absolute-mode data is truncated");
                        }

                        EnsureRunFits(x, y, literalCount, width, height);
                        for (int i = 0; i < literalCount; i++)
                        {
                            indices[y * width + x++] = data[offset++];
                        }

                        if ((literalCount & 1) != 0)
                        {
                            if (offset >= data.Length)
                            {
                                throw new InvalidDataException("RLE8 absolute-mode padding is truncated");
                            }

                            offset++;
                        }
                        break;
                }
            }

            throw new InvalidDataException("RLE8 data does not contain an end-of-bitmap command");
        }

        private static void EnsureRunFits(int x, int y, int count, int width, int height)
        {
            if (y < 0 || y >= height || x < 0 || count > width - x)
            {
                throw new InvalidDataException("RLE8 pixel run exceeds the image bounds");
            }
        }
    }
}
