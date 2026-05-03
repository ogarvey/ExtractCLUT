import struct, pathlib
p=pathlib.Path(r"C:\Dev\Gaming\PC\Dos\Games\Zyclunt\ZYCLUNT_output\a-cin.abm")
b=p.read_bytes()
off=12
end=8+struct.unpack('>I',b[4:8])[0]
fi=0
while off+8<=end:
    cid=b[off:off+4]
    clen=struct.unpack('>I',b[off+4:off+8])[0]
    ds=off+8
    de=ds+clen
    if cid==b'FORM' and b[ds:ds+4]==b'PBM ':
        io=ds+4
        dl=None
        while io+8<=de:
            k=b[io:io+4]
            l=struct.unpack('>I',b[io+4:io+8])[0]
            cds=io+8
            cde=cds+l
            if k==b'DLTA':
                dl=b[cds:cde]
                break
            io=cde+(l&1)
        if fi==1 and dl is not None:
            print('dlta len',len(dl))
            for i in range(0,256,16):
                chunk=dl[i:i+16]
                print(f'{i:04x}:',chunk.hex(' '))
            break
        fi+=1
    off=de+(clen&1)
