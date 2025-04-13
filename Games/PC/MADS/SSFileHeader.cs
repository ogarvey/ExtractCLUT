namespace ExtractCLUT.Games.PC.MADS
{
    public class SSFileHeader
    {
        public byte Mode { get; set; }
        public ushort Type1 { get; set; }
        public ushort Type2 { get; set; }
        public byte PalFlag { get; set; }
        public ushort SpriteCount { get; set; }
        public uint DataSize { get; set; }
    }
}
