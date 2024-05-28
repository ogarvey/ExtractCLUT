using static ExtractCLUT.Helpers.ColorHelper;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using OGLibCDi.Models;
using System.Drawing.Imaging;
using System.Drawing;
using System.Diagnostics;

namespace ExtractCLUT.Games
{
    public static class ZeldasAdventure
    {
        public static void ExtractAll()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            var underCdi = new CdiFile(@"C:\Dev\Projects\Gaming\CD-i\Zelda\under.rtf");

            var sectors = underCdi.Sectors;

            var dataSectorGroup = new List<CdiSector>();
            var dyuvSectorGroup = new List<CdiSector>();
            foreach (var sector in sectors)
            {
                if (sector.Coding.VideoString == "DYUV" && sector.SubMode.IsVideo)
                {
                    dyuvSectorGroup.Add(sector);
                }
                if (sector.SubMode.IsData)
                {
                    dataSectorGroup.Add(sector);
                    var data = sector.GetSectorData();
                    if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x01 && data[3] == 0x00)
                    {
                        var index = dataSectorGroup.First().SectorIndex;
                        var paletteBytes = data.Skip(4).Take(0x300).ToArray();
                        File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Zelda\Analysis\Data\under\{index}_palette.bin", paletteBytes);
                    }
                }

                if (sector.SubMode.IsEOR)
                {
                    var data = dataSectorGroup.SelectMany(x => x.GetSectorData(true)).ToArray();
                    var dyuvLineAnimData = dataSectorGroup.Skip(1).Take(9).SelectMany(x => x.GetSectorData()).ToArray();
                    var index = dataSectorGroup.First().SectorIndex;
                    File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Zelda\Analysis\Data\under\{index}_{dataSectorGroup.Count}.bin", data);

                    var dyuvData = dyuvSectorGroup.SelectMany(x => x.GetSectorData()).ToArray();
                    File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Zelda\Analysis\Data\under\{index}_dyuv.bin", dyuvData);
                    var dyuvLineData = dataSectorGroup.First().GetSectorData();
                    var image = DecodeDYUVImage(dyuvData, 384, 240, useArray: true, yuvArray: dyuvLineData);
                    image.Save($@"C:\Dev\Projects\Gaming\CD-i\Zelda\Analysis\Data\under\Screens\{index}.png", ImageFormat.Png);

                    dyuvSectorGroup.Clear();
                    var outputPath = $@"C:\Dev\Projects\Gaming\CD-i\Zelda\Analysis\Data\under\SpriteSectors\{index}.bin";
                    var spriteData = dataSectorGroup.Skip(25).SelectMany(x => x.GetSectorData()).ToArray();
                    var spriteHeaderData = dataSectorGroup.Skip(21).Take(4).SelectMany(x => x.GetSectorData()).ToArray();
                    File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Zelda\Analysis\Data\under\SpriteSectors\Headers\{index}.bin", spriteHeaderData);
                    var nullByteCount = 0;

                    for (int i = 0; i < spriteData.Length; i++)
                    {
                        if (spriteData[i] == 0)
                        {
                            nullByteCount++;
                        }
                        else
                        {
                            nullByteCount = 0;
                        }
                        if (nullByteCount == 16)
                        {
                            spriteData = spriteData.Take(i - 15).ToArray();
                            break;
                        }
                    }
                    File.WriteAllBytes(outputPath, spriteData);
                    dataSectorGroup.Clear();

                }
            }


            var files = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Zelda\Analysis\Data\under\SpriteSectors\", "*.bin").OrderBy(x => int.Parse(Path.GetFileNameWithoutExtension(x)));

