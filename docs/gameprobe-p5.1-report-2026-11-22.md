# Báo cáo phiên — GameProbe P5.1: làm mới giá mua cầu thủ bằng pixel region

> **Ngày:** 2026-11-22
> **Branch:** `main`
> **Commit:** `7e219dd` (code) + commit báo cáo này — `feat(gameprobe): add price refresh scenario with pixel region comparison`
> **Trạng thái:** code + unit test xong; dry-run live pass; đã fix hover-only và
> dry-run lại 5/5 khi không hover; chưa chạy `--confirm-buy`.

---

## 1. Tóm tắt

Phiên này thay hướng đọc memory (P3) bằng so sánh vùng ảnh nhỏ quanh `Giá tối đa`
trong popup `Mua cầu thủ`. Kịch bản lặp `Đăng ký lại`, so vùng giá với baseline
chụp lần đầu; nếu giống thì `Huỷ` và lặp, nếu khác thì click giá mới và
`Mua cầu thủ` rồi kết thúc. Không OCR, không đọc memory game.

Kết quả chính:

- Build Release: **0 warning, 0 error**.
- Unit test: **114/114 pass**.
- Live dry-run: **5/5 popup**, baseline/current cùng hash, **0 pixel khác**,
  capture method `BitBlt`, không false positive.
- Guard `relist-guard` đã chặn một lần chạy khi UI chưa sẵn sàng, không click mù.
- Code đã fast-forward vào `main`; branch `feat/price-refresh` đã xoá sau khi merge.
- Sau báo cáo ban đầu, phát hiện nút `Đăng ký lại` chỉ nhận click khi hàng đã
  hover; đã bổ sung hover pulse + guard poll và dry-run lại **5/5 popup khi
  chuột ngoài hàng** (chi tiết ở mục 14).

---

## 2. Bối cảnh

P3 dùng `ReadProcessMemory` để đọc text trong RAM đã được implement và test trên
branch `feat/gameprobe-p3` (`b7a471d`), nhưng khi chạy trên FC Online:

- `OpenProcess` và `VirtualQueryEx` thành công.
- **1206/1206 lần `ReadProcessMemory` trả Win32 5 (Access Denied)**, đọc được 0 byte.

Anti-cheat của game chặn đọc memory, nên hướng memory text không khả dụng.
Không bypass anti-cheat. P3 được giữ ở branch riêng và coi là dead code cho hướng
hiện tại. `main` chuyển sang pixel/region.

Yêu cầu nghiệp vụ đã chốt trong phiên:

1. Click `Mua cầu thủ` xong thì kết thúc luôn.
2. Lần đầu chụp vùng `Giá tối đa`, rồi `Huỷ`; các lần sau giống baseline thì
   `Huỷ` và lặp, khác thì click giá mới + `Mua cầu thủ`.
3. Cứ đổi giá là mua, không quan tâm tăng/giảm; **không OCR**.
4. Hàng `Đăng ký lại` luôn là hàng đầu; không cần kiểm tra tên player.
5. P3 nằm ở branch riêng; nhiều khả năng không bao giờ dùng lại.
6. Mặc định dry-run; chỉ mua thật khi có `--confirm-buy`.
7. Baseline chỉ lưu trong RAM cho run hiện tại.
8. Không commit `screenshots/` vì chứa thông tin account/số dư.

---

## 3. Thiết kế

### 3.1. So sánh pixel

- Chụp vùng client bằng `BitBlt` + `GetDIBits`, buffer BGRA top-down.
- So sánh trực tiếp byte RGB; **bỏ qua alpha**.
- Mặc định exact: `channelTolerance = 0`, `minChangedPixels = 1`.
- Có thể tăng tolerance/minChangedPixels nếu vùng giá dao động.
- Không dùng image subtraction, không OCR.

### 3.2. Baseline và vòng lặp

- Baseline chỉ giữ trong bộ nhớ của run hiện tại.
- Lần mở popup đầu tiên: chờ popup ổn định, chụp baseline, `Huỷ`.
- Các lần sau: chụp stable frame, so baseline.
  - Giống → `Huỷ`, chờ popup đóng, delay rồi lặp.
  - Khác → dry-run thì dừng; `--confirm-buy` thì click giá mới, click
    `Mua cầu thủ`, kết thúc ngay.

### 3.3. Phát hiện popup

