# Phân tích tĩnh FFZik.exe — Cơ chế auto click vào cửa sổ game

> Tài liệu ghi lại kết quả reverse-engineering **tĩnh** (không chạy thử) trên file `FFZik/FFZik.exe`.
> Mục đích: xác định công nghệ mà FFZik dùng để tự động click vào cửa sổ FC Online.

---

## 1. Kết luận nhanh

FFZik dùng **Win32 window messages** gửi trực tiếp vào `HWND` của cửa sổ game:

- **Click:** `PostMessageW` (chế độ nền) hoặc `SendMessageW` (chế độ đồng bộ) với:
  - `WM_LBUTTONDOWN` (`0x0201`) / `WM_LBUTTONUP` (`0x0202`)
  - `WM_RBUTTONDOWN` (`0x0204`) / `WM_RBUTTONUP` (`0x0205`)
  - `WM_MBUTTONDOWN` (`0x0207`) / `WM_MBUTTONUP` (`0x0208`)
- **Di chuyển / hover:** `PostMessageW(hwnd, WM_MOUSEMOVE, 0, MAKELPARAM(x, y))`.
- **Không dùng** driver kernel, HID giả lập (Arduino/Teensy/KMBox), `SendInput` hay `mouse_event` để click.
- **Quyết định click** chủ yếu dựa trên trạng thái đọc từ memory game và kích thước/toạ độ cửa sổ; chỉ có **một** chỗ kiểm tra màu 1 pixel trước khi click. FFZik **không** OCR/đọc text bên trong vùng render của game (xem mục 11).

`SendInput` chỉ được dùng cho **bàn phím Unicode** (gõ ID/mật khẩu/chat), không liên quan đến click chuột.
`keybd_event` chỉ dùng cho thủ thuật ép foreground bằng phím Alt.

Đây là kiểu **background message automation**: game không cần focus, con trỏ chuột thật không di chuyển. Nó khớp với tùy chọn *"Nofocus (No need focus to game)"* trong file ngôn ngữ của FFZik.

---

## 2. Thông tin nhị phân

| Thuộc tính | Giá trị |
|---|---|
| Tên file | `FFZik.exe` |
| Kích thước | `6,217,216` bytes (~5.93 MiB) |
| SHA-256 | `87ADB6C658BDCC3FF1D56DE6A43D406E4FA4922984F2900FD8323A48AFA0A2BC` |
| PE format | PE32+ (x64), `Machine = 0x8664` |
| Image base | `0x140000000` |
| Subsystem | Windows GUI |
| Số section | 10 (`.text`, `.rdata`, `.data`, `.pdata`, `.gfids`, `.giats`, `.tls`, `.poi`, `.rsrc`, `.reloc`) |
| Chữ ký số | Không có (`NotSigned`) |
| Manifest | `requestedExecutionLevel level="requireAdministrator"` |
| File version | `1.0.0.1` |
| Product | `FFZik` — `Auto FC Online` |
| Copyright | `Copyright (C) 2018` |
| Company | `http://www.ffzik.com` |
| Packer | Không phát hiện (import table và string đọc được bình thường) |

Các section có kích thước bình thường, không có dấu hiệu UPX/VMProtect/Enigma. `.rsrc` khá lớn (~1.5 MB) chứa icon/ảnh/UI resources.

---

## 3. Tech stack

| Thành phần | Công nghệ |
|---|---|
| Ngôn ngữ | C++ native (MSVC) |
| UI desktop | **MFC** (static link) — có các symbol `CMemoryException`, `CDialog`, `CWinApp`, `AFX_WM_DRAW2D`... |
| Browser nhúng | **CEF 3.3683.1920** (Chromium/Chrome 80), delay-load `libcef.dll` |
| JSON | `nlohmann/json` (thấy các symbol `input_adapter_protocol`, `input_buffer_adapter`) |
| HTTP | WinINET (`InternetCrackUrlW`), CEF |
| Đóng gói / bảo vệ | Không có packer; một số chuỗi nhạy cảm được XOR khi dùng (xem Phụ lục C) nhưng không phải packer |

