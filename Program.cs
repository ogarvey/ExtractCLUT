using System.Globalization;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace BladeRunnerSliceExporter;

// Usage:
//   BladeRunnerSliceExporter <gameDir> <outDir>
//        [--anim N] [--canvas WxH] [--facing rad] [--no-crop] [--pad N]
//
// Resource lookup mirrors the engine: loose files on disk first, then MIX archives
// (A.MIX holds INDEX.DAT / PALETTES.DAT). COREANIM.DAT / HDFRAMES.DAT are usually loose.
internal static class Program
{
  // Outer MIX archives that may contain INDEX.DAT etc. Order = search priority.
  private static readonly string[] CandidateMixes =
  {
        "A.MIX", "STARTUP.MIX"
    };

  private static int Main(string[] args)
  {
    if (args.Length < 2)
    {
      Console.Error.WriteLine(
          "Usage: BladeRunnerSliceExporter <gameDir> <outDir> " +
          "[--anim N] [--canvas WxH] [--facing rad] [--no-crop] [--pad N]");
      return 1;
    }

    string gameDir = args[0];
    string outDir = args[1];
    int? onlyAnim = null;
    int canvasW = 400, canvasH = 480;
    float facing = 0f;
    bool crop = true;
    int pad = 0;

    for (int i = 2; i < args.Length; i++)
    {
      switch (args[i])
      {
        case "--anim": onlyAnim = int.Parse(args[++i]); break;
        case "--facing": facing = float.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--no-crop": crop = false; break;
        case "--pad": pad = int.Parse(args[++i]); break;
        case "--canvas":
          var wh = args[++i].Split('x');
          canvasW = int.Parse(wh[0]);
          canvasH = int.Parse(wh[1]);
          break;
        default:
          Console.Error.WriteLine($"Unknown arg: {args[i]}");
          return 1;
      }
    }

    Directory.CreateDirectory(outDir);

    using var resolver = new ResourceResolver(gameDir);
    resolver.OpenArchives(CandidateMixes);

    var anims = new SliceAnimations();

    // 1) INDEX.DAT: header/palettes/animation table. Usually inside A.MIX.
    byte[] indexData = resolver.GetResourceRequired("INDEX.DAT");
    anims.Open(indexData);

    // 2) COREANIM.DAT: primary page file (checked first in getFramePtr).
    byte[]? coreAnim = resolver.GetResource("COREANIM.DAT");
    if (coreAnim == null || !anims.OpenCoreAnim(coreAnim))
      Console.Error.WriteLine("Warning: COREANIM.DAT not found or failed to open.");

    // 3) Frame data: HDFRAMES.DAT, or split CDFRAMES files. Prefer loose (large files).
    var framePages = new List<byte[]>();
    void TryAddFrames(string name)
    {
      var bytes = resolver.GetResource(name);
      if (bytes != null) framePages.Add(bytes);
    }
    TryAddFrames("HDFRAMES.DAT");
    if (framePages.Count == 0)
    {
      for (int cd = 1; cd <= 4; cd++)
      {
        TryAddFrames($"CDFRAMES{cd}.DAT");
        TryAddFrames(Path.Combine("CD" + cd, "CDFRAMES.DAT"));
      }
      TryAddFrames("CDFRAMES.DAT");
    }
    if (framePages.Count == 0 || !anims.OpenFrames(framePages))
      Console.Error.WriteLine("Warning: no frame data (HDFRAMES/CDFRAMES) opened.");

    var renderer = new SliceRenderer(anims);
    var encoder = new PngEncoder { ColorType = PngColorType.RgbWithAlpha };

    int start = onlyAnim ?? 0;
    int end = onlyAnim.HasValue ? onlyAnim.Value + 1 : anims.Anims.Length;

    for (int a = start; a < end; a++)
    {
      var anim = anims.Anims[a];
      if (anim.FrameCount == 0) continue;

      string animDir = Path.Combine(outDir, $"anim_{a:D4}");
      Directory.CreateDirectory(animDir);

      // ---- PASS 1: render all frames, compute the animation-wide union bounds ----
      var rendered = new Image<Rgba32>[anim.FrameCount];
      var bounds = BoundsAccumulator.Empty;

      for (uint f = 0; f < anim.FrameCount; f++)
      {
        var result = renderer.RenderFrame((uint)a, f, canvasW, canvasH, facing);
        rendered[f] = result.Image;
        if (result.HasPixels)
          bounds.Add(result.Image);
      }

      // One rectangle for the whole animation. If nothing was drawn, fall back to full canvas.
      CropInfo cropRect = (crop ? bounds.ToRect(canvasW, canvasH, pad) : null)
                          ?? new CropInfo(0, 0, canvasW, canvasH);

      // The model pivot (screenX, screenY) is fixed for every frame; express it
      // relative to the crop so callers can align frames precisely.
      int originX = (canvasW / 2) - cropRect.OffsetX;
      int originY = (canvasH / 2) - cropRect.OffsetY;

      // ---- PASS 2: crop every frame identically and save ----
      var frameEntries = new List<string>();
      for (uint f = 0; f < anim.FrameCount; f++)
      {
        using var img = rendered[f];
        if (crop)
          ImageCrop.CropTo(img, cropRect);

        string fileName = $"frame_{f:D4}.png";
        img.Save(Path.Combine(animDir, fileName), encoder);
        frameEntries.Add(FrameJson(f, fileName, img.Width, img.Height));
      }

      File.WriteAllText(Path.Combine(animDir, "animation.json"),
          AnimationJson(a, anim, cropRect, originX, originY, crop, frameEntries));

      Console.WriteLine($"anim {a}: exported {anim.FrameCount} frames " +
                        $"({cropRect.Width}x{cropRect.Height}, origin {originX},{originY}, " +
                        $"fps={anim.Fps.ToString(CultureInfo.InvariantCulture)}).");
    }

    anims.Dispose();
    Console.WriteLine("Done.");
    return 0;
  }

