# Measures how much blacker the picture in a .cdg is than the video it was made from.
# A wiped lyric line shows up as frames that are blacker than the source, because the
# black stays on screen while the next line waits for the packets to draw it.
param(
    [Parameter(Mandatory = $true)][string]$ComparisonDirectory,
    [int]$PixelStride = 8
)

$source = [IO.File]::ReadAllBytes((Join-Path $ComparisonDirectory 'source.raw'))
$cdg = [IO.File]::ReadAllBytes((Join-Path $ComparisonDirectory 'cdg.raw'))
$frameBytes = 300 * 216 * 3
$frames = [int]($cdg.Length / $frameBytes)
$darkLimit = 24

$blackerFrames = 0
$worst = 0.0

for ($frame = 0; $frame -lt $frames; $frame++) {
    $offset = $frame * $frameBytes
    $sourceBlack = 0
    $cdgBlack = 0
    $samples = 0

    for ($pixel = 0; $pixel -lt 64800; $pixel += $PixelStride) {
        $index = $offset + ($pixel * 3)
        if ($source[$index] -lt $darkLimit -and $source[$index + 1] -lt $darkLimit -and $source[$index + 2] -lt $darkLimit) { $sourceBlack++ }
        if ($cdg[$index] -lt $darkLimit -and $cdg[$index + 1] -lt $darkLimit -and $cdg[$index + 2] -lt $darkLimit) { $cdgBlack++ }
        $samples++
    }

    $delta = (($cdgBlack - $sourceBlack) * 100.0) / $samples
    if ($delta -gt 1) { $blackerFrames++ }
    if ($delta -gt $worst) { $worst = $delta }
}

Write-Output ("{0}: {1} of {2} frames more than 1% blacker than the source, worst {3:0.0}%" -f $ComparisonDirectory, $blackerFrames, $frames, $worst)
