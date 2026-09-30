# Đánh giá mức phù hợp với yêu cầu ban đầu — Lớp 1 (truy vết trên tài liệu và mã nguồn)

> **Ngày**: 2026-09-30 · **Phiên bản đối chiếu**: `main` @ `e13fa76` (sau F-01, trước khi phát hành v1.3.0)
> **Căn cứ** (thẩm quyền cao nhất): tài liệu **của đơn vị** — [`Định nghĩa yêu cầu.md`](Định%20nghĩa%20yêu%20cầu.md),
> [`Business rules - Hướng dẫn điền sổ B2.md`](Business%20rules%20-%20Hướng%20dẫn%20điền%20sổ%20B2.md), `Mẫu sổ B2.xlsx`.
> Spec (`specs/`) chỉ dùng để tìm chỗ hiện thực — **không** được coi là bằng chứng đáp ứng.

## 1. Tóm tắt

| Kết luận | Số yêu cầu | Ý nghĩa |
| --- | ---: | --- |
| ✅ Đáp ứng | 32 | Có mã nguồn + test tương ứng, đúng ý tài liệu gốc |
| 🟡 Một phần | 20 | Làm được phần lõi nhưng thiếu một phần, hoặc chỉ đúng trong file Excel xuất ra chứ không có trên màn hình |
| 🔴 Thiếu | 3 | Chưa có (báo cáo theo biển số, báo cáo tồn theo ngày/tuần, xuất Excel chọn cột) |
| 🟠 Lệch | 3 | Làm khác tài liệu gốc (chặn cứng trùng tờ khai, Excel khác mẫu, sao lưu liên tục tắt mặc định) |
| ⚪ Chưa kiểm chứng | 8 | Chưa từng đo/thử thật: hiệu năng máy yếu, 15 phút học, < 2 phút/tác vụ, Office 2010, … |
| **Tổng** | **66** | (+ `DN-21` trùng với `EX-1`) |

**Kết luận chung.** Phần **lõi nghiệp vụ và hạ tầng** đã đúng hướng: nhập ở cổng vào, đối chiếu ra ở cổng ra, KVGS,
master data, phân quyền, chạy LAN/offline, một máy vừa server vừa client, CSDL miễn phí, dòng tổng kết ngày. Nhưng
hệ thống **chưa sẵn sàng để khẳng định "phù hợp"** vì ba nhóm lý do:

1. **Báo cáo** — 5 loại báo cáo đơn vị yêu cầu đều mới "một phần" hoặc "thiếu", và **cách tính vẫn là giả định của đội
   phát triển**: thư [`xac-nhan-cach-tinh-bao-cao.md`](xac-nhan-cach-tinh-bao-cao.md) chưa có câu trả lời nào.
2. **"Sổ" trên màn hình khác sổ đơn vị quen dùng** — nhiều hành vi của sổ Excel (dòng con, chữ đỏ xe chưa ra, tô xanh
   dòng cha, giờ xe ra, người nhập/người xác nhận) **chỉ có trong file Excel xuất ra**, không thấy trong ứng dụng.
3. **Các tiêu chí phi chức năng chưa được đo** — máy yếu (i5-3570, 4 GB), học trong 15 phút, tác vụ < 2 phút, Office
   2010 — đều chưa có số đo nào; test hiệu năng đang `Skip`.

Ngoài ra có **hai lỗi vận hành nghiêm trọng** phát hiện được khi đối chiếu (mục 2, D1–D2), nên sửa trước UAT.

## 2. Phát hiện ưu tiên

Mức: **Cao** = ảnh hưởng dữ liệu/vận hành hoặc làm sai mục tiêu chính; **TB** = sai lệch người dùng sẽ gặp;
**Thấp** = nên sửa. ✔ = đã tự kiểm lại trên mã nguồn khi tổng hợp.

### Cao

