using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ExtractCLUT.Games.PC.MoonChild
{
    public static class MoonChild
    {
        public static Image<Rgba32> CreateMoonChildMap(string mapFile, int widthInTiles, int heightInTiles, string tilesetPath)
        {
            // tilesetPath is the path to the png image containing all the tiles (32x32 pixels each) for MoonChild
            var tilesetImage = Image.Load<Rgba32>(tilesetPath);
            var mapImage = new Image<Rgba32>(widthInTiles * 32, heightInTiles * 32);

            using var reader = new BinaryReader(File.OpenRead(mapFile));

            // Map file contains tile indices in row-major order, each index is a 16-bit unsigned integer
            for (int y = 0; y < heightInTiles; y++)
            {
                for (int x = 0; x < widthInTiles; x++)
                {
                    ushort tileIndex = reader.ReadUInt16();
                    int tilesetX = (tileIndex % (tilesetImage.Width / 32)) * 32;
                    int tilesetY = (tileIndex / (tilesetImage.Width / 32)) * 32;

                    // Copy the tile from the tileset to the map image
                    var tile = tilesetImage.Clone(ctx => ctx.Crop(new Rectangle(tilesetX, tilesetY, 32, 32)));
                    mapImage.Mutate(ctx => ctx.DrawImage(tile, new Point(x * 32, y * 32), new GraphicsOptions()));
                }
            }

            return mapImage;
        }
    }
}
