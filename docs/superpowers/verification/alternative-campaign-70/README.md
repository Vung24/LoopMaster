# Alternative Campaign 70 — triển khai và kiểm chứng

## Dùng trong Unity

- Mở `Tools > LoopMaster > Alternative Campaign 70 > Open campaign tools`, hoặc nút **Campaign 70** trong trình sửa màn.
- Chọn số màn rồi dùng **Open in level editor** hoặc **Play this level in sandbox**.
- Dừng Play Mode rồi chọn **Start campaign from level 1** để thử tiến trình riêng. Phiên bắt đầu từ hồ sơ mới, chỉ đọc bộ 70 màn trong Resources/LevelData, mở Undo/Shuffle/Add Space ở 4/7/9 và kết thúc tại 70.
- Dừng Play Mode để trở về nguồn campaign chính. Không đổi catalog, scene hoặc prefab để bật phiên thử.
- **Audit current campaign** chạy structural validation, replay lời giải, kiểm tra hash, số cơ chế, bảo toàn màu và các điều kiện hình học được mô tả trong `campaign-audit.json`.

## Dữ liệu và bằng chứng

JSON gameplay hiện ở `Assets/00Game/Resources/LevelData/` sau khi chuyển vào campaign chính. Mỗi JSON có `.meta` riêng và giữ GUID khi sinh lại.

`Level N.json` trong thư mục báo cáo này là hồ sơ kiểm chứng, không phải file gameplay. Hồ sơ có seed, mục tiêu học, cấu hình, SHA-256 của JSON gameplay, lời giải theo chỉ số hộp bắt đầu từ 0, kết quả mô phỏng và kết quả Unity. Bằng chứng chỉ có giá trị khi hash khớp dữ liệu hiện tại. Các trạng thái `pending` không được hiểu là đã nghiệm thu.

`campaign-audit.json` tổng hợp các màn và phạm vi kiểm tra tự động. `balance-targets.json` ghi khoảng tham chiếu số lần bấm từ bộ mẫu đã replay; đây là ngưỡng tạm cho sản xuất nội dung, chưa chứng minh độ khó đối với người chơi. Thời gian replay tăng tốc không dùng làm thời gian chơi mục tiêu.

`capacity-variety-rebalance.json` so sánh capacity mặc định và mật độ cube đôi trước/sau lượt chỉnh gần nhất; `difficulty-rebalance.json` tổng hợp Secret, số họ mechanic và bằng chứng còn khớp hash. Capacity sau khi dùng Add Space được phân biệt với capacity mặc định của JSON.

## Kiểm tra có thể chạy lại

Từ thư mục gốc dự án:

```powershell
dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File ../LoopMaster-ProjectData/LevelEditor/compile-runtime.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File ../LoopMaster-ProjectData/AlternativeCampaign70/compile.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File ../LoopMaster-ProjectData/AlternativeCampaign70/run-samples.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File ../LoopMaster-ProjectData/AlternativeCampaign70/run-playmode.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File ../LoopMaster-ProjectData/AlternativeCampaign70/run-samples.ps1 -All
powershell -NoProfile -ExecutionPolicy Bypass -File ../LoopMaster-ProjectData/AlternativeCampaign70/run-playmode.ps1 -All
powershell -NoProfile -ExecutionPolicy Bypass -File ../LoopMaster-ProjectData/AlternativeCampaign70/run-playmode.ps1 -Session
powershell -NoProfile -ExecutionPolicy Bypass -File ../LoopMaster-ProjectData/AlternativeCampaign70/run-playmode.ps1 -Session -NoDomainReload
powershell -NoProfile -ExecutionPolicy Bypass -File ../LoopMaster-ProjectData/AlternativeCampaign70/run-samples.ps1 -Baselines
powershell -NoProfile -ExecutionPolicy Bypass -File ../LoopMaster-ProjectData/AlternativeCampaign70/run-samples.ps1 -Audit
```

