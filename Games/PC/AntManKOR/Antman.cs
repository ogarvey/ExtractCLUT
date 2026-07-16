using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = System.Drawing.Color;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC
{
    public static class Antman
    {
        private struct ElementA
        {
            public int Width;
            public int Height;
            public int XOffset;
            public int YOffset;
            public int WidthBlocks;
            public int HeightBlocks;
            public int StartTile;
        }

        private struct PixelInfo
        {
            public int X;
            public int Y;
            public int ColorIndex;
        }

        public static void ExtractAll(string gameDir, string outputDir)
        {
            Console.WriteLine("Starting Antman graphics extraction...");

            string palPath = Path.Combine(gameDir, "PAL.DAT");
            string cspritePath = Path.Combine(gameDir, "CSPRITE.DAT");
            string tilePath = Path.Combine(gameDir, "TILE.DAT");
            string mapPath = Path.Combine(gameDir, "MAP.DAT");

            if (!File.Exists(palPath) || !File.Exists(cspritePath) || !File.Exists(tilePath) || !File.Exists(mapPath))
            {
                Console.WriteLine("Error: PAL.DAT, CSPRITE.DAT, TILE.DAT, or MAP.DAT is missing from the directory.");
                return;
            }

            // Load Palette 0
            Console.WriteLine("Loading palette...");
            var palette = LoadPalette0(palPath);

            // Extract Character Sprites (CSPRITE.DAT)
            string spriteOutputDir = Path.Combine(outputDir, "Sprites");
            Directory.CreateDirectory(spriteOutputDir);
            ExtractSprites(cspritePath, spriteOutputDir, palette);

            // Extract Background Tiles (TILE.DAT)
            string tileOutputDir = Path.Combine(outputDir, "Tiles");
            Directory.CreateDirectory(tileOutputDir);
            ExtractTiles(tilePath, palPath, tileOutputDir);

            // Extract Maps (MAP.DAT)
            string mapsOutputDir = Path.Combine(outputDir, "Maps");
            Directory.CreateDirectory(mapsOutputDir);
            ExtractMaps(mapPath, tileOutputDir, spriteOutputDir, mapsOutputDir);

            Console.WriteLine("Antman graphics extraction completed successfully!");
        }

        private static List<Color> LoadPalette0(string palPath)
        {
            byte[] palBytes = File.ReadAllBytes(palPath);
            // Palette 0: offset = 116. Header size = 10 bytes. Palette data size = 768 bytes.
            byte[] palData = palBytes.Skip(116 + 10).Take(768).ToArray();
            return ColorHelper.ConvertBytesToRGB(palData, false);
        }

        private static (byte[] buffer, bool[] isWritten) EmulateBlock(byte[] codeBytes, int eax)
        {
            byte[] buffer = new byte[5632];
            bool[] isWritten = new bool[5632];
            int edi = 0;
            int esi = 0;
            int i = 0;
            int sz = codeBytes.Length;

            while (i < sz)
            {
                byte b1 = codeBytes[i];
                if (b1 == 0x47) // INC EDI
                {
                    edi++;
                    i++;
                }
                else if (b1 == 0x4F) // DEC EDI
                {
                    edi--;
                    i++;
                }
                else if (b1 == 0x46) // INC ESI
                {
                    esi++;
                    i++;
                }
                else if (b1 == 0x4E) // DEC ESI
                {
                    esi--;
                    i++;
                }
                else if (b1 == 0x01 && i + 1 < sz && codeBytes[i + 1] == 0xC7) // ADD EDI, EAX
                {
                    edi += eax;
                    i += 2;
                }
                else if (b1 == 0x01 && i + 1 < sz && codeBytes[i + 1] == 0xDE) // ADD ESI, EBX
                {
                    esi += 320;
                    i += 2;
                }
                else if (b1 == 0xA4) // MOVSB
                {
                    esi++;
                    edi++;
                    i++;
                }
                else if (b1 == 0xA5) // MOVSD
                {
                    esi += 4;
                    edi += 4;
                    i++;
                }
                else if (b1 == 0x66 && i + 1 < sz && codeBytes[i + 1] == 0xA5) // MOVSW
                {
                    esi += 2;
                    edi += 2;
                    i += 2;
                }
                else if (b1 == 0xC6 && i + 2 < sz && codeBytes[i + 1] == 0x07) // MOV [EDI], imm8
                {
                    byte val = codeBytes[i + 2];
                    if (edi >= 0 && edi < 5632)
                    {
                        buffer[edi] = val;
                        isWritten[edi] = true;
                    }
                    i += 3;
                }
                else if (b1 == 0x66 && i + 4 < sz && codeBytes[i + 1] == 0xC7 && codeBytes[i + 2] == 0x07) // MOV [EDI], imm16
                {
                    ushort val = BitConverter.ToUInt16(codeBytes, i + 3);
                    if (edi >= 0 && edi < 5632)
                    {
                        buffer[edi] = (byte)(val & 0xFF);
                        isWritten[edi] = true;
                    }
                    if (edi + 1 >= 0 && edi + 1 < 5632)
                    {
                        buffer[edi + 1] = (byte)((val >> 8) & 0xFF);
                        isWritten[edi + 1] = true;
                    }
                    i += 5;
                }
                else if (b1 == 0xC7 && i + 5 < sz && codeBytes[i + 1] == 0x07) // MOV [EDI], imm32
                {
                    uint val = BitConverter.ToUInt32(codeBytes, i + 2);
                    for (int offset = 0; offset < 4; offset++)
                    {
                        int currEdi = edi + offset;
                        if (currEdi >= 0 && currEdi < 5632)
                        {
                            buffer[currEdi] = (byte)((val >> (offset * 8)) & 0xFF);
                            isWritten[currEdi] = true;
                        }
                    }
                    i += 6;
                }
                else if (b1 == 0xC6 && i + 3 < sz && codeBytes[i + 1] == 0x47) // MOV [EDI + disp8], imm8
                {
                    sbyte disp = (sbyte)codeBytes[i + 2];
                    byte val = codeBytes[i + 3];
                    int targetEdi = edi + disp;
                    if (targetEdi >= 0 && targetEdi < 5632)
                    {
                        buffer[targetEdi] = val;
                        isWritten[targetEdi] = true;
                    }
                    i += 4;
                }
                else if (b1 == 0x66 && i + 5 < sz && codeBytes[i + 1] == 0xC7 && codeBytes[i + 2] == 0x47) // MOV [EDI + disp8], imm16
                {
                    sbyte disp = (sbyte)codeBytes[i + 3];
                    ushort val = BitConverter.ToUInt16(codeBytes, i + 4);
                    int targetEdi = edi + disp;
                    if (targetEdi >= 0 && targetEdi < 5632)
                    {
                        buffer[targetEdi] = (byte)(val & 0xFF);
                        isWritten[targetEdi] = true;
                    }
                    if (targetEdi + 1 >= 0 && targetEdi + 1 < 5632)
                    {
                        buffer[targetEdi + 1] = (byte)((val >> 8) & 0xFF);
                        isWritten[targetEdi + 1] = true;
                    }
                    i += 6;
                }
                else if (b1 == 0xC7 && i + 6 < sz && codeBytes[i + 1] == 0x47) // MOV [EDI + disp8], imm32
                {
                    sbyte disp = (sbyte)codeBytes[i + 2];
                    uint val = BitConverter.ToUInt32(codeBytes, i + 3);
                    for (int offset = 0; offset < 4; offset++)
                    {
                        int targetEdi = edi + disp + offset;
                        if (targetEdi >= 0 && targetEdi < 5632)
                        {
                            buffer[targetEdi] = (byte)((val >> (offset * 8)) & 0xFF);
                            isWritten[targetEdi] = true;
                        }
                    }
                    i += 7;
                }
                else if (b1 == 0xC6 && i + 6 < sz && codeBytes[i + 1] == 0x87) // MOV [EDI + disp32], imm8
                {
                    int disp = BitConverter.ToInt32(codeBytes, i + 2);
                    byte val = codeBytes[i + 6];
                    int targetEdi = edi + disp;
                    if (targetEdi >= 0 && targetEdi < 5632)
                    {
                        buffer[targetEdi] = val;
                        isWritten[targetEdi] = true;
                    }
                    i += 7;
                }
                else if (b1 == 0x66 && i + 8 < sz && codeBytes[i + 1] == 0xC7 && codeBytes[i + 2] == 0x87) // MOV [EDI + disp32], imm16
                {
                    int disp = BitConverter.ToInt32(codeBytes, i + 3);
                    ushort val = BitConverter.ToUInt16(codeBytes, i + 7);
                    int targetEdi = edi + disp;
                    if (targetEdi >= 0 && targetEdi < 5632)
                    {
                        buffer[targetEdi] = (byte)(val & 0xFF);
                        isWritten[targetEdi] = true;
                    }
                    if (targetEdi + 1 >= 0 && targetEdi + 1 < 5632)
                    {
                        buffer[targetEdi + 1] = (byte)((val >> 8) & 0xFF);
                        isWritten[targetEdi + 1] = true;
                    }
                    i += 9;
                }
                else if (b1 == 0xC7 && i + 9 < sz && codeBytes[i + 1] == 0x87) // MOV [EDI + disp32], imm32
                {
                    int disp = BitConverter.ToInt32(codeBytes, i + 2);
                    uint val = BitConverter.ToUInt32(codeBytes, i + 6);
                    for (int offset = 0; offset < 4; offset++)
                    {
                        int targetEdi = edi + disp + offset;
                        if (targetEdi >= 0 && targetEdi < 5632)
                        {
                            buffer[targetEdi] = (byte)((val >> (offset * 8)) & 0xFF);
                            isWritten[targetEdi] = true;
                        }
                    }
                    i += 10;
                }
                else if (b1 == 0x81 && i + 5 < sz && codeBytes[i + 1] == 0xC7) // ADD EDI, imm32
                {
                    int val = BitConverter.ToInt32(codeBytes, i + 2);
                    edi += val;
                    i += 6;
                }
                else if (b1 == 0x83 && i + 2 < sz && codeBytes[i + 1] == 0xC7) // ADD EDI, imm8
                {
                    sbyte val = (sbyte)codeBytes[i + 2];
                    edi += val;
                    i += 3;
                }
                else if (b1 == 0x81 && i + 5 < sz && codeBytes[i + 1] == 0xC6) // ADD ESI, imm32
                {
                    int val = BitConverter.ToInt32(codeBytes, i + 2);
                    esi += val;
                    i += 6;
                }
                else if (b1 == 0x83 && i + 2 < sz && codeBytes[i + 1] == 0xC6) // ADD ESI, imm8
                {
                    sbyte val = (sbyte)codeBytes[i + 2];
                    esi += val;
                    i += 3;
                }
                else if (b1 == 0x81 && i + 5 < sz && codeBytes[i + 1] == 0xEF) // SUB EDI, imm32
                {
                    int val = BitConverter.ToInt32(codeBytes, i + 2);
                    edi -= val;
                    i += 6;
                }
                else if (b1 == 0x83 && i + 2 < sz && codeBytes[i + 1] == 0xEF) // SUB EDI, imm8
                {
                    sbyte val = (sbyte)codeBytes[i + 2];
                    edi -= val;
                    i += 3;
                }
                else if (b1 == 0x81 && i + 5 < sz && codeBytes[i + 1] == 0xEE) // SUB ESI, imm32
                {
                    int val = BitConverter.ToInt32(codeBytes, i + 2);
                    esi -= val;
                    i += 6;
                }
                else if (b1 == 0x83 && i + 2 < sz && codeBytes[i + 1] == 0xEE) // SUB ESI, imm8
                {
                    sbyte val = (sbyte)codeBytes[i + 2];
                    esi -= val;
                    i += 3;
                }
                else if (b1 == 0xCB) // RETF
                {
                    break;
                }
                else
                {
                    i++;
                }
            }

            return (buffer, isWritten);
        }

        private static void ExtractSprites(string cspritePath, string outputDir, List<Color> palette)
        {
            Console.WriteLine("Extracting sprites from CSPRITE.DAT...");

            using (var fs = File.OpenRead(cspritePath))
            using (var br = new BinaryReader(fs))
            {
                int numSprites = br.ReadInt32();
                Console.WriteLine($"Found {numSprites} sprites in CSPRITE.DAT");

                var spriteOffsetsAndSizes = new List<(uint offset, uint size)>();
                for (int i = 0; i < numSprites; i++)
                {
                    uint offset = br.ReadUInt32();
                    uint size = br.ReadUInt32();
                    spriteOffsetsAndSizes.Add((offset, size));
                }

                for (int s = 0; s < numSprites; s++)
                {
                    var (offset, size) = spriteOffsetsAndSizes[s];
                    if (offset == 0 || size == 0) continue;

                    br.BaseStream.Seek(offset, SeekOrigin.Begin);
                    int numElementsA = br.ReadInt32();

                    var elementsA = new List<ElementA>();
                    for (int f = 0; f < numElementsA; f++)
                    {
                        byte[] elBytes = br.ReadBytes(0x4c);
                        int[] elInts = new int[19];
                        for (int k = 0; k < 19; k++)
                        {
                            elInts[k] = BitConverter.ToInt32(elBytes, k * 4);
                        }

                        elementsA.Add(new ElementA
                        {
                            Width = elInts[0],
                            Height = elInts[1],
                            XOffset = elInts[2],
                            YOffset = elInts[3],
                            WidthBlocks = elInts[16],
                            HeightBlocks = elInts[17],
                            StartTile = elInts[18]
                        });
                    }

                    int numElementsB = br.ReadInt32();
                    var descriptors = new List<int>();
                    for (int b = 0; b < numElementsB; b++)
                    {
                        int sz = br.ReadInt32();
                        br.ReadInt32(); // skip dummy pointer/value
                        descriptors.Add(sz);
                    }

                    var codeBlocks = new List<byte[]>();
                    for (int b = 0; b < numElementsB; b++)
                    {
                        codeBlocks.Add(br.ReadBytes(descriptors[b]));
                    }

                    // First pass: Emulate all frames to find the global bounding box relative to the anchor (0, 0)
                    int globalMinX = int.MaxValue;
                    int globalMaxX = int.MinValue;
                    int globalMinY = int.MaxValue;
                    int globalMaxY = int.MinValue;

                    var framePixels = new List<PixelInfo>[numElementsA];

                    for (int f = 0; f < numElementsA; f++)
                    {
                        framePixels[f] = new List<PixelInfo>();
                        var el = elementsA[f];

                        for (int row = 0; row < el.HeightBlocks; row++)
                        {
                            for (int col = 0; col < el.WidthBlocks; col++)
                            {
                                int tileIdx = el.StartTile + row * el.WidthBlocks + col;
                                if (tileIdx >= codeBlocks.Count) continue;

                                var (buf, isW) = EmulateBlock(codeBlocks[tileIdx], 352);

                                int relBlockX = el.XOffset + 16 + col * 32;
                                int relBlockY = el.YOffset + 16 + row * 16;

                                for (int y = 0; y < 16; y++)
                                {
                                    for (int x = 0; x < 32; x++)
                                    {
                                        int srcIdx = y * 352 + x;
                                        if (isW[srcIdx])
                                        {
                                            int pxX = relBlockX + x;
                                            int pxY = relBlockY + y;
                                            framePixels[f].Add(new PixelInfo { X = pxX, Y = pxY, ColorIndex = buf[srcIdx] });

                                            if (pxX < globalMinX) globalMinX = pxX;
                                            if (pxX > globalMaxX) globalMaxX = pxX;
                                            if (pxY < globalMinY) globalMinY = pxY;
                                            if (pxY > globalMaxY) globalMaxY = pxY;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Render frames if we found any pixels
                    if (globalMinX <= globalMaxX && globalMinY <= globalMaxY)
                    {
                        int canvasWidth = globalMaxX - globalMinX + 1;
                        int canvasHeight = globalMaxY - globalMinY + 1;

                        string spriteSubdir = Path.Combine(outputDir, $"Sprite_{s:D2}");
                        Directory.CreateDirectory(spriteSubdir);

                        for (int f = 0; f < numElementsA; f++)
                        {
                            using (var image = new Image<Rgba32>(canvasWidth, canvasHeight))
                            {
                                // Set transparent background
                                image.ProcessPixelRows(accessor =>
                                {
                                    for (int y = 0; y < accessor.Height; y++)
                                    {
                                        var row = accessor.GetRowSpan(y);
                                        for (int x = 0; x < row.Length; x++)
                                        {
                                            row[x] = new Rgba32(0, 0, 0, 0);
                                        }
                                    }
                                });

                                foreach (var pixel in framePixels[f])
                                {
                                    int canvasX = pixel.X - globalMinX;
                                    int canvasY = pixel.Y - globalMinY;
                                    if (canvasX >= 0 && canvasX < canvasWidth && canvasY >= 0 && canvasY < canvasHeight)
                                    {
                                        var color = palette[pixel.ColorIndex];
                                        image[canvasX, canvasY] = new Rgba32(color.R, color.G, color.B, 255);
                                    }
                                }

                                string framePath = Path.Combine(spriteSubdir, $"frame_{f:D3}.png");
                                image.SaveAsPng(framePath);
                            }
                        }

                        // Save anchor metadata: where the sprite anchor (0,0) sits within the canvas
                        // anchorX = -globalMinX means the anchor is at that pixel column in the canvas
                        // anchorY = -globalMinY means the anchor is at that pixel row in the canvas
                        var anchorMeta = new { anchorX = -globalMinX, anchorY = -globalMinY, canvasWidth, canvasHeight };
                        File.WriteAllText(
                            Path.Combine(spriteSubdir, "anchor.json"),
                            JsonSerializer.Serialize(anchorMeta));

                        Console.WriteLine($"Extracted Sprite {s}: {numElementsA} frames (aligned to {canvasWidth}x{canvasHeight})");
                    }
                }
            }
        }

        private static void ExtractTiles(string tilePath, string palPath, string outputDir)
        {
            Console.WriteLine("Extracting tiles from TILE.DAT...");

            byte[] palBytes = File.ReadAllBytes(palPath);

            using (var fs = File.OpenRead(tilePath))
            using (var br = new BinaryReader(fs))
            {
                int numResources = br.ReadInt32();
                Console.WriteLine($"Found {numResources} resources in TILE.DAT");

                var resourceOffsetsAndSizes = new List<(uint offset, uint size)>();
                for (int i = 0; i < numResources; i++)
                {
                    uint offset = br.ReadUInt32();
                    uint size = br.ReadUInt32();
                    resourceOffsetsAndSizes.Add((offset, size));
                }

                for (int r = 0; r < numResources; r++)
                {
                    var (offset, size) = resourceOffsetsAndSizes[r];
                    if (offset == 0 || size == 0) continue;

                    // Load palette for this resource dynamically
                    int palOffset = BitConverter.ToInt32(palBytes, 4 + r * 8);
                    byte[] palData = palBytes.Skip(palOffset + 10).Take(768).ToArray();
                    var palette = ColorHelper.ConvertBytesToRGB(palData, false);

                    br.BaseStream.Seek(offset, SeekOrigin.Begin);
                    int numTiles = br.ReadInt32();

                    var offsets = new List<uint>();
                    for (int t = 0; t < numTiles; t++)
                    {
                        offsets.Add(br.ReadUInt32());
                    }
                    uint totalCodeSize = size - 4 - (uint)numTiles * 4;
                    offsets.Add(totalCodeSize);

                    long codeDataStart = br.BaseStream.Position;

                    string resourceSubdir = Path.Combine(outputDir, $"Resource_{r:D2}");
                    Directory.CreateDirectory(resourceSubdir);

                    for (int t = 0; t < numTiles; t++)
                    {
                        uint tOffset = offsets[t];
                        uint tNext = offsets[t + 1];
                        int tSize = (int)(tNext - tOffset);

                        br.BaseStream.Seek(codeDataStart + tOffset, SeekOrigin.Begin);
                        byte[] codeBytes = br.ReadBytes(tSize);

                        var (buf, isW) = EmulateBlock(codeBytes, 336);

                        // Background tiles are 16x16 pixels
                        int widthPx = 16;
                        int heightPx = 16;

                        using (var image = new Image<Rgba32>(widthPx, heightPx))
                        {
                            image.ProcessPixelRows(accessor =>
                            {
                                for (int y = 0; y < accessor.Height; y++)
                                {
                                    var row = accessor.GetRowSpan(y);
                                    for (int x = 0; x < row.Length; x++)
                                    {
                                        row[x] = new Rgba32(0, 0, 0, 0);
                                    }
                                }
                            });

                            for (int y = 0; y < heightPx; y++)
                            {
                                for (int x = 0; x < widthPx; x++)
                                {
                                    int srcIdx = y * 352 + x;
                                    if (isW[srcIdx])
                                    {
                                        var color = palette[buf[srcIdx]];
                                        image[x, y] = new Rgba32(color.R, color.G, color.B, 255);
                                    }
                                }
                            }

                            string tileFile = Path.Combine(resourceSubdir, $"tile_{t:D3}.png");
                            image.SaveAsPng(tileFile);
                        }
                    }

                    Console.WriteLine($"Extracted TILE resource {r}: {numTiles} tiles");
                }
            }
        }

        // Object type ID -> (CSPRITE.DAT sprite index, start frame) from template table at DAT_00030d38
        // Start frame determined from animation table entries at each template's anim_ptr
        private static readonly Dictionary<int, (int spriteIdx, int startFrame)> ObjectTypeInfo =
            new Dictionary<int, (int, int)>
        {
            {0,  (11, 0)},   // player spawn marker
            {1,  (0,  52)},  // main character (idle)
            {2,  (14, 6)},   // lamp / object type A
            {3,  (14, 7)},   // lamp / object type B
            {4,  (14, 8)},   // object type C
            {5,  (14, 9)},   // object type D
            {6,  (14, 10)},  // lamp / object type E
            {7,  (14, 11)},  // object type F
            {8,  (14, 12)},  // object type G
            {9,  (15, 13)},  // crate type A
            {10, (15, 272)}, // crate type B
            {11, (15, 273)}, // crate type C
            {12, (15, 274)}, // crate type D
            {13, (15, 275)}, // crate type E
            {14, (8,  291)}, // enemy beetle
            {15, (8,  291)}, // enemy beetle variant
            {16, (8,  291)}, // enemy beetle variant 2
            {17, (1,  310)}, // mech/robo ally
            {18, (1,  310)}, // mech/robo ally variant
            {19, (0,  0)},   // no anim
            {20, (0,  0)},   // no anim
            {21, (4,  354)}, // powerup / item
            {22, (3,  355)}, // powerup / item B
            {23, (2,  356)}, // powerup / item C
            {24, (2,  357)}, // powerup / item D
            {25, (6,  332)}, // boss
            {26, (5,  358)}, // powerup / item E
            {27, (27, 359)}, // enemy type A
            {28, (26, 363)}, // enemy type B
            {29, (25, 373)}, // enemy type C
            {30, (24, 387)}, // enemy type D
            {31, (23, 389)}, // enemy type E
            {32, (22, 408)}, // boss variant
            {33, (21, 427)}, // item/object X
            {34, (21, 428)}, // item/object Y
            {35, (21, 429)}, // item/object Z
            {36, (29, 430)}, // object type 36
        };

        private static int GetMapResourceIndex(int mapIndex)
        {
            return mapIndex < 5 ? mapIndex : mapIndex - 4;
        }

        private static void ExtractMaps(string mapPath, string tilesDir, string spritesDir, string outputDir)
        {
            Console.WriteLine("Extracting maps from MAP.DAT...");

            // Pre-load the specific start frame for each object type (not always frame 0)
            // Also load anchor.json so we can position sprites correctly
            var spriteFrameCache = new Dictionary<int, SixLabors.ImageSharp.Image<Rgba32>?>(); // key = objId
            var spriteAnchorX = new Dictionary<int, int>(); // anchor col in canvas, keyed by objId
            var spriteAnchorY = new Dictionary<int, int>(); // anchor row in canvas, keyed by objId

            foreach (var kv in ObjectTypeInfo)
            {
                int objId = kv.Key;
                var (spriteIdx, startFrame) = kv.Value;

                string spriteDir = Path.Combine(spritesDir, $"Sprite_{spriteIdx:D2}");
                string frameFile = Path.Combine(spriteDir, $"frame_{startFrame:D3}.png");
                spriteFrameCache[objId] = File.Exists(frameFile)
                    ? SixLabors.ImageSharp.Image.Load<Rgba32>(frameFile)
                    : null;

                // Read anchor offset from metadata
                string anchorFile = Path.Combine(spriteDir, "anchor.json");
                if (File.Exists(anchorFile))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(anchorFile));
                    spriteAnchorX[objId] = doc.RootElement.GetProperty("anchorX").GetInt32();
                    spriteAnchorY[objId] = doc.RootElement.GetProperty("anchorY").GetInt32();
                }
                else
                {
                    // Fallback: no anchor info, treat top-left as anchor
                    spriteAnchorX[objId] = 0;
                    spriteAnchorY[objId] = 0;
                }
            }

            using (var fs = File.OpenRead(mapPath))
            using (var br = new BinaryReader(fs))
            {
                int numMaps = br.ReadInt32();
                Console.WriteLine($"Found {numMaps} maps in MAP.DAT");

                var mapOffsetsAndSizes = new List<(uint offset, uint size)>();
                for (int i = 0; i < numMaps; i++)
                {
                    uint offset = br.ReadUInt32();
                    uint size = br.ReadUInt32();
                    mapOffsetsAndSizes.Add((offset, size));
                }

                for (int m = 0; m < numMaps; m++)
                {
                    var (offset, size) = mapOffsetsAndSizes[m];
                    if (offset == 0 || size == 0) continue;

                    br.BaseStream.Seek(offset, SeekOrigin.Begin);
                    int width = br.ReadInt32();
                    int height = br.ReadInt32();

                    var cells = new uint[width * height];
                    for (int c = 0; c < cells.Length; c++)
                    {
                        cells[c] = br.ReadUInt32();
                    }

                    int resIdx = GetMapResourceIndex(m);
                    string resTileDir = Path.Combine(tilesDir, $"Resource_{resIdx:D2}");

                    if (!Directory.Exists(resTileDir))
                    {
                        Console.WriteLine($"Warning: Tile resource directory {resTileDir} is missing. Skipping map {m}.");
                        continue;
                    }

                    int mapWidthPx = width * 16;
                    int mapHeightPx = height * 16;

                    // --- Layer 1: Background tile layer ---
                    using var bgLayer = new SixLabors.ImageSharp.Image<Rgba32>(mapWidthPx, mapHeightPx);
                    var loadedTiles = new Dictionary<int, SixLabors.ImageSharp.Image<Rgba32>?>();

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            uint cellVal = cells[y * width + x];
                            int tileIdx = (int)(cellVal & 0xFFFF);

                            if (!loadedTiles.TryGetValue(tileIdx, out var tileImg))
                            {
                                string tileFile = Path.Combine(resTileDir, $"tile_{tileIdx:D3}.png");
                                tileImg = File.Exists(tileFile)
                                    ? SixLabors.ImageSharp.Image.Load<Rgba32>(tileFile)
                                    : null;
                                loadedTiles[tileIdx] = tileImg;
                            }

                            if (tileImg != null)
                            {
                                bgLayer.Mutate(ctx => ctx.DrawImage(tileImg,
                                    new SixLabors.ImageSharp.Point(x * 16, y * 16), 1.0f));
                            }
                        }
                    }

                    foreach (var img in loadedTiles.Values) img?.Dispose();

                    // --- Layer 2: Foreground sprite overlay ---
                    using var fgLayer = new SixLabors.ImageSharp.Image<Rgba32>(mapWidthPx, mapHeightPx);

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            uint cellVal = cells[y * width + x];
                            // Object ID encoded in bits [21:16] | bits [27:26] (shifted left by 4)
                            int objId = (int)((cellVal & 0x003F0000) >> 16) |
                                        (int)((cellVal & 0x0C000000) >> 20);

                            if (objId == 0) continue;
                            if (!spriteFrameCache.TryGetValue(objId, out var spriteFrame) || spriteFrame == null) continue;

                            // The sprite anchor (0,0) maps to the object's feet/origin.
                            // In map space the anchor sits at the bottom-left of the 16x16 cell:
                            //   anchorMapX = x * 16  (left edge of cell)
                            //   anchorMapY = (y + 1) * 16  (bottom edge of cell)
                            // So the top-left of the sprite canvas on the map is:
                            //   pasteX = anchorMapX - spriteAnchorX[objId]
                            //   pasteY = anchorMapY - spriteAnchorY[objId]
                            int anchorMapX = x * 16;
                            int anchorMapY = (y + 1) * 16;
                            int pasteX = anchorMapX - spriteAnchorX.GetValueOrDefault(objId, 0);
                            int pasteY = anchorMapY - spriteAnchorY.GetValueOrDefault(objId, 0);

                            // Clamp so ImageSharp doesn't throw for out-of-bounds pastes
                            int drawX = Math.Max(0, pasteX);
                            int drawY = Math.Max(0, pasteY);
                            int cropLeft = drawX - pasteX;
                            int cropTop = drawY - pasteY;
                            int cropW = Math.Min(spriteFrame.Width - cropLeft, mapWidthPx - drawX);
                            int cropH = Math.Min(spriteFrame.Height - cropTop, mapHeightPx - drawY);

                            if (cropW <= 0 || cropH <= 0) continue;

                            using var cropped = spriteFrame.Clone(ctx =>
                                ctx.Crop(new SixLabors.ImageSharp.Rectangle(cropLeft, cropTop, cropW, cropH)));

                            fgLayer.Mutate(ctx => ctx.DrawImage(cropped,
                                new SixLabors.ImageSharp.Point(drawX, drawY), 1.0f));
                        }
                    }

                    // Save background layer
                    string bgFile = Path.Combine(outputDir, $"map_{m:D2}_bg.png");
                    bgLayer.SaveAsPng(bgFile);

                    // Save foreground overlay layer
                    string fgFile = Path.Combine(outputDir, $"map_{m:D2}_fg.png");
                    fgLayer.SaveAsPng(fgFile);

                    // Save composite (bg + fg)
                    using var composite = bgLayer.Clone();
                    composite.Mutate(ctx => ctx.DrawImage(fgLayer, new SixLabors.ImageSharp.Point(0, 0), 1.0f));
                    string compositeFile = Path.Combine(outputDir, $"map_{m:D2}_composite.png");
                    composite.SaveAsPng(compositeFile);

                    Console.WriteLine($"Extracted Map {m}: {width}x{height} -> bg, fg, composite saved.");
                }
            }

            // Dispose cached sprite images (keyed by objId)
            foreach (var img in spriteFrameCache.Values) img?.Dispose();
        }
    }
}
