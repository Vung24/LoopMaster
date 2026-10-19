# Alternative Campaign 70 — triển khai và kiểm chứng

## Dùng trong Unity

- Mở `Tools > LoopMaster > Alternative Campaign 70 > Open campaign tools`, hoặc nút **Campaign 70** trong trình sửa màn.
- Chọn số màn rồi dùng **Open in level editor** hoặc **Play this level in sandbox**.
- Dừng Play Mode rồi chọn **Start campaign from level 1** để thử tiến trình riêng. Phiên bắt đầu từ hồ sơ mới, chỉ đọc thư mục AlternativeCampaign_70, mở Undo/Shuffle/Add Space ở 4/7/9 và kết thúc tại 70.
- Dừng Play Mode để trở về nguồn campaign chính. Không đổi catalog, scene hoặc prefab để bật phiên thử.
- **Audit current campaign** chạy structural validation, replay lời giải, kiểm tra hash, số cơ chế, bảo toàn màu và các điều kiện hình học được mô tả trong `campaign-audit.json`.

## Dữ liệu và bằng chứng

JSON gameplay ở `Assets/00Game/05LevelData/AlternativeCampaign_70/`; không ghi vào Resources/LevelData. Mỗi JSON có `.meta` riêng và giữ GUID khi sinh lại.

`Level N.json` trong thư mục báo cáo này là hồ sơ kiểm chứng, không phải file gameplay. Hồ sơ có seed, mục tiêu học, cấu hình, SHA-256 của JSON gameplay, lời giải theo chỉ số hộp bắt đầu từ 0, kết quả mô phỏng và kết quả Unity. Bằng chứng chỉ có giá trị khi hash khớp dữ liệu hiện tại. Các trạng thái `pending` không được hiểu là đã nghiệm thu.

`campaign-audit.json` tổng hợp các màn và phạm vi kiểm tra tự động. `balance-targets.json` ghi khoảng tham chiếu số lần bấm từ bộ mẫu đã replay; đây là ngưỡng tạm cho sản xuất nội dung, chưa chứng minh độ khó đối với người chơi. Thời gian replay tăng tốc không dùng làm thời gian chơi mục tiêu.

## Kiểm tra có thể chạy lại

Từ thư mục gốc dự án:

```powershell
dotnet run --project Rules/Verification/CoreRulesVerification.csproj --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File Rules/LevelEditor/compile-runtime.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Rules/AlternativeCampaign70/compile.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Rules/AlternativeCampaign70/run-samples.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Rules/AlternativeCampaign70/run-playmode.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Rules/AlternativeCampaign70/run-samples.ps1 -All
powershell -NoProfile -ExecutionPolicy Bypass -File Rules/AlternativeCampaign70/run-playmode.ps1 -All
powershell -NoProfile -ExecutionPolicy Bypass -File Rules/AlternativeCampaign70/run-playmode.ps1 -Session
powershell -NoProfile -ExecutionPolicy Bypass -File Rules/AlternativeCampaign70/run-playmode.ps1 -Session -NoDomainReload
powershell -NoProfile -ExecutionPolicy Bypass -File Rules/AlternativeCampaign70/run-samples.ps1 -Baselines
powershell -NoProfile -ExecutionPolicy Bypass -File Rules/AlternativeCampaign70/run-samples.ps1 -Audit
```

Các runner dùng project Unity kiểm chứng riêng và product/company riêng; không chạy vào phiên Unity đang mở của người dùng. `Rules` và `.utmp` là các thư mục công cụ cục bộ vốn đang được gitignore trong dự án. Log Unity chi tiết ở `Rules/AlternativeCampaign70/`.

Builder không tin cờ `verified` của generator chung. Nó từ chối ứng viên không có replay thắng, không coi hết ngân sách tìm kiếm là vô nghiệm và không tự chứng nhận ứng viên thất bại. Sinh đủ campaign qua menu yêu cầu bộ mẫu có bằng chứng Unity còn khớp hash và có số liệu tham chiếu.

