using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using ImageMagick;

namespace ExtractCLUT.Games.Generic
{
    public class AniMagic
    {
        public static void ExtractIMG(string imgPath, List<Color> palette, bool isChill = false, bool isImages = false)
        {
            var outputDirectory = Path.Combine(Path.GetDirectoryName(imgPath), "img_output", Path.GetFileNameWithoutExtension(imgPath));
            Directory.CreateDirectory(outputDirectory);
            var imgFileData = File.ReadAllBytes(imgPath);

            var unmaskedOffsetCount = 0;
            var switchOffsetCount = 0;
            var maskedOffsetCount = 0;

            var unmaskedOffsets = new List<uint>();
            var switchOffsets = new List<uint>();
            var maskedOffsets = new List<uint>();

            if (isImages)
            {
                unmaskedOffsetCount = BitConverter.ToUInt16(imgFileData.Skip(2).Take(2).ToArray(), 0);
                switchOffsetCount = BitConverter.ToUInt16(imgFileData.Skip(4).Take(2).ToArray(), 0);
                maskedOffsetCount = BitConverter.ToUInt16(imgFileData.Skip(6).Take(2).ToArray(), 0);
                // find next 4 bytes which match the sequence 0x00 0x10 0x00 0x00
                var start = 0xa;
                while (start < imgFileData.Length)
                {
                    if (imgFileData[start] == 0x00 && imgFileData[start + 1] == 0x10 && imgFileData[start + 2] == 0x00 && imgFileData[start + 3] == 0x00)
                    {
                        break;
                    }
                    start++;
                }
                for (int i = 0; i < unmaskedOffsetCount; i++)
                {
                    var offset = BitConverter.ToUInt32(imgFileData.Skip(start + (i * 4)).Take(4).ToArray(), 0);
                    unmaskedOffsets.Add(offset);
                }
                for (int i = 0; i < switchOffsetCount; i++)
                {
                    var offset = BitConverter.ToUInt32(imgFileData.Skip(start + (unmaskedOffsetCount * 4) + (i * 4)).Take(4).ToArray(), 0);
                    switchOffsets.Add(offset);
                }
                for (int i = 0; i < maskedOffsetCount; i++)
                {
                    var offset = BitConverter.ToUInt32(imgFileData.Skip(start + (unmaskedOffsetCount * 4) + (switchOffsetCount * 4) + (i * 4)).Take(4).ToArray(), 0);
                    maskedOffsets.Add(offset);
                }
            }
            else
            {
                unmaskedOffsetCount = BitConverter.ToUInt16(imgFileData.Skip(2).Take(2).ToArray(), 0);
                for (int i = 0; i < unmaskedOffsetCount; i++)
                {
                    var offset = BitConverter.ToUInt32(imgFileData.Skip(4 + (i * 4)).Take(4).ToArray(), 0);
                    unmaskedOffsets.Add(offset);
                }
            }



            for (int i = 0; i < unmaskedOffsetCount; i++)
            {
                var start = unmaskedOffsets[i];
                var end = (i == unmaskedOffsetCount - 1) ? (switchOffsetCount == 0 ? imgFileData.Length : (int)switchOffsets[0]) : (int)unmaskedOffsets[i + 1];
                var length = end - start;
                var data = imgFileData.Skip((int)start).Take((int)length).ToArray();
                if (isChill)
                {
                    var subOffsets = new List<uint>();
                    for (int j = 0; j < 0x1c; j += 4)
                    {
                        var subOffset = BitConverter.ToUInt32(data.Skip(j).Take(4).ToArray(), 0);
                        subOffsets.Add(subOffset);
                    }
                    for (int j = 0; j < subOffsets.Count; j++)
                    {
                        var subStart = subOffsets[j];
                        var subEnd = j == subOffsets.Count - 1 ? data.Length : (int)subOffsets[j + 1];
                        var subLength = subEnd - subStart;
                        var subData = data.Skip((int)subStart).Take((int)subLength).ToArray();
                        var width = subLength switch
                        {
                            0x1 => 1,
                            0x2 => 2,
                            0x4 => 2,
                            0x8 => 4,
                            0x10 => 4,
                            0x20 => 8,
                            0x40 => 8,
                            0x80 => 16,
                            0x100 => 16,
                            0x200 => 32,
                            0x400 => 32,
                            0x800 => 64,
                            0x1000 => 64,
                            0x2000 => 128,
                            _ => 0
                        };
                        var height = subLength / width;
                        //File.WriteAllBytes(Path.Combine(outputDirectory, $"{i}_{j}.bin"), subData);
                        var image = ImageFormatHelper.GenerateClutImage(palette, subData, (int)width, (int)height);
                        image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        image.Save(Path.Combine(outputDirectory, $"{i}_{j}.png"), ImageFormat.Png);
                    }
                }
                else
                {
                    var width = Math.Sqrt(length);
                    var image = ImageFormatHelper.GenerateClutImage(palette, data, (int)width, (int)width);
                    // rotate image 90 degrees clockwise
                    image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    image.Save(Path.Combine(outputDirectory, $"{i}.png"), ImageFormat.Png);
                }
            }

            for (int i = 0; i < switchOffsetCount; i++)
            {
                var start = switchOffsets[i];
                var end = (i == switchOffsetCount - 1) ? (maskedOffsetCount == 0 ? imgFileData.Length : (int)maskedOffsets[0]) : (int)switchOffsets[i + 1];
                var length = end - start;
                var data = imgFileData.Skip((int)start).Take((int)length).ToArray();
                if (isChill)
                {
                    var subOffsets = new List<uint>();
                    for (int j = 0; j < 0x1c; j += 4)
                    {
                        var subOffset = BitConverter.ToUInt32(data.Skip(j).Take(4).ToArray(), 0);
                        subOffsets.Add(subOffset);
                    }
                    for (int j = 0; j < subOffsets.Count; j++)
                    {
                        var subStart = subOffsets[j];
                        var subEnd = j == subOffsets.Count - 1 ? data.Length : (int)subOffsets[j + 1];
                        var subLength = subEnd - subStart;
                        var subData = data.Skip((int)subStart).Take((int)subLength).ToArray();
                        var width = subLength switch
                        {
                            0x2 => 2,
                            0x8 => 4,
                            0x20 => 8,
                            0x80 => 16,
                            0x100 => 16,
                            0x200 => 32,
                            0x400 => 32,
                            0x800 => 64,
                            0x1000 => 64,
                            0x2000 => 128,
                            _ => 0
                        };
                        var height = subLength / width;
                        var image = ImageFormatHelper.GenerateClutImage(palette, subData, (int)width, (int)height);
                        image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        image.Save(Path.Combine(outputDirectory, $"inter_{i}_{j}.png"), ImageFormat.Png);
                    }
                }
                else
                {
                    var width = Math.Sqrt(length);
                    var image = ImageFormatHelper.GenerateClutImage(palette, data, (int)width, (int)width);
                    // rotate image 90 degrees clockwise
                    image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    image.Save(Path.Combine(outputDirectory, $"inter_{i}.png"), ImageFormat.Png);
                }
            }

            for (int i = 0; i < maskedOffsetCount; i++)
            {
                var start = maskedOffsets[i];
                var end = (i == maskedOffsetCount - 1) ? imgFileData.Length : (int)maskedOffsets[i + 1];
                var length = end - start;
                var imgData = imgFileData.Skip((int)start).Take((int)length).ToArray();
                if (isChill)
                {
                    var subOffsets = new List<uint>();
                    for (int j = 0; j < 0x1c; j += 4)
                    {
                        var subOffset = BitConverter.ToUInt32(imgData.Skip(j).Take(4).ToArray(), 0);
                        subOffsets.Add(subOffset);
                    }
                    for (int j = 0; j < subOffsets.Count; j++)
                    {
                        var subStart = subOffsets[j];
                        var subEnd = j == subOffsets.Count - 1 ? imgData.Length : (int)subOffsets[j + 1];
                        var subLength = subEnd - subStart;
                        var subData = imgData.Skip((int)subStart).Take((int)subLength).ToArray();
                        // if (j == 0) File.WriteAllBytes(Path.Combine(outputDirectory, "bin", $"masked_{i}_{j}.bin"), subData);
                        ExtractIMGSpriteChill(palette, outputDirectory, i, j, subData);
                    }
                }
                else
                {
                    ExtractIMGSpriteMeen(palette, outputDirectory, i, imgData);
                }
            }
        }

        
        public static void ExtractLab(string inputPath)
        {
            var outputPath = Path.Combine(Path.GetDirectoryName(inputPath), "lab_output", Path.GetFileNameWithoutExtension(inputPath));
            Directory.CreateDirectory(outputPath);
            var data = File.ReadAllBytes(inputPath);
            if (data[0] == 0x0A || data[1] == 0x05 || data[2] == 0x01) return;
            // read 16 byte file details in loop until we get 16bytes of 0x00
            var index = 0;
            var offsets = new Dictionary<string, int>();
            while (data.Skip(index).Take(4).All(b => b != 0x00))
            {
                var fileDetails = data.Skip(index).Take(16).ToArray();
                var fileName = Encoding.ASCII.GetString(fileDetails.Take(12).ToArray()).TrimEnd('\0');
                var fiOffset = BitConverter.ToUInt32(fileDetails.Skip(12).Take(4).ToArray(), 0);
                // if filename exists, then we have a duplicate, so add a suffix
                if (offsets.ContainsKey(fileName))
                {
                    var suffix = 1;
                    while (offsets.ContainsKey($"{fileName}_{suffix}"))
                    {
                        suffix++;
                    }
                    fileName = $"{fileName}_{suffix}";
                }
                offsets.Add(fileName, (int)fiOffset);
                index += 16;
            }

            foreach (var offset in offsets)
            {
                var fileName = offset.Key;
                var fileOffset = offset.Value;
                var nextOffset = offsets.SkipWhile(o => o.Key != fileName).Skip(1).FirstOrDefault().Value;
                // if this is the last offset, then next offset is the end of the file
                if (nextOffset == 0)
                {
                    nextOffset = data.Length;
                }
                var fileData = data.Skip(fileOffset).Take(nextOffset - fileOffset).ToArray();
                File.WriteAllBytes(Path.Combine(outputPath, fileName), fileData);
            }
        }

