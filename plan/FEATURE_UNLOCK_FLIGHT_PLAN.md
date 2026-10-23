# Kế hoạch: Icon unlock feature bay từ UI đến item gameplay

Ngày: 23/10/2026. Trạng thái: đã triển khai code và compile kiểm tra; chưa nghiệm thu hình ảnh/PlayMode. Bằng chứng và giới hạn ở mục 14.

## 1. Mục tiêu và phạm vi

Sau khi người chơi nhấn Continue trên popup giới thiệu feature, icon rời popup, bay đến đúng item đại diện trên board và hòa vào visual gameplay. Không nhảy vị trí, đổi kích thước đột ngột hoặc chớp hai hình ở thời điểm bàn giao.

“Unlock” ở đây là giới thiệu mechanic cho người chơi. Hiệu ứng không mở khóa ColorLock, phá băng, thay đổi màu, lộ cube bí mật hoặc thay đổi bất kỳ luật chơi nào.

Phạm vi gồm các FeatureType hiện có, chọn đích runtime, quy đổi UI/gameplay, animation và cleanup. Giữ nguyên thứ tự giới thiệu feature, chính sách lưu đã xem và luồng booster. Không thêm package, sửa ProjectSettings, assembly definition hoặc refactor hệ thống gameplay ngoài nhu cầu này.

## 2. Hiện trạng đã đối chiếu source

| File trong Assets/00Game/01Scripts | Vai trò và giới hạn hiện tại |
| --- | --- |
| 08UI/Dialogs/FeatureUnlockDialog.cs | Chọn nội dung và sprite theo FeatureType; Continue gọi TryClose; chỉ công khai IconPosition, chưa có kích thước/hình học icon |
| 02Gameplay/Flow/GameLevelPresentationFlow.cs | ShowFeatureTutorial mở popup, ghi cờ đã xem trước khi chờ đóng; chưa chọn item hoặc chạy flight cho feature |
| 02Gameplay/Boosters/BoosterFlyPresenter.cs | Có flight UI đến HUD; không áp dụng trực tiếp phép căn kích thước RectTransform cho vật thể world |
| 02Gameplay/Flow/GameplayCameraFlow.cs | Fit camera orthographic theo board và viewport; cập nhật khi kích thước màn hình/viewport đổi |
| 02Gameplay/Elements/Box.cs | Có Info, Cubes và visual khóa; khởi tạo animation entrance riêng |
| 02Gameplay/Elements/Cube.cs | Có cả SkinnedMeshRenderer và SpriteRenderer; không thể coi mọi cube là một sprite phẳng |
| 02Gameplay/Elements/GroupDoorVisualReferences.cs | Có reference khóa, keyhole và renderer của group door |
| 08UI/Framework/BasePopup.cs | Quản lý đóng, disable, destroy và cancellation của popup; flight không được phụ thuộc token đã bị hủy khi popup đóng |

Các file trên có thể đang có thay đổi trong workspace. Khi triển khai phải đọc lại diff hiện tại, giữ nguyên công việc đang có và không dùng bản source cũ để ghi đè.

### 2.1. Đã có → tái sử dụng → còn thiếu → thay đổi tối thiểu

| Thành phần đã có | Tái sử dụng | Còn thiếu / giới hạn | Thay đổi tối thiểu dự kiến |
| --- | --- | --- | --- |
| BoosterPresentationUI và Resources/BoosterPresentation.prefab | Overlay Canvas, flight Image, input blocker, Dismiss | Create hiện bắt buộc có guide và khởi tạo font; lớp UI còn gắn với booster | Prototype đánh giá dùng nguyên view hiện có trước. Chỉ tách phần flight view khi dependency guide hoặc sorting thật sự cản trở; không tạo ngay Canvas/prefab song song |
| BoosterFlyPresenter | Tham khảo ownership tween, finally và phản hồi arrival | Đích là RectTransform; nguồn chỉ có position, chưa đủ cho world target | Giữ hành vi booster; presenter feature dùng phép chiếu riêng. Không tổng quát hóa toàn bộ presenter bằng delegate |
| GameLevelPresentationFlow | Token theo lượt presentation, MonitorInterruption, Cancel và IsPlaying | Chưa phân biệt Continue với close ngoài ý muốn; chưa có feature flight | Thêm kết quả Continue và await flight vào flow này, không thêm coordinator mới |
| GameController.IsLevelIntroPlaying / IsGameplayActive | Gate gameplay và conveyor theo intro hiện có | Raycast UI và gameplay gate có vai trò khác nhau | Giữ IsPlaying xuyên suốt; dùng blocker UI hiện có nếu cần, không thêm boolean khóa gameplay thứ hai |
| GameplayCameraFlow và GameController.LateUpdate | Fit camera và theo dõi viewport thay đổi | Presenter phải lấy pose sau camera fit của frame | Chốt thứ tự cập nhật projection sau LateUpdate, xác minh trong prototype |
| BoxInfo.PeekTopCube(), CubeInfo.CubeSc, Box.Cubes, Cube.VisibleSize | Xác định cube theo data và liên kết runtime | Top cube/VisibleSize không chứng minh không bị cửa hoặc hình học khác che | Selector dùng reference hiện có và điều kiện visual cụ thể; không thêm mô hình Board/Slot mới |
| GameplaySceneBuilder.GetGroupRollDoor(groupId, out door) | Tra đúng group door trong dictionary hiện có | Presentation chưa nhận reference truy cập phù hợp | Truyền dependency hiện có với phạm vi hẹp; không thêm registry, singleton hoặc lookup theo tên |
| GroupDoorVisualReferences | Reference khóa, keyhole, renderer | Cần xác nhận instance runtime giữ đúng binding sau build | Tái sử dụng binding, chỉ bổ sung accessor nếu thực sự thiếu |
| GameController quản lý linked cable và pair runtime | Xác định đúng cặp/visual liên kết | Chưa có hợp đồng target presentation | Cung cấp reference cặp cụ thể; không dựng danh sách pair authoritative thứ hai |
| Box.PlayEntranceAnimation và routine version hiện có | Owner của entrance và cơ chế nhận diện reset | Chưa có readiness công khai chứng minh visual đích đã ổn định | Chỉ bổ sung completion cho entrance ở owner hiện có nếu feature đích cần chờ; không tạo readiness manager |

