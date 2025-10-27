using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Anvil
{
    public static class DfaDecompress
    {
        public static byte[] DecompressType3(BinaryReader reader)
        {
            // Read the header - pixel count and output offset
            int remainingPixels = reader.ReadInt32();
            if (remainingPixels == 0)
            {
                return new byte[0];
            }

            int outputOffset = reader.ReadInt32();

            // Create output buffer - each pixel is 2 bytes (ushort)
            var outputBuffer = new List<ushort>();

            // Skip to the specified offset position in output
            // In C++, this was pointer arithmetic, here we'll track position
            int currentOutputPosition = outputOffset / 2; // Convert byte offset to ushort index

            // Ensure we have enough capacity
            while (outputBuffer.Count <= currentOutputPosition)
            {
                outputBuffer.Add(0);
            }

            while (true)
            {
                // Read control bits
                ushort controlBits = reader.ReadUInt16();
                ushort bitMask = 1;

                do
                {
                    // Process each bit in the control byte
                    while ((bitMask & controlBits) == 0)
                    {
                        // Literal copy - copy next value directly
                        ushort literal = reader.ReadUInt16();

                        if (currentOutputPosition >= outputBuffer.Count)
                        {
                            outputBuffer.Add(literal);
                        }
                        else
                        {
                            outputBuffer[currentOutputPosition] = literal;
                        }

                        currentOutputPosition++;
                        remainingPixels--;

                        if (remainingPixels == 0)
                        {
                            // Convert to byte array for return
                            var result = new byte[outputBuffer.Count * 2];
                            for (int i = 0; i < outputBuffer.Count; i++)
                            {
                                var bytes = BitConverter.GetBytes(outputBuffer[i]);
                                result[i * 2] = bytes[0];
                                result[i * 2 + 1] = bytes[1];
                            }
                            return result;
                        }

                        // Check if we need to move to next control bits
                        bool bitMaskOverflow = (bitMask & 0x8000) != 0;
                        bitMask <<= 1;
                        if (bitMaskOverflow)
                        {
                            goto NextControlBits;
                        }
                    }

                    // Back reference - copy from earlier in the buffer
                    ushort backRefCommand = reader.ReadUInt16();
                    int backRefOffset = backRefCommand & 0x1FFF; // Lower 13 bits
                    int copyLength = (backRefCommand >> 13) + 2; // Upper 3 bits + 2

                    int backRefSourceIndex = currentOutputPosition - backRefOffset;

                    // Ensure we have enough space in output buffer
                    while (outputBuffer.Count < currentOutputPosition + copyLength)
                    {
                        outputBuffer.Add(0);
                    }

                    // Copy the specified number of values
                    for (int i = 0; i < copyLength; i++)
                    {
                        if (backRefSourceIndex + i >= 0 && backRefSourceIndex + i < outputBuffer.Count)
                        {
                            outputBuffer[currentOutputPosition + i] = outputBuffer[backRefSourceIndex + i];
                        }
                    }

                    currentOutputPosition += copyLength;
                    remainingPixels--;

                    if (remainingPixels == 0)
                    {
                        // Convert to byte array for return
                        var result = new byte[outputBuffer.Count * 2];
                        for (int i = 0; i < outputBuffer.Count; i++)
                        {
                            var bytes = BitConverter.GetBytes(outputBuffer[i]);
                            result[i * 2] = bytes[0];
                            result[i * 2 + 1] = bytes[1];
                        }
                        return result;
                    }

                    // Check if we need to continue with current control bits
                    bool shouldContinue = (bitMask & 0x8000) == 0;
                    bitMask <<= 1;

                    if (!shouldContinue)
                    {
                        break;
                    }

                } while (true);

            NextControlBits:;
            }
        }
    }
}