Không có dấu hiệu của .NET (`mscoree` chỉ là string kiểm tra), Python, AutoIt, AutoHotkey, Electron hay Node.js.

CEF được dùng cho UI web/login/website, **không phải** để điều khiển game.

---

## 4. Import table đáng chú ý

Các API Win32 được import (trích):

```text
USER32.dll
  SendMessageW, PostMessageW
  FindWindowW, FindWindowExW, GetClassNameW
  GetWindowRect, GetClientRect, MapWindowPoints, ClientToScreen, ScreenToClient
  IsWindow, SetActiveWindow, SetForegroundWindow, AllowSetForegroundWindow
  AttachThreadInput, GetForegroundWindow, GetWindowThreadProcessId
  ShowWindow, SetWindowPos, SetWindowLongPtrW, SetWindowsHookExW
  keybd_event, SendInput, GetAsyncKeyState, GetKeyboardState
  ChangeWindowMessageFilter
  GetDC, ReleaseDC (GDI)

KERNEL32.dll / ntdll.dll / PSAPI.dll
  OpenProcess, ReadProcessMemory, NtReadVirtualMemory, NtOpenProcess
  EnumProcesses, CreateToolhelp32Snapshot, Process32FirstW/NextW
  DeviceIoControl
  VirtualProtect, CreateThread, CreateTimerQueueTimer
```

Ý nghĩa nhanh:

- `SendMessageW` / `PostMessageW` + `FindWindowW` + `GetWindowRect` + `MapWindowPoints` = bộ API điển hình để **gửi chuột/bàn phím vào cửa sổ khác**.
- `ReadProcessMemory` / `NtReadVirtualMemory` = đọc memory game (trạng thái trận, tỉ số, thời gian...).
- `DeviceIoControl` = đọc thông tin ổ cứng (`\\.\PhysicalDrive%d`) cho HWID/license, không phải driver input.
- Không có `mouse_event`, `SetCursorPos`, `Interception`, `HID`, `libusb`, `Serial`, `Arduino`.

---

## 5. Hàm click chính — RVA `0x28340`

- **RVA:** `0x28340` → **VA:** `0x1400028340`
- **Kết thúc:** `0x28598`
- Đây là một hàm C++ (arg1 là `this`, arg2 là `HWND`).

### 5.1. Chữ ký suy luận

```cpp
BOOL Click(HWND hwnd,          // arg2 (rdx)
           int  button,        // arg3 (r8d): 0 = left, 1 = right, 2 = middle
           int  count,         // arg4 (r9d): số lần click
           int  x,             // arg5 [rsp+0x90]
           int  y,             // arg6 [rsp+0x98]
           int  delayMs,       // arg7 [rsp+0xa0]
           bool syncMode);     // arg8 [rsp+0xa8]
```

### 5.2. Logic

1. `IsWindow(hwnd)` — nếu không hợp lệ thì trả về `0`.
2. `SetActiveWindow(hwnd)`.
3. `GetWindowRect(hwnd, &rect)`.
4. Nếu `x == -1` → `x = (rect.right - rect.left) / 2`.
   Nếu `y == -1` → `y = (rect.bottom - rect.top) / 2`.
5. `lParam = MAKELPARAM(x, y)` = `(y << 16) | (x & 0xFFFF)`.
6. Chọn message theo nút:

   | `button` | Down | Up | `wParam` |
   |---|---|---|---|
   | `0` (left) | `WM_LBUTTONDOWN` `0x0201` | `WM_LBUTTONUP` `0x0202` | `MK_LBUTTON` `0x0001` |
   | `1` (right) | `WM_RBUTTONDOWN` `0x0204` | `WM_RBUTTONUP` `0x0205` | `MK_RBUTTON` `0x0002` |
   | `2` (middle) | `WM_MBUTTONDOWN` `0x0207` | `WM_MBUTTONUP` `0x0208` | `MK_MBUTTON` `0x0010` |

7. Có **2 nhánh**:

**Nhánh `syncMode == true`** (RVA `0x284c0`–`0x2850f`):

