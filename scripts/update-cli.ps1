<#
.SYNOPSIS
    BaseForge.CodeGen'i (baseforge CLI) yerelde pack'leyip global dotnet tool olarak günceller.

.DESCRIPTION
    Directory.Build.props'taki resmi <Version>'a (git tag'lerle senkron, release pipeline'ının
    kontrolünde) hiç dokunmaz. Onun yerine, halihazırda kurulu local sürümü (varsa) baz alıp
    patch numarasını 1 artırarak "-local" etiketli, her seferinde kesin biçimde daha yeni bir
    sürüm üretir. Böylece `dotnet tool update` her çalıştırmada değişikliği görür.

.PARAMETER SkipWebBuild
    Designer ve Identity arayüzlerinin npm build adımlarını atlar (BaseForge.CodeGen.csproj'daki
    SkipWebBuild MSBuild property'sine karşılık gelir). Sadece CLI/codegen tarafında çalışırken
    hızlı iterasyon için kullanılır.
#>
[CmdletBinding()]
param(
    [switch]$SkipWebBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot  = Split-Path -Parent $PSScriptRoot
$csproj    = Join-Path $repoRoot 'src\BaseForge.CodeGen\BaseForge.CodeGen.csproj'
$localFeed = Join-Path $repoRoot 'localpkgs'
$packageId = 'BaseForge.CodeGen'

function Get-BasePropsVersion {
    $propsPath = Join-Path $repoRoot 'Directory.Build.props'
    [xml]$xml = Get-Content $propsPath
    $raw = $xml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $raw) { throw "Directory.Build.props içinde <Version> bulunamadı." }
    return ($raw -split '-')[0]
}

function Get-InstalledLocalVersion {
    $line = dotnet tool list -g 2>$null | Select-String -Pattern "^$([regex]::Escape($packageId.ToLower()))\s+(\S+)"
    if (-not $line) { return $null }
    return $line.Matches[0].Groups[1].Value
}

function Get-NextLocalVersion {
    $core = [version](Get-BasePropsVersion)

    $installed = Get-InstalledLocalVersion
    if ($installed -and $installed -match '^(\d+)\.(\d+)\.(\d+)-local$') {
        $installedCore = [version]"$($Matches[1]).$($Matches[2]).$($Matches[3])"
        if ($installedCore -ge $core) { $core = $installedCore }
    }

    return "{0}.{1}.{2}-local" -f $core.Major, $core.Minor, ($core.Build + 1)
}

$version = Get-NextLocalVersion
Write-Host "Yeni local sürüm: $version" -ForegroundColor Cyan

if (-not (Test-Path $localFeed)) {
    New-Item -ItemType Directory -Path $localFeed | Out-Null
}
Get-ChildItem $localFeed -Filter '*.nupkg' -ErrorAction SilentlyContinue | Remove-Item -Force

$packArgs = @('pack', $csproj, '-c', 'Release', '-o', $localFeed, "-p:Version=$version")
if ($SkipWebBuild) { $packArgs += '-p:SkipWebBuild=true' }

Write-Host "dotnet $($packArgs -join ' ')" -ForegroundColor DarkGray
& dotnet @packArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet pack başarısız oldu (exit $LASTEXITCODE)." }

if (Get-InstalledLocalVersion) {
    & dotnet tool update -g $packageId --add-source $localFeed --version $version
} else {
    & dotnet tool install -g $packageId --add-source $localFeed --version $version
}
if ($LASTEXITCODE -ne 0) { throw "dotnet tool update/install başarısız oldu (exit $LASTEXITCODE)." }

Write-Host "baseforge CLI güncellendi -> $version" -ForegroundColor Green
