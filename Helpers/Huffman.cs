using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Helpers
{

    //-----------------------------------------------------------------------
    // Huffman Tree data structure and related classes
    //-----------------------------------------------------------------------

    /// <summary>
    /// Base class for nodes in the Huffman Tree.
    /// </summary>
    public abstract class HuffmanTree
    {
        /// <summary>
        /// Calculates the number of leaf nodes (output symbols) in the tree/subtree.
        /// </summary>
        public abstract int GetLeafCount();
    }

    /// <summary>
    /// Represents an internal node in the Huffman Tree, branching to two children.
    /// </summary>
    public class HuffNode : HuffmanTree
    {
        public HuffmanTree Left { get; }
        public HuffmanTree Right { get; }

        public HuffNode(HuffmanTree left, HuffmanTree right)
        {
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
        }

        public override int GetLeafCount()
        {
            // Recursively count leaves in both subtrees
            return Left.GetLeafCount() + Right.GetLeafCount();
        }

        public override string ToString() => $"Node({Left}, {Right})";
    }

    /// <summary>
    /// Represents a leaf node in the Huffman Tree, holding a decoded byte value.
    /// </summary>
    public class HuffLeaf : HuffmanTree
    {
        public byte Value { get; }

        public HuffLeaf(byte value)
        {
            Value = value;
        }

        public override int GetLeafCount()
        {
            // A leaf node counts as 1
            return 1;
        }

        public override string ToString() => $"Leaf({Value})";
    }

    //-----------------------------------------------------------------------
    // Helper class for reading bits from a byte array
    //-----------------------------------------------------------------------

    /// <summary>
    /// Reads bits sequentially from a byte array, handling little-endian bit order.
    /// </summary>
    public class BitStreamReader
    {
        private readonly byte[] _data;
        private int _byteIndex;
        private int _bitIndex; // 0-7

        public BitStreamReader(byte[] data, int startIndex = 0)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _byteIndex = startIndex;
            _bitIndex = 0; // Start at the least significant bit (little-endian)
        }

        /// <summary>
        /// Reads the next bit from the stream.
        /// </summary>
        /// <returns>The bit value (true for 1, false for 0).</returns>
        /// <exception cref="InvalidOperationException">Thrown if no more bits are available.</exception>
        public bool ReadBit()
        {
            if (_byteIndex >= _data.Length)
            {
                throw new InvalidOperationException("Cannot read past the end of the data stream.");
            }

            // Extract the bit using little-endian order (0th bit is least significant)
            bool bit = (_data[_byteIndex] & (1 << _bitIndex)) != 0;

            _bitIndex++;
            if (_bitIndex == 8) // Move to the next byte
            {
                _bitIndex = 0;
                _byteIndex++;
            }

            return bit;
        }

        /// <summary>
        /// Checks if there are more bits available to read.
        /// </summary>
        public bool HasMoreBits => _byteIndex < _data.Length;
    }


    //-----------------------------------------------------------------------
    // Main Decompression Logic
    //-----------------------------------------------------------------------

    public static class HuffmanDecoder
    {
        /// <summary>
        /// Reads the Huffman Tree dictionary from a byte array.
        /// Tries node 254 first, then searches 0-253 if the first tree isn't complete.
        /// </summary>
        /// <param name="dictionaryData">The raw byte data of the Huffman dictionary file.</param>
        /// <returns>The root of the constructed Huffman Tree.</returns>
        /// <exception cref="FormatException">Thrown if a valid, complete Huffman Tree cannot be found.</exception>
        public static HuffmanTree ReadHuffmanTree(byte[] dictionaryData)
        {
            if (dictionaryData == null || dictionaryData.Length < 4) // Need at least one node definition
            {
                throw new ArgumentException("Dictionary data is null or too short.", nameof(dictionaryData));
            }

            // Try reading from the standard root node index first (254)
            HuffmanTree tree = ReadHuffmanTreeInternal(dictionaryData, 254);

            // Check if the tree contains all 256 possible byte values
            if (tree.GetLeafCount() == 256)
            {
                return tree;
            }

            // If not complete, search other potential root nodes (0 to 253)
            for (int i = 0; i <= 253; i++)
            {
                try
                {
                    tree = ReadHuffmanTreeInternal(dictionaryData, i);
                    if (tree.GetLeafCount() == 256)
                    {
                        return tree;
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    // Ignore errors from trying invalid indices during the search,
                    // as the format implies not all indices necessarily define nodes.
                    continue;
                }
                catch (FormatException fe) when (fe.Message.Contains("Encountered a non-00/01 option"))
                {
                    // Ignore specific format errors during search, maybe some indices are invalid
                    continue;
                }
            }

            // If no complete tree was found after checking all candidates
            throw new FormatException("ReadHuffmanTree: No Full Huffman Tree found (256 leaves).");
        }

        /// <summary>
        /// Recursively reads a Huffman Tree or subtree starting from a specific node index.
        /// </summary>
        /// <param name="dictionaryData">The raw dictionary data.</param>
        /// <param name="nodeIndex">The index of the node to start reading from.</param>
        /// <returns>The constructed HuffmanTree node.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown if nodeIndex is invalid.</exception>
        /// <exception cref="FormatException">Thrown for invalid node type flags.</exception>
        private static HuffmanTree ReadHuffmanTreeInternal(byte[] dictionaryData, int nodeIndex)
        {
            int offset = nodeIndex * 4;

            // Ensure we can read 4 bytes for the node definition
            if (offset + 3 >= dictionaryData.Length)
            {
                throw new IndexOutOfRangeException($"ReadHuffmanTreeInternal: Node index {nodeIndex} (offset {offset}) is out of bounds for dictionary data length {dictionaryData.Length}.");
            }

            // Read the 4 bytes defining the node and its children
            byte x1 = dictionaryData[offset];
            byte x2 = dictionaryData[offset + 1]; // Left child type/index
            byte y1 = dictionaryData[offset + 2];
            byte y2 = dictionaryData[offset + 3]; // Right child type/index

            // Determine left and right children based on the type bytes (x2, y2)
            HuffmanTree leftChild = CreateChildNode(dictionaryData, x1, x2);
            HuffmanTree rightChild = CreateChildNode(dictionaryData, y1, y2);

            return new HuffNode(leftChild, rightChild);
        }

        /// <summary>
        /// Helper to create a child node (either leaf or internal node) based on dictionary data.
        /// </summary>
        private static HuffmanTree CreateChildNode(byte[] dictionaryData, byte valueOrIndex, byte typeFlag)
        {
            switch (typeFlag)
            {
                case 0x00: // Leaf node: valueOrIndex is the actual byte value
                    return new HuffLeaf(valueOrIndex);
                case 0x01: // Internal node: valueOrIndex is the index of the child node
                           // Recursively read the subtree
                    return ReadHuffmanTreeInternal(dictionaryData, (int)valueOrIndex);
                default:
                    // As per original code, only 0x00 and 0x01 are valid type flags
                    throw new FormatException($"CreateChildNode: Encountered an invalid type flag {typeFlag:X2}. Expected 0x00 or 0x01.");
            }
        }

        /// <summary>
        /// Decompresses Huffman-encoded data using a provided tree.
        /// </summary>
        /// <param name="rootNode">The root of the Huffman Tree.</param>
        /// <param name="compressedData">The raw byte data of the compressed chunk.</param>
        /// <returns>A byte array containing the decompressed data.</returns>
        /// <exception cref="ArgumentException">Thrown if compressed data is too short.</exception>
        /// <exception cref="FormatException">Thrown if data ends unexpectedly or format is invalid.</exception>
        public static byte[] Decompress(HuffmanTree rootNode, byte[] compressedData)
        {
            if (rootNode == null) throw new ArgumentNullException(nameof(rootNode));
            if (compressedData == null) throw new ArgumentNullException(nameof(compressedData));
            if (compressedData.Length < 4)
            {
                throw new ArgumentException("Decompress: Input data must be at least 4 bytes long (for size header).", nameof(compressedData));
            }

            // First 4 bytes are the total number of decompressed bytes (little-endian)
            uint totalOutputBytes = BitConverter.ToUInt32(compressedData, 0);

            // If the expected output is 0 bytes, return empty array immediately.
            if (totalOutputBytes == 0)
            {
                return Array.Empty<byte>();
            }

            // Initialize output list and bit stream reader (starting after the size header)
            List<byte> outputBytes = new List<byte>((int)totalOutputBytes); // Pre-allocate capacity
            BitStreamReader bitStream = new BitStreamReader(compressedData, 4); // Start reading data bits from byte 4

            HuffmanTree currentNode = rootNode;

            try
            {
                // Loop until the expected number of bytes have been decompressed
                while (outputBytes.Count < totalOutputBytes)
                {
                    if (!bitStream.HasMoreBits && !(currentNode is HuffLeaf)) // Check for premature end of stream only if we need more bits
                    {
                        throw new FormatException($"Decompress: Ran out of bits to read after decoding {outputBytes.Count} bytes, expected {totalOutputBytes}.");
                    }

                    switch (currentNode)
                    {
                        case HuffLeaf leaf:
                            // Found a leaf node, output its value
                            outputBytes.Add(leaf.Value);
                            // Reset to the root node for the next symbol
                            currentNode = rootNode;
                            // Check immediately if we have finished after adding the byte
                            if (outputBytes.Count == totalOutputBytes)
                            {
                                goto decoding_finished; // Exit loop efficiently
                            }
                            break;

                        case HuffNode node:
                            // Internal node, read the next bit to decide direction
                            bool bit = bitStream.ReadBit();
                            currentNode = bit ? node.Right : node.Left; // True (1) -> Right, False (0) -> Left
                            break;

                        default:
                            // Should not happen with the defined classes
                            throw new InvalidOperationException("Decompress: Encountered an unknown HuffmanTree node type.");
                    }
                }

            decoding_finished:; // Label for jumping out when done

                // Final check: Ensure we decoded exactly the expected number of bytes
                if (outputBytes.Count != totalOutputBytes)
                {
                    // This case might occur if the last bits perfectly form a byte but totalOutputBytes wasn't reached.
                    throw new FormatException($"Decompress: Decoding finished but byte count mismatch. Decoded {outputBytes.Count}, expected {totalOutputBytes}.");
                }

                return outputBytes.ToArray();
            }
            catch (InvalidOperationException ex) // Catches BitStreamReader end-of-stream errors
            {
                throw new FormatException($"Decompress: Error reading bitstream after decoding {outputBytes.Count} bytes (expected {totalOutputBytes}). Input data may be truncated or corrupted. Details: {ex.Message}", ex);
            }
        }

    }
}
