using System.Text;

namespace BladeRunnerSliceExporter;

// Byte-exact port of BladeRunner::MIXArchive (engines/bladerunner/archive.{h,cpp}).
// Blade Runner MIX layout:
//   uint16 entryCount
//   uint32 dataSize
//   entryCount * { int32 hash, uint32 offset, uint32 length }   // sorted ascending by signed hash
//   <member data...>                                            // offsets relative to end of table
// Member data start = offset + 6 + 12*entryCount.
public sealed class MixArchive : IDisposable
{
    private struct ArchiveEntry
    {
        public int Hash;
        public uint Offset;
        public uint Length;
    }

    private readonly FileStream _fd;
    private readonly bool _isTlk;
    private readonly ushort _entryCount;
    private readonly ArchiveEntry[] _entries;
    private readonly string _name;

    public string Name => _name;

    private MixArchive(FileStream fd, bool isTlk, ushort entryCount, ArchiveEntry[] entries, string name)
    {
        _fd = fd;
        _isTlk = isTlk;
        _entryCount = entryCount;
        _entries = entries;
        _name = name;
    }

    public static bool Exists(string path) => File.Exists(path);

    // Mirrors MIXArchive::open (archive.cpp lines 44-77).
    public static MixArchive Open(string path)
    {
        var fd = new FileStream(path, FileMode.Open, FileAccess.Read);
        var br = new BinaryReader(fd);

        bool isTlk = path.EndsWith(".TLK", StringComparison.OrdinalIgnoreCase);

        ushort entryCount = br.ReadUInt16();
        uint size = br.ReadUInt32(); // dataSize; not needed after read, kept for parity

        var entries = new ArchiveEntry[entryCount];
        int prevHash = 0;
        for (int i = 0; i != entryCount; ++i)
        {
            entries[i].Hash = br.ReadInt32();
            entries[i].Offset = br.ReadUInt32();
            entries[i].Length = br.ReadUInt32();

            // Entries must be sorted ascending by signed hash (used by the binary search).
            if (i > 0 && !(entries[i].Hash > prevHash))
                throw new InvalidDataException(
                    $"MIX '{Path.GetFileName(path)}': entries not sorted at index {i}.");
            prevHash = entries[i].Hash;
        }

        return new MixArchive(fd, isTlk, entryCount, entries, Path.GetFileName(path));
    }

    // ROL(n) = ((n << 1) | ((n >> 31) & 1)), 32-bit rotate-left-by-1 (archive.cpp line 87).
    private static uint Rol(uint n) => (n << 1) | ((n >> 31) & 1u);

    // Byte-exact port of MIXArchive::getHash (archive.cpp lines 89-107).
    // Uppercase to <=12 chars, process 4-byte LE chunks: id = ROL(id) + chunk.
    public static int GetHash(string name)
    {
        // buffer[12] zero-initialized; chars beyond name length stay 0.
        var buffer = new byte[12];
        int n = Math.Min(name.Length, 12);
        for (int i = 0; i < n; ++i)
            buffer[i] = (byte)char.ToUpperInvariant(name[i]);

        uint id = 0;
        for (int i = 0; i < 12 && buffer[i] != 0; i += 4)
        {
            // Note: the original reads buffer[i..i+3]; because buffer is 12 bytes and
            // zero-padded, indexing up to i+3 is always in-bounds for i in {0,4,8}.
            uint t = (uint)buffer[i + 3] << 24
                   | (uint)buffer[i + 2] << 16
                   | (uint)buffer[i + 1] << 8
                   | (uint)buffer[i + 0];
            id = Rol(id) + t;
        }
        return unchecked((int)id);
    }

    // Byte-exact port of tlk_id (archive.cpp lines 109-124), for *.TLK speech archives.
    private static int TlkId(string name)
    {
        var buffer = new byte[12];
        int n = Math.Min(name.Length, 12);
        for (int i = 0; i < n; ++i)
            buffer[i] = (byte)char.ToUpperInvariant(name[i]);

        int actorId = 10 * (buffer[0] - '0') + (buffer[1] - '0');
        int speechId = 1000 * (buffer[3] - '0')
                     + 100 * (buffer[4] - '0')
                     + 10 * (buffer[5] - '0')
                     + (buffer[6] - '0');
        return 10000 * actorId + speechId;
    }

    // Mirrors MIXArchive::indexForHash (archive.cpp lines 126-141): binary search on signed hash.
    private int IndexForHash(int hash)
    {
        int lo = 0, hi = _entryCount;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (hash > _entries[mid].Hash) lo = mid + 1;
            else if (hash < _entries[mid].Hash) hi = mid;
            else return mid;
        }
        return _entryCount;
    }

    // Returns the full member bytes, or null if not present.
    // Mirrors createReadStreamForMember (archive.cpp lines 143-162).
    public byte[]? ReadMember(string memberName)
    {
        int hash = _isTlk ? TlkId(memberName) : GetHash(memberName);
        int i = IndexForHash(hash);
        if (i == _entryCount)
            return null;

        long start = (long)_entries[i].Offset + 6 + 12L * _entryCount;
        uint length = _entries[i].Length;

        _fd.Seek(start, SeekOrigin.Begin);
        var data = new byte[length];
        int read = 0;
        while (read < length)
        {
            int r = _fd.Read(data, read, (int)length - read);
            if (r <= 0)
                throw new IOException($"Short read for '{memberName}' in MIX '{_name}'.");
            read += r;
        }
        return data;
    }

    public bool HasMember(string memberName)
    {
        int hash = _isTlk ? TlkId(memberName) : GetHash(memberName);
        return IndexForHash(hash) != _entryCount;
    }

    public void Dispose() => _fd.Dispose();
}
