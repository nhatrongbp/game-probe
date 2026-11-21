# Quy tắc làm việc trong repo

## 1. Không tự ý điều khiển máy tính của người dùng

Agent **không được tự ý** thực hiện các thao tác thay đổi trạng thái desktop, ứng dụng hoặc thiết bị của người dùng, bao gồm nhưng không giới hạn:

- mở, đóng, khởi động lại game hoặc bất kỳ ứng dụng nào;
- thu nhỏ, phóng to, di chuyển, resize, focus hoặc đặt cửa sổ lên foreground;
- di chuyển chuột, click, gõ phím, gửi `SendInput`, `SetCursorPos`, `SetForegroundWindow`, `ShowWindow` hoặc tạo input giả lập lên desktop thật;
- thay đổi độ phân giải, DPI, scaling, layout desktop, registry hoặc cài đặt hệ thống;
- chạy GUI app yêu cầu UAC, chạy game, hoặc chạy tool automation trên cửa sổ thật mà chưa được người dùng đồng ý.

Việc **đọc thông tin** như liệt kê cửa sổ/process, đọc title/class/client size/DPI là read-only và được phép khi cần cho việc phát triển, nhưng không được kèm theo thao tác thay đổi trạng thái.

## 2. Khi cần người dùng thao tác

Nếu công việc cần một thao tác thủ công, agent phải:

1. Dừng lại trước khi thực hiện.
2. Mô tả chính xác từng bước cần làm.
3. Nói rõ thao tác đó dùng để kiểm chứng điều gì.
4. Nhờ người dùng thực hiện và chờ họ xác nhận kết quả.

Ví dụ:

- “Anh/chị mở FC Online bằng account test, để cửa sổ ở chế độ windowed/borderless, rồi chạy lệnh này giúp tôi...”
- “Anh/chị thu nhỏ cửa sổ game và đặt ở góc trên-trái màn hình, sau đó cho tôi biết kết quả...”
- “Anh/chị chạy app bằng Administrator giúp tôi; tôi sẽ không tự bấm UAC.”

Không được tự làm thay các bước trên, kể cả khi có thể dùng shell/CLI để làm.

## 3. Phạm vi an toàn của tool

- Tool GameProbe chỉ được đọc memory (`PROCESS_VM_READ`); không ghi memory, không inject, không tạo remote thread.
- Không dùng `NtReadVirtualMemory` để lách hook/anti-cheat.
- Không bypass anti-cheat, không ẩn process, không che giấu hành vi.
- Chỉ test trên account test, ở login/lobby; không chạy memory scan khi đang thi đấu.
- Dừng ngay nếu game crash, mất kết nối, có dialog anti-cheat hoặc có dấu hiệu bất thường.

## 4. Quy ước code

- C# .NET 8, nullable enabled, target x64.
- Core không phụ thuộc WinForms; GUI/CLI chỉ gọi API của Core.
- Mọi thao tác Win32 phải kiểm tra handle/trạng thái trước khi dùng.
- Lỗi phải được log và trả về thông báo rõ ràng; không nuốt lỗi im lặng.
- Không thêm dependency mới nếu không cần thiết.
- Build phải sạch warning; test phải pass trước khi kết thúc phase.
