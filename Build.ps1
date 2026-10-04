param(
    [string]$ToolCache = (Join-Path $PSScriptRoot '.build-tools'),
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$ToolCache = [System.IO.Path]::GetFullPath($ToolCache)
New-Item -ItemType Directory -Path $ToolCache -Force | Out-Null

# Use pinned NuGet packages with the MSBuild included in Windows.
function Get-BuildPackage([string]$Id, [string]$Version) {
    $folder = Join-Path $ToolCache $Id
    $nuspec = Join-Path $folder ($Id + '.nuspec')
    $cachedVersion = ''
    if (Test-Path -LiteralPath $nuspec) {
        [xml]$metadata = Get-Content -LiteralPath $nuspec -Raw
        $cachedVersion = $metadata.package.metadata.version
    }
    if ($cachedVersion -ne $Version) {
        $archive = Join-Path $ToolCache ($Id + '.' + $Version + '.zip')
        $url = 'https://api.nuget.org/v3-flatcontainer/' + $Id + '/' + $Version + '/' + $Id + '.' + $Version + '.nupkg'
        Write-Host "Загрузка инструмента: $Id $Version"
        Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
        Expand-Archive -LiteralPath $archive -DestinationPath $folder -Force
    }
    return $folder
}
$compilerPackage = Get-BuildPackage 'microsoft.net.compilers.toolset' '3.11.0'
$referencePackage = Get-BuildPackage 'microsoft.netframework.referenceassemblies.net472' '1.0.3'
$compilerFolder = Join-Path $compilerPackage 'tasks\net472'
$referenceRoot = Join-Path $referencePackage 'build'
$referenceFolder = Join-Path $referenceRoot '.NETFramework\v4.7.2'
$msbuildCommand = Get-Command msbuild.exe -ErrorAction SilentlyContinue
if ($msbuildCommand) {
    $msbuild = $msbuildCommand.Source
    $toolsVersion = @()
} else {
    $msbuild = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'
    if (!(Test-Path -LiteralPath $msbuild)) {
        $msbuild = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\MSBuild.exe'
    }
    $toolsVersion = @('/ToolsVersion:4.0')
}
if (!(Test-Path -LiteralPath $msbuild)) { throw 'MSBuild не найден. Требуется Windows с .NET Framework 4.7.2 или новее.' }

Push-Location $projectRoot
try {
    $msbuildArgs = @((Join-Path $projectRoot 'RemnantSaveManager.csproj'), '/t:Build', '/p:Configuration=Release',
        "/p:CscToolPath=$compilerFolder", "/p:TargetFrameworkRootPath=$referenceRoot\",
        "/p:FrameworkPathOverride=$referenceFolder", '/verbosity:minimal', '/nologo')
    & $msbuild @msbuildArgs @toolsVersion
    if ($LASTEXITCODE -ne 0) { throw 'Сборка завершилась с ошибкой.' }
    $output = Join-Path $projectRoot 'bin\Release'

    if ($Test) {
        $testRun = Join-Path $projectRoot '.test-run'
        New-Item -ItemType Directory -Path $testRun -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $output 'RemnantSaveManager.exe') -Destination $testRun -Force
        $argsForCompiler = @('/nologo', '/nostdlib', '/target:exe',
            "/out:$testRun\LocalizationChecks.exe", "/reference:$testRun\RemnantSaveManager.exe")
        foreach ($name in @('mscorlib','System','System.Core','System.Configuration','System.Xaml','System.Xml','System.Xml.Linq','WindowsBase','PresentationCore','PresentationFramework')) {
            $argsForCompiler += '/reference:' + (Join-Path $referenceFolder ($name + '.dll'))
        }
        & (Join-Path $compilerFolder 'csc.exe') @argsForCompiler (Join-Path $projectRoot 'Tests\LocalizationChecks.cs')
        if ($LASTEXITCODE -ne 0) { throw 'Не удалось скомпилировать проверки.' }
        & (Join-Path $testRun 'LocalizationChecks.exe')
        if ($LASTEXITCODE -ne 0) { throw 'Проверки локализации не прошли.' }
    }

    $distribution = Join-Path $projectRoot 'dist\RemnantSaveManager-ru'
    New-Item -ItemType Directory -Path $distribution -Force | Out-Null
    foreach ($name in @('RemnantSaveManager.exe','RemnantSaveManager.exe.config','GameInfo.xml')) {
        Copy-Item -LiteralPath (Join-Path $output $name) -Destination $distribution -Force
    }
    foreach ($name in @('LICENSE','README.md','README.ru.md')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $distribution -Force
    }
    $zip = Join-Path $projectRoot 'dist\RemnantSaveManager-ru.zip'
    Compress-Archive -LiteralPath $distribution -DestinationPath $zip -Force
    Write-Host "Готовая программа: $distribution\RemnantSaveManager.exe"
    Write-Host "Архив: $zip"
} finally {
    Pop-Location
}
