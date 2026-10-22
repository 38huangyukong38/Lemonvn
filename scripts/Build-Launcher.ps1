param([string]$ZigPath=$env:LEMONVN_ZIG)
$ErrorActionPreference='Stop'
$lemonRoot=Split-Path -Parent $PSScriptRoot
if(-not $ZigPath){$lemonZig=Get-Command zig -ErrorAction SilentlyContinue;if($lemonZig){$ZigPath=$lemonZig.Source}}
if(-not $ZigPath -or -not(Test-Path -LiteralPath $ZigPath)){throw 'Build-only Zig0.15.2 is required; pass -ZigPath. End users do not need it.'}
$lemonVersion=& $ZigPath version
if($LASTEXITCODE -ne 0 -or $lemonVersion -ne '0.15.2'){throw 'Use the reviewed Zig0.15.2 compiler'}
$lemonOut=Join-Path $lemonRoot 'Launcher/bin/x64/Release'
New-Item -ItemType Directory -Path $lemonOut -Force|Out-Null
$env:ZIG_GLOBAL_CACHE_DIR=Join-Path $lemonRoot '.tools/zig-cache'
$env:ZIG_LOCAL_CACHE_DIR=Join-Path $lemonRoot '.tools/zig-local-cache'
$lemonIcon=(Join-Path $lemonRoot 'GalgameManager/Assets/LemonAppIcon.ico').Replace('\','/')
[xml]$lemonManifest=Get-Content -LiteralPath (Join-Path $lemonRoot 'GalgameManager/Package.appxmanifest') -Raw
$lemonVersionString=[string]$lemonManifest.Package.Identity.Version
if($lemonVersionString -notmatch '^\d+\.\d+\.\d+\.\d+$'){throw 'Invalid manifest version'}
$lemonVersionNumbers=$lemonVersionString.Replace('.',',')
$lemonNativeManifest=(Join-Path $lemonRoot 'Launcher/launcher.manifest').Replace('\','/')
$lemonRc=@"
1 ICON "$lemonIcon"
1 24 "$lemonNativeManifest"
1 VERSIONINFO
FILEVERSION $lemonVersionNumbers
PRODUCTVERSION $lemonVersionNumbers
FILEFLAGSMASK 0x3fL
FILEOS 0x40004L
FILETYPE 0x1L
BEGIN
  BLOCK "StringFileInfo"
  BEGIN
    BLOCK "040904B0"
    BEGIN
      VALUE "CompanyName", "LemonVN contributors\0"
      VALUE "FileDescription", "LemonVN launcher\0"
      VALUE "FileVersion", "$lemonVersionString\0"
      VALUE "ProductName", "LemonVN\0"
      VALUE "ProductVersion", "$lemonVersionString\0"
      VALUE "LegalCopyright", "Copyright 2026 LemonVN contributors. Apache-2.0.\0"
    END
  END
  BLOCK "VarFileInfo"
  BEGIN
    VALUE "Translation", 0x409, 1200
  END
END
"@
[IO.File]::WriteAllText((Join-Path $lemonOut 'launcher.rc'),$lemonRc,[Text.UTF8Encoding]::new($false))
& $ZigPath rc /nologo /c65001 /fo (Join-Path $lemonOut 'launcher.res') (Join-Path $lemonOut 'launcher.rc')
if($LASTEXITCODE -ne 0){throw 'Launcher resources failed'}
& $ZigPath cc -target x86_64-windows-gnu -Os -nostdlib -fno-builtin -fno-stack-protector (Join-Path $lemonRoot 'Launcher/launcher.c') (Join-Path $lemonOut 'launcher.res') '-Wl,--entry,LauncherEntry' '-Wl,--subsystem,windows' '-Wl,--dynamicbase,--nxcompat,--high-entropy-va' -lkernel32 -luser32 -o (Join-Path $lemonOut 'LemonVN.exe')
if($LASTEXITCODE -ne 0){throw 'Native launcher build failed'}
Get-Item -LiteralPath (Join-Path $lemonOut 'LemonVN.exe')|Select-Object Name,Length

$lemonReceipt=[ordered]@{
    compiler='Zig0.15.2'; productVersion=$lemonVersionString
    sourceSha256=(Get-FileHash -LiteralPath (Join-Path $lemonRoot 'Launcher/launcher.c') -Algorithm SHA256).Hash
    manifestSha256=(Get-FileHash -LiteralPath (Join-Path $lemonRoot 'Launcher/launcher.manifest') -Algorithm SHA256).Hash
    iconSha256=(Get-FileHash -LiteralPath (Join-Path $lemonRoot 'GalgameManager/Assets/LemonAppIcon.ico') -Algorithm SHA256).Hash
    binarySha256=(Get-FileHash -LiteralPath (Join-Path $lemonOut 'LemonVN.exe') -Algorithm SHA256).Hash
}
$lemonReceipt|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $lemonOut 'build-receipt.json') -Encoding utf8
