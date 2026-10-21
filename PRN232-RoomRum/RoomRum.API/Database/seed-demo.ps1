param(
    [string]$ApiBaseUrl = 'http://localhost:5041',
    [string]$SqlServer = 'HUY\SQLEXPRESS',
    [string]$Database = 'RoomRum_db'
)

$ErrorActionPreference = 'Stop'
$ApiBaseUrl = $ApiBaseUrl.TrimEnd('/')
$credentialsPath = Join-Path $PSScriptRoot 'demo-credentials.local.json'

function New-Password {
    [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)) + 'aA!'
}

function Invoke-RoomRumApi {
    param([string]$Method, [string]$Path, [object]$Body, [string]$Token)
    $arguments = @{ Method = $Method; Uri = "$ApiBaseUrl/api$Path"; TimeoutSec = 30 }
    if ($Token) { $arguments.Headers = @{ Authorization = "Bearer $Token" } }
    if ($null -ne $Body) {
        $arguments.ContentType = 'application/json; charset=utf-8'
        $arguments.Body = ConvertTo-Json -InputObject $Body -Depth 12 -Compress
    }
    $response = Invoke-RestMethod @arguments
    if ($response -and $response.success -eq $false) { throw "API failed: $Method $Path - $($response.message)" }
    return $response.data
}

if (Test-Path -LiteralPath $credentialsPath) {
    $credentials = Get-Content -LiteralPath $credentialsPath -Raw -Encoding UTF8 | ConvertFrom-Json
} else {
    $credentials = [pscustomobject]@{
        tenant = [pscustomobject]@{ name = 'Nguyễn Minh Anh'; email = 'khach.demo@roomrum.test'; password = (New-Password); role = 'Tenant' }
        landlord = [pscustomobject]@{ name = 'Trần Hoàng Nam'; email = 'chutro.demo@roomrum.test'; password = (New-Password); role = 'Landlord' }
        admin = [pscustomobject]@{ name = 'Quản trị RoomRum'; email = 'admin.demo@roomrum.test'; password = (New-Password); role = 'Tenant' }
    }
    $credentials | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $credentialsPath -Encoding UTF8
}

$accounts = @{}
foreach ($entry in @('tenant', 'landlord', 'admin')) {
    $account = $credentials.$entry
    try {
        $accounts[$entry] = Invoke-RoomRumApi -Method 'POST' -Path '/auth/register' -Body @{
            name = $account.name; email = $account.email; password = $account.password
            phone = $null; role = $account.role
        }
    } catch {
        if ($_.Exception.Message -notmatch '409|Email already exists') { throw }
        $accounts[$entry] = Invoke-RoomRumApi -Method 'POST' -Path '/auth/login' -Body @{
            email = $account.email; password = $account.password
        }
    }
}

# Public registration intentionally cannot assign Admin. Promote only the fixed demo account.
$promotion = "SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON; UPDATE dbo.[User] SET role_id = (SELECT role_id FROM dbo.[Role] WHERE role_name = 'Admin') WHERE email = 'admin.demo@roomrum.test'; IF @@ROWCOUNT <> 1 THROW 50001, 'Demo admin account missing', 1;"
& sqlcmd -S $SqlServer -d $Database -E -C -b -I -Q $promotion | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not promote the demo admin account.' }
$accounts.admin = Invoke-RoomRumApi -Method 'POST' -Path '/auth/login' -Body @{
    email = $credentials.admin.email; password = $credentials.admin.password
}
if ($accounts.admin.role -ne 'Admin') { throw 'Demo admin role was not applied.' }

$amenities = @{}
foreach ($name in @('Wi-Fi', 'Điều hòa', 'Máy giặt', 'Bếp riêng', 'Ban công', 'Chỗ để xe', 'Camera an ninh', 'Nội thất cơ bản')) {
    $item = Invoke-RoomRumApi -Method 'POST' -Path '/admin/amenities' -Body @{ name = $name } -Token $accounts.admin.token
    $amenities[$name] = $item.amenityId
}

