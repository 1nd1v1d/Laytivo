# Laytivo – Produktanforderungen und technische Spezifikation

**Version:** 1.1 (MVP-Spezifikation; Produktname aktualisiert)  
**Stand:** 04.11.2026  
**Status:** Entwicklungsgrundlage für Codex  
**Zielplattform:** Windows 11 (x64)  
**Produktname:** Laytivo (vor öffentlicher Veröffentlichung Domain- und Markenverfügbarkeit verbindlich prüfen)

## 1. Ziel und Produktvision

Laytivo ist eine schlanke, **lokal laufende Windows-Desktop-Anwendung**, die die **vom Benutzer manuell eingerichtete Fensteranordnung auf einem oder mehreren Monitoren speichert und bei Bedarf wiederherstellt**.

Auf Wunsch startet Laytivo **nach der Windows-Benutzeranmeldung** automatisch im Hintergrund, startet die im gespeicherten Layout benötigten Programme und stellt deren Fenster auf den vorgesehenen Monitoren mit gespeicherter Position, Größe und Fensterzustand wieder her.

Die App ist dauerhaft über ein **Symbol im Windows-Benachrichtigungsbereich (System-Tray neben der Uhr)** erreichbar. Über das Tray-Menü kann die gespeicherte Anordnung jederzeit erneut hergestellt werden.

### 1.1 Zentrales Bedienprinzip

1. Benutzer öffnet die gewünschten Anwendungen.
2. Benutzer positioniert und dimensioniert die Fenster **manuell mit Windows** auf seinen Monitoren.
3. Benutzer wählt **„Aktuelle Anordnung speichern“**.
4. Laytivo merkt sich die zugehörigen Anwendungen, Fenstermerkmale, Monitorzuordnungen, Positionen, Größen und Fensterzustände.
5. Bei Windows-Anmeldung oder manueller Auslösung startet Laytivo fehlende Anwendungen und stellt die gespeicherte Anordnung wieder her.

**Wichtig:** Laytivo erstellt im MVP **keine Fensterlayouts per Drag-and-Drop-Editor**, teilt den Bildschirm nicht automatisch in Raster auf und verschiebt Fenster nicht dauerhaft gegen den Willen des Benutzers. Die vom Benutzer erzeugte Anordnung ist die maßgebliche Quelle.

## 2. Zielgruppe und Anwendungsszenarien

- Benutzer mit einem oder mehreren Monitoren und wiederkehrender Arbeitsumgebung.
- Entwickler, die z. B. IDE, Browser, Terminal und Kommunikationssoftware immer gleich angeordnet haben möchten.
- Benutzer, deren Fenster nach Neustarts oder manuellem Verschieben wieder in die gespeicherte Anordnung gebracht werden sollen.

### 2.1 Hauptszenarien

**S1 – Erstmaliges Speichern:** Der Benutzer öffnet seine Programme, richtet alle Fenster manuell ein und speichert die aktuelle Anordnung.

**S2 – Windows-Autostart:** Nach der Benutzeranmeldung startet Laytivo minimiert im Tray, startet fehlende gespeicherte Programme, wartet auf die zugehörigen Fenster und positioniert diese.

**S3 – Manuelle Wiederherstellung:** Der Benutzer hat Fenster verschoben oder geschlossen und wählt im Tray **„Gespeicherte Anordnung wiederherstellen“**. Offene Fenster werden repositioniert; fehlende Programme/Fenster werden – soweit möglich – gestartet.

**S4 – Aktualisieren der Anordnung:** Der Benutzer verändert Positionen oder Fenstergrößen und überschreibt auf ausdrückliche Aktion den gespeicherten Zustand.

**S5 – Abweichende Monitorumgebung:** Ein Zielmonitor ist beim Wiederherstellen nicht angeschlossen. Laytivo sorgt dafür, dass Fenster sichtbar bleiben, und verändert die gespeicherte Originalanordnung nicht.

## 3. MVP-Umfang (verbindlich)

Alle Anforderungen in diesem Abschnitt gehören zum **MVP**. Die Kennungen dienen der Implementierung und Testbarkeit.

### 3.1 Monitore und Fenster erfassen

