# GameProbe

Công cụ nghiên cứu automation cửa sổ game trên Windows:
- click tại tọa độ cấu hình sẵn bằng window message;
- đọc màu một pixel / chụp và so sánh vùng ảnh nhỏ;
- kịch bản làm mới giá mua cầu thủ bằng pixel, không OCR.

> **Cảnh báo:** kỹ thuật gửi window message/automation vào game có thể vi phạm
> Điều khoản dịch vụ và bị anti-cheat phát hiện. Chỉ dùng cho nghiên cứu, học tập
> và account test. Không dùng để gian lận.

> **P3 memory text** nằm ở branch riêng `feat/gameprobe-p3`, không merge vào `main`:
> anti-cheat FC Online chặn `ReadProcessMemory` (Win32 5), nên hướng chính trên
> `main` là pixel/region.

Kế hoạch đầy đủ: [`../../docs/gameprobe-implementation-plan.md`](../../docs/gameprobe-implementation-plan.md)

---

## Trạng thái

**P2 — Click & pixel — đã xong.** Fixture pixel/click/watch pass; FC Online pixel pass;
click `missions-new` bằng CLI elevated đã làm mới UI game thành công.

**P5.1 — Kịch bản làm mới giá mua cầu thủ — đã implement, chờ test live.**
Dùng vùng pixel nhỏ quanh `Giá tối đa`; lần đầu lưu baseline, các lần sau so sánh;
khác baseline thì click giá mới + `Mua cầu thủ`. Mặc định dry-run, chỉ mua khi
có `--confirm-buy`. Code + unit test đã xong; chưa chạy live trên game.

Đã có:

- solution C# .NET 8 x64 với `Core`, `Cli`, `App`, test và TestTarget;
- manifest GUI chạy Administrator + PerMonitorV2 DPI;
- model profile và validator;
- `ProfileStore` load/save/validate JSON;
- logging skeleton (JSONL + console);
- CLI `profile validate`;
- `WindowLocator`, `GameWindow`, `GameWindowMonitor` (re-attach timer);
- CLI `window list` và `window info`;
- GUI skeleton 4 tab;
- unit test cho profile, validator, logger, window search và window locator;
- `CoordinateResolver` cho `client`, `percent`, `anchor`;
- `ClickService` với `post` và `send`, rate limit 20 ms, kiểm tra biên/minimized;
- capture tọa độ bằng F8 qua CLI `capture`;
- `PixelProbe` đọc 1×1 bằng BitBlt + GetDIBits, fallback GetPixel;
- phân loại màu FFZik 0..3, tolerance, expected class/RGB;
- `PixelWatcher` poll tối thiểu 50 ms, chỉ báo khi màu/class đổi;
- CLI `click`, `capture`, `pixel`, `pixel watch` (JSON/JSONL);
- `RegionResolver`, `GdiRegionSource` (BitBlt + desktop fallback), `RegionComparer`;
- `BmpWriter` lưu vùng chụp ra BMP để hiệu chỉnh tọa độ;
- CLI `region capture`, `region stability`;
- `PriceRefreshScenario` + CLI `scenario price-refresh` (dry-run/confirm-buy);
- profile `fc-online.json` có click points, probe, region và `priceRefresh`;
- fixture `GameProbe.TestTarget` vẽ lưới 6×4 màu, ô TOGGLE và log mouse message ra UI + file;
- profile `profiles/test-target.json` cho fixture.

### Kết quả P1 trên FC Online

| Thuộc tính | Giá trị đo được |
|---|---|
| Process | `fczf.exe` |
| Window class | `FIFAKC` |
| Title | `FC ONLINE` |
| HWND | `0x1506CA` (thay đổi mỗi phiên) |
| PID | `15732` (thay đổi mỗi phiên) |
| Client size | `1280x720` |
| Window rect | `(0,0) 1296x759` |
| DPI | `96` (100%) |

