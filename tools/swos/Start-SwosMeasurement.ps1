param(
    [string]$SwosDir = "C:\games\swos_96_97\SensiWs9\SOC",
    [string]$CdImage = "C:\games\swos_96_97\SensiWs9\cd\SWOS96-97.cue",
    [string]$DosboxExe = "C:\Program Files (x86)\DOSBox-0.74-3\DOSBox.exe",
    [ValidateSet("SB16", "MT32", "SC55")]
    [string]$Profile = "SB16",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $DosboxExe)) {
    throw "DOSBox executable not found: $DosboxExe"
}

if (-not (Test-Path -LiteralPath $SwosDir)) {
    throw "SWOS directory not found: $SwosDir"
}

if (-not (Test-Path -LiteralPath $CdImage)) {
    throw "CD image not found: $CdImage"
}

$confPath = Join-Path $PSScriptRoot "dosbox-swos-measurement.conf"
if (-not (Test-Path -LiteralPath $confPath)) {
    throw "Measurement DOSBox config not found: $confPath"
}

$cueContent = Get-Content -LiteralPath $CdImage -ErrorAction Stop
$binLine = $cueContent | Where-Object { $_ -match '^\s*FILE\s+"([^"]+)"\s+' } | Select-Object -First 1
if ($binLine) {
    $binRelative = [regex]::Match($binLine, '^\s*FILE\s+"([^"]+)"\s+').Groups[1].Value
    $cdDir = [System.IO.Path]::GetDirectoryName($CdImage)
    $binPath = Join-Path $cdDir $binRelative
    if (-not (Test-Path -LiteralPath $binPath)) {
        throw "CUE references missing file: $binPath"
    }
}

$mountCmd = "mount c `"$SwosDir`""
$mountCdCmd = "imgmount d `"$CdImage`" -t cdrom"
$profileCmd = switch ($Profile) {
    "SB16" { "if exist .\sb16\*.* copy /Y .\sb16\*.*" }
    "MT32" { "if exist .\mt32\*.* copy /Y .\mt32\*.*" }
    "SC55" { "if exist .\sc55\*.* copy /Y .\sc55\*.*" }
}

$args = @(
    "-conf", $confPath,
    "-c", $mountCmd,
    "-c", $mountCdCmd,
    "-c", "c:",
    "-c", "del MDI.INI",
    "-c", $profileCmd,
    "-c", "sws"
)

function Quote-ProcessArgument {
    param([string]$Value)

    if ($null -eq $Value) {
        return '""'
    }

    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    return '"' + ($Value -replace '"', '\"') + '"'
}

$argumentLine = ($args | ForEach-Object { Quote-ProcessArgument $_ }) -join " "

Write-Host "Launching SWOS measurement session..."
Write-Host "SWOS dir : $SwosDir"
Write-Host "CD image : $CdImage"
Write-Host "DOSBox   : $DosboxExe"
Write-Host "Profile  : $Profile"

if ($DryRun) {
    Write-Host "Dry run: DOSBox was not started."
    Write-Host "Args: $argumentLine"
    return
}

Start-Process -FilePath $DosboxExe -ArgumentList $argumentLine