            foreach (var file in files)
            {
                var fileSize = new FileInfo(file).Length;
                if (fileSize == 0)
                {
                    continue;
                }
                var index = int.Parse(Path.GetFileNameWithoutExtension(file));
                var paletteFile = $@"C:\Dev\Projects\Gaming\CD-i\Zelda\Analysis\Data\under\{index}_palette.bin";

                var paletteData = File.ReadAllBytes(paletteFile).Skip(0x180).Take(0x180).ToArray();
                var palette = ConvertBytesToRGB(paletteData);
                palette = palette.Select(c =>
                {
                    var r = c.R;
                    var g = c.G;
                    var b = c.B;
                    if (r == 0 && g == 0 && b == 0)
                    {
                        return Color.FromArgb(0, 0, 0, 0);
                    }
                    return c;
                }).ToList();

                try
                {
                    ZeldasAdventure.ExtractSpriteData(file, palette);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error extracting sprite data for {file}, with error {e.Message}");
                }
            }
            stopwatch.Stop();
            // Output the elapsed time in milliseconds
            Console.WriteLine($"Execution Time: {stopwatch.ElapsedMilliseconds} ms");

        }
        public static void ExtractSpriteData(string input, List<Color> Palette)
        {
            var data = File.ReadAllBytes(input);
            var output = Path.Combine(Path.GetDirectoryName(input), "output");
            var outputSprites = Path.Combine(output, $"{Path.GetFileNameWithoutExtension(input)}_Sprites");
            Directory.CreateDirectory(outputSprites);

            var SpriteDataList = new List<byte[]>();
            IList<int> StartOffsetsSubSub = new List<int>();
            //IList<string> StartOffsetsSubSubName = new List<string>();
            IList<int> StartOffsetsMain = new List<int>();

            int readOffset = 0;
            int readOffsetMain = readOffset;
            {
                int fileCount = Parser.ArrayToInt(Functions.GetNBytes(data, readOffsetMain, 4)); readOffsetMain += 4;

                StartOffsetsMain.Add(Parser.ArrayToInt(Functions.GetNBytes(data, readOffsetMain, 4))); readOffsetMain += 4;
                int sizeHeader = Parser.ArrayToInt(Functions.GetNBytes(data, readOffsetMain, 4)); readOffsetMain += 4;
                StartOffsetsMain[0] += sizeHeader;
                //Console.WriteLine("M[" + (readOffsetMain - 4).ToString("X") + "] = " + sizeHeader.ToString("X"));

                for (int k = 1; k < fileCount; k++)
                {
                    int startOffsetMain = Parser.ArrayToInt(Functions.GetNBytes(data, readOffsetMain, 4)); readOffsetMain += 4;
                    //Console.WriteLine("M[" + (readOffsetMain - 4).ToString("X") + "] = " + startOffsetMain.ToString("X"));
                    StartOffsetsMain.Add(startOffsetMain);
                }
            }

            for (int k = 0; k < StartOffsetsMain.Count(); k++)
            {
                //Console.WriteLine(" ");
                //Console.WriteLine("M: " + StartOffsetsMain[k].ToString("X"));
                IList<int> StartOffsetsSub = new List<int>();
                int readOffsetSub = readOffset + StartOffsetsMain[k];
                {
                    int nrFiles = Parser.ArrayToInt(Functions.GetNBytes(data, readOffsetSub, 4)); readOffsetSub += 4;

                    //int unk = Parser.ArrayToInt(Functions.GetNBytes(data, readOffsetSub, 4)); 
                    readOffsetSub += 4;
                    int sizeHeader = Parser.ArrayToInt(Functions.GetNBytes(data, readOffsetSub, 4)); readOffsetSub += 4;
                    if (nrFiles > 0)
                    {
                        StartOffsetsSub.Add(sizeHeader);
                        //Console.WriteLine("S[" + (readOffsetSub - 4).ToString("X") + "] = " + sizeHeader.ToString("X"));
                    }
                    for (int l = 1; l < nrFiles; l++)
                    {
                        int startOffsetSub = Parser.ArrayToInt(Functions.GetNBytes(data, readOffsetSub, 4));
                        //Console.WriteLine("S[" + readOffsetSub.ToString("X") + "] = " + startOffsetSub.ToString("X"));
                        StartOffsetsSub.Add(startOffsetSub);
                        readOffsetSub += 4;
                    }
                }

                for (int l = 0; l < StartOffsetsSub.Count(); l++)
                {
                    //Console.WriteLine("S: " + StartOffsetsSub[l].ToString("X"));
                    //IList<int> lstStartOffsetsSubSub = new List<int>();
                    int readOffsetSubSub = readOffset + StartOffsetsMain[k] + StartOffsetsSub[l];
                    {
                        int nrFiles = Parser.ArrayToInt(Functions.GetNBytes(data, readOffsetSubSub, 4)); readOffsetSubSub += 4;
                        //Console.WriteLine("S: nr = " + nrFiles);
                        int unk = Parser.ArrayToInt(Functions.GetNBytes(data, readOffsetSubSub, 4)); readOffsetSubSub += 4;
                        int sizeHeader = Parser.ArrayToInt(Functions.GetNBytes(data, readOffsetSubSub, 4)); readOffsetSubSub += 4;
                        if (nrFiles > 0)
                        {
                            StartOffsetsSubSub.Add(sizeHeader + StartOffsetsMain[k] + StartOffsetsSub[l]);
                            //StartOffsetsSubSubName.Add(string.Format("{0}-{1}-{2}", k, l, 0));
                            //Console.WriteLine("SS[" + (readOffsetSubSub - 4).ToString("X") + "] " + StartOffsetsSubSub[StartOffsetsSubSub.Count() - 1].ToString("X"));
                        }
                        for (int m = 1; m < nrFiles; m++)
                        {
                            int startOffsetSubSub = Parser.ArrayToInt(Functions.GetNBytes(data, readOffsetSubSub, 4));
                            StartOffsetsSubSub.Add(startOffsetSubSub + StartOffsetsMain[k] + StartOffsetsSub[l]);
                            //StartOffsetsSubSubName.Add(string.Format("{0}-{1}-{2}", k, l, m));
                            //Console.WriteLine("SS[" + readOffsetSubSub.ToString("X") + "] " + StartOffsetsSubSub[StartOffsetsSubSub.Count() - 1].ToString("X"));
                            readOffsetSubSub += 4;
                        }
                    }
                }
            }

            //Console.WriteLine(" ");
            //Console.WriteLine("sprite offsets: " + string.Join(" ", StartOffsetsSubSub.Select(x => x.ToString("X")).ToArray()));

            for (int j = 0; j < StartOffsetsSubSub.Count(); j++)
            {
                int spriteLength = Parser.ArrayToInt(Functions.GetNBytes(data, readOffset + StartOffsetsSubSub[j], 4)) - 4;
                byte[] spriteData = new byte[spriteLength];
                Buffer.BlockCopy(data, readOffset + StartOffsetsSubSub[j] + 4, spriteData, 0, spriteLength);
                //dctSpriteData.Add(String.Format("subfile {0}-spriteData-{1:d2}-{2:X4}.dat", i, j, lstStartOffsetsSubSub[j]), spriteData);
                SpriteDataList.Add(spriteData);
            }

            var index = int.Parse(Path.GetFileNameWithoutExtension(input));
            foreach (var (spriteData, counter) in SpriteDataList.WithIndex())
            {
                Functions.ZeldasAdventure(spriteData, $@"{outputSprites}\{counter}.png", Palette, index: index);
            }
        }
    }
}

