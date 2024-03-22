using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using OGLibCDi.Models;

namespace ExtractCLUT.Games
{
    public class KetherHelper
    {
        public void ExtractRL7Anim(string videoFile)
        {

            var kether = new CdiFile(videoFile);

            var rl7Sectors = kether.Sectors.Where(x => x.SubMode.IsVideo && x.Coding.VideoString == "RL7").OrderBy(x => x.Channel)
              .ThenBy(x => x.SectorIndex).ToList();

            var byteArrayList = new List<byte[]>();

            foreach (var (sector, index) in rl7Sectors.WithIndex())
            {
                var data = sector.GetSectorData();
                byteArrayList.Add(data);
                if (sector.SubMode.IsTrigger)
                {
                    var imageBytes = byteArrayList.SelectMany(x => x).ToArray();
                    var palBytes = imageBytes.Skip(0x4).Take(0x180).ToArray();
                    var pal = ColorHelper.ConvertBytesToRGB(palBytes);
                    var image = ImageFormatHelper.GenerateRle7Image(pal, imageBytes.Skip(0x184).ToArray(), 384, 280, false);
                    image.Save(Path.Combine(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Kether\output\dead", $"kether_{index}.png"), ImageFormat.Png);
                    byteArrayList.Clear();
                }
            }

        }
    }
}
