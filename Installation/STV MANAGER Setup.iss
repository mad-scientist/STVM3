; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "STV Manager 3"
#define MyAppVersion "21"
#define MyAppPublisher "Florian Thomas"
#define MyAppURL "http://www.ks15.de/apps"
#define MyAppExeName "STVM3.exe"
#define MyBaseDir "C:\Users\Flori\Visual Studio\STVM3"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{BFFA9FD2-8244-4438-8340-99D0DF547E63}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir="{#MyBaseDir}\Installation\Setup"
OutputBaseFilename="STVM3_Installer_V{#MyAppVersion}"
SetupIconFile="{#MyBaseDir}\STV MANAGER\STVM.ico"
Compression=lzma
SolidCompression=yes
UninstallDisplayIcon="{app}\STVM3.exe"
DisableStartupPrompt=False
DisableWelcomePage=False

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyBaseDir}\STV MANAGER\bin\Release\STVM3.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBaseDir}\STV MANAGER\bin\Release\STVM3.application"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBaseDir}\STV MANAGER\bin\Release\STVM3.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBaseDir}\STV MANAGER\bin\Release\HtmlAgilityPack.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBaseDir}\STV MANAGER\bin\Release\Renci.SshNet.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBaseDir}\STV MANAGER\bin\Release\RestSharp.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBaseDir}\STV MANAGER\bin\Release\TMDbLib.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBaseDir}\STV MANAGER\bin\Release\TVDB.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBaseDir}\STV MANAGER\bin\Release\ObjectListView.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBaseDir}\STV MANAGER\bin\Release\Newtonsoft.Json.dll"; DestDir: "{app}"; Flags: ignoreversion
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  UserSettingsDir: String;
begin
  case CurUninstallStep of
    usPostUninstall:
      begin
        if MsgBox('STV MANAGER wurde deinstalliert. Sollen alle Nutzerdaten gel�scht werden?', mbConfirmation, MB_YESNO) = idYes then begin
          UserSettingsDir := ExpandConstant('{userappdata}\STVM');
          DelTree(UserSettingsDir, True, True, True);
        end;
      end;
  end;
end;
