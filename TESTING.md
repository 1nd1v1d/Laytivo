# Laytivo – Abnahme und Prüfprotokoll

Stand: 04.11.2026. Automatisierte Tests prüfen Verhalten mit deterministischen Fakes und echten Dateien. Desktop-Smoke prüft echte Win32-Fenster, jedoch ausschließlich die eigenen Testprozesse. Ein Windows-Login wurde nicht simuliert und nicht ausgelöst.

## Installer 0.1.0

Der Inno-Setup-7-Installer wurde unter Windows in einem eigenen Zielordner tatsächlich installiert, gestartet, aktualisiert und wieder deinstalliert. `scripts/Test-Installer.ps1` hat alle **23 Prüfungen** bestanden:

- EXE, mitgelieferte .NET-Laufzeit und lokale HTML-Anleitung werden installiert.
- Startmenü-Verknüpfungen und benutzerbezogener Deinstallationseintrag sind vorhanden.
- Die installierte App startet; Update und Deinstallation werden während ihres Betriebs durch den App-Mutex blockiert.
- Eine erneute Installation funktioniert als Update und erhält die Daten.
- Die Deinstallation entfernt Programmdateien, Verknüpfungen und den eigenen Autostart-Eintrag.
- Ein Autostart-Eintrag einer anderen Laytivo-Kopie bleibt erhalten.
- Profile und nicht vom Installer angelegte Benutzerdateien bleiben erhalten.
- Das echte Benutzerprofil und der vorherige Autostart-Eintrag wurden nach dem isolierten Test unverändert wiederhergestellt; die zuvor laufende portable Instanz wurde wieder geöffnet.

Der Test wartet auf den gesamten Installer-/Uninstaller-Prozessbaum, da Inno Uninstall einen temporären Kindprozess verwendet. Er überschreibt keine bereits registrierte Laytivo-Installation und ist auf einer Maschine ohne installierte Laytivo-Version auszuführen. Ergebnisse und Protokolle liegen ausschließlich im ignorierten Ordner `artifacts/installer-smoke-<ID>/`.

| Kriterium | Implementierung / Nachweis | Verbleibende manuelle Abnahme |
|---|---|---|
| AT-01 Multi-Monitor speichern | Auswahl, erneute Erfassung vor Save, JSON-Roundtrip; Desktop-Smoke auf vier Monitoren | Freie Auswahl mit fünf verschiedenen Drittanbieter-Apps |
| AT-02 Login-Restore | HKCU-Run-Eintrag mit `--startup`, Hintergrundstart, getrennte Restore-Option, Delay und Launcher implementiert | Echter Ab-/Anmelde- oder Neustarttest mit aktiviertem Autostart |
| AT-03 Manuell wiederherstellen | Tray-Aktion und UI teilen Restore-Service; reale Positions-/Zustandsprüfung mit eigenen Fenstern erfolgreich | Wiederherstellungsaktion aus dem Tray mit Drittanbieter-Apps |
| AT-04 Keine Doppelstarts | xUnit-Zuordnung/Launchtests; Desktop-Smoke startet fehlendes Testfenster und wiederholt Restore ohne zusätzliche Fenster | App-spezifische Browser-/IDE-Sitzungsstarts |
| AT-05 EXE fehlt | Fehler wird pro Eintrag gesammelt; xUnit-Dateifehler und Fortsetzung anderer Fenster erfolgreich | Optional deinstallierte reale Drittanbieter-App |
| AT-06 Monitor fehlt | Zuordnungs-/Geometrietests mit fehlendem Monitor, Profil unverändert; reale Platzierung auf vier vorhandenen Displays erfolgreich | Physischen Monitor entfernen / Dockingwechsel |
| AT-07 Autostart aus | `SetEnabled(false)` löscht eigenen HKCU-Run-Wert; Einstellungen speichern den Zustand | Nach Anmeldung prüfen, dass kein eigener Autostart stattfindet |
| AT-08 Tray / Schließen | Tray/Exit implementiert; UI-Schließen geprüft: Fenster unsichtbar, Prozess weiter aktiv | Tray-Menü öffnen, Fenster per Doppelklick zeigen und „Laytivo beenden“ wählen |
| AT-09 Programmstart deaktiviert | Automatisierter Test bestätigt: fehlende Einträge übersprungen, kein Launcher-Aufruf | Optional mit realem Profil |
| AT-10 Mehrfachaufruf | Semaphore mit sofortiger Ablehnung, UI-Busy-Sperre, Tests für Parallelaufruf und Freigabe nach Abbruch | Mehrfachklick während eines langsamen App-Starts |
| AT-11 Fensterzustand | Desktop-Smoke prüft echte Normal-/Maximiert-Zustände samt Normalgeometrie | Minimierte Drittanbieter-Fenster und spezielle App-Minimalgrößen |
| AT-12 Neues Profil | Explizites Speichern, atomarer Austausch, `.bak`, validierter Roundtrip automatisiert geprüft | Zwei unterschiedliche manuelle Layouts nacheinander speichern |

## Reproduzierbarer Gesamtlauf

```powershell
.\scripts\Build.ps1 -DesktopTests -Publish
```

