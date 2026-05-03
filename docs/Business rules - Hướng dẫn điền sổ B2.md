**MÔ TẢ CẤU TRÚC, CÁCH THỨC SỬ DỤNG SỔ QUẢN LÝ**  
**PHƯƠNG TIỆN VẬN TẢI TẠI BỘ PHẬN CỔNG B2**

1. **MÔ TẢ CẤU TRÚC**

Nơi sử dụng file excel tên “Sổ B2”: Gồm có 2 cổng, Cổng vào và Cổng ra, mỗi cổng sử dụng 01 máy tính (không kết nối internet, dùng mạng nội bộ, cấu hình trung bình: Ram 4GB \- 8GB, Win 10 64 BIT, Intel(R) Core(TM) i5-3570 CPU @ 3.40GHz   3.40 GHz), sử dụng Office 2010, nội dung trong file Sổ B2 sẽ bao gồm các trường thông tin như sau:

\- Cột A: Số Thứ tự

\- Cột B: Người làm thủ tục (Sẽ là các tên được admin nhập sẵn làm quy ước là đúng, nếu nhập sai thì hệ thống sẽ không ghi nhận kết quả và có cảnh báo, VD: Nguyễn Văn Nam (tên quy ước là đúng) \=\> Nguyễn Văn Lam \=\> tên sai, báo lỗi và không chấp nhận)

\- Cột C: Số Tờ khai (mặc định luôn là 1 dãy số gồm 12 chữ số, các dãy số luôn không trùng nhau, có nhiều dãy số thì mỗi dãy số sẽ tách thành từng dòng riêng biệt trong cùng 1 ô, dòng có dãy số tờ khai sẽ mặc định là dòng cha, các dòng nối tiếp không có số tờ khai bên dưới sẽ là dòng con, lặp lại như vậy cho đến khi gặp dòng có số tờ khai (dòng cha) mới). Nếu trùng tờ khai khác, hệ thống sẽ cảnh báo bằng cách tô đỏ dãy số. Có thể cho phép copy dữ liệu từ hệ thống nội bộ của đơn vị để dán vào.

\- Cột D: Tên Công ty

\- Cột E: Biển số xe (tự động in hoa chữ cái sau khi nhập liệu, VD: 12h05769 \=\> 12H05769). Có thể cho phép copy dữ liệu từ hệ thống nội bộ của đơn vị để dán vào.

\- Cột F: Số mooc/Số container (tự động in hoa chữ cái sau khi nhập liệu, VD: 12rm05769 \=\> 12RM05769). Có thể cho phép copy dữ liệu từ hệ thống nội bộ của đơn vị để dán vào.

\- Cột G: Tên hàng

\- Cột H: Tên bãi (Sẽ là danh sách các tên bãi được admin nhập sẵn làm quy ước là đúng)

\- Cột I: Giờ nhập sổ (tự động cập nhập giờ thực tại dòng tương ứng sau khi nhập dữ liệu tại cột **F**, thời gian đó sẽ không thay đổi, được lưu và giữ nguyên sau mỗi lần tắt, mở file. Form hiển thị thời gian đó sẽ là: "26/2/2026 13:16").

\- Cột J: Xe đã ra (mặc định ký tự đánh dấu là "x")

\- Cột K: Giờ xe ra (tự động cập nhập giờ thực tại dòng tương ứng sau khi nhập dữ liệu tại cột **K**, thời gian đó sẽ không thay đổi, được lưu và giữ nguyên sau mỗi lần tắt, mở file. Form hiển thị thời gian đó sẽ là: "26/2/2026 13:16").

\- Cột L: Xác nhận qua KVGS (mặc định ký tự đánh dấu là "x")

\- Cột M: Người nhập sổ (Chỉ cho nhập, không cho sửa – xóa)

\- Cột N: Người xác nhận (Chỉ cho nhập, không cho sửa – xóa)

\- Cột O: Ghi chú

\- Cột P: Tìm xe (tại dòng đầu tiên, nhập 3 hoặc 4 số cuối của Biển số xe, thì hệ thống sẽ tự động nhảy đến số có kết quả trùng khớp, đồng thời tự động tô sáng dòng có kết quá; điều kiện để ra kết quả: dòng có kết quả tương ứng phải chưa được tick dấu x tại cột K)

