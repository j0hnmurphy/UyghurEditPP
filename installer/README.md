# Installer

`UyghurEditPP.iss` is an Inno Setup 6 script that builds a per-user (no admin rights), x64-only setup.exe.

The optional file-association task adds UyghurEdit++ to "Open with" for .txt (it never changes the default .txt program) and makes it the default for .uut only when no other program owns .uut. Uninstalling removes only those entries; settings in `%AppData%\UyghurEditPP` are kept.

## Requirements

- Visual Studio / Build Tools with MSBuild (found through `vswhere`)
- Inno Setup 6.3 or later (`winget install JRSoftware.InnoSetup`): the script uses `x64compatible`, added in 6.3.0. Built with 6.7.3.

## Build

```powershell
powershell -File scripts\build-installer.ps1
```

This builds Release into `bin\UyghurEditPP\`, runs ISCC, and prints the path, size and SHA-256 of
`installer\Output\UyghurEditPP-<version>-setup.exe`. The version is read from the built exe
(AssemblyInformationalVersion).

## Notes

- `AppId` in the script must never change (upgrades and uninstall depend on it).
- The optional "associate files" task writes only under `HKCU\Software\Classes`; uninstall removes only its own ProgID and values.
- User settings in `%AppData%\UyghurEditPP` are never removed by uninstall.
- Setup checks for .NET Framework 4.8 and aborts with a message if it is missing.