- Formatter und .NET-Analyzer, Warnungen als Fehler.
- 43 Kernlogiktests und 13 Infrastrukturtests, einschließlich unsichtbarer Snap-Rahmen, DPI-/Monitor-Fallbacks, persistiertem Andockzustand, Rückwärtskompatibilität und Teil-Erfolgsmeldungen bei fehlender nativer Andockung.
- NuGet-Sicherheitsabgleich einschließlich transitiver Testabhängigkeiten.
- 16 native Desktop-Prüfungen in der beobachteten Vier-Monitor-Konfiguration; Anzahl variiert mit vorhandenen Monitoren.
- Ergebnisdatei `artifacts/desktop-smoke/result.json`; diese enthält die tatsächlich beobachteten Monitorgeometrien und bestandenen Prüfungen.
- UI-Sichtprüfung in separatem Datenordner `.tools/ui-smoke`; keine echte Benutzeranordnung gespeichert und kein Autostart aktiviert.
- Die fertige portable App startete ohne `DOTNET_ROOT` in `.tools/portable-smoke` mit `--startup` ohne sichtbares Hauptfenster. Eine zweite Instanz endete mit Exitcode 0; die erste blieb aktiv. Anschließend wurden ausschließlich die eigenen Testprozesse beendet. Dies prüft den Hintergrundstart und Single Instance, ersetzt aber keine echte Windows-Anmeldung.

## Regression: Windows-Snap speichern

Ein bereits per Windows angedocktes Fenster genügt für diesen zusätzlichen, rein lesenden Integrationstest:

```powershell
tests/Laytivo.DesktopSmoke/bin/Release/net10.0-windows/Laytivo.DesktopSmoke.exe --verify-snap-capture artifacts/snap-regression
```

Er sucht ein normal angezeigtes Fenster, dessen aktuelle `GetWindowRect`-Geometrie von seiner `GetWindowPlacement`-Wiederherstellungsposition abweicht. Danach durchläuft es die echte Erfassung und einen JSON-Speicher-/Ladezyklus in einem separaten Testordner. Außerdem prüft er den erkannten Andockzustand, die sichtbaren Rahmen und eine Wiederherstellung mit identischen Zielkoordinaten: Diese muss ohne Positionsänderung die vorhandene native Andockung erhalten. Keine Benutzerfenster werden absichtlich verschoben, geschlossen oder neu gestartet; das aktive Laytivo-Profil bleibt unverändert. Wenn kein solches Fenster vorhanden ist, schlägt der Test mit einer fehlenden Testvoraussetzung fehl.

Am 04.11.2026 mit einem vorhandenen Snap-Fenster reproduziert und geprüft: alte Normalgeometrie `3610,-524 / 1000×750`, aktuelle Snap-Geometrie `3432,-665 / 1456×1258`; gespeichert wurde exakt die aktuelle Snap-Geometrie. Die alte Implementierung hätte die falsche Normalgeometrie gespeichert. Ergebnisdatei: `artifacts/snap-regression/result.json`.

Der ursprüngliche Erfassungstest wurde zusätzlich mit der alten, unveränderten `Laytivo.Infrastructure.dll` ausgeführt: Er scheiterte erwartungsgemäß genau am Vergleich der gespeicherten Geometrie. Mit der ersten Snap-Korrektur bestanden seine fünf Prüfungen. Die neu ergänzte Prüfung der nativen Andockerhaltung ist noch offen: Im aktuellen Desktop sind keine angedockten Fenster mehr vorhanden. Normale/maximierte Fenster, die übrigen 16 Desktop-Prüfungen und alle 56 automatisierten Tests bestehen. Ein echtes Neuerzeugen beliebiger Snap-Gruppen ist nicht implementiert; bei neu gestarteten oder aus der Andockung gelösten Fenstern wird nur die Geometrie hergestellt und das Ergebnis als teilweise wiederhergestellt angezeigt.

## Manuelle Login-Abnahme

1. Portable Version an den endgültigen Ort kopieren und starten.
2. Ein kleines Profil mit unkritischen Testanwendungen speichern, eine normal und eine maximiert.
3. Alle drei Optionen aktivieren, 5 Sekunden Delay wählen und übernehmen.
4. Fenster schließen; Laytivo muss im Tray bleiben. Aus dem Tray wieder öffnen und dort explizit beenden.
5. Windows ab- und wieder anmelden. Laytivo muss ohne Hauptfenster starten; nach dem Delay sollen vorhandene Fenster positioniert und fehlende unterstützte Apps gestartet werden.
6. Ergebnis in den Einstellungen prüfen. Restore-Automatik deaktivieren, erneut anmelden: Laytivo läuft im Tray ohne Restore.
7. Windows-Autostart deaktivieren und erneut anmelden: Laytivo darf nicht über seinen Run-Eintrag starten.
8. Physische Monitor-/DPI-Änderungen separat testen und nachsehen, dass die Originalkonfiguration unverändert bleibt.

Die noch offenen Hardware-/Login-Prüfungen bedeuten, dass die vollständige manuelle MVP-Abnahme aus der Anforderungsdatei noch nicht abgeschlossen ist. Die betreffenden Funktionen sind implementiert; ihr Erfolg bei einem echten Login wird hier nicht behauptet.
