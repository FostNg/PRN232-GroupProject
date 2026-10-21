-- Only active tenancies make a room Rented. Pending invitations leave it Available.
-- Reconcile existing Available/Rented rooms without recreating the database.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE room
SET status = CASE WHEN EXISTS (
    SELECT 1 FROM dbo.RoomTenant AS tenant
    WHERE tenant.room_id = room.room_id AND tenant.status = 'Active'
) THEN 'Rented' ELSE 'Available' END
FROM dbo.Room AS room
WHERE room.status IN ('Available', 'Rented')
  AND ((room.status = 'Available' AND EXISTS (
      SELECT 1 FROM dbo.RoomTenant AS tenant
      WHERE tenant.room_id = room.room_id AND tenant.status = 'Active'
  )) OR (room.status = 'Rented' AND NOT EXISTS (
      SELECT 1 FROM dbo.RoomTenant AS tenant
      WHERE tenant.room_id = room.room_id AND tenant.status = 'Active'
  )));

SELECT @@ROWCOUNT AS updated_rooms;
COMMIT TRANSACTION;