$existingRooms = @(Invoke-RoomRumApi -Method 'GET' -Path '/rooms/mine?page=1&pageSize=50' -Token $accounts.landlord.token)
$roomSpecs = @(
    [pscustomobject]@{
        key='studio'; title='[Demo] Studio ban công ngập nắng tại Thảo Điền'; type='Studio';
        address='Đường Nguyễn Văn Hưởng, phường Thảo Điền, TP. Thủ Đức, TP. Hồ Chí Minh';
        area=28; price=6200000; deposit=6200000; electricityFee=3800; waterFee=100000; serviceFee=180000;
        status='Available'; image='thao-dien-studio.png'; latitude=10.8033; longitude=106.7315;
        description='DỮ LIỆU MẪU — Không phải phòng đang cho thuê thật. Studio 28 m² nhiều ánh sáng, có ban công, bếp riêng và góc làm việc. Giá tham khảo 6,2 triệu đồng/tháng; điện 3.800 đồng/kWh, nước 100.000 đồng/người/tháng, dịch vụ 180.000 đồng/tháng. Có thể xem phòng theo lịch hẹn.';
        rule='Giữ yên tĩnh sau 22:00; không hút thuốc trong phòng; trao đổi trước khi nuôi thú cưng.';
        amenityNames=@('Wi-Fi','Điều hòa','Bếp riêng','Ban công','Chỗ để xe','Camera an ninh')
    },
    [pscustomobject]@{
        key='student'; title='[Demo] Phòng riêng sáng thoáng gần HUTECH Bình Thạnh'; type='Phòng trọ';
        address='Đường Điện Biên Phủ, phường 25, quận Bình Thạnh, TP. Hồ Chí Minh';
        area=22; price=3900000; deposit=3900000; electricityFee=3800; waterFee=90000; serviceFee=120000;
        status='Available'; image='binh-thanh-private-room.png'; latitude=10.8012; longitude=106.7166;
        description='DỮ LIỆU MẪU — Không phải phòng đang cho thuê thật. Phòng riêng 22 m² có cửa sổ, bàn học, tủ đồ và vệ sinh khép kín; phù hợp một người đi học hoặc đi làm. Giá tham khảo 3,9 triệu đồng/tháng; chi phí điện, nước và dịch vụ được tách rõ.';
        rule='Tối đa 2 người ở; giữ vệ sinh khu vực chung; không gây ồn sau 22:00.';
        amenityNames=@('Wi-Fi','Chỗ để xe','Camera an ninh','Nội thất cơ bản')
    },
    [pscustomobject]@{
        key='apartment'; title='[Demo] Căn hộ mini đầy đủ tiện nghi tại Phú Nhuận'; type='Căn hộ mini';
        address='Đường Phan Xích Long, phường 2, quận Phú Nhuận, TP. Hồ Chí Minh';
        area=31; price=7400000; deposit=7400000; electricityFee=3900; waterFee=110000; serviceFee=200000;
        status='Available'; image='phu-nhuan-mini-apartment.png'; latitude=10.8008; longitude=106.6832;
        description='DỮ LIỆU MẪU — Không phải phòng đang cho thuê thật. Căn hộ mini 31 m² bố trí khu ngủ, sofa, bếp và bàn ăn; cửa sổ rộng, phù hợp người đi làm hoặc cặp đôi. Giá tham khảo 7,4 triệu đồng/tháng, cọc một tháng.';
        rule='Tối đa 2 người ở; không hút thuốc; báo trước khi có khách ở qua đêm.';
        amenityNames=@('Wi-Fi','Điều hòa','Máy giặt','Bếp riêng','Chỗ để xe','Camera an ninh')
    },
    [pscustomobject]@{
        key='rented'; title='[Demo] Phòng nội thất cơ bản yên tĩnh ở Tân Bình'; type='Phòng trọ';
        address='Đường Cộng Hòa, phường 13, quận Tân Bình, TP. Hồ Chí Minh';
        area=25; price=4800000; deposit=4800000; electricityFee=3800; waterFee=100000; serviceFee=150000;
        status='Rented'; image='tan-binh-furnished-room.png'; latitude=10.8015; longitude=106.6502;
        description='DỮ LIỆU MẪU — Không phải phòng đang cho thuê thật. Phòng 25 m² có giường, bàn làm việc, tủ và khu bếp nhỏ. Bản ghi được đánh dấu đã thuê để thử quy trình người thuê và yêu cầu sửa chữa.';
        rule='Giữ yên tĩnh sau 22:00; không hút thuốc trong phòng.';
        amenityNames=@('Wi-Fi','Điều hòa','Bếp riêng','Chỗ để xe','Nội thất cơ bản')
    }
)

