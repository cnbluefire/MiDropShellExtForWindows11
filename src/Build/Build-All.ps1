function GetVersion(){
    $versionText = Get-Content "version.txt"
    if ($versionText -eq "") { $versionText = '1.0.0' }

    $version1 = [System.Version]::Parse($versionText)
    $version2 = New-Object -TypeName System.Version -ArgumentList ($version1.Major, $version1.Minor, $version1.Build, 0)
    return $version2.ToString(4);
}

.\BuildCore.ps1 XiaomiShare
if(!$?){
    Write-Host "Build xiaomi share failed"
    return false
}

.\BuildCore.ps1 HonorShare
if(!$?){
    Write-Host "Build honor share failed"
    return false
}

.\BuildCore.ps1 HuaweiShare
if(!$?){
    Write-Host "Build huawei share failed"
    return false
}

$version = GetVersion
if (!(Test-Path -Path ".\Publish\$($version)")) {
    mkdir ".\Publish\$($version)"
}

Compress-Archive -Path ".\XiaomiShare\MiDropShellExt.Package_$($version)_Test\*" -DestinationPath ".\Publish\$($version)\XiaomiShare.Package_$($version).zip" -Force
Compress-Archive -Path ".\HonorShare\MiDropShellExt.Package_$($version)_Test\*" -DestinationPath ".\Publish\$($version)\HonorShare.Package_$($version).zip" -Force
Compress-Archive -Path ".\HuaweiShare\MiDropShellExt.Package_$($version)_Test\*" -DestinationPath ".\Publish\$($version)\HuaweiShare.Package_$($version).zip" -Force