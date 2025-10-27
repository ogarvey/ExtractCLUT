using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.Generic
{
    public class CausewayDecompressor
    {
        private const int RepMinSize = 2;
        private const string CWC_SIGNATURE = "CWC";

        /// <summary>
        /// Header structure for compressed data
        /// </summary>
        private class DecodeHeader
        {
            public string ID { get; set; }  // "CWC"
            public byte Bits { get; set; }
            public uint Length { get; set; }
            public uint Size { get; set; }
        }

        /// <summary>
        /// Decompresses a Causeway-compressed executable file
        /// </summary>
        /// <param name="inputPath">Path to compressed EXE file</param>
        /// <param name="outputPath">Path for decompressed output</param>
        public static void DecompressFile(string inputPath, string outputPath)
        {
            byte[] compressedData = File.ReadAllBytes(inputPath);
            byte[] decompressed = Decompress(compressedData);
            File.WriteAllBytes(outputPath, decompressed);
        }

        /// <summary>
        /// Decompresses Causeway-compressed data
        /// </summary>
        /// <param name="compressedData">Compressed data bytes</param>
        /// <returns>Decompressed data</returns>
        public static byte[] Decompress(byte[] compressedData)
        {
            int offset = 0;

            // Find the compressed data section (skip EXE stub if present)
            offset = FindCompressedDataStart(compressedData);
            if (offset == -1)
            {
                throw new InvalidDataException("No CWC signature found in file");
            }

            return DecompressFromOffset(compressedData, offset);
        }

        /// <summary>
        /// Finds the start of CWC compressed data in the file
        /// </summary>
        private static int FindCompressedDataStart(byte[] data)
        {
            // Look for "CWC" signature
            for (int i = 0; i < data.Length - 3; i++)
            {
                if (data[i] == 'C' && data[i + 1] == 'W' && data[i + 2] == 'C')
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Decompresses data starting from a specific offset
        /// </summary>
        private static byte[] DecompressFromOffset(byte[] compressedData, int offset)
        {
            // Read header
            if (offset + 12 > compressedData.Length)
            {
                throw new InvalidDataException("Compressed data too short for header");
            }

            string id = System.Text.Encoding.ASCII.GetString(compressedData, offset, 3);
            if (id != CWC_SIGNATURE)
            {
                throw new InvalidDataException($"Invalid signature: expected {CWC_SIGNATURE}, got {id}");
            }

            byte bits = compressedData[offset + 3];
            uint length = BitConverter.ToUInt32(compressedData, offset + 4);
            uint size = BitConverter.ToUInt32(compressedData, offset + 8);

            // Calculate mask and shift values
            int mask = (1 << bits) - 1;
            int shift = bits - 8;

            // Start after header (12 bytes)
            int sourcePos = offset + 12;

            // Initialize bit reading with 32-bit buffer (encoder format, not 16-bit decoder stub)
            // The encoder (cwc.asm) uses 32-bit buffers for data-only CWC files
            uint bitBuffer = BitConverter.ToUInt32(compressedData, sourcePos);
            sourcePos += 4;
            int bitsLeft = 32;

            // Output buffer with 32KB zero-initialized history
            // The encoder (cwc.asm) uses a 32KB sliding window filled with zeros,
            // so the decompressor must also start with the same initialization
            // to handle back-references to positions before the actual data
            using (var output = new MemoryStream((int)size + 32768))
            {
                // Write 32KB of zeros to initialize the history buffer
                byte[] zeros = new byte[32768];
                output.Write(zeros, 0, zeros.Length);

                // Main decompression loop
                while (true)
                {
                    // Read next bit
                    bool bit1 = ReadBit(ref bitBuffer, ref bitsLeft, ref sourcePos, compressedData);

                    if (bit1)
                    {
                        // Literal byte
                        byte literalByte = compressedData[sourcePos++];
                        output.WriteByte(literalByte);
                        continue;
                    }

                    // Read second bit
                    bool bit2 = ReadBit(ref bitBuffer, ref bitsLeft, ref sourcePos, compressedData);

                    if (bit2)
                    {
                        // 8-bit position, 2-bit length
                        int len = 0;
                        len = (len << 1) | (ReadBit(ref bitBuffer, ref bitsLeft, ref sourcePos, compressedData) ? 1 : 0);
                        len = (len << 1) | (ReadBit(ref bitBuffer, ref bitsLeft, ref sourcePos, compressedData) ? 1 : 0);
                        len += 2;

                        int pos = compressedData[sourcePos++];
                        pos--;

                        if (pos < 0)
                        {
                            // Run
                            byte runByte = compressedData[sourcePos++];
                            len++;
                            for (int i = 0; i < len; i++)
                            {
                                output.WriteByte(runByte);
                            }
                        }
                        else
                        {
                            // Copy from history
                            pos += len;
                            CopyFromHistory(output, pos, len);
                        }
                        continue;
                    }

                    // Read third bit
                    bool bit3 = ReadBit(ref bitBuffer, ref bitsLeft, ref sourcePos, compressedData);

                    if (bit3)
                    {
                        // 12-bit position, 4-bit length
                        ushort val = BitConverter.ToUInt16(compressedData, sourcePos);
                        sourcePos += 2;

                        int len = val & 0xF;
                        len += 2;
                        int pos = val >> 4;
                        pos--;

                        if (pos < 0)
                        {
                            // Run
                            byte runByte = compressedData[sourcePos++];
                            len++;
                            for (int i = 0; i < len; i++)
                            {
                                output.WriteByte(runByte);
                            }
                        }
                        else
                        {
                            // Copy from history
                            pos += len;
                            CopyFromHistory(output, pos, len);
                        }
                        continue;
                    }

                    // 12-bit position, 12-bit length (or special codes)
                    uint val24 = compressedData[sourcePos] |
                                 ((uint)compressedData[sourcePos + 1] << 8) |
                                 ((uint)compressedData[sourcePos + 2] << 16);
                    sourcePos += 3;

                    int len3 = (int)(val24 & mask);
                    len3 += 2;
                    int pos3 = (int)(val24 >> bits);
                    pos3--;

                    if (pos3 < 0)
                    {
                        // Check for special codes
                        if (len3 < RepMinSize + 16)
                        {
                            if (len3 == RepMinSize + 2)
                            {
                                // Rationalize destination - just continue
                                continue;
                            }
                            else if (len3 == RepMinSize + 1)
                            {
                                // Rationalize source - ignore
                                continue;
                            }
                            else if (len3 == RepMinSize + 3)
                            {
                                // Literal string
                                int litLen = compressedData[sourcePos++];
                                for (int i = 0; i < litLen; i++)
                                {
                                    output.WriteByte(compressedData[sourcePos++]);
                                }
                                continue;
                            }
                            else if (len3 == RepMinSize)
                            {
                                // Terminator - we're done
                                break;
                            }
                        }
                        else
                        {
                            // Run with 12-bit length
                            byte runByte = compressedData[sourcePos++];
                            len3++;
                            for (int i = 0; i < len3; i++)
                            {
                                output.WriteByte(runByte);
                            }
                        }
                    }
                    else
                    {
                        // Copy from history
                        pos3 += len3;
                        CopyFromHistory(output, pos3, len3);
                    }
                }

                // Extract only the actual decompressed data (skip the 32KB initialization)
                byte[] fullBuffer = output.ToArray();
                int actualSize = Math.Min((int)length, fullBuffer.Length - 32768);
                byte[] result = new byte[actualSize];
                Array.Copy(fullBuffer, 32768, result, 0, actualSize);
                return result;
            }
        }

        /// <summary>
        /// Reads a single bit from the compressed stream (MSB first from 32-bit buffer)
        /// </summary>
        private static bool ReadBit(ref uint bitBuffer, ref int bitsLeft, ref int sourcePos, byte[] data)
        {
            bool result = (bitBuffer & 0x80000000) != 0;
            bitBuffer <<= 1;
            bitsLeft--;

            if (bitsLeft == 0)
            {
                // Refill bit buffer with 32 bits (encoder format)
                if (sourcePos + 3 < data.Length)
                {
                    bitBuffer = BitConverter.ToUInt32(data, sourcePos);
                    sourcePos += 4;
                    bitsLeft = 32;
                }
            }

            return result;
        }

        /// <summary>
        /// Copies data from earlier in the output stream (LZ77-style)
        /// </summary>
        private static void CopyFromHistory(MemoryStream output, int distance, int length)
        {
            long currentPos = output.Position;
            long sourcePos = currentPos - distance;

            if (sourcePos < 0)
            {
                throw new InvalidDataException($"Invalid back-reference in compressed data: trying to copy from position {sourcePos} (current={currentPos}, distance={distance}, length={length})");
            }

            byte[] buffer = output.GetBuffer();
            for (int i = 0; i < length; i++)
            {
                output.WriteByte(buffer[sourcePos + i]);
            }
        }

        /// <summary>
        /// Checks if a file appears to be Causeway-compressed
        /// </summary>
        public static bool IsCompressed(string filePath)
        {
            try
            {
                byte[] header = new byte[Math.Min(2048, (int)new FileInfo(filePath).Length)];
                using (var fs = File.OpenRead(filePath))
                {
                    fs.Read(header, 0, header.Length);
                }
                return FindCompressedDataStart(header) != -1;
            }
            catch
            {
                return false;
            }
        }
    }
}
