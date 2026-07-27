# Security Policy

Breeze is an early stage project. It embeds the Microsoft Edge WebView2 runtime, so page
rendering security depends on that runtime being kept up to date by Windows.

## Supported versions

| Version | Supported |
| --- | --- |
| `master` (latest commit) | Yes |
| Tagged releases before the first stable release | No |

Only the latest commit on `master` receives fixes. There is no long term support branch yet.
Fixes for the WebView2 rendering engine itself come from Microsoft through Windows Update.

## Reporting a vulnerability

Please report privately, not in a public issue.

- Use GitHub's private vulnerability reporting on this repository
  (Security → Report a vulnerability).
- If that is unavailable, open a public issue that says only that you have a security report and
  asks for a private contact. Do not include details.

A useful report includes the affected commit, reproduction steps, the impact you believe it has,
and any proof of concept. Reports about the WebView2 runtime or Chromium itself should go to
Microsoft; reports about how Breeze configures or hosts it belong here.

## What to expect

| Stage | Target |
| --- | --- |
| Acknowledgement of your report | 5 working days |
| Initial assessment and severity | 10 working days |
| Fix or documented mitigation for high severity issues | 30 days where practical |

This is a spare time project, so these are targets rather than guarantees. If a report goes
unanswered past the acknowledgement window, please ping the thread.

## Disclosure policy

- Coordinated disclosure. Please give us a chance to fix the issue before publishing.
- 90 days from acknowledgement is the default window, shortened by agreement if a fix ships
  earlier, or extended by agreement if a fix is genuinely complex.
- We will credit reporters in the release notes unless you prefer otherwise.
- If an issue is already being exploited, we will prioritise shipping a fix and publishing an
  advisory over holding to the window.

## Out of scope

- Findings that require an attacker to already have code execution or file write access as the
  user, since Breeze stores its data unencrypted in the user profile and cannot defend against
  an attacker at that level.
- Missing security features that are documented as not yet implemented in the README.
- Vulnerabilities in the WebView2 runtime, Chromium, Avalonia or .NET, unless Breeze's
  configuration makes them materially worse.
