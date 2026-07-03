# Quick verification of DK2 WAD format + decompressor against Meshes.WAD
$wadPath = 'C:\GOG Games\Dungeon Keeper 2\Data\Meshes.WAD'
$bytes = [IO.File]::ReadAllBytes($wadPath)

# Header
$magic = [Text.Encoding]::ASCII.GetString($bytes, 0, 4)
$version = [BitConverter]::ToUInt32($bytes, 4)
$fileCount = [BitConverter]::ToUInt32($bytes, 0x48)
$nameOffset = [BitConverter]::ToUInt32($bytes, 0x4C)
$nameSize = [BitConverter]::ToUInt32($bytes, 0x50)
"Magic=$magic Version=$version Files=$fileCount NameOffset=0x{0:X} NameSize=0x{1:X} FileLen=0x{2:X}" -f $nameOffset, $nameSize, $bytes.Length

# First entry
$e = 0x58
$unk1     = [BitConverter]::ToUInt32($bytes, $e + 0)
$nOff     = [BitConverter]::ToUInt32($bytes, $e + 4)
$nSize    = [BitConverter]::ToUInt32($bytes, $e + 8)
$dOff     = [BitConverter]::ToUInt32($bytes, $e + 12)
$cSize    = [BitConverter]::ToUInt32($bytes, $e + 16)
$type     = [BitConverter]::ToUInt32($bytes, $e + 20)
$uSize    = [BitConverter]::ToUInt32($bytes, $e + 24)
$name = [Text.Encoding]::ASCII.GetString($bytes, $nOff, $nSize).TrimEnd([char]0)
"Entry0: name='$name' dataOff=0x{0:X} csize=0x{1:X} type=$type usize=0x{2:X}" -f $dOff, $cSize, $uSize

# Decompress (LZ77 variant)
function Decompress-Dk2([byte[]]$src, [int]$decsizeExpected) {
    $i = 0; $j = 0
    if (($src[$i++] -band 1) -ne 0) { $i += 3 }
    $i++
    $decsize = ($src[$i] -shl 16) + ($src[$i+1] -shl 8) + $src[$i+2]
    $i += 3
    "Embedded decsize=0x{0:X} (expected 0x{1:X})" -f $decsize, $decsizeExpected | Out-Host
    $dest = New-Object byte[] $decsize
    $finished = $false
    while (-not $finished -and $i -lt $src.Length) {
        $flag = $src[$i++]
        if (($flag -band 0x80) -eq 0) {
            $tmp = $src[$i++]
            $counter = $flag -band 3
            while ($counter-- -ne 0) { $dest[$j++] = $src[$i++] }
            $k = $j - (($flag -band 0x60) -shl 3) - $tmp - 1
            $counter = (($flag -shr 2) -band 7) + 2
            do { $dest[$j++] = $dest[$k++] } while ($counter-- -ne 0)
        }
        elseif (($flag -band 0x40) -eq 0) {
            $tmp = $src[$i++]; $tmp2 = $src[$i++]
            $counter = $tmp -shr 6
            while ($counter-- -ne 0) { $dest[$j++] = $src[$i++] }
            $k = $j - (($tmp -band 0x3F) -shl 8) - $tmp2 - 1
            $counter = ($flag -band 0x3F) + 3
            do { $dest[$j++] = $dest[$k++] } while ($counter-- -ne 0)
        }
        elseif (($flag -band 0x20) -eq 0) {
            $t1 = $src[$i++]; $t2 = $src[$i++]; $t3 = $src[$i++]
            $counter = $flag -band 3
            while ($counter-- -ne 0) { $dest[$j++] = $src[$i++] }
            $k = $j - (($flag -band 0x10) -shl 12) - ($t1 -shl 8) - $t2 - 1
            $counter = $t3 + (($flag -band 0x0C) -shl 6) + 4
            do { $dest[$j++] = $dest[$k++] } while ($counter-- -ne 0)
        }
        else {
            $counter = ($flag -band 0x1F) * 4 + 4
            if (($counter -band 0xFF) -gt 0x70) {
                $finished = $true
                $counter = $flag -band 3
            }
            while ($counter-- -ne 0) { $dest[$j++] = $src[$i++] }
        }
    }
    "Wrote $j of $decsize bytes" | Out-Host
    return $dest
}

$comp = $bytes[$dOff..($dOff + $cSize - 1)]
$out = Decompress-Dk2 $comp $uSize
$outMagic = [Text.Encoding]::ASCII.GetString($out, 0, 4)
"Decompressed magic: '$outMagic' (expect KMSH)"
"First 64 bytes: " + (($out[0..63] | ForEach-Object { $_.ToString('X2') }) -join ' ')
