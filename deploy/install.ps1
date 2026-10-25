#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Cài đặt B2 Clearance offline (xcopy) — không cần internet, không cần cài .NET.

.DESCRIPTION
    Chạy trên PC cửa khẩu (Administrator). Cách dễ nhất: nhấp đúp CaiDat.cmd (tự xin quyền
    Administrator rồi gọi script này) hoặc dùng B2Clearance-<phiên bản>-setup.exe. Các bước:
      1. Cài prerequisites offline từ .\prereqs (VC++ x64, WebView2).
      2. Chép payload self-contained vào C:\Program Files\B2Clearance.
      3. Máy cổng vào: gọi B2Clearance.exe install — tạo thư mục dữ liệu, chứng chỉ TLS cho tên
         máy + IP LAN (xuất C:\ProgramData\B2\b2-server.cer cho máy cổng ra), đăng ký Windows
         Service (tự khởi động), mở firewall TCP 5001 (HTTPS), sinh + hiển thị MÃ PHỤC HỒI, rồi
         khởi động dịch vụ.
         Máy cổng ra: tin chứng chỉ b2-server.cer của máy cổng vào và ghi địa chỉ máy chủ.
    Vai trò máy được suy ra theo thứ tự: -Role / -ClientOnly / -ServerUrl → bản cài đang có
    (nâng cấp) → hỏi bằng menu (cài mới, không tham số).
    Idempotent: chạy lại để nâng cấp (dừng dịch vụ, chép đè, khởi động lại) — không cần nhớ
    tham số lúc cài. Dữ liệu tại C:\ProgramData\B2 luôn được giữ nguyên.

.PARAMETER Role
    Server = máy cổng vào (dịch vụ + ứng dụng). Client = máy cổng ra (chỉ ứng dụng).
    Bỏ trống: nâng cấp giữ vai trò cũ; cài mới thì hỏi.

.PARAMETER ServerUrl
    Địa chỉ LAN của máy cổng vào, dùng cho app desktop trên máy CỔNG RA, ví dụ
    -ServerUrl 192.168.1.10   (tự hiểu là https://192.168.1.10:5001/). Truyền tham số này nghĩa
    là -Role Client. Bỏ trống khi nâng cấp máy cổng ra = giữ địa chỉ đã lưu.

.PARAMETER ServerCertificate
    Đường dẫn tệp b2-server.cer chép từ máy cổng vào (C:\ProgramData\B2\b2-server.cer).
    Được nhập vào LocalMachine\Root để app desktop tin chứng chỉ HTTPS của máy chủ.
    Bỏ trống: tự tìm b2-server.cer cạnh bộ cài và ở gốc các ổ USB.

.PARAMETER ClientOnly
    Tương đương -Role Client (giữ để tương thích lệnh cũ).

.PARAMETER PauseAtEnd
    Chờ nhấn Enter trước khi đóng (kể cả khi lỗi). CaiDat.cmd và setup.exe bật sẵn.

.EXAMPLE
    .\install.ps1
    Cài mới: hỏi vai trò máy. Nâng cấp: giữ vai trò, địa chỉ máy chủ và chứng chỉ đã có.

.EXAMPLE
    .\install.ps1 -Role Server
    Máy cổng vào: cài server + desktop, dùng endpoint loopback.

.EXAMPLE
    .\install.ps1 -ServerUrl 192.168.1.10
    Máy cổng ra: chỉ cài desktop, trỏ về máy cổng vào; b2-server.cer tự tìm trên USB.

.NOTES
    Xem docs/huong-dan-cai-dat.md và docs/deployment-offline.md.
#>
[CmdletBinding()]
param(
    [ValidateSet('Server', 'Client')]
    [string] $Role,
    [string] $InstallDir = (Join-Path $env:ProgramFiles 'B2Clearance'),
    [string] $ServerUrl,
    [string] $ServerCertificate,
    [switch] $ClientOnly,
    [switch] $PauseAtEnd
)

$ErrorActionPreference = 'Stop'
$payload = $PSScriptRoot
$serviceName = 'B2ClearanceService'
$certFileName = 'b2-server.cer'
$installerExe = Join-Path $InstallDir 'B2Clearance.exe'
$clientConfig = Join-Path $InstallDir 'Desktop\b2-client.json'

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

function Wait-Close {
    if ($PauseAtEnd) { Read-Host 'Nhấn Enter để đóng cửa sổ' | Out-Null }
}

# Lỗi bất kỳ: in rõ ràng, giữ cửa sổ (CaiDat.cmd / setup.exe) để người cài đọc được, thoát mã 1.
trap {
    Write-Host ''
    Write-Host "LOI: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'Chup man hinh noi dung nay neu can gui doi phat trien.'
    Wait-Close
    exit 1
}

function Test-ServerAddress([string] $value) {
    # IP / tên máy / ip:port / URL đầy đủ. Không cho khoảng trắng, dấu nháy (ghi thẳng vào JSON).
    return $value -match '^[A-Za-z0-9.\-:/\[\]]+$'
}

function Get-SavedServerUrl {
    if (-not (Test-Path -LiteralPath $clientConfig)) { return $null }
    try { return (Get-Content -LiteralPath $clientConfig -Raw -Encoding UTF8 | ConvertFrom-Json).serverUrl }
    catch { return $null }
}

function Get-TrustedB2Certificate {
    Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $_.Subject -match 'O=B2 Clearance' } | Select-Object -First 1
}

