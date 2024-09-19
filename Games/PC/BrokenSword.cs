using System.Drawing.Imaging;
using ExtractCLUT.Helpers;
public class spraOffset
{
  public int Length { get; set; }
  public int Width { get; set; }
  public int Height { get; set; }
}

public static class BrokenSword 
{
  public static void ExtractAll()
  {
    var inputDir = @"C:\Program Files (x86)\GOG Galaxy\Games\Broken Sword DC\Output";
    var mainOutputFolder = Path.Combine(inputDir, "Output");
    Directory.CreateDirectory(mainOutputFolder);

    var backgroundOutputFolder = Path.Combine(mainOutputFolder, "Backgrounds_New");
    Directory.CreateDirectory(backgroundOutputFolder);

    var bm16OutputFolder = Path.Combine(mainOutputFolder, "BM16_New");
    Directory.CreateDirectory(bm16OutputFolder);

    var faceOutputFolder = Path.Combine(mainOutputFolder, "Faces_New");
    Directory.CreateDirectory(faceOutputFolder);

    var spraOutputFolder = Path.Combine(mainOutputFolder, "SPRA_New");
    Directory.CreateDirectory(spraOutputFolder);

    var spr4OutputFolder = Path.Combine(mainOutputFolder, "SPR4_New");
    Directory.CreateDirectory(spr4OutputFolder);

    var spr8OutputFolder = Path.Combine(mainOutputFolder, "SPR8_New");
    Directory.CreateDirectory(spr8OutputFolder);

    var foreOutputFolder = Path.Combine(mainOutputFolder, "Fore_New");
    Directory.CreateDirectory(foreOutputFolder);

    var fg16OutputFolder = Path.Combine(mainOutputFolder, "FG16_New");
    Directory.CreateDirectory(fg16OutputFolder);

    var files = Directory.GetFiles(inputDir, "*.*");
    foreach (var file in files)
    {

      string infile_name = file;
      using (FileStream infile = File.OpenRead(infile_name))
      using (BinaryReader reader = new BinaryReader(infile))
      {
        string magic = new string(reader.ReadChars(6));

        if (magic.StartsWith("BACKG"))
        {
          continue;
          reader.BaseStream.Seek(5, SeekOrigin.Begin);
          ushort width = reader.ReadUInt16();
          ushort height = reader.ReadUInt16();
          byte colors = reader.ReadByte();
          Console.WriteLine($"Background, palette with {colors} colors, {width}x{height}");

          var palette = new ushort[colors];
          for (int i = 0; i < colors; i++)
          {
            palette[i] = BitConverter.ToUInt16(reader.ReadBytes(2));
          }
          var imageData = reader.ReadBytes(width * height);
          var paletteColors = ColorHelper.Read16BitRgbPalette(palette);
          var image = ImageFormatHelper.GenerateClutImage(paletteColors, imageData, width, height);
          image.Save(Path.Combine(backgroundOutputFolder, Path.GetFileNameWithoutExtension(file) + ".png"), ImageFormat.Png);
        }
        else if (magic.StartsWith("FACE8"))
        {
          continue;
          reader.BaseStream.Seek(8, SeekOrigin.Begin);
          ushort width = 128;
          ushort height = 192;

          var palette = new ushort[250];
          for (int i = 0; i < 250; i++)
          {
            palette[i] = BitConverter.ToUInt16(reader.ReadBytes(2));
          }
          var paletteColors = ColorHelper.Read16BitRgbPalette(palette);
          reader.BaseStream.Seek(520, SeekOrigin.Begin);
          for (int i = 0; i < 3; i++)
          {
            var imageData = reader.ReadBytes(width * height);
            var image = ImageFormatHelper.GenerateClutImage(paletteColors, imageData, width, height);
            image.Save(Path.Combine(faceOutputFolder, Path.GetFileNameWithoutExtension(file) + $"_{i}.png"), ImageFormat.Png);
          }
        }
        else if (magic.StartsWith("BM16"))
        {
          continue;
          reader.BaseStream.Seek(4, SeekOrigin.Begin);
          ushort width = reader.ReadUInt16();
          ushort height = reader.ReadUInt16();

          Console.WriteLine($"BG, 16bits, {width}x{height}");

          byte[] bitmap = reader.ReadBytes(width * height * 2);
          var image = ImageFormatHelper.DecodeRgb16(bitmap, width, height);
          image.Save(Path.Combine(bm16OutputFolder, Path.GetFileNameWithoutExtension(file) + ".png"), ImageFormat.Png);
        }
        else if (magic.StartsWith("SPR4"))
        {
          var imageOutputFolder = Path.Combine(spr4OutputFolder, Path.GetFileNameWithoutExtension(file));
          Directory.CreateDirectory(imageOutputFolder);
          reader.BaseStream.Seek(6, SeekOrigin.Begin);
          var count = reader.ReadByte();
          reader.BaseStream.Seek(8, SeekOrigin.Begin);
          var palette = new ushort[16];
          for (int i = 0; i < 16; i++)
          {
            palette[i] = BitConverter.ToUInt16(reader.ReadBytes(2));
          }
          var paletteColors = ColorHelper.Read16BitRgbPalette(palette);
          var offsets = new List<spraOffset>();
          for (int i = 0; i < count; i++)
          {
            var offset = new spraOffset();
            offset.Width = reader.ReadUInt16();
            offset.Height = reader.ReadUInt16();
            if (offset.Width % 2 != 0)
            {
              offset.Width++;
            }
            reader.BaseStream.Seek(4, SeekOrigin.Current);
            offset.Length = reader.ReadInt32();
            offsets.Add(offset);
          }

          for (int i = 0; i < count; i++)
          {
            var offset = offsets[i];
            if (offset.Width == 0 || offset.Height == 0)
            {
              continue;
            }
            var nextOffset = i == count - 1 ? (int)reader.BaseStream.Length : offsets[i + 1].Length;
            var length = nextOffset - offset.Length;
            
            var size = offset.Width * offset.Height >> 1;
            for (int j = 0; j < length / size; j++)
            {
              var imageData = reader.ReadBytes(size);
              var image = ImageFormatHelper.GenerateClutImage(paletteColors, imageData, offset.Width/2, offset.Height, true);
              image.Save(Path.Combine(imageOutputFolder, Path.GetFileNameWithoutExtension(file) + $"_{i}_{j}.png"), ImageFormat.Png);
            }
          }
        }
        else if (magic.StartsWith("SPR8"))
        {
          continue;
          var imageOutputFolder = Path.Combine(spr8OutputFolder, Path.GetFileNameWithoutExtension(file));
          Directory.CreateDirectory(imageOutputFolder);
          reader.BaseStream.Seek(6, SeekOrigin.Begin);
          var count = reader.ReadByte();
          reader.BaseStream.Seek(8, SeekOrigin.Begin);
          var palette = new ushort[256];
          for (int i = 0; i < 256; i++)
          {
            palette[i] = BitConverter.ToUInt16(reader.ReadBytes(2));
          }
          var paletteColors = ColorHelper.Read16BitRgbPalette(palette);
          var offsets = new List<spraOffset>();
          for (int i = 0; i < count; i++)
          {
            var offset = new spraOffset();
            offset.Width = reader.ReadUInt16();
            offset.Height = reader.ReadUInt16();
            if (offset.Width % 2 != 0)
            {
              offset.Width++;
            }
            reader.BaseStream.Seek(8, SeekOrigin.Current);
            offset.Length = 0;
            offsets.Add(offset);
          }
          
          for (int i = 0; i < count; i++)
          {
            var offset = offsets[i];
            if (offset.Width == 0 || offset.Height == 0)
            {
              continue;
            }
            var size = offset.Width * offset.Height;
            var imageData = reader.ReadBytes(size);
            var image = ImageFormatHelper.GenerateClutImage(paletteColors, imageData, offset.Width, offset.Height, true);
            image.Save(Path.Combine(imageOutputFolder, Path.GetFileNameWithoutExtension(file) + $"_{i}.png"), ImageFormat.Png);
          }
        }
        else if (magic.StartsWith("SPRA"))
        {
          continue;
          var imageOutputFolder = Path.Combine(spraOutputFolder, Path.GetFileNameWithoutExtension(file));
          Directory.CreateDirectory(imageOutputFolder);
          reader.BaseStream.Seek(6, SeekOrigin.Begin);
          ushort count = reader.ReadUInt16();
          var palettes = new List<List<System.Drawing.Color>>();
          for (int i = 0; i < count; i++)
          {
            // take 512 bytes, and convert with Read16BitRgbPalette
            var palette = new ushort[256];
            for (int j = 0; j < 256; j++)
            {
              palette[j] = BitConverter.ToUInt16(reader.ReadBytes(2));
            }
            var paletteColors = ColorHelper.Read16BitRgbPalette(palette);
            palettes.Add(paletteColors);
          }
          var offsets = new List<spraOffset>();
          for (int i = 0; i < count; i++)
          {
            var offset = new spraOffset();
            offset.Width = reader.ReadUInt16();
            offset.Height = reader.ReadUInt16();
            if (offset.Width % 2 != 0)
            {
              offset.Width++;
            }
            reader.BaseStream.Seek(4, SeekOrigin.Current);
            offset.Length = reader.ReadInt32();
            offsets.Add(offset);
          }
          for (int i = 0; i < count; i++)
          {
            var offset = offsets[i];
            if (offset.Width == 0 || offset.Height == 0)
            {
              continue;
            }
            var nextOffset = i == count - 1 ? (int)reader.BaseStream.Length : offsets[i + 1].Length;
            var length = nextOffset - offset.Length;
            var palette = palettes[i];
            var size = offset.Width * offset.Height;
            for (int j = 0; j < length / size; j++)
            {
              var imageData = reader.ReadBytes(size);
              var image = ImageFormatHelper.GenerateClutImage(palette, imageData, offset.Width, offset.Height, true);
              image.Save(Path.Combine(imageOutputFolder, Path.GetFileNameWithoutExtension(file) + $"_{i}_{j}.png"), ImageFormat.Png);
            }
          }
        }
        else
        {
          Console.WriteLine("Unknown format");
        }
      }
    }
  }
}