- Dùng pixel đỏ cố định trên nút `Mua cầu thủ` (`#ED4428`, tolerance 16).
- Khi popup đóng, pixel này là màu tối `#2C392B`.
- Không dò cửa sổ con, không phụ thuộc title.

### 3.4. An toàn

- Dry-run là mặc định.
- `--confirm-buy` và `--dry-run` không được dùng cùng lúc.
- Chỉ click khi guard/popup hợp lệ; hết timeout thì báo lỗi, không click mù.
- Nếu capture method đổi giữa baseline và current thì abort để tránh false positive.

---

## 4. Thay đổi code

### 4.1. Core

| File | Nội dung |
|---|---|
| `src/GameProbe.Core/Models/RegionSpec.cs` | Model vùng client `x/y/width/height`. |
| `src/GameProbe.Core/Models/PriceRefreshOptions.cs` | Cấu hình scenario: point/probe/region, timeout, tolerance, guard kích thước/DPI. |
| `src/GameProbe.Core/Input/RegionResolver.cs` | Resolve region sang client pixel, kiểm tra biên. |
| `src/GameProbe.Core/Input/ResolvedRegion.cs` | Kết quả resolve region. |
| `src/GameProbe.Core/Capture/IRegionSource.cs` | Interface nguồn chụp vùng. |
| `src/GameProbe.Core/Capture/RegionCaptureResult.cs` | Buffer BGRA + hash + method + timestamp. |
| `src/GameProbe.Core/Capture/RegionCaptureMethod.cs` | `BitBlt` / `DesktopBitBlt`. |
| `src/GameProbe.Core/Capture/GdiRegionSource.cs` | BitBlt + GetDIBits, fallback desktop khi window DC đen, kiểm tra bị che. |
| `src/GameProbe.Core/Capture/RegionComparer.cs` | So RGB exact/tolerance, đếm pixel đổi, bounding box. |
| `src/GameProbe.Core/Diagnostics/BmpWriter.cs` | Ghi BMP 32-bit để kiểm tra crop. |
| `src/GameProbe.Core/Scenario/PriceRefreshScenario.cs` | State machine chính + guard + timeout + retry. |
| `src/GameProbe.Core/Scenario/ISleeper.cs` | Sleep có cancellation để test. |
| `src/GameProbe.Core/Scenario/PriceRefreshOutcome.cs` | `DryRunChanged`, `Bought`, `MaxAttemptsReached`. |
| `src/GameProbe.Core/Scenario/PriceRefreshResult.cs` | Kết quả run + baseline/current/diff. |
| `src/GameProbe.Core/Profiles/ProfileValidator.cs` | Validate region, priceRefresh, reference, threshold. |

### 4.2. CLI

| File | Nội dung |
|---|---|
| `Commands/RegionCaptureCommand.cs` | `region capture` — chụp vùng ra BMP. |
| `Commands/RegionStabilityCommand.cs` | `region stability` — chụp N frame, so frame đầu. |
| `Commands/RegionOptions.cs` | Parse option chung cho hai lệnh region. |
| `Commands/PriceRefreshCommand.cs` | `scenario price-refresh` — dry-run/confirm-buy, lưu BMP, JSON. |
| `Commands/HelpText.cs` | Help cho region/scenario. |
| `Program.cs` | Dispatch `region` và `scenario`. |

### 4.3. Profile

`profiles/fc-online.json` được bổ sung:

- Click points: `relist`, `price-value`, `cancel-buy`, `buy`.
- Pixel probes: `relist-guard`, `buy-dialog-red`.
- Region: `buy-dialog-max-price` — `96×26`.
- Config `priceRefresh` với timeout, tolerance, guard `1280×720`/DPI 96.

### 4.4. Test

| File | Nội dung |
|---|---|
| `RegionComparerTests.cs` | Exact, alpha ignored, tolerance, minChangedPixels, size/param lỗi. |
| `GdiRegionSourceTests.cs` | Đọc đúng BGRA top-down từ memory DC. |
| `RegionResolverTests.cs` | Trong biên, vượt biên, tọa độ âm, size 0. |
| `BmpWriterTests.cs` | Header/pixel BMP hợp lệ. |
| `PriceRefreshScenarioTests.cs` | Dry-run, confirm-buy, same price, timeout, guard, popup mở sẵn, vùng rỗng. |
| `PriceRefreshProfileTests.cs` | Profile mẫu, reference thiếu, expectedRgb thiếu, region trùng, width 0, threshold quá lớn. |
| `ProfileStoreTests.cs` | Round-trip regions/priceRefresh. |

