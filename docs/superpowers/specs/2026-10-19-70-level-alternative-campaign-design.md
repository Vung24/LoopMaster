# Thiết Kế Lộ Trình 70 Màn Chơi Mới (Alternative Campaign - 70 Levels)

## 1. Mục Đích & Bối Cảnh (Intent & Context)
- **Mục đích:** Xây dựng một bộ 70 màn chơi độc lập, hoàn chỉnh từ đầu đến cuối cho LoopMaster, bao quát toàn bộ cơ chế gameplay (mechanics), chướng ngại vật (obstacles), các loại hộp (boxes), kiểu đường băng chuyền (roads), và các mốc mở khóa trợ giúp (boosters).
- **Tại sao cần làm:** Dự án cần một bộ màn chơi mẫu mực để kiểm thử toàn diện trải nghiệm người chơi (onboarding -> progression -> mastery), tối ưu độ cong thử thách và thử nghiệm cân bằng trước khi đưa vào luồng campaign chính thức.
- **Môi trường lưu trữ:** Lưu trữ toàn bộ 70 file JSON trong một thư mục riêng biệt (`Assets/00Game/05LevelData/AlternativeCampaign_70/`) để bảo vệ an toàn cho dữ liệu 150+ màn chơi hiện tại của game.

---

## 2. Mục Tiêu, Phi Mục Tiêu & Chống Mục Tiêu (Goals, Non-Goals, Anti-Goals)

### Mục tiêu (Goals)
1. **70 màn chơi hoàn chỉnh:** Mỗi màn là một file JSON chuẩn cấu trúc `Puzzle` / `layout`, có đầy đủ cấu hình đường đi (`Paths`), danh sách hộp (`Boxes`), khối màu (`Cubes`), chướng ngại vật và độ khó (`HardLevel`).
2. **Nhịp độ sư phạm bài bản (Pedagogical Progression):**
   - Giới thiệu tuần tự từng cơ chế mới với màn tutorial/showcase trực quan.
   - Mỗi cơ chế gameplay mới có ít nhất 2 màn thực hành trước cơ chế gameplay tiếp theo. Hình dạng đường là biến thể bố cục, không tính là cơ chế mới; màn giới thiệu cơ chế phải dùng hình dạng đường đã quen thuộc.
   - Giữ nguyên các mốc mở khóa Booster kinh điển ở đầu game: **Undo** (Màn 4), **Shuffle** (Màn 7), **Add Space** (Màn 9).
3. **Bao quát toàn bộ danh mục cơ chế được duyệt cho campaign này (không tuyên bố bao phủ toàn bộ engine):**
   - 2 kiểu đường: Vòng khép kín (Oval, Chữ nhật bo góc) & Băng chuyền mở có Cổng Vào/Ra (Thẳng, Chữ U, Chữ S, Uốn lượn).
   - 3 loại hộp: Hộp thường (Normal 4 ô), Hộp đơn (`Single`, có thể chứa hàng đợi khối; campaign giới thiệu bằng 1 khối), Hộp dài (Long 6 ô, nhóm màu 101–105).
   - 7 nhóm chướng ngại vật & liên kết: Khối ẩn (`Secret`), Khóa màu đơn sắc (`ColorLock`), Khóa liền kề (`AdjacentLock`), Khối băng đếm ngược (`CubeNumLock`), Cặp khối nối dây xuyên hộp (`CubeData.LinkedPairId`), Cửa cuốn nhóm dùng chìa (`GroupRollDoor` & `GroupKey`), Khối biến ảo (`Surprise`). Không đồng nhất `LinkedPairId` với trường `BoxData.LinkedCubes`.
4. **Đảm bảo tính giải được (Solvability & Verification):** Reverse-action scrambling là cách tạo ứng viên, không tự động là bằng chứng cho dữ liệu cuối cùng. Cả 70 màn phải vượt structural validation, replay lời giải không dùng booster trên JSON cuối cùng và kiểm tra thắng trong Unity Play Mode. Màn chưa tìm được lời giải được đánh dấu chưa xác minh và không được đưa vào bộ nghiệm thu.
5. **Công cụ kiểm thử linh hoạt (Playtest Dual-Mode):** 
   - **Chế độ 1 (Sandbox 1-Click):** Mở nhanh bất kỳ màn nào từ 1–70 trong `LoopLevelEditorWindow` để test ngay tức thì.
   - **Chế độ 2 (Campaign Playtest):** Một phiên thử nghiệm chỉ dành cho Editor, đọc riêng `AlternativeCampaign_70`, có tiến trình và trạng thái tutorial/booster thử nghiệm, bắt đầu ở màn 1 và kết thúc tại màn 70. Không dùng save thật hoặc fallback sang campaign gốc.

### Phi mục tiêu (Non-Goals)
- Không ghi đè hoặc thay đổi các file trong `Assets/00Game/Resources/LevelData/` trong giai đoạn này.
- Không thay đổi luật gameplay, thuật toán di chuyển, điều kiện thắng/thua hoặc giới hạn cơ chế runtime. Cho phép hook tối thiểu có điều kiện `UNITY_EDITOR` tại loader, flow và nơi xác định catalog/tiến trình để chạy campaign thử nghiệm; hành vi player build giữ nguyên. Ưu tiên hook và sandbox hiện có, không sửa scene/prefab để bật chế độ thử.
- Các tính năng ngoài phạm vi gồm `MultiColorLock`, `IsEnterOnly`, `LimitedColor`, khóa đếm trên hộp (`BoxData.NumLock`), `IsFixed` và liên kết nội bộ `BoxData.LinkedCubes`. Chỉ đưa vào campaign sau khi có thiết kế và kiểm chứng riêng; không tự thêm vào các màn tổng hợp.

