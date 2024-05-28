using static ExtractCLUT.Helpers.ColorHelper;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using OGLibCDi.Models;
using System.Drawing.Imaging;

namespace ExtractCLUT.Games
{
    public class RiddleOfTheMaze
    {
        public static void ExtractCLUT()
        {
            var mazeRtr = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Riddle of the Maze (R)\maze.rtr";

            var cdiMaze = new CdiFile(mazeRtr);

            var paletteSectors = new List<CdiSector>();

            foreach (var sector in cdiMaze.DataSectors)
            {
                var data = sector.GetSectorData();
                if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x00 && data[3] == 0x80)
                {
                    paletteSectors.Add(sector);
                }
            }
            var clut7Sectors = cdiMaze.VideoSectors.Where(x => x.Coding.VideoString == "CLUT7").OrderBy(s => s.SectorIndex).ToList();

            var imageSectors = new List<CdiSector>();

            foreach (var sector in clut7Sectors)
            {
                imageSectors.Add(sector);
                if (sector.SubMode.IsTrigger)
                {
                    var imageData = imageSectors.SelectMany(x => x.GetSectorData()).ToArray();
                    var index = imageSectors.First().SectorIndex;
                    var paletteSector = paletteSectors.FirstOrDefault(x => x.SectorIndex == index - 1);
                    if (paletteSector == null)
                    {
                        paletteSector = paletteSectors.FirstOrDefault(x => x.SectorIndex == index - 2);
                    }
                    var paletteData = paletteSector.GetSectorData().Skip(4).Take(0x180).ToArray();
                    var palette = ConvertBytesToRGB(paletteData);
                    var image = imageSectors.Count > 40 ? GenerateClutImage(palette, imageData, 488, 322)
                      : GenerateClutImage(palette, imageData, 384, 240);
                    image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Riddle of the Maze (R)\Asset Extraction\CLUT7\output\{index}.png", ImageFormat.Png);
                    imageSectors.Clear();
                }
            }

        }
    }
}
