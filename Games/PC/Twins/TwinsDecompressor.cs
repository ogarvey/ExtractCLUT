using System;
using System.IO;

namespace ExtractCLUT.Games.PC.Twins
{
    public class TwinsDecompressor
    {

        private byte[] _decompressionTables;
        private byte[] _workBuffer;

        private byte[] _outputBuffer = Array.Empty<byte>();
        private int _outputBufferOffset;
        private byte[] _compressedData = Array.Empty<byte>();
        private int _compressedDataOffset;

        // Global state variables identified in decompilation
        private int _bitBuffer; // DAT_0003102c
        private int _bitsRemaining; // DAT_0003103c
        private int _currentBitOffset; // DAT_00031040
        private int _currentByteOffset; // DAT_00031038
        private int _outputIndex; // DAT_00031034
        private int _stateVar1; // DAT_00031030
        private int _bytesRemainingToDecompress; // DAT_000311e4

        private const int WORK_BUFFER_SIZE = 0x12e38;
        private const int COMPRESSED_DATA_BUFFER_OFFSET = 0x10e27;
        private const int COMPRESSED_DATA_BUFFER_SIZE = 0x2000;

        public TwinsDecompressor(string uniFilePath)
        {
            if (!File.Exists(uniFilePath))
                throw new FileNotFoundException("UNI file not found", uniFilePath);

            _decompressionTables = File.ReadAllBytes(uniFilePath);
            if (_decompressionTables.Length != 0x676c)
                Console.WriteLine($"Warning: UNI file size is {_decompressionTables.Length}, expected 0x676c.");

            _workBuffer = new byte[WORK_BUFFER_SIZE];
        }

