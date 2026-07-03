using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;

namespace ExtractCLUT.Games.PC.Bullfrog.DungeonKeeper2
{
    using GltfMesh = MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>;
    using GltfVertex = VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>;

    /// <summary>
    /// Converts parsed KMF models to glTF 2.0 (.glb) for Blender import.
    ///
    /// Conventions (mirroring OpenKeeper's JME loader):
    ///   - Coordinates: DK2 (x, y, z) -> glTF right-handed Y-up (x, -z, y);
    ///     the axis reflection flips winding, so triangles are emitted reversed.
    ///   - UVs: u16 / 32768.
    ///   - Animations: per-vertex morphing -> one morph target per frame (absolute
    ///     positions), animated with a weight track at 30 fps.
    /// </summary>
    public static class DKIIGltf
    {
        private const float UvScale = 32768f;
        private const float Fps = 30f;

        /// <summary>Broken texture references in retail data (as per OpenKeeper).</summary>
        private static readonly Dictionary<string, string> TextureFixes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Goblinbak"] = "GoblinBack",
            ["Goblin2"] = "GoblinFront",
        };

        #region Texture index

        private sealed class TextureIndex
        {
            private readonly Dictionary<string, string> _files;

            public TextureIndex(string textureDir)
            {
                _files = Directory
                    .EnumerateFiles(textureDir, "*.png", SearchOption.AllDirectories)
                    .GroupBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            }

            public string? Resolve(string textureName)
            {
                if (TextureFixes.TryGetValue(textureName, out var fixedName))
                    textureName = fixedName;

                // MM0 = largest mip level
                if (_files.TryGetValue(textureName + "MM0", out var path)) return path;
                if (_files.TryGetValue(textureName, out path)) return path;
                return null;
            }
        }

        #endregion

        #region Conversion

        private static Vector3 Swizzle(Vector3 v) => new(v.X, -v.Z, v.Y);

        private static MaterialBuilder BuildMaterial(KmfFile.KmfMaterial mat, TextureIndex textures)
        {
            var builder = new MaterialBuilder(mat.Name)
                .WithMetallicRoughnessShader()
                .WithMetallicRoughness(0f, 1f);

            var texPath = mat.Textures.Count > 0 ? textures.Resolve(mat.Textures[0]) : null;
            if (texPath != null)
                builder.WithChannelImage(KnownChannel.BaseColor, texPath);

            return builder;
        }

        /// <summary>Converts a static MESH model.</summary>
        private static SceneBuilder ConvertMesh(KmfFile kmf, TextureIndex textures)
        {
            var mesh = kmf.Mesh!;
            var materials = kmf.Materials.Select(m => BuildMaterial(m, textures)).ToList();

            var gltfMesh = new GltfMesh(mesh.Name);

            foreach (var sprite in mesh.Sprites)
            {
                var prim = gltfMesh.UsePrimitive(materials[(int)sprite.MaterialIndex]);

                foreach (var tri in sprite.TrianglesPerLod[0]) // LOD 0 only
                {
                    var a = MakeVertex(mesh, sprite.Vertices[tri.A]);
                    var b = MakeVertex(mesh, sprite.Vertices[tri.B]);
                    var c = MakeVertex(mesh, sprite.Vertices[tri.C]);
                    prim.AddTriangle(c, b, a); // reversed winding (axis reflection)
                }
            }

            var scene = new SceneBuilder(mesh.Name);
            var node = new NodeBuilder(mesh.Name) { LocalTransform = Matrix4x4.CreateTranslation(Swizzle(mesh.Pos)) };
            scene.AddRigidMesh(gltfMesh, node);
            return scene;
        }

        private static GltfVertex MakeVertex(KmfFile.KmfMesh mesh, KmfFile.KmfMeshVertex v)
            => new(
                new VertexPositionNormal(Swizzle(mesh.Geometries[v.GeomIndex]), Vector3.Normalize(Swizzle(v.Normal))),
                new VertexTexture1(new Vector2(v.Uv.U / UvScale, v.Uv.V / UvScale)));

        /// <summary>Evaluates the position of an anim vertex at a given frame.</summary>
        private static Vector3 AnimPosition(KmfFile.KmfAnim anim, ushort itabIndex, int frame)
        {
            uint geomBase = anim.Itab[frame >> 7][itabIndex];
            byte geomOffset = anim.Offsets[itabIndex][frame];
            return Swizzle(anim.Geometries[(int)(geomBase + geomOffset)].Position);
        }