# b2-server.cer cạnh bộ cài, ở gốc ổ chứa bộ cài, rồi ở gốc các ổ rời (USB).
function Find-ServerCertificate {
    $candidates = @(
        (Join-Path $payload $certFileName),
        (Join-Path ([System.IO.Path]::GetPathRoot($payload)) $certFileName)
    )
    $removable = Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=2' -ErrorAction SilentlyContinue
    foreach ($disk in $removable) { $candidates += (Join-Path "$($disk.DeviceID)\" $certFileName) }
    return $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}

function Read-Role {
    Write-Host ''
    Write-Host 'Máy này dùng làm gì?' -ForegroundColor Yellow
    Write-Host '  1) Máy cổng vào — máy chủ + ứng dụng (cài máy này TRƯỚC; máy duy nhất cũng chọn 1)'
    Write-Host '  2) Máy cổng ra  — chỉ ứng dụng, kết nối tới máy cổng vào'
    while ($true) {
        switch ((Read-Host 'Chọn 1 hoặc 2').Trim()) {
            '1' { return 'Server' }
            '2' { return 'Client' }
        }
    }
}

function Read-ServerUrl {
    while ($true) {
        $value = (Read-Host 'Nhập địa chỉ IP máy cổng vào (ví dụ 192.168.1.10)').Trim()
        if ($value -and (Test-ServerAddress $value)) { return $value }
        Write-Host 'Dia chi khong hop le — chi gom so, chu, dau cham, dau hai cham.' -ForegroundColor Red
    }
}

function Read-CertificatePath {
    Write-Host "Khong tim thay $certFileName canh bo cai hoac tren USB." -ForegroundColor Yellow
    while ($true) {
        $value = (Read-Host "Nhập đường dẫn tệp $certFileName (chép từ máy cổng vào, ví dụ E:\$certFileName)").Trim().Trim('"')
        if ($value -and (Test-Path -LiteralPath $value -PathType Leaf)) { return $value }
        Write-Host "Khong tim thay tep: $value" -ForegroundColor Red
    }
}

# --- 0. Xác định vai trò máy và thông số còn thiếu -----------------------------------
if ($ClientOnly -or $ServerUrl) {
    if ($Role -eq 'Server') { throw '-Role Server khong di cung -ClientOnly / -ServerUrl.' }
    $Role = 'Client'
}
if (-not $Role) {
    if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
        $Role = 'Server'
        Write-Step 'Đã có dịch vụ B2 trên máy này → nâng cấp máy cổng vào.'
    }
    elseif (Test-Path -LiteralPath $clientConfig) {
        $Role = 'Client'
        Write-Step 'Đã có cấu hình máy cổng ra → nâng cấp máy cổng ra.'
    }
    else {
        $Role = Read-Role
    }
}
$isClient = $Role -eq 'Client'

if ($isClient) {
    if (-not $ServerUrl) { $ServerUrl = Get-SavedServerUrl }
    if (-not $ServerUrl) { $ServerUrl = Read-ServerUrl }
    if (-not (Test-ServerAddress $ServerUrl)) { throw "Dia chi may chu khong hop le: $ServerUrl" }

    if (-not $ServerCertificate) {
        $ServerCertificate = Find-ServerCertificate
        if ($ServerCertificate) { Write-Step "Tìm thấy chứng chỉ máy chủ: $ServerCertificate" }
        elseif (Get-TrustedB2Certificate) { Write-Step 'Máy đã tin chứng chỉ máy chủ B2 từ lần cài trước — giữ nguyên.' }
        else { $ServerCertificate = Read-CertificatePath }
    }
}
if ($ServerCertificate -and -not (Test-Path -LiteralPath $ServerCertificate -PathType Leaf)) {
    throw "Khong tim thay tep chung chi: $ServerCertificate"
}

# --- 1. Prerequisites (offline, im lặng, idempotent) ---------------------------------
$prereqDir = Join-Path $payload 'prereqs'
if (Test-Path $prereqDir) {
    $vc = Join-Path $prereqDir 'vc_redist.x64.exe'
    if (Test-Path $vc) {
        Write-Step 'Cài Visual C++ 2015-2022 x64 Redistributable...'
        Start-Process -FilePath $vc -ArgumentList '/install', '/quiet', '/norestart' -Wait
    }
    $webview = Get-ChildItem -Path $prereqDir -Filter 'MicrosoftEdgeWebView2*Installer*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($webview) {
        Write-Step 'Cài Microsoft Edge WebView2 Runtime...'
        Start-Process -FilePath $webview.FullName -ArgumentList '/silent', '/install' -Wait
    }
    else {
        Write-Warning 'Khong thay bo cai WebView2 trong .\prereqs — app desktop co the khong khoi dong.'
    }
}

