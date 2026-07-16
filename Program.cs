using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text;
using ExtractCLUT;
using ExtractCLUT.Games.PC;
using ExtractCLUT.Games.PC.Anvil;
using ExtractCLUT.Games.PC.Cybermage;
using ExtractCLUT.Games.PC.Delphine;
using ExtractCLUT.Games.PC.Shadowcaster;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;



// var tileDir = @"C:\Dev\Gaming\PC\Dos\DiscImages\Wolfsbane_DOS_EN\WLFSBANE\output\Tiles\images\Village_Map_Tiles";
// var tileFiles = Directory.GetFiles(tileDir, "*", SearchOption.TopDirectoryOnly);
// // split filename on underscore and sort by number after last underscore
// var tileImages = tileFiles.OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f).Split('_').Last())).Select(f => SixLabors.ImageSharp.Image.Load<Rgba32>(f)).ToList();

// var mapFile = @"C:\Dev\Gaming\PC\Dos\DiscImages\Wolfsbane_DOS_EN\WLFSBANE\output\Maps\Village_Map";
// // read map file as big endian ushort array
// var mapData = File.ReadAllBytes(mapFile).Skip(0xC).ToArray();
// var mapUShorts = new ushort[mapData.Length / 2];
// for (int i = 0; i < mapUShorts.Length; i++)
// {
//   mapUShorts[i] = (ushort)((ushort)((mapData[i * 2] << 8) | mapData[i * 2 + 1]));
// }

// var maxTileIndex = mapUShorts.Max();

// var mapImage = CreateScreenImage(tileImages, mapUShorts, 0x1e0, 0x2a, 16, 16);
// mapImage.Save(@"C:\Dev\Gaming\PC\Dos\DiscImages\Wolfsbane_DOS_EN\WLFSBANE\output\Maps\Village_Map.png");

// static System.Drawing.Image CreateScreenImage(List<SixLabors.ImageSharp.Image<Rgba32>> _tiles, ushort[] _mapData, int widthInTiles, int heightInTiles, int tileWidth, int tileHeight)
// {
//   var tempScreenBitmap = new Bitmap(widthInTiles * tileWidth, heightInTiles * tileHeight);

//   for (int y = 0; y < heightInTiles * tileHeight; y++)
//   {
//     for (int x = 0; x < widthInTiles * tileWidth; x++)
//     {
//       int tileX = x / tileWidth;
//       int tileY = y / tileHeight;
//       int tileIndex = tileX + (tileY * widthInTiles);
//       if (tileIndex >= _mapData.Length)
//         continue;

//       int index = _mapData[tileIndex];
//       if (index <= 0)
//         continue;
//       var flipH = false;
//       if (index >= _tiles.Count)
//       {
//         index = index & 0xFF;
//         flipH = true;
//       }

//       var tile = _tiles[index];

//       int tilePixelX = x % tileWidth;
//       int tilePixelY = y % tileHeight;

//       if (tilePixelX < tile.Width && tilePixelY < tile.Height)
//       {
//         var pixel = !flipH ? tile[tilePixelX, tilePixelY] : tile[tile.Width - 1 - tilePixelX, tilePixelY]; // Accessing pixel using ImageSharp's 2D indexer
//         var color = System.Drawing.Color.FromArgb(pixel.A, pixel.R, pixel.G, pixel.B); // Convert to System.Drawing.Color
//         tempScreenBitmap.SetPixel(x, y, color); // System.Drawing.Bitmap's SetPixel
//       }
//     }
//   }
//   return tempScreenBitmap;
// }

// var woeRsc = @"C:\Dev\Gaming\PC\Dos\DiscImages\Wolfsbane_DOS_EN\WLFSBANE\WLFSBANE.MSC";
// var woeOutputDir = Path.Combine(Path.GetDirectoryName(woeRsc)!, "msc_output");
// Directory.CreateDirectory(woeOutputDir);

// using var woeReader = new BinaryReader(File.OpenRead(woeRsc));
// var woeOffsetsAndNames = new List<(uint offset, string name)>();

// var woeFileCount = woeReader.ReadUInt16();
// var dataStart = woeReader.ReadUInt32();
// for (int i = 0; i < woeFileCount; i++)
// {
//   var offset = woeReader.ReadUInt32() + dataStart;
//   var name = woeReader.ReadNullTerminatedString();
//   woeOffsetsAndNames.Add((offset, name));
// }

