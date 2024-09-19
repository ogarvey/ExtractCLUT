using System.Drawing.Imaging;
using ExtractCLUT.Helpers;
using OGLibCDi.Models;
using static ExtractCLUT.Helpers.ColorHelper;
using static ExtractCLUT.Helpers.ImageFormatHelper;

namespace ExtractCLUT.Games
{
    public static class AlienGate
    {
        public static void ExtractSprites(string inputFolder, string extension)
        {
            var files = Directory.GetFiles(inputFolder, extension);
           
            var outputFolder = $@"{inputFolder}\Spriting";
            Directory.CreateDirectory(outputFolder);

            var defaultPalette = GenerateColors(256);

            foreach (var file in files)
            {
                var cdiFile = new CdiFile(file);
                var dataSectors = cdiFile.DataSectors.OrderBy(s => s.Channel).ThenBy(s => s.SectorIndex).ToList();

                var data = dataSectors.SelectMany(s => s.GetSectorData()).ToArray();
                var blobs = FileHelpers.ExtractSpriteByteSequences(null, data, [0x10, 0xfc], [0x4e, 0x75]);

                blobs.AddRange(FileHelpers.ExtractSpriteByteSequences(null, data, [0x20, 0xfc], [0x4e, 0x75]));

                blobs.AddRange(FileHelpers.ExtractSpriteByteSequences(null, data, [0x31, 0x7c], [0x4e, 0x75]));
                blobs.AddRange(FileHelpers.ExtractSpriteByteSequences(null, data, [0x11, 0x7c], [0x4e, 0x75]));

                var fileName = Path.GetFileNameWithoutExtension(file);
              
                var outputDir = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(file));
                Directory.CreateDirectory(outputDir);
                var blobDir = Path.Combine(outputDir, "blobs");
                Directory.CreateDirectory(blobDir);

                foreach (var (blob, index) in blobs.WithIndex())
                {
                    var decodedBlob = CompiledSpriteHelper.DecodeCompiledSprite(blob, 0, 0x240);
                    // File.WriteAllBytes(Path.Combine(blobDir, $"{index}.bin"), blob);
                    // File.WriteAllBytes(Path.Combine(blobDir, $"{index}_decoded.bin"), decodedBlob);
                    var image = ImageFormatHelper.GenerateClutImage(defaultPalette, decodedBlob, 384, 240, true);
                    var outputName = Path.Combine(outputDir, $"{index}.png");
                    if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                    {
                        image.Save(outputName, ImageFormat.Png);
                    }
                }
            }

        }
    }
}