**FR-001 – Monitore erkennen**
- Laytivo erkennt alle aktiven Monitore, deren Auflösung, Arbeitsbereich (ohne Taskleiste), Skalierung, relative Position im virtuellen Desktop und eine möglichst stabile Monitoridentifikation.
- Negative Koordinaten (Monitore links/oberhalb des primären Displays) müssen unterstützt werden.
- Die Anordnung muss für unterschiedliche Auflösungen und DPI-Skalierungen funktionieren.

**FR-002 – Relevante Fenster erkennen**
- Laytivo kann die aktuell geöffneten, für Benutzer sichtbaren Top-Level-Fenster auflisten.
- Zu jedem Fenster werden mindestens Fenstertitel, Prozess-/Executable-Pfad soweit zugreifbar, Prozesskennung, Fenster-Handle zur Laufzeit, Fensterzustand und Geometrie erfasst.
- Nicht verwaltbare oder irrelevante Fenster (z. B. Systemfenster, versteckte Hilfsfenster, Tray-Pop-ups, flüchtige Dialogfenster) werden standardmäßig ausgeschlossen.
- Es muss für den Benutzer nachvollziehbar sein, **welche Fenster** gespeichert werden. Dafür genügt im MVP eine einfache Auswahlliste/Bestätigungsansicht mit standardmäßig vorausgewählten geeigneten Fenstern.

**FR-003 – Aktuelle Anordnung speichern**
- Der Benutzer positioniert und skaliert die Fenster selbst; anschließend speichert Laytivo den Zustand per Menüaktion.
- Gespeichert werden je Fenster: Zuordnung zur Anwendung, notwendige Startinformation, Erkennungsmerkmale, Monitoridentifikation, Position, Größe sowie normaler/maximierter Zustand.
- Für normal angeordnete Fenster werden die Geometrie und der Zielmonitor erfasst; für maximierte Fenster wird der Maximierungszustand gespeichert (nicht lediglich eine Bildschirmfüllung als Pixelgröße simuliert).
- Die erste erfolgreiche Speicherung erstellt das Standardprofil. Eine erneute Speicherung überschreibt es nur nach expliziter Aktion.
- Bei fehlenden Startinformationen wird das Fenster klar gekennzeichnet: Position kann ggf. gespeichert werden, der automatische Start ist aber nicht garantiert.

### 3.2 Wiederherstellen und Programmstart

**FR-004 – Gespeicherte Anordnung wiederherstellen**
- Laytivo stellt Fenster gemäß gespeicherten Monitoren, Positionen, Größen und Zuständen wieder her.
- Bereits geöffnete, eindeutig zugeordnete Fenster werden **wiederverwendet**, nicht dupliziert.
- Fenster dürfen nach der Wiederherstellung nicht vollständig außerhalb des sichtbaren Bereichs liegen.
- Ein erneuter Aufruf der Wiederherstellung soll bei unveränderter Umgebung zum gleichen Ergebnis führen (**idempotentes Verhalten**).

**FR-005 – Fehlende Programme starten**
- Für gespeicherte, noch nicht verfügbare Anwendungen startet Laytivo die ausführbare Datei mit ggf. gespeicherten Argumenten und geeignetem Arbeitsverzeichnis.
- Ein Programm wird nicht allein anhand eines Prozessnamens als „korrekt geöffnet“ betrachtet: Die Fensterzuordnung muss geprüft werden.
- Bei bereits geöffneten geeigneten Fenstern darf kein unnötiger zweiter Start erfolgen.
- Die Funktion ist über eine globale Einstellung ein-/ausschaltbar.
- Mehrere Fenster derselben Anwendung müssen möglichst zuverlässig unterschieden werden (z. B. Fenstertitel, Klassenname, Prozesspfad, Fenstermerkmale). **Einschränkung:** Das gezielte Wiederöffnen mehrerer eigenständiger Dokumente/Browser- oder IDE-Fenster ist nur möglich, wenn die Anwendung passende Startargumente oder eine Wiederherstellungsfunktion anbietet. Laytivo darf dies nicht pauschal garantieren.

