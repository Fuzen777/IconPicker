# Code Signing

> [!abstract]
> Sign Icon Picker's releases for free through the SignPath Foundation, so Windows stops warning people who download it ("Windows protected your PC", "Unknown publisher"). Releases are then built by GitHub Actions from the public repository, sent to SignPath for signing, approved by hand, and published with the signed files. The first release ships unsigned; this spec covers what comes after.

## Goal

An unsigned app downloaded from the internet meets SmartScreen's "Windows protected your PC" screen, its UAC prompt for "Retry as admin" reads "Unknown publisher", and antivirus heuristics trust it less. SmartScreen reputation for unsigned files builds per file, so every new version starts from zero. Signing removes the "Unknown publisher" prompt and lets reputation carry across versions.

SignPath Foundation signs open-source projects for free. The certificate is issued to SignPath Foundation, so Windows shows **SignPath Foundation** as the publisher, not the project's author.

## SignPath Foundation's Conditions

From [signpath.org/terms](https://signpath.org/terms), checked in October 2026; the live page wins.

| Condition | Where Icon Picker stands |
| --- | --- |
| Every component under an OSI-approved license, no commercial dual-licensing | The app is MIT. Its libraries are MIT, BSD or MS-PL (see THIRD-PARTY-NOTICES.md), except the Windows App SDK (see Open Questions). |
| No proprietary code, except "System Libraries" as defined in the GPL v3 | The bundled Windows App SDK runtime is under Microsoft's license terms: to be confirmed with SignPath. |
| Actively maintained, already released in the form that will be signed, functionality documented on the download page | Met once the first unsigned release is on GitHub Releases with the README. |
| Signed files come from automated builds of the stated repository, verifiably, with build scripts reviewed as source | Needs the release workflow below. |
| Every signing request approved by hand | The maintainer approves each release in SignPath. |
| Only the project's own binaries are signed | `IconPicker.exe` and `IconPicker.dll`. Microsoft's files are already signed by Microsoft. |
| Product name and version set and identical across builds | `Product` and `Version` in `IconPicker.App.csproj`, checked by the workflow. |
| Team roles (authors, reviewers, approvers) listed, MFA on SignPath and the repository | One maintainer holds every role; two-factor sign-in on GitHub and SignPath. |
| A "Code signing policy" page linked from the home and download pages, with the SignPath attribution, the roles and a privacy statement | A `CODE-SIGNING.md` in the repository, linked from the README and every release's notes. |
| No user data sent anywhere without a privacy policy and an opt-out | The app sends nothing anywhere: the privacy statement says so. |
| No system changes without clear warnings; uninstall instructions provided | The right-click entry is opt-in and Restart Explorer asks first; the README explains removal. |

## Resolved Decisions

| Decision | Choice | Rationale |
| --- | --- | --- |
| Signing service | SignPath Foundation | Free for open source; the alternatives cost money (Azure Trusted Signing about $10 a month, a standard certificate more) and an individual's certificate shows the person's legal name. |
| First release | Unsigned | SignPath requires an existing release, and nothing blocks shipping without it. |
| What is signed | `IconPicker.exe` and `IconPicker.dll` | The only files built from this repository. |
| Where builds run | GitHub Actions, on a version tag | SignPath requires CI builds from the public repository, and Actions is free for public repositories. |

## Assumptions

- The repository stays public at github.com/Fuzen777/IconPicker under the MIT license.
- Releases stay portable zips (`IconPicker-<version>-win-x64.zip`); no installer is signed.
- The maintainer accepts SignPath Foundation, not their own name, as the publisher Windows shows.

## Scope

### In Scope

- A release workflow in GitHub Actions: on a `v*` tag, build, test, publish, zip, and attach the zip to a GitHub release. It can come before signing, since releases need it anyway.
- The SignPath application, project set-up and signing policy.
- The signing step in the workflow, the `CODE-SIGNING.md` page, and README and release-note links to it.

### Out of Scope

- An installer, MSIX packaging, winget or the Microsoft Store.
- Signing for platforms other than Windows x64.

## Affected Areas

- `.github/workflows/release.yml` (new).
- `Scripts\Package.cmd` (new): the same publish-and-zip steps for a local build, so the workflow and a hand-made release don't drift.
- `CODE-SIGNING.md` (new), `README.md`, `THIRD-PARTY-NOTICES.md`.
- `IconPicker.App.csproj`: version metadata checked by the workflow.

## Release Workflow

1. Triggered by pushing a tag such as `v0.2.0`, which must equal `<Version>` in `IconPicker.App.csproj`; a mismatch fails the run.
2. On `windows-latest`, with the .NET 10 SDK from `global.json`.
3. Runs `Scripts\Check.cmd`: build with warnings as errors, tests, formatting.
4. Publishes Release, self-contained, win-x64, and zips the folder.
5. Signing step (once SignPath is set up): uploads the unsigned zip as a build artifact and submits a signing request with SignPath's GitHub action, using the project's signing policy; waits for approval; downloads the signed zip.
6. Creates the GitHub release (a pre-release for 0.x versions) with the zip, its SHA-256 checksum, and notes linking `CODE-SIGNING.md`.

## Setting Up SignPath

1. After the first unsigned release, apply at signpath.org/apply with the repository, license and release links.
2. Once accepted: create the project in SignPath.io, connect the GitHub repository as a trusted build system, define an artifact configuration covering `IconPicker.exe` and `IconPicker.dll` inside the zip, and a release signing policy that needs manual approval.
3. Store SignPath's API token as a GitHub Actions secret, never in the repository.
4. Turn on two-factor sign-in for SignPath (GitHub already has it, or it gets turned on).
5. Publish `CODE-SIGNING.md` and link it.

## Behavior & UX

- People downloading a signed release see SignPath Foundation as the publisher in SmartScreen and in the UAC prompt of "Retry as admin".
- SmartScreen may still warn for a while on new releases until the certificate's reputation builds.
- Nothing changes inside the app.

## Edge Cases & Risks

- **SignPath refuses the project** over the Windows App SDK. Then: ask whether a framework-dependent build (the Windows App SDK runtime installed separately from Microsoft instead of bundled) would qualify, or fall back to a paid service.
- **A signing request is never approved**, for example while the maintainer is away: the workflow times out and no release is published, which is safe.
- **The certificate is revoked** after a breach of the conditions: published files stay signed with a revoked certificate and SmartScreen warns again. Keeping the conditions avoids it.
- **SmartScreen still warns** on early signed releases: reputation builds with downloads.

## Open Questions

1. Does SignPath accept the bundled Windows App SDK runtime as a system library? Ask when applying.
2. Does SignPath require more than one team member? The terms describe roles, not a minimum team size; ask when applying.
