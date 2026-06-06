# UAT Sign-off — B2 Clearance v1.0 (001-b2-clearance)

> **Thực hiện trên phần cứng thực tế** (không phải VM ảo hóa) theo yêu cầu T-S6-12.  
> Điền vào tất cả các ô trống, ký tên, và lưu file này vào repo trước khi release.

---

## Thông tin môi trường thử nghiệm

| Trường | Giá trị |
|--------|---------|
| Ngày thực hiện | |
| Người thực hiện | |
| Máy tính (model / CPU / RAM) | |
| Hệ điều hành | |
| Phiên bản cài đặt | v |
| Hash installer (SHA-256) | |

---

## Checklist smoke test (10 bước — per `04-implementation-plan.md § 8`)

| # | Bước | Kết quả | Ghi chú |
|---|------|---------|---------|
| 1 | Chạy `B2ClearanceSetup.exe`, chấp nhận mặc định → service được tạo và khởi động | ⬜ Pass / ⬜ Fail | |
| 2 | Mở Desktop, đăng nhập Admin mặc định → hệ thống ép đổi mật khẩu | ⬜ Pass / ⬜ Fail | |
| 3 | Thêm 1 Người xử lý + 1 Bãi | ⬜ Pass / ⬜ Fail | |
| 4 | Tạo 1 tờ khai với 2 phương tiện | ⬜ Pass / ⬜ Fail | |
| 5 | Tìm kiếm bằng 4 chữ số cuối biển số → tìm thấy cả 2 | ⬜ Pass / ⬜ Fail | |
| 6 | Xác nhận ra cổng cả 2 xe → tờ khai chuyển xanh | ⬜ Pass / ⬜ Fail | |
| 7 | Xác nhận KVGS → tờ khai xanh biến mất khỏi KVGS | ⬜ Pass / ⬜ Fail | |
| 8 | Xuất Excel → file mở được trong Excel / LibreOffice không lỗi | ⬜ Pass / ⬜ Fail | |
| 9 | "Tạo bản sao lưu ngay" → file `.bak.zip` xuất hiện trong thư mục backup | ⬜ Pass / ⬜ Fail | |
| 10 | Dừng service → Desktop hiển thị rõ banner đỏ "Mất kết nối" và tự thử lại | ⬜ Pass / ⬜ Fail | |

**Kết quả tổng hợp**: ⬜ PASS (tất cả 10/10) &nbsp;&nbsp; ⬜ FAIL (xem mục Phát hiện bên dưới)

---

## Kiểm tra bổ sung (per Checklist bảo mật `§ 10`)

| # | Kiểm tra | Kết quả |
|---|----------|---------|
| S1 | Cert tự ký đã được cài vào LocalMachine\Root | ⬜ Pass / ⬜ Fail |
| S2 | Firewall rule "B2 Clearance API" chỉ mở TCP 5000 profile Private | ⬜ Pass / ⬜ Fail |
| S3 | File `backup-secrets.dpapi` tồn tại trong `C:\ProgramData\B2\keys\` | ⬜ Pass / ⬜ Fail |
| S4 | Mã phục hồi đã được ghi vào phong bì và lưu tại phòng quản lý | ⬜ Pass / ⬜ Fail |
| S5 | File `B2-Recovery-Code.txt` đã được xóa khỏi Desktop sau khi sao chép | ⬜ Pass / ⬜ Fail |

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

*Template phiên bản: v1.0 — Sprint S6 — 2026-06-06*
