## BMP Table Structure

### V0 - Magic Tales Series

{
  "offset": uint32, // offset of the bmp data + headerSize
  "size": uint32, // size of the bmp data
  "unknown": uint32, // unknown, possibly an id and/or flags, could be two 16-bit values
}

### V1 - Darby The Dragon

{
  "offset": uint32, // offset of the bmp data + headerSize
  "size": uint32, // size of the bmp data
  "unknown1": uint32, // unknown, possibly an id and/or flags, could be two 16-bit values
  "unknown2": uint32, // unknown, possibly an id and/or flags, could be two 16-bit values
}

### V2 - Jumpstart Math

{
  "offset": uint32, // offset of the bmp data + 0x10
  "size": uint32, // size of the bmp data
  "padding": uint32, // padding?, always 0
  "unknown": uint32, // unknown, possibly an id and/or flags
  "padding": uint32, // padding?, always 0
}
