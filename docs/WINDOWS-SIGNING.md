# Windows signing investigation

Checked 2026-09-10. The current development `LyricsChatbox.exe` and the downloaded public v0.4.0 installer both return `NotSigned` from `Get-AuthenticodeSignature`, with no signer certificate. The repository has no configured signing workflow. SHA256 download verification checks integrity against the release checksum; it does not establish an Authenticode publisher identity.

A public release needs a legitimate code-signing certificate or a managed public-trust signing service, validated publisher identity and controlled access to its private key. Sign the application first, package that exact application into both distributions, then sign the installer; verify signatures/timestamps before calculating release checksums. Microsoft documents [SignTool signing, timestamping and verification](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool). Signing does not guarantee that SmartScreen will never warn: reputation also depends on the downloaded file. See the [Artifact Signing FAQ](https://learn.microsoft.com/en-us/azure/artifact-signing/faq).

## Available paths

| Path | Requirements and constraints |
|---|---|
| Microsoft Artifact Signing, Public Trust | Azure subscription, Entra tenant, paid account, identity validation and certificate profile. Individual developers must currently be in the US or Canada; organizations have a broader supported-country list. Maintainer eligibility has not been established. Private/Test Trust is not a substitute for public distribution. [Setup and eligibility](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart) |
| SignPath Foundation | Conditional free signing for accepted, maintained OSS projects with an OSI-approved license, verifiable builds, named roles, a published signing/privacy policy and release approval. Its certificate identifies SignPath Foundation as publisher. This repository currently has no license for its original code, so eligibility is not established; a maintainer must choose the license and apply. No license is selected by this investigation. [Foundation conditions](https://signpath.org/terms.html) |

Artifact Signing currently lists Basic at US$9.99/month for 5,000 signatures, with US$0.005 for each additional signature. Premium is US$99.99/month for 100,000 signatures. Actual billing depends on the agreement/currency. This is an option to assess, not an approved purchase. [Microsoft pricing](https://azure.microsoft.com/en-us/products/artifact-signing)

## GitHub Actions requirements

For the Azure route, configure an Entra application/service principal, a narrowly scoped federated GitHub identity and the Certificate Profile Signer role. The signing job needs `id-token: write`, Azure client/tenant/subscription identifiers, account/region/profile configuration, Azure login and the signing action. OIDC avoids committing a long-lived signing key; the service holds the private key. [Official OIDC integration](https://github.com/Azure/artifact-signing-action/blob/main/docs/OIDC.md)

Recommended integration: limit signing to an approved release environment/ref, pin actions to reviewed commits, restrict who can approve releases, and verify the exact signed application and installer before publishing. Ordinary pull-request CI should remain unsigned and unable to request signatures. A hardware-backed commercial certificate would instead require a compatible token/HSM or cloud signing integration; no such device, contract or certificate has been provided for this project.

No account, certificate, purchase or signing secret was created. v0.5 may ship unsigned as authorized, with that state stated explicitly. Recheck signatures on the exact final artifacts; do not describe an unsigned or self-signed build as trusted.
