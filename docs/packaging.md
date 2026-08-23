# Windows packaging

## Publishing choices

- A **framework-dependent** publish is smaller, but the destination computer must already have the matching .NET Desktop Runtime.
- A **self-contained** publish includes the .NET runtime. It is larger, but a user can extract it and run the application without installing developer tools.
- An **installer or MSIX** can add Start-menu integration and managed upgrades, but it introduces signing, installer tooling, and Windows deployment-policy decisions.

MyIPTV 1.0 uses a self-contained Windows x64 ZIP. This is the most dependable beginner-friendly option for the first release and keeps LibVLC's native runtime files beside the executable. Trimming and single-file bundling are disabled because WPF and LibVLC rely on reflection and native companion files.

The x64 packaging script removes the unused LibVLC ARM64/x86 runtimes, native import libraries, and LibVLC HTTP-interface scripts. Noki's IPTV Player does not enable LibVLC's administrative HTTP interface. Release ZIPs should be scanned both as an archive and as an unpacked directory before upload.

## Create the package

From the repository root, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-win-x64.ps1
```

The script creates:

- `artifacts\Nokis-IPTV-Player-1.2.0-beta.2-win-x64.zip`
- `artifacts\SHA256SUMS.txt`
- the unpacked verification copy under `artifacts\publish\win-x64`

## Install and run on another PC

1. Use a supported 64-bit Windows 10 or Windows 11 computer.
2. Copy the ZIP to that computer.
3. Right-click the ZIP, choose **Extract All**, and open the extracted folder.
4. Double-click `MyIPTV.App.exe`.

Windows may show a reputation warning because this local build is not code-signed. Verify the SHA-256 value before running. A future public installer should be Authenticode-signed and built in a controlled release pipeline.