### 4.5. Tài liệu

- `tools/GameProbe/README.md`: thêm mục P5.1, tọa độ, quy trình test, giới hạn.
- `docs/gameprobe-implementation-plan.md`: thêm P5.1, ghi chú P3 branch riêng,
  cập nhật quyết định mở và thứ tự triển khai.

---

## 5. Luồng chạy

```text
Start
  │
  ├─ Kiểm tra cửa sổ: tồn tại, đúng PID, visible, không minimized,
  │  đúng client 1280×720, đúng DPI 96
  ├─ Nếu popup đang mở sẵn → click Huỷ, chờ đóng
  ├─ Delay khởi động (mặc định 3000 ms)
  ├─ Guard: pixel xanh trên nút Đăng ký lại phải khớp
  │
  ├─ Click relist
  ├─ Chờ pixel đỏ của Mua cầu thủ
  ├─ Chờ vùng giá ổn định 2 frame + có text sáng/tối
  ├─ Chụp baseline
  ├─ Click Huỷ, chờ popup đóng
  │
  └─ Lặp attempt 2..maxAttempts
       ├─ Delay
       ├─ Guard relist
       ├─ Click relist → chờ popup → chờ stable frame
       ├─ So baseline
       │    ├─ Giống → Huỷ → chờ đóng → lặp
       │    └─ Khác → lưu BMP
       │         ├─ Dry-run → dừng, exit 0
       │         └─ Confirm-buy → click giá mới → click Mua cầu thủ → dừng
       └─ Hết maxAttempts → exit 5, không click mù
```

---

## 6. Cấu hình và tọa độ

Tọa độ client suy ra từ ảnh chụp 1920×1080, giả định client origin `(8,31)`;
đã đối chiếu với point `missions-new` cũ và chạy live.

| Mục | Client `(x,y)` | Ghi chú |
|---|---|---|
| `relist` | `(991,181)` | tâm nút `Đăng ký lại` hàng 1 |
| `relist-guard` | `(972,181)` | chữ xanh `#08F368`, tolerance 24 |
| `buy-dialog-red` | `(792,574)` | `#ED4428`, tolerance 16; popup đóng `#2C392B` |
| `buy-dialog-max-price` | `(942,285)` `96×26` | vùng `Giá tối đa` |
| `price-value` | `(1022,299)` | click giá trị sau khi đổi (đã chỉnh từ 1017) |
| `cancel-buy` | `(964,574)` | nút `Huỷ` |
| `buy` | `(830,574)` | nút `Mua cầu thủ` |

Thông số chính trong `priceRefresh`:

| Tham số | Giá trị |
|---|---|
| `startDelayMs` | 3000 |
| `settleMs` | 350 |
| `stableFrameGapMs` | 120 |
| `stableTimeoutMs` | 1500 |
| `pollIntervalMs` | 100 |
| `popupTimeoutMs` | 3000 |
| `closeTimeoutMs` | 3000 |
| `attemptDelayMs` | 800 |
| `maxAttempts` | 40 |
| `priceClickDelayMs` | 200 |
| `channelTolerance` | 0 |
| `minChangedPixels` | 1 |
| `expectedClientWidth/Height` | 1280×720 |
| `expectedDpi` | 96 |

---

## 7. Guard và xử lý lỗi

- **Window guard:** HWND còn tồn tại, PID không đổi, cửa sổ visible, không minimized,
  client size và DPI đúng cấu hình.
- **Relist guard:** pixel chữ xanh phải khớp; lệch → abort trước khi click.
- **Popup detection:** chỉ chờ pixel đỏ của nút `Mua cầu thủ`; timeout → abort.
- **Stable frame:** hai frame liên tiếp giống nhau mới chấp nhận.
- **Content guard:** vùng giá phải có cả pixel tối (text) và pixel sáng (nền);
  tránh chụp frame đen/rỗng khi popup chưa load.
- **Capture method:** baseline và current phải cùng method; đổi method → abort.
- **Desktop fallback:** nếu window DC trả đen, chụp desktop; kiểm tra 5 điểm
  (giữa + 4 góc gần) không bị process khác che.