Các runner dùng project Unity kiểm chứng riêng và product/company riêng; không chạy vào phiên Unity đang mở của người dùng. `Rules` và `.utmp` là các thư mục công cụ cục bộ vốn đang được gitignore trong dự án. Log Unity chi tiết ở `../LoopMaster-ProjectData/AlternativeCampaign70/`.

Builder không tin cờ `verified` của generator chung. Nó từ chối ứng viên không có replay thắng, không coi hết ngân sách tìm kiếm là vô nghiệm và không tự chứng nhận ứng viên thất bại. Sinh đủ campaign qua menu yêu cầu bộ mẫu có bằng chứng Unity còn khớp hash và có số liệu tham chiếu.

Ứng viên có replay Unity thất bại được lưu trong `failures/` trước khi sinh seed khác. Một chuỗi bấm thất bại không tự chứng minh puzzle vô nghiệm; ứng viên đó chỉ chưa đủ điều kiện nằm trong bộ nghiệm thu.

## Điều chỉnh nội dung revision 2

- Type màu thường dùng liên tiếp 1..N theo số màu của mỗi màn; hộp dài từ 101, Surprise từ 201. Khóa màu, chìa cửa và màu thay thế Surprise dùng cùng palette; vị trí khối vẫn được trộn theo seed để tạo puzzle đa dạng.
- Sửa giới hạn màu: có nhiều màn số màu bằng số hộp, không yêu cầu luôn có hộp đệm. Số hộp tăng lên 6–13 sau onboarding, các chương sau chủ yếu 8–12, cuối campaign 12–13.
- Capacity được ghi riêng từng profile; đã bỏ quy tắc tự nâng bằng số hộp. Normal chủ yếu 5–6, một số màn nhiều hộp/hộp dài hoặc hồi nhịp dùng 7; Hard/Expert dùng 4. Màn 8 dùng 6, màn 9 dùng 7. Không còn capacity mặc định 8–10. Trường gameplay là `layout.PathLength`; booster vẫn cộng thêm 2 theo luật runtime hiện có.
- Học bố trí từ bộ LevelData gốc, được lưu riêng trong `geometry-references.json`: tái sử dụng hình học đường và vị trí/hướng hộp, tạo riêng stock màu và cơ chế. `profile.GeometryReference` chỉ ra màn tham khảo, không phải puzzle được sao chép. Tọa độ mở rộng tối thiểu 8%, chỉ tăng thêm theo bước 4% khi footprint còn giao nhau (tối đa 24%); nguồn gốc giữ nguyên. Khi không có mẫu đúng số hộp (như 13 hộp thường), giữ lưới sinh mới, fit đường ôm cụm hộp và vẫn chạy đầy đủ kiểm tra hình học; GeometryReference bằng 0.
- Màn 1–2 dùng đường thẳng ngắn có Enter/Exit sát cụm hộp; màn 2 chỉ có 2 hộp đầy trộn 2 màu, không có hộp đệm trống. Màn 3–4 dùng vòng chữ nhật gọn theo lưới; hộp dài dùng bố trí riêng và tránh vòng tròn dư diện tích. Các màn còn lại dùng nhiều contour mở/kín thay cho hai dãy box trong một oval lặp lại.
- Tăng trộn theo chương; từ màn 5 yêu cầu ít nhất nửa số hộp thường trộn nhiều màu và lời giải tham chiếu có ít nhất bằng số hộp lượt bấm. Chỉ số này loại bài quá nông, không tự chứng minh độ khó.
- Một số màn đầy 8–12 hộp thường/màu dùng thêm các mẫu trộn 3–4 tầng theo thứ tự tiếp xúc đường, bảo toàn đúng 4 khối mỗi màu. Khóa/cửa/băng tạo ràng buộc trên chuỗi màu thay vì chỉ tăng ngẫu nhiên độ phân tán. Báo cáo ghi MixingRecipe để phân biệt các mẫu.
- `content-distribution.json` sau audit ghi histogram số hộp/capacity, số màn màu bằng hộp, số hình học đường khác nhau và độ trộn từng màn. Atlas là sơ đồ bố cục, cần kiểm tra tiếp Game View để đánh giá tỷ lệ với HUD.
- Bằng chứng chỉ áp dụng cho revision/hash hiện hành. Khi sinh lại, Unity replay, visual và lifecycle của nội dung thay đổi trở về pending.
- Khóa màu được đặt đủ số lượng trong builder mới; không dùng kết quả generator chung có thể âm thầm thiếu khóa. Loại vòng phụ thuộc và tạo chuỗi mở khóa cho màn 27.
- Nhóm cửa cuốn là cụm hộp cùng hướng, liền nhau. Không chọn chỉ số hộp liên tiếp rồi giả định chúng liền nhau về không gian.
- Khóa liền kề phải có nguồn mở khóa còn khả năng hoàn thành sau khi bắt đầu. Màn 30 dùng hai cặp độc lập; màn 33 dùng chuỗi ba hộp thật về hình học.
- Màn 55 đặt chìa dưới khối khác; màn 59 đặt hai đầu dây ở hai phía trạng thái khóa nhóm. Thành viên dây không mang chìa hoặc băng.
- Một biến cục bộ trùng tên trong phần xóa MapData của editor hiện có được đổi tên để gỡ lỗi biên dịch; không đổi hành vi công cụ.

