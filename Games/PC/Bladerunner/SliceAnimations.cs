namespace BladeRunnerSliceExporter;

// Port of BladeRunner::SliceAnimations (engines/bladerunner/slice_animations.{h,cpp}).
public sealed class SliceAnimations : IDisposable
{
    private const uint ExpectedTimestamp = 0x3457b6f6; // Wed, 29 Oct 1997 22:21:42 GMT

    public sealed class Animation
    {
        public uint FrameCount;
        public uint FrameSize;
        public float Fps;
        public float PositionChangeX, PositionChangeY, PositionChangeZ;
        public float FacingChange;
        public uint Offset;
    }

    public struct Palette
    {
        public byte[] R; // 256
        public byte[] G;
        public byte[] B;
    }

    // page number -> (fileIndex, byteOffset). Mirrors SliceAnimations::PageFile.
    private sealed class PageFile : IDisposable
    {
        private readonly List<Stream> _streams = new(); // was List<FileStream>
        private readonly long[] _pageOffsets;
        private readonly int[] _pageFileIdx;
        private readonly uint _pageSize;
        private readonly uint _timestamp;

        public PageFile(uint pageCount, uint pageSize, uint timestamp)
        {
            _pageSize = pageSize;
            _timestamp = timestamp;
            _pageOffsets = new long[pageCount];
            _pageFileIdx = new int[pageCount];
            Array.Fill(_pageOffsets, -1);
            Array.Fill(_pageFileIdx, -1);
        }

        // Mirrors PageFile::open (slice_animations.cpp lines 177-209)
        public bool Open(Stream stream)
        {
            var br = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);

            uint timestamp = br.ReadUInt32();
            if (timestamp != _timestamp)
            {
                stream.Dispose();
                return false;
            }

            int fileIdx = _streams.Count;
            _streams.Add(stream);

            uint pageCount = br.ReadUInt32();
            long dataOffset = 8 + 4L * pageCount;

            for (uint i = 0; i != pageCount; ++i)
            {
                uint pageNumber = br.ReadUInt32();
                if (pageNumber == 0xffffffff) continue;
                if (pageNumber < _pageOffsets.Length)
                {
                    _pageOffsets[pageNumber] = dataOffset + (long)i * _pageSize;
                    _pageFileIdx[pageNumber] = fileIdx;
                }
            }
            return true;
        }

        // Mirrors PageFile::loadPage (lines 219-237)
        public byte[]? LoadPage(uint pageNumber)
        {
            if (pageNumber >= _pageOffsets.Length ||
                _pageOffsets[pageNumber] == -1 ||
                _pageFileIdx[pageNumber] == -1)
                return null;

            var fs = _streams[_pageFileIdx[pageNumber]];
            fs.Seek(_pageOffsets[pageNumber], SeekOrigin.Begin);
            var data = new byte[_pageSize];
            int read = fs.Read(data, 0, (int)_pageSize);
            if (read != _pageSize)
                throw new IOException($"Short read for page {pageNumber}");
            return data;
        }

        public void Dispose()
        {
            foreach (var s in _streams) s.Dispose();
            _streams.Clear();
        }
    }

    public uint PageSize { get; private set; }
    public uint PageCount { get; private set; }
    public uint PaletteCount { get; private set; }
    public Palette[] Palettes { get; private set; } = Array.Empty<Palette>();
    public Animation[] Anims { get; private set; } = Array.Empty<Animation>();

    private uint _timestamp;
    private PageFile _coreAnim = null!;
    private PageFile _frames = null!;
    private readonly Dictionary<uint, byte[]> _pageCache = new();

    // Mirrors SliceAnimations::open (lines 33-84). Pass INDEX.DAT here.
    public void Open(byte[] indexData)
    {
        using var ms = new MemoryStream(indexData, writable: false);
        using var br = new BinaryReader(ms);

        _timestamp = br.ReadUInt32();
        PageSize = br.ReadUInt32();
        PageCount = br.ReadUInt32();
        PaletteCount = br.ReadUInt32();

        if (_timestamp != ExpectedTimestamp)
            throw new InvalidDataException(
                $"Unexpected timestamp 0x{_timestamp:x8} in index data. " +
                "Expected INDEX.DAT (0x3457b6f6).");

        Palettes = new Palette[PaletteCount];
        for (uint i = 0; i != PaletteCount; ++i)
        {
            var p = new Palette { R = new byte[256], G = new byte[256], B = new byte[256] };
            for (int j = 0; j != 256; ++j)
            {
                byte r5 = br.ReadByte();
                byte g5 = br.ReadByte();
                byte b5 = br.ReadByte();
                p.R[j] = ColorUtil.Get8BitFrom5Bit(r5);
                p.G[j] = ColorUtil.Get8BitFrom5Bit(g5);
                p.B[j] = ColorUtil.Get8BitFrom5Bit(b5);
            }
            Palettes[i] = p;
        }

        uint animationCount = br.ReadUInt32();
        Anims = new Animation[animationCount];
        for (uint i = 0; i != animationCount; ++i)
        {
            Anims[i] = new Animation
            {
                FrameCount = br.ReadUInt32(),
                FrameSize = br.ReadUInt32(),
                Fps = br.ReadSingle(),
                PositionChangeX = br.ReadSingle(),
                PositionChangeY = br.ReadSingle(),
                PositionChangeZ = br.ReadSingle(),
                FacingChange = br.ReadSingle(),
                Offset = br.ReadUInt32()
            };
        }

        _coreAnim = new PageFile(PageCount, PageSize, _timestamp);
        _frames = new PageFile(PageCount, PageSize, _timestamp);
    }

    // Convenience overloads that take resource bytes and wrap them in a MemoryStream:
    public bool OpenCoreAnim(byte[] coreAnimData) => _coreAnim.Open(new MemoryStream(coreAnimData, false));
    public bool OpenFrames(IEnumerable<byte[]> framePages)
    {
        bool any = false;
        foreach (var bytes in framePages)
            any |= _frames.Open(new MemoryStream(bytes, false));
        return any;
    }

    // Mirrors SliceAnimations::getFramePtr (lines 239-288).
    public (byte[] page, int offset) GetFrame(uint animation, uint frame)
    {
        var anim = Anims[animation];
        if (frame >= anim.FrameCount)
            frame = 0; // non-original-bug sanitization

        uint frameOffset = anim.Offset + frame * anim.FrameSize;
        uint pageId = frameOffset / PageSize;
        uint pageOffset = frameOffset % PageSize;

        if (!_pageCache.TryGetValue(pageId, out var data))
        {
            data = _coreAnim.LoadPage(pageId) ?? _frames.LoadPage(pageId)
                   ?? throw new IOException(
                       $"Unable to locate page {pageId} for animation {animation} frame {frame}");
            _pageCache[pageId] = data;
        }
        return (data, (int)pageOffset);
    }

    public void Dispose()
    {
        _coreAnim?.Dispose();
        _frames?.Dispose();
        _pageCache.Clear();
    }
}
