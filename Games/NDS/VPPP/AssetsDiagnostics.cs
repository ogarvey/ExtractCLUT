using System;
using System.IO;
using System.Text;

namespace ExtractCLUT.Games.NDS.VPPP
{
    /// <summary>
    /// Diagnostics for the encoded <c>assets.bin</c>. These do not extract assets; they help the
    /// ongoing reverse engineering of the on-disk codec (see <see cref="IAssetDirectoryCodec"/>).
    /// </summary>
    public static class AssetsDiagnostics
    {
        /// <summary>Shannon entropy (bits/byte) of a buffer. ~8.0 = random/encrypted, ~7.x = compressed.</summary>
        public static double Entropy(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return 0;
            Span<int> freq = stackalloc int[256];
            foreach (byte b in data) freq[b]++;
            double e = 0, n = data.Length;
            for (int i = 0; i < 256; i++)
            {
                if (freq[i] == 0) continue;
                double p = freq[i] / n;
                e -= p * Math.Log2(p);
            }
            return e;
        }

        /// <summary>
        /// Produce a human-readable report on the file header, directory validity and entropy.
        /// Useful to confirm at a glance whether the configured codec successfully decoded the file.
        /// </summary>
        public static string Report(string assetsBinPath, IAssetDirectoryCodec? codec = null)
        {
            var sb = new StringBuilder();
            byte[] head = ReadHead(assetsBinPath, 16);
            long len = new FileInfo(assetsBinPath).Length;

            sb.AppendLine($"assets.bin: {assetsBinPath}");
            sb.AppendLine($"  length    : {len:N0} bytes (0x{len:X})");
            sb.AppendLine($"  head[0..16]: {Convert.ToHexString(head)}");

            using var archive = new AssetsArchive(assetsBinPath, codec);
            var dir = archive.Directory;
            sb.AppendLine($"  count     : {dir.Count}");
            sb.AppendLine($"  codec     : {(codec ?? new RawDirectoryCodec()).GetType().Name}");
            sb.AppendLine($"  keys sorted: {(dir.KeyInversions == 0 ? "yes" : $"no ({dir.KeyInversions} inversions)")}");
            sb.AppendLine($"  offsets monotonic: {(dir.NonMonotonicOffsets == 0 ? "yes" : $"no ({dir.NonMonotonicOffsets} drops)")}");
            sb.AppendLine($"  directory valid: {dir.IsValid}");

            using (var fs = File.OpenRead(assetsBinPath))
            {
                long dirBytes = AssetDirectory.DirectoryBlockSize(dir.Count);
                sb.AppendLine($"  entropy (directory region): {Entropy(ReadAt(fs, AssetsArchive.DirectoryOffset, (int)Math.Min(dirBytes, 65536))):F3} bits/byte");
                sb.AppendLine($"  entropy (data sample)     : {Entropy(ReadAt(fs, AssetsArchive.DirectoryOffset + dirBytes, (int)Math.Min(65536, len - AssetsArchive.DirectoryOffset - dirBytes))):F3} bits/byte");
            }

            if (!dir.IsValid)
            {
                sb.AppendLine();
                sb.AppendLine("  NOTE: directory is not structurally valid -> the on-disk file is encoded and");
                sb.AppendLine("        the configured codec did not decode it. Implement IAssetDirectoryCodec.");
            }
            return sb.ToString();
        }

        private static byte[] ReadHead(string path, int n)
        {
            using var fs = File.OpenRead(path);
            return ReadAt(fs, 0, (int)Math.Min(n, fs.Length));
        }

        private static byte[] ReadAt(Stream s, long offset, int count)
        {
            if (count <= 0) return Array.Empty<byte>();
            s.Position = offset;
            var buf = new byte[count];
            int read = 0;
            while (read < count)
            {
                int r = s.Read(buf, read, count - read);
                if (r <= 0) break;
                read += r;
            }
            return read == count ? buf : buf[..read];
        }
    }
}
