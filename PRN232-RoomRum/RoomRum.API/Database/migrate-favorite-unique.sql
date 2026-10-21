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

IF EXISTS (
    SELECT 1 FROM dbo.Favorite
    GROUP BY user_id, room_id HAVING COUNT(*) > 1
)
    THROW 50001, 'Duplicate favorites must be reviewed before adding the unique index.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Favorite') AND name=N'UX_Favorite_User_Room')
    CREATE UNIQUE NONCLUSTERED INDEX UX_Favorite_User_Room ON dbo.Favorite(user_id, room_id);

COMMIT TRANSACTION;
GO