```cpp
for (int i = 0; i < count; i++) {
    SendMessageW(hwnd, downMsg, wParam, lParam);  // RVA 0x284d6
    Sleep(delayMs);                               // RVA 0x284e7
    SendMessageW(hwnd, upMsg, 0, lParam);         // RVA 0x284fa
    Sleep(200);                                   // RVA 0x28500
}
```

**Nhánh `syncMode == false`** (RVA `0x28510`–`0x2857f`):

```cpp
for (int i = 0; i < count; i++) {
    PostMessageW(hwnd, downMsg, 0, lParam);       // RVA 0x28543
    Sleep(delayMs);
    PostMessageW(hwnd, upMsg, 0, lParam);         // RVA 0x28567
    Sleep(200);                                   // RVA 0x28570
}
```

8. Trả về `1` nếu đã gửi.

### 5.3. Nhận xét

- **Tất cả call site** trong code (trừ một trường hợp) đều truyền `syncMode = false` → dùng `PostMessageW` (bất đồng bộ, không cần focus).
- Một call site tại RVA `0x1f0130` truyền `syncMode = true` → dùng `SendMessageW` (đồng bộ) tại toạ độ `(568, 124)`, delay `100 ms`. Đây có thể là click vào ô login/UI cần game xử lý xong ngay.
- Cả hai nhánh đều **không di chuyển con trỏ chuột thật**, không dùng `SendInput`.
- `SetActiveWindow` gọi cross-process thường thất bại, nhưng không ảnh hưởng đến `PostMessage`.
- Toạ độ là **client coordinates** của cửa sổ game.

### 5.4. Các call site tiêu biểu

| Call site (RVA) | `syncMode` | Toạ độ | Nút | Ghi chú |
|---|---|---|---|---|
| `0x1f0130` | `1` | `(568, 124)` | Left | Click đồng bộ, sau đó gửi Tab/Enter |
| `0x1f09e6` | `0` | `(x-20, 15)` | Left | Click danh sách/item |
| `0x1f0aab` | `0` | `(60, 30)` | Left | |
| `0x1f0c4b` | `0` | `(x, 28)` | **Right** | Mở menu ngữ cảnh |
| `0x1f0ca7` | `0` | `(x, 245)` | Left | |
| `0x1f1381` | `0` | `(x-20, 15)` | Left | |
| `0x1f191e` | `0` | `(x-20, 15)` | Left | |
| `0x1f19e3` | `0` | `(60, 30)` | Left | |
| `0x1f1eb5` | `0` | `(x, 100)` | Left | |
| `0x1f2285` | `0` | `(x, y)` | Left | Toạ độ động, sau khi check màu pixel (mục 11.2) |
| `0x278e7d` | `0` | `(-1, -1)` | Left | Click tâm cửa sổ |
| `0x2796ec` | `0` | `(-1, -1)` | Left | Click tâm cửa sổ |

---

## 6. Hàm gửi `WM_MOUSEMOVE` — RVA `0x22DAA0`

- **RVA:** `0x22DAA0` → **VA:** `0x140022DAA0`
- Kết thúc: `0x22DB5E`

Luồng:

1. Lấy `hwnd` từ object (`[this+0x40]`).
2. Kiểm tra `[this+0x180]`.
3. Gọi `MapWindowPoints(hwnd, NULL, &point, 2)` — chuyển toạ độ qua lại giữa client/screen (chi tiết hơi rối do compiler tối ưu; về mặt chức năng là xử lý toạ độ cửa sổ).
4. Đóng gói `lParam = MAKELPARAM(x, y)`.
5. Gửi:

```cpp
// RVA 0x22DB53
PostMessageW(hwnd, WM_MOUSEMOVE /* 0x0200 */, 0, lParam);
```

Hàm này được gọi qua virtual table (không tìm thấy call trực tiếp), thường dùng trước click để game cập nhật hover/vị trí.

---

## 7. Bàn phím và `SendInput` — RVA `0x262C40`

- **RVA:** `0x262C40` → **VA:** `0x1400262C40`
- Đây là wrapper gửi **một ký tự Unicode** bằng `SendInput`.

