# Linked Boxes — visual prototype

Mở `LinkedBoxes_VisualPreview.unity` để xem bố cục, camera và ánh sáng mẫu.
Hoặc kéo `LinkedBoxes_VisualPreview.prefab` vào scene/prefab preview của bạn.

- `LinkedBoxConnector.prefab`: thanh nối riêng; chỉnh Anchor_A, Anchor_B và Bridge trong Prefab Mode để thử hình dáng.
- `LinkedBoxes_VisualPreview.prefab`: hai khay dùng mesh BoxBase/BoxBack hiện có, các cube màu minh họa và connector lồng bên trong.
- `LinkedBoxes_Preview.png`: ảnh render thực tế bằng Unity 2022.3.62f2.

Pivot connector ở gốc chung của cặp preview; kích thước mặc định ứng với tâm hai box tại X = -1.08 và +1.08. Đây là model tĩnh: đổi khoảng cách box chưa tự kéo giãn connector.
Prefab chỉ có Transform/MeshFilter/MeshRenderer, không collider, script gameplay hoặc cơ chế liên kết. Cube bo tròn và vật liệu Standard là hình mẫu để duyệt visual; chưa thay cube/material đang dùng trong game.
Scene mới chỉ phục vụ xem thử, không được thêm vào Build Settings. Không ghi đè prefab hoặc scene gameplay cũ.
