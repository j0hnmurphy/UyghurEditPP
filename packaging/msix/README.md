# MSIX package for UyghurEdit++

This folder builds the Microsoft Store (MSIX) version of UyghurEdit++ by hand, without Visual Studio's packaging project:
a hand-written `AppxManifest.xml`, the visual assets, `makepri` and `makeappx`.
The approach follows Microsoft's
[Generating MSIX package components](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-manual-conversion).

| File | Purpose |
|---|---|
| `AppxManifest.xml` | Manifest template. `__IDENTITY_NAME__`, `__PUBLISHER__`, `__PUBLISHER_DISPLAY_NAME__`, `__VERSION__` are replaced by the build script. |
| `Assets/` | Logos, generated from `uyghur.ico` by `scripts/make-msix-assets.ps1` (scale 100 only). |
| `Output/` | The built `.msix` (git-ignored). |
| `staging/` | Temporary folder the package is built from (git-ignored). |

## Build

Needs Visual Studio (MSBuild) and the Windows SDK 10.0.26100 (`makeappx.exe`, `makepri.exe`, `signtool.exe`).

```powershell
# Unsigned, test identity (this is enough to check that the package is valid)
.\scripts\build-msix.ps1

# Reuse the existing Release build
.\scripts\build-msix.ps1 -SkipBuild
```

Output: `packaging\msix\Output\UyghurEditPP-<version>.msix`. The version comes from the exe
(`0.84.0.0`; the last part must stay 0 for the Store). To regenerate the logos: `.\scripts\make-msix-assets.ps1`.

The package holds the same files as the release zip. The app runs as a full-trust packaged desktop app
(`runFullTrust`, `EntryPoint="Windows.FullTrustApplication"`), and opens `.uut` and `.txt` files by file type
association; Windows passes the file path as the command line (the verb's `Parameters`), as described in
[Integrate your desktop app with Windows by using packaging extensions](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/desktop-to-uwp-extensions).
The install folder is read-only; settings and logs go to `%AppData%` (see `AppPaths.cs`), which the OS redirects per package
([how packaged desktop apps run](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes)).

## Test-install on your own PC (needs an administrator PowerShell)

An unsigned or self-signed package only installs after its certificate is trusted. Do this on a test PC and remove it afterwards.
The certificate Subject must equal the manifest `Publisher`.

```powershell
# 1. Self-signed certificate whose Subject equals the Publisher used in the build
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=UyghurEditPP Test" `
  -KeyUsage DigitalSignature -FriendlyName "UyghurEditPP test" -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# 2. Export a PFX (pick your own password) and the public certificate
$pw = Read-Host -AsSecureString "PFX password"
Export-PfxCertificate -Cert $cert -FilePath "$env:TEMP\uyghureditpp-test.pfx" -Password $pw
Export-Certificate    -Cert $cert -FilePath "$env:TEMP\uyghureditpp-test.cer"

# 3. Trust it (administrator)
Import-Certificate -FilePath "$env:TEMP\uyghureditpp-test.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople

# 4. Build and sign
.\scripts\build-msix.ps1 -SkipBuild -SignWithPfx "$env:TEMP\uyghureditpp-test.pfx" -PfxPassword $pw

# 5. Install, try it, remove it
Add-AppxPackage .\packaging\msix\Output\UyghurEditPP-0.84.0.0.msix
Get-AppxPackage UyghurEditPP.Test | Remove-AppxPackage
```

Things to check after installing: it starts from the Start menu; double-click a `.txt` / `.uut` file opens it (Windows may ask you to choose the
default app; the package only offers the association); settings are saved after restart; spell checking and the fonts work.

Cleanup: delete the certificate from `Cert:\LocalMachine\TrustedPeople` and `Cert:\CurrentUser\My`, and delete the PFX/CER files.
Reference: [Create a certificate for package signing](https://learn.microsoft.com/en-us/windows/msix/package/create-certificate-package-signing).
These steps have not been run by the build author (no installs were allowed while writing this); treat them as untested.

## Windows App Certification Kit (WACK)

Microsoft recommends testing with the kit before submission
([App package requirements](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements)).
The kit is part of the Windows SDK (`appcert.exe` / `appcertui.exe` in `C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64`
or the Windows Kits `App Certification Kit` folder). Run `appcertui.exe`, choose "Validate Microsoft Store App", select the installed
package, and read the report. Run `appcert.exe /?` for the command-line form. Not run yet.

## After reserving the name in Partner Center

Take these from Partner Center (Product identity page) and pass them to the build script:

```powershell
.\scripts\build-msix.ps1 -IdentityName "<Package/Identity/Name>" `
  -Publisher "<Package/Identity/Publisher, CN=...>" `
  -PublisherDisplayName "<Package/Properties/PublisherDisplayName>"
```

Submit the unsigned `.msix`; the Store signs it. Defaults in the script (`UyghurEditPP.Test` etc.) are test values only and
must not be submitted.
