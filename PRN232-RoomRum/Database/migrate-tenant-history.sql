-- Run with sqlcmd -d <target database> -i Database/migrate-tenant-history.sql.
-- Existing RoomTenant rows remain current. Periods overwritten before this migration
-- cannot be reconstructed; future repeat invitations archive the closing row.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.RoomTenant', N'U') IS NULL
    THROW 50001, 'RoomTenant must exist before this migration.', 1;

IF OBJECT_ID(N'dbo.RoomTenantHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoomTenantHistory (
        history_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RoomTenantHistory PRIMARY KEY,
        room_id UNIQUEIDENTIFIER NOT NULL,
        user_id UNIQUEIDENTIFIER NOT NULL,
        hired_at DATETIME2 NOT NULL,
        left_at DATETIME2 NULL,
        status VARCHAR(20) NOT NULL CONSTRAINT CK_RoomTenantHistory_Status CHECK (status IN ('Ended', 'Cancelled')),
        archived_at DATETIME2 NOT NULL CONSTRAINT DF_RoomTenantHistory_ArchivedAt DEFAULT SYSDATETIME(),
        CONSTRAINT FK_RoomTenantHistory_RoomTenant FOREIGN KEY (room_id, user_id)
            REFERENCES dbo.RoomTenant(room_id, user_id) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_RoomTenantHistory_RoomUser_ArchivedAt
        ON dbo.RoomTenantHistory(room_id, user_id, archived_at DESC);
END;

COMMIT TRANSACTION;