| # | Phát hiện | Yêu cầu | Bằng chứng |
| --- | --- | --- | --- |
| D1 ✔ | **Sao lưu liên tục (Litestream) tắt mặc định.** Chỉ chạy khi cấu hình `B2:Litestream:Enabled` + replica — installer và `appsettings.json` không bật, tài liệu không hướng dẫn. Nhưng runbook khẳng định "sao lưu liên tục mỗi 60 giây". Thực tế **không có sao lưu tự động nào**. | DN-22, FR-012 | `InfrastructureServiceCollectionExtensions.cs:105-126`; `runbook.md:106,149` |
| D2 ✔ | **Khôi phục phụ thuộc hoàn toàn phong bì mã phục hồi.** "Mật khẩu cài đặt" được sinh và niêm phong DPAPI, không ai được xem; runbook lại hướng dẫn "nhập mật khẩu cài đặt hoặc mã phục hồi". Mất phong bì = không giải mã được bản sao lưu. | DN-06, DN-22 | `RecoveryCodeSetup.cs:38-60`; `runbook.md:123` |
| R1 | **Cách tính mọi báo cáo chưa được đơn vị xác nhận** (5 câu trong thư). Ví dụ: báo cáo theo người làm thủ tục hiện đếm **mọi xe nhập** trong khoảng ngày, không phải **xe đã ra** như ví dụ của đơn vị. | RP-1…RP-5 | `ReportsRepository.cs:15-149`; `xac-nhan-cach-tinh-bao-cao.md` |
| R2 | **Thiếu báo cáo**: theo biển số xe; số xe tồn theo ngày/tuần (hiện chỉ có danh sách xe *đang* tồn, `asOf` không lọc theo thời điểm); khung giờ tự chọn như 13h30–17h30 (hiện cố định từ phút :00, lọc theo ngày nhập chứ không theo ngày ra). | RP-2, RP-5 | `ReportsRepository.cs:51-64,127-149,166-173` |
| X1 | **Excel xuất ra khác mẫu đang dùng**: dòng con để trống cột B/D/G/H (mẫu lặp lại trên mọi dòng — đơn vị lọc/đếm theo các cột này); STT đánh liên tục qua nhiều ngày (mẫu đánh lại từ 1 mỗi ngày); ô P1 còn giá trị "1358" từ mẫu; định dạng tô đỏ số tờ khai trùng ở cột C bị mất. | EX-1, BR-A | `ClosedXmlSoB2Exporter.cs:52-61,118-138` ✔ (B/D/G/H, STT) |
| U1 | **Sổ trên màn hình thiếu các cột/hành vi của sổ Excel**: không hiện giờ xe ra, người nhập sổ, người xác nhận; giờ nhập chỉ `HH:mm`; mỗi tờ khai một dòng (xe con trong ngăn chi tiết); không chữ đỏ theo xe, không tô xanh cả dòng cha; dòng ngày thiếu tổng xe. | BR-I/K/M/N, BR-C4, BR-N3, BR-N4, BR-FLOW3 | `LedgerRowViewModel.cs:79-96`; `LedgerPage.xaml`; `LedgerViewModel.cs:655` |
| P1 ✔ | **Chưa đo hiệu năng trên máy cấu hình thấp** (i5-3570, 4–8 GB, Win10) — cả 3 test hiệu năng đang `Skip`, không có kịch bản NBomber, UAT chưa chạy. | DN-02, DN-23 | `tests/B2.Performance.Tests/*.cs`; `SearchEndpointTests.cs:176` |
| P2 | **Chưa đo tính dễ học (15 phút) và hiệu quả (< 2 phút/tác vụ)** — không có test người dùng, UAT không có hạng mục bấm giờ. | DN-24, DN-25 | `uat-001-b2-clearance.md` |

### Trung bình

