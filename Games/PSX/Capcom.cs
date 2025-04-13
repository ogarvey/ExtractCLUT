using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PSX
{
    public static class Capcom
    {
        public static ushort[] CapDecompress(ushort[] input)
        {
            List<ushort> outputList = new List<ushort>();
            int flag = 0x8000;
            int jmp;
            int len = 3;
            int inputIndex = 0;

            while (inputIndex < input.Length)
            {
                // Cache next 16 flags
                if ((flag & 0x8000) != 0)
                {
                    flag = (input[inputIndex++] << 16) | 1;
                }

                // Not literal
                if ((flag & 0x80000000) != 0)
                {
                    jmp = input[inputIndex++];

                    // Length encoded with 5 bits?
                    if ((jmp & 0xF800) != 0)
                    {
                        len = jmp >> 11;
                        jmp &= 0x7FF;
                    }
                    // Then it's using 16 bits
                    else
                    {
                        len = input[inputIndex++];
                    }

                    // Premature end signal
                    if ((jmp == 0) && (len == 0))
                    {
                        return outputList.ToArray();
                    }

                    // Zero filled RLE case when there's nowhere to jump back
                    if (jmp == 0)
                    {
                        outputList.AddRange(Enumerable.Repeat((ushort)0, len));
                        len = 0;
                    }
                    // Jump back & copy from output
                    else
                    {
                        int backPointer = outputList.Count - jmp;
                        while (len > 0)
                        {
                            outputList.Add(outputList[backPointer++]);
                            len--;
                        }
                    }
                }
                // Literal, read and write as is
                else
                {
                    outputList.Add(input[inputIndex++]);
                }

                // Next flag
                flag <<= 1;
            }

            return outputList.ToArray(); // Convert the list to an array.
        }
    }
}