**FR-006 – Auf Fensterstart warten und Positionierung wiederholen**
- Laytivo wartet nach Programmstart ereignisbasiert oder durch zeitlich begrenzte Prüfung auf das tatsächliche Fenster.
- Die Anwendung darf den Start nicht blockieren und soll mit parallel startenden Programmen zurechtkommen.
- Einstellbares Start-Delay nach Anmeldung (MVP: 0, 5 oder 15 Sekunden; Standard: 5 Sekunden).
- Für langsam startende Programme gibt es ein dokumentiertes Timeout mit begrenzten Wiederholungsversuchen; Fehler bei einzelnen Fenstern blockieren nicht den gesamten Ablauf.
- Falls eine Anwendung ihre Fensterposition kurz nach dem Start selbst verändert, darf Laytivo die Zielposition im begrenzten Wiederherstellungszeitraum erneut setzen.

**FR-007 – Fehlende/wechselnde Monitore behandeln**
- Wenn ein gespeicherter Monitor fehlt, werden betroffene Fenster auf einen vorhandenen Monitor innerhalb des sichtbaren Arbeitsbereichs platziert.
- Die gespeicherten Originalkoordinaten und Monitorzuordnungen bleiben unverändert.
- Bei veränderter Auflösung/Skalierung werden Positionen sinnvoll angepasst bzw. auf den sichtbaren Bereich begrenzt.
- Keine unendlichen Repositionierungsversuche.

### 3.3 Windows-Autostart und Hintergrundbetrieb

**FR-008 – Laytivo mit Windows starten**
- Aktivierbare Einstellung **„Laytivo mit Windows starten“**.
- Technisch erfolgt der Start **nach der interaktiven Benutzeranmeldung**, nicht als Windows-Dienst vor der Anmeldung.
- Laytivo startet dabei ohne störendes Hauptfenster direkt in den Hintergrund/System-Tray.
- Die Einstellung ist persistent und kann jederzeit deaktiviert werden.
- Ein zweiter gleichzeitiger Laytivo-Prozess soll vermieden oder kontrolliert abgefangen werden (Single Instance).

**FR-009 – Automatische Wiederherstellung nach Anmeldung**
- Separate Einstellung **„Gespeicherte Anordnung nach Anmeldung wiederherstellen“**.
- Wenn aktiviert, führt Laytivo nach dem Start und der konfigurierten Verzögerung den Wiederherstellungsablauf aus.
- Die Einstellung **„Fehlende Programme automatisch starten“** steuert, ob dabei auch nicht laufende Apps gestartet werden.
- Sind noch keine Fenster gespeichert, erfolgt kein Programmstart; Laytivo bleibt bedienbar im Tray.

**FR-010 – System-Tray-Integration**
- Laytivo zeigt während des Hintergrundbetriebs ein Tray-Icon im Windows-Benachrichtigungsbereich.
- Rechtsklick öffnet ein Kontextmenü mit mindestens:
  1. **Gespeicherte Anordnung wiederherstellen**
  2. **Aktuelle Anordnung speichern**
  3. **Einstellungen öffnen**
  4. **Mit Windows starten** (umschaltbar)
  5. **Programme automatisch starten** (umschaltbar)
  6. **Laytivo beenden**
- Der Zustand der Optionen ist im Menü erkennbar.
- Das Schließen des Einstellungsfensters beendet Laytivo **nicht**; nur **„Laytivo beenden“** beendet den Hintergrundprozess.
- Links- bzw. Doppelklick darf optional das Einstellungsfenster öffnen.

**FR-011 – Manuelles Wiederherstellen jederzeit**
- Die Tray-Aktion startet unabhängig vom Windows-Autostart eine Wiederherstellung.
- Ein bereits laufender Wiederherstellungsvorgang wird nicht parallel doppelt ausgeführt.
- Bereits geöffnete Fenster werden an die gespeicherte Position/Größe zurückgesetzt.
- Nicht mehr geöffnete Anwendungen werden nur gestartet, wenn die entsprechende Option aktiviert ist.
- Der Vorgang gibt verständliches Feedback über Erfolg, übersprungene und fehlgeschlagene Fenster.

### 3.4 Oberfläche und Einstellungen

**FR-012 – Schlanke Benutzeroberfläche**
- Minimale Konfigurationsansicht statt komplexem Layout-Editor.
- Anzeigen: gespeichertes Standardprofil, enthaltene Fenster/Apps, Zuordnung zu Monitoren, letzter Speichervorgang und ggf. letzte Wiederherstellung.
- Aktionen: speichern, wiederherstellen, Einstellungen öffnen und ändern.
- Bei erster Nutzung klare Anleitung: **Programme öffnen → Fenster manuell positionieren → speichern**.