        /// <summary>Converts an ANIM model: base mesh (frame 0) + a morph target per frame.</summary>
        private static SceneBuilder ConvertAnim(KmfFile kmf, TextureIndex textures)
        {
            var anim = kmf.Anim!;
            var materials = kmf.Materials.Select(m => BuildMaterial(m, textures)).ToList();

            var gltfMesh = new GltfMesh(anim.Name);

            // Base mesh: frame 0. Track vertices for morph targets.
            var spriteVertices = new List<(KmfFile.KmfAnimSprite Sprite, GltfVertex[] Base)>();

            foreach (var sprite in anim.Sprites)
            {
                var prim = gltfMesh.UsePrimitive(materials[(int)sprite.MaterialIndex]);

                var baseVerts = new GltfVertex[sprite.Vertices.Count];
                for (int i = 0; i < sprite.Vertices.Count; i++)
                {
                    var v = sprite.Vertices[i];
                    baseVerts[i] = new GltfVertex(
                        new VertexPositionNormal(AnimPosition(anim, v.ItabIndex, 0), Vector3.Normalize(Swizzle(v.Normal))),
                        new VertexTexture1(new Vector2(v.Uv.U / UvScale, v.Uv.V / UvScale)));
                }

                foreach (var tri in sprite.TrianglesPerLod[0])
                    prim.AddTriangle(baseVerts[tri.C], baseVerts[tri.B], baseVerts[tri.A]);

                spriteVertices.Add((sprite, baseVerts));
            }

            // Morph targets: one per frame (skipping frame 0 = base).
            // Some ANIM files are actually static (effects done via texture swaps);
            // exporting empty morph targets breaks glTF validation, so detect motion first.
            bool anyMotion = anim.Sprites.Any(s => s.Vertices.Any(v =>
            {
                var p0 = AnimPosition(anim, v.ItabIndex, 0);
                for (int f = 1; f < anim.Frames; f++)
                    if (AnimPosition(anim, v.ItabIndex, f) != p0) return true;
                return false;
            }));

            int targetCount = anyMotion ? anim.Frames - 1 : 0;
            for (int frame = 1; frame <= targetCount; frame++)
            {
                var morph = gltfMesh.UseMorphTarget(frame - 1);
                foreach (var (sprite, baseVerts) in spriteVertices)
                {
                    for (int i = 0; i < sprite.Vertices.Count; i++)
                    {
                        var basePos = baseVerts[i].Geometry;
                        var morphed = basePos;
                        morphed.Position = AnimPosition(anim, sprite.Vertices[i].ItabIndex, frame);
                        morph.SetVertex(basePos, morphed);
                    }
                }
            }

            var scene = new SceneBuilder(anim.Name);
            var node = new NodeBuilder(anim.Name) { LocalTransform = Matrix4x4.CreateTranslation(Swizzle(anim.Pos)) };
            var instance = scene.AddRigidMesh(gltfMesh, node);

            if (targetCount > 0)
            {
                instance.Content.UseMorphing().SetValue(new float[targetCount]);
                var track = instance.Content.UseMorphing("Default");

                var weights = new float[targetCount];
                for (int frame = 0; frame < anim.Frames; frame++)
                {
                    Array.Clear(weights);
                    if (frame > 0) weights[frame - 1] = 1f;
                    track.SetPoint(frame / Fps, true, weights);
                }
            }

            return scene;
        }

        /// <summary>Converts a single KMF file; returns false for unsupported types (GROP).</summary>
        public static bool Convert(string kmfPath, string textureDir, string outPath)
        {
            var kmf = KmfFile.Load(kmfPath);
            var textures = new TextureIndex(textureDir);

            var scene = kmf.Type switch
            {
                KmfFile.KmfType.Mesh => ConvertMesh(kmf, textures),
                KmfFile.KmfType.Anim => ConvertAnim(kmf, textures),
                _ => null,
            };

            if (scene == null) return false;

            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            scene.ToGltf2().SaveGLB(outPath);
            return true;
        }

        /// <summary>Batch-converts all KMFs below <paramref name="kmfDir"/> to GLB files.</summary>
        public static void ConvertAll(string kmfDir, string textureDir, string outDir)
        {
            var textures = new TextureIndex(textureDir);
            var files = Directory.EnumerateFiles(kmfDir, "*.kmf", SearchOption.AllDirectories).ToList();

            int ok = 0, skipped = 0, failed = 0;
            var failures = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    var kmf = KmfFile.Load(file);
                    var scene = kmf.Type switch
                    {
                        KmfFile.KmfType.Mesh => ConvertMesh(kmf, textures),
                        KmfFile.KmfType.Anim => ConvertAnim(kmf, textures),
                        _ => null,
                    };

                    if (scene == null) { skipped++; continue; }

                    var relative = Path.GetRelativePath(kmfDir, file);
                    var outPath = Path.Combine(outDir, Path.ChangeExtension(relative, ".glb"));
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                    scene.ToGltf2().SaveGLB(outPath);
                    ok++;
                }
                catch (Exception ex)
                {
                    failed++;
                    failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }
            }

            Console.WriteLine($"glTF conversion: {ok} converted, {skipped} skipped (GROP), {failed} failed of {files.Count}.");
            foreach (var f in failures.Take(15))
                Console.WriteLine($"  FAILED: {f}");
            if (failures.Count > 15)
                Console.WriteLine($"  ... and {failures.Count - 15} more.");
        }

        #endregion
    }
}
