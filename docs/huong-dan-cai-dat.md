# Hướng dẫn cài đặt — Sổ B2 (Cửa khẩu Chi Ma)

> **Dành cho**: người cài đặt tại cửa khẩu (không cần biết lập trình).
> Chi tiết kỹ thuật của gói cài: [`deployment-offline.md`](deployment-offline.md).
> Vận hành hằng ngày sau khi cài: [`runbook.md`](runbook.md).

Thông thường hệ thống gồm **2 máy** trong cùng mạng LAN:

| Máy | Vai trò | Chọn khi cài |
| --- | --- | --- |
| **Cổng vào** | Máy chủ (chạy dịch vụ, giữ cơ sở dữ liệu và bản sao lưu) + ứng dụng Sổ B2 | **Máy cổng vào** |
| **Cổng ra** | Chỉ ứng dụng Sổ B2, kết nối tới máy cổng vào | **Máy cổng ra** |
| **Một máy dùng cho cả hai cổng** | Máy chủ + ứng dụng, làm cả việc cổng vào lẫn cổng ra | **Máy cổng vào** |

**Luôn cài máy cổng vào trước.**

> **Chỉ có một máy?** Cài như máy cổng vào (§ 3, § 4) rồi dừng — **bỏ qua § 5**. Ứng dụng trên
> máy đó làm được cả nhập xe lẫn xác nhận xe ra; quyền đi theo tài khoản, không theo máy. Mỗi cán
> bộ vẫn đăng nhập **tài khoản riêng** (đăng xuất khi đổi người) để nhật ký ghi đúng ai làm gì.
> Sau này thêm máy cổng ra: chỉ cần làm § 5 trên máy mới, không phải cài lại máy này.

---

## 1. Cần chuẩn bị

| Hạng mục | Yêu cầu |
| --- | --- |
| Máy tính | Windows 10 bản 1809 (build 17763) trở lên hoặc Windows 11, **64-bit** |
| Quyền | Tài khoản Windows có quyền **Administrator** trên cả 2 máy |
| Ổ đĩa | Còn trống ít nhất **1 GB** trên ổ `C:` |
| Mạng | 2 máy cùng mạng LAN. Nhờ IT đặt **IP tĩnh** cho máy cổng vào và để mạng ở chế độ **Private** |
| Internet | **Không cần** trên máy cài |
| Nhận từ đội phát triển | **`B2Clearance-<phiên bản>-setup.exe`** (khuyên dùng) **hoặc** gói `B2Clearance-<phiên bản>-offline.zip` |
| Vật dụng | 1 USB, 1 phong bì để niêm phong mã phục hồi |

---

## 2. Chuẩn bị USB

Bộ cài đã có sẵn mọi thứ cần thiết (kể cả WebView2 và VC++) — không phải tải thêm gì.

