using System.Globalization;
using System.Numerics;
using Cast.NET;
using Cast.NET.Nodes;

namespace ExtractClut.Games.PC.Arcturus.ModelExportTool;

internal static class ModelExporters
{
    public static void ExportObj(ArcturusModel model, string outputPath, bool flipV)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        using var sw = new StreamWriter(outputPath, false);

        sw.WriteLine($"# Exported from {Path.GetFileName(model.SourcePath)}");
        sw.WriteLine($"# Meshes: {model.Meshes.Count}");

        int vertexBase = 1;
        int uvBase = 1;

        foreach (var mesh in model.Meshes)
        {
            string meshName = string.IsNullOrWhiteSpace(mesh.Name) ? "mesh" : mesh.Name;
            sw.WriteLine();
            sw.WriteLine($"o {SanitizeObjName(meshName)}");

            foreach (var v in mesh.Vertices)
            {
                var p = ToObjSpace(v, model.Format);
                sw.WriteLine(FormattableString.Invariant($"v {p.X} {p.Y} {p.Z}"));
            }

            foreach (var tv in mesh.TextureVertices)
            {
                var uv = ConvertTexVertexToUv(tv, flipV);
                sw.WriteLine(FormattableString.Invariant($"vt {uv.X} {uv.Y}"));
            }

            foreach (var f in mesh.Faces)
            {
                if ((uint)f.Vertex0 >= mesh.Vertices.Count
                    || (uint)f.Vertex1 >= mesh.Vertices.Count
                    || (uint)f.Vertex2 >= mesh.Vertices.Count)
                {
                    continue;
                }

                bool hasUv = mesh.TextureVertices.Count > 0
                    && f.Tex0 < mesh.TextureVertices.Count
                    && f.Tex1 < mesh.TextureVertices.Count
                    && f.Tex2 < mesh.TextureVertices.Count;

                if (hasUv)
                {
                    sw.WriteLine($"f {vertexBase + f.Vertex0}/{uvBase + f.Tex0} {vertexBase + f.Vertex1}/{uvBase + f.Tex1} {vertexBase + f.Vertex2}/{uvBase + f.Tex2}");
                }
                else
                {
                    sw.WriteLine($"f {vertexBase + f.Vertex0} {vertexBase + f.Vertex1} {vertexBase + f.Vertex2}");
                }
            }

            vertexBase += mesh.Vertices.Count;
            uvBase += mesh.TextureVertices.Count;
        }
    }

    public static void ExportCast(ArcturusModel model, string outputPath, bool flipV)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        var root = new CastNode(CastNodeIdentifier.Root);
        var modelNode = root.AddNode<ModelNode>();
        modelNode.AddString("n", model.Name);

        foreach (var srcMesh in model.Meshes)
        {
            if (srcMesh.Vertices.Count == 0 || srcMesh.Faces.Count == 0)
            {
                continue;
            }

            var meshNode = modelNode.AddNode<MeshNode>();
            meshNode.Name = string.IsNullOrWhiteSpace(srcMesh.Name) ? "mesh" : srcMesh.Name;

            meshNode.AddArray("vp", srcMesh.Vertices);

            if (srcMesh.TextureVertices.Count > 0)
            {
                meshNode.UVLayerCount = 1;

                var uvLayer = srcMesh.TextureVertices
                    .Select(v => ConvertTexVertexToUv(v, flipV))
                    .ToList();

                meshNode.AddUVLayer(0, new CastArrayProperty<Vector2>(uvLayer));
            }

            // Cast face buffer is vertex indices only; UVs are linked by matching vertex stream index.
            // For Arcturus meshes, UV indices are explicit and can diverge from vertex indices.
            // To preserve this in Cast, we de-index into a unified vertex stream first.
            BuildUnifiedStreams(srcMesh, flipV,
                out List<Vector3> unifiedPos,
                out List<Vector2> unifiedUv,
                out List<int> faceIndices);

            for (int i = 0; i < unifiedPos.Count; i++)
            {
                unifiedPos[i] = ToCastSpace(unifiedPos[i], model.Format);
            }

            meshNode.AddArray("vp", unifiedPos);
            meshNode.UVLayerCount = 1;
            meshNode.AddUVLayer(0, new CastArrayProperty<Vector2>(unifiedUv));

            if (unifiedPos.Count <= byte.MaxValue)
            {
                meshNode.AddArray("f", faceIndices.Select(i => (byte)i).ToList());
            }
            else if (unifiedPos.Count <= ushort.MaxValue)
            {
                meshNode.AddArray("f", faceIndices.Select(i => (ushort)i).ToList());
            }
            else
            {
                meshNode.AddArray("f", faceIndices.Select(i => (uint)i).ToList());
            }
        }

        CastWriter.Save(outputPath, root);
    }

    private static void BuildUnifiedStreams(
        ArcturusMesh mesh,
        bool flipV,
        out List<Vector3> positions,
        out List<Vector2> uvs,
        out List<int> faceIndices)
    {
        var localPositions = new List<Vector3>();
        var localUvs = new List<Vector2>();
        var localFaceIndices = new List<int>(mesh.Faces.Count * 3);

        var map = new Dictionary<(int v, int t), int>();

        foreach (var face in mesh.Faces)
        {
            if ((uint)face.Vertex0 >= mesh.Vertices.Count
                || (uint)face.Vertex1 >= mesh.Vertices.Count
                || (uint)face.Vertex2 >= mesh.Vertices.Count)
            {
                continue;
            }

            AppendCorner(face.Vertex0, face.Tex0);
            AppendCorner(face.Vertex1, face.Tex1);
            AppendCorner(face.Vertex2, face.Tex2);
        }

        void AppendCorner(int vertexIndex, int texIndex)
        {
            if ((uint)vertexIndex >= mesh.Vertices.Count)
            {
                return;
            }

            int clampedTex = (uint)texIndex < mesh.TextureVertices.Count ? texIndex : 0;
            var key = (vertexIndex, clampedTex);

            if (!map.TryGetValue(key, out int unifiedIndex))
            {
                unifiedIndex = localPositions.Count;
                map[key] = unifiedIndex;

                localPositions.Add(mesh.Vertices[vertexIndex]);

                Vector2 uv = mesh.TextureVertices.Count == 0
                    ? Vector2.Zero
                    : ConvertTexVertexToUv(mesh.TextureVertices[clampedTex], flipV);

                localUvs.Add(uv);
            }

            localFaceIndices.Add(unifiedIndex);
        }

        positions = localPositions;
        uvs = localUvs;
        faceIndices = localFaceIndices;
    }

    private static Vector2 ConvertTexVertexToUv(Vector3 texVertex, bool flipV)
    {
        // Arcturus stores texture vectors as vec3; in legacy records, X is often sentinel (-1),
        // while Y/Z contain UV values.
        float u = texVertex.Y;
        float v = texVertex.Z;

        if (flipV)
        {
            v = 1.0f - v;
        }

        return new Vector2(u, v);
    }

    private static Vector3 ToObjSpace(Vector3 value, ArcturusModelFormat format)
    {
        // Blender OBJ import check: RSX currently appears vertically inverted without this conversion.
        return format switch
        {
            ArcturusModelFormat.Grsx => new Vector3(value.X, value.Y, -value.Z),
            _ => value
        };
    }

    private static Vector3 ToCastSpace(Vector3 value, ArcturusModelFormat format)
    {
        // Cast scene check: RSX currently appears +90deg around X without this conversion.
        return format switch
        {
            ArcturusModelFormat.Grsx => new Vector3(value.X, value.Z, -value.Y),
            _ => value
        };
    }

    private static string SanitizeObjName(string name)
    {
        var chars = name
            .Select(c => char.IsWhiteSpace(c) ? '_' : c)
            .ToArray();

        return new string(chars);
    }
}