## Phối hợp cơ chế

Các bài giới thiệu giữ trọng tâm riêng; bài luyện có thể ghép mechanic đã học với Secret. Cube đôi chỉ xuất hiện ở 13 màn: 15/16/17/20/25/33/40/45/52/59/64/68/70. Sau bài giới thiệu, các lần ôn cách nhau ít nhất một màn. Nhóm 31–35 và 43–48 (trừ màn nghỉ 46) có ít nhất 3 họ; từ 58–70 có ít nhất 4 họ, Expert 68 có 6 và màn cuối có 9. Các màn khác xoay khóa màu, khóa liền kề, băng, cửa, hộp dài/đơn và Surprise. Audit đếm theo họ, không tính hai khóa/cửa cùng loại là hai cơ chế.

## Phần nghiệm thu còn phải phân biệt

Replay Unity gọi `GameController.OnClickBox`, chạy animation và đợi thắng tự nhiên; không dùng force-win hay booster. Runner có thể đợi hai vòng băng chuyền khi trạng thái trung gian khác mô phỏng. Khi có lệch, runner mới tạm dừng animation để tìm tiếp từ bản sao trạng thái thực, rồi tiếp tục bấm hợp lệ; tối đa 12 lần lập kế hoạch và 300 lượt. Không sửa board thực trong lúc tìm lời giải. Chuỗi thắng thực được lưu trong `runtime_solution_clicks`, số lần lập kế hoạch trong `runtime_replanning_count`; thời gian wall-clock có cả khoảng dừng này, không dùng để đánh giá độ khó. Ứng viên đã thất bại được lưu trước khi thử lại. Thao tác hợp lệ mà khối quay lại trạng thái cũ được đếm riêng; chỉ chấp nhận khi game thắng tự nhiên.

Kiểm tra hình học tự động chưa thay thế việc xem cả 70 màn trong Game View với HUD, tutorial, cửa và dây ở các tỷ lệ 9:16, 1:1 và 16:9. Cần ghi nhận các tình huống restart/reload, disable/destruction giữa animation, thua và chuyển cảnh cho từng cơ chế/tương tác. Việc kiểm tra session bật/tắt domain reload không được coi là đã kiểm tra toàn bộ lifecycle gameplay.

Chưa có số liệu người chơi về thời gian hoàn thành, restart hoặc mức hiểu tutorial. Các nhãn Hard/Very Hard vẫn là ý đồ thiết kế cho tới khi đo và cân bằng lại. Chỉ đánh dấu campaign nghiệm thu hoàn chỉnh khi các mục visual, lifecycle và cân bằng trong spec đều có bằng chứng; không dùng riêng kết quả solver hoặc số lượng file để kết luận hoàn tất.

