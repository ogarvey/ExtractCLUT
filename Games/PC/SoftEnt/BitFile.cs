using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.SoftEnt
{
    public enum BitContentKind
    {
        Unknown,
        Binary,
        Graphics,
        Palette,
        Audio,
        Program,
        Text,
        NestedBitFile
    }

    public class BitFile
    {
        private const int HeaderSize = 0x15;
        private const int IndexEntrySize = 0x0e;
        private const int ModuleHeaderMinimumSize = 0x0b;
        private const int ModulePayloadOffset = 0x2f;
        private const int StoredLengthAdjustment = 0x2b;
        private const int UnpackedLengthAdjustment = 0x24;
        private const int PalettePayloadAdjustment = 7;
        private const int HistoryWindowSize = 0x10000;
        private const int WordHistoryWindowSize = 0x10000;

        public List<BitEntry> Entries { get; } = new List<BitEntry>();
        public string ContainerName { get; private set; } = string.Empty;
        public byte Version { get; private set; }
        public uint PrimaryTableUnits { get; private set; }
        public uint IndexEntryCount { get; private set; }
        public uint IndexCapacity { get; private set; }
        public uint SlotSize { get; private set; }
        public uint AuxiliaryBitCount { get; private set; }
        public long IndexOffset { get; private set; }
        public long DataOffset { get; private set; }

        public static BitFile Load(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            using var stream = File.OpenRead(path);
            return Parse(stream, Path.GetFileName(path));
        }

        public void ExtractToDirectory(string outputDirectory, bool overwrite = true)
        {
            if (outputDirectory == null)
            {
                throw new ArgumentNullException(nameof(outputDirectory));
            }

            Directory.CreateDirectory(outputDirectory);

            foreach (var entry in Entries)
            {
                var outputPath = Path.Combine(outputDirectory, entry.FileName);
                if (!overwrite && File.Exists(outputPath))
                {
                    throw new IOException($"The output file already exists: {outputPath}");
                }

                File.WriteAllBytes(outputPath, entry.Data);
            }
        }

        public int ExtractGraphicsPngs(string outputDirectory, IReadOnlyList<Rgba32>? palette = null)
        {
            if (outputDirectory == null)
            {
                throw new ArgumentNullException(nameof(outputDirectory));
            }

            Directory.CreateDirectory(outputDirectory);
            var written = 0;

            foreach (var entry in Entries)
            {
                if (entry.ContentKind != BitContentKind.Graphics)
                {
                    entry.ImageError = "The entry is not classified as graphics.";
                    continue;
                }

                if (!TryRenderByteBitmap(entry, palette, out var image, out var error))
                {
                    entry.ImageError = error ?? entry.BitmapDataError ?? "The BIT2 resource requires lock-time bitmap object reconstruction.";
                    continue;
                }

                using (image)
                {
                    var fileName = Path.ChangeExtension(entry.FileName, ".png");
                    image.SaveAsPng(Path.Combine(outputDirectory, fileName));
                }

                entry.ImageError = null;
                entry.RenderedAsPng = true;
                written++;
            }

            return written;
        }

        public static List<Rgba32> CreateRgbPalette(BitFile paletteFile)
        {
            if (paletteFile == null)
            {
                throw new ArgumentNullException(nameof(paletteFile));
            }

            var palette = new List<Rgba32>(paletteFile.Entries.Count > 0 ? paletteFile.Entries[0].Data.Length / 3 : 0);
            if (paletteFile.Entries.Count == 0)
            {
                return palette;
            }

            var data = paletteFile.Entries[0].Data;
            for (var offset = 0; offset + 2 < data.Length; offset += 3)
            {
                palette.Add(new Rgba32(data[offset], data[offset + 1], data[offset + 2], 255));
            }

            return palette;
        }

        private static bool TryRenderByteBitmap(BitEntry entry, IReadOnlyList<Rgba32>? palette, out Image<Rgba32> image, out string? error)
        {
            image = null!;
            error = null;

            if (entry.BitmapData.Length <= 7 || (entry.ModuleFlags & 0x0400) == 0)
            {
                return false;
            }

            var width = BinaryPrimitives.ReadUInt16LittleEndian(entry.BitmapData.AsSpan(0, 2));
            var height = BinaryPrimitives.ReadUInt16LittleEndian(entry.BitmapData.AsSpan(2, 2));
            if (width == 0 || height == 0 || width > 2048 || height > 2048)
            {
                error = $"Invalid bitmap dimensions {width}x{height}.";
                return false;
            }

            var pixels = new byte[checked(width * height)];
            var sourceOffset = 4;
            for (var y = 0; y < height; y++)
            {
                var x = 0;
                while (true)
                {
                    if (sourceOffset >= entry.BitmapData.Length)
                    {
                        error = $"The bitmap row stream ended at row {y}.";
                        return false;
                    }

                    var control = entry.BitmapData[sourceOffset++];
                    if (control == 0)
                    {
                        break;
                    }

                    if ((control & 0x80) != 0)
                    {
                        x += control & 0x7f;
                    }
                    else
                    {
                        var literalLength = control;
                        if (x + literalLength > width || sourceOffset + literalLength > entry.BitmapData.Length)
                        {
                            error = $"The bitmap row stream exceeded row {y} width {width}.";
                            return false;
                        }

                        Buffer.BlockCopy(entry.BitmapData, sourceOffset, pixels, y * width + x, literalLength);
                        sourceOffset += literalLength;
                        x += literalLength;
                    }

                    if (x > width)
                    {
                        error = $"The bitmap row stream exceeded row {y} width {width}.";
                        return false;
                    }
                }
            }

            if (sourceOffset != entry.BitmapData.Length)
            {
                error = "The decoded BIT2 resource contains structured data after the row stream.";
                return false;
            }

            image = new Image<Rgba32>(width, height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = pixels[y * width + x];
                    image[x, y] = index == 0
                        ? new Rgba32(0, 0, 0, 0)
                        : ResolvePaletteColor(index, palette);
                }
            }

            return true;
        }

        private static Rgba32 ResolvePaletteColor(byte index, IReadOnlyList<Rgba32>? palette)
        {
            if (palette != null && index < palette.Count)
            {
                return palette[index];
            }

            return new Rgba32(index, index, index, 255);
        }

        private static BitFile Parse(Stream stream, string containerName)
        {
            if (!stream.CanSeek)
            {
                throw new ArgumentException("The BIT stream must support seeking.", nameof(stream));
            }

            var bitFile = new BitFile
            {
                ContainerName = containerName,
                IndexOffset = HeaderSize
            };

            stream.Position = 0;
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            var magic = ReadExactly(reader, 4);
            if (magic[0] != (byte)'B' || magic[1] != (byte)'I' || magic[2] != (byte)'T' || magic[3] != 0)
            {
                throw new InvalidDataException($"'{containerName}' is not a Soft Enterprises BIT file.");
            }

            bitFile.Version = reader.ReadByte();
            bitFile.PrimaryTableUnits = reader.ReadUInt32();
            bitFile.IndexEntryCount = reader.ReadUInt32();
            bitFile.SlotSize = reader.ReadUInt32();
            bitFile.AuxiliaryBitCount = reader.ReadUInt32();
            bitFile.IndexCapacity = bitFile.PrimaryTableUnits == 0
                ? bitFile.IndexEntryCount
                : bitFile.PrimaryTableUnits;

            if (bitFile.IndexCapacity > int.MaxValue)
            {
                throw new InvalidDataException($"The BIT index contains too many entries: {bitFile.IndexCapacity}.");
            }

            var entryCount = (int)bitFile.IndexCapacity;
            var indexLength = checked((long)entryCount * IndexEntrySize);
            EnsureRange(stream, bitFile.IndexOffset, indexLength, "the BIT index");

            var auxiliaryByteCount = checked(((ulong)bitFile.AuxiliaryBitCount + 7UL) / 8UL);
            var dataOffsetAdjustment = bitFile.PrimaryTableUnits == 0
                ? checked((ulong)entryCount * IndexEntrySize)
                : checked((ulong)bitFile.PrimaryTableUnits * IndexEntrySize + auxiliaryByteCount);
            bitFile.DataOffset = checked(bitFile.IndexOffset + (long)dataOffsetAdjustment);
            EnsureRange(stream, bitFile.DataOffset, 0, "the BIT data area");

            stream.Position = bitFile.IndexOffset;
            var indexRecords = new List<IndexRecord>();
            var recordIndexes = new Dictionary<uint, int>();
            for (var index = 0; index < entryCount; index++)
            {
                var moduleId = reader.ReadUInt32();
                var dataIndex = reader.ReadUInt32();
                var metadata = ReadExactly(reader, 6);
                if (IsEmptyIndexEntry(moduleId, dataIndex, metadata))
                {
                    break;
                }

                var record = new IndexRecord(index, moduleId, dataIndex, metadata);
                if (recordIndexes.TryGetValue(moduleId, out var existingRecordIndex))
                {
                    if (record.Priority >= indexRecords[existingRecordIndex].Priority)
                    {
                        indexRecords[existingRecordIndex] = record;
                    }
                }
                else
                {
                    recordIndexes.Add(moduleId, indexRecords.Count);
                    indexRecords.Add(record);
                }
            }

            foreach (var record in indexRecords)
            {
                var moduleOffset = checked(bitFile.DataOffset + checked((long)((ulong)record.DataIndex * bitFile.SlotSize)));

                bitFile.Entries.Add(ReadEntry(
                    reader,
                    stream,
                    bitFile.ContainerName,
                    record.Index,
                    record.ModuleId,
                    record.DataIndex,
                    record.Metadata,
                    moduleOffset));
            }

            return bitFile;
        }

        private static BitEntry ReadEntry(
            BinaryReader reader,
            Stream stream,
            string containerName,
            int index,
            uint moduleId,
            uint dataIndex,
            byte[] metadata,
            long moduleOffset)
        {
            EnsureRange(stream, moduleOffset, ModuleHeaderMinimumSize, "a BIT module header");
            stream.Position = moduleOffset;

            var moduleHeader = ReadExactly(reader, ModuleHeaderMinimumSize);
            var storedLength = BinaryPrimitives.ReadUInt32LittleEndian(moduleHeader.AsSpan(0, 4));
            var unpackedLength = BinaryPrimitives.ReadUInt32LittleEndian(moduleHeader.AsSpan(4, 4));
            var compressionType = moduleHeader[8];
            var moduleFlags = BinaryPrimitives.ReadUInt16LittleEndian(moduleHeader.AsSpan(9, 2));
            var headerValue0B = (ushort)0;
            var headerValue0D = (ushort)0;
            var headerFormat = (byte)0;
            var contentTypeCode = metadata[4];
            var contentSubtypeCode = metadata[5];
            var contentKind = DetectContentKind(containerName, contentTypeCode, Array.Empty<byte>());

            if (storedLength < ModulePayloadOffset && unpackedLength < ModulePayloadOffset)
            {
                if (stream.Length - moduleOffset > moduleHeader.Length)
                {
                    var availableLength = checked((int)Math.Min(ModulePayloadOffset, stream.Length - moduleOffset));
                    stream.Position = moduleOffset;
                    moduleHeader = ReadExactly(reader, availableLength);
                }

                if (moduleHeader.Length >= 0x0f)
                {
                    headerValue0B = BinaryPrimitives.ReadUInt16LittleEndian(moduleHeader.AsSpan(0x0b, 2));
                    headerValue0D = BinaryPrimitives.ReadUInt16LittleEndian(moduleHeader.AsSpan(0x0d, 2));
                    headerFormat = moduleHeader[0x0f];
                }

                return new BitEntry
                {
                    ContainerName = containerName,
                    FileName = BuildFileName(index, moduleId, contentKind, false),
                    Data = Array.Empty<byte>(),
                    ModuleIndex = index,
                    ModuleId = moduleId,
                    DataIndex = dataIndex,
                    ModuleOffset = moduleOffset,
                    StoredLength = storedLength,
                    UnpackedLength = unpackedLength,
                    ModuleHeader = moduleHeader,
                    HeaderValue0B = headerValue0B,
                    HeaderValue0D = headerValue0D,
                    FormatCode = headerFormat,
                    CompressionType = compressionType,
                    ModuleFlags = moduleFlags,
                    Metadata = metadata,
                    ContentTypeCode = contentTypeCode,
                    ContentSubtypeCode = contentSubtypeCode,
                    ContentKind = contentKind,
                    IsDecompressed = false,
                    UsedZeroFilledHistory = false,
                    HistoryWindowLength = 0,
                    ExtractionError = "The index entry contains a header-only module with no standalone payload."
                };
            }

            EnsureRange(stream, moduleOffset, ModulePayloadOffset, "a BIT module header");
            stream.Position = moduleOffset;
            moduleHeader = ReadExactly(reader, ModulePayloadOffset);
            headerValue0B = BinaryPrimitives.ReadUInt16LittleEndian(moduleHeader.AsSpan(0x0b, 2));
            headerValue0D = BinaryPrimitives.ReadUInt16LittleEndian(moduleHeader.AsSpan(0x0d, 2));
            headerFormat = moduleHeader[0x0f];

            if (storedLength < StoredLengthAdjustment)
            {
                throw new InvalidDataException($"BIT module {index} has an invalid stored length: 0x{storedLength:X8}.");
            }

            if (unpackedLength < UnpackedLengthAdjustment)
            {
                throw new InvalidDataException($"BIT module {index} has an invalid unpacked length: 0x{unpackedLength:X8}.");
            }

            var compressedLength = checked((long)storedLength - StoredLengthAdjustment);
            var extractedLength = checked((long)unpackedLength - UnpackedLengthAdjustment);
            EnsureArrayLength(compressedLength, $"the compressed length for BIT module {index}");
            EnsureArrayLength(extractedLength, $"the unpacked length for BIT module {index}");

            var isPaletteResource = containerName.Contains("SYSPAL", StringComparison.OrdinalIgnoreCase);
            var payloadOffset = isPaletteResource
                ? checked(moduleOffset + ModuleHeaderMinimumSize)
                : checked(moduleOffset + ModulePayloadOffset);
            if (isPaletteResource)
            {
                compressedLength = checked((long)storedLength - PalettePayloadAdjustment);
                extractedLength = compressedLength;
            }

            EnsureRange(stream, payloadOffset, compressedLength, $"BIT module {index} payload");
            stream.Position = payloadOffset;
            var compressedData = ReadExactly(reader, checked((int)compressedLength));
            byte[] data;
            string? extractionError = null;
            var isDecompressed = isPaletteResource;
            try
            {
                data = isPaletteResource
                    ? compressedData
                    : ExtractData(compressedData, checked((int)extractedLength), compressionType, moduleId, index);
            }
            catch (InvalidDataException exception)
            {
                data = compressedData;
                extractionError = exception.Message;
                isDecompressed = false;
            }

            var decodedModuleData = data;

            var extractionWarning = isDecompressed && data.Length != extractedLength
                ? $"Decoded length is 0x{data.Length:X}; header declares 0x{extractedLength:X}."
                : null;
            contentKind = DetectContentKind(containerName, contentTypeCode, data);
            var bitmapData = Array.Empty<byte>();
            string? bitmapDataError = null;
            if (contentKind == BitContentKind.Graphics)
            {
                try
                {
                    bitmapData = DecodeBitmapPayload(moduleHeader, compressedData, storedLength, unpackedLength, compressionType, moduleFlags, index, moduleId);
                }
                catch (InvalidDataException exception)
                {
                    bitmapDataError = exception.Message;
                }

                if (bitmapData.Length > 0)
                {
                    data = bitmapData;
                    isDecompressed = true;
                    extractionError = null;
                }
            }

            return new BitEntry
            {
                ContainerName = containerName,
                FileName = BuildFileName(index, moduleId, contentKind, isDecompressed),
                Data = data,
                DecodedModuleData = decodedModuleData,
                CompressedData = compressedData,
                BitmapData = bitmapData,
                ModuleIndex = index,
                ModuleId = moduleId,
                DataIndex = dataIndex,
                ModuleOffset = moduleOffset,
                StoredLength = storedLength,
                UnpackedLength = unpackedLength,
                CompressedLength = checked((uint)compressedLength),
                ExtractedLength = checked((uint)extractedLength),
                DecodedLength = checked((uint)data.Length),
                ModuleHeader = moduleHeader,
                HeaderValue0B = headerValue0B,
                HeaderValue0D = headerValue0D,
                FormatCode = headerFormat,
                CompressionType = compressionType,
                ModuleFlags = moduleFlags,
                Metadata = metadata,
                ContentTypeCode = contentTypeCode,
                ContentSubtypeCode = contentSubtypeCode,
                ContentKind = contentKind,
                IsDecompressed = isDecompressed,
                UsedZeroFilledHistory = compressionType == 2 || compressionType == 5,
                HistoryWindowLength = compressionType == 2 || compressionType == 5 ? HistoryWindowSize : 0,
                ExtractionError = extractionError,
                ExtractionWarning = extractionWarning,
                BitmapDataError = bitmapDataError
            };
        }

        private static byte[] DecodeBitmapPayload(
            byte[] moduleHeader,
            byte[] compressedData,
            uint storedLength,
            uint unpackedLength,
            byte compressionType,
            ushort moduleFlags,
            int index,
            uint moduleId)
        {
            const int bitmapPrefixLength = 4;
            if (storedLength < bitmapPrefixLength + 7 || moduleHeader.Length < ModulePayloadOffset)
            {
                throw new InvalidDataException($"BIT module {index} (0x{moduleId:X8}) has no bitmap payload.");
            }

            var descriptor = new byte[bitmapPrefixLength];
            Buffer.BlockCopy(moduleHeader, ModuleHeaderMinimumSize, descriptor, 0, descriptor.Length);
            var sourceOffset = ModuleHeaderMinimumSize + bitmapPrefixLength;
            var sourceLength = checked((int)storedLength - bitmapPrefixLength - 7);
            var source = new byte[sourceLength];
            var embeddedSourceLength = Math.Min(moduleHeader.Length - sourceOffset, sourceLength);
            Buffer.BlockCopy(moduleHeader, sourceOffset, source, 0, embeddedSourceLength);
            var remainingLength = sourceLength - embeddedSourceLength;
            if (remainingLength > compressedData.Length)
            {
                throw new InvalidDataException($"BIT module {index} (0x{moduleId:X8}) has a truncated bitmap source.");
            }

            Buffer.BlockCopy(compressedData, 0, source, embeddedSourceLength, remainingLength);
            byte[] decodedSource;
            if (compressionType == 0)
            {
                decodedSource = source;
            }
            else
            {
                if (compressionType == 5)
                {
                    DecryptTypeFive(source);
                }

                var expectedLength = checked((int)Math.Max(0, (long)unpackedLength - bitmapPrefixLength));
                decodedSource = (moduleFlags & 0x0200) != 0
                    ? DecodeWordBackReferences(source, expectedLength, index, moduleId)
                    : DecodeBackReferences(source, expectedLength, index, moduleId);
            }

            var bitmapData = new byte[descriptor.Length + decodedSource.Length];
            Buffer.BlockCopy(descriptor, 0, bitmapData, 0, descriptor.Length);
            Buffer.BlockCopy(decodedSource, 0, bitmapData, descriptor.Length, decodedSource.Length);
            return bitmapData;
        }

        private static byte[] ExtractData(byte[] compressedData, int extractedLength, byte compressionType, uint moduleId, int index)
        {
            switch (compressionType)
            {
                case 0:
                    if (compressedData.Length < extractedLength)
                    {
                        throw new InvalidDataException($"BIT module {index} (0x{moduleId:X8}) is shorter than its unpacked length.");
                    }

                    var rawData = new byte[extractedLength];
                    Buffer.BlockCopy(compressedData, 0, rawData, 0, extractedLength);
                    return rawData;
                case 1:
                    return DecodeRunLength(compressedData, extractedLength, index, moduleId);
                case 2:
                    return DecodeBackReferences(compressedData, extractedLength, index, moduleId);
                case 5:
                    DecryptTypeFive(compressedData);
                    return DecodeBackReferences(compressedData, extractedLength, index, moduleId);
                default:
                    throw new InvalidDataException($"BIT module {index} (0x{moduleId:X8}) uses unsupported compression type {compressionType}.");
            }
        }

        private static byte[] DecodeRunLength(byte[] input, int outputLength, int index, uint moduleId)
        {
            var output = new byte[outputLength];
            var inputOffset = 0;
            var outputOffset = 0;

            while (inputOffset < input.Length)
            {
                var control = input[inputOffset++];
                if (control < 0x80)
                {
                    var count = control + 1;
                    EnsureInput(input, inputOffset, count, index, moduleId);
                    EnsureOutput(output, outputOffset, count, index, moduleId);
                    Buffer.BlockCopy(input, inputOffset, output, outputOffset, count);
                    inputOffset += count;
                    outputOffset += count;
                }
                else
                {
                    var count = control - 0x7d;
                    EnsureInput(input, inputOffset, 1, index, moduleId);
                    EnsureOutput(output, outputOffset, count, index, moduleId);
                    Array.Fill(output, input[inputOffset], outputOffset, count);
                    inputOffset++;
                    outputOffset += count;
                }
            }

            EnsureDecodedLength(outputOffset, output.Length, index, moduleId);
            return output;
        }

        private static byte[] DecodeBackReferences(byte[] input, int outputLength, int index, uint moduleId)
        {
            var output = new List<byte>(HistoryWindowSize + Math.Min(outputLength, 0x100000));
            for (var historyIndex = 0; historyIndex < HistoryWindowSize; historyIndex++)
            {
                output.Add(0);
            }

            var maximumDecodedLength = Math.Max((long)outputLength, (long)input.Length * 0x42L);
            var inputOffset = 0;

            while (inputOffset < input.Length)
            {
                var control = input[inputOffset++];
                if ((control & 0x80) == 0)
                {
                    var count = control + 1;
                    EnsureDecodedCapacity(output, HistoryWindowSize, maximumDecodedLength, count, index, moduleId);
                    for (var literalIndex = 0; literalIndex < count; literalIndex++)
                    {
                        output.Add(ReadPaddedByte(input, inputOffset + literalIndex));
                    }

                    inputOffset += count;
                }
                else if ((control & 0x40) == 0)
                {
                    var count = control - 0x7c;
                    var distance = ReadPaddedByte(input, inputOffset) | (ReadPaddedByte(input, inputOffset + 1) << 8);
                    if (distance > output.Count)
                    {
                        throw new InvalidDataException($"BIT module {index} (0x{moduleId:X8}) has an invalid back-reference distance: {distance}.");
                    }

                    EnsureDecodedCapacity(output, HistoryWindowSize, maximumDecodedLength, count, index, moduleId);
                    for (var copyIndex = 0; copyIndex < count; copyIndex++)
                    {
                        output.Add(distance == 0 ? (byte)0 : output[output.Count - distance]);
                    }

                    inputOffset += 2;
                }
                else
                {
                    var count = control - 0xbd;
                    EnsureDecodedCapacity(output, HistoryWindowSize, maximumDecodedLength, count, index, moduleId);
                    for (var repeatIndex = 0; repeatIndex < count; repeatIndex++)
                    {
                        output.Add(ReadPaddedByte(input, inputOffset));
                    }

                    inputOffset++;
                }
            }

            var decodedLength = output.Count - HistoryWindowSize;
            var decodedData = new byte[decodedLength];
            output.CopyTo(HistoryWindowSize, decodedData, 0, decodedLength);
            return decodedData;
        }

        private static byte[] DecodeWordBackReferences(byte[] input, int outputLength, int index, uint moduleId)
        {
            var historyWords = WordHistoryWindowSize;
            var outputWords = new List<ushort>(historyWords + Math.Min((outputLength + 1) / 2, 0x100000));
            for (var historyIndex = 0; historyIndex < historyWords; historyIndex++)
            {
                outputWords.Add(0);
            }

            var inputOffset = 0;
            var maximumOutputWords = Math.Max((long)(outputLength + 1) / 2, (long)input.Length * 0x42L);
            while (inputOffset < input.Length)
            {
                var control = input[inputOffset++];
                if (control < 0x80)
                {
                    var count = control + 1;
                    EnsureWordOutputCapacity(outputWords, historyWords, maximumOutputWords, count, index, moduleId);
                    for (var literalIndex = 0; literalIndex < count; literalIndex++)
                    {
                        outputWords.Add(ReadPaddedWord(input, inputOffset + literalIndex * 2));
                    }

                    inputOffset += count * 2;
                }
                else if ((control & 0x40) == 0)
                {
                    var count = control - 0x7c;
                    var distance = ReadPaddedWord(input, inputOffset);
                    if (distance > outputWords.Count)
                    {
                        throw new InvalidDataException($"BIT module {index} (0x{moduleId:X8}) has an invalid word back-reference distance: {distance}.");
                    }

                    EnsureWordOutputCapacity(outputWords, historyWords, maximumOutputWords, count, index, moduleId);
                    for (var copyIndex = 0; copyIndex < count; copyIndex++)
                    {
                        outputWords.Add(distance == 0 ? (ushort)0 : outputWords[outputWords.Count - distance]);
                    }

                    inputOffset += 2;
                }
                else
                {
                    var count = control - 0xbd;
                    var value = ReadPaddedWord(input, inputOffset);
                    EnsureWordOutputCapacity(outputWords, historyWords, maximumOutputWords, count, index, moduleId);
                    for (var repeatIndex = 0; repeatIndex < count; repeatIndex++)
                    {
                        outputWords.Add(value);
                    }

                    inputOffset += 2;
                }
            }

            var outputLengthInWords = outputWords.Count - historyWords;
            var decodedData = new byte[outputLengthInWords * 2];
            for (var wordIndex = 0; wordIndex < outputLengthInWords; wordIndex++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(decodedData.AsSpan(wordIndex * 2, 2), outputWords[historyWords + wordIndex]);
            }

            if (decodedData.Length > outputLength)
            {
                Array.Resize(ref decodedData, outputLength);
            }

            return decodedData;
        }

        private static ushort ReadPaddedWord(byte[] input, int offset)
        {
            return (ushort)(ReadPaddedByte(input, offset) | (ReadPaddedByte(input, offset + 1) << 8));
        }

        private static byte ReadPaddedByte(byte[] input, int offset)
        {
            return offset >= 0 && offset < input.Length ? input[offset] : (byte)0;
        }

        private static void DecryptTypeFive(byte[] data)
        {
            for (var index = 0; index < data.Length; index++)
            {
                var value = (byte)(data[index] ^ (byte)((index + 0x24) * 0x9a));
                data[index] = (byte)((value << 6) | (value >> 2));
            }
        }

        private static BitContentKind DetectContentKind(string containerName, byte typeCode, byte[] data)
        {
            if (HasMagic(data, "MZ"))
            {
                return BitContentKind.Program;
            }

            if (HasMagic(data, "BIT\0"))
            {
                return BitContentKind.NestedBitFile;
            }

            var containerStem = Path.GetFileNameWithoutExtension(containerName).ToUpperInvariant();
            if (containerStem.Contains("SYSPAL", StringComparison.Ordinal) || containerStem.Contains("PAL", StringComparison.Ordinal))
            {
                return BitContentKind.Palette;
            }

            if (containerStem.Contains("SOUND", StringComparison.Ordinal) || typeCode == 2)
            {
                return BitContentKind.Audio;
            }

            if (containerStem.Contains("GRAF", StringComparison.Ordinal) || containerStem.Contains("GRAPH", StringComparison.Ordinal) || typeCode == 0)
            {
                return BitContentKind.Graphics;
            }

            if (typeCode == 3 || containerStem.Contains("DRIVER", StringComparison.Ordinal) || containerStem.Contains("GAMELIB", StringComparison.Ordinal) || containerStem.Contains("MAIN", StringComparison.Ordinal))
            {
                return BitContentKind.Program;
            }

            if (LooksLikeText(data))
            {
                return BitContentKind.Text;
            }

            return typeCode == 1 ? BitContentKind.Unknown : BitContentKind.Binary;
        }

        private static string BuildFileName(int index, uint moduleId, BitContentKind contentKind, bool isDecompressed = true)
        {
            var state = isDecompressed ? string.Empty : "_compressed";
            return $"module_{index:D4}_{moduleId:X8}_{contentKind.ToString().ToLowerInvariant()}{state}.bin";
        }

        private static bool HasMagic(byte[] data, string value)
        {
            if (data.Length < value.Length)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (data[index] != (byte)value[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsEmptyIndexEntry(uint moduleId, uint dataIndex, byte[] metadata)
        {
            if (moduleId != 0 || dataIndex != 0)
            {
                return false;
            }

            for (var index = 0; index < metadata.Length; index++)
            {
                if (metadata[index] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class IndexRecord
        {
            public IndexRecord(int index, uint moduleId, uint dataIndex, byte[] metadata)
            {
                Index = index;
                ModuleId = moduleId;
                DataIndex = dataIndex;
                Metadata = metadata;
                Priority = BinaryPrimitives.ReadUInt32LittleEndian(metadata);
            }

            public int Index { get; }
            public uint ModuleId { get; }
            public uint DataIndex { get; }
            public byte[] Metadata { get; }
            public uint Priority { get; }
        }

        private static bool LooksLikeText(byte[] data)
        {
            if (data.Length == 0)
            {
                return false;
            }

            var printable = 0;
            var sampleLength = Math.Min(data.Length, 512);
            for (var index = 0; index < sampleLength; index++)
            {
                var value = data[index];
                if (value == 0 || value < 0x09 || (value > 0x0d && value < 0x20))
                {
                    continue;
                }

                printable++;
            }

            return printable * 100 / sampleLength >= 85;
        }

        private static byte[] ReadExactly(BinaryReader reader, int count)
        {
            var data = reader.ReadBytes(count);
            if (data.Length != count)
            {
                throw new EndOfStreamException($"Expected {count} bytes but only read {data.Length}.");
            }

            return data;
        }

        private static void EnsureRange(Stream stream, long offset, long length, string description)
        {
            if (offset < 0 || length < 0 || offset > stream.Length - length)
            {
                throw new InvalidDataException($"The BIT file does not contain {description} at 0x{offset:X}.");
            }
        }

        private static void EnsureArrayLength(long length, string description)
        {
            if (length > int.MaxValue)
            {
                throw new InvalidDataException($"{description} is too large: {length} bytes.");
            }
        }

        private static void EnsureInput(byte[] input, int offset, int count, int index, uint moduleId)
        {
            if (offset < 0 || count < 0 || offset > input.Length - count)
            {
                throw new InvalidDataException($"BIT module {index} (0x{moduleId:X8}) has truncated compressed data.");
            }
        }

        private static void EnsureOutput(byte[] output, int offset, int count, int index, uint moduleId)
        {
            if (offset < 0 || count < 0 || offset > output.Length - count)
            {
                throw new InvalidDataException($"BIT module {index} (0x{moduleId:X8}) expands beyond its declared length.");
            }
        }

        private static void EnsureDecodedCapacity(List<byte> output, int historyLength, long maximumDecodedLength, int count, int index, uint moduleId)
        {
            var decodedLength = output.Count - historyLength;
            if (decodedLength < 0 || count < 0 || decodedLength > maximumDecodedLength - count)
            {
                throw new InvalidDataException($"BIT module {index} (0x{moduleId:X8}) expands beyond a safe decoded length.");
            }
        }

        private static void EnsureWordOutputCapacity(List<ushort> output, int historyLength, long maximumOutputWords, int count, int index, uint moduleId)
        {
            var decodedLength = output.Count - historyLength;
            if (decodedLength < 0 || count < 0 || decodedLength > maximumOutputWords - count)
            {
                throw new InvalidDataException($"BIT module {index} (0x{moduleId:X8}) expands beyond a safe word-decoded length.");
            }
        }

        private static void EnsureDecodedLength(int actualLength, int expectedLength, int index, uint moduleId)
        {
            if (actualLength != expectedLength)
            {
                throw new InvalidDataException($"BIT module {index} (0x{moduleId:X8}) decoded to {actualLength} bytes; expected {expectedLength}.");
            }
        }

        public class BitEntry
        {
            public string ContainerName { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public byte[] Data { get; set; } = Array.Empty<byte>();
            public byte[] DecodedModuleData { get; set; } = Array.Empty<byte>();
            public byte[] CompressedData { get; set; } = Array.Empty<byte>();
            public byte[] BitmapData { get; set; } = Array.Empty<byte>();
            public int ModuleIndex { get; set; }
            public uint ModuleId { get; set; }
            public uint DataIndex { get; set; }
            public long ModuleOffset { get; set; }
            public uint StoredLength { get; set; }
            public uint UnpackedLength { get; set; }
            public uint CompressedLength { get; set; }
            public uint ExtractedLength { get; set; }
            public uint DecodedLength { get; set; }
            public byte[] ModuleHeader { get; set; } = Array.Empty<byte>();
            public ushort HeaderValue0B { get; set; }
            public ushort HeaderValue0D { get; set; }
            public byte FormatCode { get; set; }
            public byte CompressionType { get; set; }
            public ushort ModuleFlags { get; set; }
            public byte[] Metadata { get; set; } = Array.Empty<byte>();
            public byte ContentTypeCode { get; set; }
            public byte ContentSubtypeCode { get; set; }
            public BitContentKind ContentKind { get; set; }
            public bool IsDecompressed { get; set; }
            public bool UsedZeroFilledHistory { get; set; }
            public int HistoryWindowLength { get; set; }
            public string? ExtractionError { get; set; }
            public string? ExtractionWarning { get; set; }
            public string? BitmapDataError { get; set; }
            public bool RenderedAsPng { get; set; }
            public string? ImageError { get; set; }
        }
    }
}
