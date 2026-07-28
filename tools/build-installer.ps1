# Builds Breeze and its Windows installer in one step. Run from the repository root:
#
#   powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1
#
# Produces dist\Breeze-v<version>-Setup.exe, where the version is read from Breeze\Breeze.csproj,
# the single place it is defined. Preparing a release therefore means changing that one property.
#
# Needs the .NET SDK and Inno Setup 6. See docs\Installer.md.
param(
    [string] $Configuration = "Release",
    [switch] $SkipPublish
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$project = "Breeze\Breeze.csproj"
$staging = "installer\build"
$published = "$staging\app"
$bootstrapper = "$staging\MicrosoftEdgeWebview2Setup.exe"

# One authoritative version, the same property the About page reports.
$version = ([xml] (Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ }
if (-not $version) { throw "No <Version> found in $project." }
"Breeze v$version"

# Inno Setup installs per machine or per user depending on how it was installed, so both are tried,
# along with anything already on PATH.
$compiler = @(
    (Get-Command "ISCC.exe" -ErrorAction SilentlyContinue).Source
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $compiler) {
    throw "Inno Setup 6 was not found. Install it with: winget install JRSoftware.InnoSetup"
}

if (-not $SkipPublish) {
    if (Test-Path $published) { Remove-Item $published -Recurse -Force }

    # Self contained, so the installer carries the .NET runtime and end users need no prerequisite
    # beyond WebView2. The application's folder layout, Assets included, is preserved as published.
    & dotnet publish $project -c $Configuration -r win-x64 --self-contained true `
        -p:DebugType=none -p:DebugSymbols=false -p:PublishSingleFile=false `
        -o $published --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }
}

if (-not (Test-Path "$published\Breeze.exe")) { throw "Nothing published to $published." }

New-Item -ItemType Directory -Force -Path $staging | Out-Null

# The license page wants a file the wizard can display.
Copy-Item "LICENSE" "$staging\License.txt" -Force

# Microsoft's Evergreen bootstrapper: a small downloader, not a pinned runtime. Cached between
# builds, so it is fetched once.
if (-not (Test-Path $bootstrapper)) {
    "Downloading the WebView2 Evergreen bootstrapper..."
    Invoke-WebRequest "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile $bootstrapper
}

New-Item -ItemType Directory -Force -Path "dist" | Out-Null

& $compiler "/DAppVersion=$version" "installer\Breeze.iss"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed." }

$setup = Get-Item "dist\Breeze-v$version-Setup.exe"
"", "Installer: $($setup.FullName)", "Size: $([math]::Round($setup.Length / 1MB, 1)) MB"
