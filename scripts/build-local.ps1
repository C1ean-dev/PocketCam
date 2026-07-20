[CmdletBinding()]
param(
    [ValidateSet('All', 'Windows', 'Android')]
    [string]$Target = 'All',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

if ($Target -in @('All', 'Windows')) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw '.NET SDK 10 não foi encontrado.'
    }
    dotnet test "$repo/PocketCam.sln" -c $Configuration
    dotnet publish "$repo/src/PocketCam.Desktop/PocketCam.Desktop.csproj" -c $Configuration -r win-x64 -o "$repo/artifacts/windows/PocketCam"
    dotnet publish "$repo/src/PocketCam.VirtualCamera.Host/PocketCam.VirtualCamera.Host.csproj" -c $Configuration -r win-x64 -o "$repo/artifacts/windows/PocketCam/virtual-camera"
    dotnet publish "$repo/src/PocketCam.VirtualCamera.Source/PocketCam.VirtualCamera.Source.csproj" -c $Configuration -r win-x64 -o "$repo/artifacts/windows/PocketCam/virtual-camera"
}

if ($Target -in @('All', 'Android')) {
    if (-not (Get-Command gradle -ErrorAction SilentlyContinue)) {
        throw 'Gradle 8.11.1 não foi encontrado.'
    }
    Push-Location "$repo/android"
    try {
        gradle :app:testDebugUnitTest :app:lintDebug :app:assembleDebug
    } finally {
        Pop-Location
    }
}

