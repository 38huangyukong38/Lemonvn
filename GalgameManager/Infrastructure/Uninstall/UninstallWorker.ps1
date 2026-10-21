param([Parameter(Mandatory=$true)][string]$PlanPath,
      [Parameter(Mandatory=$true)][string]$PlanSha256,
      [switch]$Quiet)
$ErrorActionPreference='Stop'
$lemonPhase='prevalidation'
$lemonSession=$null
function Assert-NoLink([string]$Path) {
    $lemonCurrent=[IO.Path]::GetFullPath($Path)
    while($lemonCurrent){
        if(Test-Path -LiteralPath $lemonCurrent){
            if((Get-Item -LiteralPath $lemonCurrent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked target'}
        }
        $lemonParent=[IO.Directory]::GetParent($lemonCurrent)
        if($null -eq $lemonParent){break}
        $lemonCurrent=$lemonParent.FullName
    }
}
function Inside([string]$Child,[string]$Parent) {
    return $Child.Equals($Parent,[StringComparison]::OrdinalIgnoreCase) -or $Child.StartsWith($Parent.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)
}
function Safe-Root([string]$Path) {
    if([string]::IsNullOrWhiteSpace($Path) -or $Path -notmatch '^[A-Za-z]:[\\/]' -or $Path.StartsWith('\\')){throw 'Invalid local root'}
    $lemonFull=[IO.Path]::GetFullPath($Path).TrimEnd('\')
    $lemonProtected=@([IO.Path]::GetPathRoot($lemonFull),[IO.Path]::GetTempPath())
    foreach($lemonSpecial in @('UserProfile','LocalApplicationData','ApplicationData','Windows','System','ProgramFiles','ProgramFilesX86','DesktopDirectory','MyDocuments')){
        $lemonProtected += [Environment]::GetFolderPath($lemonSpecial)
    }
    foreach($lemonProtectedPath in $lemonProtected){
        if($lemonProtectedPath -and (Inside $lemonProtectedPath.TrimEnd('\') $lemonFull)){throw 'Protected directory or ancestor'}
    }
    if((Test-Path -LiteralPath (Join-Path $lemonFull '.git')) -or (Test-Path -LiteralPath (Join-Path $lemonFull 'GalgameManager.sln'))){throw 'Source directory'}
    Assert-NoLink $lemonFull
    return $lemonFull
}
function Owned-File([string]$Root,[string]$Relative) {
    if(-not $Relative -or $Relative.Contains('\') -or $Relative.Contains(':')){throw 'Invalid relative file'}
    foreach($lemonPart in $Relative.Split('/')){
        if(-not $lemonPart -or $lemonPart -eq '.' -or $lemonPart -eq '..' -or $lemonPart.EndsWith('.') -or $lemonPart.EndsWith(' ') -or
            $lemonPart.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or $lemonPart -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?$'){throw 'Unsafe relative file'}
    }
    $lemonFull=[IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if($lemonFull -eq $Root -or -not (Inside $lemonFull $Root)){throw 'File escapes root'}
    Assert-NoLink $lemonFull
    return $lemonFull
}
function Tree-Entries([string]$Root) {
    if(-not (Test-Path -LiteralPath $Root)){return @()}
    $lemonRows=[Collections.Generic.List[object]]::new()
    $lemonPending=[Collections.Generic.Stack[string]]::new();$lemonPending.Push($Root)
    while($lemonPending.Count -gt 0){
        $lemonDirectory=$lemonPending.Pop()
        foreach($lemonItem in @(Get-ChildItem -LiteralPath $lemonDirectory -Force)){
            if($lemonItem.Attributes -band [IO.FileAttributes]::ReparsePoint -or $lemonItem.Name -eq '.git'){throw 'Linked target or Git history'}
            $lemonRows.Add($lemonItem)
            if($lemonRows.Count -gt 1000000){throw 'Too many uninstall targets'}
            if($lemonItem.PSIsContainer){$lemonPending.Push($lemonItem.FullName)}
        }
    }
    return $lemonRows.ToArray()
}
function Assert-DataOwner([string]$Root) {
    if(-not (Test-Path -LiteralPath $Root)){return}
    if(-not @(Get-ChildItem -LiteralPath $Root -Force).Count){return}
    foreach($lemonName in @('settings.json','license-consent.json')){
        $lemonMarker=Join-Path $Root ('Settings\'+$lemonName)
        Assert-NoLink $lemonMarker
        if((Test-Path -LiteralPath $lemonMarker) -and (Get-Item -LiteralPath $lemonMarker).Length -le 16384){
            try {
                $lemonIdentity=Get-Content -LiteralPath $lemonMarker -Raw -Encoding UTF8|ConvertFrom-Json
                if($lemonIdentity.Application -ceq 'LemonVN.Desktop' -and $lemonIdentity.FormatVersion -eq 1){return}
            }catch{}
        }
    }
    throw 'Unrecognized data owner; application data was preserved'
}
function Show-Result([string]$Message,[bool]$Failed) {
    if($Quiet){Write-Output $Message;return}
    Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class LemonUninstallMessage { [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int MessageBoxW(IntPtr owner, string text, string title, uint type); }'
    $lemonKind=if($Failed){16}else{64}
    [void][LemonUninstallMessage]::MessageBoxW([IntPtr]::Zero,$Message,'LemonVN Uninstall',$lemonKind)
}
try {
    Assert-NoLink $PlanPath
    $lemonPlanSize=(Get-Item -LiteralPath $PlanPath).Length
    $lemonPlanHash=(Get-FileHash -LiteralPath $PlanPath -Algorithm SHA256).Hash
    if($Quiet){Write-Output ('Plan bytes: '+$lemonPlanSize+'; hash matches: '+($lemonPlanHash -eq $PlanSha256))}
    if($lemonPlanSize -gt 2097152 -or $lemonPlanHash -ne $PlanSha256){throw 'Plan fingerprint mismatch'}
    $lemonPlan=Get-Content -LiteralPath $PlanPath -Raw -Encoding UTF8|ConvertFrom-Json
    if($lemonPlan.FormatVersion -ne 1 -or $lemonPlan.Application -cne 'LemonVN.Desktop' -or $lemonPlan.Nonce -cnotmatch '^[0-9a-f]{32}$'){throw 'Unsupported plan'}
    $lemonSession=[IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('LemonVN.Uninstall\'+$lemonPlan.Nonce)))
    if([IO.Path]::GetFullPath($PlanPath) -ne (Join-Path $lemonSession 'plan.json') -or [IO.Path]::GetFullPath($PSCommandPath) -ne (Join-Path $lemonSession 'worker.ps1')){throw 'Not a private uninstall session'}
    Assert-NoLink $lemonSession
    $lemonApp=Safe-Root $lemonPlan.ApplicationRoot
    $lemonData=Safe-Root $lemonPlan.DataRoot
    if((Inside $lemonApp $lemonData) -or (Inside $lemonData $lemonApp)){throw 'Overlapping application/data roots'}
    if($lemonPlan.DeleteData -isnot [bool] -or @($lemonPlan.Files).Count -gt 4001){throw 'Invalid plan scope'}
    $lemonNames=@{}
    $lemonAppFiles=@(foreach($lemonRow in $lemonPlan.Files){
        $lemonFull=Owned-File $lemonApp $lemonRow.Path
        if($lemonNames.ContainsKey($lemonRow.Path) -or $lemonRow.Bytes -lt 0 -or $lemonRow.Bytes -gt 67108864 -or $lemonRow.Sha256 -notmatch '^[0-9A-Fa-f]{64}$'){throw 'Invalid file fingerprint'}
        $lemonNames[$lemonRow.Path]=$true
        [pscustomobject]@{Path=$lemonFull;Bytes=$lemonRow.Bytes;Sha256=$lemonRow.Sha256}
    })
    foreach($lemonName in @('LemonVN.exe','LemonVN.dll','Uninstall.exe','UninstallWorker.ps1','LICENSE','NOTICE','FILE_MANIFEST.json')){
        if(-not $lemonNames.ContainsKey($lemonName)){throw 'Missing owned application file'}
    }
    # Ready before waiting: the WinUI parent exits only after this receipt.
    [IO.File]::WriteAllText((Join-Path $lemonSession 'ready'),'ready')
    $lemonParentProcess=Get-Process -Id $lemonPlan.ParentProcessId -ErrorAction SilentlyContinue
    if($lemonParentProcess -and $lemonParentProcess.StartTime.ToUniversalTime().Ticks -eq $lemonPlan.ParentStartedUtcTicks){
        if(-not $lemonParentProcess.WaitForExit(30000)){throw 'Uninstaller parent did not exit'}
    }
    foreach($lemonProcess in @(Get-Process LemonVN -ErrorAction SilentlyContinue)){
        if($lemonPlan.DeleteData -or -not $lemonProcess.Path -or (Inside $lemonProcess.Path $lemonApp)){throw 'Close LemonVN before uninstalling'}
    }
    [void](Tree-Entries $lemonApp)
    $lemonDataEntries=@()
    if($lemonPlan.DeleteData){$lemonDataEntries=@(Tree-Entries $lemonData);Assert-DataOwner $lemonData}
    foreach($lemonFile in $lemonAppFiles){
        if(Test-Path -LiteralPath $lemonFile.Path){
            if((Get-Item -LiteralPath $lemonFile.Path).Length -ne $lemonFile.Bytes -or (Get-FileHash -LiteralPath $lemonFile.Path -Algorithm SHA256).Hash -ne $lemonFile.Sha256){throw 'Application changed after confirmation'}
        }
    }
    # Validate every target and file handle before the first destructive step.
    foreach($lemonFile in @($lemonAppFiles.Path)+@($lemonDataEntries|Where-Object {-not $_.PSIsContainer}|ForEach-Object {$_.FullName})){
        if(Test-Path -LiteralPath $lemonFile){$lemonHandle=[IO.File]::Open($lemonFile,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None);$lemonHandle.Dispose()}
    }
    $lemonPhase='application removal'
    $lemonDirs=@{}
    foreach($lemonFile in $lemonAppFiles){
        Assert-NoLink $lemonFile.Path
        if(Test-Path -LiteralPath $lemonFile.Path){Remove-Item -LiteralPath $lemonFile.Path -Force}
        $lemonDir=[IO.Path]::GetDirectoryName($lemonFile.Path)
        while($lemonDir -and (Inside $lemonDir $lemonApp)){$lemonDirs[$lemonDir]=$true;if($lemonDir -eq $lemonApp){break};$lemonDir=[IO.Path]::GetDirectoryName($lemonDir)}
    }
    # Remove only empty application directories. Unlisted user files survive.
    foreach($lemonDir in @($lemonDirs.Keys|Sort-Object Length -Descending)){
        Assert-NoLink $lemonDir
        if((Test-Path -LiteralPath $lemonDir) -and -not @(Get-ChildItem -LiteralPath $lemonDir -Force).Count){[IO.Directory]::Delete($lemonDir)}
    }
    if($lemonPlan.DeleteData -and (Test-Path -LiteralPath $lemonData)){
        $lemonPhase='confirmed local data removal'
        foreach($lemonItem in @($lemonDataEntries|Where-Object {-not $_.PSIsContainer})){
            Assert-NoLink $lemonItem.FullName
            if(-not (Inside $lemonItem.FullName $lemonData)){throw 'Data leaves approved root'}
            Remove-Item -LiteralPath $lemonItem.FullName -Force
        }
        foreach($lemonItem in @($lemonDataEntries|Where-Object {$_.PSIsContainer}|Sort-Object {$_.FullName.Length} -Descending)){
            Assert-NoLink $lemonItem.FullName
            [IO.Directory]::Delete($lemonItem.FullName)
        }
        [IO.Directory]::Delete($lemonData)
    }
    $lemonPhase='complete'
    $lemonMessage=if($lemonPlan.DeleteData){'LemonVN application files and the explicitly selected local data were removed.'}else{'LemonVN application files were removed. Your local collection, covers, settings and backups were kept.'}
    if(Test-Path -LiteralPath $lemonApp){$lemonMessage+=' Unlisted files in the application directory were preserved.'}
    Show-Result $lemonMessage $false
    # Delete just this session's known helper files; no delayed reboot/admin work.
    foreach($lemonName in @('ready','plan.json','worker.ps1')){
        $lemonTemporary=Join-Path $lemonSession $lemonName
        Assert-NoLink $lemonTemporary
        if(Test-Path -LiteralPath $lemonTemporary){Remove-Item -LiteralPath $lemonTemporary -Force}
    }
    if(-not @(Get-ChildItem -LiteralPath $lemonSession -Force).Count){[IO.Directory]::Delete($lemonSession)}
    exit 0
}catch{
    # Do not log private paths, raw data or arbitrary exception messages.
    if($lemonSession -and (Test-Path -LiteralPath $lemonSession)){
        [IO.File]::WriteAllText((Join-Path $lemonSession 'failure.txt'),('Phase: '+$lemonPhase+'; '+$_.Exception.GetType().Name+'; source line '+$_.InvocationInfo.ScriptLineNumber))
    }
    Show-Result ('Uninstall could not complete during '+$lemonPhase+' (source line '+$_.InvocationInfo.ScriptLineNumber+'; '+$_.Exception.GetType().Name+'). Files that were not removed were preserved. Check file permissions, running applications and the extracted ZIP manifest. No system runtime was removed.') $true
    exit 1
}
