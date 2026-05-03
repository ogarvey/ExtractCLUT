using System.Numerics;
using System.Text;

namespace ExtractClut.Games.PC.Arcturus.ModelExportTool;

internal static class ArcturusModelParser
{
    public static ArcturusModel Load(string modelPath)
    {
        using var fs = File.OpenRead(modelPath);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

        string magic = Encoding.ASCII.GetString(br.ReadBytes(4));

        return magic switch
        {
            "GRSM" => ParseRsm(br, modelPath),
            "GRSX" => ParseRsx(br, modelPath),
            _ => throw new InvalidDataException($"Unsupported model magic '{magic}' in {modelPath}")
        };
    }

    private static ArcturusModel ParseRsm(BinaryReader br, string modelPath)
    {
        byte major = br.ReadByte();
        byte minor = br.ReadByte();

        if (major != 1 || minor > 4)
        {
            throw new InvalidDataException($"Unsupported GRSM version {major}.{minor}");
        }

        _ = br.ReadInt32(); // model-level unknown A
        _ = br.ReadInt32(); // model-level unknown B

        if (minor == 4)
        {
            _ = br.ReadByte(); // shading / mode byte
        }

        _ = br.ReadBytes(16); // reserved / transform block

        int textureNameCount = br.ReadInt32();
        for (int i = 0; i < textureNameCount; i++)
        {
            _ = ReadFixedString(br, 0x28);
        }

        string rootName = ReadFixedString(br, 0x28);
        int meshCount = br.ReadInt32();

        var model = new ArcturusModel
        {
            SourcePath = modelPath,
            Format = ArcturusModelFormat.Grsm,
            Name = string.IsNullOrWhiteSpace(rootName)
                ? Path.GetFileNameWithoutExtension(modelPath)
                : rootName
        };

        for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
        {
            var mesh = new ArcturusMesh
            {
                Name = ReadFixedString(br, 0x28),
                ParentName = ReadFixedString(br, 0x28)
            };

            int textureRefCount = br.ReadInt32();
            for (int i = 0; i < textureRefCount; i++)
            {
                _ = br.ReadInt32();
            }

            mesh.LocalMatrix = ReadAffine3x4(br);
            mesh.Position = ReadVector3(br);
            mesh.Angle = br.ReadSingle();
            mesh.Axis = ReadVector3(br);
            mesh.Scale = ReadVector3(br);

            int vertexCount = br.ReadInt32();
            mesh.Vertices.Capacity = Math.Max(0, vertexCount);
            for (int i = 0; i < vertexCount; i++)
            {
                mesh.Vertices.Add(ReadVector3(br));
            }

            int texVertexCount = br.ReadInt32();
            mesh.TextureVertices.Capacity = Math.Max(0, texVertexCount);
            if (minor == 1)
            {
                for (int i = 0; i < texVertexCount; i++)
                {
                    float u = br.ReadSingle();
                    float v = br.ReadSingle();
                    mesh.TextureVertices.Add(new Vector3(-1.0f, u, v));
                }
            }
            else
            {
                for (int i = 0; i < texVertexCount; i++)
                {
                    mesh.TextureVertices.Add(ReadVector3(br));
                }
            }

            int faceCount = br.ReadInt32();
            mesh.Faces.Capacity = Math.Max(0, faceCount);

            for (int i = 0; i < faceCount; i++)
            {
                if (minor == 1)
                {
                    var raw = new ushort[10];
                    for (int j = 0; j < raw.Length; j++)
                    {
                        raw[j] = br.ReadUInt16();
                    }

                    mesh.Faces.Add(new ArcturusFace(
                        raw[0], raw[1], raw[2],
                        raw[3], raw[4], raw[5],
                        raw[6], 0, 0, 0));
                }
                else
                {
                    mesh.Faces.Add(ReadFace24(br));
                }
            }

            int localAnimCount = br.ReadInt32();
            if (localAnimCount < 0)
            {
                throw new InvalidDataException($"Invalid GRSM local animation count {localAnimCount}");
            }

            _ = br.ReadBytes(localAnimCount * 0x14);

            model.Meshes.Add(mesh);
        }

        int modelAnimCount = br.ReadInt32();
        if (modelAnimCount > 0)
        {
            // Version 1.3+ uses 0x28 records, v1.2 uses 0x24 records.
            int stride = minor > 2 ? 0x28 : (minor == 2 ? 0x24 : 0);
            if (stride > 0)
            {
                _ = br.ReadBytes(modelAnimCount * stride);
            }
        }

        ApplyRsmHierarchyTransforms(model);

        return model;
    }

