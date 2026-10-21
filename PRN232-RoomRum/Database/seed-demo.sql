-- Run after DB.sql in SQL Server Management Studio or sqlcmd.
-- Demo records only: apartment complexes and street/ward names are real;
-- boarding rooms, listings, people, prices, availability and messages are fictional.
-- This script is intended for a freshly recreated RoomRum_db (run once).
-- See README-seed.md for accounts, row counts and current address sources.
USE RoomRum_db;
GO

SET XACT_ABORT ON;
-- Required for inserts into tables with filtered indexes, including [User].
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRY
BEGIN TRANSACTION;

DECLARE @landlord UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @t1 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';
DECLARE @t2 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000003';
DECLARE @t3 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000004';
DECLARE @t4 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000005';
DECLARE @t5 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000006';
DECLARE @landlord2 UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000007';
DECLARE @admin UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000008';
DECLARE @r1 UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000001';
DECLARE @r2 UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000002';
DECLARE @r3 UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000003';
DECLARE @r4 UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000004';
DECLARE @r5 UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000005';
DECLARE @r6 UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000006';

-- The seed accounts have unique fake emails and no real identity/phone data.
-- Password values are BCrypt hashes for the demo password RoomRum@123.
INSERT INTO [User] (user_id, role_id, name, email, password, status)
VALUES
(@landlord, (SELECT role_id FROM Role WHERE role_name = 'Landlord'), N'Nguyễn Minh Anh', 'landlord@roomrum.example', '$2a$11$42wBC2rLuZYrZHg3n9Kn4uBZcZQe4uZMJBk6ksn9.kTnIiofr/BVS', 'Active'),
(@t1, (SELECT role_id FROM Role WHERE role_name = 'Tenant'), N'Trần Gia Huy', 'tenant1@roomrum.example', '$2a$11$42wBC2rLuZYrZHg3n9Kn4uBZcZQe4uZMJBk6ksn9.kTnIiofr/BVS', 'Active'),
(@t2, (SELECT role_id FROM Role WHERE role_name = 'Tenant'), N'Lê Thanh Hà', 'tenant2@roomrum.example', '$2a$11$42wBC2rLuZYrZHg3n9Kn4uBZcZQe4uZMJBk6ksn9.kTnIiofr/BVS', 'Active'),
(@t3, (SELECT role_id FROM Role WHERE role_name = 'Tenant'), N'Phạm Quang Duy', 'tenant3@roomrum.example', '$2a$11$42wBC2rLuZYrZHg3n9Kn4uBZcZQe4uZMJBk6ksn9.kTnIiofr/BVS', 'Active'),
(@t4, (SELECT role_id FROM Role WHERE role_name = 'Tenant'), N'Võ Ngọc Mai', 'tenant4@roomrum.example', '$2a$11$42wBC2rLuZYrZHg3n9Kn4uBZcZQe4uZMJBk6ksn9.kTnIiofr/BVS', 'Active'),
(@t5, (SELECT role_id FROM Role WHERE role_name = 'Tenant'), N'Đặng Hoàng Nam', 'tenant5@roomrum.example', '$2a$11$42wBC2rLuZYrZHg3n9Kn4uBZcZQe4uZMJBk6ksn9.kTnIiofr/BVS', 'Active'),
(@landlord2, (SELECT role_id FROM Role WHERE role_name = 'Landlord'), N'Bùi Khánh Linh', 'landlord2@roomrum.example', '$2a$11$42wBC2rLuZYrZHg3n9Kn4uBZcZQe4uZMJBk6ksn9.kTnIiofr/BVS', 'Active'),
(@admin, (SELECT role_id FROM Role WHERE role_name = 'Admin'), N'Demo Admin', 'admin@roomrum.example', '$2a$11$42wBC2rLuZYrZHg3n9Kn4uBZcZQe4uZMJBk6ksn9.kTnIiofr/BVS', 'Active');

INSERT INTO Amenity (amenity_id, amenity_name) VALUES
('30000000-0000-0000-0000-000000000001', N'Máy lạnh'),
('30000000-0000-0000-0000-000000000002', N'Máy giặt'),
('30000000-0000-0000-0000-000000000003', N'Wifi'),
('30000000-0000-0000-0000-000000000004', N'Ban công'),
('30000000-0000-0000-0000-000000000005', N'Tủ lạnh'),
('30000000-0000-0000-0000-000000000006', N'Bãi đỗ xe'),
('30000000-0000-0000-0000-000000000007', N'Tivi'),
('30000000-0000-0000-0000-000000000008', N'Loa');

