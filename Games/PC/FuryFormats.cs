using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Games.Generic;
using SixLabors.ImageSharp;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;
using Rectangle = System.Drawing.Rectangle;

namespace ExtractCLUT.Games.PC
{
    public static class FuryFormats
    {
        private static readonly byte _MaxWidth = 78;
        private static readonly byte _MaxHeight = 51;
        public static byte[] DecompressRle(string filePath)
        {
            using var compReader = new BinaryReader(File.OpenRead(filePath));
            char[] magic = compReader.ReadChars(4); // skip header
                                                    // if first 3 chars != "byt", return empty array
            if (new string(magic, 0, 3) != "byt") return Array.Empty<byte>();
            // read until count is 0 
            var decompressedData = new List<byte>();
            var count = compReader.ReadUInt16();
            while (count > 0)
            {
                if (count > 0x7d00)
                {
                    count -= 0x7d00;
                    var value = compReader.ReadByte();
                    decompressedData.AddRange(Enumerable.Repeat(value, count));
                }
                else
                {
                    decompressedData.AddRange(compReader.ReadBytes(count));
                }
                count = compReader.ReadUInt16();
            }
            return decompressedData.ToArray();
        }

        public static void ExtractTilesAsPng(string imagePath, string outputDirectory, int tileSize = 16)
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            using var sourceImage = new Bitmap(imagePath);

            int tilesWide = sourceImage.Width / tileSize;
            int tilesHigh = sourceImage.Height / tileSize;

            for (int row = 0; row < tilesHigh; row++)
            {
                for (int col = 0; col < tilesWide; col++)
                {
                    // Create a new bitmap for this tile
                    using var tileBitmap = new Bitmap(tileSize, tileSize, PixelFormat.Format32bppArgb);
                    using var graphics = Graphics.FromImage(tileBitmap);

                    // Define source and destination rectangles
                    var sourceRect = new Rectangle(col * tileSize, row * tileSize, tileSize, tileSize);
                    var destRect = new Rectangle(0, 0, tileSize, tileSize);

                    // Copy the tile from source image
                    graphics.DrawImage(sourceImage, destRect, sourceRect, GraphicsUnit.Pixel);

                    // Save as PNG with col_row naming convention
                    string fileName = $"tile_{col:D3}_{row:D3}.png";
                    string filePath = Path.Combine(outputDirectory, fileName);
                    tileBitmap.Save(filePath, ImageFormat.Png);
                }
            }
        }


