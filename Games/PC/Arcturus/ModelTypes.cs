using System.Numerics;

namespace ExtractClut.Games.PC.Arcturus.ModelExportTool;

internal enum ArcturusModelFormat
{
    Unknown = 0,
    Grsm = 1,
    Grsx = 2
}

internal readonly struct ArcturusAffine3x4
{
    public readonly float M00;
    public readonly float M01;
    public readonly float M02;
    public readonly float M10;
    public readonly float M11;
    public readonly float M12;
    public readonly float M20;
    public readonly float M21;
    public readonly float M22;
    public readonly float Tx;
    public readonly float Ty;
    public readonly float Tz;

    public static ArcturusAffine3x4 Identity => new(
        1, 0, 0,
        0, 1, 0,
        0, 0, 1,
        0, 0, 0);

    public ArcturusAffine3x4(
        float m00, float m01, float m02,
        float m10, float m11, float m12,
        float m20, float m21, float m22,
        float tx, float ty, float tz)
    {
        M00 = m00;
        M01 = m01;
        M02 = m02;
        M10 = m10;
        M11 = m11;
        M12 = m12;
        M20 = m20;
        M21 = m21;
        M22 = m22;
        Tx = tx;
        Ty = ty;
        Tz = tz;
    }
}

internal sealed class ArcturusModel
{
    public string SourcePath { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ArcturusModelFormat Format { get; set; } = ArcturusModelFormat.Unknown;
    public List<ArcturusMesh> Meshes { get; } = new();
}

internal sealed class ArcturusMesh
{
    public string Name { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;

    // RSM metadata used for hierarchy placement.
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Vector3 Axis { get; set; } = new Vector3(0f, 1f, 0f);
    public float Angle { get; set; } = 0f;
    public Vector3 Scale { get; set; } = new Vector3(1f, 1f, 1f);
    public ArcturusAffine3x4 LocalMatrix { get; set; } = ArcturusAffine3x4.Identity;

    public List<Vector3> Vertices { get; } = new();
    public List<Vector3> TextureVertices { get; } = new();
    public List<ArcturusFace> Faces { get; } = new();
}

internal readonly struct ArcturusFace
{
    public readonly ushort Vertex0;
    public readonly ushort Vertex1;
    public readonly ushort Vertex2;

    public readonly ushort Tex0;
    public readonly ushort Tex1;
    public readonly ushort Tex2;

    public readonly ushort TextureId;
    public readonly ushort Flags;
    public readonly uint TwoSided;
    public readonly uint SmoothGroup;

    public ArcturusFace(ushort v0, ushort v1, ushort v2, ushort t0, ushort t1, ushort t2, ushort textureId, ushort flags, uint twoSided, uint smoothGroup)
    {
        Vertex0 = v0;
        Vertex1 = v1;
        Vertex2 = v2;
        Tex0 = t0;
        Tex1 = t1;
        Tex2 = t2;
        TextureId = textureId;
        Flags = flags;
        TwoSided = twoSided;
        SmoothGroup = smoothGroup;
    }
}
