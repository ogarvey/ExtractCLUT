using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using static ExtractCLUT.Utils;
using static ExtractCLUT.Helpers.ColorHelper;
using Color = System.Drawing.Color;
using OGLibCDi.Models;
using static ExtractCLUT.Helpers.ImageFormatHelper;

public static class LotRSHelpers
{

  public static void ExtractRLV()
  {

    var inputFile = @"C:\Dev\Projects\Gaming\CD-i\Lords of the Rising Sun\RLV.rtf";

    var cdiFile = new CdiFile(inputFile);

    var dataSectors = cdiFile.Sectors.Where(x => x.SubMode.IsData).OrderBy(x => x.SectorIndex).ToList();

    var rl7Sectors = cdiFile.VideoSectors.Where(x => x.SubMode.IsVideo && x.Coding.VideoString == "RL7")
      .OrderBy(x => x.SectorIndex).ToList();

    var sectorCountLists = new List<List<int>>();
    var sectorList = new List<CdiSector>();
    var paletteList = new List<List<Color>>();

    foreach (var sector in dataSectors)
    {
      sectorList.Add(sector);
      if (sector.SubMode.IsEOR)
      {
        var bytes = sectorList.SelectMany(x => x.GetSectorData()).ToArray();
        var palette = ReadClutBankPalettes(bytes.Skip(90).ToArray(), 4);
        paletteList.Add(palette);
        //var byte46a = bytes[0x46a];
        var countBytes = bytes.Skip(0x46a).TakeWhile(x => x != 0x00).ToArray();
        var countList = new List<int>();
        for (var i = 0; i < countBytes.Length; i++)
        {
          countList.Add(countBytes[i]);
        }
        sectorList.Clear();
        sectorCountLists.Add(countList);
      }

    }

    var rl7Groups = new List<List<CdiSector>>();
    var rl7Group = new List<CdiSector>();
    foreach (var (sector, index) in rl7Sectors.WithIndex())
    {
      rl7Group.Add(sector);
      if (sector.SubMode.IsEOR)
      {
        rl7Groups.Add(rl7Group);
        rl7Group = new List<CdiSector>();
      }
    }

    foreach (var (group, index) in rl7Groups.WithIndex())
    {
      if (index < 68 || index > 92) continue;
      var groupCopy = group.ToList();
      var imageList = new List<Image>();
      var counts = sectorCountLists[index];
      var outputFolder = Path.Combine(@"C:\Dev\Projects\Gaming\CD-i\Lords of the Rising Sun\Asset Extraction\Working Dir\rl7", $"output_{index}");
      Directory.CreateDirectory(outputFolder);
      for (int i = 0; i < counts.Count; i++)
      {
        var imageData = groupCopy.Take(counts[i]).ToList().Select(x => x.GetSectorData()).SelectMany(x => x).ToArray();
        groupCopy = groupCopy.Skip(counts[i]).ToList();
        var palette = paletteList[index + 4];
        var image = GenerateRle7Image(palette, imageData, 384, 240, true);
        imageList.Add(image);
        image.Save(Path.Combine(outputFolder, $"rl7_{i}.png"), ImageFormat.Png);
      }
      var gifOutputPath = Path.Combine(outputFolder, $"rl7_{index}.gif");
      CreateGifFromImageList(imageList, gifOutputPath, 10);
      imageList.Clear();
    }
  }

}
