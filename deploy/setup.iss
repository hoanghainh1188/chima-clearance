; B2Clearance-<phiên bản>-setup.exe — trình cài có giao diện (#214, ADR-0013).
;
; Chỉ là giao diện mỏng trên install.ps1: hỏi vai trò máy (và với máy cổng ra: IP máy cổng vào
; + tệp b2-server.cer), giải nén payload vào {tmp}, rồi chạy install.ps1 trong một cửa sổ console
; hiện rõ — để người cài thấy và chép MÃ PHỤC HỒI + MẬT KHẨU ADMIN BAN ĐẦU. Mọi bước cài thật
; (prereqs, chép vào Program Files, dịch vụ, chứng chỉ, firewall) vẫn nằm trong install.ps1 /
; B2Clearance.exe; không lặp lại ở đây. Gỡ cài vẫn bằng uninstall.ps1 (Uninstallable=no).
;
; Biên dịch (release.yml):
;   ISCC.exe /DAppVersion=1.5.0 /DPayloadDir=<publish\pkg> /DOutputDir=<Releases> deploy\setup.iss
; Tệp này phải là UTF-8 CÓ BOM — Inno Setup đọc tệp không BOM theo ANSI, làm vỡ chữ tiếng Việt.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PayloadDir
  #define PayloadDir "..\publish\pkg"
#endif
#ifndef OutputDir
  #define OutputDir "..\Releases"
#endif

[Setup]
AppId={{27AE0109-208A-494F-BE70-6D9839C30F1F}
AppName=Sổ B2 — Cửa khẩu Chi Ma
AppVersion={#AppVersion}
AppVerName=Sổ B2 — Cửa khẩu Chi Ma {#AppVersion}
AppPublisher=B2 Clearance
VersionInfoVersion={#AppVersion}
; Không có thư mục {app}: payload chỉ giải nén vào {tmp}; install.ps1 tự chép vào Program Files.
CreateAppDir=no
DisableProgramGroupPage=yes
Uninstallable=no
PrivilegesRequired=admin
; 64-bit bắt buộc: {sys} phải là System32 thật để gọi PowerShell 64-bit — PowerShell 32-bit thấy
; $env:ProgramFiles = "Program Files (x86)" và sẽ cài sai chỗ.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 1809 (build 17763) — cùng mức tối thiểu với app desktop.
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=B2Clearance-{#AppVersion}-setup
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes

[Messages]
SetupAppTitle=Cài đặt Sổ B2
SetupWindowTitle=Cài đặt — %1
ButtonBack=< &Quay lại
ButtonNext=&Tiếp >
ButtonInstall=&Cài đặt
ButtonCancel=Hủy
ButtonFinish=&Xong
ButtonBrowse=&Chọn tệp...
ButtonYes=&Có
ButtonNo=&Không
ButtonOK=OK
ExitSetupTitle=Thoát cài đặt
ExitSetupMessage=Cài đặt chưa xong. Thoát bây giờ thì chưa có gì được cài.%n%nThoát cài đặt?
WelcomeLabel1=Cài đặt [name]
WelcomeLabel2=Bộ cài sẽ cài [name/ver] lên máy này. Không cần internet.%n%nNếu máy đã có bản cài trước, bộ cài sẽ nâng cấp và giữ nguyên dữ liệu, bản sao lưu và mã phục hồi.%n%nĐóng ứng dụng Sổ B2 trước khi tiếp tục.
WizardReady=Sẵn sàng cài đặt
ReadyLabel1=Kiểm tra lại trước khi cài.
ReadyLabel2a=Bấm Cài đặt để bắt đầu, hoặc Quay lại để sửa.
ReadyLabel2b=Bấm Cài đặt để bắt đầu.
WizardPreparing=Đang chuẩn bị
PreparingDesc=Đang chuẩn bị cài [name].
WizardInstalling=Đang cài đặt
InstallingLabel=Đang cài [name]. Một cửa sổ dòng lệnh sẽ mở — làm theo hướng dẫn trong đó.
StatusExtractFiles=Đang giải nén tệp...
FinishedHeadingLabel=Đã cài xong [name]
FinishedLabelNoIcons=Đã cài xong [name]. Mở ứng dụng "Sổ B2 — Cửa khẩu Chi Ma" trên Desktop.
FinishedLabel=Đã cài xong [name]. Mở ứng dụng "Sổ B2 — Cửa khẩu Chi Ma" trên Desktop.
ClickFinish=Bấm Xong để đóng bộ cài.
BrowseDialogTitle=Chọn thư mục
WindowsVersionNotSupported=Bộ cài cần Windows 10 bản 1809 trở lên, 64-bit.
OnlyOnTheseArchitectures=Bộ cài chỉ chạy trên Windows 64-bit.
AdminPrivilegesRequired=Cần chạy bộ cài bằng tài khoản có quyền Administrator.

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{tmp}\payload"; Flags: recursesubdirs createallsubdirs ignoreversion

[Code]
const
  ServiceKey = 'SYSTEM\CurrentControlSet\Services\B2ClearanceService';
  CertFileName = 'b2-server.cer';
  DriveRemovable = 2;

var
  RolePage: TInputOptionWizardPage;
  ServerPage: TInputQueryWizardPage;
  CertPage: TInputFileWizardPage;
  ExistingRole: String; // '' = cài mới, 'Server' / 'Client' = nâng cấp
  InstallFailed: Boolean;

function GetDriveType(RootPath: String): Cardinal;
  external 'GetDriveTypeW@kernel32.dll stdcall';

function ClientConfigPath: String;
begin
  Result := ExpandConstant('{commonpf64}\B2Clearance\Desktop\b2-client.json');
end;

{ Đọc "serverUrl" từ b2-client.json của lần cài trước (tệp do install.ps1 ghi, một khóa). }
function ReadSavedServerUrl: String;
var
  Raw: AnsiString;
  Text: String;
  P: Integer;
begin
  Result := '';
  if not LoadStringFromFile(ClientConfigPath, Raw) then Exit;
  Text := String(Raw);
  P := Pos('"serverUrl"', Text);
  if P = 0 then Exit;
  Delete(Text, 1, P + Length('"serverUrl"'));
  P := Pos('"', Text);
  if P = 0 then Exit;
  Delete(Text, 1, P);
  P := Pos('"', Text);
  if P = 0 then Exit;
  Result := Copy(Text, 1, P - 1);
end;

{ b2-server.cer cạnh setup.exe, rồi ở gốc các ổ rời (USB) — giống install.ps1. }
function FindCertificate: String;
var
  I: Integer;
  Root: String;
begin
  Result := AddBackslash(ExtractFileDir(ExpandConstant('{srcexe}'))) + CertFileName;
  if FileExists(Result) then Exit;
  for I := Ord('D') to Ord('Z') do
  begin
    Root := Chr(I) + ':\';
    Result := Root + CertFileName;
    if (GetDriveType(Root) = DriveRemovable) and FileExists(Result) then Exit;
  end;
  Result := '';
end;

function IsClient: Boolean;
begin
  Result := RolePage.SelectedValueIndex = 1;
end;

{ IP / tên máy / ip:port / URL — cùng tập ký tự install.ps1 chấp nhận (ghi thẳng vào JSON). }
function IsValidAddress(Value: String): Boolean;
var
  I: Integer;
  C: Char;
begin
  Result := Length(Value) > 0;
  for I := 1 to Length(Value) do
  begin
    C := Value[I];
    if not (((C >= '0') and (C <= '9')) or ((C >= 'A') and (C <= 'Z')) or
            ((C >= 'a') and (C <= 'z')) or (Pos(C, '.-:/[]') > 0)) then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

procedure InitializeWizard;
begin
  if RegKeyExists(HKLM64, ServiceKey) then
    ExistingRole := 'Server'
  else if FileExists(ClientConfigPath) then
    ExistingRole := 'Client';

  RolePage := CreateInputOptionPage(wpWelcome,
    'Vai trò của máy này', 'Máy này dùng làm gì?',
    'Luôn cài máy cổng vào trước. Chỉ có một máy cho cả hai cổng: chọn máy cổng vào.',
    True, False);
  RolePage.Add('Máy cổng vào — máy chủ (dịch vụ, cơ sở dữ liệu, sao lưu) + ứng dụng Sổ B2');
  RolePage.Add('Máy cổng ra — chỉ ứng dụng Sổ B2, kết nối tới máy cổng vào');
  if ExistingRole = 'Client' then
    RolePage.SelectedValueIndex := 1
  else
    RolePage.SelectedValueIndex := 0;

  ServerPage := CreateInputQueryPage(RolePage.ID,
    'Địa chỉ máy cổng vào', 'Máy cổng ra kết nối tới máy cổng vào qua mạng LAN.',
    'Trên máy cổng vào, chạy ipconfig và xem dòng IPv4 Address.');
  ServerPage.Add('Địa chỉ IP máy cổng vào (ví dụ 192.168.1.10):', False);
  ServerPage.Values[0] := ReadSavedServerUrl;

  CertPage := CreateInputFilePage(ServerPage.ID,
    'Chứng chỉ máy chủ', 'Tệp b2-server.cer chép từ máy cổng vào.',
    'Tệp nằm ở C:\ProgramData\B2\b2-server.cer trên máy cổng vào. Chép vào gốc USB thì bộ cài tự tìm thấy.');
  CertPage.Add('Tệp b2-server.cer:', 'Chứng chỉ máy chủ B2 (*.cer)|*.cer', '.cer');
  CertPage.Values[0] := FindCertificate;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  { Nâng cấp giữ nguyên vai trò — như install.ps1 khi chạy không tham số. }
  if (PageID = RolePage.ID) and (ExistingRole <> '') then
    Result := True
  else if ((PageID = ServerPage.ID) or (PageID = CertPage.ID)) and not IsClient then
    Result := True;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = ServerPage.ID then
  begin
    ServerPage.Values[0] := Trim(ServerPage.Values[0]);
    if not IsValidAddress(ServerPage.Values[0]) then
    begin
      MsgBox('Địa chỉ không hợp lệ. Chỉ gồm số, chữ, dấu chấm, dấu hai chấm — ví dụ 192.168.1.10.', mbError, MB_OK);
      Result := False;
    end;
  end
  else if CurPageID = CertPage.ID then
  begin
    { Nâng cấp máy cổng ra: để trống = giữ chứng chỉ đã tin (install.ps1 tự tìm / giữ). }
    if (CertPage.Values[0] = '') and (ExistingRole = 'Client') then Exit;
    if not FileExists(CertPage.Values[0]) then
    begin
      MsgBox('Chọn tệp b2-server.cer chép từ máy cổng vào.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  Cert: String;
begin
  if IsClient then
  begin
    Cert := CertPage.Values[0];
    if Cert = '' then Cert := '(giữ chứng chỉ đã tin từ lần cài trước)';
    Result := 'Vai trò: máy cổng ra (chỉ ứng dụng)' + NewLine +
      Space + 'Máy cổng vào: ' + ServerPage.Values[0] + NewLine +
      Space + 'Chứng chỉ: ' + Cert + NewLine + NewLine +
      'Cửa sổ dòng lệnh sẽ in Thumbprint của chứng chỉ — đối chiếu với Thumbprint ghi lại khi cài máy cổng vào.';
  end
  else
    Result := 'Vai trò: máy cổng vào (máy chủ + ứng dụng)' + NewLine + NewLine +
      'Cửa sổ dòng lệnh sẽ hiện MÃ PHỤC HỒI và MẬT KHẨU ADMIN BAN ĐẦU. Chép cả hai ra giấy trước khi nhấn Enter.';
  if ExistingRole <> '' then
    Result := Result + NewLine + NewLine + 'Máy đã có bản cài — sẽ nâng cấp, giữ nguyên dữ liệu.';
end;

function InstallArgs: String;
begin
  Result := '-NoProfile -ExecutionPolicy Bypass -File "' +
    ExpandConstant('{tmp}\payload\install.ps1') + '" -PauseAtEnd';
  if IsClient then
  begin
    Result := Result + ' -Role Client -ServerUrl ' + ServerPage.Values[0];
    if CertPage.Values[0] <> '' then
      Result := Result + ' -ServerCertificate "' + CertPage.Values[0] + '"';
  end
  else
    Result := Result + ' -Role Server';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep <> ssPostInstall then Exit;
  WizardForm.StatusLabel.Caption := 'Đang cài trong cửa sổ dòng lệnh — làm theo hướng dẫn trong đó...';
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), InstallArgs,
              ExpandConstant('{tmp}\payload'), SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode)
     or (ResultCode <> 0) then
  begin
    InstallFailed := True;
    MsgBox('Cài đặt chưa xong (mã ' + IntToStr(ResultCode) + ').' + #13#10 + #13#10 +
      'Xem dòng LOI trong cửa sổ dòng lệnh, chụp màn hình gửi đội phát triển, rồi chạy lại bộ cài. ' +
      'Dữ liệu tại C:\ProgramData\B2 không bị ảnh hưởng.', mbError, MB_OK);
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpFinished) and InstallFailed then
  begin
    WizardForm.FinishedHeadingLabel.Caption := 'Cài đặt chưa hoàn tất';
    WizardForm.FinishedLabel.Caption :=
      'install.ps1 báo lỗi. Sửa theo thông báo trong cửa sổ dòng lệnh (xem docs\huong-dan-cai-dat.md § 7) rồi chạy lại bộ cài.';
  end;
end;
