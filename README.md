# WinShot

A lightweight, native Windows screenshot + annotation tool that lives in the system tray.
Press a global hotkey, drag a region, annotate, then pick Copy / Save / Save & Copy.

Version: **0.2.0**
Target: **Windows 10 1809+ / Windows 11**, x64
Runtime: **.NET 8 (WPF)** — self-contained single-file publish supported

---

## Features

- **Always-on tray icon.** WinShot runs in the notification area and responds to the global hotkey `Ctrl+Shift+S`. Right-click the tray for Capture, Settings, About, Exit. Double-click to capture.
- **Region select overlay** with live dimension readout, mixed-DPI multi-monitor aware.
- **Non-destructive vector annotations** — every shape is an object rendered into the same `DrawingContext` used for export, so what you see is exactly what is saved:
  - Arrow, Rectangle, Ellipse, Line
  - Text with dark halo for legibility on any background
  - Highlighter (semi-transparent marker)
  - Pixelate / blur redaction — mosaic tiles, safer than gaussian blur for hiding passwords
- **Edit after drawing** — `S` selects the Select tool; click any shape to move it, drag the corner/edge handles to resize, `Delete` to remove. Every edit is a single undo step.
- **Post-capture action tri-state**: **Copy** to clipboard, **Save** to the configured folder, or **Save & Copy** in one shot. `Save As…` is always available for ad-hoc locations.
- **Settings** — configurable save directory, filename template, default format (PNG / JPEG + quality slider), and "reveal in Explorer after save".
- **Keyboard-first editing** — single-letter tool shortcuts plus the usual Ctrl+Z / Ctrl+S / Ctrl+C / Ctrl+Enter.
- **Per-Monitor V2 DPI aware**, no admin required, clean disposal of GDI handles.
- **Single instance** — a named mutex prevents duplicate hotkey registrations.

---

## Keyboard shortcuts (inside the editor)

| Key           | Action                                        |
| :------------ | :-------------------------------------------- |
| `S`           | Select tool — click a shape to move / resize  |
| `A`           | Arrow tool                                    |
| `R`           | Rectangle tool                                |
| `E`           | Ellipse tool                                  |
| `L`           | Line tool                                     |
| `T`           | Text tool                                     |
| `H`           | Highlighter                                   |
| `B`           | Blur / pixelate                               |
| `Delete`      | Remove the selected shape                     |
| `Ctrl+Z`      | Undo last action (add, move, resize, delete)  |
| `Ctrl+C`      | Copy to clipboard                             |
| `Ctrl+S`      | Save (uses Settings path)                     |
| `Ctrl+Enter`  | Save **and** Copy                             |
| `Esc`         | Close editor                                  |

Single-letter shortcuts are ignored while the in-place text editor has focus, so typing annotation text won't switch tools mid-word.

Global:

| Key               | Action                |
| :---------------- | :-------------------- |
| `Ctrl+Shift+S`    | Trigger region capture|

---

## Settings

Open from the tray → **Settings…** or from the editor's bottom action bar.

| Field                       | Purpose                                                     |
| :-------------------------- | :---------------------------------------------------------- |
| Save directory              | Default folder for `Save` / `Save & Copy` (created on save) |
| Filename template           | Accepts `{timestamp:FORMAT}` tokens, e.g. `{timestamp:yyyyMMdd_HHmmss}` |
| Default format              | PNG (lossless) or JPEG                                      |
| JPEG quality                | 40 – 100 (ignored for PNG)                                  |
| Reveal in Explorer on save  | Runs `explorer /select,<path>` after a successful save      |

Settings are persisted at `%APPDATA%\WinShot\settings.json` and reloaded on every start. A `.bak` is kept on JSON corruption.

---

## Prerequisites

- Windows 10 1809+ or Windows 11, x64
- For building: **.NET 8 SDK** — <https://dot.net>
- For the classic installer: **Inno Setup 6+** — <https://jrsoftware.org/isdl.php>
- For MSIX packaging: **Windows 10/11 SDK** (ships `MakeAppx.exe` + `signtool.exe`) — <https://developer.microsoft.com/windows/downloads/windows-sdk/>
- For winget manifest linting: `winget` ≥ 1.6 (preinstalled on Win11)
- Visual Studio 2022 17.8+ or JetBrains Rider (optional, for IDE work)

---

## Build & run from source

```powershell
# From the solution root:
dotnet restore
dotnet build -c Release
dotnet run --project WinShot
```

Or open `WinShot.sln` in Visual Studio 2022 and press `F5`.

---

## Publish a portable single-file EXE

Produces `WinShot.exe` with all runtime bits embedded — no .NET install required on the target machine.

```powershell
dotnet publish WinShot\WinShot.csproj -c Release /p:PublishProfile=SingleFile
```

