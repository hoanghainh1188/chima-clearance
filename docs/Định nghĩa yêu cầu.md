# Định nghĩa yêu cầu: Phát triển phần mềm của bộ phận hải quan Chi Ma

## Thông tin client

Đơn vị yêu cầu là bộ phận kiểm soát thông quan của hải quan cửa khẩu Chi Ma.

## Bối cảnh & vấn đề

### Bối cảnh

Cửa khẩu hải quan Chi Ma đang quản lý việc nhập xuất thông tin thông quan, kiểm tra hàng hoá thông quan, … bằng excel sheet (ở đây chúng ta gọi đó là Sổ quản lý phương tiện tại cổng B2).  
File excel này cùng được truy xuất tại một thời điểm bởi máy tính từ nơi nhập, và từ nơi xuất; ngoài ra, cũng có cả máy khác truy cập vào (không thường xuyên).  
Máy tính ở cổng vào sẽ đóng vai trò là máy chính; máy ở cổng ra sẽ căn cứ vào các dữ liệu nhập ở cổng vào để đối chiếu thực tế xe ra, rồi tick vào file.

### Vấn đề

Trên thực tế sử dụng, gặp khá nhiều khó khăn như sau:

- Bên nhập và xuất cùng thao tác trên một file excel, nên dẫn đến conflict lẫn nhau.  
- Cấu hình của máy tính khá yếu, và dùng Excel 2010, nên hay có hiện tượng giật lag, treo.  
- Do khối lượng thông tin trong ngày lớn, nên thường xuyên phải backup, hoặc xoá dòng thì mới có thể edit tiếp được.

Lưu ý: Về cách điền file thì có hướng dẫn và template kèm theo, xem mục Tài liệu tham khảo.

### Các ràng buộc

Có những ràng buộc sau về nghiệp vụ và vận hành thực tế:

- Chỉ sử dụng mạng nội bộ (LAN): Giữa các máy ở nhập và xuất, hoặc các máy giám sát thì đều không dùng mạng internet, mà chỉ dùng mạng nội bộ để truy cập, sửa đổi thông tin.  
- Cấu hình của các máy đều thấp so với hiện nay. Cấu hình một máy thông thường sẽ như dưới đây:  
  - CPU: Intel(R) Core(TM) i5-3570 CPU @ 3.40GHz   3.40 GHz)  
  - RAM: 4GB \~ 8GB  
  - OS: Windows 10 64-bit  
  - Application: Microsoft Office 2010  
- Không có đội vận hành IT chuyên biệt  
- Người dùng truyền thống, quen thuộc với desktop application, trình độ tin học ở mức trung bình.

## Mục tiêu

Mong muốn xây dựng phần mềm đơn giản để có thể đơn giản hoá việc cập nhật thông tin thông quan hàng hoá; ngoài ra, cho phép có thể quản lý và truy xuất thông tin, xuất báo cáo, xuất ra file Excel (như nghiệp vụ hiện tại).

## Business rules

Phần mềm được phát triển cần phải tuân theo các nguyên tắc nghiệp vụ như mô tả ở tài liệu “Business rules \- Hướng dẫn điền sổ B2”.  
Chúng ta có 2 làn xe trên thực tế:

- Một làn dành cho xe vào, một làn dành cho xe ra.  
- Cổng xe ra sẽ cần tham chiếu thông tin đã nhập ở cổng vào

## Yêu cầu về kiến trúc và kỹ thuật

Dựa trên bối cảnh thực tế, phần mềm được xây dựng cần đảm bảo các yêu cầu sau:

- Sử dụng các kỹ thuật phù hợp với cấu hình và phần mềm của máy hiện tại.  
  - Đối với phần application, tôi đề xuất chúng ta sử dụng .NET core kết hợp với .NET MAUI để có thể chạy trên nhiều nền tảng.  
  - Không phân chia máy server và client. Tôi đề xuất chúng ta sẽ sử dụng máy ở cổng vào vừa làm client, vừa làm server  
- Sử dụng kiến trúc hiện đại, có thể mở rộng về sau.  
  - Tương lai có thể kết nối với các hệ thống khác thông qua REST API, đọc file, …  
- Database: hãy sử dụng các database dễ cài đặt, sử dụng và miễn phí.

## Yêu cầu chức năng

Kỳ vọng có thể có các chức năng sau

- Xác thực và phân quyền  
  - Quản lý người dùng  
    - Đề xuất một số nhóm người dùng nhất định: Quản trị viên, người nhập thông tin, người giám sát.  
  - Login/logout  
  - Cho phép nhóm người dùng cụ thể chỉ truy cập được các chức năng nhất định.  
- Quản lý các thông tin master: Dựa vào hướng dẫn điền sổ B2, cho phép quản trị viên nhập sẵn một số master data để phục vụ khi điền thông tin.  
- Xem và tìm kiếm thông tin: cho phép hiển thị danh sách các xe, tờ khai, … đã nhập quan, xuất quan.  
- Nhập thông tin xe vào (ở cổng vào)  
- Xác nhận thông tin xe ra (ở cổng ra)  
- Xuất ra file Excel như hiện tại đang dùng  
- Sao lưu, khôi phục

## Yêu cầu phi chức năng

Mong muốn phần mềm đảm bảo các tiêu chí sau:

- Có thể chạy trên các máy cấu hình thấp  
- Tính khả dụng (Learnability): Người dùng mới có thể thao tác nhập liệu thành thạo trong vòng 15 phút mà không cần đào tạo chuyên sâu.  
- Tính hiệu quả (Efficiency): Quy trình nhập liệu được tối ưu hóa, giảm thiểu số thao tác chuột và phím bấm; hoàn thành tác vụ tiêu chuẩn dưới 2 phút.  
- Tính trực quan (Clarity): Ngôn ngữ hiển thị phổ thông, dễ hiểu; hỗ trợ nhập liệu thông minh (gợi ý, tự động điền) để giảm sai sót.  
- Tính tiếp cận (Accessibility): Đạt chuẩn WCAG 2.1 cấp độ AA (độ tương phản cao, cỡ chữ lớn, dễ điều hướng) để hỗ trợ mọi lứa tuổi và trình độ tin học.  
- Phản hồi (Feedback): Hệ thống thông báo rõ ràng, trực quan ngay sau mỗi thao tác (thành công/lỗi) để người dùng dễ nắm bắt tình trạng xử lý.

## Tài liệu tham khảo

1. Template điền thông tin đang sử dụng: File “Mẫu sổ B2” (Bao gồm file ở định dạng excel và PDF).  
2. Hướng dẫn điền template: File “Business rules \- Hướng dẫn điền sổ B2” (bao gồm file ở định dạng md và doc).