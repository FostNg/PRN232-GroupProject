param([string]$SenderEmail, [securestring]$AppPassword)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'PRN232-RoomRum/RoomRum/RoomRum.csproj'
if (-not $SenderEmail) { $SenderEmail = Read-Host 'Gmail sender address for RoomRum' }
$SenderEmail = $SenderEmail.Trim()
$senderAddress = $null
if (-not [System.Net.Mail.MailAddress]::TryCreate($SenderEmail, [ref]$senderAddress) -or $senderAddress.Address -ne $SenderEmail) {
    throw 'Enter the full Gmail/Google Workspace address without a display name.'
}
if (-not $AppPassword) { $AppPassword = Read-Host 'Google App Password (16 characters; input is hidden)' -AsSecureString }
$smtpCredential = ([System.Net.NetworkCredential]::new('', $AppPassword).Password) -replace '\s', ''
if ($smtpCredential -notmatch '^[a-zA-Z0-9]{16}$') { throw 'Use a 16-character Google App Password, not a RoomRum or Google login password.' }
$settings = @{
    'Email:DeliveryMethod'='Smtp'; 'Smtp:Host'='smtp.gmail.com'; 'Smtp:Port'='587';
    'Smtp:EnableSsl'='true'; 'Smtp:Username'=$SenderEmail; 'Smtp:FromEmail'=$SenderEmail;
    'Smtp:Password'=$smtpCredential
}
$process = $null
$payload = $null
try {
    # Pass JSON to the official CLI through stdin, never through command-line arguments or a repo file.
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Command dotnet -ErrorAction Stop).Source
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('user-secrets', 'set', '--project', $project)) { $start.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $payload = $settings | ConvertTo-Json -Compress
    $process.StandardInput.WriteLine($payload)
    $process.StandardInput.Close()
    $process.WaitForExit()
    # Do not echo CLI output, including any diagnostic that might contain input values.
    $null = $stdout.GetAwaiter().GetResult()
    $null = $stderr.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0) { throw 'Saving Gmail configuration to .NET User Secrets failed. Check the .NET SDK and user-profile permissions.' }
    Write-Host 'Gmail SMTP settings saved to .NET User Secrets for RoomRum.'
    Write-Host 'No credential was written to source, appsettings, command-line arguments or logs.'
    Write-Host 'Run ./Start-Gmail.ps1 to restart the API with these settings.'
} finally {
    if ($null -ne $process) { $process.Dispose() }
    $settings.Clear()
    $payload = $null
    $smtpCredential = $null
}