### Chống mục tiêu (Anti-Goals)
- Tránh tạo màn chơi quá phụ thuộc vào may rủi ngẫu nhiên hoặc bẫy người chơi vào thế cờ cụt không thể thắng.
- Tuyệt đối không sử dụng `transform.Find` hay `GameObject.Find` trong bất kỳ script tiện ích/editor nào hỗ trợ (tuân thủ nghiêm ngặt quy tắc của dự án).
- Tránh dồn nhiều cơ chế phức tạp cùng một lúc ở giai đoạn đầu khiến người chơi bị ngợp thông tin.

---

## 3. Lộ Trình Phân Bổ 70 Màn Chơi (70-Level Curriculum Roadmap)

### Điều chỉnh nội dung sau playtest (revision 2)

- Không bắt buộc số màu nhỏ hơn số hộp. Nhiều màn dùng số màu bằng số hộp thường, không có hộp đệm ban đầu; tính giải được phải được xác minh trên JSON cuối cùng.
- Sau onboarding, thay đổi số hộp trong khoảng 6–13; các chương sau xen kẽ 8–12 và các màn cuối 12–13. Không dùng riêng số hộp làm thước đo độ khó.
- Capacity được cấu hình riêng từng màn: các màn Hard/Expert là 4; màn thường thay đổi 5–8, riêng bài Add Space là 4.
- Hình học tham khảo trực tiếp các JSON trong `Assets/00Game/Resources/LevelData`, chỉ tái sử dụng đường và bố trí không gian, không sao chép nội dung puzzle hoặc chướng ngại. Ghi số màn tham khảo trong `profile.GeometryReference` của báo cáo. Tuyến thẳng phải giữ đường thẳng; các biến thể còn lại bảo toàn họ mở/kín thay vì ép lặp oval/tròn. Không thay đổi JSON gốc.
- Tăng khoảng cách tọa độ mẫu tối thiểu 8%, chỉ tăng thêm theo bước 4% khi footprint còn giao nhau (tối đa 24%); từ chối box giao nhau, đường tự cắt, nhóm cửa không liền nhau. Không phóng to mesh box hoặc thay camera để che bố cục rỗng.
- Một số màn đầy 8–12 hộp thường/màu dùng thêm các mẫu trộn 3–4 tầng theo thứ tự tiếp xúc đường, bảo toàn đúng 4 khối mỗi màu. Khóa/cửa/băng tạo ràng buộc trên chuỗi màu thay vì chỉ tăng ngẫu nhiên độ phân tán. Báo cáo ghi MixingRecipe để phân biệt các mẫu.
- Màn 1–2 dùng đường thẳng ngắn có Enter/Exit sát cụm hộp để khối sớm quay lại vùng nhận; màn 3–4 dùng vòng chữ nhật bo góc ôm sát lưới hộp. Hộp dài dùng bố trí riêng theo footprint, tránh vòng tròn dư khoảng trống. Các màn sau lấy các contour khác nhau từ LevelData.
- Tăng số bước trộn theo chương, loại ứng viên có ít hơn một nửa hộp thường chứa nhiều màu hoặc lời giải tham chiếu ngắn hơn số hộp (áp dụng từ màn 5). Đây là tiêu chí chống bài quá nông, chưa thay thế đánh giá độ khó bởi người chơi.
- Revision mới làm mất hiệu lực các bằng chứng theo hash cũ; phải sinh lại và replay Unity trước khi ghi nhận đạt kiểm chứng.


### Giai đoạn 1: Làm Quen Đường Mở, Vòng Kín & Mở Khóa Booster (Màn 1 – 10)
*Mục tiêu: Dạy luật chơi cốt lõi (chạm khay để đẩy khối ra băng chuyền, ghép đủ 4 khối cùng màu để giải phóng khay) và lần lượt giới thiệu 3 booster thiết yếu.*

| Màn | Trọng tâm / Cơ chế mới | Kiểu đường & Cấu hình | Độ khó | Ghi chú nhịp độ |
|---|---|---|---|---|
| **1** | **Tutorial Cơ Bản 1** | Đường thẳng ngắn có Enter/Exit; 2 hộp (0 đơn, 0 dài), 1 màu thường, capacity 4 | Normal | Một hộp chứa màu, một hộp nhận; xác minh trạng thái đầu chưa thắng và có thao tác hợp lệ; hướng dẫn chỉ tay (Finger) chạm hộp |
| **2** | **Tutorial Cơ Bản 2** | Đường thẳng ngắn có Enter/Exit; 2 hộp (0 đơn, 0 dài), 2 màu thường, capacity 6 | Normal | Hai hộp đầy trộn 2 màu, không thêm hộp đệm trống; hướng dẫn ghép cặp màu |
| **3** | Luyện tập vòng lặp kín | Vòng chữ nhật bo góc gọn; 4 hộp (0 đơn, 0 dài), 3 màu thường, capacity 7 | Normal | Rèn luyện khả năng quan sát nhịp quay |
| **4** | **Mở Khóa Booster: UNDO** | Vòng chữ nhật bo góc gọn; 5 hộp (0 đơn, 0 dài), 4 màu thường, capacity 6 | Normal | Minh họa Undo nhưng vẫn giải được không booster; thêm chỗ dự phòng để tránh bài mở đầu phụ thuộc quá nhiều vào thời điểm luân chuyển khi mọi hộp đầy |
| **5** | Luyện tập kiểm soát nhịp | Vòng kín biến thể từ LevelData; 6 hộp (0 đơn, 0 dài), 5 màu thường, capacity 5 | Normal | Củng cố kỹ năng xoay chuyển màu |
| **6** | **Cơ Chế Mới: KHỐI ẨN (Secret ?)** | Vòng kín biến thể từ LevelData; 7 hộp (0 đơn, 0 dài), 6 màu thường, capacity 7 | Normal | Khối chỉ lộ màu khi khối trên được đẩy đi |
| **7** | **Mở Khóa Booster: SHUFFLE** | Vòng kín biến thể từ LevelData; 8 hộp (0 đơn, 0 dài), 8 màu thường, capacity 6 | Normal | Minh họa lợi ích đảo màu; vẫn có lời giải không dùng booster |
| **8** | Luyện tập phối hợp khối ẩn | Vòng kín biến thể từ LevelData; 7 hộp (0 đơn, 0 dài), 7 màu thường, capacity 5 | Normal | Thực hành đọc bàn cờ khi có nhiều khối ẩn |
| **9** | **Mở Khóa Booster: ADD SPACE** | Vòng kín biến thể từ LevelData; 9 hộp (0 đơn, 0 dài), 8 màu thường, capacity 4 | Normal | Giới thiệu công cụ mở rộng ô trống băng chuyền |
| **10** | **Màn Thử Thách Giai Đoạn 1** | Vòng kín biến thể từ LevelData; 7 hộp thường đầy, 7 màu Type 1–7, capacity 4, khối ẩn 10% | Hard | Trộn hai cụm màu 2+2 mỗi hộp, khoảng 2 khối ẩn; thử thách đầu tiên tập trung đọc thứ tự nhận màu, không dồn 9 màu và 7 khối ẩn |

