param([string]$Configuration = 'Release', [int]$Port = 5071)
$ErrorActionPreference = 'Stop'
$apiDirectory = Split-Path $PSScriptRoot -Parent
$assembly = Join-Path $apiDirectory "bin/$Configuration/net10.0/RoomRum.API.dll"
. (Join-Path $PSScriptRoot 'registration-db.ps1')
Initialize-RegistrationDb $apiDirectory
$tag = [guid]::NewGuid().ToString('N')
$prefix = "registration-check-$tag"
$privateDirectory = Join-Path $apiDirectory "App_Data/$prefix"
$mailDirectory = Join-Path $privateDirectory 'maildrop'
$baseUrl = "http://127.0.0.1:$Port"
$password = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
$emails = [System.Collections.Generic.List[string]]::new()
$servers = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$checks = 0
$smtpMock = $null
$testProfile = Join-Path $privateDirectory 'fake-user-profile'

# A loopback-only SMTP receiver verifies the real SMTP transport without contacting Gmail.
if (-not ('RoomRumTestSmtp' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
public sealed class RoomRumTestSmtp : IDisposable
{
    private readonly TcpListener listener;
    private readonly string username;
    private readonly string password;
    private TcpClient client;
    public Task<string> Completion { get; }
    public bool Authenticated { get; private set; }
    public RoomRumTestSmtp(int port, string username, string password)
    {
        this.username = username;
        this.password = password;
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        Completion = Receive();
    }
    private async Task<string> Receive()
    {
        client = await listener.AcceptTcpClientAsync();
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        var body = new StringBuilder();
        await writer.WriteLineAsync("220 localhost test SMTP");
        string line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (line.StartsWith("EHLO") || line.StartsWith("HELO"))
            {
                await writer.WriteLineAsync("250-localhost");
                await writer.WriteLineAsync("250 AUTH LOGIN");
            }
            else if (line.StartsWith("AUTH LOGIN", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string userResponse;
                if (parts.Length >= 3) userResponse = parts[2];
                else { await writer.WriteLineAsync("334 VXNlcm5hbWU6"); userResponse = await reader.ReadLineAsync(); }
                var suppliedUser = Encoding.ASCII.GetString(Convert.FromBase64String(userResponse));
                await writer.WriteLineAsync("334 UGFzc3dvcmQ6");
                var suppliedPassword = Encoding.ASCII.GetString(Convert.FromBase64String(await reader.ReadLineAsync()));
                Authenticated = suppliedUser == username && suppliedPassword == password;
                await writer.WriteLineAsync(Authenticated ? "235 Authenticated" : "535 Authentication failed");
            }
            else if (line.StartsWith("MAIL FROM:") || line.StartsWith("RCPT TO:")) await writer.WriteLineAsync("250 OK");
            else if (line == "DATA")
            {
                await writer.WriteLineAsync("354 End with a single dot");
                while ((line = await reader.ReadLineAsync()) != null && line != ".") body.AppendLine(line);
                await writer.WriteLineAsync("250 Queued locally for test");
            }
            else if (line == "QUIT") { await writer.WriteLineAsync("221 Bye"); break; }
            else await writer.WriteLineAsync("250 OK");
        }
        return body.ToString();
    }
    public void Dispose() { client?.Dispose(); listener.Stop(); }
}
'@
}

function Check([bool]$Condition, [string]$Description) {
    if (-not $Condition) { throw "FAIL: $Description" }
    $script:checks++
    "PASS: $Description" | Write-Host
}
function New-Email([string]$Label) {
    $email = "$prefix-$Label@example.invalid"
    $emails.Add($email)
    return $email
}
function Call([string]$Method, [string]$Path, $Body, [int]$Expected, [string]$Token = '', [string]$Url = $baseUrl, $Form = $null) {
    $arguments = @{ Method=$Method; Uri="$Url$Path"; SkipHttpErrorCheck=$true; TimeoutSec=25 }
    if ($null -ne $Form) { $arguments.Form = $Form }
    elseif ($null -ne $Body) { $arguments.ContentType='application/json'; $arguments.Body=$Body | ConvertTo-Json -Depth 8 }
    if ($Token) { $arguments.Headers=@{Authorization="Bearer $Token"} }
    $response = Invoke-WebRequest @arguments
    if ([int]$response.StatusCode -ne $Expected) { throw "$Method $Path expected $Expected, got $($response.StatusCode): $($response.Content)" }
    return $response.Content | ConvertFrom-Json
}
function Register-Tenant([string]$Email) {
    return (Call POST '/api/auth/register' @{name='Registration check Tenant';email=$Email;password=$password;role='Tenant'} 201).data
}
function Verification-Link([guid]$UserId) {
    foreach ($mail in (Get-ChildItem -LiteralPath $mailDirectory -Filter '*.eml' | Sort-Object LastWriteTimeUtc -Descending)) {
        $body = Get-Content -Raw -LiteralPath $mail.FullName
        $match = [regex]::Match($body, "http://127\.0\.0\.1:$Port/api/auth/verify-email\?userId=$UserId&token=[A-F0-9]{64}")
        if ($match.Success) { return $match.Value }
    }
    throw 'Verification message was not written to the private pickup directory.'
}
function Start-TestApi([int]$ServerPort, [string]$DeliveryMethod, [string]$SmtpHost = '', [int]$SmtpPort = 1, [bool]$EnableSsl = $true, [switch]$UseUserSecrets) {
    $arguments = @('"' + $assembly + '"', "--urls=http://127.0.0.1:$ServerPort", "--Email:DeliveryMethod=$DeliveryMethod",
        '--Email:PickupDirectory="' + $mailDirectory + '"', "--EmailVerification:ApiBaseUrl=http://127.0.0.1:$ServerPort",
        "--Smtp:Host=$SmtpHost", "--Smtp:Port=$SmtpPort", "--Smtp:EnableSsl=$($EnableSsl.ToString().ToLowerInvariant())",
        '--Logging:LogLevel:Microsoft.EntityFrameworkCore=Warning', '--RateLimiting:AuthPermitLimit=100')
    if (-not $UseUserSecrets) { $arguments += @('--Smtp:FromEmail=noreply@roomrum.invalid', '--Smtp:Username=', '--Smtp:Password=') }
    $previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
    $previousAppData = $env:APPDATA
    try {
        $env:ASPNETCORE_ENVIRONMENT='Development'
        $env:DOTNET_ENVIRONMENT='Development'
        $env:APPDATA=$testProfile
        $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList $arguments -WorkingDirectory $apiDirectory -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $privateDirectory "$ServerPort.log") -RedirectStandardError (Join-Path $privateDirectory "$ServerPort.error.log")
        $servers.Add($process)
    } finally {
        $env:ASPNETCORE_ENVIRONMENT=$previousEnvironment
        $env:DOTNET_ENVIRONMENT=$previousDotnetEnvironment
        $env:APPDATA=$previousAppData
    }
    for ($attempt=0; $attempt -lt 30; $attempt++) {
        if ($process.HasExited) { throw "Test API exited; inspect private log for port $ServerPort." }
        try { Invoke-WebRequest -Uri "http://127.0.0.1:$ServerPort/swagger/v1/swagger.json" -TimeoutSec 1 | Out-Null; return }
        catch { Start-Sleep -Milliseconds 300 }
    }
    throw 'Test API did not become ready.'
}

