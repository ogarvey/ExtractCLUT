using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.TSage
{
    public static class TsageDecompress
    {
        public static void Decompress(string compressedFile)
        {

            var table = new List<DecodeReference>();
            table.AddRange(Enumerable.Range(0, 0x1000).Select(i => new DecodeReference()));
            var tokenList = new List<ushort>();

            using var bitReader = new BitReader(File.OpenRead(compressedFile));

            ushort token = bitReader.ReadToken();
            var outputList = new List<byte>();
            uint bytesWritten = 0;
            ushort ctrCurrent = 0x102;
            ushort ctrMax = 0x200;
            ushort word_48050 = 0x0;
            ushort word_48054 = 0x0;
            ushort currentToken = 0x0;
            byte byte_49068 = 0x0;
            byte byte_49069 = 0x0;


            while (token != 0x101)
            {
                if (token == 0x100)
                {
                    bitReader.NumBits = 9;
                    ctrMax = 0x200;
                    ctrCurrent = 0x102;

                    currentToken = word_48050 = bitReader.ReadToken();
                    byte_49069 = byte_49068 = (byte)currentToken;

                    ++bytesWritten;
                    outputList.Add(byte_49069);
                }
                else
                {
                    word_48054 = word_48050 = token;

                    if (token >= ctrCurrent)
                    {
                        word_48050 = currentToken;
                        tokenList.Add(byte_49068);
                    }

                    while (word_48050 >= 0x100)
                    {
                        tokenList.Add(table[word_48050].vByte);
                        word_48050 = table[word_48050].vWord;
                    }

                    byte_49069 = byte_49068 = (byte)word_48050;
                    tokenList.Add(word_48050);

                    while (!tokenList.Count.Equals(0))
                    {
                        ++bytesWritten;
                        outputList.Add((byte)tokenList[tokenList.Count - 1]);
                        tokenList.RemoveAt(tokenList.Count - 1);
                    }
                    table[ctrCurrent].vByte = byte_49069;
                    table[ctrCurrent].vWord = currentToken;
                    ++ctrCurrent;

                    currentToken = word_48054;

                    if ((ctrCurrent >= ctrMax) && (bitReader.NumBits != 12))
                    {
                        // Move to the next higher bit-rate
                        ++bitReader.NumBits;
                        ctrMax <<= 1;
                    }
                }
                token = bitReader.ReadToken();
            }

            var output = outputList.ToArray();
            var outputDir  = Path.Combine(Path.GetDirectoryName(compressedFile), "decompressed");
            Directory.CreateDirectory(outputDir);
            File.WriteAllBytes(Path.Combine(outputDir, Path.GetFileNameWithoutExtension(compressedFile) + ".out"), output);
        }
    }
}
