# Kế hoạch triển khai GameProbe

> **Trạng thái:** Kế hoạch triển khai, chưa viết mã
> **Ngày:** 2026-11-21
> **Tài liệu tham chiếu:** `docs/ffzik-auto-click-analysis.md`
> **Đối tượng kiểm thử chính:** FC Online, dùng account test

---

## 0. Quyết định đã chốt

| Hạng mục | Quyết định |
|---|---|
| Ngôn ngữ / nền tảng | **C# .NET 8**, WinForms, x64, single-file publish |
| Giao diện | **GUI + CLI** dùng chung `GameProbe.Core` |
| Cách đọc text | **Window title + scan chuỗi trong RAM (UTF-16LE/UTF-8) + snapshot/diff**; known-address và OCR để giai đoạn sau |
| Đối tượng test | **FC Online trực tiếp**, account test; vẫn có unit test và fixture nhỏ để test offline trước |
| Quyền chạy | Administrator (`requireAdministrator`) |
| DPI | PerMonitorV2 |
| Mức can thiệp | **Chỉ đọc memory**, không ghi, không inject, không dùng driver, không bypass anti-cheat |

---

## 1. Mục tiêu

### 1.1. Ba chức năng bắt buộc

1. **Click point**
   - Click trái/phải/giữa tại `(x, y)` cấu hình sẵn.
   - Gửi window message trực tiếp vào HWND game, mặc định không cần focus.
   - Không di chuyển con trỏ chuột thật.
   - Hỗ trợ nhiều điểm click trong một profile.

2. **Pixel probe**
   - Đọc màu RGB tại `(x, y)` trong client area.
   - Trả về mã hex, RGB và phân loại màu giống FFZik:
     - `0`: trắng/xám
     - `1`: xanh dương
     - `2`: vàng
     - `3`: màu khác/đen
   - Hỗ trợ tolerance, đọc lặp (watch), so khớp màu mong đợi.

3. **Text probe**
   - Đọc title của cửa sổ game.
   - Scan memory của process game để tìm chuỗi text đang tồn tại trong RAM.
   - Chụp snapshot và so sánh diff giữa hai thời điểm.
   - Lọc, tìm kiếm, export kết quả.

### 1.2. Tiêu chí thành công của MVP

- Attach đúng cửa sổ FC Online, hiển thị đúng PID, class, title, client size, DPI.
- Click đúng tọa độ, game phản hồi, chuột thật không di chuyển.
- Đọc đúng RGB tại một pixel; phân loại màu đúng với dữ liệu đối chiếu.
- Scan memory tìm được ít nhất một chuỗi đang hiển thị trên UI game.
- Diff phát hiện được chuỗi mới/xoá khi UI thay đổi.
- Tool không ghi memory, không inject, không crash khi gặp vùng memory không đọc được.
- F12 dừng khẩn cấp mọi tác vụ đang chạy.

### 1.3. Không nằm trong phạm vi MVP

- Auto chơi trận, auto chiến thuật, auto chuyển account.
- OCR vùng client.
- Đọc địa chỉ/pointer chain đã reverse-engineer.
- Bypass anti-cheat, ẩn process, chống phát hiện.
- Ghi memory, inject DLL, tạo remote thread, driver input.
- Hỗ trợ fullscreen exclusive.

Các mục trên có thể làm ở giai đoạn sau nếu cần.

---

## 2. Nguyên tắc an toàn

1. **Read-only:** chỉ dùng `PROCESS_QUERY_INFORMATION | PROCESS_VM_READ`, không cấp quyền ghi.
2. **Không né tránh anti-cheat:** không dùng `NtReadVirtualMemory` để lách hook, không obfuscate API, không ẩn tool. Nếu game chặn thì ghi nhận lỗi và dừng.
3. **Không chạy khi đang thi đấu:** chỉ test ở màn hình login/lobby với account test.
4. **Dừng ngay khi có dấu hiệu bất thường:** game crash, mất kết nối, dialog anti-cheat, lag bất thường.
5. **Giới hạn tần suất:** click tối thiểu cách nhau 20 ms; pixel watch tối thiểu 50 ms; full memory scan không lặp liên tục.
6. **Kiểm tra target trước mỗi thao tác:** HWND còn hợp lệ, đúng process, cửa sổ không minimized, tọa độ nằm trong client area.
7. **Có công tắc dừng:** hotkey F12 huỷ mọi worker và vô hiệu hoá click.
8. **Không tự động chạy khi khởi động:** mọi hành động click phải do người dùng bấm Test/Start.

---

## 3. Kiến trúc

### 3.1. Sơ đồ tổng quát

```text
┌────────────────────────────────────────────────────┐
│ GameProbe.App (WinForms)                           │
│  Target bar │ Tabs: Click / Pixel / Text / Log     │
└───────────────────────┬────────────────────────────┘
                        │
┌───────────────────────▼────────────────────────────┐
│ GameProbe.Cli                                      │
│  window / click / pixel / mem scan / mem diff      │
└───────────────────────┬────────────────────────────┘
                        │
┌───────────────────────▼────────────────────────────┐
│ GameProbe.Core                                     │
│  WindowLocator        CoordinateResolver           │
│  ClickService         PixelProbe                   │
│  MemoryScanner        TextExtractor                │
│  SnapshotStore        ProfileStore                 │
│  ActionLogger         HotkeyManager                │
└───────────────────────┬────────────────────────────┘
                        │ P/Invoke
┌───────────────────────▼────────────────────────────┐
│ user32.dll / kernel32.dll / gdi32.dll              │
└────────────────────────────────────────────────────┘
```

