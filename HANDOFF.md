# Current handoff — v0.7.0 release candidate

- Branch/HEAD: `codex/v0.7.0` at its pushed release-preparation tip (`git rev-parse HEAD`); `main`/`origin/main` remain `33838408f29b853c8945a0595fc4400420d41d27`.
- Phase 5 was physically accepted by the product owner. Final full-diff audit found no P0/P1; localized P2 release defects were fixed. The current visual design is frozen.
- Gates: locked restore and Release build passed (0 warnings/errors); full suite 504/504 with 0 skips; package-policy test, publish, installer build, final package verification and native portable navigation smoke passed. Output stayed Off. No real OSC delivery or installer install/uninstall was tested.
- Candidate: ignored `artifacts/v0.7.0/` holds the portable ZIP (SHA256 `a54af8f9eee7cdd15720e646d9c4a880124c278562753fd6cc887552476957c5`) and unsigned installer (SHA256 `43222c405dc3a6305916b79d3605fa4d5b567eeefb2358f95531578872e41f53`), with checksum sidecars. Public release notes are in `docs/RELEASE-NOTES-v0.7.0.md`; PLAN has detailed evidence.
- Next: product owner checks these final artifacts on Windows, including installer install/uninstall, preserved state and Output safety, then explicitly authorizes publication. No v0.7.0 tag, GitHub Release, main merge or publication yet.
