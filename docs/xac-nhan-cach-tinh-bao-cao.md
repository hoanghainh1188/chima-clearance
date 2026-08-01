# Xin xác nhận cách tính các báo cáo

Kính gửi các anh/chị bộ phận cổng B2,

Trong tài liệu hướng dẫn điền sổ B2, phần cuối có liệt kê các loại báo cáo cần kết xuất. Chúng
tôi đã làm xong phần này, nhưng khi rà lại thì thấy có vài chỗ có thể hiểu theo hai cách khác
nhau, và mỗi cách sẽ cho ra con số khác nhau.

Vì đây là số liệu các anh/chị dùng để đối chiếu, chúng tôi không dám tự quyết. Mong các anh/chị
xem giúp và trả lời ngắn gọn từng câu dưới đây. Mỗi câu chúng tôi đều nêu một ví dụ cụ thể để dễ
hình dung.

---

## Câu 1 — Báo cáo theo "Người làm thủ tục"

Tài liệu ghi: *"Nguyễn Anh Dũng có bao nhiêu xe ra từ ngày 01/2 – 05/2/2026"*.

Chỗ chúng tôi chưa chắc: **đếm xe nào, và tính theo ngày nào.**

**Ví dụ:** anh Dũng làm thủ tục cho 10 xe, tất cả đều nhập sổ ngày 05/02.
Đến hết ngày 05/02 mới có 6 xe ra khỏi cổng, 4 xe còn nằm trong bãi, sang ngày 06/02 mới ra.

Khi hỏi *"anh Dũng có bao nhiêu xe ra từ 01/2 đến 05/2"*, các anh/chị mong con số là:

- **10** — tức là đếm tất cả xe thuộc các tờ khai anh Dũng làm, nhập sổ trong khoảng ngày đó; hay
- **6** — tức là chỉ đếm những xe **thực sự đã ra khỏi cổng** trong khoảng ngày đó.

Hiện phần mềm đang cho ra **10**. Nếu ý các anh/chị là **6** thì chúng tôi sẽ sửa lại.

*(Câu hỏi tương tự áp dụng luôn cho báo cáo theo **Tên bãi** — ví dụ "Bãi QP từ 01/2 – 05/2 có bao
nhiêu xe". Đếm xe vào bãi, hay đếm xe đã ra?)*

---

## Câu 2 — Báo cáo số lượng xe theo khung giờ xe ra

Tài liệu ghi: *"Từ 13h30 – 17h30 có bao nhiêu xe ra"*.

Có hai chỗ cần các anh/chị xác nhận.

**a) Khung giờ tự chọn hay khung giờ cố định?**

Hiện phần mềm chia sẵn cả ngày thành các khung đều nhau (ví dụ mỗi khung 1 tiếng: 13h–14h,
14h–15h…) rồi đếm từng khung. Muốn biết con số của 13h30–17h30 thì phải tự cộng, mà mốc 13h30 lại
nằm giữa khung 13h–14h nên cộng không ra đúng.

Các anh/chị có cần gõ thẳng "từ 13h30 đến 17h30" rồi phần mềm trả về một con số duy nhất không?

**b) Xe vào hôm trước, ra hôm sau thì tính vào ngày nào?**

**Ví dụ:** xe 29C-12345 nhập sổ lúc 22h00 ngày 01/02, đến 08h00 sáng 02/02 mới ra khỏi cổng.

Khi xem báo cáo giờ xe ra của **ngày 02/02**, xe này có nên xuất hiện ở khung 08h–09h không?

Hiện phần mềm **không** tính xe này vào ngày 02/02, mà tính vào ngày 01/02 (là ngày nhập sổ), ở
khung giờ 08h–09h. Chúng tôi nghĩ như vậy chưa đúng, nhưng muốn hỏi lại cho chắc.

---

## Câu 3 — Báo cáo theo "Biển số xe"

Tài liệu có liệt kê "Biển số xe" trong danh sách báo cáo, nhưng không nêu ví dụ nên chúng tôi
chưa rõ các anh/chị cần gì.

Hiện phần mềm có chức năng **tìm** một xe theo biển số, nhưng chưa có báo cáo thống kê theo biển
số. Ý các anh/chị là:

- Trong khoảng ngày, mỗi biển số ra vào **bao nhiêu lượt**, xếp từ nhiều đến ít; hay
- Tra cứu lịch sử của **một** xe cụ thể: xe đó vào ra những ngày nào, giờ nào; hay
- Ý khác?

---

## Câu 4 — Báo cáo xe tồn theo ngày, tuần

Tài liệu ghi: *"Báo cáo số lượng xe phát sinh (tồn) theo ngày, tuần"*.

Hiện phần mềm trả lời được câu hỏi *"ngay lúc này còn bao nhiêu xe chưa ra"* — kèm danh sách biển
số và đã nằm trong bãi bao nhiêu tiếng.

Nhưng "theo ngày, theo tuần" thì có thể các anh/chị muốn một bảng như thế này:

| Ngày | Số xe còn tồn cuối ngày |
|------|-------------------------|
| 01/02 | 12 |
| 02/02 | 8 |
| 03/02 | 15 |

Tức là nhìn lại được cả tuần, mỗi ngày tồn bao nhiêu, chứ không chỉ con số của hôm nay. Có đúng ý
các anh/chị không ạ?

---

## Câu 5 — Báo cáo theo "Người nhập sổ" và "Người xác nhận"

Tài liệu ghi: *"từ 01/2 – 05/2/2026 Dũng nhập sổ bao nhiêu xe, xác nhận bao nhiêu tờ khai"*.

Chỗ này chúng tôi hiểu là: với mỗi cán bộ, đưa ra **hai con số** — số **xe** người đó nhập vào sổ,
và số **tờ khai** người đó xác nhận qua KVGS. Phần mềm đang làm đúng như vậy.

Chỉ xin xác nhận lại giúp là cách hiểu này đúng.

---

## Vì sao chúng tôi hỏi kỹ như vậy

Những con số này nếu tính sai thì phần mềm vẫn chạy bình thường, vẫn hiện bảng biểu đầy đủ, không
báo lỗi gì. Chỉ đến khi các anh/chị đối chiếu với sổ giấy mới phát hiện lệch, mà lúc đó rất khó
lần ra sai ở đâu — vì cách nào cũng cho ra một con số trông hợp lý.

Nên chúng tôi muốn hỏi cho chắc ngay từ bây giờ, trước khi đưa vào dùng thật.

Rất mong nhận được phản hồi của các anh/chị. Nếu có chỗ nào trong câu hỏi chưa rõ, xin cứ trao đổi
lại để chúng tôi giải thích thêm.

Trân trọng cảm ơn.

---

*Tài liệu này dùng để trao đổi với đơn vị. Câu trả lời sẽ được ghi vào phần Clarifications của
`specs/001-b2-clearance/spec.md` trước khi sửa mã nguồn.*