Cấu trúc `INPUT` được dựng:

```cpp
INPUT input = {};
input.type = INPUT_KEYBOARD;          // 1
input.ki.wVk = ch;                    // ký tự cần gửi
input.ki.wScan = 0;
input.ki.dwFlags = KEYEVENTF_UNICODE; // 0x0004
SendInput(1, &input, sizeof(INPUT));  // RVA 0x262CF3

input.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP; // 0x0006
SendInput(1, &input, sizeof(INPUT));  // RVA 0x262D3D
```

- `INPUT.type = 1` → **keyboard**, không phải mouse.
- `dwFlags = 4` / `6` → `KEYEVENTF_UNICODE` / `KEYEVENTF_UNICODE | KEYEVENTF_KEYUP`.
- Chỉ có **2 call site** của `SendInput`, đều nằm trong wrapper này.
- Mục đích: gõ chuỗi Unicode vào cửa sổ đang focus (ID/mật khẩu/chat), không dùng để click.

Ngoài ra, FFZik còn gửi phím trực tiếp bằng message:

```cpp
SendMessageW(hwnd, WM_KEYDOWN /* 0x100 */, VK_TAB /* 0x09 */, 0);
SendMessageW(hwnd, WM_KEYUP   /* 0x101 */, VK_TAB /* 0x09 */, 0);
SendMessageW(hwnd, WM_CHAR    /* 0x102 */, ch, 0);          // Unicode char
```

Các call site nằm quanh RVA `0x1f014b`–`0x1f035e` (gửi Tab/Enter/ký tự) — dùng cho login/typing.

---

## 8. Ép foreground — RVA `0x285A0`

FFZik có một routine ép cửa sổ game lên foreground, dùng combo kinh điển:

```cpp
IsWindow(hwnd);
if (IsIconic(hwnd))
    ShowWindow(hwnd, SW_RESTORE);          // 9

GetForegroundWindow();
GetWindowThreadProcessId(...);
GetCurrentThreadId();
AttachThreadInput(...);

SystemParametersInfoW(SPI_GETFOREGROUNDLOCKTIMEOUT, ...);
SystemParametersInfoW(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, NULL, 0);
AllowSetForegroundWindow(ASFW_ANY);         // -1
SetForegroundWindow(hwnd);

// Thủ thuật Alt để vượt foreground lock
keybd_event(VK_MENU, 0, KEYEVENTF_EXTENDEDKEY, 0);
SetForegroundWindow(hwnd);
keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);

// Detach thread input, restore timeout
```

Các call `keybd_event(VK_MENU)` nằm tại RVA `0x287aa` và `0x287f1`.
Đây **không phải** click; chỉ dùng để đưa game lên trước khi gửi phím/click đồng bộ.

---

## 9. Cửa sổ / process game được nhắm tới

Trong `.rdata` có cụm chuỗi (UTF-16) nằm cạnh nhau:

```text
Garena.exe
Qt5QWindowIcon
Qt5QWindowIcon
fczf.exe
Qt5QWindowIcon
fclauncher.exe
Qt5QWindowIcon
CefBrowserWindow
Chrome_WidgetWin_0
Chrome_RenderWidgetHostHWND
Garena
Qt5QWindowIcon
CefBrowserWindow
Chrome_WidgetWin_0
Chrome_RenderWidgetHostHWND
FIFAKC
QTrayIconMessageWindowClass
```

Suy luận:

- **`FIFAKC`** rất có thể là **window class của game FC Online** (được dùng với `FindWindowW`/`FindWindowExW`).
- **`Qt5QWindowIcon`** là class cửa sổ Qt5 của Garena / fclauncher.
- **`Garena.exe`, `fczf.exe`, `fclauncher.exe`** là các process được theo dõi/khởi động.
- Chuỗi `Qt5QWindowIcon → CefBrowserWindow → Chrome_WidgetWin_0 → Chrome_RenderWidgetHostHWND` là **chain tìm cửa sổ web CEF bên trong Garena** (hàm `0x1F3370` bắt đầu từ cửa sổ `Garena`, sau đó `FindWindowExW` lần lượt xuống các cửa sổ con). FFZik dùng chain này để click nút Play/Start trên giao diện web của Garena. Bản thân UI của FFZik cũng dùng CEF nên các class này có thể xuất hiện ở cả hai nơi.
- `QTrayIconMessageWindowClass` là cửa sổ tray icon của Garena.