-- Addresses below use current wards, apartment entrances and residential streets.
-- Boarding-room addresses identify a street/alley only, not a real rental house.
-- Royal City is specified as tower R2 because the
-- wider 72A Nguyen Trai complex spans ward boundaries.
-- Project/developer sources for the physical addresses:
-- HCMC: https://vinhomes.vn/vi/vinhomes-central-park
--       https://masterisehomes.com/millennium/tin-tuc/tin-tuc-su-kien-millennium/thong-bao
-- Hanoi: https://vinhomes.vn/vi/vinhomes-royal-city
-- Boarding-room street/ward sources: see README-seed.md (Ngo Tat To,
-- alley 198 Duong Van Be and alley 2 Mac Thi Buoi).
-- Current ward sources include:
-- https://khcn.tphcm.gov.vn/app/to-chuc-khcn/tim-kiem?page=16&sort=nguoidaidien
-- https://vimda.moh.gov.vn/web/guest/van-ban-cong-bo
-- https://thanhxuan.hanoi.gov.vn/cac-truong-hoc-tren-dia-ban-phuong/cac-truong-hoc-tren-dia-ban-phuong
-- https://datafiles.hanoi.gov.vn/gov-hni/6778/FileDieuKienHoatDong/2025/11/12/11-11%20C%C6%A0%20S%E1%BB%9E%20TI%C3%8AM%20CH%E1%BB%A6NG.pdf
INSERT INTO Room (room_id, user_id, status, type, title, address, area,
                  electricity_fee, water_fee, service_fee, deposit, price, description, [rule])
VALUES
(@r1, @landlord, 'Rented', N'Căn hộ', N'Căn hộ ba phòng ngủ Thạnh Mỹ Tây', N'Vinhomes Central Park, 208 Nguyễn Hữu Cảnh, phường Thạnh Mỹ Tây, Thành phố Hồ Chí Minh', 88, 3500, 85000, 600000, 30000000, 15000000, N'Ba phòng ngủ, bếp và phòng khách dùng chung.', N'Không hút thuốc trong nhà.'),
(@r2, @landlord2, 'Rented', N'Phòng trọ', N'Phòng trọ có gác lửng Ngô Tất Tố', N'Đường Ngô Tất Tố, phường Thạnh Mỹ Tây, Thành phố Hồ Chí Minh', 30, 3500, 100000, 150000, 4200000, 4200000, N'Phòng trọ trong dãy nhà dân, diện tích sàn 30 m² chưa gồm gác lửng. Có cửa sổ, vệ sinh và bếp riêng, tủ lạnh nhỏ, wifi và chỗ để xe máy chung; phù hợp hai người. Nước tính theo người/tháng, dịch vụ tính theo phòng/tháng và đã gồm gửi một xe máy. Địa chỉ minh họa theo tuyến đường, không chỉ một nhà trọ có thật.', N'Tối đa hai người; không hút thuốc trong phòng, không sạc xe điện trong phòng; giữ yên tĩnh sau 22 giờ.'),
(@r3, @landlord, 'Rented', N'Căn hộ dịch vụ', N'Căn hộ dịch vụ Khánh Hội', N'Millennium, 132 Bến Vân Đồn, phường Khánh Hội, Thành phố Hồ Chí Minh', 62, 3800, 90000, 400000, 22000000, 11000000, N'Căn hộ hai phòng ngủ có khu bếp riêng, tivi và loa.', N'Không nuôi thú cưng.'),
(@r4, @landlord2, 'Available', N'Căn hộ', N'Căn hộ gia đình Royal City', N'Tòa R2, Vinhomes Royal City, 72A Nguyễn Trãi, phường Thanh Xuân, Thành phố Hà Nội', 82, 4000, 150000, 850000, 34000000, 17000000, N'Căn hộ ba phòng ngủ phù hợp gia đình.', N'Giữ gìn khu vực chung.'),
(@r5, @landlord, 'Available', N'Phòng trọ', N'Phòng trọ khép kín cho sinh viên Dương Văn Bé', N'Ngõ 198 Dương Văn Bé, phường Vĩnh Tuy, Thành phố Hà Nội', 20, 3500, 80000, 100000, 2800000, 2800000, N'Phòng trọ bình dân trong nhà dân, có cửa sổ và vệ sinh riêng; dùng bếp, máy giặt và chỗ để xe chung. Chưa có nội thất, phù hợp một người. Nước tính theo người/tháng, dịch vụ tính theo phòng/tháng. Địa chỉ minh họa theo ngõ, không chỉ một nhà trọ có thật.', N'Tối đa một người; không nấu ăn trong phòng, vệ sinh bếp chung sau khi dùng; không gây tiếng ồn sau 22 giờ.'),
(@r6, @landlord2, 'Pending', N'Phòng trọ', N'Phòng trọ có ban công và điều hòa Vĩnh Tuy', N'Ngõ 2 Mạc Thị Bưởi, phường Vĩnh Tuy, Thành phố Hà Nội', 28, 3500, 100000, 180000, 3800000, 3800000, N'Phòng trọ trong nhà dân có ban công, điều hòa, khu bếp và vệ sinh riêng; phù hợp một đến hai người đi làm. Nước tính theo người/tháng, dịch vụ tính theo phòng/tháng. Tin đang chờ duyệt. Địa chỉ minh họa theo ngõ, không chỉ một nhà trọ có thật.', N'Tối đa hai người; không hút thuốc trong nhà, không để đồ cản lối thoát hiểm.');

