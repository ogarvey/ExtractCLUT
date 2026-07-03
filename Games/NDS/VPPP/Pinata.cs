using System;
using System.Collections.Generic;
using System.IO;

namespace ExtractCLUT.Games.NDS.VPPP
{
    /// <summary>
    /// Top-level helper for Viva Piñata: Pocket Paradise (NDS) asset extraction.
    ///
    /// This is the main entry point; the heavy lifting is split across:
    ///   * <see cref="AssetsArchive"/>          — opens/streams <c>assets.bin</c>;
    ///   * <see cref="AssetDirectory"/>          — parses count + keys + offsets, validates layout;
    ///   * <see cref="AssetEntry"/>              — per-entry model (0x10 header + payload);
    ///   * <see cref="IAssetDirectoryCodec"/>    — pluggable on-disk decode seam;
    ///   * <see cref="AssetsDiagnostics"/>       — entropy/validity reporting for ongoing RE.
    ///
    /// STATUS: the directory/entry layout is fully reversed from the game code, but the shipped
    /// <c>assets.bin</c> is ENCODED by a custom runtime archive codec that is not yet recovered.
    /// Extraction will yield valid data only once a real <see cref="IAssetDirectoryCodec"/> is
    /// supplied. Use <see cref="Probe"/> / <see cref="AssetDirectory.IsValid"/> to confirm.
    /// </summary>
    public sealed class Pinata
    {
        private readonly IAssetDirectoryCodec? _codec;

        /// <param name="codec">
        /// On-disk directory decoder. Pass null to use the raw pass-through (which only works if the
        /// file is unencoded — the shipped file is not).
        /// </param>
        public Pinata(IAssetDirectoryCodec? codec = null) => _codec = codec;

        /// <summary>Open the archive at <paramref name="assetsBinPath"/>.</summary>
        public AssetsArchive Open(string assetsBinPath) => new AssetsArchive(assetsBinPath, _codec);

        /// <summary>Quick diagnostic report (header, count, directory validity, entropy).</summary>
        public string Probe(string assetsBinPath) => AssetsDiagnostics.Report(assetsBinPath, _codec);

        /// <summary>
        /// Extract every entry payload to <paramref name="outputDir"/> as <c>&lt;index&gt;_&lt;key&gt;.bin</c>.
        /// Throws if the directory is not valid (i.e. the file is still encoded), to avoid writing
        /// garbage. Pass <paramref name="force"/> = true to dump anyway for inspection.
        /// </summary>
        public int ExtractAll(string assetsBinPath, string outputDir, bool force = false)
        {
            using var archive = Open(assetsBinPath);
            if (!archive.Directory.IsValid && !force)
                throw new InvalidOperationException(
                    "assets.bin directory is not valid -> the file is encoded and no working codec was " +
                    "supplied. Implement IAssetDirectoryCodec, or pass force:true to dump raw entries.");

            System.IO.Directory.CreateDirectory(outputDir);
            int written = 0;
            foreach (AssetEntry entry in archive.Directory.Entries)
            {
                if (entry.TotalSize <= 0 || entry.Offset < 0 || entry.Offset + entry.TotalSize > archive.FileLength)
                    continue;
                byte[] data = archive.ReadEntryPayload(entry);
                string name = $"{entry.Index:D4}_0x{entry.Key:X4}.bin";
                File.WriteAllBytes(Path.Combine(outputDir, name), data);
                written++;
            }
            return written;
        }

        /// <summary>Enumerate the parsed entries.</summary>
        public IReadOnlyList<AssetEntry> ListEntries(string assetsBinPath)
        {
            using var archive = Open(assetsBinPath);
            return archive.Directory.Entries;
        }
    }
}
