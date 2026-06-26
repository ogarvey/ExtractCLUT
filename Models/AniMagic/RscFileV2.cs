using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Models.AniMagic
{
    // Used in version 2 of the RSC file format, which is used in Jumpstart Math
    public class RscFileV2 : RscFileBase
    {

        public RscFileV2(string filePath) : base(filePath)
        {
            _version = 2;

            using var rscReader = new BinaryReader(File.OpenRead(filePath));
            rscReader.BaseStream.Position = 0x4;
            _headerSize = rscReader.ReadUInt32(); // size of the header
            rscReader.BaseStream.Position = 0x10;
            _headerOffsetAdjustment = 0x20; // adjust the offset for version 2
            _headerCount = rscReader.ReadUInt16(); // number of headers
            rscReader.BaseStream.Position = _headerOffsetAdjustment; // move to the start of the headers

            ReadTableOffsets(rscReader);
            ProcessColorTable();

            Console.WriteLine($"RSC File Version: {_version}");
            Console.WriteLine($"Header Size: {_headerSize}");
            Console.WriteLine($"Header Count: {_headerCount}");
            Console.WriteLine("---- Header Type and Offset ---");
            foreach (var header in _headerTypeAndOffset)
            {
                Console.WriteLine($"Header Type: {header.HeaderType}, Offset: 0x{header.HeaderOffset:X8}, Size: 0x{header.HeaderSize:X8}");
            }
        }

        public uint ProcessBmpTable()
        {
            uint processedCount = 0;

            using var rscReader = new BinaryReader(File.OpenRead(_filePath));
            rscReader.BaseStream.Position = _bmpTableOffset;

            var bmpTableEntries = new List<BmpTableEntry>();

            while (rscReader.BaseStream.Position < _bmpTableOffset + _bmpTableSize)
            {
                var bmpEntry = new BmpTableEntry
                {
                    Offset = rscReader.ReadUInt32() + 0x10,
                    Size = rscReader.ReadUInt32(),
                    Padding1 = rscReader.ReadUInt32(),
                    Unknown = rscReader.ReadUInt32(),
                    Padding2 = rscReader.ReadUInt32()
                };
                if (bmpEntry.Offset == 0 || -bmpEntry.Size == 0)
                {
                    // Reached the end of the BMP table
                    break;
                }
                // Process the BMP entry as needed
                Console.WriteLine($"BMP Entry - Offset: {bmpEntry.Offset}, Size: {bmpEntry.Size}, Unknown: {bmpEntry.Unknown}");

                bmpTableEntries.Add(bmpEntry);
            }

            foreach (var bmpEntry in bmpTableEntries)
            {
                rscReader.BaseStream.Position = bmpEntry.Offset;

                var unknownValue = rscReader.ReadUInt16();
                var compressionType = (byte)(unknownValue & 0xFF);
                Console.WriteLine($"Unknown Value: {unknownValue}");
                Console.WriteLine($"Compression Type: {compressionType}");
                var height = rscReader.ReadUInt16();
                var width = rscReader.ReadUInt16();
                var dataSize = rscReader.ReadUInt32();
                var compressedData = rscReader.ReadBytes((int)dataSize);
                try
                {
                    var decompressed = DecodeBmpData(compressionType, compressedData, width * height);

                    // Type 4 decompresses to a compiled sprite stream; decode it to a flat w*h
                    // index buffer (color-table entry 0 == transparent). Other types are already flat.
                    var imageData = compressionType == 4
                        ? DecodeCompiledSprite(decompressed, width, height)
                        : decompressed;

                    if (imageData.Length == width * height)
                    {
                        var image = ImageFormatHelper.GenerateIMClutImage(
                            _palette, imageData, width, height,
                            useTransparency: compressionType == 4,
                            transparencyIndex: 0,
                            fixedIndex: true);
                        _bmpImages.Add(image);
                    }
                    else
                    {
                        Console.WriteLine($"Decoded BMP entry at offset {bmpEntry.Offset} produced {imageData.Length} bytes, expected {width * height} ({width}x{height}); render skipped.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to decode BMP entry at offset {bmpEntry.Offset} with size {dataSize}: {ex.Message}");
                }
                processedCount++;
            }

            return processedCount;
        }
    }

    public class BmpTableEntry
    {
        public uint Offset { get; set; }
        public uint Size { get; set; }
        public uint Padding1 { get; set; }
        public uint Unknown { get; set; }
        public uint Padding2 { get; set; }
    }
}
