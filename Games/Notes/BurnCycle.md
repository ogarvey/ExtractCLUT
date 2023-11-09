BurnCycle.rtr_v_CLUT7_Normal_1529 

"CLUT" File initial 4 bytes = 0180 00F0 = 384 240
4 bytes @ 0x08 = 2308 = 8968 - Unknown
4 bytes @ 0x0C = 001E = 30 (Size of chunks starting at 0x55E)
4 bytes @ 0x10 = 0003 = 3 - Possibly file number
4 bytes @ 0x18 = 00080 = 128 - unknown

Searching for 018000F0 returns 14 hits

0x00
0x63DC
0xDDD8C
0x1A6220
0x1DE4DC
0x231B58
0x2ECF20
0x33C610
0x3F15FC
0x577BBC
0xA4A5BC
0XB2DE10
0xC83718
0x1080BB8

Trigger (T) : This bit is used to synchronize the application with various
coding information, like visuals to audio, in real time. The
bit when set to one generates an interrupt (see VII.4). 

If a trigger bit and an EOR bit are set in the same sector, and the sector was
not selected via the channel mask, the EOR bit will be reset by the CD driver
so that it is not acted upon by the driver or the application.

More complicated sychronization methods are possible using an event and/or
signals (e.g. software interrupts). Events are similar to semaphores and are
described completely in the CD-RTOS technical manual (Appendix VII.1). The use
of signals are covered here. Signals may be generated for a process in any of the
following ways:

- The presence of a trigger (T), end of record (EOR), or end of file (EOF) bit in the
subheader submode byte of a real-time sector (see SS_Play in VII.2.2.3.2).
Note: (1) The EOF and T bits should generate interrupts only after file number
selection.
(2) The EOR bit should generate an interrupt only after the file and channel
number selection.

The display of each successive image may be initiated by completion of loading the
image from disc, if the images are spaced at regular intervals on the disc. If not, e.g. for runlength images, another scheme, such as regularly spaced trigger bits on the
disc, must be used.

By placing a trigger bit in every 5th sector, the application can be notified when it is time to display a new image. 

If a single runlength picture is greater in size than the allowed four sectors, it might have to be put into a different channel, thus allowing the application to put one or more of the sectors into an empty space where another image used less than 4
sectors.

### EOR Split

When split by EOR file 5 contains logo, back to back frames
file 6 contains philips logo starting at offset 0x478c has frame and a half in 6972 bytes (3 sectors)

Offset 2 - Offset 1 = 0xAC7C

Palette indicated by 0x8080 at around 0x40-0x50
