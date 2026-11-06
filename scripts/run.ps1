# Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
[CmdletBinding()]
param([switch]$BuildOnly, [switch]$Test, [switch]$Export, [switch]$Compatibility)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$([IO.Path]::GetFileName($Command)) exited with code $LASTEXITCODE." }
}

# Reuse the existing optional portable tools without altering system installations.
$toolRoots = @((Join-Path $env:LOCALAPPDATA 'BoomingStore\Tools'), (Join-Path $env:LOCALAPPDATA 'BartenderSpecialogist\Tools'))
foreach ($root in $toolRoots) {
    if (Test-Path (Join-Path $root 'dotnet\dotnet.exe')) {
        $env:DOTNET_ROOT = Join-Path $root 'dotnet'
        $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
        break
    }
}
$godot = $env:GODOT_BIN
if (-not $godot) { $godot = (Get-Command godot, godot-mono -ErrorAction SilentlyContinue | Select-Object -First 1).Source }
foreach ($root in $toolRoots) {
    if (-not $godot -and (Test-Path (Join-Path $root 'godot'))) {
        $godot = Get-ChildItem -LiteralPath (Join-Path $root 'godot') -Recurse -Filter '*console.exe' | Select-Object -First 1 -ExpandProperty FullName
    }
}
if (-not $godot) { throw 'Install Godot 4.7.2 .NET and set GODOT_BIN to its executable.' }

Push-Location -LiteralPath $projectRoot
try {
    $buildsRoot = Join-Path $projectRoot 'builds'
    New-Item -ItemType Directory -Force -Path $buildsRoot | Out-Null
    Set-Content -LiteralPath (Join-Path $buildsRoot '.gdignore') -Value ''
    Invoke-Checked dotnet @('build', '--nologo')
    Invoke-Checked $godot @('--headless', '--path', $projectRoot, '--editor', '--import', '--quit')
    if ($Test -or $Export) {
        $results = Join-Path $projectRoot '.godot\test-results'
        Invoke-Checked dotnet @('test', 'Tests/Unit/BoomingStore.Tests.csproj', '--no-build', '--no-restore', '--nologo',
            '--logger', 'trx;LogFileName=unit-tests.trx', '--results-directory', $results)
        [xml]$report = Get-Content -LiteralPath (Join-Path $results 'unit-tests.trx') -Raw
        $counters = $report.TestRun.ResultSummary.Counters
        if ([int]$counters.passed -lt 1 -or [int]$counters.failed -gt 0) { throw 'No passing unit tests were recorded.' }
        Write-Output "UNIT_TESTS passed=$($counters.passed)"
        $sceneLog = Join-Path $results 'scene.log'
        Invoke-Checked $godot @('--headless', '--path', $projectRoot, '--fixed-fps', '60', '--log-file', $sceneLog,
            '--quit-after', '13000', '--', '--test')
        if ((Select-String -LiteralPath $sceneLog -Pattern '^(ERROR:|SHADER ERROR:|SCRIPT ERROR:)' -Quiet) -or
            -not (Select-String -LiteralPath $sceneLog -Pattern 'SCENE_CHECKS passed=' -SimpleMatch -Quiet)) {
            throw "Godot scene checks failed or timed out. See $sceneLog"
        }
    }
    if ($Export) {
        $version = (Get-Content -LiteralPath 'VERSION' -Raw).Trim()
        if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must contain a version such as 0.1.0.' }
        $buildRoot = Join-Path $projectRoot "builds\$version\windows"
        New-Item -ItemType Directory -Force -Path $buildRoot | Out-Null
        Invoke-Checked $godot @('--headless', '--path', $projectRoot, '--export-release', 'Windows Desktop', (Join-Path $buildRoot 'BoomingStore.exe'))
        if (-not (Test-Path (Join-Path $buildRoot 'data_BoomingStore_windows_x86_64\BoomingStore.dll'))) { throw 'The export is missing its C# assembly.' }
        foreach ($file in @('LICENSE', 'THIRD_PARTY_NOTICES.md', 'README.md', 'VERSION')) { Copy-Item -LiteralPath $file -Destination $buildRoot -Force }
        foreach ($folder in @('LICENSES', 'docs')) { Copy-Item -LiteralPath $folder -Destination $buildRoot -Recurse -Force }
        $testDocs = Join-Path $buildRoot 'Tests'
        New-Item -ItemType Directory -Force -Path $testDocs | Out-Null
        Copy-Item -LiteralPath 'Tests\README.md' -Destination $testDocs -Force
        foreach ($folder in @('Customer', 'Materials')) {
            $destination = Join-Path $buildRoot "Assets\$folder"
            New-Item -ItemType Directory -Force -Path $destination | Out-Null
            Copy-Item -LiteralPath "Assets\$folder\sources.json" -Destination $destination -Force
            Copy-Item -LiteralPath "Assets\$folder\SOURCES.md" -Destination $destination -Force
        }
        $zip = Join-Path $projectRoot "builds\BoomingStore-$version-windows.zip"
        Compress-Archive -Path (Join-Path $buildRoot '*') -DestinationPath $zip -CompressionLevel Optimal -Force
        Write-Output "ZIP: $zip"
    } elseif (-not $BuildOnly -and -not $Test) {
        $arguments = @('--path', $projectRoot)
        if ($Compatibility) { $arguments += @('--rendering-method', 'gl_compatibility') }
        Invoke-Checked $godot $arguments
    }
} finally { Pop-Location }
