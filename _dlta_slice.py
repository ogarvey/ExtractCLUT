import pathlib, struct
b=pathlib.Path(r"C:\Dev\Gaming\PC\Dos\Games\Zyclunt\ZYCLUNT_output\a-cin.abm").read_bytes()
of=12
end=8+struct.unpack('>I',b[4:8])[0]
fi=0
dl=None
while of+8<=end:
    cid=b[of:of+4]
    clen=struct.unpack('>I',b[of+4:of+8])[0]
    ds=of+8
    de=ds+clen
    if cid==b'FORM' and b[ds:ds+4]==b'PBM ':
        io=ds+4
        while io+8<=de:
            k=b[io:io+4]
            l=struct.unpack('>I',b[io+4:io+8])[0]
            cds=io+8
            cde=cds+l
            if k==b'DLTA' and fi==1:
                dl=b[cds:cde]
                break
            io=cde+(l&1)
        fi += 1
        if dl is not None:
            break
    of=de+(clen&1)
for i in range(240, 400, 16):
    print(f"{i:04x}:", dl[i:i+16].hex(' '))