        public byte[] Load_Compressed_Resource(string resourceFilePath)
        {
            using (var fs = new FileStream(resourceFilePath, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                // Load_Compressed_Resource in Ghidra forces Resource_Count = 0.
                // This implies it treats the file as having a single entry.
                // The first 8 bytes are CompressedSize and DecompressedSize.

                int compressedSize = reader.ReadInt32();
                int decompressedSize = reader.ReadInt32();

                // Read the compressed data
                byte[] compressedData = reader.ReadBytes(compressedSize);

                return Decompress(compressedData, decompressedSize);
            }
        }

        private struct ResourceEntry
        {
            public int CompressedSize;
            public int DecompressedSize;
        }

        public byte[] Decompress(byte[] compressedData, int decompressedSize)
        {
            Console.WriteLine($"Decompressing: CompressedSize={compressedData.Length}, DecompressedSize={decompressedSize}");
            if (_decompressionTables != null && _decompressionTables.Length > 4)
            {
                Console.WriteLine($"UNI Table First Bytes: {_decompressionTables[0]:X2} {_decompressionTables[1]:X2} {_decompressionTables[2]:X2} {_decompressionTables[3]:X2}");
                Console.WriteLine($"UNI Table Offset 0x5945: {_decompressionTables[0x5945]:X2}");
            }

            _compressedData = compressedData;
            _outputBuffer = new byte[decompressedSize];
            _outputBufferOffset = 0;
            _bytesRemainingToDecompress = decompressedSize;

            Array.Copy(_decompressionTables, 0x5948, _workBuffer, 0x10003, 0xE24);
            _workBuffer[0x4000] = _decompressionTables[0x5945];
            WriteUInt16(0x10001, BitConverter.ToUInt16(_decompressionTables, 0x5946));

            int initialLoadSize = Math.Min(compressedData.Length, COMPRESSED_DATA_BUFFER_SIZE);
            Array.Copy(compressedData, 0, _workBuffer, COMPRESSED_DATA_BUFFER_OFFSET, initialLoadSize);
            _compressedDataOffset = initialLoadSize;

            _bitBuffer = 0;
            _stateVar1 = 0;
            _outputIndex = 0;
            _bitsRemaining = 0;
            _currentBitOffset = 0;
            _currentByteOffset = 0;

            Decompress_Update_State();
            _bitsRemaining = 8;

            while (true)
            {
                if (_bytesRemainingToDecompress <= 0)
                {
                    FlushOutput();
                    return _outputBuffer;
                }

                if (_currentByteOffset > 0x1ff3)
                {
                    int remaining = COMPRESSED_DATA_BUFFER_SIZE - _currentByteOffset;
                    Array.Copy(_workBuffer, COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset, _workBuffer, COMPRESSED_DATA_BUFFER_OFFSET, remaining);

                    int space = COMPRESSED_DATA_BUFFER_SIZE - remaining;
                    int available = _compressedData.Length - _compressedDataOffset;
                    int toRead = Math.Min(space, available);

                    if (toRead > 0)
                    {
                        Array.Copy(_compressedData, _compressedDataOffset, _workBuffer, COMPRESSED_DATA_BUFFER_OFFSET + remaining, toRead);
                        _compressedDataOffset += toRead;
                    }
                    _currentByteOffset = 0;
                }

                ushort lastFlushIndex = ReadUInt16(0x10e13);
                int diff = (ushort)_outputIndex - lastFlushIndex;
                if (diff < 0) diff += 0x10000;

                if (lastFlushIndex != (ushort)_outputIndex && diff < 0x110)
                {
                    FlushOutput();
                    if (_bytesRemainingToDecompress <= 0) return _outputBuffer;
                }

                if (ReadByte(0x10e0b) != 0)
                {
                    DecodeSymbol();
                    continue;
                }

                _bitsRemaining--;
                if (_bitsRemaining < 0)
                {
                    Decompress_Update_State();
                    _bitsRemaining = 7;
                }

                int bit = (_bitBuffer >> 7) & 1;
                _bitBuffer = (_bitBuffer << 1) & 0xFF;

                if (bit == 0)
                {
                    _bitsRemaining--;
                    if (_bitsRemaining < 0)
                    {
                        Decompress_Update_State();
                        _bitsRemaining = 7;
                    }

                    int bit2 = (_bitBuffer >> 7) & 1;
                    _bitBuffer = (_bitBuffer << 1) & 0xFF;

                    if (bit2 == 1)
                    {
                        if (ReadUInt16(0x10e0e) <= ReadUInt16(0x4383))
                        {
                            DecodeSymbol_Alt();
                        }
                        else
                        {
                            DecodeSymbol();
                        }
                    }
                    else
                    {
                        WriteByte(0x10e0a, 0);
                        DecodeLiteralOrMatch();
                    }
                }
                else
                {
                    if (ReadUInt16(0x10e0e) <= ReadUInt16(0x4383))
                    {
                        DecodeSymbol();
                    }
                    else
                    {
                        DecodeSymbol_Alt();
                    }
                }
            }
        }

        private void FlushOutput()
        {
            ushort lastFlushIndex = ReadUInt16(0x10e13);
            ushort currentIndex = (ushort)_outputIndex;

            if (currentIndex < lastFlushIndex)
            {
                int count = 0x10000 - lastFlushIndex;
                if (_outputBufferOffset + count > _outputBuffer.Length) count = _outputBuffer.Length - _outputBufferOffset;
                Array.Copy(_workBuffer, lastFlushIndex, _outputBuffer, _outputBufferOffset, count);
                _outputBufferOffset += count;
                lastFlushIndex = 0;
            }

            int count2 = currentIndex - lastFlushIndex;
            if (count2 > 0)
            {
                if (_outputBufferOffset + count2 > _outputBuffer.Length) count2 = _outputBuffer.Length - _outputBufferOffset;
                Array.Copy(_workBuffer, lastFlushIndex, _outputBuffer, _outputBufferOffset, count2);
                _outputBufferOffset += count2;
            }

            WriteUInt16(0x10e13, currentIndex);
        }

        private void DecodeSymbol()
        {
            // LAB_000135c8
            uint uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
            int shift = (8 - _currentBitOffset) & 0x1f;
            uint uVar19 = (uVar10 >> shift) & 0xFFFF;

            byte cVar5 = ReadByte(0x10e0b);
            byte bVar17 = ReadByte(0x10e01);

            uint uVar16;

            if (bVar17 < 0x36)
            {
                uVar16 = (uVar19 >> 4);
                if (bVar17 < 0xe)
                {
                    bVar17 = _decompressionTables[0x2500 + uVar16];
                    uVar16 = bVar17;
                    if (cVar5 != 0 && (uVar19 & 0xf000) != 0 && bVar17 == 0) uVar16 = 0x100;

                    uVar19 = (uint)(_decompressionTables[0x5440 + uVar16] + _currentBitOffset);
                }
                else
                {
                    bVar17 = _decompressionTables[0x3500 + uVar16];
                    uVar16 = bVar17;
                    if (cVar5 != 0 && (uVar19 & 0xf000) != 0 && bVar17 == 0) uVar16 = 0x100;

                    uVar19 = (uint)(_decompressionTables[0x5541 + uVar16] + _currentBitOffset);
                }
            }
            else
            {
                uVar16 = (uVar19 >> 6);
                if (bVar17 < 0x76)
                {
                    if (bVar17 < 0x5e)
                    {
                        bVar17 = _decompressionTables[0x4500 + uVar16];
                        uVar16 = bVar17;
                        if (cVar5 != 0 && (uVar19 & 0xf000) != 0 && bVar17 == 0) uVar16 = 0x100;

                        uVar19 = (uint)(_decompressionTables[0x5642 + uVar16] + _currentBitOffset);
                    }
                    else
                    {
                        bVar17 = _decompressionTables[0x4900 + uVar16];
                        uVar16 = bVar17;
                        if (cVar5 != 0 && (uVar19 & 0xf000) != 0 && bVar17 == 0) uVar16 = 0x100;

                        uVar19 = (uint)(_decompressionTables[0x5743 + uVar16] + _currentBitOffset);
                    }
                }
                else
                {
                    bVar17 = _decompressionTables[0x4d00 + uVar16];
                    uVar16 = bVar17;
                    if (cVar5 != 0 && (uVar19 & 0xf000) != 0 && bVar17 == 0) uVar16 = 0x100;

                    uVar19 = (uint)(_decompressionTables[0x5844 + uVar16] + _currentBitOffset);
                }
            }

            _currentByteOffset += (int)(uVar19 >> 3);
            _currentBitOffset = (int)(uVar19 & 7);

            if (cVar5 == 0)
            {
                byte val = ReadByte(0x10e0a);
                val++;
                WriteByte(0x10e0a, val);
                if (val > 0x10 && _bitsRemaining == 0)
                {
                    WriteByte(0x10e0b, 1);
                }
            }
            else
            {
                int iVar16 = (int)uVar16 - 1;
                if (iVar16 < 0)
                {
                    // Complex logic for cVar5 != 0 and uVar16 == 0
                    uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
                    uVar19 = (uVar10 >> (8 - _currentBitOffset & 0x1f)); // Full 32 bits?
                                                                         // Decompilation: uVar19 = CONCAT... >> ...

                    _currentByteOffset += (_currentBitOffset + 1) >> 3;
                    _currentBitOffset = (_currentBitOffset + 1) & 7;

                    if (((uVar19 >> 8) & 0xFF) < 0x80)
                    {
                        int iVar20 = 3;
                        if ((uVar19 & 0x4000) != 0) iVar20 = 4;

                        bVar17 = _decompressionTables[0x4500 + ((uVar19 >> 4) & 0x3ff)];
                        uVar19 = (uint)(_decompressionTables[0x5642 + bVar17] + 1 + _currentBitOffset);

                        _currentByteOffset += (int)(uVar19 >> 3);
                        uVar19 &= 7;

                        uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
                        _currentBitOffset = (int)uVar19;

                        int bVar13 = 8 - _currentBitOffset;
                        uVar19 += 5;
                        _currentByteOffset += (int)(uVar19 >> 3);
                        _currentBitOffset = (int)(uVar19 & 7);

                        _bytesRemainingToDecompress -= iVar20;

                        // Copy loop
                        do
                        {
                            ushort srcOffset = (ushort)((ushort)_outputIndex - ((ushort)(bVar17 << 5) | (ushort)((uVar10 >> bVar13) & 0xFFFF) >> 0xb));
                            WriteByte(_outputIndex, ReadByte(srcOffset));
                            _outputIndex = (ushort)(_outputIndex + 1);
                            iVar20--;
                        } while (iVar20 != 0);
                    }
                    else
                    {
                        WriteUInt16(0x10e0a, 0);
                    }

                    // goto LAB_000134dc (Main Loop)
                    return;
                }
                uVar16 = (uint)iVar16;
            }

            // Common path
            int iVar15 = (int)uVar16 + ReadInt16(0x4380);
            WriteInt16(0x4380, (short)iVar15);

            int iVar20_2 = ReadUInt16(0x4383) + 0x10;
            WriteUInt16(0x4383, (ushort)iVar20_2);
            if ((iVar20_2 >> 8) != 0)
            {
                WriteUInt16(0x4383, (ushort)((iVar20_2 >> 1) | 0x8000)); // Decompilation: CONCAT22(..., 0x90)? No, >> 1.
                // Wait, decompilation:
                // if ((char)((uint)iVar20 >> 8) != '\0') {
                //   puVar8[0x4383] = CONCAT22((ushort)((uint)iVar20 >> 0x11),0x90);
                // }
                // This looks like setting high byte to 0x90?
                // Or shifting right by 1?
                // Let's assume it handles overflow.
                WriteByte(0x4384, 0x90); // High byte of 0x4383? No, 0x4383 is short.
                // 0x4383 is at offset. 0x4384 is next byte.
                // If 0x4383 is short, 0x4384 is high byte.
                // So it sets high byte to 0x90.
            }

            byte valToOutput = ReadByte(0x10001 + (int)uVar16 * 2);
            if (_outputIndex < 32) Console.WriteLine($"Output[{_outputIndex}] = {valToOutput:X2} (uVar16={uVar16})");
            WriteByte(_outputIndex, valToOutput);
            _outputIndex = (ushort)(_outputIndex + 1);
            _bytesRemainingToDecompress--;

            while (true)
            {
                bVar17 = ReadByte(0x10000 + (int)uVar16 * 2);
                int countOffset = 0x10300 + bVar17;
                byte count = ReadByte(countOffset);
                count++;
                WriteByte(countOffset, count);

                if (bVar17 < 0xa1) break;
                Decompress_Reset_Table(0x10300);
            }

            byte prevCount = (byte)(ReadByte(0x10300 + bVar17) - 1);
            byte uVar6 = ReadByte(0x10001 + (int)uVar16 * 2);

            WriteUInt16(0x10000 + (int)uVar16 * 2, ReadUInt16(0x10000 + prevCount * 2));
            WriteUInt16(0x10000 + prevCount * 2, (ushort)((uVar6 << 8) | bVar17 + 1)); // +1 to the whole short? Or just low byte?
            // Decompilation: CONCAT11(uVar6,bVar17) + 1;
            // So (uVar6 << 8 | bVar17) + 1.
        }

        private void DecodeSymbol_Alt()
        {
            // LAB_00013951
            WriteByte(0x10e0a, 0);
            int iVar20 = ReadUInt16(0x10e0e) + 0x10;
            if ((iVar20 >> 8) != 0)
            {
                WriteByte(0x10e0f, 0x90); // Set high byte
                ushort val1 = ReadUInt16(0x4383);
                WriteUInt16(0x4383, (ushort)(val1 >> 1));
            }
            WriteUInt16(0x10e0e, (ushort)iVar20);

            uint uVar19 = ReadUInt16(0x10e06);
            uint uVar16, uVar11;
            uint uVar10 = 0;

            if (uVar19 < 0x7a)
            {
                if (uVar19 < 0x40)
                {
                    uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
                    uVar16 = (uVar10 >> (8 - _currentBitOffset & 0x1f)) & 0xFFFF;

                    if (uVar16 < 0x100)
                    {
                        _currentByteOffset += (_currentBitOffset + 0x10) >> 3;
                        _currentBitOffset = (_currentBitOffset + 0x10) & 7;
                    }
                    else
                    {
                        uVar16 = _decompressionTables[0x400 + (uVar16 >> 8)];
                        uVar11 = (uint)(_decompressionTables[0x5140 + uVar16] + _currentBitOffset);
                        _currentByteOffset += (int)(uVar11 >> 3);
                        _currentBitOffset = (int)(uVar11 & 7);
                    }
                }
                else
                {
                    uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
                    uVar16 = _decompressionTables[0x500 + ((uVar10 >> (8 - _currentBitOffset & 0x1f)) >> 4) & 0xFF];
                    uVar11 = (uint)(_decompressionTables[0x5240 + uVar16] + _currentBitOffset);
                    _currentByteOffset += (int)(uVar11 >> 3);
                    _currentBitOffset = (int)(uVar11 & 7);
                }
            }
            else
            {
                uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
                uVar16 = _decompressionTables[0x1500 + ((uVar10 >> (8 - _currentBitOffset & 0x1f)) >> 4) & 0xFF];
                uVar11 = (uint)(_decompressionTables[0x5340 + uVar16] + _currentBitOffset);
                _currentByteOffset += (int)(uVar11 >> 3);
                _currentBitOffset = (int)(uVar11 & 7);
            }

            WriteInt16(0x10e06, (short)((uVar19 + uVar16) - ((uVar19 + uVar16) >> 5)));

            uVar11 = ReadUInt16(0x10e02);
            uint uVar21, uVar12;

            if (uVar11 < 0x2900)
            {
                if (uVar11 < 0x700)
                {
                    uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
                    uVar21 = _decompressionTables[0x2500 + ((uVar10 >> (8 - _currentBitOffset & 0x1f)) >> 4) & 0xFF];
                    uVar12 = (uint)(_decompressionTables[0x5440 + uVar21] + _currentBitOffset);
                }
                else
                {
                    uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
                    uVar21 = _decompressionTables[0x3500 + ((uVar10 >> (8 - _currentBitOffset & 0x1f)) >> 4) & 0xFF];
                    uVar12 = (uint)(_decompressionTables[0x5541 + uVar21] + _currentBitOffset);
                }
            }
            else
            {
                uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
                uVar21 = _decompressionTables[0x4500 + ((uVar10 >> (8 - _currentBitOffset & 0x1f)) >> 6) & 0xFF];
                uVar12 = (uint)(_decompressionTables[0x5642 + uVar21] + _currentBitOffset);
            }

            _currentByteOffset += (int)(uVar12 >> 3);
            _currentBitOffset = (int)(uVar12 & 7);

            WriteInt16(0x10e02, (short)((uVar11 + uVar21) - ((uVar11 + uVar21) >> 8)));

            byte cVar5;
            while (true)
            {
                cVar5 = ReadByte(0x10600 + (int)uVar21 * 2);
                int idx = (int)((uVar10 >> 8) & 0xFFFFFF00) | cVar5;

                int countOffset = 0x10900 + cVar5;
                byte count = ReadByte(countOffset);
                count++;
                WriteByte(countOffset, count);

                if (cVar5 != 0xFF) break;
                Decompress_Reset_Table(0x10600);
            }

            byte prevCount = (byte)(ReadByte(0x10900 + cVar5) - 1);
            byte val = ReadByte(0x10601 + (int)uVar21 * 2);

            WriteUInt16(0x10600 + (int)uVar21 * 2, ReadUInt16(0x10600 + prevCount * 2));
            WriteUInt16(0x10600 + prevCount * 2, (ushort)((val << 8) | cVar5 + 1));

            uint uVar10_2 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
            uint bits = (uVar10_2 >> (8 - _currentBitOffset & 0x1f)) & 0xFFFF;
            uint uVar7 = ((uint)val << 8 | (bits >> 8)) >> 1;

            _currentByteOffset += (_currentBitOffset + 7) >> 3;
            _currentBitOffset = (_currentBitOffset + 7) & 7;

            ushort uVar9 = ReadUInt16(0x4382);
            if (uVar16 != 1 && uVar16 != 4)
            {
                if (uVar16 == 0 && uVar7 <= ReadUInt16(0x4384))
                {
                    uVar9 = (ushort)((short)(uVar9 + 1) - (short)((uVar9 + 1) >> 8));
                }
                else if (uVar9 != 0)
                {
                    uVar9--;
                }
            }
            WriteUInt16(0x4382, uVar9);

            int iVar20_3 = (int)uVar16 + 3;
            if (ReadUInt16(0x4384) <= uVar7)
            {
                iVar20_3 = (int)uVar16 + 4;
            }
            if (uVar7 < 0x101)
            {
                iVar20_3 += 8;
            }

            bool doWrite = false;
            if (uVar9 > 0xb0)
            {
                doWrite = true;
            }
            else
            {
                WriteUInt16(0x4384, 0x2001);
                if (ReadUInt16(0x4380) > 0x29ff && uVar19 < 0x40)
                {
                    doWrite = true;
                }
            }

            if (doWrite)
            {
                WriteUInt16(0x4384, 0x7f00);
            }

            ushort uVar9_2 = ReadUInt16(0x10e25);
            WriteUInt16(0x10e1d + uVar9_2 * 2, (ushort)uVar7);
            WriteUInt16(0x10e25, (ushort)((uVar9_2 + 1) & 3));
            WriteInt16(0x10e1b, (short)iVar20_3);
            WriteUInt16(0x10e19, (ushort)uVar7);

            _bytesRemainingToDecompress -= iVar20_3;

            do
            {
                ushort srcOffset = (ushort)((ushort)_outputIndex - (ushort)uVar7);
                WriteByte(_outputIndex, ReadByte(srcOffset));
                _outputIndex = (ushort)(_outputIndex + 1);
                iVar20_3--;
            } while (iVar20_3 != 0);
        }

        private void DecodeLiteralOrMatch()
        {
            uint uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
            uint uVar19 = (uVar10 >> (8 - _currentBitOffset & 0x1f)) & 0xFFFF;

            if (_stateVar1 == 2)
            {
                _currentByteOffset += (_currentBitOffset + 1) >> 3;
                _currentBitOffset = (_currentBitOffset + 1) & 7;

                if (uVar19 < 0x8000)
                {
                    _stateVar1 = 0;
                    DecodeMatch(uVar19 << 1);
                    return;
                }

                ushort count = ReadUInt16(0x10e1b);
                ushort dist = ReadUInt16(0x10e19);
                _bytesRemainingToDecompress -= count;

                do
                {
                    ushort srcOffset = (ushort)((ushort)_outputIndex - dist);
                    WriteByte(_outputIndex, ReadByte(srcOffset));
                    _outputIndex = (ushort)(_outputIndex + 1);
                    count--;
                } while (count != 0);
            }
            else
            {
                DecodeMatch(uVar19);
            }
        }

        private void DecodeMatch(uint uVar19)
        {
            uVar19 >>= 8;
            uint uVar16;

            if (ReadByte(0x10e12) == 0)
            {
                if (ReadUInt16(0x4381) < 0x25)
                {
                    uVar16 = _decompressionTables[uVar19];
                    uVar19 = (uint)(_decompressionTables[0x5100 + uVar16] + _currentBitOffset);
                }
                else
                {
                    uVar16 = _decompressionTables[0x100 + uVar19];
                    uVar19 = (uint)(_decompressionTables[0x5110 + uVar16] + _currentBitOffset);
                }
            }
            else if (ReadUInt16(0x4381) < 0x25)
            {
                uVar16 = _decompressionTables[0x200 + uVar19];
                uVar19 = (uint)(_decompressionTables[0x5120 + uVar16] + _currentBitOffset);
            }
            else
            {
                uVar16 = _decompressionTables[0x300 + uVar19];
                uVar19 = (uint)(_decompressionTables[0x5130 + uVar16] + _currentBitOffset);
            }

            _currentByteOffset += (int)(uVar19 >> 3);
            _currentBitOffset = (int)(uVar19 & 7);

            if (uVar16 < 9)
            {
                _stateVar1 = 0;
                short val = ReadInt16(0x4381);
                WriteInt16(0x4381, (short)((val + uVar16) - (val + uVar16 >> 4)));

                uint uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
                uint uVar11_2 = _decompressionTables[0x4500 + ((uVar10 >> (8 - _currentBitOffset & 0x1f)) >> 6) & 0xFF];

                uVar19 = (uint)(_decompressionTables[0x5642 + uVar11_2] + _currentBitOffset);
                _currentByteOffset += (int)(uVar19 >> 3);
                _currentBitOffset = (int)(uVar19 & 7);

                byte bVar17 = ReadByte(0x10400 + (int)uVar11_2);
                if ((int)uVar11_2 > 0)
                {
                    int countOffset = 0x10500 + bVar17;
                    byte count = ReadByte(countOffset);
                    count--;
                    WriteByte(countOffset, count);

                    byte bVar13 = ReadByte(0x103ff + (int)uVar11_2);
                    countOffset = 0x10500 + bVar13;
                    count = ReadByte(countOffset);
                    count++;
                    WriteByte(countOffset, count);

                    WriteByte(0x10400 + (int)uVar11_2, bVar13);
                    WriteByte(0x103ff + (int)uVar11_2, bVar17);
                }

                int iVar20 = (int)uVar16 + 2;
                short sVar14 = (short)(bVar17 + 1);

                ushort uVar9 = ReadUInt16(0x10e25);
                WriteInt16(0x10e1d + uVar9 * 2, sVar14);
                WriteUInt16(0x10e25, (ushort)((uVar9 + 1) & 3));
                WriteInt16(0x10e1b, (short)iVar20);
                WriteInt16(0x10e19, sVar14);

                _bytesRemainingToDecompress -= iVar20;

                do
                {
                    ushort srcOffset = (ushort)((ushort)_outputIndex - (ushort)sVar14);
                    WriteByte(_outputIndex, ReadByte(srcOffset));
                    _outputIndex = (ushort)(_outputIndex + 1);
                    iVar20--;
                } while (iVar20 != 0);
            }
            else if (uVar16 == 9)
            {
                _stateVar1++;
                int iVar20 = ReadUInt16(0x10e1b);

                ushort uVar10 = ReadUInt16(0x10e19);
                _bytesRemainingToDecompress -= iVar20;

                do
                {
                    ushort srcOffset = (ushort)((ushort)_outputIndex - uVar10);
                    WriteByte(_outputIndex, ReadByte(srcOffset));
                    _outputIndex = (ushort)(_outputIndex + 1);
                    iVar20--;
                } while (iVar20 != 0);
            }
            else if (uVar16 == 0xe)
            {
                _stateVar1 = 0;
                uint uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
                uVar16 = _decompressionTables[0x1500 + ((uVar10 >> (8 - _currentBitOffset & 0x1f)) >> 4) & 0xFF];

                uVar19 = (uint)(_decompressionTables[0x5340 + uVar16] + _currentBitOffset);
                _currentByteOffset += (int)(uVar19 >> 3);
                uVar19 &= 7;

                int iVar20 = (int)uVar16 + 5;
                uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
                _currentBitOffset = (int)uVar19;

                ushort uVar9 = (ushort)(((uVar10 >> (8 - _currentBitOffset & 0x1f)) >> 1) | 0x8000);

                uVar19 += 15;
                _currentByteOffset += (int)(uVar19 >> 3);
                _currentBitOffset = (int)(uVar19 & 7);

                WriteInt16(0x10e1b, (short)iVar20);
                WriteUInt16(0x10e19, uVar9);

                _bytesRemainingToDecompress -= iVar20;

                do
                {
                    ushort srcOffset = (ushort)((ushort)_outputIndex - uVar9);
                    WriteByte(_outputIndex, ReadByte(srcOffset));
                    _outputIndex = (ushort)(_outputIndex + 1);
                    iVar20--;
                } while (iVar20 != 0);
            }
            else
            {
                _stateVar1 = 0;
                uint uVar10 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);
                uint uVar11_2 = _decompressionTables[0x500 + ((uVar10 >> (8 - _currentBitOffset & 0x1f)) >> 4) & 0xFF];

                uVar19 = (uint)(_decompressionTables[0x5240 + uVar11_2] + _currentBitOffset);
                _currentByteOffset += (int)(uVar19 >> 3);
                _currentBitOffset = (int)(uVar19 & 7);

                int iVar20 = (int)uVar11_2 + 2;
                if (iVar20 == 0x101 && uVar16 == 10)
                {
                    WriteByte(0x10e12, (byte)(ReadByte(0x10e12) ^ 1));
                }
                else
                {
                    ushort idx = (ushort)((ReadUInt16(0x10e25) - (uVar16 - 9)) & 3);
                    ushort uVar9 = ReadUInt16(0x10e1d + idx * 2);

                    if (uVar9 > 0x100) iVar20 = (int)uVar11_2 + 3;
                    if (ReadUInt16(0x4384) < uVar9) iVar20++;

                    ushort uVar7 = ReadUInt16(0x10e25);
                    WriteUInt16(0x10e1d + uVar7 * 2, uVar9);
                    WriteUInt16(0x10e25, (ushort)((uVar7 + 1) & 3));
                    WriteInt16(0x10e1b, (short)iVar20);
                    WriteUInt16(0x10e19, uVar9);

                    _bytesRemainingToDecompress -= iVar20;

                    do
                    {
                        ushort srcOffset = (ushort)((ushort)_outputIndex - uVar9);
                        WriteByte(_outputIndex, ReadByte(srcOffset));
                        _outputIndex = (ushort)(_outputIndex + 1);
                        iVar20--;
                    } while (iVar20 != 0);
                }
            }
        } // +1 to whole short


