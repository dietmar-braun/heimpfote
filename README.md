# Tierschutz Collect

Portal, das Tiere aus vielen Tierheimen und Organisationen zusammenführt.

## Struktur
- `portal/`   ASP.NET Core (C#), Domain, Infrastructure (EF Core/PostgreSQL), Web
- `crawler/`  Python-Crawler (folgt)
- `plugins/`  WordPress-Plugin u. a. (folgt)
- `docs/`     Datenmodell, Beispiel-Requests

## Erster Start
```bash
cp .env_sample .env                        # Windows: copy .env_sample .env
docker compose up -d                       # PostgreSQL + PostGIS

cd portal                                  # hier liegt TierschutzCollect.slnx (in Visual Studio öffnen)

dotnet tool install --global dotnet-ef     # einmalig
dotnet ef migrations add Initial -p src/Portal.Infrastructure -s src/Portal.Web

dotnet run --project src/Portal.Web
```
Die Tabellen entstehen durch Migrationen (Entity Framework Core). Die erste Migration muss einmal erzeugt werden: oben per `dotnet ef migrations add Initial …` oder in Visual Studio in der Paket-Manager-Konsole mit `Add-Migration Initial -Project Portal.Infrastructure -StartupProject Portal.Web`. Der Ordner `portal/src/Portal.Infrastructure/Migrations` gehört ins Repository. Ohne Migration startet die App mit einem Hinweis und legt keine Tabellen an. Beim Start im Development-Modus werden Migrationen automatisch angewendet. Nach jeder Änderung am Datenmodell eine neue Migration anlegen.

Der Datenbank-Port steht in `.env` (`POSTGRES_PORT`, Vorlage: `.env_sample`). Docker Compose liest nur `.env`, nicht `.env_sample`. Derselbe Port muss in `portal/src/Portal.Web/appsettings.Development.json` unter `ConnectionStrings:Default` bei `Port=` stehen. Nach einer Änderung `docker compose up -d` erneut ausführen.

Beim ersten Start im Development-Modus wird die Datenbank migriert und eine
Demo-Organisation angelegt. Der API-Key steht **einmalig** im Konsolen-Log.
Danach `docs/beispiel-import.http` ausführen (Web läuft auf http://localhost:5080).

## Tests
```bash
cd portal
dotnet test
```
Die Tests laufen gegen eine SQLite-In-Memory-Datenbank (kein Docker nötig).
Abgedeckt sind: API-Keys, Slugs, Import-Validierung, Import-Logik (anlegen, aktualisieren,
archivieren, Reaktivieren, Bilder, Rassen), Datenbankmodell (Indizes, Cascade, Enum-Mapping)
und der API-Key-Filter.

## Blazor-Oberfläche (Schritt 3)
Seiten in `portal/src/Portal.Web/Components/Pages`:
- `/`            Startseite (`Home.razor`)
- `/tiere`       Tierliste mit Filtern und Seitenwechsel (`Tiere.razor`)
- `/tiere/{slug}` Detailseite eines Tiers (`TierDetail.razor`)

Im Development-Modus legt die App beim ersten Start sechs Demo-Tiere an.
Voraussetzung: Standardnamespace von `Portal.Web` ist `TierschutzCollect.Web`.

## Login und Verwaltung (Schritt 4)
- `/login` Anmeldung (ASP.NET Core Identity, Cookie), `/admin` Verwaltung (nur Rolle `Admin`)
- `/admin/organisationen` anlegen, bearbeiten, **API-Key erzeugen** (Klartext nur einmal sichtbar)
- `/admin/tiere` Tiere filtern und den Status manuell ändern
- Im Development-Modus wird ein Admin aus `appsettings.Development.json` angelegt
  (`admin@localhost.test` / `Admin!12345`). **Nur lokal!**
- Produktion: `Admin__Email` und `Admin__Password` als Umgebungsvariablen oder User-Secrets setzen,
  nie im Repository. Ein vorhandener Benutzer wird nicht überschrieben.
- Die Identity-Tabellen sind Teil des Datenmodells und kommen mit der ersten Migration (`Initial`, siehe „Erster Start“).

## Suche
- `/tiere` filtert nach Tierart, Geschlecht, Größe, Altersgruppe, Kastriert, Verträglichkeit (Kinder, Hunde, Katzen, Wohnung), Dringend und Freitext. Sortierung: empfohlen, neueste, Name.
- Der Freitext nutzt auf PostgreSQL die deutsche Volltextsuche (GIN-Index) plus Teilwort-Treffer bei Name und Rasse. In den Tests (SQLite) läuft eine einfache Teilstring-Suche.
- Alle Filter stehen in der URL und lassen sich teilen, zum Beispiel `/tiere?art=Hund&groesse=gross&kinder=1`.
- Der Volltextindex ist ebenfalls Teil der ersten Migration (`Initial`). Spätere Modelländerungen brauchen jeweils eine neue Migration.

## SEO
- `/sitemap.xml` (alle vermittelbaren Tiere) und `/robots.txt` (sperrt `/admin`, `/login`, `/api`) werden automatisch erzeugt.
- Für Produktion `Site:BaseUrl` setzen (zum Beispiel `https://beispiel.de`, per Umgebungsvariable `Site__BaseUrl`). Ohne Angabe wird die Adresse der aktuellen Anfrage verwendet.
- Detailseiten und Listen ohne Zusatzfilter haben einen Canonical-Link. Gefilterte Ergebnisseiten sind `noindex,follow`, ebenso Tiere, die nicht mehr vermittelbar sind.
