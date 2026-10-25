# Advanced room search

`GET /api/rooms` searches available rooms. All filters are optional. Repeat `amenityIds` to require **every** selected amenity. The former `amenityId` parameter remains supported.

| Parameter | Meaning |
| --- | --- |
| `q`, `type` | Title/address text and exact room type |
| `minPrice`, `maxPrice`, `minArea`, `maxArea` | Inclusive ranges |
| `maxDeposit`, `maxMoveInCost` | Deposit limit; rent plus deposit limit |
| `maxMonthlyCost`, `occupants`, `monthlyKwh` | Estimated monthly limit with 1 to 10 occupants and 0 to 2000 kWh |
| `latitude`, `longitude`, `maxDistanceKm` | Search center and straight-line radius; radius must be above 0 and at most 100 km |
| `sort` | `relevance`, `priceAsc`, `priceDesc`, `areaDesc`, `monthlyCostAsc`, `distance` |
| `page`, `pageSize` | One-based paging; page size at most 100 |

Monthly estimate = rent + service fee + water fee × occupants + electricity fee × monthlyKwh. A room with missing required fee data is excluded when `maxMonthlyCost` is used. Distance filtering includes only rooms with coordinates; distance sorting requires a search center. Both estimates are for comparison, not billing or road travel.

To upgrade an existing `RoomRum_db` without recreating it, run `migrate-room-coordinates.sql` once through `sqlcmd` or SQL Server Management Studio. The migration is idempotent. `DB.sql` is the full baseline and recreates the database, so do not use it as a migration on an existing database.

Landlords can set coordinates on room create/update. Existing clients that omit both coordinates on update keep the saved point. Send `clearCoordinates: true` with no coordinates to remove it.