        public static void AssembleLevelFromTileMap(DataFile dataFile, string tilesDirectory, string spriteDirectory, string outputPath, int tileSize = 16)
        {
            int levelWidth = _MaxWidth * tileSize;
            int levelHeight = _MaxHeight * tileSize;
            using var levelBitmap = new Bitmap(levelWidth, levelHeight, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(levelBitmap);

            graphics.Clear(Color.Transparent);

            // Draw tiles first (background layer)
            int dataIndex = 0;
            for (int mapY = 0; mapY < _MaxHeight; mapY++)
            {
                for (int mapX = 0; mapX < _MaxWidth; mapX++)
                {
                    if (dataIndex + 1 < dataFile.MapData.Length)
                    {
                        byte tileCol = dataFile.MapData[dataIndex];
                        byte tileRow = dataFile.MapData[dataIndex + 1];
                        dataIndex += 2;

                        string tileFileName = $"tile_{tileCol:D3}_{tileRow:D3}.png";
                        string tilePath = Path.Combine(tilesDirectory, tileFileName);

                        if (File.Exists(tilePath))
                        {
                            using var tileBitmap = new Bitmap(tilePath);
                            int destX = mapX * tileSize;
                            int destY = mapY * tileSize;
                            graphics.DrawImage(tileBitmap, destX, destY, tileSize, tileSize);
                        }
                    }
                }
            }
            // calc final cropped width and height based on dataFile.MapWidth and MapHeight
            int finalWidth = dataFile.MapWidth * tileSize;
            int finalHeight = dataFile.MapHeight * tileSize;
            // crop levelBitmap to finalWidth and finalHeight
            using var finalBitmap = new Bitmap(finalWidth, finalHeight);
            using (var g = Graphics.FromImage(finalBitmap))
            {
                g.DrawImage(levelBitmap, 0, 0, new Rectangle(0, 0, finalWidth, finalHeight), GraphicsUnit.Pixel);
            }
            finalBitmap.Save(outputPath.Replace(".png", "_base.png"), ImageFormat.Png);
            // Load sprite image file
            string spriteFileName = $"SPR{(dataFile.TileSetId + 1):D2}{(char)('A' + dataFile.SpriteFileIndex)}.LBM";
            string spritePath = Path.Combine(spriteDirectory, spriteFileName);

            if (File.Exists(spritePath))
            {
                using var spriteImage = LoadLbmImage(spritePath);
                if (spriteImage != null)
                {
                    // Draw sprites by layer order: Background (1), Middle (0), Foreground (2)
                    var layerOrder = new[] { 1, 0, 2 };

                    foreach (var layer in layerOrder)
                    {
                        foreach (var sprite in dataFile.Sprites.Where(s => s.Layer == layer))
                        {
                            DrawSpriteInitialFrame(graphics, (Bitmap)spriteImage, sprite, tileSize);
                        }
                    }
                }
            }
            // calc final cropped width and height based on dataFile.MapWidth and MapHeight
            
            // crop levelBitmap to finalWidth and finalHeight
            var finalBitmapWithSprites = new Bitmap(finalWidth, finalHeight);
            using (var g = Graphics.FromImage(finalBitmapWithSprites))
            {
                g.DrawImage(levelBitmap, 0, 0, new Rectangle(0, 0, finalWidth, finalHeight), GraphicsUnit.Pixel);
            }
            finalBitmapWithSprites.Save(outputPath.Replace(".png", "_final.png"), ImageFormat.Png);
        }

        private static Image? LoadLbmImage(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var converter = new LbmConverter();
                    var stream = File.OpenRead(filePath);
                    var isImage = converter.ConvertToImage(stream);
                    var pngStream = new MemoryStream();
                    isImage?.SaveAsPngAsync(pngStream);
                    // convert ImageSharp image to System.Drawing.Image
                    return isImage != null ? Image.FromStream(pngStream) : null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load sprite image {filePath}: {ex.Message}");
            }

            return null;
        }
        private static void DrawSpriteInitialFrame(Graphics graphics, Bitmap spriteImage, SpriteData sprite, int tileSize)
        {
            // Get the first state's first frame
            if (sprite.States.Count > 0 && sprite.States[0].Frames.Count > 0)
            {
                var initialFrame = sprite.States[0].Frames[0];
                var initialState = sprite.States[0];

                // Calculate frame dimensions
                int frameWidth = (initialFrame.Right - 8) - initialFrame.Left;
                int frameHeight = (initialFrame.Bottom + 1) - initialFrame.Top;

                if (frameWidth <= 0 || frameHeight <= 0) return;

                // Source rectangle in the sprite image
                var sourceRect = new Rectangle(
                    initialFrame.Left,
                    initialFrame.Top,
                    frameWidth,
                    frameHeight
                );

                // Destination position on the level (convert from tile coordinates to pixel coordinates)
                int destX = initialState.Left;
                int destY = initialState.Top;

                // Check if we need to apply a mask
                if (sprite.Mask != 0)
                {
                    // Mask is located directly below the sprite
                    var maskRect = new Rectangle(
                        initialFrame.Left,
                        initialFrame.Bottom + 1, // Mask starts where sprite ends
                        frameWidth,
                        frameHeight
                    );

                    // Create masked sprite
                    using var maskedSprite = CreateMaskedSprite(spriteImage, sourceRect, maskRect);
                    if (maskedSprite != null)
                    {
                        graphics.DrawImage(maskedSprite, destX, destY);
                    }
                }
                else
                {
                    // Draw sprite directly without mask
                    var destRect = new Rectangle(destX, destY, frameWidth, frameHeight);
                    graphics.DrawImage(spriteImage, destRect, sourceRect, GraphicsUnit.Pixel);
                }
            }
        }

