# Sổ tay vận hành — Hệ thống B2 Clearance (v1)

> **Dành cho**: Quản trị viên tại Cổng B2 và nhân viên kỹ thuật hỗ trợ.  
> Tài liệu này cần có sẵn tại phòng giám sát và lưu trên USB dự phòng.

---

## 1. Khởi động / Dừng dịch vụ

### Khởi động

```powershell
# Mở PowerShell với quyền Administrator
Start-Service -Name "B2ClearanceService"
# Xác nhận trạng thái
Get-Service -Name "B2ClearanceService"
```

Dịch vụ sẽ khởi động trong vòng 10–15 giây. Mở ứng dụng Desktop và kiểm tra
thanh trạng thái phía dưới — nếu hiển thị màu xanh lá (🟢 Trực tuyến) thì
hệ thống đã hoạt động bình thường.

### Dừng có kiểm soát

```powershell
Stop-Service -Name "B2ClearanceService"
```

> ⚠️ Dừng dịch vụ trong giờ làm việc sẽ khiến toàn bộ máy client mất kết nối
> và không thể ghi thêm tờ khai mới. Chỉ thực hiện khi cần bảo trì.

### Tự động khởi động cùng Windows

Dịch vụ được cấu hình mặc định `StartupType = Automatic`. Sau mỗi lần khởi động
lại máy chủ, nó sẽ tự chạy mà không cần can thiệp.

### Kiểm tra log dịch vụ

Dịch vụ ghi log ra file theo ngày tại `C:\ProgramData\B2\logs\b2-yyyyMMdd.log`
(cũng xem được trong ứng dụng: **Sức khỏe → Nhật ký hệ thống**, mục 5). Thư mục
`C:\ProgramData\B2` chỉ **Administrator** đọc được (chứa khoá và cơ sở dữ liệu) — chạy lệnh dưới
trong PowerShell **Run as administrator**; mở bằng File Explorer thì Windows sẽ hỏi xác nhận. Log của ứng dụng
Desktop nằm trên từng máy tại `%LOCALAPPDATA%\B2\logs\`.

```powershell
# 50 dòng gần nhất của file log dịch vụ mới nhất
Get-ChildItem C:\ProgramData\B2\logs\b2-*.log |
    Sort-Object LastWriteTime | Select-Object -Last 1 |
    Get-Content -Tail 50
```

---

## 2. Admin quên mật khẩu

> ⚠️ Lệnh `B2.Server.Service.exe --reset-admin-password` từng được mô tả ở đây **không tồn
> tại** — đừng chạy lệnh này.

**Chưa đăng nhập lần nào** (mất mật khẩu ban đầu hiện lúc cài): trên máy cổng vào, PowerShell
**Run as administrator**:

```powershell
& "C:\Program Files\B2Clearance\B2Clearance.exe" reset-initial-admin
```

Lệnh hiện mật khẩu ban đầu mới và khởi động lại dịch vụ. Lệnh **từ chối** nếu admin đã từng tự
đổi mật khẩu — khi đó làm theo các bước dưới.

**Đã từng đổi mật khẩu rồi quên:**

1. Nếu còn một tài khoản Admin khác đăng nhập được: vào **Quản trị → Người dùng**, chọn tài
   khoản Admin bị quên mật khẩu → **Đặt lại mật khẩu** (xem mục 6, Lỗi 3).
2. Nếu không còn Admin nào đăng nhập được: **không** tự xóa hay sửa file
   `C:\ProgramData\B2\db\b2.db`. Liên hệ đội phát triển (mục 7). Chuẩn bị sẵn phong bì
   mã phục hồi (không đọc mã qua điện thoại/email).

> 💡 Phòng ngừa: tạo **ít nhất 2 tài khoản Admin** ngay sau khi cài đặt.

---

## 3. Sao lưu thủ công

### Phương pháp A — Trong ứng dụng (khuyến nghị)

1. Đăng nhập bằng tài khoản Admin.
2. Vào **Quản trị → Sao lưu & Khôi phục**.
3. Nhấn nút **Tạo bản sao lưu ngay**.
4. Chờ thông báo thành công (thường dưới 30 giây).
5. File `.bak.zip` được tạo tự động tại thư mục cấu hình
   (`C:\ProgramData\B2\backups\` hoặc thư mục USB/mạng LAN đã cấu hình).

### Phương pháp B — Thủ công qua file system (fallback)

```powershell
Stop-Service -Name "B2ClearanceService"
Copy-Item "C:\ProgramData\B2\db\b2.db" `
          "D:\Backup\b2-$(Get-Date -Format 'yyyyMMdd-HHmm').db"
Start-Service -Name "B2ClearanceService"
```

> File `.db` sao lưu bằng phương pháp B **không được mã hoá**. Lưu trữ ở nơi
> an toàn và xóa ngay sau khi không cần thiết.

### Sao lưu tự động (liên tục)