  private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
  private static string FrameJson(uint index, string file, int w, int h)
  {
    // All frames share the animation-level width/height/origin now,
    // but we still emit per-frame w/h for convenience/validation.
    return "    {" +
           $"\"index\": {index}, \"file\": \"{file}\", " +
           $"\"width\": {w}, \"height\": {h}" + "}";
  }

  private static string AnimationJson(int id, SliceAnimations.Animation a,
                                      CropInfo rect, int originX, int originY,
                                      bool cropped, List<string> frames)
  {
    var sb = new StringBuilder();
    sb.AppendLine("{");
    sb.AppendLine($"  \"animationId\": {id},");
    sb.AppendLine($"  \"frameCount\": {a.FrameCount},");
    sb.AppendLine($"  \"fps\": {F(a.Fps)},");
    sb.AppendLine($"  \"facingChange\": {F(a.FacingChange)},");
    sb.AppendLine("  \"positionChange\": {" +
                  $"\"x\": {F(a.PositionChangeX)}, \"y\": {F(a.PositionChangeY)}, \"z\": {F(a.PositionChangeZ)}" + "},");
    // Uniform frame size for the whole animation:
    sb.AppendLine($"  \"frameWidth\": {rect.Width},");
    sb.AppendLine($"  \"frameHeight\": {rect.Height},");
    // Pivot/origin (model center) in cropped-frame pixel coordinates — same for every frame:
    sb.AppendLine($"  \"origin\": {{\"x\": {originX}, \"y\": {originY}}},");
    sb.AppendLine($"  \"cropped\": {(cropped ? "true" : "false")},");
    sb.AppendLine("  \"frames\": [");
    sb.AppendLine(string.Join(",\n", frames));
    sb.AppendLine("  ]");
    sb.AppendLine("}");
    return sb.ToString();
  }
}
