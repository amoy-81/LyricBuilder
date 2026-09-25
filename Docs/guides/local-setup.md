# Local setup

From a clean checkout to a running API.

## Prerequisites

| | Version |
|---|---|
| .NET SDK | 10.0 |
| PostgreSQL | 14+ (developed against 17) |
| `dotnet-ef` | matching EF Core 10 |

```bash
dotnet --version                       # expect 10.x
dotnet tool install --global dotnet-ef # or: dotnet tool update --global dotnet-ef
```

## 1. A database

Any reachable PostgreSQL works — a local install, Docker, or a remote instance.

```bash
# Docker, if you want a throwaway instance
docker run --name lyricbuilder-pg -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 -d postgres:17
```

Create the database:

```bash
psql -h localhost -U postgres -c "CREATE DATABASE lyricbuilder;"
```

### If PostgreSQL is already installed

On Windows, check the service and port:

```powershell
Get-Service -Name '*postgres*'
Test-NetConnection -ComputerName localhost -Port 5432 -InformationLevel Quiet
```

A running service on an open port does not mean the default password works. Verify before
assuming:

```bash
psql -h localhost -U postgres -c "SELECT version();"
```

`FATAL: password authentication failed` means the server is fine and the credentials are not.

## 2. The connection string

`appsettings.Development.json` ships with a **placeholder password**. It will not connect.

Use user-secrets so real credentials stay out of the repository — that file is not gitignored:

```bash
dotnet user-secrets set "ConnectionStrings:LyricBuilderDatabase" \
  "Host=localhost;Port=5432;Database=lyricbuilder;Username=postgres;Password=<your-password>" \
  --project Src/LyricBuilder.Host
```

The key is required. Startup throws `InvalidOperationException` if it is missing or blank —
by design, so a misconfigured app fails immediately rather than at the first query.

## 3. The schema

**No migration exists yet.** Generate it once:

```bash
dotnet ef migrations add Initial \
  -p Src/LyricBuilder.Infrastructure \
  -s Src/LyricBuilder.Host
```

Then apply it:

```bash
dotnet ef database update \
  -p Src/LyricBuilder.Infrastructure \
  -s Src/LyricBuilder.Host
```

`-p` is where the migration files are written. `-s` is the startup project, which supplies the
connection string.

## 4. Run

```bash
dotnet run --project Src/LyricBuilder.Host
```

| URL | What |
|---|---|
| `/scalar/v1` | Interactive API reference |
| `/openapi/v1.json` | Raw OpenAPI document |
| `/live` | Health check |

`/scalar` redirects to `/scalar/v1`.

## Verifying it works

```bash
curl -i http://localhost:<port>/live          # 200
curl -i http://localhost:<port>/api/lyrics    # 200 with an empty page
```

An empty result from `/api/lyrics` is success — the table exists and has no rows.

## Troubleshooting

**`500` from `/api/lyrics`, log shows `Npgsql.PostgresException: password authentication failed`**
The app reached PostgreSQL and the credentials are wrong. Re-check step 2. Note that
`appsettings.Development.json` overrides nothing if user-secrets is set — secrets win.

**`500`, log shows `relation "Lyrics" does not exist`**
The database exists but the schema was never applied. Step 3.

**`InvalidOperationException: ConnectionStrings:LyricBuilderDatabase is not configured`**
Thrown at startup. The key is missing or empty.

**The request hangs, then fails**
Nothing is listening on the host and port. PostgreSQL is not running, or the port differs.

**`Cannot write DateTime with Kind=Unspecified`**
Should not happen — the UTC converter handles it. If it does, a `DateTime` is being written
through a path that bypasses the model. See [persistence](../architecture/persistence.md).

**`InvalidOperationException: Jwt:SigningKey must be at least 32 bytes`**
Thrown at startup outside Development, where no key is committed. Set one:
`dotnet user-secrets set "Jwt:SigningKey" "<32+ random characters>"`, or the `Jwt__SigningKey`
environment variable.

**`401` from any `/api/lyrics` endpoint**
Every lyrics endpoint needs a token, reads included. Register, log in at `POST /api/auth/login`, and send the `accessToken` as
`Authorization: Bearer <token>`. In `/scalar/v1`, paste it into the auth section once.