Trên máy cổng vào, dịch vụ B2 **tự bật** sao lưu liên tục (Litestream) ngay sau khi cài: mọi
thay đổi được chép sang `C:\ProgramData\B2\backup\replica` trong vài giây. Kiểm tra ở
**Sức khỏe → Tổng quan**, dòng "Trạng thái Litestream" phải là ✅ Đang chạy.

> ⚠️ Bản sao này nằm **trên cùng ổ đĩa** với dữ liệu: nó cứu được khi dữ liệu bị lỗi hoặc xóa
> nhầm, **không** cứu được khi hỏng ổ cứng/mất máy. Vì vậy vẫn phải sao lưu thủ công
> (phương pháp A) **hằng tuần** và cất file sang USB, hoặc nhờ IT thêm đích USB/LAN như dưới.

**Thêm đích sao lưu USB hoặc thư mục mạng** (làm một lần, cần quyền Administrator): tạo file
`C:\Program Files\B2Clearance\appsettings.Production.json` với nội dung (thay đường dẫn cho đúng):

```json
{
  "B2": {
    "Litestream": {
      "Replicas": [
        { "Name": "local", "Path": "C:\\ProgramData\\B2\\backup\\replica" },
        { "Name": "usb",   "Path": "E:\\B2-Backup" }
      ]
    }
  }
}
```

Giữ dòng `local`, thêm các dòng khác; lưu file rồi khởi động lại dịch vụ (mục 1). File này
được giữ nguyên khi nâng cấp. Muốn tắt hẳn sao lưu liên tục: `"Enabled": false`.

---

## 4. Khôi phục từ bản sao lưu

> ⚠️ **CẢNH BÁO**: Khôi phục sẽ xóa toàn bộ dữ liệu hiện tại và thay thế
> bằng dữ liệu tại thời điểm sao lưu. Mọi tờ khai, phương tiện, hay thay đổi
> sau thời điểm đó **sẽ bị mất vĩnh viễn**.

### Thủ tục

1. Thông báo cho tất cả người dùng đang online dừng thao tác.
2. Vào **Quản trị → Sao lưu & Khôi phục** → phần **Khôi phục**.
3. Nhấn **Chọn file** và chỉ đến file `.bak.zip`.
4. Ô **Mã phục hồi**:
   - Khôi phục **trên chính máy cổng vào** đã tạo bản sao lưu: **để trống** — hệ thống tự dùng
     khóa lưu sẵn trên máy.
   - Khôi phục trên **máy khác / máy cài lại**: nhập **mã phục hồi** trong phong bì niêm phong.
5. Gõ chính xác `RESTORE` (chữ in hoa) vào ô xác nhận.
6. Nhấn **Xác nhận khôi phục**.
7. Dịch vụ sẽ tự khởi động lại sau khi khôi phục xong (~30 giây).

### Sau khi khôi phục

- Kiểm tra lại tờ khai của ngày hôm nay để xác nhận tính toàn vẹn.
- Tạo ngay một bản sao lưu mới để đánh dấu trạng thái sau khôi phục.
- Ghi lại sự việc vào nhật ký vận hành với thời gian, người thực hiện,
  và lý do khôi phục.

---

## 5. Đọc bảng điều khiển sức khỏe hệ thống

Vào **Quản trị → Sức khỏe hệ thống**. Trang có hai tab:

### Tab Tổng quan

| Chỉ số | Ý nghĩa | Giá trị mong muốn |
|--------|---------|-------------------|
| Kết nối CSDL | CSDL SQLite có phản hồi không | ✅ Kết nối tốt |
| Thời gian phản hồi | Latency truy vấn | < 50 ms |
| Dung lượng CSDL | Kích thước file `b2.db` | < 500 MB |
| Lần sao lưu cuối | Thời gian bản sao lưu gần nhất | < 2 giờ trước |
| Trạng thái Litestream | Sao lưu liên tục | ✅ Đang chạy |

Nếu bất kỳ chỉ số nào hiển thị màu đỏ hoặc cảnh báo vàng, xem mục 6 bên dưới.

### Tab Nhật ký hệ thống

Hiển thị các sự kiện hệ thống gần đây. Dùng bộ lọc **Cấp độ** để chọn:

- `Warning` — cảnh báo không nghiêm trọng, cần theo dõi.
- `Error` — lỗi nghiêm trọng, cần xử lý ngay.
- `Information` — hoạt động bình thường.

---

## 6. Lỗi thường gặp và cách xử lý

### Lỗi 1: "Mất kết nối" — thanh trạng thái đỏ (🔴)

**Triệu chứng**: Ứng dụng Desktop hiển thị banner đỏ "Mất kết nối". Không thể
ghi tờ khai mới.

**Kiểm tra**:
1. Ping máy chủ: `ping <ip-máy-chủ>` trên máy client bị lỗi.
2. Kiểm tra dịch vụ B2 còn chạy không (xem mục 1).
3. Kiểm tra tường lửa Windows trên máy cổng vào: cổng 5001 (HTTPS) chỉ được mở cho mạng **Private**.
   Chạy `Get-NetConnectionProfile` — nếu `NetworkCategory` là `Public`, nhờ IT chuyển sang
   `Private`.
