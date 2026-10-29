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

dotnet publish .\TrafficTracker -c Release -o publish\NetProxy.Plugins.TrafficTracker --self-contained false
del publish\NetProxy.Plugins.TrafficTracker\*.pdb
7z.exe a -tzip -r -mx9 ".\Publish\NetProxy.Plugins.TrafficTracker.zip" ".\publish\NetProxy.Plugins.TrafficTracker\*.*"

pause
