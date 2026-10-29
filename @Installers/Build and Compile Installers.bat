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

REM Linux service, self contained (it brings its own .NET runtime, so nothing needs to be installed on the machine).
dotnet publish ..\NetProxy.Service -c Release -o publish\linux-x64\NetProxy --runtime linux-x64 --self-contained true
del publish\linux-x64\NetProxy\*.pdb /q

dotnet publish ..\NetProxy.Service -c Release -o publish\linux-arm64\NetProxy --runtime linux-arm64 --self-contained true
del publish\linux-arm64\NetProxy\*.pdb /q

REM The ready-made plug-ins, built for each platform and put in the package of that platform, a folder each:
REM Plugins\AccessLog\NetProxy.Plugins.AccessLog.dll and so on. They are portable .NET assemblies that run in the runtime of the service.
call :PublishPlugins win-x64 publish\win-x64\Plugins || goto :failed
call :PublishPlugins linux-x64 publish\linux-x64\NetProxy\Plugins || goto :failed
call :PublishPlugins linux-arm64 publish\linux-arm64\NetProxy\Plugins || goto :failed

REM The Windows installer (the plug-ins are its Service\Plugins component).
iscc Windows.Installer.iss

REM The Linux packages (the plug-ins are in the Plugins folder of the zip).
7z.exe a -tzip -r -mx9 ".\output\NetProxy.Service.Linux.x64.zip" ".\publish\linux-x64\NetProxy\*.*"
7z.exe a -tzip -r -mx9 ".\output\NetProxy.Service.Linux.Arm64.zip" ".\publish\linux-arm64\NetProxy\*.*"

rd publish /q /s

pause
exit /b 0

:failed
echo.
echo A plug-in did not build, so the packages are not complete. Fix it and run this again.
pause
exit /b 1

REM Publishes every plug-in for one platform. %1 is the platform (win-x64, linux-x64, linux-arm64), %2 the folder that the plug-ins go in.
:PublishPlugins
for %%P in (AccessLog Alerts AntiHammer BasicAuth Cache GeoFilter Maintenance Metrics Redirect SecurityHeaders TrafficSplit) do (
	dotnet publish ..\NetProxy.Plugins\%%P -c Release -o %2\%%P --runtime %1 --self-contained false || exit /b 1
	del %2\%%P\*.pdb /q
)

REM The traffic tracker (it needs a CatMQ server and the ingestion service, see its README).
dotnet publish ..\NetProxy.Plugins\TrafficTracker.Plugin -c Release -o %2\TrafficTracker --runtime %1 --self-contained false || exit /b 1
del %2\TrafficTracker\*.pdb /q
exit /b 0
