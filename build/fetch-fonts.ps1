<#
.SYNOPSIS
  Downloads the Noto Sans fonts (SIL Open Font License 1.1) used by the Filee UI: Latin and Cyrillic only, Filee Lite ships English and Russian.

.DESCRIPTION
  Fonts are not committed to git. Run this once after cloning, and CI runs it before publishing.
  Without the fonts the app still works and falls back to system fonts.

  Static font files are used because Avalonia does not support variable fonts.
  Every file is pinned to a tag/commit and verified with SHA-256.

.EXAMPLE
  pwsh build/fetch-fonts.ps1
#>
[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $PSScriptRoot '..\src\Filee.App\Assets\Fonts')
)

$ErrorActionPreference = 'Stop'
$noto = 'https://raw.githubusercontent.com/notofonts/notofonts.github.io/28b15b4b43b7bed62b5cf6e6b0b5ff5846270535/fonts'
$latin = "$noto/NotoSans/hinted/ttf"

# File name => (URL, SHA-256). Update both together when bumping a version.
$fonts = [ordered]@{
    'NotoSans-Regular.ttf'           = @("$latin/NotoSans-Regular.ttf", '478C558EA716033CD60C03438F628DFA75694DCF6B5F6D505A2F05FD2B4F3823')
    'NotoSans-Medium.ttf'            = @("$latin/NotoSans-Medium.ttf", '635D93D1131D791F2576DE90B3BB0F7CDF61929906E8420A61B5F7F8E76420BB')
    'NotoSans-Bold.ttf'              = @("$latin/NotoSans-Bold.ttf", '1DF075A380FC7CB898ACF64C1F7B3B4DD780DE3CAA860178BF929DE35817A913')
}

New-Item -ItemType Directory -Force -Path $Destination | Out-Null

foreach ($name in $fonts.Keys) {
    $url, $expected = $fonts[$name]
    $target = Join-Path $Destination $name

    if (Test-Path $target) {
        $actual = (Get-FileHash $target -Algorithm SHA256).Hash
        if ($actual -eq $expected) {
            Write-Host "ok   $name (cached)"
            continue
        }
    }

    Write-Host "get  $name"
    Invoke-WebRequest -Uri $url -OutFile $target -UseBasicParsing
    $actual = (Get-FileHash $target -Algorithm SHA256).Hash
    if ($actual -ne $expected) {
        Remove-Item $target
        throw "Checksum mismatch for $name. Expected $expected, got $actual."
    }
}

Write-Host "Fonts ready in $Destination"
