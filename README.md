# game-probe

Repo nghiên cứu kỹ thuật automation cửa sổ game trên Windows: gửi window message
để click, đọc pixel/vùng ảnh để phát hiện thay đổi UI, và một kịch bản mẫu
(làm mới giá mua cầu thủ) chạy hoàn toàn bằng pixel — không OCR, không đọc memory game.

Tool chính là **GameProbe** (C# .NET 8, x64, WinForms GUI + CLI dùng chung Core).

> **Cảnh báo:** gửi window message/automation vào game có thể vi phạm Điều khoản
> dịch vụ và bị anti-cheat phát hiện. Repo này chỉ phục vụ nghiên cứu, học tập và
> account test. Không dùng để gian lận, không bypass anti-cheat.

## Nội dung repo

```text
docs/            Phân tích kỹ thuật, kế hoạch triển khai, báo cáo từng phase
screenshots/     Ảnh chụp UI game dùng để hiệu chuẩn tọa độ/region
tools/GameProbe/ Source C# (.NET 8): Core, Cli, App, tests, profiles
AGENTS.md        Quy tắc làm việc và giới hạn an toàn khi phát triển trong repo
```

Tài liệu nên đọc trước:

| Tài liệu | Nội dung |
|---|---|
| [`docs/ffzik-auto-click-analysis.md`](docs/ffzik-auto-click-analysis.md) | Phân tích tĩnh một tool auto-click tham chiếu: cơ chế `PostMessage`/`SendMessage`, phân loại màu pixel |
| [`docs/gameprobe-implementation-plan.md`](docs/gameprobe-implementation-plan.md) | Kế hoạch, quyết định kỹ thuật, phạm vi từng phase (P0–P6) |
| [`docs/gameprobe-p5.1-report-2026-11-22.md`](docs/gameprobe-p5.1-report-2026-11-22.md) | Báo cáo triển khai P5.1 — kịch bản làm mới giá mua cầu thủ |
| [`tools/GameProbe/README.md`](tools/GameProbe/README.md) | Hướng dẫn build, CLI, profile, fixture và kết quả kiểm thử |

## Build & test

```powershell
dotnet build tools/GameProbe/GameProbe.sln -c Release
dotnet test  tools/GameProbe/GameProbe.sln -c Release
```

Yêu cầu: Windows 10/11 x64, .NET 8 SDK. GUI và thao tác click vào game cần chạy
bằng **Administrator**.

## Nguyên tắc an toàn

- Trên nhánh `main`: chỉ đọc pixel/region và gửi window message; không đọc/ghi
  memory, không inject, không bypass anti-cheat.
- Nhánh `feat/gameprobe-p3` (không merge) chứa thử nghiệm đọc memory chỉ với
  `PROCESS_VM_READ`; không chạy trên FC Online.
- Chỉ test trên account phụ, dừng ngay nếu game crash, mất kết nối hoặc
  anti-cheat cảnh báo.

## Ghi chú

- `FFZik/` và `FFZik.zip` (binary tham chiếu dùng cho phân tích tĩnh) **không**
  được commit, chỉ giữ ở máy local.
- Các artifact debug (BMP/ảnh chụp vùng, log JSONL) bị gitignore.
