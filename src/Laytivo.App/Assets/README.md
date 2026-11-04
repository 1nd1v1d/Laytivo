# Laytivo-Icon

Erstellt am 04.11.2026 mit dem integrierten Imagegen-Werkzeug. Das Motiv stellt drei geordnete Fensterflächen auf einem dunkelblauen, abgerundeten Quadrat dar. Der Außenbereich ist transparent.

- `Laytivo-source.png`: unverändertes, generiertes Original.
- `Laytivo.png`: 256-Pixel-Version für die Oberfläche.
- `Laytivo.ico`: transparente Windows-Icondatei mit 16, 20, 24, 32, 40, 48, 64, 128 und 256 Pixeln. Wird in die EXE sowie als WPF-/Tray-Ressource eingebettet.

Die Formatkonvertierung lässt sich unter Windows ohne zusätzliche Pakete mit `scripts/Build-Icon.ps1` reproduzieren. Das Icon muss für normale Builds nicht neu erzeugt werden.

## Verwendeter Imagegen-Prompt

> Use case: logo-brand. Asset type: one production Windows desktop application icon for Laytivo, a local tool that saves and restores desktop window arrangements. Create one polished square icon, centered and filling nearly the whole canvas. Match the app's clean slate-blue Windows interface: deep navy rounded-square badge, containing three bold, precisely aligned window panes with softly rounded corners — one tall pane at left, two smaller panes stacked at right. The panes are bright ice-blue and white with one restrained sky-blue accent. This represents multiple desktop windows neatly back in place. Flat, crisp vector-like geometric finish, strong silhouette, generous separation between shapes, excellent legibility at 16 and 32 pixels. Very minimal shading, no tiny details, no outlines thinner than the pane gaps. Transparent background outside the badge. Straight-on view, no perspective or mockup, no typography, no letters, no words, no watermark, no extra symbols or alternate icons. Deliver a single final high-resolution icon.