### 3.2. Cấu trúc solution

```text
tools/GameProbe/
├─ GameProbe.sln
├─ Directory.Build.props
├─ src/
│  ├─ GameProbe.Core/            # net8.0-windows, x64
│  │  ├─ Interop/                # NativeMethods, structs, constants
│  │  ├─ Windows/                # WindowLocator, GameWindow
│  │  ├─ Input/                  # CoordinateResolver, ClickService
│  │  ├─ Capture/                # PixelProbe
│  │  ├─ Memory/                 # MemoryScanner, TextExtractor, SnapshotStore
│  │  ├─ Profiles/               # ProfileStore
│  │  ├─ Diagnostics/            # ActionLogger, error mapping
│  │  └─ Models/                 # Profile, ClickPoint, PixelProbeSpec, ...
│  ├─ GameProbe.Cli/
│  └─ GameProbe.App/             # WinForms
├─ tests/
│  ├─ GameProbe.Tests/           # xUnit
│  └─ GameProbe.TestTarget/      # fixture nhỏ, chạy offline
├─ profiles/
│  └─ fc-online.json
└─ docs/
   ├─ usage.md
   └─ troubleshooting.md
```

### 3.3. Luồng hoạt động

1. Khởi động app/CLI, nạp profile JSON.
2. `WindowLocator` tìm HWND theo class/process; nếu không thấy thì báo lỗi hoặc cho chọn tay.
3. `GameWindow` đọc client rect, DPI, title, PID; bắt đầu timer kiểm tra cửa sổ còn sống.
4. Người dùng chọn point/pixel probe, bấm Test.
5. `CoordinateResolver` quy đổi tọa độ cấu hình → tọa độ client thực tế.
6. `ClickService` hoặc `PixelProbe` thực hiện thao tác trên worker thread.
7. Kết quả được ghi log và hiển thị lên UI.
8. Memory scan chạy worker riêng, có progress/cancel; kết quả có thể lưu snapshot.

### 3.4. Threading

| Worker | Số lượng | Ghi chú |
|---|---|---|
| UI thread | 1 | Không block bởi I/O hoặc scan |
| Click queue | 1 | Tuần tự, tránh chồng click |
| Pixel watcher | 1 / probe | Interval riêng, dừng khi tắt |
| Memory scanner | 1 | Semaphore: không cho 2 scan cùng lúc |
| Window health timer | 1 | 1 giây/lần, re-attach nếu cửa sổ đổi |

Mọi worker nhận `CancellationToken`; F12 huỷ tất cả.

---

## 4. Đặc tả module

### 4.1. WindowLocator / GameWindow

**Chức năng:**

- Liệt kê top-level window: HWND, class, title, PID, process name, client size, visible/minimized.
- Tìm theo thứ tự ưu tiên:
  1. `className` cấu hình (mặc định `FIFAKC` theo phân tích FFZik).
  2. `processName` cấu hình (mặc định `fczf.exe`).
  3. Title chứa chuỗi cấu hình.
  4. Người dùng chọn tay từ danh sách.
- Hiển thị thông tin chẩn đoán:
  - HWND, PID, process path.
  - Window class, title.
  - Client rect, window rect.
  - DPI (`GetDpiForWindow`).
  - Trạng thái minimized / hung.
- Kiểm tra lại mỗi giây; nếu HWND chết thì thử tìm lại theo tiêu chí cũ.

**API chính:** `FindWindowW`, `FindWindowExW`, `EnumWindows`, `GetClassNameW`, `GetWindowTextW`, `GetWindowThreadProcessId`, `GetClientRect`, `GetWindowRect`, `IsWindow`, `IsIconic`, `IsHungAppWindow`, `QueryFullProcessImageNameW`.

**Lưu ý:**

- Không hard-code duy nhất class `FIFAKC`; game update có thể đổi class hoặc có nhiều cửa sổ.
- Nếu nhiều cửa sổ khớp, chọn cửa sổ visible có client area lớn nhất và cho người dùng xác nhận.

### 4.2. CoordinateResolver

Ba chế độ tọa độ:

| Mode | Công thức | Dùng khi |
|---|---|---|
| `client` | `x = valueX`, `y = valueY` | Tọa độ cố định, giống FFZik `(568, 124)` |
| `percent` | `x = round(width * pctX) + offsetX` | UI co giãn theo cửa sổ |
| `anchor` | neo `left/center/right` + offset, neo `top/center/bottom` + offset | Giống FFZik `centerX + 30`, `height - 67` |

Quy tắc:

- Tọa độ luôn là **client coordinates** (gốc `(0,0)` ở góc trên-trái client area).
- `anchor center`: `x = width / 2 + offsetX`.
- `anchor right`: `x = width + offsetX` (offset thường âm).
- `anchor bottom`: `y = height + offsetY` (offset thường âm).
- Clamp vào `[0, width-1]`, `[0, height-1]`; log cảnh báo nếu point nằm ngoài.
- Nếu cửa sổ minimized hoặc client size bằng 0 → từ chối thao tác.
- Hotkey F8: lấy `GetCursorPos` → `ScreenToClient` → tạo point mới với mode `client`.

