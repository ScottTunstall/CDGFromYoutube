# The Windows installer

Builds a normal `setup.exe` for CDGFromYoutube: it installs the program under `Program Files`, adds it to
the PATH so `cdgfromyoutube` works from any Command Prompt, and registers an entry in "Installed apps" /
"Programs and Features" so it can be removed the same way as any other program. It does not bundle
yt-dlp, ffmpeg or Deno - `--download-tools` still fetches those on first run, as described in the main
[README](../README.md).

This installer is built with [Inno Setup](https://jrsoftware.org/isinfo.php), a free compiler that turns
one script into one `setup.exe`. It was chosen over heavier options such as WiX/MSI because a single
script is all a program this size needs, and because Inno Setup's uninstaller and "Installed apps" entry
come for free, with no extra project to write.

## Building it

Needs the .NET 10 SDK and [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install
JRSoftware.InnoSetup`). `build.ps1` looks for its compiler, `ISCC.exe`, on the `PATH` and in the
locations winget and the Inno Setup installer both use by default, so no extra setup is needed either
way.

```console
installer\build.ps1
```

This publishes CdgFromYoutube (framework-dependent, so the installer is what fetches the .NET runtime,
not the program itself) into `installer\publish`, then compiles `CDGFromYoutube.iss` into
`installer\Output\CDGFromYoutubeSetup.exe`. The version stamped into the installed program's entry is
read from `<Version>` in `CdgFromYoutube.csproj` (the same version `--version` reports); pass
`-Version 1.2.0` to stamp a different one instead.

## What it does

* Installs the published files into `Program Files\CDGFromYoutube`.
* Offers to add that folder to the machine `PATH` (checked by default), so `cdgfromyoutube` can be run
  from a new Command Prompt without typing the full path. Unchecking it still installs the program; it
  just has to be run by its full path, or from the Start Menu shortcut this installer also adds.
* Runs Microsoft's own `dotnet-install.ps1` script to install the .NET 10 runtime, if a machine-wide
  install is not already there. The script checks the installed version itself and does nothing when it
  is new enough, so this step costs nothing on a machine that already has .NET, and does not repeat on
  a second run of the installer. If the download fails - no internet, a blocked connection - the program
  still installs; when it is later run without the runtime, Windows shows its own "you need .NET" prompt,
  exactly as it would for any other .NET program.
* Registers the standard uninstaller. Remove the program from Windows Settings > Apps, or Control Panel >
  Programs and Features, like any other program; this also removes the PATH entry.

## What it does not do

* It does not sign the installer or the program, so Windows SmartScreen may warn about it on a machine
  that has not seen it before. Signing needs a code signing certificate, which is a separate decision.
* It targets 64 bit Windows only (`ArchitecturesAllowed=x64compatible`), matching the tools the program
  itself depends on, which are only published as 64 bit Windows executables.
