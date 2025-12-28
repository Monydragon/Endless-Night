# Endless-Night

Console adventure/horror game prototype.

## MongoDB (Docker)

NOTE: MongoDB instructions below are legacy/outdated. Current builds use the local SQLite database file `endless-night.db` by default.

This project is set up to use MongoDB (default connection string: `mongodb://localhost:27017`, database: `endless-night`).

### Start MongoDB

From the repo root:

```powershell
docker compose up -d
```

### Stop MongoDB

```powershell
docker compose down
```

### Wipe the database (dev reset)

This removes the Docker volume:

```powershell
docker compose down -v
```

### Configure the game

Mongo settings are in `EndlessNight/appsettings.json`:

- `Mongo:ConnectionString`
- `Mongo:DatabaseName`

You can also override via environment variables:

- `Mongo__ConnectionString`
- `Mongo__DatabaseName`

Example:

```powershell
$env:Mongo__ConnectionString = "mongodb://localhost:27017"
$env:Mongo__DatabaseName = "endless-night"
```

### Run

```powershell
dotnet run --project .\EndlessNight\EndlessNight.csproj -c Debug
```

## Can the DB be self-contained inside the project?

Not really in a production sense (MongoDB is a separate server process). Common options:

1. **Docker (recommended for dev)**: what this repo uses.
2. **Local install**: install MongoDB Community Server and point the connection string at it.
3. **Embedded/test Mongo**: there are test-only approaches, but they aren’t reliable or supported as a true embedded database.

If you want a single-file/local-only database without running a separate server, consider switching to an embedded DB (SQLite/LiteDB). But if you specifically want MongoDB features and scalability, Docker/local server is the right setup.

## Console UI rendering mode

In some IDE consoles (notably JetBrains/Rider Run/Debug), ANSI cursor control is limited, which can cause menu screens to appear stacked.

You can override the menu renderer with an environment variable:

- `ENDLESSNIGHT_MENU_MODE=live`  – use Spectre.Console Live rendering (best in real terminals)
- `ENDLESSNIGHT_MENU_MODE=clear` – use clear/redraw mode (best in IDE/limited consoles)

In `clear` mode, menus redraw only when input changes (to avoid scroll spam in IDE consoles).

Windows PowerShell example:

- `setx ENDLESSNIGHT_MENU_MODE "clear"`

(You may need to restart the terminal/IDE after setting it.)
