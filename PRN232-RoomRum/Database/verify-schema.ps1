param(
    [string]$SqlServer = 'HUY\SQLEXPRESS',
    [string]$Database = 'RoomRum_db',
    [string]$ReferenceDatabase = 'RoomRum_audit_20261015'
)

$ErrorActionPreference = 'Stop'
foreach ($name in @($Database, $ReferenceDatabase)) {
    if ($name -notmatch '^[A-Za-z][A-Za-z0-9_]*$') { throw "Invalid database name: $name" }
}

$query = @'
SET NOCOUNT ON;
SELECT CONCAT('COLUMN|', t.name, '|', c.name, '|', ty.name, '|', c.max_length, '|',
    c.precision, '|', c.scale, '|', c.is_nullable, '|', c.is_identity, '|',
    ISNULL(c.collation_name, ''))
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
UNION ALL
SELECT CONCAT('INDEX|', t.name, '|', i.name, '|', i.is_unique, '|',
    ISNULL(i.filter_definition, ''), '|',
    STRING_AGG(CONCAT(c.name, ':', ic.is_descending_key), ',') WITHIN GROUP (ORDER BY ic.key_ordinal))
FROM sys.tables t
JOIN sys.indexes i ON i.object_id = t.object_id
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.is_primary_key = 0 AND i.is_hypothetical = 0
GROUP BY t.name, i.name, i.is_unique, i.filter_definition
UNION ALL
SELECT CONCAT('FK|', pt.name, '|', rt.name, '|', fk.delete_referential_action, '|',
    fk.update_referential_action, '|', fk.is_disabled, '|',
    STRING_AGG(CONCAT(pc.name, '=', rc.name), ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id))
FROM sys.foreign_keys fk
JOIN sys.tables pt ON pt.object_id = fk.parent_object_id
JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
GROUP BY pt.name, rt.name, fk.object_id, fk.delete_referential_action,
    fk.update_referential_action, fk.is_disabled
UNION ALL
SELECT CONCAT('CHECK|', t.name, '|', cc.definition)
FROM sys.check_constraints cc
JOIN sys.tables t ON t.object_id = cc.parent_object_id
UNION ALL
SELECT CONCAT('DEFAULT|', t.name, '|', c.name, '|', dc.definition)
FROM sys.default_constraints dc
JOIN sys.tables t ON t.object_id = dc.parent_object_id
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
UNION ALL
SELECT CONCAT('PK|', t.name, '|',
    STRING_AGG(CONCAT(c.name, ':', ic.is_descending_key), ',') WITHIN GROUP (ORDER BY ic.key_ordinal))
FROM sys.tables t
JOIN sys.indexes i ON i.object_id = t.object_id AND i.is_primary_key = 1
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
GROUP BY t.name
UNION ALL
SELECT CONCAT('TRIGGER|', t.name, '|', tr.name, '|', tr.is_disabled, '|', sm.definition)
FROM sys.triggers tr
JOIN sys.tables t ON t.object_id = tr.parent_id
JOIN sys.sql_modules sm ON sm.object_id = tr.object_id
UNION ALL
SELECT CONCAT('DB_COLLATION|', CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS NVARCHAR(128)))
ORDER BY 1;
'@

function Read-Schema([string]$name) {
    $rows = & sqlcmd -I -S $SqlServer -d $name -E -b -h -1 -W -w 65535 -Q $query 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Could not read schema $name`: $rows" }
    return @($rows | Where-Object { $_ -and $_ -notmatch '^\(\d+ rows? affected\)$' } | ForEach-Object { $_.Trim() })
}

$actual = Read-Schema $Database
$reference = Read-Schema $ReferenceDatabase
# The audit database predates the additive room-coordinate migration.
$coordinateMigration = @(
    'COLUMN|Room|latitude|float|8|53|0|1|0|',
    'COLUMN|Room|longitude|float|8|53|0|1|0|',
    'INDEX|Room|IX_Room_Coordinates|0|([latitude] IS NOT NULL AND [longitude] IS NOT NULL)|latitude:0,longitude:0',
    'CHECK|Room|([latitude] IS NULL AND [longitude] IS NULL OR [latitude]>=(-90) AND [latitude]<=(90) AND ([longitude]>=(-180) AND [longitude]<=(180)))'
)
$expected = @($reference)
if (-not ($reference | Where-Object { $_ -like 'COLUMN|Room|latitude|*' })) {
    $expected += $coordinateMigration
}
$differences = @(Compare-Object -ReferenceObject $expected -DifferenceObject $actual)
if ($differences.Count) {
    $differences | Format-Table -AutoSize | Out-String | Write-Output
    throw "Schema differs between $Database and $ReferenceDatabase."
}
"PASS $($actual.Count) schema facts match between $Database and $ReferenceDatabase."