---

### Giai đoạn 2: Băng Chuyền Mở & Khay Đơn Giải Phóng (Màn 11 – 22)
*Mục tiêu: Mở rộng đường có Enter/Exit đã làm quen ở màn 1–2 sang nhiều hộp và nhiều dạng đường hơn. Trên băng chuyền mở, khối đi qua Cổng Ra sẽ vòng lặp lại Cổng Vào, đòi hỏi căn thời điểm và quản lý dung lượng đường chạy.*

| Màn | Trọng tâm / Cơ chế mới | Kiểu đường & Cấu hình | Độ khó | Ghi chú nhịp độ |
|---|---|---|---|---|
| **11** | **BĂNG CHUYỀN THẲNG MỞ: NHIỀU HỘP** | Tuyến thẳng mở; 8 hộp (0 đơn, 0 dài), 8 màu thường, capacity 7 | Normal | Khối đi qua cổng vào/ra, yêu cầu tính toán lượt |
| **12** | Luyện tập băng chuyền thẳng | Tuyến thẳng mở; 8 hộp (0 đơn, 0 dài), 7 màu thường, capacity 5 | Normal | Căn thời điểm thả khối chính xác |
| **13** | **Cơ Chế Mới: ĐƯỜNG chữ S MỞ** | Tuyến mở biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 10 màu thường, capacity 6 | Normal | Đường cong tạo khoảng cách tiếp xúc khác nhau |
| **14** | Luyện tập đường chữ S | Tuyến mở biến thể từ LevelData; 9 hộp (0 đơn, 0 dài), 8 màu thường, capacity 7 | Normal | Phối hợp quan sát chiều di chuyển |
| **15** | **Cơ Chế Mới: HỘP ĐƠN (Single Box)** | Vòng kín biến thể từ LevelData; 8 hộp (1 đơn, 0 dài), 6 màu thường, capacity 6 | Normal | Hộp đơn chứa 1 khối chốt chặn, giải phóng trước |
| **16** | Luyện tập hộp đơn trên vòng kín | Vòng kín biến thể từ LevelData; 9 hộp (1 đơn, 0 dài), 8 màu thường, capacity 5 | Normal | Củng cố hộp đơn trên bố cục quen thuộc |
| **17** | Luyện tập hộp đơn trên đường cong | Tuyến mở biến thể từ LevelData; 8 hộp (1 đơn, 0 dài), 6 màu thường, capacity 7 | Normal | Mở khóa khối đơn để giải tỏa luồng di chuyển |
| **18** | Biến thể đường chữ U & hộp đơn | Tuyến mở biến thể từ LevelData; 10 hộp (1 đơn, 0 dài), 8 màu thường, capacity 5 | Normal | Làm quen thứ tự tiếp xúc của đường S, không thêm chướng ngại mới |
| **19** | Tuyến mở uốn lượn đa đoạn | Tuyến mở biến thể từ LevelData; 9 hộp (0 đơn, 0 dài), 9 màu thường, capacity 6 | Normal | Thử thách khả năng quản lý đường dài |
| **20** | **Màn Thử Thách Giai Đoạn 2** | Tuyến mở biến thể từ LevelData; 12 hộp (1 đơn, 0 dài), 10 màu thường, capacity 4 | Hard | Căng thẳng dung lượng băng chuyền |
| **21** | Tối ưu hóa lượt đi | Tuyến mở biến thể từ LevelData; 8 hộp (0 đơn, 0 dài), 8 màu thường, capacity 8 | Normal | Màn thư giãn phục hồi nhịp sau màn Hard |
| **22** | Thành thạo băng chuyền mở | Tuyến mở biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 9 màu thường, capacity 5 | Normal | Hoàn tất làm chủ đường mở |

---

### Giai đoạn 3: Hệ Thống Khóa & Ràng Buộc Khay (Màn 23 – 36)
*Mục tiêu: Đưa vào các chướng ngại vật logic khóa hộp. Hộp bị khóa không thể giải phóng khối ra đường cho đến khi thỏa mãn điều kiện giải khóa.*

