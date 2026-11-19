# 0005 Accounts, sign-in and setup

Status: accepted

- Cookie authentication (`playground.auth`, HttpOnly, SameSite=Lax, Secure over HTTPS, 14 days sliding). No email, no external providers, no passkeys: quick tunnel hostnames change, which would orphan passkeys.
- Passwords: PBKDF2-HMAC-SHA512, 210,000 iterations, 16-byte salt, stored as `pbkdf2-sha512$<iterations>$<salt>$<hash>`. Raising the iteration count rehashes on the next sign-in. Built into .NET, so no package. At least 12 characters, not the user name, not one repeated character.
- Every cookie carries the account's security stamp. At most once a minute per browser the stamp, the disabled flag, the display name and the administrator role are checked against the database. A password change or disabling an account rotates the stamp and signs the account out everywhere; role changes reach the cookie within a minute without a sign-out.
- Sign-in, registration, setup and password changes are throttled per network address and per user name (10 burst, one every 10 s). Unknown user names still pay for a hash so timing does not reveal them.
- `/setup` creates the first administrator. It needs a one-time code that is generated at start-up while no administrator exists and printed to the server log (`scripts/instance.sh setup-code`). The public URL is reachable by anyone, so the code is what stops a stranger from claiming the site. A table lock makes a second concurrent setup fail.
- Registration is open by default and can be closed by an administrator.
- Two-step sign in with an authenticator app is optional per account (ADR 0014). Turning it on or off rotates the stamp.
