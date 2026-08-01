# UAT Sign-off — B2 Clearance (001-b2-clearance)

> **Thực hiện trên phần cứng thực tế** (không phải VM ảo hóa) theo yêu cầu T-S6-12.
> Điền vào tất cả các ô trống, ký tên, và lưu file này vào repo trước khi release.
>
> **Cần đủ HAI máy.** Nghiệp vụ tại cổng B2 có hai làn: máy **cổng vào** vừa chạy dịch vụ
> vừa là client, máy **cổng ra** chỉ là client nối qua LAN
> (`Business rules - Hướng dẫn điền sổ B2.md` § B, `Định nghĩa yêu cầu.md` § Business rules).
> Chạy UAT trên một máy sẽ **không** kiểm tra được nửa nghiệp vụ của cổng ra.

---

## Thông tin môi trường thử nghiệm

| Trường | Máy CỔNG VÀO (server + client) | Máy CỔNG RA (client) |
|--------|-------------------------------|----------------------|
| Máy tính (model / CPU / RAM) | | |
| Hệ điều hành | | |
| Địa chỉ IP trong LAN | | — |
| Phiên bản cài đặt | v | v |
| Hash gói cài (SHA-256) | | |

| Trường | Giá trị |
|--------|---------|
| Ngày thực hiện | |
| Người thực hiện | |
| Hai máy nối cùng switch/LAN, không internet | ⬜ Đúng |

---

## Phần A — Máy cổng vào (10 bước, per `04-implementation-plan.md § 8`)

| # | Bước | Kết quả | Ghi chú |
|---|------|---------|---------|
| 1 | Giải nén gói `B2Clearance-<version>-offline.zip`, chạy `.\install.ps1` với quyền Administrator, chấp nhận mặc định → dịch vụ được tạo và khởi động | ⬜ Pass / ⬜ Fail | |
| 2 | Mở Desktop, đăng nhập Admin mặc định → hệ thống ép đổi mật khẩu | ⬜ Pass / ⬜ Fail | |
| 3 | Thêm 1 Người xử lý + 1 Bãi | ⬜ Pass / ⬜ Fail | |
| 4 | Tạo 1 tờ khai với 2 phương tiện | ⬜ Pass / ⬜ Fail | |
| 5 | Tìm kiếm bằng 4 chữ số cuối biển số → tìm thấy cả 2 | ⬜ Pass / ⬜ Fail | |
| 6 | Xác nhận ra cổng cả 2 xe → tờ khai chuyển xanh | ⬜ Pass / ⬜ Fail | |
| 7 | Xác nhận KVGS → tờ khai xanh biến mất khỏi KVGS | ⬜ Pass / ⬜ Fail | |
| 8 | Xuất Excel → file mở được trong Excel 2010 / LibreOffice không lỗi | ⬜ Pass / ⬜ Fail | |
| 9 | "Tạo bản sao lưu ngay" → file sao lưu xuất hiện trong thư mục backup | ⬜ Pass / ⬜ Fail | |
| 10 | Dừng dịch vụ → Desktop hiển thị rõ banner đỏ "Mất kết nối" và tự thử lại | ⬜ Pass / ⬜ Fail | |

**Kết quả phần A**: ⬜ PASS (10/10) &nbsp;&nbsp; ⬜ FAIL (xem mục Phát hiện)

---

## Phần B — Máy cổng ra (client trên LAN)

> Khởi động lại dịch vụ trên máy cổng vào trước khi làm phần này (bước 10 đã dừng nó).
> Tài khoản dùng ở đây phải là **Người nhập liệu (Operator)** — đúng vai trò cán bộ trực cổng ra,
> **không** dùng Admin, vì mục đích là kiểm tra cán bộ làm trọn được việc của mình.

| # | Bước | Kết quả | Ghi chú |
|---|------|---------|---------|
| B1 | Trên máy cổng vào: Admin tạo 1 tài khoản **Operator** cho cán bộ cổng ra | ⬜ Pass / ⬜ Fail | |
| B2 | Trên máy cổng ra: chạy `.\install.ps1 -ClientOnly -ServerUrl <ip-máy-cổng-vào>` | ⬜ Pass / ⬜ Fail | |
| B3 | Máy cổng ra **không** có dịch vụ B2 (`sc query B2ClearanceService` → không tồn tại) và **không** sinh mã phục hồi | ⬜ Pass / ⬜ Fail | |
| B4 | File `...\B2Clearance\Desktop\b2-client.json` tồn tại, `serverUrl` đúng IP máy cổng vào | ⬜ Pass / ⬜ Fail | |
| B5 | Mở Desktop trên máy cổng ra, đăng nhập bằng tài khoản Operator (B1) → chỉ báo 🟢 **Online** | ⬜ Pass / ⬜ Fail | |
| B6 | Máy cổng vào tạo 1 tờ khai mới 2 xe → máy cổng ra tìm theo **3** và **4** số cuối đều thấy | ⬜ Pass / ⬜ Fail | |
| B7 | Từ máy cổng ra, tick **Xe đã ra** cho cả 2 xe → tờ khai chuyển xanh; màn hình máy cổng vào tự cập nhật trong ≤ 1 giây | ⬜ Pass / ⬜ Fail | |
| B8 | Vẫn tài khoản Operator đó, **Xác nhận KVGS** → thành công, tờ khai rời danh sách chờ KVGS *(FR-027 — cán bộ cổng ra làm trọn việc, không cần Supervisor)* | ⬜ Pass / ⬜ Fail | |
| B9 | Mở chi tiết 1 xe, nhập **Ghi chú** rồi bấm "Lưu ghi chú" → lưu thành công; máy cổng vào mở lại chi tiết thấy đúng nội dung | ⬜ Pass / ⬜ Fail | |
| B10 | Rút dây mạng máy cổng ra → banner đỏ "Mất kết nối"; cắm lại → tự hồi phục về 🟢 Online, không cần khởi động lại app | ⬜ Pass / ⬜ Fail | |
| B11 | Tắt dịch vụ ở máy cổng vào → máy cổng ra hiện banner đỏ và **từ chối ghi**; bật lại → tự hồi phục | ⬜ Pass / ⬜ Fail | |

