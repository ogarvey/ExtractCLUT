using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public static class SoTC
    {
        private static BinaryReader _source;
        private static byte[] _dest;
        private static byte _bitBuf;
        private static int _bitBufLeft;
        private static byte[] _buffer = new byte[32768];
        private static int _bufferPos;
        private static int _bytesWritten;
        private static HufNode[] _literalTree = new HufNode[256];
        private static HufNode[] _distanceTree = new HufNode[64];
        private static HufNode[] _lengthTree = new HufNode[64];

        private class HufNode
        {
            public ushort B0;
            public ushort B1;
            public HufNode? Jump;
        }

        public static byte[] Decompress(Stream source, int flags, int size)
        {
            int minMatchLen = (flags & 4) != 0 ? 3 : 2;
            int distBits = (flags & 2) != 0 ? 7 : 6;

            _source = new BinaryReader(source);
            _dest = new byte[size];
            Array.Clear(_buffer, 0, _buffer.Length);
            _bufferPos = 0;
            _bitBufLeft = 0;
            _bytesWritten = 0;

            if ((flags & 4) != 0)
            {
                CreateTree(_literalTree);
            }
            CreateTree(_lengthTree);
            CreateTree(_distanceTree);

            while (_bytesWritten < size)
            {
                if (ReadBit() == 1)
                {
                    PutByte((byte)((flags & 4) != 0 ? (byte)DecodeSFValue(_literalTree) : ReadBits(8)));
                }
                else
                {
                    int distance = (ReadBits(distBits) | (DecodeSFValue(_distanceTree) << distBits)) + 1;
                    int len = DecodeSFValue(_lengthTree);
                    if (len == 63)
                    {
                        len += ReadBits(8);
                    }
                    len += minMatchLen;
                    while (len-- > 0)
                    {
                        PutByte(_buffer[(_bufferPos - distance) & 0x7FFF]);
                    }
                }
            }
            Flush();
            return _dest;
        }

        private static byte ReadByte()
        {
            if (_source.BaseStream.Position >= _source.BaseStream.Length)
            {
                throw new EndOfStreamException("Unexpected end of stream");
            }
            return _source.ReadByte();
        }

        private static int ReadBit()
        {
            if (_bitBufLeft == 0)
            {
                _bitBuf = ReadByte();
                _bitBufLeft = 8;
            }
            int bit = _bitBuf & 1;
            _bitBuf >>= 1;
            _bitBufLeft--;
            return bit;
        }

        private static int ReadBits(int count)
        {
            int res = 0;
            int pos = 0;
            while (count-- > 0)
            {
                res |= ReadBit() << (pos++);
            }
            return res;
        }

        private static void PutByte(byte value)
        {
            _bytesWritten++;
            _buffer[_bufferPos++] = value;
            if (_bufferPos == 0x8000)
            {
                Flush();
            }
        }

        private static void Flush()
        {
            Array.Copy(_buffer, 0, _dest, _bytesWritten - _bufferPos, _bufferPos);
            _bufferPos = 0;
        }

        private static void RecreateTree(ref HufNode[] currentTree, ref byte len, ref short[] fpos, ref int[] flens, short fmax)
        {
            if (currentTree == null || currentTree.Length == 0)
            {
                currentTree = new HufNode[fmax];
                for (int i = 0; i < fmax; i++)
                {
                    currentTree[i] = new HufNode();
                }
            }
            if (len == 17)
            {
                throw new Exception("DecompressImplode::recreateTree() Invalid huffman tree");
            }
            len++;
            while (true)
            {
                if (fpos[len] >= fmax)
                {
                    currentTree[len] = new HufNode { B0 = 0x8000 };
                    RecreateTree(ref currentTree, ref len, ref fpos, ref flens, fmax);
                    break;
                }
                if (flens[fpos[len]] == len)
                {
                    currentTree[len] = new HufNode { B0 = (ushort)fpos[len]++ };
                    break;
                }
                fpos[len]++;
            }
            while (true)
            {
                if (fpos[len] >= fmax)
                {
                    currentTree[len].B1 = 0x8000;
                    currentTree[len].Jump = currentTree[len];
                    RecreateTree(ref currentTree, ref len, ref fpos, ref flens, fmax);
                    break;
                }
                if (flens[fpos[len]] == len)
                {
                    currentTree[len].B1 = (ushort)fpos[len]++;
                    currentTree[len].Jump = null;
                    break;
                }
                fpos[len]++;
            }
            len--;
        }

        private static int DecodeSFValue(HufNode[] currentTree)
        {
            HufNode currNode = currentTree[0];
            while (true)
            {
                if (ReadBit() == 0)
                {
                    if ((currNode.B1 & 0x8000) == 0)
                    {
                        return currNode.B1;
                    }
                    currNode = currNode.Jump;
                }
                else
                {
                    if ((currNode.B0 & 0x8000) == 0)
                    {
                        return currNode.B0;
                    }
                    currNode = currentTree[Array.IndexOf(currentTree, currNode) + 1];
                }
            }
        }

        private static void CreateTree(HufNode[] currentTree)
        {
            byte len = 0;
            short[] fpos = new short[17];
            int[] lengths = new int[256];
            int lengthsCount = 0;
            int treeBytes = ReadByte() + 1;
            for (int i = 0; i < treeBytes; i++)
            {
                int a = ReadByte();
                int bitValues = ((a >> 4) & 0x0F) + 1;
                int bitLength = (a & 0x0F) + 1;
                while (bitValues-- > 0)
                {
                    lengths[lengthsCount++] = bitLength;
                }
            }
            RecreateTree(ref currentTree, ref len, ref fpos, ref lengths, (short)lengthsCount);
        }
    }
}


