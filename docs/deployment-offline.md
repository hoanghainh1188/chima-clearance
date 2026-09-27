# Triển khai offline (máy client không có internet)

> Mục tiêu: cài đặt **đơn giản, minh bạch, offline** trên PC cửa khẩu **không có internet**.
> Cơ chế: **xcopy** — một gói `.zip` self-contained + `install.ps1`. Không có framework cài
> đặt, không có cơ chế auto-update (đã bỏ Velopack — xem [ADR-0007](adr/ADR-0007-evaluate-go-backend-port.md)).
>
> Nguyên tắc: máy đích **không cần cài .NET runtime**; các thành phần OS còn thiếu
> (WebView2, VC++) được mang theo qua USB.

> 👉 **Người cài đặt tại cửa khẩu**: làm theo [`huong-dan-cai-dat.md`](huong-dan-cai-dat.md)
> (từng bước cho máy cổng vào + cổng ra, checklist, xử lý lỗi). Tài liệu này giải thích phần
> kỹ thuật của gói cài.

## Vì sao self-contained là đủ (và điều gì vẫn thiếu)

| Thành phần | Có cần cài trên máy đích? |
| --- | --- |
| **.NET 8 runtime** | ❌ Không — nhúng trong exe. `B2.Server.Service.exe` (~112 MB) và Desktop (`WindowsAppSDKSelfContained=true`) đã bao gồm runtime. |
| **Windows App SDK runtime** | ❌ Không — nhúng khi `WindowsAppSDKSelfContained=true`. |
| **WebView2 Runtime** | ✅ **Có** — MAUI/WinUI cần, KHÔNG nhúng. Mang bản *Standalone (offline)* qua USB. |
| **VC++ 2015–2022 x64 Redistributable** | ✅ **Có** — KHÔNG nhúng. Đã đóng gói sẵn `prereqs/vc_redist.x64.exe`. |
| **SQLite** | ❌ Không — `e_sqlite3.dll` đi kèm; DB là 1 file `b2.db`. |
| **Litestream** (backup) | ❌ Không — `litestream.exe` đi kèm; chạy trên đĩa nội bộ/LAN, không cần internet. |

> ⚠️ Bỏ qua WebView2 hoặc VC++ redist → app desktop có thể **khởi động rồi crash** trên
> máy sạch. `install.ps1` tự cài 2 prereq này từ `prereqs\` trước khi cài app.

## Payload (bản phát hành tạo ra gì)

Mỗi tag `vX.Y.Z` kích hoạt `.github/workflows/release.yml` (runner Windows có internet) và
tạo GitHub Release đính kèm **một file zip**: `B2Clearance-<version>-offline.zip`. Giải nén ra:

```
install.ps1 / uninstall.ps1   # script vận hành (chạy quyền Administrator)
B2Clearance.exe               # installer CLI (đăng ký service, sinh mã phục hồi)
B2.Server.Service.exe         # Windows Service (self-contained)
Desktop/                      # app desktop (self-contained)
Templates/                    # mẫu Sổ B2 (.xlsx)
litestream.exe                # backup liên tục
prereqs/
  vc_redist.x64.exe           # đã tải sẵn (offline)
  README-WebView2.txt         # nhắc bổ sung WebView2 standalone
```

## Bước 1 — Chuẩn bị USB (trên MÁY CÓ internet, làm 1 lần)

1. Tải `B2Clearance-<version>-offline.zip` từ Release, **giải nén**.
2. Tải **WebView2 Evergreen *Standalone* Installer (x64)** từ
   <https://developer.microsoft.com/microsoft-edge/webview2/> → **Standalone Installer**, **x64**.
   Đặt file (`MicrosoftEdgeWebView2RuntimeInstaller*.exe`) vào thư mục `prereqs\`.
3. Chép toàn bộ thư mục đã giải nén vào USB.

> Từ đây trở đi **không cần internet nữa**.

## Bước 2 — Cài trên PC cửa khẩu (offline)

1. Chép thư mục từ USB vào PC (ví dụ `C:\B2-Setup\`).
2. Chuột phải `install.ps1` → **Run with PowerShell** (hoặc PowerShell Administrator):

   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass -Force   # nếu bị chặn chạy script
   .\install.ps1
   ```

`install.ps1` sẽ tự động: cài VC++ + WebView2 (im lặng) → chép chương trình vào
`C:\Program Files\B2Clearance` → gọi `B2Clearance.exe install` (tạo thư mục dữ liệu, chứng
chỉ TLS localhost, đăng ký Windows Service tự khởi động, mở firewall TCP 5000, **sinh + hiển
thị MÃ PHỤC HỒI**) → tạo shortcut desktop → khởi động dịch vụ.

> 📋 **Ghi lại MÃ PHỤC HỒI** hiển thị trên màn hình (và file `B2-Recovery-Code.txt` trên
> Desktop) vào phong bì dán kín — dùng để giải mã backup khi quên mật khẩu cài đặt (FR-026).
> Xóa file nhắc sau khi đã lưu an toàn.

