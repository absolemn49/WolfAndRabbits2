# WOLF AND RABBITS 3D — TÀI LIỆU TÍNH NĂNG & LOGIC GAME

> **Mục đích tài liệu:** Nguồn tham chiếu tập trung (Single Source of Truth) cho toàn bộ luật chơi, thông số cân bằng, kiến trúc Client–Server, cấu hình JSON, đa nền tảng (Web/Mobile) và danh sách theo dõi tiến độ của dự án **Wolf and Rabbits 3D**.

---

## 1. Tổng quan & Kiến trúc hệ thống

| Thành phần | Công nghệ / Cấu trúc | Ghi chú |
|---|---|---|
| **Thể loại** | Chiến thuật sinh tồn bất đối xứng 3D (phong cách Warcraft) | **1 Sói** vs **1–3 Thỏ** (2–4 người/phòng) |
| **Backend (Authoritative Server)** | ASP.NET Core `.NET 10` WebSocket (`Server/`) | Toàn bộ va chạm, kinh tế, sát thương, mua đồ, thắng/thua xử lý 100% trên Server |
| **Tần số mô phỏng** | Simulation **20 Hz** (`0.05s/tick`) • Snapshot **10 Hz** | Client nội suy chuyển động mượt 60+ FPS |
| **Client 3D** | Unity 6 (`6000.3.14f1`, URP 3D, New Input System) (`Client/`) | 1 mã nguồn duy nhất build cho **WebGL** và **Mobile (Android/iOS)** |
| **Kiểm thử tự động** | `Server.Tests/` (xác thực bản đồ, kinh tế, combat, shop, WebSocket live 1v3) | Chạy qua `dotnet` |

---

## 2. Luồng trận đấu & Giai đoạn (Match Flow)

### 2.1. Trước trận đấu (Lobby & Waiting Room)
1. Người chơi mở game vào scene `Lobby` (nền 3D xoay quanh rừng và căn cứ).
2. Có thể **Tạo phòng** (tùy chọn đặt mật khẩu) hoặc **Tham gia phòng** qua danh sách phòng mở (tự cập nhật mỗi 3 giây) / nhập mã phòng.
3. Phòng đầy `4/4` hoặc đã bắt đầu sẽ **tự động ẩn** khỏi danh sách phòng chờ.
4. **Phân vai trò:**
   - **Chủ phòng (Host)** = **Sói (Wolf)**
   - **Khách (Guests)** = **Thỏ (Rabbit)** (tối đa 3 Thỏ)
5. Cần tối thiểu **2 người** để Chủ phòng bấm **Bắt đầu trận**.

### 2.2. Giai đoạn 1 — Chuẩn bị (`Prep`: 30 giây đầu tiên)
- **Thỏ (Rabbits):**
  - Xuất hiện ngay tại rìa Quảng trường trung tâm với **200 vàng khởi đầu**.
  - Vàng tự động tăng ngay từ giây `0` (`+1 vàng/giây` + thu nhập từ Nhà tiền xây thêm).
  - Tự do chạy vào rừng, chọn 1 trong 10 căn cứ, đặt Cổng, Nhà tiền, Pháo và nâng cấp.
- **Sói (Wolf):**
  - **Chưa xuất hiện trên bản đồ** (`spawned = false`) trong suốt 30 giây đầu tiên.
  - Có sẵn **200 vàng khởi đầu**, nhưng **vàng KHÔNG tăng** (`+0/s`) trong 30 giây này.
  - Không hiển thị mô hình 3D, không hiện trên Minimap, không thể di chuyển, không thể tấn công và không thể mua đồ tại Cửa hàng (`err.wolf_not_spawned`).
  - Bản đồ của người chơi Sói phủ Sương mù (không thể soi hướng chạy của Thỏ trước khi xuất hiện) kèm thanh đếm ngược giữa màn hình: *"SÓI CHƯA XUẤT HIỆN • Xuất hiện sau 00:XX"*.

