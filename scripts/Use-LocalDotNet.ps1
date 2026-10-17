# LemonVN development only: dot-source this file to use the workspace-local SDK.
# Does not install anything globally or change persistent environment settings.
$ErrorActionPreference = 'Stop'
$lemonWorkspace = Split-Path -Parent $PSScriptRoot
$lemonSdkRoot = Join-Path $lemonWorkspace '.tools/dotnet10'
if (-not (Test-Path -LiteralPath (Join-Path $lemonSdkRoot 'dotnet.exe')))
{
    throw 'Workspace-local SDK is missing. See docs/M0_SOURCE_AUDIT.md for setup.'
}

$env:DOTNET_ROOT = $lemonSdkRoot
$env:DOTNET_ROOT_X64 = $lemonSdkRoot
$env:PATH = $lemonSdkRoot + [IO.Path]::PathSeparator + $env:PATH
$env:DOTNET_CLI_HOME = Join-Path $lemonWorkspace '.tools/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $lemonWorkspace '.tools/nuget-packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $lemonWorkspace '.tools/nuget-http-cache'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
