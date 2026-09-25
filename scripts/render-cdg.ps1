# Renders the picture out of a .cdg file at a few moments, so that the encoded
# graphics can be inspected without a karaoke player. Raw frames are written out
# for ffmpeg to turn into images.
param(
    [Parameter(Mandatory = $true)][string]$CdgPath,
    [Parameter(Mandatory = $true)][int[]]$Seconds,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    # Show color zero as a blue backdrop, the way players such as KaraFun draw it as transparent.
    [switch]$ShowTransparency
)

$bytes = [IO.File]::ReadAllBytes($CdgPath)
$packetCount = [int]($bytes.Length / 24)
$width = 300
$height = 216
$palette = New-Object 'int[]' 48
$screen = New-Object 'byte[]' ($width * $height)
$targets = @{}
foreach ($second in $Seconds) { $targets[[int]($second * 300)] = $second }
$remaining = $targets.Count

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

for ($index = 0; $index -lt $packetCount -and $remaining -gt 0; $index++) {
    $offset = $index * 24
    if ($bytes[$offset] -eq 9) {
        $data = $offset + 4
        $instruction = $bytes[$offset + 1]

        if ($instruction -eq 1) {
            $fill = $bytes[$data]
            for ($pixel = 0; $pixel -lt $screen.Length; $pixel++) { $screen[$pixel] = $fill }
        }
        elseif ($instruction -eq 6 -or $instruction -eq 38) {
            $colour0 = $bytes[$data]
            $colour1 = $bytes[$data + 1]
            $firstX = $bytes[$data + 3] * 6
            $firstY = $bytes[$data + 2] * 12
            for ($y = 0; $y -lt 12; $y++) {
                $bits = $bytes[$data + 4 + $y]
                $rowStart = ($firstY + $y) * $width + $firstX
                for ($x = 0; $x -lt 6; $x++) {
                    $value = $colour0
                    if ((($bits -shr (5 - $x)) -band 1) -eq 1) { $value = $colour1 }
                    if ($instruction -eq 38) { $value = $value -bxor $screen[$rowStart + $x] }
                    $screen[$rowStart + $x] = $value
                }
            }
        }
        elseif ($instruction -eq 30 -or $instruction -eq 31) {
            $base = 0
            if ($instruction -eq 31) { $base = 8 }
            for ($n = 0; $n -lt 8; $n++) {
                $spec = ([int]$bytes[$data + (2 * $n)] -shl 8) -bor [int]$bytes[$data + (2 * $n) + 1]
                $palette[($base + $n) * 3] = (($spec -shr 10) -band 0xF) * 17
                $palette[($base + $n) * 3 + 1] = (((($spec -shr 6) -band 0xC) -bor (($spec -shr 4) -band 0x3))) * 17
                $palette[($base + $n) * 3 + 2] = ($spec -band 0xF) * 17
            }
        }
    }

    if ($targets.ContainsKey($index)) {
        $second = $targets[$index]
        $rgb = New-Object 'byte[]' ($width * $height * 3)
        for ($pixel = 0; $pixel -lt $screen.Length; $pixel++) {
            $colour = $screen[$pixel] * 3
            $rgb[$pixel * 3] = $palette[$colour]
            $rgb[$pixel * 3 + 1] = $palette[$colour + 1]
            $rgb[$pixel * 3 + 2] = $palette[$colour + 2]
            if ($ShowTransparency -and $screen[$pixel] -eq 0) { $rgb[$pixel * 3] = 20; $rgb[$pixel * 3 + 1] = 60; $rgb[$pixel * 3 + 2] = 200 }
        }

        $rawPath = Join-Path $OutputDirectory "at-$second.raw"
        [IO.File]::WriteAllBytes($rawPath, $rgb)
        Write-Output "rendered second $second at packet $index"
        $remaining--
    }
}