Tối ưu ưu tiên là giảm code và dependency trùng lặp. Không thêm pooling cho flight chỉ xuất hiện khi giới thiệu lần đầu nếu chưa có bằng chứng chi phí đáng kể; không ép tái sử dụng prefab khi kéo theo nhiều dependency không liên quan hơn một view nhỏ chuyên dụng. Quyết định cuối về view phải được ghi lại sau prototype.

## 3. Quyết định thiết kế

1. Dùng một bản sao icon trên lớp UI hiệu ứng trong suốt chuyến bay. Item gameplay thật đã tồn tại; không biến UI GameObject thành object gameplay hay tạo thêm state gameplay.
2. Dùng screen space làm cầu nối. Tâm, kích thước và hướng đích được suy ra từ visual thật qua camera gameplay.
3. Bản đầu giữ nguyên visual đích trong toàn bộ flight, fade icon ở đoạn cuối rồi highlight đích. Không disable GameObject gameplay, collider hoặc renderer. Chỉ bổ sung ẩn/crossfade renderer nếu video prototype chứng minh phương án đơn giản không đạt.
4. Một chuyến bay đến một item đại diện. Linked feature có đích là một cặp cụ thể và vùng liên kết; không tự chia icon thành hai chuyến bay.
5. Nếu art popup khác visual thật, dùng chuyển mờ ở đoạn cuối và phản hồi sáng trên đích. Không hứa khớp pixel khi hai asset có hình dạng hoặc material khác nhau.
6. Giữ nguyên chính sách hiện tại: ghi đã xem khi mở popup. Không dời thao tác lưu sang cuối animation trong phạm vi này.

## 4. Chọn đúng item đích

Chọn trên danh sách runtime hiện có và dữ liệu authoritative. Không suy ra cube đích chỉ từ box index hoặc tên GameObject. Khi cần cube, lấy đúng Cube có Info thỏa điều kiện và xác thực thuộc level hiện tại.

| FeatureType | Điều kiện dữ liệu cần đối chiếu | Visual đích dự kiến |
| --- | --- | --- |
| SecretCube | CubeInfo.IsSecret | Marker/visual hidden đang thấy được, không lộ màu bí mật |
| ColorLock | BoxInfo.LockColor > 0 | Khóa màu trên box tương ứng |
| EnterOnly | BoxInfo.IsEnterOnly | Dấu nhận diện receive-only |
| NumLock | BoxInfo.NumLock > 0 | Visual băng và counter trên slot |
| GroupColorLock | GroupInfo.IsLocked | Khóa cửa của đúng group runtime |
| LimitedColor | BoxInfo.LimitedColor > 0 | Dấu màu giới hạn trên slot |
| AdjacentLock | BoxInfo.IsAdjacentLocked | Phần khóa gỗ trên slot |
| SingleBox | BoxInfo.BoxType == Single | Thân slot đơn |
| LongBox | BoxInfo.BoxType == Long | Thân slot dài, giữ đúng tỉ lệ |
| CubeNumLock | CubeInfo.NumLock > 0 | Visual băng của đúng cube |
| SurpriseCube | CubeData.IsSurpriseColor(CubeInfo.Type) | Visual surprise hiện tại |
| LinkedCubes | Đúng quan hệ LinkedPair đang có | Hai member và phần dây đang hiển thị |
| LinkedHiddenCubes | LinkedPairId > 0 và IsSecret | Visual liên kết/marker đang được phép thấy; không lộ partner hoặc màu đang bị che |

Quy tắc chọn:

- Lọc object còn sống, đang active, thuộc level hiện tại và có visual hợp lệ trước camera.
- Ưu tiên feature có dấu hiệu nhận diện đang thấy rõ, không bị cửa/khay/cube phía trên che. Active hoặc nằm trong viewport chưa đủ chứng minh không bị che.
- Xử lý che khuất dựa trước hết vào API thật của project: BoxInfo.PeekTopCube() xác định cube trên cùng còn Size > 0, CubeInfo.CubeSc/Box.Cubes liên kết runtime và Cube.VisibleSize phản ánh kích thước hiển thị. Không coi chúng là bằng chứng đầy đủ về occlusion qua camera. Không giả định có IsExposed hoặc GetVisibleCube.
- Với cửa group/khay/marker, kiểm tra điều kiện visual từ owner tương ứng và fixture thực tế; không dùng API kiểm tra khả năng thao tác để thay cho khả năng nhìn thấy. Bản đầu không thêm raycast hoặc hệ thống occlusion hình học chung; nếu chưa xác định được đích phù hợp thì bỏ flight.
- Trong các đích tương đương, chọn theo thứ tự runtime ổn định để tái hiện kiểm thử.
- Nếu không có đích nhìn thấy phù hợp: vẫn cho đóng popup bình thường, bỏ flight và ghi diagnostic một lần có feature/level. Không bay đến tâm board hoặc item khác loại.
- Kiểm tra lại đích trước khi bay và trước khi bàn giao; hủy nếu đích đã mất hoặc chuyển level.

## 5. Hợp đồng hình học và độ khớp

### 5.1. Hình học nguồn UI

Đọc nguồn sau khi layout popup đã hoàn tất và ngay trước khi close bắt đầu:

- Sprite thực tế, tint/alpha, preserveAspect, hướng và vùng hình đang được vẽ.
- Tọa độ các góc hiển thị, không chỉ transform.position hoặc toàn bộ RectTransform nếu Image đang có khoảng trống do preserveAspect.
- Quy đổi các góc qua camera của Canvas nguồn sang lớp UI hiệu ứng; Canvas Overlay dùng camera null.
- Tạo icon bay trước khi popup bị ẩn/destroy và đặt ở overlay nằm ngoài hierarchy popup. Ưu tiên đánh giá overlay sẵn có của BoosterPresentationUI theo mục 2.1; không mặc định thêm Root Canvas mới. Kiểm tra CanvasGroup cha, mask và sorting thực tế để icon không bị fade/clip cùng popup.
- Icon sao chép không nhận raycast; lớp chặn input thuộc flow giới thiệu.

### 5.2. Hình học đích gameplay

Luồng quy đổi: local visual → world → screen qua MainCamera → local của RectTransform lớp hiệu ứng qua camera Canvas đích.

- Với sprite phẳng: lấy các góc visual theo local bounds, pivot và transform; xử lý flip, scale cha và rotation.
- Với sprite sliced/tiled: căn theo vùng render thực tế, không giả định kích thước bằng sprite bounds gốc.
- Với mesh hoặc nhiều renderer: tập hợp vùng chiếu của các renderer được chỉ định cho feature. Chiếu tám góc world bounds chỉ là phép xấp xỉ bao ngoài, không phải silhouette chính xác.
- Với quad nghiêng tạo phối cảnh/shear: Image UI thông thường không biểu diễn chính xác mọi hình chiếu. Dùng chuyển mờ ở cuối; chỉ cân nhắc mesh UI riêng nếu prototype chứng minh thật sự cần.
- Không căn theo bounds của toàn bộ Box nếu đích thực tế chỉ là icon khóa.
- Phân biệt tâm visual và pivot. Tâm đích dùng tâm vùng hiển thị, không dùng mặc định transform.position.
- Kiểm tra kết quả phép chiếu, độ sâu trước camera, pixelRect và vùng viewport trước khi dùng.
- Không dùng kích thước pixel asset hoặc pixels-per-unit như phép đổi trực tiếp sang canvas units.

### 5.3. Camera và layout thay đổi