Cả `--class FIFAKC` và `--process fczf.exe` đều khớp đúng 1 cửa sổ.
Window rect lớn hơn client size do viền/title bar; click/pixel dùng client coordinates.

### Đã kiểm tra tự động

- `dotnet build -c Release`: 0 warning, 0 error.
- `dotnet test -c Release`: **114/114 pass**.
- Bao gồm: coordinate math, color classify, click message mapping, ClickService với fake transport,
  PixelProbe/PixelWatcher với fake source, GDI extraction trên memory DC,
  PostMessage/SendMessage trên hidden window, profile fixture;
  region comparer (exact/tolerance/minChangedPixels/alpha), GDI region extraction,
  region resolver, BMP writer, price-refresh state machine (dry-run/buy/same/timeout/guard)
  và profile validation cho `priceRefresh`.

**Bằng chứng fixture (2026-11-22):**

- Log `testtarget-mouse.log` xác nhận `WM_MOUSEMOVE`/`WM_LBUTTONDOWN`/`WM_LBUTTONUP`
  tại `(50,60)`, `(550,60)` và `(60,540)`.
- Click `toggle` đổi màu `#00A0FF` ↔ `#FFD700`; watch ghi 99 mẫu, 3 changes,
  chuỗi `#00A0FF → #FFD700 → #00A0FF`.

**Bằng chứng FC Online (2026-11-22):**

- Attach đúng `fczf.exe` / class `FIFAKC`, client `1280x720`, DPI `96`.
- Pixel `missions-new` tại `(1030,645)`: `#07F468`, class 3, khớp exact.
- Click `missions-new` bằng CLI **elevated** lúc `20:28:45`: `post`, hover=yes,
  HWND `0xB00F0`, PID `5276`, 73.6 ms → UI game làm mới nhiệm vụ thành công.
- Thử từ PowerShell không elevated bị `WM_MOUSEMOVE` `PostMessage` trả `Win32 5`
  (UIPI). Click vào game cần CLI Administrator; nếu elevated vẫn lỗi thì
  game/anti-cheat chặn message, không bypass.

### Việc còn lại cho P5.1/P5

- Chạy `region capture` + `region stability` trên FC Online để xác nhận tọa độ
  và exact match (popup đang mở).
- Chạy `scenario price-refresh --dry-run` 5–10 lần; nếu false positive thì
  tăng `minChangedPixels`/`channelTolerance` hoặc chỉnh region.
- Chỉ chạy `--confirm-buy` sau khi dry-run ổn định.
- Test nút play ở lobby và DPI 125% (T15) khi hiệu chuẩn profile.

`sendInput` chưa bật: profile có `syncMode: sendInput` sẽ bị từ chối rõ ràng.
P3 memory text nằm ở branch riêng, không dùng trên main.

## Yêu cầu

- Windows 10/11 x64
- .NET 8 SDK
- Chạy GUI bằng Administrator (manifest yêu cầu UAC)
- CLI click vào game nên chạy trong PowerShell **Run as administrator** (game/Garena thường elevated)

## Build & test

```powershell
dotnet build tools/GameProbe/GameProbe.sln -c Release
dotnet test  tools/GameProbe/GameProbe.sln -c Release
```

Log JSONL của CLI/GUI nằm trong `%LocalAppData%\GameProbe\logs\`.

## CLI

```powershell
# Kiểm tra profile
dotnet run --project tools/GameProbe/src/GameProbe.Cli -- profile validate --file tools/GameProbe/profiles/fc-online.json

# Liệt kê cửa sổ (read-only)
dotnet run --project tools/GameProbe/src/GameProbe.Cli -- window list

# Tìm cửa sổ FC Online theo class/process
dotnet run --project tools/GameProbe/src/GameProbe.Cli -- window info --class FIFAKC
dotnet run --project tools/GameProbe/src/GameProbe.Cli -- window info --process fczf.exe --json
```

Hoặc dùng bản build (ví dụ dưới đây chạy trong `tools/GameProbe`):

```powershell
$gp = "src\GameProbe.Cli\bin\Release\net8.0-windows\gameprobe.exe"