        private static Bitmap? CreateMaskedSprite(Bitmap spriteImage, Rectangle spriteRect, Rectangle maskRect)
        {
            try
            {
                var result = new Bitmap(spriteRect.Width, spriteRect.Height, PixelFormat.Format32bppArgb);

                using (var g = Graphics.FromImage(result))
                {
                    g.Clear(Color.Transparent);

                    // Apply mask pixel by pixel
                    for (int y = 0; y < spriteRect.Height; y++)
                    {
                        for (int x = 0; x < spriteRect.Width; x++)
                        {
                            if (spriteRect.X + x < spriteImage.Width && spriteRect.Y + y < spriteImage.Height &&
                                maskRect.X + x < spriteImage.Width && maskRect.Y + y < spriteImage.Height)
                            {
                                var spritePixel = spriteImage.GetPixel(spriteRect.X + x, spriteRect.Y + y);
                                var maskPixel = spriteImage.GetPixel(maskRect.X + x, maskRect.Y + y);

                                // If mask pixel is not black, make sprite pixel transparent
                                if (maskPixel.R > 128 || maskPixel.G > 128 || maskPixel.B > 128) // White/bright areas
                                {
                                    result.SetPixel(x, y, spritePixel);
                                }
                            }
                        }
                    }
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        public static void AssembleLevelFromTileMap(byte[] tileMapData, string tilesDirectory, string outputPath, int mapWidth = 78, int mapHeight = 51, int tileSize = 16)
        {
            // Convert flat array to 2D array structure
            // Assuming the data is stored as [col][row pairs] sequentially
            int levelWidth = mapWidth * tileSize;
            int levelHeight = mapHeight * tileSize;
            using var levelBitmap = new Bitmap(levelWidth, levelHeight, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(levelBitmap);

            graphics.Clear(Color.Transparent);

            int dataIndex = 0;
            for (int mapY = 0; mapY < mapHeight; mapY++)
            {
                for (int mapX = 0; mapX < mapWidth; mapX++)
                {
                    if (dataIndex + 1 < tileMapData.Length)
                    {
                        byte tileCol = tileMapData[dataIndex];
                        byte tileRow = tileMapData[dataIndex + 1];
                        dataIndex += 2;

                        string tileFileName = $"tile_{tileCol:D3}_{tileRow:D3}.png";
                        string tilePath = Path.Combine(tilesDirectory, tileFileName);

                        if (File.Exists(tilePath))
                        {
                            using var tileBitmap = new Bitmap(tilePath);

                            int destX = mapX * tileSize;
                            int destY = mapY * tileSize;

                            graphics.DrawImage(tileBitmap, destX, destY, tileSize, tileSize);
                        }
                    }
                }
            }

            levelBitmap.Save(outputPath, ImageFormat.Png);
        }
    }

    public class DataFile
    {
        private readonly byte _MaxWidth = 78;
        private readonly byte _MaxHeight = 51;
        public ushort MapWidth { get; set; }
        public ushort MapHeight { get; set; }
        public byte[] MapData { get; set; } = Array.Empty<byte>();
        public ushort TileSetId { get; set; }
        public ushort PlayerStartLeft { get; set; }
        public ushort PlayerStartTop { get; set; }
        public ushort FGPaletteIndex { get; set; }
        public List<ExitData> Exits { get; set; } = new();
        public List<WaterData> Water { get; set; } = new();
        public List<TeleportData> Teleports { get; set; } = new();
        public List<NonStickData> NonSticks { get; set; } = new();
        public List<AcidData> Acids { get; set; } = new();
        public List<DangerData> Dangers { get; set; } = new(); // 20 entries
        public List<SpriteData> Sprites { get; set; } = new(); // 10 entries
        public ushort BlueStart { get; set; }
        public ushort GreenStart { get; set; }
        public ushort RedStart { get; set; }
        public ushort YellowStart { get; set; }
        public byte[] Reserved { get; set; } = Array.Empty<byte>(); // 12 bytes
        public List<FieldData> RedFields { get; set; } = new();
        public List<FieldData> GreenFields { get; set; } = new();
        public List<FieldData> YellowFields { get; set; } = new();
        public List<FieldData> BlueFields { get; set; } = new();
        public List<WaterData> Water2 { get; set; } = new();
        public byte[] Unused { get; set; } = Array.Empty<byte>(); // 10 bytes
        public byte WaterPaletteIndex { get; set; }
        public byte AirPaletteIndex { get; set; }
        public ushort Time { get; set; }
        public List<CurrentData> Currents { get; set; } = new();
        public byte MotePaletteIndex { get; set; }
        public byte UnknownByte { get; set; }
        public ushort SpriteFileIndex { get; set; } // 0 = A, 1 = B, etc
        public List<ExitReturnData> ExitReturns { get; set; } = new();
        public ushort[] ExitGraphics { get; set; } = Array.Empty<ushort>(); // up to 5 entries
        public byte UnknownByte2 { get; set; }

        public DataFile()
        {
            Exits = new List<ExitData>();
            Water = new List<WaterData>();
            Teleports = new List<TeleportData>();
            NonSticks = new List<NonStickData>();
            Acids = new List<AcidData>();
            Dangers = new List<DangerData>();
            Sprites = new List<SpriteData>();
            RedFields = new List<FieldData>();
            GreenFields = new List<FieldData>();
            YellowFields = new List<FieldData>();
            BlueFields = new List<FieldData>();
            Water2 = new List<WaterData>();
            Currents = new List<CurrentData>();
            ExitReturns = new List<ExitReturnData>();
            ExitGraphics = new ushort[5];
        }

        public DataFile(string filePath) : this()
        {
            using var reader = new BinaryReader(File.OpenRead(filePath));
            MapWidth = reader.ReadUInt16();
            MapHeight = reader.ReadUInt16();
            MapData = reader.ReadBytes(_MaxWidth * _MaxHeight * 2);
            TileSetId = reader.ReadUInt16();
            PlayerStartLeft = reader.ReadUInt16();
            PlayerStartTop = reader.ReadUInt16();
            FGPaletteIndex = reader.ReadUInt16();

            // Read Exits
            for (int i = 0; i < 5; i++)
            {
                var exit = new ExitData
                {
                    Left = reader.ReadUInt16(),
                    Top = reader.ReadUInt16(),
                    Destination = reader.ReadUInt16()
                };
                Exits.Add(exit);
            }

            // Read Water regions
            for (int i = 0; i < 5; i++)
            {
                var water = new WaterData
                {
                    Left = reader.ReadUInt16(),
                    Top = reader.ReadUInt16(),
                    Right = reader.ReadUInt16(),
                    Bottom = reader.ReadUInt16()
                };
                Water.Add(water);
            }

            // Read Teleports
            for (int i = 0; i < 5; i++)
            {
                var teleport = new TeleportData
                {
                    SrcX = reader.ReadUInt16(),
                    SrcY = reader.ReadUInt16(),
                    DestX = reader.ReadUInt16(),
                    DestY = reader.ReadUInt16()
                };
                Teleports.Add(teleport);
            }

            // Read Non-Stick regions
            for (int i = 0; i < 5; i++)
            {
                var nonStick = new NonStickData
                {
                    Left = reader.ReadUInt16(),
                    Top = reader.ReadUInt16(),
                    Right = reader.ReadUInt16(),
                    Bottom = reader.ReadUInt16()
                };
                NonSticks.Add(nonStick);
            }

            // Read Acid regions
            for (int i = 0; i < 5; i++)
            {
                var acid = new AcidData
                {
                    Left = reader.ReadUInt16(),
                    Top = reader.ReadUInt16(),
                    Right = reader.ReadUInt16(),
                    Bottom = reader.ReadUInt16()
                };
                Acids.Add(acid);
            }

            // Read Danger regions
            for (int i = 0; i < 20; i++)
            {
                var danger = new DangerData
                {
                    Left = reader.ReadUInt16(),
                    Top = reader.ReadUInt16(),
                    Right = reader.ReadUInt16(),
                    Bottom = reader.ReadUInt16()
                };
                Dangers.Add(danger);
            }
            // Read Sprites
            for (int i = 0; i < 10; i++)
            {
                var sprite = new SpriteData
                {
                    Layer = reader.ReadByte(),
                    Malevolence = reader.ReadByte(),
                    Unknown = reader.ReadUInt16(),
                    Mask = reader.ReadUInt16(),
                    CleanUp = reader.ReadUInt16(),
                    Strength = reader.ReadUInt16(),
                    BlastArea = reader.ReadUInt16(),
                    Active = reader.ReadUInt16(),
                    Unknown2 = reader.ReadUInt16(),
                    EntryRegion = new RegionData
                    {
                        Left = reader.ReadUInt16(),
                        Top = reader.ReadUInt16(),
                        Right = reader.ReadUInt16(),
                        Bottom = reader.ReadUInt16()
                    },
                    ExitRegion = new RegionData
                    {
                        Left = reader.ReadUInt16(),
                        Top = reader.ReadUInt16(),
                        Right = reader.ReadUInt16(),
                        Bottom = reader.ReadUInt16()
                    },
                    UnknownData = reader.ReadBytes(72),
                    FireRate = reader.ReadUInt16(),
                    FireType = reader.ReadUInt16()
                };
                // Read States
                for (int j = 0; j < 10; j++)
                {
                    var state = new StateData
                    {
                        Left = reader.ReadUInt16(),
                        Top = reader.ReadUInt16(),
                        DestinationState = reader.ReadUInt16(),
                        Speed = reader.ReadUInt16(),
                        MoveType = reader.ReadByte(),
                        DestinationWaterState = reader.ReadByte(),
                        Gravity = reader.ReadByte(),
                        Current = reader.ReadByte(),
                        ActivateSprite = reader.ReadUInt16(),
                        EntryTrigger = new TriggerData
                        {
                            TargetState = reader.ReadUInt16(),
                            Left = reader.ReadUInt16(),
                            Top = reader.ReadUInt16(),
                            Right = reader.ReadUInt16(),
                            Bottom = reader.ReadUInt16()
                        },
                        ExitTrigger = new TriggerData
                        {
                            TargetState = reader.ReadUInt16(),
                            Left = reader.ReadUInt16(),
                            Top = reader.ReadUInt16(),
                            Right = reader.ReadUInt16(),
                            Bottom = reader.ReadUInt16()
                        },
                        SpriteEntryTrigger = new TriggerData
                        {
                            TargetState = reader.ReadUInt16(),
                            Left = reader.ReadUInt16(),
                            Top = reader.ReadUInt16(),
                            Right = reader.ReadUInt16(),
                            Bottom = reader.ReadUInt16()
                        },
                        SpriteExitTrigger = new TriggerData
                        {
                            TargetState = reader.ReadUInt16(),
                            Left = reader.ReadUInt16(),
                            Top = reader.ReadUInt16(),
                            Right = reader.ReadUInt16(),
                            Bottom = reader.ReadUInt16()
                        },
                        Destroy = reader.ReadByte(),
                        Bounce = reader.ReadByte(),
                        EmptyWater = reader.ReadUInt16(),
                        FillWater = reader.ReadUInt16(),
                        Unknown = reader.ReadBytes(4),
                        WaterTriggerLeft = reader.ReadUInt16(),
                        WaterTriggerTop = reader.ReadUInt16(),
                        WaterTriggerRight = reader.ReadUInt16()
                    };
                    // Read Frames
                    for (int k = 0; k < 10; k++)
                    {
                        var frame = new FrameData
                        {
                            Left = reader.ReadUInt16(),
                            Top = reader.ReadUInt16(),
                            Right = reader.ReadUInt16(),
                            Bottom = reader.ReadUInt16()
                        };
                        state.Frames.Add(frame);
                    }
                    state.AnimSpeed = reader.ReadUInt16();
                    state.Cycle = reader.ReadUInt16();
                    state.CycleCount = reader.ReadByte();
                    state.AnimTriggerState = reader.ReadByte();
                    state.WaterTriggerBottom = reader.ReadUInt16();
                    sprite.States.Add(state);
                }
                Sprites.Add(sprite);
            }
            BlueStart = reader.ReadUInt16();
            GreenStart = reader.ReadUInt16();
            RedStart = reader.ReadUInt16();
            YellowStart = reader.ReadUInt16();
            Reserved = reader.ReadBytes(12);
            // Read Red Fields
            for (int i = 0; i < 5; i++)
            {
                var field = new FieldData
                {
                    Left = reader.ReadUInt16(),
                    Top = reader.ReadUInt16(),
                    Right = reader.ReadUInt16(),
                    Bottom = reader.ReadUInt16()
                };
                RedFields.Add(field);
            }
            // Read Green Fields
            for (int i = 0; i < 5; i++)
            {
                var field = new FieldData
                {
                    Left = reader.ReadUInt16(),
                    Top = reader.ReadUInt16(),
                    Right = reader.ReadUInt16(),
                    Bottom = reader.ReadUInt16()
                };
                GreenFields.Add(field);
            }
            // Read Yellow Fields
            for (int i = 0; i < 5; i++)
            {
                var field = new FieldData
                {
                    Left = reader.ReadUInt16(),
                    Top = reader.ReadUInt16(),
                    Right = reader.ReadUInt16(),
                    Bottom = reader.ReadUInt16()
                };
                YellowFields.Add(field);
            }
            // Read Blue Fields
            for (int i = 0; i < 5; i++)
            {
                var field = new FieldData
                {
                    Left = reader.ReadUInt16(),
                    Top = reader.ReadUInt16(),
                    Right = reader.ReadUInt16(),
                    Bottom = reader.ReadUInt16()
                };
                BlueFields.Add(field);
            }
            // Read second set of Water regions
            for (int i = 0; i < 5; i++)
            {
                var water = new WaterData
                {
                    Left = reader.ReadUInt16(),
                    Top = reader.ReadUInt16(),
                    Right = reader.ReadUInt16(),
                    Bottom = reader.ReadUInt16()
                };
                Water2.Add(water);
            }
            Unused = reader.ReadBytes(10);
            WaterPaletteIndex = reader.ReadByte();
            AirPaletteIndex = reader.ReadByte();
            Time = reader.ReadUInt16();
            if (reader.BaseStream.Length == reader.BaseStream.Position)
            {
                // No more data
                return;
            }
            // Read Currents
            for (int i = 0; i < 5; i++)
            {
                var current = new CurrentData
                {
                    Left = reader.ReadUInt16(),
                    Top = reader.ReadUInt16(),
                    Right = reader.ReadUInt16(),
                    Bottom = reader.ReadUInt16(),
                    Flags = reader.ReadUInt16()
                };
                Currents.Add(current);
            }
            if (reader.BaseStream.Length == reader.BaseStream.Position)
            {
                // No more data
                return;
            }
            MotePaletteIndex = reader.ReadByte();
            UnknownByte = reader.ReadByte();
            if (reader.BaseStream.Length == reader.BaseStream.Position)
            {
                // No more data
                return;
            }
            SpriteFileIndex = reader.ReadUInt16();
            // Read Exit Returns                            
            for (int i = 0; i < 5; i++)
            {
                var exitReturn = new ExitReturnData
                {
                    Left = reader.ReadUInt16(),
                    Top = reader.ReadUInt16()
                };
                ExitReturns.Add(exitReturn);
            }
            // Read Exit Graphics
            for (int i = 0; i < 5; i++)
            {
                ExitGraphics[i] = reader.ReadUInt16();
            }
            if (reader.BaseStream.Length == reader.BaseStream.Position)
            {
                // No more data
                return;
            }
            UnknownByte2 = reader.ReadByte();
        }
    }

    public class ExitData
    {
        public ushort Left { get; set; }
        public ushort Top { get; set; }
        public ushort Destination { get; set; }
    }

    public class WaterData
    {
        public ushort Left { get; set; }
        public ushort Top { get; set; }
        public ushort Right { get; set; }
        public ushort Bottom { get; set; }
    }

    public class TeleportData
    {
        public ushort SrcX { get; set; }
        public ushort SrcY { get; set; }
        public ushort DestX { get; set; }
        public ushort DestY { get; set; }
    }

    public class NonStickData
    {
        public ushort Left { get; set; }
        public ushort Top { get; set; }
        public ushort Right { get; set; }
        public ushort Bottom { get; set; }
    }

    public class AcidData
    {
        public ushort Left { get; set; }
        public ushort Top { get; set; }
        public ushort Right { get; set; }
        public ushort Bottom { get; set; }
    }

    public class DangerData
    {
        public ushort Left { get; set; }
        public ushort Top { get; set; }
        public ushort Right { get; set; }
        public ushort Bottom { get; set; }
    }

    public class SpriteData
    {
        public byte Layer { get; set; } // 0 = Middle, 1 = BG, 2 = FG
        // Bit-0: sprite will kill player in ? form
        // Bit-1: sprite will kill player in yellow form
        // Bit-2: sprite will kill player in ? form
        // Bit-3: sprite will kill player in ? form.
        public byte Malevolence { get; set; } // 0 = Good, 1 = Evil
        public ushort Unknown { get; set; }
        public ushort Mask { get; set; } // Sprite requires a mask
        public ushort CleanUp { get; set; } // Sprite needs to be cleaned up
        public ushort Strength { get; set; } // How much damage the sprite can take. 0 - inf, otherwise 1-127
        public ushort BlastArea { get; set; } // Radius of blast when sprite is destroyed
        public ushort Active { get; set; }
        public ushort Unknown2 { get; set; }
        public RegionData EntryRegion { get; set; } = new();
        public RegionData ExitRegion { get; set; } = new();
        public byte[] UnknownData { get; set; } = Array.Empty<byte>(); // 72 bytes
        public ushort FireRate { get; set; } // How often the sprite fires projectiles. 0 = none, 1-5 = low-high.
        public ushort FireType { get; set; } // how are projectiles fired. 1 = All directions slowly, 2 = Right, 3 = Left, 4 = All directions medium, 5 = All directions fast.
        public List<StateData> States { get; set; } = new(); // up to 10 states
    }

    public class StateData
    {
        public ushort Left { get; set; }
        public ushort Top { get; set; }
        public ushort DestinationState { get; set; }
        public ushort Speed { get; set; } // 0 = None, 1-5 = slow to fast
        public byte MoveType { get; set; } // 0 = horizontal then vertical, 1 = as the crow flies, 2 = just track player vertically, no sideways movement, 3 = just track player horizontally, no up/down movement, 4 = track player, 5 = fast move??, 6 = None.
        public byte DestinationWaterState { get; set; }
        public byte Gravity { get; set; } // If 1, the sprite falls when unsupported.
        public byte Current { get; set; } // Control currents. Bits 0-4 specify currents affected, Bit-5 turns currents off, Bit-6 turns currents on.
        public ushort ActivateSprite { get; set; } // Sprite to activate
        public TriggerData EntryTrigger { get; set; } = new();
        public TriggerData ExitTrigger { get; set; } = new();
        public TriggerData SpriteEntryTrigger { get; set; } = new();
        public TriggerData SpriteExitTrigger { get; set; } = new();
        public byte Destroy { get; set; }
        public byte Bounce { get; set; }
        public ushort EmptyWater { get; set; } // Bits 8-15 speed of emptying, Bits 0-7 number of water region to empty.
        public ushort FillWater { get; set; } // Bits 8-15 speed of filling, Bits 0-7 number of water region to fill.
        public byte[] Unknown { get; set; } = Array.Empty<byte>(); // 4 bytes
        public ushort WaterTriggerLeft { get; set; }
        public ushort WaterTriggerTop { get; set; }
        public ushort WaterTriggerRight { get; set; }
        public List<FrameData> Frames { get; set; } = new();
        public ushort AnimSpeed { get; set; }
        public ushort Cycle { get; set; } // Does the anim cycle repeat. 0 = no, 1 = yes.
        public byte CycleCount { get; set; } // Frames before changing state
        public byte AnimTriggerState { get; set; } // State to change to after anim completes
        public ushort WaterTriggerBottom { get; set; }
    }

    public class FrameData
    {
        public ushort Left { get; set; }
        public ushort Top { get; set; }
        public ushort Right { get; set; }
        public ushort Bottom { get; set; }
    }

    public class TriggerData
    {
        public ushort TargetState { get; set; }
        public ushort Left { get; set; }
        public ushort Top { get; set; }
        public ushort Right { get; set; }
        public ushort Bottom { get; set; }
    }

    public class RegionData
    {
        public ushort Left { get; set; }
        public ushort Top { get; set; }
        public ushort Right { get; set; }
        public ushort Bottom { get; set; }
    }

    public class FieldData
    {
        public ushort Left { get; set; }
        public ushort Top { get; set; }
        public ushort Right { get; set; }
        public ushort Bottom { get; set; }
    }

    public class CurrentData
    {
        public ushort Left { get; set; }
        public ushort Top { get; set; }
        public ushort Right { get; set; }
        public ushort Bottom { get; set; }

        // bit-0 and bit-1 : direction 00 = down, 01 = right, 10 = up, 11 = left
        // bit-2: strength of current 0 = weak, 1 = strong
        // bit-3: show current using dust motes 0 = no, 1 = yes
        public ushort Flags { get; set; }
    }

    public class ExitReturnData
    { 
        public ushort Left { get; set; }
        public ushort Top { get; set; }
    }
}