Các chuỗi class/API nhạy cảm trên không được dùng trực tiếp: trước khi gọi API, FFZik copy chúng qua một bước XOR với key tăng dần (`0x2258 + i` cho UTF-16, `0x58 + i` cho ANSI). Đây là obfuscation chuỗi ở runtime, không phải packer — xem Phụ lục C.

Ngoài ra có các chuỗi:

```text
FC Online: fczf.exe - Application Error
FC Online: Windows - Application Error
WerFault.exe
dwwin.exe
```

→ Dùng để phát hiện dialog crash của game và tự restart.

---

## 10. Đọc memory game và command line

FFZik dùng:

```text
OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION)  // 0x410
ReadProcessMemory(...)
NtOpenProcess / NtReadVirtualMemory
EnumProcesses / CreateToolhelp32Snapshot
```

Các call site đáng chú ý:

| RVA | API | Kích thước đọc |
|---|---|---|
| `0x25529` | `OpenProcess(0x410, ...)` | — |
| `0x255cd` | `ReadProcessMemory` (PEB) | `0x2C8` (712 bytes) |
| `0x25600` | `ReadProcessMemory` (ProcessParameters) | `0x80` (128 bytes) |
| `0x25670` | `ReadProcessMemory` (CommandLine) | `Length + 2` |

Đây là kỹ thuật **đọc trạng thái game** (có thể là tỉ số, thời gian, trạng thái trận), tách biệt với cơ chế click. Việc dùng thêm `NtReadVirtualMemory` có thể nhằm lách hook của anti-cheat ở user-mode.

### 10.1. Đọc command line của game qua PEB — RVA `0x25040`

FFZik không chỉ đọc struct trạng thái; nó còn đọc **chuỗi command line** của process game:

1. Resolve động `NtQueryInformationProcess` bằng `GetProcAddress` (tên API bị XOR — xem Phụ lục C) tại RVA `0x25503`.
2. Gọi `NtQueryInformationProcess(handle, ProcessBasicInformation /* 0 */, &pbi, 0x30, &ret)` để lấy `PebBaseAddress`.
3. `ReadProcessMemory(peb, 0x2C8)` — đọc PEB.
4. Đọc `PEB->ProcessParameters` (offset `+0x20`), rồi `ReadProcessMemory(processParams, 0x80)` — đọc đầu `RTL_USER_PROCESS_PARAMETERS`.
5. Lấy `UNICODE_STRING CommandLine` tại offset `+0x70` (Length) và `+0x78` (Buffer), sau đó `ReadProcessMemory` đọc chuỗi command line.
6. So chuỗi này với giá trị cấu hình bằng `CString::Find` (RVA `0x24D70`).

Hàm `0x24730` dùng kết quả để quyết định có `TerminateProcess` game hay không (phục vụ đổi account/restart). Đây là **đọc chuỗi từ memory**, không phải đọc text hiển thị trên màn hình.

---

## 11. Kiểm tra trạng thái UI — không dùng OCR

FFZik **không** đọc text hiển thị trong client area của game/launcher. Ba nguồn thông tin nó dùng để quyết định hành động:

### 11.1. Window title / class

- Wrapper đọc title tại **RVA `0x1F5250`** dùng `SendMessageW` chứ không phải `GetWindowTextW`:
  ```cpp
  len = SendMessageW(hwnd, WM_GETTEXTLENGTH /* 0x0E */, 0, 0);
  SendMessageW(hwnd, WM_GETTEXT /* 0x0D */, len + 1, buf);
  ```
