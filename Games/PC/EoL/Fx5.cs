using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.EoL
{
    public class Fx5
    {
        private readonly uint _OffsetListOffset = 0x80;
        private readonly string _FilePath;
        public List<uint> Offsets { get; } = new List<uint>();
        public List<Image<Rgba32>> Images { get; } = new List<Image<Rgba32>>();

        public Fx5(string filePath, bool parseHeader = true)
        {
            _FilePath = filePath;
            if (parseHeader)
            {
                ParseHeader();
            }
        }

        public void ParseHeader()
        {
            using var br = new BinaryReader(File.OpenRead(_FilePath));
            br.BaseStream.Seek(0x40, SeekOrigin.Begin);
            var offsetListOffset = br.ReadUInt32();
            if (offsetListOffset != _OffsetListOffset)
            {
                throw new Exception($"Unexpected offset list offset: {offsetListOffset}");
            }
            var firstImageOffset = br.ReadUInt32();
            var imageCount = br.ReadUInt32();
            br.BaseStream.Seek(_OffsetListOffset, SeekOrigin.Begin);
            while (br.BaseStream.Position < firstImageOffset)
            {
                var offset = br.ReadUInt32();
                if (offset == 0)
                    break;
                Offsets.Add(offset);
            }
            if (Offsets.Count != imageCount)
            {
                throw new Exception($"Unexpected image count: {imageCount}, found {Offsets.Count} offsets");
            }
        }

        public void ParseImages(string palFilePath, bool isCompressed = true)
        {
            using var palBr = new BinaryReader(File.OpenRead(palFilePath));
            var palData = palBr.ReadBytes((int)palBr.BaseStream.Length);
            var palette = ColorHelper.ConvertBytesToRgbIS(palData, !palFilePath.Contains("MIN"));

            using var br = new BinaryReader(File.OpenRead(_FilePath));
            foreach (var (offset, index) in Offsets.WithIndex())
            {
                br.BaseStream.Seek(offset, SeekOrigin.Begin);
                var width = br.ReadUInt16();
                var height = br.ReadUInt16();
                var compressedDataSize = br.ReadUInt32();
                var compressedData = br.ReadBytes((int)compressedDataSize);

                if (isCompressed)
                {
                    var remainingBytesCount = Offsets.Count > index + 1 ? Offsets[index + 1] - (offset + 8 + compressedDataSize) : (uint)(br.BaseStream.Length - (offset + 8 + compressedDataSize));
                    var remainingBytes = br.ReadBytes((int)remainingBytesCount);
                    var decompressedData = DecompressImage(compressedData, remainingBytes, width, height);
                    var image = ImageFormatHelper.GenerateIMClutImage(palette, decompressedData, width, height, true, 0);
                    Images.Add(image);
                }
                else
                {
                    var image = ImageFormatHelper.GenerateIMClutImage(palette, compressedData, width, height, true, 0);
                    Images.Add(image);
                }
            }
        }

        public void SaveImages(string outputDirectory)
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
            for (int i = 0; i < Images.Count; i++)
            {
                var outputPath = Path.Combine(outputDirectory, $"image_{i:D4}.png");
                Images[i].Save(outputPath);
            }
        }

        public static byte[] DecompressImage(byte[] compressedData, byte[] lineOffsets, int width, int height)
        {
            var decompressedData = new List<byte>();

            using var dataReader = new BinaryReader(new MemoryStream(compressedData));

            for (int y = 0; y < height; y++)
            {
                var lineOffset = BitConverter.ToUInt32(lineOffsets, y * 4);
                var nextLineOffset = y < height - 1 ? BitConverter.ToUInt32(lineOffsets, (y + 1) * 4) : (uint)compressedData.Length;
                var lineDataSize = nextLineOffset - lineOffset;
                dataReader.BaseStream.Seek(lineOffset, SeekOrigin.Begin);
                var compressedLineData = dataReader.ReadBytes((int)lineDataSize);
                var lineData = new List<byte>();
                var lineDataReader = new BinaryReader(new MemoryStream(compressedLineData));
                while (lineDataReader.BaseStream.Position < lineDataReader.BaseStream.Length && lineData.Count < width)
                {
                    var transparentPixelCount = lineDataReader.ReadUInt16();
                    lineData.AddRange(Enumerable.Repeat((byte)0, Math.Min(width - lineData.Count, transparentPixelCount)));
                    if (lineData.Count >= width)
                        break;
                    var opaquePixelCount = lineDataReader.ReadUInt16();
                    lineData.AddRange(lineDataReader.ReadBytes(opaquePixelCount));
                }
                decompressedData.AddRange(lineData);
            }

            return [.. decompressedData];
        }
    }
}
