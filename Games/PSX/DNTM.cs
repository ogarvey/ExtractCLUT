using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PSX
{
	public static class DNTM
	{
		public static byte[] DecompressAssetData(byte[] sourceBuffer)
		{
			// Use a List<byte> for the destination as its size is unknown beforehand.
			var destinationBuffer = new List<byte>();
			int sourceIndex = 0;

			// This is used to process control bits one by one.
			uint bitMask = 0;

			// Main decompression loop.
			while (true)
			{
				// This inner loop handles literal byte copies.
				while (true)
				{
					// Shift the bit mask to get the next control bit.
					bitMask >>= 1;

					// If all 8 control bits have been used, read a new byte from the
					// source to serve as the next 8 control bits.
					if ((bitMask & 0xff00) == 0)
					{
						uint controlWord = sourceBuffer[sourceIndex++];
						bitMask = controlWord | 0xff00;
					}

					// Check the current control bit (the LSB).
					// If the bit is 1, it indicates a back-reference, so we break to handle it.
					if ((bitMask & 1) != 0)
					{
						break;
					}

					// If the bit is 0, it's a literal copy.
					// Copy one byte directly from the source to the destination.
					destinationBuffer.Add(sourceBuffer[sourceIndex++]);
				}

				// --- Back-reference handling ---
				int copyLength;
				int copyOffset;

				// Read the control word that defines the copy length and offset.
				uint backRefControlWord = sourceBuffer[sourceIndex++];

				// The format of the back-reference depends on the value of the control word.
				if (backRefControlWord < 0x60)
				{
					// --- Long back-reference ---
					// The copy offset is encoded in the lower 4 bits of the control word
					// and the next byte in the source stream.
					copyOffset = (int)((backRefControlWord & 0xf) << 8 | sourceBuffer[sourceIndex++]);

					// A copy_offset of 0 is the end-of-stream marker.
					if (copyOffset == 0)
					{
						break; // Decompression is complete.
					}

					// The copy length is encoded in the upper 4 bits of the control word.
					// A special case for longer copies: if the upper 4 bits are 5,
					// the length is extended by the value of the next byte.
					if ((backRefControlWord >> 4) == 5)
					{
						copyLength = sourceBuffer[sourceIndex++] + 8;
					}
					else
					{
						// Base length is 3.
						copyLength = ((int)backRefControlWord >> 4) + 3;
					}
				}
				else
				{
					// --- Short back-reference ---
					// The copy offset is derived from the control word.
					copyOffset = (int)(0x100 - backRefControlWord);
					// The copy length is fixed at 2.
					copyLength = 2;
				}

				// Perform the back-reference copy.
				int readPosition = destinationBuffer.Count - copyOffset;
				for (int i = 0; i < copyLength; i++)
				{
					destinationBuffer.Add(destinationBuffer[readPosition + i]);
				}
			}

			return destinationBuffer.ToArray();
		}
	}
}
