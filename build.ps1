[CmdletBinding()]
param(
    [string]$GamePath = 'D:\SteamLibrary\steamapps\common\Rain World',
    [string]$UserRequestId = 'c9c29f8ddc90474b8c7a0ec3bb45f9ec'
)

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath($PSScriptRoot)
$gameRoot = [IO.Path]::GetFullPath($GamePath)
$managed = Join-Path $gameRoot 'RainWorld_Data\Managed'
$core = Join-Path $gameRoot 'BepInEx\core'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$referenceRoot = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
$outputDll = Join-Path $workspace 'mod\newest\plugins\UniverseVoyager.dll'
$metadata = Get-Content -LiteralPath (Join-Path $workspace 'mod\modinfo.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$outputZip = Join-Path $workspace ('dist\UniverseVoyager-' + $metadata.version + '.zip')
$testExe = Join-Path $workspace 'artifacts\VisualMathTests.exe'
$auditModule = 'C:\Users\AyariaPrescott\.codex\tools\AgentAudit.psm1'

$references = @(
    (Join-Path $referenceRoot 'mscorlib.dll'),
    (Join-Path $referenceRoot 'System.dll'),
    (Join-Path $referenceRoot 'System.Core.dll'),
    (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $core 'BepInEx.dll'),
    (Join-Path $gameRoot 'BepInEx\plugins\HOOKS-Assembly-CSharp.dll')
)
foreach ($required in @($compiler, $auditModule) + $references) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Missing build dependency: $required"
    }
}

Import-Module $auditModule -Force
$transactionId = Start-AgentChange -Paths @($outputDll, $outputZip, $testExe) `
    -Summary '编译神经元猫视觉插件、运行离线检查并打包 Mod；只写入已登记的三个构建输出。' `
    -UserRequestId $UserRequestId -Actor codex -Workspace $workspace
Write-Host "BUILD_TRANSACTION=$transactionId"

try {
    foreach ($output in @($outputDll, $outputZip, $testExe)) {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
    }
    $common = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:5', '/codepage:65001',
        '/utf8output', '/debug-', '/optimize+', '/warn:4', '/warnaserror+')
    $referenceArgs = @($references | ForEach-Object { '/reference:' + $_ })
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $workspace 'src') -Filter '*.cs' |
        Sort-Object Name | Select-Object -ExpandProperty FullName)
    & $compiler @common '/target:library' "/out:$outputDll" @referenceArgs @sources
    if ($LASTEXITCODE -ne 0) { throw 'Mod compilation failed.' }

    $testReferences = @($references[0..2] | ForEach-Object { '/reference:' + $_ })
    & $compiler @common '/target:exe' "/out:$testExe" @testReferences `
        (Join-Path $workspace 'src\VisualMath.cs') (Join-Path $workspace 'src\NeuronMotion.cs') `
        (Join-Path $workspace 'tests\VisualMathTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Visual checks compilation failed.' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Visual math checks failed.' }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $stream = [IO.File]::Open($outputZip, [IO.FileMode]::Create, [IO.FileAccess]::Write)
    $archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $modRoot = Join-Path $workspace 'mod'
        foreach ($file in Get-ChildItem -LiteralPath $modRoot -Recurse -File) {
            $relative = $file.FullName.Substring($modRoot.Length + 1).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName,
                ('universevoyager/' + $relative), [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally { $archive.Dispose(); $stream.Dispose() }

    & (Join-Path $workspace 'tools\Verify-Mod.ps1') -GamePath $gameRoot
    Complete-AgentChange -TransactionId $transactionId | Out-Null
    [pscustomobject]@{ Dll = $outputDll; Package = $outputZip; AuditTransaction = $transactionId }
}
catch {
    Complete-AgentChange -TransactionId $transactionId -Outcome failed | Out-Null
    throw
}