### 4.3. ClickService

**Hành vi mặc định (giống FFZik):**

1. Kiểm tra `IsWindow(hwnd)`.
2. (Tuỳ chọn) gửi `WM_MOUSEMOVE` trước để game cập nhật hover.
3. Gửi `WM_*BUTTONDOWN` với `lParam = MAKELPARAM(x, y)`.
4. Chờ `holdMs` (mặc định 50 ms).
5. Gửi `WM_*BUTTONUP`.
6. Nếu `count > 1`, chờ `intervalMs` rồi lặp.

**Message map:**

| Button | Down | Up | wParam |
|---|---|---|---|
| Left | `WM_LBUTTONDOWN` `0x0201` | `WM_LBUTTONUP` `0x0202` | `MK_LBUTTON` `0x0001` |
| Right | `WM_RBUTTONDOWN` `0x0204` | `WM_RBUTTONUP` `0x0205` | `MK_RBUTTON` `0x0002` |
| Middle | `WM_MBUTTONDOWN` `0x0207` | `WM_MBUTTONUP` `0x0208` | `MK_MBUTTON` `0x0010` |

**Chế độ gửi:**

| Mode | API | Ưu điểm | Nhược điểm |
|---|---|---|---|
| `post` (mặc định) | `PostMessageW` | Không block, không cần focus | Một số game bỏ qua message |
| `send` | `SendMessageTimeoutW` + `SMTO_ABORTIFHUNG` | Game xử lý đồng bộ | Có thể chậm/treo nếu timeout |
| `sendInput` (troubleshoot) | `SetForegroundWindow` + `SendInput` | Hoạt động với game đọc raw input | Di chuyển chuột thật, cần focus, rủi ro cao |

Quy tắc an toàn:

- `sendInput` mặc định bị tắt; muốn dùng phải bật rõ trong profile và xác nhận cảnh báo.
- Không gọi `SetActiveWindow`/`SetForegroundWindow` trong mode `post` (giống FFZik, các call này thường thất bại cross-process và không cần thiết).
- Nếu `PostMessageW` trả về 0, log `GetLastError`.
- Rate limit: tối thiểu 20 ms giữa hai lần click.
- Click queue tuần tự; không cho click chồng khi đang chạy sequence.
- Không click nếu cửa sổ minimized hoặc tọa độ ngoài client area.

### 4.4. PixelProbe

**Thuật toán (mô phỏng FFZik RVA `0x1EE6C0`):**

1. Kiểm tra cửa sổ hợp lệ và không minimized.
2. `GetDC(hwnd)`.
3. Tạo memory DC + bitmap 1×1.
4. `BitBlt(memDC, 0, 0, 1, 1, dc, x, y, SRCCOPY)`.
5. `GetDIBits` với `DIB_RGB_COLORS`, bitmap 32bpp.
6. Trả về `R, G, B`, hex `#RRGGBB`.
7. Giải phóng DC/bitmap.

**Phân loại màu:**

| Class | Điều kiện (có tolerance) | Ý nghĩa |
|---|---|---|
| `0` | `|R-G| <= tol`, `|G-B| <= tol`, `R > 0` | trắng/xám |
| `1` | `|R-G| <= tol`, `B - G > tol` | xanh dương |
| `2` | `|R-G| <= tol`, `G - B > tol` | vàng |
| `3` | còn lại | màu khác/đen |

- `tolerance` mặc định 8, cấu hình được.
- Trả thêm `IsExactMatch` khi so với `expectedRgb`.
- Hỗ trợ lấy mẫu nhiều pixel quanh tâm (3×3 hoặc 5×5) và trả về trung bình/mode nếu bật.
- Watch mode: poll `watchIntervalMs` (tối thiểu 50 ms), log mỗi lần RGB hoặc class thay đổi.
- Fallback: nếu BitBlt lỗi hoặc trả đen bất thường, thử `GetPixel`; nếu vẫn lỗi, báo rõ nguyên nhân.

**Giới hạn:**

- Minimized: không đọc được.
- Fullscreen exclusive: BitBlt có thể trả đen; yêu cầu windowed/borderless.
- Cửa sổ bị che một phần: DC của window thường vẫn trả nội dung, nhưng có thể khác tuỳ game; ghi nhận khi test.

### 4.5. MemoryScanner / TextExtractor

#### 4.5.1. Mở process

- `OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, pid)`.
- Quyền `0x0410`.
- Nếu thất bại:
  - `ERROR_ACCESS_DENIED (5)`: báo cần chạy admin hoặc game/anti-cheat đang chặn.
  - `ERROR_INVALID_PARAMETER (87)`: process đã thoát.
- Xác định target 32-bit hay 64-bit bằng `IsWow64Process2` để xử lý pointer size khi cần.

#### 4.5.2. Liệt kê vùng memory

Dùng `VirtualQueryEx`:

- Chỉ lấy `MEM_COMMIT`.
- Bỏ `PAGE_NOACCESS`, `PAGE_GUARD`.
- Mặc định chỉ lấy `MEM_PRIVATE` (heap, buffer UI).
- Tuỳ chọn thêm `MEM_IMAGE` và `MEM_MAPPED`.
- Bỏ region có `RegionSize = 0`.
- Ghi lại `BaseAddress`, `RegionSize`, `Protect`, `Type` để hiển thị và debug.

