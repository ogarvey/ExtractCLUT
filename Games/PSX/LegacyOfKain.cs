using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PSX
{
    class SpriteHeader
    {
        public int Offset { get; set; }
        public byte Width { get; set; }
        public byte Height { get; set; }
        public byte X { get; set; }
        public byte Y { get; set; }
    }

    public static class LegacyOfKain
    {
        public static void ConvertSDR(string spriteFile)
        {
            var spriteData = File.ReadAllBytes(spriteFile);

            var spriteCount = BitConverter.ToUInt32(spriteData.Take(4).ToArray(), 0);
            var dataSize = BitConverter.ToUInt32(spriteData.Skip(4).Take(4).ToArray(), 0);
            var palData = spriteData.Skip(8).Take(0x300).ToArray();
            var palette = ColorHelper.ConvertBytesToRGB(palData);

            var xOffset = BitConverter.ToInt16(spriteData.Skip(0x308).Take(2).ToArray(), 0);
            var yOffset = BitConverter.ToInt16(spriteData.Skip(0x30A).Take(2).ToArray(), 0);

            if (xOffset > 320 || yOffset > 240) Debugger.Break();

            var headerList = new List<SpriteHeader>();

            for (int i = 0; i < spriteCount; i++)
            {
                var header = new SpriteHeader
                {
                    Offset = BitConverter.ToInt32(spriteData.Skip(0x30c + (i * 8)).Take(4).ToArray(), 0),
                    Width = spriteData[0x30c + (i * 8) + 4],
                    Height = spriteData[0x30c + (i * 8) + 5],
                    X = spriteData[0x30c + (i * 8) + 6],
                    Y = spriteData[0x30c + (i * 8) + 7]
                };
                headerList.Add(header);
            }

            var imageData = spriteData.Skip((int)(0x30c + (spriteCount * 8))).ToArray();

            var outputDir = Path.Combine(Path.GetDirectoryName(spriteFile), $"{Path.GetFileNameWithoutExtension(spriteFile)}");
            Directory.CreateDirectory(outputDir);

            var maxWidth = headerList.Max(h => h.Width + (h.X * 2));
            var maxHeight = headerList.Max(h => h.Height + h.Y);

            var frameWidth = maxWidth + xOffset * 2;
            var frameHeight = maxHeight + yOffset;
            if (frameWidth < 0 || frameHeight < 0) Debugger.Break();

            foreach (var (header, hIndex) in headerList.WithIndex())
            {
                var nextOffset = hIndex == headerList.Count - 1 ? imageData.Length : headerList[hIndex + 1].Offset;
                var byteCount = nextOffset - header.Offset;
                var imageBytes = imageData.Skip(header.Offset).Take(byteCount).ToArray();
                var bytes = DecodeBORle(imageBytes, header.Width, header.Height, 0x00);
                if (bytes.Length == 0) continue;
                var image = ImageFormatHelper.GenerateClutImage(palette, bytes, header.Width, header.Height, true);
                // create a new image with the frame width and height
                var newImage = new Bitmap(frameWidth, frameHeight);
                using (var g = Graphics.FromImage(newImage))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImage(image, header.X + xOffset, header.Y + yOffset);
                }
                image.Save(Path.Combine(outputDir, $"{hIndex}_{header.X}-{header.Y}.png"), ImageFormat.Png);
            }
        }

        public static void ConvertSDT(string spriteFile)
        {
            var spriteData = File.ReadAllBytes(spriteFile);

            var spriteCount = BitConverter.ToUInt32(spriteData.Take(4).ToArray(), 0);
            var dataSize = BitConverter.ToUInt32(spriteData.Skip(4).Take(4).ToArray(), 0);
            var palData = spriteData.Skip(8).Take(0x300).ToArray();
            var palette = ColorHelper.ConvertBytesToRGB(palData);
            
            var headerList = new List<SpriteHeader>();

            for (int i = 0; i < spriteCount; i++)
            {
                var header = new SpriteHeader
                {
                    Offset = BitConverter.ToInt32(spriteData.Skip(0x308 + (i * 8)).Take(4).ToArray(), 0),
                    Width = spriteData[0x308 + (i * 8) + 4],
                    Height = spriteData[0x308 + (i * 8) + 5],
                    X = spriteData[0x308 + (i * 8) + 6],
                    Y = spriteData[0x308 + (i * 8) + 7]
                };
                headerList.Add(header);
            }

            var imageData = spriteData.Skip((int)(0x308 + (spriteCount * 8))).ToArray();

            var outputDir = Path.Combine(Path.GetDirectoryName(spriteFile), $"{Path.GetFileNameWithoutExtension(spriteFile)}");
            Directory.CreateDirectory(outputDir);
            
            foreach (var (header, hIndex) in headerList.WithIndex())
            {
                var nextOffset = hIndex == headerList.Count - 1 ? imageData.Length : headerList[hIndex + 1].Offset;
                var byteCount = nextOffset - header.Offset;
                var imageBytes = imageData.Skip(header.Offset).Take(byteCount).ToArray();
                var bytes = DecodeBORle(imageBytes, header.Width, header.Height, 0x00);
                if (bytes.Length == 0) continue;
                var image = ImageFormatHelper.GenerateClutImage(palette, bytes, header.Width, header.Height, true);
                
                image.Save(Path.Combine(outputDir, $"{hIndex}_{header.X}-{header.Y}.png"), ImageFormat.Png);
            }
        }
        public static void ConvertSHA(string spriteFile)
        {
            var spriteData = File.ReadAllBytes(spriteFile);

            var spriteCount = BitConverter.ToUInt32(spriteData.Take(4).ToArray(), 0);
            var dataSize = BitConverter.ToUInt32(spriteData.Skip(4).Take(4).ToArray(), 0);

            // palette offset is 0x08 + the spriteCount rounded to the greatest multiple of 4
            var palOffset = 0x08 + ((spriteCount + 3) & ~3);

            var palData = spriteData.Skip((int)palOffset).Take(0x300).ToArray();
            var palette = ColorHelper.ConvertBytesToRGB(palData);

            int headerOffset = (int)(palOffset + 0x300);

            var xOffset = BitConverter.ToInt16(spriteData.Skip(headerOffset).Take(2).ToArray(), 0);
            var yOffset = BitConverter.ToInt16(spriteData.Skip(headerOffset+2).Take(2).ToArray(), 0);

            if (xOffset > 320 || yOffset > 240) return;

            var headerList = new List<SpriteHeader>();
            int frameOffset = headerOffset + 4;
            for (int i = 0; i < spriteCount; i++)
            {
                var header = new SpriteHeader
                {
                    Offset = BitConverter.ToInt32(spriteData.Skip(frameOffset + (i * 8)).Take(4).ToArray(), 0),
                    Width = spriteData[frameOffset + (i * 8) + 4],
                    Height = spriteData[frameOffset + (i * 8) + 5],
                    X = spriteData[frameOffset + (i * 8) + 6],
                    Y = spriteData[frameOffset + (i * 8) + 7]
                };
                headerList.Add(header);
            }

            var imageData = spriteData.Skip((int)(frameOffset + (spriteCount * 8))).ToArray();

            var outputDir = Path.Combine(Path.GetDirectoryName(spriteFile), $"{Path.GetFileNameWithoutExtension(spriteFile)}");
            Directory.CreateDirectory(outputDir);

            var maxWidth = headerList.Max(h => h.Width + (h.X ));
            var maxHeight = headerList.Max(h => h.Height + (h.Y ));

            var frameWidth = maxWidth;
            var frameHeight = maxHeight;
            if (frameWidth < 0 || frameHeight < 0) Debugger.Break();

            foreach (var (header, hIndex) in headerList.WithIndex())
            {
                var nextOffset = hIndex == headerList.Count - 1 ? imageData.Length : headerList[hIndex + 1].Offset;
                var byteCount = nextOffset - header.Offset;
                var imageBytes = imageData.Skip(header.Offset).Take(byteCount).ToArray();
                if (header.Width == 0 || header.Height == 0) continue;
                var bytes = DecodeBORle(imageBytes, header.Width, header.Height, 0x00, true);
                if (bytes.Length == 0) continue;
                var image = ImageFormatHelper.GenerateClut4Image(palette, bytes, header.Width, header.Height);
                // create a new image with the frame width and height
                var newImage = new Bitmap(frameWidth, frameHeight);
                using (var g = Graphics.FromImage(newImage))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImage(image, header.X, header.Y);
                }
                newImage.Save(Path.Combine(outputDir, $"{hIndex}_{header.X}-{header.Y}.png"), ImageFormat.Png);
            }
        }

        public static byte[] DecodeBORle(byte[] data, int expectedWidth, int expectedHeight, byte flag, bool ignoreFlag = false)
        {
            // simple rle where flag is the byte to repeat, and the next byte is the count
            // otherwise the byte is copied as is.
            // if ignoreFlag is true, then 0x00 and 0xFF are both treated as flags
            var output = new List<byte>();
            var index = 0;
            while (index < data.Length)
            {
                var b = data[index++];
                if (ignoreFlag && (b == 0x00 || b == 0xFF))
                {
                    var count = data[index++];
                    for (int i = 0; i < count; i++)
                    {
                        output.Add(b);
                    }
                }
                else if (b == flag)
                {
                    var count = data[index++];
                    for (int i = 0; i < count; i++)
                    {
                        output.Add(flag);
                    }
                }
                else
                {
                    output.Add(b);
                }
            }
            // check if the output is the expected size
            if (output.Count < expectedWidth * expectedHeight)
            {
                //return [];
            }
            return output.ToArray();
        }
    }
}