-- Current tenants are inserted before ratings because RoomRating has a composite FK.
INSERT INTO RoomTenant (room_id, user_id, hired_at, left_at, status) VALUES
(@r1, @t1, '2026-07-01', '2026-09-30', 'Ended'), (@r1, @t2, '2026-07-01', NULL, 'Active'),
(@r2, @t3, '2026-08-01', NULL, 'Active'), (@r2, @t4, '2026-08-01', NULL, 'Active'),
(@r3, @t5, '2026-09-01', NULL, 'Active'), (@r3, @t1, '2026-10-01', NULL, 'Active');

-- Previous invitation periods for those same six room/tenant pairs.
INSERT INTO RoomTenantHistory (history_id, room_id, user_id, hired_at, left_at, status, archived_at) VALUES
(NEWID(), @r1, @t1, '2026-01-05', '2026-02-05', 'Ended', '2026-06-28'),
(NEWID(), @r1, @t2, '2026-03-05', '2026-03-06', 'Cancelled', '2026-06-28'),
(NEWID(), @r2, @t3, '2026-02-05', '2026-03-05', 'Ended', '2026-07-28'),
(NEWID(), @r2, @t4, '2026-04-05', '2026-04-06', 'Cancelled', '2026-07-28'),
(NEWID(), @r3, @t5, '2026-03-05', '2026-04-05', 'Ended', '2026-08-28'),
(NEWID(), @r3, @t1, '2026-05-05', '2026-05-06', 'Cancelled', '2026-09-28');

INSERT INTO Sharing (sharing_id, user_id, room_id, title, quantity, gender, sharing_fee, introduction, [rule]) VALUES
(NEWID(), @t2, @r1, N'Tìm bạn thuê phòng ngủ lớn', 1, N'Nữ', 6000000, N'Phòng có cửa sổ và bàn làm việc.', N'Giữ vệ sinh chung.'),
(NEWID(), @t2, @r1, N'Tìm bạn thuê phòng ngủ nhỏ', 1, N'Không yêu cầu', 4500000, N'Phòng riêng, dùng chung bếp và phòng khách.', N'Không hút thuốc.'),
(NEWID(), @t3, @r2, N'Tìm bạn thay chỗ ở ghép Ngô Tất Tố', 1, N'Nam', 2100000, N'Hai người đang ở; một người dự kiến chuyển đi, tìm bạn thay chỗ. Tiền phòng chia đôi, điện nước tính riêng.', N'Tổng số người ở không quá hai; giữ yên tĩnh sau 22 giờ.'),
(NEWID(), @t4, @r2, N'Tìm người tiếp quản chỗ ở phòng trọ có gác', 1, N'Không yêu cầu', 2100000, N'Tin của người sắp chuyển đi cho cùng chỗ ở ghép; đã có tủ lạnh nhỏ, không có máy giặt riêng.', N'Thống nhất ngày bàn giao với bạn cùng phòng; giữ khu vực chung sạch sẽ.'),
(NEWID(), @landlord2, @r4, N'Tìm nhóm thuê căn hộ gia đình', 3, N'Không yêu cầu', 17000000, N'Cho thuê cả căn hộ, có thể ở theo nhóm.', N'Giữ gìn khu vực chung.'),
(NEWID(), @landlord, @r5, N'Cho thuê phòng trọ khép kín Dương Văn Bé', 1, N'Không yêu cầu', 2800000, N'Cho thuê nguyên phòng 20 m², vệ sinh riêng, bếp và máy giặt chung.', N'Tối đa một người; giữ yên tĩnh buổi tối.');

