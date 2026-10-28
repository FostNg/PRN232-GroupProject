-- Run against an existing RoomRum_db. Safe to rerun; preserves all rooms.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Room', 'latitude') IS NULL
    ALTER TABLE dbo.Room ADD latitude FLOAT NULL;
IF COL_LENGTH('dbo.Room', 'longitude') IS NULL
    ALTER TABLE dbo.Room ADD longitude FLOAT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Room_Coordinates' AND parent_object_id = OBJECT_ID('dbo.Room'))
    ALTER TABLE dbo.Room ADD CONSTRAINT CK_Room_Coordinates CHECK (
        (latitude IS NULL AND longitude IS NULL) OR
        (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180)
    );

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Room_Coordinates' AND object_id = OBJECT_ID('dbo.Room'))
    CREATE NONCLUSTERED INDEX IX_Room_Coordinates ON dbo.Room(latitude, longitude)
    WHERE latitude IS NOT NULL AND longitude IS NOT NULL;
GO

COMMIT TRANSACTION;