| # | Phát hiện | Yêu cầu | Bằng chứng |
| --- | --- | --- | --- |
| C2 | **Trùng số tờ khai bị chặn cứng** (409) trong khi tài liệu chỉ yêu cầu **cảnh báo tô đỏ**. Quyết định "chặn" do đội tự đưa ra (Clarif. 2026-08-01), đơn vị chưa xác nhận. | BR-C2 | `CreateDeclarationCommandHandler.cs:60-69` |
| F1 ✔ | **Thông báo lỗi chung chung**: trùng số tờ khai, "xe đã được người khác đánh dấu ra", mất mạng… đều hiện cùng một câu ("Không tạo được…/Không đánh dấu được (kiểm tra kết nối hoặc quyền)"). Không tô đỏ ô trùng, không nói số nào trùng. | DN-28, BR-C2, FR-024 | `EntryViewModel.cs:395-402`; `ExitViewModel.cs:164-172` |
| N2 | **Máy cổng ra không bị giới hạn theo cột** như tài liệu (chỉ J, L, N, O, P): phân quyền theo **vai trò**, không theo **máy**. Operator ở cổng ra vẫn nhập/sửa tờ khai; Supervisor lại không đánh dấu xe ra được. | BR-N2 | `B2ApiHost.cs:104-109`; `MainShellPage.cs:299-316` |
| S1 ✔ | **Tìm xe theo biển số không loại tờ khai đã hủy** → xe của tờ khai hủy vẫn hiện, bấm "đánh dấu ra" thì lỗi 409. | BR-P, FR-023 | `VehicleSuffixSearchRepository.cs:44-50` |
| E2 | **Xuất Excel chỉ cho Supervisor/Admin** — Operator (người đang dùng Excel hằng ngày) không xuất được. Cần đơn vị xác nhận. | DN-16, DN-21 | `ReportsEndpoints.cs:12` ✔ |
| EX2 | **Không chọn được cột khi xuất để in sổ lưu trữ**; trang in A4 dọc, 16 cột, không co vừa trang. | EX-2 | `ExportViewModel.cs` |
| A11 | **WCAG 2.1 AA mới kiểm tĩnh** trên token XAML ("AxeRunner" không phải công cụ axe); còn ~32 chỗ cỡ chữ 12–14 pt; chưa thử bàn phím/trình đọc màn hình trên ứng dụng thật. | DN-27 | `tests/B2.Desktop.Tests/AccessibilityTests.cs` |
| I1 | **Cài đặt chưa thật sự "không cần IT"**: cần máy có internet để tải WebView2, PowerShell Admin, ExecutionPolicy, IP tĩnh, profile mạng Private. | DN-06 | `huong-dan-cai-dat.md` |
| L1 | **Nền tảng sắp hết hỗ trợ**: .NET MAUI 8 đã ngừng hỗ trợ; .NET 8 LTS hết hạn **10/11/2026**. | DN-10, DN-12 | `global.json` |
| SG | **Gợi ý thiếu "Người làm thủ tục"** (đang là danh sách chọn); nhận gợi ý bằng nút, không bằng Tab. | SG-1, DN-26 | `EntryPage.xaml:47,90-106` |

### Thấp

- **BR-E1** — biển số bị ép theo khuôn `^[0-9]{2}[A-Z]{1,2}[0-9]{4,5}$` (tài liệu không yêu cầu) → biển nước ngoài/khác khuôn bị từ chối (`PlateNumber.cs:7`).
- **BR-C5** — dán nhiều số tờ khai cách nhau bằng dấu cách/tab bị báo lỗi (chỉ tách theo xuống dòng, `,`, `;`).
- **BR-K** — Admin "hoàn tác xe ra" xóa và đóng dấu lại giờ xe ra, trái "không thay đổi" (có ghi audit).
- **RP-4** — số xe nhập tính cho người nhập **tờ khai** (không phải người nhập từng xe); số tờ khai xác nhận tính theo **ngày nhập**, không theo ngày xác nhận.
- **DN-03** — Ledger sắp xếp/lọc `since` trong bộ nhớ, `pageSize` không có trần — rủi ro trên máy 4 GB khi khoảng ngày lớn.
- **EXTRA-3** — lưu trữ năm **xóa** dữ liệu > 3 năm khỏi CSDL (đơn vị không yêu cầu xóa) và chỉ xuất `.xlsx` (spec hứa thêm PDF).

## 3. Câu cần hỏi lại đơn vị

Gửi kèm thư [`xac-nhan-cach-tinh-bao-cao.md`](xac-nhan-cach-tinh-bao-cao.md) (5 câu về báo cáo, chưa trả lời):

1. **Trùng số tờ khai**: chỉ **cảnh báo** (tô đỏ, vẫn cho lưu) hay **không cho lưu**? (BR-C2)
2. **Cột K "Giờ xe ra"** và **cột P "Tìm xe"**: tài liệu ghi "sau khi nhập **cột K**" / "chưa tick **cột K**" — có phải ý là **cột J (Xe đã ra)**? (BR-K, BR-P)
3. **Máy cổng ra**: có cần khóa theo **máy** (chỉ sửa J, L, N, O, P) hay theo **người** là đủ? Cán bộ giám sát có được đánh dấu xe ra không? (BR-N2)
4. **Ai được xuất Excel**: cả cán bộ nhập liệu hay chỉ giám sát/quản trị? (DN-16)
5. **Sổ trên màn hình**: có cần hiển thị như sổ Excel (mỗi xe một dòng, chữ đỏ/tô xanh, đủ cột I–N), hay xem trên màn hình theo tờ khai và in/xuất Excel là đủ? (U1)
6. **Excel xuất ra**: dòng xe con có cần lặp lại Người làm thủ tục/Công ty/Hàng/Bãi như sổ hiện tại? STT đánh lại mỗi ngày? Cần chọn cột khi in sổ lưu trữ không? (X1, EX-2)
7. **Lưu trữ**: dữ liệu cũ hơn 3 năm có được **xóa** khỏi phần mềm sau khi xuất file lưu trữ không? (EXTRA-3)
8. **Biển số khác khuôn** (xe nước ngoài, biển tạm): có gặp không? (BR-E1)

