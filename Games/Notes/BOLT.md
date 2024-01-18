# Bolt Files

## Known Values

@0x0 - 0x42 0x4F 0x4C 0x54 = BOLT

@0xC - 4 byte value representing eof

0x18 - 4? byte value representing start of data? 0xC70

0x20 - Start of 16 byte blocks 

Block1: 00 00 00 2D ->  2d0
        00 00 F1 8E     f18e 
        00 00 0E B2     eb2
        00 00 00 00     =10310

Block2: 00 00 00 A2 ->  a20
        00 01 E2 0E     1e20e
        00 01 03 10     10310 <-- total of block1
        00 00 00 00     =2ef3e
