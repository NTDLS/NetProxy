@echo off
set path=%PATH%;C:\Program Files\7-Zip;

rem Publishes every plug-in to its own folder and zip. The plug-ins are portable (no native code): the same files run on Windows and Linux.

rd .\Publish /q /s
rd .\publish /q /s

md Publish

for %%P in (AccessLog Alerts AntiHammer BasicAuth Cache GeoFilter Maintenance Metrics Redirect SecurityHeaders TrafficSplit) do (
	dotnet publish .\%%P -c Release -o publish\NetProxy.Plugins.%%P --self-contained false
	del publish\NetProxy.Plugins.%%P\*.pdb
	7z.exe a -tzip -r -mx9 ".\Publish\NetProxy.Plugins.%%P.zip" ".\publish\NetProxy.Plugins.%%P\*.*"
)

dotnet publish .\TrafficTracker.Plugin -c Release -o publish\NetProxy.TrafficTracker.Plugin --self-contained false
del publish\NetProxy.TrafficTracker.Plugin\*.pdb
7z.exe a -tzip -r -mx9 ".\Publish\NetProxy.TrafficTracker.Plugin.zip" ".\publish\NetProxy.TrafficTracker.Plugin\*.*"

pause
