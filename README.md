# Laytivo

Laytivo speichert eine manuell eingerichtete Windows-Fensteranordnung und stellt sie auf Wunsch aus dem System-Tray oder nach der Anmeldung wieder her. Windows 11 x64, .NET 10 und WPF. Alle Daten bleiben lokal; keine Telemetrie, Cloudverbindung, Screenshots oder Erfassung von Fensterinhalten. Die Anwendung benötigt keine Administratorrechte.

## Direkt starten / installieren

Den Installer gibt es unter [GitHub Releases](https://github.com/1nd1v1d/Laytivo/releases). Das Repository ist privat; der Download setzt ein berechtigtes GitHub-Konto voraus. `Laytivo-Setup-0.1.0-x64.exe` installiert pro Benutzer ohne Administratorrechte, inklusive .NET-Laufzeit, Startmenü-Verknüpfungen und optionalem Desktop-Symbol. **Vor Installation, Update oder Deinstallation Laytivo über das Tray-Menü beenden.**

Die ausführliche [Installations- und Bedienungsanleitung](ANLEITUNG.html) wird mitinstalliert und ist auch als eigene Release-Datei verfügbar. Der Installer löscht bei der Deinstallation keine Profile oder Einstellungen. Die Vorschauversion ist nicht codesigniert; zu den Release-Dateien gehören SHA-256-Prüfsummen.

Die gebaute portable Version liegt in `artifacts/Laytivo/`. `Laytivo.exe` direkt öffnen oder im Projekt `Start-Laytivo.ps1` ausführen. Die portable Version enthält die .NET-Laufzeit; eine separate .NET-Installation ist dafür nicht erforderlich.

Alternativ den **gesamten Ordner** aus der portablen ZIP-Datei an einen dauerhaften Ort entpacken und dort `Laytivo.exe` öffnen. Vor Aktivierung des Autostarts an den endgültigen Ort verschieben. Installer und portable Version verwenden denselben separaten Datenordner. Nach einem Wechsel von portabel zu installiert die Autostart-Einstellung erneut übernehmen, damit der Eintrag auf die installierte EXE zeigt. Es gibt keinen Dienst und keine automatische Rechteerhöhung.

1. Gewünschte Programme öffnen und ihre Fenster mit Windows positionieren.
2. In Laytivo die Fensterliste aktualisieren und die vorausgewählte Liste prüfen. Ungewünschte Fenster abwählen.
3. Optional unter „Startinformationen“ Dokumentargumente und Arbeitsverzeichnis eintragen. Laufende Prozesskommandozeilen werden nicht ausgelesen. Browser-Tabs oder IDE-Projekte werden nicht automatisch rekonstruiert.
4. „Auswahl als Anordnung speichern“ wählen. Diese ausdrückliche Aktion ersetzt das Standardprofil. Ein vorheriger Konfigurationsstand bleibt als `.bak` erhalten.
5. „Gespeicherte Anordnung wiederherstellen“ ausprobieren. Im Status stehen Ergebnis und Fehler pro Fenster.
6. Bei Bedarf Einstellungen ändern und „Einstellungen übernehmen“ wählen.

**Schließen des Fensters verbirgt nur die Oberfläche.** Im Windows-Tray bleibt Laytivo erreichbar; gegebenenfalls unter dem Pfeil für ausgeblendete Symbole. Rechtsklick bietet Wiederherstellen, Speichern (öffnet die Fensterauswahl), Einstellungen, zwei direkt wirksame Optionen und „Laytivo beenden“. Doppelklick öffnet die Einstellungen. Ein zweiter Laytivo-Prozess beendet sich ohne einen weiteren Restore.

## Einstellungen und Windows-Anmeldung

| Einstellung | Standard | Wirkung |
|---|---|---|
| Mit Windows starten | Aus | Benutzerbezogener Eintrag `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Laytivo` mit `"…\Laytivo.exe" --startup` |
| Nach Anmeldung wiederherstellen | Ein | Wiederherstellung nur bei Start mit `--startup`; ein normaler manueller App-Start verschiebt keine Fenster |
| Fehlende Programme starten | Ein | Startet unterstützte fehlende EXEs; bei Aus werden nur offene passende Fenster verwendet |
| Startverzögerung | 5 Sekunden | Auswahl 0 / 5 / 15; nur für die automatische Wiederherstellung |

„Mit Windows starten“ wird tatsächlich im Betriebssystem umgesetzt. Der aktuelle eigene Run-Eintrag wird beim Start gelesen. Falls Windows den Autostart zusätzlich in seinen Einstellungen deaktiviert, muss er dort wieder freigegeben werden. Ein manuelles Wiederherstellen funktioniert unabhängig von beiden Autostart-Optionen. Ohne gespeichertes Profil startet Laytivo keine Programme. `--startup` öffnet kein Hauptfenster und keine blockierenden Dialoge.

## Build und Prüfungen

Voraussetzung für die Entwicklung: Windows und [.NET-10-SDK von Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). `global.json` akzeptiert aktuelle stabile 10.0-SDK-Featurebänder ab 10.0.100. In dieser Arbeitskopie wurde das SDK zusätzlich isoliert in `.tools/dotnet` eingerichtet. Das Buildskript verwendet es, falls vorhanden, sonst das installierte SDK.

```powershell
.\scripts\Build.ps1
# Zusätzlich echte Desktop-Prüfung und portable Distribution:
.\scripts\Build.ps1 -DesktopTests -Publish
# Anwendung starten:
.\Start-Laytivo.ps1
```

Installer und Release-Dateien reproduzierbar bauen:

```powershell
# Richtet den offiziellen, SHA-256- und signaturgeprüften Inno-Setup-7.1.1-Compiler
# nur unter .tools/inno ein, ohne systemweite Installation oder Dateizuordnungen.
.\scripts\Install-InnoSetup.ps1
.\scripts\Build-Installer.ps1
# Optional: vollständiger Installer-Test, wenn noch keine Laytivo-Version installiert ist.
.\scripts\Test-Installer.ps1
```

`Build-Installer.ps1` führt standardmäßig alle bestehenden Qualitätsprüfungen aus und erzeugt unter `artifacts/installer/` den Setup-Installer, eine portable ZIP, `ANLEITUNG.html` und `SHA256SUMS.txt`. Die Version wird aus `Directory.Build.props` gelesen. `-SkipChecks` ist nur für einen bereits geprüften unveränderten Stand vorgesehen; `-CompilerPath` erlaubt einen vorhandenen Inno-Setup-7-Compiler. `Test-Installer.ps1` prüft unter Windows anschließend die echte Installation, Aktualisierung und Deinstallation in einem isolierten Zielordner. Eine bereits registrierte Laytivo-Installation wird dabei nicht überschrieben.

Die Pipeline führt Restore, `dotnet format --verify-no-changes`, Compiler/.NET-Analyzer mit Warnungen als Fehlern, xUnit-Tests und NuGet-Sicherheitsabgleich aus. Es werden keine konkurrierenden Analysewerkzeuge installiert; Semgrep ist hier nicht vorhanden. Paket- und Sicherheitsabgleiche benötigen Netzwerk, die fertige Anwendung arbeitet vollständig offline. Nur die Testprojekte verwenden NuGet-Testpakete; die Anwendung hat keine Drittanbieter-Laufzeitabhängigkeiten. Der letzte NuGet-Abgleich meldete keine bekannten anfälligen direkten oder transitiven Pakete.

Die optionale Desktop-Prüfung öffnet zwei **eigene** temporäre Fenster, prüft Normal-/Maximiert-Zustand, Speicherung, reale Platzierung auf jedem angeschlossenen Monitor sowie Programmstart und Doppelstartvermeidung. Andere Fenster werden dabei nicht verschoben oder geschlossen. Ergebnisse: `artifacts/desktop-smoke/result.json`. Nur in einer interaktiven, entsperrten Windows-Sitzung ausführen.

Plattformunabhängige Kernlogik allein testen:

```powershell
dotnet test tests/Laytivo.Tests -c Release
```

## Architektur

- `src/Laytivo.Core`: unveränderliche Datenmodelle, Validierung, Auswahl/Erfassung, Fensterzuordnung, Geometrie und Restore-Orchestrierung; ohne Windows-Abhängigkeit.
- `src/Laytivo.Infrastructure`: Win32/PInvoke, Prozessstart, HKCU-Autostart, atomarer JSON-Store und begrenzte strukturierte Logs.
- `src/Laytivo.App`: WPF/MVVM mit expliziter Komposition der Dienste, Windows-Forms-NotifyIcon und benutzerbezogenem Single-Instance-Mutex. Kein zusätzlicher DI-Container oder Tray-Anbieter.
- `tests/Laytivo.Tests`: Kernlogik und Ablauf mit kontrollierten Abhängigkeiten.
- `tests/Laytivo.Infrastructure.Tests`: Persistenz, Validierung, Archivierung, Log-Aufbewahrung und Startvalidierung.
- `tests/Laytivo.DesktopSmoke`: ausführbare Integration mit ausschließlich eigenen Testfenstern.

## Wiederherstellungsverhalten

Die Zuordnung bevorzugt EXE-Pfad + Fensterklasse + exakten Titel. Bei geändertem Dokumenttitel bleibt Pfad + Klasse nutzbar. Bei nicht zugreifbarem Pfad sind Titel, Klasse und Prozessname gemeinsam erforderlich. Ein bekannt anderer EXE-Pfad wird nicht zugeordnet. Fenster-Handle und PID dienen nur zur Laufzeit; jedes Fenster wird höchstens einem Profileintrag zugewiesen.

Fehlende Programme werden mit `UseShellExecute=false` und gespeichertem Arbeitsverzeichnis gestartet. Ohne dokumentbezogene Argumente gibt es höchstens einen Start pro EXE; vorhandene App-Fenster verhindern einen blinden Zweitstart. Mit explizit verschiedenen Argumenten ist je Argumentkombination ein Start möglich. Alle fehlenden Fenster teilen sich eine begrenzte Wartephase von **20 Sekunden** mit **500 ms** Prüfabstand. Vorhandene Fenster werden vorher positioniert. Jede gefundene Gruppe erhält zwei Platzierungsdurchläufe mit **750 ms** Abstand; jeder native Aufruf prüft bis zu **400 ms**, ob die Anwendung die Position angenommen hat. Fehler pro Fenster werden gesammelt, die übrigen Fenster werden weiter bearbeitet. Im Leerlauf findet kein Fensterpolling statt.

Die App ist PerMonitorV2-DPI-aware und speichert physische Desktopkoordinaten. `WINDOWPLACEMENT`-Arbeitsbereichskoordinaten werden um Taskleistenversatz korrigiert. Maximierung bleibt ein echter Fensterzustand. Minimierte Fenster werden anhand ihrer normalen Wiederherstellungsgeometrie gespeichert und normal bzw. zuvor maximiert wiederhergestellt; sie starten nicht minimiert.

Bei normal angezeigten Fenstern, einschließlich Windows-Snap und Snap-Layouts, wird die **aktuelle Bildschirmgeometrie** mit `GetWindowRect` gespeichert. `GetWindowPlacement` kann bei Snap noch die Position vor dem Andocken enthalten; diese wird nur für minimierte/maximierte Fenster als Normalgeometrie verwendet. Nach einem Update von der ursprünglichen Version müssen bereits falsch erfasste Snap-Anordnungen ausdrücklich erneut gespeichert werden.

**Windows-Andockung:** Neu gespeicherte Profile enthalten zusätzlich den mit `IsWindowArranged` erkannten Andockzustand und die sichtbaren Rahmenabmessungen. Dadurch werden unsichtbare Fensterrahmen an Bildschirmkanten nicht als sichtbare Lücken nach innen verschoben. Bereits korrekt positionierte Fenster bleiben unverändert. Bei einem noch angedockten normalen Fenster verwendet Laytivo keinen Aufruf mehr, der die native Andockung unnötig aufhebt. Wenn eine Anwendung beendet oder das Fenster bereits aus der Andockung gelöst wurde, stellt Laytivo weiterhin die Geometrie her; eine beliebige native Snap-Gruppe wird **nicht neu erzeugt**. Das Ergebnis lautet in diesem Fall ausdrücklich „teilweise wiederhergestellt“. Bestehende Profile bleiben lesbar, müssen aber erneut gespeichert werden, um diese zusätzlichen Snap-Informationen zu enthalten. Die Dokumentation von [`IsWindowArranged`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindowarranged) beschreibt die Andockung als eigenständigen Fensterzustand. Die verwendeten dokumentierten Win32-Platzierungsfunktionen bieten keinen Setter zum Wiederaufbau einer beliebigen Windows-Snap-Gruppe.

Die Monitorzuordnung verwendet Geräte-Interface-IDs, danach Gerätenamen und schließlich den primären/ersten aktiven Monitor. Relative Positionen passen sich dem Arbeitsbereich an; die Fenstergröße berücksichtigt DPI-Änderungen. Fenster werden vollständig in den Zielarbeitsbereich begrenzt. Wiederherstellung ändert niemals das gespeicherte Profil.

## Daten, Updates und Deinstallation

Standardverzeichnis: `%LocalAppData%\Laytivo\`.

- `configuration.json`: versioniertes Format, derzeit Schema 1, mit Einstellungen und einem Standardprofil. Speichert Titel, App-Pfade, optionale Argumente und Fenster-/Monitormerkmale; diese Metadaten können persönliche Dokumentnamen enthalten.
- `configuration.json.bak`: vorheriger atomar ersetzter Konfigurationsstand.
- `logs/laytivo.jsonl` und bis zu vier Rotationen: strukturierte lokale Diagnose, Rotation ab ungefähr 1 MiB (ein zusätzlicher Eintrag kann die Grenze überschreiten).
- Ungültige/inkompatible Konfigurationen werden gemeldet; Speichern/Restore bleiben gesperrt. Der explizite UI-Befehl archiviert die Originaldatei als `.invalid-…` und erlaubt einen Neuanfang. Keine automatische Rückmigration oder stille Überschreibung.

Updates: Laytivo über das Tray beenden und den neuen Installer mit demselben Zielordner ausführen; bei portabler Nutzung stattdessen den Programmordner ersetzen. Nutzerdaten liegen außerhalb des Programmordners. Bei Änderung des Installationsorts die Autostart-Einstellung erneut übernehmen. Installierte Versionen über Windows „Installierte Apps“ deinstallieren. Der Uninstaller entfernt nur einen exakt zur installierten EXE gehörenden Run-Eintrag und lässt Benutzerprofile erhalten. Portable Versionen nach Deaktivierung ihres Autostarts und Beenden durch Entfernen des Programmordners löschen. Nutzerdaten können bei Bedarf separat entfernt werden.

Für isolierte Entwicklungs-/UI-Prüfungen: `Laytivo.exe --data-directory "C:\Pfad\zu\Testdaten"`. Der Windows-Autostart gehört weiterhin zum Benutzerkonto und wird durch diesen Parameter nicht isoliert; dort keine Autostart-Änderung für einen Test vornehmen.

## Grenzen und Teststand

**Geprüft am 04.11.2026:** 56 automatisierte Tests, Release-Build ohne Warnungen, realer Desktop-Smoke mit 16 Prüfungen auf vier Monitoren bei 125 % Skalierung, einschließlich links/oberhalb liegender Displays. Die WPF-Oberfläche wurde geöffnet und visuell geprüft; Schließen blendete sie aus und ließ den Prozess laufen. Die portable Version wurde ohne `DOTNET_ROOT` gestartet: `--startup` blieb ohne sichtbares Hauptfenster, eine zweite Instanz beendete sich erfolgreich und ließ die erste weiterlaufen. Der neue native Snap-Erhaltungstest konnte mangels aktuell angedockten Fensters noch nicht ausgeführt werden. Der genaue Abnahmestand und die noch manuellen Prüfungen stehen in [TESTING.md](TESTING.md).

Ein echter Windows-Neustart/Login, Abziehen eines physischen Monitors, gemischte reale DPI-Skalierungen und alle Sonderanwendungen wurden nicht abschließend getestet. Diese Unterschiede sind durch deterministische Kernlogiktests teilweise abgedeckt, ersetzen aber keine Hardware-/Anmeldetests.

Store-/UWP-Apps und bekannte Hilfsprozesse sind als nicht automatisch startbar markiert; ihre vorhandenen Fenster können häufig positioniert werden. Erhöhte oder von der App selbst kontrollierte Fenster können Zugriffe verweigern. Identische Fenster mit identischen Titeln/Klassen können nicht eindeutig unterschieden werden. Browser-/IDE-Sitzungen, geschlossene Dokumente und appinterne Zustände werden nur soweit möglich über explizite Argumente rekonstruiert. Es gibt keinen allgemeinen Adapter für die Wiederherstellung solcher Inhalte. Monitor-IDs können sich durch Hardware-/Treiberänderungen ändern; Fallbacks halten die Fenster sichtbar. Keine garantierte Unterstützung für andere virtuelle Windows-Desktops.

Mehrere Profile, Hotkeys, Rastereditor und permanente Live-Regeln sind nicht Bestandteil dieses MVP. Die Anwendung und ZIP sind lokal gebaut und nicht codesigniert. Vor öffentlicher Veröffentlichung sind die in den Anforderungen erwähnten Namens-/Markenfragen separat zu klären.
