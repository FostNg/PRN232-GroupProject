param(
    [string]$BaseUrl = 'http://localhost:5041',
    [string]$SqlServer = 'HUY\SQLEXPRESS',
    [string]$Database = 'RoomRum_db'
)

$ErrorActionPreference = 'Stop'
$ids = @{ owner = [guid]::Empty; tenant = [guid]::Empty; admin = [guid]::Empty;
    subject = [guid]::Empty; room = [guid]::Empty; amenity = [guid]::Empty;
    amenity2 = [guid]::Empty; conversation = [guid]::Empty }
$concurrentAmenityName = ''
$checks = [System.Collections.Generic.List[string]]::new()

function CallApi([string]$method, [string]$path, $body, [string]$token, [int]$expected) {
    $arguments = @{ Method = $method; Uri = "$BaseUrl$path"; SkipHttpErrorCheck = $true }
    if ($token) { $arguments.Headers = @{ Authorization = "Bearer $token" } }
    if ($null -ne $body) {
        $arguments.ContentType = 'application/json'
        $arguments.Body = ConvertTo-Json -InputObject $body -Depth 8 -Compress
    }
    $response = Invoke-WebRequest @arguments
    if ([int]$response.StatusCode -ne $expected) {
        throw "$method $path expected $expected, got $($response.StatusCode): $($response.Content)"
    }
    $checks.Add("$method $path => $expected")
    if ($response.Content) {
        $payload = $response.Content | ConvertFrom-Json
        if ($path -notlike '/swagger/*' -and $payload.success -ne ($expected -lt 400)) {
            throw "$method $path returned an inconsistent success envelope."
        }
        return $payload
    }
    if ($expected -ne 204) { throw "$method $path returned an empty response body." }
    return $null
}

function Sql([string]$query) {
    $output = & sqlcmd -I -S $SqlServer -d $Database -E -b -Q $query 2>&1
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $output" }
}