public class SpriteSize
{
    public int Width { get; set; }
    public int Height { get; set; }
}

public static class Parser
{
    public static int ArrayToInt(byte[] arr)
    {
        int num = 0;
        for (int index = 0; index < arr.Length; ++index)
            num += (int)arr[index] << 8 * (arr.Length - 1 - index);
        return num;
    }
}

public static class Functions
{
    public static void ZeldasAdventure(byte[] spriteData, string fileName, List<Color> colorTable, bool dumpBinary = false, int index = 0)
    {
        var sizeList = new List<SpriteSize>();
        if (index > 0)
        {
            var underOver = fileName.Contains("under") ? "under" : "over";
            var headerData = File.ReadAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Zelda\Analysis\Data\{underOver}\SpriteSectors\Headers\{index}.bin");
            var headerCount = headerData.Skip(0x0b).Take(1).First();
            var sizeDataOffset = headerCount switch {
                3 => 0x34,
                5 => 0x3c,
                7 => 0x44,
                _ => 0x3c
            };
            var dataStart = headerData.Skip(0x13).Take(1).First();
            var sizeCount = headerData.Skip(dataStart+(sizeDataOffset-9)).Take(1).First();
            var sizeData = headerData.Skip(dataStart+sizeDataOffset).Take(sizeCount * 0x2e).ToArray();
            for (int sIndex = 0; sIndex < sizeCount*0x2e; sIndex+=0x2e)
            {
                var height = BitConverter.ToInt16(sizeData.Skip(sIndex).Take(2).Reverse().ToArray(), 0);
                var width = BitConverter.ToInt16(sizeData.Skip(sIndex+2).Take(2).Reverse().ToArray(), 0);
                sizeList.Add(new SpriteSize { Width = width, Height = height });
            }
        }
        //reserve screen-sized byte array (a single sprite will never be this big) and prefill with 'background'
        byte[] imageData = new byte[384 * 240];
        for (int j = 0; j < 384 * 240; j++)
        {
            imageData[j] = 0x00;
        }

        //decode pixel shifted sprite
        int i = 0;
        int writeOffset = 0;
        while (i < spriteData.Count())
        {
            int skipLength = BitConverter.ToInt16(spriteData.Skip(i).Take(2).Reverse().ToArray(), 0);
            int nrBytes = BitConverter.ToInt16(spriteData.Skip(i + 2).Take(2).Reverse().ToArray(), 0) * 4;
            i += 4;
            writeOffset += skipLength;
            Buffer.BlockCopy(spriteData, i, imageData, writeOffset, nrBytes);
            writeOffset += nrBytes;
            i += nrBytes;
        }

        //determine number of rows required for sprite and copy sprite to smaller datablob
        i = writeOffset + (384 - writeOffset % 384);
        byte[] sprite = new byte[i];
        Buffer.BlockCopy(imageData, 0, sprite, 0, i);
        if (dumpBinary && sprite.Length != 0) {
            File.WriteAllBytes(fileName.Replace(".png", "_raw.bin"), sprite);
        }
        //determine width and height of sprite (and extract datablob of these dimensions)
        // int width = 0;
        // int height = 0;
        // imageData = ExtractSprite(sprite, 384, ref width, ref height);

        //decode CLUT of sprite
        // var image = GenerateClutImage(colorTable, imageData, width, height, true);
        // image.Save(fileName, ImageFormat.Png);
        var image = GenerateClutImage(colorTable, sprite, 384, 240);
        if (sizeList.Count > 0)
        {
            for (int sIndex = 0; sIndex < sizeList.Count; sIndex++)
            {
                Rectangle cropRect = new Rectangle(0, 0, sizeList[sIndex].Width, sizeList[sIndex].Height);

                // Crop the image
                using (Bitmap croppedImage = image.Clone(cropRect, image.PixelFormat))
                {
                    // if every pixel of image is black, skip saving
                    for (int x = 0; x < croppedImage.Width; x++)
                    {
                        for (int y = 0; y < croppedImage.Height; y++)
                        {
                            if (croppedImage.GetPixel(x, y).ToArgb() != Color.Black.ToArgb())
                            {
                                break;
                            }
                            if (x == croppedImage.Width - 1 && y == croppedImage.Height - 1)
                            {
                                return;
                            }
                        }
                    }                    

                    // Save the cropped image with "_icon" suffix
                    var cropFileName = fileName.Replace(".png", $"_cropped_{sIndex}.png");

                    croppedImage.Save(cropFileName, ImageFormat.Png);
                }
            }
        }
        else
        {
            image.Save(fileName.Replace(".png", "_alt.png"), ImageFormat.Png);
        }
    }

