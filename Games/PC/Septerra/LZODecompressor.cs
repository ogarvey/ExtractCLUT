using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Septerra
{
    public class LZODecompressor
    {
        public static byte[] Decompress(byte[] input, int outputSize)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (outputSize < 0) throw new ArgumentOutOfRangeException(nameof(outputSize));

            var output = new byte[outputSize];
            int ip = 0;
            int op = 0;

            int ipEnd = input.Length;

            byte t = input[ip++];
            if (t >= 0x12)
            {
                int len = t - 0x11;
                CopyLiteral(input, ref ip, ref output, ref op, len);
            }
            else
            {
                if (!TryReadLiteralRun(input, ref ip, ipEnd, ref output, ref op, t, out _, out _))
                {
                    return output;
                }
            }

            bool hasToken = false;
            byte token = 0;

            while (true)
            {
                if (!hasToken)
                {
                    if (ip >= ipEnd) break;
                    token = input[ip++];
                }
                hasToken = false;

                if (token <= 0x0F)
                {
                    int b = ReadByte(input, ref ip);
                    int offset = 0x801 + (b << 2) + (token >> 2);
                    CopyMatch(ref output, ref op, offset, 3);

                    int litCount = input[ip - 2] & 3;
                    if (litCount == 0)
                    {
                        if (!TryReadLiteralRun(input, ref ip, ipEnd, ref output, ref op, null, out hasToken, out token))
                        {
                            break;
                        }
                        if (hasToken)
                        {
                            continue;
                        }
                        continue;
                    }

                    CopyLiteral(input, ref ip, ref output, ref op, litCount);
                    continue;
                }

                while (true)
                {
                    ProcessMatchToken(input, ref ip, ipEnd, ref output, ref op, token);

                    int litCount = input[ip - 2] & 3;
                    if (litCount == 0)
                    {
                        if (!TryReadLiteralRun(input, ref ip, ipEnd, ref output, ref op, null, out hasToken, out token))
                        {
                            return output;
                        }
                        if (!hasToken)
                        {
                            break;
                        }
                        continue;
                    }

                    CopyLiteral(input, ref ip, ref output, ref op, litCount);
                    break;
                }
            }

            if (op != outputSize)
            {
                Array.Resize(ref output, op);
            }

            return output;
        }

        private static bool TryReadLiteralRun(
            byte[] input,
            ref int ip,
            int ipEnd,
            ref byte[] output,
            ref int op,
            byte? initialToken,
            out bool hasToken,
            out byte token)
        {
            hasToken = false;
            token = 0;

            byte t;
            if (initialToken.HasValue)
            {
                t = initialToken.Value;
            }
            else
            {
                if (ip >= ipEnd) return false;
                t = input[ip++];
            }

            if (t >= 0x10)
            {
                hasToken = true;
                token = t;
                return true;
            }

            int len = t;
            if (len == 0)
            {
                int extra = 0;
                while (ip < ipEnd && input[ip] == 0)
                {
                    extra += 0xFF;
                    ip++;
                }
                len = ReadByte(input, ref ip) + 0x0F + extra;
            }

            CopyLiteral(input, ref ip, ref output, ref op, 3);
            CopyLiteral(input, ref ip, ref output, ref op, len);
            return true;
        }

        private static void ProcessMatchToken(
            byte[] input,
            ref int ip,
            int ipEnd,
            ref byte[] output,
            ref int op,
            byte token)
        {
            uint uVar9 = token;
            if (uVar9 < 0x40)
            {
                if (uVar9 < 0x20)
                {
                    if (uVar9 > 0x0F)
                    {
                        uint uVar10 = uVar9 & 7;
                        if (uVar10 == 0)
                        {
                            int extra = 0;
                            byte b = ReadByte(input, ref ip);
                            while (b == 0)
                            {
                                extra += 0xFF;
                                b = ReadByte(input, ref ip);
                            }
                            uVar10 = (uint)(b + 7 + extra);
                        }

                        byte b1 = ReadByte(input, ref ip);
                        byte b2 = ReadByte(input, ref ip);
                        int offset = 0x4000 + (b2 << 6) + ((int)(uVar9 & 8) << 11) + (b1 >> 2);
                        CopyMatch(ref output, ref op, offset, (int)uVar10 + 2);
                    }
                    else
                    {
                        byte b = ReadByte(input, ref ip);
                        int offset = 1 + (b << 2) + ((int)uVar9 >> 2);
                        CopyMatch(ref output, ref op, offset, 2);
                    }
                }
                else
                {
                    uint uVar10 = uVar9 & 0x1F;
                    if (uVar10 == 0)
                    {
                        int extra = 0;
                        byte b = ReadByte(input, ref ip);
                        while (b == 0)
                        {
                            extra += 0xFF;
                            b = ReadByte(input, ref ip);
                        }
                        uVar10 = (uint)(b + 0x1F + extra);
                    }

                    byte b1 = ReadByte(input, ref ip);
                    byte b2 = ReadByte(input, ref ip);
                    int offset = 1 + (b1 >> 2) + (b2 << 6);
                    CopyMatch(ref output, ref op, offset, (int)uVar10 + 2);
                }
            }
            else
            {
                byte b = ReadByte(input, ref ip);
                int offset = 1 + (((int)uVar9 >> 2) & 7) + (b << 3);
                int len = ((int)uVar9 >> 5) - 1;
                CopyMatch(ref output, ref op, offset, len + 2);
            }
        }

        private static byte ReadByte(byte[] input, ref int ip)
        {
            if (ip >= input.Length)
            {
                throw new InvalidOperationException("Unexpected end of LZO stream.");
            }
            return input[ip++];
        }

        private static void CopyLiteral(byte[] input, ref int ip, ref byte[] output, ref int op, int length)
        {
            if (length <= 0) return;
            if (ip + length > input.Length)
            {
                throw new InvalidOperationException("LZO literal copy exceeds input length.");
            }
            EnsureCapacity(ref output, op + length);
            Buffer.BlockCopy(input, ip, output, op, length);
            ip += length;
            op += length;
        }

        private static void CopyMatch(ref byte[] output, ref int op, int offset, int length)
        {
            if (length <= 0) return;
            int refPos = op - offset;
            EnsureCapacity(ref output, op + length);
            if (refPos < 0)
            {
                int pad = Math.Min(-refPos, length);
                for (int i = 0; i < pad; i++)
                {
                    output[op++] = 0;
                }
                length -= pad;
                refPos = 0;
            }
            for (int i = 0; i < length; i++)
            {
                output[op++] = output[refPos++];
            }
        }

        private static void EnsureCapacity(ref byte[] output, int required)
        {
            if (required <= output.Length) return;
            int newSize = output.Length == 0 ? required : output.Length;
            while (newSize < required)
            {
                newSize = Math.Max(newSize * 2, required);
            }
            Array.Resize(ref output, newSize);
        }
    }
}
