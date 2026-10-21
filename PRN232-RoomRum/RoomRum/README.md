# RoomRum API for `RoomRum_db`

This API targets the 16-table SQL Server schema in [`../Database/DB.sql`](../Database/DB.sql). The project runs with its local `appsettings.json`: ASP.NET Core loads that file automatically, and `Program.cs` reads its `ConnectionStrings:RoomRumConnection`, `Jwt`, `Logging`, `Cors` and `RateLimiting` settings. No `appsettings.example.json` file is used. `appsettings.json` is excluded from Git because it contains a database connection string and JWT signing key; provide it locally on each machine. Environment variables such as `ConnectionStrings__RoomRumConnection` and `Jwt__Key` can override individual values. Run `dotnet run --launch-profile RoomRum` from the `RoomRum` directory; the Visual Studio profile opens Swagger at `http://localhost:5041/swagger`.

Build the three-layer solution with `dotnet build RoomRum.slnx`. With the API running locally, `../Database/verify-all.ps1 -BaseUrl http://localhost:5041` exercises all Swagger operations, including permissions, concurrent appointment updates, image ID stability, invitation acceptance, concurrent repeat invitations and paged tenancy history, authentication throttling, Unicode round trips, decimal precision, CORS, inactive accounts, mixed-participant legacy chat data and write flows. It creates temporary records and removes them in a `finally` block. Restart the local API between repeated runs to reset the in-memory authentication rate limiter. Set `-SqlServer` and `-Database` when testing a different SQL Server database. `../Database/verify-api.ps1` is a smaller smoke check and accepts the same SQL target parameters for cleanup. Run the scripts with PowerShell 7 so their UTF-8 literals parse correctly.

Set `Cors:AllowedOrigins` to the exact web origins allowed to call the API. During Development, localhost and 127.0.0.1 origins are also accepted on any port. Registration creates an `Inactive` account and returns account details without a JWT. An admin approves it with `PATCH /api/admin/users/{id}/status` and `{"status":"Active"}`. Only a successful login after approval returns a JWT. Account login and existing JWTs require the database `User.status` value to be `Active`; an admin can deactivate an account with `{"status":"Inactive"}`.
`RateLimiting:AuthPermitLimit` limits the combined registration and login requests from one remote IP per minute; rejected requests return 429. Deployments with multiple API instances need a shared rate limiter if the limit must apply across instances.

Swagger documents typed JSON success contracts, applicable client errors and a standard JSON envelope for other errors. The three `204 No Content` operations have no success body. Success responses stream through the error middleware; error responses are buffered up to 64 KB for envelope conversion.

List endpoints accept `page` and `pageSize` query parameters. They return an array for the requested page (default 1 and 50 records, maximum 100); request the next page until the returned array is shorter than `pageSize`. Invalid page values return 400. Room search and tenancy history retain their paged response objects with a total count.

`../Database/DB.sql` is a **destructive baseline**: it drops and recreates `RoomRum_db`. Do not run it against a populated database. For an existing database created from the supplied script, run `../Database/seed-roles.sql`, `../Database/migrate-user-optional-unique.sql`, `../Database/migrate-unicode-labels.sql`, `../Database/migrate-favorite-unique.sql`, `../Database/migrate-catalog-unique.sql`, then `../Database/migrate-tenant-invitations.sql`, `../Database/migrate-tenant-history.sql` and `../Database/migrate-room-status.sql`. Run each migration with `sqlcmd -d <database> -i ../Database/<migration>.sql`; the invitation migration retains existing tenancies and changes the default to `Pending`, while the history migration adds an archive table without deleting current records. The room status migration maps legacy `Draft` to `Pending`, `Deleted` to `Cancelled`, and NULL to `Pending`; apply it to existing databases before using the updated API. The Unicode migration widens user-facing label columns to `NVARCHAR`; the favorite and catalog migrations add unique indexes and stop if duplicate records need review. Email remains optional in SQL, although API registration requires it. `../Database/verify-schema.ps1` compares the live database with the isolated database after the same migrations, including columns, indexes, foreign keys, checks, defaults, primary keys, triggers and collation.

## Layers

| Project | Responsibility |
| --- | --- |
| `RoomRum` (`RoomRum.API`) | HTTP controllers, JWT/CORS/Swagger setup, error envelope and composition root |
| `BLL` (`RoomRum.BLL`) | `DTOs/Requests`, `DTOs/Responses`, `Services/Interfaces` and `Services/Implementations`; business rules and response mapping |
| `DAL` (`RoomRum.DAL`) | `Entities/RoomRumDbContext.cs`, entities, `Repositories/Interfaces` and `Repositories/Implementations`; queries, persistence and transactions |

