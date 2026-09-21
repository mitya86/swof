param(
    [Parameter(Mandatory = $true)]
    [string] $SourcePath,

    [string] $ContentDirectory = (Join-Path $PSScriptRoot "..\Swof.Game\Content")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$columns = 4
$rows = 2
$cellWidth = 24
$cellHeight = 32

$palette = @(
    [System.Drawing.Color]::FromArgb(23, 21, 26),
    [System.Drawing.Color]::FromArgb(57, 51, 55),
    [System.Drawing.Color]::FromArgb(111, 103, 101),
    [System.Drawing.Color]::FromArgb(62, 32, 12),
    [System.Drawing.Color]::FromArgb(126, 76, 25),
    [System.Drawing.Color]::FromArgb(188, 125, 46),
    [System.Drawing.Color]::FromArgb(151, 70, 28),
    [System.Drawing.Color]::FromArgb(222, 126, 53),
    [System.Drawing.Color]::FromArgb(255, 196, 111),
    [System.Drawing.Color]::FromArgb(128, 125, 117),
    [System.Drawing.Color]::FromArgb(211, 208, 195),
    [System.Drawing.Color]::FromArgb(255, 248, 226),
    [System.Drawing.Color]::FromArgb(112, 15, 20),
    [System.Drawing.Color]::FromArgb(202, 27, 27),
    [System.Drawing.Color]::FromArgb(255, 76, 60),
    [System.Drawing.Color]::FromArgb(14, 42, 87),
    [System.Drawing.Color]::FromArgb(25, 88, 166),
    [System.Drawing.Color]::FromArgb(72, 157, 226)
)

function Test-ChromaKey([System.Drawing.Color] $color) {
    return $color.R -ge 205 `
        -and $color.B -ge 180 `
        -and $color.G -le 105 `
        -and ((($color.R + $color.B) / 2) - $color.G) -ge 115
}

function Find-PaletteColor([System.Drawing.Color] $color) {
    $best = $palette[0]
    $bestDistance = [double]::MaxValue

    foreach ($candidate in $palette) {
        $red = [int]$color.R - [int]$candidate.R
        $green = [int]$color.G - [int]$candidate.G
        $blue = [int]$color.B - [int]$candidate.B
        $distance = $red * $red + $green * $green + $blue * $blue
        if ($distance -lt $bestDistance) {
            $bestDistance = $distance
            $best = $candidate
        }
    }

    return $best
}

function Get-CellBounds(
    [int] $index,
    [int] $count,
    [int] $totalLength
) {
    $start = [int][Math]::Round($index * $totalLength / [double]$count)
    $end = [int][Math]::Round(($index + 1) * $totalLength / [double]$count)
    return [System.Drawing.Rectangle]::new($start, 0, $end - $start, 1)
}

$resolvedSource = (Resolve-Path -LiteralPath $SourcePath).Path
$resolvedContent = (Resolve-Path -LiteralPath $ContentDirectory).Path
$source = [System.Drawing.Bitmap]::FromFile($resolvedSource)

try {
    $sampled = [System.Drawing.Bitmap]::new(
        $columns * $cellWidth,
        $rows * $cellHeight,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    try {
        $graphics = [System.Drawing.Graphics]::FromImage($sampled)
        try {
            $graphics.Clear([System.Drawing.Color]::Magenta)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighSpeed
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None

            for ($row = 0; $row -lt $rows; $row++) {
                $sourceY = Get-CellBounds $row $rows $source.Height
                for ($column = 0; $column -lt $columns; $column++) {
                    $sourceX = Get-CellBounds $column $columns $source.Width
                    $sourceRectangle = [System.Drawing.Rectangle]::new(
                        $sourceX.X,
                        $sourceY.X,
                        $sourceX.Width,
                        $sourceY.Width)
                    $destinationRectangle = [System.Drawing.Rectangle]::new(
                        $column * $cellWidth,
                        $row * $cellHeight,
                        $cellWidth,
                        $cellHeight)
                    $graphics.DrawImage(
                        $source,
                        $destinationRectangle,
                        $sourceRectangle,
                        [System.Drawing.GraphicsUnit]::Pixel)
                }
            }
        }
        finally {
            $graphics.Dispose()
        }

        $runAtlas = [System.Drawing.Bitmap]::new(
            $sampled.Width,
            $sampled.Height,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

        try {
            for ($y = 0; $y -lt $sampled.Height; $y++) {
                for ($x = 0; $x -lt $sampled.Width; $x++) {
                    $pixel = $sampled.GetPixel($x, $y)
                    if (Test-ChromaKey $pixel) {
                        $runAtlas.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                    }
                    else {
                        $mapped = Find-PaletteColor $pixel
                        $runAtlas.SetPixel(
                            $x,
                            $y,
                            [System.Drawing.Color]::FromArgb(255, $mapped.R, $mapped.G, $mapped.B))
                    }
                }
            }

            $runAtlas.Save(
                (Join-Path $resolvedContent "PlayerRunAtlas.png"),
                [System.Drawing.Imaging.ImageFormat]::Png)

            $idleAtlas = [System.Drawing.Bitmap]::new(
                2 * $cellWidth,
                $cellHeight,
                [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

            try {
                $idleGraphics = [System.Drawing.Graphics]::FromImage($idleAtlas)
                try {
                    $idleGraphics.Clear([System.Drawing.Color]::Transparent)
                    for ($row = 0; $row -lt $rows; $row++) {
                        $sourceRectangle = [System.Drawing.Rectangle]::new(
                            $cellWidth,
                            $row * $cellHeight,
                            $cellWidth,
                            $cellHeight)
                        $destinationRectangle = [System.Drawing.Rectangle]::new(
                            $row * $cellWidth,
                            0,
                            $cellWidth,
                            $cellHeight)
                        $idleGraphics.DrawImage(
                            $runAtlas,
                            $destinationRectangle,
                            $sourceRectangle,
                            [System.Drawing.GraphicsUnit]::Pixel)
                    }
                }
                finally {
                    $idleGraphics.Dispose()
                }

                $idleAtlas.Save(
                    (Join-Path $resolvedContent "PlayerAtlasV2.png"),
                    [System.Drawing.Imaging.ImageFormat]::Png)
            }
            finally {
                $idleAtlas.Dispose()
            }
        }
        finally {
            $runAtlas.Dispose()
        }
    }
    finally {
        $sampled.Dispose()
    }

    $idleSourceWidth = [int][Math]::Round($source.Width / [double]$columns) * 2
    $idleSourceHeight = [int][Math]::Round($source.Height / [double]$rows)
    $idleSource = [System.Drawing.Bitmap]::new(
        $idleSourceWidth,
        $idleSourceHeight,
        [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)

    try {
        $idleSourceGraphics = [System.Drawing.Graphics]::FromImage($idleSource)
        try {
            $idleSourceGraphics.Clear([System.Drawing.Color]::Magenta)
            for ($row = 0; $row -lt $rows; $row++) {
                $sourceX = Get-CellBounds 1 $columns $source.Width
                $sourceY = Get-CellBounds $row $rows $source.Height
                $sourceRectangle = [System.Drawing.Rectangle]::new(
                    $sourceX.X,
                    $sourceY.X,
                    $sourceX.Width,
                    $sourceY.Width)
                $destinationRectangle = [System.Drawing.Rectangle]::new(
                    $row * ($idleSourceWidth / 2),
                    0,
                    $idleSourceWidth / 2,
                    $idleSourceHeight)
                $idleSourceGraphics.DrawImage(
                    $source,
                    $destinationRectangle,
                    $sourceRectangle,
                    [System.Drawing.GraphicsUnit]::Pixel)
            }
        }
        finally {
            $idleSourceGraphics.Dispose()
        }

        $idleSource.Save(
            (Join-Path $resolvedContent "PlayerAtlasV2.source.png"),
            [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $idleSource.Dispose()
    }
}
finally {
    $source.Dispose()
}

Write-Output "Built 24x32 player cells with a fixed 18-color palette."