- Board build phải hoàn tất trước khi lấy target. Layout UI và camera fit dùng flow hiện có; khi Continue được chấp nhận phải xác nhận lại readiness và target, không chỉ dùng trạng thái lúc popup mở.
- Chưa có readiness công khai cho entrance trong các entry point đã đọc. Nếu visual đích chịu entrance, bổ sung completion ở Box/Cube đang sở hữu animation đó, gắn với routine/version hiện có. Completion chỉ báo sẵn sàng khi animation entrance thực sự kết thúc; việc PlayEntranceAnimation đã phát hết lệnh PlayAnim chưa đủ chứng minh hoàn tất.
- Cho popup mở trong lúc chuẩn bị target; không giữ popup chờ readiness trước khi hiển thị. Chuẩn bị sẵn khi có thể, không block thao tác Continue. Nếu lúc Continue target chưa sẵn sàng hoặc setup không hợp lệ thì đóng popup và bỏ flight có diagnostic một lần; không chờ vô hạn hoặc dùng delay đoán readiness.
- Không đợi toàn bộ Animation.IsPlaying vì có thể gồm loop idle/breathing. Xác định đúng completion của entrance cần chờ.
- GameController fit camera trong LateUpdate. Presenter phải tính projection sau lượt fit camera và animation cập nhật của frame, trước render UI. Trong prototype chọn điểm cập nhật phù hợp của PlayerLoop hiện có và xác minh thứ tự; không dựa vào thứ tự LateUpdate ngẫu nhiên giữa component, không đổi ProjectSettings Script Execution Order.
- Trong flight, tính lại pose đích sau LateUpdate bằng UniTask LastPreLateUpdate, trước lượt rebuild/render Canvas ở PostLateUpdate; không tạo lại tween hoặc cấp phát collection mỗi frame.
- Bản triển khai đầu hủy flight có cleanup khi pixelRect hoặc kích thước màn hình đổi, thay vì thêm ngưỡng phân biệt thay đổi lớn/nhỏ chưa được tune. Nếu camera fit đổi trong cùng viewport, pose đích vẫn được tính lại. Cần xác minh continuity trong PlayMode.

## 6. Timeline dự kiến

Các thời lượng sau là đề xuất cho prototype, chưa được xác nhận bằng playtest. Ba giai đoạn rời popup → bay → landing chạy tuần tự, tổng 0,55–0,75 giây sau Continue; hòa hình nằm trong thời gian bay, không cộng thêm. Giữ gate đến hết landing và cleanup. Nếu không chạy flight thì không thêm khoảng chờ này. Thời gian người chơi đọc popup và các intro kế tiếp không nằm trong con số trên. Chỉ đặt timing ở một owner serialized phù hợp, không thêm config asset riêng chỉ cho vài giá trị.

| Giai đoạn | Thời lượng khởi điểm | Hành vi |
| --- | --- | --- |
| Popup mở | Theo luồng hiện có | Giới thiệu feature; chuẩn bị đích mà không thay đổi visual/state gameplay |
| Continue được nhận | Trong cùng lượt xử lý | Chặn double click, snapshot nguồn và tạo icon bay trước khi popup đóng |
| Rời popup | Khoảng 0,08–0,12 giây | Icon giữ continuity rồi nhấn nhẹ (scale punch ~1.05x); phần text/nền popup đóng theo flow |
| Bay | Khoảng 0,35–0,45 giây | Đường cong nhẹ (EaseOutQuad / EaseInOutCubic), nội suy vị trí, kích thước và hướng |
| Hòa vào đích | 20–30% cuối flight | Fade icon trên visual thật được giữ nguyên; không sửa alpha/material của renderer đích trong bản đầu |
| Bàn giao | Cùng frame, trước render | Căn pose cuối, tắt icon tạm; visual đích luôn còn nguyên |
| Phản hồi (Landing) | Khoảng 0,12–0,18 giây | Highlight bằng khả năng effect hiện có. Punch khoảng 1.08x chỉ dùng nếu có visual child độc lập và không tranh chấp animation; không scale transform authoritative hoặc chạy unlock gameplay |

Mặc định visual đích được giữ nguyên cả khi popup mở lẫn trong flight; không cần snapshot/restore renderer ở bản đầu. Chỉ mở rộng sang ẩn/crossfade visual đích sau khi xem video prototype và ghi rõ lý do. Khi đó phải thêm snapshot/restore và kiểm thử interruption tương ứng. Không thay shader/material để ép crossfade.

## 7. Thành phần và ownership dự kiến

Tên dưới đây là đề xuất, cần kiểm tra quy ước và khả năng tái sử dụng trước khi thêm file.

| Thành phần | Trách nhiệm |
| --- | --- |
| FeatureUnlockDialog | Cung cấp dữ liệu icon và một điểm bàn giao khi Continue được chấp nhận, trước close; không điều khiển board |
| FeatureFlightTargetSelector | Chọn feature target từ data và runtime references được truyền trực tiếp; có thể là plain C# |
| FeatureFlightTarget | Dữ liệu ngắn hạn chứa owner, anchor/visual references và nhận diện level; không copy gameplay state |
| FeatureUnlockFlightPresenter | Sở hữu icon tạm, tiến độ flight, phép chiếu, tween và cleanup effect; chỉ sở hữu snapshot visual nếu mở rộng sau prototype |
| GameLevelPresentationFlow | Điều phối popup → flight → phần giới thiệu tiếp theo; sở hữu lifetime theo level và khóa input |

