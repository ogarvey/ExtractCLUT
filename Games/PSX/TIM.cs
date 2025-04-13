using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PSX
{
    public class TIMHeader
    {
        public TIMType Type { get; set; }

    }

    public enum TIMType
    {
        TIM_4bpp_NO_CLUT = 0x00,
        TIM_8bpp_NO_CLUT = 0x01,
        TIM_16bpp = 0x02,
        TIM_24bpp = 0x03,
        TIM_4bpp = 0x08,
        TIM_8bpp = 0x09,
    }
}