**FR-013 – Persistente Einstellungen**
- Mindestens folgende Einstellungen:
  - Laytivo mit Windows starten (Ein/Aus)
  - Anordnung nach Anmeldung wiederherstellen (Ein/Aus)
  - Fehlende Programme automatisch starten (Ein/Aus)
  - Startverzögerung: 0 / 5 / 15 Sekunden (Standard 5)
- Einstellungen überleben Neustart und Update.
- Änderungen an Autostart-Einstellungen werden tatsächlich im Betriebssystem wirksam, nicht nur intern gespeichert.

**FR-014 – Fehlerbehandlung und Status**
- Fehlende Executables, verweigerter Zugriff, nicht positionierbare Fenster und Zeitüberschreitungen führen nicht zum Absturz.
- Nicht erfolgreiche Fenster werden pro Wiederherstellung protokolliert; die übrigen Fenster werden weiter verarbeitet.
- Eine kurze Statusanzeige oder ein Protokoll in den Einstellungen nennt Ursache und betroffene Anwendung.
- Keine modalen Dialoge beim automatischen Windows-Start, die den Benutzer blockieren.

## 4. Nichtfunktionale Anforderungen

| ID | Anforderung | Erwartung |
|---|---|---|
| NFR-001 | Plattform | Windows 11 x64; Unterstützung aktueller DPI-/Multi-Monitor-Konfigurationen |
| NFR-002 | Performance | Geringe Leerlauf-CPU-Auslastung, kein dauerhaftes intensives Polling |
| NFR-003 | Bedienbarkeit | Einmal manuell einrichten, danach per Tray oder Autostart bedienen |
| NFR-004 | Offlinefähigkeit | Vollständig offline und lokal nutzbar |
| NFR-005 | Datenschutz | Keine Cloudpflicht, kein Telemetriezwang; keine Sammlung von Fensterinhalten oder Screenshots |
| NFR-006 | Sicherheit | Im Normalfall ohne Administratorrechte; keine automatischen Privilegienerhöhungen |
| NFR-007 | Zuverlässigkeit | Keine Fenster außerhalb sichtbarer Monitore, kein unkontrolliertes Duplizieren, keine Endlosschleifen |
| NFR-008 | Wartbarkeit | Klare Trennung zwischen UI, Windows-Interop, Erfassung, Persistenz, Start und Wiederherstellung |
| NFR-009 | Logging | Lokales strukturiertes Log, sparsam und mit begrenzter Aufbewahrung |
| NFR-010 | Installation | Einfach startbar/installierbar; Tray-Autostart pro Benutzer ohne Adminrechte, sofern möglich |

## 5. Technische Empfehlung

### 5.1 Tech-Stack