## 4. Ma trận truy vết

Bảng rút gọn; đường dẫn mã nguồn/test cho các điểm lệch nằm ở mục 2. Các dòng ✅ về chức năng đều có test tự động trong `tests/` (nhãn `Feature`/`Requirement`); các dòng ✅ về kiến trúc/triển khai (DN-04, DN-10, DN-11, DN-13) dựa trên cấu hình, build CI và tài liệu triển khai, không có test riêng.

### 4.1. Business rules — cột A–P và quy trình hai cổng

| ID | Yêu cầu | Kết luận | Ghi chú chính |
| --- | --- | --- | --- |
| BR-A | Cột A: STT | 🟡 | Excel đánh STT liên tục qua nhiều ngày (mẫu đánh lại mỗi ngày); trên sổ màn hình STT chỉ là thứ tự hiển thị |
| BR-B | Người làm thủ tục từ danh sách admin; sai thì không nhận | ✅ | Danh sách chọn — không gõ sai được; API trả 400 nếu không tồn tại |
| BR-C1 | Số tờ khai 12 chữ số | ✅ | Domain + validator + test |
| BR-C2 | Không trùng; trùng thì **cảnh báo tô đỏ** | 🟠 | Chặn cứng 409; UI không tô đỏ, không nói số trùng (C2, F1) |
| BR-C3 | Nhiều số trong một ô | ✅ | Theo câu trả lời của đơn vị (2026-08-01); 10 test |
| BR-C4 | Dòng cha / dòng con | 🟡 | Dữ liệu + Excel đúng; sổ màn hình không có dòng con (U1) |
| BR-C5 | Dán từ hệ thống nội bộ | ⚪ | Không tách theo dấu cách/tab; chưa rõ định dạng nguồn |
| BR-D | Tên công ty | ✅ | |
| BR-E1 | Biển số tự in hoa | ✅ | In hoa khi lưu (không khi gõ); thêm khuôn biển số (Thấp) |
| BR-E2 | Dán biển số | ✅ | Bỏ ký tự phân cách — theo câu trả lời của đơn vị |
| BR-F1/F2 | Số mooc/container in hoa, dán được | ✅ | |
| BR-G | Tên hàng | ✅ | |
| BR-H | Tên bãi từ danh sách admin | ✅ | Thiếu test nhánh bãi không tồn tại |
| BR-I | Giờ nhập tự đóng dấu, không đổi, dạng "26/2/2026 13:16" | 🟡 | Server + Excel đúng; màn hình chỉ `HH:mm` / chuỗi chưa định dạng |
| BR-J | Xe đã ra "x" | ✅ | |
| BR-K | Giờ xe ra tự đóng dấu, không đổi | 🟡 | Không hiện trên màn hình; hoàn tác xe ra đổi giờ |
| BR-L | Xác nhận KVGS "x" | ✅ | Mở cho mọi vai trò theo tài liệu gốc |
| BR-M | Người nhập sổ — chỉ nhập, không sửa/xóa | ✅ | Tự lấy người đăng nhập; không hiện trên màn hình |
| BR-N | Người xác nhận — chỉ nhập, không sửa/xóa | ✅ | Tự lấy người xác nhận; không hiện trên màn hình |
| BR-O | Ghi chú | ✅ | Sửa có ghi audit |
| BR-P | Tìm 3–4 số cuối, nhảy tới, tô sáng, chỉ xe chưa ra | 🟡 | Trang tìm riêng, không nhảy trong sổ; chưa loại tờ khai hủy (S1) |
| BR-FLOW1 | Cổng vào nhập A–H | ✅ | |
| BR-FLOW2 | Cổng ra dùng dữ liệu cổng vào qua LAN | ✅ | Máy chủ chung thay file share |
| BR-FLOW3 | Ra hết xe → dòng cha tô xanh | 🟡 | Trạng thái + Excel đúng; màn hình chỉ biểu tượng/chữ xanh |
| BR-FLOW4 | Tick L → hết xanh; ghi tên người xác nhận | 🟡 | Màn hình chuyển 🔵 (theo wireframe), không "về bình thường" |
| BR-N1 | Đồng bộ liên tục cổng vào → ra | ✅ | SignalR ≤ 1 s + polling 3 s; có test |
| BR-N2 | Máy cổng ra chỉ sửa J, L, N, O, P | 🟡 | Theo vai trò, không theo máy (N2) |
| BR-N3 | Dòng đầu ngày: ngày, trực chính, tổng xe (23:00), tạo 00:01 | 🟡 | Backend + Excel đủ; màn hình thiếu tổng xe |
| BR-N4 | Xe chưa ra chữ đỏ, ra rồi chữ đen | 🟡 | Chỉ đúng trong Excel |