// for (int i = 0; i < woeOffsetsAndNames.Count; i++)
// {
//   var (offset, name) = woeOffsetsAndNames[i];
//   uint size = 0;
//   if (i < woeOffsetsAndNames.Count - 1)
//   {
//     size = woeOffsetsAndNames[i + 1].offset - offset;
//   }
//   else
//   {
//     size = (uint)(woeReader.BaseStream.Length - offset);
//   }
//   woeReader.BaseStream.Seek(offset, SeekOrigin.Begin);
//   var data = woeReader.ReadBytes((int)size);
//   File.WriteAllBytes(Path.Combine(woeOutputDir, name), data);
// }

var framePcxFile = @"C:\Dev\Gaming\PC\Dos\DiscImages\Wolfsbane_DOS_EN\WLFSBANE\output\PCX\Frame.PCX";
var framePcxData = File.ReadAllBytes(framePcxFile);
// gt last 768 bytes for palette
var framePcxPaletteData = framePcxData.Skip(framePcxData.Length - 768).Take(768).ToArray();
var framePcxPalette = ColorHelper.ConvertBytesToRgbIS(framePcxPaletteData);
var wlfsImageFileDir = @"C:\Dev\Gaming\PC\Dos\DiscImages\Wolfsbane_DOS_EN\WLFSBANE\output\Definitions";
var wlfsImageFiles = Directory.GetFiles(wlfsImageFileDir, "*village*", SearchOption.TopDirectoryOnly);
var wlfsPalFile = @"C:\Dev\Gaming\PC\Dos\DiscImages\Wolfsbane_DOS_EN\WLFSBANE\output\Palettes\Village_Palette";
var wlfsPalData = File.ReadAllBytes(wlfsPalFile);

var wlfsPalette = ColorHelper.ConvertBytesToRgbIS(wlfsPalData, true);
// replace colors 1 - 31 with colors from framePcxPalette
for (int i = 1; i < 32; i++)
{
    wlfsPalette[i] = framePcxPalette[i];
}

foreach (var wlfsImageFile in wlfsImageFiles)
{
    var wlfsOutputDir = Path.Combine(Path.GetDirectoryName(wlfsImageFile)!, "images", $"{Path.GetFileNameWithoutExtension(wlfsImageFile)}");
    Directory.CreateDirectory(wlfsOutputDir);
    using var wlfsReader = new BinaryReader(File.OpenRead(wlfsImageFile));
    var iCount = wlfsReader.ReadUInt16();
    var test = wlfsReader.ReadByte();

    var offsetAdjustment = iCount * 2 + 3;
    List<ushort> offsets = new List<ushort>();
    for (int i = 0; i < iCount; i++)
    {
        offsets.Add((ushort)(wlfsReader.ReadUInt16() + offsetAdjustment));
    }

    for (int i = 0; i < offsets.Count; i++)
    {
        var offset = offsets[i];
        wlfsReader.BaseStream.Seek(offset, SeekOrigin.Begin);
        var height = wlfsReader.ReadUInt16();
        var width = wlfsReader.ReadUInt16();
        var compressedSize = test == 1 ? wlfsReader.ReadUInt16() : (ushort)(width * height);
        var decompressedData = new List<byte>();
        if (test == 1)
        {
            while (decompressedData.Count < width * height)
            {
                var b = wlfsReader.ReadByte();
                if (b == 0x00)
                {
                    var count = wlfsReader.ReadByte();
                    decompressedData.AddRange(Enumerable.Repeat((byte)0x00, count));
                }
                else
                {
                    decompressedData.Add(b);
                }
            }
        }
        else
        {
            decompressedData.AddRange(wlfsReader.ReadBytes(width * height));
        }
        var image = ImageFormatHelper.GenerateIMClutImage(wlfsPalette, decompressedData.ToArray(), width, height, true, 0);
        image.SaveAsPng(Path.Combine(wlfsOutputDir, $"image_{i:D3}.png"));
    }
}



var fpgDir = @"C:\Dev\Gaming\PC\Dos\Games\Akiko-and-Minami_DOS_EN\FPG";
var fpgFiles = Directory.GetFiles(fpgDir, "*.fpg", SearchOption.TopDirectoryOnly);

