# Cân chỉnh bộ level — 22/10/2026

Bộ 70 màn đã được cập nhật theo yêu cầu một box trống, nhiều secret hơn và linked xuất hiện thường xuyên hơn. So sánh trước/sau dùng snapshot `c3cf4091e13a504578df0d12e160a3eba1ace3fd`.

| Nội dung | Trước | Sau |
|---|---:|---:|
| Khối secret | 253 | 447 |
| Cặp linked | 16 | 77 |
| Màn có linked | 16 | 41 |
| Màn có 2–3 cặp linked | 0 | 29 |
| Normal từ level 3 có đúng một box trống | — | 54 |

## Box trống và capacity

Toàn bộ **54 màn Normal từ level 3** có đúng một box trống thực tế. Stock màu thường cũng chỉ chừa một box đệm, tránh rải khối lưng chừng để dư chỗ tương đương hai box. Tutorial 1–2 được giữ nguyên.

**37 màn nhẹ/dạy mechanic/màn nghỉ** có capacity bằng số box hoặc hơn một slot; lời giải tham chiếu không quá hai lần số box. Các màn Normal phối hợp lớn hơn giữ capacity chặt hơn. Hard/Expert giữ capacity **4**; một số màn khó có thêm chỗ trong box để mở các lớp che của nhiều linked, không áp dụng biên độ đó cho màn Normal.

Level 7: **6 box, 5 màu, 20 khối, đúng 1 box trống, capacity 6**. Năm box có khối đều đầy bốn khối, trộn ba lớp màu; chỉ khối đáy là secret. Lời giải tham chiếu 11 lượt và đã chơi thắng trong Unity. Level 18–19 thu còn 7 box để luyện linked gọn hơn.

## Secret và linked

Secret tăng từ 253 lên 447 khối. Bố trí theo lớp và rải qua các box; giữ mặt trên có màu gợi ý. Chìa khóa nhóm, bộ đếm ice và màu box đặc biệt không bị gắn secret. Khối linked hidden là ngoại lệ có chủ đích: được phủ bởi một khối nhìn rõ.

Linked tăng thành 77 cặp trong 41 màn; 29 màn dùng hai hoặc ba cặp. Màn 15–17 và màn giới thiệu linked hidden 21 vẫn chỉ dùng một cặp. Các màn 35, 45, 52, 60, 62, 66, 68 dùng ba cặp. Màn 64 và 70 dùng hai cặp để phối với các khóa/ice hoặc các loại box khác.

Ví dụ phối hợp:

- 22: một cặp ẩn hai đầu, một cặp lộ rõ, board nhỏ và một box đệm.
- 52: ba cặp linked hidden với ice và khóa màu; một box trống.
- 55: linked hidden, ice, khóa màu và cửa nhóm. Box chứa chìa khóa nhóm độc lập với linked để tránh phụ thuộc vòng.
- 59: một cặp linked nối qua ranh giới cửa nhóm; chìa khóa mở đường riêng.
- 68: ba cặp linked hidden, khóa màu, khóa kề, ice và surprise.
- 70: hai cặp linked hidden với box đơn, box dài, cửa nhóm, các khóa, ice và surprise; lời giải mô phỏng 22 lượt, đã chơi thắng trong Unity.

## Thứ tự học và nhịp khó/nghỉ

Hidden bắt đầu ở 6; linked thường ở 15; linked hidden chỉ bắt đầu ở **21**, sau Hard 20. Không có cube vừa linked vừa hidden ở level 1–20.

| Level giới thiệu | Mechanic | Box | Capacity | Lượt tham chiếu |
|---|---|---:|---:|---:|
| 6 | Hidden | 5 | 5 | 10 |
| 15 | Linked thường | 6 | 6 | 11 |
| 21 | Linked hidden | 6 | 6 | 11 |
| 23 | Khóa màu | 6 | 6 | 12 |
| 29 | Khóa kề | 6 | 6 | 12 |
| 37 | Ice | 6 | 6 | 12 |
| 41 | Box dài | 7 | 7 | 9 |
| 49 | Box đơn | 6 | 6 | 8 |
| 53 | Cửa nhóm | 6 | 6 | 12 |
| 56 | Surprise | 6 | 6 | 10 |

Mọi Hard/Expert có màn kế tiếp đều được nối bằng một màn nghỉ: **10→11, 20→21, 25→26, 30→31, 35→36, 40→41, 45→46, 55→56, 60→61, 62→63, 64→65, 66→67, 68→69**. Có 14 màn Hard/Expert; Hard 10 dùng hidden đã học, các Hard từ 20 trở đi có ít nhất hai mechanic đã học. Level 10 và 46 khác nhau về mechanic trung tâm, capacity và bố cục.

