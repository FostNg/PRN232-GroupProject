param(
    [string]$BaseUrl = 'http://127.0.0.1:5041',
    [string]$SqlServer = 'HUY\SQLEXPRESS',
    [string]$Database = 'RoomRum_db'
)

$ErrorActionPreference = 'Stop'
$ownerId = [guid]::Empty
$tenantId = [guid]::Empty
$roomId = [guid]::Empty
$conversationId = [guid]::Empty

function CallApi([string]$method, [string]$path, $body, [string]$token = '') {
    $args = @{ Method = $method; Uri = "$BaseUrl$path" }
    if ($token) { $args.Headers = @{ Authorization = "Bearer $token" } }
    if ($null -ne $body) {
        $args.ContentType = 'application/json'
        $args.Body = $body | ConvertTo-Json -Depth 8
    }
    return Invoke-RestMethod @args
}

try {
    $tag = [guid]::NewGuid().ToString('N')
    $owner = CallApi POST '/api/auth/register' @{name='Verify Owner';email="owner-$tag@example.invalid";password='VerifyPass123!';role='Landlord'}
    $ownerId = [guid]$owner.data.userId
    $tenant = CallApi POST '/api/auth/register' @{name='Verify Tenant';email="tenant-$tag@example.invalid";password='VerifyPass123!';role='Tenant'}
    $tenantId = [guid]$tenant.data.userId
    $ownerToken = $owner.data.token
    $tenantToken = $tenant.data.token

    $room = CallApi POST '/api/rooms' @{title='Verify Room';status='Available';price=1000000;area=20;imageUrls=@('https://example.invalid/verify.jpg')} $ownerToken
    $roomId = [guid]$room.data.roomId
    $detail = CallApi GET "/api/rooms/$roomId" $null
    if ($detail.data.title -ne 'Verify Room' -or @($detail.data.images).Count -ne 1) { throw 'Room detail failed.' }

    $favorite = CallApi POST "/api/favorites/$roomId" $null $tenantToken
    $date = (Get-Date).AddDays(1).ToString('yyyy-MM-dd')
    $appointment = CallApi POST '/api/appointments' @{roomId=$roomId;appointmentDate=$date;appointmentTime='15:00:00'} $tenantToken
    $confirmed = CallApi PATCH "/api/appointments/$($appointment.data.appointmentId)/status" @{status='Confirmed'} $ownerToken
    $tenancy = CallApi POST "/api/room-tenants/$roomId" @{userId=$tenantId} $ownerToken
    CallApi PATCH "/api/room-tenants/$roomId/accept" $null $tenantToken | Out-Null
    $rating = CallApi PUT "/api/rooms/$roomId/ratings/mine" @{rating=5;comment='Verified'} $tenantToken
    $sharing = CallApi POST '/api/sharings' @{roomId=$roomId;title='Verify Sharing';quantity=1;sharingFee=500000} $tenantToken
    $repair = CallApi POST '/api/repairs' @{roomId=$roomId;description='Verify repair';priority='Normal'} $tenantToken
    $report = CallApi POST '/api/reports' @{roomId=$roomId;reason='Verify report'} $tenantToken
    $message = CallApi POST '/api/chat/messages' @{receiver=$ownerId;content='Verify message'} $tenantToken
    $conversationId = [guid]$message.data.conversationId
    $messages = CallApi GET "/api/chat/conversations/$conversationId/messages" $null $ownerToken
    if (@($messages.data).Count -ne 1) { throw 'Chat read failed.' }
    "PASS room, favorite, appointment, tenancy, rating, sharing, repair, report, chat"
}
finally {
    $query = @"
DELETE FROM Message WHERE sender IN ('$ownerId','$tenantId') OR receiver IN ('$ownerId','$tenantId');
DELETE FROM Conversation WHERE conversation_id = '$conversationId';
DELETE FROM RoomRating WHERE room_id = '$roomId';
DELETE FROM Repairing WHERE room_id = '$roomId';
DELETE FROM Report WHERE room_id = '$roomId';
DELETE FROM Sharing WHERE room_id = '$roomId';
DELETE FROM Appointment WHERE room_id = '$roomId';
DELETE FROM Favorite WHERE room_id = '$roomId';
DELETE FROM RoomTenant WHERE room_id = '$roomId';
DELETE FROM RoomImage WHERE room_id = '$roomId';
DELETE FROM RoomAmenity WHERE room_id = '$roomId';
DELETE FROM Room WHERE room_id = '$roomId';
DELETE FROM [User] WHERE user_id IN ('$ownerId','$tenantId');
"@
    $cleanupOutput = & sqlcmd -I -S $SqlServer -d $Database -E -b -Q $query 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Smoke-test cleanup failed: $cleanupOutput" }
}