foreach (var fpgFile in fpgFiles)
{
    var fpgOutputDir = Path.Combine(Path.GetDirectoryName(fpgFile)!, Path.GetFileNameWithoutExtension(fpgFile) + "_images");
    Directory.CreateDirectory(fpgOutputDir);

    var fpgReader = new BinaryReader(File.OpenRead(fpgFile));
    fpgReader.BaseStream.Seek(0x08, SeekOrigin.Begin);
    var paletteData = fpgReader.ReadBytes(0x300);
    var palette = ColorHelper.ConvertBytesToRgbIS(paletteData, true);

    fpgReader.BaseStream.Seek(0x548, SeekOrigin.Begin);
    var imageIndex = 0;
    while (fpgReader.BaseStream.Position < fpgReader.BaseStream.Length)
    {
        var flag = fpgReader.ReadUInt32();
        var size = fpgReader.ReadUInt32();
        fpgReader.BaseStream.Seek(0x2C, SeekOrigin.Current);
        var width = fpgReader.ReadUInt32();
        var height = fpgReader.ReadUInt32();
        var headerSize = size - (width * height);
        if (headerSize == 0x44)
        {
            fpgReader.ReadBytes(0x08);
        }
        else if (headerSize == 0x40)
        {
            fpgReader.ReadBytes(0x04);
        }
        var imageData = fpgReader.ReadBytes((int)(width * height));
        var image = ImageFormatHelper.GenerateIMClutImage(palette, imageData, (int)width, (int)height, true, 0);
        image.SaveAsPng(Path.Combine(fpgOutputDir, $"image_{flag:D3}.png"));
        imageIndex++;
    }
}




var binFiles = @"C:\Dev\Gaming\PC\Dos\Games\Cybermage\CYBERMAG\anmh_output";

foreach (var spriteFile in Directory.GetFiles(binFiles, "*.bin", SearchOption.TopDirectoryOnly))
{
    var sprOutputDir = Path.Combine(Path.GetDirectoryName(spriteFile)!, Path.GetFileNameWithoutExtension(spriteFile) + "_sprites");
    var palPath = @"C:\Dev\Gaming\PC\Dos\Games\Cybermage\CYBERMAG\cybermage.pal";
    Directory.CreateDirectory(sprOutputDir);
    SpriteHelper.ExtractSpriteFile(spriteFile, sprOutputDir, palPath);
}

var hipakRsc = @"C:\Dev\Gaming\PC\Dos\Games\Cybermage\CYBERMAG\anmh.RSC";
var outputDir = Path.Combine(Path.GetDirectoryName(hipakRsc)!, "anmh_output");
Directory.CreateDirectory(outputDir);
var hipakReader = new BinaryReader(File.OpenRead(hipakRsc));

var offsetsAndSizes = new List<(uint offset, uint size)>();

var slotCount = hipakReader.ReadUInt32();
var actualCount = hipakReader.ReadUInt32();

for (int i = 0; i < slotCount; i++)
{
    uint offset = hipakReader.ReadUInt32();
    uint size = 0;
    offsetsAndSizes.Add((offset, size));
}

for (int i = 0; i < offsetsAndSizes.Count; i++)
{
    var size = hipakReader.ReadUInt32();
    if (size == 0)
    {
        continue;
    }
    offsetsAndSizes[i] = (offsetsAndSizes[i].offset, size);
}

var largeImages = new List<byte[]>();
var mediumImages = new List<byte[]>();
var smallImages = new List<byte[]>();
var palettes = new List<byte[]>();

var largeImageDir = Path.Combine(outputDir, "large_images");
var mediumImageDir = Path.Combine(outputDir, "medium_images");
var smallImageDir = Path.Combine(outputDir, "small_images");
Directory.CreateDirectory(largeImageDir);
Directory.CreateDirectory(mediumImageDir);
Directory.CreateDirectory(smallImageDir);



