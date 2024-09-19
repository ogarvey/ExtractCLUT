using Pfim;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC
{
    public static class DDSHelper
    {
        public static void ConvertDDSImageToPNG(string ddsInput, string ddsOutput, bool splitChannels = false, bool invertRed = false, bool invertGreen = false, bool invertBlue = false)
        {
            using (var ddsImage = Pfimage.FromFile(ddsInput))
            {
                if (ddsImage.Compressed)
                {
                    ddsImage.Decompress();
                }

                // Get width, height, and stride
                int width = ddsImage.Width;
                int height = ddsImage.Height;
                int stride = width * (ddsImage.BitsPerPixel / 8);

                // Extract color channels
                using (var originalImage = new Image<Rgba32>(width, height))
                using (var rChannelImage = new Image<L8>(width, height))
                using (var gChannelImage = new Image<L8>(width, height))
                using (var bChannelImage = new Image<L8>(width, height))
                using (var aChannelImage = new Image<L8>(width, height))
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int pixelIndex = y * stride + x * (ddsImage.BitsPerPixel / 8);
                            byte r = ddsImage.Data[pixelIndex + 2];
                            byte g = ddsImage.Data[pixelIndex + 1];
                            byte b = ddsImage.Data[pixelIndex];
                            byte a = ddsImage.BitsPerPixel == 32 ? ddsImage.Data[pixelIndex + 3] : (byte)255;
                            // Invert color channels based on parameters
                            if (invertRed) r = (byte)(255 - r);
                            if (invertGreen) g = (byte)(255 - g);
                            if (invertBlue) b = (byte)(255 - b);
                            originalImage[x, y] = new Rgba32(r, g, b, a);
                            rChannelImage[x, y] = new L8(r);
                            gChannelImage[x, y] = new L8(g);
                            bChannelImage[x, y] = new L8(b);
                            aChannelImage[x, y] = new L8(a);
                        }
                    }
                    // Save each channel as a PNG file
                    if (splitChannels) rChannelImage.SaveAsPng(Path.Combine(ddsOutput, $"{Path.GetFileNameWithoutExtension(ddsInput)}{(invertRed ? "_inverted_" : "")}_red_channel.png"));
                    if (splitChannels) gChannelImage.SaveAsPng(Path.Combine(ddsOutput, $"{Path.GetFileNameWithoutExtension(ddsInput)}{(invertGreen ? "_inverted_" : "")}_green_channel.png"));
                    if (splitChannels) bChannelImage.SaveAsPng(Path.Combine(ddsOutput, $"{Path.GetFileNameWithoutExtension(ddsInput)}{(invertBlue ? "_inverted_" : "")}_blue_channel.png"));
                    if (splitChannels) aChannelImage.SaveAsPng(Path.Combine(ddsOutput, $"{Path.GetFileNameWithoutExtension(ddsInput)}_alpha_channel.png"));

                    // Save the original image as a PNG file
                    originalImage.SaveAsPng(Path.Combine(ddsOutput, $"{Path.GetFileNameWithoutExtension(ddsInput)}.png"));
                }
            }
        }

        public static void ExtractDDSImages(string inputFilePath, string outputDirectory, List<string> filenames)
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            byte[] ddsHeader = { 0x44, 0x44, 0x53, 0x20 }; // 'DDS ' in ASCII
            int headerSize = 4;

            using (FileStream fs = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    long fileLength = reader.BaseStream.Length;
                    int imageIndex = 0;

                    while (reader.BaseStream.Position < fileLength)
                    {
                        // Search for DDS header
                        byte[] buffer = reader.ReadBytes(headerSize);
                        if (buffer.Length < headerSize)
                            break;

                        if (IsMatch(buffer, ddsHeader))
                        {
                            // Read DDS header (DDS_HEADER structure size is 124 bytes)
                            byte[] ddsHeaderBuffer = new byte[124];
                            reader.Read(ddsHeaderBuffer, 0, 124);

                            // Calculate DDS image size based on header information
                            int height = BitConverter.ToInt32(ddsHeaderBuffer, 8);
                            int width = BitConverter.ToInt32(ddsHeaderBuffer, 12);
                            int mipMapCount = BitConverter.ToInt32(ddsHeaderBuffer, 28);
                            int fourCC = BitConverter.ToInt32(ddsHeaderBuffer, 80);

                            // Assuming a common format like DXT1, DXT3, or DXT5
                            int blockSize = (fourCC == 0x31545844) ? 8 : 16; // DXT1 (8 bytes), DXT3/DXT5 (16 bytes)
                            int dataSize = ((width + 3) / 4) * ((height + 3) / 4) * blockSize;

                            // Read DDS image data
                            byte[] ddsData = new byte[headerSize + 124 + dataSize];
                            Array.Copy(buffer, 0, ddsData, 0, headerSize);
                            Array.Copy(ddsHeaderBuffer, 0, ddsData, headerSize, 124);
                            reader.Read(ddsData, headerSize + 124, dataSize);

                            // Save DDS image
                            string outputFilePath = Path.Combine(outputDirectory, $"extracted_{imageIndex}_{filenames[imageIndex]}.dds");
                            File.WriteAllBytes(outputFilePath, ddsData);

                            Console.WriteLine($"Extracted DDS image {imageIndex} to {outputFilePath}");
                            imageIndex++;
                        }
                        else
                        {
                            // Move one byte forward and continue searching
                            reader.BaseStream.Position -= (headerSize - 1);
                        }
                    }
                }
            }
            Console.WriteLine("Extraction completed.");
        }

        static bool IsMatch(byte[] buffer, byte[] pattern)
        {
            if (buffer.Length < pattern.Length)
                return false;

            for (int i = 0; i < pattern.Length; i++)
            {
                if (buffer[i] != pattern[i])
                    return false;
            }

            return true;
        }
    }
}
