using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Helpers
{
    public static class TiledExportHelper
    {
        private static string _tilesetTemplate = """

        """;

        private static string _tilemapTemplate = """

        """;
        public static void CreateTilesetFile()
        {

        }

        public static void CreateTilemapFile()
        {

        }

        public static string GenerateTileMapString(byte[] data, int width, int height)
        {
            var sb = new StringBuilder();

            // Assuming each tile index takes 2 bytes and the array is stored in row-major order.
            // Calculate number of tiles based on data length
            if (data.Length != width * height * 2)
            {
                throw new ArgumentException($"Data length does not match expected size for a {width}x{height} map with 2 bytes per tile.");
            }

            // Treat each 2 bytes as a tile index
            // Create a string holding the tile indices
            // Create new line after each row of tiles using the width as the step
            for (int i = 0; i < data.Length; i += 2)
            {
                var tileIndex = BitConverter.ToUInt16(data.Skip(i).Take(2).Reverse().ToArray(), 0);
                sb.Append($"{(tileIndex + 1).ToString()},");
                if ((i + 2) % (width * 2) == 0)
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString().Trim().TrimEnd(',');
        }
    }
}