Output: `WinShot\bin\Release\net8.0-windows\publish\win-x64\WinShot.exe`
(≈150 MB because WPF requires the full desktop runtime to be self-contained. Flip `SelfContained` to `false` in `SingleFile.pubxml` to fall back to a ~1 MB framework-dependent EXE that requires the .NET 8 Desktop runtime preinstalled.)

---

## Build the Windows installer (`WinShotSetup.exe`)

The installer gives you the full native experience — Start Menu entry, Programs & Features uninstaller, optional "Start with Windows" autostart, optional desktop shortcut.

One-shot build (publishes + packs):

```powershell
.\build-installer.ps1
```

Flags:

```powershell
.\build-installer.ps1 -SkipPublish                         # re-pack only
.\build-installer.ps1 -InnoSetupPath "C:\Tools\iscc.exe"   # custom Inno location
.\build-installer.ps1 -SkipInstaller                       # publish only
```

Or manually:

```powershell
dotnet publish WinShot\WinShot.csproj -c Release /p:PublishProfile=SingleFile
iscc Installer\WinShot.iss
```

Installer output: `dist\WinShotSetup.exe`

Run it (elevation required — it installs to `%ProgramFiles%`):

```powershell
& .\dist\WinShotSetup.exe
```

### What the installer does

- Installs `WinShot.exe` to `%ProgramFiles%\WinShot`
- Creates Start Menu shortcut (and optionally a desktop shortcut)
- **Optional** — adds `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\WinShot` so it launches at login (tray utility only makes sense if it's running)
- Registers a proper uninstaller in *Apps & Features*
- On uninstall: removes files, removes the Run entry, best-effort kills any running instance

### Uninstall

Start → *Apps & Features* → WinShot → **Uninstall**
(or run the `unins000.exe` in the install folder)

User settings at `%APPDATA%\WinShot\` are **not** removed by the uninstaller — delete that folder manually if you want a truly clean slate.

---

## Build an MSIX package (`WinShot_0.2.0_x64.msix`)

MSIX is the modern Windows app package format — sandboxed install, differential updates, and a path to the Microsoft Store. WinShot ships an MSIX build alongside the Inno Setup installer so you can compare both idioms.

### One-shot: publish, pack, self-sign, output signed `.msix`

```powershell
.\build-msix.ps1 -GenerateCert
```

Output: `dist\WinShot_0.2.0_x64.msix`
Cert:   `Packaging\WinShot.Dev.pfx` (password `WinShot.Dev.LocalOnly`)

Flags:

```powershell
.\build-msix.ps1 -SkipPublish                      # re-pack + re-sign only
.\build-msix.ps1 -SkipSigning                      # unsigned (use for Store upload)
.\build-msix.ps1 -CertificateThumbprint <THUMB>    # use an existing cert in CurrentUser\My
.\build-msix.ps1 -CertSubject "CN=MyCompany"       # different Publisher identity
```

### Sideload the signed MSIX

Self-signed MSIX packages install only if the signing cert is trusted on the target machine. One-time cert import:

```powershell
# From an elevated PowerShell prompt:
Import-Certificate -FilePath .\Packaging\WinShot.Dev.cer `
                   -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

Or right-click the `.pfx` → Install → Local Machine → Place in *Trusted People*.

Then install the package:

```powershell
Add-AppxPackage .\dist\WinShot_0.2.0_x64.msix
```

Launch from the Start Menu. The MSIX declares a `windows.startupTask`, so WinShot auto-starts at login unless the user disables it via **Settings → Apps → Startup**.

### MSIX vs Inno Setup — why both?

| Concern                              | Inno Setup (.exe)               | MSIX (.msix)                              |
| :----------------------------------- | :------------------------------ | :---------------------------------------- |
| Install location                     | `%ProgramFiles%\WinShot`        | Isolated package container (`%ProgramFiles%\WindowsApps\WinShot.WinShot_…`) |
| Uninstall                            | *Apps & Features*               | *Apps & Features* + `Remove-AppxPackage`  |
| Autostart                            | `HKCU\...\Run` (optional)       | `windows.startupTask` manifest extension  |
| File-system writes                   | Full access                     | Virtualized (redirected to per-package store) |
| Registry writes                      | Full access                     | Virtualized                                |
| Code signing                         | Optional for unsigned EXEs      | Required — unsigned MSIX will not install |
| Update mechanism                     | Run new installer               | Differential, background, via MSIX URI    |
| Store submission                     | Not applicable                  | Direct Partner Center upload              |
| Build footprint                      | `iscc.exe` + Inno Setup         | `MakeAppx.exe` + `signtool.exe` (Windows SDK) |

WinShot's `P/Invoke` / `RegisterHotKey` / `BitBlt` code is full-trust Win32. The MSIX manifest declares `<rescap:Capability Name="runFullTrust"/>`, which is approved for desktop apps in Partner Center.

---

## winget manifest

A ready-to-submit winget manifest lives at `winget/manifests/w/WinShot/WinShot/0.2.0/` (three files — the v1.6 multi-file schema). Once you've uploaded `WinShotSetup.exe` and the signed `.msix` to a public URL (typically a GitHub Release), finish the manifest like this:

```powershell
# 1. Publish your release assets (GitHub Release / S3 / etc.), then:
Get-FileHash .\dist\WinShotSetup.exe         -Algorithm SHA256
Get-FileHash .\dist\WinShot_0.2.0_x64.msix   -Algorithm SHA256

# 2. For MSIX, also compute the signature hash (winget uses this to verify
#    that the signature chain hasn't changed across updates):
#    Rename the .msix to .zip, extract AppxSignature.p7x, hash it:
Expand-Archive -Path .\dist\WinShot_0.2.0_x64.msix -DestinationPath .\dist\msix-extract -Force
Get-FileHash .\dist\msix-extract\AppxSignature.p7x -Algorithm SHA256

# 3. Find the PackageFamilyName after your first install:
Get-AppxPackage -Name WinShot.WinShot | Select-Object PackageFamilyName
```

Edit the generated YAMLs under `winget/manifests/w/WinShot/WinShot/0.2.0/` to replace the four placeholder values (two SHA256, one SignatureSha256, one PackageFamilyName) and the URLs.

Lint and test locally:

```powershell
winget validate .\winget\manifests\w\WinShot\WinShot\0.2.0\
winget install  -m   .\winget\manifests\w\WinShot\WinShot\0.2.0\
```

Submit to the public winget repository by forking <https://github.com/microsoft/winget-pkgs>, dropping these three files into the same relative path, and opening a PR. The `wingetcreate submit` tool automates the fork/PR steps.

Once merged, end users install with:

```powershell
winget install WinShot.WinShot
```

---

## Project layout

```
WinShot.sln
WinShot/
├── App.xaml(.cs)                    # DI, hotkey registration, tray wiring
├── app.manifest                     # Per-Monitor V2 DPI
├── Assets/
│   └── WinShot.ico                  # Multi-size app icon (16…256)
├── Interop/
│   ├── NativeMethods.cs             # user32/gdi32 P/Invoke + SafeHBitmapHandle
│   ├── HiddenMessageWindow.cs       # HWND owner for WM_HOTKEY
│   └── HotKeyManager.cs             # IDisposable RegisterHotKey wrapper
├── Services/
│   ├── IScreenCaptureService.cs / ScreenCaptureService.cs   # BitBlt virtual screen
│   ├── IClipboardService.cs    / ClipboardService.cs        # PNG + DIB + retry
│   ├── ISettingsService.cs     / SettingsService.cs         # JSON persist + events
│   └── AppSettings.cs                                       # Settings model + path resolver
├── ViewModels/
│   └── EditorViewModel.cs           # MVVM state (CommunityToolkit.Mvvm)
├── Views/
│   ├── RegionSelectWindow.xaml(.cs) # Drag-to-select overlay
│   ├── EditorWindow.xaml(.cs)       # Annotation editor + action bar
│   ├── SettingsWindow.xaml(.cs)     # Save path / template / format / quality
│   └── TrayIconHost.xaml            # Hardcodet.NotifyIcon.Wpf TaskbarIcon + menu
├── Annotations/
│   ├── Annotation.cs                # Base class (DrawingContext Render)
│   ├── AnnotationTool.cs            # Tool enum
│   ├── ShapeAnnotations.cs          # Rect / Ellipse / Line / Arrow / Highlighter
│   ├── TextAnnotation.cs            # Text with FormattedText + halo
│   └── BlurAnnotation.cs            # Mosaic pixelation via scale down+up
└── Properties/PublishProfiles/
    └── SingleFile.pubxml            # Self-contained single-file publish

Installer/
└── WinShot.iss                      # Inno Setup 6 script
Packaging/
├── Package.appxmanifest             # MSIX manifest (identity, startup task, capabilities)
├── Images/                          # MSIX visual assets (44/150/310/71/StoreLogo/Splash/Wide)
└── make_msix_assets.py              # Regenerates Images/ from the source design
winget/
└── manifests/w/WinShot/WinShot/0.2.0/
    ├── WinShot.WinShot.yaml                    # Version manifest
    ├── WinShot.WinShot.installer.yaml          # Installer manifest (inno + msix)
    └── WinShot.WinShot.locale.en-US.yaml       # Default-locale manifest
build-installer.ps1                  # Publish + Inno Setup driver
build-msix.ps1                       # Publish + MakeAppx + signtool driver
dist/
├── WinShotSetup.exe                 # Inno Setup output
└── WinShot_0.2.0_x64.msix           # MSIX output
```

---

## Architectural notes

- **Why WPF, not WinUI 3?** WPF's retained-mode `Visual` / `DrawingContext` pipeline makes non-destructive annotation trivial: each annotation is an object that renders itself. Undo is `List.RemoveLast()`. Exporting is the same render call into a `RenderTargetBitmap`, so what you see on screen matches the saved PNG bit-for-bit. WinUI 3's drawing primitives are less mature and MSIX packaging would add friction for a single-EXE tool.
- **Why MVVM-lite?** `EditorViewModel` uses `CommunityToolkit.Mvvm`'s `ObservableObject` for property change notifications. The view code-behind stays thick for mouse interaction — drawing gestures are inherently imperative, and shoehorning them into commands adds more indirection than value.
- **Tray + single instance.** `Hardcodet.NotifyIcon.Wpf` hosts a proper WPF `ContextMenu`. `ShutdownMode=OnExplicitShutdown` keeps the process alive when the editor closes. A named `Mutex` (`Global\WinShot.SingleInstance.{6B9E...}`) stops a second launch from double-registering the hotkey. `RegisterHotKey` requires an HWND; a hidden zero-size `Window` (not `HWND_MESSAGE` — message-only HWNDs can't receive `WM_HOTKEY`) owns one and hooks its `HwndSource`.
- **DPI correctness.** `app.manifest` declares `PerMonitorV2`. `ScreenCaptureService` operates in physical pixels (`GetSystemMetrics(SM_*VIRTUALSCREEN)`). The region overlay converts DIPs → px via the current `PresentationSource.CompositionTarget.TransformToDevice`, the right thing on mixed-DPI spans. The editor forces the `CanvasHost` to the image's pixel size so 1 image-pixel = 1 device-pixel regardless of the monitor the editor opens on.
- **GDI cleanup.** Every `CreateCompatibleDC` / `CreateCompatibleBitmap` / `GetWindowDC` is matched in a `finally` block. The `SelectObject` restore runs before we `DeleteObject` the bitmap so GDI isn't holding a selected object at delete time.
- **Blur = pixelation.** True gaussian blur can sometimes be reversed (descreening). Mosaic pixelation at ~14 px tiles via `TransformedBitmap` down-scale + `NearestNeighbor` up-scale is safer for redacting passwords, emails, and faces.
- **Clipboard compatibility.** `ClipboardService` attaches both a raw PNG stream (retains alpha) **and** a DIB (what Win32 paint tools expect), and retries up to 10× through `OpenClipboard` failures.

---

## Troubleshooting

**Hotkey conflict.** If `Ctrl+Shift+S` is already owned (Snip & Sketch on some builds, Edge, Chrome…), WinShot shows a warning at startup and the tray menu / double-click still work. To change the hotkey, edit `DefaultModifiers` / `DefaultVirtualKey` in `App.xaml.cs`; making this user-configurable is a natural next step.

**Installer says "WinShot is running".** The installer best-effort runs `taskkill /IM WinShot.exe /F /T` at both install and uninstall time. If you've launched WinShot from a path outside `%ProgramFiles%`, close it manually from the tray first.

**Icon doesn't appear in the tray.** Check Notification Area Settings in Windows — WinShot's tray icon may be collapsed into the overflow chevron. Drag it out to pin.

**Installer can't find `iscc.exe`.** Install Inno Setup 6, or pass `-InnoSetupPath` to `build-installer.ps1`.

**`build-msix.ps1` can't find `MakeAppx.exe` / `signtool.exe`.** Install the Windows 10/11 SDK, or pass `-WindowsSdkPath` pointing at a folder that contains both tools (e.g. `C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64`).

**MSIX install fails with "The root certificate of the signature is not trusted".** The self-signed cert must be imported to `Cert:\LocalMachine\TrustedPeople` on the target machine — see the sideload instructions above.

**MSIX install fails with "The publisher of an installed package doesn't match".** You rebuilt with a different `-CertSubject` than a prior install. Either bump the version, uninstall the old package first (`Get-AppxPackage WinShot.WinShot | Remove-AppxPackage`), or reuse the original Subject DN.

---

## Roadmap (not yet implemented)

- Configurable hotkey UI (currently a code constant)
- Freeform / lasso capture
- Freehand pen tool (WPF `InkCanvas` composited onto the same `DrawingVisual` pipeline)
- Optional OCR on selected region
- Partner Center store submission (the MSIX build is Store-ready; flip `-SkipSigning` and upload)

---

## License
Private / internal — add your license of choice.