    private static ArcturusModel ParseRsx(BinaryReader br, string modelPath)
    {
        byte major = br.ReadByte();
        byte minor = br.ReadByte();

        // RSX loader accepts v1 and minor > 1 in current game build.
        if (major != 1 || minor <= 1)
        {
            throw new InvalidDataException($"Unsupported GRSX version {major}.{minor}");
        }

        if (minor > 1)
        {
            _ = br.ReadInt32(); // unknown
            _ = br.ReadBytes(0x0c);
        }

        if (minor == 3)
        {
            _ = br.ReadInt32(); // optional unknown
        }

        _ = br.ReadInt32(); // unknown
        int uvLayerCount = br.ReadInt32();
        _ = br.ReadInt32(); // unknown
        _ = br.ReadBytes(0x10); // reserved block

        int textureNameCount = br.ReadInt32();
        for (int i = 0; i < textureNameCount; i++)
        {
            _ = ReadFixedString(br, 0x28);
        }

        string rootName = ReadFixedString(br, 0x28);
        int meshCount = br.ReadInt32();

        var model = new ArcturusModel
        {
            SourcePath = modelPath,
            Format = ArcturusModelFormat.Grsx,
            Name = string.IsNullOrWhiteSpace(rootName)
                ? Path.GetFileNameWithoutExtension(modelPath)
                : rootName
        };

        for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
        {
            int sentryStart = br.ReadInt32();
            if (sentryStart != 0x12345678)
            {
                throw new InvalidDataException($"Invalid RSX block sentinel start: 0x{sentryStart:X8}");
            }

            var mesh = new ArcturusMesh
            {
                Name = ReadFixedString(br, 0x28),
                ParentName = ReadFixedString(br, 0x28)
            };

            int materialRefCount = br.ReadInt32();
            for (int i = 0; i < materialRefCount; i++)
            {
                _ = br.ReadInt32();
            }

            int vertexCount = br.ReadInt32();

            // The runtime allocates per-layer buffers of vec3. Layer 0 is enough for base OBJ/Cast validation.
            var layer0Vertices = new List<Vector3>(Math.Max(0, vertexCount));
            for (int layer = 0; layer < uvLayerCount; layer++)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    var value = ReadVector3(br);
                    if (layer == 0)
                    {
                        layer0Vertices.Add(value);
                    }
                }
            }

            mesh.Vertices.AddRange(layer0Vertices);

            int texVertexCount = br.ReadInt32();
            int sharedTexFlag = br.ReadInt32();

            if (sharedTexFlag == 0)
            {
                var layer0Tex = new List<Vector3>(Math.Max(0, texVertexCount));

                for (int layer = 0; layer < uvLayerCount; layer++)
                {
                    for (int i = 0; i < texVertexCount; i++)
                    {
                        var value = ReadVector3(br);
                        if (layer == 0)
                        {
                            layer0Tex.Add(value);
                        }
                    }
                }

                mesh.TextureVertices.AddRange(layer0Tex);
            }
            else
            {
                for (int i = 0; i < texVertexCount; i++)
                {
                    mesh.TextureVertices.Add(ReadVector3(br));
                }
            }

            int faceCount = br.ReadInt32();

            // Loader allocates an extra vec3 array not directly read from file here.
            _ = new Vector3[faceCount];

            mesh.Faces.Capacity = Math.Max(0, faceCount);
            for (int i = 0; i < faceCount; i++)
            {
                mesh.Faces.Add(ReadFace24(br));
            }

            int sentryEnd = br.ReadInt32();
            if (sentryEnd != 0x12345678)
            {
                throw new InvalidDataException($"Invalid RSX block sentinel end: 0x{sentryEnd:X8}");
            }