# FC Online — nút Nhiệm vụ mới (green #07F468, class 3 theo FFZik)
& $gp pixel --profile profiles\fc-online.json --probe missions-new-color
& $gp click --profile profiles\fc-online.json --point missions-new --dry-run
& $gp click --profile profiles\fc-online.json --point missions-new --json

# Point play để dành cho lobby có nút play xanh (P5)
& $gp click --profile profiles\fc-online.json --point play --dry-run

# Pixel theo tọa độ trực tiếp
& $gp pixel --x 568 --y 124 --class FIFAKC --tolerance 8 --expect-rgb "#00A0FF"

# Watch: chỉ in khi màu/class thay đổi, Ctrl+C để dừng
& $gp pixel watch --profile profiles\fc-online.json --probe missions-new-color --interval 200

# Capture tọa độ: đưa chuột lên điểm cần lấy rồi nhấn F8
& $gp capture --class FIFAKC --timeout 30

# P5.1 — chụp thử vùng Giá tối đa ra BMP để kiểm tra crop
& $gp region capture --profile profiles\fc-online.json --region buy-dialog-max-price --out price.bmp

# Kiểm tra vùng giá có ổn định pixel khi mở popup (chạy khi popup đang mở)
& $gp region stability --profile profiles\fc-online.json --region buy-dialog-max-price --count 10 --interval 200

# Dry-run: chỉ phát hiện giá đổi, chưa click Mua
& $gp scenario price-refresh --profile profiles\fc-online.json --dry-run

# Chạy thật sau khi dry-run pass
& $gp scenario price-refresh --profile profiles\fc-online.json --confirm-buy
```

Exit code:

| Code | Ý nghĩa |
|---|---|
| `0` | Thành công |
| `1` | Lỗi cú pháp hoặc profile không hợp lệ |
| `2` | Không tìm thấy file/cửa sổ |
| `3` | Không có quyền truy cập |
| `4` | Tọa độ ngoài client area |
| `5` | Timeout hoặc gửi message thất bại |
| `6` | Tác vụ bị huỷ (Ctrl+C) |
| `7` | Lỗi nội bộ/không mong muốn |

### Fixture offline (P2)

`GameProbe.TestTarget` vẽ lưới 6×4 ô 100×120 và log mọi mouse message nhận được.
Chạy fixture (người dùng tự mở app), sau đó:

```powershell
# Xác nhận fixture đang chạy
& $gp window info --process GameProbe.TestTarget

# Đọc màu các ô đã biết
& $gp pixel --profile profiles\test-target.json --probe white-color
& $gp pixel --profile profiles\test-target.json --probe blue-color
& $gp pixel --profile profiles\test-target.json --probe yellow-color
& $gp pixel --profile profiles\test-target.json --probe black-color
& $gp pixel --profile profiles\test-target.json --probe fc-blue-color

# Click vào ô trắng; log bên phải fixture phải có WM_MOUSEMOVE/DOWN/UP
& $gp click --profile profiles\test-target.json --point white

# Thử click khi fixture không focus (chuyển sang Notepad) — PostMessage vẫn tới
& $gp click --profile profiles\test-target.json --point blue

# Watch transition: cửa sổ 1 chạy watch ô TOGGLE, cửa sổ 2 click TOGGLE
& $gp pixel watch --profile profiles\test-target.json --probe toggle-color --duration 15
& $gp click --profile profiles\test-target.json --point toggle
```

Tâm ô: cột `0..5`, hàng `0..3`, tọa độ
`(cột*100 + 50, hàng*120 + 60)`. Profile dùng sẵn:
white `(50,60)`, black `(250,60)`, blue `(550,60)`, yellow `(50,180)`,
fc-blue `(50,300)`.
Ô `TOGGLE` ở `(60,540)`, đổi màu `#00A0FF` ↔ `#FFD700` mỗi lần click.

