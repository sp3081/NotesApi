# Notes API (ASP.NET Core + SQL Server + JWT)

## Layout
```
NotesApi.slnx
Database/CreateTables.sql
src/NotesApi/              the Web API
tests/NotesApi.Tests/      xUnit tests
  Unit/                    controllers and token service called directly
  Integration/             real HTTP requests against the API running in memory
```

## Running the tests
```
dotnet test
```
No SQL Server is needed: the tests use an in-memory SQLite database, which (unlike EF's InMemory provider)
enforces foreign keys, unique user names and cascade deletes. The integration tests start the API with
`WebApplicationFactory` in the `Testing` environment, which is how `Program.cs` knows to skip the SQL Server registration.

## Setup
1. Run `Database/CreateTables.sql` in SQL Server Management Studio / Azure Data Studio.
   It is safe to re-run; if you already created the tables, it adds the new `PasswordHash` column.
2. Edit the `NotesDb` connection string in `appsettings.json` if you're not using a local default instance with Windows auth
   (e.g. `Server=localhost;Database=NotesDb;User Id=sa;Password=...;TrustServerCertificate=True`).
3. **Replace the JWT signing key.** The key in `appsettings.json` is for development only. For anything real, keep it out of source control (run from `src/NotesApi`):
   ```
   dotnet user-secrets init
   dotnet user-secrets set "Jwt:Key" "<a long random string, 32+ bytes>"
   ```
   or set the environment variable `Jwt__Key`.
4. `dotnet run --project src/NotesApi`. In Development, Swagger UI is at `/swagger`; use **Authorize** to paste a token.
   Requires the .NET 10 SDK (to use .NET 8, change `TargetFramework` to `net8.0` and package versions to `8.0.*`;
   the Swagger "Authorize" transformer in `src/NotesApi/Auth/BearerSecuritySchemeTransformer.cs` uses the .NET 10 OpenAPI types).

## How auth works
1. Register with `POST /api/users` `{ userName, nickname, password }` (password: 8 to 128 characters, stored as a PBKDF2 hash).
2. Get a token with `POST /api/token` `{ userName, password }` → `{ accessToken, tokenType, expiresAt }`.
3. Send `Authorization: Bearer <accessToken>` on every other request. Tokens last `Jwt:ExpiresMinutes` (default 60); log in again when one expires.

## Endpoints
| Method | Route                  | Auth            | Description                                   |
|--------|------------------------|-----------------|-----------------------------------------------|
| POST   | /api/token             | none            | Log in, returns a JWT                         |
| POST   | /api/users             | none            | Register `{ userName, nickname, password }`   |
| GET    | /api/users             | token           | List users                                    |
| GET    | /api/users/me          | token           | The user the token belongs to                 |
| GET    | /api/users/{id}        | token           | Get a user                                    |
| GET    | /api/users/{id}/notes  | token, self     | Your notes (403 for another user's id)        |
| PUT    | /api/users/{id}        | token, self     | Update `{ userName, nickname, password? }`    |
| DELETE | /api/users/{id}        | token, self     | Delete yourself (cascades to your notes)      |
| GET    | /api/notes             | token           | Your notes, newest first                      |
| GET    | /api/notes/{id}        | token, owner    | Get one of your notes                         |
| POST   | /api/notes             | token           | Create `{ note }` (owner taken from the token) |
| PUT    | /api/notes/{id}        | token, owner    | Update note text `{ note }`                   |
| DELETE | /api/notes/{id}        | token, owner    | Delete one of your notes                      |

Missing or invalid token → 401. Another user's note → 404. Changing another user's account → 403.
Timestamps are set by the server in UTC on create and update.
