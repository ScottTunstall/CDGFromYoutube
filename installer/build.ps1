<#
.SYNOPSIS
    Builds CDGFromYoutubeSetup.exe: publishes the program, then compiles the Inno Setup script around it.

.DESCRIPTION
    Two steps, run in order:
      1. dotnet publish, framework-dependent, so the installer (not the .exe) is what fetches the
         .NET 10 runtime. The result goes into installer\publish, which the .iss script picks up.
      2. ISCC.exe (the Inno Setup compiler) turns CDGFromYoutube.iss into Output\CDGFromYoutubeSetup.exe.

    Needs the .NET 10 SDK and Inno Setup 6 (https://jrsoftware.org/isinfo.php) installed.

.PARAMETER Version
    The version number written into the installed program's "Installed apps" entry. Defaults to the
    <Version> in CdgFromYoutube.csproj, which is also what --version reports, so the two stay in step.
#>
param(
    [string]$Version
)

$ErrorActionPreference = "Stop"
$installerDir = $PSScriptRoot
$repoRoot = Split-Path $installerDir -Parent
$publishDir = Join-Path $installerDir "publish"
$projectPath = Join-Path $repoRoot "src\CdgFromYoutube\CdgFromYoutube.csproj"

if (-not $Version) {
    $Version = ([xml](Get-Content $projectPath)).Project.PropertyGroup.Version
    if (-not $Version) {
        throw "No <Version> found in $projectPath, and -Version was not given."
    }
}

Write-Output "Publishing CdgFromYoutube $Version (framework-dependent, win-x64)..."
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $projectPath -c Release -r win-x64 --self-contained false -o $publishDir `
    -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

$isccPath = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $isccPath) {
    foreach ($candidate in @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe")) {
        if (Test-Path $candidate) { $isccPath = $candidate; break }
    }
}
if (-not $isccPath) {
    throw "Inno Setup 6 was not found. Install it with 'winget install JRSoftware.InnoSetup', or " +
        "from https://jrsoftware.org/isinfo.php, and run this script again."
}

Write-Output "Compiling the installer with $isccPath..."
& $isccPath "/DMyAppVersion=$Version" (Join-Path $installerDir "CDGFromYoutube.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC.exe failed." }

Write-Output "Done: $(Join-Path $installerDir 'Output\CDGFromYoutubeSetup.exe')"
