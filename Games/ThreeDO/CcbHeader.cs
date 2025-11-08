using System;

namespace ExtractCLUT.Games.ThreeDO
{
    /// <summary>
    /// Represents a 3DO Cel Control Block (CCB) header with all flags and settings
    /// </summary>
    public class CcbHeader
    {
        // Raw CCB fields
        public uint Flags { get; set; }
        public uint NextPtr { get; set; }
        public uint SourcePtr { get; set; }
        public uint PlutPtr { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        
        // Preamble words (if CCBPRE flag is set)
        public uint Pre0 { get; set; }
        public uint Pre1 { get; set; }

        // === Flag Properties (from FLAGS word) ===
        
        // Bit 31: SKIP - If set, skip (do not project) this CCB
        public bool Skip => (Flags & 0x80000000) != 0;
        
        // Bit 30: LAST - If set, this is the last linked CCB to project
        public bool Last => (Flags & 0x40000000) != 0;
        
        // Bit 29: NPABS - Determine absolute or relative address for NEXTPTR (1=absolute; 0=relative)
        public bool NpAbs => (Flags & 0x20000000) != 0;
        
        // Bit 28: SPABS - Determine absolute or relative address for SOURCEPTR (1=absolute; 0=relative)
        public bool SpAbs => (Flags & 0x10000000) != 0;
        
        // Bit 27: PPABS - Determine absolute or relative address for PLUTPTR (1=absolute; 0=relative)
        public bool PpAbs => (Flags & 0x08000000) != 0;
        
        // Bit 26: LDSIZE - If set, load four CCB words of offset data (HDX, HDY, VDX, VDY)
        public bool LdSize => (Flags & 0x04000000) != 0;
        
        // Bit 25: LDPRS - If set, load two CCB words of perspective offset data (HDDX, HDDY)
        public bool LdPrs => (Flags & 0x02000000) != 0;
        
        // Bit 24: LDPIXC - If set, load a new pixel-processor CCB control word (PIXC)
        public bool LdPixc => (Flags & 0x01000000) != 0;
        
        // Bit 23: LDPLUT - If set, load a new PLUT from the location specified by PLUTPTR
        public bool LdPlut => (Flags & 0x00800000) != 0;
        
        // Bit 22: CCBPRE - Determine preamble location (1=at end of CCB; 0=at start of data)
        public bool CcbPre => (Flags & 0x00400000) != 0;
        
        // Bit 21: YOXY - 1=write the CCB origin coordinates (XPOS and YPOS) to the cel engine
        public bool YoXy => (Flags & 0x00200000) != 0;
        
        // Bit 20: ACSC - Enable cel super clipping (1=on; 0=off)
        public bool AcSc => (Flags & 0x00100000) != 0;
        
        // Bit 19: ALSC - Enable line super clipping (1=on; 0=off)
        public bool AlSc => (Flags & 0x00080000) != 0;
        
        // Bit 18: ACW - Enable clockwise pixel rendering (1=render CW pixels; 0=do not render CW pixels)
        public bool Acw => (Flags & 0x00040000) != 0;
        
        // Bit 17: ACCW - Enable counterclockwise pixel rendering (1=render CCW pixels; 0=do not render CCW pixels)
        public bool Accw => (Flags & 0x00020000) != 0;
        
        // Bit 16: TWD - 1=terminate cel projection if first cel pixel is a backface pixel (CCW)
        public bool Twd => (Flags & 0x00010000) != 0;
        
        // Bit 15: LCE - 1=lock the two corner engines together
        public bool Lce => (Flags & 0x00008000) != 0;
        
        // Bit 14: ACE - 1=allow both corner engines to function
        public bool Ace => (Flags & 0x00004000) != 0;
        
        // Bit 13: Spare bit (must be 0)
        
        // Bit 12: MARIA - 1=disable regional fill (speed fill only)
        public bool Maria => (Flags & 0x00001000) != 0;
        
        // Bit 11: PXOR - 1=enable the pixel-processor XOR option
        public bool Pxor => (Flags & 0x00000800) != 0;
        
        // Bit 10: USEAV - 1=use the AV bits of the PIXC CCB word for pixel-processor math functions
        public bool UseAv => (Flags & 0x00000400) != 0;
        
        // Bit 9: PACKED - 1=packed source data; 0=unpacked source data
        public bool Packed => (Flags & 0x00000200) != 0;
        
        // Bits 8-7: POVER - Set P-mode (00=use P-mode from pixel decoder; 10=use P-mode 0; 11=use P-mode 1)
        public int POver => (int)((Flags >> 7) & 0x03);
        
        // Bit 6: PLUTPOS - Derives VH value from PLUT or CCB (1=get VH from PLUT; 0=get VH from subposition)
        public bool PlutPos => (Flags & 0x00000040) != 0;
        
        // Bit 5: BGND - Decides 000 decoder value treatment (1=pass 000 as RGB value; 0=treat as transparent)
        public bool Bgnd => (Flags & 0x00000020) != 0;
        
        // Bit 4: NOBLK - 0=write 000 pixel as 100; 1=write 000 pixel as 000
        public bool NoBlk => (Flags & 0x00000010) != 0;
        
        // Bits 3-1: PLUTA - Used to fill in high-order bits for cel pixels with less than 5 bits per pixel
        public byte Pluta => (byte)((Flags >> 1) & 0x07);

        // === Preamble Properties (from PRE0 and PRE1) ===
        
        // PRE0 Bit 7: Packed flag (when CCBPRE=0)
        public bool Pre0Packed => (Pre0 & 0x80) != 0;
        
        // PRE0 Bit 6: Linear flag (1=unpacked; 0=packed) - redundant with bit 7
        public bool Pre0Linear => (Pre0 & 0x40) != 0;
        
        // PRE0 Bit 4: Coded flag (1=coded/palette; 0=uncoded/RGB)
        public bool Pre0Coded => (Pre0 & 0x10) != 0;
        
        // PRE0 Bits 2-0: Bits per pixel minus 1 (BPP_MASK)
        public int Pre0BitsPerPixel => ((int)(Pre0 & 0x07) + 1);
        
        // PRE1 Bits 23-13: TLHPCNT - Total Line Horizontal Pixel Count (width)
        public int Pre1Width => (int)((Pre1 >> 13) & 0x7FF);
        
        // PRE1 Bits 12-2: VCNT - Vertical Count (height in lines)
        public int Pre1Height => (int)((Pre1 >> 2) & 0x7FF);

        /// <summary>
        /// Determines if the cel data is packed (has row offset headers and packet encoding)
        /// Checks both CCB FLAGS (bit 9) and PRE0 (bit 7) depending on CCBPRE flag
        /// </summary>
        public bool IsPacked
        {
            get
            {
                if (CcbPre)
                {
                    // Preamble at end of CCB - check CCB FLAGS bit 9
                    return Packed;
                }
                else
                {
                    // Preamble at start of data - check PRE0 bit 7
                    // But CCB FLAGS bit 9 can override
                    return Packed || Pre0Packed;
                }
            }
        }

        /// <summary>
        /// Determines if the cel data is coded (uses palette indices) or uncoded (direct RGB)
        /// </summary>
        public bool IsCoded => Pre0Coded;

        /// <summary>
        /// Gets the bits per pixel for this cel
        /// Priority: CCB stored value > PRE0 value
        /// </summary>
        public int GetBitsPerPixel(int ccbStoredBpp = 0)
        {
            if (ccbStoredBpp > 0)
                return ccbStoredBpp;
            
            return Pre0BitsPerPixel;
        }

        /// <summary>
        /// Gets the width for this cel
        /// Priority: CCB stored value > PRE1 value
        /// </summary>
        public int GetWidth(int ccbStoredWidth = 0)
        {
            if (ccbStoredWidth > 0)
                return ccbStoredWidth;
            
            return Pre1Width;
        }

        /// <summary>
        /// Gets the height for this cel
        /// Priority: CCB stored value > PRE1 value
        /// </summary>
        public int GetHeight(int ccbStoredHeight = 0)
        {
            if (ccbStoredHeight > 0)
                return ccbStoredHeight;
            
            return Pre1Height;
        }

        public override string ToString()
        {
            return $"CCB: {GetWidth()}x{GetHeight()}, {GetBitsPerPixel()}bpp, " +
                   $"{(IsCoded ? "Coded" : "Uncoded")}, {(IsPacked ? "Packed" : "Unpacked")}, " +
                   $"BGND={Bgnd}, NOBLK={NoBlk}, PLUTA=0x{Pluta:X}";
        }
    }
}