**Kết quả phần B**: ⬜ PASS (11/11) &nbsp;&nbsp; ⬜ FAIL (xem mục Phát hiện)

---

## Phần C — Quy tắc nghiệp vụ đã chỉnh theo tài liệu của đơn vị

> Các mục này kiểm tra đúng những hành vi vừa được sửa cho khớp
> `Business rules - Hướng dẫn điền sổ B2.md`. Làm trên máy nào cũng được.

| # | Bước | Kết quả | Ghi chú |
|---|------|---------|---------|
| C1 | Nhập 1 tờ khai → Admin **huỷ** tờ khai đó (lý do bắt buộc) → nhập lại **đúng số tờ khai đó** → hệ thống **chấp nhận** *(INV-1 / FR-022)* | ⬜ Pass / ⬜ Fail | |
| C2 | Nhập tờ khai trùng số với một tờ khai **đang hiệu lực** → hệ thống **từ chối** và báo rõ | ⬜ Pass / ⬜ Fail | |
| C3 | Nhập biển số chữ thường (ví dụ `12h05769`) → tự in hoa thành `12H05769` *(cột E)* | ⬜ Pass / ⬜ Fail | |
| C4 | Ghi chú nhập lúc tạo xe hiển thị lại được trong ngăn chi tiết *(cột O)* | ⬜ Pass / ⬜ Fail | |
| C5 | Xoá trắng ô Ghi chú rồi lưu → ghi chú bị xoá, không báo lỗi | ⬜ Pass / ⬜ Fail | |

---

## Kiểm tra bổ sung — bảo mật (per Checklist bảo mật `§ 10`)

> S1–S5 chỉ áp dụng cho **máy cổng vào**. Máy cổng ra cài `-ClientOnly` nên không có
> chứng chỉ, không có `C:\ProgramData\B2\keys`, không có mã phục hồi — đó là hành vi đúng.

| # | Kiểm tra | Kết quả |
|---|----------|---------|
| S1 | Cert tự ký đã được cài vào LocalMachine\Root (máy cổng vào) | ⬜ Pass / ⬜ Fail |
| S2 | Firewall rule "B2 Clearance API" chỉ mở TCP 5000 profile Private (máy cổng vào) | ⬜ Pass / ⬜ Fail |
| S3 | File `backup-secrets.dpapi` tồn tại trong `C:\ProgramData\B2\keys\` (máy cổng vào) | ⬜ Pass / ⬜ Fail |
| S4 | Mã phục hồi đã được ghi vào phong bì và lưu tại phòng quản lý | ⬜ Pass / ⬜ Fail |
| S5 | File `B2-Recovery-Code.txt` đã được xóa khỏi Desktop sau khi sao chép | ⬜ Pass / ⬜ Fail |
| S6 | Máy cổng ra **không** tạo dịch vụ và **không** có `C:\ProgramData\B2\keys` | ⬜ Pass / ⬜ Fail |

---

## Nâng cấp phiên bản (chỉ làm nếu đây là bản cài đè)

| # | Kiểm tra | Kết quả |
|---|----------|---------|
| U1 | Chạy lại `.\install.ps1` từ gói mới → dịch vụ dừng, chép đè, khởi động lại, **không** lỗi 1073 | ⬜ Pass / ⬜ Fail |
| U2 | Dữ liệu cũ trong `C:\ProgramData\B2` còn nguyên (tờ khai, mã phục hồi, backup) | ⬜ Pass / ⬜ Fail |

---

## Phát hiện (Findings)

> Ghi lại mọi vấn đề phát sinh trong quá trình UAT. Mỗi mục ghi: bước liên quan,
> mô tả vấn đề, mức độ (CRITICAL / HIGH / MEDIUM / LOW), trạng thái.

| ID | Bước | Mô tả | Mức độ | Trạng thái |
|----|------|-------|--------|------------|
| UAT-001 | | | | |

*(Xóa dòng mẫu nếu không có phát hiện nào)*

---

## Hành động trước khi release

- [ ] Tất cả phát hiện mức CRITICAL và HIGH đã được sửa và smoke test lại.
- [ ] Mã phục hồi đã được lưu an toàn (phong bì dán kín).
- [ ] File `B2-Recovery-Code.txt` trên Desktop đã xóa.
- [ ] File này đã được commit vào repo với đầy đủ thông tin.

---

## Ký duyệt

| Vai trò | Họ tên | Chữ ký / Xác nhận | Ngày |
|---------|--------|-------------------|------|
| Người thực hiện UAT | | | |
| Người duyệt (Supervisor) | | | |

---

*Template v1.1 — 2026-08-01: thêm phần B (máy cổng ra) và phần C (quy tắc nghiệp vụ đã chỉnh);
sửa bước 1 từ `B2ClearanceSetup.exe` (Velopack — đã bỏ ở PR #103) sang `install.ps1`.*
