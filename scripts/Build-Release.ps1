# LemonVN modification: local unsigned unpackaged client build only; no upload/signing.
param([switch]$RunTests)
$ErrorActionPreference = 'Stop'
$lemonRoot = Split-Path -Parent $PSScriptRoot
Push-Location $lemonRoot
try
{
    $lemonProperties = @('-p:Platform=x64','-p:Configuration=Release','-p:WindowsPackageType=None',
        '-p:GenerateAppxPackageOnBuild=false','-p:AppxPackageSigningEnabled=false',
        '-p:GenerateTemporaryStoreCertificate=false','-m:1','-nr:false')
    & dotnet restore GalgameManager.Test/GalgameManager.Test.csproj --configfile NuGet.Config @lemonProperties
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    & dotnet build GalgameManager/GalgameManager.csproj --no-restore @lemonProperties
    if ($LASTEXITCODE -ne 0) { throw 'Client build failed.' }
    & dotnet build GalgameManager.Test/GalgameManager.Test.csproj --no-restore -p:BuildProjectReferences=false @lemonProperties
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    if ($RunTests)
    {
        & dotnet test GalgameManager.Test/GalgameManager.Test.csproj --no-build --no-restore -p:Platform=x64 -p:Configuration=Release --filter 'TestCategory!=Phraser&TestCategory!=E2E'
        if ($LASTEXITCODE -ne 0) { throw 'Offline tests failed.' }
    }
}
finally { Pop-Location }
