#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Cài đặt B2 Clearance offline (xcopy) — không cần internet, không cần cài .NET.

.DESCRIPTION
    Chạy trên PC cửa khẩu (Administrator). Các bước:
      1. Cài prerequisites offline từ .\prereqs (VC++ x64, WebView2) nếu có.
      2. Chép payload self-contained vào C:\Program Files\B2Clearance.
      3. Gọi B2Clearance.exe install: tạo thư mục dữ liệu, chứng chỉ TLS localhost,
         đăng ký Windows Service (tự khởi động), mở firewall TCP 5000, sinh + hiển thị
         MÃ PHỤC HỒI, rồi khởi động dịch vụ.
    Idempotent: chạy lại để nâng cấp (dừng dịch vụ, chép đè, khởi động lại). Dữ liệu tại
    C:\ProgramData\B2 luôn được giữ nguyên.

.NOTES
    Xem docs/deployment-offline.md. Thay thế trình cài Velopack cũ bằng xcopy thuần.
#>
[CmdletBinding()]
param(
    [string] $InstallDir = (Join-Path $env:ProgramFiles 'B2Clearance')
)

$ErrorActionPreference = 'Stop'
$payload = $PSScriptRoot
$serviceName = 'B2ClearanceService'
$installerExe = Join-Path $InstallDir 'B2Clearance.exe'

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

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
        Write-Warning 'Khong thay WebView2 standalone installer trong .\prereqs — app desktop co the khong khoi dong. Xem prereqs\README-WebView2.txt.'
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
# robocopy: /MIR gương payload trừ prereqs (không cần sau khi cài) và chính script deploy.
# Mã thoát robocopy < 8 là thành công.
$robolog = robocopy $payload $InstallDir /E /NFL /NDL /NJH /NJS /NP /XD (Join-Path $payload 'prereqs') /XF 'install.ps1' 'uninstall.ps1'
if ($LASTEXITCODE -ge 8) { throw "robocopy that bai (ma $LASTEXITCODE)" }

# --- 3. Chạy installer CLI (đăng ký service + hiển thị mã phục hồi) -------------------
if (-not (Test-Path $installerExe)) { throw "Khong tim thay $installerExe trong payload." }
Write-Step 'Đăng ký dịch vụ và sinh mã phục hồi...'
& $installerExe install
if ($LASTEXITCODE -ne 0) { throw "B2Clearance.exe install that bai (ma $LASTEXITCODE)" }

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
Write-Host 'HOAN TAT. Dich vu B2 dang chay. Mo app "So B2 — Cua khau Chi Ma" tren Desktop.' -ForegroundColor Green
Write-Host 'Endpoint mac dinh: http://127.0.0.1:5000/  (xem docs/deployment-offline.md neu client o may khac).'
