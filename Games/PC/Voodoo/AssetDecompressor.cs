using System;
using System.Collections.Generic;

namespace VoodooAssetSystem
{
    /// <summary>
    /// C# implementation of the Voodoo game's asset decompression algorithm
    /// Based on reverse engineering of FUN_0043987c (DecompressAssetData)
    /// </summary>
    public static class AssetDecompressor
    {
        /// <summary>
        /// Main decompression method that processes compressed asset data
        /// </summary>
        /// <param name="compressedData">Input compressed byte array</param>
        /// <param name="compressedLength">Length of compressed data to process</param>
        /// <returns>Decompressed byte array</returns>
        public static byte[] DecompressAssetData(byte[] compressedData, int compressedLength)
        {
            if (compressedData == null)
                throw new ArgumentNullException(nameof(compressedData));
            
            if (compressedLength <= 0 || compressedLength > compressedData.Length)
                throw new ArgumentException("Invalid compressed length", nameof(compressedLength));

            var output = new List<byte>();
            int inputPos = 0;
            
            // Read initial control byte pattern (based on decompiled logic)
            if (inputPos >= compressedLength) return output.ToArray();
            
            int controlPattern = (compressedData[inputPos] << 8);
            inputPos++;
            
            while (inputPos < compressedLength)
            {
                if (inputPos >= compressedLength) break;
                
                byte currentByte = compressedData[inputPos];
                byte nextByte = (inputPos + 1 < compressedLength) ? compressedData[inputPos + 1] : (byte)0;
                
                byte controlByte = (byte)(controlPattern >> 8);
                
                if (controlByte == currentByte)
                {
                    // Compressed sequence detected
                    inputPos += 2; // Skip control and length bytes
                    
                    if (inputPos > compressedLength) break;
                    
                    byte compressionType = nextByte;
                    
                    if (compressionType == 0)
                    {
                        // End of data marker
                        break;
                    }
                    else if ((compressionType & 0x80) == 0)
                    {
                        // Pattern-based compression mode
                        ProcessPatternCompression(compressedData, ref inputPos, compressedLength, compressionType, output);
                    }
                    else
                    {
                        // Run-Length Encoding mode
                        ProcessRLECompression(compressedData, ref inputPos, compressedLength, compressionType, output);
                    }
                    
                    controlPattern = (int)((uint)controlPattern & 0xFFFFFF00u) | (inputPos < compressedLength ? compressedData[inputPos] : 0);
                }
                else
                {
                    // Literal byte - copy directly
                    output.Add(currentByte);
                    inputPos++;
                    controlPattern = (int)((controlPattern & 0xFFFFFF00) | currentByte);
                }
            }
            
            return output.ToArray();
        }
        
        /// <summary>
        /// Processes pattern-based compression sequences
        /// Based on the (compressionType & 0x80) == 0 branch in original code
        /// </summary>
        private static void ProcessPatternCompression(byte[] compressedData, ref int inputPos, int compressedLength, 
            byte compressionType, List<byte> output)
        {
            // Extract pattern information from compression type
            int patternMode = (compressionType & 0x60) >> 3; // Bits 5-6 shifted
            int baseLength = (compressionType & 0x0F) + 4;   // Lower 4 bits + 4
            
            // Simulate function pointer table dispatch based on pattern mode
            switch (patternMode)
            {
                case 0: // Basic pattern copy
                    ProcessBasicPattern(compressedData, ref inputPos, compressedLength, baseLength, output);
                    break;
                    
                case 1: // Extended pattern with offset
                    ProcessExtendedPattern(compressedData, ref inputPos, compressedLength, baseLength, output);
                    break;
                    
                case 2: // Dictionary-based pattern
                    ProcessDictionaryPattern(compressedData, ref inputPos, compressedLength, baseLength, output);
                    break;
                    
                case 3: // Complex pattern matching
                    ProcessComplexPattern(compressedData, ref inputPos, compressedLength, baseLength, output);
                    break;
                    
                default:
                    // Fallback - copy as literal
                    if (inputPos < compressedLength)
                    {
                        output.Add(compressedData[inputPos]);
                        inputPos++;
                    }
                    break;
            }
        }
        
        /// <summary>
        /// Processes Run-Length Encoding compression sequences
        /// Based on the (compressionType & 0x80) != 0 branch in original code
        /// </summary>
        private static void ProcessRLECompression(byte[] compressedData, ref int inputPos, int compressedLength, 
            byte compressionType, List<byte> output)
        {
            // Extract RLE parameters
            int repeatCount = (compressionType & 0x1F) + 2;  // Lower 5 bits + 2
            int byteCount = ((compressionType & 0x60) >> 5) + 1; // Bits 5-6 + 1
            
            // Read the bytes to repeat
            var patternBytes = new byte[byteCount];
            for (int i = 0; i < byteCount && inputPos < compressedLength; i++)
            {
                patternBytes[i] = compressedData[inputPos];
                inputPos++;
            }
            
            // Repeat the pattern
            for (int repeat = 0; repeat < repeatCount; repeat++)
            {
                for (int i = 0; i < byteCount; i++)
                {
                    output.Add(patternBytes[i]);
                }
            }
        }
        
        /// <summary>
        /// Basic pattern processing - simple byte copying
        /// </summary>
        private static void ProcessBasicPattern(byte[] compressedData, ref int inputPos, int compressedLength, 
            int length, List<byte> output)
        {
            for (int i = 0; i < length && inputPos < compressedLength; i++)
            {
                output.Add(compressedData[inputPos]);
                inputPos++;
            }
        }
        
