# Ghi chú thay đổi

## 28/10/2026 — Sửa lỗi build sau khi merge code

### Nguyên nhân

Sau merge `ddf3cec`, project BLL đồng thời chứa bộ service cũ ở namespace
`RoomRum.BLL` và bộ service mới ở `RoomRum.BLL.Services.*`. Các tên như
`IAdminService`, `IAuthService`, `ApiOutcome` và nhiều response DTO bị trùng tên
giữa hai bộ code.

Các `using` của service mới ban đầu nằm ngoài namespace. Khi tìm một tên kiểu
chưa ghi đầy đủ, C# tìm trong namespace cha trước khi xét các `using` ở phạm vi
ngoài đó. Vì vậy, `Services/Implementations/AuthService.cs` nhận nhầm
`RoomRum.BLL.IAuthService`, trong khi tham số lại dùng DTO của bộ code mới.
Interface cũ còn yêu cầu `VerifyEmail`, `SendVerificationEmail` và overload
`Register` có file, nên phát sinh thêm lỗi thiếu phương thức.

Tương tự, `AdminService` và các service khác nhận nhầm interface hoặc response.
Hai kiểu cùng tên, cùng trường nhưng thuộc namespace khác nhau vẫn là hai kiểu
khác nhau trong C#. Build thực tế trước khi sửa báo **22 lỗi CS0535**.

### Cách sửa và các file thay đổi

Đưa các `using` vào bên trong namespace bằng cách đặt chúng sau dòng khai báo
file-scoped `namespace`. Nhờ đó, service mới ưu tiên đúng
`Services.Interfaces`, `DTOs.Requests` và `DTOs.Responses` trước khi tìm trong
namespace cha. Không thay đổi thân các phương thức nghiệp vụ.

Thay đổi header của 13 cặp interface/implementation trong
`PRN232-RoomRum/BLL/Services/Interfaces/` và
`PRN232-RoomRum/BLL/Services/Implementations/`:

| Interface | Implementation |
| --- | --- |
| IAdminService.cs | AdminService.cs |
| IAppointmentService.cs | AppointmentService.cs |
| IAuthService.cs | AuthService.cs |
| IChatService.cs | ChatService.cs |
| IFavoriteService.cs | FavoriteService.cs |
| ILocationSearchService.cs | LocationSearchService.cs |
| IRatingService.cs | RatingService.cs |
| IRepairService.cs | RepairService.cs |
| IReportService.cs | ReportService.cs |
| IRoomImageService.cs | RoomImageService.cs |
| IRoomService.cs | RoomService.cs |
| IRoomTenantService.cs | RoomTenantService.cs |
| ISharingService.cs | SharingService.cs |

Project API mới `PRN232-RoomRum/RoomRum` chưa có `appsettings.json` trên máy.
Đã tạo file local này từ cấu hình local đang có của `RoomRum.API` để chạy với
SQL Server `THUAN`, database `RoomRum_db`. File được Git ignore; không ghi
connection string, mật khẩu hoặc JWT key vào tài liệu này.

### Kết quả kiểm tra

- Solution mới `RoomRum/RoomRum.slnx`: build Debug và Release thành công,
  **0 lỗi, 0 cảnh báo**.
- Solution cũ `RoomRum.API.slnx`: build Release thành công,
  **0 lỗi, 0 cảnh báo**.
- API mới khởi chạy được ở Development, cổng 5041; DI không báo lỗi.
- Swagger UI và OpenAPI JSON: HTTP 200; có các route auth/admin.
- `GET /api/rooms?page=1&pageSize=1`: HTTP 200, đọc SQL thành công.
- Đăng ký với role `Admin`: HTTP 400 đúng validation; không tạo tài khoản.
- Gọi `GET /api/admin/users` chưa đăng nhập: HTTP 401.
- Thử thông tin đăng nhập test đã dùng trước đây: HTTP 404. SQL xác nhận
  `thuanroomrum@gmail.com` vẫn là Tenant/Active; tài khoản
  `hieuthuan822@gmail.com` không có trong database hiện tại. Không xác nhận
  đăng nhập thành công với mật khẩu cũ, không đặt lại mật khẩu hay tạo lại user.
- Không sửa dữ liệu SQL, không gửi email trong lần kiểm tra này.

### Cách chạy lại

Từ thư mục gốc repository:

```powershell
dotnet build .\PRN232-RoomRum\RoomRum\RoomRum.slnx
dotnet run --project .\PRN232-RoomRum\RoomRum\RoomRum.csproj --launch-profile RoomRum
```

Mở `http://localhost:5041/swagger`. Nếu API đang chạy sẵn ở cổng này, dừng
instance đó trước khi khởi chạy instance mới. Trong Visual Studio, mở
`RoomRum/RoomRum.slnx` và chọn project `RoomRum` làm startup project.

Hai startup project `RoomRum` và `RoomRum.API` vẫn dùng hai bộ nghiệp vụ riêng.
Bản sửa này xử lý phân giải kiểu và build; không chuyển chức năng xác thực
Gmail từ `RoomRum.API` sang `RoomRum`.
