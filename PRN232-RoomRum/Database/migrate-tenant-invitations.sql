-- Run with sqlcmd -d <target database>. This migration preserves existing tenancies.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @tableId INT = OBJECT_ID(N'dbo.RoomTenant');
DECLARE @statusColumnId INT = COLUMNPROPERTY(@tableId, N'status', 'ColumnId');
IF @tableId IS NULL OR @statusColumnId IS NULL
    THROW 50001, 'RoomTenant.status does not exist.', 1;

DECLARE @constraintName SYSNAME;
DECLARE @sql NVARCHAR(MAX);
DECLARE check_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.check_constraints
    WHERE parent_object_id = @tableId
      AND (parent_column_id = @statusColumnId OR definition LIKE '%status%');
OPEN check_cursor;
FETCH NEXT FROM check_cursor INTO @constraintName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE dbo.RoomTenant DROP CONSTRAINT ' + QUOTENAME(@constraintName);
    EXEC (@sql);
    FETCH NEXT FROM check_cursor INTO @constraintName;
END;
CLOSE check_cursor;
DEALLOCATE check_cursor;

SET @constraintName = NULL;
SELECT @constraintName = name FROM sys.default_constraints
WHERE parent_object_id = @tableId AND parent_column_id = @statusColumnId;
IF @constraintName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.RoomTenant DROP CONSTRAINT ' + QUOTENAME(@constraintName);
    EXEC (@sql);
END;

ALTER TABLE dbo.RoomTenant ADD CONSTRAINT CK_RoomTenant_Status
    CHECK (status IN ('Pending', 'Active', 'Ended', 'Cancelled'));
ALTER TABLE dbo.RoomTenant ADD CONSTRAINT DF_RoomTenant_Status DEFAULT 'Pending' FOR status;

COMMIT TRANSACTION;
