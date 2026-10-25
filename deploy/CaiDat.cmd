@echo off
rem Nhap dup de cai hoac nang cap B2 Clearance (#214). Tu xin quyen Administrator (UAC),
rem bo qua ExecutionPolicy cho rieng lan chay nay, roi goi install.ps1 cung thu muc.
rem install.ps1 tu hoi vai tro may khi cai moi va giu nguyen vai tro khi nang cap.
rem Giu tep nay ASCII: cmd.exe doc theo code page OEM, chu co dau se bi vo.
setlocal

net session >nul 2>&1
if errorlevel 1 (
    echo Dang xin quyen Administrator...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    if errorlevel 1 (
        echo Khong co quyen Administrator - chon "Yes" khi Windows hoi, roi chay lai CaiDat.cmd.
        pause
    )
    exit /b
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -PauseAtEnd
exit /b %errorlevel%
