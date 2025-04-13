using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC
{
    public static class Discworld
    {
        const int C16_240 = 0x4000; // Example value, replace with actual value
        const int C16_224 = 0x8000; // Example value, replace with actual value
        const int C16_MAP = 0xC000; // Example value, replace with actual value

        const int C16_FLAG_MASK = C16_240 | C16_224 | C16_MAP;

        public static void ExtractAll(string scnDir)
        {
            
            var indexFile = Path.Combine(scnDir, "INDEX");
            // var scnDir = @"C:\Dev\Gaming\PC_DOS\Games\Discworld_DOS_EN_ISO-Version---Directors-Cut\Discworld_Directors_Cut_ISO\DISCWLD";
            // var indexFile = @"C:\Dev\Gaming\PC_DOS\Games\Discworld_DOS_EN_ISO-Version---Directors-Cut\Discworld_Directors_Cut_ISO\DISCWLD\INDEX";
            var indexData = File.ReadAllBytes(indexFile);
            var fileList = new Dictionary<int, string>();
            var dwTiles = new List<byte[]>();
            var iCount = indexData.Length / 0x14;
            for (int i = 0; i < iCount; i++)
            {
                var filename = Encoding.ASCII.GetString(indexData.Skip(i*0x14).Take(12).ToArray()).TrimEnd('\0');
                fileList.Add(i, filename);
            }
            var scnFiles = Directory.GetFiles(scnDir, "*.SCN");

            foreach (var fl in fileList)
            {
                var scnFile = scnFiles.FirstOrDefault(f => f.ToLower().Contains(fl.Value));
                //if (!scnFile.ToLower().Contains("dining")) continue;
                var data = File.ReadAllBytes(scnFile);

                var chunks = new List<ScnChunk>();
                var chunkHeaderLength = 8;
                // check if first 4 bytes are 02003433
                if (data[0] != 0x02 || data[1] != 0x00 || data[2] != 0x34 || data[3] != 0x33)
                {
                    Console.WriteLine("Invalid SCN file");
                    return;
                }
                var transIndex = BitConverter.ToInt32(data.Skip(0x14).Take(4).ToArray(), 0);
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
                    if (chunk.ChunkType == ScnChunkType.SCENE)
                    {
                        var sceneData = data.Skip(chunk.Offset + chunkHeaderLength).Take(chunk.ChunkLength - 8).ToArray();
                        var sceneStructure = new SceneStructure
                        {
                            NumEntrances = BitConverter.ToUInt32(sceneData.Skip(0).Take(4).ToArray(), 0),
                            NumPolygons = BitConverter.ToUInt32(sceneData.Skip(4).Take(4).ToArray(), 0),
                            NumTaggedActors = BitConverter.ToUInt32(sceneData.Skip(8).Take(4).ToArray(), 0),
                            DefRefer = BitConverter.ToUInt32(sceneData.Skip(12).Take(4).ToArray(), 0),
                            SceneScriptHandle = BitConverter.ToUInt32(sceneData.Skip(16).Take(4).ToArray(), 0),
                            EntranceHandle = BitConverter.ToUInt32(sceneData.Skip(20).Take(4).ToArray(), 0),
                            PolygonHandle = BitConverter.ToUInt32(sceneData.Skip(24).Take(4).ToArray(), 0),
                            TaggedActorHandle = BitConverter.ToUInt32(sceneData.Skip(28).Take(4).ToArray(), 0)
                        };
                    }
                    if (chunk.ChunkType == ScnChunkType.GRAPHICS_INFO)
                    {
                        var graphicsInfoData = data.Skip(chunk.Offset + chunkHeaderLength).Take(chunk.ChunkLength - 8).ToArray();
                        for (int i = 0; i < graphicsInfoData.Length; i += 16)
                        {
                            var width = BitConverter.ToUInt16(graphicsInfoData.Skip(i).Take(2).ToArray(), 0);
                            var height = BitConverter.ToUInt16(graphicsInfoData.Skip(i + 2).Take(2).ToArray(), 0) & ~C16_FLAG_MASK;
                            var unk1 = BitConverter.ToInt16(graphicsInfoData.Skip(i + 4).Take(2).ToArray(), 0);
                            var unk2 = BitConverter.ToInt16(graphicsInfoData.Skip(i + 6).Take(2).ToArray(), 0);
                            var fileId = (BitConverter.ToUInt32(graphicsInfoData.Skip(i + 8).Take(4).ToArray(), 0) & 0xFF800000)>>23;
                            //if (fileId != fileListFile.Key) Debugger.Break();
                            var offset = BitConverter.ToUInt32(graphicsInfoData.Skip(i + 8).Take(4).ToArray(), 0) & 0x007FFFFF;
                            var paletteOffset = BitConverter.ToUInt32(graphicsInfoData.Skip(i + 12).Take(4).ToArray(), 0) & 0x007FFFFF;
                            gInfoList.Add(new GraphicsInfo { Width = width, Height = (ushort)height, AnimOffsetX = unk1, AnimOffsetY = unk2, Offset = offset, PaletteOffset = paletteOffset });
                        }
                    }
                }
                var imageDataOffset = chunks.Where(c => c.ChunkType == ScnChunkType.BLOCKS).FirstOrDefault().Offset + chunkHeaderLength;
                var imageData = data.Skip(imageDataOffset).ToArray();
                var tileOutputFolder = Path.Combine(Path.GetDirectoryName(scnFile), "Tiles_NEW", Path.GetFileNameWithoutExtension(scnFile));
                //Directory.CreateDirectory(tileOutputFolder);
                var tiles = new List<byte[]>();
                for (int i = 0; i < imageData.Length; i += 0x10)
                {
                    var tile = imageData.Skip(i).Take(0x10).ToArray();
                    //File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(scnFile), "Tiles", $"{Path.GetFileNameWithoutExtension(scnFile)}", $"{i / 0x10}.bin"), tile);
                    tiles.Add(tile);
                    
                    // var palette1 = data.Skip((int)gInfoList.FirstOrDefault().PaletteOffset).Take(1024).ToArray();
                    // var pal = ColorHelper.ConvertBytesToARGB(palette1);
                    // var image = ImageFormatHelper.GenerateClutImage(pal, tile, 4, 4);
                    // // // //image.Save(Path.Combine(Path.GetDirectoryName(scnFile), "Tiles", $"{Path.GetFileNameWithoutExtension(scnFile)}", $"{i / 0x10}.png"), ImageFormat.Png);
                    // image = ImageFormatHelper.GenerateClutImage(pal, tile, 4, 4, true);
                    // image.Save(Path.Combine(tileOutputFolder, $"{i / 0x10}_tp.png"), ImageFormat.Png);
                }

                var outputFolder = Path.Combine(Path.GetDirectoryName(scnFile), "Images_by_offset", Path.GetFileNameWithoutExtension(scnFile));
                Directory.CreateDirectory(outputFolder);
                var binaryOutputFolder = Path.Combine(outputFolder, "Binary");
                Directory.CreateDirectory(binaryOutputFolder);
                var maxWidth = gInfoList.Max(g => g.Width);
                var maxHeight = gInfoList.Max(g => g.Height);
                var parsedOffsets = new List<uint>();
                foreach (var (gInfo, gIndex) in gInfoList.WithIndex())
                {
                    if (parsedOffsets.Contains(gInfo.Offset)) continue;
                    if (gInfo.PaletteOffset == 0) Debugger.Break();
                    var palette1 = data.Skip((int)gInfo.PaletteOffset).Take(1024).ToArray();
                    //File.WriteAllBytes(Path.Combine(binaryOutputFolder, $"{gIndex}_0x{gInfo.Offset:X8}_0x{gInfo.PaletteOffset:X8}_palette.bin"), palette1);
                    var pal = ColorHelper.ConvertBytesToARGB(palette1);
                    var adjustedWidth = gInfo.Width % 4 == 0 ? gInfo.Width : ((gInfo.Width / 4) * 4) + 4;
                    var adjustedHeight = gInfo.Height % 4 == 0 ? gInfo.Height : ((gInfo.Height / 4) * 4) + 4;
                    var tileByteCount = (adjustedWidth / 4) * (adjustedHeight / 4) * 2;
                    var tileData = data.Skip((int)gInfo.Offset).Take(tileByteCount).ToArray();
                    //File.WriteAllBytes(Path.Combine(binaryOutputFolder, $"{gIndex}_0x{gInfo.Offset:X8}_0x{gInfo.PaletteOffset:X8}.bin"), tileData);
                    var finalImage = ParseTileData(tileData, tiles, transIndex, pal, adjustedWidth, adjustedHeight, gInfo.AnimOffsetX, gInfo.AnimOffsetY, maxWidth, maxHeight);
                    finalImage.Save(Path.Combine(outputFolder, $"{gInfo}.png"), ImageFormat.Png);
                }
            }
            static Image ParseTileData(byte[] tileData, List<byte[]> tiles, int transIndex, List<Color> pColors, int imageWidth, int imageHeight, int xPos, int yPos, int maxWidth = 0, int maxHeight = 0)
            {
                var finalImageData = new byte[imageWidth * imageHeight];

                var widthInTiles = imageWidth % 4 == 0 ? imageWidth / 4 : (int)Math.Floor((double)(((imageWidth / 4) * 4) + 4)/4);
                var heightInTiles = imageHeight % 4 == 0 ? imageHeight / 4 : (int)Math.Floor((double)(((imageHeight / 4) * 4) + 4)/4);
                // Assuming tileData is an array of 16-bit tile indices and tiles is a list of 16-byte tiles
                for (int tileY = 0; tileY < heightInTiles; tileY++) // 50 tiles high
                {
                    for (int tileX = 0; tileX < widthInTiles; tileX++) // 80 tiles wide
                    {
                        // Get the tile index from tileData
                        int tileDataIndex = (tileY * widthInTiles + tileX) * 2; // 2 bytes per tile index
                        var tileIndex = BitConverter.ToInt16(tileData.Skip(tileDataIndex).Take(2).ToArray(), 0); // 15-bit tile index
                        
                        // Get the corresponding tile
                        var thisTile = tileIndex > 0 ? tiles[tileIndex] : (tileIndex & 0x7FFF) > 0  ? tiles[(transIndex + (tileIndex & 0x7FFF))] : new byte[16];

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
                                    return new Bitmap(ImageFormatHelper.GenerateClutImage(pColors, finalImageData, imageWidth, imageHeight, true));

                                }
                            }
                        }
                    }
                }
                pColors[0] = Color.Black;
                var initialImage = ImageFormatHelper.GenerateClutImage(pColors, finalImageData, imageWidth, imageHeight, !(imageWidth == maxWidth && imageHeight == maxHeight));
                if (xPos != 0 || yPos != 0)
                {
                    var finalImage = new Bitmap(maxWidth, maxHeight, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(finalImage))
                    {  // Clear the image with a transparent background
                        g.Clear(Color.Transparent);

                        // Set high-quality rendering options (optional but recommended)
                        g.CompositingMode = CompositingMode.SourceOver;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                        // Draw the initial image at the specified position
                        g.DrawImage(initialImage, new Rectangle(Math.Abs(xPos), Math.Abs(yPos), initialImage.Width, initialImage.Height));
                    }
                    return finalImage;
                }
                return initialImage;
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
        public short AnimOffsetX { get; set; }
        public short AnimOffsetY { get; set; }
        public uint Offset { get; set; }
        public uint PaletteOffset { get; set; }
        public override string ToString()
        {
            return $"O{Offset}_P{PaletteOffset}_W{Width}_H{Height}_X{AnimOffsetX}_Y{AnimOffsetY}";
        }
    }

    class SceneStructure
    {
        public uint NumEntrances { get; set; }
        public uint NumPolygons { get; set; }
        public uint NumTaggedActors { get; set; }
        public uint DefRefer { get; set; }
        public uint SceneScriptHandle { get; set; }
        public uint EntranceHandle { get; set; }
        public uint PolygonHandle { get; set; }
        public uint TaggedActorHandle { get; set; }
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
        SCENE = 0x000E
    }

}
