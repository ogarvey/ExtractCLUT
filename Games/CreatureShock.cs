using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using OGLibCDi.Models;

namespace ExtractCLUT.Games
{
    public static class CreatureShock
    {
        public static List<Image> ExtractSprites(byte[] data, List<Color> palette, string outputDir = null)
        {
            var imageList = new List<Image>();
            var offsets = FileHelpers.FindSequenceOffsets(data, [0x24, 0x40, 0x95, 0xfc]);

            var blobs = new List<byte[]>();

            for (int i = 0; i < offsets.Count; i++)
            {
                var start = offsets[i];
                var end = i == offsets.Count - 1 ? data.Length : offsets[i + 1];
                var blob = data.Skip((int)start).Take((int)(end - start)).ToArray();
                blobs.Add(blob);
            }

            foreach (var (blob, index) in blobs.WithIndex())
            {
                if (index % 2 != 0)
                {
                    continue;
                }
                var blobData = blob.Skip(4).ToArray();
                var output = CompiledSpriteHelper.DecodeCompiledSprite(blobData, 0, 0x180);
                if (outputDir != null)
                {
                    File.WriteAllBytes(Path.Combine(outputDir, $"{index}.bin"), output);
                }
                var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true).CropTransparentEdges();
                imageList.Add(image);
            }
            return imageList;
        }

        public static void ExtractData(string inputDir, bool saveBlobs = false)
        {
            var mainOutputFolder = Path.Combine(inputDir, "Extracted");
            Directory.CreateDirectory(mainOutputFolder);

            var aniFiles = Directory.GetFiles(inputDir, "*.ani");
            var palFiles = Directory.GetFiles(inputDir, "*.pal");
            var binFiles = Directory.GetFiles(inputDir, "*.bin");
            var cl7Files = Directory.GetFiles(inputDir, "*.cl7");

            var palDictionary = new Dictionary<string, List<Color>>();
            var palOutputDir = Path.Combine(mainOutputFolder, "Palettes");
            Directory.CreateDirectory(palOutputDir);
            foreach (var file in cl7Files)
            {
                var cdiFile = new CdiFile(file);
                var data = cdiFile.DataSectors.OrderBy(s => s.Channel).ThenBy(s => s.SectorIndex).SelectMany(s => s.GetSectorData()).ToArray();
                var output = ImageFormatHelper.ExtractPaletteAndImageBytes(null, data);
                var palette = ColorHelper.ConvertBytesToRGB(output.palette);
                var image = ImageFormatHelper.GenerateClutImage(palette, output.image, 480, 240);
                image.Save(Path.Combine(mainOutputFolder, Path.GetFileNameWithoutExtension(file) + ".png"));
            }
            foreach (var palFile in palFiles)
            {
                var palData = new CdiFile(palFile).DataSectors.SelectMany(x => x.GetSectorData()).ToArray();
                if (saveBlobs)
                {
                    File.WriteAllBytes(Path.Combine(palOutputDir, Path.GetFileName(palFile)), palData);
                }
                var palette = ColorHelper.ConvertBytesToRGB(palData.Take(0x180).ToArray());
                var paletteOutputImageName = Path.Combine(palOutputDir, Path.GetFileNameWithoutExtension(palFile) + ".png");
                ColorHelper.WritePalette(paletteOutputImageName, palette);
                palDictionary.Add(Path.GetFileNameWithoutExtension(palFile), palette);
            }

            // foreach (var aniFile in aniFiles)
            // {
            //     var outputDir = Path.Combine(mainOutputFolder, Path.GetFileNameWithoutExtension(aniFile));
            //     Directory.CreateDirectory(outputDir);
            //     var aniData = new CdiFile(aniFile).DataSectors.SelectMany(x => x.GetSectorData()).ToArray();
            //     var paletteName = Path.GetFileNameWithoutExtension(aniFile).Substring(0, 4);
            //     var palette = palDictionary[paletteName];
            //     var sprites = ExtractSprites(aniData, palette);
            //     for (int i = 0; i < sprites.Count; i++)
            //     {
            //         sprites[i].Save(Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(aniFile)}_{i}.png"));
            //     }
            // }

            foreach(var binFile in binFiles)
            {
                var outputDir = Path.Combine(mainOutputFolder, Path.GetFileNameWithoutExtension(binFile));
                Directory.CreateDirectory(outputDir);
                var binData = new CdiFile(binFile).DataSectors.SelectMany(x => x.GetSectorData()).ToArray();
                var paletteName = Path.GetFileNameWithoutExtension(binFile).Substring(0, 3);
                var palette = palDictionary.Where(x => x.Key.Contains(paletteName)).FirstOrDefault().Value;
                var blobFolder = saveBlobs ? Path.Combine(outputDir, "blobs") : null;
                if (saveBlobs) Directory.CreateDirectory(blobFolder);
                var sprites = ExtractSprites(binData, palette, blobFolder);

                for (int i = 0; i < sprites.Count; i++)
                {
                    sprites[i].Save(Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(binFile)}_{i}.png"));
                }
            }
        }

    }
}