        static void ExtractIMGSpriteMeen(List<Color> palette, string outputDirectory, int i, byte[] imgData)
        {
            var height = BitConverter.ToUInt16(imgData.Take(2).ToArray(), 0);
            var width = BitConverter.ToUInt16(imgData.Skip(2).Take(2).ToArray(), 0);
            var paddingBelow = imgData[6];
            var top = imgData[5];
            var paddingAbove = imgData[4];
            var bottom = imgData[7];

            var footerOffset = BitConverter.ToUInt16(imgData.Skip(0xc).Take(2).ToArray(), 0);

            var footerBytes = imgData.Skip(footerOffset).Take(0x8).ToArray();
            var footerOffsetCount = (BitConverter.ToUInt16(footerBytes.Skip(4).Take(2).ToArray(), 0) - 4) / 2;
            var footerOffsets = new List<ushort>();

            for (int j = 0; j < footerOffsetCount; j += 2)
            {
                var offset = BitConverter.ToUInt16(imgData.Skip(4 + (j)).Take(2).ToArray(), 0);
                footerOffsets.Add(offset);
            }

            var imageLines = new List<byte[]>();

            for (int j = 0; j < paddingAbove; j++)
            {
                imageLines.Add(new byte[64]);
            }

            for (int j = 0; j < footerOffsets.Count; j++)
            {
                // ===== [Footer] =====
                // The footer is composed of the pixel lines.By which, I mean it tells the game where to put each pixel. Each section is formatted as such.

                // "AA 00 BB 00 CC 00 XX 00"

                // AA - The end of the pixel set on that vertical line.The exact spot the transparency starts again.
                // BB - The start of the pixel line.The exact spot of the first pixel in line.
                // CC - The offset of the pixels themselves.Read from Pixel Data. Add "1C000" to this byte.
                // XX - trigger / command.If it is 00, then there are no more pixels on that vertical line, move onto the next one. If it is not 00, treat it as the next command's AA and continue on the same line.
                var imageLineData = new byte[64];
                var currentOffset = footerOffsets[j];
                var nextOffset = j == footerOffsets.Count - 1 ? imgData.Length : footerOffsets[j + 1];
                var lineData = imgData.Skip(currentOffset).Take(nextOffset - currentOffset).ToArray();

                var command = -1;

                while (lineData.Length >= 6)
                {
                    var pixelEnd = BitConverter.ToUInt16(lineData.Take(2).ToArray(), 0);
                    if (pixelEnd == 0)
                    {
                        imageLines.Add(imageLineData);
                        imageLineData = new byte[64];
                        lineData = lineData.Skip(2).ToArray();
                        continue;
                    }
                    var pixelStart = BitConverter.ToUInt16(lineData.Skip(2).Take(2).ToArray(), 0);
                    var pixelOffset = BitConverter.ToUInt16(lineData.Skip(4).Take(2).ToArray(), 0);
                    var pixelCount = pixelEnd - pixelStart;
                    var pixelData = imgData.Skip(pixelOffset).Take(pixelCount).ToArray();
                    // insert pixel data into imageLineData at pixelStart
                    for (int k = 0; k < pixelCount; k++)
                    {
                        imageLineData[pixelStart + k] = pixelData[k];
                    }
                    command = BitConverter.ToUInt16(lineData.Skip(6).Take(2).ToArray(), 0);
                    if (command == 0)
                    {
                        imageLines.Add(imageLineData);
                        imageLineData = new byte[64];
                        lineData = lineData.Skip(8).ToArray();
                    }
                    else
                    {
                        lineData = lineData.Skip(6).ToArray();
                    }
                }
            }
            var imageBytes = imageLines.SelectMany(l => l).ToArray();
            var image = ImageFormatHelper.GenerateClutImage(palette, imageBytes, width, width, true);
            // rotate image 90 degrees clockwise
            image.RotateFlip(RotateFlipType.Rotate90FlipNone);
            image.Save(Path.Combine(outputDirectory, $"masked_{i}.png"), ImageFormat.Png);
        }