| Màn | Trọng tâm / Cơ chế mới | Kiểu đường & Cấu hình | Độ khó | Ghi chú nhịp độ |
|---|---|---|---|---|
| **23** | **Cơ Chế Mới: KHÓA MÀU (Color Lock)** | Vòng kín biến thể từ LevelData; 8 hộp (0 đơn, 0 dài), 7 màu thường, capacity 6 | Normal | Phải thu thập đủ màu chìa khóa để mở hộp |
| **24** | Luyện tập khóa màu đơn sắc | Tuyến mở biến thể từ LevelData; 9 hộp (0 đơn, 0 dài), 9 màu thường, capacity 7 | Normal | Thứ tự mở khóa quyết định thắng bại |
| **25** | Khóa màu kết hợp khối ẩn | Vòng kín biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 10 màu thường, capacity 5 | Normal | Khối cần giải lại nằm ẩn dưới đáy hộp khác |
| **26** | **Khóa màu kép (2 Khóa độc lập)** | Vòng kín biến thể từ LevelData; 8 hộp (0 đơn, 0 dài), 8 màu thường, capacity 6 | Normal | Người chơi phải chọn mở khóa nào trước |
| **27** | Chuỗi khóa màu liên hoàn | Tuyến mở biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 9 màu thường, capacity 5 | Normal | Lập kế hoạch theo chuỗi ưu tiên |
| **28** | Nhịp độ cân bằng | Vòng kín biến thể từ LevelData; 9 hộp (0 đơn, 0 dài), 8 màu thường, capacity 8 | Normal | Màn thư giãn, củng cố sự tự tin |
| **29** | **Cơ Chế Mới: KHÓA LIỀN KỀ (Adjacent Lock)**| Vòng kín biến thể từ LevelData; 8 hộp (0 đơn, 0 dài), 7 màu thường, capacity 6 | Normal | Hộp A chỉ mở khi hộp B bên cạnh hoàn thành |
| **30** | Luyện tập khóa liền kề theo dãy | Vòng kín biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 10 màu thường, capacity 4 | Hard | Hiệu ứng domino giải tỏa từng hộp |
| **31** | Phối hợp khóa liền kề & khóa màu | Tuyến mở biến thể từ LevelData; 9 hộp (0 đơn, 0 dài), 9 màu thường, capacity 5 | Normal | Phân tách hai lớp điều kiện mở |
| **32** | Khóa liền kề trên tuyến mở | Tuyến thẳng mở; 10 hộp (0 đơn, 0 dài), 9 màu thường, capacity 6 | Normal | Quản lý không gian trên băng chuyền một chiều |
| **33** | Cụm khóa 3 hộp liên tiếp | Vòng kín biến thể từ LevelData; 9 hộp (0 đơn, 0 dài), 8 màu thường, capacity 5 | Normal | Rèn luyện tư duy quy hoạch bước đi xa |
| **34** | Đa khóa trên bàn cờ lớn | Vòng kín biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 10 màu thường, capacity 7 | Normal | Phức tạp hóa bàn cờ vừa phải |
| **35** | **Màn Thử Thách Giai Đoạn 3** | Vòng kín biến thể từ LevelData; 12 hộp (0 đơn, 0 dài), 12 màu thường, capacity 4 | Hard | Bài thi tổng hợp toàn bộ hệ thống khóa |
| **36** | Phục hồi nhịp độ | Vòng kín biến thể từ LevelData; 8 hộp (0 đơn, 0 dài), 6 màu thường, capacity 8 | Normal | Xả stress trước khi bước sang giai đoạn 4 |

---

### Giai đoạn 4: Khối Băng Đếm Ngược & Hộp Dài (Màn 37 – 48)
*Mục tiêu: Đưa vào yếu tố số đếm (Hit counter) và kích thước khay đặc biệt (Hộp Dài luôn đi theo cặp để đảm bảo ghép màu hợp lệ).*

| Màn | Trọng tâm / Cơ chế mới | Kiểu đường & Cấu hình | Độ khó | Ghi chú nhịp độ |
|---|---|---|---|---|
| **37** | **Cơ Chế Mới: KHỐI BĂNG / ĐẾM NGƯỢC (NumLock)**| Vòng kín biến thể từ LevelData; 9 hộp (0 đơn, 0 dài), 8 màu thường, capacity 7 | Normal | Giảm đếm theo sự kiện hộp hoàn thành; hộp chứa băng không được có khóa chặn khi cần giảm đếm |
| **38** | Luyện tập khối băng số cao | Vòng kín biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 10 màu thường, capacity 5 | Normal | Đảm bảo còn đủ sự kiện hoàn thành hộp khả dụng trước khi cần phóng khối băng |
| **39** | Khối băng kết hợp hộp đơn | Tuyến mở biến thể từ LevelData; 9 hộp (1 đơn, 0 dài), 7 màu thường, capacity 6 | Normal | Khối từ hộp đơn hỗ trợ hoàn thành hộp khác; không coi việc phóng/hấp thụ riêng lẻ là giảm đếm |
| **40** | Khối băng tầng dưới khối ẩn | Vòng kín biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 9 màu thường, capacity 5 | Normal | Bất ngờ hai lớp chướng ngại vật |
| **41** | **Cơ Chế Mới: HỘP DÀI (Long Box - 6 ô)** | Vòng chữ nhật ôm sát hộp dài; 10 hộp (0 đơn, 2 dài), 7 màu thường, capacity 6 | Normal | Hộp dài chứa 6 khối, giải phóng lượng lớn |
| **42** | Luyện tập 4 hộp dài đa màu | Vòng chữ nhật ôm sát hộp dài; 10 hộp (0 đơn, 4 dài), 5 màu thường, capacity 5 | Normal | Phối hợp luân chuyển các khối dài |
| **43** | Hộp dài kết hợp khóa màu | Tuyến mở dành cho hộp dài; 12 hộp (0 đơn, 2 dài), 10 màu thường, capacity 7 | Normal | Ưu tiên giải phóng chìa khóa cho hộp dài |
| **44** | Hộp dài & khối băng song hành | Vòng chữ nhật ôm sát hộp dài; 10 hộp (0 đơn, 2 dài), 7 màu thường, capacity 5 | Normal | Tính giảm đếm theo số hộp hoàn thành, không theo số khối trong hộp dài |
| **45** | **Màn Thử Thách Giai Đoạn 4** | Tuyến mở dành cho hộp dài; 12 hộp (0 đơn, 2 dài), 9 màu thường, capacity 4 | Hard | Bài toán cân đối thể tích băng chuyền lớn |
| **46** | Cân bằng nhịp độ | Vòng kín biến thể từ LevelData; 9 hộp (0 đơn, 0 dài), 7 màu thường, capacity 8 | Normal | Màn chuyển tiếp thư giãn |
| **47** | Phối hợp khối băng & khóa liền kề | Vòng kín biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 10 màu thường, capacity 6 | Normal | Tương tác chéo giữa hai cơ chế |
| **48** | Hoàn thiện kỹ năng hộp dài | Vòng chữ nhật ôm sát hộp dài; 12 hộp (1 đơn, 2 dài), 8 màu thường, capacity 5 | Normal | Chuẩn bị bước vào các cơ chế cao cấp |

