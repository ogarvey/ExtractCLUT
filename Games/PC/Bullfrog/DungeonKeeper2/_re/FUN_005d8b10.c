
undefined4 * FUN_005d8b10(int *param_1,undefined4 param_2,undefined4 param_3)

{
  char cVar1;
  int iVar2;
  undefined4 uVar3;
  undefined4 *puVar4;
  int *piVar5;
  int in_ECX;
  bool bVar6;
  undefined4 *unaff_retaddr;
  int iStack_70;
  int iStack_60;
  undefined1 auStack_50 [4];
  int local_4c;
  undefined1 auStack_40 [4];
  undefined4 auStack_3c [4];
  undefined4 uStack_2c;
  undefined4 uStack_28;
  byte bStack_24;
  undefined4 uStack_20;
  void *pvStack_10;
  void *pvStack_c;
  undefined1 *puStack_8;
  undefined4 local_4;
  
  local_4 = 0xffffffff;
  puStack_8 = &LAB_00650ccf;
  pvStack_c = ExceptionList;
  ExceptionList = &pvStack_c;
  FUN_005b8770(in_ECX + 0x24);
  local_4 = 0;
  (**(code **)(local_4c + 0x20))(param_3);
  iVar2 = FUN_00634e80(0x10);
  puStack_8._0_1_ = 1;
  if (iVar2 == 0) {
    uVar3 = 0;
  }
  else {
    uVar3 = FUN_005ff710();
  }
  puStack_8 = (undefined1 *)((uint)puStack_8._1_3_ << 8);
  FUN_005b6f20(uVar3);
  if (*(int *)(in_ECX + 0xc4) == 0) {
    FUN_005fe5d0(&stack0xffffff4c,in_ECX + 0xb8,
                 CONCAT31((int3)((uint)*(int *)(in_ECX + 100) >> 8),*(int *)(in_ECX + 100) == 2));
  }
  FUN_005feef0(in_ECX + 0xc4,auStack_50,auStack_3c);
  puStack_8._0_1_ = 2;
  cVar1 = FUN_005ff3d0();
  if (cVar1 != '\0') {
    (**(code **)(*param_1 + 4))();
    *unaff_retaddr = 0xffffffff;
    puStack_8._0_1_ = 5;
    FUN_005d9190();
    puStack_8._0_1_ = 4;
    while (iStack_70 != 0) {
      iVar2 = *(int *)(iStack_70 + 0x14);
      bVar6 = iStack_70 != 0;
      iStack_70 = iVar2;
      if (bVar6) {
        FUN_005d91e0(1);
      }
    }
    puStack_8._0_1_ = 3;
LAB_005d8dd2:
    FUN_005b88c0();
    puStack_8 = (undefined1 *)((uint)puStack_8._1_3_ << 8);
    FUN_005b88c0();
    puStack_8 = (undefined1 *)0xffffffff;
    FUN_005b88c0();
    ExceptionList = pvStack_10;
    return unaff_retaddr;
  }
  (**(code **)(*param_1 + 0x10))(auStack_3c[0],0);
  (**(code **)(*param_1 + 8))(auStack_40,0x28);
  (**(code **)(*param_1 + 0x10))(auStack_3c[0],0);
  iVar2 = FUN_00634e80(0x1c);
  puStack_8._0_1_ = 6;
  if (iVar2 == 0) {
    puVar4 = (undefined4 *)0x0;
  }
  else {
    puVar4 = (undefined4 *)FUN_005ffc80();
  }
  puStack_8._0_1_ = 2;
  piVar5 = (int *)FUN_005ffcb0(&stack0xffffff4c,uStack_28,uStack_2c);
  if (*piVar5 < 0) {
    if (puVar4 != (undefined4 *)0x0) {
      (**(code **)*puVar4)(1);
    }
    *unaff_retaddr = 0xffffffff;
    puStack_8._0_1_ = 9;
    FUN_005d9190();
    puStack_8._0_1_ = 8;
    while (iStack_70 != 0) {
      iVar2 = *(int *)(iStack_70 + 0x14);
      bVar6 = iStack_70 != 0;
      iStack_70 = iVar2;
      if (bVar6) {
        FUN_005d91e0(1);
      }
    }
    puStack_8._0_1_ = 7;
    goto LAB_005d8dd2;
  }
  FUN_005b6f20(puVar4);
  if ((bStack_24 & 1) != 0) {
    *unaff_retaddr = 0xffffffff;
    puStack_8._0_1_ = 0xc;
    FUN_005d9190();
    puStack_8._0_1_ = 0xb;
    while (iStack_70 != 0) {
      iVar2 = *(int *)(iStack_70 + 0x14);
      bVar6 = iStack_70 != 0;
      iStack_70 = iVar2;
      if (bVar6) {
        FUN_005d91e0(1);
      }
    }
    puStack_8._0_1_ = 10;
    goto LAB_005d8dd2;
  }
  if ((bStack_24 & 2) == 0) {
    if ((bStack_24 & 4) != 0) {
      iVar2 = FUN_00634e80(0x18);
      puStack_8._0_1_ = 0x11;
      if (iVar2 == 0) {
        puVar4 = (undefined4 *)0x0;
      }
      else {
        puVar4 = (undefined4 *)FUN_005ff9b0();
      }
      puStack_8._0_1_ = 2;
      piVar5 = (int *)FUN_005ffb10(&stack0xffffff4c,uStack_20);
      if (*piVar5 < 0) {
        if (puVar4 != (undefined4 *)0x0) {
          (**(code **)*puVar4)(1);
        }
        *unaff_retaddr = 0xffffffff;
        puStack_8._0_1_ = 0x14;
        while (iStack_60 != 0) {
          iVar2 = *(int *)(iStack_60 + 0x14);
          bVar6 = iStack_60 != 0;
          iStack_60 = iVar2;
          if (bVar6) {
            FUN_005d91e0(1);
          }
        }
        puStack_8._0_1_ = 0x13;
        while (iVar2 = iStack_70, iVar2 != 0) {
          iStack_70 = *(int *)(iVar2 + 0x14);
          if (iVar2 != 0) {
            FUN_005b88c0();
            FUN_006341b0(iVar2);
          }
        }
        puStack_8._0_1_ = 0x12;
        goto LAB_005d9071;
      }
      goto LAB_005d8fd9;
    }
  }
  else {
    iVar2 = FUN_00634e80(0x18);
    puStack_8._0_1_ = 0xd;
    if (iVar2 == 0) {
      puVar4 = (undefined4 *)0x0;
    }
    else {
      puVar4 = (undefined4 *)FUN_005ffad0();
    }
    puStack_8._0_1_ = 2;
    piVar5 = (int *)FUN_005ffb10(&stack0xffffff4c,uStack_20);
    if (*piVar5 < 0) {
      if (puVar4 != (undefined4 *)0x0) {
        (**(code **)*puVar4)(1);
      }
      *unaff_retaddr = 0xffffffff;
      puStack_8._0_1_ = 0x10;
      FUN_005d9190();
      puStack_8._0_1_ = 0xf;
      while (iVar2 = iStack_70, iVar2 != 0) {
        iStack_70 = *(int *)(iVar2 + 0x14);
        if (iVar2 != 0) {
          FUN_005b88c0();
          FUN_006341b0(iVar2);
        }
      }
      puStack_8._0_1_ = 0xe;
      goto LAB_005d9071;
    }
LAB_005d8fd9:
    FUN_005b6f20(puVar4);
  }
  *unaff_retaddr = 0;
  puStack_8._0_1_ = 0x17;
  while (iVar2 = iStack_60, iVar2 != 0) {
    iStack_60 = *(int *)(iVar2 + 0x14);
    if (iVar2 != 0) {
      FUN_005b88c0();
      FUN_006341b0(iVar2);
    }
  }
  puStack_8._0_1_ = 0x16;
  while (iVar2 = iStack_70, iVar2 != 0) {
    iStack_70 = *(int *)(iVar2 + 0x14);
    if (iVar2 != 0) {
      FUN_005b88c0();
      FUN_006341b0(iVar2);
    }
  }
  puStack_8._0_1_ = 0x15;
LAB_005d9071:
  FUN_005b88c0();
  puStack_8 = (undefined1 *)((uint)puStack_8._1_3_ << 8);
  FUN_005b88c0();
  puStack_8 = (undefined1 *)0xffffffff;
  FUN_005b88c0();
  ExceptionList = pvStack_10;
  return unaff_retaddr;
}