$rooms = @{}
foreach ($spec in $roomSpecs) {
    $match = @($existingRooms | Where-Object { $_.title -eq $spec.title })
    if ($match.Count -gt 0) { $rooms[$spec.key] = $match[0].roomId; continue }
    $body = @{
        type=$spec.type; title=$spec.title; address=$spec.address; area=$spec.area;
        electricityFee=$spec.electricityFee; waterFee=$spec.waterFee; serviceFee=$spec.serviceFee;
        deposit=$spec.deposit; price=$spec.price; description=$spec.description; rule=$spec.rule;
        status=$spec.status; latitude=$spec.latitude; longitude=$spec.longitude;
        amenityIds=@($spec.amenityNames | ForEach-Object { $amenities[$_] });
        imageUrls=@("$ApiBaseUrl/demo/rooms/$($spec.image)")
    }
    $created = Invoke-RoomRumApi -Method 'POST' -Path '/rooms' -Body $body -Token $accounts.landlord.token
    $rooms[$spec.key] = $created.roomId
}

# Existing demo rows created before coordinate support get approximate area points once.
foreach ($spec in $roomSpecs) {
    $roomId = [guid]$rooms[$spec.key]
    $latitude = $spec.latitude.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $longitude = $spec.longitude.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $locationSql = "SET QUOTED_IDENTIFIER ON; UPDATE dbo.Room SET latitude=$latitude, longitude=$longitude WHERE room_id='$roomId' AND latitude IS NULL AND longitude IS NULL;"
    & sqlcmd -S $SqlServer -d $Database -E -C -b -I -Q $locationSql | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not locate demo room $roomId." }
}

foreach ($key in @('studio','apartment')) {
    Invoke-RoomRumApi -Method 'POST' -Path "/favorites/$($rooms[$key])" -Token $accounts.tenant.token | Out-Null
}

$appointments = @(Invoke-RoomRumApi -Method 'GET' -Path '/appointments/mine?page=1&pageSize=50' -Token $accounts.tenant.token)
if (-not @($appointments | Where-Object { $_.roomId -eq $rooms.student }).Count) {
    $date = (Get-Date).AddDays(3).ToString('yyyy-MM-dd')
    $appointment = Invoke-RoomRumApi -Method 'POST' -Path '/appointments' -Body @{
        roomId=$rooms.student; appointmentDate=$date; appointmentTime='15:00:00';
        description='Em muốn xem phòng và hỏi thêm về chỗ để xe. Nếu 15 giờ chưa thuận tiện, anh báo em khung giờ khác nhé.'
    } -Token $accounts.tenant.token
    Invoke-RoomRumApi -Method 'PATCH' -Path "/appointments/$($appointment.appointmentId)/status" -Body @{ status='Confirmed' } -Token $accounts.landlord.token | Out-Null
}

