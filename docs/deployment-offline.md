# Triển khai offline (máy client không có internet)

> Mục tiêu: cài đặt **đơn giản, minh bạch, offline** trên PC cửa khẩu **không có internet**.
> Cơ chế: **xcopy** — một payload self-contained + `install.ps1`, phát hành dưới hai dạng cùng
> nội dung: gói `.zip` (nhấp đúp `CaiDat.cmd`) và `setup.exe` có giao diện (Inno Setup, chỉ là
> lớp vỏ gọi `install.ps1` — [ADR-0013](adr/ADR-0013-inno-setup-gui-installer.md)). Không có cơ
> chế auto-update (đã bỏ Velopack — xem [ADR-0007](adr/ADR-0007-evaluate-go-backend-port.md)).
>
> Nguyên tắc: máy đích **không cần cài .NET runtime**; các thành phần OS còn thiếu
> (WebView2, VC++) có sẵn trong bộ cài.

> 👉 **Người cài đặt tại cửa khẩu**: làm theo [`huong-dan-cai-dat.md`](huong-dan-cai-dat.md)
> (từng bước cho máy cổng vào + cổng ra, checklist, xử lý lỗi). Tài liệu này giải thích phần
> kỹ thuật của gói cài.

## Vì sao self-contained là đủ (và điều gì vẫn thiếu)

