# PRN232-GroupProject

RoomRum group project. The ASP.NET Core API is in [`PRN232-RoomRum/RoomRum`](PRN232-RoomRum/RoomRum/README.md).

Build the three-layer solution with `dotnet build PRN232-RoomRum/RoomRum.slnx`. Run the API with `dotnet run --project PRN232-RoomRum/RoomRum/RoomRum.csproj --launch-profile RoomRum`.
The API uses `RoomRum_db`; see its README for local configuration, database migrations and verification scripts. Huy's advanced search, coordinates, location lookup, email invitations and room image upload are integrated into this API.

See [ARCHITECTURE.md](ARCHITECTURE.md) for the API → BLL → DAL structure. Controllers are in `RoomRum/Controllers`, business contracts and services in `BLL/DTOs` and `BLL/Services`, and entities and repositories in `DAL/Entities` and `DAL/Repositories`.

These are the only subdirectories inside the three projects. API support files stay at the root of `RoomRum`; SQL and verification scripts are in [`PRN232-RoomRum/Database`](PRN232-RoomRum/Database). Build outputs are redirected to the ignored `PRN232-RoomRum/artifacts` directory.
