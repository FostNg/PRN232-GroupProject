# RoomRum API for `RoomRum_db`

This API targets the SQL Server schema in [`Database/DB.sql`](Database/DB.sql), plus the additive migrations described below. Copy `appsettings.example.json` to `appsettings.json`, then set the SQL Server connection string and a private, random JWT signing key. Local `appsettings*.json` files are excluded from Git. You can also override the connection with `ConnectionStrings__RoomRumConnection` and the signing key with `Jwt__Key`. Run `dotnet run --launch-profile http` from the `RoomRum.API` directory and open `/swagger`.

## Registration and verification

Apply `Database/migrate-registration-verification.sql` to an existing database before starting this version. Tenant JSON registration creates an Inactive account without a JWT and sends a single-use, expiring API verification link. Configure `EmailVerification:ApiBaseUrl`, `EmailVerification:TokenMinutes` and the `Smtp` settings shown in the example. Missing SMTP or delivery failure returns 503 and rolls back registration. `GET /api/auth/verify-email?userId=...&token=...` activates the user; then use the normal login endpoint. Existing users are not automatically activated or deactivated.

Landlords register through `POST /api/auth/register/landlord` with multipart fields `name`, `email`, `password`, optional `phone`, 1–3 `propertyImages` and 1–2 `identityDocuments`. Each file is limited to 5 MB; the total request is limited to 26 MB. Images accept JPEG/PNG/WebP; documents also accept PDF. Verified file signatures/extensions and private binary storage replace use of the excluded legacy upload controller for this flow. Files are attached to the user in `UserRegistrationFile`, never served publicly. The existing Landlord Active/login behavior is retained.

Existing inactive Tenants can request a fresh link with `POST /api/auth/send-verification-email` using their RoomRum `email` and `password`, or the resend button on the login page. Resends have a persisted 60-second cooldown. A successfully sent new token replaces the old token; SMTP failure rolls the rotation back. Already verified accounts disabled by an administrator cannot unlock themselves through this endpoint.

