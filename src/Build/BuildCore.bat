@echo off
setlocal enabledelayedexpansion

set ShareProjectTargetName=%1
set OUTPUT_PATH=%cd%\!ShareProjectTargetName!

for /f "delims=" %%i in ('="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe') do set MSBUILD_PATH=%%i

rmdir /s /q ..\MiDropShellExt.Package\obj
rmdir /s /q ..\MiDropShellExt.Package\bin


"!MSBUILD_PATH!" ../MiDropShellExt.Package/MiDropShellExt.Package.wapproj /p:ShareProjectTargetName=!ShareProjectTargetName! /p:Configuration=Release /p:Platform=x64 /p:AppxBundlePlatforms=x64 /p:OutputPath=NonPackagedApp /p:UapAppxPackageBuildMode=SideLoadOnly /p:AppxBundle=Always /p:AppxPackageDir=!OUTPUT_PATH!\ /p:AppxPackageSigningEnabled=true /p:PackageCertificateThumbprint=cfcc41845e3d8f50ffc5f35adf3f06788c717f7d /p:PackageCertificatePassword=123456