INSERT INTO Appointment (appointment_id, user_id, room_id, appointment_date, appointment_time, status, description) VALUES
(NEWID(), @t1, @r4, '2026-10-12', '09:00', 'Completed', N'Đã xem căn hộ gia đình.'),
(NEWID(), @t2, @r5, '2026-10-13', '14:00', 'Rejected', N'Chủ phòng không thể sắp xếp lịch này.'),
(NEWID(), @t3, @r5, '2026-10-14', '10:00', 'Cancelled', N'Khách hủy do thay đổi kế hoạch.'),
(NEWID(), @t4, @r4, '2026-10-20', '15:00', 'Confirmed', N'Đã xác nhận lịch xem căn hộ.'),
(NEWID(), @t5, @r5, '2026-10-21', '09:30', 'Pending', N'Muốn xem phòng và khu bếp.'),
(NEWID(), @t1, @r5, '2026-10-22', '16:00', 'Pending', N'Xin xem phòng sau giờ làm.');

INSERT INTO Report (report_id, user_id, room_id, reason, report_date, status) VALUES
(NEWID(), @t1, @r4, N'Giá trong tin khác với giá được trao đổi khi xem phòng.', '2026-10-12', 'Pending'),
(NEWID(), @t2, @r5, N'Ảnh phòng chỉ là ảnh minh họa, cần ghi rõ hơn.', '2026-10-13', 'Resolved'),
(NEWID(), @t3, @r2, N'Cần làm rõ phụ phí gửi xe máy thứ hai khi ở ghép.', '2026-10-14', 'Pending'),
(NEWID(), @t4, @r4, N'Diện tích sử dụng cần được xác minh.', '2026-10-15', 'InReview'),
(NEWID(), @t5, @r1, N'Tin ở ghép đăng trùng nội dung.', '2026-10-16', 'Resolved'),
(NEWID(), @t1, @r5, N'Cần làm rõ quy định sử dụng bếp chung.', '2026-10-17', 'Pending');

INSERT INTO Repairing (maintenance_id, user_id, room_id, priority, repair_items, description, status, expecting_date, response) VALUES
(NEWID(), @t2, @r1, N'Trung bình', N'Máy lạnh', N'Máy lạnh làm mát yếu.', 'Pending', '2026-10-23', NULL),
(NEWID(), @t2, @r1, N'Thấp', N'Đèn phòng khách', N'Đèn chập chờn.', 'InProgress', '2026-10-24', N'Đã liên hệ thợ.'),
(NEWID(), @t3, @r2, N'Cao', N'Vòi nước', N'Vòi nước bị rò.', 'Pending', '2026-10-20', NULL),
(NEWID(), @t4, @r2, N'Khẩn cấp', N'Ổ cắm', N'Ổ cắm có mùi khét.', 'InProgress', '2026-10-19', N'Đã ngắt điện và hẹn thợ kiểm tra.'),
(NEWID(), @t5, @r3, N'Thấp', N'Cửa tủ', N'Bản lề cửa tủ bị lỏng.', 'Completed', '2026-10-17', N'Đã sửa xong.'),
(NEWID(), @t1, @r3, N'Trung bình', N'Máy giặt', N'Máy giặt phát tiếng ồn.', 'Rejected', '2026-10-16', N'Kỹ thuật kiểm tra và xác nhận máy hoạt động bình thường.');

-- Periodic maintenance scheduled by each room's landlord, not tenant repair requests.
INSERT INTO Repairing (maintenance_id, user_id, room_id, priority, repair_items, description, status, expecting_date, response) VALUES
(NEWID(), @landlord, @r3, N'Trung bình', N'Máy lạnh, máy giặt', N'Chủ trọ lên lịch bảo trì định kỳ 6 tháng: vệ sinh máy lạnh, kiểm tra đường thoát nước và bảo dưỡng máy giặt trong căn hộ.', 'Pending', '2026-10-25', N'Đã thông báo lịch dự kiến cho người thuê, đang chờ xác nhận thời gian vào phòng.'),
(NEWID(), @landlord2, @r2, N'Trung bình', N'Hệ thống điện, thiết bị vệ sinh', N'Chủ trọ tổ chức bảo trì định kỳ hằng quý: kiểm tra aptomat, siết đầu nối điện, vệ sinh vòi nước và kiểm tra rò rỉ đường ống tại phòng trọ.', 'InProgress', '2026-10-19', N'Đã thống nhất lịch với người thuê; thợ đang kiểm tra và bảo dưỡng thiết bị.');

