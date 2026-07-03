using System;
using System.Buffers.Binary;
using System.IO;

namespace ExtractCLUT.Games.NDS.VPPP
{
    /// <summary>
    /// Reader for the Viva Piñata: Pocket Paradise <c>assets.bin</c> archive.
    ///
    /// Mirrors the game's loader (Assets_LoadDirectory / FUN_020769f4):
    ///   1. read the u32 entry count at file offset 0;
    ///   2. read <c>align4(count * 6 + 4)</c> bytes of directory immediately after it;
    ///   3. run the directory bytes through an <see cref="IAssetDirectoryCodec"/> (the on-disk
    ///      directory is encoded — see <see cref="IAssetDirectoryCodec"/> for details);
    ///   4. parse keys + offsets via <see cref="AssetDirectory"/>.
    ///
    /// Entry payloads are read on demand: each entry has a 0x10-byte header the game skips, so the
    /// payload is <c>[Offset + 0x10 .. Offset + TotalSize)</c>.
    /// </summary>
    public sealed class AssetsArchive : IDisposable
    {
        /// <summary>File offset of the u32 entry count.</summary>
        public const int CountOffset = 0;

        /// <summary>File offset where the directory block begins (right after the u32 count).</summary>
        public const int DirectoryOffset = 4;

        private readonly Stream _stream;
        private readonly bool _ownsStream;

        public string? Path { get; }
        public long FileLength { get; }
        public AssetDirectory Directory { get; }

        public AssetsArchive(string path, IAssetDirectoryCodec? codec = null)
            : this(File.OpenRead(path), ownsStream: true, codec, path)
        {
        }

        public AssetsArchive(Stream stream, bool ownsStream, IAssetDirectoryCodec? codec = null, string? path = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _ownsStream = ownsStream;
            Path = path;
            FileLength = stream.Length;
            codec ??= new RawDirectoryCodec();

            _stream.Position = CountOffset;
            int count = (int)ReadU32(_stream);

            long dirBytes = AssetDirectory.DirectoryBlockSize(count);
            long dataRegionBase = DirectoryOffset + dirBytes;

            _stream.Position = DirectoryOffset;
            var raw = ReadExactly(_stream, checked((int)dirBytes));

            byte[] decoded = codec.DecodeDirectory(raw, count);
            Directory = new AssetDirectory(decoded, count, dataRegionBase, FileLength);
        }

        /// <summary>Read an entry's full bytes (including its 0x10 header).</summary>
        public byte[] ReadEntryRaw(AssetEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            _stream.Position = entry.Offset;
            return ReadExactly(_stream, checked((int)entry.TotalSize));
        }

        /// <summary>Read an entry's payload (skipping the 0x10 header), as the game does.</summary>
        public byte[] ReadEntryPayload(AssetEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            _stream.Position = entry.PayloadOffset;
            return ReadExactly(_stream, checked((int)entry.PayloadSize));
        }

        /// <summary>Read the 0x10-byte header that prefixes an entry.</summary>
        public byte[] ReadEntryHeader(AssetEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            _stream.Position = entry.Offset;
            return ReadExactly(_stream, Math.Min(AssetEntry.HeaderSize, (int)entry.TotalSize));
        }

        private static uint ReadU32(Stream s)
        {
            Span<byte> b = stackalloc byte[4];
            ReadExactly(s, b);
            return BinaryPrimitives.ReadUInt32LittleEndian(b);
        }

        private static byte[] ReadExactly(Stream s, int count)
        {
            var buf = new byte[count];
            ReadExactly(s, buf);
            return buf;
        }

        private static void ReadExactly(Stream s, Span<byte> buffer)
        {
            int read = 0;
            while (read < buffer.Length)
            {
                int n = s.Read(buffer.Slice(read));
                if (n <= 0)
                    throw new EndOfStreamException(
                        $"Expected {buffer.Length} bytes, got {read}.");
                read += n;
            }
        }

        public void Dispose()
        {
            if (_ownsStream) _stream.Dispose();
        }
    }
}
