import pathlib
b=pathlib.Path(r"C:\Dev\Gaming\PC\Dos\Games\Zyclunt\ZYCLUNT_output\a-cin.abm").read_bytes()
# extract frame1 dlta quickly
import struct
off=12; end=8+struct.unpack('>I',b[4:8])[0]; fi=0; dl=None
while off+8<=end:
    cid=b[off:off+4]; clen=struct.unpack('>I',b[off+4:off+8])[0]; ds=off+8; de=ds+clen
    if cid==b'FORM' and b[ds:ds+4]==b'PBM ':
        io=ds+4
        while io+8<=de:
            k=b[io:io+4]; l=struct.unpack('>I',b[io+4:io+8])[0]; cds=io+8; cde=cds+l
            if k==b'DLTA' and fi==1: dl=b[cds:cde]; break
            io=cde+(l&1)
        fi+=1
        if dl: break
    off=de+(clen&1)

pos=0
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
        # try stop
        print('unknown op',op,'at',pos)
        break
print('end pos',pos,'len',len(dl),'remaining',len(dl)-pos)
print('op histogram',sorted((k,v) for k,v in ops.items()))
