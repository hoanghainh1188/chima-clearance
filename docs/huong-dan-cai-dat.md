# Hướng dẫn cài đặt — Sổ B2 (Cửa khẩu Chi Ma)

> **Dành cho**: người cài đặt tại cửa khẩu (không cần biết lập trình).
> Chi tiết kỹ thuật của gói cài: [`deployment-offline.md`](deployment-offline.md).
> Vận hành hằng ngày sau khi cài: [`runbook.md`](runbook.md).

Thông thường hệ thống gồm **2 máy** trong cùng mạng LAN:

| Máy | Vai trò | Lệnh cài |
| --- | --- | --- |
| **Cổng vào** | Máy chủ (chạy dịch vụ, giữ cơ sở dữ liệu và bản sao lưu) + ứng dụng Sổ B2 | `.\install.ps1` |
| **Cổng ra** | Chỉ ứng dụng Sổ B2, kết nối tới máy cổng vào | `.\install.ps1 -ClientOnly -ServerUrl <IP máy cổng vào>` |
| **Một máy dùng cho cả hai cổng** | Máy chủ + ứng dụng, làm cả việc cổng vào lẫn cổng ra | `.\install.ps1` |

**Luôn cài máy cổng vào trước.**

> **Chỉ có một máy?** Cài như máy cổng vào (§ 3, § 4) rồi dừng — **bỏ qua § 5**, không dùng
> `-ClientOnly`. Ứng dụng trên máy đó làm được cả nhập xe lẫn xác nhận xe ra; quyền đi theo
> tài khoản, không theo máy. Mỗi cán bộ vẫn đăng nhập **tài khoản riêng** (đăng xuất khi đổi
> người) để nhật ký ghi đúng ai làm gì. Sau này thêm máy cổng ra: chỉ cần làm § 5 trên máy mới,
> không phải cài lại máy này.

---

## 1. Cần chuẩn bị

| Hạng mục | Yêu cầu |
| --- | --- |
| Máy tính | Windows 10 bản 1809 (build 17763) trở lên hoặc Windows 11, **64-bit** |
| Quyền | Tài khoản Windows có quyền **Administrator** trên cả 2 máy |
| Ổ đĩa | Còn trống ít nhất **1 GB** trên ổ `C:` |
| Mạng | 2 máy cùng mạng LAN. Nhờ IT đặt **IP tĩnh** cho máy cổng vào và để mạng ở chế độ **Private** |
| Internet | **Không cần** trên máy cài (chỉ cần ở máy chuẩn bị USB) |
| Nhận từ đội phát triển | Gói `B2Clearance-<phiên bản>-offline.zip` và **bản PDF hướng dẫn** (có mật khẩu Admin ban đầu — giữ như tài liệu mật, không gửi qua email) |
| Vật dụng | 1 USB, 1 phong bì để niêm phong mã phục hồi |

---

## 2. Chuẩn bị USB (trên máy có internet, làm 1 lần)

1. Giải nén `B2Clearance-<phiên bản>-offline.zip`.
2. Tải **Microsoft Edge WebView2 Runtime** — bản **Evergreen Standalone Installer**, **x64**
   — tại <https://developer.microsoft.com/microsoft-edge/webview2/>.
