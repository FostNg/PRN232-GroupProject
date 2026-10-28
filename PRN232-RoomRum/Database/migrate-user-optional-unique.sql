USE RoomRum_db;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @drop nvarchar(max) = N'';
SELECT @drop = @drop + N'ALTER TABLE dbo.[User] DROP CONSTRAINT ' + QUOTENAME(k.name) + N';'
FROM sys.key_constraints AS k
JOIN sys.index_columns AS ic ON ic.object_id = k.parent_object_id AND ic.index_id = k.unique_index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE k.parent_object_id = OBJECT_ID(N'dbo.[User]')
  AND k.type = 'UQ' AND c.name IN (N'email', N'citizen_id');
IF LEN(@drop) > 0 EXEC sys.sp_executesql @drop;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[User]') AND name = N'UX_User_Email_NotNull')
    CREATE UNIQUE NONCLUSTERED INDEX UX_User_Email_NotNull ON dbo.[User](email) WHERE email IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[User]') AND name = N'UX_User_CitizenId_NotNull')
    CREATE UNIQUE NONCLUSTERED INDEX UX_User_CitizenId_NotNull ON dbo.[User](citizen_id) WHERE citizen_id IS NOT NULL;

COMMIT TRANSACTION;
GO