function Check([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function CallRaw([string]$method, [string]$path, [string]$body, [string]$contentType, [int]$expected) {
    $arguments = @{Method=$method;Uri="$BaseUrl$path";SkipHttpErrorCheck=$true;Body=$body;ContentType=$contentType}
    $response = Invoke-WebRequest @arguments
    if ([int]$response.StatusCode -ne $expected) {
        throw "$method $path expected $expected, got $($response.StatusCode): $($response.Content)"
    }
    $payload = $response.Content | ConvertFrom-Json
    if ($payload.success -ne $false) { throw "$method $path did not return an error envelope." }
    $checks.Add("$method $path => $expected")
}

function CheckPreflight([string]$origin, [bool]$allowed) {
    $response = Invoke-WebRequest -Method Options -Uri "$BaseUrl/api/rooms" -SkipHttpErrorCheck -Headers @{
        Origin = $origin
        'Access-Control-Request-Method' = 'GET'
    }
    $actual = $response.Headers['Access-Control-Allow-Origin']
    Check ($response.StatusCode -eq 204 -and (($actual -contains $origin) -eq $allowed)) "Unexpected CORS result for $origin."
    $checks.Add("OPTIONS /api/rooms => 204")
}

function RouteKey([string]$method, [string]$path) {
    $path = ($path -split '\?')[0]
    $path = $path -replace '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}', '{}'
    $path = $path -replace '\{[^}]+\}', '{}'
    return "$method $path"
}

try {
    $swagger = CallApi GET '/swagger/v1/swagger.json' $null '' 200
    if (@($swagger.paths.PSObject.Properties).Count -ne 34) { throw 'Swagger path count changed.' }
    foreach ($path in $swagger.paths.PSObject.Properties) {
        foreach ($operation in $path.Value.PSObject.Properties | Where-Object { $_.Name -in @('get','post','put','patch','delete') }) {
            $successResponses = @($operation.Value.responses.PSObject.Properties | Where-Object {
                $code = 0
                [int]::TryParse($_.Name, [ref]$code) -and $code -ge 200 -and $code -lt 300
            })
            Check ($successResponses.Count -gt 0) "No documented success response for $($operation.Name) $($path.Name)."
            Check ($operation.Value.responses.default.content.'application/json'.schema.'$ref' -eq
                '#/components/schemas/ApiResponseOfObject') "No typed error envelope for $($operation.Name) $($path.Name)."
            Check ($null -ne $operation.Value.responses.'500' -and $null -ne $operation.Value.responses.'503') "Missing server error responses for $($operation.Name) $($path.Name)."
            foreach ($response in $successResponses) {
                if ($response.Name -eq '204') { continue }
                Check ($null -ne $response.Value.content.'application/json' -and
                    $null -eq $response.Value.content.'application/octet-stream') "Incorrect Swagger response media type for $($operation.Name) $($path.Name)."
                Check ($response.Value.content.'application/json'.schema.'$ref' -and
                    $response.Value.content.'application/json'.schema.'$ref' -notmatch 'ApiResponseOfObject$') "Swagger response is still untyped for $($operation.Name) $($path.Name)."
            }
        }
    }
    Check ($swagger.paths.'/api/rooms'.get.responses.'200'.content.'application/json'.schema.'$ref' -eq
        '#/components/schemas/ApiResponseOfRoomSearchResponse') 'Room search Swagger schema is not typed.'
    Check ($swagger.paths.'/api/auth/login'.post.responses.'200'.content.'application/json'.schema.'$ref' -eq
        '#/components/schemas/ApiResponseOfAuthSessionResponse') 'Authentication Swagger schema is not typed.'
    Check ($null -ne $swagger.paths.'/api/auth/login'.post.responses.'429' -and
        $null -ne $swagger.paths.'/api/auth/me'.get.responses.'401' -and
        $null -ne $swagger.paths.'/api/auth/me'.get.responses.'403') 'Authentication errors are missing from Swagger.'
    CheckPreflight 'http://localhost:3000' $true
    CheckPreflight 'https://not-allowed.example' $false
    CallRaw POST '/api/auth/register' '{' 'application/json' 400
    CallRaw POST '/api/auth/register' 'invalid' 'text/plain' 415
    CallApi GET '/api/no-such-route' $null '' 404 | Out-Null
    CallApi DELETE '/api/rooms/amenities' $null '' 405 | Out-Null
    CallApi GET '/api/rooms/not-a-guid' $null '' 404 | Out-Null
    CallApi GET '/api/rooms' $null '' 200 | Out-Null
    CallApi GET '/api/rooms/amenities' $null '' 200 | Out-Null
    CallApi GET '/api/rooms/mine' $null '' 401 | Out-Null
    CallApi GET '/api/rooms/mine' $null 'invalid-token' 401 | Out-Null
    CallApi POST '/api/auth/register' @{name='Invalid';email='invalid@example.invalid';password='long-pass';role='Admin'} '' 400 | Out-Null
    CallApi POST '/api/auth/register' @{name='Invalid';email='non-ascii-é@example.invalid';password='long-pass';role='Tenant'} '' 400 | Out-Null
    CallApi POST '/api/auth/register' @{name='Invalid';email='short-password@example.invalid';password='123';role='Tenant'} '' 400 | Out-Null
    CallApi POST '/api/auth/register' @{name='Invalid';email='missing-at-sign';password='VerifyPass123!';role='Tenant'} '' 400 | Out-Null
    CallApi POST '/api/auth/register' @{name='Invalid';email='bad@@example.invalid';password='VerifyPass123!';role='Tenant'} '' 400 | Out-Null

    $tag = [guid]::NewGuid().ToString('N')
    $owner = CallApi POST '/api/auth/register' @{name='Verify Owner';email="owner-$tag@example.invalid";password='VerifyPass123!';role='Landlord'} '' 201
    $ids.owner = [guid]$owner.data.userId
    $tenant = CallApi POST '/api/auth/register' @{name='Verify Tenant';email="tenant-$tag@example.invalid";password='VerifyPass123!';role='Tenant'} '' 201
    $ids.tenant = [guid]$tenant.data.userId
    $admin = CallApi POST '/api/auth/register' @{name='Verify Admin';email="admin-$tag@example.invalid";password='VerifyPass123!';role='Tenant'} '' 201
    $ids.admin = [guid]$admin.data.userId
    $subject = CallApi POST '/api/auth/register' @{name='Verify Subject';email="subject-$tag@example.invalid";password='VerifyPass123!';role='Tenant'} '' 201
    $ids.subject = [guid]$subject.data.userId
    $ownerToken = $owner.data.token
    $tenantToken = $tenant.data.token
    $subjectToken = $subject.data.token
    CallApi POST '/api/auth/register' @{name='Invalid phone';email="long-phone-$tag@example.invalid";password='VerifyPass123!';role='Tenant';phone=('1' * 21)} '' 400 | Out-Null
    CallApi POST '/api/auth/register' @{name='Invalid phone';email="unicode-phone-$tag@example.invalid";password='VerifyPass123!';role='Tenant';phone='１２３'} '' 400 | Out-Null
    CallApi POST '/api/auth/register' @{name='Duplicate';email="owner-$tag@example.invalid";password='VerifyPass123!';role='Landlord'} '' 409 | Out-Null
    CallApi POST '/api/auth/login' @{email="owner-$tag@example.invalid";password='wrong'} '' 401 | Out-Null
    CallApi POST '/api/auth/login' @{email="owner-$tag@example.invalid";password='VerifyPass123!'} '' 200 | Out-Null
    CallApi GET '/api/auth/me' $null $ownerToken 200 | Out-Null

    Sql "UPDATE dbo.[User] SET role_id = (SELECT role_id FROM dbo.Role WHERE role_name='Admin') WHERE user_id = '$($ids.admin)'"
    CallApi GET '/api/auth/me' $null $admin.data.token 401 | Out-Null
    $adminLogin = CallApi POST '/api/auth/login' @{email="admin-$tag@example.invalid";password='VerifyPass123!'} '' 200
    $adminToken = $adminLogin.data.token
    CallApi GET '/api/admin/users' $null $tenantToken 403 | Out-Null
    CallApi POST '/api/admin/amenities' @{name='Forbidden'} $tenantToken 403 | Out-Null
    CallApi GET '/api/admin/users' $null $adminToken 200 | Out-Null
    CallApi PATCH "/api/admin/users/$($ids.admin)/status" @{status='Locked'} $adminToken 400 | Out-Null
    CallApi PATCH "/api/admin/users/$($ids.admin)/role" @{role='Tenant'} $adminToken 400 | Out-Null
    $amenity = CallApi POST '/api/admin/amenities' @{name="Test Amenity $tag"} $adminToken 201
    $ids.amenity = [guid]$amenity.data.amenityId
    CallApi POST '/api/admin/amenities' @{name="Test Amenity $tag"} $adminToken 200 | Out-Null
    $concurrentAmenityName = "Concurrent Amenity $tag"
    $amenityBurst = 1..6 | ForEach-Object -Parallel {
        $body = @{name=$using:concurrentAmenityName} | ConvertTo-Json -Compress
        $response = Invoke-WebRequest -Method Post -Uri "$using:BaseUrl/api/admin/amenities" -Headers @{
            Authorization="Bearer $using:adminToken"
        } -ContentType 'application/json' -Body $body -SkipHttpErrorCheck
        [pscustomobject]@{Status=[int]$response.StatusCode; Id=($response.Content | ConvertFrom-Json).data.amenityId}
    } -ThrottleLimit 6
    Check (@($amenityBurst | Where-Object { $_.Status -notin @(200,201) }).Count -eq 0 -and
        @($amenityBurst | Where-Object { $_.Status -eq 201 }).Count -eq 1 -and
        @($amenityBurst | Select-Object -ExpandProperty Id -Unique).Count -eq 1) 'Concurrent amenity creation returned inconsistent results.'
    foreach ($result in $amenityBurst) { $checks.Add("POST /api/admin/amenities => $($result.Status)") }
    CallApi GET '/api/rooms/amenities' $null '' 200 | Out-Null
    CallApi POST '/api/rooms' @{title=('R' * 256);price=1000000;area=20} $ownerToken 400 | Out-Null
    CallApi POST '/api/rooms' @{title='Invalid type';type=('X' * 51);price=1000000;area=20} $ownerToken 400 | Out-Null
    CallApi POST '/api/rooms' @{title='Invalid status';status=('X' * 51);price=1000000;area=20} $ownerToken 400 | Out-Null
    CallApi POST '/api/rooms' @{title='Invalid status';status='Đã thuê';price=1000000;area=20} $ownerToken 400 | Out-Null
    CallApi POST '/api/rooms' @{title='Invalid status';status='Unreviewed';price=1000000;area=20} $ownerToken 400 | Out-Null
    CallApi POST '/api/rooms' @{title='Invalid area';price=1000000;area=100000000} $ownerToken 400 | Out-Null
    CallApi POST '/api/rooms' @{title='Invalid price';price=10000000000000000;area=20} $ownerToken 400 | Out-Null
    CallApi POST '/api/rooms' @{title='Rounded price';price=1000.001;area=20} $ownerToken 400 | Out-Null
    CallApi POST '/api/rooms' @{title='Rounded area';price=1000;area=20.001} $ownerToken 400 | Out-Null
    CallApi POST '/api/rooms' @{title='Unknown amenity';price=1000000;area=20;amenityIds=@([guid]::NewGuid())} $ownerToken 400 | Out-Null
    CallApi POST '/api/rooms' @{title='Invalid image';price=1000000;area=20;imageUrls=@('file:///private.jpg')} $ownerToken 400 | Out-Null
    CallApi GET '/api/rooms?page=2147483647&pageSize=100' $null '' 400 | Out-Null
    CallApi GET '/api/rooms?minPrice=1.001' $null '' 400 | Out-Null
    CallApi GET '/api/rooms?minPrice=200&maxPrice=100' $null '' 400 | Out-Null

    $draft = CallApi POST '/api/rooms' @{title="Draft Room $tag";status='Draft';price=1000000;area=20} $ownerToken 201
    CallApi GET "/api/rooms/$($draft.data.roomId)" $null '' 404 | Out-Null
    $draftSearch = CallApi GET "/api/rooms?q=Draft%20Room%20$tag" $null '' 200
    Check ($draftSearch.data.total -eq 0) 'Draft room appeared in public search.'
    CallApi PUT "/api/rooms/$($draft.data.roomId)" @{title="Edited Draft Room $tag";price=1000000;area=20} $ownerToken 200 | Out-Null
    CallApi GET "/api/rooms/$($draft.data.roomId)" $null '' 404 | Out-Null
    CallApi POST "/api/favorites/$($draft.data.roomId)" $null $tenantToken 404 | Out-Null
    CallApi GET "/api/rooms/$($draft.data.roomId)/ratings" $null '' 404 | Out-Null
    CallApi POST '/api/appointments' @{roomId=$draft.data.roomId;appointmentDate=(Get-Date).AddDays(2).ToString('yyyy-MM-dd');appointmentTime='15:00:00'} $tenantToken 404 | Out-Null

    $room = CallApi POST '/api/rooms' @{title="Test Room $tag";type='Private';status='Available';price=1000000.25;area=20.75;
        amenityIds=@($ids.amenity);imageUrls=@('https://example.invalid/one.jpg')} $ownerToken 201
    $ids.room = [guid]$room.data.roomId
    $search = CallApi GET "/api/rooms?amenityId=$($ids.amenity)&q=Test%20Room" $null '' 200
    Check ($search.data.total -eq 1 -and $search.data.items[0].roomId -eq "$($ids.room)") 'Room search did not return the matching room.'
    $noMatch = CallApi GET "/api/rooms?amenityId=$($ids.amenity)&minPrice=2000000" $null '' 200
    Check ($noMatch.data.total -eq 0) 'Price search did not exclude the room.'
    $detail = CallApi GET "/api/rooms/$($ids.room)" $null '' 200
    if (@($detail.data.amenities).Count -ne 1 -or @($detail.data.images).Count -ne 1) { throw 'Room relations missing.' }
    Check ($detail.data.price -eq 1000000.25 -and $detail.data.area -eq 20.75) 'Two-decimal room values did not round trip exactly.'
    CallApi GET '/api/rooms/mine' $null $ownerToken 200 | Out-Null
    CallApi POST '/api/rooms' @{title='Forbidden';price=1000000;area=20} $tenantToken 403 | Out-Null
    CallApi PUT "/api/rooms/$($ids.room)" @{title=('X' * 256);price=1000000;area=20} $ownerToken 400 | Out-Null
    CallApi PUT "/api/rooms/$($ids.room)" @{title='Unknown amenity';price=1000000;area=20;amenityIds=@([guid]::NewGuid())} $ownerToken 400 | Out-Null
    CallApi PUT "/api/rooms/$($ids.room)" @{title="Updated Room $tag";status='Available';price=1100000;area=22;
        amenityIds=@($ids.amenity);imageUrls=@('https://example.invalid/two.jpg')} $tenantToken 403 | Out-Null
    CallApi PUT "/api/rooms/$($ids.room)" @{title="Updated Room $tag";status='Available';price=1100000;area=22;
        amenityIds=@($ids.amenity);imageUrls=@('https://example.invalid/two.jpg')} $ownerToken 200 | Out-Null
    $updated = CallApi GET "/api/rooms/$($ids.room)" $null '' 200
    if ($updated.data.title -ne "Updated Room $tag" -or @($updated.data.images).Count -ne 1 -or
        $updated.data.images[0].imageUrl -ne 'https://example.invalid/two.jpg') { throw 'Room update did not persist.' }
    $amenity2Name = "Điều hòa $tag"
    $amenity2 = CallApi POST '/api/admin/amenities' @{name=$amenity2Name} $adminToken 201
    $ids.amenity2 = [guid]$amenity2.data.amenityId
    $amenitiesAfterCreate = CallApi GET '/api/rooms/amenities' $null '' 200
    Check (@($amenitiesAfterCreate.data | Where-Object { $_.amenityId -eq "$($ids.amenity2)" -and $_.amenityName -eq $amenity2Name }).Count -eq 1) 'Vietnamese amenity name was not stored losslessly.'
    CallApi PUT "/api/rooms/$($ids.room)" @{title="Updated Room $tag";price=1100000;area=22;
        amenityIds=@($ids.amenity,$ids.amenity2);imageUrls=@('https://example.invalid/a.jpg','https://example.invalid/b.jpg')} $ownerToken 200 | Out-Null
    $relations = CallApi GET "/api/rooms/$($ids.room)" $null '' 200
    Check (@($relations.data.amenities).Count -eq 2 -and @($relations.data.images).Count -eq 2) 'Adding multiple room relations failed.'
    $imageIds = @{}; foreach ($image in $relations.data.images) { $imageIds[$image.imageUrl] = $image.roomImageId }
    CallApi PUT "/api/rooms/$($ids.room)" @{title="Updated Room $tag";price=1100000;area=22;
        amenityIds=@($ids.amenity,$ids.amenity2);imageUrls=@('https://example.invalid/a.jpg','https://example.invalid/b.jpg')} $ownerToken 200 | Out-Null
    $sameImages = CallApi GET "/api/rooms/$($ids.room)" $null '' 200
    Check (@($sameImages.data.images).Count -eq 2 -and
        @($sameImages.data.images | Where-Object { $imageIds[$_.imageUrl] -eq $_.roomImageId }).Count -eq 2) 'Unchanged image URLs lost their IDs.'
    CallApi PUT "/api/rooms/$($ids.room)" @{title="Updated Room $tag";price=1100000;area=22;
        amenityIds=@($ids.amenity,$ids.amenity2);imageUrls=@('https://example.invalid/a.jpg','https://example.invalid/c.jpg')} $ownerToken 200 | Out-Null
    $partialImages = CallApi GET "/api/rooms/$($ids.room)" $null '' 200
    Check (@($partialImages.data.images | Where-Object {
        $_.imageUrl -eq 'https://example.invalid/a.jpg' -and $_.roomImageId -eq $imageIds['https://example.invalid/a.jpg']
    }).Count -eq 1 -and @($partialImages.data.images | Where-Object {
        $_.imageUrl -eq 'https://example.invalid/c.jpg' -and $_.roomImageId -ne $imageIds['https://example.invalid/b.jpg']
    }).Count -eq 1) 'Partially changed image URLs did not preserve only existing IDs.'
    CallApi PUT "/api/rooms/$($ids.room)" @{title="Updated Room $tag";price=1100000;area=22;
        amenityIds=@($ids.amenity2);imageUrls=@()} $ownerToken 200 | Out-Null
    $relations = CallApi GET "/api/rooms/$($ids.room)" $null '' 200
    Check (@($relations.data.amenities).Count -eq 1 -and @($relations.data.images).Count -eq 0) 'Removing room relations failed.'
    CallApi PUT "/api/rooms/$($ids.room)" @{title="Updated Room $tag";type='Phòng riêng';price=1100000;area=22;
        amenityIds=@($ids.amenity);imageUrls=@('https://münich.example/ảnh.jpg')} $ownerToken 200 | Out-Null
    $unicodeUrl = CallApi GET "/api/rooms/$($ids.room)" $null '' 200
    Check ($unicodeUrl.data.images[0].imageUrl -eq 'https://xn--mnich-kva.example/%E1%BA%A3nh.jpg') "Unicode image URL was stored as '$($unicodeUrl.data.images[0].imageUrl)'."
    Check ($unicodeUrl.data.type -eq 'Phòng riêng') 'Vietnamese room type was not stored losslessly.'

    CallApi POST "/api/favorites/$($ids.room)" $null $tenantToken 201 | Out-Null
    CallApi POST "/api/favorites/$($ids.room)" $null $tenantToken 200 | Out-Null
    $favorites = CallApi GET '/api/favorites' $null $tenantToken 200
    Check (@($favorites.data | Where-Object { $_.roomId -eq "$($ids.room)" }).Count -eq 1) 'Duplicate favorite was stored.'
    CallApi DELETE "/api/favorites/$($ids.room)" $null $tenantToken 204 | Out-Null
    $favoritePath = "/api/favorites/$($ids.room)"
    $favoriteBurst = 1..8 | ForEach-Object -Parallel {
        $result = Invoke-WebRequest -Method Post -Uri "$using:BaseUrl$using:favoritePath" -Headers @{Authorization="Bearer $using:tenantToken"} -SkipHttpErrorCheck
        [int]$result.StatusCode
    } -ThrottleLimit 8
    Check (@($favoriteBurst | Where-Object { $_ -notin @(200,201) }).Count -eq 0) "Concurrent favorite requests returned unexpected statuses: $($favoriteBurst -join ',')."
    $favorites = CallApi GET '/api/favorites' $null $tenantToken 200
    Check (@($favorites.data | Where-Object { $_.roomId -eq "$($ids.room)" }).Count -eq 1) 'Concurrent favorite requests stored duplicates.'
    CallApi DELETE "/api/favorites/$($ids.room)" $null $tenantToken 204 | Out-Null

    $date = (Get-Date).AddDays(2).ToString('yyyy-MM-dd')
    $appointment = CallApi POST '/api/appointments' @{roomId=$ids.room;appointmentDate=$date;appointmentTime='15:00:00'} $tenantToken 201
    CallApi POST '/api/appointments' @{roomId=$ids.room;appointmentDate=$date;appointmentTime='15:00:00'} $ownerToken 400 | Out-Null
    CallApi POST '/api/appointments' @{roomId=$ids.room;appointmentDate='2020-01-01';appointmentTime='15:00:00'} $tenantToken 400 | Out-Null
    CallApi GET '/api/appointments/mine' $null $tenantToken 200 | Out-Null
    CallApi GET '/api/appointments/mine' $null $ownerToken 200 | Out-Null
    CallApi PATCH "/api/appointments/$($appointment.data.appointmentId)/status" @{status='Confirmed'} $tenantToken 409 | Out-Null
    CallApi PATCH "/api/appointments/$($appointment.data.appointmentId)/status" @{status='Confirmed'} $subjectToken 409 | Out-Null
    CallApi PATCH "/api/appointments/$($appointment.data.appointmentId)/status" @{status='Confirmed'} $ownerToken 200 | Out-Null
    CallApi PATCH "/api/appointments/$($appointment.data.appointmentId)/status" @{status='Completed'} $ownerToken 200 | Out-Null
    CallApi PATCH "/api/appointments/$($appointment.data.appointmentId)/status" @{status='Cancelled'} $tenantToken 409 | Out-Null
    $raceAppointment = CallApi POST '/api/appointments' @{roomId=$ids.room;appointmentDate=$date;appointmentTime='16:00:00'} $tenantToken 201
    $racePath = "/api/appointments/$($raceAppointment.data.appointmentId)/status"
    $raceResults = @('Confirmed','Rejected') | ForEach-Object -Parallel {
        $body = @{status=$_} | ConvertTo-Json -Compress
        $response = Invoke-WebRequest -Method Patch -Uri "$using:BaseUrl$using:racePath" -Headers @{
            Authorization="Bearer $using:ownerToken"
        } -ContentType 'application/json' -Body $body -SkipHttpErrorCheck
        [int]$response.StatusCode
    } -ThrottleLimit 2
    Check (@($raceResults | Where-Object { $_ -eq 200 }).Count -eq 1 -and
        @($raceResults | Where-Object { $_ -eq 409 }).Count -eq 1) "Concurrent appointment updates returned $($raceResults -join ',')."
    foreach ($status in $raceResults) { $checks.Add("PATCH $racePath => $status") }

    CallApi POST "/api/room-tenants/$($ids.room)" @{userId=$ids.tenant} $ownerToken 201 | Out-Null
    $pendingHistory = CallApi GET "/api/room-tenants/$($ids.room)/$($ids.tenant)/history" $null $tenantToken 200
    Check ($pendingHistory.data.total -eq 1 -and $pendingHistory.data.items[0].status -eq 'Pending') 'Pending invitation is missing from history.'
    CallApi GET "/api/room-tenants/$($ids.room)/$($ids.tenant)/history" $null $subjectToken 403 | Out-Null
    CallApi GET "/api/room-tenants/$($ids.room)/$($ids.tenant)/history?page=0" $null $ownerToken 400 | Out-Null
    CallApi POST "/api/room-tenants/$($ids.room)" @{userId=$ids.tenant} $ownerToken 409 | Out-Null
    CallApi POST "/api/room-tenants/$($ids.room)" @{userId=$ids.subject} $tenantToken 403 | Out-Null
    CallApi GET '/api/room-tenants/mine' $null $tenantToken 200 | Out-Null
    CallApi PATCH "/api/room-tenants/$($ids.room)/accept" $null $ownerToken 409 | Out-Null
    CallApi PATCH "/api/room-tenants/$($ids.room)/accept" $null $subjectToken 409 | Out-Null
    CallApi PUT "/api/rooms/$($ids.room)/ratings/mine" @{rating=5;comment='Pending invitation'} $tenantToken 403 | Out-Null
    CallApi POST '/api/sharings' @{roomId=$ids.room;title='Pending invitation';quantity=1} $tenantToken 403 | Out-Null
    CallApi PATCH "/api/room-tenants/$($ids.room)/accept" $null $tenantToken 200 | Out-Null
    CallApi PATCH "/api/room-tenants/$($ids.room)/accept" $null $tenantToken 409 | Out-Null
    CallApi POST "/api/room-tenants/$($ids.room)" @{userId=$ids.subject} $ownerToken 201 | Out-Null
    CallApi PATCH "/api/room-tenants/$($ids.room)/$($ids.subject)/end" $null $subjectToken 200 | Out-Null
    CallApi PATCH "/api/room-tenants/$($ids.room)/accept" $null $subjectToken 409 | Out-Null
    CallApi PUT "/api/rooms/$($ids.room)/ratings/mine" @{rating=5;comment='Not a tenant'} $subjectToken 403 | Out-Null
    $repeatInvitePath = "/api/room-tenants/$($ids.room)"
    $repeatInviteBody = @{userId=$ids.subject} | ConvertTo-Json -Compress
    $repeatInviteStatuses = 1..2 | ForEach-Object -Parallel {
        $response = Invoke-WebRequest -Method Post -Uri "$using:BaseUrl$using:repeatInvitePath" `
            -Headers @{Authorization="Bearer $using:ownerToken"} -ContentType 'application/json' `
            -Body $using:repeatInviteBody -SkipHttpErrorCheck
        [int]$response.StatusCode
    } -ThrottleLimit 2
    Check (@($repeatInviteStatuses | Where-Object { $_ -eq 201 }).Count -eq 1 -and
        @($repeatInviteStatuses | Where-Object { $_ -eq 409 }).Count -eq 1) "Concurrent repeat invitations returned $($repeatInviteStatuses -join ',')."
    foreach ($status in $repeatInviteStatuses) { $checks.Add("POST $repeatInvitePath => $status") }
    $repeatHistory = CallApi GET "/api/room-tenants/$($ids.room)/$($ids.subject)/history" $null $ownerToken 200
    Check ($repeatHistory.data.total -eq 2 -and $repeatHistory.data.items[0].status -eq 'Cancelled' -and
        $repeatHistory.data.items[0].isCurrent -eq $false -and $repeatHistory.data.items[1].status -eq 'Pending') 'Cancelled invitation was not archived.'
    $historyPage1 = CallApi GET "/api/room-tenants/$($ids.room)/$($ids.subject)/history?page=1&pageSize=1" $null $ownerToken 200
    $historyPage2 = CallApi GET "/api/room-tenants/$($ids.room)/$($ids.subject)/history?page=2&pageSize=1" $null $ownerToken 200
    Check ($historyPage1.data.items[0].status -eq 'Cancelled' -and
        $historyPage2.data.items[0].status -eq 'Pending') 'Tenancy history pagination returned the wrong period.'
    CallApi PATCH "/api/room-tenants/$($ids.room)/accept" $null $subjectToken 200 | Out-Null
    CallApi PATCH "/api/room-tenants/$($ids.room)/$($ids.subject)/end" $null $subjectToken 200 | Out-Null
    CallApi PUT "/api/rooms/$($ids.room)/ratings/mine" @{rating=6;comment='Invalid'} $tenantToken 400 | Out-Null
    CallApi PUT "/api/rooms/$($ids.room)/ratings/mine" @{rating=5;comment='Verified'} $tenantToken 200 | Out-Null
    CallApi PUT "/api/rooms/$($ids.room)/ratings/mine" @{rating=4;comment='Updated'} $tenantToken 200 | Out-Null
    CallApi GET "/api/rooms/$($ids.room)/ratings" $null '' 200 | Out-Null
    $sharing = CallApi POST '/api/sharings' @{roomId=$ids.room;title='Verified sharing';gender='Nữ';quantity=1;sharingFee=500000.25} $tenantToken 201
    CallApi POST '/api/sharings' @{roomId=$ids.room;title='Not allowed';quantity=1;sharingFee=500000} $subjectToken 403 | Out-Null
    CallApi POST '/api/sharings' @{roomId=$ids.room;title=('S' * 256);quantity=1;sharingFee=500000} $tenantToken 400 | Out-Null
    CallApi POST '/api/sharings' @{roomId=$ids.room;title='Invalid gender';gender=('X' * 21);quantity=1;sharingFee=500000} $tenantToken 400 | Out-Null
    CallApi POST '/api/sharings' @{roomId=$ids.room;title='Rounded fee';quantity=1;sharingFee=1000.001} $tenantToken 400 | Out-Null
    $sharings = CallApi GET "/api/sharings?roomId=$($ids.room)" $null '' 200
    Check (@($sharings.data | Where-Object { $_.sharingId -eq $sharing.data.sharingId -and $_.gender -eq 'Nữ' -and $_.sharingFee -eq 500000.25 }).Count -eq 1) 'Sharing gender or two-decimal fee was not stored losslessly.'
    CallApi DELETE "/api/sharings/$($sharing.data.sharingId)" $null $tenantToken 204 | Out-Null
    CallApi DELETE "/api/sharings/$($sharing.data.sharingId)" $null $tenantToken 404 | Out-Null

    $repair = CallApi POST '/api/repairs' @{roomId=$ids.room;description='Verified repair';priority='Khẩn cấp'} $tenantToken 201
    CallApi POST '/api/repairs' @{roomId=$ids.room;description='Not tenant'} $subjectToken 403 | Out-Null
    CallApi POST '/api/repairs' @{roomId=$ids.room;priority=('X' * 51);description='Invalid priority'} $tenantToken 400 | Out-Null
    $repairs = CallApi GET '/api/repairs/mine' $null $ownerToken 200
    Check (@($repairs.data | Where-Object { $_.maintenanceId -eq $repair.data.maintenanceId -and $_.priority -eq 'Khẩn cấp' }).Count -eq 1) 'Vietnamese repair priority was not stored losslessly.'
    CallApi PATCH "/api/repairs/$($repair.data.maintenanceId)" @{status='Completed';response='Forbidden'} $tenantToken 403 | Out-Null
    CallApi PATCH "/api/repairs/$($repair.data.maintenanceId)" @{status='Wrong'} $ownerToken 400 | Out-Null
    CallApi PATCH "/api/repairs/$($repair.data.maintenanceId)" @{status='Completed';response='Fixed'} $ownerToken 200 | Out-Null
    $report = CallApi POST '/api/reports' @{roomId=$ids.room;reason='Verified report'} $tenantToken 201
    CallApi GET '/api/reports/mine' $null $tenantToken 200 | Out-Null
    CallApi GET '/api/reports/mine' $null $adminToken 200 | Out-Null
    CallApi PATCH "/api/reports/$($report.data.reportId)/status" @{status='Resolved'} $adminToken 200 | Out-Null
    CallApi PATCH "/api/reports/$($report.data.reportId)/status" @{status='Rejected'} $tenantToken 403 | Out-Null

    $message = CallApi POST '/api/chat/messages' @{receiver=$ids.owner;content='Verified message'} $tenantToken 201
    CallApi POST '/api/chat/messages' @{receiver=$ids.tenant;content='Self message'} $tenantToken 400 | Out-Null
    CallApi POST '/api/chat/messages' @{receiver=[guid]::NewGuid();content='Missing receiver'} $tenantToken 404 | Out-Null
    CallApi POST '/api/chat/messages' @{conversationId=[guid]::NewGuid();receiver=$ids.owner;content='Missing conversation'} $tenantToken 404 | Out-Null
    $ids.conversation = [guid]$message.data.conversationId
    CallApi GET '/api/chat/conversations' $null $ownerToken 200 | Out-Null
    CallApi GET "/api/chat/conversations/$($ids.conversation)/messages" $null $subjectToken 404 | Out-Null
    CallApi GET "/api/chat/conversations/$($ids.conversation)/messages" $null $ownerToken 200 | Out-Null
    CallApi POST '/api/chat/messages' @{conversationId=$ids.conversation;receiver=$ids.subject;content='Forbidden'} $tenantToken 403 | Out-Null
    CallApi POST '/api/chat/messages' @{conversationId=$ids.conversation;receiver=$ids.tenant;content='Reply'} $ownerToken 201 | Out-Null
    Sql "INSERT INTO Message (message_id, conversation_id, sender, receiver, content, chat_at) VALUES (NEWID(), '$($ids.conversation)', '$($ids.subject)', '$($ids.admin)', N'Private legacy message', DATEADD(day, 1, GETDATE()));"
    $ownerMessages = CallApi GET "/api/chat/conversations/$($ids.conversation)/messages" $null $ownerToken 200
    Check (@($ownerMessages.data).Count -eq 2 -and
        @($ownerMessages.data | Where-Object { $_.content -eq 'Private legacy message' }).Count -eq 0) 'Conversation exposed a message between other users.'
    $ownerConversations = CallApi GET '/api/chat/conversations' $null $ownerToken 200
    Check (@($ownerConversations.data | Where-Object { $_.conversationId -eq $ids.conversation -and
        $_.lastMessage.content -eq 'Reply' }).Count -eq 1) 'Conversation summary exposed another pair''s latest message.'
    $subjectMessages = CallApi GET "/api/chat/conversations/$($ids.conversation)/messages" $null $subjectToken 200
    Check (@($subjectMessages.data).Count -eq 1 -and @($subjectMessages.data)[0].content -eq 'Private legacy message') 'Legacy participant could not read their own message.'

    $pagedLists = @(
        @{Path='/api/admin/users'; Token=$adminToken},
        @{Path='/api/appointments/mine'; Token=$tenantToken},
        @{Path='/api/favorites'; Token=$tenantToken},
        @{Path='/api/chat/conversations'; Token=$ownerToken},
        @{Path="/api/chat/conversations/$($ids.conversation)/messages"; Token=$ownerToken},
        @{Path='/api/sharings'; Token=''},
        @{Path="/api/rooms/$($ids.room)/ratings"; Token=''},
        @{Path='/api/room-tenants/mine'; Token=$ownerToken},
        @{Path='/api/rooms/mine'; Token=$ownerToken},
        @{Path='/api/rooms/amenities'; Token=''},
        @{Path='/api/reports/mine'; Token=$tenantToken},
        @{Path='/api/repairs/mine'; Token=$tenantToken}
    )
    foreach ($list in $pagedLists) {
        CallApi GET "$($list.Path)?page=0" $null $list.Token 400 | Out-Null
        CallApi GET "$($list.Path)?pageSize=101" $null $list.Token 400 | Out-Null
        $one = CallApi GET "$($list.Path)?page=1&pageSize=1" $null $list.Token 200
        Check (@($one.data).Count -le 1) "Page limit was ignored for $($list.Path)."
    }
    $usersPage1 = CallApi GET '/api/admin/users?page=1&pageSize=1' $null $adminToken 200
    $usersPage2 = CallApi GET '/api/admin/users?page=2&pageSize=1' $null $adminToken 200
    Check ($usersPage1.data[0].userId -ne $usersPage2.data[0].userId) 'Admin user pages overlap.'
    $amenitiesPage1 = CallApi GET '/api/rooms/amenities?page=1&pageSize=1' $null '' 200
    $amenitiesPage2 = CallApi GET '/api/rooms/amenities?page=2&pageSize=1' $null '' 200
    Check ($amenitiesPage1.data[0].amenityId -ne $amenitiesPage2.data[0].amenityId) 'Amenity pages overlap.'

    CallApi PATCH "/api/admin/users/$($ids.subject)/status" @{status='Locked'} $adminToken 200 | Out-Null
    CallApi GET '/api/auth/me' $null $subjectToken 401 | Out-Null
    CallApi PATCH "/api/admin/users/$($ids.subject)/status" @{status='Active'} $adminToken 200 | Out-Null
    Sql "UPDATE [User] SET status = NULL WHERE user_id = '$($ids.subject)';"
    CallApi GET '/api/auth/me' $null $subjectToken 401 | Out-Null
    CallApi POST '/api/auth/login' @{email="subject-$tag@example.invalid";password='VerifyPass123!'} '' 401 | Out-Null
    Sql "UPDATE [User] SET status = 'Active' WHERE user_id = '$($ids.subject)';"
    CallApi PATCH "/api/admin/users/$($ids.subject)/role" @{role='Landlord'} $adminToken 200 | Out-Null
    CallApi GET '/api/auth/me' $null $subjectToken 401 | Out-Null
    CallApi POST '/api/auth/login' @{email="subject-$tag@example.invalid";password='VerifyPass123!'} '' 200 | Out-Null

    $loginStatuses = 1..35 | ForEach-Object {
        $response = Invoke-WebRequest -Method Post -Uri "$BaseUrl/api/auth/login" -ContentType 'application/json' `
            -Body (@{email='rate-limit-probe@example.invalid';password='wrong-password'} | ConvertTo-Json -Compress) -SkipHttpErrorCheck
        Check (($response.Content | ConvertFrom-Json).success -eq $false) 'Rate-limited login lacked an error envelope.'
        $checks.Add("POST /api/auth/login => $([int]$response.StatusCode)")
        [int]$response.StatusCode
    }
    Check (@($loginStatuses | Where-Object { $_ -eq 429 }).Count -gt 0 -and
        @($loginStatuses | Where-Object { $_ -notin @(401,429) }).Count -eq 0) "Authentication rate limit did not reject the login burst: $($loginStatuses -join ',')."

    CallApi PATCH "/api/room-tenants/$($ids.room)/$($ids.tenant)/end" $null $ownerToken 200 | Out-Null
    CallApi PATCH "/api/room-tenants/$($ids.room)/$($ids.tenant)/end" $null $ownerToken 409 | Out-Null
    CallApi POST '/api/repairs' @{roomId=$ids.room;description='Tenancy ended'} $tenantToken 403 | Out-Null
    CallApi POST '/api/sharings' @{roomId=$ids.room;title='Tenancy ended';quantity=1} $tenantToken 403 | Out-Null
    CallApi PUT "/api/rooms/$($ids.room)/ratings/mine" @{rating=5;comment='Former tenant'} $tenantToken 200 | Out-Null
    CallApi POST "/api/room-tenants/$($ids.room)" @{userId=$ids.tenant} $ownerToken 201 | Out-Null
    $endedHistory = CallApi GET "/api/room-tenants/$($ids.room)/$($ids.tenant)/history" $null $tenantToken 200
    Check ($endedHistory.data.total -eq 2 -and $endedHistory.data.items[0].status -eq 'Ended' -and
        $endedHistory.data.items[1].status -eq 'Pending') 'Ended tenancy was not archived.'
    CallApi PATCH "/api/room-tenants/$($ids.room)/accept" $null $tenantToken 200 | Out-Null
    CallApi DELETE "/api/rooms/$($ids.room)" $null $ownerToken 204 | Out-Null
    CallApi GET "/api/rooms/$($ids.room)" $null '' 404 | Out-Null
    CallApi GET "/api/rooms/$($ids.room)/ratings" $null '' 404 | Out-Null
    CallApi POST "/api/favorites/$($ids.room)" $null $tenantToken 404 | Out-Null
    $hiddenSearch = CallApi GET "/api/rooms?q=$tag" $null '' 200
    Check ($hiddenSearch.data.total -eq 0) 'Hidden room remained searchable.'
    $expectedOperations = @($swagger.paths.PSObject.Properties | ForEach-Object {
        $path = $_.Name
        $_.Value.PSObject.Properties | Where-Object { $_.Name -in @('get','post','put','patch','delete') } |
            ForEach-Object { RouteKey $_.Name.ToUpperInvariant() $path }
    } | Sort-Object -Unique)
    $observedOperations = @($checks | ForEach-Object {
        $operation = ($_ -split ' => ')[0]
        $parts = $operation -split ' ', 2
        RouteKey $parts[0] $parts[1]
    } | Sort-Object -Unique)
    $missingOperations = @($expectedOperations | Where-Object { $_ -notin $observedOperations })
    Check ($missingOperations.Count -eq 0) "Swagger operations not exercised: $($missingOperations -join ', ')"
    "PASS $($checks.Count) HTTP checks across $($expectedOperations.Count) Swagger operations and $(@($swagger.paths.PSObject.Properties).Count) paths."
}
finally {
    $query = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @RoomIds TABLE (id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @RoomIds (id)
SELECT room_id FROM Room WHERE room_id = '$($ids.room)' OR user_id = '$($ids.owner)';
DELETE FROM Message WHERE sender IN ('$($ids.owner)','$($ids.tenant)','$($ids.admin)','$($ids.subject)') OR receiver IN ('$($ids.owner)','$($ids.tenant)','$($ids.admin)','$($ids.subject)');
DELETE FROM Conversation WHERE conversation_id = '$($ids.conversation)';
DELETE FROM RoomRating WHERE room_id IN (SELECT id FROM @RoomIds);
DELETE FROM Repairing WHERE room_id IN (SELECT id FROM @RoomIds);
DELETE FROM Report WHERE room_id IN (SELECT id FROM @RoomIds);
DELETE FROM Sharing WHERE room_id IN (SELECT id FROM @RoomIds);
DELETE FROM Appointment WHERE room_id IN (SELECT id FROM @RoomIds);
DELETE FROM Favorite WHERE room_id IN (SELECT id FROM @RoomIds);
DELETE FROM RoomTenant WHERE room_id IN (SELECT id FROM @RoomIds);
DELETE FROM RoomImage WHERE room_id IN (SELECT id FROM @RoomIds);
DELETE FROM RoomAmenity WHERE room_id IN (SELECT id FROM @RoomIds);
DELETE FROM Room WHERE room_id IN (SELECT id FROM @RoomIds);
DELETE FROM Amenity WHERE amenity_id = '$($ids.amenity)';
DELETE FROM Amenity WHERE amenity_id = '$($ids.amenity2)';
DELETE FROM Amenity WHERE amenity_name = '$concurrentAmenityName';
DELETE FROM [User] WHERE user_id IN ('$($ids.owner)','$($ids.tenant)','$($ids.admin)','$($ids.subject)');
COMMIT TRANSACTION;
"@
    Sql $query
}
