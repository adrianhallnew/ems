# ADR-0005: Adopt the Blazor Identity Template

**Status:** Accepted
**Date:** 2026-08-12

## Context

Version 2.0 scaffolded a plain Blazor project and specified `AddIdentity` with a `LoginPath`, then listed `Login.razor` among the interactive pages built in the UI phase.

That does not work. `SignInManager.PasswordSignInAsync` must write an authentication cookie, which requires a writable HTTP response. An Interactive Server component runs over a WebSocket connection and has none. The login page would render, accept input, and fail to authenticate — and the failure mode gives no hint as to why.

The same constraint applies to sign-out, password change, and the forced password reset flow.

Version 2.0 also had no mechanism for three related requirements: terminating a deactivated employee's live session, applying a role change without a sign-out, and enforcing the 30-minute session timeout on an open connection where cookie expiry never fires.

## Decision

Generate the web project with Individual Accounts:

```
dotnet new blazor --interactivity Server --auth Individual
```

Keep the template's account infrastructure and remove the features that are out of scope: self-registration, email confirmation, external logins, and two-factor authentication.

## What the template supplies

| Component | Problem it solves |
|---|---|
| Account pages as static SSR | `SignInManager` gets a writable response |
| `IdentityRevalidatingAuthenticationStateProvider` | Security stamp re-checked on an interval, which is what makes deactivation, role change, and session expiry take effect on a live connection |
| `IdentityRedirectManager` | Redirects that work correctly from static SSR components |
| `IdentityUserAccessor` | Consistent current-user resolution in account pages |
| Endpoint routing, antiforgery, cascading auth state | Wired correctly, in the right order |

One mechanism — security stamp revalidation — resolves three separate v2.0 defects. That is the strongest argument for adopting the template rather than assembling the pieces.

## Alternatives considered

**Hand-roll on `AddIdentityCore`.** Full control, and it forces an understanding of each piece. Rejected: it means re-deriving the static-SSR constraint, the revalidation provider, and the redirect manager, each of which fails in ways that are hard to diagnose. The template is not a shortcut past understanding; it is the reference implementation of the understanding.

**Interactive login with a JavaScript post to an endpoint.** Works, but reintroduces by hand what static SSR does natively, and puts credentials through an extra hop. Rejected.

**External identity provider.** Out of scope — the application is local-only with admin-provisioned accounts.

## Consequences

- Generated code lands in the repository and must be reviewed rather than assumed correct. Unused features are deleted, not left dormant.
- Account pages are static SSR while the rest of the application is interactive. This mixed render mode is deliberate and is documented in the architecture, because it looks like an inconsistency to a reader who does not know why.
- Two-factor authentication and passkeys become straightforward later, since the Identity stack already supports them. They are deliberately not enabled in v1.0.
- The template's Identity schema is added to the same `DbContext` and the same migration history as the business entities.
