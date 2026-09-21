[CmdletBinding()]
param(
    [ValidateSet('Fast', 'Full', 'Smoke')]
    [string]$Mode = 'Fast',

    [ValidateRange(2, 60)]
    [int]$SmokeSeconds = 8
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$originalLocation = Get-Location

function Invoke-CheckedCommand
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0)
    {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($ArgumentList -join ' ')"
    }
}

function Invoke-SmokeCheck
{
    param([int]$DurationSeconds)

    $stdoutPath = [System.IO.Path]::GetTempFileName()
    $stderrPath = [System.IO.Path]::GetTempFileName()
    $process = $null

    try
    {
        $process = Start-Process `
            -FilePath 'dotnet' `
            -ArgumentList @('run', '--project', 'Swof.Game\Swof.Game.csproj', '--no-build') `
            -PassThru `
            -WindowStyle Hidden `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath

        $exited = $process.WaitForExit($DurationSeconds * 1000)
        if ($exited)
        {
            $stdout = Get-Content -Raw -LiteralPath $stdoutPath -ErrorAction SilentlyContinue
            $stderr = Get-Content -Raw -LiteralPath $stderrPath -ErrorAction SilentlyContinue
            if ($stdout)
            {
                Write-Host $stdout
            }

            if ($stderr)
            {
                Write-Host $stderr
            }

            throw "Swof.Game exited before the $DurationSeconds-second smoke interval (exit code $($process.ExitCode))."
        }

        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()

        $capturedError = Get-Content -Raw -LiteralPath $stderrPath -ErrorAction SilentlyContinue
        if ($capturedError)
        {
            throw "Swof.Game wrote to stderr during startup:`n$capturedError"
        }

        Write-Host "Swof.Game remained alive for $DurationSeconds seconds with no captured stderr."
    }
    finally
    {
        if ($null -ne $process -and -not $process.HasExited)
        {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit()
        }

        Remove-Item -Force -LiteralPath $stdoutPath, $stderrPath -ErrorAction SilentlyContinue
    }
}

function Test-MgcbAvailable
{
    Push-Location (Join-Path $repoRoot 'Swof.Game')
    try
    {
        & dotnet tool run mgcb -- /help *> $null
        return $LASTEXITCODE -eq 0
    }
    finally
    {
        Pop-Location
    }
}

try
{
    Set-Location $repoRoot

    if ($Mode -eq 'Smoke')
    {
        Invoke-SmokeCheck -DurationSeconds $SmokeSeconds
        return
    }

    Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @(
        'run',
        '--project',
        'Swof.Engine.Tests\Swof.Engine.Tests.csproj',
        '--no-restore')

    if ($Mode -eq 'Full')
    {
        if (-not (Test-MgcbAvailable))
        {
            Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @(
                'tool',
                'restore',
                '--tool-manifest',
                'Swof.Game\.config\dotnet-tools.json')
        }

        Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @('test', 'Swof.sln', '--no-restore')
        Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @('build', 'Swof.sln', '--no-restore')
        Invoke-CheckedCommand -FilePath 'dotnet' -ArgumentList @(
            'format',
            'Swof.sln',
            '--no-restore',
            '--verify-no-changes')
    }

    Invoke-CheckedCommand -FilePath 'git' -ArgumentList @('diff', '--check')
}
finally
{
    Set-Location $originalLocation
}