Level 10 dùng 7 hộp/7 màu Type 1–7, mỗi hộp trộn ba màu theo cụm 2+1+1, khối ẩn 10% (2 khối), giữ Hard/capacity 4. MixingMode 2 là mẫu cho thử thách đầu tiên với lời giải tham chiếu mục tiêu 14–18 lượt. Builder và audit loại lời giải tham chiếu ngoài khoảng này; đây chưa phải bằng chứng số lượt tối thiểu hay đánh giá độ khó của người chơi.

## Giảm hộp gần hoàn thành và tăng luyện mechanic

Từ màn 5, tối đa 1 hộp thường chứa đúng 3 khối cùng màu (cả dạng 3+1 và 3 khối cùng màu còn thiếu ô). Builder đổi chéo màu giữa các hộp thường trước khi đặt khóa/chìa/dây; giữ nguyên tổng stock màu, số khối từng hộp và vị trí hình học. Các bài đầu 1–4 giữ cấu hình dạy luật. Mô phỏng phải thắng lại trên JSON sau thay đổi.

Từ màn 6, mọi màn dùng ít nhất một mechanic đã học, trừ hai màn nghỉ 36/46. Cube đôi được giới thiệu ở 15 (hai đầu cùng màu), 16 (một đầu bị chôn), 17 (hai đầu khác màu + Secret), rồi ôn cách quãng. Hộp đơn bắt đầu ở 49; bài 50 thêm Secret để luyện. Bài 39 dùng băng + khóa màu + Secret; 48 dùng hộp dài + khóa màu + Secret. Các bài giới thiệu vẫn giữ trọng tâm riêng; audit kiểm tra số mechanic và thứ tự giới thiệu.

Các ứng viên không thắng khi replay được giữ trong `failures/` trước khi đổi seed. Bằng chứng của lần đổi thứ tự cube đôi chỉ áp dụng cho hash của lần đó; khi tăng độ khó, đọc trạng thái và hash trong từng báo cáo mới. Kết quả tự động chưa thay cho đánh giá tutorial và độ khó bằng người chơi.

## Tăng thử thách và giá trị booster

Secret ở bài luyện chủ yếu 25–40% khối đủ điều kiện, các đỉnh Hard/Expert 40–60%; riêng 30 dùng 30% và 10 giữ cấu hình đã cân trước. Không che cube trên cùng, chìa cửa hoặc đầu dây. Màn nghỉ 28/36/46 và các bài giới thiệu tạo khoảng hồi nhịp.

Thêm đỉnh Hard tại 25/40/55, giữ capacity 4 ở Hard/Expert; Normal dùng mức được chọn riêng trong 5–7. Hard từ 20 có lời giải tham chiếu dài ít nhất ceil(1,7 × số hộp), từ 40 là 1,9, từ 60 là 2,1; Expert là 2,3. Các ngưỡng này loại bài quá nông, không chứng minh số lượt tối thiểu hay mức khó đối với người chơi.

Add Space tăng capacity 4→6 để tăng biên độ sai; Undo giúp sửa lần phóng nhầm, Shuffle thay đổi các stack đủ điều kiện. Mọi màn vẫn cần lời giải thắng không booster. Chế độ kiểm tra capacity thử Add Space và tiêu đúng một vật phẩm trong inventory cô lập ở các màn Hard/Expert; kiểm tra đó không chứng minh người chơi bắt buộc hoặc sẽ muốn dùng booster. Cần đo tỷ lệ dùng booster, restart và thời gian thắng qua playtest.

Capacity được kiểm tra qua RoadMaxCapacity, GetPathLength và số nhóm còn trống trên GameData; HUD đọc cùng RoadMaxCapacity. Chế độ -Capacity kiểm tra cả 70 màn và xác nhận không sinh GameObject Capacity phụ trong map. Việc kiểm tra dữ liệu này không thay thế Game View của HUD.
