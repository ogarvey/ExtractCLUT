
void FUN_005fef70(int *param_1,int param_2,undefined4 *param_3)

{
  void *pvVar1;
  char cVar2;
  int iVar3;
  int iVar4;
  int iVar5;
  int iVar6;
  int iVar7;
  uint uVar8;
  int *piVar9;
  int *in_ECX;
  uint uVar10;
  undefined4 uVar11;
  char *pcVar12;
  int iVar13;
  undefined *puVar14;
  undefined1 auStack_54 [16];
  undefined1 auStack_44 [16];
  undefined1 auStack_34 [8];
  char *pcStack_2c;
  undefined1 auStack_20 [8];
  undefined4 uStack_18;
  void *local_c;
  undefined1 *puStack_8;
  int iStack_4;
  
  pvVar1 = ExceptionList;
  iStack_4 = 0xffffffff;
  puStack_8 = &LAB_006513a8;
  local_c = ExceptionList;
  uVar11 = 0;
  iVar13 = *param_1;
  ExceptionList = &local_c;
  in_ECX[5] = iVar13;
  in_ECX[6] = 0;
  in_ECX[7] = 0;
  *(undefined1 *)(in_ECX + 0xd) = 0;
  if (iVar13 == 0) {
    ExceptionList = pvVar1;
    return;
  }
  (**(code **)(in_ECX[8] + 0x14))(&DAT_006da8a8);
  puVar14 = &DAT_006c4914;
  iVar13 = *(int *)(param_2 + 4);
  FUN_005d8080(auStack_54,1);
  cVar2 = FUN_005d7d30(puVar14);
  if (cVar2 == '\0') {
    puVar14 = &DAT_006c491c;
    FUN_005d8080(auStack_54,1);
    cVar2 = FUN_005d7d30(puVar14);
    if (cVar2 != '\0') goto LAB_005feffd;
  }
  else {
LAB_005feffd:
    iVar13 = iVar13 + -1;
  }
  puVar14 = &DAT_006c4914;
  FUN_005d8050(auStack_54,1);
  cVar2 = FUN_005d7d30(puVar14);
  if (cVar2 == '\0') {
    puVar14 = &DAT_006c491c;
    FUN_005d8050(auStack_54,1);
    cVar2 = FUN_005d7d30(puVar14);
    if (cVar2 != '\0') goto LAB_005ff03a;
  }
  else {
LAB_005ff03a:
    uVar11 = 1;
    iVar13 = iVar13 + -1;
  }
  uVar11 = FUN_005d8100(auStack_54,uVar11,iVar13);
  FUN_005b8770(uVar11);
  iStack_4 = 0;
  FUN_005fe970(auStack_34);
  param_1 = (int *)CONCAT31(param_1._1_3_,0x2a);
  iVar13 = FUN_005d7e20(&param_1,0);
  if (iVar13 == -1) {
    param_1 = (int *)CONCAT31(param_1._1_3_,0x3f);
    iVar13 = FUN_005d7e20(&param_1,0);
    if (iVar13 != -1) goto LAB_005ff13d;
    uVar10 = 0xffffffff;
    *(undefined1 *)((int)in_ECX + 0x35) = 1;
    pcVar12 = pcStack_2c;
    do {
      if (uVar10 == 0) break;
      uVar10 = uVar10 - 1;
      cVar2 = *pcVar12;
      pcVar12 = pcVar12 + 1;
    } while (cVar2 != '\0');
    param_1 = (int *)(~uVar10 - 1);
    iVar13 = 0;
    iVar3 = (int)param_1 >> 2;
    pcVar12 = pcStack_2c;
    if (-1 < iVar3 + -1) {
      do {
        iVar4 = FUN_00638880((int)*pcVar12);
        iVar5 = FUN_00638880((int)pcVar12[1]);
        iVar6 = FUN_00638880((int)pcVar12[2]);
        iVar7 = FUN_00638880((int)pcVar12[3]);
        pcStack_2c = pcVar12 + 4;
        iVar3 = iVar3 + -1;
        iVar13 = iVar13 + iVar4 + iVar5 * 2 + iVar6 * 4 + iVar7 * 8;
        pcVar12 = pcStack_2c;
      } while (iVar3 != 0);
    }
    uVar10 = (uint)param_1 & 3;
    if (-1 < (int)(uVar10 - 1)) {
      do {
        cVar2 = *pcStack_2c;
        pcStack_2c = pcStack_2c + 1;
        uVar8 = FUN_00638880((int)cVar2);
        iVar13 = iVar13 + (uVar8 & 0xff);
        uVar10 = uVar10 - 1;
      } while (uVar10 != 0);
    }
    in_ECX[0xe] = iVar13;
  }
  else {
LAB_005ff13d:
    *(undefined1 *)((int)in_ECX + 0x35) = 0;
    in_ECX[0xe] = 0;
  }
  iVar13 = FUN_005d7fe0(&DAT_006c3c88,0xffffffff);
  if (iVar13 != -1) {
    uVar11 = FUN_005d8050(auStack_44,iVar13);
    FUN_005b8770(uVar11);
    iStack_4._0_1_ = 1;
    iVar3 = FUN_005fedd0(auStack_20,in_ECX + 8);
    in_ECX[5] = iVar3;
    iStack_4 = (uint)iStack_4._1_3_ << 8;
    FUN_005b88c0();
  }
  if (in_ECX[5] != 0) {
    iVar3 = *in_ECX;
    uVar11 = FUN_005d80c0(auStack_44,iVar13 + 1);
    (**(code **)(iVar3 + 0x1c))(uVar11);
    piVar9 = (int *)FUN_00602d10(&param_1,in_ECX[2]);
    if (-1 < *piVar9) {
      puVar14 = &DAT_006c5404;
      *(undefined1 *)(in_ECX + 0x17) = 0;
      FUN_005d8080(auStack_44,2);
      cVar2 = FUN_005d7d30(puVar14);
      if (cVar2 != '\0') {
        *(undefined1 *)(in_ECX + 0x17) = 1;
        uVar11 = FUN_005d8050(auStack_44,in_ECX[1] + -2);
        FUN_005b8770(uVar11);
        iStack_4._0_1_ = 2;
        piVar9 = (int *)FUN_00602d10(&param_1,uStack_18);
        iStack_4 = (uint)iStack_4._1_3_ << 8;
        if (*piVar9 < 0) {
          FUN_005b88c0();
          goto LAB_005ff285;
        }
        FUN_005b88c0();
      }
      in_ECX[6] = *(int *)(in_ECX[5] + 0xc);
      *(undefined1 *)(in_ECX + 0xd) = 1;
      FUN_005ff2b0();
      if (param_3 != (undefined4 *)0x0) {
        if (((int *)in_ECX[7] == (int *)0x0) || ((char)in_ECX[0xd] != '\0')) {
          *param_3 = 0;
        }
        else {
          *param_3 = *(undefined4 *)(*(int *)in_ECX[7] + 0xc);
        }
      }
    }
  }
LAB_005ff285:
  iStack_4 = 0xffffffff;
  FUN_005b88c0();
  ExceptionList = local_c;
  return;
}


