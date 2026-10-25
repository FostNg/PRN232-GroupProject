-- Run against an existing database with sqlcmd -d <database> -i Database/migrate-room-status.sql.
-- Legacy Draft rooms become Pending; Deleted rooms become Cancelled.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Room', N'U') IS NULL OR COL_LENGTH(N'dbo.Room', N'status') IS NULL
    THROW 50001, 'Room.status does not exist.', 1;

UPDATE dbo.Room SET status = 'Pending' WHERE status = 'Draft' OR status IS NULL;
UPDATE dbo.Room SET status = 'Cancelled' WHERE status = 'Deleted';

IF EXISTS (SELECT 1 FROM dbo.Room
           WHERE status NOT IN ('Pending', 'Available', 'Rented', 'Hidden', 'Cancelled'))
    THROW 50002, 'Room contains an unknown status; resolve it before migrating.', 1;

DECLARE @tableId INT = OBJECT_ID(N'dbo.Room');
DECLARE @columnId INT = COLUMNPROPERTY(@tableId, N'status', 'ColumnId');
DECLARE @defaultName SYSNAME;
DECLARE @sql NVARCHAR(MAX);
SELECT @defaultName = name FROM sys.default_constraints
WHERE parent_object_id = @tableId AND parent_column_id = @columnId;
IF @defaultName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.Room DROP CONSTRAINT ' + QUOTENAME(@defaultName);
    EXEC (@sql);
END;

IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE parent_object_id = @tableId AND name = N'CK_Room_Status')
    ALTER TABLE dbo.Room DROP CONSTRAINT CK_Room_Status;

ALTER TABLE dbo.Room ADD CONSTRAINT DF_Room_Status DEFAULT 'Pending' FOR status;
ALTER TABLE dbo.Room WITH CHECK ADD CONSTRAINT CK_Room_Status
    CHECK (status IS NOT NULL AND status IN ('Pending', 'Available', 'Rented', 'Hidden', 'Cancelled'));

COMMIT TRANSACTION;
