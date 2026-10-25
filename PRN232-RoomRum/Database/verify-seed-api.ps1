param([string]$BaseUrl = 'http://localhost:5041')

$ErrorActionPreference = 'Stop'
$checks = [System.Collections.Generic.List[string]]::new()
$room1 = '20000000-0000-0000-0000-000000000001'
$room2 = '20000000-0000-0000-0000-000000000002'
$room3 = '20000000-0000-0000-0000-000000000003'
$room4 = '20000000-0000-0000-0000-000000000004'
$room5 = '20000000-0000-0000-0000-000000000005'
$room6 = '20000000-0000-0000-0000-000000000006'
$tenant1 = '10000000-0000-0000-0000-000000000002'
$tenant2 = '10000000-0000-0000-0000-000000000003'
$conversation1 = '40000000-0000-0000-0000-000000000001'

function Check([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    $checks.Add($message)
}

function CallApi([string]$path, [string]$token = '', [int]$expected = 200) {
    $args = @{ Method='GET'; Uri="$BaseUrl$path"; SkipHttpErrorCheck=$true }
    if ($token) { $args.Headers = @{ Authorization="Bearer $token" } }
    $response = Invoke-WebRequest @args
    Check ([int]$response.StatusCode -eq $expected) "GET $path => $expected"
    if ($response.Content) { return ($response.Content | ConvertFrom-Json) }
    return $null
}

function Login([string]$email, [string]$role) {
    $response = Invoke-WebRequest -Method POST -Uri "$BaseUrl/api/auth/login" -ContentType 'application/json' `
        -Body (@{email=$email;password='RoomRum@123'} | ConvertTo-Json -Compress) -SkipHttpErrorCheck
    Check ([int]$response.StatusCode -eq 200) "Seed login $email"
    $session = $response.Content | ConvertFrom-Json
    Check ($session.data.role -eq $role -and -not [string]::IsNullOrWhiteSpace($session.data.token)) "Seed role $email"
    return $session.data.token
}

$admin = Login 'admin@roomrum.example' 'Admin'
$landlord = Login 'landlord@roomrum.example' 'Landlord'
$landlord2 = Login 'landlord2@roomrum.example' 'Landlord'
$t1 = Login 'tenant1@roomrum.example' 'Tenant'
$t2 = Login 'tenant2@roomrum.example' 'Tenant'

$me = CallApi '/api/auth/me' $t1
Check ($me.data.userId -eq $tenant1) 'Tenant profile resolves to seed ID'
CallApi '/api/admin/users' $t1 403 | Out-Null
CallApi '/api/rooms/mine' '' 401 | Out-Null

$search = CallApi '/api/rooms?page=1&pageSize=100'
Check (@($search.data.items | Where-Object roomId -eq $room4).Count -eq 1) 'Available seed room appears in search'
Check (@($search.data.items | Where-Object roomId -in @($room1, $room2)).Count -eq 0) 'Rented seed rooms stay out of available search'
Check (@($search.data.items | Where-Object roomId -eq $room6).Count -eq 0) 'Pending seed room stays out of search'
$r3 = CallApi "/api/rooms/$room3"
Check ($r3.data.status -eq 'Rented') 'Rented seed room detail is visible'
CallApi "/api/rooms/$room6" '' 404 | Out-Null
$r5 = CallApi "/api/rooms/$room5"
Check ($r5.data.price -eq 2800000 -and $r5.data.images.Count -eq 1) 'Seed room detail includes price and image'
$amenities = CallApi '/api/rooms/amenities?pageSize=100'
Check (@($amenities.data | Where-Object amenityName -eq 'Wifi').Count -eq 1) 'Seed amenities are available'
$pendingRooms = CallApi '/api/admin/rooms/pending' $admin
Check (@($pendingRooms.data | Where-Object roomId -eq $room6).Count -eq 1) 'Pending seed room reaches admin review'
CallApi '/api/admin/rooms/pending' $landlord 403 | Out-Null
$ownedRooms = CallApi '/api/rooms/mine' $landlord
Check (@($ownedRooms.data | Where-Object roomId -eq $room1).Count -eq 1) 'Rented seed room appears in owner list'
$ownedRooms2 = CallApi '/api/rooms/mine' $landlord2
Check (@($ownedRooms2.data | Where-Object roomId -eq $room2).Count -eq 1) 'Second landlord sees rented seed room'
Check (@($ownedRooms2.data | Where-Object roomId -eq $room6).Count -eq 0) 'Pending seed room stays out of owner list'

$favorites = CallApi '/api/favorites' $t1
Check (@($favorites.data | Where-Object roomId -eq $room4).Count -eq 1) 'Seed favorite belongs to tenant'
$appointments = CallApi '/api/appointments/mine' $t1
Check (@($appointments.data | Where-Object roomId -eq $room5).Count -ge 1) 'Seed appointment belongs to tenant'
$tenancies = CallApi '/api/room-tenants/mine' $t2
Check (@($tenancies.data | Where-Object { $_.roomId -eq $room1 -and $_.status -eq 'Active' }).Count -eq 1) 'Seed active tenancy belongs to tenant'
$history = CallApi "/api/room-tenants/$room1/$tenant2/history" $t2
Check ($history.data.total -eq 2) 'Seed tenancy includes current and archived periods'
CallApi "/api/room-tenants/$room1/$tenant2/history" $t1 403 | Out-Null
$ratings = CallApi "/api/rooms/$room1/ratings"
Check (@($ratings.data).Count -eq 2) 'Seed room ratings are visible'
$sharings = CallApi "/api/sharings?roomId=$room1"
Check (@($sharings.data).Count -eq 2) 'Seed sharing posts are visible'
$repairs = CallApi '/api/repairs/mine' $t2
Check (@($repairs.data | Where-Object roomId -eq $room1).Count -ge 1) 'Seed repair requests belong to tenant'
$reports = CallApi '/api/reports/mine' $t1
Check (@($reports.data | Where-Object roomId -eq $room4).Count -ge 1) 'Seed reports belong to tenant'
$pendingReports = CallApi '/api/reports/pending' $admin
Check (@($pendingReports.data | Where-Object roomId -eq $room4).Count -ge 1) 'Seed pending report reaches admin'
$messages = CallApi "/api/chat/conversations/$conversation1/messages" $t1
Check (@($messages.data).Count -eq 2) 'Seed conversation messages are visible to participant'
CallApi "/api/chat/conversations/$conversation1/messages" $t2 404 | Out-Null

$r1 = CallApi "/api/rooms/$room1"
Check ($r1.data.status -eq 'Rented') 'Seed room 1 with one active tenant is Rented'
$r2 = CallApi "/api/rooms/$room2"
Check ($r2.data.status -eq 'Rented') 'Seed room 2 with multiple active tenants is Rented'
"PASS $($checks.Count) seed API checks."