INSERT INTO Favorite (favorite_id, user_id, room_id) VALUES
(NEWID(), @t1, @r4), (NEWID(), @t2, @r5), (NEWID(), @t3, @r4),
(NEWID(), @t4, @r5), (NEWID(), @t5, @r4), (NEWID(), @t1, @r5);

INSERT INTO RoomRating (rating_id, user_id, room_id, rating, comment, rating_date) VALUES
(NEWID(), @t1, @r1, 2, N'Phòng thoáng nhưng cách âm chưa tốt.', '2026-10-01'),
(NEWID(), @t2, @r1, 4, N'Vị trí đi lại thuận tiện.', '2026-10-02'),
(NEWID(), @t3, @r2, 5, N'Tiện ích đáp ứng nhu cầu.', '2026-10-03'),
(NEWID(), @t4, @r2, 4, N'Phòng đủ ánh sáng.', '2026-10-04'),
(NEWID(), @t5, @r3, 3, N'Tiện đi lại, phí dịch vụ hơi cao.', '2026-10-05'),
(NEWID(), @t1, @r3, 3, N'Vị trí thuận tiện nhưng còn tiếng ồn.', '2026-10-06');

INSERT INTO RoomAmenity (room_id, amenity_id) VALUES
(@r1, '30000000-0000-0000-0000-000000000003'),
(@r2, '30000000-0000-0000-0000-000000000003'),
(@r2, '30000000-0000-0000-0000-000000000005'),
(@r3, '30000000-0000-0000-0000-000000000007'),
(@r3, '30000000-0000-0000-0000-000000000008'),
(@r4, '30000000-0000-0000-0000-000000000006'),
(@r5, '30000000-0000-0000-0000-000000000002'),
(@r6, '30000000-0000-0000-0000-000000000001');

-- Neutral placeholder images; they do not depict the named buildings.
INSERT INTO RoomImage (room_image_id, room_id, image_url) VALUES
(NEWID(), @r1, 'https://placehold.co/800x600/2F6B5A/FFFFFF/png?text=Demo+HCMC+1'),
(NEWID(), @r2, 'https://placehold.co/800x600/8A5A44/FFFFFF/png?text=Demo+HCMC+2'),
(NEWID(), @r3, 'https://placehold.co/800x600/355C7D/FFFFFF/png?text=Demo+HCMC+3'),
(NEWID(), @r4, 'https://placehold.co/800x600/765A88/FFFFFF/png?text=Demo+Hanoi+1'),
(NEWID(), @r5, 'https://placehold.co/800x600/9B6B3F/FFFFFF/png?text=Demo+Hanoi+2'),
(NEWID(), @r6, 'https://placehold.co/800x600/4F6370/FFFFFF/png?text=Demo+Hanoi+3');

DECLARE @c1 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000001';
DECLARE @c2 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000002';
DECLARE @c3 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000003';
DECLARE @c4 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000004';
DECLARE @c5 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000005';
DECLARE @c6 UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000006';
INSERT INTO Conversation (conversation_id, created_at) VALUES
(@c1, '2026-10-10'), (@c2, '2026-10-11'), (@c3, '2026-10-12'),
(@c4, '2026-10-13'), (@c5, '2026-10-14'), (@c6, '2026-10-15');

INSERT INTO Message (message_id, conversation_id, sender, receiver, content, chat_at) VALUES
(NEWID(), @c1, @t1, @landlord2, N'Chào anh/chị, căn hộ Royal City còn xem được không?', '2026-10-10 09:00'),
(NEWID(), @c1, @landlord2, @t1, N'Có thể xem lúc 9 giờ ngày 12/10 nhé.', '2026-10-10 09:15'),
(NEWID(), @c2, @t2, @landlord, N'Phòng trọ ngõ 198 Dương Văn Bé có dùng chung bếp không?', '2026-10-11 10:00'),
(NEWID(), @c3, @t3, @landlord2, N'Phòng trọ Ngô Tất Tố có phụ phí gửi xe máy thứ hai không?', '2026-10-12 11:00'),
(NEWID(), @c4, @landlord2, @t4, N'Mình đã xác nhận lịch xem căn hộ Royal City.', '2026-10-13 12:00'),
(NEWID(), @c5, @t5, @landlord2, N'Căn hộ Royal City có chỗ đỗ xe không?', '2026-10-14 13:00'),
(NEWID(), @c6, @landlord, @t1, N'Bạn có thể xem phòng trọ ngõ 198 Dương Văn Bé chiều 22/10.', '2026-10-15 14:00');

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