#### 4.5.3. Đọc và trích chuỗi

- Đọc theo chunk 1–4 MB bằng `ReadProcessMemory`.
- Xử lý partial read: nếu đọc được `n > 0` byte thì xử lý phần đã đọc rồi chuyển tiếp.
- Giữ overlap 4 KB giữa hai chunk để không cắt chuỗi ở ranh giới.
- Trích xuất:
  - **UTF-16LE:** cặp byte thành ký tự Unicode; chấp nhận chữ, số, dấu câu, khoảng trắng; loại control char (trừ tab/xuống dòng); loại surrogate lỗi.
  - **UTF-8/ASCII:** run byte printable; decode UTF-8 có kiểm tra hợp lệ.
- Điều kiện nhận:
  - Độ dài >= `minLength` (mặc định 4).
  - Tỷ lệ ký tự hợp lệ >= 90%.
  - Có ít nhất 2 ký tự chữ/số.
- Dedupe theo `(address, string)`; giới hạn `maxResults` (mặc định 20.000) để tránh tràn RAM UI.
- Mỗi kết quả gồm: `address`, `string`, `encoding`, `length`, `regionType`, `protect`, `lastSeen`.
- Hỗ trợ filter `contains` (case-insensitive) và filter theo encoding/độ dài.

#### 4.5.4. Snapshot và diff

- Snapshot lưu JSON:
  - metadata: timestamp, PID, process path, window title, client size.
  - danh sách string kèm address/encoding.
- Diff so sánh hai snapshot:
  - `Added`: chuỗi chỉ có ở snapshot mới.
  - `Removed`: chuỗi chỉ có ở snapshot cũ.
  - `Changed`: cùng address nhưng nội dung khác.
- UI hiển thị 3 nhóm riêng; cho phép export JSON/CSV.
- Không tự động scan lại liên tục; người dùng bấm Scan hoặc bật interval tối thiểu 30 giây.

#### 4.5.5. Title

- Đọc title bằng `SendMessageW(hwnd, WM_GETTEXTLENGTH)` + `WM_GETTEXT` (giống FFZik mục 11.1), fallback `GetWindowTextW`.
- Poll mỗi 500 ms khi tab Text đang mở; log khi title đổi.
- Title là nguồn text rẻ và ổn định nhất; luôn hiển thị cùng kết quả scan.

#### 4.5.6. Hướng mở rộng (chưa làm trong MVP)

- **Known-address watch:** nhập address hoặc module + offset, kiểu dữ liệu `utf16`, `utf8`, `fixed-length`; poll định kỳ.
- **Pointer chain:** hỗ trợ chain offset cho địa chỉ thay đổi sau restart.
- **OCR:** chụp client area, dùng Windows.Media.Ocr hoặc Tesseract, hiển thị text nhìn thấy trên màn hình.
- **Neighborhood view:** xem các chuỗi xung quanh ±4 KB của một địa chỉ mục tiêu.

Các mục này được thiết kế sau `ITextSource`, không ảnh hưởng scanner hiện tại.

### 4.6. ProfileStore