foreach (var (offset, size) in offsetsAndSizes)
{
    if (offset == 0 || size == 0)
    {
        continue;
    }
    hipakReader.BaseStream.Seek(offset, SeekOrigin.Begin);
    var data = hipakReader.ReadBytes((int)size);
    switch (size)
    {
        case 0x4b000:
            // 640 x 480, 8-bit indexed image.
            largeImages.Add(data);
            break;
        case 0x12c00:
            // 320 x 240, 8-bit indexed image.
            mediumImages.Add(data);
            break;
        case 0x8000:
            // 256 x 128, 8-bit indexed image.
            smallImages.Add(data);
            break;
        case 0x4000:
            // Fade table/color map???
            break;
        case 0x300:
            // Palette (256 colors, 3 bytes each, vga 6-bit).
            palettes.Add(data);
            break;
        default:
            // Unknown data type, just dump it to a file and log the first 16 bytes as hex.
            Console.WriteLine($"Unknown data type: offset=0x{offset:X8}, size=0x{size:X8}, first 16 bytes: {BitConverter.ToString(data.Take(16).ToArray())}");
            File.WriteAllBytes(Path.Combine(outputDir, $"hipak_0x{offset:X8}_0x{size:X8}.bin"), data);
            break;
    }
}

if (palettes.Count == 0)
{
    var palFileDefault = @"C:\Dev\Gaming\PC\Dos\Games\Cybermage\CYBERMAG\cybermage.pal";
    if (File.Exists(palFileDefault))
    {
        var palData = File.ReadAllBytes(palFileDefault);
        palettes.Add(palData);
        Console.WriteLine($"No palettes found in RSC; loaded default palette from {palFileDefault}.");
    }
    else
    {
        Console.WriteLine("No palettes found in RSC and default palette file not found.");
    }
}

for (int i = 0; i < largeImages.Count; i++)
{
    var imgData = largeImages[i];
    var paletteData = palettes[i % palettes.Count];
    var palette = ColorHelper.ConvertBytesToRgbIS(paletteData, true);
    var image = ImageFormatHelper.GenerateIMClutImage(palette, imgData, 640, 480);
    image.SaveAsPng(Path.Combine(largeImageDir, $"large_image_{i:D4}.png"));
}

for (int i = 0; i < mediumImages.Count; i++)
{
    var imgData = mediumImages[i];
    var paletteData = palettes[i % palettes.Count];
    var palette = ColorHelper.ConvertBytesToRgbIS(paletteData, true);
    var image = ImageFormatHelper.GenerateIMClutImage(palette, imgData, 320, 240);
    image.SaveAsPng(Path.Combine(mediumImageDir, $"medium_image_{i:D4}.png"));
}

for (int i = 0; i < smallImages.Count; i++)
{
    var imgData = smallImages[i];
    var paletteData = palettes[i % palettes.Count];
    var palette = ColorHelper.ConvertBytesToRgbIS(paletteData, true);
    var image = ImageFormatHelper.GenerateIMClutImage(palette, imgData, 256, 128);
    image.SaveAsPng(Path.Combine(smallImageDir, $"small_image_{i:D4}.png"));
}


// var compressedBmp = @"C:\Dev\Gaming\PC\Dos\Games\DARK_SUN\DATA1\bmp_output_compressed\1.bin";
// var compReader = new BinaryReader(File.OpenRead(compressedBmp));

// var totalLength = compReader.ReadUInt32();
// if (totalLength != compReader.BaseStream.Length)
// {
//     Console.WriteLine($"Warning: total length {totalLength} does not match file length {compReader.BaseStream.Length - 4}");
// }

// var shortVal = compReader.ReadUInt16(); // should be 0x0001
// if (shortVal != 0x0001)
// {
//     Console.WriteLine($"Warning: expected 0x0001, got {shortVal:X4}");
// }
// shortVal = compReader.ReadUInt16(); // should be 0x0x000A
// if (shortVal != 0x000A)
// {
//     Console.WriteLine($"Warning: expected 0x000A, got {shortVal:X4}");
// }
// compReader.ReadInt16(); // unknown, should be 0x0000
// var width = compReader.ReadUInt16();
// var height = compReader.ReadUInt16();

// var outputSize = width * height;

// for (int y = 0; y < height; y++)
// {
//   var linePixels = new List<byte>();
//   var lineNo = compReader.ReadByte();
//   var unk1 = compReader.ReadInt16();
//   var unk2 = compReader.ReadInt16();
//   while (linePixels.Count < width)
//   {
//     var b = compReader.ReadByte();
//   }
//   Console.WriteLine($"Line {lineNo}: {unk1:X4} {unk2:X4}");
// }

// record bmpLine(byte lineNo, List<byte> pixels);

