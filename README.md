# PRN232-GroupProject

RoomRum group project. The current ASP.NET Core API is in [`PRN232-RoomRum/RoomRum`](PRN232-RoomRum/RoomRum/README.md).

Build the three-layer solution with `dotnet build PRN232-RoomRum/RoomRum/RoomRum.slnx`.
The API uses `RoomRum_db`; see its README for local configuration, database migrations and verification scripts.

See [ARCHITECTURE.md](ARCHITECTURE.md) for the API → BLL → DAL structure. Controllers are in `RoomRum/Controllers`, business contracts and services in `BLL/DTOs` and `BLL/Services`, and entities and repositories in `DAL/Entities` and `DAL/Repositories`.

These are the only subdirectories inside the three projects. API support files stay at the root of `RoomRum`; SQL and verification scripts are in [`PRN232-RoomRum/Database`](PRN232-RoomRum/Database). Build outputs are redirected to the ignored `PRN232-RoomRum/artifacts` directory.
