USE master;
GO

-- Kiểm tra nếu CSDL RoomRum_db đã tồn tại thì đóng các kết nối và xóa nó đi
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'RoomRum_db')
BEGIN
    ALTER DATABASE RoomRum_db SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE RoomRum_db;
END
GO

-- Tạo CSDL mới
CREATE DATABASE RoomRum_db;
GO

-- Chuyển sang sử dụng CSDL vừa tạo
USE RoomRum_db;
GO

-- Required for filtered unique indexes on optional user fields.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

-- Tạo bảng Role
CREATE TABLE Role (
    role_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    role_name VARCHAR(50) NOT NULL
);
CREATE UNIQUE NONCLUSTERED INDEX UX_Role_Name ON Role(role_name);

-- Tạo bảng Amenity
CREATE TABLE Amenity (
    amenity_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    amenity_name NVARCHAR(255) NOT NULL
);
CREATE UNIQUE NONCLUSTERED INDEX UX_Amenity_Name ON Amenity(amenity_name);

-- Tạo bảng User
CREATE TABLE [User] (
    user_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    role_id UNIQUEIDENTIFIER NOT NULL,
    phone VARCHAR(20),
    name NVARCHAR(255) NOT NULL,
    email VARCHAR(255),
    dob DATE,
    gender NVARCHAR(20),
    citizen_id VARCHAR(50),
    password VARCHAR(255) NOT NULL,
    image_url VARCHAR(MAX),
    status VARCHAR(50),
    created_at DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (role_id) REFERENCES Role(role_id)
);
-- Index cho khóa ngoại hay dùng để phân quyền
CREATE NONCLUSTERED INDEX IX_User_RoleID ON [User](role_id);
CREATE UNIQUE NONCLUSTERED INDEX UX_User_Email_NotNull ON [User](email) WHERE email IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX UX_User_CitizenId_NotNull ON [User](citizen_id) WHERE citizen_id IS NOT NULL;

-- Tạo bảng Room
CREATE TABLE Room (
    room_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    user_id UNIQUEIDENTIFIER NOT NULL,
    status VARCHAR(50),
    type NVARCHAR(50),
    title NVARCHAR(255),
    address NVARCHAR(MAX),
    area DECIMAL(10,2),
    electricity_fee DECIMAL(18,2),
    water_fee DECIMAL(18,2),
    service_fee DECIMAL(18,2),
    deposit DECIMAL(18,2),
    price DECIMAL(18,2),
    description NVARCHAR(MAX),
    [rule] NVARCHAR(MAX),
    FOREIGN KEY (user_id) REFERENCES [User](user_id)
);
-- Index cho chủ phòng (JOIN thường xuyên)
CREATE NONCLUSTERED INDEX IX_Room_UserID ON Room(user_id);
-- Composite Index cho các bộ lọc tìm kiếm phòng phổ biến nhất
CREATE NONCLUSTERED INDEX IX_Room_Filter ON Room(status, type, price);

-- Tạo bảng Sharing
CREATE TABLE Sharing (
    sharing_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    user_id UNIQUEIDENTIFIER NOT NULL,
    room_id UNIQUEIDENTIFIER NOT NULL,
    title NVARCHAR(255),
    quantity INT,
    gender NVARCHAR(20),
    sharing_fee DECIMAL(18,2),
    introduction NVARCHAR(MAX),
    [rule] NVARCHAR(MAX),
    FOREIGN KEY (user_id) REFERENCES [User](user_id),
    FOREIGN KEY (room_id) REFERENCES Room(room_id)
);
-- Tìm tin ở ghép theo phòng
CREATE NONCLUSTERED INDEX IX_Sharing_RoomID ON Sharing(room_id);

-- Tạo bảng Appointment
CREATE TABLE Appointment (
    appointment_id UNIQUEIDENTIFIER PRIMARY KEY
        DEFAULT NEWSEQUENTIALID(),

    user_id UNIQUEIDENTIFIER NOT NULL,
    room_id UNIQUEIDENTIFIER NOT NULL,

    appointment_date DATE NOT NULL,
    appointment_time TIME NOT NULL,

    -- Trạng thái lịch hẹn
    status VARCHAR(20) NOT NULL DEFAULT 'Pending'
        CHECK (status IN (
            'Pending',
            'Confirmed',
            'Rejected',
            'Cancelled',
            'Completed'
        )),

    description NVARCHAR(MAX),

    -- Thời gian tạo và cập nhật
    created_at DATETIME2 NOT NULL
        DEFAULT SYSDATETIME(),
    updated_at DATETIME2 NULL,

    -- Khóa ngoại
    FOREIGN KEY (user_id) REFERENCES [User](user_id),
    FOREIGN KEY (room_id) REFERENCES Room(room_id)
);

