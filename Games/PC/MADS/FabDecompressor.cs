using System;
using System.IO;
using System.Text;

public static class FabDecompressor
{
    public static MemoryStream ReadFab(BinaryReader src, int? length = null)
    {
        MemoryStream dest = new MemoryStream();

        // Verify header
        char[] headerChars = src.ReadChars(3);
        string header = new string(headerChars);
        if (header != "FAB")
            throw new Exception("fab_decode: header not found");

        byte shift_val = src.ReadByte();
        if (!(shift_val >= 10 && shift_val < 14))
            throw new Exception($"fab_decode: invalid shift_val: {shift_val}");
        //Console.WriteLine($"Shift value: {shift_val}");
        // Calculate masks and shifts
        int copy_adr_shift = 16 - shift_val;
        //Console.WriteLine($"Copy address shift: {copy_adr_shift}");
        int copy_adr_fill = (0xFF << (shift_val - 8)) & 0xFF;
        //Console.WriteLine($"Copy address fill: {copy_adr_fill:B8}");
        int copy_len_mask = (1 << copy_adr_shift) - 1;
        //Console.WriteLine($"Copy length mask: {copy_len_mask:B8}");

        // In Python code: copy_adr = 0xFFFF0000
        // We'll store copy_adr as int so that sign is preserved
        int copy_adr;
        int copy_len;
        // Variables for bit reading
        int _bits_left = 16;
        int _bit_buffer = src.ReadUInt16();
        //Console.WriteLine($"Initial bit buffer: {_bit_buffer:B16}");

        // Debug counters (not strictly needed)
        int cs_bytes = 2;
        int cs_bits = 0;

        int j = 0; // position in dest

        // Local function to read a single bit from the input stream
        int get_bit()
        {
            _bits_left -= 1;
            if (_bits_left == 0)
            {
                // Refill buffer: 
                // read next 16 bits and shift in a bit from the old buffer
                ushort nextBits = src.ReadUInt16();
                _bit_buffer = (nextBits << 1) | (_bit_buffer & 1);
                _bits_left = 16;
                cs_bytes += 2;
                //Console.WriteLine($"Read from {src.BaseStream.Position - 2}; bit buffer = {_bit_buffer:B16}[{_bits_left}]");
                //Console.WriteLine($"read bits; i={src.BaseStream.Position} {(_bit_buffer>>1):B16}");
            }

            int bit = _bit_buffer & 1;
            _bit_buffer >>= 1;
            cs_bits += 1;
            //Console.WriteLine($"bb = {_bit_buffer:B16}[{_bits_left}]");
            return bit;
        }

        while (true)
        {
            // 1 - take next byte literal
            // 00 - copy earlier pattern
            // 01 - special handling
            if (get_bit() == 0)
            {
                // If the first bit is 0, we need the second bit:
                if (get_bit() == 0)
                {
                    // pattern: 00
                    int b1 = get_bit();
                    int b2 = get_bit();
                    copy_len = ((b1 << 1) | b2) + 2;

                    // read negative num in range [255 -> -1, 0 -> -256]
                    byte raw_copy_adr = src.ReadByte();
                    // Sign extension: raw_copy_adr in [0..255]
                    // copy_adr = raw_copy_adr | 0xFFFFFF00
                    // Ensures copy_adr is negative if raw_copy_adr < 256
                    copy_adr = (int)(raw_copy_adr | 0xFFFFFF00);
                    //Console.WriteLine($"00 copy {copy_len} from {copy_adr}");
                }
                else
                {
                    // pattern: 01
                    byte A = src.ReadByte();
                    byte B = src.ReadByte();
                    //Console.WriteLine($"A: {A:X8}, B: {B:X8}");

                    // // copy_adr = (((B >> copy_adr_shift) | copy_adr_fill) << 8) | A
                    // // Then or with 0xFFFF0000 for sign extension
                    // int adrHigh = ((B >> copy_adr_shift) | copy_adr_fill);
                    // copy_adr = (int)((adrHigh << 8) | A);
                    int val = (((B >> copy_adr_shift) | copy_adr_fill) << 8) | A;

                    //copy_adr |= unchecked((int)0xFFFF0000);
                    copy_adr = val | unchecked((int)0xFFFF0000);

                    copy_len = B & copy_len_mask;
                    if (copy_len == 0)
                    {
                        // use next byte for length
                        int c = src.ReadByte();
                        if (c == -1)
                            throw new Exception("Unexpected end of stream.");
                        //Console.WriteLine($"copy_len2 = {c}; from {src.BaseStream.Position-1}");
                        if (c == 0)
                        {
                            // HALT
                            break;
                        }
                        else if (c == 1)
                        {
                            // NOP, do nothing
                            continue;
                        }
                        else
                        {
                            copy_len = c + 1;
                        }
                    }
                    else
                    {
                        copy_len += 2;
                    }
                    //Console.WriteLine($"01 copy {copy_len} from {copy_adr}");
                }

                while (copy_len > 0)
                {
                    long srcPos = j + copy_adr;
                    dest.Position = srcPos;
                    int v = dest.ReadByte();
                    if (v < 0)
                        throw new Exception("Unexpected end of dest data during copy.");

                    dest.Position = j;
                    dest.WriteByte((byte)v);
                    //Console.WriteLine($"{j}: copying {v}");
                    j++;
                    copy_len--;
                }
            }
            else
            {
                // 1: literal byte
                int v = src.ReadByte();
                if (v < 0)
                    throw new Exception("Unexpected end of stream reading literal byte.");

                dest.Position = j;
                dest.WriteByte((byte)v);
                //Console.WriteLine($"{j}: write {v}");
                j++;
            }
        }

        if (length.HasValue && length.Value != j)
            Console.WriteLine($"Decompressed length mismatch. Expected {length}, got {j}");

        dest.Position = 0;
        return dest;
    }

