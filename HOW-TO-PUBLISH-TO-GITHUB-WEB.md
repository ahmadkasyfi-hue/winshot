# Publishing WinShot to GitHub — web-only edition (no installs)

This guide uses only your web browser. No Git installation, no PowerShell,
no command line — everything happens at github.com.

Total time first run: about **15 minutes**, mostly waiting for files to upload.

---

## What you'll end up with

Same outcome as the other tutorial:

- Your source code browsable at `https://github.com/YourUsername/winshot`
- `WinShotSetup.exe` downloadable from the **Releases** tab so anyone can
  install WinShot without building it themselves
- A README, license, and `.gitignore` displayed automatically

You can share the URL anywhere — LinkedIn, email, chat.

---

## What this approach is good for (and what it isn't)

**Good for**: a first publish, occasional small edits, and situations where
you don't want to install anything else on your computer. **This is the
right path for you right now.**

**Two limits to know:**

- Web uploads are capped at **25 MB per file** and **100 files per upload
  session**. Your source code is well under both. Your `WinShotSetup.exe`
  installer (60–100 MB) is too large for a web upload — but installers
  belong on a **Release page** anyway, which has a separate **2 GB per file**
  limit, so this isn't actually a problem.
- The web UI gets clunky when you're editing dozens of files at a time.
  If you find yourself reaching for the command line, switch over to the
  Git tutorial — but you don't need to today.

**Use Chrome, Edge, or Firefox.** They support dragging folders (with
their full structure) onto the upload area. Other browsers may not.

---

## Section 1 — Create your GitHub account

If you already have one, skip this.

1. Open https://github.com
2. Click **Sign up**. Pick a username — it becomes part of your repo URL,
   so choose something you're OK with publicly. Use your real email.
3. The free plan is fine for everything we'll do.
4. Verify your email (GitHub sends a confirmation link).

---

## Section 2 — Create the repository on GitHub

1. Open https://github.com/new (you must be logged in).
2. **Repository name**: `winshot` (lowercase, no spaces — easier to type later).
3. **Description** (short pitch — this shows up in search results):
   > Tray-resident screenshot and annotation utility for Windows. Press Ctrl+Shift+S, mark it up, copy or save.
4. **Public** vs **Private**:
   - **Public** = anyone on the internet can see it. Recommended if you
     want to share on LinkedIn.
   - **Private** = only you and people you invite can see it.
5. **Initialize this repository with — TICK ALL THREE:**
   - ✅ **Add a README file**
   - ✅ **Add .gitignore** → in the dropdown, type `Visual` and pick
     **VisualStudio**. (This is a pre-made list of files Git should
     ignore — the standard one for .NET projects.)
   - ✅ **Choose a license** → in the dropdown, pick **MIT License**.
     This is the simplest and most common permissive license.
6. Click **Create repository**.

You land on a repo page with three files: `README.md`, `.gitignore`,
`LICENSE`. The repo now exists — we just need to fill it with your code.

> **Why these three boxes matter for the web-only flow:** in the Git CLI
> tutorial we ticked NONE of them because the local repo already had its
> own. For the web-only flow we let GitHub create them, because there's
> no easy way to upload a hidden file like `.gitignore` through a Windows
> File Explorer drag-drop.

---

## Section 3 — Upload your source code

This is the main step. We'll drag-drop your project folders onto the
GitHub upload page in one batch.

### 3A. Open the upload page

On your repo page, click **Add file → Upload files**. Or go directly to:

```
https://github.com/YourUsername/winshot/upload/main
```

You'll see a big dashed zone that says "Drag files here..."

### 3B. Open File Explorer in a separate window

Go to your project folder:

```
C:\Users\Kasyfi\Documents\Claude\Projects\Native Windows App Builder
```

### 3C. Drag the right things, skip the wrong things

**Drag THESE onto GitHub's upload zone** (you can drag them all at once
— select multiple items in File Explorer, then drag the selection):