        /// <summary>
        /// Extended pattern with offset referencing
        /// </summary>
        private static void ProcessExtendedPattern(byte[] compressedData, ref int inputPos, int compressedLength, 
            int length, List<byte> output)
        {
            if (inputPos + 1 >= compressedLength) return;
            
            // Read offset (assuming 16-bit offset)
            int offset = compressedData[inputPos] | (compressedData[inputPos + 1] << 8);
            inputPos += 2;
            
            // Copy from previous data if offset is valid
            int sourcePos = output.Count - offset;
            if (sourcePos >= 0)
            {
                for (int i = 0; i < length && sourcePos + i < output.Count; i++)
                {
                    output.Add(output[sourcePos + i]);
                }
            }
        }
        
        /// <summary>
        /// Dictionary-based pattern matching
        /// </summary>
        private static void ProcessDictionaryPattern(byte[] compressedData, ref int inputPos, int compressedLength, 
            int length, List<byte> output)
        {
            // Simplified dictionary lookup - in real implementation this would use a predefined dictionary
            if (inputPos < compressedLength)
            {
                byte dictIndex = compressedData[inputPos];
                inputPos++;
                
                // Generate pattern based on dictionary index (simplified)
                byte patternByte = (byte)(dictIndex ^ 0xAA); // Simple transformation
                for (int i = 0; i < length; i++)
                {
                    output.Add(patternByte);
                }
            }
        }
        
        /// <summary>
        /// Complex pattern matching with multiple parameters
        /// </summary>
        private static void ProcessComplexPattern(byte[] compressedData, ref int inputPos, int compressedLength, 
            int length, List<byte> output)
        {
            if (inputPos + 2 >= compressedLength) return;
            
            byte param1 = compressedData[inputPos];
            byte param2 = compressedData[inputPos + 1];
            inputPos += 2;
            
            // Complex pattern generation based on parameters
            for (int i = 0; i < length; i++)
            {
                byte value = (byte)((param1 + i * param2) & 0xFF);
                output.Add(value);
            }
        }
        
        /// <summary>
        /// Alternative decompression method for bit-level compressed data
        /// Based on FUN_0043ab01 (BitwiseDecompressor)
        /// </summary>
        public static byte[] DecompressBitwiseData(byte[] compressedData, int compressedLength)
        {
            if (compressedData == null)
                throw new ArgumentNullException(nameof(compressedData));
                
            var output = new List<byte>();
            var bitReader = new BitReader(compressedData, compressedLength);
            
            while (!bitReader.IsAtEnd())
            {
                // Read mode bits (2 bits)
                int mode = bitReader.ReadBits(2);
                
                switch (mode)
                {
                    case 0:
                        ProcessBitwiseMode0(bitReader, output);
                        break;
                    case 1:
                        ProcessBitwiseMode1(bitReader, output);
                        break;
                    case 2:
                        ProcessBitwiseMode2(bitReader, output);
                        break;
                    case 3:
                        // End marker or special case
                        return output.ToArray();
                }
            }
            
            return output.ToArray();
        }
        
        private static void ProcessBitwiseMode0(BitReader reader, List<byte> output)
        {
            // Mode 0: Direct byte copy
            if (!reader.IsAtEnd())
            {
                int value = reader.ReadBits(8);
                output.Add((byte)value);
            }
        }
        
        private static void ProcessBitwiseMode1(BitReader reader, List<byte> output)
        {
            // Mode 1: Short RLE
            int count = reader.ReadBits(4) + 1;
            int value = reader.ReadBits(8);
            
            for (int i = 0; i < count; i++)
            {
                output.Add((byte)value);
            }
        }
        
        private static void ProcessBitwiseMode2(BitReader reader, List<byte> output)
        {
            // Mode 2: Reference to previous data
            int offset = reader.ReadBits(8);
            int length = reader.ReadBits(4) + 1;
            
            int sourcePos = output.Count - offset;
            if (sourcePos >= 0)
            {
                for (int i = 0; i < length && sourcePos + i < output.Count; i++)
                {
                    output.Add(output[sourcePos + i]);
                }
            }
        }
    }
    
    /// <summary>
    /// Helper class for reading individual bits from byte array
    /// </summary>
    internal class BitReader
    {
        private readonly byte[] data;
        private readonly int maxLength;
        private int bytePos;
        private int bitPos;
        private int bitBuffer;
        private int bitsInBuffer;
        
        public BitReader(byte[] data, int length)
        {
            this.data = data;
            this.maxLength = Math.Min(length, data.Length);
            this.bytePos = 0;
            this.bitPos = 0;
            this.bitBuffer = 0;
            this.bitsInBuffer = 0;
        }
        
        public bool IsAtEnd()
        {
            return bytePos >= maxLength && bitsInBuffer == 0;
        }
        
        public int ReadBits(int count)
        {
            while (bitsInBuffer < count && bytePos < maxLength)
            {
                bitBuffer |= data[bytePos] << bitsInBuffer;
                bitsInBuffer += 8;
                bytePos++;
            }
            
            if (bitsInBuffer < count)
                return 0; // Not enough bits available
            
            int result = bitBuffer & ((1 << count) - 1);
            bitBuffer >>= count;
            bitsInBuffer -= count;
            
            return result;
        }
    }
}