Ứng viên có replay Unity thất bại được lưu trong `failures/` trước khi sinh seed khác. Một chuỗi bấm thất bại không tự chứng minh puzzle vô nghiệm; ứng viên đó chỉ chưa đủ điều kiện nằm trong bộ nghiệm thu.

## Điều chỉnh nội dung revision 2

- Type màu thường dùng liên tiếp 1..N theo số màu của mỗi màn; hộp dài từ 101, Surprise từ 201. Khóa màu, chìa cửa và màu thay thế Surprise dùng cùng palette; vị trí khối vẫn được trộn theo seed để tạo puzzle đa dạng.
- Sửa giới hạn màu: có nhiều màn số màu bằng số hộp, không yêu cầu luôn có hộp đệm. Số hộp tăng lên 6–13 sau onboarding, các chương sau chủ yếu 8–12, cuối campaign 12–13.
- Capacity nằm trong từng profile: Hard/Expert dùng 4; Normal thay đổi 5–8, riêng bài Add Space dùng 4. Trường gameplay là `layout.PathLength`; không đổi luật sức chứa runtime.
- Học bố trí từ `Resources/LevelData`: tái sử dụng hình học đường và vị trí/hướng hộp, tạo riêng stock màu và cơ chế. `profile.GeometryReference` chỉ ra màn tham khảo, không phải puzzle được sao chép. Tọa độ mở rộng tối thiểu 8%, chỉ tăng thêm theo bước 4% khi footprint còn giao nhau (tối đa 24%); nguồn gốc giữ nguyên.
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

Các bài giới thiệu vẫn có hai màn làm quen riêng. Phần luyện tập bổ sung 51 (dây + khối ẩn), 52 (dây + khóa màu) và 62 (cửa cuốn kép + khối ẩn), cùng các cặp đã có như băng + khóa liền kề, hộp dài + băng, Surprise + hộp đơn và cửa cuốn + dây. Từ 58–70, mỗi màn phải có ít nhất hai họ cơ chế; audit đếm theo họ, không tính hai khóa/cửa cùng loại là hai cơ chế. Xem trường `mechanics` trong báo cáo phân bố nội dung.

## Phần nghiệm thu còn phải phân biệt

Replay Unity gọi `GameController.OnClickBox`, chạy animation và đợi thắng tự nhiên; không dùng force-win hay booster. Runner có thể đợi hai vòng băng chuyền khi trạng thái trung gian khác mô phỏng. Chuỗi bấm phải hợp lệ và dẫn tới thắng thực tế; thao tác hợp lệ mà khối quay lại trạng thái cũ được đếm riêng, không tự coi là puzzle lỗi; xem chính sách chờ trong từng báo cáo.

Kiểm tra hình học tự động chưa thay thế việc xem cả 70 màn trong Game View với HUD, tutorial, cửa và dây ở các tỷ lệ 9:16, 1:1 và 16:9. Cần ghi nhận các tình huống restart/reload, disable/destruction giữa animation, thua và chuyển cảnh cho từng cơ chế/tương tác. Việc kiểm tra session bật/tắt domain reload không được coi là đã kiểm tra toàn bộ lifecycle gameplay.

Chưa có số liệu người chơi về thời gian hoàn thành, restart hoặc mức hiểu tutorial. Các nhãn Hard/Very Hard vẫn là ý đồ thiết kế cho tới khi đo và cân bằng lại. Chỉ đánh dấu campaign nghiệm thu hoàn chỉnh khi các mục visual, lifecycle và cân bằng trong spec đều có bằng chứng; không dùng riêng kết quả solver hoặc số lượng file để kết luận hoàn tất.

Level 10 được giảm áp lực: 7 hộp/7 màu Type 1–7, mỗi hộp trộn hai cụm 2+2, khối ẩn 10% (2 khối), giữ Hard/capacity 4. MixingMode 2 là mẫu hai tầng dành cho thử thách đầu tiên; các profile khác giữ cách trộn hiện có.
