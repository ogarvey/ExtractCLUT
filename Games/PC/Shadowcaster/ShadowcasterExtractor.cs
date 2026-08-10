using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.PC.Shadowcaster
{
    public class ShadowcasterExtractor
    {
        private const int ScreenWidth = 320;
        private const int ScreenHeight = 200;
        private const int ScreenSize = ScreenWidth * ScreenHeight;

        private readonly byte[][] _screenBuffers = new byte[2][];
        private readonly List<SixLabors.ImageSharp.Color>[] _paletteBuffers = new List<SixLabors.ImageSharp.Color>[256];
        private List<SixLabors.ImageSharp.Color> _activePalette = new List<SixLabors.ImageSharp.Color>(256);
        private List<SixLabors.ImageSharp.Color> _fadedPalette = new List<SixLabors.ImageSharp.Color>(256);
        private readonly List<byte> _activeDisplayBuffers = new List<byte>();
        private readonly byte[] _blendedScreen = new byte[ScreenSize];

        public ShadowcasterExtractor()
        {
            _screenBuffers[0] = new byte[ScreenSize];
            _screenBuffers[1] = new byte[ScreenSize];

            _activeDisplayBuffers.Add(0); // Default to buffer 0

            // Initialize default black palette
            for (int i = 0; i < 256; i++)
            {
                _activePalette.Add(SixLabors.ImageSharp.Color.Black);
                _fadedPalette.Add(SixLabors.ImageSharp.Color.Black);
            }
        }

        public static void ExtractAll(string samplesDir, string outputDir)
        {
            Directory.CreateDirectory(outputDir);

            foreach (var file in Directory.GetFiles(samplesDir, "*.DAT"))
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                string fileOutputDir = Path.Combine(outputDir, filename);
                Directory.CreateDirectory(fileOutputDir);

                Console.WriteLine($"Processing animation file: {Path.GetFileName(file)}");
                var extractor = new ShadowcasterExtractor();
                try
                {
                    extractor.ExtractFile(file, fileOutputDir);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error extracting {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        public void ExtractFile(string datFilePath, string outputDir)
        {
            _activeDisplayBuffers.Clear();
            _activeDisplayBuffers.Add(0);
            Array.Clear(_screenBuffers[0], 0, ScreenSize);
            Array.Clear(_screenBuffers[1], 0, ScreenSize);
            Array.Clear(_blendedScreen, 0, ScreenSize);

            using var fs = File.OpenRead(datFilePath);
            using var reader = new BinaryReader(fs);

            if (fs.Length < 64)
            {
                throw new Exception("File too small to contain header.");
            }

            // Read 64-byte header
            ushort magic = reader.ReadUInt16();
            if (magic != 0x0105)
            {
                throw new Exception($"Invalid magic number: 0x{magic:X4}. Expected 0x0105.");
            }

            ushort width = reader.ReadUInt16();
            ushort height = reader.ReadUInt16();
            ushort numPalettes = reader.ReadUInt16();
            
            // Skip remaining 56 bytes of header
            fs.Seek(56, SeekOrigin.Current);

            Console.WriteLine($"Header: Width={width}, Height={height}, NumPalettes={numPalettes}");

            int frameIndex = 0;

            while (fs.Position < fs.Length)
            {
                if (fs.Position + 6 > fs.Length)
                {
                    // Truncated chunk header
                    break;
                }

                ushort commandId = reader.ReadUInt16();
                uint dataLength = reader.ReadUInt32();

                if (fs.Position + dataLength > fs.Length)
                {
                    Console.WriteLine($"Warning: Chunk {commandId} payload length {dataLength} extends beyond EOF. Truncating.");
                    dataLength = (uint)(fs.Length - fs.Position);
                }

                byte[] payload = reader.ReadBytes((int)dataLength);

                ProcessChunk(commandId, payload, outputDir, ref frameIndex);
            }
        }

        private void ProcessChunk(ushort commandId, byte[] payload, string outputDir, ref int frameIndex)
        {
            switch (commandId)
            {
                case 1:
                    // Draw Frame (RLE)
                    {
                        if (payload.Length < 9) return;
                        byte targetBuffer = payload[0];
                        ushort x = BitConverter.ToUInt16(payload, 1);
                        ushort y = BitConverter.ToUInt16(payload, 3);
                        ushort w = BitConverter.ToUInt16(payload, 5);
                        ushort h = BitConverter.ToUInt16(payload, 7);

                        if (w < x || h < y) return;
                        ushort width = (ushort)(w - x);
                        ushort height = (ushort)(h - y);

                        byte[] rleData = new byte[payload.Length - 9];
                        Array.Copy(payload, 9, rleData, 0, rleData.Length);

                        DrawFrameRle(targetBuffer, x, y, width, height, rleData);
                        BlendScreen();
                        SaveFrame(outputDir, frameIndex++);
                    }
                    break;

                case 2:
                    // Draw Frame (LZSS compressed)
                    {
                        if (payload.Length < 5) return;
                        byte targetBuffer = payload[0];
                        byte[] compressedData = new byte[payload.Length - 5];
                        Array.Copy(payload, 5, compressedData, 0, compressedData.Length);

                        byte[] decompressed = DecompressLzss(compressedData);

                        if (decompressed.Length < 5) return;
                        byte decTargetBuffer = decompressed[0];
                        ushort y = BitConverter.ToUInt16(decompressed, 1);
                        ushort height = BitConverter.ToUInt16(decompressed, 3);

                        if (height == 0) return;

                        byte[] rleData = new byte[decompressed.Length - 5];
                        Array.Copy(decompressed, 5, rleData, 0, rleData.Length);

                        DrawFrameRleCommand2(decTargetBuffer, y, height, rleData);
                        BlendScreen();
                        SaveFrame(outputDir, frameIndex++);
                    }
                    break;

                case 3:
                    // Full Screen Frame (Raw or LZSS)
                    {
                        if (payload.Length < 1) return;
                        byte targetBuffer = payload[0];
                        int remainingLen = payload.Length - 1;

                        byte[] rawPixels;
                        if (remainingLen == ScreenSize)
                        {
                            rawPixels = new byte[ScreenSize];
                            Array.Copy(payload, 1, rawPixels, 0, ScreenSize);
                        }
                        else
                        {
                            if (remainingLen < 4) return;
                            byte[] compressedData = new byte[remainingLen - 4];
                            Array.Copy(payload, 5, compressedData, 0, compressedData.Length);
                            rawPixels = DecompressLzss(compressedData);
                        }

                        if (rawPixels.Length >= ScreenSize)
                        {
                            Array.Copy(rawPixels, 0, _screenBuffers[targetBuffer & 1], 0, ScreenSize);
                        }
                        BlendScreen();
                        SaveFrame(outputDir, frameIndex++);
                    }
                    break;

                case 4:
                    // Palette Update (Custom Runs)
                    {
                        if (payload.Length < 2) return;
                        byte startIdx = payload[0];
                        byte numRuns = payload[1];

                        int payloadOffset = 2;

                        // Ensure we have a list initialized at startIdx
                        if (_paletteBuffers[startIdx] == null)
                        {
                            _paletteBuffers[startIdx] = new List<SixLabors.ImageSharp.Color>(256);
                            for (int i = 0; i < 256; i++)
                            {
                                _paletteBuffers[startIdx].Add(SixLabors.ImageSharp.Color.Black);
                            }
                        }

                        var palette = _paletteBuffers[startIdx];

                        for (int r = 0; r < numRuns; r++)
                        {
                            if (payloadOffset + 2 > payload.Length) break;
                            byte runOffset = payload[payloadOffset++];
                            byte runLength = payload[payloadOffset++];

                            int destOffset = runOffset * 3;
                            int colorCount = (runLength == 0) ? 256 : runLength;

                            int neededBytes = colorCount * 3;
                            if (payloadOffset + neededBytes > payload.Length)
                            {
                                colorCount = (payload.Length - payloadOffset) / 3;
                            }

                            for (int c = 0; c < colorCount; c++)
                            {
                                if (payloadOffset + 3 > payload.Length) break;
                                byte vr = payload[payloadOffset++];
                                byte vg = payload[payloadOffset++];
                                byte vb = payload[payloadOffset++];

                                int paletteColorIndex = destOffset / 3;
                                if (paletteColorIndex < 256)
                                {
                                    palette[paletteColorIndex] = new Rgba32(vr, vg, vb);
                                }
                                destOffset += 3;
                            }
                        }
                    }
                    break;

                case 18:
                    // XMI Audio Payload
                    {
                        if (payload.Length > 0)
                        {
                            // Strip any trailing nulls from filename (if the payload contains it)
                            string stringContent = Encoding.ASCII.GetString(payload);
                            int nullIdx = stringContent.IndexOf('\0');
                            string xmiName = (nullIdx >= 0) ? stringContent.Substring(0, nullIdx) : stringContent;
                            
                            // Clean up file name
                            xmiName = xmiName.Trim();
                            if (string.IsNullOrEmpty(xmiName))
                            {
                                xmiName = "audio.xmi";
                            }

                            Console.WriteLine($"Audio reference: {xmiName}");
                            // Save string reference or write dummy file if needed
                            File.WriteAllText(Path.Combine(outputDir, "audio_ref.txt"), xmiName);
                        }
                    }
                    break;

                case 21:
                    // Display Buffers List
                    {
                        if (payload.Length < 1) return;
                        byte count = payload[0];
                        _activeDisplayBuffers.Clear();
                        for (int i = 0; i < count; i++)
                        {
                            if (i + 1 < payload.Length)
                            {
                                _activeDisplayBuffers.Add(payload[i + 1]);
                            }
                        }
                    }
                    break;

                case 22:
                    // Apply/Set Palette
                    {
                        if (payload.Length < 1) return;
                        byte paletteIndex = payload[0];
                        if (_paletteBuffers[paletteIndex] != null)
                        {
                            for (int i = 0; i < 256; i++)
                            {
                                _activePalette[i] = _paletteBuffers[paletteIndex][i];
                                _fadedPalette[i] = _paletteBuffers[paletteIndex][i]; // Sync faded
                            }
                        }
                    }
                    break;

                case 32:
                    // Fade Out / Clear Screen
                    {
                        // Clear faded palette to black
                        for (int i = 0; i < 256; i++)
                        {
                            _fadedPalette[i] = SixLabors.ImageSharp.Color.Black;
                        }
                        Array.Clear(_screenBuffers[0], 0, ScreenSize);
                        Array.Clear(_screenBuffers[1], 0, ScreenSize);
                    }
                    break;

                case 33:
                    // Step Palette Fade In
                    {
                        if (payload.Length < 1) return;
                        byte targetPaletteIndex = payload[0];
                        var targetPalette = _paletteBuffers[targetPaletteIndex];
                        if (targetPalette != null)
                        {
                            // Gradually shift _fadedPalette towards targetPalette
                            for (int i = 0; i < 256; i++)
                            {
                                Rgba32 fColor = (Rgba32)_fadedPalette[i];
                                Rgba32 tColor = (Rgba32)targetPalette[i];

                                byte r = ShiftComponent(fColor.R, tColor.R);
                                byte g = ShiftComponent(fColor.G, tColor.G);
                                byte b = ShiftComponent(fColor.B, tColor.B);

                                _fadedPalette[i] = new Rgba32(r, g, b);
                            }
                        }
                    }
                    break;

                case 35:
                    // Wait/Engine Param
                    break;

                default:
                    // Unknown or unhandled commands
                    break;
            }
        }

        private byte ShiftComponent(byte current, byte target)
        {
            if (current < target) return (byte)(current + 1);
            if (current > target) return (byte)(current - 1);
            return current;
        }

        private void DrawFrameRleCommand2(byte targetBufferIndex, ushort startY, ushort height, byte[] rleData)
        {
            byte[] destBuffer = _screenBuffers[targetBufferIndex & 1];
            int rleOffset = 0;

            for (int row = 0; row < height; row++)
            {
                int destY = startY + row;
                if (destY >= ScreenHeight) break;

                if (rleOffset >= rleData.Length) break;
                byte startXFlag = rleData[rleOffset++];

                int startX = 0;
                if ((startXFlag & 0x80) != 0)
                {
                    if (rleOffset >= rleData.Length) break;
                    startX = 256 + rleData[rleOffset++];
                }
                else
                {
                    startX = startXFlag;
                }

                if (rleOffset >= rleData.Length) break;
                byte runCount = rleData[rleOffset++];

                int currentX = startX;

                for (int r = 0; r < runCount; r++)
                {
                    if (rleOffset >= rleData.Length) break;
                    sbyte tag = (sbyte)rleData[rleOffset++];

                    if (tag > 0)
                    {
                        if (rleOffset >= rleData.Length) break;
                        byte val = rleData[rleOffset++];

                        for (int i = 0; i < tag; i++)
                        {
                            int targetX = currentX + i;
                            if (targetX < ScreenWidth)
                            {
                                destBuffer[destY * ScreenWidth + targetX] = val;
                            }
                        }
                        currentX += tag;
                    }
                    else if (tag < 0)
                    {
                        int copyCount = -tag;
                        for (int i = 0; i < copyCount; i++)
                        {
                            if (rleOffset >= rleData.Length) break;
                            byte val = rleData[rleOffset++];

                            int targetX = currentX + i;
                            if (targetX < ScreenWidth)
                            {
                                destBuffer[destY * ScreenWidth + targetX] = val;
                            }
                        }
                        currentX += copyCount;
                    }
                }
            }
        }

        private void DrawFrameRle(byte targetBufferIndex, ushort startX, ushort startY, ushort width, ushort height, byte[] rleData)
        {
            byte[] destBuffer = _screenBuffers[targetBufferIndex & 1];
            int rleOffset = 0;

            for (int row = 0; row < height; row++)
            {
                int currentX = startX;
                int destY = startY + row;

                if (destY >= ScreenHeight) break;

                while (currentX < startX + width)
                {
                    if (rleOffset >= rleData.Length) break;
                    sbyte tag = (sbyte)rleData[rleOffset++];

                    if (tag > 0)
                    {
                        // Run of identical pixels
                        if (rleOffset >= rleData.Length) break;
                        byte val = rleData[rleOffset++];

                        for (int i = 0; i < tag; i++)
                        {
                            int targetX = currentX + i;
                            if (targetX < ScreenWidth)
                            {
                                destBuffer[destY * ScreenWidth + targetX] = val;
                            }
                        }
                        currentX += tag;
                    }
                    else if (tag < 0)
                    {
                        // Raw copy run
                        int copyCount = -tag;
                        for (int i = 0; i < copyCount; i++)
                        {
                            if (rleOffset >= rleData.Length) break;
                            byte val = rleData[rleOffset++];

                            int targetX = currentX + i;
                            if (targetX < ScreenWidth)
                            {
                                destBuffer[destY * ScreenWidth + targetX] = val;
                            }
                        }
                        currentX += copyCount;
                    }
                    else
                    {
                        // tag == 0: Safety break to avoid infinite loop
                        break;
                    }
                }
            }
        }

        private byte[] DecompressLzss(byte[] compressed)
        {
            byte[] decompressed = new byte[64000];
            int readOffset = 0;
            int writeOffset = 0;

            byte[] ringBuffer = new byte[4096];
            // Initialize ring buffer with spaces (0x20)
            for (int i = 0; i < 4078; i++)
            {
                ringBuffer[i] = 0x20;
            }

            int ringIndex = 4078;
            ushort control = 0;

            while (true)
            {
                control >>= 1;
                if ((control & 0x100) == 0)
                {
                    if (readOffset >= compressed.Length) break;
                    control = (ushort)(0xFF00 | compressed[readOffset++]);
                }

                if ((control & 1) != 0)
                {
                    if (readOffset >= compressed.Length) break;
                    byte b = compressed[readOffset++];
                    if (writeOffset >= decompressed.Length)
                    {
                        Array.Resize(ref decompressed, decompressed.Length * 2);
                    }
                    decompressed[writeOffset++] = b;
                    ringBuffer[ringIndex] = b;
                    ringIndex = (ringIndex + 1) & 0xFFF;
                }
                else
                {
                    if (readOffset + 1 >= compressed.Length) break;
                    byte b1 = compressed[readOffset++];
                    byte b2 = compressed[readOffset++];

                    int matchOffset = b1 | ((b2 & 0xF0) << 4);
                    int matchLen = (b2 & 0x0F) + 3;

                    for (int i = 0; i < matchLen; i++)
                    {
                        byte b = ringBuffer[(matchOffset + i) & 0xFFF];
                        if (writeOffset >= decompressed.Length)
                        {
                            Array.Resize(ref decompressed, decompressed.Length * 2);
                        }
                        decompressed[writeOffset++] = b;
                        ringBuffer[ringIndex] = b;
                        ringIndex = (ringIndex + 1) & 0xFFF;
                    }
                }
            }

            if (writeOffset < decompressed.Length)
            {
                byte[] finalDecompressed = new byte[writeOffset];
                Array.Copy(decompressed, 0, finalDecompressed, 0, writeOffset);
                return finalDecompressed;
            }
            return decompressed;
        }

        private void BlendScreen()
        {
            if (_activeDisplayBuffers.Count == 0)
            {
                Array.Copy(_screenBuffers[0], 0, _blendedScreen, 0, ScreenSize);
                return;
            }

            // Copy the first buffer
            byte firstBufferIdx = _activeDisplayBuffers[0];
            Array.Copy(_screenBuffers[firstBufferIdx & 1], 0, _blendedScreen, 0, ScreenSize);

            // Layer the subsequent buffers transparently (0 is transparency key)
            for (int i = 1; i < _activeDisplayBuffers.Count; i++)
            {
                byte bufferIdx = _activeDisplayBuffers[i];
                byte[] src = _screenBuffers[bufferIdx & 1];
                for (int p = 0; p < ScreenSize; p++)
                {
                    byte pixel = src[p];
                    if (pixel != 0)
                    {
                        _blendedScreen[p] = pixel;
                    }
                }
            }
        }

        private void SaveFrame(string outputDir, int index)
        {
            using var image = new Image<Rgba32>(ScreenWidth, ScreenHeight);

            // Use faded palette if it's currently being used, otherwise active palette
            List<SixLabors.ImageSharp.Color> palette = _fadedPalette;

            for (int y = 0; y < ScreenHeight; y++)
            {
                for (int x = 0; x < ScreenWidth; x++)
                {
                    byte colorIdx = _blendedScreen[y * ScreenWidth + x];
                    Rgba32 c = (Rgba32)palette[colorIdx];
                    byte r8 = (byte)(c.R * 255 / 63);
                    byte g8 = (byte)(c.G * 255 / 63);
                    byte b8 = (byte)(c.B * 255 / 63);
                    image[x, y] = new Rgba32(r8, g8, b8);
                }
            }

            string filename = Path.Combine(outputDir, $"frame_{index:D4}.png");
            image.Save(filename, new PngEncoder());
        }
    }
}
