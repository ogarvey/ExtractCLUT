using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Model.Scumm
{
    public class Costume
    {
        public uint Size { get; set; }
        public string Header { get; set; }
        public byte NumAnim { get; set; }
        public byte Format { get; set; }
        public List<byte> Palette { get; set; }
        public ushort AnimCommandsOffset { get; set; }
        public List<ushort> LimbsOffsets { get; set; }
        public List<ushort> AnimOffsets { get; set; }
        public List<Animation> Animations { get; set; }
        public List<byte> Commands { get; set; }
        public List<Limb> Limbs { get; set; }
        public List<CostumeImageData> Pictures { get; set; }
        public byte CloseByte { get; set; } //sei lah, mas parece que os costumes vem com um close byte !?!?
        public bool HasCloseByte { get; set; }
        public int PaletteSize { get; set; }
    }
    public class Animation
    {
        private ushort _limbMask;
        private byte _numLimbs;

        //Como varios indicies tb apontam para o mesmooffset de animação, vou lelo apenas uma vez, para isso, vou armazenar aqui o offset
        //da animação, assim, já tenho como consulta-lo se ele foi lido ou não. Porem é obvio que ele não entra no calculo do tamanho.
        public ushort Offset { get; set; }

        public Animation()
        {
            AnimDefinitions = new List<AnimationDefinition>();
        }

        //LimbMask contem a quantidade de limbs e suas respectivas posições. Cada bit ligado é um lib que será usado, sendo o indice o mesmo do bit.
        public ushort LimbMask
        {
            get { return _limbMask; }
            set
            {
                _limbMask = value;
                for (int i = 0; i < 16; i++)
                {
                    if (Utils.CheckBitState(_limbMask, i)) _numLimbs++;
                }
            }
        }

        //Deixa pré-calculado o numero de limbs.
        public byte NumLimbs { get { return _numLimbs; } }

        public List<AnimationDefinition> AnimDefinitions { get; set; }

        public ushort GetSize()
        {
            ushort size = 2; //LimbMask;
            foreach (AnimationDefinition animationDefinition in AnimDefinitions)
            {
                size += animationDefinition.GetSize();
            }
            return size;
        }
    }


    /*
    anim definition
        0xFFFF       : 16le disabled limb code
        OR
        start        : 16le
        noloop       : 1
        end offset   : 7 offset of the last frame, or len-1  (Ate onde entendi, isso é o tamanho e não o offset final. O texto abaixo foi tirado
                                                              do SCUMMC, no github:
        if the index is not 0xFFFF, then it’s followed by the length of the sequence (8 bits). 
        The highest bit of the length is used to indicate whether the sequence should loop, if it is set the animation doesn’t loop.
     */
    public class AnimationDefinition
    {
        public ushort Start { get; set; }
        public byte NoLoopAndEndOffset { get; set; }

        public bool NoLoop
        {
            get
            {
                return Utils.CheckBitState(NoLoopAndEndOffset, 7);
            }
        }

        public byte Length
        {
            get
            {
                return Utils.GetBitsFromByte(NoLoopAndEndOffset, 7);
            }
        }

        public bool Disabled
        {
            get
            {
                return Start == 0xFFFF;
            }
        }

        public ushort GetSize()
        {
            ushort size = 2; //Start;
            if (!Disabled)
            {
                size += 1; //NoLoopeAndEndOffset, but have only have this value when start is not 0xFFFF;
            }
            return size;
        }
    }

    public class Limb
    {
        public Limb()
        {
            ImageOffsets = new List<ushort>();
        }
        public ushort OffSet { get; set; }
        public ushort Size { get; set; }
        public List<ushort> ImageOffsets { get; set; }
    }

    /*
    picts
        width            : 16le
        height           : 16le
        rel_x            : s16le
        rel_y            : s16le
        move_x           : s16le
        move_y           : s16le
        redir_limb       : 8 only present if((format & 0x7E) == 0x60)
        redir_pict       : 8 only present if((format & 0x7E) == 0x60)
        rle data

     */
    public class CostumeImageData
    {
        //As propriedades abaixo são calculadas pelo reader para ajudar a extrair a informação e depois regera-la,
        //atualizando as informações de posição dos limbs.
        public int ImageDataSize { get; set; }
        public ushort ImageStartOffSet { get; set; }
        public bool HasRedirInfo { get; set; }

        //Dados extraidos do binario
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public short RelX { get; set; }
        public short RelY { get; set; }
        public short MoveX { get; set; }
        public short MoveY { get; set; }
        public byte RedirLimb { get; set; }
        public byte RedirPict { get; set; }
        public byte[] ImageData { get; set; }

        public ushort GetSize()
        {
            ushort size = 2; //Width;
            size += 2; //Height
            size += 2; //RelX
            size += 2; //RelY
            size += 2; //MoveX
            size += 2; //MoveY
            if (HasRedirInfo)
            {
                size += 1; //RedirLimb
                size += 1; //RedirPict
            }
            size += (ushort)ImageData.Length; //Size of ImageData

            return size;
        }
    }

}