        private void WriteByte(int offset, byte value) => _workBuffer[offset] = value;
        private byte ReadByte(int offset) => _workBuffer[offset];

        private void WriteUInt16(int offset, ushort value)
        {
            _workBuffer[offset] = (byte)value;
            _workBuffer[offset + 1] = (byte)(value >> 8);
        }

        private ushort ReadUInt16(int offset)
        {
            return (ushort)(_workBuffer[offset] | (_workBuffer[offset + 1] << 8));
        }

        private void WriteInt16(int offset, short value) => WriteUInt16(offset, (ushort)value);
        private short ReadInt16(int offset) => (short)ReadUInt16(offset);

        private void WriteUInt32(int offset, uint value)
        {
            _workBuffer[offset] = (byte)value;
            _workBuffer[offset + 1] = (byte)(value >> 8);
            _workBuffer[offset + 2] = (byte)(value >> 16);
            _workBuffer[offset + 3] = (byte)(value >> 24);
        }

        private uint ReadUInt32(int offset)
        {
            return (uint)(_workBuffer[offset] | (_workBuffer[offset + 1] << 8) |
                         (_workBuffer[offset + 2] << 16) | (_workBuffer[offset + 3] << 24));
        }

        private void Decompress_Update_State()
        {
            uint uVar3 = ReadUInt32(COMPRESSED_DATA_BUFFER_OFFSET + _currentByteOffset);

            int shift = (8 - _currentBitOffset) & 0x1f;
            uint val = (uVar3 >> shift) & 0xFFFF;

            uint index = _decompressionTables[0x4500 + (val >> 6)];

            uint uVar4 = (uint)(_decompressionTables[0x5642 + index] + _currentBitOffset);

            _currentByteOffset += (int)(uVar4 >> 3);
            _currentBitOffset = (int)(uVar4 & 7);

            while (true)
            {
                byte bVar2 = ReadByte(0x10a00 + (int)index * 2);

                int countOffset = 0x10d00 + bVar2;
                byte count = ReadByte(countOffset);
                count++;
                WriteByte(countOffset, count);

                if (bVar2 != 0xff) break;

                Decompress_Reset_Table(0x10a00);
            }

            byte bVar2_final = ReadByte(0x10a00 + (int)index * 2);
            byte newCount = (byte)(ReadByte(0x10d00 + bVar2_final) - 1);

            byte highByte = ReadByte(0x10a00 + (int)index * 2 + 1);
            _bitBuffer = (int)((_bitBuffer & 0xFFFFFF00) | highByte);

            ushort valToCopy = ReadUInt16(0x10a00 + newCount * 2);
            WriteUInt16(0x10a00 + (int)index * 2, valToCopy);

            WriteUInt16(0x10a00 + newCount * 2, (ushort)(((highByte << 8) | bVar2_final) + 1));
        }

        private void Decompress_Reset_Table(int offset)
        {
            int currentOffset = offset;
            for (int c = 7; c >= 0; c--)
            {
                for (int i = 0; i < 0x40; i += 2)
                {
                    WriteByte(currentOffset + i, (byte)c);
                }
                currentOffset += 0x40;
            }

            int clearOffset = offset + 0x300;
            for (int i = 0; i < 0x100; i++)
            {
                WriteByte(clearOffset + i, 0);
            }

            int endOffset = offset + 0x400;
            WriteByte(endOffset - 0x100, 0xE0);
            WriteByte(endOffset - 0xFF, 0xC0);
            WriteByte(endOffset - 0xFE, 0xA0);
            WriteByte(endOffset - 0xFD, 0x80);
            WriteByte(endOffset - 0xFC, 0x60);
            WriteByte(endOffset - 0xFB, 0x40);
            WriteByte(endOffset - 0xFA, 0x20);
        }
    }
}
