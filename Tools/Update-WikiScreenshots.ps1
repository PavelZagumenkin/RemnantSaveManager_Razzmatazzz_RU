param(
    [string]$ToolCache = (Join-Path $PSScriptRoot '..\.build-tools')
)
$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ToolCache = [System.IO.Path]::GetFullPath($ToolCache)
$compiler = Join-Path $ToolCache 'microsoft.net.compilers.toolset\tasks\net472\csc.exe'
$references = Join-Path $ToolCache 'microsoft.netframework.referenceassemblies.net472\build\.NETFramework\v4.7.2'
$program = Join-Path $projectRoot 'bin\Release\RemnantSaveManager.exe'
if (!(Test-Path -LiteralPath $compiler) -or !(Test-Path -LiteralPath $program)) {
    throw 'Сначала выполните Build.ps1 с тем же параметром ToolCache.'
}
$renderRun = Join-Path $projectRoot '.test-run\wiki'
New-Item -ItemType Directory -Path $renderRun -Force | Out-Null
Copy-Item -LiteralPath $program -Destination $renderRun -Force
$compilerArgs = @('/nologo', '/nostdlib', '/target:exe',
    "/out:$renderRun\WikiScreenshots.exe", "/reference:$renderRun\RemnantSaveManager.exe")
foreach ($name in @('mscorlib','System','System.Core','System.Configuration','System.Xaml','System.Xml','System.Xml.Linq','WindowsBase','PresentationCore','PresentationFramework')) {
    $compilerArgs += '/reference:' + (Join-Path $references ($name + '.dll'))
}
& $compiler @compilerArgs (Join-Path $PSScriptRoot 'WikiScreenshots.cs')
if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать инструмент снимков интерфейса.' }
& (Join-Path $renderRun 'WikiScreenshots.exe') (Join-Path $projectRoot 'docs\wiki\images')
if ($LASTEXITCODE -ne 0) { throw 'Не удалось создать снимки интерфейса.' }
