# RoomRum demo data

`seed-demo.ps1` adds a small, clearly marked demo story to `RoomRum_db` through the current API:

- one Tenant, one Landlord and one Admin account;
- four room listings with locally served generated photos (three available, one rented);
- approximate area coordinates for distance filter demos (not exact street addresses);
- eight amenities, two favorites, one confirmed viewing, a four-message conversation;
- one active tenancy and one repair request in progress.

Run the API on `http://localhost:5041`, then run from the solution directory:

```powershell
./RoomRum.API/Database/seed-demo.ps1
```

The script is safe to rerun against its own demo records. It creates random passwords on first run and stores them in `Database/demo-credentials.local.json`, which is excluded from Git. Keep that file private. The script uses Windows authentication for SQL Server only to promote the fixed demo admin account; normal demo records are created through the API.

The room photos are generated demonstration assets under `wwwroot/demo/rooms`. Titles and descriptions explicitly identify these listings as sample data. Addresses and prices are illustrative and do not represent real rental offers.