            model.Meshes.Add(mesh);
        }

        return model;
    }

    private static ArcturusFace ReadFace24(BinaryReader br)
    {
        ushort v0 = br.ReadUInt16();
        ushort v1 = br.ReadUInt16();
        ushort v2 = br.ReadUInt16();

        ushort t0 = br.ReadUInt16();
        ushort t1 = br.ReadUInt16();
        ushort t2 = br.ReadUInt16();

        ushort textureId = br.ReadUInt16();
        ushort flags = br.ReadUInt16();

        uint twoSided = br.ReadUInt32();
        uint smoothGroup = br.ReadUInt32();

        return new ArcturusFace(v0, v1, v2, t0, t1, t2, textureId, flags, twoSided, smoothGroup);
    }

    private static ArcturusAffine3x4 ReadAffine3x4(BinaryReader br)
    {
        float m00 = br.ReadSingle();
        float m01 = br.ReadSingle();
        float m02 = br.ReadSingle();

        float m10 = br.ReadSingle();
        float m11 = br.ReadSingle();
        float m12 = br.ReadSingle();

        float m20 = br.ReadSingle();
        float m21 = br.ReadSingle();
        float m22 = br.ReadSingle();

        float tx = br.ReadSingle();
        float ty = br.ReadSingle();
        float tz = br.ReadSingle();

        return new ArcturusAffine3x4(
            m00, m01, m02,
            m10, m11, m12,
            m20, m21, m22,
            tx, ty, tz);
    }

    private static void ApplyRsmHierarchyTransforms(ArcturusModel model)
    {
        if (model.Meshes.Count == 0)
        {
            return;
        }

        var meshByName = new Dictionary<string, ArcturusMesh>(StringComparer.OrdinalIgnoreCase);
        foreach (ArcturusMesh mesh in model.Meshes)
        {
            if (!string.IsNullOrWhiteSpace(mesh.Name) && !meshByName.ContainsKey(mesh.Name))
            {
                meshByName.Add(mesh.Name, mesh);
            }
        }

        var nodeWorldByMesh = new Dictionary<ArcturusMesh, ArcturusAffine3x4>(ReferenceEqualityComparer.Instance);

        foreach (ArcturusMesh mesh in model.Meshes)
        {
            ArcturusAffine3x4 nodeWorld = ResolveNodeWorld(
                mesh,
                meshByName,
                nodeWorldByMesh,
                new HashSet<ArcturusMesh>(ReferenceEqualityComparer.Instance));

            // Runtime render path applies: final = meshLocalMatrix(0x40) * nodeWorld.
            ArcturusAffine3x4 final = ComposeRowVectorAffine(mesh.LocalMatrix, nodeWorld);

            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                mesh.Vertices[i] = TransformPoint(final, mesh.Vertices[i]);
            }
        }
    }

    private static ArcturusAffine3x4 ResolveNodeWorld(
        ArcturusMesh mesh,
        IReadOnlyDictionary<string, ArcturusMesh> meshByName,
        IDictionary<ArcturusMesh, ArcturusAffine3x4> worldByMesh,
        ISet<ArcturusMesh> active)
    {
        if (worldByMesh.TryGetValue(mesh, out ArcturusAffine3x4 cached))
        {
            return cached;
        }

        if (!active.Add(mesh))
        {
            return BuildNodeMatrix(mesh);
        }

        ArcturusAffine3x4 world = BuildNodeMatrix(mesh);
        if (!string.IsNullOrWhiteSpace(mesh.ParentName) &&
            meshByName.TryGetValue(mesh.ParentName, out ArcturusMesh? parent) &&
            !ReferenceEquals(parent, mesh))
        {
            ArcturusAffine3x4 parentWorld = ResolveNodeWorld(parent, meshByName, worldByMesh, active);
            world = ComposeRowVectorAffine(world, parentWorld);
        }

        active.Remove(mesh);
        worldByMesh[mesh] = world;
        return world;
    }

    private static ArcturusAffine3x4 BuildNodeMatrix(ArcturusMesh mesh)
    {
        Vector3 axis = mesh.Axis;
        if (axis.LengthSquared() < 1e-12f)
        {
            axis = new Vector3(0f, 1f, 0f);
        }
        else
        {
            axis = Vector3.Normalize(axis);
        }

        float angle = mesh.Angle;
        if (MathF.Abs(angle) > (MathF.PI * 2.0f + 0.01f))
        {
            angle = angle * (MathF.PI / 180.0f);
        }

        float half = angle * 0.5f;
        float s = MathF.Sin(half);
        float c = MathF.Cos(half);

        float x = axis.X * s;
        float y = axis.Y * s;
        float z = axis.Z * s;
        float w = c;

        float xx2 = x + x;
        float yy2 = y + y;
        float zz2 = z + z;

        float m00 = 1.0f - (y * yy2 + z * zz2);
        float m01 = x * yy2 + w * zz2;
        float m02 = x * zz2 - w * yy2;

        float m10 = x * yy2 - w * zz2;
        float m11 = 1.0f - (x * xx2 + z * zz2);
        float m12 = y * zz2 + w * xx2;

        float m20 = x * zz2 + w * yy2;
        float m21 = y * zz2 - w * xx2;
        float m22 = 1.0f - (x * xx2 + y * yy2);

        // Runtime scales rows of this matrix by (sx, sy, sz).
        m00 *= mesh.Scale.X;
        m01 *= mesh.Scale.X;
        m02 *= mesh.Scale.X;

        m10 *= mesh.Scale.Y;
        m11 *= mesh.Scale.Y;
        m12 *= mesh.Scale.Y;

        m20 *= mesh.Scale.Z;
        m21 *= mesh.Scale.Z;
        m22 *= mesh.Scale.Z;

        return new ArcturusAffine3x4(
            m00, m01, m02,
            m10, m11, m12,
            m20, m21, m22,
            mesh.Position.X, mesh.Position.Y, mesh.Position.Z);
    }

    // Matches row-vector affine usage: world = local * parent.
    private static ArcturusAffine3x4 ComposeRowVectorAffine(ArcturusAffine3x4 local, ArcturusAffine3x4 parent)
    {
        float m00 = local.M00 * parent.M00 + local.M01 * parent.M10 + local.M02 * parent.M20;
        float m01 = local.M00 * parent.M01 + local.M01 * parent.M11 + local.M02 * parent.M21;
        float m02 = local.M00 * parent.M02 + local.M01 * parent.M12 + local.M02 * parent.M22;

        float m10 = local.M10 * parent.M00 + local.M11 * parent.M10 + local.M12 * parent.M20;
        float m11 = local.M10 * parent.M01 + local.M11 * parent.M11 + local.M12 * parent.M21;
        float m12 = local.M10 * parent.M02 + local.M11 * parent.M12 + local.M12 * parent.M22;

        float m20 = local.M20 * parent.M00 + local.M21 * parent.M10 + local.M22 * parent.M20;
        float m21 = local.M20 * parent.M01 + local.M21 * parent.M11 + local.M22 * parent.M21;
        float m22 = local.M20 * parent.M02 + local.M21 * parent.M12 + local.M22 * parent.M22;

        float tx = local.Tx * parent.M00 + local.Ty * parent.M10 + local.Tz * parent.M20 + parent.Tx;
        float ty = local.Tx * parent.M01 + local.Ty * parent.M11 + local.Tz * parent.M21 + parent.Ty;
        float tz = local.Tx * parent.M02 + local.Ty * parent.M12 + local.Tz * parent.M22 + parent.Tz;

        return new ArcturusAffine3x4(
            m00, m01, m02,
            m10, m11, m12,
            m20, m21, m22,
            tx, ty, tz);
    }

    private static Vector3 TransformPoint(ArcturusAffine3x4 affine, Vector3 p)
    {
        float x = p.X * affine.M00 + p.Y * affine.M10 + p.Z * affine.M20 + affine.Tx;
        float y = p.X * affine.M01 + p.Y * affine.M11 + p.Z * affine.M21 + affine.Ty;
        float z = p.X * affine.M02 + p.Y * affine.M12 + p.Z * affine.M22 + affine.Tz;
        return new Vector3(x, y, z);
    }

    private static Vector3 ReadVector3(BinaryReader br)
    {
        return new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
    }

    private static string ReadFixedString(BinaryReader br, int byteLength)
    {
        byte[] bytes = br.ReadBytes(byteLength);

        int nullPos = Array.IndexOf(bytes, (byte)0);
        if (nullPos < 0)
        {
            nullPos = bytes.Length;
        }

        return Encoding.ASCII.GetString(bytes, 0, nullPos).Trim();
    }

}