- So khớp class/title bằng `CString` (RVA `0x11500`) và tìm chuỗi con (RVA `0x24D70`). Hàm `0x1EF800` tìm cửa sổ class `Qt5QWindowIcon` có title chứa `" - "` để tách **Game ID** (feature *"Đang tìm cửa sổ ID..."*).
- Hàm `0x1F4450` duyệt các cửa sổ sibling bằng `GetWindow(hwnd, GW_HWNDNEXT)` rồi so cả class lẫn title — dùng để tìm đúng cửa sổ Garena/game/dialog lỗi.
- Giới hạn: chỉ đọc được **title bar**, không đọc được chữ vẽ bên trong game.

### 11.2. Màu 1 pixel — RVA `0x1EE6C0`

```cpp
HDC dc = GetDC(hwnd);
BitBlt(memDC, 0, 0, 1, 1, dc, x, y, SRCCOPY);          // chụp đúng 1 pixel
GetDIBits(memDC, bmp, 0, 1, &pixel, &bmi, DIB_RGB_COLORS);
```

Kết quả được phân loại:

| Return | Điều kiện | Ý nghĩa |
|---|---|---|
| `0` | R = G = B > 0 | trắng/xám |
| `1` | R = G, B > G | xanh dương |
| `2` | R = G, B < G | vàng |
| `3` | còn lại | màu khác/đen |

Caller duy nhất nằm tại **`0x1F2107`**, trong hàm **`0x1F0CD0`** (luồng vào game qua Garena):

- Lấy toạ độ `(centerX + 30, height - 67)` của cửa sổ.
- Pixel trả về `1` (xanh) → gọi hàm click `0x28340` tại đúng điểm đó (chuột trái, 1 lần, delay `100 ms`, chế độ nền).
- Pixel trả về `0` (trắng/xám) → dừng, không click.
- Pixel trả về `2`/`3` → chờ và thử lại, tối đa ~10 giây.

Đây là "UI state check" duy nhất trong tool; nó **không đọc chữ** trên nút, chỉ phân biệt màu.

### 11.3. Vị trí cấu hình tay

- FFZik không nhận diện icon FC Online bằng text/hình ảnh. Người dùng nhập vị trí icon trên giao diện Garena theo công thức `(Hàng - 1) x 4 + Cột` (xem `Language/vi.lang`).
- Hàm `0x1F03E0` chỉ lọc cửa sổ con theo kích thước (`0x64`–`0x190`, tức 100–400 px) rồi click; không kiểm tra nội dung.

### 11.4. Kết luận

Không có tính năng kiểu *"vùng UI có text bằng X → click A, ngược lại click B"*. Muốn có phải thêm OCR (Tesseract/`Windows.Media.Ocr`) hoặc template matching (OpenCV); FFZik hiện chỉ có window-title matching, memory reading và pixel-color probe.

---

## 12. Những gì FFZik **không** dùng

- Không có kernel driver input (không thấy `\\.\Interception`, `MouClass`, `Mouhid`, `HID`, `USB`, `Serial`, `Arduino`).
- Không có `mouse_event`, `SetCursorPos`.
- Không dùng `SendInput` cho chuột.
- Không dùng AutoIt/AutoHotkey/Python/.NET.
- Không dùng OCR/template matching (không có OpenCV/Tesseract). GDI+ chủ yếu để vẽ UI; ngoại lệ là probe màu 1 pixel bằng `BitBlt`/`GetDIBits` ở mục 11.2 — không phải nhận dạng ảnh.
- Không đọc text trong client area của game; `WM_GETTEXT` chỉ dùng cho title bar/control của chính FFZik.
- Không dùng accessibility để đọc UI của app khác: `OLEACC.dll` chỉ có `CreateStdAccessibleObject` (ATL `IAccessible` cho UI của chính FFZik), không có `AccessibleObjectFromWindow`/UIAutomation.
- Không inject DLL vào game (không có `CreateRemoteThread`; chỉ `ReadProcessMemory`).
- `DeviceIoControl` chỉ dùng để đọc `\\.\PhysicalDrive%d` (HWID/license), không phải driver input.

---

## 13. Cách kiểm chứng động (nếu muốn)

1. **Spy++** (Visual Studio) hoặc **Windows Message Analyzer**:
   - Tìm cửa sổ class `FIFAKC`.
   - Log messages khi FFZik chạy → sẽ thấy `WM_MOUSEMOVE`, `WM_LBUTTONDOWN`, `WM_LBUTTONUP`.