The dependency direction is API → BLL → DAL. `Program.cs` registers the context, repositories and services directly with scoped lifetimes. Controllers depend on service interfaces. JWT account validation also goes through `IAuthService`. Services depend on repository interfaces and contain no EF queries or SQL operations. DAL owns EF tracking, pagination queries, conditional updates, duplicate-key handling and the tenancy history transaction. DAL does not reference BLL or API. Repository projection selectors keep response mapping in BLL while allowing SQL to select only the necessary columns. The API error middleware translates storage exceptions into HTTP errors. See [ARCHITECTURE.md](../../ARCHITECTURE.md).

The only project subdirectories are `RoomRum/Controllers`, `BLL/DTOs`, `BLL/Services`, `DAL/Entities` and `DAL/Repositories`. API support classes and configuration are files at the root of `RoomRum`; the active Visual Studio launch profile is `Properties/launchSettings.json` and uses port 5041. SQL and verification scripts live in `../Database`, outside the three projects. Shared `../Directory.Build.props` redirects build outputs and intermediate files to `../artifacts`, so builds do not create `bin` or `obj` directories inside the projects.

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

All IDs in this API are GUIDs. Protected routes require `Authorization: Bearer <token>`. Public registration accepts only `Tenant` and `Landlord`; create the first administrator by registering a normal account, then setting its `role_id` to the `Admin` role and its `status` to `Active` in SQL Server under your own database administration process. Subsequent role changes can use `/api/admin/users/{id}/role`.

Room statuses are `Pending`, `Available`, `Rented`, `Hidden` and `Cancelled`. Room create and update requests have no `status` field and reject it if supplied. New rooms start as `Pending`. An admin lists rooms awaiting review with `GET /api/admin/rooms/pending`, then reviews one with `PATCH /api/admin/rooms/{id}/review` and `{"status":"Available"}` to approve or `{"status":"Cancelled"}` to reject. The owner hides a room with `DELETE /api/rooms/{id}`. Public detail accepts `Rented` and open `Available` rooms. An `Available` room with a pending invitation is visible by ID only to its owner and invited tenant using a JWT; others receive 404. `Pending`, `Hidden` and `Cancelled` return 404. Public search, new appointments and new favorites require `Available` with no pending invitation. A room owner invites a tenant with `POST /api/room-tenants/{roomId}`, which creates a `Pending` tenancy invitation while the room stays `Available`. The invited user accepts with `PATCH /api/room-tenants/{roomId}/accept`; only then is the tenancy record `Active` and the room becomes `Rented`. Either the owner or invitee can cancel a pending invitation with the existing `/end` route. An active tenancy can be ended with the same route. The owner may invite the same user again after `Cancelled` or `Ended`. The closed record is archived in `RoomTenantHistory` in the same transaction that opens the new invitation. The owner and tenant can read paged history with `GET /api/room-tenants/{roomId}/{tenantId}/history?page=1&pageSize=20`. Periods overwritten before the history migration cannot be recovered. Pending or cancelled invitations do not grant rating or sharing privileges.

When the last `Active` tenancy ends, the room returns to `Available` even if other invitations remain `Pending`. A pending invitation hides an `Available` room from public search, detail, ratings and sharing lists until the invitation is accepted or all pending invitations end. If another tenant is still active, the room stays `Rented`.

An admin lists reports awaiting review with `GET /api/reports/pending`, then reviews a report with `PATCH /api/reports/{id}/status` and `{"status":"Resolved"}` or `{"status":"Rejected"}`. Only a `Pending` report can be reviewed.

`GET /api/rooms/mine` shows only the owner's `Available` and `Rented` rooms. `PUT /api/rooms/{id}` accepts partial room updates. Supplied nonblank text and non-null numbers replace their current values; omitted, null, or blank values leave them unchanged. Nonempty `amenityIds` replace the amenity list; an omitted or empty list leaves it unchanged. An omitted or empty `imageUrls` list leaves images unchanged. For each URL in a nonempty `imageUrls` list, an existing image with that URL is deleted, while a new URL adds an image. Other images are preserved. An empty object makes no changes.

The active API project compiles its `Controllers` normally. Clients built for the old `/api/Listings` contract must switch to `/api/rooms` and GUID IDs.

The new schema has no tables for payment, notification, refresh token or listing review history, so those old endpoints are unavailable. `RoomImage` stores only `image_url`; image bytes and provenance cannot be served from this schema. The database has no seed rooms or amenities, so an empty public list is expected until real records are added. JWT access tokens expire after the configured access lifetime; there is no refresh endpoint without persistent refresh-token storage.
