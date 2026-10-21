# Dữ liệu mẫu RoomRum

File `seed-demo.sql` dùng với cấu trúc trong `DB.sql`, mã hóa UTF-8, dành cho SQL Server. Dữ liệu người dùng, tin đăng, giá và hội thoại đều là giả lập; ảnh là ảnh placeholder. Có cả căn hộ và phòng trọ nhà dân tại TP.HCM và Hà Nội. Địa chỉ phòng trọ chỉ ghi đến tuyến đường/ngõ đã đối chiếu phường, không gán tin cho thuê giả lập cho một số nhà cụ thể. Hai phòng đầu có người thuê `Active`, nên trạng thái seed của chúng là `Rented`.

## Cách chạy

1. Nếu chưa có database, chạy `DB.sql` trong SSMS. **File này xóa và tạo lại `RoomRum_db`; không chạy trên database có dữ liệu cần giữ.**
2. Chạy `seed-demo.sql` một lần trong SSMS, hoặc từ thư mục Database:

```powershell
sqlcmd -S '.\SQLEXPRESS' -E -I -b -f 65001 -i .\seed-demo.sql
```

Thay tên SQL Server cho đúng máy. Script seed chỉ thêm dữ liệu, không xóa dữ liệu hoặc tạo lại bảng. Nếu trùng ID/email/tên tiện ích, script báo lỗi và rollback toàn bộ lượt chèn; không chạy lại `DB.sql` chỉ để xử lý lỗi này. Các vai trò Tenant, Landlord, Admin đã được `DB.sql` tạo sẵn.

Nếu database đã nạp bản seed cũ, chạy `migrate-occupied-room-status.sql` để đồng bộ trạng thái: phòng có người thuê `Active` là `Rented`; phòng `Rented` nhưng không có người thuê `Active` trở lại `Available`. Lời mời `Pending` không làm đổi trạng thái phòng:

```powershell
sqlcmd -S '.\SQLEXPRESS' -E -I -b -i .\migrate-occupied-room-status.sql
```

## Tài khoản demo

Mật khẩu chung: `RoomRum@123` (lưu BCrypt trong SQL). Tất cả tài khoản đều `Active`.

| Vai trò | Email |
| --- | --- |
| Admin | admin@roomrum.example |
| Chủ phòng | landlord@roomrum.example, landlord2@roomrum.example |
| Người thuê | tenant1@roomrum.example đến tenant5@roomrum.example |

## Dữ liệu

| Bảng | Số dòng seed |
| --- | ---: |
| User | 8 |
| Amenity | 8 |
| Room | 6 (3 TP.HCM, 3 Hà Nội) |
| RoomTenant | 6 |
| RoomTenantHistory | 6 |
| Sharing | 6 |
| Appointment | 6 |
| Report | 6 |
| Repairing | 8 |
| Favorite | 6 |
| RoomRating | 6 |
| RoomAmenity | 8 |
| RoomImage | 6 |
| Conversation | 6 |
| Message | 7 |

`Role` có sẵn 3 dòng trong `DB.sql`. Người thuê và lịch sử thuê được chèn trước đánh giá để đáp ứng khóa ngoại ghép. Các lịch hẹn mẫu nằm trong tháng 10/2026. Giá/cọc/phí dùng VND; điện tính theo kWh. Với phòng trọ, nước tính theo người/tháng và dịch vụ theo phòng/tháng, đã ghi trong mô tả; với căn hộ, các khoản này là số tiền minh họa theo tháng.

`Repairing` gồm 6 yêu cầu sửa chữa từ khách thuê và 2 yêu cầu bảo trì định kỳ từ chủ trọ. Hai dòng bảo trì dùng `user_id` của chủ sở hữu phòng: bảo dưỡng máy lạnh/máy giặt căn hộ Millennium mỗi 6 tháng và kiểm tra điện/nước phòng trọ Ngô Tất Tố hằng quý.

