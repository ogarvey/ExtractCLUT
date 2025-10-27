using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = SixLabors.ImageSharp.Color;

namespace ExtractCLUT.Games.PC.MADE
{
    public static class FormatHelper
    {
        public static Image<Rgba32> ConvertRawFlex(string inputFile)
        {
            using var fReader = new BinaryReader(new FileStream(inputFile, FileMode.Open, FileAccess.Read));

            // check header != "FLEX"
            var header = fReader.ReadChars(4);
            if (new string(header) == "FLEX")
                return new Image<Rgba32>(1, 1);
            fReader.BaseStream.Seek(0, SeekOrigin.Begin);
            var hasPalette = fReader.ReadBoolean();
            byte cmdFlags = fReader.ReadByte();
            byte pixelFlags = fReader.ReadByte();
            byte maskFlags = fReader.ReadByte();
            ushort cmdOFfset = fReader.ReadUInt16();
            ushort pixelOffset = fReader.ReadUInt16();
            ushort maskOffset = fReader.ReadUInt16();
            ushort lineSize = fReader.ReadUInt16();
            fReader.ReadUInt16(); // padding
            ushort width = fReader.ReadUInt16();
            ushort height = fReader.ReadUInt16();

            var palColCount = (cmdOFfset - 18) / 3;
            List<Color> palette = new();
            if (hasPalette)
            {
                for (int i = 0; i < palColCount; i++)
                {
                    byte r = fReader.ReadByte();
                    byte g = fReader.ReadByte();
                    byte b = fReader.ReadByte();
                    palette.Add(new Rgba32(r, g, b));
                }
            }

            var pixelData = DecompressImage(fReader, width, height, cmdOFfset, pixelOffset, maskOffset, lineSize, cmdFlags, pixelFlags, maskFlags);
            var image = ImageFormatHelper.GenerateIMClutImage(palette, pixelData, width, height);
            return image;
        }

        public static byte[] DecompressImage(BinaryReader source, ushort width, ushort height, ushort cmdOffs, ushort pixelOffs, ushort maskOffs, ushort lineSize, byte cmdFlags, byte pixelFlags, byte maskFlags, bool deltaFrame = false)
        {
            int[] offsets = {
                0, 1, 2, 3,
                320, 321, 322, 323,
                640, 641, 642, 643,
                960, 961, 962, 963
            };

            var pitch = width;
            var dest = new byte[width * height];
            int destPtr = 0;

            source.BaseStream.Seek(cmdOffs, SeekOrigin.Begin);
            byte[] cmdBuffer = source.ReadBytes((int)(source.BaseStream.Length - cmdOffs));
            int cmdBufferPtr = 0;

            source.BaseStream.Seek(maskOffs, SeekOrigin.Begin);
            var maskReader = new ValueReader(new BinaryReader(new MemoryStream(source.ReadBytes((int)(source.BaseStream.Length - maskOffs)))), (maskFlags & 2) != 0);

            source.BaseStream.Seek(pixelOffs, SeekOrigin.Begin);
            var pixelReader = new ValueReader(new BinaryReader(new MemoryStream(source.ReadBytes((int)(source.BaseStream.Length - pixelOffs)))), (pixelFlags & 2) != 0);

            byte[] lineBuf = new byte[640 * 4];
            byte[] bitBuf = new byte[40];

            int bitBufLastOfs = (((lineSize + 1) >> 1) << 1) - 2;
            int bitBufLastCount = ((width + 3) >> 2) & 7;
            if (bitBufLastCount == 0)
                bitBufLastCount = 8;

            int remainingHeight = height;
            while (remainingHeight > 0)
            {
                int drawDestOfs = 0;
                Array.Clear(lineBuf, 0, lineBuf.Length);

                Buffer.BlockCopy(cmdBuffer, cmdBufferPtr, bitBuf, 0, lineSize);
                cmdBufferPtr += lineSize;

                for (int bitBufOfs = 0; bitBufOfs < lineSize; bitBufOfs += 2)
                {
                    ushort bits = BitConverter.ToUInt16(bitBuf, bitBufOfs);

                    int bitCount = (bitBufOfs == bitBufLastOfs) ? bitBufLastCount : 8;

                    for (int curCmd = 0; curCmd < bitCount; curCmd++)
                    {
                        int cmd = bits & 3;
                        bits >>= 2;

                        byte[] pixels = new byte[4];
                        uint mask;

                        switch (cmd)
                        {
                            case 0:
                                pixels[0] = pixelReader.ReadPixel();
                                for (int i = 0; i < 16; i++)
                                    lineBuf[drawDestOfs + offsets[i]] = pixels[0];
                                break;

                            case 1:
                                pixels[0] = pixelReader.ReadPixel();
                                pixels[1] = pixelReader.ReadPixel();
                                mask = maskReader.ReadUInt16();
                                for (int i = 0; i < 16; i++)
                                {
                                    lineBuf[drawDestOfs + offsets[i]] = pixels[mask & 1];
                                    mask >>= 1;
                                }
                                break;

                            case 2:
                                pixels[0] = pixelReader.ReadPixel();
                                pixels[1] = pixelReader.ReadPixel();
                                pixels[2] = pixelReader.ReadPixel();
                                pixels[3] = pixelReader.ReadPixel();
                                mask = maskReader.ReadUInt32();
                                for (int i = 0; i < 16; i++)
                                {
                                    lineBuf[drawDestOfs + offsets[i]] = pixels[mask & 3];
                                    mask >>= 2;
                                }
                                break;

                            case 3:
                                if (!deltaFrame)
                                {
                                    maskReader.ResetNibbleSwitch();
                                    for (int i = 0; i < 16; i++)
                                        lineBuf[drawDestOfs + offsets[i]] = maskReader.ReadPixel();
                                }
                                break;
                        }
                        drawDestOfs += 4;
                    }
                }

                if (deltaFrame)
                {
                    for (int y = 0; y < 4 && remainingHeight > 0; y++, remainingHeight--)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            if (lineBuf[x + y * 320] != 0)
                                dest[destPtr] = lineBuf[x + y * 320];
                            destPtr++;
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < 4 && remainingHeight > 0; y++, remainingHeight--)
                    {
                        Buffer.BlockCopy(lineBuf, y * 320, dest, destPtr, width);
                        destPtr += pitch;
                    }
                }
            }
            return dest;

        }

    }


    public class ValueReader
    {
        private readonly BinaryReader _reader;
        private readonly bool _nibbleMode;
        private bool _useLowNibble;
        private byte _currentByte;

        public ValueReader(BinaryReader reader, bool nibbleMode)
        {
            _reader = reader;
            _nibbleMode = nibbleMode;
            _useLowNibble = false;
        }

        public byte ReadPixel()
        {
            byte value;
            if (_nibbleMode)
            {
                if (_useLowNibble)
                {
                    value = (byte)((_reader.ReadByte() >> 4) & 0x0F);
                }
                else
                {
                    value = (byte)(_reader.ReadByte() & 0x0F);
                }
                _useLowNibble = !_useLowNibble;
            }
            else
            {
                value = _reader.ReadByte();
            }
            return value;
        }

        public ushort ReadUInt16()
        {
            return _reader.ReadUInt16();
        }

        public uint ReadUInt32()
        {
            return _reader.ReadUInt32();
        }

        public void ResetNibbleSwitch()
        {
            _useLowNibble = false;
        }
    }

}
