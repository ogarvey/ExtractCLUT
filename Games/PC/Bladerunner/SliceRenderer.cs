using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace BladeRunnerSliceExporter;

// Port of the offline-capable path of BladeRunner::SliceRenderer:
// loadFrame + drawOnScreen + drawSlice (engines/bladerunner/slice_renderer.cpp).
public sealed class SliceRenderer
{
    private readonly SliceAnimations _anims;

    private float _frameScaleX, _frameScaleY;
    private float _frameSliceHeight;
    private float _framePosX, _framePosY;
    private float _frameBottomZ;
    private uint _framePaletteIndex;
    private uint _frameSliceCount;

    private byte[] _framePage = Array.Empty<byte>();
    private int _frameBase;

    private readonly int[] _m11 = new int[256];
    private readonly int[] _m12 = new int[256];
    private readonly int[] _m21 = new int[256];
    private readonly int[] _m22 = new int[256];
    private int _m13, _m23;

    public SliceRenderer(SliceAnimations anims) => _anims = anims;

    public readonly record struct FrameResult(Image<Rgba32> Image, bool HasPixels);

    private void LoadFrame(uint animation, uint frame)
    {
        (_framePage, _frameBase) = _anims.GetFrame(animation, frame);
        int p = _frameBase;
        _frameScaleX = BitConverter.ToSingle(_framePage, p); p += 4;
        _frameScaleY = BitConverter.ToSingle(_framePage, p); p += 4;
        _frameSliceHeight = BitConverter.ToSingle(_framePage, p); p += 4;
        _framePosX = BitConverter.ToSingle(_framePage, p); p += 4;
        _framePosY = BitConverter.ToSingle(_framePage, p); p += 4;
        _frameBottomZ = BitConverter.ToSingle(_framePage, p); p += 4;
        _framePaletteIndex = BitConverter.ToUInt32(_framePage, p); p += 4;
        _frameSliceCount = BitConverter.ToUInt32(_framePage, p); p += 4;
    }

    private static void SetupLookupTable(int[] t, int inc)
    {
        int v = 0;
        for (int i = 0; i != 256; ++i) { t[i] = v; v += inc; }
    }

    // Renders one frame using the drawOnScreen algorithm.
    // Uncovered pixels remain transparent. Returns whether anything was drawn.
    public FrameResult RenderFrame(uint animation, uint frame, int canvasW, int canvasH,
                                   float facing = 0.0f, float? scaleOverride = null)
    {
        LoadFrame(animation, frame);

        var img = new Image<Rgba32>(canvasW, canvasH, new Rgba32(0, 0, 0, 0));
        if (_frameSliceCount == 0)
            return new FrameResult(img, false);

        // ---- drawOnScreen setup ----
        float frameHeight = _frameSliceHeight * _frameSliceCount;
        float frameSize = MathF.Sqrt(
            _frameScaleX * 255.0f * _frameScaleX * 255.0f +
            _frameScaleY * 255.0f * _frameScaleY * 255.0f);

        float scale = scaleOverride ?? (0.9f * canvasH);
        float size = scale / MathF.Max(frameSize, frameHeight);

        int screenX = canvasW / 2;
        int screenY = canvasH / 2;

        float s = MathF.Sin(facing);
        float c = MathF.Cos(facing);

        var mRotation = new Matrix3x2f(c, -s, 0f, s, c, 0f);
        var mFrame = new Matrix3x2f(_frameScaleX, 0f, _framePosX, 0f, _frameScaleY, _framePosY);
        var mScale = new Matrix3x2f(size, 0f, 0f, 0f, 25.5f, 0f);
        var mTranslate = new Matrix3x2f(1f, 0f, screenX, 0f, 1f, 32768.0f);
        var mScaleFixed = new Matrix3x2f(65536.0f, 0f, 0f, 0f, 64.0f, 0f);

        var m = mScaleFixed * (mTranslate * (mScale * (mRotation * mFrame)));

        SetupLookupTable(_m11, (int)m.Get(0, 0));
        SetupLookupTable(_m12, (int)m.Get(0, 1));
        _m13 = (int)m.Get(0, 2);
        SetupLookupTable(_m21, (int)m.Get(1, 0));
        SetupLookupTable(_m22, (int)m.Get(1, 1));
        _m23 = (int)m.Get(1, 2);

        int frameY = (int)(screenY + (size / 2.0f * frameHeight));
        int currentY = frameY;
        float currentSlice = 0f;
        float sliceStep = 1.0f / size / _frameSliceHeight;

        var zLine = new ushort[canvasW];
        bool hasPixels = false;

        // Guard against the original's infinite-loop quirk for off-screen rows.
        int safety = (int)_frameSliceCount * 4 + canvasH + 16;
        while (currentSlice < _frameSliceCount && safety-- > 0)
        {
            if (currentY >= 0 && currentY < canvasH)
            {
                Array.Fill(zLine, ushort.MaxValue);
                hasPixels |= DrawSlice((int)currentSlice, currentY, img, zLine, canvasW);
                currentSlice += sliceStep;
                --currentY;
            }
            else if (currentY < 0)
            {
                // currentY only ever decreases; once above the top edge we're done.
                // (Prevents the original's theoretical infinite loop off-screen.)
                break;
            }
            else // currentY >= canvasH (below the bottom edge): skip down until on-screen.
            {
                --currentY;
            }
        }

        return new FrameResult(img, hasPixels);
    }

    // Mirrors SliceRenderer::drawSlice (advanced == false: raw palette color).
    private bool DrawSlice(int slice, int y, Image<Rgba32> img, ushort[] zLine, int w)
    {
        if (slice < 0 || (uint)slice >= _frameSliceCount)
            return false;

        var pal = _anims.Palettes[_framePaletteIndex];

        int p = _frameBase + 0x20 + 4 * slice;
        uint polyOffset = ReadU32(_framePage, p);

        p = _frameBase + (int)polyOffset;
        uint polyCount = ReadU32(_framePage, p);
        p += 4;

        var row = img.DangerousGetPixelRowMemory(y).Span;
        bool drew = false;

        while (polyCount-- > 0)
        {
            uint vertexCount = ReadU32(_framePage, p);
            p += 4;
            if (vertexCount == 0)
                continue;

            uint lastVertex = vertexCount - 1;
            int lastVx = Math.Max(
                (_m11[_framePage[p + 3 * lastVertex]] +
                 _m12[_framePage[p + 3 * lastVertex + 1]] + _m13) / 65536, 0);
            int previousVx = lastVx;

            while (vertexCount-- > 0)
            {
                int x0 = _framePage[p + 0];
                int y0 = _framePage[p + 1];
                int colIdx = _framePage[p + 2];

                int vertexX = Clamp((_m11[x0] + _m12[y0] + _m13) / 65536, 0, w);

                if (vertexX > previousVx)
                {
                    int vertexZ = (_m21[x0] + _m22[y0] + _m23) / 64;
                    if (vertexZ >= 0 && vertexZ < 65536)
                    {
                        var color = new Rgba32(pal.R[colIdx], pal.G[colIdx], pal.B[colIdx], 255);
                        for (int x = previousVx; x != vertexX; ++x)
                        {
                            if ((uint)x < (uint)w && vertexZ < zLine[x])
                            {
                                zLine[x] = (ushort)vertexZ;
                                row[x] = color;
                                drew = true;
                            }
                        }
                    }
                }
                p += 3;
                previousVx = vertexX;
            }
        }
        return drew;
    }

    private static uint ReadU32(byte[] b, int off) =>
        (uint)(b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24));

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
}