| Loại phòng | Diện tích | Giá thuê/tháng | Đặc điểm |
| --- | ---: | ---: | --- |
| Căn hộ Central Park | 88 m² | 15.000.000 | Ba phòng ngủ |
| Phòng trọ Ngô Tất Tố | 30 m² sàn, chưa gồm gác | 4.200.000 | Gác lửng, bếp và vệ sinh riêng, ở ghép hai người |
| Căn hộ dịch vụ Millennium | 62 m² | 11.000.000 | Hai phòng ngủ, nội thất |
| Căn hộ Royal City | 82 m² | 17.000.000 | Cho gia đình/nhóm |
| Phòng trọ Dương Văn Bé | 20 m² | 2.800.000 | Bình dân, vệ sinh riêng, bếp và máy giặt chung |
| Phòng trọ Mạc Thị Bưởi | 28 m² | 3.800.000 | Ban công, điều hòa, bếp và vệ sinh riêng, chờ duyệt |

Hai tin Sharing của phòng Ngô Tất Tố cùng tìm người thay một chỗ sắp trống, không phải hai chỗ trống độc lập. Giá ở ghép là một nửa tiền thuê phòng, chưa gồm điện nước.

## Địa chỉ hành chính

Dùng phân cấp **phường → thành phố trực thuộc trung ương**, không thêm quận/huyện hay TP. Thủ Đức vào địa chỉ. Cơ sở sắp xếp năm 2025: [Nghị quyết 1685/NQ-UBTVQH15 (TP.HCM)](https://chinhphu.vn/?classid=2&docid=214007&pageid=27160), [Nghị quyết 1656/NQ-UBTVQH15 (Hà Nội)](https://chinhphu.vn/?classid=2&docid=214008&pageid=27160).

| Căn hộ / phòng trọ | Đường, phường, thành phố |
| --- | --- |
| Vinhomes Central Park | 208 Nguyễn Hữu Cảnh, phường Thạnh Mỹ Tây, Thành phố Hồ Chí Minh |
| Phòng trọ có gác lửng | Đường Ngô Tất Tố, phường Thạnh Mỹ Tây, Thành phố Hồ Chí Minh |
| Millennium | 132 Bến Vân Đồn, phường Khánh Hội, Thành phố Hồ Chí Minh |
| Royal City, tòa R2 | 72A Nguyễn Trãi, phường Thanh Xuân, Thành phố Hà Nội |
| Phòng trọ khép kín | Ngõ 198 Dương Văn Bé, phường Vĩnh Tuy, Thành phố Hà Nội |
| Phòng trọ có ban công | Ngõ 2 Mạc Thị Bưởi, phường Vĩnh Tuy, Thành phố Hà Nội |

Nguồn đối chiếu địa chỉ (tra cứu ngày 19/10/2026):

- [Thuế cơ sở Bình Thạnh: 208 Nguyễn Hữu Cảnh thuộc phường Thạnh Mỹ Tây](https://www.binhthanhtax.gov.vn/wp-content/uploads/2025/12/DS-NNT-no-thue-thang-11.-2025-5-1.pdf).
- [Bộ Công an: đường Ngô Tất Tố thuộc phường Thạnh Mỹ Tây](https://www.mps.gov.vn/bai-viet/kip-thoi-dap-tat-dam-chay-giai-cuu-an-toan-3-nguoi-an-toan-1777705495). Nguồn dùng để đối chiếu đường/phường, không dùng căn nhà trong bài làm địa chỉ phòng trọ.
- [Thi hành án dân sự TP.HCM: 132 Bến Vân Đồn, phường Khánh Hội](https://thads.moj.gov.vn/hcm/tin-tuc/chi-tiet/tphcm-phong-thads-khu-vuc-1-quyet-inh-ve-viec-chua-co-ieu-kien-thi-hanh-an-so-1234q-thads-ngay-1562026-oi-voi-ong-huynh-tu-luc-ia-chi-132-ben-van-on-phuong-khanh-hoi-thanh-pho-ho-chi-minh-th2gq3dac0.html).
- [Cổng thông tin phường Thanh Xuân: tòa R2 Royal City, 72A Nguyễn Trãi](https://thanhxuan.hanoi.gov.vn/cac-truong-hoc-tren-dia-ban-phuong/cac-truong-hoc-tren-dia-ban-phuong). Chỉ rõ tòa R2 để tránh nhập nhằng ranh giới của toàn khu.
- [Cổng thông tin phường Vĩnh Tuy](https://vinhtuy.hanoi.gov.vn/trang-chu/thong-tin-da-phuong-tien): thông tin dự án cải tạo đường ngõ ngách năm 2026 liệt kê ngõ 198 Dương Văn Bé và ngõ 2 Mạc Thị Bưởi thuộc phường Vĩnh Tuy.
