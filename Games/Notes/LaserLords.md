            DAT_8000262e      => A6-0x59d2
AOB+0x7e => Money             => A6-0x5772
            DAT_800028b0      => A6-0x5750
            PLAYER_MAX_HEALTH => A6-0x5744
            PLAYER_HEALTH     => A6-0x5740
            PLAYER_FACING_L_R => A6-0x5728 -- 3 = L, 4 = R, 6 = F
            PLAYER_CROUCHED   => A6-0x570c
            MAIN_FLOOR_Y_POS  => A6-0x56e4
            NPC_X_POS         => A6-0x56e0
            MAIN_FLOOR_Y_POS  => A6-0x56d4
            NPC_DIRECTION     => A6-0x56d0 -- 3 = L, 4 = R, 6 = F
            DAT_80002940      => A6-0x56c0
            DAT_80002998      => A6-0x5668
            DAT_800029ac      => A6-0x5654
            DAT_800029d6      => A6-0x562a
            DAT_80004c0e      => A6-0x33f2
            CURRENT_SCREEN_ID => A6-0x33c6
            LEVEL_ID          => A6-0x33be
            

```c
    uVar1 = __os9_getStat__d0(0x59,param_1,uVar4,unaff_D3,in_A0,in_A1,unaff_A2);
    uVar2 = __os9_getStat__d1(0x59,param_1,uVar4,unaff_D3,in_A0,in_A1,unaff_A2);
    __os9_getStat__d2(0x59,param_1,uVar4,unaff_D3,in_A0,in_A1,unaff_A2);
    __os9_getStat__d3(0x59,param_1,uVar4,unaff_D3,in_A0,in_A1,unaff_A2);
    __os9_getStat__a0(0x59,param_1,uVar4,unaff_D3,in_A0,in_A1,unaff_A2);
    __os9_getStat__a1(0x59,param_1,uVar4,unaff_D3,in_A0,in_A1,unaff_A2);
    __os9_getStat__a2(0x59,param_1,uVar4,unaff_D3,in_A0,in_A1,unaff_A2);
    __os9_getStat__a3(0x59,param_1,uVar4,unaff_D3,in_A0,in_A1,unaff_A2);
```

Is there a way to translate these calls in Ghidra into something cdi-related? 