2. **API Monitor** / **Process Monitor**:
   - Hook `PostMessageW`, `SendMessageW`, `SendInput`, `SetForegroundWindow`.
   - Kiểm tra tham số message và toạ độ.
3. **x64dbg**:
   - Attach FFZik, đặt breakpoint tại `0x1400028340` (hàm click) và `0x140022DAA0` (mouse move).
   - Xem `rdx` (hwnd), `r8d` (button), `r9d` (count), stack args (x, y, delay, syncMode).
4. **Cheat Engine** (chỉ để quan sát):
   - Xem FFZik có đọc memory game không; nhưng cẩn thận với anti-cheat.

---

## 14. Code minh hoạ tối giản

```cpp
#include <windows.h>

void ClickGame(int x, int y)
{
    HWND hwnd = FindWindowW(L"FIFAKC", nullptr);
    if (!hwnd)
        return;

    // (tuỳ chọn) đưa game lên foreground
    // SetForegroundWindow(hwnd);

    LPARAM lp = MAKELPARAM(x, y);

    // hover trước
    PostMessageW(hwnd, WM_MOUSEMOVE, 0, lp);

    // click nền, không cần focus
    PostMessageW(hwnd, WM_LBUTTONDOWN, 0, lp);
    Sleep(30);
    PostMessageW(hwnd, WM_LBUTTONUP, 0, lp);
}
```

Ghi chú:

- Nếu game cần trạng thái nút, đổi `wParam` của `WM_LBUTTONDOWN` thành `MK_LBUTTON` (`1`).
- Nếu game cần xử lý đồng bộ, dùng `SendMessageW` thay `PostMessageW`; nhưng `SendMessageW` có thể block nếu game treo.
- Toạ độ là **client coordinates**, gốc `(0,0)` ở góc trên-trái vùng client của cửa sổ game.
- Chạy app cùng mức quyền với game (FFZik yêu cầu admin).
- Nếu game dùng Raw Input/DirectInput hoặc anti-cheat kiểm tra message, cách này sẽ không work hoặc bị phát hiện.

---

## 15. Hạn chế của phân tích

- Đây là **phân tích tĩnh** trên một bản build cụ thể (`1.0.0.1`, SHA-256 ở trên). Bản khác có thể thay đổi RVA/offset.
- Không chạy thử nên chưa xác nhận 100% hành vi runtime; kết luận dựa trên:
  - Import table
  - Chuỗi trong `.rdata`
  - Disassembly các hàm liên quan
  - Manifest và metadata PE
- Việc giải thích chính xác tham số `syncMode` (byte thứ 8) dựa trên hành vi hai nhánh; tên gọi chỉ là suy luận.
- `FIFAKC` được suy đoán là class cửa sổ game dựa trên ngữ cảnh cụm chuỗi; chưa xác minh bằng `Spy++` runtime.

---

## 16. Cảnh báo

- Kỹ thuật gửi window message vào game là can thiệp vào tiến trình khác. Việc sử dụng có thể vi phạm **Điều khoản dịch vụ** của FC Online/Garena và có thể bị anti-cheat phát hiện/xử lý.
- Tài liệu này chỉ nhằm mục đích **nghiên cứu bảo mật và học tập** về cách các tool automation hoạt động.
- Không nên dùng để gian lận trong game.

---

## Phụ lục A — Bảng RVA/VA quan trọng