Fixture ghi mouse message vào cả panel bên phải và file
`%LocalAppData%\GameProbe\logs\testtarget-mouse.log`, nên có thể gửi log này
làm bằng chứng kể cả sau khi đóng fixture.

### P5.1 — Kịch bản làm mới giá mua cầu thủ

Mục tiêu: lặp `Đăng ký lại` → popup `Mua cầu thủ` → so vùng `Giá tối đa` với
baseline lần đầu. Giá chưa đổi thì `Huỷ` và lặp; giá đổi thì click giá mới,
click `Mua cầu thủ` và kết thúc. Không OCR, không đọc memory game.

Luồng:

1. Guard: đúng client `1280x720`, DPI 96, cửa sổ visible/không minimized;
   pixel chữ xanh trên nút `Đăng ký lại` phải khớp.
2. Click `relist` → chờ pixel đỏ của nút `Mua cầu thủ` → chờ popup ổn định
   (2 frame giống nhau) → chụp baseline vùng giá → click `Huỷ` → chờ popup đóng.
3. Lặp: click `relist` → chờ popup → chụp stable frame → so baseline.
   - Giống: `Huỷ`, chờ đóng, delay rồi lặp.
   - Khác: lưu baseline/current BMP; nếu dry-run thì dừng; nếu `--confirm-buy`
     thì click vùng giá mới → click `Mua cầu thủ` → kết thúc ngay.
4. Hết `maxAttempts` → exit code 5, không click mù.

Tọa độ hiện tại trong `profiles/fc-online.json` được suy ra từ ảnh chụp
1920×1080, giả định client origin `(8,31)` (khớp với point `missions-new` cũ):

| Mục | Client `(x,y)` | Ghi chú |
|---|---|---|
| `relist` | `(991,181)` | tâm nút `Đăng ký lại` hàng 1 |
| `relist-guard` | `(972,181)` | chữ xanh `#08F368`, tolerance 24 |
| `buy-dialog-red` | `(792,574)` | `#ED4428`; khi popup đóng là `#2C392B` |
| `buy-dialog-max-price` | `(942,285)` `96×26` | vùng crop `Giá tối đa` |
| `price-value` | `(1022,299)` | click vào giá trị sau khi đổi |
| `cancel-buy` | `(964,574)` | nút `Huỷ` |
| `buy` | `(830,574)` | nút `Mua cầu thủ` |

Quy trình kiểm tra trước khi mua thật:

```powershell
# 1. Mở popup, chụp thử vùng giá và xem BMP
& $gp region capture --profile profiles\fc-online.json --region buy-dialog-max-price --out price.bmp

# 2. Mở popup, kiểm tra vùng giá có ổn định pixel
& $gp region stability --profile profiles\fc-online.json --region buy-dialog-max-price --count 10 --interval 200

# 3. Dry-run nhiều lần, xác nhận không có false positive
& $gp scenario price-refresh --profile profiles\fc-online.json --dry-run

# 4. Chỉ khi dry-run ổn định mới chạy thật
& $gp scenario price-refresh --profile profiles\fc-online.json --confirm-buy
```

Lưu ý:

- Nếu window DC trả frame đen, tool fallback sang desktop capture. Khi đó phải
  giữ cửa sổ game **visible và không bị cửa sổ khác che**; nếu capture method
  đổi giữa baseline/current, scenario abort để tránh false positive.
- Exact match mặc định là 1 pixel khác = đổi. Nếu `region stability` báo dao động,
  tăng `priceRefresh.minChangedPixels` hoặc `channelTolerance` trong profile.
- `--confirm-buy` mua ngay khi phát hiện khác baseline, kể cả giá giảm; không
  kiểm tra hướng giá.
- Sau khi click `Mua cầu thủ`, scenario kết thúc luôn; không chờ dialog xác nhận.

### Phân loại màu

