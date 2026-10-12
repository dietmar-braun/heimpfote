# Datenmodell (Schritt 1)

## Entitäten
- **Organization**: Tierheim/Verein inkl. Adresse, Geokoordinaten, API-Key-Hash, erkanntem CMS
- **Animal**: ein vermittelbares Tier inkl. Filterattributen und Sync-Feldern
- **Breed**: Rassenliste pro Tierart
- **AnimalImage**: Bilder je Tier (Reihenfolge, Hauptbild)

## Wichtige Entwurfsentscheidungen
1. **Eindeutigkeit:** Unique-Index auf (OrganizationId, ExternalId). Mehrfaches Importieren erzeugt keine Dubletten, sondern aktualisiert.
2. **Lebenszyklus:** `LastSeenAt` wird bei jedem Import gesetzt. Tiere, die länger nicht geliefert werden, werden automatisch auf *Archiviert* gesetzt.
3. **Änderungserkennung:** `ContentHash` spart Schreibvorgänge, wenn sich nichts geändert hat.
4. **Tri-State statt bool** bei Verträglichkeit (Ja/Nein/Unbekannt): Crawler liefern oft keine Info, "Unbekannt" ist nicht "Nein".
5. **Alter:** Geburtsdatum oder geschätztes Alter in Monaten. Filter rechnen auf Monate um.
6. **Standort:** Tiere erben den Standort der Organisation, damit PostGIS-Umkreissuche später einfach bleibt.
7. **Quelle:** `DataSource` (Crawler/Plugin/Feed/Manuell). Plugin-Daten sollen später Crawler-Daten überschreiben.

## Offen für spätere Schritte
- Duplikaterkennung zwischen Quellen (Phase 4)
- Indizes für Filter und Volltextsuche (Schritt 3)
- Rassen-Normalisierung (Synonyme)
