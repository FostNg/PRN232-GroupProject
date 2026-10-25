-- Remove the records created by seed-demo.ps1 only when this database still
-- contains exactly that isolated dataset. Keep real user data untouched.
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF (SELECT COUNT(*) FROM dbo.[User]) <> 3
   OR EXISTS (SELECT 1 FROM dbo.[User] WHERE email NOT IN
       ('admin.demo@roomrum.test', 'chutro.demo@roomrum.test', 'khach.demo@roomrum.test'))
   OR (SELECT COUNT(*) FROM dbo.Room) <> 4
   OR EXISTS (SELECT 1 FROM dbo.Room WHERE LEFT(title, 7) <> N'[Demo] ')
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50001, 'Database contains records outside the isolated demo dataset; nothing was deleted.', 1;
END;

DELETE FROM dbo.RoomRating;
DELETE FROM dbo.RoomTenantHistory;
DELETE FROM dbo.RoomTenant;
DELETE FROM dbo.Appointment;
DELETE FROM dbo.Favorite;
DELETE FROM dbo.Repairing;
DELETE FROM dbo.Report;
DELETE FROM dbo.Sharing;
DELETE FROM dbo.RoomAmenity;
DELETE FROM dbo.RoomImage;
DELETE FROM dbo.Message;
DELETE FROM dbo.Conversation;
DELETE FROM dbo.Room;
DELETE FROM dbo.[User];
DELETE FROM dbo.Amenity;

COMMIT TRANSACTION;
