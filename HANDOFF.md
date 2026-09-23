# Handoff — LyricsChatbox v0.7.0 publication

- Release-candidate source/artifact commit: `cbabf1a8ef24715e6acf517834e46a6090ace3a8`. The exact candidate ZIP and unsigned installer are in ignored `artifacts/v0.7.0/`; sizes, SHA256 sidecars and `Verify-Package.ps1` results match the prepared values.
- Windows CI run `35801740451` completed successfully on that exact SHA. `main` and `origin/main` were `33838408f29b853c8945a0595fc4400420d41d27`; the candidate was 11 commits ahead. The pre-publication check found no v0.7.0 tag or GitHub Release.
- The product owner authorized publication and waived the additional final ZIP/installer physical smoke: `WAIVED / NOT PERFORMED BY PRODUCT OWNER`. The earlier native portable navigation smoke and automated validations remain valid. Installer install/uninstall and real OSC delivery were not validated.
- The waiver/authorization update is documentation-only; artifacts remain tied to the candidate commit above and must not be rebuilt when their hashes match.
- Continue by integrating the approved candidate into `main`, validating main CI, creating the immutable tag and public release, independently verifying the release, then cleaning only the v0.7.0 development branch/worktree. Do not start v0.8.0 in this task.