- Tách type ra file riêng theo quy tắc repository; chỉ thêm khi trách nhiệm thực sự cần.
- Không mở rộng BoosterFlyPresenter thành hệ thống nhận hàng loạt Func/Action. Chỉ trích phần UI dùng chung nếu có duplication cụ thể và không làm thay đổi booster.
- Reference visual ổn định dùng serialized field; component dynamic lookup một lần lúc khởi tạo. Không thêm Find hoặc tìm hierarchy mỗi frame.
- Group target tra qua GameplaySceneBuilder.GetGroupRollDoor(groupId, out door) và reference visual đang có. Giữ dependency truy cập hẹp, không công khai toàn bộ scene builder hoặc tạo interface context rộng chỉ để phục vụ selector.
- Nếu cần prefab binding mới, lập danh sách field/prefab chính xác; giữ GUID và dữ liệu serialized, không sửa scene hàng loạt.
- Lớp effect phải nằm ngoài mask popup, trên board và đúng thứ tự so với popup khác; không tự đưa lên trên loading/navigation toàn ứng dụng.

## 8. Flow và vòng đời

1. Chọn feature theo logic hiện tại; chuẩn bị target/readiness theo lifetime level mà không trì hoãn việc mở popup.
2. Mở và init popup; giữ nguyên thời điểm ghi FeatureTutorial như hiện tại.
3. Chỉ Continue chủ động khởi động flight. Escape, ForceClose, disable do navigation hoặc teardown phải đi nhánh bỏ/hủy flight; không suy luận Continue từ việc GameObject inactive.
4. Khi Continue được chấp nhận: kiểm tra lại target/readiness. Nếu hợp lệ thì snapshot icon, vô hiệu thao tác lặp và tạo view flight trước khi popup đóng; nếu không thì đóng bình thường và bỏ flight. Không đọc icon từ popup đã bị destroy.
5. Presenter dùng lifetime của level, không dùng OpenCancellationToken của popup đã đóng.
6. Flow tiếp tục giữ IsPlaying cho tới khi flight hoàn tất hoặc đã cleanup. Không có frame hở input giữa đóng popup và bắt đầu flight.
7. Khi thành công, tiếp tục difficulty panel/booster/new-player tutorial theo thứ tự đang có.
8. Khi restart, level reload, controller disable/destroy, canvas mất, scene unload, target mất hoặc flow bị thay thế: hủy tween, xóa effect và giải phóng reference.
9. Giữ chính sách interruption khi Time.timeScale <= 0 hiện tại; không âm thầm chuyển flight sang unscaled time để tiếp tục lúc pause.

Cleanup đặt trong finally và chạy an toàn nhiều lần: kill tween, xóa view và khôi phục visual child/effect nếu landing đã thay đổi nó. Bản đầu không đổi renderer.enabled/alpha. Nếu bổ sung cơ chế này sau prototype, phải khôi phục đúng trạng thái ban đầu khi owner/version còn hợp lệ; không bật visual vốn đã tắt hoặc sửa object thuộc lượt build mới/tái sử dụng. Không sửa sharedMaterial. Exception ngoài cancellation cần được ghi nhận theo convention dự án và không để IsPlaying bị treo.

## 9. Các bước triển khai

### Bước 1 — Khảo sát và pin hành vi

- Đọc lại caller của presentation, input gate, popup close và initialization camera/board.
- Xác nhận bảng tái sử dụng mục 2.1 trên workspace mới nhất; ghi quyết định dùng view cũ hay view chuyên dụng, cùng lý do. Không bắt đầu bằng việc tạo bộ Canvas, input gate, registry hoặc pool mới.
- Kiểm tra prefab/art cho đủ 13 feature: sprite/mesh, pivot, shader, dấu hiệu nhận diện và đường lấy reference.
- Tìm fixture level hiện có từ DATA_INDEX; nếu cần dữ liệu kiểm tra mới, ghi rõ nơi tạo, không sửa level campaign tùy tiện.
- Ghi video/checklist baseline: popup mở/đóng, thứ tự giới thiệu, booster và restart.
- Chốt mapping visual cho từng feature; ghi rõ trường hợp art không thể khớp hoàn toàn.

### Bước 2 — Prototype ColorLock xuyên suốt

- Bổ sung điểm bàn giao nguồn trước close, selector ColorLock và presenter tối thiểu.
- Tích hợp vào flow với cancellation/cleanup và input gate đầy đủ.
- Giữ renderer đích nguyên trạng; dùng fade icon + highlight. Chốt completion entrance nếu cần và điểm cập nhật projection sau camera LateUpdate. Nếu readiness chưa đạt lúc Continue thì bỏ flight.
- Căn center, projected size, rotation; thử nhiều lock, box xoay và viewport khác nhau.
- Chỉ mở rộng sau khi prototype không nhảy hình ở đầu/cuối và không kích hoạt unlock thật.

### Bước 3 — Mở rộng nhóm slot

- Thêm EnterOnly, NumLock, LimitedColor, AdjacentLock, SingleBox và LongBox.
- Xác định vùng visual đúng cho mỗi loại; kiểm tra trường hợp một slot có nhiều dấu hiệu.
- Không dùng scale cuối hard-code theo tên feature để che sai số hình học.