### 2.3. Giai đoạn 2 — Săn đuổi (`Hunt`: 6 phút = 360 giây)
- Ngay khi đồng hồ 30 giây về `0`:
  - **Sói chính thức xuất hiện** (`spawned = true`) tại Quảng trường trung tâm (cạnh Nhà trang bị) kèm hiệu ứng vòng sáng + nổ + rung camera và thông báo toàn trận.
  - Camera của Sói tự xoay về điểm xuất hiện, Sương mù mở tầm nhìn quanh Sói.
  - **Vàng thụ động của Sói bắt đầu tăng** (`+2 vàng/giây`) từ thời điểm xuất hiện.
  - Sói có thể mua ngay trang bị/trái cây bằng 200 vàng khởi đầu ngay cạnh Nhà trang bị hoặc chạy đi săn Thỏ.
  - Pháo của Thỏ bắt đầu tự động nhắm bắn Sói khi Sói lọt tầm bắn và không bị cây chắn.

### 2.4. Điều kiện kết thúc trận đấu (`Ended`)
- **Phe Sói thắng (`Wolf`):** Hạ gục toàn bộ Thỏ (hoặc tất cả Thỏ ngắt kết nối).
- **Phe Thỏ thắng (`Rabbits`):**
  1. Tiêu diệt được Sói (bằng Pháo phòng thủ) hoặc Sói ngắt kết nối; **HOẶC**
  2. Còn ít nhất 1 Thỏ sống sót khi hết **6 phút** giai đoạn Săn đuổi.
- Sau trận đấu, bảng kết quả hiển thị thống kê từng người (công trình xây/phá, sát thương, vàng kiếm được) và cho chọn **Chơi lại (Phòng chờ)** hoặc **Về danh sách phòng**.

---

## 3. Bản đồ 44×44 & Hệ thống Sương mù (Fog of War)

- **Kích thước bản đồ:** `44 × 44` ô (`MapDefinition.cs`).
- **Quảng trường trung tâm:** Nằm giữa bản đồ, có **Nhà trang bị của Sói (`CenterShop`)** với bán kính tương tác `3.5` ô.
- **10 Căn cứ Thỏ:** Phân bố quanh rừng với hình dạng/kích thước khác nhau (`≥ 12` ô sàn mỗi căn cứ), bao quanh bởi tường cây kín và **chỉ có đúng 1 ô lối vào (`BaseEntrance`)** xoay đủ 4 hướng Đông/Tây/Nam/Bắc.
- **Tầm nhìn (Fog of War):**
  - Bán kính tầm nhìn chuẩn: `5.5` ô, bị chặn bởi ô cây (`Tree`).
  - **Luật cửa căn cứ:** Khi nhân vật đứng trên ô lối vào hoặc ngay trước cửa căn cứ (≤ `1.35` ô có đường nhìn thông thoáng), toàn bộ nội thất bên trong căn cứ đó được soi sáng.
  - **Tầm nhìn đồng đội Thỏ:** Các Thỏ luôn nhìn thấy nhau trên bản đồ và Minimap; chỉ ẩn kẻ địch (Sói) ngoài vùng sương mù.
  - **Bộ nhớ công trình của Sói (Wolf Ghost Memory):** Khi Sói đi qua và nhìn thấy công trình của Thỏ, công trình đó được ghi nhớ trên màn hình của Sói cho đến khi Sói quay lại soi khu vực đó và thấy công trình đã thay đổi/bị phá.
  - **Chế độ quan sát khi bị hạ:** Người chơi đã chết được mở toàn bộ sương mù để theo dõi phần còn lại của trận đấu.

---

## 4. Chỉ số gốc & Kinh tế