| Chức năng | RVA | VA |
|---|---|---|
| Hàm click chính | `0x28340` | `0x1400028340` |
| Nhánh `SendMessageW` (down/up) | `0x284d6` / `0x284fa` | `0x14000284D6` / `0x14000284FA` |
| Nhánh `PostMessageW` (down/up) | `0x28543` / `0x28567` | `0x1400028543` / `0x1400028567` |
| Hàm `WM_MOUSEMOVE` | `0x22DAA0` | `0x140022DAA0` |
| `PostMessageW(WM_MOUSEMOVE)` | `0x22DB53` | `0x140022DB53` |
| Wrapper `SendInput` Unicode | `0x262C40` | `0x1400262C40` |
| `SendInput` keydown/keyup | `0x262CF3` / `0x262D3D` | `0x1400262CF3` / `0x1400262D3D` |
| Routine ép foreground | `0x285A0` | `0x14000285A0` |
| `keybd_event(VK_MENU)` | `0x287AA` / `0x287F1` | `0x14000287AA` / `0x14000287F1` |
| Gửi `WM_KEYDOWN/UP/CHAR` | `0x1f014b`–`0x1f035e` | `0x14001F014B`–`0x14001F035E` |
| Đọc memory (`ReadProcessMemory`) | `0x255cd` / `0x25600` / `0x25670` | `0x1400255CD` / `0x140025600` / `0x140025670` |
| Đọc PEB + command line game | `0x25040` | `0x140025040` |
| Resolve `NtQueryInformationProcess` | `0x25503` | `0x140025503` |
| Logic đổi account/restart (có `TerminateProcess`) | `0x24730` | `0x140024730` |
| Kiểm tra màu 1 pixel | `0x1EE6C0` | `0x14001EE6C0` |
| Caller pixel check (luồng Garena) | `0x1F2107` | `0x14001F2107` |
| Wrapper đọc window text (`WM_GETTEXT`) | `0x1F5250` | `0x14001F5250` |
| Tìm cửa sổ theo class/title | `0x1F4450` | `0x14001F4450` |
| Luồng vào game qua Garena | `0x1F0CD0` | `0x14001F0CD0` |

## Phụ lục B — Hằng số message

| Constant | Giá trị |
|---|---|
| `WM_MOUSEMOVE` | `0x0200` |
| `WM_LBUTTONDOWN` | `0x0201` |
| `WM_LBUTTONUP` | `0x0202` |
| `WM_RBUTTONDOWN` | `0x0204` |
| `WM_RBUTTONUP` | `0x0205` |
| `WM_MBUTTONDOWN` | `0x0207` |
| `WM_MBUTTONUP` | `0x0208` |
| `WM_KEYDOWN` | `0x0100` |
| `WM_KEYUP` | `0x0101` |
| `WM_CHAR` | `0x0102` |
| `MK_LBUTTON` | `0x0001` |
| `MK_RBUTTON` | `0x0002` |
| `MK_MBUTTON` | `0x0010` |
| `KEYEVENTF_UNICODE` | `0x0004` |
| `KEYEVENTF_KEYUP` | `0x0002` |
| `SPI_GETFOREGROUNDLOCKTIMEOUT` | `0x2000` |
| `SPI_SETFOREGROUNDLOCKTIMEOUT` | `0x2001` |

## Phụ lục C — Chuỗi bị XOR khi dùng

| Loại | Key XOR | Call site ví dụ | Chuỗi gốc |
|---|---|---|---|
| UTF-16 | `0x2258 + i` | `0x1F0D54` | `Garena.exe` |
| UTF-16 | `0x2258 + i` | `0x1EF86E` | `Qt5QWindowIcon` |
| UTF-16 | `0x2258 + i` | `0x1F23C0` / `0x1F3370` | `CefBrowserWindow`, `Chrome_WidgetWin_0`, `Chrome_RenderWidgetHostHWND` |
| ANSI | `0x58 + i` | `0x254AB` | `NtQueryInformationProcess` |
| ANSI | `0x58 + i` | `0x1FA129` | `Static` |

Đây là obfuscation chuỗi ở runtime: chương trình copy chuỗi qua một buffer có XOR với key tăng dần trước khi đưa vào API/so sánh. Không phải packer — chuỗi gốc vẫn nằm trong `.rdata` nên phân tích tĩnh vẫn đọc được.

---

*Tài liệu được tạo bằng phân tích tĩnh PE/import/disassembly. Không có file nào của FFZik bị sửa đổi.*
*Bổ sung: mục 10.1 (đọc PEB/command line), mục 11 (kiểm tra màu pixel & window title) và Phụ lục C (chuỗi XOR).*