### Bước 4 — Mở rộng nhóm cube

- Thêm SecretCube, CubeNumLock và SurpriseCube dựa trên đúng CubeInfo/runtime Cube.
- Chỉ hướng dẫn visual đang được phép thấy; không reveal dữ liệu hidden.
- Kiểm tra mesh/sprite composite, cube bị che và entrance chậm.

### Bước 5 — Group và linked pair

- Truyền đúng group door reference, không chọn khóa gần nhất bằng khoảng cách.
- Tạo đích cặp linked từ pair identity; bao gồm visual dây nếu đang hiển thị.
- Hidden linked chỉ dùng marker hợp lệ; nếu cả cặp chưa thể thấy thì dùng nhánh bỏ flight, không reveal partner.
- Đánh giá fade/highlight cho composite; chỉ thêm kỹ thuật render phức tạp nếu cách đơn giản không đạt tiêu chí.

### Bước 6 — Hoàn thiện và review

- Tune timing/sound dựa trên prototype; ưu tiên clip hiện có, tránh phát trùng âm thanh mở popup.
- Review các scope code, architecture, safety theo guideline-reviewer khi có refactor nhiều file; mỗi bước refactor phải kiểm tra riêng.
- Kiểm tra hồi quy booster, difficulty panel, SkipTutorial và cờ đã xem.
- Ghi kết quả thực tế, screenshot/video và rủi ro chưa xử lý trước khi đánh dấu hoàn tất.

## 10. Ma trận kiểm thử

| Nhóm | Trường hợp | Kết quả mong đợi |
| --- | --- | --- |
| Mapping | Từng feature trong 13 loại | Đúng loại và đúng instance; highlight đúng vùng visual |
| Nhiều đích | Hai hoặc nhiều item cùng feature | Chọn ổn định một đích nhìn thấy |
| Composite | Group, linked pair, mesh cube | Không dùng sai bounds toàn board; không lộ hidden data |
| Nguồn | Popup animation/layout, preserveAspect, pivot lệch | Frame rời popup liên tục, không đổi aspect ratio |
| Đích | Box xoay, scale cha, sprite flip, slot dài | Không lệch tâm hoặc bóp hình ở đoạn cuối |
| Màn hình | Phone dài, phone phổ thông, tablet, safe area | Đích chiếu đúng theo viewport, không dùng offset cố định |
| Layout động | Đổi aspect/viewport trong flight | Cập nhật đúng hoặc hủy sạch theo chính sách |
| Initialization | Loading lâu, camera fit muộn, entrance chưa xong | Không bay đến pose tạm thời, không đợi vô hạn |
| Thứ tự frame | Camera fit ở LateUpdate trong frame có thay đổi viewport | Projection dùng pose camera cuối của frame, không trễ một frame |
| Readiness khi Continue | Người chơi bấm ngay khi target chưa ổn định | Popup đóng, flight được bỏ có diagnostic, không thêm delay hoặc treo input |
| Input | Double click Continue, chạm board lúc popup vừa đóng | Một flight; gameplay không nhận thao tác xuyên qua |
| Đóng ngoài Continue | Escape, ForceClose, navigation | Không tự bắt đầu flight |
| Interruption | Restart, reload, pause, disable, destroy, target mất | Không icon rác, renderer ẩn vĩnh viễn hoặc tween còn chạy |
| Thiếu setup | Sprite/reference thiếu, đích ngoài camera/bị che | Bỏ flight có diagnostic hữu ích, không khóa flow |
| Hồi quy | Feature đã xem, SkipTutorial, booster intro, hard level | Giữ thứ tự và chính sách hiện tại |

Kiểm thử tự động tập trung vào hành vi: selector chọn đúng feature/instance, kết quả ổn định khi nhiều ứng viên, loại đích không hợp lệ, bỏ flight khi thiếu đích. Chỉ tách hàm thuần cho hình học nếu giúp kiểm tra sai số thực tế; không tạo abstraction chỉ để mock chuỗi lời gọi Unity.

Lifecycle, thứ tự render và độ khớp hình ảnh cần PlayMode/in-Editor. Không dùng backend test để tuyên bố đã kiểm tra chất lượng animation.

## 11. Tiêu chí nghiệm thu

