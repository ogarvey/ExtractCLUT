
void FUN_0055c020(void)

{
  char cVar1;
  char *pcVar2;
  char *pcVar3;
  undefined4 *in_ECX;
  uint uVar4;
  uint uVar5;
  LPSTR pCVar6;
  char *pcVar7;
  undefined1 local_118 [8];
  char local_110 [260];
  void *local_c;
  undefined1 *puStack_8;
  undefined4 local_4;
  
  local_4 = 0xffffffff;
  puStack_8 = &LAB_0064e51a;
  local_c = ExceptionList;
  ExceptionList = &local_c;
  FUN_00556870();
  local_4 = 0;
  FUN_00556870();
  local_4._0_1_ = 1;
  FUN_00556870();
  local_4._0_1_ = 2;
  FUN_00556870();
  local_4._0_1_ = 3;
  FUN_00556870();
  local_4._0_1_ = 4;
  FUN_00556870();
  local_4._0_1_ = 5;
  FUN_00556870();
  local_4._0_1_ = 6;
  FUN_00556870();
  local_4._0_1_ = 7;
  FUN_00556870();
  local_4._0_1_ = 8;
  FUN_00556870();
  uVar4 = 0xffffffff;
  *in_ECX = 0;
  pcVar2 = &DAT_006bcbd8;
  do {
    pcVar3 = pcVar2;
    if (uVar4 == 0) break;
    uVar4 = uVar4 - 1;
    pcVar3 = pcVar2 + 1;
    cVar1 = *pcVar2;
    pcVar2 = pcVar3;
  } while (cVar1 != '\0');
  uVar4 = ~uVar4;
  local_4 = CONCAT31(local_4._1_3_,9);
  pcVar2 = pcVar3 + -uVar4;
  pcVar3 = local_110;
  for (uVar5 = uVar4 >> 2; uVar5 != 0; uVar5 = uVar5 - 1) {
    *(undefined4 *)pcVar3 = *(undefined4 *)pcVar2;
    pcVar2 = pcVar2 + 4;
    pcVar3 = pcVar3 + 4;
  }
  for (uVar4 = uVar4 & 3; uVar4 != 0; uVar4 = uVar4 - 1) {
    *pcVar3 = *pcVar2;
    pcVar2 = pcVar2 + 1;
    pcVar3 = pcVar3 + 1;
  }
  pcVar2 = GetCommandLineA();
  if (*pcVar2 == '\"') {
    pcVar2 = pcVar2 + 1;
    pcVar3 = (char *)FUN_00637c90(pcVar2,0x22);
LAB_0055c16a:
    if (pcVar3 == (char *)0x0) goto LAB_0055c1a6;
  }
  else {
    pcVar3 = (char *)FUN_00637c90(pcVar2 + 1,0x20);
    if (pcVar3 == (char *)0x0) {
      uVar4 = 0xffffffff;
      pCVar6 = pcVar2;
      do {
        if (uVar4 == 0) break;
        uVar4 = uVar4 - 1;
        cVar1 = *pCVar6;
        pCVar6 = pCVar6 + 1;
      } while (cVar1 != '\0');
      pcVar3 = pcVar2 + (~uVar4 - 1);
      goto LAB_0055c16a;
    }
  }
  if (pcVar2 < pcVar3) {
    do {
      if (*pcVar3 == '\\') break;
      pcVar3 = pcVar3 + -1;
    } while (pcVar2 < pcVar3);
    if (pcVar2 < pcVar3) {
      pcVar3[1] = '\0';
    }
  }
  uVar4 = 0xffffffff;
  do {
    pcVar3 = pcVar2;
    if (uVar4 == 0) break;
    uVar4 = uVar4 - 1;
    pcVar3 = pcVar2 + 1;
    cVar1 = *pcVar2;
    pcVar2 = pcVar3;
  } while (cVar1 != '\0');
  uVar4 = ~uVar4;
  pcVar2 = pcVar3 + -uVar4;
  pcVar3 = local_110;
  for (uVar5 = uVar4 >> 2; uVar5 != 0; uVar5 = uVar5 - 1) {
    *(undefined4 *)pcVar3 = *(undefined4 *)pcVar2;
    pcVar2 = pcVar2 + 4;
    pcVar3 = pcVar3 + 4;
  }
  for (uVar4 = uVar4 & 3; uVar4 != 0; uVar4 = uVar4 - 1) {
    *pcVar3 = *pcVar2;
    pcVar2 = pcVar2 + 1;
    pcVar3 = pcVar3 + 1;
  }
LAB_0055c1a6:
  FUN_005595c0(&DAT_00756ee8,s_HD_Path___s_006bcbc8,local_110);
  uVar4 = 0xffffffff;
  pcVar2 = local_110;
  do {
    pcVar3 = pcVar2;
    if (uVar4 == 0) break;
    uVar4 = uVar4 - 1;
    pcVar3 = pcVar2 + 1;
    cVar1 = *pcVar2;
    pcVar2 = pcVar3;
  } while (cVar1 != '\0');
  uVar4 = ~uVar4;
  pcVar2 = (char *)(in_ECX + 0x42);
  pcVar3 = pcVar3 + -uVar4;
  pcVar7 = pcVar2;
  for (uVar5 = uVar4 >> 2; uVar5 != 0; uVar5 = uVar5 - 1) {
    *(undefined4 *)pcVar7 = *(undefined4 *)pcVar3;
    pcVar3 = pcVar3 + 4;
    pcVar7 = pcVar7 + 4;
  }
  for (uVar4 = uVar4 & 3; uVar4 != 0; uVar4 = uVar4 - 1) {
    *pcVar7 = *pcVar3;
    pcVar3 = pcVar3 + 1;
    pcVar7 = pcVar7 + 1;
  }
  FUN_0055c3c0();
  FUN_00634db0(in_ECX + 0x24a,s__sdata_editor__006bcbb8,pcVar2);
  FUN_00634db0(in_ECX + 0x105,s__sdata_Save__006bcba8,pcVar2);
  FUN_00634db0(in_ECX + 0x83,s__sdata_Settings__006bcb94,pcVar2);
  FUN_00634db0(in_ECX + 0x146,s_GLOBAL__0068ed34);
  FUN_00634db0(in_ECX + 0x187,s__sdata_Text__006bcb84,pcVar2);
  FUN_00634db0(in_ECX + 0xc4,s__s_Dk2TextureCache_006bcb70,pcVar2);
  FUN_00634db0(in_ECX + 0x1c8,s__sdata_sound_SFX__006bcb5c,pcVar2);
  FUN_00634db0(in_ECX + 0x209,s__sdata_sound_Music__006bcb48,pcVar2);
  FUN_0055be80(local_118,in_ECX + 0x2cc,s__sdata_Meshes_Wad_006bcb34,pcVar2);
  FUN_0055be80(local_118,in_ECX + 0x319,s_K__DK2_Dev_Data_Meshes_Wad_006bcb18,pcVar2);
  FUN_0055be80(local_118,in_ECX + 0x366,s__sdata_EngineTextures_wad_006bcafc,pcVar2);
  FUN_0055be80(local_118,in_ECX + 0x49a,s__sdata_Sprite_Wad_006bcae8,pcVar2);
  FUN_0055be80(local_118,in_ECX + 0x581,s__sdata_FrontEnd_wad_006bcad4,pcVar2);
  FUN_0055be80(local_118,in_ECX + 0x534,s__sdata_Paths_wad_006bcac0,pcVar2);
  FUN_0055bf40(local_118,in_ECX + 0x400,s__sdata_editor_006bcab0,pcVar2);
  FUN_0055bf40(local_118,in_ECX + 0x4e7,s__sdata_text__006bcaa0,pcVar2);
  FUN_0055bf40(local_118,in_ECX + 0x3b3,s__sdata_Texture_006bca90,pcVar2);
  FUN_0055bf40(local_118,in_ECX + 0x44d,s__sdata_palette_006bca80,pcVar2);
  ExceptionList = local_c;
  return;
}


