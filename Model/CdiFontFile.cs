using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Model
{
  public class CdiFontFile
  {
    public CdiFontFile(byte[] bytes)
    {
        var fontTypeBytes = new byte[2];
        var widthBytes = new byte[2];
        var heightBytes = new byte[2];
        var ascentBytes = new byte[2];
        var descentBytes = new byte[2];
        var pixelSizeBytes = new byte[2];
        var firstCharBytes = new byte[2];
        var lastCharBytes = new byte[2];
        var lineLengthBytes = new byte[4];
        var glyphTableOffsetBytes = new byte[4];
        var glyphDataTableLengthBytes = new byte[4];
        var firstBmpOffsetBytes = new byte[4];
        var secondBmpOffsetBytes = new byte[4];

        Array.Copy(bytes, 0, fontTypeBytes, 0, 2);
        Array.Copy(bytes, 2, widthBytes, 0, 2);
        Array.Copy(bytes, 4, heightBytes, 0, 2);
        Array.Copy(bytes, 6, ascentBytes, 0, 2);
        Array.Copy(bytes, 8, descentBytes, 0, 2);
        Array.Copy(bytes, 10, pixelSizeBytes, 0, 2);
        Array.Copy(bytes, 12, firstCharBytes, 0, 2);
        Array.Copy(bytes, 14, lastCharBytes, 0, 2);
        Array.Copy(bytes, 16, lineLengthBytes, 0, 4);
        Array.Copy(bytes, 20, glyphTableOffsetBytes, 0, 4);
        Array.Copy(bytes, 24, glyphDataTableLengthBytes, 0, 4);
        Array.Copy(bytes, 28, firstBmpOffsetBytes, 0, 4);
        Array.Copy(bytes, 32, secondBmpOffsetBytes, 0, 4);

        Array.Reverse(fontTypeBytes);
        Array.Reverse(widthBytes);
        Array.Reverse(heightBytes);
        Array.Reverse(ascentBytes);
        Array.Reverse(descentBytes);
        Array.Reverse(pixelSizeBytes);
        Array.Reverse(firstCharBytes);
        Array.Reverse(lastCharBytes);
        Array.Reverse(lineLengthBytes);
        Array.Reverse(glyphTableOffsetBytes);
        Array.Reverse(glyphDataTableLengthBytes);
        Array.Reverse(firstBmpOffsetBytes);
        Array.Reverse(secondBmpOffsetBytes);

        FontType = BitConverter.ToInt16(fontTypeBytes, 0);
        Width = BitConverter.ToInt16(widthBytes, 0);
        Height = BitConverter.ToInt16(heightBytes, 0);
        Ascent = BitConverter.ToInt16(ascentBytes, 0);
        Descent = BitConverter.ToInt16(descentBytes, 0);
        PixelSize = BitConverter.ToInt16(pixelSizeBytes, 0);
        FirstChar = BitConverter.ToInt16(firstCharBytes, 0);
        LastChar = BitConverter.ToInt16(lastCharBytes, 0);
        LineLength = BitConverter.ToInt32(lineLengthBytes, 0);
        GlyphTableOffset = BitConverter.ToInt32(glyphTableOffsetBytes, 0);
        GlyphDataTableLength = BitConverter.ToInt32(glyphDataTableLengthBytes, 0);
        FirstBmpOffset = BitConverter.ToInt32(firstBmpOffsetBytes, 0);
        SecondBmpOffset = BitConverter.ToInt32(secondBmpOffsetBytes, 0);
    }
    public short FontType { get; set; }
    public short Width { get; set; }
    public short Height { get; set; }
    public short Ascent { get; set; }
    public short Descent { get; set; }
    public short PixelSize { get; set; }
    public short FirstChar { get; set; }
    public short LastChar { get; set; }
    public int LineLength { get; set; }
    public int GlyphTableOffset { get; set; }
    public int GlyphDataTableLength { get; set; }
    public int FirstBmpOffset { get; set; }
    public int SecondBmpOffset { get; set; }
  }
}
