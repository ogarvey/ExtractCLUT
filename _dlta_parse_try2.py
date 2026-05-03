import pathlib, struct
b=pathlib.Path(r"C:\Dev\Gaming\PC\Dos\Games\Zyclunt\ZYCLUNT_output\a-cin.abm").read_bytes()
# extract frame1 dlta
of=12; end=8+struct.unpack('>I',b[4:8])[0]; fi=0; dl=None
while of+8<=end:
    cid=b[of:of+4]; clen=struct.unpack('>I',b[of+4:of+8])[0]; ds=of+8; de=ds+clen
    if cid==b'FORM' and b[ds:ds+4]==b'PBM ':
        io=ds+4
        while io+8<=de:
            k=b[io:io+4]; l=struct.unpack('>I',b[io+4:io+8])[0]; cds=io+8; cde=cds+l
            if k==b'DLTA' and fi==1: dl=b[cds:cde]; break
            io=cde+(l&1)
        fi+=1
        if dl: break
    of=de+(clen&1)
start=struct.unpack('>I',dl[:4])[0]
pos=start
ops={}
while pos < len(dl):
    op=dl[pos]
    ops[op]=ops.get(op,0)+1
    if op==3:
        pos += 4
    elif op==4:
        if pos+5>len(dl): break
        cnt=dl[pos+4]
        pos += 5 + cnt
    else:
        print('unknown op',op,'at',pos)
        break
print('start',start,'end pos',pos,'len',len(dl),'remaining',len(dl)-pos)
print('ops',sorted(ops.items()))
