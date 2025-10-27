using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ExtractCLUT.Games.Generic.ScummVM.Decompression
{
    public class DecompressorDCL
    {
        private const uint HUFFMAN_LEAF = 0x40000000;

        private const int DCL_BINARY_MODE = 0;
        private const int DCL_ASCII_MODE = 1;

        private const int MIDI_SETUP_BUNDLE_FILE_MAXIMUM_DICTIONARY_SIZE = 4096;

        private uint _dwBits;
        private byte _nBits;
        private long _sourceSize;
        private uint _targetSize;
        private bool _targetFixedSize;
        private uint _bytesRead;
        private uint _bytesWritten;
        private Stream _sourceStream;
        private Stream _targetStream;

        private void Init(Stream sourceStream, Stream targetStream, uint targetSize, bool targetFixedSize)
        {
            _sourceStream = sourceStream;
            _targetStream = targetStream;
            _sourceSize = sourceStream.Length;
            _targetSize = targetSize;
            _targetFixedSize = targetFixedSize;
            _nBits = 0;
            _bytesRead = _bytesWritten = 0;
            _dwBits = 0;
        }

        private void FetchBitsLSB()
        {
            while (_nBits <= 24)
            {
                int byteRead = _sourceStream.ReadByte();
                if (byteRead == -1) break;
                _dwBits |= (uint)byteRead << _nBits;
                _nBits += 8;
                _bytesRead++;
            }
        }

        private uint GetBitsLSB(int n)
        {
            if (_nBits < n)
            {
                FetchBitsLSB();
            }
            uint ret = _dwBits & ((1u << n) - 1);
            _dwBits >>= n;
            _nBits -= (byte)n;
            return ret;
        }

        private byte GetByteLSB()
        {
            return (byte)GetBitsLSB(8);
        }

        private void PutByte(byte b)
        {
            _targetStream.WriteByte(b);
            _bytesWritten++;
        }

        private int HuffmanLookup(int[] tree)
        {
            int pos = 0;

            while (((uint)tree[pos] & HUFFMAN_LEAF) == 0)
            {
                uint bit = GetBitsLSB(1);
                pos = (bit != 0) ? (tree[pos] & 0xFFF) : (tree[pos] >> 12);
            }

            int result = tree[pos] & 0xFFFF;
            return result;
        }

        public bool Unpack(Stream sourceStream, Stream targetStream, uint targetSize, bool targetFixedSize)
        {
            byte[] dictionary = new byte[MIDI_SETUP_BUNDLE_FILE_MAXIMUM_DICTIONARY_SIZE];
            ushort dictionaryPos = 0;
            ushort dictionarySize = 0;
            ushort dictionaryMask = 0;

            Init(sourceStream, targetStream, targetSize, targetFixedSize);

            byte mode = GetByteLSB();
            byte dictionaryType = GetByteLSB();

            if (mode != DCL_BINARY_MODE && mode != DCL_ASCII_MODE)
            {
                Debug.WriteLine($"DCL-IMPLODE: Error: Encountered mode {mode:X2}, expected 00 or 01");
                return false;
            }

            switch (dictionaryType)
            {
                case 4:
                    dictionarySize = 1024;
                    break;
                case 5:
                    dictionarySize = 2048;
                    break;
                case 6:
                    dictionarySize = 4096;
                    break;
                default:
                    Debug.WriteLine($"DCL-IMPLODE: Error: unsupported dictionary type {dictionaryType:X2}");
                    return false;
            }
            dictionaryMask = (ushort)(dictionarySize - 1);

            while (!_targetFixedSize || _bytesWritten < _targetSize)
            {
                if (GetBitsLSB(1) != 0) // (length,distance) pair
                {
                    int value = HuffmanLookup(DclHuffmanTrees.LengthTree);
                    int tokenLength;

                    if (value < 8)
                    {
                        tokenLength = value + 2;
                    }
                    else
                    {
                        tokenLength = 8 + (1 << (value - 7)) + (int)GetBitsLSB(value - 7);
                    }

                    if (tokenLength == 519)
                    {
                        break; // End of stream signal
                    }


                    value = HuffmanLookup(DclHuffmanTrees.DistanceTree);
                    int tokenOffset;

                    if (tokenLength == 2)
                    {
                        tokenOffset = (value << 2) | (int)GetBitsLSB(2);
                    }
                    else
                    {
                        tokenOffset = (value << dictionaryType) | (int)GetBitsLSB(dictionaryType);
                    }
                    tokenOffset++;

                    if (_targetFixedSize && tokenLength + _bytesWritten > _targetSize)
                    {
                        Debug.WriteLine($"DCL-IMPLODE Error: Write out of bounds while copying {tokenLength} bytes.");
                        return false;
                    }

                    if (_bytesWritten < tokenOffset)
                    {
                        Debug.WriteLine($"DCL-IMPLODE Error: Attempt to copy from before beginning of input stream. (Bytes Written: {_bytesWritten}, Token Offset: {tokenOffset})");
                        return false;
                    }

                    int dictionaryBaseIndex = (dictionaryPos - tokenOffset) & dictionaryMask;
                    int dictionaryIndex = dictionaryBaseIndex;
                    int dictionaryNextIndex = dictionaryPos;

                    while (tokenLength > 0)
                    {
                        byte b = dictionary[dictionaryIndex];
                        PutByte(b);
                        
                        dictionary[dictionaryNextIndex] = b;

                        dictionaryNextIndex = (dictionaryNextIndex + 1) & dictionaryMask;
                        dictionaryIndex = (dictionaryIndex + 1) & dictionaryMask;

                        if (dictionaryIndex == dictionaryPos)
                        {
                            dictionaryIndex = dictionaryBaseIndex;
                        }
                        if (dictionaryNextIndex == dictionarySize)
                        {
                            dictionaryNextIndex = 0;
                        }

                        tokenLength--;
                    }
                    
                    dictionaryPos = (ushort)dictionaryNextIndex;
                }
                else // Copy byte verbatim
                {
                    int value = (mode == DCL_ASCII_MODE) ? HuffmanLookup(DclHuffmanTrees.AsciiTree) : GetByteLSB();

                    PutByte((byte)value);

                    dictionary[dictionaryPos] = (byte)value;
                    dictionaryPos++;
                    if (dictionaryPos >= dictionarySize)
                    {
                        dictionaryPos = 0;
                    }
                }
            }

            if (_targetFixedSize && _bytesWritten != _targetSize)
            {
                Debug.WriteLine($"DCL-IMPLODE Error: Inconsistent bytes written ({_bytesWritten}) and target buffer size ({_targetSize})");
                return false;
            }

            return true;
        }
    }
    internal static class DclHuffmanTrees
    {
        private const int HUFFMAN_LEAF = 0x40000000;

        public static readonly int[] LengthTree = {
            ((1 << 12) | (2)),
            ((3 << 12) | (4)),     ((5 << 12) | (6)),
            ((7 << 12) | (8)),     ((9 << 12) | (10)),    ((11 << 12) | (12)),  ((1) | HUFFMAN_LEAF),
            ((13 << 12) | (14)),   ((15 << 12) | (16)),   ((17 << 12) | (18)),  ((3) | HUFFMAN_LEAF),  ((2) | HUFFMAN_LEAF),  ((0) | HUFFMAN_LEAF),
            ((19 << 12) | (20)),  ((21 << 12) | (22)),  ((23 << 12) | (24)), ((6) | HUFFMAN_LEAF),  ((5) | HUFFMAN_LEAF),  ((4) | HUFFMAN_LEAF),
            ((25 << 12) | (26)),  ((27 << 12) | (28)),  ((10) | HUFFMAN_LEAF),     ((9) | HUFFMAN_LEAF),  ((8) | HUFFMAN_LEAF),  ((7) | HUFFMAN_LEAF),
            ((29 << 12) | (30)),  ((13) | HUFFMAN_LEAF),      ((12) | HUFFMAN_LEAF),     ((11) | HUFFMAN_LEAF),
            ((15) | HUFFMAN_LEAF),      ((14) | HUFFMAN_LEAF)
        };

        public static readonly int[] DistanceTree = {
            ((1 << 12) | (2)),
            ((3 << 12) | (4)),       ((5 << 12) | (6)),
            ((7 << 12) | (8)),       ((9 << 12) | (10)),      ((11 << 12) | (12)),     ((0) | HUFFMAN_LEAF),
            ((13 << 12) | (14)),     ((15 << 12) | (16)),     ((17 << 12) | (18)),     ((19 << 12) | (20)),
            ((21 << 12) | (22)),    ((23 << 12) | (24)),
            ((25 << 12) | (26)),    ((27 << 12) | (28)),    ((29 << 12) | (30)),    ((31 << 12) | (32)),
            ((33 << 12) | (34)),    ((35 << 12) | (36)),    ((37 << 12) | (38)),    ((39 << 12) | (40)),
            ((41 << 12) | (42)),    ((43 << 12) | (44)),    ((2) | HUFFMAN_LEAF),         ((1) | HUFFMAN_LEAF),
            ((45 << 12) | (46)),    ((47 << 12) | (48)),    ((49 << 12) | (50)),    ((51 << 12) | (52)),
            ((53 << 12) | (54)),    ((55 << 12) | (56)),    ((57 << 12) | (58)),    ((59 << 12) | (60)),
            ((61 << 12) | (62)),      ((63 << 12) | (64)),    ((65 << 12) | (66)),    ((67 << 12) | (68)),
            ((69 << 12) | (70)),    ((71 << 12) | (72)),    ((73 << 12) | (74)),    ((75 << 12) | (76)),
            ((6) | HUFFMAN_LEAF),         ((5) | HUFFMAN_LEAF),         ((4) | HUFFMAN_LEAF),         ((3) | HUFFMAN_LEAF),
            ((77 << 12) | (78)),    ((79 << 12) | (80)),    ((81 << 12) | (82)),    ((83 << 12) | (84)),
            ((85 << 12) | (86)),    ((87 << 12) | (88)),    ((89 << 12) | (90)),    ((91 << 12) | (92)),
            ((93 << 12) | (94)),    ((95 << 12) | (96)),    ((97 << 12) | (98)),    ((99 << 12) | (100)),
            ((101 << 12) | (102)),  ((103 << 12) | (104)),  ((105 << 12) | (106)),  ((107 << 12) | (108)),
            ((109 << 12) | (110)),  ((21) | HUFFMAN_LEAF),        ((20) | HUFFMAN_LEAF),        ((19) | HUFFMAN_LEAF),
            ((18) | HUFFMAN_LEAF),        ((17) | HUFFMAN_LEAF),        ((16) | HUFFMAN_LEAF),        ((15) | HUFFMAN_LEAF),
            ((14) | HUFFMAN_LEAF),        ((13) | HUFFMAN_LEAF),        ((12) | HUFFMAN_LEAF),        ((11) | HUFFMAN_LEAF),
            ((10) | HUFFMAN_LEAF),        ((9) | HUFFMAN_LEAF),         ((8) | HUFFMAN_LEAF),         ((7) | HUFFMAN_LEAF),
            ((111 << 12) | (112)),  ((113 << 12) | (114)),  ((115 << 12) | (116)),  ((117 << 12) | (118)),
            ((119 << 12) | (120)),  ((121 << 12) | (122)),  ((123 << 12) | (124)),  ((125 << 12) | (126)),
            ((47) | HUFFMAN_LEAF),        ((46) | HUFFMAN_LEAF),        ((45) | HUFFMAN_LEAF),        ((44) | HUFFMAN_LEAF),
            ((43) | HUFFMAN_LEAF),        ((42) | HUFFMAN_LEAF),        ((41) | HUFFMAN_LEAF),        ((40) | HUFFMAN_LEAF),
            ((39) | HUFFMAN_LEAF),        ((38) | HUFFMAN_LEAF),        ((37) | HUFFMAN_LEAF),        ((36) | HUFFMAN_LEAF),
            ((35) | HUFFMAN_LEAF),        ((34) | HUFFMAN_LEAF),        ((33) | HUFFMAN_LEAF),        ((32) | HUFFMAN_LEAF),
            ((31) | HUFFMAN_LEAF),       ((30) | HUFFMAN_LEAF),       ((29) | HUFFMAN_LEAF),       ((28) | HUFFMAN_LEAF),
            ((27) | HUFFMAN_LEAF),       ((26) | HUFFMAN_LEAF),       ((25) | HUFFMAN_LEAF),       ((24) | HUFFMAN_LEAF),
            ((23) | HUFFMAN_LEAF),       ((22) | HUFFMAN_LEAF),       ((63) | HUFFMAN_LEAF),       ((62) | HUFFMAN_LEAF),
            ((61) | HUFFMAN_LEAF),       ((60) | HUFFMAN_LEAF),       ((59) | HUFFMAN_LEAF),       ((58) | HUFFMAN_LEAF),
            ((57) | HUFFMAN_LEAF),       ((56) | HUFFMAN_LEAF),       ((55) | HUFFMAN_LEAF),       ((54) | HUFFMAN_LEAF),
            ((53) | HUFFMAN_LEAF),       ((52) | HUFFMAN_LEAF),       ((51) | HUFFMAN_LEAF),       ((50) | HUFFMAN_LEAF),
            ((49) | HUFFMAN_LEAF),       ((48) | HUFFMAN_LEAF)
        };

        public static readonly int[] AsciiTree = {
            ((1 << 12) | (2)),       ((3 << 12) | (4)),       ((5 << 12) | (6)),       ((7 << 12) | (8)),
            ((9 << 12) | (10)),      ((11 << 12) | (12)),     ((13 << 12) | (14)),     ((15 << 12) | (16)),
            ((17 << 12) | (18)),     ((19 << 12) | (20)),     ((21 << 12) | (22)),    ((23 << 12) | (24)),
            ((25 << 12) | (26)),    ((27 << 12) | (28)),    ((29 << 12) | (30)),    ((31 << 12) | (32)),
            ((33 << 12) | (34)),    ((35 << 12) | (36)),    ((37 << 12) | (38)),    ((39 << 12) | (40)),
            ((41 << 12) | (42)),    ((43 << 12) | (44)),    ((45 << 12) | (46)),    ((47 << 12) | (48)),
            ((49 << 12) | (50)),    ((51 << 12) | (52)),    ((53 << 12) | (54)),    ((55 << 12) | (56)),
            ((57 << 12) | (58)),    ((59 << 12) | (60)),    ((32) | HUFFMAN_LEAF),
            ((61 << 12) | (62)),    ((63 << 12) | (64)),    ((65 << 12) | (66)),    ((67 << 12) | (68)),
            ((69 << 12) | (70)),    ((71 << 12) | (72)),    ((73 << 12) | (74)),    ((75 << 12) | (76)),
            ((77 << 12) | (78)),    ((79 << 12) | (80)),    ((81 << 12) | (82)),    ((83 << 12) | (84)),
            ((85 << 12) | (86)),    ((87 << 12) | (88)),    ((89 << 12) | (90)),    ((91 << 12) | (92)),
            ((93 << 12) | (94)),    ((95 << 12) | (96)),    ((97 << 12) | (98)),    ((117) | HUFFMAN_LEAF),
            ((116) | HUFFMAN_LEAF),       ((115) | HUFFMAN_LEAF),       ((114) | HUFFMAN_LEAF),       ((111) | HUFFMAN_LEAF),
            ((110) | HUFFMAN_LEAF),       ((108) | HUFFMAN_LEAF),       ((105) | HUFFMAN_LEAF),       ((101) | HUFFMAN_LEAF),
            ((97) | HUFFMAN_LEAF),        ((69) | HUFFMAN_LEAF),
            ((99 << 12) | (100)),   ((101 << 12) | (102)),  ((103 << 12) | (104)),  ((105 << 12) | (106)),
            ((107 << 12) | (108)),  ((109 << 12) | (110)),    ((111 << 12) | (112)),  ((113 << 12) | (114)),
            ((115 << 12) | (116)),  ((117 << 12) | (118)),  ((119 << 12) | (120)),  ((121 << 12) | (122)),
            ((123 << 12) | (124)),  ((125 << 12) | (126)),  ((127 << 12) | (128)),  ((129 << 12) | (130)),
            ((131 << 12) | (132)),  ((133 << 12) | (134)),  ((112) | HUFFMAN_LEAF),       ((109) | HUFFMAN_LEAF),
            ((104) | HUFFMAN_LEAF),       ((103) | HUFFMAN_LEAF),       ((102) | HUFFMAN_LEAF),       ((100) | HUFFMAN_LEAF),
            ((99) | HUFFMAN_LEAF),        ((98) | HUFFMAN_LEAF),        ((84) | HUFFMAN_LEAF),        ((83) | HUFFMAN_LEAF),
            ((82) | HUFFMAN_LEAF),        ((79) | HUFFMAN_LEAF),        ((78) | HUFFMAN_LEAF),        ((76) | HUFFMAN_LEAF),
            ((73) | HUFFMAN_LEAF),        ((68) | HUFFMAN_LEAF),        ((67) | HUFFMAN_LEAF),        ((65) | HUFFMAN_LEAF),
            ((49) | HUFFMAN_LEAF),        ((45) | HUFFMAN_LEAF),
            ((135 << 12) | (136)),  ((137 << 12) | (138)), ((139 << 12) | (140)), ((141 << 12) | (142)),
            ((143 << 12) | (144)), ((145 << 12) | (146)),    ((147 << 12) | (148)), ((149 << 12) | (150)),
            ((151 << 12) | (152)), ((153 << 12) | (154)), ((155 << 12) | (156)), ((157 << 12) | (158)),
            ((159 << 12) | (160)), ((161 << 12) | (162)), ((163 << 12) | (164)), ((119) | HUFFMAN_LEAF),
            ((107) | HUFFMAN_LEAF),      ((85) | HUFFMAN_LEAF),       ((80) | HUFFMAN_LEAF),       ((77) | HUFFMAN_LEAF),
            ((70) | HUFFMAN_LEAF),       ((66) | HUFFMAN_LEAF),       ((61) | HUFFMAN_LEAF),       ((56) | HUFFMAN_LEAF),
            ((55) | HUFFMAN_LEAF),       ((53) | HUFFMAN_LEAF),       ((52) | HUFFMAN_LEAF),       ((51) | HUFFMAN_LEAF),
            ((50) | HUFFMAN_LEAF),       ((48) | HUFFMAN_LEAF),       ((46) | HUFFMAN_LEAF),       ((44) | HUFFMAN_LEAF),
            ((41) | HUFFMAN_LEAF),       ((40) | HUFFMAN_LEAF),       ((13) | HUFFMAN_LEAF),       ((10) | HUFFMAN_LEAF),
            ((165 << 12) | (166)), ((167 << 12) | (168)), ((169 << 12) | (170)), ((171 << 12) | (172)),
            ((173 << 12) | (174)), ((175 << 12) | (176)), ((177 << 12) | (178)), ((179 << 12) | (180)),
            ((181 << 12) | (182)), ((183 << 12) | (184)), ((185 << 12) | (186)), ((187 << 12) | (188)),
            ((189 << 12) | (190)), ((191 << 12) | (192)), ((121) | HUFFMAN_LEAF),      ((120) | HUFFMAN_LEAF),
            ((118) | HUFFMAN_LEAF),      ((95) | HUFFMAN_LEAF),       ((91) | HUFFMAN_LEAF),       ((87) | HUFFMAN_LEAF),
            ((72) | HUFFMAN_LEAF),       ((71) | HUFFMAN_LEAF),       ((58) | HUFFMAN_LEAF),       ((57) | HUFFMAN_LEAF),
            ((54) | HUFFMAN_LEAF),       ((47) | HUFFMAN_LEAF),       ((42) | HUFFMAN_LEAF),       ((39) | HUFFMAN_LEAF),
            ((34) | HUFFMAN_LEAF),       ((9) | HUFFMAN_LEAF),
            ((193 << 12) | (194)), ((195 << 12) | (196)), ((197 << 12) | (198)), ((199 << 12) | (200)),
            ((201 << 12) | (202)), ((203 << 12) | (204)), ((205 << 12) | (206)), ((207 << 12) | (208)),
            ((209 << 12) | (210)), ((211 << 12) | (212)), ((213 << 12) | (214)), ((215 << 12) | (216)),
            ((217 << 12) | (218)), ((219 << 12) | (220)), ((221 << 12) | (222)), ((223 << 12) | (224)),
            ((225 << 12) | (226)), ((227 << 12) | (228)),    ((229 << 12) | (230)), ((231 << 12) | (232)),
            ((233 << 12) | (234)), ((93) | HUFFMAN_LEAF),       ((89) | HUFFMAN_LEAF),       ((88) | HUFFMAN_LEAF),
            ((86) | HUFFMAN_LEAF),       ((75) | HUFFMAN_LEAF),       ((62) | HUFFMAN_LEAF),       ((43) | HUFFMAN_LEAF),
            ((235 << 12) | (236)), ((237 << 12) | (238)), ((239 << 12) | (240)), ((241 << 12) | (242)),
            ((243 << 12) | (244)), ((245 << 12) | (246)),    ((247 << 12) | (248)), ((249 << 12) | (250)),
            ((251 << 12) | (252)), ((253 << 12) | (254)), ((255 << 12) | (256)), ((257 << 12) | (258)),
            ((259 << 12) | (260)), ((261 << 12) | (262)), ((263 << 12) | (264)), ((265 << 12) | (266)),
            ((267 << 12) | (268)), ((269 << 12) | (270)),    ((271 << 12) | (272)), ((273 << 12) | (274)),
            ((275 << 12) | (276)), ((277 << 12) | (278)), ((279 << 12) | (280)), ((281 << 12) | (282)),
            ((283 << 12) | (284)), ((285 << 12) | (286)), ((287 << 12) | (288)), ((289 << 12) | (290)),
            ((291 << 12) | (292)), ((293 << 12) | (294)), ((295 << 12) | (296)), ((297 << 12) | (298)),
            ((299 << 12) | (300)), ((301 << 12) | (302)), ((303 << 12) | (304)), ((305 << 12) | (306)),
            ((307 << 12) | (308)), ((122) | HUFFMAN_LEAF),      ((113) | HUFFMAN_LEAF),      ((38) | HUFFMAN_LEAF),
            ((36) | HUFFMAN_LEAF),       ((33) | HUFFMAN_LEAF),
            ((309 << 12) | (310)), ((311 << 12) | (312)), ((313 << 12) | (314)), ((315 << 12) | (316)),
            ((317 << 12) | (318)), ((319 << 12) | (320)), ((321 << 12) | (322)), ((323 << 12) | (324)),
            ((325 << 12) | (326)), ((327 << 12) | (328)), ((329 << 12) | (330)), ((331 << 12) | (332)),
            ((333 << 12) | (334)), ((335 << 12) | (336)), ((337 << 12) | (338)), ((339 << 12) | (340)),
            ((341 << 12) | (342)), ((343 << 12) | (344)),    ((345 << 12) | (346)), ((347 << 12) | (348)),
            ((349 << 12) | (350)), ((351 << 12) | (352)), ((353 << 12) | (354)), ((355 << 12) | (356)),
            ((357 << 12) | (358)), ((359 << 12) | (360)), ((361 << 12) | (362)), ((363 << 12) | (364)),
            ((365 << 12) | (366)), ((367 << 12) | (368)), ((369 << 12) | (370)), ((371 << 12) | (372)),
            ((373 << 12) | (374)), ((375 << 12) | (376)), ((377 << 12) | (378)), ((379 << 12) | (380)),
            ((381 << 12) | (382)), ((383 << 12) | (384)), ((385 << 12) | (386)), ((387 << 12) | (388)),
            ((389 << 12) | (390)), ((391 << 12) | (392)), ((393 << 12) | (394)), ((395 << 12) | (396)),
            ((397 << 12) | (398)), ((399 << 12) | (400)), ((401 << 12) | (402)), ((403 << 12) | (404)),
            ((405 << 12) | (406)), ((407 << 12) | (408)), ((409 << 12) | (410)), ((411 << 12) | (412)),
            ((413 << 12) | (414)), ((415 << 12) | (416)), ((417 << 12) | (418)), ((419 << 12) | (420)),
            ((421 << 12) | (422)), ((423 << 12) | (424)), ((425 << 12) | (426)), ((427 << 12) | (428)),
            ((429 << 12) | (430)), ((431 << 12) | (432)), ((433 << 12) | (434)), ((435 << 12) | (436)),
            ((124) | HUFFMAN_LEAF),      ((123) | HUFFMAN_LEAF),      ((106) | HUFFMAN_LEAF),      ((92) | HUFFMAN_LEAF),
            ((90) | HUFFMAN_LEAF),       ((81) | HUFFMAN_LEAF),       ((74) | HUFFMAN_LEAF),       ((63) | HUFFMAN_LEAF),
            ((60) | HUFFMAN_LEAF),       ((0) | HUFFMAN_LEAF),
            ((437 << 12) | (438)), ((439 << 12) | (440)), ((441 << 12) | (442)), ((443 << 12) | (444)),
            ((445 << 12) | (446)), ((447 << 12) | (448)), ((449 << 12) | (450)), ((451 << 12) | (452)),
            ((453 << 12) | (454)), ((455 << 12) | (456)), ((457 << 12) | (458)), ((459 << 12) | (460)),
            ((461 << 12) | (462)), ((463 << 12) | (464)), ((465 << 12) | (466)), ((467 << 12) | (468)),
            ((469 << 12) | (470)), ((471 << 12) | (472)),    ((473 << 12) | (474)), ((475 << 12) | (476)),
            ((477 << 12) | (478)), ((479 << 12) | (480)), ((481 << 12) | (482)), ((483 << 12) | (484)),
            ((485 << 12) | (486)), ((487 << 12) | (488)), ((489 << 12) | (490)), ((491 << 12) | (492)),
            ((493 << 12) | (494)), ((495 << 12) | (496)), ((497 << 12) | (498)), ((499 << 12) | (500)),
            ((501 << 12) | (502)), ((503 << 12) | (504)), ((505 << 12) | (506)), ((507 << 12) | (508)),
            ((509 << 12) | (510)), ((244) | HUFFMAN_LEAF),      ((243) | HUFFMAN_LEAF),      ((242) | HUFFMAN_LEAF),
            ((238) | HUFFMAN_LEAF),      ((233) | HUFFMAN_LEAF),      ((229) | HUFFMAN_LEAF),      ((225) | HUFFMAN_LEAF),
            ((223) | HUFFMAN_LEAF),      ((222) | HUFFMAN_LEAF),      ((221) | HUFFMAN_LEAF),      ((220) | HUFFMAN_LEAF),
            ((219) | HUFFMAN_LEAF),      ((218) | HUFFMAN_LEAF),      ((217) | HUFFMAN_LEAF),      ((216) | HUFFMAN_LEAF),
            ((215) | HUFFMAN_LEAF),      ((214) | HUFFMAN_LEAF),      ((213) | HUFFMAN_LEAF),      ((212) | HUFFMAN_LEAF),
            ((211) | HUFFMAN_LEAF),      ((210) | HUFFMAN_LEAF),      ((209) | HUFFMAN_LEAF),      ((208) | HUFFMAN_LEAF),
            ((207) | HUFFMAN_LEAF),      ((206) | HUFFMAN_LEAF),      ((205) | HUFFMAN_LEAF),      ((204) | HUFFMAN_LEAF),
            ((203) | HUFFMAN_LEAF),      ((202) | HUFFMAN_LEAF),      ((201) | HUFFMAN_LEAF),      ((200) | HUFFMAN_LEAF),
            ((199) | HUFFMAN_LEAF),      ((198) | HUFFMAN_LEAF),      ((197) | HUFFMAN_LEAF),      ((196) | HUFFMAN_LEAF),
            ((195) | HUFFMAN_LEAF),      ((194) | HUFFMAN_LEAF),      ((193) | HUFFMAN_LEAF),      ((192) | HUFFMAN_LEAF),
            ((191) | HUFFMAN_LEAF),      ((190) | HUFFMAN_LEAF),      ((189) | HUFFMAN_LEAF),      ((188) | HUFFMAN_LEAF),
            ((187) | HUFFMAN_LEAF),      ((186) | HUFFMAN_LEAF),      ((185) | HUFFMAN_LEAF),      ((184) | HUFFMAN_LEAF),
            ((183) | HUFFMAN_LEAF),      ((182) | HUFFMAN_LEAF),      ((181) | HUFFMAN_LEAF),      ((180) | HUFFMAN_LEAF),
            ((179) | HUFFMAN_LEAF),      ((178) | HUFFMAN_LEAF),      ((177) | HUFFMAN_LEAF),      ((176) | HUFFMAN_LEAF),
            ((127) | HUFFMAN_LEAF),      ((126) | HUFFMAN_LEAF),      ((125) | HUFFMAN_LEAF),      ((96) | HUFFMAN_LEAF),
            ((94) | HUFFMAN_LEAF),       ((64) | HUFFMAN_LEAF),       ((59) | HUFFMAN_LEAF),       ((37) | HUFFMAN_LEAF),
            ((35) | HUFFMAN_LEAF),       ((31) | HUFFMAN_LEAF),       ((30) | HUFFMAN_LEAF),       ((29) | HUFFMAN_LEAF),
            ((28) | HUFFMAN_LEAF),       ((27) | HUFFMAN_LEAF),       ((25) | HUFFMAN_LEAF),       ((24) | HUFFMAN_LEAF),
            ((23) | HUFFMAN_LEAF),       ((22) | HUFFMAN_LEAF),       ((21) | HUFFMAN_LEAF),       ((20) | HUFFMAN_LEAF),
            ((19) | HUFFMAN_LEAF),       ((18) | HUFFMAN_LEAF),       ((17) | HUFFMAN_LEAF),       ((16) | HUFFMAN_LEAF),
            ((15) | HUFFMAN_LEAF),       ((14) | HUFFMAN_LEAF),       ((12) | HUFFMAN_LEAF),       ((11) | HUFFMAN_LEAF),
            ((8) | HUFFMAN_LEAF),        ((7) | HUFFMAN_LEAF),        ((6) | HUFFMAN_LEAF),        ((5) | HUFFMAN_LEAF),
            ((4) | HUFFMAN_LEAF),        ((3) | HUFFMAN_LEAF),        ((2) | HUFFMAN_LEAF),        ((1) | HUFFMAN_LEAF),
            ((255) | HUFFMAN_LEAF),      ((254) | HUFFMAN_LEAF),      ((253) | HUFFMAN_LEAF),      ((252) | HUFFMAN_LEAF),
            ((251) | HUFFMAN_LEAF),      ((250) | HUFFMAN_LEAF),      ((249) | HUFFMAN_LEAF),      ((248) | HUFFMAN_LEAF),
            ((247) | HUFFMAN_LEAF),      ((246) | HUFFMAN_LEAF),      ((245) | HUFFMAN_LEAF),      ((241) | HUFFMAN_LEAF),
            ((240) | HUFFMAN_LEAF),      ((239) | HUFFMAN_LEAF),      ((237) | HUFFMAN_LEAF),      ((236) | HUFFMAN_LEAF),
            ((235) | HUFFMAN_LEAF),      ((234) | HUFFMAN_LEAF),      ((232) | HUFFMAN_LEAF),      ((231) | HUFFMAN_LEAF),
            ((230) | HUFFMAN_LEAF),      ((228) | HUFFMAN_LEAF),      ((227) | HUFFMAN_LEAF),      ((226) | HUFFMAN_LEAF),
            ((224) | HUFFMAN_LEAF),      ((175) | HUFFMAN_LEAF),      ((174) | HUFFMAN_LEAF),      ((173) | HUFFMAN_LEAF),
            ((172) | HUFFMAN_LEAF),      ((171) | HUFFMAN_LEAF),      ((170) | HUFFMAN_LEAF),      ((169) | HUFFMAN_LEAF),
            ((168) | HUFFMAN_LEAF),      ((167) | HUFFMAN_LEAF),      ((166) | HUFFMAN_LEAF),      ((165) | HUFFMAN_LEAF),
            ((164) | HUFFMAN_LEAF),      ((163) | HUFFMAN_LEAF),      ((162) | HUFFMAN_LEAF),      ((161) | HUFFMAN_LEAF),
            ((160) | HUFFMAN_LEAF),      ((159) | HUFFMAN_LEAF),      ((158) | HUFFMAN_LEAF),      ((157) | HUFFMAN_LEAF),
            ((156) | HUFFMAN_LEAF),      ((155) | HUFFMAN_LEAF),      ((154) | HUFFMAN_LEAF),      ((153) | HUFFMAN_LEAF),
            ((152) | HUFFMAN_LEAF),      ((151) | HUFFMAN_LEAF),      ((150) | HUFFMAN_LEAF),      ((149) | HUFFMAN_LEAF),
            ((148) | HUFFMAN_LEAF),      ((147) | HUFFMAN_LEAF),      ((146) | HUFFMAN_LEAF),      ((145) | HUFFMAN_LEAF),
            ((144) | HUFFMAN_LEAF),      ((143) | HUFFMAN_LEAF),      ((142) | HUFFMAN_LEAF),      ((141) | HUFFMAN_LEAF),
            ((140) | HUFFMAN_LEAF),      ((139) | HUFFMAN_LEAF),      ((138) | HUFFMAN_LEAF),      ((137) | HUFFMAN_LEAF),
            ((136) | HUFFMAN_LEAF),      ((135) | HUFFMAN_LEAF),      ((134) | HUFFMAN_LEAF),      ((133) | HUFFMAN_LEAF),
            ((132) | HUFFMAN_LEAF),      ((131) | HUFFMAN_LEAF),      ((130) | HUFFMAN_LEAF),      ((129) | HUFFMAN_LEAF),
            ((128) | HUFFMAN_LEAF),      ((26) | HUFFMAN_LEAF)
        };
    }
}
