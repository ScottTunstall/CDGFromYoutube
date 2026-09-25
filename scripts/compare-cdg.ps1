# Compares the picture in a .cdg file against the video it was made from, one
# second at a time, and reports where the two disagree most. The CDG is decoded
# here exactly as a player would decode it.
param(
    [Parameter(Mandatory = $true)][string]$CdgPath,
    [Parameter(Mandatory = $true)][string]$SourcePath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$FfmpegPath
)

$width = 300
$height = 216
$FfmpegPath = (Resolve-Path $FfmpegPath).Path
$bytes = [IO.File]::ReadAllBytes($CdgPath)
$packetCount = [int]($bytes.Length / 24)
$lastSecond = [int]($packetCount / 300) - 1

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$cdgRaw = Join-Path $OutputDirectory 'cdg.raw'
$sourceRaw = Join-Path $OutputDirectory 'source.raw'

# The video, sampled one frame a second at the same size the encoder used.
& $FfmpegPath -v error -y -i $SourcePath -vf "fps=1,scale=300:216:force_original_aspect_ratio=decrease:force_divisible_by=2:flags=lanczos,pad=300:216:0:0:color=black,format=rgb24" -f rawvideo -pix_fmt rgb24 $sourceRaw
if ($LASTEXITCODE -ne 0) { throw 'could not decode the source video' }

# The CDG, decoded a packet at a time, writing the screen at each whole second.
$palette = New-Object 'int[]' 48
$screen = New-Object 'byte[]' ($width * $height)
$stream = [IO.File]::Create($cdgRaw)
$rendered = 0

for ($index = 0; $index -lt $packetCount; $index++) {
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

    if ($index % 300 -eq 0) {
        $rgb = New-Object 'byte[]' ($width * $height * 3)
        for ($pixel = 0; $pixel -lt $screen.Length; $pixel++) {
            $colour = $screen[$pixel] * 3
            $rgb[$pixel * 3] = $palette[$colour]
            $rgb[$pixel * 3 + 1] = $palette[$colour + 1]
            $rgb[$pixel * 3 + 2] = $palette[$colour + 2]
        }

        $stream.Write($rgb, 0, $rgb.Length)
        $rendered++
    }
}

$stream.Dispose()
Write-Output "decoded $rendered frames from $packetCount packets ($lastSecond seconds)"

# ffmpeg cannot hold a Windows path in a filter argument, so the per frame scores are
# written beside the frames and read back from there.
Push-Location $OutputDirectory
try {
    & $FfmpegPath -y -f rawvideo -pix_fmt rgb24 -s 300x216 -i $sourceRaw -f rawvideo -pix_fmt rgb24 -s 300x216 -i $cdgRaw -lavfi 'psnr=stats_file=psnr.log' -f null - 2>&1 | Out-Null
    $stats = Get-Content 'psnr.log'
}
finally {
    Pop-Location
}

$scores = @()
foreach ($line in $stats) {
    if ($line -match '^n:(\d+).*psnr_avg:([\d.]+|inf)') {
        $second = [int]$Matches[1] - 1
        $score = $Matches[2]
        if ($score -eq 'inf') { $score = 99 }
        $scores += [pscustomobject]@{ Second = $second; Psnr = [double]$score }
    }
}

if ($scores.Count -eq 0) { throw 'the comparison produced no results' }

# The first frame is the screen before any packet has been read, so it is not comparable.
$comparable = $scores | Where-Object { $_.Second -ge 1 }
$mean = ($comparable | Measure-Object -Property Psnr -Average).Average
Write-Output ("mean PSNR {0:0.0} dB over {1} frames" -f $mean, $comparable.Count)
Write-Output "worst ten seconds:"
$comparable | Sort-Object Psnr | Select-Object -First 10 | ForEach-Object { Write-Output ("  second {0}: {1:0.0} dB" -f $_.Second, $_.Psnr) }
$csv = ($comparable | Sort-Object Second | ForEach-Object { "{0},{1:0.0}" -f $_.Second, $_.Psnr }) -join ' '
Write-Output "by second: $csv"