- Tất cả 13 feature có mapping đích và chính sách fallback rõ ràng; kiểm tra bằng fixture đại diện.
- Đảm bảo tính liền mạch thị giác (perceptual continuity): Không đổi vị trí/kích thước đột ngột khi icon tách khỏi popup (no popping) hoặc biến mất ở đích (no double imaging).
- Nghiệm thu độ liền mạch trên các resolution đại diện bằng video tốc độ thường và chậm, không dùng ngưỡng pixel cứng làm điều kiện duy nhất. Đồng thời kiểm tra pose tâm/kích thước cuối để phát hiện lỗi phép chiếu; highlight/punch không được dùng để che sai mapping hoặc sai scale.
- Bản đầu đạt bằng fade icon + highlight với visual thật giữ nguyên. Punch/crossfade renderer là tùy chọn cần chứng minh qua prototype, không phải điều kiện bắt buộc.
- Với art khác nhau/mesh/composite: không áp tiêu chí pixel identity; nghiệm thu video chậm đoạn hòa hình, không có bật/tắt gây chớp hoặc hình kép dễ thấy.
- Không có gameplay mutation từ animation; input gate và cleanup hoạt động trong toàn bộ ma trận interruption.
- Không thêm Find, package/settings/asmdef ngoài phạm vi hoặc làm mất .meta/GUID.
- Nếu tuyên bố cải thiện allocation/performance, phải có Unity Profiler và GC.Alloc của thao tác; không tuyên bố zero allocation từ việc dùng method group hoặc pooling đơn thuần.

## 12. Verification khi triển khai

Đọc `../LoopMaster-ProjectData/DATA_INDEX.md` để chọn compile/test project hiện hành phù hợp với các file thực sự thay đổi.

Chạy từ repository root:

```powershell
dotnet run --project ../LoopMaster-ProjectData/Verification/CoreRulesVerification.csproj
```

Lệnh trên bao gồm kiểm tra No-Find theo contract dự án. Chạy thêm compile verification phù hợp, Unity compile và PlayMode/manual checks trong ma trận khi có Editor. Trích output thực tế vào báo cáo; không đánh dấu pass cho lệnh chưa chạy.

Review diff cuối để tách thay đổi mới khỏi các thay đổi có sẵn, kiểm tra prefab/scene/.meta, dependency null và thay đổi serialized. Nếu cần sửa asset thì liệt kê chính xác lý do và asset chịu tác động.

## 13. Trạng thái checklist

- [x] Đối chiếu source chính và tạo kế hoạch.
- [x] Bổ sung đánh giá tái sử dụng từ project và giới hạn của API hiện có.
- [ ] Chốt view tái sử dụng, completion entrance và thứ tự projection qua prototype.
- [ ] Kiểm kê visual/prefab/fixture của 13 feature.
- [ ] Prototype ColorLock và đo continuity nguồn/đích.
- [x] Triển khai mapping nhóm slot; chờ nghiệm thu visual từng prefab.
- [x] Triển khai mapping nhóm cube; chờ nghiệm thu visual từng prefab.
- [x] Triển khai group và linked/hidden-linked với fallback khi không có đích nhìn thấy.
- [ ] Kiểm tra interruption và hồi quy presentation/booster.
- [ ] Chạy verification, compile, review và Unity visual QA.

Không đánh dấu các bước prototype/visual QA hoàn tất chỉ vì code đã có hoặc compile đã pass.

## 14. Kết quả triển khai và verification ngày 23/10/2026

### 14.1. Code đã có

- FeatureUnlockDialog phát ContinueAccepted trước khi đóng, chặn Continue lặp và phân biệt với Escape/ForceClose. Timing serialized trên dialog có mặc định departure/flight/landing = 0,10/0,40/0,15 giây.
- GameLevelPresentationFlow giữ input gate/lifetime hiện có, tạo presenter theo lượt giới thiệu và cleanup khi Cancel; giữ nguyên thời điểm lưu cờ đã xem và thứ tự intro/booster.
- Cancel chụp reference popup/presenter và giải phóng field trước khi hủy token hoặc SetActive(false), để continuation đóng popup chạy đồng bộ không làm mất reference hoặc gây null giữa cleanup.
- FeatureUnlockFlightPresenter dùng nguyên BoosterPresentationUI/BoosterPresentation.prefab. Chưa tách view chung vì prefab hiện có đáp ứng flight, blocker và sorting; guide có sẵn nhưng không được bật. Không thêm pool hoặc Canvas/prefab mới.
- FeatureFlightTargetSelector chọn một instance ổn định trong các Box đã build. Group door dùng lookup hiện có; linked pair dùng identity runtime và chỉ hướng tới marker đang thấy được. Không tạo state gameplay hay registry mới.
- FeatureFlightTarget giữ reference/version ngắn hạn, kiểm tra lại visual chính và partner. FeatureFlightProjection quy đổi qua screen space, tính nguồn preserveAspect theo đúng pivot; sprite đích dùng local bounds, composite dùng projected bounds.
- Box chỉ bổ sung readiness cho giai đoạn phát entrance và trạng thái clip entrance cụ thể trên Cube. Không đợi loop idle hoặc trì hoãn popup; Continue quá sớm thì bỏ flight có diagnostic.
- Bản đầu giữ nguyên renderer gameplay. Icon fade ở 25% cuối và dùng chính ảnh UI làm highlight ngắn; không sửa sharedMaterial, không scale object gameplay. Hiệu quả highlight cần nghiệm thu trên art thật.
- Đã thêm FeatureFlightVerification trong folder Editor, dùng preview scene và reflection để kiểm tra boundary nội bộ mà không mở rộng runtime API. Menu chạy: Tools → LoopMaster → Verify Feature Unlock Flight, ngoài PlayMode. Runner kiểm tra nguồn UI, selection, 13 mapping và binding; chưa chạy được trong Unity ở lượt triển khai này.
- Code cho các nhóm feature đã được mở rộng trước khi có video prototype ColorLock vì phiên Unity hiện tại đang chiếm project. Đây là bước triển khai code, không thay thế điều kiện nghiệm thu prototype trước khi phát hành.

