using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace ExtractCLUT.Games.PC.Bullfrog.DungeonKeeper2
{
    /// <summary>
    /// Dungeon Keeper 2 KMF model file parser (see DKII_Formats.md section 3).
    ///
    /// Chunked little-endian format: KMSH -> HEAD (+ MATL) -> MESH | ANIM | GROP.
    /// MESH  = static model: shared positions (GEOM) + per-sprite (sub-mesh) triangles/vertices, LODs.
    /// ANIM  = per-vertex morph animation: packed 10-bit key positions (GEOM) indexed
    ///         via ITAB (per-128-frame base) + VGEO (per-frame byte offsets).
    /// GROP  = a group node placing multiple mesh KMFs at offsets.
    /// </summary>
    public sealed class KmfFile
    {
        public enum KmfType { Mesh = 1, Anim = 2, Grop = 3 }

        public int Version { get; private set; }
        public KmfType Type { get; private set; }
        /// <summary>Bytes left unread after parsing; should be 0 for a fully understood file.</summary>
        public long TrailingBytes { get; private set; }
        public List<KmfMaterial> Materials { get; } = new();
        public KmfMesh? Mesh { get; private set; }
        public KmfAnim? Anim { get; private set; }
        public List<KmfGropElement> Grops { get; } = new();

        #region Data classes

        public sealed class KmfMaterial
        {
            public required string Name { get; init; }
            public required List<string> Textures { get; init; }
            public uint Flags { get; init; }
            public float Brightness { get; init; }
            public float Shininess { get; init; }
            public required string EnvironmentMappingTexture { get; init; }
        }

        public readonly record struct KmfTriangle(byte A, byte B, byte C);
        public readonly record struct KmfUv(ushort U, ushort V);

        public sealed class KmfMesh
        {
            public required string Name { get; init; }
            public Vector3 Pos { get; init; }
            public float Scale { get; init; }
            public required List<KmfMeshSprite> Sprites { get; init; }
            public required List<Vector3> Geometries { get; init; }
        }

        public sealed class KmfMeshSprite
        {
            public uint MaterialIndex { get; set; }
            public float MmFactor { get; init; }
            /// <summary>Triangle lists, one per LOD (index 0 = full detail).</summary>
            public List<KmfTriangle[]> TrianglesPerLod { get; } = new();
            public List<KmfMeshVertex> Vertices { get; } = new();
            internal uint[] TriangleCounts = Array.Empty<uint>();
            internal uint VertexCount;
        }

        public readonly record struct KmfMeshVertex(ushort GeomIndex, KmfUv Uv, Vector3 Normal);

        public sealed class KmfAnim
        {
            public required string Name { get; init; }
            public int Frames { get; init; }
            public int Indexes { get; init; }
            public Vector3 Pos { get; init; }
            public float CubeScale { get; init; }
            public float Scale { get; init; }
            /// <summary>0 = clamp, 1 = wrap.</summary>
            public uint FrameFactorFunction { get; init; }
            public required List<KmfAnimSprite> Sprites { get; init; }
            /// <summary>[frameChunk (128 frames each)][itabIndex] - base geometry index.</summary>
            public required uint[][] Itab { get; init; }
            public required List<KmfAnimGeom> Geometries { get; init; }
            /// <summary>[itabIndex][frame] - byte offset added to the ITAB base.</summary>
            public required byte[][] Offsets { get; init; }
        }

        public sealed class KmfAnimSprite
        {
            public uint MaterialIndex { get; set; }
            public float MmFactor { get; init; }
            public List<KmfTriangle[]> TrianglesPerLod { get; } = new();
            public List<KmfAnimVertex> Vertices { get; } = new();
            internal uint[] TriangleCounts = Array.Empty<uint>();
            internal uint VertexCount;
        }

        public readonly record struct KmfAnimVertex(KmfUv Uv, Vector3 Normal, ushort ItabIndex);

        public readonly record struct KmfAnimGeom(Vector3 Position, byte FrameBase);

        public sealed class KmfGropElement
        {
            public required string Name { get; init; }
            public Vector3 Pos { get; init; }
        }

        #endregion

        public static KmfFile Load(string path) => Parse(File.ReadAllBytes(path));

        public static KmfFile Parse(byte[] data)
        {
            var kmf = new KmfFile();
            using var r = new BinaryReader(new MemoryStream(data));

            ExpectTag(r, "KMSH");
            r.ReadUInt32(); // section size
            kmf.Version = r.ReadInt32();

            ExpectTag(r, "HEAD");
            r.ReadUInt32(); // section size
            kmf.Type = (KmfType)r.ReadUInt32();
            r.ReadUInt32(); // unknown (=1)

            if (kmf.Type != KmfType.Grop)
            {
                ExpectTag(r, "MATL");
                kmf.ParseMaterials(r);
            }

            string tag = ReadTag(r);
            switch (kmf.Type)
            {
                case KmfType.Mesh when tag == "MESH":
                    kmf.Mesh = ParseMesh(r);
                    break;
                case KmfType.Anim when tag == "ANIM":
                    kmf.Anim = ParseAnim(r);
                    break;
                case KmfType.Grop when tag == "GROP":
                    kmf.ParseGrop(r);
                    break;
                default:
                    throw new InvalidDataException($"Unexpected chunk '{tag}' for type {kmf.Type}.");
            }

            kmf.TrailingBytes = r.BaseStream.Length - r.BaseStream.Position;
            return kmf;
        }

        #region Section parsers

        private void ParseMaterials(BinaryReader r)
        {
            r.ReadUInt32(); // section size
            uint count = r.ReadUInt32();
            for (int i = 0; i < count; i++)
            {
                ExpectTag(r, "MAT2");
                r.ReadUInt32(); // section size

                string name = ReadCString(r);
                uint textureCount = r.ReadUInt32();
                var textures = new List<string>((int)textureCount);
                for (int t = 0; t < textureCount; t++)
                    textures.Add(ReadCString(r));

                Materials.Add(new KmfMaterial
                {
                    Name = name,
                    Textures = textures,
                    Flags = r.ReadUInt32(),
                    Brightness = r.ReadSingle(),
                    Shininess = r.ReadSingle(),
                    EnvironmentMappingTexture = ReadCString(r),
                });
            }
        }

        private static KmfMesh ParseMesh(BinaryReader r)
        {
            r.ReadUInt32(); // MESH section size

            ExpectTag(r, "HEAD");
            r.ReadUInt32();
            string name = ReadCString(r);
            uint sprsCount = r.ReadUInt32();
            uint geomCount = r.ReadUInt32();
            var pos = ReadVector3(r);
            float scale = r.ReadSingle();
            uint lodCount = r.ReadUInt32();

            // CTRL - unknown semantics, skip
            ExpectTag(r, "CTRL");
            r.ReadUInt32();
            uint controlCount = r.ReadUInt32();
            r.BaseStream.Seek(controlCount * 8, SeekOrigin.Current);

            // SPRS
            ExpectTag(r, "SPRS");
            r.ReadUInt32();
            var sprites = new List<KmfMeshSprite>((int)sprsCount);

            for (int i = 0; i < sprsCount; i++)
            {
                ExpectTag(r, "SPHD");
                r.ReadUInt32();
                var counts = new uint[lodCount];
                for (int l = 0; l < lodCount; l++) counts[l] = r.ReadUInt32();
                uint vertexCount = r.ReadUInt32();
                float mmFactor = r.ReadSingle();
                sprites.Add(new KmfMeshSprite { MmFactor = mmFactor, TriangleCounts = counts, VertexCount = vertexCount });
            }

            foreach (var sprite in sprites)
            {
                ExpectTag(r, "SPRS");
                r.ReadUInt32();
                sprite.MaterialIndex = r.ReadUInt32();

                foreach (var count in sprite.TriangleCounts)
                {
                    var tris = new KmfTriangle[count];
                    for (int t = 0; t < count; t++)
                        tris[t] = new KmfTriangle(r.ReadByte(), r.ReadByte(), r.ReadByte());
                    sprite.TrianglesPerLod.Add(tris);
                }

                for (int v = 0; v < sprite.VertexCount; v++)
                {
                    ushort geomIndex = r.ReadUInt16();
                    var uv = new KmfUv(r.ReadUInt16(), r.ReadUInt16());
                    sprite.Vertices.Add(new KmfMeshVertex(geomIndex, uv, ReadVector3(r)));
                }
            }

            // GEOM
            ExpectTag(r, "GEOM");
            r.ReadUInt32();
            var geometries = new List<Vector3>((int)geomCount);
            for (int i = 0; i < geomCount; i++)
                geometries.Add(ReadVector3(r));

            return new KmfMesh { Name = name, Pos = pos, Scale = scale, Sprites = sprites, Geometries = geometries };
        }

        private static KmfAnim ParseAnim(BinaryReader r)
        {
            r.ReadUInt32(); // ANIM section size

            ExpectTag(r, "HEAD");
            r.ReadUInt32();
            string name = ReadCString(r);
            uint sprsCount = r.ReadUInt32();
            int frameCount = r.ReadInt32();
            int indexCount = r.ReadInt32();
            uint geomCount = r.ReadUInt32();
            uint frameFactorFunction = r.ReadUInt32();
            var pos = ReadVector3(r);
            float cubeScale = r.ReadSingle();
            float scale = r.ReadSingle();
            uint lodCount = r.ReadUInt32();

            // CTRL - unknown semantics, skip
            ExpectTag(r, "CTRL");
            r.ReadUInt32();
            uint controlCount = r.ReadUInt32();
            r.BaseStream.Seek(controlCount * 8, SeekOrigin.Current);

            // SPRS
            ExpectTag(r, "SPRS");
            r.ReadUInt32();
            var sprites = new List<KmfAnimSprite>((int)sprsCount);

            for (int i = 0; i < sprsCount; i++)
            {
                ExpectTag(r, "SPHD");
                r.ReadUInt32();
                var counts = new uint[lodCount];
                for (int l = 0; l < lodCount; l++) counts[l] = r.ReadUInt32();
                uint vertexCount = r.ReadUInt32();
                float mmFactor = r.ReadSingle();
                sprites.Add(new KmfAnimSprite { MmFactor = mmFactor, TriangleCounts = counts, VertexCount = vertexCount });
            }

            foreach (var sprite in sprites)
            {
                ExpectTag(r, "SPRS");
                r.ReadUInt32();
                sprite.MaterialIndex = r.ReadUInt32();

                ExpectTag(r, "POLY");
                r.ReadUInt32();
                foreach (var count in sprite.TriangleCounts)
                {
                    var tris = new KmfTriangle[count];
                    for (int t = 0; t < count; t++)
                        tris[t] = new KmfTriangle(r.ReadByte(), r.ReadByte(), r.ReadByte());
                    sprite.TrianglesPerLod.Add(tris);
                }

                ExpectTag(r, "VERT");
                r.ReadUInt32();
                for (int v = 0; v < sprite.VertexCount; v++)
                {
                    var uv = new KmfUv(r.ReadUInt16(), r.ReadUInt16());
                    var normal = ReadVector3(r);
                    sprite.Vertices.Add(new KmfAnimVertex(uv, normal, r.ReadUInt16()));
                }
            }

            // ITAB - base geom index per 128-frame chunk, per itab index
            ExpectTag(r, "ITAB");
            r.ReadUInt32();
            int chunks = (frameCount - 1) / 128 + 1;
            var itab = new uint[chunks][];
            for (int c = 0; c < chunks; c++)
            {
                itab[c] = new uint[indexCount];
                for (int i = 0; i < indexCount; i++)
                    itab[c][i] = r.ReadUInt32();
            }

            // GEOM - packed 10-bit coordinates + frame base
            ExpectTag(r, "GEOM");
            r.ReadUInt32();
            var geometries = new List<KmfAnimGeom>((int)geomCount + 1);
            KmfAnimGeom geom = default;
            for (int i = 0; i < geomCount; i++)
            {
                uint packed = r.ReadUInt32();
                float x = (((packed >> 20) % 1024) - 512) / 511.0f * scale;
                float y = (((packed >> 10) % 1024) - 512) / 511.0f * scale;
                float z = (((packed >> 0) % 1024) - 512) / 511.0f * scale;
                geom = new KmfAnimGeom(new Vector3(x, y, z), r.ReadByte());
                geometries.Add(geom);
            }
            geometries.Add(geom); // interpolation sentinel (mirrors OpenKeeper)

            // VGEO - per-frame offsets
            ExpectTag(r, "VGEO");
            r.ReadUInt32();
            var offsets = new byte[indexCount][];
            for (int i = 0; i < indexCount; i++)
                offsets[i] = r.ReadBytes(frameCount);

            return new KmfAnim
            {
                Name = name,
                Frames = frameCount,
                Indexes = indexCount,
                Pos = pos,
                CubeScale = cubeScale,
                Scale = scale,
                FrameFactorFunction = frameFactorFunction,
                Sprites = sprites,
                Itab = itab,
                Geometries = geometries,
                Offsets = offsets,
            };
        }

        private void ParseGrop(BinaryReader r)
        {
            r.ReadUInt32(); // GROP section size

            ExpectTag(r, "HEAD");
            r.ReadUInt32();
            uint elementCount = r.ReadUInt32();

            for (int i = 0; i < elementCount; i++)
            {
                ExpectTag(r, "ELEM");
                r.ReadUInt32();
                Grops.Add(new KmfGropElement { Name = ReadCString(r), Pos = ReadVector3(r) });
            }
        }

        #endregion

        #region Reader helpers

        private static string ReadTag(BinaryReader r)
            => Encoding.ASCII.GetString(r.ReadBytes(4));

        private static void ExpectTag(BinaryReader r, string expected)
        {
            long pos = r.BaseStream.Position;
            string tag = ReadTag(r);
            if (tag != expected)
                throw new InvalidDataException($"Expected chunk '{expected}' at 0x{pos:X}, found '{tag}'.");
        }

        private static string ReadCString(BinaryReader r)
        {
            var sb = new StringBuilder();
            byte b;
            while ((b = r.ReadByte()) != 0)
                sb.Append((char)b);
            return sb.ToString();
        }

        private static Vector3 ReadVector3(BinaryReader r)
            => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        #endregion

        /// <summary>
        /// Batch-parses every .kmf file below <paramref name="kmfDir"/> and prints a report.
        /// Sanity harness for validating the parser against the whole retail data set.
        /// </summary>
        public static void ValidateAll(string kmfDir)
        {
            var files = Directory.EnumerateFiles(kmfDir, "*.kmf", SearchOption.AllDirectories).ToList();
            int ok = 0, failed = 0;
            var typeCounts = new Dictionary<KmfType, int>();
            var failures = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    var kmf = Load(file);
                    ok++;
                    typeCounts[kmf.Type] = typeCounts.GetValueOrDefault(kmf.Type) + 1;

                    if (kmf.TrailingBytes != 0)
                        failures.Add($"{Path.GetFileName(file)}: {kmf.TrailingBytes} trailing bytes unread");

                    // Consistency checks
                    var maxMat = kmf.Mesh?.Sprites.Max(s => (uint?)s.MaterialIndex)
                              ?? kmf.Anim?.Sprites.Max(s => (uint?)s.MaterialIndex);
                    if (maxMat is uint m && m >= kmf.Materials.Count)
                        failures.Add($"{Path.GetFileName(file)}: material index {m} out of range ({kmf.Materials.Count})");

                    if (kmf.Mesh is { } mesh)
                    {
                        foreach (var s in mesh.Sprites)
                            if (s.Vertices.Any(v => v.GeomIndex >= mesh.Geometries.Count))
                                failures.Add($"{Path.GetFileName(file)}: geom index out of range");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }
            }

            Console.WriteLine($"KMF validation: {ok}/{files.Count} parsed OK, {failed} failed.");
            foreach (var (type, count) in typeCounts.OrderBy(kv => kv.Key))
                Console.WriteLine($"  {type}: {count}");
            foreach (var f in failures.Take(20))
                Console.WriteLine($"  ISSUE: {f}");
            if (failures.Count > 20)
                Console.WriteLine($"  ... and {failures.Count - 20} more issues.");
        }
    }
}