| Thông số | Sói (Wolf) | Thỏ (Rabbit) |
|---|---|---|
| **Máu gốc (`BaseMaxHp`)** | `260 HP` | `100 HP` |
| **Tốc độ di chuyển gốc** | `4.45 ô/giây` | `4.10 ô/giây` |
| **Sát thương tấn công gốc** | `35` (lên Thỏ & Công trình) | Không tấn công tay (dùng Pháo) |
| **Tầm đánh cận chiến** | `1.65 ô` | — |
| **Hồi chiêu đánh gốc** | `0.85 giây` | — |
| **Vàng khởi đầu** | **200 vàng** | **200 vàng** |
| **Vàng thụ động** | **+2 vàng/giây** *(chỉ tăng sau khi xuất hiện ở giây thứ 30)* | **+1 vàng/giây** *(tăng ngay từ giây 0)* |
| **Vàng thưởng phá công trình** | **+1 vàng mỗi 8 HP sát thương thực tế** gây lên công trình *(tích lũy phần dư sang đòn sau)* | — |
| **Bán kính va chạm (`PlayerRadius`)** | `0.34 ô` | `0.34 ô` |

---

## 5. Hệ thống Công trình của Thỏ (3 cấp độ)

### 5.1. Luật xây dựng & nâng cấp
- Chỉ xây/nâng cấp trong phạm vi **vùng `3 × 3` ô** xung quanh vị trí đứng của Thỏ.
- **Cổng (`gate`):** Chỉ đặt được trên ô lối vào căn cứ (`BaseEntrance`). **Chặn Sói** khi `HP > 0`, nhưng **Thỏ đi xuyên qua tự do**. Khi Cổng vỡ (`HP = 0`), đường vào căn cứ mở ra.
- **Nhà tiền (`gold_mine`) & Pháo (`cannon`):** Chỉ đặt được trên ô sàn bên trong căn cứ (`BaseFloor`).
- Không thể đặt công trình đè lên ô đã có công trình khác hoặc đang có nhân vật (đã xuất hiện) đứng trên đó.
- Chỉ chủ sở hữu công trình mới được nâng cấp (`Lv1 → Lv2 → Lv3`); khi nâng cấp, công trình được cộng thêm đúng lượng máu tối đa tăng thêm.
- Mọi yêu cầu xây/nâng cấp/mua đồ/đánh đều có `requestId` chống xử lý trùng lặp (idempotency).

### 5.2. Bảng thông số công trình (`GameConfig.cs`)

| Công trình | Cấp | Tên hiển thị | Giá (Vàng) | Máu (HP) | Giáp | Vàng/giây | Sát thương | Tầm bắn | Nhịp bắn |
|---|---:|---|---:|---:|---:|---:|---:|---:|---:|
| **Cổng (`gate`)** | 1 | Cổng gỗ | 50 | 300 | 0 | — | — | — | — |
| **Cổng (`gate`)** | 2 | Cổng sắt | 90 | 550 | 5 | — | — | — | — |
| **Cổng (`gate`)** | 3 | Cổng thần thánh | 150 | 900 | 10 | — | — | — | — |
| **Nhà tiền (`gold_mine`)** | 1 | Mỏ vàng I | 60 | 180 | 0 | +2/s | — | — | — |
| **Nhà tiền (`gold_mine`)** | 2 | Mỏ vàng II | 110 | 300 | 0 | +4/s | — | — | — |
| **Nhà tiền (`gold_mine`)** | 3 | Kho báu III | 180 | 450 | 0 | +7/s | — | — | — |
| **Pháo (`cannon`)** | 1 | Pháo I | 90 | 240 | 0 | — | 12 | 4.0 ô | 1.00s |
| **Pháo (`cannon`)** | 2 | Pháo đôi II | 130 | 380 | 0 | — | 20 | 4.5 ô | 1.00s |
| **Pháo (`cannon`)** | 3 | Pháo ma pháp III | 210 | 560 | 0 | — | 32 | 5.0 ô | 0.87s |