        static void ExtractIMGSpriteChill(List<Color> palette, string outputDirectory, int i, int i2, byte[] imgData)
        {
            if (imgData.Length <= 0xc || i2 > 0) return;
            var height = BitConverter.ToUInt16(imgData.Take(2).ToArray(), 0);
            if (height <= 1) return;
            var width = BitConverter.ToUInt16(imgData.Skip(2).Take(2).ToArray(), 0);
            var leftPadding = BitConverter.ToUInt16(imgData.Skip(4).Take(2).ToArray(), 0);
            var topPadding = BitConverter.ToUInt16(imgData.Skip(8).Take(2).ToArray(), 0);
            var footerOffsetCount = BitConverter.ToUInt16(imgData.Skip(0xa).Take(2).ToArray(), 0) - topPadding;
            var footerOffset = BitConverter.ToUInt16(imgData.Skip(0xc).Take(2).ToArray(), 0);

            var footerBytes = imgData.Skip(0xe).Take((footerOffsetCount) * 2).ToArray();
            var footerOffsets = new List<ushort>
            {
                footerOffset
            };

            for (int j = 0; j < footerOffsetCount; j++)
            {
                var offset = BitConverter.ToUInt16(footerBytes.Skip(j * 2).Take(2).ToArray(), 0);
                footerOffsets.Add(offset);
            }

            var imageLines = new List<byte[]>();

            if (topPadding > 0)
            {
                for (int j = 0; j < topPadding; j++)
                {
                    imageLines.Add(new byte[width]);
                }
            }

            //var sb = new StringBuilder();
            for (int j = 0; j < footerOffsets.Count; j++)
            {
                var imageLineData = new byte[width];
                var currentOffset = footerOffsets[j];
                var nextOffset = j == footerOffsets.Count - 1 ? imgData.Length : footerOffsets[j + 1];
                var lineData = imgData.Skip(currentOffset).Take(nextOffset - currentOffset).ToArray();
                // // convert lineData to a string of bytes 
                // foreach (var b in lineData)
                // {
                //     sb.Append($"{b:X2} ");
                // }
                // sb.AppendLine();

                if (lineData[0] == 0xFF)
                {
                    continue;
                }

                var lineDataIndex = 0;
                var imageLineDataIndex = leftPadding;

                var pixelsRemaining = 0;

                if (lineData[0] == 0) {
                    pixelsRemaining = 128;
                    lineDataIndex++;
                }
                
                while (lineDataIndex < lineData.Length && imageLineDataIndex < width)
                {
                    if (((lineData[lineDataIndex] & 0x80) > 0) && pixelsRemaining == 0)
                    {
                        var count = (lineData[lineDataIndex] & 0x7f); 
                        // insert (lineData[index] & 0x7f) transparent pixels
                        for (int k = 0; k < count; k++)
                        {
                            if (imageLineDataIndex >= width) break;
                            imageLineData[imageLineDataIndex] = 0;
                            imageLineDataIndex++;
                        }
                        lineDataIndex++;
                    }
                    else if (lineData[lineDataIndex] < 0x80 && pixelsRemaining == 0)
                    {
                        // the number of pixels following this byte
                        pixelsRemaining = lineData[lineDataIndex] > 0 ? lineData[lineDataIndex] : width - imageLineDataIndex;
                        lineDataIndex++;
                    } 
                    else
                    {
                        // insert as pixel
                        imageLineData[imageLineDataIndex++] = lineData[lineDataIndex++];
                        pixelsRemaining--;
                    }
                }
                imageLines.Add(imageLineData);
            }
            var imageBytes = imageLines.SelectMany(l => l).ToArray();
            // write sb to file
            //File.WriteAllText(Path.Combine(outputDirectory, $"masked_{i}_{i2}.txt"), sb.ToString());
            var image = ImageFormatHelper.GenerateClutImage(palette, imageBytes, width, height, true);
            // rotate image 90 degrees clockwise
            image.RotateFlip(RotateFlipType.Rotate90FlipNone);
            image.Save(Path.Combine(outputDirectory, $"masked_{i}_{i2}.png"), ImageFormat.Png);
        }

        public static void ExtractCMP(string cmpPath)
        {
            var cmpBytes = File.ReadAllBytes(cmpPath);
            // first four bytes is the filesize remaining
            var fileSize = BitConverter.ToUInt32(cmpBytes.Take(4).ToArray(), 0);
            cmpBytes = cmpBytes.Skip(4).ToArray();

        }
    }
}