3. Đặt file vừa tải (tên dạng `MicrosoftEdgeWebView2RuntimeInstallerX64.exe`) vào thư mục
   `prereqs\` của thư mục đã giải nén.
4. Chép **toàn bộ** thư mục đã giải nén sang USB.

Kiểm tra trên USB có đủ: `install.ps1`, `B2Clearance.exe`, `B2.Server.Service.exe`,
thư mục `Desktop\`, `Templates\`, `docs\` (chính hướng dẫn này và sổ tay vận hành),
`litestream.exe`, và trong `prereqs\` có `vc_redist.x64.exe` **và** file WebView2.

---

## 3. Cài máy cổng vào

1. Chép thư mục từ USB vào máy, ví dụ `C:\B2-Setup\`.
2. Mở **Start** → gõ `PowerShell` → chuột phải **Windows PowerShell** →
   **Run as administrator**.
3. Chạy:

   ```powershell
   cd C:\B2-Setup
   Set-ExecutionPolicy -Scope Process Bypass -Force
   .\install.ps1
   ```

4. Chờ khoảng 1–3 phút. Script tự làm các bước: cài VC++ và WebView2 → chép chương trình
   vào `C:\Program Files\B2Clearance` → đăng ký dịch vụ **B2ClearanceService** (tự chạy cùng
   Windows) → mở firewall TCP 5000 → **hiển thị MÃ PHỤC HỒI** → tạo shortcut trên Desktop →
   khởi động dịch vụ.
5. Khi thấy dòng chữ xanh **HOAN TAT**, cài đặt đã xong.
6. Ghi lại **địa chỉ IP** của máy này (chạy `ipconfig`, xem dòng `IPv4 Address`, ví dụ
   `192.168.1.10`) — cần cho bước cài máy cổng ra.

### 📋 Mã phục hồi — BẮT BUỘC làm ngay

- Chép **MÃ PHỤC HỒI** hiện trên màn hình (cũng được lưu trong file
  `B2-Recovery-Code.txt` trên Desktop) ra giấy, cho vào phong bì, **niêm phong**, cất tại
  phòng quản lý.
- Mã này là cách duy nhất để giải mã bản sao lưu nếu mất mật khẩu cài đặt.
- Sau khi đã cất phong bì, **xóa** file `B2-Recovery-Code.txt` trên Desktop.

---

## 4. Đăng nhập lần đầu và cấu hình (trên máy cổng vào)

1. Mở shortcut **"Sổ B2 — Cửa khẩu Chi Ma"** trên Desktop.
2. Đăng nhập tài khoản **`admin`** với mật khẩu ban đầu ghi trong **bản PDF hướng dẫn** do đội
   phát triển bàn giao (mật khẩu không có trong gói cài).

   > ⚠️ Mọi bản cài đều dùng **chung** mật khẩu ban đầu này. Máy cổng vào mở cổng 5000 cho mạng
   > nội bộ, nên cho tới khi đổi mật khẩu, **bất kỳ máy nào trong LAN** cũng đăng nhập được.
   > Làm bước 3 **ngay trong buổi cài**, không để qua ngày.
3. Hệ thống **bắt buộc đổi mật khẩu** ngay. Mật khẩu mới: ít nhất 10 ký tự, gồm ít nhất
   3 trong 4 loại: chữ hoa, chữ thường, số, ký tự đặc biệt.
4. Cấu hình ban đầu trong ứng dụng:
   - **Người dùng**: tạo tài khoản cho từng cán bộ, gán vai trò phù hợp. Tạo **ít nhất
     2 tài khoản Admin** (do 2 người giữ) — hiện **chưa có** công cụ tự đặt lại mật khẩu
     Admin khi quên, chỉ Admin khác đặt lại được (xem [`runbook.md`](runbook.md) § 2).
   - **Danh mục**: cập nhật danh sách cán bộ xử lý và bãi. Dữ liệu mẫu có đuôi `(seed)` —
     ngừng sử dụng các mục này khi đã tạo mục thật.
5. Tạo **bản sao lưu đầu tiên** (xem [`runbook.md`](runbook.md) § 3, Phương pháp A).

---

## 5. Cài máy cổng ra

1. Chép thư mục từ USB vào máy, mở PowerShell **Run as administrator** như § 3.
2. Chạy (thay `192.168.1.10` bằng IP máy cổng vào đã ghi ở § 3 bước 6):

   ```powershell
   cd C:\B2-Setup
   Set-ExecutionPolicy -Scope Process Bypass -Force
   .\install.ps1 -ClientOnly -ServerUrl 192.168.1.10
   ```

3. Máy này **không** có dịch vụ, cơ sở dữ liệu hay mã phục hồi riêng — mọi dữ liệu nằm ở
   máy cổng vào.
4. Mở ứng dụng, đăng nhập bằng tài khoản đã tạo ở § 4.

Tham số `-ServerUrl` tạo file `C:\Program Files\B2Clearance\Desktop\b2-client.json` (thư mục
`Desktop` **bên trong thư mục cài**, không phải màn hình Desktop của Windows). File này **chỉ có
trên máy cổng ra** — máy cổng vào (hoặc máy dùng cho cả hai cổng) không có file này là đúng: ứng
dụng tự kết nối tới dịch vụ trên chính máy đó.

Nếu sau này IP máy cổng vào thay đổi: sửa `serverUrl` trong file trên (mở Notepad bằng
**Run as administrator**) rồi mở lại ứng dụng — không cần cài lại.

---

## 6. Kiểm tra sau cài

**Máy cổng vào**

- [ ] Dịch vụ đang chạy: trong PowerShell, `Get-Service B2ClearanceService` → `Running`.
- [ ] Mở ứng dụng, đăng nhập được, góc trên hiện **🟢 Online**.
- [ ] Không xuất hiện hộp thoại yêu cầu cài ".NET", "Windows App Runtime" hay "WebView2".
- [ ] File `C:\ProgramData\B2\db\b2.db` đã được tạo.
- [ ] Thư mục `C:\ProgramData\B2\backup\` có dữ liệu sau vài phút.
- [ ] Mã phục hồi đã niêm phong, file nhắc trên Desktop đã xóa.

**Máy cổng ra**

- [ ] Mở ứng dụng, đăng nhập được, góc trên hiện **🟢 Online**.
- [ ] File `C:\Program Files\B2Clearance\Desktop\b2-client.json` tồn tại và `serverUrl` đúng IP
      máy cổng vào.
- [ ] Nhập thử một xe ở cổng vào → xe hiện trên màn hình cổng ra trong vài giây.

---

## 7. Xử lý lỗi khi cài

| Hiện tượng | Cách xử lý |
| --- | --- |
| `install.ps1 cannot be loaded because running scripts is disabled` | Chạy lại `Set-ExecutionPolicy -Scope Process Bypass -Force` trong **cùng cửa sổ** PowerShell. Nếu vẫn bị chặn (chính sách nhóm của đơn vị), nhờ IT cho phép chạy script. |
| `The script ... contains a "#requires" statement for running as Administrator` | PowerShell chưa chạy quyền Administrator — mở lại theo § 3 bước 2. |
| `-ClientOnly phai di kem -ServerUrl` | Thiếu IP máy cổng vào trong lệnh — xem § 5 bước 2. |
| `robocopy that bai` | Kiểm tra ổ `C:` còn trống và không có ai đang mở ứng dụng Sổ B2 / thư mục `C:\Program Files\B2Clearance`. Chạy lại `install.ps1`. |
| `B2Clearance.exe install that bai` hoặc dòng lỗi có `sc.exe` | Chụp màn hình toàn bộ nội dung lỗi và gửi đội phát triển (§ 9). Không sửa tay trong Registry/Services. |
| `Khong thay WebView2 standalone installer` | Thiếu file WebView2 trong `prereqs\` — làm lại § 2 bước 2–3, rồi chạy lại `install.ps1`. |
| Ứng dụng mở lên rồi tự tắt | Thường do thiếu WebView2 hoặc VC++. Chạy thủ công 2 file trong `prereqs\`, rồi mở lại ứng dụng. |
| Máy cổng vào báo **🔴 Mất kết nối** | Kiểm tra dịch vụ: `Get-Service B2ClearanceService`. Nếu `Stopped` → `Start-Service B2ClearanceService`, chờ 15 giây. |
| Máy cổng ra thiếu file `b2-client.json` | Lúc cài chưa truyền `-ServerUrl` (không thấy dòng `Endpoint desktop -> ...`). Chạy lại `.\install.ps1 -ClientOnly -ServerUrl <IP máy cổng vào>`. |
| Máy cổng vào 🟢 nhưng máy cổng ra **🔴 Mất kết nối** | 1) Trên máy cổng ra: `ping <IP máy cổng vào>`. 2) Đối chiếu IP trong `b2-client.json` (§ 5). 3) Trên máy cổng vào: `Get-NetConnectionProfile` phải là `Private` — firewall chỉ mở cổng 5000 cho mạng Private; nếu là `Public`, nhờ IT chuyển sang Private. |

Chạy lại `install.ps1` là an toàn: dữ liệu tại `C:\ProgramData\B2` được giữ nguyên.

---

## 8. Nâng cấp và gỡ cài

- **Nâng cấp**: chuẩn bị USB với gói zip mới (§ 2), rồi chạy **đúng lệnh đã dùng khi cài**
  trên từng máy (§ 3 hoặc § 5). Nâng cấp máy cổng vào trước. Dữ liệu, bản sao lưu và mã phục
  hồi được giữ nguyên; mã phục hồi **không** đổi.
- **Gỡ cài** (PowerShell Administrator, trong thư mục gói cài):

  ```powershell
  .\uninstall.ps1              # gỡ chương trình, GIỮ dữ liệu
  .\uninstall.ps1 -PurgeData   # xóa cả dữ liệu — KHÔNG khôi phục được
  ```

---

## 9. Liên hệ hỗ trợ

Xem [`runbook.md`](runbook.md) § 7. Gửi kèm ảnh chụp màn hình lỗi và file log mới nhất:

- Dịch vụ (máy cổng vào): `C:\ProgramData\B2\logs\` — chỉ Administrator mở được; File
  Explorer sẽ hỏi xác nhận quyền.
- Ứng dụng (máy gặp lỗi): `%LOCALAPPDATA%\B2\logs\`

**Không** gửi mật khẩu Admin hay mã phục hồi.
