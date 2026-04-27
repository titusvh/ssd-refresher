# SSD Refresher (v1 hash manifest)

## Doel van v1
`ssdrefresh` v1 is een **Windows-first, read-only** scan-tool die SHA-256 hash-manifests en machineleesbare rapporten in JSON Lines maakt.

### Expliciet read-only
v1 doet **niet**:
- bestanden herschrijven;
- bestanden hernoemen;
- bestanden verwijderen;
- temp-kopieën van gescande bestanden maken.

v1 doet **wel**:
- bestanden selecteren vanaf drive/directory/bestand/file-list;
- streaming SHA-256 berekenen;
- manifest- en report-bestanden schrijven op opgegeven paden.

## Branch/releasebeleid
- `main` = laatste stabiele release
- `develop` = integratiebranch
- feature branches mergen naar `develop`
- geen directe feature-ontwikkeling op `main`

## Architectuur
- Service-gebaseerde core (`SsdRefresh.Core`) met manifest/report-first ontwerp.
- Geen DDD, geen Clean Architecture-lagen, geen generiek pipeline-framework.
- Core bevat scanlogica, bestandselectie, progressmodel, manifestwriter en reportwriter.
- CLI (`SsdRefresh.Cli`) bevat alleen parsing, progress output en exitcode-bepaling.

## Commandovoorbeelden
```bash
ssdrefresh scan D:\Data --manifest D:\out\manifest.jsonl --report D:\out\report.jsonl --recursive
ssdrefresh scan C:\Users\me\Documents --manifest manifest.jsonl --report report.jsonl
ssdrefresh scan-file C:\Users\me\Desktop\archive.zip --manifest manifest.jsonl --report report.jsonl
ssdrefresh scan --file-list C:\input\paths.txt --manifest manifest.jsonl --report report.jsonl --recursive
```

Opties:
- `--recursive`
- `--include-hidden`
- `--include-system`
- `--include-reparse-points`

Default (veilig): hidden/system/reparse points worden overgeslagen.

## Manifest vs report
### Manifest (JSONL)
- Alleen records met `status = Hashed`.
- Bevat o.a. pad, lengte, SHA-256, timestamps, attributes en duur.

### Report (JSONL)
- Voor elke verwerkte/overgeslagen file precies één record.
- Bevat status (`Hashed`, `Skipped*`, `Failed*`) plus exception-info waar relevant.

## Exitcodes
- `0`: scan klaar zonder `Failed*` statussen (`Skipped*` toegestaan)
- `1`: scan klaar met minimaal één `FailedIo` of `FailedUnexpected`
- `2`: ongeldige commandline
- `130`: geannuleerd (Ctrl+C)

## Bekende beperkingen v1
- Gericht op Windows-classificatie van lock-fouten; detectie is geïsoleerd en kan later verfijnd worden.
- Geen retry/herwrite logica in v1.
- Geen v2/v3 workflows geïmplementeerd.

## Voorbeeld retry-workflow richting latere v3 (niet geïmplementeerd in v1)
1. Run v1 scan en bewaar report.
2. Filter report op `SkippedLocked`/`FailedIo`.
3. Gebruik gefilterde lijst als nieuwe `--file-list` input.
4. Herhaal scans op geschikte onderhoudsmomenten.
5. Pas in v3 zou een veilige rewrite/refresh fase kunnen volgen op basis van stabiele v1 manifest/report data.