### 14.2. Kiểm tra đã thực sự chạy

| Check | Kết quả |
| --- | --- |
| Compile runtime bằng Roslyn và response file/reference của Unity 2022.3.62f2, output ở .utmp/FeatureFlightCompile | `Runtime compiler exit: 0`; 332 warning |
| Compile baseline trước feature với cùng compiler/reference | `Baseline compiler exit: 0`; 332 warning; so sánh message không có warning mới |
| Compile Editor, bao gồm FeatureFlightVerification | `Editor compiler exit: 0`; đây là compile, chưa thực thi runner |
| CoreRulesVerification build vào .utmp rồi thực thi DLL từ root project | No-Find và các check gameplay phía trước đã pass; toàn bộ suite dừng ở fixture reference bị thiếu, không được ghi là full pass |
| Kiểm tra static YAML và asset metadata | `Feature asset audit passed: 13 popup sprite bindings, slot anchors, and 6 script/meta pairs.` |
| git diff --check | Không báo lỗi whitespace ở thời điểm kiểm tra |
| Unity batchmode executeMethod | Không thực thi runner: `It looks like another Unity instance is running with this project open.` Giữ nguyên phiên Editor đang mở, không xóa lock hoặc đóng Editor |

Output CoreRules thực tế có:

```text
No-Find policy verified: 0 violations across Assets/00Game/01Scripts.
Linked cubes: admission, boundaries, reservations, landing, cross-Undo, serialization and independent multiple pairs passed.
Asset selection: Level 1 = 10 prefabs; special box/lock dependencies verified.
Unhandled exception. System.InvalidOperationException: Failed: reference puzzle bytes exist
```

Lệnh verification mặc định gặp vấn đề relative output/source path trong môi trường này. Đã build project verification từ thư mục của nó với output/intermediate tuyệt đối trong .utmp, sau đó chạy `dotnet .utmp/CoreRulesFeatureFlight/bin/CoreRulesVerification.dll` từ root để No-Find và asset checks đọc đúng project. Không sửa project verification bên ngoài repository.

### 14.3. Level ứng viên đã tìm thấy từ dữ liệu hiện có

Đây là kiểm kê điều kiện dữ liệu, chưa chứng minh có đích nhìn thấy hoặc feature được giới thiệu đầu tiên trong level. Khi kiểm tra cần save test có bit feature chưa xem và SkipTutorial tắt; không xóa save chính.

| Feature | Level JSON ứng viên |
| --- | --- |
| SecretCube | Level 6 |
| LinkedCubes | Level 15 |
| LinkedHiddenCubes | Level 21 |
| ColorLock | Level 23 |
| AdjacentLock | Level 29 |
| CubeNumLock | Level 37 |
| LongBox | Level 41 |
| SingleBox | Level 49 |
| GroupColorLock | Level 53 |
| SurpriseCube | Level 56 |
| NumLock | Chưa tìm thấy trong lượt quét Level JSON hiện tại; cần fixture test |
| EnterOnly, LimitedColor | Có prefab/anchor, chưa tìm thấy trong lượt quét Level JSON hiện tại; cần fixture test |

### 14.4. Việc còn cần nghiệm thu trong Editor

1. Chạy menu verification sau khi Unity compile xong; lưu output thực tế. Runner EditMode không thay thế PlayMode.
2. Chơi thử ColorLock trước: quay frame rời popup và đoạn fade/landing ở tốc độ thường/chậm. Điều chỉnh projection/timing nếu cần trước khi nghiệm thu các nhóm khác.
3. Chạy các level/fixture đại diện cho 13 feature, nhất là group khóa che slot, cube bị che và hidden-linked không có marker nhìn thấy.
4. Restart/thoát màn, disable và pause trong departure/flight/landing; kiểm tra không còn overlay/blocker và không hở input trước intro tiếp theo.
5. Kiểm tra phone/tablet, safe area, viewport đổi giữa flight và hồi quy booster/difficulty intro.

Các thay đổi source/prefab/scene/art có sẵn trước triển khai đã được chia thành 6 commit local. Push bị automatic approval review từ chối do chưa xác nhận đích GitHub và payload; đang chờ người dùng trả lời câu hỏi xác nhận. Ảnh hidden-linked có thay đổi tiếp trong workspace sau các commit baseline; không gộp hoặc ghi đè thay đổi đó vào feature flight.
