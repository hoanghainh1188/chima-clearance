#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Gỡ B2 Clearance. Dừng + xóa Windows Service và firewall rule; GIỮ dữ liệu.

.DESCRIPTION
    Chạy quyền Administrator. Gọi B2Clearance.exe uninstall (dừng + xóa dịch vụ, xóa
    firewall rule), rồi xóa thư mục chương trình. Dữ liệu tại C:\ProgramData\B2 (database,
    backup, keys, mã phục hồi) được GIỮ LẠI để có thể cài lại và khôi phục.
    Muốn xóa hẳn dữ liệu, thêm -PurgeData.
#>
[CmdletBinding()]
param(
    [string] $InstallDir = (Join-Path $env:ProgramFiles 'B2Clearance'),
    [switch] $PurgeData
)

$ErrorActionPreference = 'Stop'
$installerExe = Join-Path $InstallDir 'B2Clearance.exe'
$dataDir = 'C:\ProgramData\B2'

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

if (Test-Path $installerExe) {
    Write-Step 'Dừng và gỡ dịch vụ B2...'
    & $installerExe uninstall
}
else {
    Write-Warning "Khong thay $installerExe — thu go dich vu truc tiep."
    & sc.exe stop 'B2ClearanceService' | Out-Null
    Start-Sleep -Seconds 2
    & sc.exe delete 'B2ClearanceService' | Out-Null
}

if (Test-Path $InstallDir) {
    Write-Step "Xóa thư mục chương trình $InstallDir ..."
    Remove-Item -Path $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
}

if ($PurgeData) {
    Write-Warning "XOA TOAN BO DU LIEU tai $dataDir (database, backup, keys)."
    Remove-Item -Path $dataDir -Recurse -Force -ErrorAction SilentlyContinue
}
else {
    Write-Host "Du lieu tai $dataDir duoc giu lai. Dung -PurgeData de xoa han." -ForegroundColor Yellow
}

Write-Host 'Da go B2 Clearance.' -ForegroundColor Green