4. **Trên máy cổng ra**: mở `C:\Program Files\B2Clearance\Desktop\b2-client.json` và đối chiếu
   `serverUrl` với IP hiện tại của máy cổng vào (`ipconfig` trên máy đó). IP đổi sau khi khởi
   động lại router là nguyên nhân phổ biến nhất.
5. **Chứng chỉ HTTPS** (#147): trên máy cổng ra mở `https://<ip-máy-cổng-vào>:5001/` bằng Edge.
   Trang trắng / 404 là bình thường. Edge cảnh báo "không phải kết nối riêng tư" → máy cổng ra
   chưa tin chứng chỉ hiện tại của máy cổng vào (chưa nhập, hoặc máy cổng vào đã cấp lại).

**Khắc phục**:
- Nếu dịch vụ bị dừng: khởi động lại (xem mục 1).
- Nếu sai địa chỉ máy chủ: sửa `serverUrl` trong `b2-client.json` rồi mở lại app. Đề nghị IT
  đặt IP tĩnh cho máy cổng vào để khỏi lặp lại. **Máy cổng vào đã đổi IP**: chứng chỉ chỉ ghi IP
  cũ — trên máy cổng vào chạy (Administrator)
  `& "C:\Program Files\B2Clearance\B2Clearance.exe" renew-certificate`, chép
  `C:\ProgramData\B2\b2-server.cer` mới sang máy cổng ra và chạy
  `.\install.ps1 -ClientOnly -ServerUrl <IP mới> -ServerCertificate <tệp .cer>`.
- Nếu lỗi chứng chỉ (bước 5): chép lại `b2-server.cer` từ máy cổng vào và cài lại máy cổng ra
  như trên; đối chiếu Thumbprint script in ra với Thumbprint lúc cài máy cổng vào.
- Nếu vấn đề mạng: liên hệ IT.
- Nếu sau 5 phút vẫn không kết nối được: leo thang theo mục 7.

**Đổi địa chỉ máy chủ cho máy cổng ra**

```json
{
  "serverUrl": "192.168.1.10"
}
```

Đặt file tên `b2-client.json` cạnh `B2.Desktop.exe`. Chấp nhận IP trần (mặc định
`https://…:5001/`), `192.168.1.10:5001`, hoặc `https://192.168.1.10:5001/`. Máy chủ **không**
còn nghe `http://…:5000` — sửa file cũ ghi địa chỉ đó thành IP trần. Không có file này thì app tự nối tới
server trên **chính máy đó** — đúng cho máy cổng vào, sai cho máy cổng ra.

---

### Lỗi 2: "Lỗi sao lưu" — thông báo trên trang Sức khỏe

**Triệu chứng**: Trang Sức khỏe hiển thị cảnh báo "Lần sao lưu cuối > 2 giờ"
hoặc "Litestream: dừng".

**Kiểm tra**:
1. Kiểm tra USB sao lưu còn cắm vào và có dung lượng trống (> 1 GB).
2. Kiểm tra thư mục mạng LAN có còn truy cập được không.
3. Xem log: vào **Nhật ký hệ thống**, lọc `Error`, tìm dòng có từ
   "backup" hoặc "litestream".

**Khắc phục**:
- Thay USB nếu đầy hoặc hỏng.
- Khởi động lại dịch vụ B2 (Litestream supervisor sẽ tự khởi động lại).
- Tạo bản sao lưu thủ công ngay (xem mục 3, Phương pháp A).

---

### Lỗi 3: Màn hình đăng nhập báo "Tài khoản bị khoá"

**Triệu chứng**: Sau 5 lần nhập sai mật khẩu, tài khoản bị khoá 15 phút.

**Khắc phục tạm thời**: Chờ 15 phút và thử lại.

**Khắc phục vĩnh viễn (chỉ Admin)**:
1. Đăng nhập bằng tài khoản Admin (từ máy khác).
2. Vào **Quản trị → Người dùng**, tìm tài khoản bị khoá.
3. Nhấn **Đặt lại mật khẩu** và cung cấp mật khẩu tạm thời cho người dùng.
4. Yêu cầu người dùng đổi mật khẩu ngay sau khi đăng nhập.

---

## 7. Leo thang — Liên hệ đội phát triển

Khi gặp lỗi không xử lý được theo hướng dẫn trên, liên hệ:

| Thông tin | Giá trị |
|-----------|---------|
| **Email** | hoanghainh.1188@gmail.com |
| **Ưu tiên** | Dùng tiêu đề: `[B2-URGENT] <mô tả ngắn>` cho sự cố ảnh hưởng vận hành |
| **Thông tin cần cung cấp** | Ảnh chụp màn hình lỗi, log từ tab Nhật ký hệ thống (xuất file nếu được), thời điểm lỗi xảy ra, thao tác đang thực hiện |

> Không chia sẻ mật khẩu Admin hay mã phục hồi qua email hoặc điện thoại.
> Đội phát triển không bao giờ yêu cầu những thông tin này.

---

*Phiên bản tài liệu: v1.0 — Sprint S6 — 2026-06-06*