Số lượt là lời giải kiểm chứng, không phải số lượt tối ưu hay thước đo độ khó của người chơi. Bộ lọc không ép màn nhiều linked phải kéo dài tới ngưỡng lượt cũ: một lần phóng linked xử lý hai đầu cùng lúc. Các kiểm tra stock màu, độ trộn, số mechanic, capacity và thắng tự nhiên vẫn được giữ.

## Bố cục và runtime

Mỗi box có điểm vào riêng, `PathIndexV2 = PathIndex + 1`; khoảng cách ngang từ miệng box tới điểm vào không quá **1,6 đơn vị Unity**. Bố cục bay xa/đè qua cột khác bị loại khi sinh và audit. Mỗi cặp linked có hai box gần nhau, cách không quá 2,2 đơn vị; bước chọn cặp kiểm tra còn đủ hàng xóm cho các cặp sau. Bộ cuối có 39 đường bao khác nhau.

`GameData` sở hữu nhiều cặp độc lập. Bấm, reservation, clone cho solver, Undo và cable đều dùng đúng cặp của box nguồn. Mỗi box thuộc tối đa một cặp; một lần phóng vẫn sở hữu reservation như trước. Level Editor hiển thị tất cả dây và tháo đúng cặp đang chọn. JSON/MessagePack giữ nguyên trường integer `LinkedPairId`; không đổi GUID, prefab, scene, package hoặc ProjectSettings trong phạm vi thay đổi này.

Các nhóm file chính: `Resources/LevelData/Level N.json`; runtime state/launch/cable và `DebugLevelSolver`; campaign builder/audit/learning curve/secret layout; các công cụ linked trong Level Editor. Chi tiết API và rà soát code ở [code review](2026-10-22-multi-linked-code-review.md).

## Kiểm tra đã chạy

- Compile: `PASS player and level editor compilation`, 0 lỗi. Có cảnh báo assembly và field Editor chưa dùng từ dự án kiểm chứng.
- Audit: `70 structural/simulation/contracts passed`; kiểm tra lời giải, stock màu, một box trống ở Normal, capacity màn nhẹ, curriculum, mechanic, footprint, nhóm, linked và điểm vào đường. Generator geometry catalog smoke cũng đạt.
- Unity: **26 dữ liệu có proof Play Mode khớp SHA hiện tại**: 1, 2, 7, 15, 18, 20, 21, 22, 24, 27, 30, 33, 34, 35, 38, 41, 43, 52, 55, 59, 60, 61, 64, 68, 69, 70. Các proof khác hiện mới có mô phỏng, chưa chạy thắng toàn bộ 70 dữ liệu cuối trong Unity.
- Lifecycle: 7, 18, 35, 52 qua restart lúc đang phóng, disable/enable rồi replay, phục hồi trạng thái đầu và đủ cable. Màn nhiều cặp ưu tiên ngắt cặp thứ hai/thứ ba khi có thể. Smoke hủy controller/root trong màn đang chạy đạt.
- Capacity: **70/70** giá trị khởi tạo đúng; không spawn indicator bản đồ trùng. **14/14** Hard/Expert qua Add Space 4→6 và tiêu thụ đúng một vật phẩm trong save kiểm thử riêng.
- No-Find source scan: 0 vi phạm. `git diff --check` đạt; metadata level, ProjectSettings và Packages không thay đổi.

CoreRulesVerification đã có lượt chạy thành công trong đợt triển khai nhiều cặp:

```text
No-Find policy verified: 0 violations across Assets/00Game/01Scripts.
Linked cubes: admission, boundaries, reservations, landing, cross-Undo, serialization and independent multiple pairs passed.
Core game verification passed.
```

Lượt chạy lặp cuối bị Windows Application Control chặn DLL với mã `0x800711C7`; không tính lượt đó là thành công. Log ở `../LoopMaster-ProjectData/AlternativeCampaign70/core-rules-final.log`.

## Phạm vi chưa xác nhận

Chưa đo độ khó, thời gian hoàn thành hay tỷ lệ restart bằng người chơi thật. Play Mode dùng fixture riêng, prefab/gameplay thực, bỏ tutorial và tăng tốc thời gian; không thay thế kiểm tra Game View/HUD hoặc animation bằng mắt. Nên xem trực tiếp các mốc 7, 18, 21–22, 52, 64 và 68–70 để đánh giá mật độ secret, màu dây và nhịp nghỉ.

[Atlas 70 màn](alternative-campaign-70/atlas.png) là sơ đồ footprint và liên kết, không phải ảnh Game View.