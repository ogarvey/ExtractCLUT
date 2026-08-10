using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ExtractCLUT.Games.PC.Arcturus
{
    public class ArcturusExtractor
    {
        public sealed class PakEntry
        {
            public int Index { get; init; }
            public string Name { get; init; } = string.Empty;
            public byte CompressionType { get; init; }
            public ushort HashChainPrev { get; init; }
            public uint DataOffset { get; init; }
            public uint StoredSize { get; init; }
            public uint UnpackedSize { get; init; }

            public bool IsCompressed => CompressionType == 1;

            public override string ToString()
            {
                return $"[{Index:D5}] {Name} type={CompressionType} off=0x{DataOffset:X8} packed={StoredSize} unpacked={UnpackedSize}";
            }
        }

        public sealed class PakArchive
        {
            public sealed class ActAlignedFrame
            {
                public int ActionIndex { get; init; }
                public int PatternIndex { get; init; }
                public int KeyframeIndex { get; init; }
                public int SpriteBank { get; init; }
                public int SpriteFrameIndex { get; init; }
                public int FrameWidth { get; init; }
                public int FrameHeight { get; init; }
                public int SourceX { get; init; }
                public int SourceY { get; init; }
                public int AlignedX { get; init; }
                public int AlignedY { get; init; }
                public float Scale { get; init; }
                public string SuggestedImageName { get; init; } = string.Empty;
            }

            public string FilePath { get; }
            public uint TableOffset { get; }
            public uint EntryCount { get; }
            public byte FooterTag { get; }
            public IReadOnlyList<PakEntry> Entries => _entries;

            private readonly List<PakEntry> _entries;

            private PakArchive(string filePath, uint tableOffset, uint entryCount, byte footerTag, List<PakEntry> entries)
            {
                FilePath = filePath;
                TableOffset = tableOffset;
                EntryCount = entryCount;
                FooterTag = footerTag;
                _entries = entries;
            }

            public static PakArchive Load(string pakPath)
            {
                if (!File.Exists(pakPath))
                    throw new FileNotFoundException("PAK file not found", pakPath);

                using var fs = new FileStream(pakPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true);

                if (fs.Length < 9)
                    throw new InvalidDataException("PAK file too small to contain footer");

                fs.Seek(-9, SeekOrigin.End);
                uint tableOffset = br.ReadUInt32() + 0x13;
                uint entryCount = br.ReadUInt32();
                byte footerTag = br.ReadByte();

                if (tableOffset >= fs.Length)
                    throw new InvalidDataException($"Invalid table offset 0x{tableOffset:X8}");

                fs.Seek(tableOffset, SeekOrigin.Begin);
                var entries = new List<PakEntry>((int)entryCount);

                for (int i = 0; i < entryCount-1; i++)
                {
                    long recordStart = fs.Position;
                    if (recordStart >= fs.Length - 9)
                        throw new EndOfStreamException($"Entry table truncated at index {i}");

                    byte nameLen = br.ReadByte();
                    byte compressionType = br.ReadByte();
                    uint dataOffset = br.ReadUInt32();
                    uint storedSize = br.ReadUInt32();
                    uint unpackedSize = br.ReadUInt32();

                    byte[] rawName = br.ReadBytes(nameLen + 1);
                    if (rawName.Length != nameLen + 1)
                        throw new EndOfStreamException($"Name bytes truncated for entry {i}");

                    int nullPos = Array.IndexOf(rawName, (byte)0);
                    if (nullPos < 0) nullPos = rawName.Length;
                    string name = Encoding.ASCII.GetString(rawName, 0, nullPos);

                    entries.Add(new PakEntry
                    {
                        Index = i,
                        Name = name,
                        CompressionType = compressionType,
                        DataOffset = dataOffset,
                        StoredSize = storedSize,
                        UnpackedSize = unpackedSize
                    });
                }

                return new PakArchive(pakPath, tableOffset, entryCount, footerTag, entries);
            }

            public byte[] ReadEntryBytes(PakEntry entry)
            {
                using var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                if ((long)entry.DataOffset + entry.StoredSize > fs.Length)
                    throw new InvalidDataException($"Entry out of bounds: {entry}");

                fs.Seek(entry.DataOffset, SeekOrigin.Begin);
                byte[] packed = new byte[entry.StoredSize];
                int read = fs.Read(packed, 0, packed.Length);
                if (read != packed.Length)
                    throw new EndOfStreamException($"Failed reading packed payload for entry: {entry.Name}");

                if (entry.CompressionType == 0)
                {
                    return packed;
                }

                if (entry.CompressionType == 1)
                {
                    return ArcturusDecompressor.DecompressLzssLike(packed, checked((int)entry.UnpackedSize));
                }

                // Unknown compression type: return packed payload as-is.
                return packed;
            }

            public void ExtractAll(string outputDirectory, bool keepOriginalCase = true)
            {
                Directory.CreateDirectory(outputDirectory);

                foreach (var entry in _entries)
                {
                    byte[] data = ReadEntryBytes(entry);

                    string relativeName = SanitizeRelativePath(entry.Name, keepOriginalCase);
                    string outputPath = Path.Combine(outputDirectory, relativeName);

                    string? parent = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(parent))
                        Directory.CreateDirectory(parent);

                    if (data.Length > 0) {
                        if (Path.GetExtension(outputPath).Equals(".spr", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var spriteOutputFolder = Path.Combine(Path.GetDirectoryName(outputPath) ?? outputDirectory, Path.GetFileNameWithoutExtension(outputPath));
                                Directory.CreateDirectory(spriteOutputFolder);
                                // .SPR files are multi-image sprite containers.
                                using var imageDataReader = new BinaryReader(new MemoryStream(data));
                                imageDataReader.ReadUInt16(); // magic
                                var flags = imageDataReader.ReadUInt16();
                                uint imageCountIndexed =  imageDataReader.ReadUInt16();
                                uint imageCountRgba = imageDataReader.ReadUInt16();

                                imageDataReader.BaseStream.Seek(-0x400, SeekOrigin.End);

                                var paletteData = imageDataReader.ReadBytes(0x400);
                                var palette = ColorHelper.ConvertRgbxIS(paletteData);

                                imageDataReader.BaseStream.Seek(flags > 0x1FF ? 8 : 6, SeekOrigin.Begin);

                                for (int i = 0; i < imageCountIndexed; i++)
                                {
                                    var width = imageDataReader.ReadUInt16();
                                    var height = imageDataReader.ReadUInt16();
                                    var imageData = imageDataReader.ReadBytes(width * height);
                                    var image = ImageFormatHelper.GenerateIMClutImage(palette, imageData, width, height, true, [0]);
                                    string imageOutPath = Path.Combine(spriteOutputFolder, $"{Path.GetFileNameWithoutExtension(outputPath)}_{i:D2}.png");
                                    image.SaveAsPng(imageOutPath);
                                }
                                // should be 0x400 bytes of palette data after the frames, in RGBX format
                                
                                if (flags > 0x1FF)
                                {
                                    for (int i = 0; i < imageCountRgba; i++)
                                    {
                                        var width = imageDataReader.ReadUInt16();
                                        var height = imageDataReader.ReadUInt16();
                                        var imageData = imageDataReader.ReadBytes(width * height * 4);
                                        var image = ImageFormatHelper.DecodeRgbaToImageSharp(imageData, width, height);
                                        string imageOutPath = Path.Combine(spriteOutputFolder, $"{Path.GetFileNameWithoutExtension(outputPath)}_rgba_{i:D2}.png");
                                        image.SaveAsPng(imageOutPath);
                                    }
                                }
                            } 
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to parse .SPR entry '{entry.Name}': {ex.Message}");
                                // Fall back to writing raw data if parsing fails.
                            }
                        }
                        File.WriteAllBytes(outputPath, data);
                    }
                }
            }

            /// <summary>
            /// Uses the paired .act file for a given .spr file and exports per-keyframe alignment metadata.
            ///
            /// The output JSON references already extracted frame PNG naming used by <see cref="ExtractAll"/>:
            ///   indexed : {sprBase}_{frameIndex:D2}.png
            ///   rgba    : {sprBase}_rgba_{frameIndex:D2}.png
            ///
            /// This method does not composite images; it emits corrected placement data (`AlignedX`, `AlignedY`)
            /// derived from the game logic so a renderer can place frames exactly as the game does.
            /// </summary>
            public void ExportActAlignedFramesForSprite(string sprEntryName, string outputDirectory, bool saveAlignedImages)
            {
                if (string.IsNullOrWhiteSpace(sprEntryName))
                    throw new ArgumentException("Sprite entry name is required", nameof(sprEntryName));

                if (string.IsNullOrWhiteSpace(outputDirectory))
                    throw new ArgumentException("Output directory is required", nameof(outputDirectory));

                string normalizedSprName = sprEntryName.Replace('/', '\\');
                var sprEntry = _entries.FirstOrDefault(e =>
                  string.Equals(e.Name.Replace('/', '\\'), normalizedSprName, StringComparison.OrdinalIgnoreCase));

                if (sprEntry == null)
                    throw new FileNotFoundException($"SPR entry not found in archive: {sprEntryName}");

                string actEntryName = Path.ChangeExtension(sprEntry.Name.Replace('/', '\\'), ".act") ?? string.Empty;
                var actEntry = _entries.FirstOrDefault(e =>
                  string.Equals(e.Name.Replace('/', '\\'), actEntryName, StringComparison.OrdinalIgnoreCase));

                if (actEntry == null)
                    throw new FileNotFoundException($"Paired ACT entry not found for SPR '{sprEntry.Name}'. Expected '{actEntryName}'.");

                byte[] sprData = ReadEntryBytes(sprEntry);
                byte[] actData = ReadEntryBytes(actEntry);

                ParseSprFrameLists(sprData, out ushort sprVersion, out List<(int Width, int Height)> indexedFrames, out List<(int Width, int Height)> rgbaFrames);
                List<ActAlignedFrame> alignedFrames = ParseActAlignedFrames(actData, sprVersion, Path.GetFileNameWithoutExtension(sprEntry.Name), indexedFrames, rgbaFrames);

                Directory.CreateDirectory(outputDirectory);

                string sprBase = Path.GetFileNameWithoutExtension(sprEntry.Name);
                string jsonPath = Path.Combine(outputDirectory, $"{sprBase}_act_aligned_frames.json");

                if (saveAlignedImages)
                {
                    RenderAlignedFramesFromSprData(sprData, sprBase, alignedFrames, outputDirectory);
                }

                var payload = new
                {
                    Sprite = sprEntry.Name,
                    Act = actEntry.Name,
                    SprVersion = sprVersion,
                    IndexedFrameCount = indexedFrames.Count,
                    RgbaFrameCount = rgbaFrames.Count,
                    Frames = alignedFrames
                };

                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(jsonPath, json, Encoding.UTF8);
            }

            /// <summary>
            /// Loads a single already-extracted .spr file from disk, loads paired .act from the same folder,
            /// and exports ACT-aligned frame metadata (and optionally rendered aligned images).
            /// </summary>
            public static void ExportActAlignedFramesForExtractedSprite(string sprFilePath, string outputDirectory, bool saveAlignedImages)
            {
                if (string.IsNullOrWhiteSpace(sprFilePath))
                    throw new ArgumentException("SPR file path is required", nameof(sprFilePath));

                if (!File.Exists(sprFilePath))
                    throw new FileNotFoundException("SPR file not found", sprFilePath);

                string actPath = Path.ChangeExtension(sprFilePath, ".act") ?? string.Empty;
                if (!File.Exists(actPath))
                    throw new FileNotFoundException($"Paired ACT file not found: {actPath}");

                if (string.IsNullOrWhiteSpace(outputDirectory))
                    throw new ArgumentException("Output directory is required", nameof(outputDirectory));

                byte[] sprData = File.ReadAllBytes(sprFilePath);
                byte[] actData = File.ReadAllBytes(actPath);

                ParseSprFrameLists(sprData, out ushort sprVersion, out List<(int Width, int Height)> indexedFrames, out List<(int Width, int Height)> rgbaFrames);
                string sprBase = Path.GetFileNameWithoutExtension(sprFilePath);
                List<ActAlignedFrame> alignedFrames = ParseActAlignedFrames(actData, sprVersion, sprBase, indexedFrames, rgbaFrames);

                Directory.CreateDirectory(outputDirectory);

                if (saveAlignedImages)
                {
                    RenderAlignedFramesFromSprData(sprData, sprBase, alignedFrames, outputDirectory);
                }

                string jsonPath = Path.Combine(outputDirectory, $"{sprBase}_act_aligned_frames.json");
                var payload = new
                {
                    Sprite = Path.GetFileName(sprFilePath),
                    Act = Path.GetFileName(actPath),
                    SprVersion = sprVersion,
                    IndexedFrameCount = indexedFrames.Count,
                    RgbaFrameCount = rgbaFrames.Count,
                    Frames = alignedFrames
                };

                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(jsonPath, json, Encoding.UTF8);
            }

            private static void RenderAlignedFramesFromSprData(
              byte[] sprData,
              string sprBaseName,
              IReadOnlyList<ActAlignedFrame> alignedFrames,
              string outputDirectory)
            {
                string imageOutDir = Path.Combine(outputDirectory, $"{sprBaseName}_aligned");
                Directory.CreateDirectory(imageOutDir);

                ParseSprDecodedImages(sprData, out _, out var indexedImages, out var rgbaImages);

                try
                {
                    // Compute a shared canvas for all aligned frames so frame-to-frame playback is stable
                    // (no jitter from varying per-frame canvas extents).
                    int minX = int.MaxValue;
                    int minY = int.MaxValue;
                    int maxX = int.MinValue;
                    int maxY = int.MinValue;
                    bool hasAnyRenderable = false;

                    foreach (var f in alignedFrames)
                    {
                        int sourceW;
                        int sourceH;

                        if (f.SpriteBank == 0)
                        {
                            if ((uint)f.SpriteFrameIndex >= indexedImages.Count)
                                continue;
                            sourceW = indexedImages[f.SpriteFrameIndex].Width;
                            sourceH = indexedImages[f.SpriteFrameIndex].Height;
                        }
                        else
                        {
                            if ((uint)f.SpriteFrameIndex >= rgbaImages.Count)
                                continue;
                            sourceW = rgbaImages[f.SpriteFrameIndex].Width;
                            sourceH = rgbaImages[f.SpriteFrameIndex].Height;
                        }

                        int scaledW = Math.Max(1, (int)Math.Round(sourceW * f.Scale, MidpointRounding.AwayFromZero));
                        int scaledH = Math.Max(1, (int)Math.Round(sourceH * f.Scale, MidpointRounding.AwayFromZero));

                        minX = Math.Min(minX, f.AlignedX);
                        minY = Math.Min(minY, f.AlignedY);
                        maxX = Math.Max(maxX, f.AlignedX + scaledW);
                        maxY = Math.Max(maxY, f.AlignedY + scaledH);
                        hasAnyRenderable = true;
                    }

                    if (!hasAnyRenderable)
                        return;

                    int sharedCanvasW = Math.Max(1, maxX - minX);
                    int sharedCanvasH = Math.Max(1, maxY - minY);

                    foreach (var f in alignedFrames)
                    {
                        Image<Rgba32>? source = null;
                        if (f.SpriteBank == 0)
                        {
                            if ((uint)f.SpriteFrameIndex < indexedImages.Count)
                                source = indexedImages[f.SpriteFrameIndex];
                        }
                        else
                        {
                            if ((uint)f.SpriteFrameIndex < rgbaImages.Count)
                                source = rgbaImages[f.SpriteFrameIndex];
                        }

                        if (source == null)
                            continue;

                        int scaledW = Math.Max(1, (int)Math.Round(source.Width * f.Scale, MidpointRounding.AwayFromZero));
                        int scaledH = Math.Max(1, (int)Math.Round(source.Height * f.Scale, MidpointRounding.AwayFromZero));

                        using var scaled = source.Clone(ctx =>
                        {
                            if (scaledW != source.Width || scaledH != source.Height)
                                ctx.Resize(scaledW, scaledH, KnownResamplers.NearestNeighbor);
                        });

                        int drawX = f.AlignedX - minX;
                        int drawY = f.AlignedY - minY;

                        using var canvas = new Image<Rgba32>(sharedCanvasW, sharedCanvasH, new Rgba32(0, 0, 0, 0));
                        canvas.Mutate(ctx => ctx.DrawImage(scaled, new Point(drawX, drawY), 1f));

                        string fileName =
                          $"{sprBaseName}_a{f.ActionIndex:D3}_p{f.PatternIndex:D3}_k{f.KeyframeIndex:D3}_b{f.SpriteBank}_f{f.SpriteFrameIndex:D3}.png";
                        string outPath = Path.Combine(imageOutDir, fileName);
                        canvas.SaveAsPng(outPath);
                    }
                }
                finally
                {
                    foreach (var img in indexedImages)
                        img.Dispose();
                    foreach (var img in rgbaImages)
                        img.Dispose();
                }
            }

            private static void ParseSprDecodedImages(
              byte[] sprData,
              out ushort version,
              out List<Image<Rgba32>> indexedImages,
              out List<Image<Rgba32>> rgbaImages)
            {
                indexedImages = new List<Image<Rgba32>>();
                rgbaImages = new List<Image<Rgba32>>();

                using var br = new BinaryReader(new MemoryStream(sprData), Encoding.ASCII, leaveOpen: false);
                if (sprData.Length < 6)
                    throw new InvalidDataException("SPR data too small");

                ushort magic = br.ReadUInt16();
                if (magic != 0x5053)
                    throw new InvalidDataException("Invalid SPR magic");

                version = br.ReadUInt16();
                ushort indexedCount = br.ReadUInt16();
                ushort rgbaCount = 0;

                if (version > 0x1FF)
                    rgbaCount = br.ReadUInt16();

                byte[] paletteData;
                if (sprData.Length >= 0x400)
                {
                    br.BaseStream.Seek(-0x400, SeekOrigin.End);
                    paletteData = br.ReadBytes(0x400);
                }
                else
                {
                    paletteData = new byte[0x400];
                }

                var palette = ColorHelper.ConvertRgbxIS(paletteData);

                br.BaseStream.Seek(version > 0x1FF ? 8 : 6, SeekOrigin.Begin);

                for (int i = 0; i < indexedCount; i++)
                {
                    ushort w = br.ReadUInt16();
                    ushort h = br.ReadUInt16();
                    byte[] pixels = br.ReadBytes(w * h);
                    if (pixels.Length != w * h)
                        throw new EndOfStreamException("Indexed frame data truncated");

                    using var image = ImageFormatHelper.GenerateIMClutImage(palette, pixels, w, h, true, [0]);
                    indexedImages.Add(image.CloneAs<Rgba32>());
                }

                for (int i = 0; i < rgbaCount; i++)
                {
                    ushort w = br.ReadUInt16();
                    ushort h = br.ReadUInt16();
                    byte[] pixels = br.ReadBytes(w * h * 4);
                    if (pixels.Length != w * h * 4)
                        throw new EndOfStreamException("RGBA frame data truncated");

                    using var image = ImageFormatHelper.DecodeRgbaToImageSharp(pixels, w, h);
                    rgbaImages.Add(image.CloneAs<Rgba32>());
                }
            }

            private static void ParseSprFrameLists(byte[] sprData, out ushort version, out List<(int Width, int Height)> indexedFrames, out List<(int Width, int Height)> rgbaFrames)
            {
                indexedFrames = new List<(int Width, int Height)>();
                rgbaFrames = new List<(int Width, int Height)>();

                using var br = new BinaryReader(new MemoryStream(sprData), Encoding.ASCII, leaveOpen: false);

                if (sprData.Length < 6)
                    throw new InvalidDataException("SPR data too small");

                ushort magic = br.ReadUInt16();
                if (magic != 0x5053) // 'SP'
                    throw new InvalidDataException("Invalid SPR magic");

                version = br.ReadUInt16();
                ushort indexedCount = br.ReadUInt16();
                ushort rgbaCount = 0;

                if (version > 0x1FF)
                    rgbaCount = br.ReadUInt16();

                br.BaseStream.Seek(version > 0x1FF ? 8 : 6, SeekOrigin.Begin);

                for (int i = 0; i < indexedCount; i++)
                {
                    if (br.BaseStream.Position + 4 > br.BaseStream.Length)
                        throw new EndOfStreamException("Indexed frame header truncated");

                    ushort w = br.ReadUInt16();
                    ushort h = br.ReadUInt16();
                    indexedFrames.Add((w, h));

                    long pixelBytes = (long)w * h;
                    if (br.BaseStream.Position + pixelBytes > br.BaseStream.Length)
                        throw new EndOfStreamException("Indexed frame pixels truncated");

                    br.BaseStream.Seek(pixelBytes, SeekOrigin.Current);
                }

                for (int i = 0; i < rgbaCount; i++)
                {
                    if (br.BaseStream.Position + 4 > br.BaseStream.Length)
                        throw new EndOfStreamException("RGBA frame header truncated");

                    ushort w = br.ReadUInt16();
                    ushort h = br.ReadUInt16();
                    rgbaFrames.Add((w, h));

                    long pixelBytes = (long)w * h * 4;
                    if (br.BaseStream.Position + pixelBytes > br.BaseStream.Length)
                        throw new EndOfStreamException("RGBA frame pixels truncated");

                    br.BaseStream.Seek(pixelBytes, SeekOrigin.Current);
                }
            }

            private static List<ActAlignedFrame> ParseActAlignedFrames(
              byte[] actData,
              ushort sprVersion,
              string sprBaseName,
              IReadOnlyList<(int Width, int Height)> indexedFrames,
              IReadOnlyList<(int Width, int Height)> rgbaFrames)
            {
                var result = new List<ActAlignedFrame>();

                using var br = new BinaryReader(new MemoryStream(actData), Encoding.ASCII, leaveOpen: false);
                if (actData.Length < 16)
                    throw new InvalidDataException("ACT data too small");

                ushort magic = br.ReadUInt16();
                if (magic != 0x4341) // 'AC'
                    throw new InvalidDataException("Invalid ACT magic");

                ushort actVersion = br.ReadUInt16();
                ushort actionCount = br.ReadUInt16();

                // ACT loader reads a fixed 0x10-byte header before body.
                br.BaseStream.Seek(0x10, SeekOrigin.Begin);

                for (int actionIndex = 0; actionIndex < actionCount; actionIndex++)
                {
                    int patternCount = br.ReadInt32();

                    for (int patternIndex = 0; patternIndex < patternCount; patternIndex++)
                    {
                        // Two 16-byte blocks copied into pattern metadata in loader.
                        br.BaseStream.Seek(0x10 + 0x10, SeekOrigin.Current);

                        int keyframeCount = br.ReadInt32();

                        for (int keyframeIndex = 0; keyframeIndex < keyframeCount; keyframeIndex++)
                        {
                            int recordSize = actVersion < 0x200 ? 0x10 : 0x20;
                            byte[] rec = br.ReadBytes(recordSize);
                            if (rec.Length != recordSize)
                                throw new EndOfStreamException("ACT keyframe record truncated");

                            int sourceX = BitConverter.ToInt32(rec, 0x00);
                            int sourceY = BitConverter.ToInt32(rec, 0x04);
                            int frameIndex = BitConverter.ToInt32(rec, 0x08);

                            float scale = 1.0f;
                            int bank = 0;

                            if (actVersion >= 0x200)
                            {
                                scale = BitConverter.ToSingle(rec, 0x14);
                                if (Math.Abs(scale) < 1e-6f)
                                    scale = 1.0f;

                                bank = BitConverter.ToInt32(rec, 0x1C) == 0 ? 0 : 1;
                            }

                            frameIndex = Math.Clamp(frameIndex, 0, 999);

                            (int Width, int Height) dims;
                            if (bank == 0)
                            {
                                if (indexedFrames.Count == 0)
                                    dims = (0, 0);
                                else
                                    dims = indexedFrames[Math.Clamp(frameIndex, 0, indexedFrames.Count - 1)];
                            }
                            else
                            {
                                if (rgbaFrames.Count == 0)
                                    dims = (0, 0);
                                else
                                    dims = rgbaFrames[Math.Clamp(frameIndex, 0, rgbaFrames.Count - 1)];
                            }

                            int alignedX = NormalizeActAxis(sourceX, dims.Width, scale);
                            int alignedY = NormalizeActAxis(sourceY, dims.Height, scale);

                            string suggestedImageName = bank == 0
                              ? $"{sprBaseName}_{Math.Clamp(frameIndex, 0, Math.Max(0, indexedFrames.Count - 1)):D2}.png"
                              : $"{sprBaseName}_rgba_{Math.Clamp(frameIndex, 0, Math.Max(0, rgbaFrames.Count - 1)):D2}.png";

                            result.Add(new ActAlignedFrame
                            {
                                ActionIndex = actionIndex,
                                PatternIndex = patternIndex,
                                KeyframeIndex = keyframeIndex,
                                SpriteBank = bank,
                                SpriteFrameIndex = frameIndex,
                                FrameWidth = dims.Width,
                                FrameHeight = dims.Height,
                                SourceX = sourceX,
                                SourceY = sourceY,
                                AlignedX = alignedX,
                                AlignedY = alignedY,
                                Scale = scale,
                                SuggestedImageName = suggestedImageName
                            });
                        }

                        // Loader reads one extra int per pattern when ACT version > 0x1FF.
                        if (actVersion > 0x1FF)
                            _ = br.ReadInt32();
                    }
                }

                // Optional ACT tail (names table) for version > 0x200.
                if (actVersion > 0x200 && br.BaseStream.Position + 4 <= br.BaseStream.Length)
                {
                    int tailCount = br.ReadInt32();
                    long tailBytes = (long)tailCount * 0x28;
                    if (tailCount > 0 && br.BaseStream.Position + tailBytes <= br.BaseStream.Length)
                        br.BaseStream.Seek(tailBytes, SeekOrigin.Current);
                }

                _ = sprVersion; // retained for future version-specific alignment differences.
                return result;
            }

            private static int NormalizeActAxis(int sourceCoord, int frameExtent, float scale)
            {
                if (Math.Abs(scale) < 1e-6f)
                    scale = 1.0f;

                int halfExtent = frameExtent / 2;
                double normalized = (sourceCoord - (halfExtent * scale)) / scale;

                // MSVC __ftol behavior here is nearest-int on x87 control defaults.
                return (int)Math.Round(normalized, MidpointRounding.ToEven);
            }

            public void WriteIndexReport(string reportPath)
            {
                using var writer = new StreamWriter(reportPath, append: false, Encoding.UTF8);
                writer.WriteLine($"PAK: {FilePath}");
                writer.WriteLine($"Entries: {EntryCount}");
                writer.WriteLine($"TableOffset: 0x{TableOffset:X8}");
                writer.WriteLine($"FooterTag: 0x{FooterTag:X2}");
                writer.WriteLine();

                foreach (var entry in _entries)
                    writer.WriteLine(entry.ToString());
            }

            private static string SanitizeRelativePath(string path, bool keepOriginalCase)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return "unnamed.bin";

                string normalized = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                var parts = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < parts.Length; i++)
                {
                    string p = parts[i].Trim();
                    foreach (char invalid in Path.GetInvalidFileNameChars())
                        p = p.Replace(invalid, '_');

                    if (!keepOriginalCase)
                        p = p.ToLowerInvariant();

                    parts[i] = string.IsNullOrEmpty(p) ? "_" : p;
                }

                return parts.Length == 0 ? "unnamed.bin" : Path.Combine(parts);
            }
        }

    }
}
