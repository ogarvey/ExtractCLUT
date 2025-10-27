using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Bubsy
{
    public static class BubsyDecompress
    {
        /// <summary>
        /// Decompresses RLE-encoded data in forward direction.
        /// Format: [count][byte] where count indicates how many times to repeat the byte.
        /// The algorithm writes the byte once, then writes it 'count' more times.
        /// </summary>
        /// <param name="uncompressedLength">The expected length of decompressed data.</param>
        /// <param name="compressedData">The compressed input data.</param>
        /// <returns>The decompressed byte array.</returns>
        public static byte[] UncRLE(int uncompressedLength, byte[] compressedData)
        {
            if (uncompressedLength < 0)
                throw new ArgumentOutOfRangeException(nameof(uncompressedLength), "Uncompressed length must be non-negative.");
            
            if (compressedData == null)
                throw new ArgumentNullException(nameof(compressedData));

            var output = new byte[uncompressedLength];
            int outputPos = 0;
            int inputPos = 0;
            int remaining = uncompressedLength;

            while (remaining > 0 && inputPos + 1 < compressedData.Length)
            {
                // Read the count byte and value byte
                byte count = compressedData[inputPos];
                byte value = compressedData[inputPos + 1];
                inputPos += 2;

                // Inner loop: write value, decrement remaining, then check count and loop
                do
                {
                    output[outputPos++] = value;
                    remaining--;
                    
                    if (remaining == 0)
                        break;
                    
                    if (count == 0)
                        break;
                    
                    count--;
                } while (true);
            }

            return output;
        }

        /// <summary>
        /// Decompresses RLE-encoded data in reverse direction.
        /// Format: [count][byte] where count indicates how many times to repeat the byte.
        /// Data is written from the end of the output buffer backwards.
        /// The algorithm writes the byte once, then writes it 'count' more times.
        /// </summary>
        /// <param name="uncompressedLength">The expected length of decompressed data.</param>
        /// <param name="compressedData">The compressed input data.</param>
        /// <returns>The decompressed byte array.</returns>
        public static byte[] UncRLEReverse(int uncompressedLength, byte[] compressedData)
        {
            if (uncompressedLength < 0)
                throw new ArgumentOutOfRangeException(nameof(uncompressedLength), "Uncompressed length must be non-negative.");
            
            if (compressedData == null)
                throw new ArgumentNullException(nameof(compressedData));

            var output = new byte[uncompressedLength];
            int outputPos = uncompressedLength; // Start at the end
            int inputPos = 0;
            int remaining = uncompressedLength;

            while (remaining > 0 && inputPos + 1 < compressedData.Length)
            {
                // Read the count byte and value byte
                byte count = compressedData[inputPos];
                byte value = compressedData[inputPos + 1];
                inputPos += 2;

                // Inner loop: write value in reverse, decrement remaining, then check count and loop
                do
                {
                    outputPos--;
                    output[outputPos] = value;
                    remaining--;
                    
                    if (remaining == 0)
                        break;
                    
                    if (count == 0)
                        break;
                    
                    count--;
                } while (true);
            }

            return output;
        }

        /// <summary>
        /// Decompresses RLE-encoded data with bit-level compression in forward direction.
        /// Uses a combination of RLE and bitwise operations for compression.
        /// High bit (0x80) of the control byte determines the mode:
        /// - If clear (0): Standard RLE - controlByte is the count, next byte is the value
        /// - If set (1): Bitwise mode - uses bit buffer to construct output bytes
        /// </summary>
        /// <param name="uncompressedLength">The expected length of decompressed data.</param>
        /// <param name="compressedData">The compressed input data.</param>
        /// <param name="compressedOffset">Starting offset in compressed data.</param>
        /// <param name="output">The output buffer to write decompressed data to.</param>
        /// <param name="outputOffset">Starting position in output buffer.</param>
        /// <returns>Number of bytes consumed from compressedData.</returns>
        public static int UncRLELobit(uint decompressedSize, byte[] source, byte[] destination)
        {
            int sourceIndex = 0;
            int destIndex = 0;

            // A counter for the bit-packed mode, corresponding to 'local_8'.
            int bitpackCounter = 0;

            // The bitmask for the current bit-packed group, corresponding to 'pbVar4'.
            byte bitmask = 0;

            uint remainingBytes = decompressedSize;

            if (remainingBytes == 0)
            {
                return 0;
            }

            while (remainingBytes > 0)
            {
                byte controlByte = source[sourceIndex];
                int bytesConsumed = 1; // Default bytes consumed per loop is 1.

                // Check the most significant bit to determine the packet type.
                if ((controlByte & 0x80) == 0)
                {
                    // --- Case 1: Run-Length Encoded (RLE) data ---
                    // This packet is 2 bytes: [Control Byte, Value Byte].
                    bytesConsumed = 2;

                    // The length of the run is the lower 7 bits + 1.
                    uint runLength = (uint)controlByte + 1;

                    // Safety check to avoid writing past the destination buffer's expected end.
                    if (remainingBytes < runLength)
                    {
                        runLength = remainingBytes;
                    }

                    // The next byte in the source is the value to be repeated.
                    byte valueToRepeat = source[sourceIndex + 1];

                    for (int i = 0; i < runLength; i++)
                    {
                        destination[destIndex++] = valueToRepeat;
                    }

                    remainingBytes -= runLength;
                }
                else
                {
                    // --- Case 2: Bit-Packed data ---
                    // These packets are processed in groups of 8.
                    // The first packet is 2 bytes: [Control Byte, Bitmask Byte].
                    // The next 7 packets are 1 byte each: [Control Byte].
                    if (bitpackCounter == 0)
                    {
                        // Start of a new 8-byte group. This packet includes the bitmask.
                        bytesConsumed = 2;
                        bitpackCounter = 8;
                        bitmask = source[sourceIndex + 1];
                    }

                    // Construct the output byte. The lower 7 bits from the control byte
                    // are shifted left, and the LSB is filled from the bitmask.
                    byte outputByte = (byte)(controlByte << 1);

                    // Use the most significant bit of the current bitmask for the LSB.
                    if ((bitmask & 0x80) != 0)
                    {
                        outputByte |= 1;
                    }
                    destination[destIndex++] = outputByte;

                    // Shift the bitmask left to expose the next bit for the next iteration.
                    bitmask <<= 1;
                    bitpackCounter--;
                    remainingBytes--;
                }

                // Advance the source index by the number of bytes consumed in this step.
                sourceIndex += bytesConsumed;
            }

            // Return the total number of bytes read from the source buffer.
            return sourceIndex;
        }

        public static byte[] DecompressLobitImage(byte[] compressedData, int initialCompressedOffset, int width = 32, int height = 32)
        {
            List<byte> output = new List<byte>();
            int compressedOffset = initialCompressedOffset;

            for (int line = 0; line < height; line++)
            {
                var lineData = new byte[width];
                int bytesConsumed = UncRLELobit((uint)width, compressedData, lineData);
                output.AddRange(lineData);
                compressedData = compressedData.Skip(bytesConsumed).ToArray();
                compressedOffset += bytesConsumed;
            }

            return output.ToArray();
        }
        /// <summary>
        /// Decompresses RLE-encoded data with bit-level compression in reverse direction.
        /// Uses a combination of RLE and bitwise operations for compression.
        /// Data is written from the end of the output buffer backwards.
        /// High bit (0x80) of the control byte determines the mode:
        /// - If clear (0): Standard RLE - controlByte is the count, next byte is the value
        /// - If set (1): Bitwise mode - uses bit buffer to construct output bytes
        /// </summary>
        /// <param name="uncompressedLength">The expected length of decompressed data.</param>
        /// <param name="compressedData">The compressed input data.</param>
        /// <returns>The decompressed byte array.</returns>
        public static byte[] UncRLELobitReverse(int uncompressedLength, byte[] compressedData)
        {
            if (uncompressedLength < 0)
                throw new ArgumentOutOfRangeException(nameof(uncompressedLength), "Uncompressed length must be non-negative.");
            
            if (compressedData == null)
                throw new ArgumentNullException(nameof(compressedData));

            var output = new byte[uncompressedLength];
            int outputPos = uncompressedLength; // Start at the end
            int inputPos = 0;
            int bitBufferBitsRemaining = 0;
            byte bitBuffer = 0;
            int remaining = uncompressedLength;

            while (remaining > 0 && inputPos < compressedData.Length)
            {
                byte controlByte = compressedData[inputPos];
                inputPos++;

                if ((controlByte & 0x80) == 0)
                {
                    // Standard RLE mode: controlByte is the count
                    int count = controlByte + 1;
                    if (remaining < count)
                    {
                        count = remaining;
                    }
                    
                    if (inputPos >= compressedData.Length)
                        break;

                    byte value = compressedData[inputPos];
                    remaining -= count;
                    inputPos++;

                    for (int i = 0; i < count; i++)
                    {
                        outputPos--;
                        output[outputPos] = value;
                    }
                }
                else
                {
                    // Bitwise mode: use bit buffer
                    if (bitBufferBitsRemaining == 0)
                    {
                        if (inputPos >= compressedData.Length)
                            break;

                        bitBuffer = compressedData[inputPos];
                        inputPos++;
                        bitBufferBitsRemaining = 8;
                    }

                    // Multiply controlByte by 2 (shift left by 1)
                    byte outputByte = (byte)(controlByte * 2);
                    
                    // If high bit of bitBuffer is set, OR with 1
                    if ((bitBuffer & 0x80) != 0)
                    {
                        outputByte |= 1;
                    }

                    outputPos--;
                    output[outputPos] = outputByte;
                    remaining--;

                    // Shift bit buffer left (multiply by 2)
                    bitBuffer = (byte)(bitBuffer * 2);
                    bitBufferBitsRemaining--;
                }
            }

            return output;
        }    
    }
}