$conversations = @(Invoke-RoomRumApi -Method 'GET' -Path '/chat/conversations?page=1&pageSize=50' -Token $accounts.tenant.token)
if ($conversations.Count -eq 0) {
    $first = Invoke-RoomRumApi -Method 'POST' -Path '/chat/messages' -Body @{
        conversationId=$null; receiver=$accounts.landlord.userId;
        content='Chào anh Nam, em thấy phòng gần HUTECH khá phù hợp. Phòng có chỗ để xe máy và giờ giấc ra vào thế nào ạ?'
    } -Token $accounts.tenant.token
    Invoke-RoomRumApi -Method 'POST' -Path '/chat/messages' -Body @{
        conversationId=$first.conversationId; receiver=$accounts.tenant.userId;
        content='Chào Minh Anh, tòa nhà có chỗ để xe máy và camera ở khu chung. Giờ ra vào linh hoạt, sau 22 giờ mình chỉ cần giữ yên tĩnh nhé.'
    } -Token $accounts.landlord.token | Out-Null
    Invoke-RoomRumApi -Method 'POST' -Path '/chat/messages' -Body @{
        conversationId=$first.conversationId; receiver=$accounts.landlord.userId;
        content='Dạ cảm ơn anh. Em đã đặt lịch xem phòng lúc 15 giờ, mong anh xác nhận giúp em.'
    } -Token $accounts.tenant.token | Out-Null
    Invoke-RoomRumApi -Method 'POST' -Path '/chat/messages' -Body @{
        conversationId=$first.conversationId; receiver=$accounts.tenant.userId;
        content='Anh đã xác nhận lịch trên RoomRum rồi. Hẹn gặp em lúc 15 giờ tại khu vực phòng nhé.'
    } -Token $accounts.landlord.token | Out-Null
}

$tenancies = @(Invoke-RoomRumApi -Method 'GET' -Path '/room-tenants/mine?page=1&pageSize=50' -Token $accounts.tenant.token)
if (-not @($tenancies | Where-Object { $_.roomId -eq $rooms.rented }).Count) {
    Invoke-RoomRumApi -Method 'POST' -Path "/room-tenants/$($rooms.rented)/invite-by-email" -Body @{
        email=$credentials.tenant.email
    } -Token $accounts.landlord.token | Out-Null
    Invoke-RoomRumApi -Method 'PATCH' -Path "/room-tenants/$($rooms.rented)/accept" -Token $accounts.tenant.token | Out-Null
}

$repairs = @(Invoke-RoomRumApi -Method 'GET' -Path '/repairs/mine?page=1&pageSize=50' -Token $accounts.tenant.token)
if (-not @($repairs | Where-Object { $_.roomId -eq $rooms.rented }).Count) {
    $repair = Invoke-RoomRumApi -Method 'POST' -Path '/repairs' -Body @{
        roomId=$rooms.rented; priority='Medium'; repairItems='Vòi nước bồn rửa';
        description='Vòi nước khu bếp bị rò nhẹ khi khóa. Nhờ anh kiểm tra và thay gioăng nếu cần.';
        expectingDate=(Get-Date).AddDays(5).ToString('yyyy-MM-dd')
    } -Token $accounts.tenant.token
    Invoke-RoomRumApi -Method 'PATCH' -Path "/repairs/$($repair.maintenanceId)" -Body @{
        status='InProgress'; response='Anh đã nhận yêu cầu và sẽ sắp xếp thợ kiểm tra trong 2 ngày tới.'
    } -Token $accounts.landlord.token | Out-Null
}

[pscustomobject]@{
    message='Demo data is ready'; accounts=@(
        @{ role='Tenant'; name=$credentials.tenant.name; email=$credentials.tenant.email; password=$credentials.tenant.password },
        @{ role='Landlord'; name=$credentials.landlord.name; email=$credentials.landlord.email; password=$credentials.landlord.password },
        @{ role='Admin'; name=$credentials.admin.name; email=$credentials.admin.email; password=$credentials.admin.password }
    ); rooms=$rooms; credentialsFile=$credentialsPath
} | ConvertTo-Json -Depth 6
