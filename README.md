# VRamUsage - AMD Radeon 890M & System RAM Monitor Overlay

Ein modernes, leichtgewichtiges und transparentes Desktop-Overlay für Windows zur Echtzeit-Überwachung der **AMD Radeon 890M iGPU** (dediziertes und gemeinsam genutztes VRAM, GPU-Auslastung) sowie des **System-RAM** (64 GB) und der **CPU-Auslastung**.

---

## Highlights & Features

- **Hardware-Spezifisch optimiert**:
  - Erkennt automatisch die **AMD Radeon(TM) 890M Graphics** des AMD Ryzen AI 9 HX PRO 370.
  - Zeigt sowohl das feste dedizierte BIOS-VRAM (z. B. 338 MB / 512 MB) als auch den dynamisch aus dem 64 GB System-RAM bereitgestellten **Shared VRAM** an.
  - Exakte Übereinstimmung mit den Werten im Windows Task-Manager (Nutzung der DXGI- und Windows Performance Counter / PDH-Schnittstelle).
- **Kompaktes, transparentes Overlay**:
  - Dunkles, transparentes Acryl-/Slate-Design mit abgerundeten Ecken und dezenter Umrandung.
  - Platziert sich standardmäßig dezent **oben links**.
  - Frei per Maus verschiebbar (Drag & Drop) oder fixierbar („Position sperren“).
  - Kein störender Taskleisten-Button (`ShowInTaskbar="False"`).
- **System Tray Icon**:
  - Tray-Icon im Windows-Infobereich mit Tooltip-Zusammenfassung.
  - Kontextmenü:
    - Position sperren / entsperren
    - Immer im Vordergrund an/aus
    - Deckkraft / Transparenz (100 %, 88 %, 75 %, 60 %, 40 %)
    - Aktualisierungsintervall (500 ms, 1 s, 2 s)
    - Einblenden / Ausblenden (auch per Doppelklick auf das Tray-Icon)
    - Beenden
- **Ressourceneffizienz**:
  - Entwickelt in **.NET 10 / C# 14** (WPF).
  - Sub-1% CPU-Auslastung, extrem geringer Speicherbedarf.

---

## Projektstruktur

```text
c:/Security/vRam_usage/
├── Configuration/
│   └── HardwareMonitoring/
│       └── OverlayOptions.cs
├── Interfaces/
│   └── HardwareMonitoring/
│       ├── IGpuMetricsProvider.cs
│       ├── ISystemRamMetricsProvider.cs
│       ├── ICpuMetricsProvider.cs
│       └── ISystemMetricsService.cs
├── Services/
│   └── HardwareMonitoring/
│       ├── DxgiPdhGpuMetricsProvider.cs
│       ├── Win32SystemRamMetricsProvider.cs
│       ├── Win32CpuMetricsProvider.cs
│       └── SystemMetricsService.cs
├── Controllers/
│   └── HardwareMonitoring/
│       ├── OverlayViewModel.cs
│       └── TrayIconController.cs
├── Models/
│   └── HardwareMonitoring/
│       └── HardwareMetrics.cs
├── Views/
│   └── HardwareMonitoring/
│       ├── OverlayWindow.xaml
│       └── OverlayWindow.xaml.cs
├── App.xaml / App.xaml.cs
├── appsettings.json
└── VRamUsage.csproj
```

---

## Starten & Ausführen

### Direkt über dotnet:
```powershell
dotnet run
```

### Als kompilierte Anwendung:
Die kompilierte Binärdatei befindet sich unter:
`C:\Security\vRam_usage\publish\VRamUsage.exe`
bzw.
`C:\Security\vRam_usage\bin\Debug\net10.0-windows\VRamUsage.exe`
