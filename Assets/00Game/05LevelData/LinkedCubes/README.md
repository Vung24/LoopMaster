# Prototype cube đôi

Trong Unity, dùng menu **LoopMaster → Cube đôi** để chọn một trong năm màn thử. Màn chạy qua Level Playtest với save sandbox của editor, không được thêm vào campaign.

Hoặc chọn file JSON → **Assets → LoopMaster → Sửa màn chơi JSON** để chỉnh trong Level Editor rồi Play.

## Các màn

1. `01_Exposed`: hai member đã ở trên cùng, thả cả cặp ngay.
2. `02_OneBlocked`: cần dọn cube thường trên partner trước.
3. `03_BothBlocked`: dọn cả hai phía rồi mới kích hoạt.
4. `04_SameColorBoundary`: nhóm màu thường phải dừng trước member cùng màu.
5. `05_IndependentAfterLaunch`: sau khi cặp được xử lý, các lớp còn lại hoạt động độc lập.

## Authoring

Mở tab Hộp và phần Nâng cao của danh sách khối. Chọn **Chọn làm đầu nối** ở cube thứ nhất, chuyển sang hộp khác rồi **Nối với khối đã chọn**. **Tháo liên kết** bỏ ID của cả hai member. Scene preview vẽ dây và nhãn ID. Mọi thao tác lưu qua Undo/Redo của tài liệu editor.

Prototype hỗ trợ một cặp trong hai Normal box. Mỗi member là một cube đầy, không có khóa, secret, key hoặc surprise. Khi cả hai ở top, bấm một bên phát hành đúng hai cube. Trong lúc cặp đang phát hành/hạ cánh, chưa nhận lượt thả mới; pause giữ operation. Box có member chưa xử lý bị loại khỏi shuffle. Undo chỉ khả dụng khi toàn bộ gameplay settled và record chưa bị finish hoặc thay đổi không liên quan vô hiệu hóa.

Prefab runtime: `Assets/00Game/Resources/CoreGamePrefabs/LinkedCubeCable.prefab`. Component nhận state cặp khi load, gắn dây vào cube và giữ virtual anchor theo board root; prefab không chứa logic điều khiển cặp.

Visual chỉ dùng một thanh nối 3D nhỏ bo tròn, cong nhẹ, nằm giữa tâm mặt bên hai cube; đầu thanh chìm nhẹ vào cube, không có socket hay vòng cổ. Không có text hướng dẫn nổi hoặc finger tutorial riêng. Game giới thiệu bằng **FeatureUnlockDialog → Linked Blocks** ở lần đầu gặp level có cặp, dùng bit FeatureTutorial hiện có như Secret Cube. Editor sandbox vẫn theo quy tắc reset preference khi mở phiên playtest mới.

Prefab xem hình: `Assets/00Game/04Prefabs/Gameplay/LinkedCubes/LinkedCubePair_VisualPreview.prefab`. Kéo vào scene để xem hai khay với dây nối hai cube; prefab này không có gameplay script/collider. Giữ nguyên mẫu hai box cũ trong thư mục LinkedBoxes.

Nhân bản đủ một cặp cấp ID mới cho bản sao. Nếu cặp gốc vẫn còn, bản nháp vượt giới hạn một cặp và chưa được Lưu/Play/Export; xóa hoặc Tháo liên kết một cặp trước. Designer kiểm tra hai khay kề nhau và dây không xuyên khay thứ ba trong Game View.

## Kiểm chứng

Core: `Rules/Verification/CoreRulesVerification.csproj`.
Solver: `Rules/Verification/DebugAutoPlay/DebugAutoPlayVerification.csproj` (bao gồm cả năm fixture và các màn regression).
Play Mode: công cụ, cách chạy và phạm vi báo cáo nằm tại `Rules/LinkedCubes/README.md`. Báo cáo `Results/timing-report.txt` có 20 lượt thắng và 20 so sánh transition của lượt thả cặp ở bước thời gian 30/60 Hz, speed-up bật/tắt. Đây là kiểm tra runtime trong Unity, không phải đo FPS trên thiết bị.

Sau sửa visual/popup, Unity Full qua 40 kiểm tra Play Mode và 9 kiểm tra authoring; Visual cuối qua 12 kiểm tra gồm popup không lặp, root xoay/scale và connector sau Undo. Xem manifest và `IMPLEMENTATION_AUDIT.md` để biết phạm vi bằng chứng. Trải nghiệm người chơi vẫn cần playtest.

Các JSON là màn mẫu kỹ thuật; độ khó và sự dễ hiểu của tutorial cần playtest người chơi trước khi đưa vào campaign.
