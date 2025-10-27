## RLE

Header {
  uint: imageFlag; // 0x01
  uint: compressionFlag; // 0x01 == compressed
  uint: width;
  uint: height;
  if (compressed) {
    uint: compressedSize; // size of compressed data
    uint: non-empty-lines; // number of non-empty lines in the image?
  } else {
    byte[]: data; // uncompressed data (width * height bytes)
  }  
}
