using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC
{
    public class Discworld
    {
        const int C16_240 = 0x4000; // Example value, replace with actual value
        const int C16_224 = 0x8000; // Example value, replace with actual value
        const int C16_MAP = 0xC000; // Example value, replace with actual value

        const int C16_FLAG_MASK = C16_240 | C16_224 | C16_MAP;

        public void ExtractAll()
        {
            var scnDir = @"C:\Dev\Projects\Gaming\VGR\PC\Discworld_DOS_EN_ISO-Version---First-release-Mar-93\Discworld_19950303_ISO\DISCWLD";

            var scnFiles = Directory.GetFiles(scnDir, "*.gra");

            foreach (var scnFile in scnFiles)
            {
                if (!scnFile.ToLower().Contains("dining")) continue;
                var data = File.ReadAllBytes(scnFile);

                var chunks = new List<ScnChunk>();
                var chunkHeaderLength = 8;
                // check if first 4 bytes are 02003433
                if (data[0] != 0x02 || data[1] != 0x00 || data[2] != 0x34 || data[3] != 0x33)
                {
                    Console.WriteLine("Invalid SCN file");
                    return;
                }

                var index = 4;
                var nextOffset = BitConverter.ToInt32(data.Skip(index).Take(4).ToArray(), 0);

                var scnChunk = new ScnChunk() { Offset = 0, NextOffset = nextOffset, ChunkType = ScnChunkType.SCNFILE };
                chunks.Add(scnChunk);
                while (nextOffset != 0)
                {
                    var chunkType = (ScnChunkType)BitConverter.ToInt16(data.Skip(nextOffset).Take(2).ToArray(), 0);
                    var chunkOffset = nextOffset;
                    nextOffset = BitConverter.ToInt32(data.Skip(nextOffset + 4).Take(4).ToArray(), 0);
                    scnChunk = new ScnChunk() { Offset = chunkOffset, NextOffset = nextOffset, ChunkType = chunkType };
                    chunks.Add(scnChunk);
                }

                var gInfoList = new List<GraphicsInfo>();

                // write details of each chunk to console
                foreach (var chunk in chunks)
                {
                    if (chunk.ChunkType == ScnChunkType.GRAPHICS_INFO)
                    {
                        var graphicsInfoData = data.Skip(chunk.Offset + chunkHeaderLength).Take(chunk.ChunkLength - 8).ToArray();
                        for (int i = 0; i < graphicsInfoData.Length; i += 16)
                        {
                            var width = BitConverter.ToUInt16(graphicsInfoData.Skip(i).Take(2).ToArray(), 0);
                            var height = BitConverter.ToUInt16(graphicsInfoData.Skip(i + 2).Take(2).ToArray(), 0) & ~C16_FLAG_MASK;
                            var unk1 = BitConverter.ToUInt16(graphicsInfoData.Skip(i + 4).Take(2).ToArray(), 0);
                            var unk2 = BitConverter.ToUInt16(graphicsInfoData.Skip(i + 6).Take(2).ToArray(), 0);
                            var offset = BitConverter.ToUInt32(graphicsInfoData.Skip(i + 8).Take(4).ToArray(), 0) & 0x007FFFFF;
                            var paletteOffset = BitConverter.ToUInt32(graphicsInfoData.Skip(i + 12).Take(4).ToArray(), 0) & 0x007FFFFF;
                            gInfoList.Add(new GraphicsInfo { Width = width, Height = (ushort)height, AnimOffsetX = unk1, AnimOffsetY = unk2, Offset = offset, PaletteOffset = paletteOffset });
                        }
                    }
                }
                var imageDataOffset = chunks.Where(c => c.ChunkType == ScnChunkType.BLOCKS).FirstOrDefault().Offset + chunkHeaderLength;
                var imageData = data.Skip(imageDataOffset).ToArray();
                var tileOutputFolder = Path.Combine(Path.GetDirectoryName(scnFile), "Tiles", Path.GetFileNameWithoutExtension(scnFile));
                Directory.CreateDirectory(tileOutputFolder);
                var tiles = new List<byte[]>();
                for (int i = 0; i < imageData.Length; i += 0x10)
                {
                    var tile = imageData.Skip(i).Take(0x10).ToArray();
                    //File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(scnFile), "Tiles", $"{Path.GetFileNameWithoutExtension(scnFile)}", $"{i / 0x10}.bin"), tile);
                    tiles.Add(tile);
                    var palette1 = data.Skip((int)gInfoList.FirstOrDefault().PaletteOffset).Take(1024).ToArray();
                    var pal = ColorHelper.ConvertBytesToARGB(palette1);
                    var image = ImageFormatHelper.GenerateClutImage(pal, tile, 4, 4);
                    // //image.Save(Path.Combine(Path.GetDirectoryName(scnFile), "Tiles", $"{Path.GetFileNameWithoutExtension(scnFile)}", $"{i / 0x10}.png"), ImageFormat.Png);
                    image = ImageFormatHelper.GenerateClutImage(pal, tile, 4, 4, true);
                    image.Save(Path.Combine(tileOutputFolder, $"{i / 0x10}_tp.png"), ImageFormat.Png);
                }

                var outputFolder = Path.Combine(Path.GetDirectoryName(scnFile), "ImagesTP", Path.GetFileNameWithoutExtension(scnFile));
                Directory.CreateDirectory(outputFolder);

                foreach (var (gInfo, gIndex) in gInfoList.WithIndex())
                {
                    if (gInfo.PaletteOffset == 0) Debugger.Break();
                    var palette1 = data.Skip((int)gInfo.PaletteOffset).Take(1024).ToArray();
                    var pal = ColorHelper.ConvertBytesToARGB(palette1);
                    var adjustedWidth = gInfo.Width % 4 == 0 ? gInfo.Width : gInfo.Width + 1;
                    var adjustedHeight = gInfo.Height % 4 == 0 ? gInfo.Height : gInfo.Height + 1;
                    var tileByteCount = ((adjustedWidth * adjustedHeight) / 16) * 2;
                    var tileData = data.Skip((int)gInfo.Offset).Take(tileByteCount).ToArray();
                    File.WriteAllBytes(Path.Combine(outputFolder, $"{gIndex}_0x{gInfo.Offset:X8}_0x{gInfo.PaletteOffset:X8}.bin"), tileData);
                    var finalImage = ParseTileData(tileData, tiles, pal, adjustedWidth, adjustedHeight);
                    finalImage.Save(Path.Combine(outputFolder, $"{gIndex}_0x{gInfo.Offset:X8}_0x{gInfo.PaletteOffset:X8}.png"), ImageFormat.Png);
                }
            }
            static Bitmap ParseTileData(byte[] tileData, List<byte[]> tiles, List<Color> pColors, int imageWidth, int imageHeight)
            {
                var finalImageData = new byte[imageWidth * imageHeight];

                var widthInTiles = imageWidth % 4 == 0 ? imageWidth / 4 : (int)Math.Floor(imageWidth / 4.0);
                var heightInTiles = imageHeight % 4 == 0 ? imageHeight / 4 : (int)Math.Floor(imageHeight / 4.0);
                // Assuming tileData is an array of 16-bit tile indices and tiles is a list of 16-byte tiles
                for (int tileY = 0; tileY < heightInTiles; tileY++) // 50 tiles high
                {
                    for (int tileX = 0; tileX < widthInTiles; tileX++) // 80 tiles wide
                    {
                        // Get the tile index from tileData
                        int tileDataIndex = (tileY * widthInTiles + tileX) * 2; // 2 bytes per tile index
                        var tileIndex = BitConverter.ToInt16(tileData.Skip(tileDataIndex).Take(2).ToArray(), 0); // 15-bit tile index
                        tileIndex = (short)(tileIndex > 0 ? tileIndex : tileIndex & 0x7FFF);
                        // Get the corresponding tile
                        var thisTile = tileIndex > 0 ? tiles[tileIndex] : new byte[16];

                        // Calculate the position in finalImageData
                        int pixelX = tileX * 4; // 4 pixels wide per tile
                        int pixelY = tileY * 4; // 4 pixels high per tile

                        // Copy the tile into the correct position in finalImageData
                        for (int y = 0; y < 4; y++) // 4 rows in a tile
                        {
                            for (int x = 0; x < 4; x++) // 4 columns in a tile
                            {
                                // Calculate the position in the final image data array
                                int finalIndex = (pixelY + y) * imageWidth + (pixelX + x);

                                // Copy the pixel from the tile to the final image
                                try
                                {
                                    finalImageData[finalIndex] = thisTile[y * 4 + x];
                                }
                                catch
                                {
                                    return ImageFormatHelper.GenerateClutImage(pColors, finalImageData, imageWidth, imageHeight, true);

                                }
                            }
                        }
                    }
                }

                return ImageFormatHelper.GenerateClutImage(pColors, finalImageData, imageWidth, imageHeight, true);
            }
        }



    }


    class ScnChunk
    {
        public int Offset { get; set; }
        public int NextOffset { get; set; }
        public ScnChunkType ChunkType { get; set; }
        public byte[]? ChunkData { get; set; }
        public int ChunkLength => NextOffset - Offset;
    }

    class GraphicsInfo
    {
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public ushort AnimOffsetX { get; set; }
        public ushort AnimOffsetY { get; set; }
        public uint Offset { get; set; }
        public uint PaletteOffset { get; set; }
    }

    enum ScnChunkType
    {
        UNUSED = 0x0000,
        DIALOGUE = 0x0001,
        SCNFILE = 0x0002,
        BLOCK_LIST = 0x0003,
        BLOCKS = 0x0004,
        PALETTES = 0x0005,
        GRAPHICS_INFO = 0x0006,
        GRAPHICS_LIST = 0x0007,
    }

}
