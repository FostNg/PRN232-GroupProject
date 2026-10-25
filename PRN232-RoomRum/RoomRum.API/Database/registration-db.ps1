# Shared by the registration verification/seed scripts. Never writes credentials to disk/output.
function Initialize-RegistrationDb([string]$ApiDirectory) {
    $config = Get-Content -Raw -LiteralPath (Join-Path $ApiDirectory 'appsettings.json') | ConvertFrom-Json
    $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
    $connection.set_ConnectionString($config.ConnectionStrings.RoomRumConnection)
    $script:RegistrationSqlArgs = @('-S', [string]$connection.get_Item('Server'), '-d', [string]$connection.get_Item('Database'), '-C', '-I', '-b', '-l', '10', '-h', '-1', '-W')
    $script:RegistrationSqlPassword = $null
    if ($connection.ContainsKey('User Id')) {
        $script:RegistrationSqlArgs += @('-U', [string]$connection.get_Item('User Id'))
        $script:RegistrationSqlPassword = [string]$connection.get_Item('Password')
    } else { $script:RegistrationSqlArgs += '-E' }
}

function Invoke-RegistrationSql([string]$Query) {
    $previousPassword = $env:SQLCMDPASSWORD
    try {
        if ($null -ne $script:RegistrationSqlPassword) { $env:SQLCMDPASSWORD = $script:RegistrationSqlPassword }
        $result = & sqlcmd @script:RegistrationSqlArgs -Q "SET NOCOUNT ON; $Query" 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Registration SQL command failed: $result" }
        return ($result -join "`n").Trim()
    } finally { $env:SQLCMDPASSWORD = $previousPassword }
}
