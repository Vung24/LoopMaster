# Cân chỉnh bộ level — 22/10/2026

## Bố cục và đường bay

Đã sửa 22 level có đoạn nối từ miệng box tới path quá dài: 10, 11, 12, 17, 20, 21, 24, 27, 29, 30, 31, 32, 46, 49, 50, 52, 54, 57, 58, 61, 65 và 69. 21 màn giữ nguyên khối và mechanic, chỉ chỉnh bố cục/điểm nối. Riêng Hard 30 được tạo lại với 9 box, 7 màu, hidden và hai khóa kề để có lời giải trên source gameplay hiện tại.

Mỗi box phải có index riêng, `PathIndexV2 = PathIndex + 1`, và khoảng cách ngang từ miệng box tới điểm nối không quá **1,6 đơn vị Unity**. Đoạn dài nhất trước khi sửa là **5,58 đơn vị**. Bố cục không đạt sẽ được thay bằng hai dãy quay ra path phía ngoài; cửa nhóm vẫn liền nhau và hai thành viên linked vẫn ở gần nhau. Generator và audit đều kiểm tra điều kiện này để không xuất lại kiểu nhiều cột bay qua nhau như ảnh tham chiếu.

Level 11, 12 và 32 cũng bỏ lựa chọn đường thẳng một bên làm bố cục mặc định. Bộ hiện tại vẫn có 45 đường bao khác nhau. `atlas.png` là sơ đồ footprint; không thay thế kiểm tra Game View và animation.

## Level 7

- Normal, 6 box, 5 màu, capacity 6 bằng số box.
- 5 box đầy, mỗi box 4 khối; chừa đúng 1 box trống.
- 20 khối tổng cộng, mỗi màu 4 khối. Mỗi stack có ba lớp màu, khối đáy secret, các khối phía trên lộ màu.
- Lời giải tham chiếu mô phỏng: **11 lượt**. Đây không phải số lượt tối ưu hoặc thời gian chơi thực tế.

Tiêu chí cân màn dễ theo yêu cầu mới: thường dư 1 box trống, capacity ngang số box hoặc hơn một chút. Level 7 đã áp dụng chính xác tiêu chí này. Rà các màn dạy/nghỉ khác còn thấy ngoại lệ ở 15, 22, 26, 38, 41, 46, 49, 50, 57, 61, 63, 65, 67, 69; chưa đồng loạt đổi dữ liệu những màn này trong lượt chỉnh level 7.

## Thứ tự học mechanic

Hidden bắt đầu ở 6; linked thường được giới thiệu ở 15, luyện tiếp ở 16–17. Không có khối vừa linked vừa hidden ở level 1–20. **Linked hidden bắt đầu ở 21**, ngay sau Hard 20: một đầu ẩn, một đầu rõ; 22 luyện hai đầu ẩn trên board nhỏ. Sau đó mới phối với khóa kề ở 33, luyện nhẹ ở 46, ice/khóa màu ở 52 và cửa nhóm ở 59.

| Màn giới thiệu | Mechanic chính | Box | Lượt tham chiếu |
|---|---|---:|---:|
| 6 | Hidden | 5 | 10 |
| 15 | Linked thường | 6 | 7 |
| 21 | Linked hidden | 6 | 9 |
| 23 | Một khóa màu | 6 | 12 |
| 29 | Một khóa kề | 6 | 10 |
| 37 | Một khối ice | 6 | 9 |
| 41 | Box dài | 7 | 11 |
| 49 | Một box đơn | 6 | 9 |
| 53 | Một cửa nhóm hai box | 6 | 9 |
| 56 | Một màu surprise | 6 | 10 |

Box dài dùng tối thiểu hai box theo ràng buộc của generator hiện có. Các màn giới thiệu đều là Normal, chỉ có 5–7 box, ít màu và đường rộng. Các số lượt là lời giải mô phỏng đã kiểm tra, không phải số lượt tối ưu hoặc thời gian người chơi thực tế.

## Hard/Expert và màn nghỉ

Hard 10 dùng hidden đã học. Từ Hard 20 trở đi phải có ít nhất hai mechanic đã được giới thiệu; các màn cuối tiếp tục phối nhiều mechanic. Không dùng một màn ghép màu thường làm Hard/Expert.

Mọi Hard/Expert có màn kế tiếp trong bộ đều được nối bằng một màn Normal nhỏ hơn: **10→11, 20→21, 25→26, 30→31, 35→36, 40→41, 45→46, 55→56, 60→61, 62→63, 64→65, 66→67, 68→69**. Level 70 kết thúc bộ. Đoạn 60–70 luân phiên khó và nghỉ, không còn chuỗi Hard liên tiếp.

Màn nghỉ có tối đa 8 box, tối đa 6 màu thường, đường 6–7 slot và lời giải tham chiếu không quá hai lần số box. Level 20 vẫn là Hard, 9 box/7 màu/4 slot; level 21 chỉ còn 6 box/4 màu/7 slot. Level 10 và 46 khác nhau về bố cục, sức chứa đường và mechanic trung tâm.

## Kiểm tra

- Audit từng đạt 70 structural/simulation/contracts; lần chạy với source gameplay hiện tại phát hiện lời giải cũ của 33, 47 và 68 không còn hợp lệ. Đã tìm lại lời giải 33 và 47; Expert 68 đang được kiểm tra riêng. Chưa kết luận audit cuối toàn bộ chiến dịch thành công.
- Compile: `PASS player and level editor compilation`. Manifest kiểm thử đã đồng bộ file runtime hiện có; có cảnh báo tham chiếu assembly/field chưa dùng từ project kiểm thử.
- No-Find: `PASS No-Find source scan: 0 violations`.
- Metadata: `PASS campaign script metadata GUIDs`.
- Unity Play Mode: bản cuối level 7 chơi thắng với 11 lượt, không booster và không cần lập lại kế hoạch; restart khi đang bay, disable/enable rồi replay đều phục hồi trạng thái đầu. Smoke destruction cũng đạt. Level 30 đã chơi lại thành công.

CoreRulesVerification đã chạy thành công ở đầu đợt chỉnh. Lượt lặp sau bị Windows Application Control chặn DLL với mã `0x800711C7`, kể cả khi chạy ngoài sandbox; không báo lượt lặp đó là thành công.

Chưa đo độ khó, thời gian hoàn thành hay tỷ lệ restart bằng người chơi. Cần xem trực tiếp reveal/Undo ở 7 và 21–22, màu dây linked trước reveal, đường bay ở 11–12, và cảm giác chuyển từ Hard sang màn nghỉ. Các mục tiêu lượt cũ của một số màn cuối được giữ làm tín hiệu cần playtest, không kéo dài lời giải để khớp chỉ tiêu cũ.