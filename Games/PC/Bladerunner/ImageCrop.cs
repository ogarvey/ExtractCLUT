using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BladeRunnerSliceExporter;

public readonly record struct CropInfo(int OffsetX, int OffsetY, int Width, int Height);

// Accumulates the union of non-transparent bounds across many frames.
public struct BoundsAccumulator
{
    public int MinX, MinY, MaxX, MaxY;
    public bool Any;

    public static BoundsAccumulator Empty => new() { MinX = int.MaxValue, MinY = int.MaxValue, MaxX = -1, MaxY = -1, Any = false };

    public void Add(Image<Rgba32> img)
    {
        for (int y = 0; y < img.Height; y++)
        {
            var row = img.DangerousGetPixelRowMemory(y).Span;
            for (int x = 0; x < img.Width; x++)
            {
                if (row[x].A != 0)
                {
                    if (x < MinX) MinX = x;
                    if (x > MaxX) MaxX = x;
                    if (y < MinY) MinY = y;
                    if (y > MaxY) MaxY = y;
                    Any = true;
                }
            }
        }
    }

    // Final union rectangle (with padding), clamped to the canvas.
    public CropInfo? ToRect(int canvasW, int canvasH, int padding)
    {
        if (!Any) return null;
        int minX = Math.Max(0, MinX - padding);
        int minY = Math.Max(0, MinY - padding);
        int maxX = Math.Min(canvasW - 1, MaxX + padding);
        int maxY = Math.Min(canvasH - 1, MaxY + padding);
        return new CropInfo(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}

public static class ImageCrop
{
    // Crops an image to an explicit rectangle (used for uniform per-animation crop).
    public static void CropTo(Image<Rgba32> img, CropInfo rect)
    {
        img.Mutate(ctx => ctx.Crop(new Rectangle(rect.OffsetX, rect.OffsetY, rect.Width, rect.Height)));
    }
}
