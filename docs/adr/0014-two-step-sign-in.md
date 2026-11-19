# 0014 Optional two-step sign in with authenticator apps

Status: accepted

- Anyone can turn on TOTP (RFC 6238) on `/account`; nobody is required to. Passkeys stay out: they are bound to a domain and quick tunnel hostnames change. TOTP codes do not depend on the address.
- Parameters every app understands: SHA-1, 6 digits, 30 s, a 20-byte random secret, one step of drift either way. A code's step must be newer than the last accepted step (`person_totp.last_step`, updated with a conditional `UPDATE`), so a code works once.
- Setup: `Set up an authenticator app` stores a pending secret (`enabled_at IS NULL`) and shows a QR code of the `otpauth://` URI, the key in groups of four and an `otpauth://` link. The QR code is an inline SVG drawn by `Domain/Graphics/QrCode` (byte mode, level M, versions 1 to 10, no package), so the CSP is unchanged. Entering a valid code turns it on.
- Secrets are encrypted with ASP.NET Data Protection (purpose `playground.totp.v1`), whose keys are already in PostgreSQL. A secret that cannot be decrypted is treated as a cancelled setup.
- Turning it on makes ten recovery codes (`xxxxx-xxxxx`, 31 unambiguous characters, about 49 bits), shown once, stored as SHA-256 in `person_recovery_code`, each usable once. New codes need the password and replace the old ones.
- Turning it on or off rotates the security stamp, so other sessions sign in again; the current browser is signed in again with the new stamp. Turning it off needs the password.
- Sign in: a correct password for an account with TOTP does not issue the site cookie. It issues `playground.2fa` (path `/account`, SameSite=Strict, 5 minutes, holds the person id, security stamp and the remember choice) and redirects to `/account/sign-in/code`, which accepts a TOTP code or a recovery code. The stamp must still match. Attempts are throttled per network address and per account like sign-in.
- Administrators can turn it off for someone who lost both phone and codes (`Turn off two-step` on `/admin`). Turning on, off, renewing codes, using a recovery code, resets and wrong codes are in the audit log.