---

### Giai đoạn 5: Cặp Khối Nối Dây, Cửa Cuốn & Khối Biến Ảo (Màn 49 – 60)
*Mục tiêu: Đưa vào các cơ chế liên kết phức tạp nhất của game.*

| Màn | Trọng tâm / Cơ chế mới | Kiểu đường & Cấu hình | Độ khó | Ghi chú nhịp độ |
|---|---|---|---|---|
| **49** | **Cơ Chế Mới: CẶP KHỐI NỐI DÂY (Linked Cubes)** | Vòng kín biến thể từ LevelData; 9 hộp (0 đơn, 0 dài), 8 màu thường, capacity 6 | Normal | Hai khối phải được phóng ra đồng thời; bài giới thiệu có một hộp đệm |
| **50** | Cặp nối dây một bên bị kẹt | Vòng kín biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 9 màu thường, capacity 5 | Normal | Học cách thông đường cho khối bên kia trước |
| **51** | Cặp nối dây khác màu + khối ẩn 20% | Vòng kín biến thể từ LevelData; 9 hộp (0 đơn, 0 dài), 9 màu thường, capacity 7 | Normal | Bổ sung biến thể so với cặp cùng màu ở màn 49; tối đa một cặp trong puzzle |
| **52** | Cặp nối dây + khóa màu trên băng chuyền mở | Tuyến mở biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 10 màu thường, capacity 5 | Normal | Căn thời điểm thả dây trên đường một chiều |
| **53** | **Cơ Chế Mới: CỬA CUỐN NHÓM (Group Roll Door)**| Vòng kín biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 9 màu thường, capacity 6 | Normal | Thu thập khối chứa chìa khóa để cuốn cửa lên |
| **54** | Cửa cuốn nhóm chắn 3 hộp | Vòng kín biến thể từ LevelData; 12 hộp (0 đơn, 0 dài), 12 màu thường, capacity 5 | Normal | Mở khóa cả một cụm tài nguyên lớn |
| **55** | Cửa cuốn kết hợp khối ẩn | Tuyến mở biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 10 màu thường, capacity 7 | Normal | Chuỗi truy tìm chìa khóa giải cứu cụm hộp |
| **56** | **Cơ Chế Mới: KHỐI BIẾN ẢO (Surprise Cube)** | Vòng kín biến thể từ LevelData; 9 hộp (0 đơn, 0 dài), 9 màu thường, capacity 6 | Normal | Khối đổi thành màu thật khi lấp đầy hộp |
| **57** | Luyện tập khối biến ảo | Vòng kín biến thể từ LevelData; 10 hộp (0 đơn, 0 dài), 10 màu thường, capacity 5 | Normal | Dự đoán màu biến đổi để ghép tiếp |
| **58** | Khối biến ảo kết hợp hộp đơn | Tuyến mở biến thể từ LevelData; 12 hộp (1 đơn, 0 dài), 10 màu thường, capacity 7 | Normal | Hộp đơn hỗ trợ gỡ kẹt màu biến ảo |
| **59** | Cửa cuốn nhóm + Cặp nối dây | Vòng kín biến thể từ LevelData; 12 hộp (0 đơn, 0 dài), 11 màu thường, capacity 5 | Normal | Tuyệt chiêu liên hoàn giữa 2 cơ chế tương tác |
| **60** | **Màn Thử Thách Giai Đoạn 5** | Vòng kín biến thể từ LevelData; 12 hộp (0 đơn, 0 dài), 12 màu thường, capacity 4 | Hard | Thử thách đỉnh cao kết hợp 3 cơ chế cao cấp |

---

### Giai đoạn 6: Đỉnh Cao Thử Thách & Bậc Thầy Phân Phối (Màn 61 – 70)
*Mục tiêu: Các màn chơi tổng hợp quy mô lớn, thử thách toàn diện người chơi với nhãn Hard và Very Hard.*

| Màn | Trọng tâm / Đặc điểm giải đố | Cấu hình & Độ phức tạp | Độ khó | Ghi chú thiết kế |
|---|---|---|---|---|
| **61** | Đấu trường băng chuyền mở chữ U | Tuyến mở biến thể từ LevelData; 12 hộp (1 đơn, 0 dài), 11 màu thường, capacity 4 | Hard | Tính toán dòng chảy dung lượng đường; dây chỉ nằm trong hộp thường |
| **62** | Mê cung cửa cuốn kép + khối ẩn 20% | Vòng kín biến thể từ LevelData; 12 hộp (0 đơn, 0 dài), 12 màu thường, capacity 4 | Hard | Hai chìa khóa độc lập, quy hoạch mở tuần tự |
| **63** | Đại chiến Hộp Dài & Khóa Màu | Vòng chữ nhật ôm sát hộp dài; 12 hộp (0 đơn, 2 dài), 10 màu thường, capacity 4 | Hard | Giải phóng thể tích lớn trong không gian hẹp |
| **64** | Cặp dây nối xuyên chướng ngại | Vòng kín biến thể từ LevelData; 12 hộp (0 đơn, 0 dài), 12 màu thường, capacity 4 | Hard | Không gắn NumLock lên thành viên cặp dây; kiểm chứng thứ tự gỡ chặn |
| **65** | Trận địa liên hoàn toàn diện | Vòng kín biến thể từ LevelData; 12 hộp (1 đơn, 0 dài), 11 màu thường, capacity 4 | Hard | Kiểm tra khả năng ứng biến linh hoạt |
| **66** | Đỉnh cao nhịp độ đường mở | Tuyến mở biến thể từ LevelData; 12 hộp (0 đơn, 0 dài), 12 màu thường, capacity 4 | Hard | Giới hạn ô đệm đòi hỏi độ chính xác cao |
| **67** | Bão tố biến ảo & đếm ngược | Vòng kín biến thể từ LevelData; 12 hộp (0 đơn, 0 dài), 12 màu thường, capacity 4 | Hard | Biến số thay đổi liên tục trên bàn cờ |
| **68** | **ĐẠI THỬ THÁCH (Climax 1)** | Vòng kín biến thể từ LevelData; 13 hộp (0 đơn, 0 dài), 12 màu thường, capacity 4 | **Very Hard** | Đòi hỏi quan sát toàn cục và tư duy đa bước |
| **69** | Đòn bẩy hộp đơn | Tuyến mở biến thể từ LevelData; 12 hộp (2 đơn, 0 dài), 10 màu thường, capacity 4 | Hard | Tận dụng khối đơn để giải phóng thế cờ hiểm |
| **70** | **BẬC THẦY LOOP MASTER (Grand Finale)** | Vòng chữ nhật ôm sát hộp dài; 13 hộp (1 đơn, 2 dài), 10 màu thường, capacity 4 | **Very Hard** | Chỉ ghép các tương tác đã được kiểm chứng; hoàn thành màn này kết thúc campaign thử nghiệm |

