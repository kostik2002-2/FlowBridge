# Firefox installation correction in installer 0.1.2

The signed extension remains 0.1.1, unchanged. Only the Windows installer changes.

The previous installer could accept the presence of an old extension card in Firefox's accessibility tree as confirmation of replacement, and returned success after sending navigation without checking that Flow opened.

The installer now:
- reads the installed version from the active Add-ons Manager document;
- reopens that manager after the XPI prompt and waits for version 0.1.1;
- resolves the extension address only from its own manifest link in the active debugging document;
- rejects conflicting UUIDs instead of selecting another extension's address;
- confirms navigation to a new Flow tab before reporting completed preparation.

Validation on 2026-10-31:
- installed the archived, Mozilla-signed 0.1.0 over the installed extension without removing it;
- ran the new installer, which read 0.1.0, upgraded to 0.1.1, verified the version and confirmed the new Flow tab;
- the Flow page exposed the project list; no generation was submitted;
- repeated installation returned already-installed and preserved the browser tabs and window bounds;
- the signed 0.1.1 XPI is byte-for-byte unchanged.
