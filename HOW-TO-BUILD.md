# Building WinShot — a step-by-step guide for first-timers

This guide walks you from zero to a clickable `WinShotSetup.exe` installer on
your Windows machine. No programming experience is assumed — I'll tell you
exactly what to type and what you should see.

Total time first run: about **15 minutes of installing things + 5 minutes of
actually building**.

---

## Section 0 — What you're going to end up with

At the end of this guide you'll have a single file:

```
C:\dev\WinShot\dist\WinShotSetup.exe
```

That file is a normal Windows installer. Double-clicking it installs WinShot
on any Windows 10 or 11 PC: adds a Start Menu entry, an uninstaller in Apps &
Features, and (optionally) starts WinShot automatically with Windows.

You can email this file, put it on a shared drive, copy it to a USB stick —
anything you'd do with an ordinary `.exe`.

### `.exe` vs `.msi` vs `.msix` — which is this?

You asked for `.exe` or `.msi`. Here's the lay of the land so nothing surprises you:

- **`.exe` installer (this guide).** Built by a tool called **Inno Setup**. Looks and behaves exactly like installers you're used to. Just double-click it.
- **`.msi`.** Microsoft's older, stricter installer format — used mostly by large companies that have automated deployment tools requiring `.msi`. WinShot does **not** currently build an `.msi`. Making one would require adding a second packaging pipeline (WiX Toolset). If your company specifically requires `.msi`, tell me and we can set that up as a follow-up task — but most people don't need it.
- **`.msix`.** A newer, sandboxed installer format. WinShot supports it via `build-msix.ps1`, but it requires a code-signing certificate and a matching "publisher" name, so it's a bit more finicky. Not covered in this guide.

**Short answer: follow this guide and you'll get the `.exe`, which is what 95 % of people actually want.**

---

## Section 1 — One-time setup on your Windows machine

You need to install three tools, one time. Skip any you already have.

### 1A. .NET 8 SDK

This is the thing that turns the C# source code into a Windows `.exe`.

1. Open https://dotnet.microsoft.com/download/dotnet/8.0 in your browser.
2. Under the **.NET 8.0** heading, find the box labelled **SDK x64** for Windows.
3. Click **Download**, then run the file it downloads.
4. Click **Install**, accept defaults, and finish.

**Verify it worked:**

1. Press the **Windows** key, type `powershell`, press **Enter**.
2. In the black/blue window that opens, type:
   ```
   dotnet --version
   ```
3. Press Enter. You should see something like `8.0.402`. As long as it starts with `8.`, you're good.

If you see "not recognized as..." — close PowerShell and reopen it. The installer only updates the PATH for newly-opened windows.

### 1B. Inno Setup

This is the tool that wraps the compiled program into a single `.exe` installer.