---

### Nhịp phối hợp cơ chế ở nửa sau

- Giữ hai bài đầu của mỗi cơ chế mới tập trung vào cơ chế đó. Các bài luyện tập sau mới ghép cơ chế đã học.
- 39: hộp đơn + băng; 40: khối ẩn + băng; 43: hộp dài + khóa màu; 44–45: hộp dài + băng; 47: khóa liền kề + băng; 48: hộp đơn + hộp dài + khối ẩn.
- 51: dây khác màu + khối ẩn; 52: dây + khóa màu; 55: cửa cuốn + khối ẩn; 58: Surprise + hộp đơn; 59: cửa cuốn + dây. Màn 60 là bài tổng hợp ba cơ chế.
- Từ 58 đến 70, mỗi màn có ít nhất hai họ cơ chế khác nhau. Hai cửa cuốn vẫn chỉ tính là một họ; vì vậy màn 62 thêm khối ẩn để tạo phối hợp thật.
- Luân phiên cặp cơ chế và hình học đường; không lặp một cặp qua cả chương. Màn 46 vẫn là bài phục hồi nhịp trước cơ chế dây.
- Không đặt chìa trong chính nhóm đang khóa, không gắn băng lên thành viên dây. Các phối hợp mới phải có lời giải không booster và replay thắng trong Unity; không chỉ cộng cờ JSON.

## 4. Kiến Trúc Kỹ Thuật & Cấu Trúc Dữ Liệu (Technical Architecture)

### 4.1. Định Dạng & Lưu Trữ
- Thư mục lưu trữ: `Assets/00Game/05LevelData/AlternativeCampaign_70/`
- Tên file chuẩn: `Level 1.json`, `Level 2.json`, ..., `Level 70.json`
- Cấu trúc file tuân thủ chính xác schema JSON của `ExportedPuzzleJsonDecoder`:
  ```json
  {
    "level_number": 1,
    "puzzle_id": 1,
    "layout": {
      "Version": 4,
      "HardLevel": 0,
      "Size": 10.0,
      "PathLength": 8,
      "PathSectionDistance": 0.2,
      "Boxes": [ ... ],
      "Paths": [ ... ],
       "MapData": [ ... ],
      "Groups": [ ... ],
      "Covers": [ ... ],
      "SurpriseDatas": [ ... ]
    }
  }
  ```

### 4.2. Bộ Sinh & Kiểm Tra Màn Chơi Tự Động (Alternative Campaign Builder)
- Xây dựng một Editor script chuyên biệt `AlternativeCampaign70Builder.cs` đặt trong `Assets/00Game/01Scripts/Editor/LevelEditor/`:
  - Kế thừa và sử dụng các API sẵn có trong `LoopLevelAuthoring`, `LoopLevelArchetypes`, `LoopLevelBoardLayout`.
   - Sinh ứng viên bằng reverse-action scrambling, sau đó áp dụng cơ chế và kiểm chứng lại JSON cuối cùng. Không kế thừa trạng thái `verified` từ generator nếu không có replay thắng thực tế.
   - Tích hợp `LoopLevelAuthoring.Validate` để kiểm tra cấu trúc/chỉ số. Kiểm tra hình học đường, giao cắt, khoảng trống hộp và dây là bước riêng; không coi validator hiện tại đã đảm bảo các thuộc tính này.
   - Menu `Tools/LoopMaster/Tạo 70 Màn Chơi Mới (Alternative Campaign)` chạy theo từng bước có thể hủy, lưu trạng thái xác minh và báo cáo lỗi theo màn. Chỉ đưa đủ 70 màn đã đạt nghiệm thu vào bộ campaign hoàn chỉnh.

### 4.3. Tiện Ích Chơi Thử Nhanh (Dual-Mode Playtest Support)
1. **Sandbox Playtest:** Cung cấp menu hoặc nút bấm trong `LoopLevelEditorWindow` để nạp trực tiếp bất kỳ màn nào trong `AlternativeCampaign_70` vào `LoopLevelPlaytest.Play()` (chơi thử tức thì, cô lập không ảnh hưởng save).
2. **Sequential Campaign Session:** Bổ sung phiên thử trong Editor (hoặc cửa sổ Debug) theo hợp đồng tại mục 4.5. Không chỉ đổi folder của `BuiltinPuzzleLoader`.