try {
    New-Item -ItemType Directory -Path $privateDirectory -Force | Out-Null
    Start-TestApi $Port 'PickupDirectory'
    $tenantEmail = New-Email 'tenant'
    $tenant = Register-Tenant $tenantEmail
    Check ($tenant.status -eq 'Inactive' -and -not $tenant.token -and $tenant.emailDelivery -eq 'PickupDirectory') 'Tenant remains inactive; no JWT at registration; pickup mode is disclosed'
    $link = Verification-Link $tenant.userId
    $token = [regex]::Match($link, 'token=([A-F0-9]{64})').Groups[1].Value
    $hash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($token)))
    $hashed = Invoke-RegistrationSql "SELECT COUNT(*) FROM dbo.[User] WHERE user_id='$($tenant.userId)' AND DATALENGTH(password)=60 AND email_verification_token_hash=0x$hash AND email_verification_expires_at BETWEEN DATEADD(MINUTE,55,SYSUTCDATETIME()) AND DATEADD(MINUTE,65,SYSUTCDATETIME());"
    Check ($hashed -eq '1') 'Database stores BCrypt password and only a token hash with a 60-minute expiry'
    Call POST '/api/auth/send-verification-email' @{email=$tenantEmail;password='WrongPassword123!'} 401 | Out-Null
    Call POST '/api/auth/send-verification-email' @{email=$tenantEmail;password=$password} 429 | Out-Null
    Check $true 'Resending requires the correct account password and enforces the 60-second cooldown'
    Call POST '/api/auth/login' @{email=$tenantEmail;password=$password} 401 | Out-Null
    Call GET '/api/auth/me' $null 401 | Out-Null
    Call GET "/api/auth/verify-email?userId=$($tenant.userId)&token=$('0'*64)" $null 400 | Out-Null
    Call GET "/api/auth/verify-email?userId=$([guid]::NewGuid())&token=$token" $null 400 | Out-Null
    Check $true 'Inactive login, anonymous access, tampered token and wrong user are rejected'
    $verified = Invoke-WebRequest -Uri $link -SkipHttpErrorCheck
    Check ($verified.StatusCode -eq 200 -and $verified.Headers['Cache-Control'] -contains 'no-store') 'Valid emailed API link activates the Tenant without caching the token'
    Call GET "/api/auth/verify-email?userId=$($tenant.userId)&token=$token" $null 400 | Out-Null
    $consumed = Invoke-RegistrationSql "SELECT COUNT(*) FROM dbo.[User] WHERE user_id='$($tenant.userId)' AND status='Active' AND email_verified_at IS NOT NULL AND email_verification_token_hash IS NULL AND email_verification_expires_at IS NULL;"
    Check ($consumed -eq '1') 'Token is consumed and cannot be replayed'
    $session = (Call POST '/api/auth/login' @{email=$tenantEmail;password=$password} 200).data
    $me = (Call GET '/api/auth/me' $null 200 $session.token).data
    Call POST '/api/rooms' @{title='Must not be created'} 403 $session.token | Out-Null
    Check ($me.email -eq $tenantEmail) 'Activated Tenant can log in and use JWT; Landlord-only API remains forbidden'
    Call POST '/api/auth/register' @{name='Registration check duplicate';email=$tenantEmail;password=$password;role='Tenant'} 409 | Out-Null
    Call POST '/api/auth/register' @{name='Registration check Admin';email=(New-Email 'admin');password=$password;role='Admin'} 400 | Out-Null
    Check $true 'Duplicate email and public Admin registration are rejected'

    $expired = Register-Tenant (New-Email 'expired')
    $expiredLink = Verification-Link $expired.userId
    Invoke-RegistrationSql "UPDATE dbo.[User] SET email_verification_expires_at=DATEADD(MINUTE,-1,SYSUTCDATETIME()) WHERE user_id='$($expired.userId)';" | Out-Null
    Check ((Invoke-WebRequest -Uri $expiredLink -SkipHttpErrorCheck).StatusCode -eq 400) 'Expired token does not activate the user'
    $concurrent = Register-Tenant (New-Email 'concurrent')
    $concurrentLink = Verification-Link $concurrent.userId
    $http = [System.Net.Http.HttpClient]::new()
    try {
        $first = $http.GetAsync($concurrentLink)
        $second = $http.GetAsync($concurrentLink)
        $responses = @($first.GetAwaiter().GetResult(), $second.GetAwaiter().GetResult())
        $codes = @($responses | ForEach-Object { [int]$_.StatusCode } | Sort-Object)
        Check (($codes -join ',') -eq '200,400') 'Concurrent verification consumes the token exactly once'
    } finally { $http.Dispose() }

    $expiredEmail = "$prefix-expired@example.invalid"
    Invoke-RegistrationSql "UPDATE dbo.[User] SET email_verification_sent_at=DATEADD(MINUTE,-2,SYSUTCDATETIME()) WHERE user_id='$($expired.userId)';" | Out-Null
    $resent = (Call POST '/api/auth/send-verification-email' @{email=$expiredEmail;password=$password} 200).data
    $resentLink = Verification-Link $expired.userId
    Check ($resent.emailDelivery -eq 'PickupDirectory' -and $resentLink -ne $expiredLink) 'Expired or existing inactive accounts can receive a fresh verification email'
    Check ((Invoke-WebRequest -Uri $expiredLink -SkipHttpErrorCheck).StatusCode -eq 400) 'Resending invalidates the previous verification token'
    Check ((Invoke-WebRequest -Uri $resentLink -SkipHttpErrorCheck).StatusCode -eq 200) 'The newly emailed link activates the account'
    $alreadyActive = (Call POST '/api/auth/send-verification-email' @{email=$expiredEmail;password=$password} 200).data
    Check ($null -eq $alreadyActive.emailDelivery) 'Active accounts do not trigger extra verification emails'

    $imagePath = Join-Path $privateDirectory 'room.png'
    $documentPath = Join-Path $privateDirectory 'document.pdf'
    [IO.File]::WriteAllBytes($imagePath,[Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jf1sAAAAASUVORK5CYII='))
    [IO.File]::WriteAllText($documentPath, "%PDF-1.4`n1 0 obj<</Type/Catalog>>endobj`n%%EOF")
    $landlordEmail = New-Email 'landlord'
    $form = @{name='Registration check Landlord';email=$landlordEmail;password=$password;propertyImages=(Get-Item $imagePath);identityDocuments=(Get-Item $documentPath)}
    $landlord = (Call POST '/api/auth/register/landlord' $null 201 '' $baseUrl $form).data
    $stored = Invoke-RegistrationSql "SELECT COUNT(*) FROM dbo.UserRegistrationFile WHERE user_id='$($landlord.userId)' AND DATALENGTH(file_content)>0 AND category IN ('PropertyImage','IdentityDocument');"
    Check ($stored -eq '2' -and $landlord.role -eq 'Landlord') 'Multipart Landlord registration stores both files attached to its GUID'
    $ownerSession = (Call POST '/api/auth/login' @{email=$landlordEmail;password=$password} 200).data
    Call GET '/api/rooms/mine' $null 200 $ownerSession.token | Out-Null
    Call GET '/api/admin/users' $null 403 $ownerSession.token | Out-Null
    Check $true 'Existing Landlord login and role restrictions remain functional'
    $form.email = New-Email 'badfile'
    [IO.File]::WriteAllText($documentPath, 'This is not a PDF, even though its extension is .pdf.')
    Call POST '/api/auth/register/landlord' $null 400 '' $baseUrl $form | Out-Null
    $form.email = New-Email 'missingfiles'
    $form.Remove('identityDocuments')
    Call POST '/api/auth/register/landlord' $null 400 '' $baseUrl $form | Out-Null
    Check $true 'Spoofed document and missing required upload are rejected before user creation'
    [IO.File]::WriteAllText($documentPath, "%PDF-1.4`n1 0 obj<</Type/Catalog>>endobj`n%%EOF")
    $form.email = New-Email 'badextension'
    $fakePath = Join-Path $privateDirectory 'room.exe'
    Copy-Item -LiteralPath $imagePath -Destination $fakePath
    $form.propertyImages = Get-Item $fakePath
    $form.identityDocuments = Get-Item $imagePath
    Call POST '/api/auth/register/landlord' $null 400 '' $baseUrl $form | Out-Null
    $form.email = New-Email 'toomany'
    $form.propertyImages = @(1..4 | ForEach-Object {
        $copyPath = Join-Path $privateDirectory "room-$_.png"
        Copy-Item -LiteralPath $imagePath -Destination $copyPath
        Get-Item $copyPath
    })
    $form.identityDocuments = Get-Item $documentPath
    Call POST '/api/auth/register/landlord' $null 400 '' $baseUrl $form | Out-Null
    Check $true 'Unsupported extensions and excessive upload counts are rejected'
    $form.email = New-Email 'oversize'
    $form.identityDocuments = Get-Item $documentPath
    [IO.File]::WriteAllBytes($imagePath,[byte[]]::new(5*1024*1024+1))
    $form.propertyImages = Get-Item $imagePath
    Call POST '/api/auth/register/landlord' $null 400 '' $baseUrl $form | Out-Null
    Check $true 'Files exceeding 5 MB are rejected'
    Call POST '/api/auth/register' @{name='Registration check bypass';email=(New-Email 'bypass');password=$password;role='Landlord'} 400 | Out-Null
    Check $true 'JSON registration cannot bypass Landlord upload requirements'

    Start-TestApi ($Port+1) 'Smtp'
    $unconfigured = New-Email 'unconfigured'
    Call POST '/api/auth/register' @{name='Registration check no SMTP';email=$unconfigured;password=$password;role='Tenant'} 503 '' "http://127.0.0.1:$($Port+1)" | Out-Null
    Start-TestApi ($Port+2) 'Smtp' '127.0.0.1'
    $failed = New-Email 'smtp-failure'
    Call POST '/api/auth/register' @{name='Registration check SMTP failure';email=$failed;password=$password;role='Tenant'} 503 '' "http://127.0.0.1:$($Port+2)" | Out-Null
    $notCreated = Invoke-RegistrationSql "SELECT COUNT(*) FROM dbo.[User] WHERE email IN ('$unconfigured','$failed');"
    Check ($notCreated -eq '0') 'Missing SMTP or failed delivery returns 503 and leaves no account to block retry'
    $pendingEmail = New-Email 'resend-failure'
    $pending = Register-Tenant $pendingEmail
    Invoke-RegistrationSql "UPDATE dbo.[User] SET email_verification_sent_at=DATEADD(MINUTE,-2,SYSUTCDATETIME()) WHERE user_id='$($pending.userId)';" | Out-Null
    $resendClient = [System.Net.Http.HttpClient]::new()
    try {
        $resendBody = @{email=$pendingEmail;password=$password} | ConvertTo-Json
        $firstContent = [System.Net.Http.StringContent]::new($resendBody, [Text.Encoding]::UTF8, 'application/json')
        $secondContent = [System.Net.Http.StringContent]::new($resendBody, [Text.Encoding]::UTF8, 'application/json')
        $firstResend = $resendClient.PostAsync("$baseUrl/api/auth/send-verification-email", $firstContent)
        $secondResend = $resendClient.PostAsync("$baseUrl/api/auth/send-verification-email", $secondContent)
        $resendCodes = @([int]$firstResend.GetAwaiter().GetResult().StatusCode, [int]$secondResend.GetAwaiter().GetResult().StatusCode) | Sort-Object
        Check (($resendCodes -join ',') -eq '200,429') 'Concurrent resend requests issue only one email and enforce the persisted cooldown'
    } finally { $resendClient.Dispose() }
    $pendingLink = Verification-Link $pending.userId
    Invoke-RegistrationSql "UPDATE dbo.[User] SET email_verification_sent_at=DATEADD(MINUTE,-2,SYSUTCDATETIME()) WHERE user_id='$($pending.userId)';" | Out-Null
    Call POST '/api/auth/send-verification-email' @{email=$pendingEmail;password=$password} 503 '' "http://127.0.0.1:$($Port+1)" | Out-Null
    Call POST '/api/auth/send-verification-email' @{email=$pendingEmail;password=$password} 503 '' "http://127.0.0.1:$($Port+2)" | Out-Null
    Check ((Invoke-WebRequest -Uri $pendingLink -SkipHttpErrorCheck).StatusCode -eq 200) 'Failed resend preserves the previously issued valid link'
    Invoke-RegistrationSql "UPDATE dbo.[User] SET status='Inactive' WHERE user_id='$($tenant.userId)';" | Out-Null
    Call GET '/api/auth/me' $null 401 $session.token | Out-Null
    Call POST '/api/auth/send-verification-email' @{email=$tenantEmail;password=$password} 403 | Out-Null
    Check $true 'JWT for an account made inactive is still rejected'
    Check $true 'Email resend cannot unlock a previously verified account disabled by an administrator'
    # All stored credentials below are synthetic fixtures in an isolated profile, never the user's real store.
    $fakeSender = 'fixture-sender@example.invalid'
    $fakePassword = 'abcdefghijklmnop'
    New-Item -ItemType Directory -Path $testProfile -Force | Out-Null
    $previousAppData = $env:APPDATA
    try {
        $env:APPDATA = $testProfile
        '{"Fixture:Preserved":"existing-value"}' | & dotnet user-secrets set --project (Join-Path $apiDirectory 'RoomRum.API.csproj') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the isolated User Secrets fixture.' }
        & (Join-Path $apiDirectory '../../Setup-GmailSecrets.ps1') -SenderEmail $fakeSender -AppPassword (ConvertTo-SecureString $fakePassword -AsPlainText -Force)
        $projectXml = [xml](Get-Content -Raw (Join-Path $apiDirectory 'RoomRum.API.csproj'))
        $secretId = $projectXml.SelectSingleNode('//UserSecretsId').InnerText
        $fixtureSecretsPath = Join-Path $testProfile "Microsoft/UserSecrets/$secretId/secrets.json"
        $fixtureSecrets = Get-Content -Raw -LiteralPath $fixtureSecretsPath | ConvertFrom-Json
        Check ($fixtureSecrets.'Smtp:Username' -eq $fakeSender -and $fixtureSecrets.'Smtp:Password' -eq $fakePassword -and $fixtureSecrets.'Fixture:Preserved' -eq 'existing-value') 'Setup saves Gmail credentials to isolated .NET User Secrets and preserves unrelated keys'
    } finally { $env:APPDATA = $previousAppData }
    $smtpMock = [RoomRumTestSmtp]::new($Port+3, $fakeSender, $fakePassword)
    Start-TestApi ($Port+4) 'Smtp' '127.0.0.1' ($Port+3) $false -UseUserSecrets
    $smtpEmail = New-Email 'smtp-success'
    $smtpRegistration = (Call POST '/api/auth/register' @{name='Registration check SMTP success';email=$smtpEmail;password=$password;role='Tenant'} 201 '' "http://127.0.0.1:$($Port+4)").data
    if (-not $smtpMock.Completion.Wait([TimeSpan]::FromSeconds(5))) { throw 'SMTP receiver did not finish.' }
    $smtpMessage = $smtpMock.Completion.GetAwaiter().GetResult()
    $smtpLink = [regex]::Match($smtpMessage, "http://127\.0\.0\.1:$($Port+4)/api/auth/verify-email\?userId=$($smtpRegistration.userId)&token=[A-F0-9]{64}").Value
    Check ($smtpMock.Authenticated -and $smtpRegistration.emailDelivery -eq 'Smtp' -and $smtpMessage.Contains($smtpEmail) -and $smtpLink.Length -gt 0) 'Development loads SMTP credentials from User Secrets and authenticates with the loopback SMTP receiver'
    Check ((Invoke-WebRequest -Uri $smtpLink -SkipHttpErrorCheck).StatusCode -eq 200) 'Verification link from the SMTP-delivered message activates its account'
    $smtpMock.Dispose()
    $smtpMock = [RoomRumTestSmtp]::new($Port+5, $fakeSender, $fakePassword)
    Start-TestApi ($Port+6) 'Smtp' '127.0.0.1' ($Port+5) $false -UseUserSecrets
    $smtpResendEmail = New-Email 'smtp-resend'
    $smtpPending = Register-Tenant $smtpResendEmail
    Invoke-RegistrationSql "UPDATE dbo.[User] SET email_verification_sent_at=DATEADD(MINUTE,-2,SYSUTCDATETIME()) WHERE user_id='$($smtpPending.userId)';" | Out-Null
    $smtpResent = (Call POST '/api/auth/send-verification-email' @{email=$smtpResendEmail;password=$password} 200 '' "http://127.0.0.1:$($Port+6)").data
    if (-not $smtpMock.Completion.Wait([TimeSpan]::FromSeconds(5))) { throw 'SMTP resend receiver did not finish.' }
    $resentSmtpMessage = $smtpMock.Completion.GetAwaiter().GetResult()
    $resentSmtpLink = [regex]::Match($resentSmtpMessage, "http://127\.0\.0\.1:$($Port+6)/api/auth/verify-email\?userId=$($smtpPending.userId)&token=[A-F0-9]{64}").Value
    Check ($smtpMock.Authenticated -and $smtpResent.emailDelivery -eq 'Smtp' -and $resentSmtpMessage.Contains($smtpResendEmail) -and $resentSmtpLink.Length -gt 0) 'Inactive-account resend authenticates and sends SMTP email with User Secrets credentials'
    Check ((Invoke-WebRequest -Uri $resentSmtpLink -SkipHttpErrorCheck).StatusCode -eq 200) 'Verification link from the resent SMTP message activates the Inactive account'
    "PASS: $checks registration/security checks. Pickup and loopback SMTP tested; real Gmail delivery requires an App Password."
}
finally {
    if ($null -ne $smtpMock) { $smtpMock.Dispose() }
    foreach ($server in $servers) { if (-not $server.HasExited) { Stop-Process -Id $server.Id -ErrorAction SilentlyContinue; $server.WaitForExit(5000) | Out-Null } }
    if ($emails.Count -gt 0) {
        $in = ($emails | ForEach-Object { "'" + $_.Replace("'", "''") + "'" }) -join ','
        Invoke-RegistrationSql "DELETE FROM dbo.[User] WHERE email IN ($in) AND name LIKE 'Registration check%'; SELECT COUNT(*) AS remaining_check_users FROM dbo.[User] WHERE email IN ($in);"
    }
    # The private .eml/log fixtures remain ignored under App_Data for diagnosis; they contain test tokens only.
}