# --- 2. Chép payload vào Program Files (dừng dịch vụ trước nếu đang nâng cấp) ---------
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Write-Step 'Dừng dịch vụ hiện có để nâng cấp...'
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

Write-Step "Chép chương trình vào $InstallDir ..."
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
# robocopy: chép payload trừ prereqs (không cần sau khi cài), các script deploy và tệp chứng
# chỉ người cài để cạnh bộ cài. Mã thoát robocopy < 8 là thành công.
# Máy cổng ra không chạy service — bỏ luôn binary server (~112 MB) cho khỏi nhầm lẫn.
$excludedFiles = @('install.ps1', 'uninstall.ps1', 'CaiDat.cmd', $certFileName)
if ($isClient) { $excludedFiles += 'B2.Server.Service.exe' }
$robolog = robocopy $payload $InstallDir /E /NFL /NDL /NJH /NJS /NP /XD (Join-Path $payload 'prereqs') /XF $excludedFiles
if ($LASTEXITCODE -ge 8) { throw "robocopy that bai (ma $LASTEXITCODE)" }

# --- 3. Chạy installer CLI (đăng ký service + hiển thị mã phục hồi) -------------------
if ($isClient) {
    Write-Step 'Máy cổng ra: bỏ qua đăng ký dịch vụ, chứng chỉ và mã phục hồi.'
}
else {
    if (-not (Test-Path $installerExe)) { throw "Khong tim thay $installerExe trong payload." }
    Write-Step 'Đăng ký dịch vụ và sinh mã phục hồi...'
    & $installerExe install
    if ($LASTEXITCODE -ne 0) { throw "B2Clearance.exe install that bai (ma $LASTEXITCODE)" }
}

