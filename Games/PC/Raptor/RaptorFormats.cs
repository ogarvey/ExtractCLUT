using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ExtractCLUT.Games.PC.Raptor
{
    public static class RaptorFormats
    {
        public static Image<Rgba32> CreateRaptorMap(string mapFile, string tilesetsPath)
        {
            const int mapWidthInTiles = 9;
            const int mapHeightInTiles = 150;

            // 1. Pre-load all tile images from the tileset folders
            var tilesets = new List<List<Image<Rgba32>>>();
            for (int i = 0; i < 4; i++) // Assuming 4 tilesets
            {
                var tilesetFolder = Path.Combine(tilesetsPath, i.ToString());
                if (!Directory.Exists(tilesetFolder))
                {
                    throw new DirectoryNotFoundException($"Tileset folder not found: {tilesetFolder}");
                }

                var tileFiles = Directory.GetFiles(tilesetFolder, "*.png")
                                         .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f)))
                                         .ToList();

                var tileImages = tileFiles.Select(Image.Load<Rgba32>).ToList();
                tilesets.Add(tileImages);
            }

            if (tilesets.All(ts => ts.Count == 0))
            {
                throw new FileNotFoundException("No tile images found in any tileset folder.");
            }

            // Determine tile dimensions from the first available tile
            var firstTile = tilesets.SelectMany(t => t).First();
            int tileWidth = firstTile.Width;
            int tileHeight = firstTile.Height;

            // 2. Create the final canvas
            int finalWidth = mapWidthInTiles * tileWidth;
            int finalHeight = mapHeightInTiles * tileHeight;
            var mapImage = new Image<Rgba32>(finalWidth, finalHeight);

            // 3. Read the map file and draw tiles
            using var reader = new BinaryReader(File.OpenRead(mapFile));
            
            reader.BaseStream.Seek(12, SeekOrigin.Begin); // Seek to the start of tile data

            for (int i = 0; i < mapWidthInTiles * mapHeightInTiles; i++)
            {
                uint tileValue = reader.ReadUInt32();
                ushort tileNumber = (ushort)(tileValue & 0xFFFF);
                ushort tilesetNumber = (ushort)(tileValue >> 16);

                if (tilesetNumber >= tilesets.Count || tileNumber >= tilesets[tilesetNumber].Count)
                {
                    continue; // Skip if tile or tileset index is out of bounds
                }

                var tileImage = tilesets[tilesetNumber][tileNumber];

                int x = i % mapWidthInTiles;
                int y = i / mapWidthInTiles;

                var location = new Point(x * tileWidth, y * tileHeight);

                mapImage.Mutate(ctx => ctx.DrawImage(tileImage, location, 1f));
            }

            // Clean up loaded tile images
            foreach (var image in tilesets.SelectMany(t => t))
            {
                image.Dispose();
            }

            return mapImage;
        }
    }
}