- **Cancel retry:** nếu popup không đóng sau lần `Huỷ` đầu, thử lại một lần;
  vẫn không đóng → timeout.
- **Max attempts:** hết số lần cấu hình → exit code 5, không mua.

---

## 8. Kết quả kiểm thử

### 8.1. Build và unit test

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed! - Failed: 0, Passed: 114, Skipped: 0, Total: 114
```

### 8.2. Live dry-run

Nguồn: `%LocalAppData%\GameProbe\logs\gameprobe-20261122.jsonl`

Run lúc `23:12:54–23:13:08`, profile `fc-online.json`:

| Thông số | Kết quả |
|---|---|
| HWND / PID | `0xF0312` / `3272` |
| Chế độ | dry-run (`confirmBuy=false`) |
| Số popup | 5/5 |
| Popup detection | ~0.44–0.55 s mỗi lần |
| Capture method | `BitBlt` |
| Baseline hash | `0x5FDB9B324C20B362` |
| Current hash | `0x5FDB9B324C20B362` |
| Changed pixels | 0 ở cả 4 lần so sánh |
| Max channel delta | 0 |
| Kết quả | `MaxAttemptsReached` |
| Tổng thời gian | ~14.6 s |

Kết luận: vùng giá ổn định pixel, không false positive; click `relist`, `Huỷ`
và chờ popup hoạt động đúng trên UI thật.

### 8.3. Guard chặn khi UI chưa sẵn sàng

Run lúc `23:13:13`:

- Baseline chụp thành công (`0x5FDB9B324C20B362`).
- Sau khi `Huỷ`, guard `relist-guard` đọc `#1B1C1E` thay vì `#08F368`.
- Scenario abort với `InvalidConfiguration`, **không click mù**.

Đây là hành vi an toàn đúng thiết kế. Nếu gặp lại, cần đợi UI TTCT ổn định
hoặc tăng delay trước lần `relist` tiếp theo.

### 8.4. Kiểm tra crop BMP

- Baseline/current: `96×26`, `Format32bppRgb`.
- Text tối nằm trong vùng: bounding box tương đối `(53,7)-(84,21)`.
- Nền sáng chiếm phần lớn vùng (2221/2496 pixel sáng, 243 pixel tối).
- Baseline và current giống nhau hoàn toàn (0 pixel khác).

Crop hiện tại đủ rộng và ổn định. Có thể thu hẹp vùng nếu muốn giảm nguy cơ
ảnh hưởng bởi hiệu ứng xung quanh, nhưng không bắt buộc.

### 8.5. Xác nhận của người dùng

Người dùng xác nhận toàn bộ quy trình chạy ổn và pass. Log kỹ thuật của phiên
chỉ ghi nhận dry-run; **chưa có bản ghi `--confirm-buy`**, nên báo cáo này không
khẳng định đã thực hiện mua thật thành công.

---

## 9. Sự cố và cách xử lý trong phiên

| Vấn đề | Cách xử lý |
|---|---|
| Test `RegionComparer` sai kỳ vọng do alpha/nền | Sửa dữ liệu test theo đúng BGRA và tolerance. |
| Warning nullable trong `RegionStabilityCommand` | Thêm guard `reference ?? throw` và truyền biến `first`. |
| Desktop capture có thể chụp nhầm cửa sổ khác | Thêm `WindowFromPoint` kiểm tra 5 điểm, abort nếu PID khác. |
| `price-value` click hơi lệch trái | Chỉnh `1017 → 1022`. |
| Popup chưa load đã chụp baseline | Thêm content guard: cần cả text tối và nền sáng. |
| Dry-run kết thúc để popup mở | Run sau tự phát hiện popup mở sẵn và `Huỷ` trước khi bắt đầu. |
| Guard lệch sau khi UI đổi trạng thái | Abort an toàn; cần đợi UI ổn định trước khi relist. |
| Ảnh chụp chứa thông tin account | Giữ `screenshots/` untracked, không commit. |

---

## 10. Trạng thái git

| Branch | Commit | Nội dung |
|---|---|---|
| `main` | `7e219dd` | P5.1 pixel region + scenario (code) |
| `main` | commit báo cáo này | Tài liệu P5.1 |
| `feat/gameprobe-p3` | `b7a471d` | P3 memory text scan (không merge) |