-- Index xem lịch hẹn theo phòng
CREATE NONCLUSTERED INDEX IX_Appointment_RoomID
ON Appointment(room_id);

-- Index xem lịch hẹn của một user theo ngày/giờ
CREATE NONCLUSTERED INDEX IX_Appointment_User_Date
ON Appointment(user_id, appointment_date, appointment_time);

-- Tạo bảng Report
CREATE TABLE Report (
    report_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    user_id UNIQUEIDENTIFIER NOT NULL,
    room_id UNIQUEIDENTIFIER NOT NULL,
    reason NVARCHAR(MAX),
    report_date DATETIME DEFAULT GETDATE(),
    status VARCHAR(50),
    FOREIGN KEY (user_id) REFERENCES [User](user_id),
    FOREIGN KEY (room_id) REFERENCES Room(room_id)
);

-- Tạo bảng Repairing
CREATE TABLE Repairing (
    maintenance_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    user_id UNIQUEIDENTIFIER NOT NULL,
    room_id UNIQUEIDENTIFIER NOT NULL,
    priority NVARCHAR(50),
    repair_items NVARCHAR(MAX),
    description NVARCHAR(MAX),
    status VARCHAR(50),
    expecting_date DATE,
    response NVARCHAR(MAX),
    FOREIGN KEY (user_id) REFERENCES [User](user_id),
    FOREIGN KEY (room_id) REFERENCES Room(room_id)
);
-- Lọc danh sách sửa chữa theo phòng và trạng thái
CREATE NONCLUSTERED INDEX IX_Repairing_RoomID_Status ON Repairing(room_id, status);

-- Tạo bảng Favorite
CREATE TABLE Favorite (
    favorite_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    user_id UNIQUEIDENTIFIER NOT NULL,
    room_id UNIQUEIDENTIFIER NOT NULL,
    FOREIGN KEY (user_id) REFERENCES [User](user_id),
    FOREIGN KEY (room_id) REFERENCES Room(room_id)
);
-- Lấy danh sách yêu thích của 1 user nhanh hơn
CREATE NONCLUSTERED INDEX IX_Favorite_UserID ON Favorite(user_id);
CREATE UNIQUE NONCLUSTERED INDEX UX_Favorite_User_Room ON Favorite(user_id, room_id);

-- Tạo bảng RoomRating
CREATE TABLE RoomRating (
    rating_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    user_id UNIQUEIDENTIFIER NOT NULL,
    room_id UNIQUEIDENTIFIER NOT NULL,
    rating INT NOT NULL CHECK (rating >= 1 AND rating <= 5),
    comment NVARCHAR(MAX),
    rating_date DATETIME2 NOT NULL DEFAULT SYSDATETIME(),

    -- Mỗi user chỉ được đánh giá một phòng một lần
    CONSTRAINT UQ_RoomRating_User_Room UNIQUE (user_id, room_id),

    FOREIGN KEY (user_id) REFERENCES [User](user_id),
    FOREIGN KEY (room_id) REFERENCES Room(room_id)
);
-- Lấy danh sách đánh giá và tính sao trung bình của 1 phòng
CREATE NONCLUSTERED INDEX IX_RoomRating_RoomID ON RoomRating(room_id);

