# UyghurEdit++ (fork: j0hnmurphy/UyghurEditPP)

C# / .NET Framework 4.8 WinForms. The editor is a renamed copy of AvalonEdit (namespace `UyghurEditPP.*`) hosted in an `ElementHost`.

## Build and test
- Build (this also builds the app): `"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" UyghurEditPP.Tests\UyghurEditPP.Tests.csproj -restore -p:Configuration=Debug`
- Test: `dotnet test UyghurEditPP.Tests\UyghurEditPP.Tests.csproj --no-build`
- `dotnet build` alone fails on the resources. Use MSBuild.
- Release installer: `scripts\build-installer.ps1`. Store package: `scripts\build-msix.ps1 -SkipBuild -IdentityName KenjisoftUyghur.UyghurEdit -Publisher 'CN=C222AB67-D740-48AB-B2F8-0F3600B80B3C' -PublisherDisplayName 'Kenjisoft Uyghur'`. The version comes from `Properties/AssemblyInfo.cs` (3 lines).

## Gotchas
- `UyghurEditPP.csproj` is old style: every new `.cs` needs a `<Compile Include>` line. The test project is SDK style (files are picked up).
- New files: UTF-8 with BOM and CRLF, like the rest. Do not rewrite CRLF files with sed.
- In worktrees, `packages/` is a junction to the main checkout. Remove only the link (`[IO.Directory]::Delete(path, $false)`) before deleting a worktree.
- Run the app with `UYGHUREDITPP_DATA_FOLDER=<scratch folder>` so it does not touch the owner's settings in %AppData%.
- `gh` defaults to the upstream repo: pass `--repo j0hnmurphy/UyghurEditPP`.
- No network code (`System.Net`) in the app.

## UI text (langdata.json)
- New entries use the English text as the key, with `uly`, `jpn` and `eng` fields. An empty `uly` shows the English text.
- Do not write Uyghur text unless the owner asks. Drafts use words already in the file, and the owner checks them.

## Releases
- Artifacts and publish guides: `E:\Github\UyghurEditPP-work\release\` (`publish-<version>.md`).