// var sotcPakFile = @"C:\GOGGames\Shadow of the Comet\CD\SHADOW\A03.PAK";

// var sotcPakData = File.ReadAllBytes(sotcPakFile);

// var index = 4;

// var offsets = new List<uint>();

// while (BitConverter.ToUInt32(sotcPakData.Skip(index).Take(4).ToArray()) != 0)
// {
//     var offset = BitConverter.ToUInt32(sotcPakData.Skip(index).Take(4).ToArray(), 0);
//     offsets.Add(offset);
//     index += 4;
// }

// var outputFolder = Path.Combine(Path.GetDirectoryName(sotcPakFile), "output");
// Directory.CreateDirectory(outputFolder);

// var uncompressedFolder = Path.Combine(outputFolder, "uncompressed");
// Directory.CreateDirectory(uncompressedFolder);

// var decompressedFolder = Path.Combine(outputFolder, "compressed");
// Directory.CreateDirectory(decompressedFolder);

// for (int i = 0; i < offsets.Count; i++)
// {
//     var offset = offsets[i];
//     var nextOffset = (i == offsets.Count - 1) ? sotcPakData.Length : (int)offsets[i + 1];
//     var data = sotcPakData.Skip((int)offset).Take(nextOffset - (int)offset).ToArray();
//     var headerData = data.Skip(0x4).Take(12).ToArray();
//     var dSize = BitConverter.ToUInt32(headerData.Take(4).ToArray(), 0);
//     var uncompressedSize = BitConverter.ToUInt32(headerData.Skip(4).Take(4).ToArray(), 0);
//     var compressionType = headerData[8];
//     var flags = headerData[9];
//     var nameLength = BitConverter.ToUInt16(headerData.Skip(10).Take(2).ToArray(), 0);
//     var nameBytes = data.Skip(0x12).Take(nameLength - 2).ToArray();
//     var name = "";
//     data = data.Skip(0x10 + nameLength).ToArray();
//     index = 0;
//     while (nameBytes[index] != 0)
//     {
//         name += (char)nameBytes[index];
//         index++;
//     }
//     if (compressionType == 0)
//     {
//         File.WriteAllBytes(Path.Combine(uncompressedFolder, $"{name}"), data);
//     }
//     else
//     {
//         var dataStream = new MemoryStream(data);
//         var decompressed = SoTC.Decompress(dataStream, flags, (int)uncompressedSize);
//         File.WriteAllBytes(Path.Combine(decompressedFolder, $"{name}"), decompressed);
//     }
// }
