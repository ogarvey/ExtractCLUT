import struct, pathlib
p = pathlib.Path(r"C:\Dev\Gaming\PC\Dos\Games\Zyclunt\ZYCLUNT_output\a-cin.abm")
b = p.read_bytes()
assert b[:4] == b'FORM'
flen = struct.unpack('>I', b[4:8])[0]
print('top', b[8:12].decode('ascii'), 'formLen', flen, 'fileLen', len(b))
off = 12
end = min(len(b), 8 + flen)
fi = 0
while off + 8 <= end:
    cid = b[off:off+4]
    clen = struct.unpack('>I', b[off+4:off+8])[0]
    ds = off + 8
    de = ds + clen
    if de > len(b) or de > end:
        break
    if cid == b'FORM' and clen >= 4:
        sub = b[ds:ds+4]
        if sub in (b'PBM ', b'ILBM'):
            io = ds + 4
            body = 0
            dlta = 0
            op = None
            inter = None
            bits = None
            dhead = None
            while io + 8 <= de:
                k = b[io:io+4]
                l = struct.unpack('>I', b[io+4:io+8])[0]
                cds = io + 8
                cde = cds + l
                if cde > de:
                    break
                if k == b'BODY':
                    body = l
                elif k == b'DLTA':
                    dlta = l
                    dhead = b[cds:cds+64]
                elif k == b'ANHD' and l >= 26:
                    op = b[cds]
                    inter = b[cds+20]
                    bits = struct.unpack('>I', b[cds+22:cds+26])[0]
                io = cde + (l & 1)
            if fi < 8 or fi % 20 == 0:
                print(f"frame {fi:03d} fmt={sub.decode('ascii')} body={body} dlta={dlta} op={op} inter={inter} bits=0x{(bits or 0):08X}")
                if dhead is not None:
                    print('  dlta[0:32]=', dhead[:32].hex(' '))
            fi += 1
    off = de + (clen & 1)
print('total frames', fi)
