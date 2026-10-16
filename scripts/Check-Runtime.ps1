# LemonVN modification: read-only current-user runtime diagnostic; never installs software.
param([string]$Requirements = (Join-Path $PSScriptRoot 'runtime-requirements.json'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Requirements))
{
    throw 'runtime-requirements.json is missing. Use the file supplied with this candidate.'
}
$lemonRequirement = Get-Content -LiteralPath $Requirements -Raw | ConvertFrom-Json
$lemonVersion = [Version]$lemonRequirement.minimumFrameworkVersion
$lemonPackages = @(Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.*' | Where-Object {
    $_.PackageFamilyName -eq $lemonRequirement.frameworkPackageFamily -and
    $_.Architecture.ToString() -eq 'X64' -and [Version]$_.Version -ge $lemonVersion
})
[pscustomobject]@{
    RequiredFamily = $lemonRequirement.frameworkPackageFamily
    MinimumVersion = $lemonVersion.ToString()
    Architecture = 'x64'
    CandidateFrameworkFound = ($lemonPackages.Count -gt 0)
    InstalledCandidates = @($lemonPackages | ForEach-Object { $_.Version.ToString() })
    IsWindowsAdministrator = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    Note = 'Read-only current-user registration check. Actual LemonVN Bootstrap.Initialize and GUI startup are the final compatibility check; DDLM and other runtime components must also be intact.'
} | ConvertTo-Json -Depth 5
if ($lemonPackages.Count -eq 0)
{
    Write-Warning 'No suitable current-user x64 framework found. See INSTALLATION_GUIDE.md for official Microsoft runtime installation instructions. Nothing has been installed or changed.'
    exit 2
}
