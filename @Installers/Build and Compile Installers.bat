@echo off
set path=C:\Program Files (x86)\Inno Setup 6\;%path%

rd publish /q /s
rd output /q /s

md output
md publish

dotnet publish ..\NetProxy.Client -c Release -o publish\win-x64\Client --runtime win-x64 --self-contained false
del publish\win-x64\Client\*.pdb /q

dotnet publish ..\NetProxy.Service -c Release -o publish\win-x64\Service --runtime win-x64 --self-contained false
del publish\win-x64\Service\*.pdb /q

iscc Windows.Installer.iss
rd publish /q /s

pause