    public static byte[] GetNBytes(byte[] input, int start, int nrBytes)
    {
        byte[] dst = new byte[nrBytes];
        Buffer.BlockCopy(input, start, dst, 0, nrBytes);
        return dst;
    }

    public static byte[] ExtractSprite(
        byte[] spriteData,
        int baseWidth,
        ref int resultWidth,
        ref int resultHeight)
    {
        int num1 = ((IEnumerable<byte>)spriteData).Count<byte>();
        int num2 = 0;
        for (; num2 * baseWidth < num1; ++num2)
        {
            for (int index1 = resultWidth; index1 < baseWidth; ++index1)
            {
                int index2 = num2 * baseWidth + index1;
                if (index2 < num1 && spriteData[index2] > (byte)0 && spriteData[index2] < byte.MaxValue)
                    resultWidth = index1;
            }
        }
        resultHeight = num2;
        ++resultWidth;
        byte[] dst = new byte[resultWidth * resultHeight];
        for (int index = 0; index < resultWidth * resultHeight; ++index)
            dst[index] = byte.MaxValue;
        for (int index = 0; index < resultHeight; ++index)
        {
            int count = Math.Min(resultWidth, spriteData.Length - index * baseWidth);
            Buffer.BlockCopy((Array)spriteData, index * baseWidth, (Array)dst, index * resultWidth, count);
        }
        return dst;
    }
}

/*

Sprite Header File:

0x04 - 4 byte value - End of Data?
0x08 - 4 byte value - File ?Block Count?
0x0C - 4 byte value - UNKNOWN
0x10 - 4 byte value - Start of Data?
0x14 - 4 byte value - UNKNOWN
0x18 - 4 byte value - UNKNOWN
0x1C - 4 byte value - UNKNOWN - Same as 0x08?

*/
