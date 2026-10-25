-- Additive migration. Run against RoomRum_db; never recreates or updates existing users.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.[User]', 'email_verification_token_hash') IS NULL
    ALTER TABLE dbo.[User] ADD email_verification_token_hash VARBINARY(32) NULL;
IF COL_LENGTH('dbo.[User]', 'email_verification_expires_at') IS NULL
    ALTER TABLE dbo.[User] ADD email_verification_expires_at DATETIME2 NULL;
IF COL_LENGTH('dbo.[User]', 'email_verified_at') IS NULL
    ALTER TABLE dbo.[User] ADD email_verified_at DATETIME2 NULL;
IF COL_LENGTH('dbo.[User]', 'email_verification_sent_at') IS NULL
    ALTER TABLE dbo.[User] ADD email_verification_sent_at DATETIME2 NULL;

IF OBJECT_ID(N'dbo.UserRegistrationFile', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRegistrationFile (
        file_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UserRegistrationFile PRIMARY KEY,
        user_id UNIQUEIDENTIFIER NOT NULL,
        category VARCHAR(30) NOT NULL CONSTRAINT CK_UserRegistrationFile_Category
            CHECK (category IN ('PropertyImage', 'IdentityDocument')),
        file_name NVARCHAR(255) NOT NULL,
        content_type VARCHAR(100) NOT NULL,
        file_content VARBINARY(MAX) NOT NULL,
        created_at DATETIME2 NOT NULL,
        CONSTRAINT CK_UserRegistrationFile_Size CHECK (DATALENGTH(file_content) BETWEEN 1 AND 5242880),
        CONSTRAINT FK_UserRegistrationFile_User FOREIGN KEY (user_id)
            REFERENCES dbo.[User](user_id) ON DELETE CASCADE
    );
    CREATE INDEX IX_UserRegistrationFile_UserId ON dbo.UserRegistrationFile(user_id);
END;

COMMIT TRANSACTION;
