# Room management regression tests

Run from the backend root:

```powershell
dotnet test tests/RoomManagement.Tests/RoomManagement.Tests.csproj
```

The tests use a fresh EF Core in-memory database per case; they do not connect to or modify the configured SQL Server database.

Coverage: full management detail across Pending/Available/Rented/Cancelled/Hidden, owner/admin access, cross-owner denial, unknown IDs, owner listing of rejected/hidden posts, public detail exclusion, private image access, and endpoint role requirements.

Management detail: `GET /api/rooms/{id}/management` requires a Landlord or Admin token. It returns 404 for unknown rooms or another landlord's room. Public detail keeps its existing visibility rules. Uploaded image content also checks visibility and requires the owner/admin token for nonpublic rooms.