### 4.4. Hợp Đồng Cơ Chế & Bằng Chứng Lời Giải
- Cặp dây xuyên hộp: dùng `CubeData.LinkedPairId`; tối đa một ID cặp dương và đúng hai thành viên trong hai hộp Normal khác nhau. Màu thành viên từ 1–14, `GroupKeyId = 0`, `NumLock = 0`, `IsFixed = 0`, tuân thủ `LinkedCubePair.Validate`. Cặp dây không được mang chìa khóa hoặc là khối băng. Cửa cuốn và dây có thể cùng xuất hiện chỉ khi chuỗi mở cửa/phóng dây đã replay thành công và hình học dây rõ ràng.
- Khối băng dùng `CubeData.NumLock`, khác khóa đếm trên hộp. Giảm đếm theo `Box.OnBoxFinished`; không giả định mọi lần hấp thụ đều giảm đếm. Phải kiểm tra cả trường hợp hộp chứa băng có khóa chặn và thời điểm mở khóa.
- Khóa màu mở theo sự kiện hoàn thành hộp đúng màu; AdjacentLock phụ thuộc vị trí và hướng thực tế theo `AdjacentLockRules`, không phải chỉ đặt hai hộp gần nhau hoặc khai báo một cạnh phụ thuộc tùy ý.
- Màu thường của mỗi màn dùng Type liên tiếp 1..N, không chọn một tập ID ngẫu nhiên trong 1–14. Hộp dài dùng Type liên tiếp từ 101 trong dải được hỗ trợ 101–105; Surprise từ 201, màu thay thế vẫn thuộc 1..N. Chuẩn hóa palette trước khi tạo khóa/cửa/Surprise để đồng bộ mọi tham chiếu màu. Hộp dài chỉ dùng màu được hỗ trợ 101–105. Bố trí theo cặp là quy ước thiết kế của campaign, không phải bằng chứng đủ về bảo toàn màu hoặc có lời giải. Single không được xem là hộp Normal sức chứa một ô.
- Surprise phải có `SurpriseDatas` hợp lệ; kiểm chứng cả cấu hình thay thế, tổng lượng màu sau biến đổi và bước thắng cuối cùng. Đường mở phải có `MapData` Enter/Exit phù hợp bố cục và được kiểm tra vòng quay từ Ra về Vào trong Play Mode.
- Mỗi màn có hồ sơ kiểm chứng ngoài JSON gameplay, lưu trong `docs/superpowers/verification/alternative-campaign-70/`: số màn, phiên bản builder, seed, hash JSON, danh sách thao tác theo thứ tự, điều kiện chờ ổn định nếu cần, kết quả structural validation, replay mô phỏng và replay Unity. Không thêm trường gameplay chỉ để lưu báo cáo.
- Replay phải bắt đầu từ JSON đã xuất rồi nạp lại, kiểm tra thao tác hợp lệ, dung lượng và trạng thái kết thúc thắng không dùng booster. Lời giải của ứng viên trước khi thêm cơ chế không được dùng làm chứng nhận cho bản sau.
- Dùng `DebugLevelSolver`/mô phỏng hiện có khi cơ chế được hỗ trợ; nếu mô phỏng chưa tương đương runtime, ghi rõ giới hạn và dùng replay Unity để xác nhận trước nghiệm thu. Hết thời gian/trạng thái tìm kiếm nghĩa là **chưa xác minh**, không kết luận vô nghiệm hoặc gán `verified = true`.
- Generator hiện có nhánh hết lượt thử vẫn ghi `verified = true` với `solution_clicks` rỗng. Builder campaign phải tự xác minh và từ chối chứng nhận này; không mở rộng phạm vi sửa generator chung chỉ để xây campaign. Màn có trạng thái đầu đã thắng cũng bị loại.

### 4.5. Hợp Đồng Phiên Campaign Trong Editor
- Dùng lại `GamePrefs.EditorSandbox` và các hook playtest hiện có khi phù hợp. Sandbox hiện đọc xuyên xuống save thật khi chưa có giá trị overlay, vì vậy bật sandbox đơn thuần chưa tạo hồ sơ người chơi mới.
- Khi bắt đầu phiên, khởi tạo rõ các giá trị thử nghiệm về tiến trình, khóa legacy, tutorial và inventory/booster cần cho onboarding; không xóa hoặc ghi đè PlayerPrefs thật. Kiểm tra cả manager đã cache các giá trị này, không chỉ lớp persistence. Quy định thao tác riêng để bắt đầu lại từ màn 1; reload màn giữ tiến trình phiên.
- Nguồn màn thử nghiệm chỉ có 1–70; thiếu hoặc hỏng file phải hiện lỗi cụ thể và dừng tải. Không fallback qua `_lastJsonFolder`, thư mục mặc định, Resources hoặc AssetBundle của campaign chính. Cache/availability phải phân biệt nguồn và được làm mới khi đổi chế độ.
- Mọi nơi quyết định màn kế tiếp, availability và campaign complete trong phiên phải dùng cùng giới hạn 70. Thắng màn 70 hiển thị hoàn tất; không tải màn 71. Không sửa asset catalog chính để đạt điều này.
- Giữ mốc Undo/Shuffle/Add Space là 4/7/9. Kiểm tra trải nghiệm qua tiến trình phiên vì sandbox một màn (`IsEditorPlaytest`) có thể mở booster sớm theo code hiện có. Mọi màn vẫn có lời giải không dùng booster; dùng booster trong phiên phải giữ inventory thật nguyên vẹn.
- Thoát phiên, dừng Play Mode hoặc chuyển về campaign chính phải dọn hook, cache và overlay thuộc phiên, phục hồi nguồn đọc và trạng thái sandbox trước phiên. Xử lý domain reload và chế độ tắt domain reload; không để nguồn thử nghiệm hoặc tiến trình thử rò sang lần chạy sau.
- Hook mới trong runtime chỉ nằm dưới `UNITY_EDITOR`, dùng API nhỏ và tái sử dụng interface hiện có. Kiểm tra player compilation để xác nhận không tham chiếu UnityEditor hoặc code chỉ có trong Editor.

