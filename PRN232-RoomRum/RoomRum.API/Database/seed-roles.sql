USE RoomRum_db;
GO

-- Safe to rerun against an existing database; does not recreate tables.
INSERT INTO Role (role_name)
SELECT v.role_name
FROM (VALUES ('Tenant'), ('Landlord'), ('Admin')) AS v(role_name)
WHERE NOT EXISTS (SELECT 1 FROM Role AS r WHERE r.role_name = v.role_name);
GO
