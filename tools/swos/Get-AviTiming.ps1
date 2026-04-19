param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path)) {
    throw "File not found: $Path"
}

$bytes = [System.IO.File]::ReadAllBytes($Path)
if ($bytes.Length -lt 80) {
    throw "File is too small to be a valid AVI: $Path"
}

$tag = [System.Text.Encoding]::ASCII.GetString($bytes, 0, 4)
if ($tag -ne "RIFF") {
    throw "Not a RIFF container: $Path"
}

function Find-AsciiTagOffset {
    param(
        [byte[]]$Data,
        [string]$AsciiTag
    )

    $needle = [System.Text.Encoding]::ASCII.GetBytes($AsciiTag)
    for ($i = 0; $i -le $Data.Length - $needle.Length; $i++) {
        $matched = $true
        for ($j = 0; $j -lt $needle.Length; $j++) {
            if ($Data[$i + $j] -ne $needle[$j]) {
                $matched = $false
                break
            }
        }

        if ($matched) {
            return $i
        }
    }

    return -1
}

$avihOffset = Find-AsciiTagOffset -Data $bytes -AsciiTag "avih"
if ($avihOffset -lt 0) {
    throw "AVI main header chunk 'avih' not found: $Path"
}

$chunkSize = [BitConverter]::ToUInt32($bytes, $avihOffset + 4)
if ($chunkSize -lt 40) {
    throw "AVI main header chunk too small ($chunkSize bytes): $Path"
}

$headerOffset = $avihOffset + 8
$microSecPerFrame = [BitConverter]::ToUInt32($bytes, $headerOffset + 0)
$maxBytesPerSec = [BitConverter]::ToUInt32($bytes, $headerOffset + 4)
$flags = [BitConverter]::ToUInt32($bytes, $headerOffset + 12)
$totalFrames = [BitConverter]::ToUInt32($bytes, $headerOffset + 16)
$streams = [BitConverter]::ToUInt32($bytes, $headerOffset + 24)
$width = [BitConverter]::ToUInt32($bytes, $headerOffset + 32)
$height = [BitConverter]::ToUInt32($bytes, $headerOffset + 36)

$fps = if ($microSecPerFrame -gt 0) { 1000000.0 / $microSecPerFrame } else { 0.0 }
$durationSeconds = if ($fps -gt 0 -and $totalFrames -gt 0) { $totalFrames / $fps } else { 0.0 }

[pscustomobject]@{
    Path = (Resolve-Path -LiteralPath $Path).Path
    Width = $width
    Height = $height
    TotalFrames = $totalFrames
    MicrosecondsPerFrame = $microSecPerFrame
    FramesPerSecond = [math]::Round($fps, 6)
    DurationSeconds = [math]::Round($durationSeconds, 6)
    Streams = $streams
    MaxBytesPerSecond = $maxBytesPerSec
    Flags = ('0x{0:X8}' -f $flags)
}
