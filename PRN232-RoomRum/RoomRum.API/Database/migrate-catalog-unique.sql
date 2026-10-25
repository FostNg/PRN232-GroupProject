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

IF EXISTS (SELECT 1 FROM dbo.Role GROUP BY role_name HAVING COUNT(*) > 1)
    THROW 50001, 'Duplicate role names must be reviewed before adding the unique index.', 1;
IF EXISTS (SELECT 1 FROM dbo.Amenity GROUP BY amenity_name HAVING COUNT(*) > 1)
    THROW 50002, 'Duplicate amenity names must be reviewed before adding the unique index.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Role') AND name = N'UX_Role_Name')
    CREATE UNIQUE NONCLUSTERED INDEX UX_Role_Name ON dbo.Role(role_name);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Amenity') AND name = N'UX_Amenity_Name')
    CREATE UNIQUE NONCLUSTERED INDEX UX_Amenity_Name ON dbo.Amenity(amenity_name);

COMMIT TRANSACTION;
GO