- `0`: trắng/xám — `R≈G≈B`, `R > 0`
- `1`: xanh dương — `R≈G`, `B > G`; có fallback cho xanh gradient kiểu `#00A0FF`
- `2`: vàng — `R≈G`, `B < G`
- `3`: còn lại/đen

`tolerance` mặc định 8. `expectedRgb` so khớp theo tolerance và có cờ `exact` khi trùng tuyệt đối.

## Cấu trúc

```text
tools/GameProbe/
├─ src/GameProbe.Core/
│  ├─ Input/       CoordinateResolver, RegionResolver, ClickService, CursorCapture
│  ├─ Capture/     PixelProbe, PixelWatcher, GdiRegionSource, RegionComparer
│  ├─ Scenario/    PriceRefreshScenario
│  ├─ Interop/     P/Invoke user32/kernel32/gdi32
│  ├─ Windows/     WindowLocator, GameWindow, monitor
│  ├─ Profiles/    model/profile store/validator
│  └─ Diagnostics/ logging, error mapping, BmpWriter
├─ src/GameProbe.Cli/      command line
├─ src/GameProbe.App/      WinForms GUI skeleton
├─ tests/GameProbe.Tests/  xUnit
├─ tests/GameProbe.TestTarget/  fixture lưới màu + mouse log
└─ profiles/
   ├─ fc-online.json
   └─ test-target.json
```

## Roadmap

| Phase | Nội dung |
|---|---|
| P0 | Khung dự án, profile, logging, CLI/GUI skeleton — **xong** |
| P1 | Attach cửa sổ, window info, re-attach — **xong** |
| P2 | CoordinateResolver, ClickService, PixelProbe, CLI click/pixel/capture, fixture — **xong** |
| P3 | MemoryScanner, TextExtractor, snapshot/diff — **branch `feat/gameprobe-p3`; game bị chặn Win32 5, không merge main** |
| P4 | GUI đầy đủ, hotkey F8–F12, log viewer |
| P5 | Test trên FC Online bằng account test |
| P5.1 | Region capture/comparer + scenario làm mới giá — **code/test xong; chờ dry-run live** |
| P6 | Đóng gói, tài liệu usage/troubleshooting |

## Giới hạn

- `sendInput` chưa bật; chỉ dùng `post`/`send`. Muốn dùng `sendInput` phải bổ sung sau
  với cảnh báo và xác nhận rõ ràng.
- Click vào game cần CLI chạy **Administrator** (game/Garena thường elevated).
  `PostMessage` trả `Win32 5` là UIPI/anti-cheat chặn; thử elevated trước, không bypass.
- BitBlt/GetPixel cần cửa sổ **windowed hoặc borderless**; fullscreen exclusive có thể trả đen.
- Region capture có fallback desktop; khi dùng fallback, cửa sổ game phải visible
  và không bị che.
- Pixel watch tối thiểu 50 ms; click sequence cách nhau tối thiểu 20 ms.
- Tọa độ là client coordinates tại DPI hiện tại; DPI/độ phân giải khác cần chỉnh profile.
- Capture F8 hiện có qua CLI; hotkey toàn cục trong GUI thuộc P4.
- Scenario chỉ so vùng giá; nếu UI đổi layout/font/scale, phải cập nhật region và
  chạy lại `region stability` trước khi mua.
- Không OCR nên không biết giá tăng/giảm; bất kỳ khác baseline nào cũng kích hoạt mua.
- P3 memory text không có trên `main`; nếu cần xem code, dùng branch
  `feat/gameprobe-p3`. Không chạy memory scan trên FC Online.

## Nguyên tắc an toàn

- Tool trên `main` chỉ đọc pixel/region và gửi window message; không đọc/ghi memory,
  không inject, không bypass anti-cheat.
- P3 memory (branch riêng) chỉ dùng `PROCESS_VM_READ`; không chạy trên FC Online.
- Chỉ test trên account phụ; không chạy khi đang thi đấu.
- Dừng ngay nếu game crash, mất kết nối hoặc anti-cheat cảnh báo.
