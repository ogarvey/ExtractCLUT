using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
namespace ExtractCLUT.Games.PC.Quarantine
{
    public class SprFile
    {
        public string FilePath { get; set; }
        public byte SpriteCount { get; set; }
        public List<(byte width, byte height, byte[] data)> Sprites { get; set; }

        public SprFile(string filePath)
        {
            FilePath = filePath;
            Sprites = new List<(byte width, byte height, byte[] data)>();
            SpriteCount = 0;
        }

        public void Parse()
        {
            using (var stream = new System.IO.FileStream(FilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            using (var reader = new System.IO.BinaryReader(stream))
            {
                // Read the sprite count (1 byte)
                SpriteCount = reader.ReadByte();
                var widthAndHeightList = new List<(byte width, byte height)>();
                // Read each sprite
                for (int i = 0; i < SpriteCount; i++)
                {
                    // Read width and height (1 byte each)
                    byte width = reader.ReadByte();
                    byte height = reader.ReadByte();
                    widthAndHeightList.Add((width, height));
                }

                // Read sprite data
                foreach (var (width, height) in widthAndHeightList)
                {
                    int spriteSize = width * height; // Assuming 1 byte per pixel
                    byte[] spriteData = reader.ReadBytes(spriteSize);
                    Sprites.Add((width, height, spriteData));
                }
            }
        }

        public void SaveSpritesRaw(string outputDirectory)
        {
            if (!System.IO.Directory.Exists(outputDirectory))
            {
                System.IO.Directory.CreateDirectory(outputDirectory);
            }

            for (int i = 0; i < Sprites.Count; i++)
            {
                string spriteFilePath = System.IO.Path.Combine(outputDirectory, $"sprite_{i}.bin");
                System.IO.File.WriteAllBytes(spriteFilePath, Sprites[i].data);
            }
        }

        public void SaveSpritesAsPng(string outputDirectory, List<Color> palette)
        {
            if (!System.IO.Directory.Exists(outputDirectory))
            {
                System.IO.Directory.CreateDirectory(outputDirectory);
            }

            for (int i = 0; i < Sprites.Count; i++)
            {
                var (width, height, data) = Sprites[i];
                
                var image = ImageFormatHelper.GenerateIMClutImage(palette, data, width, height, true);
                string spriteFilePath = System.IO.Path.Combine(outputDirectory, $"sprite_{i}.png");
                image.SaveAsPng(spriteFilePath);
            }
        }
    }
}