// var gffFile = @"C:\Dev\Gaming\PC\Dos\Games\DARK_SUN\DATA1\CINE.GFF";
// DarkSun.ParseGffFile(gffFile);
// Console.WriteLine($"Parsed {DarkSun.GffRecords.Count} GFF records from {gffFile}.");

// using var br = new BinaryReader(File.OpenRead(gffFile));

// foreach (var bmpRecord in DarkSun.BmpRecords)
// {
//     Console.WriteLine($"BMP Record: ID={bmpRecord.Id}, Offset={bmpRecord.Offset}, Size={bmpRecord.Size}");
//     br.BaseStream.Seek(bmpRecord.Offset, SeekOrigin.Begin);
//     var bmpData = br.ReadBytes((int)bmpRecord.Size);
//     File.WriteAllBytes($@"C:\Dev\Gaming\PC\Dos\Games\DARK_SUN\DATA1\bmp_output_compressed\{bmpRecord.Id}.bin", bmpData);
// }

// var compressedFile = @"C:\Dev\Gaming\PC\Dos\Games\DARK_SUN\DATA1\output\cine_1_compressed.bin";
// var decompressedFile = @"C:\Dev\Gaming\PC\Dos\Games\DARK_SUN\DATA1\output\cine_1_decomp.bin";










// var libDir = @"C:\Dev\Gaming\PC\Dos\Games\Crazy-Drake_DOS_EN_Registered\output\";
// var libFiles = Directory.GetFiles(libDir, "*.lib", SearchOption.AllDirectories);


// foreach (var libFile in libFiles)
// {
//   var lib = new ExtractCLUT.Games.PC.CrazyDrake.LibFile(libFile);
//   var imageCount = lib.ParseLib();
//   Console.WriteLine($"Parsed {imageCount} images from {lib.Filename}.");
//   var frameCount = lib.LoadAnm();
//   Console.WriteLine($"Parsed {frameCount} frames from {lib.Anm?.Filename}");

//   if (frameCount > 0 && lib.Anm != null)
//   {
//     lib.SaveAlignedImages(Path.Combine(Path.GetDirectoryName(libFile)!, $"{Path.GetFileNameWithoutExtension(libFile)}_aligned"));
//   }
//   else if (imageCount > 0)
//   {
//     lib.SaveImages(Path.Combine(Path.GetDirectoryName(libFile)!, $"{Path.GetFileNameWithoutExtension(libFile)}_images"));
//   }
// }
// //var libFile = @"C:\Dev\Gaming\PC\Dos\Games\Crazy-Drake_DOS_EN_Registered\output\EPISODE1\EPISODE1\CHARS\BUTERFLY.LIB";



// namespace BladeRunnerSliceExporter;

// // Usage:
// //   BladeRunnerSliceExporter <gameDir> <outDir>
// //        [--anim N] [--canvas WxH] [--facing rad] [--no-crop] [--pad N]
// //
// // Resource lookup mirrors the engine: loose files on disk first, then MIX archives
// // (A.MIX holds INDEX.DAT / PALETTES.DAT). COREANIM.DAT / HDFRAMES.DAT are usually loose.
// internal static class Program
// {
//   // Outer MIX archives that may contain INDEX.DAT etc. Order = search priority.
//   private static readonly string[] CandidateMixes =
//   {
//         "A.MIX", "STARTUP.MIX"
//     };

//   private static int Main(string[] args)
//   {
//     if (args.Length < 2)
//     {
//       Console.Error.WriteLine(
//           "Usage: BladeRunnerSliceExporter <gameDir> <outDir> " +
//           "[--anim N] [--canvas WxH] [--facing rad] [--no-crop] [--pad N]");
//       return 1;
//     }

//     string gameDir = args[0];
//     string outDir = args[1];
//     int? onlyAnim = null;
//     int canvasW = 400, canvasH = 480;
//     float facing = 0f;
//     bool crop = true;
//     int pad = 0;

//     for (int i = 2; i < args.Length; i++)
//     {
//       switch (args[i])
//       {
//         case "--anim": onlyAnim = int.Parse(args[++i]); break;
//         case "--facing": facing = float.Parse(args[++i], CultureInfo.InvariantCulture); break;
//         case "--no-crop": crop = false; break;
//         case "--pad": pad = int.Parse(args[++i]); break;
//         case "--canvas":
//           var wh = args[++i].Split('x');
//           canvasW = int.Parse(wh[0]);
//           canvasH = int.Parse(wh[1]);
//           break;
//         default:
//           Console.Error.WriteLine($"Unknown arg: {args[i]}");
//           return 1;
//       }
//     }

