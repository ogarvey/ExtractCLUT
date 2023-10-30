using System;

public class ImageRenderer
{
  public static int ISRENDERINGDISABLED;
  public static int TRANSPARENCY_FLAG_GLOBAL;
  public static int ISCLIPPINGENABLED;
  public static int IMAGE_INDEX_GLOBAL;
  public static byte[] IMAGE_DATA_TABLE;
  public static int IMAGE_DATA_OFFSET;
  public static byte[,] BUFFER_ROW_DATA;
  public static int BUFFERBASEADDRESS;
  public static int BUFFERROWSIZE;
  public static byte[] IMAGESOURCE;
  public static byte[] IMAGEDATA;
  public static int CLIPPINGMAXX;
  public static int CLIPPINGMINX;
  public static int CLIPPINGMINY;
  public static int[] SUB_0000639c;
  public static int[] SUB_00006374;
  public static int[] SUB_000063c4;
  public static unsafe void RenderImageWithOptionalClippingAndTransparency(int imageIndex, int xCoord, int yCoord, int transparencyFlag)
  {
    byte bVar1;
    short sVar2, sVar4;
    int iVar3;
    char cVar5;
    byte[] pbVar6, pbVar7, pbVar8, pbVar9, pbVar10;

    if (ISRENDERINGDISABLED != 0)
    {
      return;
    }

    TRANSPARENCY_FLAG_GLOBAL = transparencyFlag;
    ISCLIPPINGENABLED = 0;
    IMAGE_INDEX_GLOBAL = imageIndex;

    if ((2 < xCoord) && (xCoord < 0x25) && (4 < yCoord))
    {
      int ushortIndex = (int)(BitConverter.ToUInt16(IMAGE_DATA_TABLE, imageIndex * 2) + IMAGE_DATA_OFFSET);
      pbVar9 = new byte[ushortIndex];

      BUFFERBASEADDRESS = (yCoord * BUFFERROWSIZE) + xCoord * 8 + -0x6418;
      pbVar7 = new byte[(BUFFERBASEADDRESS + transparencyFlag + BUFFERROWSIZE)];
      sVar4 = 0x2f;
      pbVar6 = IMAGESOURCE;
      IMAGEDATA = pbVar9;
      do
      {
        sVar2 = 0x37;
        pbVar10 = pbVar9;
        do
        {
          if (pbVar10[0] == 0)
          {
            pbVar9 = pbVar10.Skip(2).ToArray();
            pbVar8 = pbVar7.Skip(pbVar10[1]).ToArray();
            sVar2 = (short)((sVar2 - pbVar10[1]) + 1);
          }
          else
          {
            bVar1 = pbVar7[0];
            pbVar9 = pbVar6;
            if (TRANSPARENCY_FLAG_GLOBAL == 0)
            {
              pbVar9 = pbVar6.Skip(1).ToArray();
              pbVar6[0] = bVar1;
            }
            pbVar6 = pbVar9;
            if ((bVar1 < 0x60) || (0x7f < bVar1))
            {
              pbVar9 = pbVar10.Skip(1).ToArray();
              pbVar8 = pbVar7.Skip(1).ToArray();
              pbVar7[0] = pbVar10[0];
            }
            else
            {
              pbVar9 = pbVar10.Skip(1).ToArray();
              pbVar8 = pbVar7.Skip(1).ToArray();
            }
          }
          sVar2 -= 1;
          pbVar10 = pbVar9;
          pbVar7 = pbVar8;
        } while (sVar2 != -1);
        pbVar7 = pbVar8.Skip(0x248).ToArray();
        sVar4 -= 1;
      } while (sVar4 != -1);
      return;
    }

    // rest of code
    ISCLIPPINGENABLED = 1;
    int ushortIndex2 = (int)(BitConverter.ToUInt16(IMAGE_DATA_TABLE, imageIndex * 2) + IMAGE_DATA_OFFSET);
    pbVar6 = new byte[ushortIndex2];
    BUFFERBASEADDRESS = (yCoord * BUFFERROWSIZE) + xCoord * 8 + -0x6418;
    pbVar9 = new byte[(BUFFERBASEADDRESS + transparencyFlag + BUFFERROWSIZE)];
    CLIPPINGMAXX = SUB_0000639c[xCoord];
    CLIPPINGMINX = SUB_00006374[xCoord];
    CLIPPINGMINY = SUB_000063c4[yCoord];
    cVar5 = '\0';
    sVar4 = 0x2f;
    pbVar7 = IMAGESOURCE;
    IMAGEDATA = pbVar6;

    do
    {
      sVar2 = 0x37;
      iVar3 = 0;
      do
      {
        if (pbVar6[0] == 0)
        {
          pbVar6 = pbVar6.Skip(1).ToArray();
          bVar1 = pbVar6[0];
          pbVar9 = pbVar9.Skip(bVar1).ToArray();
          iVar3 += bVar1;
          sVar2 = (short)((sVar2 - bVar1) + 1);
        }
        else
        {
          pbVar10 = pbVar7;
          if (CLIPPINGMINY <= cVar5 && CLIPPINGMINX <= iVar3 && iVar3 < CLIPPINGMAXX)
          {
            bVar1 = pbVar9[0];
            if (TRANSPARENCY_FLAG_GLOBAL == 0)
            {
              pbVar10 = pbVar7.Skip(1).ToArray();
              pbVar7[0] = bVar1;
            }
            if (bVar1 < 0x60 || 0x7f < bVar1)
            {
              pbVar9[0] = pbVar6[0];
            }
          }
          pbVar9 = pbVar9.Skip(1).ToArray();
          iVar3 += 1;
          pbVar7 = pbVar10;
        }
        pbVar6 = pbVar6.Skip(1).ToArray();
        sVar2 -= 1;
      } while (sVar2 != -1);

      pbVar9 = pbVar9.Skip(0x248).ToArray();
      cVar5++;
      sVar4 -= 1;
    } while (sVar4 != -1);

    return;

  }
}
