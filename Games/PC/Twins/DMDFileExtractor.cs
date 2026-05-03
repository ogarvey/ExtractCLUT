using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Twins
{

    public class DMDFileExtractor
    {
        private const int HEADER_SIZE = 16;
        private const int PALETTE_SIZE = 768;
        private const int SKIPPED_BYTES = 0;
        private const int FRAME_HEADER_SIZE = 5;
        private const int WIDTH = 320;
        private const int HEIGHT = 200;

        // Virtual Screen Buffer (320x200)
        private byte[] _videoBuffer = new byte[WIDTH * HEIGHT];
        private int[] _yTable;

        public DMDFileExtractor()
        {
            _yTable = new int[HEIGHT];
            for (int i = 0; i < HEIGHT; i++)
            {
                _yTable[i] = i * WIDTH;
            }
        }

        public void Extract(string dmdFilePath, string outputDirectory)
        {
            if (!File.Exists(dmdFilePath))
                throw new FileNotFoundException($"DMD file not found: {dmdFilePath}");

            string fileName = Path.GetFileNameWithoutExtension(dmdFilePath);
            string targetDir = Path.Combine(outputDirectory, fileName);

            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            byte[] fileData = File.ReadAllBytes(dmdFilePath);
            using (var reader = new BinaryReader(new MemoryStream(fileData)))
            {
                // 1. Read Global Header
                byte[] header = reader.ReadBytes(HEADER_SIZE);
                ushort frameCount = BitConverter.ToUInt16(header, 0);

                Console.WriteLine($"Processing {fileName}.DMD");
                Console.WriteLine($"Frame Count: {frameCount}");

                // 2. Read Palette
                byte[] palette = reader.ReadBytes(PALETTE_SIZE);
                string palettePath = Path.Combine(targetDir, "palette.bin");
                File.WriteAllBytes(palettePath, palette);

                // 3. Skip Bytes
                reader.ReadBytes(SKIPPED_BYTES);

                // 4. Process Frames
                for (int i = 0; i < frameCount; i++)
                {
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                        break;

                    long frameStartPos = reader.BaseStream.Position;

                    // Read Frame Header
                    ushort chunkSize = reader.ReadUInt16();
                    byte delay = reader.ReadByte();
                    reader.ReadByte(); // Unused
                    byte opcode = reader.ReadByte();

                    Console.WriteLine($"Frame {i}: Offset {frameStartPos:X}, Size {chunkSize}, Opcode {opcode:X2}, Delay {delay}");

                    // Calculate data length (ChunkSize - HeaderSize)
                    // Note: ChunkSize includes the header size (5 bytes)
                    int dataLength = chunkSize - FRAME_HEADER_SIZE;
                    if (dataLength < 0)
                    {
                        Console.WriteLine($"Warning: Invalid chunk size {chunkSize} at frame {i}");
                        break;
                    }

                    byte[] frameData = reader.ReadBytes(dataLength);

                    // Process Opcode
                    switch (opcode)
                    {
                        case 0x10:
                            ProcessOpcode10(frameData);
                            break;
                        case 0x0C:
                            ProcessOpcode0C(frameData);
                            break;
                        case 0x0D:
                            ProcessOpcode0D(frameData);
                            break;
                        case 0x0F:
                            ProcessOpcode0F(frameData);
                            break;
                        default:
                            Console.WriteLine($"Unknown Opcode {opcode:X2} at frame {i}");
                            break;
                    }

                    // Save frame
                    string frameFileName = $"frame_{i:D3}.bin";
                    File.WriteAllBytes(Path.Combine(targetDir, frameFileName), _videoBuffer);
                }
            }
        }

        private void ProcessOpcode10(byte[] data)
        {
            // Raw Copy
            // If size is 64000, copy all.
            if (data.Length == 64000)
            {
                Array.Copy(data, _videoBuffer, 64000);
            }
            else
            {
                // Partial copy, assume starting at 0 for now as we lack offset info in data
                Array.Copy(data, 0, _videoBuffer, 0, Math.Min(data.Length, _videoBuffer.Length));
            }
        }

        private void ProcessOpcode0C(byte[] data)
        {
            // Planar RLE
            int ptr = 0;
            if (ptr + 5 > data.Length) return;

            ushort yIndex = BitConverter.ToUInt16(data, ptr); ptr += 2;
            ushort height = BitConverter.ToUInt16(data, ptr); ptr += 2;
            byte segmentsCount = data[ptr]; ptr++;

            int currentY = yIndex;
            if (currentY >= HEIGHT) return;

            int destOffset = _yTable[currentY];

            for (int h = 0; h < height; h++)
            {
                int lineDest = destOffset;

                for (int s = 0; s < segmentsCount; s++)
                {
                    if (ptr + 2 > data.Length) break;

                    byte skip = data[ptr];
                    byte cmd = data[ptr + 1];
                    ptr += 2;

                    lineDest += skip;

                    if (cmd < 0x80)
                    {
                        // Fill
                        if (ptr >= data.Length) break;
                        byte color = data[ptr]; ptr++;

                        int count = cmd;
                        for (int k = 0; k < count; k++)
                        {
                            if (lineDest + k < _videoBuffer.Length)
                                _videoBuffer[lineDest + k] = color;
                        }
                        lineDest += count;
                    }
                    else
                    {
                        // Copy
                        int count = cmd & 0x7f;
                        if (ptr + count > data.Length) break;

                        for (int k = 0; k < count; k++)
                        {
                            if (lineDest + k < _videoBuffer.Length)
                                _videoBuffer[lineDest + k] = data[ptr + k];
                        }
                        ptr += count;
                        lineDest += count;
                    }
                }

                // Next line
                if (h < height - 1)
                {
                    if (ptr >= data.Length) break;
                    segmentsCount = data[ptr]; ptr++;

                    currentY++;
                    if (currentY < HEIGHT)
                        destOffset = _yTable[currentY];
                    else
                        break;
                }
            }
        }

        private void ProcessOpcode0D(byte[] data)
        {
            // Fill screen with color
            if (data.Length > 0)
            {
                byte color = data[0];
                for (int i = 0; i < _videoBuffer.Length; i++)
                    _videoBuffer[i] = color;
            }
        }

        private void ProcessOpcode0F(byte[] data)
        {
            // Columnar RLE
            int ptr = 0;
            int destOffset = 0;
            int remainingHeight = HEIGHT;

            while (remainingHeight > 0 && ptr < data.Length)
            {
                // Check for 00 00 marker
                if (ptr + 1 < data.Length && data[ptr] == 0 && data[ptr + 1] == 0)
                {
                    ptr += 2;
                    if (ptr >= data.Length) break;

                    byte skipLines = data[ptr]; ptr++;

                    if (skipLines < HEIGHT)
                        destOffset += _yTable[skipLines];

                    remainingHeight -= skipLines;

                    if (remainingHeight <= 0) break;
                }

                if (ptr >= data.Length) break;

                byte b = data[ptr];
                if (b < 0xC0)
                {
                    // Single pixel
                    if (destOffset < _videoBuffer.Length)
                        _videoBuffer[destOffset] = b;

                    ptr++;
                    destOffset += WIDTH; // Move down
                    remainingHeight--;
                }
                else
                {
                    // Horizontal Run
                    ptr++; // Consume command byte
                    if (ptr >= data.Length) break;

                    byte color = data[ptr]; ptr++;
                    int count = (b & 0x3F);

                    // Draw horizontal run
                    for (int k = 0; k < count; k++)
                    {
                        if (destOffset + k < _videoBuffer.Length)
                            _videoBuffer[destOffset + k] = color;
                    }

                    destOffset += WIDTH; // Move down from START of run
                    remainingHeight--;
                }
            }
        }
    }

}
