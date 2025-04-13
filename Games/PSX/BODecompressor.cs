using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PSX
{
    [StructLayout(LayoutKind.Explicit)]
    struct SixteenBitOverlay
    {
        [FieldOffset(0)]
        public short asShort;
        [FieldOffset(0)]
        public ushort asUShort;
    }

    public static class BODecompressor
    {
        const int _MaxIterations = 4096000;
        const long _MaxOutputSize = 67108864;

        public static int DecompressFile(string inPath, string outPath)
        {
            FileStream inStream = new FileStream(inPath, FileMode.Open, FileAccess.Read);
            FileStream outStream = new FileStream(outPath, FileMode.Create, FileAccess.ReadWrite);
            BinaryReader inReader = new BinaryReader(inStream);
            BinaryWriter outWriter = new BinaryWriter(outStream);
            BinaryReader outReader = new BinaryReader(outStream);

            inStream.Seek(0, SeekOrigin.Begin);

            FileInfo inInfo = new FileInfo(inPath);
            long compressedSize = inReader.ReadUInt32() + 4;
            if (compressedSize > inInfo.Length)
            {
                Console.WriteLine("Error: the file '" + inPath + "' has a header indicating it should be " +
                    compressedSize.ToString() + " bytes in size, but the actual file size is " + inInfo.Length.ToString() +
                    " bytes. The file is either incomplete or is not a compressed Blood Omen data file.");
                return 10;
            }
            if (compressedSize < inInfo.Length)
            {
                Console.WriteLine("Warning: the file '" + inPath + "' has a header indicating it should be " +
                    compressedSize.ToString() + " bytes in size, but the actual file size is " + inInfo.Length.ToString() +
                    " bytes. The file either contains extra data or is not a compressed Blood Omen data file.");
            }

            bool doneDecompressing = false;

            int numIterations = 0;
            int bytesWritten = 0;

            ushort tempIndex = 4078;
            ushort command = 0;
            short offset;
            byte length;

            byte[] tempBuffer = new byte[4114];
            for (int i = 0; i < 4078; i++)
            {
                tempBuffer[i] = 0x20;
            }

            while (!doneDecompressing)
            {
                if (numIterations > _MaxIterations)
                {
                    Console.WriteLine("Error: the program has iterated " + numIterations.ToString() + " times, " +
                        "which is greater than the maximum number permitted. This indicates that the input is not " +
                        "a compressed Blood Omen data file.");
                    return 21;
                }
                if (outStream.Position > _MaxOutputSize)
                {
                    Console.WriteLine("Error: the output file size has reached " + outStream.Position.ToString() + " bytes, " +
                        "which is greater than the maximum size permitted. This indicates that the input is not " +
                        "a compressed Blood Omen data file.");
                    return 22;
                }

                command = (ushort)(command >> 1);

                if ((command & 0x0100) == 0)
                {
                    command = (ushort)((ushort)(inReader.ReadByte()) | 0xFF00);
                }
                if ((command & 0x01) == 0x01)
                {
                    byte currentByte = inReader.ReadByte();
                    outWriter.Write(currentByte);
                    bytesWritten++;
                    tempBuffer[tempIndex] = currentByte;
                    tempIndex++;
                    //tempIndex = (ushort)(tempIndex % 4096);
                    tempIndex = (ushort)(tempIndex & 0x0FFF);   // may be slightly faster?
                    if (inStream.Position >= inStream.Length)
                    {
                        break;
                    }
                }
                else
                {
                    byte currentByte1 = inReader.ReadByte();
                    byte currentByte2 = inReader.ReadByte();
                    // Some sleight of hand is required here because of how C# handles casting between 
                    // signed and unsigned data types of the same length
                    // The data should never result in the sign bit being negative, but just in case it 
                    // does...
                    int offsetTemp = currentByte1;
                    offsetTemp |= (currentByte2 & 0xF0) << 4;
                    SixteenBitOverlay offsetOL = new SixteenBitOverlay();
                    offsetOL.asUShort = (ushort)offsetTemp;
                    offset = offsetOL.asShort;

                    length = (byte)((currentByte2 & 0x0F) + 3);

                    for (int i = 0; i < length; i++)
                    {
                        //outWriter.Write(tempBuffer[(offset + i) % 4096]);
                        outWriter.Write(tempBuffer[(offset + i) & 0x0FFF]);   // may be slightly faster?
                        bytesWritten++;
                        if (inStream.Position >= inStream.Length)
                        {
                            doneDecompressing = true;
                            break;
                        }
                        //tempBuffer[tempIndex] = tempBuffer[(offset + i) % 4096];
                        tempBuffer[tempIndex] = tempBuffer[(offset + i) & 0x0FFF];   // may be slightly faster?
                        tempIndex++;
                        //tempIndex = (ushort)(tempIndex % 4096);
                        tempIndex = (ushort)(tempIndex & 0x0FFF);   // may be slightly faster?
                    }
                }
                numIterations++;
            }

            // Playstation files apparently need to be multiples of 1024 bytes in size
            // (or maybe it's just PSXMC that complains if they aren't).
            if ((bytesWritten % 1024) > 0)
            {
                int numPadBytes = 1024 - (bytesWritten % 1024);
                for (int i = 0; i < numPadBytes; i++)
                {
                    outWriter.Write((byte)0x00);
                }
            }

            outStream.Close();
            inStream.Close();

            return 0;
        }


    }
}
