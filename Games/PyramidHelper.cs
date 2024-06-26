using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using ImageFormatHelper = ExtractCLUT.Helpers.ImageFormatHelper;
using static ExtractCLUT.Helpers.FileHelpers;
using OGLibCDi.Models;
using System.Drawing.Imaging;
using System.Drawing;

namespace ExtractCLUT.Games
{
    public static class PyramidHelper
    {
        public static void ExtractSprites(string inputFolder, bool existingData = false, List<Color> palette = null)
        {
            var file = Path.Combine(inputFolder, "pyrdata.rtr");

            var cdiFile = new CdiFile(file);

            var outputDir = Path.Combine(inputFolder, "Output", Path.GetFileNameWithoutExtension(file));
            Directory.CreateDirectory(outputDir);
            
            var dataSectors = cdiFile.DataSectors.OrderBy(s=> s.Channel).ThenBy(s => s.SectorIndex).ToList();

            var sectorList = new List<CdiSector>();

            foreach (var sector in dataSectors)
            {
                sectorList.Add(sector);
                if (sector.SubMode.IsEOR || sector == dataSectors.Last())
                {
                    var data = sectorList.SelectMany(x => x.GetSectorData()).ToArray();
                    var index = sectorList.First().SectorIndex;
                    var output = Path.Combine(outputDir, $"{sector.Channel}_{index}-{sector.SectorIndex}.bin");
                    File.WriteAllBytes(output, data);
                    sectorList.Clear();
                }
            }

            var files = Directory.GetFiles(outputDir, "*.bin").ToList();
            foreach (var dataFile in files)
            {
                var blobs = ExtractSpriteByteSequences(dataFile);

                var spriteOutputDir = Path.Combine(outputDir, "Sprites", Path.GetFileNameWithoutExtension(dataFile));
                //var blobOutputDir = Path.Combine(spriteOutputDir, "Blobs");

                Directory.CreateDirectory(spriteOutputDir);
                //Directory.CreateDirectory(blobOutputDir);
                foreach (var (blob, index) in blobs.WithIndex())
                {
                    // File.WriteAllBytes(Path.Combine(blobOutputDir, $"{index}.bin"), blob);
                    try
                    {
                        var decodedBlob = CompiledSpriteHelper.DecodeCompiledSprite(blob, 0x10);
                        // var output = Path.Combine(spriteOutputDir, $"{index}.bin");
                        // File.WriteAllBytes(output, decodedBlob);
                        if (palette != null)
                        {
                            var image = ImageFormatHelper.GenerateClutImage(palette, decodedBlob, 388, 240, true);
                            var imageOutputPath = Path.Combine(spriteOutputDir, "Images");
                            Directory.CreateDirectory(imageOutputPath);
                            var outputName = Path.Combine(imageOutputPath, $"{index}.png");
                            Rectangle cropRect = new Rectangle(0, 0, 72, 57);

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

                                croppedImage.Save(outputName, ImageFormat.Png);

                            }
                        }
                    }
                    catch (Exception)
                    {
                        //Console.WriteLine($"Error decoding sprite {index} in {file}");
                    }
                }
            }


        }
        public static List<byte[]> ExtractSpriteByteSequences(string filePath)
        {
            // Byte sequences to find
            byte[] startSequence = [0x4e, 0x56];
            byte[] endSequence = [0x4e, 0x75];

            // Read all bytes from the file
            byte[] fileContent = File.ReadAllBytes(filePath);

            List<byte[]> extractedSequences = new List<byte[]>();
            int currentIndex = 0;

            while (currentIndex < fileContent.Length)
            {
                // Find the start sequence
                int startIndex = FindSequence(fileContent, startSequence, currentIndex);

                if (startIndex == -1) // No more start sequences found
                    break;

                // Find the end sequence starting from where the start sequence was found
                int endIndex = FindSequence(fileContent, endSequence, startIndex);

                if (endIndex == -1) // No end sequence found after the start
                    break;

                // Calculate length to copy (including the end sequence)
                int length = endIndex - startIndex + endSequence.Length;

                // Copy the sequence from start to end (including the end sequence)
                byte[] extracted = new byte[length];
                Array.Copy(fileContent, startIndex, extracted, 0, length);
                extractedSequences.Add(extracted);

                // Move the current index to the byte after the current end sequence
                currentIndex = endIndex + endSequence.Length;
            }

            return extractedSequences;
        }
    }
}