For Gmail, build Release, then run `../../Setup-GmailSecrets.ps1` followed by `../../Start-Gmail.ps1` from the API directory (or use `./Start-Gmail.ps1 -Configure` from the repository root). Setup prompts for the dedicated Gmail sender and a Google App Password using hidden input, then pipes JSON to the official .NET User Secrets CLI. It preserves unrelated secrets and never writes credentials to source/appsettings/command-line arguments. The project's UserSecretsId lets WebApplication.CreateBuilder load the values automatically in Development. Gmail requires port 587, EnableSsl=true (STARTTLS), username and App Password. On Windows the startup script restarts only this workspace's Release API if it already owns the target port; unrelated listeners cause an error. Google requires 2-Step Verification to create an App Password; see [Google's instructions](https://support.google.com/accounts/answer/185833). User Secrets are for local development and are not encrypted; use the deployment environment's secret mechanism in production. See [Microsoft's documentation](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0).

For Development-only testing, `Email:DeliveryMethod=PickupDirectory` stores .eml files outside wwwroot under `Email:PickupDirectory` (default `App_Data/maildrop`); this does not send real email. Production accepts only SMTP with TLS and a configured HTTPS API base URL. Run `Database/verify-registration.ps1 -Configuration Release` after a Release build for isolated local registration/email/upload checks; it uses ports 5071–5077 and a synthetic User Secrets profile to authenticate against loopback-only SMTP receivers, then cleans up only its own random users. The user's real User Secrets are not used by these tests. See [CHANGE_NOTES.md](../../CHANGE_NOTES.md) for Vietnamese setup, test results and the designated test account. The older business verification scripts assume the previous registration response and require activated fixtures before use with this version.

`appsettings.example.json` is a template; ASP.NET Core does not load it automatically. Editing only that file leaves the API without JWT settings. For first-time local setup, run the following in `RoomRum.API` using PowerShell 7. It preserves an existing local configuration:

```powershell
if (-not (Test-Path -LiteralPath appsettings.json)) {
    $config = Get-Content -Raw -LiteralPath appsettings.example.json | ConvertFrom-Json
    $config.Jwt.Key = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
    $config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath appsettings.json -Encoding utf8
}
```

Set `ConnectionStrings:RoomRumConnection` in the generated `appsettings.json` to your SQL Server, then run the API. JWT settings are checked at startup: the key must be at least 32 UTF-8 bytes and cannot be the example placeholder, issuer and audience must be set, and the access-token lifetime must be positive. Keep the generated key private and stable between restarts so existing access tokens remain valid.

Build the three-layer solution with `dotnet build ../RoomRum.API.slnx`. With the API running locally, `Database/verify-all.ps1 -BaseUrl http://localhost:5041` exercises all 41 Swagger operations across 36 paths, including permissions, advanced room search, location lookup validation, concurrent appointment updates, image ID stability, invitation acceptance, concurrent repeat invitations and paged tenancy history, authentication throttling, Unicode round trips, decimal precision, CORS, inactive accounts, mixed-participant legacy chat data and write flows. It creates temporary records and removes them in a `finally` block. Restart the local API between repeated runs to reset the in-memory authentication rate limiter. Set `-SqlServer` and `-Database` when testing a different SQL Server database. `Database/verify-api.ps1` is a smaller smoke check and accepts the same SQL target parameters for cleanup. Run the scripts with PowerShell 7 so their UTF-8 literals parse correctly.

Set `Cors:AllowedOrigins` to the exact web origins allowed to call the API. During Development, localhost and 127.0.0.1 origins are also accepted on any port. Account login and existing JWTs require the database `User.status` value to be `Active`.
`RateLimiting:AuthPermitLimit` limits the combined registration and login requests from one remote IP per minute; rejected requests return 429. Deployments with multiple API instances need a shared rate limiter if the limit must apply across instances.

Swagger documents typed JSON success contracts, applicable client errors and a standard JSON envelope for other errors. The three `204 No Content` operations have no success body. Success responses stream through the error middleware; error responses are buffered up to 64 KB for envelope conversion.

List endpoints accept `page` and `pageSize` query parameters. They return an array for the requested page (default 1 and 50 records, maximum 100); request the next page until the returned array is shorter than `pageSize`. Invalid page values return 400. Room search and tenancy history retain their paged response objects with a total count.

`Database/DB.sql` is a **destructive baseline**: it drops and recreates `RoomRum_db`. Do not run it against a populated database. For an existing database created from the supplied script, run `Database/seed-roles.sql`, `Database/migrate-user-optional-unique.sql`, `Database/migrate-unicode-labels.sql`, `Database/migrate-favorite-unique.sql`, `Database/migrate-catalog-unique.sql`, then `Database/migrate-tenant-invitations.sql`, `Database/migrate-tenant-history.sql`, and `Database/migrate-room-coordinates.sql`. Run each migration with `sqlcmd -d <database> -i Database/<migration>.sql`; the invitation migration retains existing tenancies and changes the default to `Pending`, while the history migration adds an archive table without deleting current records. The coordinate migration adds nullable location columns, their range check and a filtered index. These migrations have been applied to the local `RoomRum_db`; the isolated audit database intentionally remains the pre-coordinate reference. The Unicode migration widens user-facing label columns to `NVARCHAR`; the favorite and catalog migrations add unique indexes and stop if duplicate records need review. Email remains optional in SQL, although API registration requires it. `Database/verify-schema.ps1` compares the live database with the isolated reference plus the expected coordinate additions, including columns, indexes, foreign keys, checks, defaults, primary keys, triggers and collation.

`GET /api/locations/search?q=...` resolves a user-submitted place name for the map. The BLL caches results and limits upstream requests to one at a time, at least 1.1 seconds apart. Configure `Maps:GeocodingUrl` and `Maps:UserAgent` in application configuration when changing providers. Room markers come from the stored room coordinates; the API does not automatically geocode every room address. Map tiles can be changed in the web UI with `VITE_MAP_TILE_URL`.

## Layers

| Project | Responsibility |
| --- | --- |
| `RoomRum.API` | HTTP routes, JWT/CORS/Swagger setup, error envelope and dependency injection |
| `BLL` (`RoomRum.BLL`) | Business rules, request contracts and service interfaces; returns outcomes consumed by the API layer |
| `DAL` (`RoomRum.DAL`) | EF Core context, entity mappings and SQL Server data model |

The dependency direction is API → BLL → DAL. API references DAL only in the composition root (`Program.cs`) to register the DbContext and validate JWT users. Controllers do not call EF directly.

## API groups

| Routes | Database tables |
| --- | --- |
| `/api/auth` | `User`, `Role` |
| `/api/rooms`, `/api/rooms/amenities` | `Room`, `RoomImage`, `RoomAmenity`, `Amenity`, `RoomRating` |
| `/api/appointments` | `Appointment` |
| `/api/favorites` | `Favorite` |
| `/api/sharings` | `Sharing` |
| `/api/rooms/{roomId}/ratings` | `RoomRating`, `RoomTenant` |
| `/api/room-tenants` | `RoomTenant`, `RoomTenantHistory` |
| `/api/reports`, `/api/repairs` | `Report`, `Repairing` |
| `/api/chat` | `Conversation`, `Message` |
| `/api/admin` | `User`, `Role`, `Amenity` |

All IDs in this API are GUIDs. Protected routes require `Authorization: Bearer <token>`. Public registration accepts only `Tenant` and `Landlord`; create the first administrator by registering a normal account and changing its `role_id` to the `Admin` role in SQL Server under your own database administration process. Subsequent role changes can use `/api/admin/users/{id}/role`.

Room statuses accepted by the API are `Available`, `Rented`, `Draft`, `Hidden` and `Deleted`. Only `Available` rooms appear in public search/detail and can receive new appointments. A room owner invites a tenant with `POST /api/room-tenants/{roomId}`, which creates `Pending`. The invited user accepts with `PATCH /api/room-tenants/{roomId}/accept`; only then is the record `Active`. Either the owner or invitee can cancel a pending invitation with the existing `/end` route. An active tenancy can be ended with the same route. The owner may invite the same user again after `Cancelled` or `Ended`. The closed record is archived in `RoomTenantHistory` in the same transaction that opens the new invitation. The owner and tenant can read paged history with `GET /api/room-tenants/{roomId}/{tenantId}/history?page=1&pageSize=20`. Periods overwritten before the history migration cannot be recovered. Pending or cancelled invitations do not grant rating or sharing privileges.

The old `Controllers`, `Services`, `DTOs`, `Models` and `Data` folders remain in the workspace for reference but are excluded from compilation in the project file. Their endpoints and `int` IDs are not part of this API. Clients built for the old `/api/Listings` contract must switch to `/api/rooms` and GUID IDs.

The new schema has no tables for payment, notification, refresh token or listing moderation, so those old endpoints are unavailable. `RoomImage` stores only `image_url`; image bytes and provenance cannot be served from this schema. The database has no seed rooms or amenities, so an empty public list is expected until real records are added. JWT access tokens expire after the configured access lifetime; there is no refresh endpoint without persistent refresh-token storage.