//     Directory.CreateDirectory(outDir);

//     using var resolver = new ResourceResolver(gameDir);
//     resolver.OpenArchives(CandidateMixes);

//     var anims = new SliceAnimations();

//     // 1) INDEX.DAT: header/palettes/animation table. Usually inside A.MIX.
//     byte[] indexData = resolver.GetResourceRequired("INDEX.DAT");
//     anims.Open(indexData);

//     // 2) COREANIM.DAT: primary page file (checked first in getFramePtr).
//     byte[]? coreAnim = resolver.GetResource("COREANIM.DAT");
//     if (coreAnim == null || !anims.OpenCoreAnim(coreAnim))
//       Console.Error.WriteLine("Warning: COREANIM.DAT not found or failed to open.");

//     // 3) Frame data: HDFRAMES.DAT, or split CDFRAMES files. Prefer loose (large files).
//     var framePages = new List<byte[]>();
//     void TryAddFrames(string name)
//     {
//       var bytes = resolver.GetResource(name);
//       if (bytes != null) framePages.Add(bytes);
//     }
//     TryAddFrames("HDFRAMES.DAT");
//     if (framePages.Count == 0)
//     {
//       for (int cd = 1; cd <= 4; cd++)
//       {
//         TryAddFrames($"CDFRAMES{cd}.DAT");
//         TryAddFrames(Path.Combine("CD" + cd, "CDFRAMES.DAT"));
//       }
//       TryAddFrames("CDFRAMES.DAT");
//     }
//     if (framePages.Count == 0 || !anims.OpenFrames(framePages))
//       Console.Error.WriteLine("Warning: no frame data (HDFRAMES/CDFRAMES) opened.");

//     var renderer = new SliceRenderer(anims);
//     var encoder = new PngEncoder { ColorType = PngColorType.RgbWithAlpha };

//     int start = onlyAnim ?? 0;
//     int end = onlyAnim.HasValue ? onlyAnim.Value + 1 : anims.Anims.Length;

//     for (int a = start; a < end; a++)
//     {
//       var anim = anims.Anims[a];
//       if (anim.FrameCount == 0) continue;

//       string animDir = Path.Combine(outDir, $"anim_{a:D4}");
//       Directory.CreateDirectory(animDir);

//       // ---- PASS 1: render all frames, capture each frame's own tight content ----
//       // bounds, and accumulate the union that tightly contains every frame. The
//       // overall canvas is derived from this union rather than the fixed render size.
//       var rendered = new Image<Rgba32>[anim.FrameCount];
//       var frameBounds = new CropInfo?[anim.FrameCount];

//       int unionMinX = int.MaxValue, unionMinY = int.MaxValue;
//       int unionMaxX = int.MinValue, unionMaxY = int.MinValue;

//       for (uint f = 0; f < anim.FrameCount; f++)
//       {
//         var result = renderer.RenderFrame((uint)a, f, canvasW, canvasH, facing);
//         rendered[f] = result.Image;

//         CropInfo? b = (crop && result.HasPixels)
//             ? ImageCrop.ComputeContentBounds(result.Image, pad)
//             : null;
//         frameBounds[f] = b;

//         if (b is CropInfo cb)
//         {
//           unionMinX = Math.Min(unionMinX, cb.OffsetX);
//           unionMinY = Math.Min(unionMinY, cb.OffsetY);
//           unionMaxX = Math.Max(unionMaxX, cb.OffsetX + cb.Width);
//           unionMaxY = Math.Max(unionMaxY, cb.OffsetY + cb.Height);
//         }
//       }

//       // Overall canvas = union of every frame's content. When nothing was drawn
//       // (or cropping is disabled) fall back to the full render surface.
//       int canvasWidth, canvasHeight;
//       if (crop && unionMaxX > unionMinX)
//       {
//         canvasWidth = unionMaxX - unionMinX;
//         canvasHeight = unionMaxY - unionMinY;
//       }
//       else
//       {
//         unionMinX = 0;
//         unionMinY = 0;
//         canvasWidth = canvasW;
//         canvasHeight = canvasH;
//       }

