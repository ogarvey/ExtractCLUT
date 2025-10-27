using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.TSage
{
    public class DecodeReference
    {
        public ushort vWord { get; set; }
        public byte vByte { get; set; }
    }

    public static class TsageImageFormats
    {
        public static byte[] DecodeRle(byte[] rleData, int width, int height, byte transColor = 0)
        {
            List<byte> decoded = new List<byte>(width * height);
            
            var index = 0;
            for (int yp = 0; yp < height; ++yp)
            {
                var widthRemaining = width;
                while (widthRemaining > 0)
                {
                    byte controlVal = rleData[index++];
                    if ((controlVal & 0x80) == 0)
                    {
                        // Copy specified number of bytes

                        // Common::copy(srcP, srcP + controlVal, destP);
                        decoded.AddRange(rleData.Skip(index).Take(controlVal));
                        index += controlVal;
                        widthRemaining -= controlVal;
                    }
                    else if ((controlVal & 0x40) == 0)
                    {
                        // Skip a specified number of output pixels
                        decoded.AddRange(Enumerable.Repeat(transColor, controlVal & 0x3f));
                        widthRemaining -= controlVal & 0x3f;
                    }
                    else
                    {
                        // Copy a specified pixel a given number of times
                        controlVal &= 0x3f;
                        int pixel = rleData[index++];

                        decoded.AddRange(Enumerable.Repeat((byte)pixel, controlVal));
                        widthRemaining -= controlVal;
                    }
                }
            }
            return decoded.ToArray();
        }
    }
}
