using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.BlackMagic
{
    public sealed class MapFileParser
    {
        public sealed class MapObjectRecord
        {
            public int Index { get; init; }
            public byte ActiveFlag { get; init; }
            public byte MirrorFlag { get; init; }
            public short FrameId { get; init; }
            public short WorldX { get; init; }
            public short WorldY { get; init; }
            public short AuxHx { get; init; }
            public short AuxHy { get; init; }
            public short AuxHv { get; init; }
            public byte[] Raw22Bytes { get; init; } = Array.Empty<byte>();

            public bool IsActive => ActiveFlag != 0;
        }

        public sealed class ParsedMapFile
        {
            public string FilePath { get; }
            public string SignatureText { get; }
            public byte[] Header520 { get; }

            public int GroundColumns { get; }
            public int GroundRows { get; }
            public int GroundStride { get; }
            public ushort[] GroundTileIds { get; }

            public int PackedColumns { get; }
            public int PackedRows { get; }
            public int PackedStride { get; }
            public ushort[] PackedHeightFace { get; }

            public IReadOnlyList<MapObjectRecord> Objects => _objects;
            private readonly List<MapObjectRecord> _objects;

            public ParsedMapFile(
              string filePath,
              string signatureText,
              byte[] header520,
              int groundColumns,
              int groundRows,
              int groundStride,
              ushort[] groundTileIds,
              int packedColumns,
              int packedRows,
              int packedStride,
              ushort[] packedHeightFace,
              List<MapObjectRecord> objects)
            {
                FilePath = filePath;
                SignatureText = signatureText;
                Header520 = header520;
                GroundColumns = groundColumns;
                GroundRows = groundRows;
                GroundStride = groundStride;
                GroundTileIds = groundTileIds;
                PackedColumns = packedColumns;
                PackedRows = packedRows;
                PackedStride = packedStride;
                PackedHeightFace = packedHeightFace;
                _objects = objects;
            }

            public ushort GetGround(int x, int y)
            {
                if ((uint)x >= GroundColumns || (uint)y >= GroundRows)
                    return 0;
                return GroundTileIds[(x * GroundStride) + y];
            }

            public ushort GetPacked(int x, int y)
            {
                if ((uint)x >= PackedColumns || (uint)y >= PackedRows)
                    return 0;
                return PackedHeightFace[(x * PackedStride) + y];
            }
        }

        public ParsedMapFile Parse(string mapPath)
        {
            if (!File.Exists(mapPath))
                throw new FileNotFoundException("MAP file not found", mapPath);

            using var fs = new FileStream(mapPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            const int signatureSize = 0x20;
            const int headerSize = 0x520;
            const int groundChunkCount = 0x100;
            const int groundChunkSize = 0x400;
            const int packedChunkCount = 0x40;
            const int packedChunkSize = 0x200;
            const int objectBlockSize = 0x2c00;
            const int objectRecordSize = 0x16;

            long minLength = signatureSize + headerSize +
                             (groundChunkCount * groundChunkSize) +
                             (packedChunkCount * packedChunkSize) +
                             objectBlockSize;

            if (fs.Length < minLength)
                throw new InvalidDataException($"MAP file too small. Expected >= {minLength} bytes.");

            byte[] signatureBytes = br.ReadBytes(signatureSize);
            string signatureText = Encoding.ASCII.GetString(signatureBytes).TrimEnd('\0', ' ', '\r', '\n', '\t');
            if (!signatureText.StartsWith("APPLE SHEED MAP FILE", StringComparison.Ordinal))
                throw new InvalidDataException($"Unexpected MAP signature: '{signatureText}'");

            byte[] header520 = br.ReadBytes(headerSize);

            int groundStride = 0x202;
            int groundColumns = 0x100;
            int groundRows = 0x200;
            ushort[] ground = new ushort[groundColumns * groundStride];

            for (int x = 0; x < groundChunkCount; x++)
            {
                byte[] chunk = br.ReadBytes(groundChunkSize);
                if (chunk.Length != groundChunkSize)
                    throw new EndOfStreamException("Unexpected EOF while reading ground chunks.");

                for (int y = 0; y < groundRows; y++)
                {
                    int chunkOff = y * 2;
                    ground[(x * groundStride) + y] = (ushort)(chunk[chunkOff] | (chunk[chunkOff + 1] << 8));
                }
            }

            int packedStride = 0x101;
            int packedColumns = 0x40;
            int packedRows = 0x100;
            ushort[] packed = new ushort[packedColumns * packedStride];

            for (int x = 0; x < packedChunkCount; x++)
            {
                byte[] chunk = br.ReadBytes(packedChunkSize);
                if (chunk.Length != packedChunkSize)
                    throw new EndOfStreamException("Unexpected EOF while reading packed height/face chunks.");

                for (int y = 0; y < packedRows; y++)
                {
                    int chunkOff = y * 2;
                    packed[(x * packedStride) + y] = (ushort)(chunk[chunkOff] | (chunk[chunkOff + 1] << 8));
                }
            }

            byte[] objectBytes = br.ReadBytes(objectBlockSize);
            if (objectBytes.Length != objectBlockSize)
                throw new EndOfStreamException("Unexpected EOF while reading object record block.");

            int objectCount = objectBlockSize / objectRecordSize;
            var objects = new List<MapObjectRecord>(objectCount);

            for (int i = 0; i < objectCount; i++)
            {
                int off = i * objectRecordSize;
                byte[] raw = new byte[objectRecordSize];
                Buffer.BlockCopy(objectBytes, off, raw, 0, objectRecordSize);

                byte active = raw[0];
                byte mirror = raw[2];
                short frameId = (short)(raw[4] | (raw[5] << 8));
                short worldX = (short)(raw[10] | (raw[11] << 8));
                short worldY = (short)(raw[12] | (raw[13] << 8));
                short hx = (short)(raw[14] | (raw[15] << 8));
                short hy = (short)(raw[16] | (raw[17] << 8));
                short hv = (short)(raw[18] | (raw[19] << 8));

                objects.Add(new MapObjectRecord
                {
                    Index = i,
                    ActiveFlag = active,
                    MirrorFlag = mirror,
                    FrameId = frameId,
                    WorldX = worldX,
                    WorldY = worldY,
                    AuxHx = hx,
                    AuxHy = hy,
                    AuxHv = hv,
                    Raw22Bytes = raw
                });
            }

            return new ParsedMapFile(
              mapPath,
              signatureText,
              header520,
              groundColumns,
              groundRows,
              groundStride,
              ground,
              packedColumns,
              packedRows,
              packedStride,
              packed,
              objects);
        }

        public void ExportLayerImages(string mapPath, string outputDirectory)
        {
            var parsed = Parse(mapPath);
            ExportLayerImages(parsed, outputDirectory);
        }

        public void ExportTerrain(
          string mapPath,
          string chp1Path,
          string outputDirectory)
        {
            var parsed = Parse(mapPath);
            ExportTerrain(parsed, chp1Path, outputDirectory);
        }

        public void ExportTerrain(
          ParsedMapFile parsed,
          string chp1Path,
          string outputDirectory)
        {
            if (!File.Exists(chp1Path))
                throw new FileNotFoundException("CHP #1 bitmap not found", chp1Path);

            Directory.CreateDirectory(outputDirectory);

            // Load the atlas as a raw 8-bit image, but interpret it as 16-bit pixels.
            byte[] bmpBytes = File.ReadAllBytes(chp1Path);
            int pixelOffset = BitConverter.ToInt32(bmpBytes, 10);
            int width = BitConverter.ToInt32(bmpBytes, 18);
            int height = BitConverter.ToInt32(bmpBytes, 22);

            // The game treats this as a 16-bit surface.
            // So width in 16-bit pixels is width / 2.
            int surfaceWidth = width / 2;
            int surfaceHeight = height;

            ushort[] atlasPixels = new ushort[surfaceWidth * surfaceHeight];

            // BMP rows are padded to 4 bytes, and stored bottom-up.
            int rowSize = (width + 3) & ~3;

            for (int y = 0; y < height; y++)
            {
                int srcRow = height - 1 - y; // Bottom-up
                int srcOffset = pixelOffset + srcRow * rowSize;
                int dstOffset = y * surfaceWidth;

                for (int x = 0; x < surfaceWidth; x++)
                {
                    atlasPixels[dstOffset + x] = BitConverter.ToUInt16(bmpBytes, srcOffset + x * 2);
                }
            }

            Rgba32 DecodePixel(ushort p)
            {
                int r = (p >> 11) & 0x1F;
                int g = (p >> 5) & 0x3F;
                int b = p & 0x1F;
                return new Rgba32((byte)((r * 255) / 31), (byte)((g * 255) / 63), (byte)((b * 255) / 31), 255);
            }

            int[] A_X = { 14, 12, 14, 10, 12, 14, 8, 10, 12, 14, 6, 8, 10, 12, 14, 4, 6, 8, 10, 12, 14, 2, 4, 6, 8, 10, 12, 14, 0, 2, 4, 6, 8, 10, 12, 14, 2, 4, 6, 8, 10, 12, 14, 4, 6, 8, 10, 12, 14, 6, 8, 10, 12, 14, 8, 10, 12, 14, 10, 12, 14, 12, 14, 14 };
            int[] A_Y = { 1, 2, 2, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 9, 9, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 12, 12, 12, 12, 13, 13, 13, 14, 14, 15 };
            int[] B_X = { 0, 0, 2, 0, 2, 4, 0, 2, 4, 6, 0, 2, 4, 6, 8, 0, 2, 4, 6, 8, 10, 0, 2, 4, 6, 8, 10, 12, 0, 2, 4, 6, 8, 10, 12, 14, 0, 2, 4, 6, 8, 10, 12, 0, 2, 4, 6, 8, 10, 0, 2, 4, 6, 8, 0, 2, 4, 6, 0, 2, 4, 0, 2, 0 };
            int[] B_Y = { 1, 2, 2, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 9, 9, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 12, 12, 12, 12, 13, 13, 13, 14, 14, 15 };

            int outWidth = parsed.GroundColumns * 8;
            int outHeight = parsed.GroundRows * 8 + 16;
            using var outImg = new Image<Rgba32>(outWidth, outHeight, new Rgba32(0, 0, 0, 255));

            for (int y = 0; y < parsed.GroundRows; y++)
            {
                for (int x = 0; x < parsed.GroundColumns; x++)
                {
                    ushort tileId = parsed.GetGround(x, y);
                    if (tileId == 0) continue;

                    // Checkerboard logic
                    int uVar15 = x >> 31; // 0 for positive
                    int uVar10 = ((x ^ uVar15) - uVar15) & 1 ^ uVar15; // x % 2
                    int uVar16 = y >> 31; // 0 for positive
                    int yMod2 = ((y ^ uVar16) - uVar16) & 1 ^ uVar16; // y % 2

                    int iVar11;
                    if (((uVar10 - uVar15 == 1) && (yMod2 == uVar16)) ||
                        ((uVar10 == uVar15) && (yMod2 - uVar16 == 1)))
                    {
                        iVar11 = 0; // Array A
                    }
                    else
                    {
                        iVar11 = 1; // Array B
                    }

                    int row = tileId / 40;
                    int col = tileId % 40;

                    int srcBaseX = col * 8;
                    int srcBaseY = 815 - row * 8;

                    int dstBaseX = x * 8;
                    int dstBaseY = (parsed.GroundRows - 1 - y) * 8 + 16;

                    for (int i = 0; i < 64; i++)
                    {
                        int px = iVar11 == 0 ? A_X[i] / 2 : B_X[i] / 2;
                        int py = iVar11 == 0 ? A_Y[i] : B_Y[i];

                        int srcX = srcBaseX + px;
                        int srcY = srcBaseY - py;

                        int dstX = dstBaseX + px;
                        int dstY = dstBaseY - py;

                        if (srcX >= 0 && srcX < surfaceWidth && srcY >= 0 && srcY < surfaceHeight &&
                            dstX >= 0 && dstX < outWidth && dstY >= 0 && dstY < outHeight)
                        {
                            ushort p = atlasPixels[srcY * surfaceWidth + srcX];
                            if (p != 0) // Assuming 0 is transparent
                            {
                                outImg[dstX, dstY] = DecodePixel(p);
                            }
                        }
                    }
                }
            }

            string phase0Path = Path.Combine(outputDirectory, "terrain.png");
            outImg.SaveAsPng(phase0Path);
        }

        public void ExportLayerImages(ParsedMapFile parsed, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            ExportGroundTileIdLayer(parsed, Path.Combine(outputDirectory, "ground_tile_id.png"));
            ExportHeightLevelLayer(parsed, Path.Combine(outputDirectory, "height_level.png"));
            ExportFaceMaskLayer(parsed, Path.Combine(outputDirectory, "face_mask.png"));
            ExportObjectsMaskLayer(parsed, Path.Combine(outputDirectory, "objects_mask.png"));
        }

        private static void ExportGroundTileIdLayer(ParsedMapFile parsed, string outputPath)
        {
            using var img = new Image<Rgba32>(parsed.GroundColumns, parsed.GroundRows);

            for (int x = 0; x < parsed.GroundColumns; x++)
            {
                for (int y = 0; y < parsed.GroundRows; y++)
                {
                    ushort tileId = parsed.GetGround(x, y);
                    byte v = (byte)(tileId & 0xFF);
                    img[x, y] = new Rgba32(v, v, v, 255);
                }
            }

            img.SaveAsPng(outputPath);
        }

        private static void ExportHeightLevelLayer(ParsedMapFile parsed, string outputPath)
        {
            using var img = new Image<Rgba32>(parsed.PackedColumns, parsed.PackedRows);

            for (int x = 0; x < parsed.PackedColumns; x++)
            {
                for (int y = 0; y < parsed.PackedRows; y++)
                {
                    ushort packed = parsed.GetPacked(x, y);
                    int h = packed & 0x3F;
                    byte v = (byte)((h * 255) / 63);
                    img[x, y] = new Rgba32(v, v, v, 255);
                }
            }

            img.SaveAsPng(outputPath);
        }

        private static void ExportFaceMaskLayer(ParsedMapFile parsed, string outputPath)
        {
            using var img = new Image<Rgba32>(parsed.PackedColumns, parsed.PackedRows);

            for (int x = 0; x < parsed.PackedColumns; x++)
            {
                for (int y = 0; y < parsed.PackedRows; y++)
                {
                    ushort packed = parsed.GetPacked(x, y);
                    bool rMask = (packed & 0x40) != 0 || (packed & 0x80) != 0;
                    bool gMask = (packed & 0x100) != 0;
                    bool bMask = (packed & 0x200) != 0;

                    img[x, y] = new Rgba32(
                      rMask ? (byte)255 : (byte)0,
                      gMask ? (byte)255 : (byte)0,
                      bMask ? (byte)255 : (byte)0,
                      255);
                }
            }

            img.SaveAsPng(outputPath);
        }

        private static void ExportObjectsMaskLayer(ParsedMapFile parsed, string outputPath)
        {
            using var img = new Image<Rgba32>(parsed.PackedColumns, parsed.PackedRows, new Rgba32(0, 0, 0, 255));

            foreach (var obj in parsed.Objects)
            {
                if (!obj.IsActive)
                    continue;

                int cellX = obj.WorldX >> 6;
                int cellY = obj.WorldY >> 4;

                if ((uint)cellX < (uint)parsed.PackedColumns && (uint)cellY < (uint)parsed.PackedRows)
                {
                    img[cellX, cellY] = new Rgba32(255, 255, 255, 255);
                }
            }

            img.SaveAsPng(outputPath);
        }
    }

}
