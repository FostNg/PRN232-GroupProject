# Kết quả test API (21/10/2026)

Swagger: http://localhost:5041/swagger

## Cách chạy lại

Chạy API từ thư mục `RoomRum`:

```powershell
dotnet run --launch-profile http
```

Từ thư mục gốc dự án, chạy bộ test chỉ đọc dùng tài khoản và GUID trong `seed-demo.sql`:

```powershell
pwsh -NoProfile -File Database/verify-seed-api.ps1 -BaseUrl http://localhost:5041
```

Chạy bộ test hồi quy toàn bộ API (tạo dữ liệu tạm rồi dọn trong `finally`):

```powershell
pwsh -NoProfile -File Database/verify-all.ps1 -BaseUrl http://localhost:5041 -SqlServer .\SQLEXPRESS -Database RoomRum_db
```

Khởi động lại API giữa các lượt chạy `verify-all.ps1` liên tiếp để đặt lại rate limiter đăng nhập. Bộ test hồi quy cần SQL Server có schema hiện hành và đăng nhập Windows được phép ghi/xóa dữ liệu test.

## Test case đã chạy

| Nhóm | Ca kiểm tra chính | Kết quả |
| --- | --- | --- |
| Swagger | 37 path, 42 thao tác, response schema, trường request đăng ký | Đạt |
| Đăng ký/đăng nhập | `phone`, `dob`, `gender`, `image_url` được lưu; không có `citizen_id`; tài khoản mới Inactive; đăng nhập, phân quyền, rate limit | Đạt |
| Phòng | Tìm kiếm, xem chi tiết, tiện ích, duyệt tin, sửa tin, ẩn tin, ảnh, phân trang | Đạt |
| Thuê phòng | Mời, nhận, hủy, kết thúc, lịch sử, nhiều người thuê và cập nhật đồng thời | Đạt |
| Tương tác | Yêu thích, lịch xem, đánh giá, ở ghép, sửa chữa, báo cáo, chat | Đạt |
| Dữ liệu seed | Đăng nhập 4 vai trò/tài khoản, xem dữ liệu phòng, lịch hẹn, thuê, báo cáo, sửa chữa và chat theo GUID seed | Đạt |

Kết quả cuối: `verify-all.ps1` đạt **332 HTTP checks** trên **42 thao tác Swagger**; `verify-seed-api.ps1` đạt **59 checks**. Dữ liệu tạm của bộ hồi quy được dọn trong `finally`.

## Quy tắc đã chốt

1. Chỉ người thuê `Active` mới làm phòng chuyển sang `Rented`. Gửi lời mời `Pending` giữ phòng `Available`; chấp nhận lời mời chuyển sang `Rented`. Khi người thuê `Active` cuối cùng rời đi, phòng trở lại `Available` kể cả khi còn lời mời `Pending`. `migrate-occupied-room-status.sql` đồng bộ database cũ theo quy tắc này.
2. `GET /api/rooms/mine` chỉ hiện phòng `Available` và `Rented` của chủ trọ; phòng `Pending` không hiện.
3. `PUT /api/rooms/{id}` với `imageUrls: []` giữ nguyên ảnh. Với mảng URL không rỗng, URL đã có ở phòng sẽ xóa ảnh đó; URL chưa có sẽ thêm ảnh; ảnh không có trong request giữ nguyên. Một URL không hợp lệ trả 400. `amenityIds` tiếp tục giữ quy tắc cũ: mảng rỗng giữ nguyên, mảng không rỗng thay danh sách tiện ích.
4. Phòng `Available` có lời mời `Pending` bị ẩn khỏi tìm kiếm, đánh giá công khai và danh sách tin ở ghép. Người ngoài không xem được chi tiết bằng ID hoặc tạo lịch hẹn/yêu thích mới; chủ phòng và người được mời vẫn xem được chi tiết bằng token. Mục yêu thích đã lưu của người ngoài cũng tạm ẩn.

Database được test có thêm dữ liệu ngoài seed (10 người dùng, 9 phòng trước khi chạy test). Các test seed kiểm tra đúng GUID/tài khoản demo thay vì giả định tổng số bản ghi bằng đúng số dòng trong `seed-demo.sql`.
