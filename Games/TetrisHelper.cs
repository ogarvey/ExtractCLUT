using System.Drawing;
using OGLibCDi.Enums;
using OGLibCDi.Helpers;
using OGLibCDi.Models;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;

namespace ExtractCLUT.Games
{
    public static class TetrisHelper
    {
        public static List<byte[]> GetPaletteAndDataBytes(CdiFile cdiFile)
        {
            var dataSectors = cdiFile.Sectors
                .Where(s => s.GetSectorType() == CdiSectorType.Data && s.SectorIndex < 203576)
                .OrderBy(s => s.SectorIndex).ToList();

            var byteList = new List<byte[]>();
            var dataBytes = new List<byte[]>();
            for (int i = 0; i < dataSectors.Count; i++)
            {
                var sector = dataSectors[i];
                var bytes = sector.GetSectorData();
                dataBytes.Add(bytes);
                if (sector.SubMode.IsEOR) {
                    byteList.Add(dataBytes.SelectMany(b => b).ToArray());
                    dataBytes.Clear();
                }
            }

            return byteList;
        }

        public static List<byte> GetSectorCounts(byte[] blob)
        {
            var sectorCounts = new List<byte>();
            for (int i=0; i<blob.Length; i++)
            {
                if (blob[i] == 0x00) return sectorCounts;
                sectorCounts.Add(blob[i]);
            }
            return sectorCounts;
        }

        public static void ExtractBackgroundAnimations(string rlvFile, string outputPath)
        {
            var cdiFile = new CdiFile(rlvFile);
            var rl7Sectors = cdiFile.VideoSectors.Where(x => x.Coding.VideoString == "RL7")
              .OrderBy(x => x.SectorIndex).ToList();

            var paletteAndData = GetPaletteAndDataBytes(cdiFile);

            var palettes = new List<List<Color>>();

            foreach (var (blob, index) in paletteAndData.WithIndex())
            {
                var blobImages = new List<Image>();
                var palette = index == 10 ?
                ColorHelper.ReadClutBankPalettes(blob.Skip(0x5a).ToArray(), 4) : ColorHelper.ReadClutBankPalettes(blob.Skip(0x5a).ToArray(), 2);
                palettes.Add(palette);
                var palettePath = @$"{outputPath}\palettes";
                if (!Directory.Exists(palettePath)) Directory.CreateDirectory(palettePath);
                ColorHelper.WritePalette(@$"{palettePath}\palette_{index}.png", palette);

                var sectorCounts = TetrisHelper.GetSectorCounts(blob.Skip(0x46a).ToArray());

                foreach (var sc in sectorCounts)
                {
                    var group = rl7Sectors.Take(sc).ToList();
                    var bytes = group.SelectMany(x => x.GetSectorData()).ToArray();
                    var image = ImageFormatHelper.GenerateRle7Image(palette, bytes, 384, 240, true);
                    blobImages.Add(image);
                    rl7Sectors.RemoveRange(0, sc);
                }
                var gifOutputPath = @$"{outputPath}\gifs";
                if (!Directory.Exists(gifOutputPath)) Directory.CreateDirectory(gifOutputPath);
                ImageFormatHelper.CreateGifFromImageList(blobImages, @$"{gifOutputPath}\gifs\output_{index}.gif", 10);
                blobImages.Clear();
            }

        }
    }
}
