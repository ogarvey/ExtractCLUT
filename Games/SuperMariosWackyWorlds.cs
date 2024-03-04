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
