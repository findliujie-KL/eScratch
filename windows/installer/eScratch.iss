[Setup]
AppId={{E808DF73-8374-49BF-B28D-CE606CC12264}
AppName=eScratch
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\eScratch
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
SetupIconFile=..\eScratch\Assets\icon.ico
UninstallDisplayIcon={app}\eScratch.exe
OutputDir=..\artifacts\net48\installer
OutputBaseFilename=eScratch-{#AppVersion}-net48-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "..\artifacts\net48\portable\*"; DestDir: "{app}"; Excludes: "*.pdb,x86\*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\eScratch"; Filename: "{app}\eScratch.exe"
Name: "{autodesktop}\eScratch"; Filename: "{app}\eScratch.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\eScratch.exe"; Description: "Launch eScratch"; Flags: nowait postinstall skipifsilent; Check: CanLaunch

[Code]
var
  Prerequisites: TInputOptionWizardPage;
  Downloads: TDownloadWizardPage;
  RebootRequired: Boolean;

function HasNetFrameworkInView(Root: Integer): Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(Root,
    'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
    and (Release >= 528040);
end;

function HasNetFramework: Boolean;
begin
  Result := HasNetFrameworkInView(HKLM32) or HasNetFrameworkInView(HKLM64);
end;

function HasVcInView(Root: Integer): Boolean;
var
  Installed, Major, Minor: Cardinal;
  Key: String;
begin
  Key := 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64';
  Result := RegQueryDWordValue(Root, Key, 'Installed', Installed) and (Installed = 1)
    and RegQueryDWordValue(Root, Key, 'Major', Major)
    and RegQueryDWordValue(Root, Key, 'Minor', Minor)
    and ((Major > 14) or ((Major = 14) and (Minor >= 29)));
end;

function HasVc: Boolean;
begin
  Result := HasVcInView(HKLM32) or HasVcInView(HKLM64);
end;

function CanLaunch: Boolean;
begin
  Result := HasNetFramework and HasVc and not RebootRequired;
end;

function StatusText: String;
begin
  Result := '.NET Framework 4.8 or later: ';
  if HasNetFramework then Result := Result + 'installed' else Result := Result + 'missing (web installer; additional files download from Microsoft)';
  Result := Result + #13#10 + 'Visual C++ runtime (x64), for OCR: ';
  if HasVc then Result := Result + 'installed' else Result := Result + 'missing (about 26 MB download)';
end;

function InitializeSetup: Boolean;
var Report: String;
begin
  Report := ExpandConstant('{param:CHECKONLY|}');
  Result := Report = '';
  if not Result then SaveStringToFile(Report, StatusText + #13#10, False);
end;

procedure InitializeWizard;
begin
  Prerequisites := CreateInputOptionPage(wpSelectDir, 'Required components',
    'Choose how to handle missing components', '', True, False);
  Prerequisites.Add('Download and install missing components (recommended)');
  Prerequisites.Add('Open Microsoft download pages; I will install them manually');
  Prerequisites.Add('Install eScratch only; install missing components later');
  Prerequisites.SelectedValueIndex := 0;
  Prerequisites.SubCaptionLabel.Height := ScaleY(112);
  Prerequisites.CheckListBox.Top := Prerequisites.SubCaptionLabel.Top + ScaleY(120);
  Prerequisites.CheckListBox.Height := Prerequisites.SurfaceHeight - Prerequisites.CheckListBox.Top;
  Downloads := CreateDownloadPage('Downloading required components',
    'Microsoft downloads are verified before running.', nil);
  Downloads.ShowBaseNameInsteadOfUrl := True;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = Prerequisites.ID then
    Prerequisites.SubCaptionLabel.Caption := StatusText + #13#10#13#10 +
      'Automatic installation needs internet access and may ask for administrator approval.' + #13#10 +
      'The app needs .NET Framework 4.8 or later; OCR also needs Visual C++. No .NET 10 runtime is used. After manual installation, click Next again.';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = Prerequisites.ID) and HasNetFramework and HasVc;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var Code: Integer;
begin
  Result := True;
  if (CurPageID = Prerequisites.ID) and (Prerequisites.SelectedValueIndex = 1) then begin
    if not HasNetFramework then
      ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48', '', '', SW_SHOWNORMAL, ewNoWait, Code);
    if not HasVc then
      ShellExec('open', 'https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist', '', '', SW_SHOWNORMAL, ewNoWait, Code);
    Result := HasNetFramework and HasVc;
    CurPageChanged(CurPageID);
  end;
end;

procedure InstallComponent(FileName, Parameters: String);
var Code: Integer;
begin
  if not ShellExec('runas', ExpandConstant('{tmp}\') + FileName,
    Parameters, '', SW_SHOWNORMAL, ewWaitUntilTerminated, Code) then
    RaiseException('Could not start the component installer. ' + SysErrorMessage(Code));
  if (Code = 3010) or (Code = 1641) then RebootRequired := True
  else if Code <> 0 then RaiseException('Component installation failed (code ' + IntToStr(Code) + ').');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Mode: String; FrameworkMissing, VcMissing: Boolean;
begin
  Result := '';
  FrameworkMissing := not HasNetFramework;
  VcMissing := not HasVc;
  if not (FrameworkMissing or VcMissing) then Exit;
  Mode := Lowercase(ExpandConstant('{param:PREREQUISITES|}'));
  if WizardSilent then begin
    if Mode = 'skip' then Exit;
    if Mode <> 'download' then begin
      Result := 'Missing required components. Specify /PREREQUISITES=download or /PREREQUISITES=skip.';
      Exit;
    end;
  end else if Prerequisites.SelectedValueIndex = 2 then Exit;
  Downloads.Clear;
  if FrameworkMissing then Downloads.Add('{#FrameworkUrl}', 'ndp48-web.exe', '{#FrameworkHash}');
  if VcMissing then Downloads.Add('{#VcUrl}', 'vc_redist.x64.exe', '{#VcHash}');
  Downloads.Show;
  try
    try
      Downloads.Download;
    except
      Result := 'Download failed or was cancelled. ' + GetExceptionMessage +
        ' Go Back to choose another option, or retry.';
    end;
  finally
    Downloads.Hide;
  end;
  if Result <> '' then Exit;
  try
    if FrameworkMissing then InstallComponent('ndp48-web.exe', '/passive /norestart');
    if VcMissing then InstallComponent('vc_redist.x64.exe', '/install /passive /norestart');
    if not (HasNetFramework and HasVc) then
      Result := 'Required components are not detected yet. Restart Windows if requested, then run setup again.';
  except
    Result := GetExceptionMessage + ' Go Back to choose another option, or retry.';
  end;
  if (Result <> '') and RebootRequired then NeedsRestart := True;
end;

function NeedRestart: Boolean;
begin
  Result := RebootRequired;
end;
