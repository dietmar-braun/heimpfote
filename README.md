# Tierschutz Collect

Portal, das Tiere aus vielen Tierheimen und Organisationen zusammenführt.

## Struktur
- `portal/`   ASP.NET Core (C#), Domain, Infrastructure (EF Core/PostgreSQL), Web
- `crawler/`  Python-Crawler (folgt)
- `plugins/`  WordPress-Plugin u. a. (folgt)
- `docs/`     Datenmodell, Beispiel-Requests

## Erster Start
```bash
docker compose up -d                       # PostgreSQL + PostGIS

cd portal
dotnet new sln -n TierschutzCollect
dotnet sln add src/Portal.Domain src/Portal.Infrastructure src/Portal.Web

dotnet tool install --global dotnet-ef     # einmalig
dotnet ef migrations add Initial -p src/Portal.Infrastructure -s src/Portal.Web

dotnet run --project src/Portal.Web
```
Beim ersten Start im Development-Modus wird die Datenbank migriert und eine
Demo-Organisation angelegt. Der API-Key steht **einmalig** im Konsolen-Log.
Danach `docs/beispiel-import.http` ausführen.

## Tests
```bash
cd portal
dotnet sln add tests/Portal.Tests
dotnet test
```
Die Tests laufen gegen eine SQLite-In-Memory-Datenbank (kein Docker nötig).
Abgedeckt sind: API-Keys, Slugs, Import-Validierung, Import-Logik (anlegen, aktualisieren,
archivieren, Reaktivieren, Bilder, Rassen), Datenbankmodell (Indizes, Cascade, Enum-Mapping)
und der API-Key-Filter.