- `WinShot.sln` (the file)
- `build-installer.ps1` (the file)
- `build-msix.ps1` (the file)
- `HOW-TO-BUILD.md` (the file)
- `HOW-TO-PUBLISH-TO-GITHUB-WEB.md` (this file — optional, useful for future you)
- `app.manifest` if you see it at root level (it's actually under `WinShot\` so probably not)
- The whole **`WinShot\`** folder (drag the folder itself, not its contents — Chrome/Edge/Firefox preserve the folder structure)
- The whole **`Installer\`** folder (same: drag the folder)

**DON'T drag any of these** (they're either build outputs, caches, or
already exist in the repo):

- `dist\` — contains `WinShotSetup.exe` which goes in Releases (Section 5).
- `WinShot\bin\` — compile cache, recreated on every build.
- `WinShot\obj\` — compile cache, recreated on every build.
- `.vs\` — Visual Studio's cache (only exists if you opened the .sln in VS).
- `README.md` — GitHub already created one; we'll overwrite it in Section 4.
- `.gitignore` — GitHub already created one.
- Any file ending in `.pfx`, `.cer`, `.snk`, `.pvk` — code-signing keys
  must NEVER be uploaded to a public repo.
- `.DS_Store`, `Thumbs.db` — operating-system metadata clutter.
- `SKILL-NATIVE-WIN-APP.md` — your private project notes; only upload if
  you're comfortable making them public.

> **Tip if you're nervous about getting it right:** before uploading, make
> a temporary copy of `Native Windows App Builder` somewhere (e.g.
> `Desktop\winshot-upload`) and **delete** the unwanted folders inside
> the copy. Then drag everything from that clean copy. Costs you 30
> seconds and removes the chance of fat-fingering.

### 3D. Wait for the upload

Each file gets a green progress bar. Wait for them all to finish — for
~30 source files it takes 10–30 seconds. If a file fails, you'll see a
red X next to it; you can drag it again or remove it from the batch.

### 3E. Commit

Below the upload area:

1. **Commit message**: type something like `Initial source upload`. This is the description GitHub records for this batch of changes.
2. Leave the default option **"Commit directly to the main branch"**.
3. Click **Commit changes**.

GitHub processes the upload (typically a few seconds) and you land back on
the repo page. Your folders are now visible alongside the README, .gitignore,
and LICENSE.

---

## Section 4 — Replace the placeholder README with your real one

GitHub's auto-generated README only has the repo name. You already have a
good README — let's swap it in.

**Easiest method (overwrite via upload):**

1. Click **Add file → Upload files** again.
2. Drag your local `README.md` from `Native Windows App Builder\` onto the
   upload zone.
3. GitHub notices the filename matches an existing file and will replace it.
4. Commit message: `Replace placeholder README with project README`.
5. Click **Commit changes**.

**Alternative method (edit in browser):**

1. On the repo page, click `README.md`.
2. Click the **pencil icon** at the top right of the file.
3. Delete everything in the editor.
4. Open your local `README.md` in Notepad. Copy all of it. Paste into the
   GitHub editor.
5. Scroll down, set commit message, click **Commit changes**.

---

## Section 5 — Publish a Release with the installer

This is how `WinShotSetup.exe` becomes downloadable to anyone with the
URL. Releases allow files up to 2 GB, so the installer's size isn't a
problem here.

1. On the repo page, click the **Releases** link in the right sidebar.
   (Or go directly to `https://github.com/YourUsername/winshot/releases/new`.)
2. Click **Draft a new release**.
3. **Choose a tag** dropdown: type `v0.2.0`. A "Create new tag: v0.2.0 on
   publish" option appears — click that. (A tag is a name pinned to this
   specific version of the code.)
4. **Release title**: `WinShot 0.2.0`
5. **Description**: copy this template, tweak as you like:
   ```markdown
   First public release.

   Tray-resident screenshot + annotation utility for Windows 10 / 11.
   Press Ctrl+Shift+S, drag a region, mark it up — arrows, boxes, text,
   highlighter, mosaic pixelation for redacting passwords — then copy
   or save.

   ## Install
   Download `WinShotSetup.exe` below and run it.
   Windows SmartScreen may show a warning the first time — click
   **More info → Run anyway**. This is normal for an unsigned installer.
   ```
6. Scroll down to **"Attach binaries by dropping them here..."**.
7. From File Explorer, drag `dist\WinShotSetup.exe` onto that area (or
   click the area and browse for it).
8. Wait for the upload — 60–100 MB takes a minute or two. You'll see a
   progress indicator next to the file name.
9. Click **Publish release**.

The release is now live. People can download the installer from
`https://github.com/YourUsername/winshot/releases/latest`.