### 4.2. Báo cáo, gợi ý, xuất Excel

| ID | Yêu cầu | Kết luận | Ghi chú chính |
| --- | --- | --- | --- |
| RP-1 | Số xe theo người làm thủ tục, khoảng ngày | 🟡 | Đếm mọi xe nhập (không chỉ xe ra); chờ đơn vị trả lời câu 1 |
| RP-2 | Theo biển số; số xe ra theo khung giờ | 🔴 | Không có báo cáo theo biển số; khung giờ cố định, lọc theo ngày nhập |
| RP-3 | Số xe theo bãi, khoảng ngày | 🟡 | Cùng cách đếm RP-1 |
| RP-4 | Người nhập sổ / người xác nhận | 🟡 | Gán theo người nhập tờ khai; xác nhận theo ngày nhập |
| RP-5 | Xe tồn theo ngày, tuần | 🔴 | Chỉ danh sách xe đang tồn; không theo ngày/tuần |
| SG-1 | Gợi ý mooc theo biển, người làm thủ tục, công ty, hàng | 🟡 | Có mooc/công ty/hàng; thiếu người làm thủ tục |
| EX-1 | Xuất Excel như hiện tại | 🟠 | Khác mẫu ở dòng con, STT, ô P1, tô đỏ trùng (X1) |
| EX-2 | Xuất với trường chọn lọc để in sổ | 🔴 | Không chọn cột; khổ in chưa phù hợp |
| EX-3 | Mở được bằng Office 2010 | ⚪ | Kiểm tĩnh không thấy tính năng vượt 2010; cần thử trên Office 2010 thật |

### 4.3. Định nghĩa yêu cầu

| ID | Yêu cầu | Kết luận | Ghi chú chính |
| --- | --- | --- | --- |
| DN-01 | Hai cổng thao tác cùng lúc không xung đột | 🟡 | Xử lý tranh chấp có test; chưa thử tải nhiều máy |
| DN-02 | Không giật lag trên máy yếu | ⚪ | Chưa đo (P1) |
| DN-03 | Khối lượng lớn không phải xóa/backup mới làm tiếp | 🟡 | Phân trang, lưu trữ năm; chưa thử tải |
| DN-04 | Chỉ LAN | ✅ | Chuẩn bị USB cần một máy có internet (WebView2) |
| DN-05 | Office 2010 | ⚪ | Xem EX-3 |
| DN-06 | Không có đội IT | 🟡 | Tài liệu tiếng Việt đủ; cài đặt vẫn cần thao tác kỹ thuật (I1); D1, D2 |
| DN-07 | Người dùng quen desktop, tin học trung bình | ⚪ | Đúng hình thức; chưa thử với người dùng |
| DN-08 | Mục tiêu: cập nhật, tra cứu, báo cáo, Excel | ✅ | Về chức năng; mức "đơn giản" xem DN-24/25 |
| DN-09 | Hai làn, cổng ra tham chiếu cổng vào | ✅ | |
| DN-10 | .NET Core + MAUI | ✅ | Sắp hết hỗ trợ (L1) |
| DN-11 | Máy cổng vào vừa server vừa client | ✅ | Có thêm mô hình một máy |
| DN-12 | Kiến trúc hiện đại, REST/đọc file | 🟡 | Có REST; chưa có nhập/đọc file |
| DN-13 | CSDL dễ cài, miễn phí | ✅ | SQLite nhúng |
| DN-14 | Quản lý người dùng, 3 nhóm | ✅ | |
| DN-15 | Đăng nhập/đăng xuất | ✅ | Tự đăng xuất khi rảnh chưa có test |
| DN-16 | Phân quyền theo chức năng | ✅ | Hỏi lại quyền xuất Excel (E2) |
| DN-17 | Master data | ✅ | |
| DN-18 | Xem, tìm danh sách xe/tờ khai | ✅ | Không tìm theo công ty/hàng |
| DN-19 | Nhập xe ở cổng vào | ✅ | |
| DN-20 | Xác nhận xe ra ở cổng ra | ✅ | |
| DN-21 | Xuất Excel như hiện tại | → EX-1 | |
| DN-22 | Sao lưu, khôi phục | 🟠 | Sao lưu tự động tắt (D1); khôi phục chỉ bằng phong bì (D2) |
| DN-23 | Chạy trên máy cấu hình thấp | ⚪ | Chưa đo (P1) |
| DN-24 | Người mới thành thạo trong 15 phút | ⚪ | Chưa đo (P2) |
| DN-25 | Ít thao tác, < 2 phút/tác vụ | ⚪ | Chưa đo (P2) |
| DN-26 | Ngôn ngữ phổ thông, gợi ý, tự điền | ✅ | Về chức năng |
| DN-27 | WCAG 2.1 AA | 🟡 | Mới kiểm tĩnh (A11) |
| DN-28 | Phản hồi rõ sau mỗi thao tác | 🟡 | Thành công rõ; lỗi chung chung (F1) |