> **Luật đạn Pháo:** Tốc độ bay `11.5 ô/giây`, tự bám Sói, bị chặn nếu gặp ô Cây (`Tree`). Sát thương thực nhận của Sói: `max(1, Damage_Pháo - Giáp_Sói)`.

---

## 6. Hệ thống 5 Chỉ số Sói & Cửa hàng cấu hình bằng JSON

### 6.1. Công thức 5 chỉ số Sói
1. **Tấn công (`attack`):** `Damage = 35 + BonusAttack` (áp dụng cho cả đánh Thỏ và phá Công trình).
2. **Tốc độ tấn công (`attack_speed_percent`):** tăng `%` so với tốc độ đánh gốc:
   $$\text{Cooldown} = \max\left(0.25\text{s},\ \frac{0.85}{1 + \frac{\text{BonusAttackSpeedPercent}}{100}}\right)$$
3. **Tốc độ di chuyển (`move_speed_percent`):** tăng `%` so với tốc độ gốc:
   $$\text{MoveSpeed} = 4.45 \times \left(1 + \frac{\text{BonusMoveSpeedPercent}}{100}\right)$$
4. **Giáp (`armor`):** `Armor = BonusArmor` — trừ thẳng vào sát thương mỗi phát Pháo bắn trúng Sói (tối thiểu 1 sát thương).
5. **Máu tối đa (`max_hp`):** `MaxHp = 260 + BonusMaxHp` — khi mua sẽ cộng ngay lượng máu tăng thêm vào `Hp` hiện tại.

### 6.2. Hai loại vật phẩm
- **`"slot"` (Trang bị ô đồ):** Chiếm 1 ô trong túi đồ của Sói, giới hạn mặc định **`maxSlotItems = 6`** (cấu hình được trong JSON). Khi đủ 6/6 ô sẽ không mua thêm được đồ slot (`err.inventory_full`).
- **`"fruit"` (Trái cây vĩnh viễn):** Ăn ngay khi mua, **không tốn ô đồ**, **không giới hạn số lần mua**, cộng vĩnh viễn vào 1 trong 5 chỉ số của Sói.

### 6.3. File cấu hình JSON (đồng bộ Backend & Client)
- Backend (authoritative, kiểm tra qua `GET /api/wolf-shop`): `Server/Config/wolf_shop.json`
- Client (fallback & tra cứu icon): `Client/Assets/Game/Resources/Config/wolf_shop.json`
- Ảnh icon tùy chỉnh (tùy chọn): đặt file PNG tại `Client/Assets/Game/Resources/Icons/<icon>.png`; nếu chưa có, Client tự dùng bộ icon procedural vẽ sẵn trong `ProceduralTextureFactory.cs`.

#### Danh sách 13 vật phẩm mặc định

| ID | Tên (VI / EN) | Loại (`itemType`) | Chỉ số (`statType`) | Giá trị | Giá (Vàng) | Icon |
|---|---|---|---|---:|---:|---|
| `iron_claws` | Vuốt sắt / Iron Claws | `slot` | `attack` | +15 | 70 | `iron_claws` |
| `siege_hammer` | Búa phá cổng / Siege Hammer | `slot` | `attack` | +35 | 150 | `siege_hammer` |
| `fury_gloves` | Găng cuồng nộ / Fury Gloves | `slot` | `attack_speed_percent` | +25% | 90 | `fury_gloves` |
| `swift_boots` | Giày tốc độ / Swift Boots | `slot` | `move_speed_percent` | +18% | 85 | `swift_boots` |
| `wind_cloak` | Áo choàng gió / Wind Cloak | `slot` | `move_speed_percent` | +30% | 160 | `wind_cloak` |
| `leather_armor` | Giáp da / Leather Armor | `slot` | `armor` | +4 | 80 | `leather_armor` |
| `iron_armor` | Giáp sắt / Iron Armor | `slot` | `armor` | +8 | 160 | `iron_armor` |
| `vitality_ring` | Nhẫn sinh lực / Vitality Ring | `slot` | `max_hp` | +120 | 110 | `vitality_ring` |
| `fruit_attack` | Táo sức mạnh / Power Apple | `fruit` | `attack` | +6 | 55 | `fruit_attack` |
| `fruit_attack_speed` | Quả tốc đánh / Fury Berry | `fruit` | `attack_speed_percent` | +12% | 60 | `fruit_attack_speed` |
| `fruit_move_speed` | Quả gió rừng / Wind Fruit | `fruit` | `move_speed_percent` | +8% | 60 | `fruit_move_speed` |
| `fruit_armor` | Trái vỏ sắt / Ironbark Fruit | `fruit` | `armor` | +2 | 65 | `fruit_armor` |
| `fruit_hp` | Đào sinh mệnh / Vitality Peach | `fruit` | `max_hp` | +45 | 50 | `fruit_hp` |

