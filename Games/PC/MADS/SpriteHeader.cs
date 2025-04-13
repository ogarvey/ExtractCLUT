namespace ExtractCLUT.Games.PC.MADS
{
    public class SpriteHeader
    {
        public uint Offset { get; set; }
        public uint Length { get; set; }
        public ushort WidthPadded { get; set; }
        public ushort HeightPadded { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
    }
}
