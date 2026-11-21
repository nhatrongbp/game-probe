# GameProbe

Công cụ nghiên cứu automation cửa sổ game trên Windows:
- click tại tọa độ cấu hình sẵn bằng window message;
- đọc/đoán màu một pixel;
- đọc text từ memory game, snapshot và diff.

> **Cảnh báo:** kỹ thuật gửi window message và đọc memory vào game có thể vi phạm
> Điều khoản dịch vụ và bị anti-cheat phát hiện. Chỉ dùng cho nghiên cứu, học tập
> và account test. Tool chỉ đọc memory, không ghi, không inject, không bypass
> anti-cheat. Không dùng để gian lận.

Kế hoạch đầy đủ: [`../../docs/gameprobe-implementation-plan.md`](../../docs/gameprobe-implementation-plan.md)

---

## Trạng thái

**P1 — Window attach & info — đã nghiệm thu trên FC Online.** Đã có:

- solution C# .NET 8 x64 với `Core`, `Cli`, `App`, test và TestTarget;
- manifest GUI chạy Administrator + PerMonitorV2 DPI;
- model profile và validator;
- `ProfileStore` load/save/validate JSON;
- logging skeleton (JSONL + console);
- CLI `profile validate`;
- `WindowLocator`, `GameWindow`, `GameWindowMonitor` (re-attach timer);
- CLI `window list` và `window info`;
- GUI skeleton 4 tab;
- unit test cho profile, validator, logger, window search và window locator.

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

Cả `--class FIFAKC` và `--process fczf.exe` đều khớp đúng 1 cửa sổ. Window rect lớn hơn client size do viền/title bar; **P2 sẽ dùng client coordinates**.

P2 sẽ thêm click và pixel probe. Memory scan thuộc P3.

## Yêu cầu

- Windows 10/11 x64
- .NET 8 SDK
- Chạy GUI bằng Administrator (manifest yêu cầu UAC)

## Build & test

```powershell
dotnet build tools/GameProbe/GameProbe.sln -c Release
dotnet test  tools/GameProbe/GameProbe.sln -c Release
```

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

Hoặc dùng bản build:

```powershell
tools/GameProbe/src/GameProbe.Cli/bin/Release/net8.0-windows/gameprobe.exe window list
```

Exit code: `0` thành công, `1` lỗi cú pháp/profile, `2` không tìm thấy file/cửa sổ,
`3` không có quyền đọc.

## Cấu trúc

```text
tools/GameProbe/
├─ src/GameProbe.Core/     model, profile, validator, logging
├─ src/GameProbe.Cli/      command line
├─ src/GameProbe.App/      WinForms GUI skeleton
├─ tests/GameProbe.Tests/  xUnit
├─ tests/GameProbe.TestTarget/  app giả lập cho P2
└─ profiles/fc-online.json
```

## Roadmap

| Phase | Nội dung |
|---|---|
| P0 | Khung dự án, profile, logging, CLI/GUI skeleton — **xong** |
| P1 | Attach cửa sổ, window info, re-attach — **xong** |
| P2 | ClickService, PixelProbe, CLI click/pixel |
| P3 | MemoryScanner, TextExtractor, snapshot/diff |
| P4 | GUI đầy đủ, hotkey, log viewer |
| P5 | Test trên FC Online bằng account test |
| P6 | Đóng gói, tài liệu usage/troubleshooting |

## Nguyên tắc an toàn

- Chỉ dùng `PROCESS_VM_READ`, không cấp quyền ghi.
- Không dùng `NtReadVirtualMemory` để lách hook/anti-cheat.
- Không chạy memory scan khi đang thi đấu; chỉ test ở login/lobby.
- Dừng ngay nếu game crash, mất kết nối hoặc anti-cheat cảnh báo.
