using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;

namespace ExtractCLUT.Games
{
    public static class SuperMariosWackyWorlds
    {
        public static List<byte[]> GetScreenTiles(byte[] data)
        {
            var tiles = new List<byte[]>();
            for (int i = 0; i < data.Length; i += 0x100)
            {
                var tile = new byte[0x100];
                Array.Copy(data, i, tile, 0, 0x100);
                tiles.Add(tile);
            }
            return tiles;
        }

        public static Image CreateScreenImage(int width, int height, List<byte[]> _tiles, byte[] _mapData, List<Color> _colors)
        {
            var mapTiles = new List<byte[]>();
            
            for (int i = 0; i < _mapData.Length; i += 2)
            {
                int index = (_mapData[i] << 8) + _mapData[i + 1];
                byte[] tile = new byte[256];
                Array.Copy(_tiles[index], tile, 256);
                mapTiles.Add(tile);
            }

            var tempScreenBitmap = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int tileX = x / 16;
                    int tileY = y / 16;
                    int tileIndex = tileX + (tileY * (width / 16));
                    int tilePixelX = x % 16;
                    int tilePixelY = y % 16;
                    int tilePixelIndex = tilePixelX + (tilePixelY * 16);
                    int colorIndex = mapTiles[tileIndex][tilePixelIndex];
                    Color color = _colors[colorIndex % 128];
                    tempScreenBitmap.SetPixel(x, y, color);
                }
            }
            return tempScreenBitmap;
        }
    }
}

// var file = @"C:\Dev\Projects\Gaming\CD-i\MARIO\Output\Swamp3.JBR_0_0_0.bin";

// var data = File.ReadAllBytes(file);

// var widthBytes = new byte[] { data[0], data[1] };
// var heightBytes = new byte[] { data[2], data[3] };

// var widthInTiles = BitConverter.ToInt16(widthBytes.Reverse().ToArray(), 0);
// var heightInTiles = BitConverter.ToInt16(heightBytes.Reverse().ToArray(), 0);

// var width = widthInTiles * 16;
// var height = heightInTiles * 16;

// var screenByteCount = widthInTiles * heightInTiles * 2;
// var tileByteCountArray = new byte[] { 0x00, data[0x4], data[0x5], data[0x6] };
// var tileByteCount = BitConverter.ToInt32(tileByteCountArray.Reverse().ToArray(), 0);
// var paletteOffset = FindClutColorTableOffset(data, 2);
// var tilesOffset = paletteOffset - tileByteCount;

// var tileBytes = data.Skip(tilesOffset).Take(tileByteCount).ToArray();
// var screenBytes = data.Skip(0x7).Take(screenByteCount).ToArray();
// var paletteBytes = data.Skip(paletteOffset).Take(0x208).ToArray();

// var palette = ReadClutBankPalettes(paletteBytes, 2);

// var tiles = SuperMariosWackyWorlds.GetScreenTiles(tileBytes);

// foreach (var (tile, index) in tiles.WithIndex())
// {
//     var image = ImageFormatHelper.GenerateClutImage(palette, tile, 16, 16);
//     image.Save($@"C:\Dev\Projects\Gaming\CD-i\MARIO\Output\Swamp\tiles\Swamp3\Swamp3_{index}.png", ImageFormat.Png);
// }

// var screenArray = new ushort[widthInTiles * heightInTiles];
// var sIndex = 0;
// for (int i = 0; i < screenArray.Length; i++)
// {
//     var value = (ushort)((screenBytes[sIndex] << 8) + screenBytes[sIndex + 1]);
//     screenArray[i] = (ushort)(value + 1537);
//     sIndex += 2;
// }

// // write to a json file
// var json = JsonSerializer.Serialize(screenArray);
// File.WriteAllText(@"C:\Dev\Projects\Gaming\CD-i\MARIO\Output\Swamp\screens\Swamp3.json", json);
