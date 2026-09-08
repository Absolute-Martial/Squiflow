# Identity and Session Architecture

**Version:** v0.0.15

Use a canonical SquiFlow browser identity authority for Web, custom-domain applications, Admin Web and native Workstation interactive login.

## Native Workstation

Use the system browser and an authorization-code style flow with PKCE/state verification.

The Workstation does not collect the user's password as its primary authentication path and does not receive database credentials.

## Web/custom domains

A custom-domain application redirects to the canonical identity authority, authenticates there, then returns through a pre-registered validated callback and establishes its own safe application session.

Do not share one broad login cookie across arbitrary customer-owned domains.

## Sessions

Authentication, tenant membership, device posture, capability permission and resource scope are separate proofs. A valid session alone does not authorize every operation.

Session/permission changes are rechecked on commands that have material effects.
