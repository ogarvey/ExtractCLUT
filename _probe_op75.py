import struct
from pathlib import Path

p=Path(r'Games/PSX/Aconcagua/U/ANIM/a-cin.abm')
d=p.read_bytes()

# locate frame1 DLTA quickly
assert d[0:4]==b'FORM'
form_len=struct.unpack('>I',d[4:8])[0]
o=12
frames=[]
end=min(len(d),8+form_len)
while o+8<=end:
    cid=d[o:o+4]
    clen=struct.unpack('>I',d[o+4:o+8])[0]
    ds=o+8; de=ds+clen
    if cid==b'FORM' and clen>=4 and d[ds:ds+4] in (b'PBM ',b'ILBM'):
        fo=ds+4; fe=de
        anhd=None; dlta=None
        x=fo
        while x+8<=fe:
            k=d[x:x+4]; l=struct.unpack('>I',d[x+4:x+8])[0]; s=x+8; e=s+l
            if e>fe: break
            if k==b'ANHD': anhd=d[s:e]
            if k==b'DLTA': dlta=d[s:e]
            x=e+(l&1)
        frames.append((anhd,dlta))
    o=de+(clen&1)

anhd,dlta=frames[1]
print('dlta len',len(dlta),'anhd op',anhd[0],'inter',anhd[20],'bits',struct.unpack('>I',anhd[22:26])[0])

width=320;height=256;bitplanes=8
planepitch_byte=(width+7)//8
planePitch=((width+15)//16)*2
pitch=planePitch*bitplanes
kludgeJ=(320-width)//8//2 if width<320 else 0

def map_off(raw):
    if kludgeJ!=0:
        return ((raw//(320//8))*pitch)+(raw%(320//8))-kludgeJ
    return ((raw//planepitch_byte)*pitch)+(raw%planepitch_byte)

def try_parse(buf,start):
    pos=start
    steps=0
    groups=0
    while pos+2<=len(buf) and steps<200000:
        t=struct.unpack('>H',buf[pos:pos+2])[0]; pos+=2; steps+=1
        if t==0:
            return ('ok',pos-start,groups)
        if t==1:
            if pos+6>len(buf): return ('trunc1h',pos-start,groups)
            rev,bcount,gcount=struct.unpack('>HHH',buf[pos:pos+6]); pos+=6
            for g in range(gcount):
                if pos+2>len(buf): return ('trunc1go',pos-start,groups)
                raw=struct.unpack('>H',buf[pos:pos+2])[0]; pos+=2
                need=bcount*bitplanes
                if pos+need>len(buf): return ('trunc1data',pos-start,groups)
                pos+=need
                if (need&1)!=0 and pos<len(buf): pos+=1
                groups+=1
            continue
        if t==2:
            if pos+8>len(buf): return ('trunc2h',pos-start,groups)
            rev,rowCount,byteCount,groupCount=struct.unpack('>HHHH',buf[pos:pos+8]); pos+=8
            for g in range(groupCount):
                if pos+2>len(buf): return ('trunc2go',pos-start,groups)
                pos+=2
                need=rowCount*byteCount*bitplanes
                if pos+need>len(buf): return ('trunc2data',pos-start,groups)
                pos+=need
                if (need&1)!=0 and pos<len(buf): pos+=1
                groups+=1
            continue
        return ('badtype_%04x'%t,pos-start,groups)
    return ('loop',pos-start,groups)

best=[]
for s in range(0,min(1024,len(dlta)-2)):
    st,cons,grp=try_parse(dlta,s)
    if st=='ok' or grp>10 or cons>1000:
        best.append((s,st,cons,grp))

print('candidates',len(best))
for row in best[:40]:
    print(row)

# also print first places with type markers
for marker in (b'\x00\x01',b'\x00\x02',b'\x00\x00'):
    idx=dlta.find(marker)
    print('first',marker,idx)