> **Heads-up:** when you build a new version of WinShot later, you make
> a new release with a new tag (`v0.3.0`, `v0.2.1`, etc.) and attach the
> new `WinShotSetup.exe`. Old releases stay around as a download archive.

---

## Section 6 — Updating files later (web-only)

### Edit a single file in place

1. Click the file in the repo browser.
2. Click the **pencil icon** at the top right.
3. Edit, scroll down, write a one-line commit message, click **Commit changes**.

This is fine for fixing typos in the README or tweaking small things.

### Replace many files / add a folder

1. Click **Add file → Upload files** on the repo page.
2. Drag the new/updated files in. Existing filenames get replaced.
3. Commit.

### Delete a file

1. Click the file in the repo browser.
2. Click the **trash can icon**.
3. Confirm + commit.

### Push a new release

Repeat Section 5 with a new tag. GitHub keeps the old releases visible
indefinitely.

### When the web flow gets painful

If you find yourself uploading files three or four times a week, or
needing to tweak many files at once, switch to the Git CLI flow — see
`HOW-TO-PUBLISH-TO-GITHUB.md` (the other tutorial in this folder).
You can switch to it any time without redoing anything; it just adds a
faster way to push changes you've made locally.

---

## Section 7 — Troubleshooting

### "Yowza, that's a lot of files. Try again with fewer than 100 files."

You hit the per-batch upload cap. Split into two batches: drag half the
items and commit, then come back to **Add file → Upload files** for the rest.

### "File is too big" / "Yowza, that file is too big."

Single-file 25 MB limit. Almost certainly you accidentally included
`dist\`, `bin\`, or `obj\`. Click the X next to the failed file in the
upload list, retry the upload without the offending folder.

### Folder structure didn't preserve — I see all my files at the root level

Your browser flattened the folder. Use Chrome, Edge, or Firefox. If
you're already using one of those, make sure you're dragging the FOLDER
itself (not opening the folder and dragging its contents).

### My upload silently stopped

Browser tab might've gone to sleep or your network blipped. Refresh the
page, check what's actually on GitHub, drag-drop only the missing items.

### I committed a file I shouldn't have (e.g. a private note, accidentally a key)

1. Click the file in the repo browser.
2. Click the trash can icon.
3. Commit the deletion.

> **Important caveat:** for SECRETS specifically (API keys, passwords,
> code-signing keys) — once they're pushed to a public repo, they should
> be considered compromised even after you delete them. Git history retains
> the file. Rotate the secret (issue a new one and invalidate the old one)
> rather than relying on the deletion.

### My README looks weird — text smashed together with no spaces

GitHub renders Markdown. If your local file uses Windows line endings
(`\r\n`), it should still work. The most common mistake is forgetting that
single line breaks in Markdown don't render as line breaks — you need a
blank line between paragraphs.

### The Releases page shows my installer is "uploading" forever

Refresh. Sometimes the upload completes but the page doesn't update.
Usually a hard refresh (Ctrl+F5) shows the file is actually there.

### I can't find the WinShot.ico file in `WinShot\Assets\`

Ensure your File Explorer is showing all files. Some Windows setups hide
icon files. The path should exist; if not, your build wouldn't have
produced the right tray icon.

---

## Glossary (for the no-Git path)

- **Repo (repository)** — a project on GitHub with its own history.
- **Commit** — a saved snapshot. The web UI makes one for each upload or edit.
- **Branch** — a named line of commits. `main` is the default; you'll only ever interact with `main` in the web flow.
- **Tag** — a permanent label on a specific commit. Used for marking releases.
- **Release** — a GitHub feature on top of tags that lets you attach
  built files (like installers) for download.
- **`.gitignore`** — a file telling Git which files to ignore. GitHub
  generated one for you when you created the repo.
- **README** — the file GitHub displays automatically on the repo's main
  page. Markdown formatted.
- **Issue** — a bug report or feature request. Other people (or you) can
  open them on the **Issues** tab.

---

## What you tell people

To download and try the app:
> Visit `https://github.com/YourUsername/winshot/releases/latest`,
> download `WinShotSetup.exe`, run it.

To browse the code:
> Visit `https://github.com/YourUsername/winshot`.

That's it. No mention of Git, no command-line steps, nothing for
non-developers to figure out.
