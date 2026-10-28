@echo off
set path=%PATH%;C:\Program Files\7-Zip;
set path=C:\Program Files (x86)\Inno Setup 6\;%path%

rd publish /q /s
rd output /q /s

md output
md publish

dotnet pack ..\NetProxy.PluginApi -c Release -o ".\output"

REM Windows console and service (the installer needs the .NET 10 Desktop runtime on the machine).
dotnet publish ..\NetProxy.Client -c Release -o publish\win-x64\Client --runtime win-x64 --self-contained false
del publish\win-x64\Client\*.pdb /q

dotnet publish ..\NetProxy.Service -c Release -o publish\win-x64\Service --runtime win-x64 --self-contained false
del publish\win-x64\Service\*.pdb /q

iscc Windows.Installer.iss

REM Linux service, self contained (it brings its own .NET runtime, so nothing needs to be installed on the machine).
dotnet publish ..\NetProxy.Service -c Release -o publish\linux-x64\NetProxy --runtime linux-x64 --self-contained true
del publish\linux-x64\NetProxy\*.pdb /q

dotnet publish ..\NetProxy.Service -c Release -o publish\linux-arm64\NetProxy --runtime linux-arm64 --self-contained true
del publish\linux-arm64\NetProxy\*.pdb /q

7z.exe a -tzip -r -mx9 ".\output\NetProxy.Service.Linux.x64.zip" ".\publish\linux-x64\NetProxy\*.*"
7z.exe a -tzip -r -mx9 ".\output\NetProxy.Service.Linux.Arm64.zip" ".\publish\linux-arm64\NetProxy\*.*"

rd publish /q /s

pause