| Thành phần | Có cần cài trên máy đích? |
| --- | --- |
| **.NET 8 runtime** | ❌ Không — nhúng trong exe. `B2.Server.Service.exe` (~112 MB) và Desktop (`WindowsAppSDKSelfContained=true`) đã bao gồm runtime. |
| **Windows App SDK runtime** | ❌ Không — nhúng khi `WindowsAppSDKSelfContained=true`. |
| **WebView2 Runtime** | ✅ **Có** — MAUI/WinUI cần, KHÔNG nhúng. Đã đóng gói sẵn bản *Evergreen Standalone* x64 trong `prereqs/` (#214). |
| **VC++ 2015–2022 x64 Redistributable** | ✅ **Có** — KHÔNG nhúng. Đã đóng gói sẵn `prereqs/vc_redist.x64.exe`. |
| **SQLite** | ❌ Không — `e_sqlite3.dll` đi kèm; DB là 1 file `b2.db`. |
| **Litestream** (backup) | ❌ Không — `litestream.exe` đi kèm; chạy trên đĩa nội bộ/LAN, không cần internet. |

> ⚠️ Bỏ qua WebView2 hoặc VC++ redist → app desktop có thể **khởi động rồi crash** trên
> máy sạch. `install.ps1` tự cài 2 prereq này từ `prereqs\` trước khi cài app.

## Payload (bản phát hành tạo ra gì)

Mỗi tag `vX.Y.Z` kích hoạt `.github/workflows/release.yml` (runner Windows có internet) và
tạo GitHub Release đính kèm **hai file cùng nội dung**:

- `B2Clearance-<version>-setup.exe` — trình cài có giao diện: chọn vai trò máy (và với máy cổng
  ra: IP máy cổng vào + `b2-server.cer`), giải nén payload vào `%TEMP%`, chạy `install.ps1` trong
  cửa sổ console hiện rõ (để chép mã phục hồi + mật khẩu Admin), kiểm tra mã thoát. Không có
  mục gỡ cài trong Windows — gỡ bằng `uninstall.ps1`.
- `B2Clearance-<version>-offline.zip` — giải nén ra:

```
CaiDat.cmd                    # nhấp đúp: tự xin quyền Administrator, chạy install.ps1
install.ps1 / uninstall.ps1   # script vận hành (chạy quyền Administrator)
B2Clearance.exe               # installer CLI (đăng ký service, sinh mã phục hồi)
B2.Server.Service.exe         # Windows Service (self-contained)
Desktop/                      # app desktop (self-contained)
Templates/                    # mẫu Sổ B2 (.xlsx)
litestream.exe                # backup liên tục
prereqs/
  vc_redist.x64.exe                              # đã tải sẵn (offline), kiểm chữ ký Microsoft
  MicrosoftEdgeWebView2RuntimeInstallerX64.exe   # đã tải sẵn (offline), kiểm chữ ký Microsoft
docs/                         # hướng dẫn cài, runbook, tài liệu này
```

## Bước 1 — Chuẩn bị USB (làm 1 lần, không cần internet)

Tải `setup.exe` (hoặc zip, rồi giải nén) từ Release và chép vào USB. Không còn bước bổ sung
WebView2 bằng tay — CI đã tải và kiểm chữ ký.

## Bước 2 — Cài trên PC cửa khẩu (offline)

Nhấp đúp `setup.exe` hoặc `CaiDat.cmd` → **Yes** ở UAC → chọn vai trò máy. Tương đương bằng
lệnh (PowerShell Administrator, trong thư mục gói zip):

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force   # nếu bị chặn chạy script
.\install.ps1                                      # cài mới: hỏi vai trò; nâng cấp: giữ vai trò
.\install.ps1 -Role Server                         # máy cổng vào, không hỏi
.\install.ps1 -ServerUrl 192.168.1.10              # máy cổng ra, không hỏi
```

Vai trò máy được suy ra theo thứ tự: `-Role` / `-ClientOnly` / `-ServerUrl` → bản cài đang có
(dịch vụ `B2ClearanceService` → cổng vào; `Desktop\b2-client.json` → cổng ra) → menu (chỉ khi cài
mới, không tham số). `-PauseAtEnd` giữ cửa sổ mở kể cả khi lỗi (`CaiDat.cmd`/`setup.exe` bật sẵn);
lỗi in dòng `LOI: …` và thoát mã 1.

Với máy cổng vào, `install.ps1` sẽ tự động: cài VC++ + WebView2 (im lặng) → chép chương trình vào
`C:\Program Files\B2Clearance` → gọi `B2Clearance.exe install` (tạo thư mục dữ liệu, chứng
chỉ TLS cho tên máy + IP LAN — xuất `C:\ProgramData\B2\b2-server.cer` cho máy cổng ra, ADR-0010
—, đăng ký Windows Service tự khởi động, mở firewall TCP 5001 (HTTPS), **sinh + hiển
thị MÃ PHỤC HỒI** và **mật khẩu Admin ban đầu** sinh ngẫu nhiên cho riêng lần cài này — ADR-0009) →
tạo shortcut desktop → khởi động dịch vụ.

> 📋 **Ghi lại MÃ PHỤC HỒI** hiển thị trên màn hình (và file `B2-Recovery-Code.txt` trên
> Desktop) vào phong bì dán kín — dùng để giải mã backup khi quên mật khẩu cài đặt (FR-026).
> Xóa file nhắc sau khi đã lưu an toàn.

> ⚠️ `setup.exe` chưa ký số: SmartScreen có thể cảnh báo lần chạy đầu (**More info → Run anyway**).

## Bước 3 — Kiểm tra sau cài

- [ ] Dịch vụ **B2ClearanceService** đang chạy (`services.msc` / `sc query B2ClearanceService`).
- [ ] Mở app "Sổ B2 — Cửa khẩu Chi Ma", đăng nhập được → chỉ báo **🟢 Online**.
- [ ] Không có hộp thoại "cần cài .NET / Windows App Runtime / WebView2".
- [ ] `C:\ProgramData\B2\db\b2.db` xuất hiện; Litestream ghi replica trong `backup\`.

## Máy cổng ra (client trên LAN)

Nghiệp vụ có **2 máy**: máy **cổng vào** chạy service (vừa server vừa client) và máy **cổng ra**
chỉ là client, nối qua LAN. Cài máy cổng ra bằng `setup.exe` / `CaiDat.cmd` (chọn **Máy cổng
ra**), hoặc:

```powershell
# b2-server.cer chép bằng USB từ C:\ProgramData\B2\ của máy cổng vào; để ở gốc USB hoặc cạnh
# bộ cài thì tự tìm thấy, khỏi truyền -ServerCertificate.
.\install.ps1 -ServerUrl 192.168.1.10 -ServerCertificate E:\b2-server.cer
```

- `-ServerUrl` ghi `Desktop\b2-client.json` → `{"serverUrl": "192.168.1.10"}`. Nhận IP trần
  (tự hiểu `https://…:5001/`), `ip:port`, hoặc URL đầy đủ. Không có file này thì desktop dùng
  `https://127.0.0.1:5001/` (máy cổng vào).
- `-ServerCertificate` nhập chứng chỉ **công khai** của máy cổng vào vào `LocalMachine\Root`
  (gỡ chứng chỉ B2 cũ nếu có) và in **Thumbprint** — đối chiếu với dòng `Thumbprint` hiện trên
  máy cổng vào lúc cài. Từ chối tệp có khóa bí mật (`server.pfx` không bao giờ rời máy cổng vào).
- Không truyền `-ServerCertificate`: tìm `b2-server.cer` cạnh bộ cài, ở gốc ổ chứa bộ cài, rồi ở
  gốc các ổ rời (USB). Không thấy mà `LocalMachine\Root` đã có chứng chỉ B2 (nâng cấp) → giữ
  nguyên; không có gì → hỏi đường dẫn.
- Vai trò cổng ra (`-Role Client`, ngụ ý bởi `-ServerUrl`; `-ClientOnly` giữ để tương thích) bỏ qua
  đăng ký service, cấp chứng chỉ và mã phục hồi — máy cổng ra **không** có database riêng.
  Nâng cấp không truyền `-ServerUrl` → dùng lại `serverUrl` trong `b2-client.json`.
- Đổi địa chỉ sau khi cài: sửa trực tiếp `C:\Program Files\B2Clearance\Desktop\b2-client.json`
  rồi mở lại app. Không cần cài lại. (Lưu bằng Notepad kèm BOM vẫn đọc được.)
- Biến môi trường `B2_SERVER_URL` ghi đè file — tiện khi cần thử nhanh một địa chỉ khác.

Server chỉ lắng nghe **HTTPS** `https://0.0.0.0:5001` (khai báo trong `appsettings.json`;
`http://` trên địa chỉ LAN bị từ chối lúc khởi động — #147) nên máy khác trong LAN gọi được qua
kênh mã hóa; installer đã mở sẵn firewall TCP 5001 ở profile Private. Chứng chỉ có SAN gồm
`localhost`, tên máy và mọi IPv4 LAN đang hoạt động lúc cài. Nếu app báo 🔴 **Mất kết nối**:
`ping <ip-máy-cổng-vào>`, rồi kiểm tra service đang chạy, firewall, và chứng chỉ (dưới đây).

### Đổi IP / tên máy cổng vào

Chứng chỉ gắn với IP + tên máy lúc cấp. Sau khi đổi IP (nên đặt **IP tĩnh** cho máy cổng vào)
hoặc đổi tên máy, chạy trên máy cổng vào (Administrator):

```powershell
& 'C:\Program Files\B2Clearance\B2Clearance.exe' renew-certificate
```

Lệnh chỉ cấp lại khi tên/IP hiện tại không còn nằm trong chứng chỉ (hoặc sắp hết hạn), rồi khởi
động lại dịch vụ. Khi in "Đã cấp chứng chỉ TLS mới", chép `b2-server.cer` mới sang từng máy cổng
ra (gốc USB) và chạy lại bộ cài trên máy đó với IP mới (`.\install.ps1 -ServerUrl <IP mới>`).

## Cập nhật phiên bản (vẫn offline)

Không có auto-update. Nâng cấp = chạy lại `setup.exe` / `CaiDat.cmd` / `install.ps1` (không tham
số) của bản mới (qua USB) — vai trò máy và địa chỉ máy chủ được giữ nguyên. Script dừng
dịch vụ, chép đè binaries, khởi động lại. Dữ liệu `C:\ProgramData\B2` (database, backup, keys,
mã phục hồi) **được giữ nguyên** — `B2Clearance.exe install` bỏ qua các bước sinh secrets
nếu đã tồn tại (idempotent). Chứng chỉ TLS chỉ được cấp lại khi chứng chỉ cũ không còn đúng tên
máy / IP LAN — bản ≤ v1.3.0 chỉ có `localhost`, nên lần nâng cấp đầu lên HTTPS **luôn** cấp mới:
sau đó nâng cấp máy cổng ra với `b2-server.cer` mới ở gốc USB.

## Gỡ cài

```powershell
.\uninstall.ps1              # dừng + xóa dịch vụ, giữ lại dữ liệu C:\ProgramData\B2
.\uninstall.ps1 -PurgeData   # xóa cả dữ liệu (không hồi phục được)
```

## Diễn tập cài đặt trên máy ảo (dành cho dev trên Mac)

Gói cài và app desktop **chỉ chạy trên Windows** (`win-x64`, Windows Service, `sc.exe`,
WebView2), nên trên Mac không cài thử trực tiếp được. Trước mỗi đợt UAT hoặc sau khi sửa
`install.ps1` / `B2.Installer`, diễn tập trên máy ảo Windows để bắt lỗi cài đặt sớm — lỗi
`sc create` của v1.1.0–v1.2.0 chỉ lộ ra trên máy **sạch**, đúng loại lỗi mà bước này bắt được.

> Máy ảo **không thay thế** kiểm tra trên PC x64 thật ở cửa khẩu (hiệu năng, driver, chính sách
> nhóm của đơn vị). Kết quả UAT chính thức vẫn ghi trên PC thật.

### Chọn máy ảo

| Mac | Máy ảo | Độ sát với PC thật |
| --- | --- | --- |
| Apple Silicon (M1 trở lên) | **Windows 11 ARM** trên Parallels Desktop, VMware Fusion hoặc UTM | Khá — Windows 11 ARM chạy app x64 qua giả lập. Đủ để kiểm tra script, đăng ký service, firewall, đăng nhập, 2 máy qua LAN. |
| Intel | **Windows 10/11 x64** trên Parallels / VMware Fusion / VirtualBox | Sát nhất — cùng kiến trúc với PC cửa khẩu. |

Tránh chạy Windows x64 giả lập hoàn toàn trên Apple Silicon (UTM chế độ *Emulate*): chậm tới mức
không dùng được cho diễn tập.

Cấu hình tối thiểu mỗi máy ảo: 2 vCPU, 4 GB RAM, 64 GB ổ đĩa. Windows chưa kích hoạt vẫn dùng
được cho diễn tập.

### Chuẩn bị máy ảo sạch (làm 1 lần)

1. Cài Windows, tạo tài khoản quản trị cục bộ. **Không** cài .NET, Visual Studio hay công cụ dev
   — máy ảo phải giống PC cửa khẩu.
2. Tắt đồng bộ/chia sẻ thư mục mặc định nếu không cần, rồi **chụp snapshot** tên `clean`.
   Mọi lần diễn tập đều bắt đầu bằng việc khôi phục snapshot này — cài đè lên máy đã từng cài
   sẽ đi nhánh *nâng cấp* và che mất lỗi chỉ có ở lần cài đầu.
3. Muốn diễn tập máy cổng ra: nhân bản (clone) máy ảo thành máy thứ hai, đổi tên máy
   (`Rename-Computer`) để 2 máy không trùng tên trên mạng, chụp snapshot `clean` cho máy này.

### Mô phỏng mạng offline + LAN

- Đặt card mạng của cả 2 máy ảo ở chế độ **host-only** (Parallels: *Host-Only Network*;
  VMware Fusion: *Private to my Mac*; UTM: *Host Only*). Máy ảo thấy nhau nhưng **không** ra
  internet — đúng như cửa khẩu.
- Mạng host-only thường bị Windows xếp là **Public**, mà rule firewall TCP 5001 do installer tạo
  chỉ áp cho **Private**. Trên máy cổng vào, kiểm tra và chỉnh:

  ```powershell
  Get-NetConnectionProfile
  Set-NetConnectionProfile -InterfaceAlias '<tên card>' -NetworkCategory Private
  ```

  Ghi nhận nếu phải làm bước này — PC thật có thể gặp đúng tình huống đó
  (xem [`huong-dan-cai-dat.md`](huong-dan-cai-dat.md) § 7).

### Đưa gói cài vào máy ảo

Chuẩn bị như § Bước 1 trên Mac (`setup.exe`, hoặc giải nén zip), rồi chép vào máy ảo bằng thư mục chia sẻ hoặc kéo-thả. **Chép vào `C:\B2-Setup\`
trước khi chạy** — không chạy `CaiDat.cmd` / `install.ps1` trực tiếp từ đường dẫn mạng/chia sẻ
(`\\Mac\...`), vì robocopy và ExecutionPolicy xử lý đường dẫn UNC khác ổ cục bộ.

Windows 11 đã có sẵn WebView2 Runtime, nên trên máy ảo Windows 11 bước cài WebView2 gần như
không có tác dụng; PC cửa khẩu chạy Windows 10 thì có.

### Kịch bản diễn tập

Làm theo [`huong-dan-cai-dat.md`](huong-dan-cai-dat.md) từng bước, như người cài thật. Ghi lại
mọi chỗ tài liệu thiếu hoặc sai.

| # | Kịch bản | Kỳ vọng |
| --- | --- | --- |
| 1 | Máy cổng vào (snapshot `clean`): nhấp đúp `CaiDat.cmd` → chọn `1` | UAC hỏi quyền; hiện mã phục hồi, `HOAN TAT`; `Get-Service B2ClearanceService` → `Running` |
| 2 | Đăng nhập `admin` lần đầu | Bị buộc đổi mật khẩu; sau đó 🟢 Online |
| 3 | Máy cổng ra (snapshot `clean`): `setup.exe` → **Máy cổng ra**, IP máy cổng vào, `b2-server.cer` ở gốc USB tự điền | `HOAN TAT (may cong ra)`; app 🟢 Online |
| 4 | Nhập 1 xe ở cổng vào | Xe hiện trên máy cổng ra trong vài giây |
| 5 | Tắt service trên máy cổng vào | Máy cổng ra chuyển 🔴 Mất kết nối; bật lại → 🟢 |
| 6 | Chạy lại `setup.exe` trên máy cổng vào, rồi `CaiDat.cmd` trên máy cổng ra (nâng cấp) | Không hỏi vai trò; dữ liệu ở bước 4 còn nguyên; mã phục hồi không đổi; máy cổng ra giữ IP + chứng chỉ |
| 7 | `.\uninstall.ps1` rồi `.\install.ps1` lại | Dữ liệu còn nguyên (không có `-PurgeData`) |
| 8 | *(kiểm tra hồi quy)* Khôi phục `clean`, cài bằng gói **v1.2.0** | **Thất bại** ở bước đăng ký dịch vụ (`sc.exe`) — xác nhận máy ảo tái hiện được lỗi đã sửa ở v1.2.1 |

Sau khi xong, khôi phục snapshot `clean` để lần sau dùng lại. Lỗi phát hiện được: mở issue kèm
ảnh chụp màn hình và log (`C:\ProgramData\B2\logs\`, `%LOCALAPPDATA%\B2\logs\`).

### Chạy server trên Mac (không phải diễn tập cài đặt)

Để phát triển/kiểm thử API, server chạy thẳng trên Mac:

```bash
dotnet run --project src/B2.Server.Service -- --console
```

Trên macOS, server **không** dùng DPAPI — secrets (mật khẩu cài đặt, mã phục hồi, khóa JWT) được
lưu ở file dev **không mã hóa** (`keys/backup-secrets.dev.json`, `keys/jwt-rsa.dev.pem`) trong
thư mục dữ liệu của người dùng. Chỉ dùng cho dev, không bao
giờ cho dữ liệu thật. App desktop không build được trên Mac.

## Vì sao chọn xcopy thay vì Velopack

- Client **offline vĩnh viễn** ⇒ cơ chế auto-update/feed của Velopack là gánh nặng vô dụng.
- Một site duy nhất ⇒ không cần hạ tầng phân phối/cập nhật; `.zip` + script là đủ và **minh
  bạch hoàn toàn** (dễ kiểm toán an ninh trên máy khoá cứng).
- Logic bảo mật đã kiểm thử (mã phục hồi, DPAPI-seal, self-signed cert, đăng ký service) được
  **giữ nguyên** trong `B2Clearance.exe` — chỉ bóc lớp Velopack bọc ngoài.

## Quyền truy cập thư mục dữ liệu

`B2Clearance.exe install` đặt quyền cho `C:\ProgramData\B2` (và mọi thứ bên trong): **chỉ SYSTEM
(tài khoản chạy dịch vụ) và nhóm Administrators**, không kế thừa quyền từ `C:\ProgramData`.
Lý do: mọi secret trong `keys\` (khoá ký JWT, secrets sao lưu, `server.pfx`) được niêm phong DPAPI
phạm vi *LocalMachine* — **bất kỳ** tài khoản nào trên máy mở niêm phong được, nên chỉ quyền thư
mục mới ngăn được người dùng Windows thường. Chạy lại `install.ps1` (nâng cấp) áp lại quyền này cho
cả bản cài cũ. Ứng dụng Desktop không đọc thư mục này (log riêng ở `%LOCALAPPDATA%\B2\logs\`).

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