- **C# mit .NET 10**
- **WPF** für die Konfigurationsoberfläche (MVVM-Muster)
- Windows-spezifischer Interop-Layer für Fenster-/Monitoroperationen (Win32/PInvoke)
- **JSON** als lokale Konfigurationsdatei im Benutzerprofil, z. B. `%LocalAppData%\Laytivo\`
- Tray-Icon über geeignete Windows-/WPF-kompatible Implementierung
- Unittests für Koordinatenberechnung, Zuordnung, Konfigurationsmigration und Wiederherstellungsplanung

**Hinweis:** Die Auswahl einer Drittanbieterbibliothek für das Tray-Icon bleibt Implementierungsentscheidung. Bevorzugt wenige, gepflegte Abhängigkeiten.

### 5.2 Relevante Windows-APIs (Orientierung)

- `EnumDisplayMonitors`, `GetMonitorInfo` – Monitore und Arbeitsbereiche
- `EnumWindows`, `IsWindowVisible` – Fensterenumeration
- `GetWindowThreadProcessId` – Prozesszuordnung
- `GetWindowRect`, `GetWindowPlacement` – Fenstergeometrie/-zustand
- `SetWindowPos`, `SetWindowPlacement`, ggf. `ShowWindow` – Wiederherstellen
- `MonitorFromWindow` – Monitorzuordnung
- `SetWinEventHook` – Fensterereignisse beobachten
- DPI-bezogene Win32-Funktionen – korrekte Skalierungs-/Koordinatenbehandlung

Der Interop-Layer muss die Win32-Eigenheiten berücksichtigen: Rahmenkoordinaten vs. sichtbare Fenstergrenzen, maximierte Fenster, DPI-Awareness, Prozessrechte und Sonderfenster. **Prozess-ID und Fenster-Handle sind nicht dauerhaft stabil und dürfen nicht als alleinige persistente Identifikatoren dienen.**

### 5.3 Architekturvorschlag

```text
Laytivo/
├── src/
│   ├── Laytivo.App/                  # WPF UI, Settings, Tray, DI
│   ├── Laytivo.Core/                 # Domainmodelle, Restore-Plan, Regeln
│   └── Laytivo.Infrastructure/       # Win32, Prozessstart, Autostart, JSON
├── tests/
│   ├── Laytivo.Core.Tests/
│   └── Laytivo.Infrastructure.Tests/
├── README.md
└── Laytivo.sln
```

Mögliche Komponenten:

- `MonitorService` – Monitorinventar und Identifikationen
- `WindowDiscoveryService` – verwaltbare Fenster erfassen
- `WorkspaceCaptureService` – momentane Anordnung speichern
- `WindowMatchingService` – gespeicherte Einträge zu laufenden Fenstern zuordnen
- `ApplicationLauncher` – fehlende Apps starten
- `WorkspaceRestoreService` – Restore-Ablauf orchestrieren
- `WindowPlacementService` – Fensterposition/-zustand anwenden
- `AutostartService` – Laytivo-Autostart pro Benutzer konfigurieren
- `ConfigurationStore` – persistente und versionierte Konfiguration
- `TrayService` – Tray-Menü und Aktionen

## 6. Persistenz und Datenmodell

MVP: **ein aktives Standardprofil**. Datenformat muss versionierbar und um mehrere Profile erweiterbar sein.

### 6.1 Beispielhafte Struktur (schematisch)

```json
{
  "schemaVersion": 1,
  "settings": {
    "startWithWindows": true,
    "restoreAfterLogin": true,
    "launchMissingApps": true,
    "startupDelaySeconds": 5
  },
  "workspace": {
    "id": "default",
    "name": "Standard-Desktop",
    "savedAtUtc": "2026-11-04T08:00:00Z",
    "monitors": [
      {
        "id": "stable-monitor-identifier",
        "deviceName": "DISPLAY1",
        "bounds": { "x": 0, "y": 0, "width": 2560, "height": 1440 },
        "workArea": { "x": 0, "y": 0, "width": 2560, "height": 1400 },
        "dpiScale": 1.0
      }
    ],
    "windows": [
      {
        "id": "window-entry-1",
        "processPath": "C:\\Program Files\\Example\\Example.exe",
        "launchArguments": "",
        "workingDirectory": "C:\\Program Files\\Example",
        "titlePattern": "Example",
        "windowClass": "ExampleWindowClass",
        "monitorId": "stable-monitor-identifier",
        "normalBounds": { "x": 100, "y": 80, "width": 1200, "height": 900 },
        "state": "Normal",
        "autoLaunchSupported": true
      }
    ]
  }
}
```

Dies ist ein **Datenmodellvorschlag**, kein vorgeschriebener finaler Codevertrag. `titlePattern` darf sich bei Dokumentwechsel ändern; die Zuordnung muss mehrstufig und fehlertolerant sein. Monitor-IDs können abhängig von Hardware/Windows-API instabil sein; Implementierung muss Fallbacks vorsehen.

### 6.2 Speicherregeln

- Konfiguration atomar speichern (temporäre Datei + Austausch), um Datenverlust bei Abstürzen zu reduzieren.
- Fehlerhafte oder inkompatible Konfigurationen erkennen und nachvollziehbar melden.
- Keine unkontrollierte Überschreibung des Layouts beim Wiederherstellen.
- Vor einer expliziten Neuspeicherung darf optional eine Sicherung der bisherigen Datei angelegt werden.

## 7. Wiederherstellungsalgorithmus (Soll-Ablauf)

1. Konfiguration laden und validieren.
2. Aktuelle Monitore inklusive DPI und Arbeitsbereichen erkennen.
3. Bereits vorhandene verwaltbare Fenster erfassen.
4. Gespeicherte Fenster mit laufenden Fenstern abgleichen; Mehrfachzuordnungen vermeiden.
5. Wenn `launchMissingApps=true`, fehlende und startbare Anwendungen starten.
6. Auf zugehörige Fenster warten (begrenzte Wartezeit und Retry-Logik).
7. Zielmonitor bestimmen; bei Abwesenheit einen sichtbaren Fallback auswählen.
8. Gespeicherte Bounds bei geänderter Bildschirmumgebung anpassen und auf sichtbaren Bereich begrenzen.
9. Fensterzustand und Position/Größe in geeigneter Reihenfolge setzen; ggf. innerhalb eines begrenzten Zeitraums erneut anwenden.
10. Ergebnisse pro Fenstereintrag protokollieren und im UI als Zusammenfassung anzeigen.

**Nebenläufigkeit:** Nur ein Restore-Vorgang gleichzeitig. Beim manuellen erneuten Auslösen während eines laufenden Vorgangs entweder Button/Menu deaktivieren oder die neue Anforderung kontrolliert zusammenführen.

**Wichtig:** Die Wiederherstellung soll nicht permanent überwachen und manuell verschobene Fenster sofort zurückzwingen. Sie erfolgt nur bei entsprechend aktiviertem Login-Autostart oder expliziter Tray-Aktion.

## 8. UI-/UX-Anforderungen

### 8.1 Tray-Kontextmenü

```text
Laytivo
────────────────────────────────────────
↻ Gespeicherte Anordnung wiederherstellen
▣ Aktuelle Anordnung speichern
⚙ Einstellungen öffnen
────────────────────────────────────────
✓ Mit Windows starten
✓ Programme automatisch starten
────────────────────────────────────────
  Laytivo beenden