---

## 7. Giao thức Mạng & HTTP Endpoints

- **HTTP Endpoints (`Server/Program.cs`):**
  - `GET /health` — Kiểm tra tình trạng Server, môi trường (`Development`/`Release`) và cổng mặc định.
  - `GET /api/rooms` — Danh sách phòng chờ khả dụng (JSON).
  - `GET /api/wolf-shop` — Cấu hình `wolf_shop.json` đang hoạt động trên Server.
  - `GET /` & static files (`Server/wwwroot/`) — Phục vụ trực tiếp bản build Unity WebGL (hỗ trợ sẵn nén `.br`, `.gz`, `.wasm`).
- **WebSocket Endpoint:** `/ws`
  - **Client → Server:** `list_rooms`, `create_room`, `join_room`, `leave_room`, `start_match`, `return_to_room`, `move_input`, `build_request`, `upgrade_request`, `attack_request`, `buy_item`.
  - **Server → Client:** `welcome`, `error`, `room_list`, `room_state`, `match_start`, `state_snapshot`, `match_end`.
  - **Sự kiện trong `state_snapshot.events`:** `wolf_spawned`, `phase_changed`, `attack_swing`, `damage`, `building_placed`, `building_upgraded`, `building_destroyed`, `cannon_shot`, `cannon_hit`, `gold_earned`, `item_bought`, `player_died`.

---

## 8. Đa nền tảng (Mobile vs Web), Đa ngôn ngữ & Cờ Build

### 8.1. Tách giao diện Mobile và Web (`PlatformProfile.cs`)
- **Mobile (Android / iOS):**
  - Có **Virtual Joystick** góc dưới-trái, áp dụng `SafeAreaFitter` chống tai thỏ, ẩn gợi ý phím tắt bàn phím.
  - Có thể bật chế độ xem trước Mobile trong Unity Editor qua menu: `WolfAndRabbits/Preview Mobile UI (Joystick) in Editor`.
- **WebGL / Desktop:**
  - **Không có Virtual Joystick**, điều khiển bằng `WASD` / mũi tên, `E` (đánh/xây), `B` (cửa hàng Sói), giữ `Space` bám nhân vật, kéo chuột xem bản đồ, `Esc` đóng hộp thoại.
  - Tự suy ra địa chỉ WebSocket cùng domain (`ws://` hoặc `wss://`) khi chạy trong trình duyệt WebGL.

### 8.2. Đa ngôn ngữ JSON & Font tiếng Việt
- Font tích hợp sẵn `Client/Assets/Game/Resources/Fonts/GameFont.ttf` (hỗ trợ đầy đủ dấu tiếng Việt).
- Nút **bánh răng Cài đặt** có mặt ở cả Lobby và trong trận (góc trên-phải), cho phép đổi **Tiếng Việt ↔ English** tức thì không cần tải lại scene.
- Bảng từ điển lưu tại:
  - `Client/Assets/Game/Resources/Localization/vi.json`
  - `Client/Assets/Game/Resources/Localization/en.json`

