using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ExtractCLUT.Games.PC.CrazyDrake
{
    public class LibFile(string path)
    {
        private const string LIB_MAGIC = "LIB ";
        private const string PAL_MAGIC = "PAL ";
        private const string BMPH_MAGIC = "BMPH";
        private const string BMP_MAGIC = "BMP ";
        public string Path { get; init; } = path;
        public string Filename => System.IO.Path.GetFileName(Path);
        public List<Image<Rgba32>> Images { get; set; } = [];
        public List<Color> Palette { get; set; } = [];
        public AnmFile? Anm { get; set; } = null;

        public int ParseLib()
        {
            using var libReader = new BinaryReader(File.OpenRead(Path));
            var magic = Encoding.ASCII.GetString(libReader.ReadBytes(4));
            if (magic != LIB_MAGIC)
                throw new InvalidDataException($"Invalid LIB file magic: {magic}");

            libReader.BaseStream.Seek(0x19, SeekOrigin.Begin);
            var imageCount = libReader.ReadUInt32();

            var palMagic = Encoding.ASCII.GetString(libReader.ReadBytes(4));
            if (palMagic != PAL_MAGIC)
                throw new InvalidDataException($"Invalid LIB file palette magic: {palMagic}");
            var palSize = libReader.ReadUInt32();
            // Read palette data, each color is 3 bytes (RGB) but in VGA 6-bit format, so we need to convert to 8-bit
            for (int i = 0; i < palSize/3; i++)
            {
                var r = (byte)(libReader.ReadByte() * 255 / 63);
                var g = (byte)(libReader.ReadByte() * 255 / 63);
                var b = (byte)(libReader.ReadByte() * 255 / 63);
                Palette.Add(new Rgba32(r, g, b));
            }
            
            // Read each image header and data
            for (int i = 0; i < imageCount; i++)
            {
                var bmphMagic = Encoding.ASCII.GetString(libReader.ReadBytes(4));
                if (bmphMagic != BMPH_MAGIC)
                    throw new InvalidDataException($"Invalid LIB file BMPH magic: {bmphMagic}");
                var headerSize = libReader.ReadUInt32(); // should be 0x0C
                var width = libReader.ReadUInt32();
                var height = libReader.ReadUInt32();
                libReader.ReadBytes((int)(headerSize - 8)); // skip any remaining header bytes
                var bmpMagic = Encoding.ASCII.GetString(libReader.ReadBytes(4));
                if (bmpMagic != BMP_MAGIC)
                    throw new InvalidDataException($"Invalid LIB file BMP magic: {bmpMagic}");
                var imageDataSize = libReader.ReadUInt32();
                var imageData = libReader.ReadBytes((int)imageDataSize);
                
                var image = ImageFormatHelper.GenerateIMClutImage(Palette, imageData, (int)width, (int)height, true);
                Images.Add(image);
            }

            return Images.Count;
        }
    
        public int LoadAnm()
        {
            var anmPath = System.IO.Path.ChangeExtension(Path, ".ANM");
            Anm = new AnmFile(anmPath);
            var frames = Anm.ParseFrames();
            return frames.Count;
        }

        public void SaveImages(string outputDir)
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            for (int i = 0; i < Images.Count; i++)
            {
                var image = Images[i];
                var outputPath = System.IO.Path.Combine(outputDir, $"{System.IO.Path.GetFileNameWithoutExtension(Filename)}_{i:D4}.png");
                image.SaveAsPng(outputPath);
            }
        }

        public void SaveAlignedImages(string outputDir)
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            if (Anm == null)
                throw new InvalidOperationException("ANM file not loaded. Call LoadAnm() first.");

            // Calculate the maximum width and height for each animation based on the frames
            for (int animIndex = 0; animIndex < Anm.Frames.Count; animIndex++)
            {
                var frames = Anm.Frames[animIndex];
                int maxWidth = 0;
                int maxHeight = 0;
                int minWidth = int.MaxValue;
                int minHeight = int.MaxValue;

                foreach (var frame in frames)
                {
                    var image = Images[(int)frame.FrameIndex];
                    maxWidth = Math.Max(maxWidth, frame.X + image.Width);
                    maxHeight = Math.Max(maxHeight, frame.Y + image.Height);
                    minWidth = Math.Min(minWidth, frame.X);
                    minHeight = Math.Min(minHeight, frame.Y);
                }

                foreach (var frame in frames)
                {
                    var image = Images[(int)frame.FrameIndex];
                    var alignedImage = new Image<Rgba32>(maxWidth - minWidth, maxHeight - minHeight);
                    alignedImage.Mutate(ctx => ctx.DrawImage(image, new Point(frame.X - minWidth, frame.Y - minHeight), 1f));
                    // Save the aligned image
                    var outputPath = System.IO.Path.Combine(outputDir, $"{System.IO.Path.GetFileNameWithoutExtension(Filename)}_anim{animIndex:D4}_frame{frame.FrameIndex:D4}.png");
                    alignedImage.SaveAsPng(outputPath);
                }

            }
        }

        public void SavePalette(string outputDir)
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var paletteImage = new Image<Rgba32>(Palette.Count, 1);
            for (int i = 0; i < Palette.Count; i++)
            {
                paletteImage[i, 0] = Palette[i];
            }
            var outputPath = System.IO.Path.Combine(outputDir, $"{System.IO.Path.GetFileNameWithoutExtension(Filename)}_palette.png");
            paletteImage.SaveAsPng(outputPath);
        }
    }
}
