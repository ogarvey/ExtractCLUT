# Link - The Faces Of Evil

## Images (Backgrounds)

Images can be found by searching for the IDAT header bytes - 0x49 0x44 0x41 0x54

### ldata.rtr

Generally, the 4 bytes following this, e.g `0x00 0x04 0x38 0x00` (decimal 1080) indicate the width of the image. But to get the actual size, we need to add to this `24 * c`, where c is equal to `image_width / 360`. Annoyingly, the first image claims to have a size of 1210, but actually has a width of 704, which clearly doesn't fit this pattern.

#### ldata main image 1:
![Alt text](Resources/LinkTFOE/Images/ldata/ldata_2048.png)

The image itself, can then be found at an offset of 0x67c from the start of the IDAT header. Any further images which share the palette will follow the previous one after a a gap of 0x678 * 0x00 bytes but do not share the dimensions of the initial image.

In the case of ldata.rtr, following the main image which contains the map screen, are a series of images for the map labels, these as mentioned do not share the original images' width of 704, and thus, require some manual tweaking to get the correct result.

As you can see below, only the first label image has the correct size, in this instance the label has a width of 136 pixels.

#### ldata label images:
![Alt text](Resources/LinkTFOE/Images/ldata/ldata_313464.png)

ldata label image sizes | 
---------|
84|
92|
96|
100|
104|
112|
120|
124|
132|
136|
148|
156|

#### ldata main image 2:
![Alt text](Resources/LinkTFOE/Images/ldata/ldata_76C08.png)


## Palettes

Palettes can be found in lanim.rtr, and ldata.rtr. Again, these can be found by searching for the IDAT header bytes, but this time, the palette will be the 0x180 bytes preceding the start of the IDAT header.

Also in ldata.rtr are some CLUT palettes, which can be found by searching for 0xC3 0x00 0x00 and one of (0x00, 0x01,0x02,0x03) and taking the following 0x100 bytes

### lanim.rtr

Contains 7 unindexed palettes, 3 of which appear to be duplicates.

![Palette: 1](Resources/LinkTFOE/Images/lanim/165109868.png)
![Palette: 2](Resources/LinkTFOE/Images/lanim/165118888.png)
![Palette: 3](Resources/LinkTFOE/Images/lanim/165127908.png)

![Palette: 4](Resources/LinkTFOE/Images/lanim/198013188.png)
![Palette: 5](Resources/LinkTFOE/Images/lanim/198108196.png)
![Palette: 6](Resources/LinkTFOE/Images/lanim/211956636.png)

![Palette: 7](Resources/LinkTFOE/Images/lanim/212051644.png)

### ldata.rtr

Contains 6 indexed palettes, which need to be grouped in pairs to form 3 palettes

![Alt text](Resources/LinkTFOE/Images/ldata/404104.png)
![Alt text](Resources/LinkTFOE/Images/ldata/404624.png)
![Alt text](Resources/LinkTFOE/Images/ldata/406152.png)

Contains 72 unindexed palettes, of which there again appear to be some duplicates

![Alt text](Resources/LinkTFOE/Images/ldata/0.png)
![Alt text](Resources/LinkTFOE/Images/ldata/486020.png)
![Alt text](Resources/LinkTFOE/Images/ldata/901172.png)
![Alt text](Resources/LinkTFOE/Images/ldata/1346144.png)

![Alt text](Resources/LinkTFOE/Images/ldata/1764608.png)
![Alt text](Resources/LinkTFOE/Images/ldata/2202260.png)
![Alt text](Resources/LinkTFOE/Images/ldata/2651924.png)
![Alt text](Resources/LinkTFOE/Images/ldata/3079640.png)

![Alt text](Resources/LinkTFOE/Images/ldata/3510392.png)
![Alt text](Resources/LinkTFOE/Images/ldata/3938384.png)
![Alt text](Resources/LinkTFOE/Images/ldata/4354640.png)
![Alt text](Resources/LinkTFOE/Images/ldata/4787876.png)

![Alt text](Resources/LinkTFOE/Images/ldata/5221112.png)
![Alt text](Resources/LinkTFOE/Images/ldata/5668292.png)
![Alt text](Resources/LinkTFOE/Images/ldata/6083996.png)
![Alt text](Resources/LinkTFOE/Images/ldata/6528620.png)