### 4.6. Hồ Sơ Thiết Kế & Cân Bằng
- Mỗi màn có: mục tiêu học, cơ chế mới/ôn tập, loại và tổng số hộp (bao gồm Single/Long), số hộp trống, số màu, sức chứa đường theo đơn vị runtime, lượng màu, cấu hình khóa và phụ thuộc, seed, lời giải tham chiếu và nhãn độ khó.
- Tỷ lệ Secret phải ghi rõ mẫu số là tổng số khối đủ điều kiện và số khối thực tế sau làm tròn. Không che thông tin bắt buộc khiến bước duy nhất đúng phụ thuộc vào đoán màu.
- Ghi số thao tác lời giải tham chiếu (không gọi là tối ưu nếu chưa chứng minh), mức chiếm dụng đường cao nhất, thời gian hoàn thành và số lần restart trong playtest. Đặt khoảng mục tiêu theo giai đoạn sau đợt mẫu; chốt các khoảng này trước khi sinh hàng loạt, không dùng riêng số hộp để suy ra độ khó.
- Các màn 4/7/9 minh họa lợi ích booster nhưng không bắt buộc dùng booster để thắng. Kiểm tra tutorial hiện có trên bố cục mới, đặc biệt màn 1–3 và tình huống chỉ tay.
- Có ít nhất một màn riêng giới thiệu và hai màn thực hành cho mỗi cơ chế gameplay mới trong danh mục. Hình học đường có thể thay đổi giữa các màn luyện nhưng không đồng thời tăng nhiều trục độ khó. Điều chỉnh nội dung cụ thể sau playtest phải cập nhật bảng 70 màn và hồ sơ tương ứng.

---

## 5. Ràng Buộc & Quy Tắc Cốt Lõi (Constraints & Project Rules)
- **Tuyệt đối không dùng `Find`:** Toàn bộ code C# editor/builder mới tuân thủ nghiêm ngặt `VerifyNoFindPolicy()`.
- **Độc lập dữ liệu:** Không chỉnh sửa bất kỳ file nào trong thư mục `Assets/00Game/Resources/LevelData/` cho đến khi người chơi kiểm thử xong và yêu cầu tích hợp.
- **Tuân thủ Runtime Engine:** Giữ nguyên luật và giới hạn gameplay. Chỉ cho phép hook Editor tối thiểu theo mục 4.5; không mở rộng hỗ trợ nhiều cặp dây hoặc cơ chế ngoài danh mục trong công việc này.

## 6. Trình Tự Triển Khai & Tiêu Chí Nghiệm Thu

### 6.1. Thực hiện theo các cổng kiểm chứng
1. **Chốt hợp đồng dữ liệu và công cụ:** Kiểm tra API hiện có, đơn vị sức chứa đường, vòng đời sandbox và giới hạn solver; chuẩn hóa hồ sơ thiết kế, báo cáo và trạng thái chưa xác minh/đã xác minh. Không xuất hàng loạt trước bước này.
2. **Làm bộ mẫu đại diện:** Màn 1–3, 4/7/9, 11, 15, 23, 29, 37, 41, 49, 53, 56, 59 và 70. Mẫu 70 kiểm tra quy mô/bố cục/tương tác cuối game, có thể tiếp tục cân bằng sau; mọi mẫu phải có lời giải đã replay. Kiểm tra các cơ chế riêng trước tương tác kết hợp.
3. **Hoàn thiện hai chế độ chơi thử:** Xác nhận sandbox một màn, tiến trình liên tiếp, unlock 4/7/9 và kết thúc tại 70; thử với save thật đã đi xa để phát hiện đọc nhầm tiến trình. Chốt ngưỡng cân bằng từ bộ mẫu trước bước 4.
4. **Sinh và tinh chỉnh theo giai đoạn:** Hoàn thiện lần lượt 1–10, 11–22, 23–36, 37–48, 49–60, 61–70. Validate, replay, xem bố cục trong Game View và cập nhật báo cáo cho mỗi giai đoạn; seed thất bại không được thay bằng ứng viên chưa xác minh.
5. **Playtest trọn campaign và nghiệm thu:** Chạy từ hồ sơ mới qua cả 70 màn, hiệu chỉnh nhịp độ theo số liệu rồi kiểm chứng lại mọi JSON thay đổi. Lưu bằng chứng cuối cùng khớp hash dữ liệu bàn giao.

### 6.2. Điều kiện hoàn thành
- Có đúng 70 JSON đánh số liên tục 1–70; tên file, `level_number` và `puzzle_id` khớp nhau; nạp qua decoder thực tế thành công. Có `.meta` ổn định cho asset/thư mục mới, giữ GUID khi sinh lại.
- 70/70 màn không có lỗi structural validation, không bắt đầu ở trạng thái đã thắng và có lời giải không booster replay thành công từ JSON cuối cùng trong mô phỏng tương thích và Unity Play Mode. Mọi warning phải có kết quả xử lý hoặc lý do chấp nhận trong báo cáo.
- Kiểm tra Game View cho cả 70 bố cục: hộp, cổng, đường, camera và dây không che thao tác hoặc giao cắt gây hiểu nhầm; kiểm tra trên các tỷ lệ màn hình mục tiêu của dự án. Validator không thay thế bước này.
- Với mỗi cơ chế và tương tác kết hợp, kiểm tra spawn, thao tác thường, thao tác bị chặn/đường đầy, restart/reload, disable/destruction hoặc thoát Play Mode giữa animation, cùng chuyển tiếp thắng/thua. Báo cáo trạng thái pass/fail và dữ liệu tái hiện.
- Phiên campaign chạy đúng 1→70, khóa/mở booster đúng 4/7/9, không lẫn level nguồn khác và không thay đổi tiến trình/inventory thật sau khi thoát. Kiểm tra thiếu file, JSON hỏng, chuyển chế độ và cả hai cấu hình domain reload.
- Chạy `VerifyNoFindPolicy()` qua `Rules/Verification/CoreRulesVerification.csproj` và compile/check phù hợp cho code thay đổi, bao gồm player compilation khi có hook runtime. Review diff xác nhận không đổi level gốc, scene/prefab, package, ProjectSettings, assembly definition hoặc GUID ngoài phạm vi.
- Bàn giao kèm báo cáo độ bao phủ cơ chế, số liệu cân bằng và các giới hạn kiểm chứng. Nếu chưa chạy được Unity Editor thì ghi rõ chưa nghiệm thu Play Mode; không tuyên bố bộ 70 màn hoàn chỉnh chỉ dựa trên static validation hoặc solver.