### 8.3. Cờ môi trường Development / Release & Docker
- Cấu hình tại `Client/Assets/Game/Resources/server_config.json` & `BuildEnvironment.cs`:
  - **`Development` (Test):** Cổng mặc định **`5000`** (`ws://localhost:5000/ws`).
  - **`Release` (Production):** Cổng mặc định **`80`** (`ws://<domain>/ws` hoặc `wss://<domain>/ws`).
- Chuyển chế độ nhanh trong Unity Editor qua menu `WolfAndRabbits/Environment/...` và build WebGL Release qua `WolfAndRabbits/Build/Build WebGL (Release -> Server wwwroot)`.
- Đóng gói 1 container Docker duy nhất (`Dockerfile` + `docker-compose.yml`) chạy chung Backend + WebGL, phục vụ đồng thời cả trình duyệt Web và app Mobile.

---

## 9. Theo dõi tiến độ & Lịch sử thay đổi (Tracking)

### Checklist tính năng hiện tại
- [x] Kiến trúc Server Authoritative WebSocket (20 Hz sim / 10 Hz snapshot)
- [x] Lobby + Phòng chờ + Mật khẩu + Tự ẩn phòng đầy 4/4 + Chơi lại
- [x] Bản đồ 44×44 với 10 căn cứ đa dạng (1 ô cửa duy nhất) & Quảng trường trung tâm
- [x] Sương mù (Fog of War) + luật nhìn xuyên căn cứ + bộ nhớ công trình của Sói
- [x] Giai đoạn 30s đầu: **Thỏ & Sói khởi đầu 200 vàng**, **Sói chưa xuất hiện và chưa tăng vàng**; hết 30s Sói xuất hiện mới bắt đầu tăng vàng (`+2g/s`)
- [x] 3 loại công trình Thỏ (Cổng, Nhà tiền, Pháo) × 3 cấp nâng cấp + luật ô 3×3
- [x] 5 chỉ số Sói (`attack`, `attack_speed_percent`, `move_speed_percent`, `armor`, `max_hp`)
- [x] Cửa hàng Sói cấu hình bằng JSON (`wolf_shop.json`) trên cả Backend & Client: đồ `slot` (giới hạn 6 ô) + `fruit` (ăn vĩnh viễn)
- [x] Đa ngôn ngữ bằng JSON (`vi.json` / `en.json`) + Font tiếng Việt + Menu Cài đặt (icon bánh răng)
- [x] Tách UI Mobile (có Joystick) vs Web/PC (không Joystick) trên 1 mã nguồn Client
- [x] Cờ môi trường `Development` (`:5000`) / `Release` (`:80`) & Docker hợp nhất Web + Backend
- [x] Bộ kiểm thử tự động `Server.Tests` (5/5 test kịch bản + live WebSocket 1v3)

### Nhật ký cập nhật gần nhất
- **Cập nhật 1:** Chuyển toàn bộ text UI sang bảng ngôn ngữ JSON (`vi.json`, `en.json`), bổ sung font tiếng Việt và menu Cài đặt bánh răng.
- **Cập nhật 2:** Thêm hệ thống 5 chỉ số Sói + `wolf_shop.json` cấu hình ở cả Server & Client với 2 loại đồ (`slot` tối đa 6 ô và `fruit` ăn vĩnh viễn) + 13 icon procedural.
- **Cập nhật 3:** Chỉnh luật khởi đầu trận: Thỏ và Sói đều bắt đầu với **200 vàng**; **30 giây đầu Sói chưa xuất hiện** (ẩn 3D/Minimap, phủ sương mù, khóa thao tác, chưa tăng vàng); sau 30 giây Sói xuất hiện tại trung tâm và bắt đầu tăng vàng (`+2g/s`). Tạo tài liệu theo dõi `GAME_DESIGN_AND_LOGIC.md`.
