#define AppVersion "2.4.1"

[Setup]
;-- Main Setup Information
 AppName                         = NetProxy
 AppVersion                      = {#AppVersion}
 AppVerName                      = NetTunnel {#AppVersion}
 AppCopyright                    = Copyright © 1995-2026 NetworkDLS.
 DefaultDirName                  = {commonpf}\NetworkDLS\NetProxy
 DefaultGroupName                = NetworkDLS\NetProxy
 SetupIconFile                   = "..\Images\AppIcon.ico"
 UninstallDisplayIcon            = {app}\NetProxy.Client.Exe
 PrivilegesRequired              = PowerUser
 Uninstallable                   = Yes
 Compression                     = zip/9
 OutputBaseFilename              = NetProxy {#AppVersion}
 ArchitecturesInstallIn64BitMode = x64
 ArchitecturesAllowed            = x64
 AppPublisher                    = NetworkDLS
 AppPublisherURL                 = http://www.NetworkDLS.com/
 AppUpdatesURL                   = http://www.NetworkDLS.com/

[Components]
 Name: Base;            Description: "Base Install";       Types: full compact custom;  Flags: Fixed;
 Name: Base\Management; Description: "Management Console"; Types: full compact custom;
 Name: Service;         Description: "Proxy Service";      Types: full compact custom;

[Files]
 Source: "publish\win-x64\Client\*.exe";   DestDir: "{app}"; Components: Base\Management; Flags: IgnoreVersion;
 Source: "publish\win-x64\Client\*.json";  DestDir: "{app}"; Components: Base\Management; Flags: IgnoreVersion;
 Source: "publish\win-x64\Client\*.dll";   DestDir: "{app}"; Components: Base\Management; Flags: IgnoreVersion;
 Source: "publish\win-x64\Service\*.json"; DestDir: "{app}"; Components: Service;         Flags: IgnoreVersion;
 Source: "publish\win-x64\Service\*.exe";  DestDir: "{app}"; Components: Service;         Flags: IgnoreVersion;
 Source: "publish\win-x64\Service\*.dll";  DestDir: "{app}"; Components: Service;         Flags: IgnoreVersion;
  
[Icons]
 Name: "{group}\Manage NetProxy"; Filename: "{app}\NetProxy.Client.Exe"; WorkingDir: "{app}"; Components: Base\Management;

[Registry]
 Root: HKLM; Subkey: "Software\NetworkDLS\NetProxy"; Flags: uninsdeletekey noerror;
 Root: HKLM; Subkey: "Software\NetworkDLS\NetProxy"; ValueName: "ConfigPath"; ValueType: String; ValueData: "{app}\Config"; Flags: CreateValueIfDoesntExist; Components: Service;
 Root: HKLM; Subkey: "Software\NetworkDLS\NetProxy"; ValueName: "AppPath";    ValueType: String; ValueData: "{app}";        Flags: CreateValueIfDoesntExist;

[Run]
 Filename: "{app}\NetProxy.Service"; Parameters: "install"; Flags: runhidden; StatusMsg: "Installing service..."
 Filename: "{app}\NetProxy.Service"; Parameters: "start"; Flags: runhidden; StatusMsg: "Starting service..."
 Filename: "{app}\NetProxy.Client.exe"; Description: "Open Management Console"; Flags: postinstall nowait skipifsilent runasoriginaluser;  Components: Base\Management;

[UninstallRun]
 Filename: "{app}\NetProxy.Service"; Parameters: "uninstall"; Flags: runhidden; StatusMsg: "Installing service..."; RunOnceId: "ServiceRemoval"