# --- 3a. Tin chứng chỉ HTTPS của máy cổng vào (#147) -----------------------------------
# Máy cổng vào: B2Clearance.exe install đã tự thêm chứng chỉ vào LocalMachine\Root.
# Máy cổng ra: nhập b2-server.cer, gỡ chứng chỉ B2 cũ (đổi IP / cấp lại) để Root gọn.
if ($ServerCertificate) {
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 (Resolve-Path -LiteralPath $ServerCertificate).Path
    if ($cert.HasPrivateKey) { throw 'Tep chung chi khong duoc chua khoa bi mat — dung b2-server.cer, KHONG dung server.pfx.' }
    if ($cert.Subject -notmatch 'O=B2 Clearance') { throw "Tep nay khong phai chung chi may chu B2: $($cert.Subject)" }
    # Chỉ nhận chứng chỉ lá (CA=false) dùng cho serverAuth — một CA trong Root sẽ được tin cho MỌI tên.
    $basic = $cert.Extensions | Where-Object { $_ -is [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension] }
    if (-not $basic -or $basic.CertificateAuthority) { throw 'Tep chung chi khong phai chung chi la (CA=false) cua may chu B2.' }
    $eku = $cert.Extensions | Where-Object { $_ -is [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension] }
    if (-not $eku -or -not ($eku.EnhancedKeyUsages | Where-Object { $_.Value -eq '1.3.6.1.5.5.7.3.1' })) {
        throw 'Tep chung chi thieu muc dich Server Authentication.'
    }
    # Nhập trước, gỡ bản cũ sau: lỗi giữa chừng không để máy mất hẳn chứng chỉ B2.
    $root = New-Object System.Security.Cryptography.X509Certificates.X509Store 'Root', 'LocalMachine'
    $root.Open('ReadWrite')
    try { $root.Add($cert) } finally { $root.Close() }
    Get-ChildItem Cert:\LocalMachine\Root |
        Where-Object { $_.Subject -match 'O=B2 Clearance' -and $_.Thumbprint -ne $cert.Thumbprint } |
        ForEach-Object { Write-Step "Gỡ chứng chỉ B2 cũ $($_.Thumbprint)"; Remove-Item -LiteralPath $_.PSPath }
    Write-Step "Đã tin chứng chỉ máy chủ B2 — Thumbprint $($cert.Thumbprint), hết hạn $($cert.NotAfter.ToString('yyyy-MM-dd'))"
    Write-Host '    Doi chieu Thumbprint nay voi dong "Thumbprint" hien tren may cong vao luc cai.'
}

# --- 3b. Endpoint cho app desktop ------------------------------------------------------
# Không có file này thì desktop dùng https://127.0.0.1:5001/ (server cùng máy).
if ($isClient) {
    $desktopDir = Split-Path $clientConfig
    if (-not (Test-Path $desktopDir)) { throw "Khong tim thay thu muc $desktopDir de ghi cau hinh endpoint." }
    $json = "{`n  `"serverUrl`": `"$ServerUrl`"`n}`n"
    # UTF-8 KHONG BOM: System.Text.Json tu choi doc file co BOM.
    [System.IO.File]::WriteAllText($clientConfig, $json, (New-Object System.Text.UTF8Encoding $false))
    Write-Step "Endpoint desktop -> $ServerUrl (ghi vao $clientConfig)"
}

# --- 4. Shortcut desktop app (tùy chọn, không chặn nếu lỗi) --------------------------
try {
    $desktopExe = Join-Path $InstallDir 'Desktop\B2.Desktop.exe'
    if (Test-Path $desktopExe) {
        $lnk = Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'Sổ B2 — Cửa khẩu Chi Ma.lnk'
        $ws = New-Object -ComObject WScript.Shell
        $s = $ws.CreateShortcut($lnk)
        $s.TargetPath = $desktopExe
        $s.WorkingDirectory = (Split-Path $desktopExe)
        $s.Save()
    }
}
catch { Write-Warning "Khong tao duoc shortcut: $($_.Exception.Message)" }

Write-Host ''
if ($isClient) {
    Write-Host 'HOAN TAT (may cong ra). Mo app "So B2 — Cua khau Chi Ma" tren Desktop.' -ForegroundColor Green
    Write-Host "Endpoint desktop: $ServerUrl"
    Write-Host 'Neu app bao "Mat ket noi": kiem tra ping toi may chu, firewall TCP 5001 tren may chu,'
    Write-Host 'va Thumbprint chung chi o tren trung voi dong Thumbprint khi cai may cong vao.'
}
else {
    Write-Host 'HOAN TAT. Dich vu B2 dang chay. Mo app "So B2 — Cua khau Chi Ma" tren Desktop.' -ForegroundColor Green
    Write-Host 'Endpoint mac dinh: https://127.0.0.1:5001/  (may nay vua la server vua la client).'
    Write-Host 'May cong ra: chep C:\ProgramData\B2\b2-server.cer vao goc USB cung bo cai,'
    Write-Host 'roi nhap dup CaiDat.cmd (hoac setup.exe) tren may cong ra va chon "May cong ra".'
}
Wait-Close
# Exit code rõ ràng: setup.exe kiểm tra mã này (robocopy để lại $LASTEXITCODE = 1 khi chép thành công).
exit 0