-- Trạng thái thuê hiện tại: mỗi cặp phòng/người có đúng một dòng.
CREATE TABLE RoomTenant (
    room_id UNIQUEIDENTIFIER NOT NULL,
    user_id UNIQUEIDENTIFIER NOT NULL,

    hired_at DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    left_at DATETIME2 NULL,

    status VARCHAR(20) NOT NULL CONSTRAINT DF_RoomTenant_Status DEFAULT 'Pending'
        CONSTRAINT CK_RoomTenant_Status CHECK (status IN ('Pending', 'Active', 'Ended', 'Cancelled')),

    PRIMARY KEY (room_id, user_id),
    FOREIGN KEY (room_id) REFERENCES Room(room_id),
    FOREIGN KEY (user_id) REFERENCES [User](user_id),

    -- Nếu đã rời phòng thì thời điểm rời không được trước thời điểm bắt đầu thuê
    CONSTRAINT CK_RoomTenant_LeftAt CHECK (left_at IS NULL OR left_at >= hired_at)
);
-- Khóa chính đã cover room_id, ta cần thêm index cho user_id để tìm các phòng 1 user đang/đã thuê
CREATE NONCLUSTERED INDEX IX_RoomTenant_UserID ON RoomTenant(user_id);

-- Lưu trạng thái đã đóng trước khi mời lại cùng người thuê.
CREATE TABLE RoomTenantHistory (
    history_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RoomTenantHistory PRIMARY KEY,
    room_id UNIQUEIDENTIFIER NOT NULL,
    user_id UNIQUEIDENTIFIER NOT NULL,
    hired_at DATETIME2 NOT NULL,
    left_at DATETIME2 NULL,
    status VARCHAR(20) NOT NULL CONSTRAINT CK_RoomTenantHistory_Status CHECK (status IN ('Ended', 'Cancelled')),
    archived_at DATETIME2 NOT NULL CONSTRAINT DF_RoomTenantHistory_ArchivedAt DEFAULT SYSDATETIME(),
    CONSTRAINT FK_RoomTenantHistory_RoomTenant FOREIGN KEY (room_id, user_id)
        REFERENCES RoomTenant(room_id, user_id) ON DELETE CASCADE
);
CREATE NONCLUSTERED INDEX IX_RoomTenantHistory_RoomUser_ArchivedAt
    ON RoomTenantHistory(room_id, user_id, archived_at DESC);

-- Chỉ user đang hoặc đã từng thuê phòng mới được đánh giá phòng đó.
-- FK này được thêm sau RoomTenant vì RoomRating được tạo trước RoomTenant trong script.
ALTER TABLE RoomRating
ADD CONSTRAINT FK_RoomRating_RoomTenant
FOREIGN KEY (room_id, user_id)
REFERENCES RoomTenant(room_id, user_id);

-- Tạo bảng RoomAmenity
CREATE TABLE RoomAmenity (
    room_id UNIQUEIDENTIFIER NOT NULL,
    amenity_id UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY (room_id, amenity_id),
    FOREIGN KEY (room_id) REFERENCES Room(room_id),
    FOREIGN KEY (amenity_id) REFERENCES Amenity(amenity_id)
);
-- Khóa chính cover room_id, thêm index cho amenity_id nếu cần lọc "những phòng có máy lạnh..."
CREATE NONCLUSTERED INDEX IX_RoomAmenity_AmenityID ON RoomAmenity(amenity_id);

-- Tạo bảng RoomImage
CREATE TABLE RoomImage (
    room_image_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    room_id UNIQUEIDENTIFIER NOT NULL,
    image_url VARCHAR(MAX) NOT NULL,
    FOREIGN KEY (room_id) REFERENCES Room(room_id)
);
-- Load ảnh của phòng
CREATE NONCLUSTERED INDEX IX_RoomImage_RoomID ON RoomImage(room_id);

-- Tạo bảng Conversation
CREATE TABLE Conversation (
    conversation_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    created_at DATETIME DEFAULT GETDATE()
);

-- Tạo bảng Message
CREATE TABLE Message (
    message_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    conversation_id UNIQUEIDENTIFIER NOT NULL,
    sender UNIQUEIDENTIFIER NOT NULL,
    receiver UNIQUEIDENTIFIER NOT NULL,
    content NVARCHAR(MAX),
    chat_at DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (conversation_id) REFERENCES Conversation(conversation_id),
    FOREIGN KEY (sender) REFERENCES [User](user_id),
    FOREIGN KEY (receiver) REFERENCES [User](user_id)
);
-- Tải tin nhắn của một cuộc hội thoại (Rất hay dùng)
CREATE NONCLUSTERED INDEX IX_Message_ConversationID ON Message(conversation_id);

-- Vai trò bắt buộc để API cho phép đăng ký và phân quyền.
INSERT INTO Role (role_name) VALUES ('Tenant'), ('Landlord'), ('Admin');