//       // The model pivot (screenX, screenY) is fixed for every frame; express it
//       // relative to the overall canvas so every frame aligns to the same origin.
//       int originX = (canvasW / 2) - unionMinX;
//       int originY = (canvasH / 2) - unionMinY;

//       // ---- PASS 2: crop each frame to its own content and record where that ----
//       // content sits within the shared canvas (offsetX/offsetY). Drawing every
//       // frame at its offset reconstructs the animation aligned to a single origin.
//       var frameEntries = new List<string>();
//       for (uint f = 0; f < anim.FrameCount; f++)
//       {
//         using var img = rendered[f];

//         int offX, offY, w, h;
//         if (frameBounds[f] is CropInfo fb)
//         {
//           ImageCrop.CropTo(img, fb);
//           offX = fb.OffsetX - unionMinX;
//           offY = fb.OffsetY - unionMinY;
//           w = fb.Width;
//           h = fb.Height;
//         }
//         else if (crop)
//         {
//           // Fully transparent frame: keep a 1x1 placeholder anchored at the origin.
//           ImageCrop.CropTo(img, new CropInfo(0, 0, 1, 1));
//           offX = originX;
//           offY = originY;
//           w = 1;
//           h = 1;
//         }
//         else
//         {
//           offX = 0;
//           offY = 0;
//           w = img.Width;
//           h = img.Height;
//         }

//         string fileName = $"frame_{f:D4}.png";
//         img.Save(Path.Combine(animDir, fileName), encoder);
//         frameEntries.Add(FrameJson(f, fileName, w, h, offX, offY));
//       }

//       File.WriteAllText(Path.Combine(animDir, "animation.json"),
//           AnimationJson(a, anim, canvasWidth, canvasHeight, originX, originY, crop, frameEntries));

//       Console.WriteLine($"anim {a}: exported {anim.FrameCount} frames " +
//                         $"(canvas {canvasWidth}x{canvasHeight}, origin {originX},{originY}, " +
//                         $"fps={anim.Fps.ToString(CultureInfo.InvariantCulture)}).");
//     }

//     anims.Dispose();
//     Console.WriteLine("Done.");
//     return 0;
//   }

//   private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
//   private static string FrameJson(uint index, string file, int w, int h, int offX, int offY)
//   {
//     // Each frame is cropped to its own content; offsetX/offsetY position it within
//     // the overall canvas so the animation can be reassembled precisely.
//     return "    {" +
//            $"\"index\": {index}, \"file\": \"{file}\", " +
//            $"\"width\": {w}, \"height\": {h}, " +
//            $"\"offsetX\": {offX}, \"offsetY\": {offY}" + "}";
//   }

//   private static string AnimationJson(int id, SliceAnimations.Animation a,
//                                       int canvasWidth, int canvasHeight, int originX, int originY,
//                                       bool cropped, List<string> frames)
//   {
//     var sb = new StringBuilder();
//     sb.AppendLine("{");
//     sb.AppendLine($"  \"animationId\": {id},");
//     sb.AppendLine($"  \"frameCount\": {a.FrameCount},");
//     sb.AppendLine($"  \"fps\": {F(a.Fps)},");
//     sb.AppendLine($"  \"facingChange\": {F(a.FacingChange)},");
//     sb.AppendLine("  \"positionChange\": {" +
//                   $"\"x\": {F(a.PositionChangeX)}, \"y\": {F(a.PositionChangeY)}, \"z\": {F(a.PositionChangeZ)}" + "},");
//     // Overall canvas that tightly contains every frame (derived from frame offsets/dimensions):
//     sb.AppendLine($"  \"canvas\": {{\"width\": {canvasWidth}, \"height\": {canvasHeight}}},");
//     // Pivot/origin (model center) in overall-canvas pixel coordinates — same for every frame:
//     sb.AppendLine($"  \"origin\": {{\"x\": {originX}, \"y\": {originY}}},");
//     sb.AppendLine($"  \"cropped\": {(cropped ? "true" : "false")},");
//     sb.AppendLine("  \"frames\": [");
//     sb.AppendLine(string.Join(",\n", frames));
//     sb.AppendLine("  ]");
//     sb.AppendLine("}");
//     return sb.ToString();
//   }
// }