## 5. Phần làm thêm ngoài yêu cầu

Không sai, nhưng cần cân nhắc vì đơn vị **không có IT** (DN-06):

| # | Hạng mục | Gánh nặng vận hành |
| --- | --- | --- |
| EXTRA-1 | Mã hóa bản sao lưu (AES-256-GCM, 2 lớp khóa, mã phục hồi niêm phong) | **Cao** — mất phong bì là mất khả năng khôi phục (D2) |
| EXTRA-2 | Litestream sao lưu liên tục | Trung bình — cấu hình bằng file, không có giao diện, đang tắt (D1) |
| EXTRA-3 | Lưu trữ năm và **xóa** dữ liệu > 3 năm | Trung bình — dữ liệu cũ không tra được trong ứng dụng |
| EXTRA-4/5 | Trang sức khỏe hệ thống, nhật ký audit | Thấp |
| EXTRA-6 | Khóa tài khoản, mật khẩu phức tạp, JWT | Trung bình — dễ bị khóa/quên; mất hết Admin phải gọi đội phát triển |
| EXTRA-7 | Hủy tờ khai, hoàn tác xe ra | Thấp — hữu ích, cần đơn vị xác nhận quy trình |
| EXTRA-8 | Kết nối 3 trạng thái, chống gửi trùng | Không — thuần kỹ thuật |

## 6. Việc tiếp theo

1. **Sửa trước UAT** (lỗi rõ ràng, không cần hỏi đơn vị): D1 (bật/cấu hình sao lưu tự động hoặc sửa runbook cho đúng),
   D2 (hiện "mật khẩu cài đặt" một lần khi cài hoặc bỏ khỏi hướng dẫn), F1 (thông báo lỗi cụ thể), S1 (loại tờ khai hủy
   khi tìm xe), X1 phần không gây tranh cãi (ô P1, tô đỏ trùng ở cột C).
2. **Gửi đơn vị** thư báo cáo (đã có) + 8 câu ở mục 3; sửa R1/R2/X1/U1/C2/N2 theo câu trả lời.
3. **Lớp 2 — dữ liệu thật**: nhập lại một tuần sổ B2 thật, xuất Excel và so từng ô với sổ gốc; chạy báo cáo so với số
   đơn vị tự tính; mở file trên **Office 2010**.
4. **Lớp 3 — môi trường**: đo trên máy tương đương i5-3570/4 GB (khởi động, RAM, thời gian tìm/nhập, xuất 30 ngày);
   bật lại các test hiệu năng đang `Skip`.
5. **Lớp 4 — người dùng**: UAT `T-S6-12` có bấm giờ (15 phút học, < 2 phút/tác vụ) và chạy song song với Excel 1–2 tuần.
6. **Nền tảng**: lên kế hoạch nâng .NET 8 → .NET 10 LTS (L1) — cần ADR theo Constitution II.
