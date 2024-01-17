using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games
{
    public static class ZeldasAdventure
    {
        static void ExtractSpriteData()
        {

            var file = @"C:\Dev\Projects\Gaming\CD-i\Zelda_Release\Output\zelda\zelda.rtf_1_1_RL7_Normal_Even_805.bin";

            var data = File.ReadAllBytes(file);
            // number of data blobs
            var offsetCount = data[0x3];

            var offsets = new List<int>();

            // Get the offsets
            for (int i = 8; offsets.Count < offsetCount; i += 4)
            {
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(data, i, 4);
                }
                var offset = BitConverter.ToInt32(data, i);
                offsets.Add(offset);
            }

            var byteArrays = new List<byte[]>();

            // Get the data blobs
            foreach (var item in offsets)
            {
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(data, item, 4);
                }
                var length = BitConverter.ToInt32(data, item);
                byteArrays.Add(data.Skip(item + 4).Take(length).ToArray());
            }


            foreach (var (spriteImage, counter) in byteArrays.WithIndex())
            {
                var index = 0;
                var imageBytes = new byte[0];
                while (index < spriteImage.Length)
                {
                    var blankPixelsToAdd = 0;
                    var banks = 0;
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(spriteImage, index, 2);
                        blankPixelsToAdd = BitConverter.ToInt16(spriteImage, index);
                        index += 2;
                        Array.Reverse(spriteImage, index, 2);
                        banks = BitConverter.ToInt16(spriteImage, index);
                        index += 2;
                    }

                    var bankByteCount = banks * 4;

                    imageBytes = imageBytes.Concat(Enumerable.Repeat<byte>(0, blankPixelsToAdd)).ToArray();

                    Console.WriteLine($"Blank pixels added: {blankPixelsToAdd}, new length: {imageBytes.Length}");

                    var bankImageBytes = spriteImage.Skip(index).Take(bankByteCount).ToArray();
                    imageBytes = imageBytes.Concat(bankImageBytes).ToArray();

                    Console.WriteLine($"Bank pixel added: {bankImageBytes.Length}, new length: {imageBytes.Length}");

                    index += bankByteCount;
                }

                var outputFile = Path.GetFileNameWithoutExtension(file) + $"_{counter}_parsed.bin";
                var outputDir = Path.Combine(Path.GetDirectoryName(file), "parsed");
                Directory.CreateDirectory(outputDir);
                File.WriteAllBytes(Path.Combine(outputDir, outputFile), imageBytes);
            }

        }
        private static string _rl7Path = @"C:\Dev\Projects\Gaming\CD-i\Zelda\records\zelda_rl\video\zelda_rl_v_1_1_RL7_Normal_3.bin";
        private static string _rlDyuvPath = @"C:\Dev\Projects\Gaming\CD-i\Zelda\records\zelda_rl\video\zelda_rl_v_1_1_DYUV_Normal_4.bin";
        private static string _overDyuvPath = @"C:\Dev\Projects\Gaming\CD-i\Zelda\records\over\video\over_v_1_1_DYUV_Normal_1.bin";

        public static void ExtractOverDyuv()
        {
            var dyuvBytes = File.ReadAllBytes(_overDyuvPath);

            for (int i = 0; i < dyuvBytes.Length; i += 92960)
            {
                var data = dyuvBytes.Skip(i).Take(92160).ToArray();
                var image = ImageFormatHelper.DecodeDYUVImage(data, 384, 240);
                image.Save($@"C:\Dev\Projects\Gaming\CD-i\Zelda\records\over\video\output\dyuv16\Normal_4_{i}.png");
            }
        }
        public static void ExtractRLDyuv()
        {
            var dyuvBytes = File.ReadAllBytes(_rlDyuvPath);

            for (int i = 0; i < dyuvBytes.Length; i+= 92160)
            {
                var data = dyuvBytes.Skip(i).Take(92160).ToArray();
                var image = ImageFormatHelper.DecodeDYUVImage(data, 384, 240);
                image.Save($@"C:\Dev\Projects\Gaming\CD-i\Zelda\records\zelda_rl\video\output\dyuv\Normal_4_{i}.png");
            }
        }
        public static void ExtractRL7() 
        {
            var paletteData1 = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Zelda\records\zelda_rl\data\zelda_rl_9417408_9424464_d_2.bin").Skip(4).Take(512).ToArray();
            var paletteData2 = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Zelda\records\zelda_rl\data\zelda_rl_9417408_9424464_d_2.bin").Skip(2052).Take(512).ToArray();
            var palette = ColorHelper.ConvertBytesToRGB(paletteData1);
            //var fileData = File.ReadAllBytes(_rl7Path);
            var chunks = FileHelpers.SplitBinaryFileIntoChunks(_rl7Path, new byte[] { 0x00, 0x00, 0x00 }, true, true,0);
            // var count = chunks.Count();
            // for (int i = 0; i < 88; i++)
            // {
            //     var data = chunks[i];
            //     var image = ImageFormatHelper.GenerateRle7Image(palette, data, 384, 240);
            //     image.Save($@"C:\Dev\Projects\Gaming\CD-i\Zelda\records\zelda_rl\video\output\rle7\Normal_3_{i}.png");
            // }
            palette = ColorHelper.ConvertBytesToRGB(paletteData2);
            for (int i = 188; i < 200; i++)
            {
                var data = chunks[i];
                var image = ImageFormatHelper.GenerateRle7Image(palette, data, 384, 240);
                image.Save($@"C:\Dev\Projects\Gaming\CD-i\Zelda\records\zelda_rl\video\output\rle7\Normal_3_{i}.png");
            }
        }
    }
}