- Định dạng JSON, có `schemaVersion`.
- Vị trí:
  - Dev: `profiles/` trong repo.
  - Bản phát hành: `%AppData%\GameProbe\profiles\`.
- Validate khi load:
  - Tọa độ hợp lệ.
  - Button/mode hợp lệ.
  - ID không trùng.
  - Giá trị delay trong khoảng cho phép.
- Lưu profile nguyên tử (ghi file tạm rồi replace) để tránh hỏng file.
- Cho phép export/import profile.

### 4.7. CLI

Lệnh dự kiến:

```text
gameprobe window list
gameprobe window info --hwnd 0x00123ABC
gameprobe click --profile fc-online.json --point play
gameprobe pixel --x 568 --y 124 --json
gameprobe pixel watch --profile fc-online.json --probe play-color
gameprobe mem scan --pid 1234 --min-len 4 --filter "FC" --out scan.json
gameprobe mem diff --before scan-a.json --after scan-b.json
gameprobe profile validate --file fc-online.json
```

Quy ước:

- `--json` trả kết quả máy đọc được.
- Exit code:
  - `0`: thành công.
  - `2`: không tìm thấy cửa sổ/process.
  - `3`: access denied.
  - `4`: tọa độ ngoài client area.
  - `5`: timeout hoặc message send thất bại.
  - `6`: scan bị huỷ.
- CLI không tự động click nếu thiếu tham số rõ ràng.

### 4.8. GUI

Bố cục đề xuất:

- **Target bar:** dropdown cửa sổ, nút Refresh, Attach, thông tin PID/class/size/DPI.
- **Tab Click:**
  - Bảng point: tên, tọa độ cấu hình, tọa độ thực tế, button, count, mode.
  - Nút Capture cursor (F8), Test (F9), Add/Delete.
  - Hiển thị cảnh báo nếu point ngoài client area.
- **Tab Pixel:**
  - Nhập x/y hoặc chọn probe.
  - Nút Read (F10), swatch màu, RGB/hex/class.
  - Bật/tắt watch, interval, lịch sử thay đổi.
  - Cấu hình tolerance và expected class/RGB.
- **Tab Text:**
  - Title hiện tại.
  - Nút Scan, Cancel, progress, thời gian, số region/byte.
  - Bảng kết quả: address, string, encoding, length, region.
  - Filter, min length, encoding, private-only.
  - Snapshot, Diff, Export.
- **Tab Log:**
  - Log hành động, lỗi Win32, thời gian.
  - Nút mở thư mục log.
- **Settings:** hotkey, rate limit, logging level, chế độ click mặc định.
- **Status bar:** trạng thái attach, worker đang chạy, cảnh báo admin.

### 4.9. Logging / Diagnostics

- Log JSONL tại `%LocalAppData%\GameProbe\logs\`.
- Mỗi entry: timestamp, action, hwnd, pid, point/probe id, tọa độ thực tế, kết quả, error code.
- Log không ghi nội dung memory đầy đủ mặc định; chỉ ghi chuỗi khi người dùng export snapshot để tránh lộ dữ liệu.
- Chụp screenshot khi lỗi (tuỳ chọn) để debug pixel/click.
- Mapping mã lỗi Win32 sang thông báo tiếng Việt dễ hiểu.

---

## 5. Định dạng cấu hình

Ví dụ profile FC Online:

```json
{
  "schemaVersion": 1,
  "name": "FC Online",
  "target": {
    "className": "FIFAKC",
    "processName": "fczf.exe",
    "titleContains": ""
  },
  "clickPoints": [
    {
      "id": "garena-login",
      "name": "Garena login",
      "x": { "mode": "client", "value": 568 },
      "y": { "mode": "client", "value": 124 },
      "button": "left",
      "count": 1,
      "holdMs": 50,
      "intervalMs": 200,
      "syncMode": "post",
      "hover": true,
      "wParamMode": "button"
    },
    {
      "id": "play",
      "name": "Play button",
      "x": { "mode": "anchor", "anchor": "center", "offset": 30 },
      "y": { "mode": "anchor", "anchor": "bottom", "offset": -67 },
      "button": "left",
      "count": 1,
      "holdMs": 50,
      "intervalMs": 200,
      "syncMode": "post",
      "hover": true,
      "wParamMode": "button"
    }
  ],
  "pixelProbes": [
    {
      "id": "play-color",
      "name": "Play button state",
      "x": { "mode": "anchor", "anchor": "center", "offset": 30 },
      "y": { "mode": "anchor", "anchor": "bottom", "offset": -67 },
      "expectedClass": 1,
      "expectedRgb": "#00A0FF",
      "tolerance": 8,
      "watchIntervalMs": 200
    }
  ],
  "memory": {
    "minLength": 4,
    "encodings": ["utf16le", "utf8"],
    "regionTypes": ["private"],
    "maxResults": 20000,
    "filter": ""
  },
  "hotkeys": {
    "captureCursor": "F8",
    "testClick": "F9",
    "readPixel": "F10",
    "memorySnapshot": "F11",
    "panic": "F12"
  },
  "logging": {
    "level": "info",
    "screenshotOnError": false
  }
}
```

Quy ước:

- `wParamMode`: `button` (gửi `MK_*`) hoặc `none` (gửi 0), vì một số game cần một trong hai.
- `syncMode`: `post`, `send`, `sendInput`.
- `anchor`: `left`, `center`, `right` cho X; `top`, `center`, `bottom` cho Y.
- Profile mặc định chỉ dùng `post` và `wParamMode: button`.

---

## 6. Kế hoạch triển khai

Ước lượng cho 1 người, gồm test trên FC Online.

| Phase | Nội dung | Thời lượng | Đầu ra |
|---|---|---|---|
| P0 | Khung dự án, manifest admin, DPI, logging, model/profile | 0.5 ngày | Solution build được, app/CLI khởi động |
| P1 | Window locator + attach + thông tin cửa sổ | 0.5 ngày | `window list/info` hoạt động trên FC Online |
| P2 | CoordinateResolver + ClickService + PixelProbe + CLI | 1.0 ngày | Click/pixel chạy được, có unit test |
| P3 | MemoryScanner + TextExtractor + snapshot/diff + CLI | 1.5 ngày | Scan/diff chạy được, có test |
| P4 | WinForms GUI + hotkey + log viewer | 1.0 ngày | Dùng được đầy đủ qua GUI |
| P5 | Tích hợp và hiệu chuẩn trên FC Online | 1.0 ngày | Profile FC Online + biên bản test |
| P6 | Đóng gói, README/usage/troubleshooting | 0.5 ngày | Bản single-file + tài liệu |
| **Tổng** |  | **~6 ngày** | MVP hoàn chỉnh |

### P0 — Khung dự án

- [x] Tạo solution, 3 project `Core`, `Cli`, `App`, project test và fixture.
- [x] `Directory.Build.props`: `net8.0-windows`, x64, nullable, warning level.
- [x] `app.manifest`: `requireAdministrator`, PerMonitorV2.
- [x] Logging skeleton, model `Profile`, `ClickPoint`, `PixelProbeSpec`.
- [x] `ProfileStore` load/save/validate.
- [x] CLI parser (parser đơn giản, không dependency).
- [x] README ghi rõ mục đích nghiên cứu và cảnh báo ToS.

**Done khi:** app và CLI chạy, load/validate được profile mẫu, log được tạo. — **Đã xong.**

### P1 — Attach cửa sổ

- [x] `NativeMethods` cho user32/kernel32 cần thiết.
- [x] `WindowLocator` liệt kê cửa sổ và process.
- [x] `GameWindow` đọc class/title/PID/client rect/DPI.
- [x] Tự tìm theo class/process; fallback chọn tay qua `--hwnd`.
- [x] Timer kiểm tra cửa sổ còn sống (`GameWindowMonitor`).
- [x] CLI `window list`, `window info`.

**Done khi:** chạy trên FC Online, xác định đúng HWND, PID, class, client size, DPI.

**Kết quả nghiệm thu (2026-11-21):** `fczf.exe`, class `FIFAKC`, title `FC ONLINE`,
HWND `0x1506CA`, PID `15732`, client `1280x720`, DPI `96`, window rect
`(0,0) 1296x759`. Cả tìm theo class và process đều khớp đúng 1 cửa sổ.
**Đã xong P1.**

### P2 — Click và pixel

- [ ] `CoordinateResolver` cho `client`, `percent`, `anchor`.
- [ ] Hotkey F8 capture cursor → point.
- [ ] `ClickService` với `post`/`send`; `sendInput` để sau flag.
- [ ] `PixelProbe` đọc 1×1, phân loại màu, tolerance.
- [ ] CLI `click`, `pixel`, `pixel watch`.
- [ ] Rate limit, kiểm tra biên, log lỗi.
- [ ] Fixture offline: panel vẽ lưới màu + log mouse message.
- [ ] Unit test: coordinate math, color classify, message mapping.

**Done khi:** click đúng trên fixture; pixel đọc đúng RGB; trên FC Online, click vào một point an toàn có phản hồi và pixel đọc được.

### P3 — Memory text

- [ ] `OpenProcess` + xử lý lỗi quyền.
- [ ] `VirtualQueryEx` + filter region.
- [ ] Đọc chunk, overlap, partial read.
- [ ] `TextExtractor` UTF-16LE/UTF-8, min length, printable filter.
- [ ] Kết quả, dedupe, giới hạn, progress/cancel.
- [ ] Title reader.
- [ ] Snapshot JSON + diff.
- [ ] CLI `mem scan`, `mem diff`.
- [ ] Unit test: extractor với tiếng Việt, surrogate pair, ranh giới chunk; test trên fixture.

**Done khi:** scan fixture tìm đúng chuỗi cắm sẵn; trên FC Online tìm được ít nhất một chuỗi visible; diff hoạt động.

### P4 — GUI

- [ ] Target bar + window picker.
- [ ] Tab Click: bảng point, add/delete/test/capture.
- [ ] Tab Pixel: read/watch, swatch, history.
- [ ] Tab Text: title, scan/cancel/progress, filter, snapshot/diff/export.
- [ ] Tab Log.
- [ ] Settings + hotkey F9/F10/F11/F12.
- [ ] Trạng thái worker, cảnh báo ngoài biên/minimized.

**Done khi:** thực hiện cả 3 chức năng hoàn toàn qua GUI, không cần CLI.

### P5 — Tích hợp FC Online

- [ ] Tạo profile `fc-online.json` từ tọa độ thực tế.
- [ ] Chạy checklist test mục 7.
- [ ] Ghi lại kết quả, ảnh chụp, log.
- [ ] Điều chỉnh tolerance, anchor, delay, wParamMode nếu cần.
- [ ] Ghi rõ điểm nào hoạt động, điểm nào game bỏ qua.

**Done khi:** checklist mục 7 pass hoặc có biên bản lý do không pass; profile được lưu.

### P6 — Đóng gói và tài liệu

- [ ] Single-file publish `win-x64`.
- [ ] README, `usage.md`, `troubleshooting.md`.
- [ ] Hướng dẫn tạo point bằng F8, đọc pixel, scan/diff.
- [ ] Cảnh báo anti-cheat/ToS và giới hạn kỹ thuật.
- [ ] Zip bản phát hành kèm profile mẫu.

---

## 7. Test plan trên FC Online

### 7.1. Chuẩn bị

1. Dùng **account test**, không dùng account chính.
2. Chạy game ở chế độ **windowed hoặc borderless 16:9**, không dùng fullscreen exclusive.
3. Đặt độ phân giải cố định trong suốt bộ test (ví dụ 1280×720 hoặc độ phân giải native).
4. Tắt overlay không cần thiết (Discord, NVIDIA, OBS) để tránh ảnh hưởng pixel.
5. Chạy GameProbe bằng quyền Administrator.
6. Mở `gameprobe window list` hoặc GUI để xác nhận:
   - Process `fczf.exe`.
   - Class cửa sổ (kỳ vọng `FIFAKC`, nhưng phải xác minh runtime).
   - Client size và DPI.
7. Tạo profile ban đầu với:
   - Một point an toàn, có tác dụng đảo ngược (mở/đóng menu hoặc tab).
   - Một pixel tại vùng màu ổn định.
8. Chuẩn bị công cụ đối chiếu:
   - Spy++ hoặc Window Detective để xem message.
   - Công cụ screenshot/color picker để đối chiếu RGB.
   - Thư mục lưu log và snapshot.

### 7.2. Thứ tự test (quan trọng)

Test read-only trước, click sau, memory scan sau cùng.

| ID | Test | Cách làm | Kỳ vọng |
|---|---|---|---|
| T1 | Attach | Chạy `window list`, attach game | Đúng PID/class/title/client size/DPI |
| T2 | Re-attach | Thoát game, mở lại, đăng nhập | Tool tự tìm lại HWND hoặc báo rõ |
| T3 | Pixel read | Chọn pixel ở vùng UI ít đổi màu | RGB/hex hiển thị; đối chiếu ảnh chụp khớp |
| T4 | Pixel classification | Test các điểm trắng/xám, xanh, vàng, đen | Class 0/1/2/3 đúng quy tắc |
| T5 | Pixel watch | Hover/click làm đổi màu một nút | Log transition đúng, không spam |
| T6 | Click foreground | Click point an toàn khi game đang foreground | UI phản hồi đúng, cursor không di chuyển |
| T7 | Click background | Chuyển focus sang Notepad, click lại | UI game vẫn phản hồi nếu PostMessage hoạt động |
| T8 | Click repeat | 10 click, interval 200 ms | Đủ 10, không dư, không treo |
| T9 | Message verify | Chạy Spy++ song song | Thấy `WM_LBUTTONDOWN/UP` đúng tọa độ |
| T10 | Title | Đổi màn hình/title nếu có | Title cập nhật, log đúng |
| T11 | Memory scan | Scan với `minLength=4`, private-only | Tìm được chuỗi visible, thời gian hợp lý |
| T12 | Snapshot/diff | Snapshot → mở menu → snapshot | Diff hiện chuỗi mới/xoá |
| T13 | Cancel | Bắt đầu scan rồi bấm Cancel/F12 | Dừng trong ~1 giây, không crash |
| T14 | Minimize | Minimize game, thử click/pixel | Tool từ chối, báo lỗi rõ |
| T15 | DPI | Đổi 100% ↔ 125%, lặp T1/T3/T6 | Tọa độ vẫn đúng |
| T16 | Restart | Khởi động lại app, load profile | Profile và hotkey hoạt động |

### 7.3. Cách xác minh từng phần

**Click:**

- Dùng Spy++ gắn vào HWND game, lọc mouse messages.
- Khi tool gửi click, phải thấy `WM_LBUTTONDOWN`, `WM_LBUTTONUP` với `lParam` đúng.
- Đồng thời quan sát UI game phản hồi.
- Kiểm tra `GetCursorPos` trước/sau để chắc chắn con trỏ không đổi.

**Pixel:**

- Chụp màn hình tại thời điểm test, đọc màu bằng công cụ ngoài.
- So sánh với RGB tool trả về; sai lệch cho phép do scaling/overlay.
- Test ở cửa sổ foreground và bị che một phần để ghi nhận khác biệt.

**Memory text:**

- Chọn chuỗi chắc chắn đang hiển thị: tên account, tên menu, thông báo lobby.
- Scan với filter tương ứng.
- Nếu không tìm thấy, thử:
  - Đổi encoding.
  - Bật include image/mapped.
  - Giảm minLength xuống 3.
  - Tìm chuỗi ngắn hơn hoặc ký tự ASCII.
- Ghi lại: có/không tìm thấy, số lượng kết quả, thời gian, mức nhiễu.
- Diff: mở/đóng menu, xem chuỗi nào xuất hiện/biến mất.

### 7.4. Điều kiện dừng ngay

Dừng toàn bộ test, đóng tool nếu:

- Game crash hoặc hiện dialog lỗi.
- Mất kết nối server bất thường.
- Anti-cheat hiện cảnh báo hoặc kick.
- Account bị logout ngoài ý muốn.
- CPU/RAM tăng bất thường kéo dài.
- Click/pixel hoạt động sai liên tục.

Sau khi dừng:

1. Lưu log, snapshot, ảnh chụp.
2. Không chạy lại ngay trong cùng phiên.
3. Ghi rõ hiện tượng vào biên bản.
4. Nếu nghi anti-cheat can thiệp, dừng tính năng memory scan trên game thật; chỉ tiếp tục pixel/click hoặc chuyển sang môi trường offline.

### 7.5. Bằng chứng cần lưu

- `window-info.txt`: class, title, PID, client size, DPI.
- `click-log.jsonl`: point, tọa độ, message, kết quả.
- `pixel-samples.csv`: tọa độ, RGB, class, ảnh đối chiếu.
- `scan-*.json`, `diff-*.json`: kết quả memory.
- Ảnh chụp màn hình trước/sau mỗi test click quan trọng.
- Biên bản test ghi rõ pass/fail/không xác định.

---

## 8. Acceptance criteria

### 8.1. Chức năng

- [ ] Attach được cửa sổ FC Online; hiển thị đúng thông tin.
- [ ] Tự re-attach sau khi game restart.
- [ ] Click đúng point cấu hình; game phản hồi; cursor thật không di chuyển.
- [ ] Click background hoạt động khi game không focus (nếu game chấp nhận PostMessage).
- [ ] Đọc pixel đúng RGB/hex; phân loại đúng class; tolerance hoạt động.
- [ ] Watch mode log đúng khi màu đổi.
- [ ] Đọc title đúng.
- [ ] Memory scan tìm được ít nhất một chuỗi visible trên FC Online.
- [ ] Snapshot/diff phát hiện thay đổi text khi UI đổi.
- [ ] Scan có progress, cancel được, không treo UI.
- [ ] F12 dừng mọi worker trong ~1 giây.

### 8.2. An toàn và chất lượng

- [ ] Chỉ mở process với quyền đọc.
- [ ] Không có lệnh ghi memory/inject trong codebase.
- [ ] Không crash khi region memory không đọc được.
- [ ] Không crash khi cửa sổ minimize/đóng giữa thao tác.
- [ ] Log đủ để tái hiện lỗi.
- [ ] Chạy ổn ở DPI 100% và 125%.
- [ ] Profile load/save/validate đúng.

### 8.3. Tài liệu

- [ ] README cảnh báo ToS/anti-cheat.
- [ ] Hướng dẫn tạo point bằng F8.
- [ ] Hướng dẫn đọc pixel và hiệu chỉnh tolerance.
- [ ] Hướng dẫn scan/diff và giới hạn của memory text.
- [ ] Troubleshooting: access denied, pixel đen, click không phản hồi, không tìm thấy text.

---

## 9. Rủi ro và phương án

| Rủi ro | Khả năng | Ảnh hưởng | Phương án |
|---|---|---|---|
| Anti-cheat chặn `OpenProcess` | Trung bình | Không scan được memory | Báo lỗi rõ; chỉ dùng title/pixel; không bypass |
| Anti-cheat phát hiện/flag account test | Thấp–trung bình | Account bị xử lý | Chỉ test account phụ; dừng ngay khi bất thường |
| `PostMessage` bị game bỏ qua | Trung bình | Click không tác dụng | Thử `SendMessageTimeout`; ghi nhận; tuỳ chọn `sendInput` có cảnh báo |
| Pixel BitBlt trả đen | Trung bình | Không đọc được màu | Dùng windowed/borderless; fallback `PrintWindow`; ghi nhận |
| Text không nằm plaintext trong RAM | Cao | Scan không tìm thấy | Diff/nhiều encoding; chuyển sang known-address hoặc OCR ở phase sau |
| Scan quá chậm/nhiều kết quả | Trung bình | UX kém | Private-only, minLength, filter, giới hạn kết quả, chunk hợp lý |
| Tọa độ lệch do DPI/độ phân giải | Trung bình | Click sai | Anchor mode, PerMonitorV2, F8 calibration, test nhiều DPI |
| `SendMessage` làm treo app | Thấp | App không phản hồi | `SendMessageTimeout`, worker thread, F12 |
| HWND đổi sau restart | Chắc chắn | Click sai target | Re-attach theo class/process; kiểm tra trước mỗi thao tác |
| Game update đổi class/UI | Trung bình | Tool không attach/click sai | Class cấu hình được; profile theo phiên bản; test lại |

---

## 10. Việc để sau MVP

1. **Known-address text source**
   - Nhập địa chỉ hoặc module + offset.
   - Hỗ trợ `utf16`, `utf8`, fixed-length, pointer chain.
   - Poll định kỳ, hiển thị chính xác text đang dùng.

2. **OCR client area**
   - Chụp client area.
   - Windows.Media.Ocr hoặc Tesseract.
   - So sánh/ bổ sung cho memory scan khi text không nằm plaintext.

3. **Neighborhood explorer**
   - Tìm chuỗi mục tiêu, xem các chuỗi xung quanh ±4 KB.
   - Hỗ trợ quá trình reverse-engineer offset.

4. **Screenshot picker**
   - Chụp client area, hiển thị ảnh, click chọn tọa độ trực quan.

5. **Sequence/scenario**
   - Kết hợp click → chờ pixel → click → đọc text.
   - Chỉ làm sau khi 3 primitive ổn định.

6. **Export/import sang Cheat Engine**
   - Xuất danh sách địa chỉ/chuỗi để hỗ trợ RE.

---

## 11. Quyết định mở

| Vấn đề | Hiện tại | Khi nào chốt |
|---|---|---|
| Có cần OCR ngay không? | Không, để sau MVP | Sau T11/T12 nếu memory scan thất bại |
| Có cần known-address ngay không? | Không, để sau MVP | Khi đã có offset từ RE |
| Có cần `sendInput` không? | Chỉ là tuỳ chọn troubleshoot | Sau T6/T7 nếu PostMessage thất bại |
| Có cần hỗ trợ fullscreen exclusive? | Không | Có thể dùng desktop capture ở phase sau |
| Có cần đa ngôn ngữ UI? | Không | Sau MVP |
| Có cần ký số executable? | Không bắt buộc | Khi phát hành cho người khác |

---

## 12. Thứ tự bắt tay khi chuyển sang implement

1. P0: dựng solution + manifest + profile + logging.
2. P1: `window list` trên FC Online, xác minh class/process thực tế.
3. P2: pixel read trước (read-only), sau đó click trên fixture, cuối cùng click trên FC Online.
4. P3: scan memory trên fixture, sau đó FC Online ở lobby.
5. P4: GUI.
6. P5: chạy checklist mục 7, lưu biên bản.
7. P6: đóng gói + tài liệu.

**Nguyên tắc:** mỗi phase phải có bằng chứng test trước khi sang phase sau; không thêm tính năng mới khi checklist của phase hiện tại chưa đạt.
