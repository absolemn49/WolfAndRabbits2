# Wolf and Rabbits 3D (Warcraft 3 Top-Down Style)

Trò chơi chiến thuật sinh tồn multiplayer 3D góc nhìn từ trên xuống phong cách **Warcraft 3** xây dựng trên **Unity 6 (URP 3D)** và máy chủ authoritative **ASP.NET Core WebSocket**.

- **1 Sói (chủ phòng)** săn lùng, công phá căn cứ và mua trang bị tại Nhà trang bị trung tâm.
- **1–3 Thỏ (khách)** chạy vào rừng, chọn 1 trong 10 căn cứ, dựng Cổng, Nhà tiền, Pháo (nâng cấp cấp 1 → 2 → 3) và sống sót hết 6 phút săn hoặc hạ gục Sói.

---

## Cấu trúc dự án chuẩn Unity & ASP.NET Core

```text
WolfAndRabbits2/
├── Server/                              # ASP.NET Core WebSocket Authoritative Server (20Hz tick, 10Hz snapshot)
│   ├── Program.cs                       # Endpoint /ws, /health, /api/rooms
│   ├── Protocol/                        # ClientMessages.cs, ServerMessages.cs
│   └── Game/                            # GameConfig, MapDefinition (10 bases), MatchSimulation, Room, RoomManager
├── Server.Tests/                        # Kiểm thử tự động logic trận đấu & WebSocket 1v3 thực
└── Client/Assets/Game/
    ├── Scenes/
    │   ├── Lobby.unity                  # Scene 0: Sảnh chờ, danh sách phòng, tạo/vào phòng, phòng chờ
    │   └── Play.unity                   # Scene 1: Trận đấu 3D Warcraft 3, Fog of War, HUD, kết quả trận
    ├── Resources/Shaders/               # WarcraftStylizedFog.shader (URP 17), WarcraftGlowUnlit.shader
    └── Scripts/
        ├── Core/                        # GameConstants, GameSession (DontDestroyOnLoad), SceneAtmosphereSetup
        ├── Network/                     # GameProtocol, GameWebSocketClient (Native + WebGL .jslib)
        ├── Audio/                       # ProceduralAudioManager
        ├── Lobby/                       # LobbySceneController, LobbyUiController
        ├── Play/                        # PlaySceneController, PlayerInputController, BuildPlacementController,
        │                                # MatchEntityManager, WarcraftCameraController
        ├── Visuals/                     # MapRenderer3D, FogOfWarController, CharacterVisual3D, BuildingVisual3D,
        │                                # WorldHealthBar3D, VfxManager3D, StylizedMeshFactory, ProceduralTextureFactory
        ├── UI/                          # WarcraftUiFactory, HudWidgets, MatchHudController
        └── Editor/                      # ProjectSetupMenu (Mở/Tạo lại Scene, cấu hình Build & Multiplayer)
```

---

## Hướng dẫn chạy thử nhanh (2–4 người chơi)

### Bước 1: Chạy máy chủ ASP.NET Core (Cờ Development / Release)

- **Chế độ Development (Test — cổng `5000`)**: Chạy `Server/start-server.bat`, hoặc:
  ```powershell
  dotnet run --project Server/WolfAndRabbits.Server.csproj -- --mode=development
  ```
  Lắng nghe tại `ws://localhost:5000/ws` (`http://localhost:5000/health`).

- **Chế độ Release (Production — cổng `80`)**: Chạy `Server/start-server-release.bat`, hoặc:
  ```powershell
  dotnet run --project Server/WolfAndRabbits.Server.csproj -c Release -- --mode=release
  ```
  (hoặc đặt biến môi trường `GAME_ENV=Release`). Lắng nghe tại cổng **`80`** (`http://<host>` phục vụ luôn WebGL từ `wwwroot/` và `ws://<host>/ws`).

### Bước 2: Chạy Client Unity 3D (Cờ Development / Release)

1. Mở dự án `Client` trong **Unity 6000.3**.
2. Chọn cờ môi trường trong menu **`Wolf and Rabbits -> Build Flag`**:
   - **`Development (Port 5000)`**: mặc định khi test (`APP_DEVELOPMENT`), tự nối tới `ws://localhost:5000/ws`.
   - **`Release (Port 80)`**: gắn cờ `APP_RELEASE`, tự nối cổng **`80`** (bản WebGL tự dùng domain/cổng hiện tại; bản Mobile đọc host VPS tại `Client/Assets/Game/Resources/server_config.json`).
3. Mở scene **`Assets/Game/Scenes/Lobby.unity`** (hoặc vào menu **`Wolf and Rabbits -> Open Lobby Scene`**).
3. Nhấn **Play**! Khi chủ phòng bấm **Bắt đầu trận**, `GameSession` tự động chuyển sang scene **`Play.unity`**; khi kết thúc trận hoặc rời phòng sẽ quay lại **`Lobby.unity`**.
4. Để test **2–4 người chơi trên cùng 1 máy**:
   - **Cách 1 (Unity 6)**: Cài `com.unity.multiplayer.playmode` trong Package Manager → mở `Window -> Multiplayer -> Multiplayer Play Mode` → bật Player 2–4.
   - **Cách 2 (Nhẹ RAM)**: Chạy menu `Wolf and Rabbits -> Configure Multiplayer & Mobile Settings` (tự đặt cửa sổ `960×540` + `Run In Background`), build Windows `.exe` rồi mở song song với Editor.

---

## Điều khiển

| Nền tảng | Thao tác |
|---|---|
| **PC / WebGL** | **Không hiển thị Virtual Joystick** • **WASD / Mũi tên**: Di chuyển • **Giữ chuột trái + kéo**: Di chuyển camera • **Cuộn chuột**: Zoom • **Giữ Space**: Bám nhân vật • **E**: Mở menu xây (Thỏ) / Đánh (Sói) • **B**: Mở cửa hàng (Sói gần trung tâm) • **Esc**: Hủy / Đóng |
| **Android / iOS (Màn hình ngang)** | **Tự động bật Virtual Joystick + SafeArea** • **Joystick góc dưới trái**: Di chuyển • **Kéo vùng bản đồ**: Di chuyển camera • **Chạm ô phát sáng**: Đặt/Nâng cấp công trình • **Nút lớn góc dưới phải (GIỮ)**: Bám nhân vật • **4 nút quạt**: Xây/Đánh, Nâng cấp/Cửa hàng, và 2 ô chờ hiển thị mờ |

> **Xem thử giao diện Mobile ngay trong Unity Editor**: Vào menu **`Wolf and Rabbits -> Preview Mobile UI (Joystick) in Editor`** để bật/tắt nhanh Virtual Joystick & SafeArea trước khi bấm Play.