- Commit code `7e219dd`: 39 file thay đổi, +4121/−34.
- Đã fast-forward vào `main`; branch `feat/price-refresh` đã xoá sau khi merge.
- Chưa push remote.
- `screenshots/` và `tools/GameProbe/price.bmp` đang untracked.
- Các thay đổi tiếp theo commit trực tiếp trên `main`.

---

## 11. Cách chạy lại

Mở PowerShell **Run as administrator**, giữ game ở `1280×720`, DPI 96,
windowed/borderless:

```powershell
cd "D:\git\New folder\tools\GameProbe"
$gp = "src\GameProbe.Cli\bin\Release\net8.0-windows\gameprobe.exe"

# 1. Mở popup thủ công, chụp thử vùng giá
& $gp region capture --profile profiles\fc-online.json --region buy-dialog-max-price --out price.bmp

# 2. Vẫn để popup mở, kiểm tra vùng giá ổn định
& $gp region stability --profile profiles\fc-online.json --region buy-dialog-max-price --count 10 --interval 200

# 3. Dry-run 5 popup, chưa mua
& $gp scenario price-refresh --profile profiles\fc-online.json --dry-run --max-attempts 5 --start-delay 5000

# 4. Chỉ chạy sau khi dry-run ổn định, dùng account test
& $gp scenario price-refresh --profile profiles\fc-online.json --confirm-buy
```

---

## 12. Việc còn lại và rủi ro

### Còn lại

- Chạy `--confirm-buy` trên account test và ghi lại kết quả vào báo cáo
  (nếu chưa thực hiện).
- Chạy dry-run thêm 5–10 lần để xác nhận không false positive trên nhiều
  trạng thái UI khác nhau.
- Nếu guard lệch sau khi `Huỷ`, bổ sung delay/retry trước lần `relist` kế tiếp
  hoặc kiểm tra lại vị trí nút khi danh sách thay đổi.
- Có thể thu hẹp `buy-dialog-max-price` quanh text để giảm nhiễu.

### Rủi ro

- UI đổi layout/font/scale → phải cập nhật tọa độ và chạy lại `region stability`.
- Không OCR nên không biết giá tăng/giảm; mọi thay đổi pixel đều kích hoạt mua.
- Nếu cửa sổ bị che khi dùng desktop fallback, scenario abort (an toàn) nhưng
  có thể không chạy được.
- Nếu capture method đổi giữa baseline/current, scenario abort để tránh mua nhầm.
- Chạy `--confirm-buy` là thao tác mua thật; chỉ dùng account test.

---

## 13. Kết luận

P5.1 đã hoàn thành phần code, cấu hình và kiểm thử offline. Live dry-run xác nhận
vùng giá ổn định pixel, guard và luồng click hoạt động trên FC Online. Bước mua
thật cần được chạy và ghi nhận riêng với account test trước khi coi P5.1 là
hoàn tất nghiệm thu.

---

## 14. Cập nhật 2026-11-23 — nút `Đăng ký lại` chỉ nhận click khi đã hover

### 14.1. Hiện tượng

Trên UI thật, nút `Đăng ký lại` và `Hủy` của hàng 1 chỉ hiện khi hover vào hàng.
Khi chuột thật ở nơi khác, vùng nút là nền tối `#1B1C1E`. Câu hỏi: gửi click
vào tọa độ `(991,181)` khi chưa hover có mở được popup `Mua cầu thủ` không?

### 14.2. Kết quả test thủ công

Chuột thật giữ ngoài hàng 1, chạy CLI bằng PowerShell Administrator:

| Thời điểm | Lệnh | Kết quả |
|---|---|---|
| `23:44:28` | `click --point relist --no-hover` | Hàng hover, **popup không mở** |
| `23:50:37` | `click --point relist` (hover=yes) | Hàng hover, **popup vẫn không mở** |
| sau đó | chạy lại đúng lệnh `click --point relist` | **Popup mở** |
| `00:05:29` | `click --point relist --hover-only` | Hàng hover, guard xanh `#07F368` |
| `23:54:47` | đọc sau khi `Huỷ` | guard `#1B1C1E`, popup `#2C392B` |

Kết luận:

- Click đầu tiên trên hàng chưa hover chỉ **bật hover**, chưa kích hoạt nút.
- Click thứ hai, khi hover đã hiện, mới mở popup.
- `WM_MOUSEMOVE` đơn lẻ đã đủ bật hover (test `--hover-only`).
- Sau khi popup đóng, hover bị xoá nếu chuột thật ở nơi khác; guard đọc lại nền tối.

### 14.3. Màu xanh không phải nguyên nhân

Đọc trực tiếp từ ảnh `screenshots/3.png` tại điểm guard `(972,181)`
(quy đổi screen `(980,212)`):

- Màu thực tế: `#07F368`.
- Profile kỳ vọng: `#08F368`, tolerance 24.
- Lệch 1 ở R/G, vẫn khớp tolerance.

Lỗi guard `#1B1C1E` ở lần chạy `23:13` là do hover đã mất, không phải màu bị đổi.

### 14.4. Fix

| File | Thay đổi |
|---|---|
| `Models/ClickPoint.cs` | Thêm `hoverSettleMs` (mặc định 0; `relist` = 250 ms). |
| `Input/HoverExecutionResult.cs` | Kết quả hover (x, y, duration). |
| `Input/IClickExecutor.cs` | Thêm `Hover(window, point)`. |
| `Input/ClickService.cs` | `Hover` chỉ gửi `WM_MOUSEMOVE` rồi chờ `hoverSettleMs`; `Click` cũng chờ `hoverSettleMs` sau move trước down/up. |
| `Models/PriceRefreshOptions.cs` | Thêm `relistGuardTimeoutMs` (mặc định 1000 ms). |
| `Scenario/PriceRefreshScenario.cs` | Trước mỗi guard: gọi `Hover(relist)`, poll `relist-guard` đến khi xanh hoặc timeout. |
| `Cli/Commands/ClickCommand.cs` | Thêm `--hover-only` để kiểm tra hover mà không click. |
| `profiles/fc-online.json` | `relist.hoverSettleMs=250`, `priceRefresh.relistGuardTimeoutMs=1000`. |
| `Profiles/ProfileValidator.cs` | Validate `hoverSettleMs` 0..10000, `relistGuardTimeoutMs` 100..60000. |
| Tests | +5 test: hover chỉ gửi move, validate settle, guard đổi xanh sau hover, round-trip profile, timeout guard. |

Luồng mới:

```text
mỗi attempt:
  hover pulse relist (WM_MOUSEMOVE + 250 ms)
  poll relist-guard tối đa 1000 ms
  click relist (move + 250 ms + down/up)
  chờ popup → chụp → Huỷ
```

### 14.5. Live dry-run sau fix

Chuột thật ngoài hàng 1, chạy:

```powershell
& $gp scenario price-refresh --profile profiles\fc-online.json --dry-run --max-attempts 5 --start-delay 3000
```

Log `gameprobe-20261123.jsonl`, run `00:06:22–00:06:37`:

| Thông số | Kết quả |
|---|---|
| Số popup | **5/5** |
| Hover pulse mỗi attempt | ~265–280 ms |
| Click `relist` | ~320–328 ms (250 ms settle + 50 ms hold) |
| Popup detection | ~542–660 ms |
| Guard | Pass cả 5 lần, không abort |
| Baseline/current hash | `0xFB57650394FD6A28` |
| Changed pixels | 0 ở cả 4 lần so sánh |
| Kết quả | `MaxAttemptsReached` |
| Tổng thời gian | 15679.3 ms |
| Popup sau run | Đã đóng (`#2C392B`) |

Đây là lần đầu scenario chạy đủ 5 popup **không cần đặt chuột thật lên hàng**.

### 14.6. Build và test sau fix

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed! - Failed: 0, Passed: 119, Skipped: 0, Total: 119
```

### 14.7. Git

| Commit | Nội dung |
|---|---|
| `92a5be2` | `fix(gameprobe): hover relist button before guard and click` (code + profile + tests + README) |
| commit báo cáo này | Cập nhật mục 14 |

### 14.8. Việc còn lại

- Chạy `--confirm-buy` trên account test và ghi log.
- Lặp dry-run 5–10 lần với chuột ngoài hàng để xác nhận ổn định.
- Thử thêm trường hợp chuột thật đang hover hàng 1 (không được làm click đôi).
- Nếu UI chậm, tăng `relist.hoverSettleMs` hoặc `relistGuardTimeoutMs`.