## Bước 3 — Kiểm tra sau cài

- [ ] Dịch vụ **B2ClearanceService** đang chạy (`services.msc` / `sc query B2ClearanceService`).
- [ ] Mở app "Sổ B2 — Cửa khẩu Chi Ma", đăng nhập được → chỉ báo **🟢 Online**.
- [ ] Không có hộp thoại "cần cài .NET / Windows App Runtime / WebView2".
- [ ] `C:\ProgramData\B2\db\b2.db` xuất hiện; Litestream ghi replica trong `backup\`.

## Máy cổng ra (client trên LAN)

Nghiệp vụ có **2 máy**: máy **cổng vào** chạy service (vừa server vừa client) và máy **cổng ra**
chỉ là client, nối qua LAN. Cài máy cổng ra bằng:

```powershell
.\install.ps1 -ClientOnly -ServerUrl 192.168.1.10     # IP của máy cổng vào
```

- `-ServerUrl` ghi `Desktop\b2-client.json` → `{"serverUrl": "192.168.1.10"}`. Nhận IP trần
  (tự hiểu `http://…:5000/`), `ip:port`, hoặc URL đầy đủ. Không có file này thì desktop dùng
  `http://127.0.0.1:5000/` (máy cổng vào).
- `-ClientOnly` bỏ qua đăng ký service, chứng chỉ và mã phục hồi — máy cổng ra **không** có
  database riêng. Bắt buộc đi kèm `-ServerUrl`.
- Đổi địa chỉ sau khi cài: sửa trực tiếp `C:\Program Files\B2Clearance\Desktop\b2-client.json`
  rồi mở lại app. Không cần cài lại. (Lưu bằng Notepad kèm BOM vẫn đọc được.)
- Biến môi trường `B2_SERVER_URL` ghi đè file — tiện khi cần thử nhanh một địa chỉ khác.

Server lắng nghe `http://0.0.0.0:5000` (khai báo trong `appsettings.json`) nên máy khác trong
LAN gọi được; installer đã mở sẵn firewall TCP 5000 ở profile Private. Nếu app báo
🔴 **Mất kết nối**: `ping <ip-máy-cổng-vào>`, rồi kiểm tra service đang chạy và firewall.

## Cập nhật phiên bản (vẫn offline)

Không có auto-update. Nâng cấp = chạy lại `install.ps1` từ gói zip mới (qua USB): script dừng
dịch vụ, chép đè binaries, khởi động lại. Dữ liệu `C:\ProgramData\B2` (database, backup, keys,
mã phục hồi) **được giữ nguyên** — `B2Clearance.exe install` bỏ qua các bước sinh secrets/cert
nếu đã tồn tại (idempotent).

## Gỡ cài

```powershell
.\uninstall.ps1              # dừng + xóa dịch vụ, giữ lại dữ liệu C:\ProgramData\B2
.\uninstall.ps1 -PurgeData   # xóa cả dữ liệu (không hồi phục được)
```

## Vì sao chọn xcopy thay vì Velopack

- Client **offline vĩnh viễn** ⇒ cơ chế auto-update/feed của Velopack là gánh nặng vô dụng.
- Một site duy nhất ⇒ không cần hạ tầng phân phối/cập nhật; `.zip` + script là đủ và **minh
  bạch hoàn toàn** (dễ kiểm toán an ninh trên máy khoá cứng).
- Logic bảo mật đã kiểm thử (mã phục hồi, DPAPI-seal, self-signed cert, đăng ký service) được
  **giữ nguyên** trong `B2Clearance.exe` — chỉ bóc lớp Velopack bọc ngoài.

## Ghi chú xác minh

- ✅ Publish self-contained single-file của **Server** đã chạy thật (macOS→win-x64):
  `B2.Server.Service.exe` 112 MB, nhúng runtime, chỉ kèm `e_sqlite3.dll`.
- ⏳ Publish self-contained của **Desktop** (`WindowsAppSDKSelfContained=true`) và build
  **B2Clearance.exe** (TFM `net8.0-windows`) chỉ chạy trên runner Windows có workload MAUI —
  xác nhận ở lần chạy `release.yml` của tag kế tiếp. macOS/Linux không build được 2 project này.
- ⏳ `install.ps1` / `uninstall.ps1` kiểm thử bằng tay (không có PowerShell trên máy build);
  chạy thực tế trên PC Windows đích ở UAT.
- 🐛 Đã sửa: trên máy **sạch**, `B2Clearance.exe install` gọi `sc create` với `binpath="…"`,
  `start=auto` (thiếu dấu cách sau `=` mà `sc.exe` bắt buộc) → `sc` từ chối, không đăng ký
  được dịch vụ. `sc failure` (`reset=60`, `actions=…`) cùng lỗi và chạy cả khi nâng cấp.
  Các bản zip **v1.1.0 → v1.2.0** đều mang lỗi này. Cần xác nhận lại trên PC Windows sạch.