1. Open https://jrsoftware.org/isdl.php
2. Download the latest **innosetup-6.x.x.exe** (the plain one, not the Unicode or Quickstart Pack).
3. Run it. Accept the default install location (`C:\Program Files (x86)\Inno Setup 6\`). The build script knows to look there.

No PATH changes needed — the build script finds Inno Setup automatically.

### 1C. Git (only if you don't already have the source)

Skip this if you already have the WinShot source folders on your computer.

1. Open https://git-scm.com/download/win and install with defaults.

---

## Section 2 — Put the source in one folder

The build scripts expect a specific folder layout. In this Cowork workspace
the pieces are in separate folders — you need to merge them into one folder
on your Windows machine.

### The layout you need

```
C:\dev\WinShot\                         <-- pick any folder name; this is the "repo root"
├── WinShot.sln
├── build-installer.ps1
├── build-msix.ps1
├── README.md
├── HOW-TO-BUILD.md                     <-- this file
├── WinShot\                            <-- source code subfolder
│   ├── WinShot.csproj
│   ├── App.xaml.cs
│   ├── Annotations\
│   ├── Services\
│   ├── Views\
│   └── ... (everything else)
└── Installer\
    └── WinShot.iss
```

### How to build that layout from the Cowork folders

**Important:** the Cowork folders **"Native Windows App Builder"**, **"WinShot"**, and **"Installer"** are *separate* folders, not nested inside each other. On your Windows computer they usually sit side-by-side in the same parent directory (look at the Cowork sidebar — each one has its own entry). You need to merge them into one layout.

A typical setup on your machine will look something like:

```
C:\Users\YourName\Documents\Claude\Projects\
├── Installer\                    (a Cowork folder, contains WinShot.iss)
├── Native Windows App Builder\   (a Cowork folder, contains build-installer.ps1 etc.)
├── Packaging\                    (a Cowork folder, not needed for the .exe build)
├── winget\                       (a Cowork folder, not needed for the .exe build)
└── WinShot\                      (a Cowork folder, contains all the C# source code)
```

Pick **one** of these two approaches:

**Approach A — use "Native Windows App Builder" as your repo root (easiest if you already have it):**

1. Move the **"WinShot"** folder *into* the **"Native Windows App Builder"** folder (cut and paste in File Explorer).
2. Move the **"Installer"** folder *into* the **"Native Windows App Builder"** folder.
3. Whenever this guide says `C:\dev\WinShot`, substitute your actual path to "Native Windows App Builder" (e.g. `C:\Users\YourName\Documents\Claude\Projects\Native Windows App Builder`). Remember to wrap paths with spaces in double quotes when using PowerShell.

**Approach B — copy everything into a new clean folder:**

1. In File Explorer, create a new folder, e.g. `C:\dev\WinShot`. A short path with no spaces makes later commands easier.
2. Copy the **contents** of the Cowork folder **"Native Windows App Builder"** into `C:\dev\WinShot\`. You should now see files like `build-installer.ps1` and `WinShot.sln` directly inside `C:\dev\WinShot\`.
3. Copy the entire **"WinShot"** Cowork folder into `C:\dev\WinShot\`. You should now have `C:\dev\WinShot\WinShot\WinShot.csproj`.
4. Copy the entire **"Installer"** Cowork folder into `C:\dev\WinShot\`. You should now have `C:\dev\WinShot\Installer\WinShot.iss`.

If you can't find the "WinShot" or "Installer" folders on your computer at all, check the Cowork app sidebar — they need to be listed as selected folders there. If they aren't, re-add them via Cowork's "Add folder" option.

### Sanity check

Before moving on, verify these three files exist (use File Explorer or any way you like):

- `C:\dev\WinShot\build-installer.ps1`
- `C:\dev\WinShot\WinShot\WinShot.csproj`
- `C:\dev\WinShot\Installer\WinShot.iss`

If any are missing, you've got the layout wrong — re-check step 2/3/4.

---

## Section 3 — Build the installer

This is the easy part. The whole build is one command.

### Step-by-step

1. Press **Windows+X**, then click **Windows PowerShell** (or **Terminal**). You don't need to run this as administrator — building doesn't require admin rights. (Installing the result will, but that's later.)

2. In PowerShell, navigate to your repo folder:
   ```
   cd C:\dev\WinShot
   ```

3. **First time only:** allow PowerShell to run locally-authored scripts. Paste this and press Enter:
   ```
   Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
   ```
   If it asks for confirmation, type `y` and press Enter. This doesn't weaken your system's security — it just lets scripts you have on your own computer run without needing a publisher signature. It only affects your user account.

4. Run the build:
   ```
   .\build-installer.ps1
   ```

5. Wait. First build takes **2–5 minutes** (it downloads NuGet packages — small dependency files — and compiles everything). Subsequent builds are ~30 seconds.

### What success looks like

If it all worked, you'll see a green banner near the bottom of the output:

```
========================================================================
  Done
========================================================================
Installer: C:\dev\WinShot\dist\WinShotSetup.exe
Size:      XX.X MB
```

(The exact size depends on compression — typically 60–100 MB.)

**That's the file.** `C:\dev\WinShot\dist\WinShotSetup.exe` is the installer you wanted.

If the build fails, skip to Section 5 — Troubleshooting.

---

## Section 4 — Install WinShot from your new installer

1. Open File Explorer and go to `C:\dev\WinShot\dist\`.
2. Double-click `WinShotSetup.exe`.
3. Windows will probably show a blue screen saying **"Windows protected your PC"**. This is SmartScreen — it doesn't recognize the installer because you built it yourself, so it hasn't been "signed" by a recognized publisher. Click **More info → Run anyway**. This warning is normal for installers you build yourself. (Commercial software pays for a code-signing certificate that makes this dialog go away; it's not a bug in your build.)
4. **User Account Control (UAC)** will ask for admin permission. Click **Yes** — the installer writes into `C:\Program Files`, which requires it.
5. The Inno Setup wizard opens. Click **Next**. You'll see two optional checkboxes:
   - **Create a desktop shortcut** (unchecked by default — check if you want one)
   - **Start WinShot automatically when Windows starts** (checked by default — recommended; WinShot is a tray app that has to be running to do anything)
6. Click **Next → Install**, then **Finish** with "Launch WinShot now" checked.
7. The WinShot icon appears in your system tray (bottom-right of your screen, next to the clock). If you don't see it, click the small up-arrow `^` to expand hidden tray icons.
8. **Try it:** press **Ctrl+Shift+S**. Your screen should dim, and you can drag a rectangle to capture a region.

---

## Section 5 — Troubleshooting

### "The term 'dotnet' is not recognized..."

The .NET 8 SDK isn't installed or isn't on the PATH. Close PowerShell, reopen it, and try `dotnet --version` again. If it still fails, reinstall the SDK from Section 1A.

### "Could not locate Inno Setup (iscc.exe)"

Inno Setup isn't installed, or you installed it to a non-default location. Either reinstall it to the default path, or pass the path explicitly:
```
.\build-installer.ps1 -InnoSetupPath "C:\where\you\put\ISCC.exe"
```

### "File ... cannot be loaded because running scripts is disabled on this system"

You skipped the `Set-ExecutionPolicy` step. Run it now:
```
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

### "error MSB3644: The reference assemblies for ... were not found"

You have the .NET 8 **Runtime** installed, but not the **SDK**. Only the SDK can build software. Check with:
```
dotnet --list-sdks
```
You should see a line starting with `8.0.`. If not, install the SDK from Section 1A.

### The build produces errors I don't understand

Scroll up through the PowerShell output and find the first line that starts with `error CS...` or `error MSB...`. Copy that line and ask me to help. The first error is usually the real one; the later errors are often just cascading consequences.

### The build succeeds but `WinShotSetup.exe` isn't in the `dist` folder

Re-check that `Installer\WinShot.iss` exists. If it's missing, you forgot step 4 in Section 2.

### SmartScreen blocks the installer

Expected for unsigned installers. Click **More info → Run anyway**. If you plan to distribute WinShot to other people outside your own machine, buy an **EV code-signing certificate** (DigiCert, Sectigo, and similar sell these) and re-run the build with signing — but that's a separate project.

### I want a smaller installer

Yeah, 60–100 MB is a lot. It's big because we bundle the whole .NET 8 runtime inside it so users don't need to install anything extra.

If you'd rather have a ~1 MB installer that requires users to install .NET 8 first:

1. Open `WinShot\Properties\PublishProfiles\SingleFile.pubxml` in Notepad.
2. Find `<SelfContained>true</SelfContained>`.
3. Change `true` to `false`.
4. Save and rebuild.

The trade-off: a much smaller installer, but users who don't already have .NET 8 will see an error when they launch WinShot, and have to install the runtime from Microsoft's site.

### I need to rebuild after changing code

Just run `.\build-installer.ps1` again. If you only changed the Inno Setup script (`WinShot.iss`) and not the C# code, you can skip the slow compile step:
```
.\build-installer.ps1 -SkipPublish
```

---

## Glossary (for the no-coder path)

- **PowerShell** — a text-based shell for running commands on Windows. Works like the old "Command Prompt" but more capable.
- **SDK** — Software Development Kit. The toolbox that lets you build software. A **Runtime** only lets you run software someone else built.
- **NuGet** — .NET's package manager. When you build, it quietly downloads small libraries the source code depends on.
- **Publish** — compile the source into a form ready for distribution (EXE plus its dependencies).
- **Inno Setup** — takes the published output and wraps it into a single installer `.exe`.
- **SmartScreen** — Windows' defence mechanism that warns on unrecognized software. It's not calling your build broken; it just doesn't know who made it.
- **UAC (User Account Control)** — the "this app wants to make changes to your device" dialog. Standard for any installer that writes to `Program Files`.
- **Tray** — the area at the bottom-right of your screen with the clock and small icons. WinShot lives here.

---

## One-liner for later

Once everything is set up, rebuilding is literally this from `C:\dev\WinShot`:

```powershell
.\build-installer.ps1
```

That's it. Output lands in `dist\WinShotSetup.exe`.
