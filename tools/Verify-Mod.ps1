[CmdletBinding()]
param([string]$GamePath = 'D:\SteamLibrary\steamapps\common\Rain World')
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$modRoot = Join-Path $workspace 'mod'
$mod = Get-Content -LiteralPath (Join-Path $modRoot 'modinfo.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$character = Get-Content -LiteralPath (Join-Path $modRoot 'slugbase\universe_voyager.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$expectedName = '寰宇观游//虚拟神明'
if ($mod.name -cne $expectedName -or $character.name -cne $expectedName) { throw 'Character name changed.' }
if ($mod.id -cne 'ayaria.universevoyager' -or $character.id -cne 'Ayaria_UniverseVoyager') { throw 'Character identity mismatch.' }
if (@($mod.requirements).Count -ne 1 -or $mod.requirements[0] -ne 'slime-cubed.slugbase') { throw 'Unexpected dependency.' }
$featureNames = @($character.features.PSObject.Properties.Name | Sort-Object)
if (($featureNames -join ',') -cne 'color,world_state') { throw 'Unexpected character feature.' }
if ($character.features.color -cne 'FFFFFF' -or $character.features.world_state -cne 'White') { throw 'Unexpected base configuration.' }

Add-Type -Path (Join-Path $GamePath 'BepInEx\core\Mono.Cecil.dll')
$dllPath = Join-Path $modRoot 'newest\plugins\UniverseVoyager.dll'
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dllPath)
try {
    $plugin = $assembly.MainModule.Types | Where-Object FullName -eq 'UniverseVoyager.Plugin'
    if (-not $plugin) { throw 'Plugin entry point missing.' }
    $renderState = $assembly.MainModule.Types | Where-Object FullName -eq 'UniverseVoyager.RenderState'
    $opacity = ($renderState.Fields | Where-Object Name -eq 'BodyOpacity').Constant
    if ($opacity -gt 0.2 -or $opacity -le 0) { throw 'Ghost body opacity is not sufficiently reduced.' }
    $attribute = $plugin.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInPlugin' }
    if ($attribute.ConstructorArguments[0].Value -cne $mod.id -or
        $attribute.ConstructorArguments[1].Value -cne $expectedName -or
        $attribute.ConstructorArguments[2].Value -cne $mod.version) { throw 'Plugin metadata mismatch.' }
    $allowed = @('add_InitiateSprites','remove_InitiateSprites','add_DrawSprites','remove_DrawSprites','add_AddToContainer','remove_AddToContainer')
    $actual = @()
    $types = @($assembly.MainModule.Types)
    for ($i = 0; $i -lt $types.Count; $i++) { $types += @($types[$i].NestedTypes) }
    foreach ($type in $types) {
        foreach ($method in $type.Methods) {
            if (-not $method.HasBody) { continue }
            foreach ($instruction in $method.Body.Instructions) {
                $operand = $instruction.Operand
                if ($instruction.OpCode.Name -eq 'stfld' -and $operand -is [Mono.Cecil.FieldReference] -and
                    $operand.DeclaringType.FullName -in @('BodyChunk','Player','Creature','PhysicalObject')) {
                    throw "Visual displacement writes gameplay state: $operand"
                }
                if ($operand -isnot [Mono.Cecil.MethodReference]) { continue }
                $owner = $operand.DeclaringType.FullName
                if ($owner -eq 'UnityEngine.Random') { throw 'Visual code consumes gameplay randomness.' }
                if ($owner.StartsWith('On.') -and $operand.Name -match '^(add_|remove_)') {
                    if ($owner -cne 'On.PlayerGraphics' -or $operand.Name -notin $allowed) { throw "Unexpected game hook: $operand" }
                    $actual += $operand.Name
                }
                if ($instruction.OpCode.Name -eq 'newobj' -and $owner -in @('OracleSwarmer','SSOracleSwarmer','SLOracleSwarmer','LightSource','Explosion')) {
                    throw "Unexpected physical object or effect: $owner"
                }
            }
        }
    }
    if ((@($actual | Sort-Object -Unique) -join ',') -cne (@($allowed | Sort-Object) -join ',')) { throw 'Graphics hook registration/cleanup incomplete.' }
}
finally { $assembly.Dispose() }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $workspace ('dist\UniverseVoyager-' + $mod.version + '.zip')))
try {
    $entries = @($archive.Entries.FullName | Sort-Object)
    $expected = @('universevoyager/modinfo.json','universevoyager/newest/plugins/UniverseVoyager.dll','universevoyager/slugbase/universe_voyager.json') | Sort-Object
    if (($entries -join ',') -cne ($expected -join ',')) { throw 'Unexpected mod package contents.' }
}
finally { $archive.Dispose() }
Write-Host 'PASS: exact name, minimal character settings, graphics-only hooks, RNG isolation, balanced cleanup and package layout.'