- **Dùng `setup.exe`**: chép file `B2Clearance-<phiên bản>-setup.exe` vào USB. Xong.
- **Dùng gói zip**: giải nén `B2Clearance-<phiên bản>-offline.zip`, chép **toàn bộ** thư mục vừa
  giải nén vào USB. Kiểm tra có `CaiDat.cmd`, `install.ps1`, thư mục `Desktop\`, `prereqs\`, `docs\`.

> Lần đầu mở `setup.exe`, Windows có thể hiện **"Windows protected your PC"** (bộ cài chưa ký
> số). Bấm **More info** → **Run anyway**.

---

## 3. Cài máy cổng vào

1. Cắm USB vào máy cổng vào.
2. Nhấp đúp **`B2Clearance-<phiên bản>-setup.exe`** (hoặc **`CaiDat.cmd`** trong thư mục gói
   zip). Windows hỏi quyền Administrator → bấm **Yes**.
3. Chọn **Máy cổng vào**, bấm **Tiếp** rồi **Cài đặt**. (`CaiDat.cmd` hỏi bằng chữ: gõ `1` rồi
   Enter.)
4. Một cửa sổ dòng lệnh màu đen mở ra và tự chạy khoảng 1–3 phút: cài VC++ và WebView2 → chép
   chương trình vào `C:\Program Files\B2Clearance` → đăng ký dịch vụ **B2ClearanceService** (tự
   chạy cùng Windows) → cấp **chứng chỉ bảo mật (HTTPS)** cho máy này → mở firewall TCP 5001 →
   **hiển thị MÃ PHỤC HỒI** → **hiển thị MẬT KHẨU ADMIN BAN ĐẦU** → tạo shortcut trên Desktop →
   khởi động dịch vụ. Làm theo hai mục 📋 và 🔑 dưới đây khi màn hình dừng lại chờ Enter.
5. Khi thấy dòng chữ xanh **HOAN TAT**, nhấn Enter để đóng cửa sổ. Cài đặt đã xong.
6. Ghi lại **địa chỉ IP** của máy này (chạy `ipconfig`, xem dòng `IPv4 Address`, ví dụ
   `192.168.1.10`) — cần cho bước cài máy cổng ra. Nên nhờ IT đặt **IP tĩnh** cho máy cổng vào:
   chứng chỉ gắn với IP lúc cài (đổi IP → xem § 7, dòng "Đổi IP máy cổng vào").
7. Ghi lại dòng **`Thumbprint`** trong khung chứng chỉ TLS trên màn hình, và **chép tệp
   `C:\ProgramData\B2\b2-server.cer` vào gốc USB** (ví dụ `E:\b2-server.cer`, cùng USB chứa bộ
   cài) — bộ cài trên máy cổng ra sẽ tự tìm thấy. Tệp này là phần **công khai** của chứng chỉ,
   chép đi không ảnh hưởng an toàn; **không** chép `server.pfx` trong thư mục `keys`.

### 📋 Mã phục hồi — BẮT BUỘC làm ngay

- Chép **MÃ PHỤC HỒI** hiện trên màn hình (cũng được lưu trong file
  `B2-Recovery-Code.txt` trên Desktop) ra giấy, cho vào phong bì, **niêm phong**, cất tại
  phòng quản lý.
- Mã này là cách duy nhất để giải mã bản sao lưu nếu mất mật khẩu cài đặt.
- Sau khi đã cất phong bì, **xóa** file `B2-Recovery-Code.txt` trên Desktop.

### 🔑 Mật khẩu Admin ban đầu — BẮT BUỘC ghi lại

- Ngay sau mã phục hồi, màn hình hiện khung **TÀI KHOẢN QUẢN TRỊ BAN ĐẦU**: tên đăng nhập
  `admin` và một mật khẩu ngẫu nhiên dạng `Hk7m-Pq4R-xT2n-Wd9s` (có dấu `-`, phân biệt chữ
  hoa/thường). Mỗi lần cài có mật khẩu **khác nhau**.
- Mật khẩu chỉ hiện **một lần** và **không** được lưu ra file. Chép ra giấy trước khi nhấn Enter,
  dùng ở § 4, rồi hủy tờ giấy sau khi đã đổi mật khẩu.
- Lỡ mất trước khi đăng nhập lần đầu: PowerShell **Run as administrator**, chạy
  `& "C:\Program Files\B2Clearance\B2Clearance.exe" reset-initial-admin` để cấp mật khẩu mới.

---

## 4. Đăng nhập lần đầu và cấu hình (trên máy cổng vào)

1. Mở shortcut **"Sổ B2 — Cửa khẩu Chi Ma"** trên Desktop.
2. Đăng nhập tài khoản **`admin`** với mật khẩu ban đầu đã ghi ở § 3.

   > ⚠️ Máy cổng vào mở cổng 5001 cho mạng nội bộ: cho tới khi đổi mật khẩu, ai biết mật khẩu
   > ban đầu đều đăng nhập được từ máy khác trong LAN. Làm bước 3 **ngay trong buổi cài**.
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

1. Cắm USB có bộ cài **và** tệp `b2-server.cer` ở gốc USB (§ 3 bước 7).
2. Nhấp đúp **`B2Clearance-<phiên bản>-setup.exe`** (hoặc **`CaiDat.cmd`**) → **Yes**.
3. Chọn **Máy cổng ra** → **Tiếp**.
4. Nhập **IP máy cổng vào** đã ghi ở § 3 bước 6 (ví dụ `192.168.1.10`) → **Tiếp**.
5. Ô **Tệp b2-server.cer** đã tự điền nếu tệp nằm ở gốc USB; nếu trống, bấm **Chọn tệp...** và
   chọn tệp. → **Tiếp** → **Cài đặt**.
   (`CaiDat.cmd`: gõ `2`, rồi nhập IP; tệp `b2-server.cer` ở gốc USB được tự tìm, không thấy thì
   script hỏi đường dẫn.)
6. Cửa sổ dòng lệnh in dòng **`Đã tin chứng chỉ máy chủ B2 — Thumbprint …`**: đối chiếu với
   Thumbprint đã ghi ở § 3 bước 7 — phải **giống hệt**. Khác nhau → dừng lại, chép lại tệp từ máy
   cổng vào rồi cài lại.
7. Máy này **không** có dịch vụ, cơ sở dữ liệu hay mã phục hồi riêng — mọi dữ liệu nằm ở máy
   cổng vào. Mở ứng dụng, đăng nhập bằng tài khoản đã tạo ở § 4.

Địa chỉ máy cổng vào được ghi vào `C:\Program Files\B2Clearance\Desktop\b2-client.json` (thư mục
`Desktop` **bên trong thư mục cài**, không phải màn hình Desktop của Windows). File này **chỉ có
trên máy cổng ra** — máy cổng vào (hoặc máy dùng cho cả hai cổng) không có file này là đúng: ứng
dụng tự kết nối tới dịch vụ trên chính máy đó.

Nếu sau này IP máy cổng vào thay đổi: sửa `serverUrl` trong file trên (mở Notepad bằng
**Run as administrator**) rồi mở lại ứng dụng — không cần cài lại.

> Cài bằng lệnh (cho IT): `.\install.ps1 -ServerUrl 192.168.1.10 [-ServerCertificate E:\b2-server.cer]`
> trong PowerShell Administrator. Lệnh cũ `-ClientOnly -ServerUrl … -ServerCertificate …` vẫn dùng được.

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

Khi có lỗi, cửa sổ dòng lệnh hiện dòng đỏ **`LOI: …`** và chờ Enter — đọc dòng đó rồi tra bảng
dưới.

| Hiện tượng | Cách xử lý |
| --- | --- |
| **"Windows protected your PC"** khi mở `setup.exe` | Bộ cài chưa ký số. Bấm **More info** → **Run anyway**. |
| Bấm **No** ở hộp thoại quyền Administrator / `CaiDat.cmd` báo *Khong co quyen Administrator* | Chạy lại và bấm **Yes**. Tài khoản Windows phải có quyền Administrator. |
| `Khong tim thay tep chung chi` / bộ cài hỏi đường dẫn `b2-server.cer` | Chưa có tệp ở gốc USB — xem § 3 bước 7. |
| `Tep chung chi khong duoc chua khoa bi mat` | Đã chọn nhầm `server.pfx`. Dùng `C:\ProgramData\B2\b2-server.cer` của máy cổng vào. |
| `Dia chi khong hop le` | Chỉ nhập IP (ví dụ `192.168.1.10`), không có khoảng trắng. |
| `robocopy that bai` | Kiểm tra ổ `C:` còn trống và không có ai đang mở ứng dụng Sổ B2 / thư mục `C:\Program Files\B2Clearance`. Chạy lại bộ cài. |
| Mất mật khẩu Admin ban đầu trước lần đăng nhập đầu tiên | PowerShell **Run as administrator**: `& "C:\Program Files\B2Clearance\B2Clearance.exe" reset-initial-admin` — hiện mật khẩu mới và khởi động lại dịch vụ. Nếu báo "đã đổi mật khẩu" thì làm theo [`runbook.md`](runbook.md) § 2. |
| `B2Clearance.exe install that bai` hoặc dòng lỗi có `sc.exe` | Chụp màn hình toàn bộ nội dung lỗi và gửi đội phát triển (§ 9). Không sửa tay trong Registry/Services. |
| Ứng dụng mở lên rồi tự tắt | Thường do thiếu WebView2 hoặc VC++. Chạy lại bộ cài; nếu vẫn bị, chạy thủ công 2 file trong thư mục `prereqs\` của gói zip rồi mở lại ứng dụng. |
| Máy cổng vào báo **🔴 Mất kết nối** | Kiểm tra dịch vụ: `Get-Service B2ClearanceService`. Nếu `Stopped` → `Start-Service B2ClearanceService`, chờ 15 giây. |
| Máy cổng ra thiếu file `b2-client.json` / cài nhầm vai trò | Chạy `uninstall.ps1` trong gói zip, rồi cài lại và chọn đúng **Máy cổng ra**. |
| Máy cổng vào 🟢 nhưng máy cổng ra **🔴 Mất kết nối** | 1) Trên máy cổng ra: `ping <IP máy cổng vào>`. 2) Đối chiếu IP trong `b2-client.json` (§ 5). 3) Trên máy cổng vào: `Get-NetConnectionProfile` phải là `Private` — firewall chỉ mở cổng 5001 cho mạng Private; nếu là `Public`, nhờ IT chuyển sang Private. 4) Chứng chỉ: mở `https://<IP máy cổng vào>:5001/` bằng Edge trên máy cổng ra — trang trắng / lỗi 404 là **bình thường**; nếu Edge báo *"Kết nối của bạn không phải là kết nối riêng tư"* nghĩa là máy cổng ra chưa nhập đúng `b2-server.cer` mới nhất (cài lại theo § 5). |
| Đổi IP máy cổng vào | Trên máy cổng vào (Administrator): `& "C:\Program Files\B2Clearance\B2Clearance.exe" renew-certificate`. Nếu in "Đã cấp chứng chỉ TLS mới": chép `b2-server.cer` mới vào gốc USB, rồi chạy lại bộ cài trên máy cổng ra và nhập IP mới. |

Chạy lại bộ cài là an toàn: dữ liệu tại `C:\ProgramData\B2` được giữ nguyên.

---

## 8. Nâng cấp và gỡ cài

- **Nâng cấp**: chép `setup.exe` (hoặc gói zip) của bản mới vào USB, rồi nhấp đúp trên từng máy —
  **nâng cấp máy cổng vào trước**. Bộ cài tự nhận ra máy đã cài và giữ nguyên vai trò (cổng vào /
  cổng ra), địa chỉ máy chủ và chứng chỉ; không phải chọn lại. Dữ liệu, bản sao lưu và mã phục hồi
  được giữ nguyên; mã phục hồi **không** đổi.
- **Nâng cấp từ v1.3.0 trở về trước lên bản HTTPS** (#147): máy cổng vào được cấp chứng chỉ mới
  (đọc lại § 3 bước 7 — ghi Thumbprint, chép `b2-server.cer` vào gốc USB). Máy cổng ra **phải**
  có tệp đó khi nâng cấp; nếu `b2-client.json` cũ ghi `http://…:5000`, sửa thành IP trần.
- **Gỡ cài** (PowerShell Administrator, trong thư mục gói zip):

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
