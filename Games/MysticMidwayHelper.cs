using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using OGLibCDi;
using OGLibCDi.Models;
using OGLibCDi.Enums;
using Image = System.Drawing.Image;

namespace ExtractCLUT.Games
{
    public static class MysticMidwayHelper
    {
        public static void ParseRIPVideo(string ripVideoPath)
        {
            var cdiFile = new CdiFile(ripVideoPath);

            var dataSector = cdiFile.DataSectors.First().GetSectorData().AsSpan();

            var paletteData = dataSector.Slice(0x5a, 0x208);

            var palette = ColorHelper.ReadClutBankPalettes(paletteData.ToArray(), 2);

            var sectorCountData = dataSector.Slice(0x46a, 0x359);

            var rl7Sectors = cdiFile.VideoSectors.Where(vs => vs.Coding.Coding == (int)CdiVideoType.RL7).ToList();

            var rl7Data = new List<byte[]>();

            foreach (var sectorCount in sectorCountData)
            {
                var sectors = rl7Sectors.Take(sectorCount).Select(s => s.GetSectorData()).ToList();
                rl7Sectors.RemoveRange(0, sectorCount);
                rl7Data.Add(sectors.SelectMany(s => s).ToArray());
            }

            var rl7ImageList = new List<Image>();

            foreach (var rl7 in rl7Data)
            {
                rl7ImageList.Add(GenerateRle7Image(palette, rl7, 384, 240).Scale4());
            }

            CreateGifFromImageList(rl7ImageList, @"C:\Dev\Projects\Gaming\CD-i\Mystic Midway_ Rest in Pieces\output\ripvideo1_x4.gif",10,1);
        }
    }
}
