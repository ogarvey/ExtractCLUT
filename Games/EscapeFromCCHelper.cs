using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using ExtractCLUT.Writers;

namespace ExtractCLUT.Games
{
    public static class EscapeFromCCHelper
    {
        static string _DyuvPath = @"C:\Dev\Projects\Gaming\CD-i\Escape From CyberCity (R)\NewRecords\CCStart1\video\output\CCStart1_v_1_0_DYUV_Normal_3.bin";
        static string _outputPath = @"C:\Dev\Projects\Gaming\CD-i\Escape From CyberCity (R)\NewRecords\CCStart1\video\output\processed";
        public static void ExtractDYUVs()
        {
            Directory.CreateDirectory(_outputPath);
            var images = new List<Bitmap>();
            var bytes = File.ReadAllBytes(_DyuvPath);
            for (int i = 0; i < bytes.Length; i+= 92960)
            {
                var dyuv = i > 0 ? bytes.Skip(i).Take(92160).ToArray() : bytes.Take(92160).ToArray();
                var image = ImageFormatHelper.DecodeDYUVImage(dyuv, 384, 240);
                images.Add(image);
            }

            using (var gifWriter = new GifWriter($"{_outputPath}/DYUV.gif", 400, -1))
            {
                foreach (var image in images)
                {
                    gifWriter.WriteFrame(image);
                }
            }
        }
    }
}