```

Die Option „Anordnung nach Anmeldung wiederherstellen“ befindet sich mindestens in den Einstellungen und kann zusätzlich im Tray angeboten werden.

### 8.2 Einstellungen

- Schalter: mit Windows starten
- Schalter: nach Anmeldung Anordnung wiederherstellen
- Schalter: fehlende Programme automatisch starten
- Auswahl: Startverzögerung 0 / 5 / 15 Sekunden
- Übersicht des gespeicherten Profils und der enthaltenen Fenster
- Schaltflächen: „Aktuelle Anordnung speichern“, „Wiederherstellen“
- Letztes Wiederherstellungsergebnis mit etwaigen Fehlern

### 8.3 Verhalten beim Schließen

- Fenster schließen = UI verbergen, Laytivo bleibt im Tray.
- Explizit „Laytivo beenden“ = Anwendung beendet.
- Beim System-Autostart öffnet sich standardmäßig **kein sichtbares Hauptfenster**.

## 9. Nichtziele des MVP / spätere Ausbaustufen

Nicht Teil des MVP:

- Visueller Drag-and-Drop-Layout-Editor oder FancyZones-ähnliche Bildschirmraster
- Mehrere benannte Workspace-Profile und Profilwechsel
- Globale Hotkeys
- Regelbasierte Live-Automatisierung bei jedem neuen Fenster
- Automatischer Profilwechsel bei Monitor-/Docking-Konfigurationen
- Profil-Import/-Export und Cloud-Synchronisierung
- Fenster-/Dokumentinhalte auslesen oder Screenshots aufzeichnen
- Garantierte Wiederherstellung interner Dokumentzustände und Browser-Tabs von Drittprogrammen

**Mögliche Phase 2:** mehrere Profile, Profilwechsel über Tray/Hotkey, Erkennung von Dockingstationen, Import/Export, app-spezifische Launch-Adapter.

## 10. Technische Risiken und Einschränkungen

1. **Nicht jedes Fenster ist extern positionierbar.** UWP-/Spezialfenster, Apps mit eigener Platzierungslogik und erhöhte Prozesse können Sonderbehandlung erfordern.
2. **Apps mit erhöhten Rechten:** Laytivo soll nicht stillschweigend UAC umgehen oder automatisch selbst erhöhte Rechte anfordern.
3. **Anwendungen mit mehreren Fenstern:** Fenster-Zuordnung und gezieltes Wiederöffnen sind anwendungsabhängig; vorhandene Fenster können oft besser wiederhergestellt werden als geschlossene Zweitfenster.
4. **Browser-/IDE-Sitzungen:** Das Starten einer EXE garantiert nicht, dass dieselben Tabs, Projekte oder Dokumente geöffnet werden.
5. **Auflösungswechsel und DPI:** Korrekte physische/logische Koordinaten und Monitoridentifikation sind kritisch.
6. **Timing/Races:** Programme können verzögert öffnen oder Positionen nachträglich selbst verändern. Begrenzte Retry-Strategie nötig.
7. **Minimierte Fenster:** MVP speichert Normal-/Maximiert-Zustände; minimierte Fenster sollten beim Speichern entweder ausgeschlossen oder nach klar definierter Regel behandelt werden (Vorschlag: normalen Wiederherstellungszustand verwenden, nicht minimiert starten).

## 11. Abnahmekriterien / Testfälle

### AT-01 – Speichern eines Multi-Monitor-Layouts
**Gegeben:** Drei aktive Monitore und fünf geeignete geöffnete Anwendungsfenster.  
**Wenn:** Der Benutzer die Fenster frei anordnet und „Aktuelle Anordnung speichern“ wählt.  
**Dann:** Laytivo speichert pro Fenster Anwendung, Zielmonitor, Geometrie und Zustand; das Ergebnis übersteht einen Laytivo-Neustart.

### AT-02 – Automatische Wiederherstellung nach Anmeldung
**Gegeben:** Ein gespeichertes Standardprofil, `startWithWindows=true`, `restoreAfterLogin=true`, `launchMissingApps=true`.  
**Wenn:** Windows neu startet und der Benutzer sich anmeldet.  
**Dann:** Laytivo startet im Tray, öffnet fehlende Programme und stellt die Fenster auf den gespeicherten Monitoren in gespeicherter Größe/Position wieder her, soweit technisch unterstützt.

### AT-03 – Manuelle Wiederherstellung
**Gegeben:** Ein gespeichertes Profil und manuell verschobene Fenster.  
**Wenn:** Im Tray „Gespeicherte Anordnung wiederherstellen“ gewählt wird.  
**Dann:** Zugeordnete Fenster erhalten ihre gespeicherte Position/Größe/Zustand zurück.

### AT-04 – Keine unnötigen Doppelstarts
**Gegeben:** Eine gespeicherte Anwendung ist bereits mit einem passenden Fenster geöffnet.  
**Wenn:** Eine Wiederherstellung erfolgt.  
**Dann:** Laytivo nutzt das vorhandene Fenster statt ohne Grund die Anwendung erneut zu starten.

### AT-05 – Fehlende Programme
**Gegeben:** Eine gespeicherte EXE ist nicht mehr vorhanden.  
**Wenn:** Wiederherstellung ausgeführt wird.  
**Dann:** Laytivo protokolliert den Fehler verständlich und führt die übrigen Fensteraktionen fort.

### AT-06 – Zielmonitor fehlt
**Gegeben:** Ein Profil wurde mit drei Monitoren gespeichert, aktuell sind zwei verbunden.  
**Wenn:** Wiederherstellung ausgeführt wird.  
**Dann:** Alle verwaltbaren Fenster bleiben sichtbar; das gespeicherte Layout bleibt unverändert.

### AT-07 – Autostart deaktiviert
**Gegeben:** `startWithWindows=false`.  
**Wenn:** Der Benutzer sich erneut anmeldet.  
**Dann:** Laytivo wird nicht durch seinen eigenen Autostart-Eintrag gestartet.

### AT-08 – Tray und Schließen
**Gegeben:** Laytivo läuft.  
**Wenn:** Der Benutzer das Einstellungsfenster schließt.  
**Dann:** Laytivo bleibt im Tray erreichbar. Bei „Laytivo beenden“ wird der Prozess beendet.

### AT-09 – Kein App-Start bei deaktivierter Option
**Gegeben:** Ein gespeichertes Profil und `launchMissingApps=false`.  
**Wenn:** Wiederherstellung ausgelöst wird.  
**Dann:** Nur bereits vorhandene passende Fenster werden positioniert; fehlende Apps werden nicht gestartet.

### AT-10 – Mehrfachaufruf
**Gegeben:** Eine Wiederherstellung läuft bereits.  
**Wenn:** Der Benutzer erneut „Wiederherstellen“ auslöst.  
**Dann:** Es entsteht kein unkontrollierter paralleler zweiter Durchlauf und keine doppelte App-Startwelle.

### AT-11 – Fensterzustand
**Gegeben:** Ein maximiertes und ein normal skaliertes Fenster sind gespeichert.  
**Wenn:** Wiederherstellung ausgeführt wird.  
**Dann:** Beide erhalten den korrekten gespeicherten Zustand; das normale Fenster erhält seine gespeicherte Geometrie.

### AT-12 – Speicherung aktualisieren
**Gegeben:** Ein bereits vorhandenes Profil und eine neue Fensteranordnung.  
**Wenn:** Der Benutzer ausdrücklich erneut speichert.  
**Dann:** Das Profil enthält die neue Anordnung und spätere Wiederherstellungen nutzen diese.

## 12. Definition of Done (MVP)

Der MVP gilt erst dann als abgeschlossen, wenn:

- [ ] Laytivo unter Windows 11 x64 startbar ist und stabil im System-Tray läuft.
- [ ] Der Benutzer relevante Fenster aus seiner aktuellen Umgebung speichern kann.
- [ ] Gespeicherte Fenster vorhandenen Anwendungen zuverlässig zugeordnet werden.
- [ ] Fehlende Programme bei aktivierter Option automatisch gestartet werden.
- [ ] Fenster nach einem Windows-Neustart und Login weitgehend entsprechend dem gespeicherten Profil platziert werden.
- [ ] Eine manuelle Wiederherstellung aus dem Tray jederzeit funktioniert.
- [ ] Laytivo-Autostart und Restore-Automatik getrennt konfigurierbar sind.
- [ ] Fehlende Monitore, EXEs und Zeitüberschreitungen sauber behandelt werden.
- [ ] Konfiguration dauerhaft, versioniert und ausfallsicher gespeichert wird.
- [ ] Alle Testfälle AT-01 bis AT-12 umgesetzt bzw. technische Abweichungen ausdrücklich dokumentiert sind.
- [ ] Ein README Build-, Start-, Installations- und Testanweisungen enthält.
- [ ] Automatisierte Tests für Kernlogik vorhanden sind; reale Multi-Monitor-Tests werden manuell durchgeführt.

## 13. Umsetzungsvorschlag für Codex

In dieser Reihenfolge implementieren; **nach jedem Schritt lauffähigen Stand und Tests sicherstellen**:

1. **Grundgerüst:** Solution, WPF-App, MVVM, DI, Tray-Icon, Einstellungen, Single Instance.
2. **Fenster-/Monitorerkennung:** Win32-Interop, DPI-Awareness, Filterung, Fensterliste.
3. **Snapshot/Persistenz:** Standardprofil, Fensterauswahl, JSON-Store, Schema-Versionierung.
4. **Wiederherstellung bestehender Fenster:** Matching, Geometrie, Zustände, Monitor-Fallbacks.
5. **Programmstart:** EXE/Argumente, Startüberwachung, Timeout, Retry, Mehrfachstarts vermeiden.
6. **Windows-Integration:** Login-Autostart, Startverzögerung, automatische Wiederherstellung.
7. **Robustheit:** Fehlerstatus, Logging, Multi-Monitor-/DPI-Tests, Dokumentation.

### Codex-Arbeitsauftrag (Startprompt)

> Implementiere das Projekt **Laytivo** gemäß dieser Datei. Beginne mit einer kurzen Architektur- und Umsetzungsplanung, erstelle anschließend eine kompilierbare Windows-11-WPF-Solution mit .NET 10 und setze den MVP schrittweise um. Beachte besonders DPI-korrekte Fenstergeometrie, stabile Fenster-/Monitorzuordnung, Tray-Betrieb, Autostart nach Benutzeranmeldung, das Starten fehlender Anwendungen und idempotente Wiederherstellung. Implementiere Tests für die plattformunabhängige Logik. Verwende möglichst wenige externe Abhängigkeiten. Behaupte nicht, Windows-spezifische Integrationsfunktionen seien erfolgreich getestet, wenn keine Windows-Testumgebung vorhanden ist. Dokumentiere bekannte Einschränkungen und offene Punkte im README.
