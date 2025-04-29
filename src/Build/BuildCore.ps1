function UpdateVersion($path){
    $versionText = Get-Content "version.txt"
    if ($versionText -eq "") { $versionText = '1.0.0' }

    $version1 = [System.Version]::Parse($versionText)
    $version2 = New-Object -TypeName System.Version -ArgumentList ($version1.Major, $version1.Minor, $version1.Build, 0)
    $fullVersionText = $version2.ToString(4);

    [xml]$manifest= get-content $path
    $manifest.Package.Identity.Version = $fullVersionText
    $manifest.save($path)
}

$SHARE_TARGET = $args[0]
if($SHARE_TARGET -eq "") {
    Write-Host 'Share target is empty'
    return false
}
if(($SHARE_TARGET -ne 'XiaomiShare') -and ($SHARE_TARGET -ne 'HonorShare') -and ($SHARE_TARGET -ne 'HuaweiShare')){
    Write-Host 'Unknown share target'
    return false
}

$scriptDir = (Split-Path -Parent $MyInvocation.MyCommand.Definition)
$OUTOUT_PATH="$scriptDir\$SHARE_TARGET"

$MSBUILD_PATH = & "$([Environment]::GetEnvironmentVariable("ProgramFiles(x86)"))\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
if($MSBUILD_PATH -eq ""){
    Write-Host 'Cannot find msbuild'
    return false
}

if ((Test-Path -Path '..\MiDropShellExt.Package\obj')) {
    Remove-Item '..\MiDropShellExt.Package\obj' -Recurse
}
if ((Test-Path -Path '..\MiDropShellExt.Package\bin')) {
    Remove-Item '..\MiDropShellExt.Package\bin' -Recurse
}

UpdateVersion("..\MiDropShellExt.Package\$($SHARE_TARGET).Package.appxmanifest");

& $MSBUILD_PATH ../MiDropShellExt.Package/MiDropShellExt.Package.wapproj `
  /p:ShareProjectTargetName=$SHARE_TARGET `
  /p:Configuration=Release `
  /p:Platform=x64 `
  /p:AppxBundlePlatforms=x64 `
  /p:OutputPath=NonPackagedApp `
  /p:UapAppxPackageBuildMode=SideLoadOnly `
  /p:AppxBundle=Always `
  /p:AppxPackageDir=$OUTOUT_PATH\ `
  /p:AppxPackageSigningEnabled=true `
  /p:PackageCertificateThumbprint=cfcc41845e3d8f50ffc5f35adf3f06788c717f7d `
  /p:PackageCertificatePassword=123456

return $?