**B. CÁCH THỨC HOẠT ĐỘNG:**

**1\. Cổng vào:** người ngồi máy tính sẽ căn cứ hồ sơ giấy để nhập thủ công thông tin vào sổ B2 từ cột A đến cột H.

**2\. Cổng ra:** sử dụng file Sổ B2 lấy tử Cổng vào bằng chế độ share trên mạng nội bộ. 

\- Đối chiếu xe ra: Khi có xe ra, căn cứ vào biển số thực tế xe đó, gõ 3 hoặc 4 số thuộc biển số xe vào dòng đầu tiên thuộc cột P \=\> điều kiện để ra kết quả: dãy số gõ vào có trong dữ liệu thuộc cột E, đồng thời dòng tương ứng với kết quả tại cột J chưa được tick dấu “x” \=\> nếu ra kết quả, hệ thống sẽ tự nhảy tới dòng có kết quả, đồng thời dòng hệ thống tự động tô sáng dòng đó để dễ nhìn.

\- Xác nhận qua KVGS: 

\+ Xác định đủ điều kiện xác nhận: khi các xe thuộc dòng cha và dòng con đều đã được tick dấu “x” tại cột J (số lượng xe thuộc tờ khai đó đã ra hết), hệ thống sẽ tự động tô màu xanh dòng cha để thể hiện số tờ khai thuộc dòng đó đã đủ điều kiện được xác nhận.

\+ Xác nhận qua KVGS: Sau khi hoàn thành thao tác xác nhận qua KVGS trên hệ thống nội bộ của đơn vị, cán bộ sẽ tick dấu “x” vào cột L tại dòng tương ứng với dòng cha (dòng có số tờ khai), khi đó hệ thống sẽ tự động chuyển từ tô màu xanh trở về trạng thái bình thường, sau đó cán bộ sẽ điền tên mình vào ô "Người xác nhận".

**3 Một số lưu ý, yêu cầu:**

\- Việc truyền dữ liệu tại cổng xe vào sang file tại cổng xe ra phải được thực hiện thường xuyên, liên tục để đảm bảo tính kịp thời, nhất là trong thời gian cao điểm xe ra liên tục

\- Tại máy tính Cổng xe ra, cán bộ chỉ được thao tác nhập liệu đối với các cột "Xe đã ra", "Xác nhận qua KVGS", "Người xác nhận", 'Ghi chú", “Tìm xe”.

\- Tại mỗi dòng đầu tiên của ngày nhập dữ liệu, sẽ có 1 dòng chính để hiển thị Thời gian ngày hôm đó, tên người trực chính, tổng số xe trong ngày (sẽ được thống kê tổng kết vào hồi 23h00 hằng ngày). Dòng chính này sẽ tự động được tạo mỗi khi sang 00h01 hằng ngày.

\- Các xe chưa được tick dấu x ở dòng tương ứng thuộc cột "Đánh dấu xe ra" thì sẽ tự động tô chữ màu đỏ, khi được tick dấu x thì chữ sẽ tự động được tô đen trở lại.

\- Có thể kết xuất báo cáo số lượng theo:

\+ “Người làm thủ tục” (VD: Nguyễn Anh Dũng có bao nhiêu xe ra từ ngày 01/2 – 05/2/2026), 

\+ “Biển số xe”, số lượng xe theo khung giờ xe ra (VD: Từ 13h30 – 17h30 có bao nhiêu xe ra), 

\+ Tên bãi (VD: Bãi QP từ 01/2 – 05/2/2026 có bao nhiêu xe)

\+ Người nhập sổ, Người xác nhận (VD: từ 01/2 – 05/2/2026 Dũng nhập sổ bao nhiêu xe, xác nhận bao nhiêu tờ khai).

\+ Báo cáo số lượng xe phát sinh (tồn) theo ngày, tuần.

\+ Hệ thống có thể nhận diện được những dữ liệu ít có sự biến động, thay đổi để đưa ra dữ liệu gợi ý (số moóc gắn với số xe, tên người làm thủ tục, tên công ty, tên hàng).

\+ Có thể kết xuất dữ liệu ra dạng excel với các trường thông tin chọn lọc để in sổ lưu trữ.