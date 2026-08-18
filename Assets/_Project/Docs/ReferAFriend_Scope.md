# Refer a Friend — Scoping

Roadmap item #5. Depends on `StepsAsCurrency_Scope.md` (`StepCurrency`,
`walletTransactions/{uid}`) landing first — this is what pays the reward.
Scoping only, not implemented yet.

## Current state

- **`FriendCode` already exists and can be reused as-is.** It's a
  deterministic 7-char code derived from the user's UID
  (`FirebaseController.GenerateFriendCode`), written to `friendCodes/{code}
  → uid` at account creation (`DatabaseManager.CreateUser`), and already
  displayed to the user on `ProfileScreen`. Today it's used purely for the
  social friend-request flow (`ProfileScreen.OnSearchFriend` →
  `GetUserByFriendCodeAsync` → `SendFriendRequestAsync` →
  `friendRequests/{toUid}/{fromUid}` → accept writes `friends/{uid}/{friendUid}`
  both ways). **No new code system is needed for referrals** — the existing
  code is the referral code.
- **No referral hook exists at signup today.** `FirebaseController.FirebaseRegister`
  creates the Auth account, builds `UserData`, and calls
  `DatabaseManager.CreateUser` — there's no code-entry field in
  `RegisterScreen.cs` and no `ReferredBy` (or similar) field on `UserData`.
  This is new UI + a new field, not a rewiring of something existing.
- **No anti-multi-account safeguard exists beyond mandatory email
  verification** (`SendEmailVerificationAsync`/`EnsureEmailVerifiedAsync` in
  `FirebaseRegister`). No device-fingerprint check, no rate limiting. A
  referral reward is realistically farmable with disposable email accounts
  — same category of risk already accepted for `StepCurrency` itself, just
  a different attack shape (fake accounts instead of a modified client).

## The write-permission problem this design has to solve

Per `StepsAsCurrency_Scope.md`'s decision, there's no Cloud Functions layer
— everything is written client-side, by each user, to their own node.
**Firebase rules only ever let a user write to `users/{auth.uid}`** (see
`RealtimeDatabase.rules.json`: `.write: auth.uid == $uid || ...admin`). That
means **the new signup's client cannot credit the referrer's `StepCurrency`
directly** — there's no rule that would allow it, and adding one (letting
any authenticated user write into an arbitrary other user's node) would be
a much bigger hole than anything accepted so far. Rewarding "the referrer"
from the referred account's own client is not actually possible under the
current model as a direct write.

**Resolution: reuse the exact pattern `friendRequests` already uses** — a
value written by user A into a namespace keyed by user B, which B's own
client later reads and acts on:

```
referralRewards/{referrerUid}/{newUid}: { amount, createdAt, status }
```

with rules shaped exactly like `friendRequests/{toUid}/{fromUid}`: the
*new* user (`$newUid == auth.uid`) can write the pending entry into the
referrer's namespace, and the *referrer* (`$referrerUid == auth.uid`) can
read it and update its `status`. Neither side ever writes into the other's
`users/{uid}` subtree — only into this shared, purpose-built node, same
boundary the friend-request system already respects.

## Design

- **Signup**: `RegisterScreen` gains an optional "referral code" field.
  Before `CreateUser`, validate it via the existing
  `GetUserByFriendCodeAsync` (same lookup the friend-add flow already uses).
  If valid, set `users/{uid}/ReferredBy = referrerUid` once, at creation —
  immutable afterward, and only settable at signup (can't retroactively
  attach a referral to an existing account).
- **Milestone gate, not instant reward.** Reward fires on the new account's
  **first successful non-zero step sync** (inside the existing
  `SyncFromProviderAsync`/`WriteStepsToFirebaseAsync` path), not at signup.
  This piggybacks on a flow that already runs automatically, so no extra
  friction for a genuine user, while requiring: email verified, app
  installed, step permission granted, and at least some real device usage
  — meaningfully harder to script than "create account, done." Guard with
  `users/{uid}/ReferralRewardGranted` (bool) so it can only fire once.
- **New user's reward (self-write, straightforward):** credit its own
  `StepCurrency`, write an `earn` entry to its own `walletTransactions/{uid}`
  tagged with the referral, set `ReferralRewardGranted = true`. Also write
  the pending claim: `referralRewards/{referrerUid}/{newUid} = {amount,
  createdAt, status: "pending"}`.
- **Referrer's reward (pull, on their own client, own write):** on
  app-open or next sync, the referrer's client reads
  `referralRewards/{myUid}/*` for any `status: "pending"` entries. For each:
  credit its own `StepCurrency`, write its own `earn` walletTransactions
  entry, then flip that entry's `status` to `"claimed"`. This means the
  referrer isn't credited the instant the friend hits the milestone — only
  the next time the referrer's own app is open — which is an acceptable
  trade-off for staying inside the existing write-permission model.
- **Both sides get rewarded** (referrer for referring, new user for
  redeeming a code) — gives the new signup an actual reason to enter one.

## Resolved

- **Reward amounts:** referrer gets 1000 `StepCurrency` (one free Basic-tier
  race entry — rewards the harder-to-farm side generously); new user gets
  500 as a welcome bonus (kept lower to cap the payoff from disposable-email
  farming, since account creation is the directly farmable side).
- **Friend auto-add: yes.** A valid referral code at signup writes both
  sides of the `friends/{uid}/{friendUid}` link, but not in one shot —
  Firebase rules only ever allow a self-write to `friends/{auth.uid}/...`,
  so each side is written by its own client: the new user writes their own
  `friends/{newUid}/{referrerUid}` atomically alongside `CreateUser`; the
  referrer writes the reverse `friends/{referrerUid}/{newUid}` entry when
  their own client claims the pending reward (`ClaimReferralRewardsAsync`).
  Same asymmetric-timing trade-off already accepted for the StepCurrency
  reward itself — the referrer's friends list won't show the new user until
  the referrer's app is next opened.
- **Implemented** — see `DatabaseManager.GrantReferralMilestoneRewardAsync`
  (new user's milestone reward + `referralRewards` pending claim) and
  `ClaimReferralRewardsAsync` (referrer's pull-based claim + friend link).
  `RegisterScreen` gained a plain (non-localized) optional "referral code"
  field, matching the existing DOB fields' convention of skipping the
  Localization system for secondary/optional inputs.
- **Farming is reduced, not eliminated** — someone determined can still run
  multiple disposable-email accounts through the milestone gate manually.
  Same "accepted risk, revisit only if actually observed" framing already
  used for `StepCurrency`'s client-write trust model; not worth
  over-engineering for a hobby-scale app before there's any evidence of
  abuse.