![Alt text](Resources/LinkTFOE/Images/ldata/6965792.png)
![Alt text](Resources/LinkTFOE/Images/ldata/7398752.png)
![Alt text](Resources/LinkTFOE/Images/ldata/7819976.png)
![Alt text](Resources/LinkTFOE/Images/ldata/8268812.png)

![Alt text](Resources/LinkTFOE/Images/ldata/8690588.png)
![Alt text](Resources/LinkTFOE/Images/ldata/9126032.png)
![Alt text](Resources/LinkTFOE/Images/ldata/9576800.png)
![Alt text](Resources/LinkTFOE/Images/ldata/9997268.png)

![Alt text](Resources/LinkTFOE/Images/ldata/10434716.png)
![Alt text](Resources/LinkTFOE/Images/ldata/10863812.png)
![Alt text](Resources/LinkTFOE/Images/ldata/11284832.png)
![Alt text](Resources/LinkTFOE/Images/ldata/11717516.png)

![Alt text](Resources/LinkTFOE/Images/ldata/12156620.png)
![Alt text](Resources/LinkTFOE/Images/ldata/12582056.png)
![Alt text](Resources/LinkTFOE/Images/ldata/13013636.png)
![Alt text](Resources/LinkTFOE/Images/ldata/13442660.png)

![Alt text](Resources/LinkTFOE/Images/ldata/13892324.png)
![Alt text](Resources/LinkTFOE/Images/ldata/14321696.png)
![Alt text](Resources/LinkTFOE/Images/ldata/14749136.png)
![Alt text](Resources/LinkTFOE/Images/ldata/15169532.png)

![Alt text](Resources/LinkTFOE/Images/ldata/15603872.png)
![Alt text](Resources/LinkTFOE/Images/ldata/16047944.png)
![Alt text](Resources/LinkTFOE/Images/ldata/16485392.png)
![Alt text](Resources/LinkTFOE/Images/ldata/16915040.png)

![Alt text](Resources/LinkTFOE/Images/ldata/17340200.png)
![Alt text](Resources/LinkTFOE/Images/ldata/17772332.png)
![Alt text](Resources/LinkTFOE/Images/ldata/18208952.png)
![Alt text](Resources/LinkTFOE/Images/ldata/18628796.png)

![Alt text](Resources/LinkTFOE/Images/ldata/19075700.png)
![Alt text](Resources/LinkTFOE/Images/ldata/19508936.png)
![Alt text](Resources/LinkTFOE/Images/ldata/19935272.png)
![Alt text](Resources/LinkTFOE/Images/ldata/20353184.png)

![Alt text](Resources/LinkTFOE/Images/ldata/20783660.png)
![Alt text](Resources/LinkTFOE/Images/ldata/21215240.png)
![Alt text](Resources/LinkTFOE/Images/ldata/21645716.png)
![Alt text](Resources/LinkTFOE/Images/ldata/22082060.png)

![Alt text](Resources/LinkTFOE/Images/ldata/22522268.png)
![Alt text](Resources/LinkTFOE/Images/ldata/22939904.png)
![Alt text](Resources/LinkTFOE/Images/ldata/23388464.png)
![Alt text](Resources/LinkTFOE/Images/ldata/23814452.png)

![Alt text](Resources/LinkTFOE/Images/ldata/24249968.png)
![Alt text](Resources/LinkTFOE/Images/ldata/24672020.png)
![Alt text](Resources/LinkTFOE/Images/ldata/25117820.png)
![Alt text](Resources/LinkTFOE/Images/ldata/25542704.png)

![Alt text](Resources/LinkTFOE/Images/ldata/25975664.png)
![Alt text](Resources/LinkTFOE/Images/ldata/26411936.png)
![Alt text](Resources/LinkTFOE/Images/ldata/26838200.png)
![Alt text](Resources/LinkTFOE/Images/ldata/27286484.png)

![Alt text](Resources/LinkTFOE/Images/ldata/27703844.png)
![Alt text](Resources/LinkTFOE/Images/ldata/28153232.png)
![Alt text](Resources/LinkTFOE/Images/ldata/28587020.png)
![Alt text](Resources/LinkTFOE/Images/ldata/29015216.png)

![Alt text](Resources/LinkTFOE/Images/ldata/29447900.png)
![Alt text](Resources/LinkTFOE/Images/ldata/29884172.png)
![Alt text](Resources/LinkTFOE/Images/ldata/30312920.png)
![Alt text](Resources/LinkTFOE/Images/ldata/30746708.png)
