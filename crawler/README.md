# Crawler

Liest Tierlisten von Tierheim-Webseiten und schickt sie an die Import-API des Portals
(`POST /api/import/animals`, siehe `docs/beispiel-import.http`).

Jede Webseite bekommt eine eigene YAML-Datei mit CSS-Selektoren (`config/example-site.yaml`).
Neue Seite anbinden = Datei kopieren, Selektoren anpassen, kein Code.

## Einrichtung
```bash
cd crawler
python -m venv .venv
.venv\Scripts\activate            # Linux/macOS: source .venv/bin/activate
pip install -e ".[dev]"
```

## Benutzung
```bash
# 1) CMS der Seite herausfinden (WordPress, TYPO3, Joomla ... -> evtl. Plugin statt Crawler)
tsc-crawler detect https://www.beispiel-tierheim.de

# 2) Testlauf: gibt das JSON aus, sendet nichts
tsc-crawler run config/mein-tierheim.yaml --dry-run

# 3) Echter Import. Organisation vorher im Admin-Bereich anlegen und den API-Key hier setzen
set TSC_API_KEY=tsc_...           # Linux/macOS: export TSC_API_KEY=tsc_...
tsc-crawler run config/mein-tierheim.yaml --api-url http://localhost:5080

# Alle Seiten eines Ordners
tsc-crawler run-all config/sites
```
`--limit 5` bricht nach 5 Tieren ab (praktisch zum Ausprobieren; es wird dann kein Snapshot gesendet).

## Verhalten, das man kennen sollte
- **Höflich**: beachtet `robots.txt`, wartet pro Host `delay_seconds` zwischen Anfragen, wiederholt bei 429/5xx.
- **Snapshot-Schutz**: Das Portal archiviert Tiere, die in einem Snapshot fehlen. Der Crawler sendet nur dann
  einen Snapshot, wenn alles fehlerfrei durchlief, nicht durch `max_pages`/`max_items` abgeschnitten wurde und
  mindestens `min_animals` Tiere gefunden wurden. Sonst werden Tiere nur angelegt/aktualisiert, nichts archiviert.
  So löscht eine geänderte Seitenstruktur nicht versehentlich alle Tiere.
- **Stabile IDs**: `external_id` aus dem Feld `external_id`, sonst aus der Tier-URL, sonst aus dem Namen.
  Ändert sich die ID, entsteht im Portal ein neues Tier.
- **Limit**: Ein Snapshot muss in eine Anfrage passen (max. 500 Tiere). Mehr wird ohne Snapshot gesendet.
- **Normalisierung**: Freitext wie „3 Jahre 2 Monate“, „Rüde“, „nicht verträglich“, „1.200 €“ wird in die
  Werte des Portals übersetzt (`src/tsc_crawler/normalize.py`). Unbekanntes bleibt „Unbekannt“.

## Tests
```bash
pytest            # oder ohne Zusatzpaket: python -m unittest discover -s tests -t .
```
Die Tests laufen offline mit HTML-Beispieldateien aus `tests/fixtures`.

## Rechtliches
Vor dem Crawlen die Nutzungsbedingungen der Seite prüfen und im Zweifel die Organisation um Erlaubnis bitten.
Besser noch: sie nutzt später das Plugin/den Feed und der Crawler wird für diese Seite überflüssig.
