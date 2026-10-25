param([string]$Configuration = 'Release', [securestring]$Password)
$ErrorActionPreference = 'Stop'
$apiDirectory = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'registration-db.ps1')
Initialize-RegistrationDb $apiDirectory
if (-not $Password) { $Password = Read-Host 'Password for the requested test account' -AsSecureString }
$plainPassword = [System.Net.NetworkCredential]::new('', $Password).Password
if ($plainPassword.Length -lt 8) { throw 'Use a password of at least 8 characters.' }
Add-Type -Path (Join-Path $apiDirectory "bin/$Configuration/net10.0/BCrypt-Net-Next.dll")
$passwordHash = [BCrypt.Net.BCrypt]::HashPassword($plainPassword)
if (-not [BCrypt.Net.BCrypt]::Verify($plainPassword, $passwordHash)) { throw 'BCrypt verification failed.' }
$plainPassword = $null
# Only this exact user is authorized for insert/update. Keep its current status and other fields.
$query = @"
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;
DECLARE @role UNIQUEIDENTIFIER = (SELECT role_id FROM dbo.Role WHERE role_name='Tenant');
IF @role IS NULL THROW 50001, 'Tenant role is missing.', 1;
IF (SELECT COUNT(*) FROM dbo.[User] WITH (UPDLOCK,HOLDLOCK) WHERE email='hieuthuan822@gmail.com') > 1
    THROW 50002, 'Duplicate test accounts require manual review.', 1;
IF EXISTS (SELECT 1 FROM dbo.[User] WHERE email='hieuthuan822@gmail.com')
    UPDATE dbo.[User] SET name=N'thuan', password='$passwordHash', role_id=@role
    WHERE email='hieuthuan822@gmail.com';
ELSE
    INSERT dbo.[User](user_id,role_id,name,email,password,status)
    VALUES(NEWID(),@role,N'thuan','hieuthuan822@gmail.com','$passwordHash','Inactive');
COMMIT TRANSACTION;
SELECT u.user_id,u.name,u.email,u.status,r.role_name FROM dbo.[User] u
JOIN dbo.Role r ON u.role_id=r.role_id WHERE email='hieuthuan822@gmail.com';
"@
Invoke-RegistrationSql $query
$storedHash = Invoke-RegistrationSql "SELECT password FROM dbo.[User] WHERE email='hieuthuan822@gmail.com';"
if ($storedHash -ne $passwordHash) { throw 'The stored password hash does not match the seeded hash.' }
'PASS: one test account; BCrypt hash stored; unrelated users unchanged.'
