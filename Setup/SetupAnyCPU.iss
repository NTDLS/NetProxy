[Setup]
;-- Main Setup Information
 AppName                         = NetProxy
 AppVerName                      = NetProxy 1.0.0.1
 AppCopyright                    = Copyright © 1995-2018 NetworkDLS.
 DefaultDirName                  = {pf}\NetworkDLS\NetProxy
 DefaultGroupName                = NetworkDLS\NetProxy
 UninstallDisplayIcon            = {app}\NetProxy.Client.Exe
 WizardImageFile                 = /../../@Resources/Setup/LgSetup.bmp
 WizardSmallImageFile            = /../../@Resources/Setup/SmSetup.bmp
 PrivilegesRequired              = PowerUser
 Uninstallable                   = Yes
 Compression                     = zip/9
 OutputBaseFilename              = NetProxy
 MinVersion                      = 0.0,5.0
 ArchitecturesInstallIn64BitMode = x64
 ArchitecturesAllowed            = x86 x64

;-- Windows 2000 & XP (Support Dialog)
 AppPublisher    = NetworkDLS
 AppPublisherURL = http://www.NetworkDLS.com/
 AppUpdatesURL   = http://www.NetworkDLS.com/
 AppVersion      = 1.0.0.1

[Components]
 Name: Base;            Description: "Base Install";       Types: full compact custom;  Flags: Fixed;
 Name: Base\Management; Description: "Management Console"; Types: full compact custom;
 Name: Service;         Description: "Router Service";     Types: full compact custom;

[Files]
 Source: "..\NetProxy.Client\bin\release\NetProxy.Client.exe";      DestDir: "{app}";  Components: Base\Management; Flags: IgnoreVersion;
 Source: "..\NetProxy.Client\bin\release\*.dll";                     DestDir: "{app}";  Components: Base\Management; Flags: IgnoreVersion;
 Source: "..\NetProxy.Service\bin\release\NetProxy.Service.exe";    DestDir: "{app}";  Components: Service;         Flags: IgnoreVersion;
 Source: "..\NetProxy.Service\bin\release\*.dll";                    DestDir: "{app}";  Components: Service;         Flags: IgnoreVersion;
 Source: "Data\*.*";                                                  DestDir: "{app}\Config"; Components: Service;         Flags: OnlyIfDoesntExist;

[Icons]
 Name: "{group}\Manage NetProxy"; Filename: "{app}\NetProxy.Client.Exe"; WorkingDir: "{app}"; Components: Base\Management;

[Registry]
 Root: HKLM; Subkey: "Software\NetworkDLS\NetProxy"; Flags: uninsdeletekey noerror;
 Root: HKLM; Subkey: "Software\NetworkDLS\NetProxy"; ValueName: "ConfigPath";      ValueType: String; ValueData: "{app}\Config";  Flags: CreateValueIfDoesntExist; Components: Service;
 Root: HKLM; Subkey: "Software\NetworkDLS\NetProxy"; ValueName: "AppPath";         ValueType: String; ValueData: "{app}";         Flags: CreateValueIfDoesntExist;

[Run]
 Filename: "{app}\NetProxy.Service.exe"; Parameters: "/Install"; StatusMsg: "Installing service..."; Flags: runhidden;                      Components: Service;
 Filename: "{app}\NetProxy.Service.exe"; Parameters: "/Start";   StatusMsg: "Starting Service...";   Flags: runhidden;                      Components: Service;
 Filename: "{app}\NetProxy.Client.exe";  Description: "Open Management Console"; Flags: postinstall nowait skipifsilent runasoriginaluser;  Components: Base\Management;

[UninstallRun]
 Filename: "{app}\NetProxy.Service.exe"; Parameters: "/Stop";      RunOnceId: "StopService";   Flags: runhidden; Components: Service;
 Filename: "{app}\NetProxy.Service.exe"; Parameters: "/Uninstall"; RunOnceId: "DeleteService"; Flags: runhidden; Components: Service;