    public static MemoryStream ReadFab(Stream src, int? length = null, bool verbose = false)
    {
        var dest = new MemoryStream();
        long start = src.Position;

        using (var reader = new BinaryReader(src, Encoding.ASCII, leaveOpen: true))
        {
            // Check FAB header
            var header = Encoding.ASCII.GetString(reader.ReadBytes(3));
            if (header != "FAB")
            {
                throw new Exception("fab_decode: header not found");
            }

            if (verbose)
            {
                Console.WriteLine("\nFAB decompression start");
            }

            // Read shift_val
            byte shiftVal = reader.ReadByte();
            if (shiftVal < 10 || shiftVal >= 14)
            {
                throw new Exception($"fab_decode: invalid shift_val: {shiftVal}");
            }

            int copyAdrShift = 16 - shiftVal;
            int copyAdrFill = 0xFF << (shiftVal - 8);
            int copyLenMask = (1 << copyAdrShift) - 1;

            if (verbose)
            {
                Console.WriteLine($"copy_adr_shift = {copyAdrShift}");
                Console.WriteLine($"copy_adr_fill = {Convert.ToString(copyAdrFill, 2).PadLeft(8, '0')}");
                Console.WriteLine($"copy_len_mask = {Convert.ToString(copyLenMask, 2).PadLeft(8, '0')}");
            }

            int copyAdr = unchecked((int)0xFFFF0000);
            int bitsLeft = 16;
            ushort bitBuffer = reader.ReadUInt16();

            int j = 0; // Destination position
            int csBytes = 2, csBits = 0;

            Func<int> getBit = () =>
            {
                bitsLeft--;
                int bit = bitBuffer & 1;
                bitBuffer >>= 1;
                csBits++;

                if (bitsLeft == 0)
                {
                    bitBuffer = (ushort)((reader.ReadUInt16() << 1) | (bitBuffer & 1));
                    bitsLeft = 16;
                    csBytes += 2;

                    if (verbose)
                    {
                        Console.WriteLine($"Read from {src.Position - 2 - start}; bit_buffer = {Convert.ToString(bitBuffer, 2).PadLeft(bitsLeft, '0')}");
                    }
                }

                return bit;
            };

            while (true)
            {
                if (getBit() == 0)
                {
                    if (getBit() == 0)
                    {
                        // 00, bit, bit, byte
                        int b1 = getBit();
                        int b2 = getBit();
                        int copyLen = (b1 << 1 | b2) + 2;

                        int rawCopyAdr = reader.ReadByte();
                        copyAdr = rawCopyAdr | unchecked((int)0xFFFFFF00);

                        if (verbose)
                        {
                            Console.WriteLine($"00 copy {copyLen} from {copyAdr}");
                        }

                        CopyFromDestination(dest, ref j, copyLen, copyAdr, verbose);
                    }
                    else
                    {
                        // 01, byte A, byte B
                        byte A = reader.ReadByte();
                        byte B = reader.ReadByte();

                        copyAdr = (((B >> copyAdrShift) | copyAdrFill) << 8) | A;
                        copyAdr |= unchecked((int)0xFFFF0000);

                        int copyLen = B & copyLenMask;
                        if (copyLen == 0)
                        {
                            copyLen = reader.ReadByte();
                            if (copyLen == 0)
                            {
                                break; // HALT
                            }
                            else if (copyLen == 1)
                            {
                                continue; // NOP
                            }
                            else
                            {
                                copyLen += 1;
                            }
                        }
                        else
                        {
                            copyLen += 2;
                        }

                        if (verbose)
                        {
                            Console.WriteLine($"01 copy {copyLen} from {copyAdr}");
                        }

                        CopyFromDestination(dest, ref j, copyLen, copyAdr, verbose);
                    }
                }
                else
                {
                    // Literal byte
                    byte v = reader.ReadByte();
                    dest.Seek(j, SeekOrigin.Begin);
                    dest.WriteByte(v);

                    if (verbose)
                    {
                        Console.WriteLine($"{j}: write    {v}");
                    }

                    j++;
                }
            }

            if (length.HasValue && length.Value != j)
            {
                throw new Exception($"Length mismatch: expected {length.Value}, got {j}");
            }

            dest.Seek(0, SeekOrigin.Begin);
            return dest;
        }
    }

    private static void CopyFromDestination(MemoryStream dest, ref int j, int copyLen, int copyAdr, bool verbose)
    {
        while (copyLen > 0)
        {
            long sourcePos = j + copyAdr;
            dest.Seek(sourcePos, SeekOrigin.Begin);
            byte v = (byte)dest.ReadByte();
            dest.Seek(j, SeekOrigin.Begin);
            dest.WriteByte(v);

            if (verbose)
            {
                //Console.WriteLine($"{j}: copying  {v}");
            }

            j++;
            copyLen--;
        }
    }
}
