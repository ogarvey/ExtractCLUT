using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using System.Text;

namespace ExtractCLUT.Games.PC.Quarantine
{
    public class ImgFile
    {
        public string FilePath { get; set; }
        public List<Color> Palette { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public ImgFile(string filePath)
        {
            FilePath = filePath;
            Palette = new List<Color>();
        }

        public void Parse()
        {
            using (var stream = new System.IO.FileStream(FilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            using (var reader = new System.IO.BinaryReader(stream))
            {
                // Magic `IMAGEX` (6 bytes)
                var magic = Encoding.ASCII.GetString(reader.ReadBytes(6));
                // Read width and height (2 bytes each)
                Width = reader.ReadUInt16();
                Height = reader.ReadUInt16();

                var unknown1 = reader.ReadUInt16(); // Unknown 1 (2 bytes)
                reader.ReadByte(); // padding?

                // Read palette colors
                for (int i = 0; i < 256; i++)
                {
                    byte r = reader.ReadByte();
                    byte g = reader.ReadByte();
                    byte b = reader.ReadByte();
                    Palette.Add(Color.FromRgb(r, g, b));
                }
            }
        }
    }
}
