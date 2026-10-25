USE RoomRum_db;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Amenity') AND name=N'amenity_name' AND system_type_id=167)
    ALTER TABLE dbo.Amenity ALTER COLUMN amenity_name NVARCHAR(255) NOT NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.[User]') AND name=N'gender' AND system_type_id=167)
    ALTER TABLE dbo.[User] ALTER COLUMN gender NVARCHAR(20) NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Room') AND name=N'type' AND system_type_id=167)
BEGIN
    DROP INDEX IX_Room_Filter ON dbo.Room;
    ALTER TABLE dbo.Room ALTER COLUMN [type] NVARCHAR(50) NULL;
    CREATE NONCLUSTERED INDEX IX_Room_Filter ON dbo.Room(status, [type], price);
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Sharing') AND name=N'gender' AND system_type_id=167)
    ALTER TABLE dbo.Sharing ALTER COLUMN gender NVARCHAR(20) NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Repairing') AND name=N'priority' AND system_type_id=167)
    ALTER TABLE dbo.Repairing ALTER COLUMN priority NVARCHAR(50) NULL;

COMMIT TRANSACTION;
GO
