# Release runbook (code-signed)

How a release of MPP Viewer is produced. The binary is **built in CI** (reproducible) and
**signed locally** before it is attached to a GitHub release.

## Why signing is local, not in CI

The signing certificate is a **Certum Open Source Code Signing** certificate, held in
**SimplySign cloud** (the private key lives in Certum's cloud HSM, not on a USB token).

Certum exposes the cloud key only through the **SimplySign Desktop** application, which
requires a manual TOTP login (a 2-hour session) and has **no headless API**. Automating it
inside GitHub Actions is only possible through fragile GUI-injection workarounds, which are
not worth maintaining for an infrequently released project. Therefore:

- **CI** keeps building the unsigned `mpp-viewer-win-x64` artifact (reproducible).
- **A human** downloads that artifact and signs it on a local Windows machine, then uploads
  the signed `.exe` to the release.

## One-time setup

Done once, then reused for every release.

1. **Buy the certificate.** Certum → *Open Source Code Signing* → **"w chmurze" (cloud /
   SimplySign)** variant (~245 PLN gross / 1 year). Not the token bundle.
   - Individual validation as an "Open Source Developer"; Certum verifies your identity and
     that the project is genuinely open source (point them at this public repository).
2. **Install SimplySign Desktop** on the Windows machine you will sign from, and pair the
   **SimplySign mobile app** (it provides the TOTP code used to log in).
3. **Install the Windows SDK** so `signtool.exe` is available (ships with the Windows 10/11
   SDK; also bundled with Visual Studio).
4. **Confirm the certificate is visible.** Log in through SimplySign Desktop, then check that
   the certificate appears in the Windows store:
   ```powershell
   Get-ChildItem Cert:\CurrentUser\My
   ```
   Note the **Subject (CN)** and **Thumbprint** — you will reference one of them when signing.

## Per-release steps

1. **Cut the release on `dev` → `main` as usual** (see CLAUDE.md "Branch workflow & release"):
   bump `<Version>`/`<FileVersion>` in `MppViewer.csproj`, merge `dev` into `main` with
   `--ff-only`, let CI build.
2. **Download the CI artifact** `mpp-viewer-win-x64` from the `main` build run (this is the
   reproducible binary that will ship — do not substitute a local build).
3. **Log in to SimplySign Desktop** (enter the TOTP from the mobile app). The session lasts
   ~2 hours; sign within that window.
4. **Sign the binary** with a SHA-256 signature and an RFC-3161 timestamp:
   ```powershell
   signtool sign `
     /n "YOUR CERTIFICATE COMMON NAME" `
     /fd sha256 `
     /tr http://time.certum.pl `
     /td sha256 `
     MppViewer.exe
   ```
   - `/n` selects the cert by Subject CN. Alternatively pin it exactly with
     `/sha1 <THUMBPRINT>`.
   - `/tr` + `/td` add a trusted timestamp so the signature stays valid **after the
     certificate expires** (without it, the signature dies when the 1-year cert lapses).
5. **Verify the signature** before uploading:
   ```powershell
   signtool verify /pa /v MppViewer.exe
   ```
   Expect "Successfully verified" and a visible timestamp. You can also right-click the
   `.exe` → Properties → **Digital Signatures** tab and confirm your name is listed.
6. **Create the release** with the **signed** binary:
   ```bash
   gh release create vX.Y.Z --target main MppViewer.exe
   ```

## SmartScreen reputation is not instant

Signing makes Windows show your name as the publisher instead of "unknown publisher", but
**Microsoft SmartScreen reputation builds gradually** as signed downloads accumulate. Early
downloads of a freshly-signed app may still show a warning; it fades over time and the
reputation carries forward to later releases signed with the same certificate.

## When the first signed release ships

Update the README to reflect that the binary is now signed:

- The **"Windows will warn that this app is unrecognized"** section and the **"Using MPP
  Viewer in an organization"** section currently say the exe is *not* code-signed and that
  signing is "on the roadmap". Soften these to: the exe **is** signed with an OV
  certificate, SmartScreen reputation is still building, so a warning may briefly appear.
- Do **not** edit those sections before a signed binary is actually published — until then
  the unsigned wording is correct.
