# Step Tracking Overhaul — Plain-Language Summary

This explains, without any code talk, what was wrong with step counting in
the app, what's been done about it, and where things stand.

## The problem we were fixing

The app used to read steps directly off the phone's own step-counting
sensor, but only while the app was actually open. The issue: a phone's raw
step sensor resets whenever the phone restarts, and the app had no way to
see what happened while it wasn't running. So if someone didn't open the
app for a while and their phone restarted in between (which happens
routinely — software updates, low battery, etc.), every step taken during
that gap was silently lost forever. Over several months of normal,
infrequent app use, this added up to a real, noticeable shortfall — tens
of thousands of missing steps for some users, with no error message and no
way to know it was even happening.

## The fix

Both Apple and Google build a permanent, background step-tracking service
right into the phone itself — Android's is called **Health Connect**,
Apple's is called **Health**. These keep a running history of every step
taken, whether or not any particular app is open. The fix was to stop
reading the phone's raw sensor and instead simply ask the phone's own
health service, "how many steps have happened since I last checked?" —
which can never lose data to a restart, because the phone itself has
already been keeping score the whole time.

Android's version of this is fully built and has been tested on a real
phone. iOS's version is fully written but hasn't been tested yet — that
needs an Apple computer, which is a separate piece of setup currently in
progress.

## Extra problems found and fixed along the way

Real-device testing surfaced several additional issues beyond the core fix,
all now resolved on Android:

- **The app wasn't showing up as a health app at all.** Android requires
  apps to register themselves in a specific way to even appear in the
  "which apps can access your health data" list — this was missing, so
  Health Connect had no way to recognize the app existed, no matter how
  correctly it asked for permission.
- **The "fix it in Settings" button sent people to the wrong screen.**
  When something went wrong with permissions, the app's fallback button
  opened the phone's generic app-info page instead of the actual Health
  Connect permission screen — which has nothing to do with health
  permissions at all, so it could never actually fix anything.
- **A step-cheating loophole.** Because the app now reads a phone's real
  step history instead of a live sensor, a new risk appeared: someone
  could log into their account on a different phone that already had
  months of someone else's walking history, and instantly inherit all of
  it. This is now blocked — a phone only ever contributes *new* steps from
  the point an account first uses it onward, with a 30-day safety cap as
  a backstop against slower versions of the same trick.
- **The daily step graph had two separate bugs.** First, it was deciding
  when "today" starts using the wrong time zone, which could cause an
  early-morning walk to get filed under the wrong day and silently
  vanish from the day's total. Second, once that was fixed, the graph's
  "today" number stopped updating live — it would freeze at whatever it
  showed when the screen first opened, even as the person kept walking.
  Both are now fixed.
- **A hidden developer shortcut was removed.** A button labeled "Privacy"
  was secretly left over from testing — tapping it silently logged the
  device into an admin account using a password that was written directly
  in the app's source code. That shortcut has been removed. The exposed
  password itself still needs to be changed on the account directly,
  since simply deleting it from the code doesn't undo the fact that it
  was visible in the project's history.

## Where things stand

- **Android:** working and tested end-to-end on a real device.
- **iOS:** all the code is written, but genuinely untested — it needs to
  be compiled and run on an Apple computer, which hasn't happened yet.
- **Before this can go live to real users**, three things are still
  needed: broader testing across different phone models, getting iOS
  actually running and verified the same way Android has been, and
  writing the privacy-policy text that Apple and Google both require
  before approving an app that reads health data.
