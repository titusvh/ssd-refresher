# SSD Refresher (v1)

## Doel v1
`ssdrefresh` v1 is een **Windows-first, read-only** tool die SHA-256 hash-manifests en machineleesbare rapporten in JSON Lines (JSONL) maakt voor bestanden op:
- één bestand;
- een directory;
- een drive/path;
- een file-list.

### Expliciet read-only in v1
Versie 1:
- herschrijft geen bestanden;
- hernoemt niets;
- verwijdert niets;
- maakt geen tempkopieën van gescande bestanden;
- schrijft alleen manifest- en reportbestanden op opgegeven locaties.

## Branch/releasebeleid
- `main` = laatste stabiele release
- `develop` = integratiebranch voor volgende release
- feature branches mergen naar `develop`

## Commands
```bash
ssdrefresh scan <path> --manifest <manifestPath> --report <reportPath> [--recursive]
ssdrefresh scan-file <filePath> --manifest <manifestPath> --report <reportPath>
ssdrefresh scan --file-list <fileListPath> --manifest <manifestPath> --report <reportPath> [--recursive]
```

Extra filters:
- `--include-hidden`
- `--include-system`
- `--include-reparse-points`

## Manifest vs report
- **Manifest (`*.jsonl`)**: alleen succesvol gehashte bestanden (`Hashed`).
- **Report (`*.jsonl`)**: voor ieder verwerkt item exact één regel met `Hashed`, `Skipped*` of `Failed*` status.

## Exitcodes
- `0`: scan voltooid zonder `Failed*`
- `1`: scan voltooid met minimaal één `FailedIo` of `FailedUnexpected`
- `2`: ongeldige CLI-argumenten
- `130`: geannuleerd (Ctrl+C)

## Bekende beperkingen (v1)
- Geen schrijf-/refreshoperaties op bestanden.
- Locked-file detectie gebruikt momenteel `IOException.HResult`-classificatie.
- Gericht op Windows-semantiek (reparse points, hidden/system attributen).

## Voorbeeld retry-workflow richting v3 (nog niet geïmplementeerd)
1. Voer v1 scan uit en archiveer manifest + report.
2. Filter report op `SkippedLocked`/`FailedIo`.
3. Herhaal scan later op subset (bijv. via `--file-list`).
4. Gebruik in v3 dezelfde stabiele JSONL records als invoer voor geavanceerde refresh/retry-logica.
