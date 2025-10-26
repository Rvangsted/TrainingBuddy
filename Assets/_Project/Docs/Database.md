# TrainingBuddy

## Firebase Realtime Database Design for Races

The following structure supports race hosting with controlled participation limits and self-service join/leave flows.

```json
{
  "races": {
    "<raceId>": {
      "hostId": "<uid>",
      "title": "Morning Tempo Run",
      "description": "5k training race",
      "capacity": 20,
      "status": "open", // open | in_progress | completed | cancelled
      "createdAt": 1689194820000,
      "participants": {
        "<uid>": {
          "joinedAt": 1689195000000,
          "displayName": "Alex",
          "isHost": false
        }
      }
    }
  },
  "userRaces": {
    "<uid>": {
      "<raceId>": {
        "role": "host", // host | participant
        "joinedAt": 1689194820000
      }
    }
  },
  "joinRequests": {
    "<raceId>": {
      "<uid>": {
        "requestedAt": 1689194800000,
        "status": "pending" // pending | approved | rejected
      }
    }
  }
}
```

### Structure overview

- `races/<raceId>` holds metadata for each race, including capacity, scheduling, and the authoritative list of participants maintained by the host.
- `races/<raceId>/participants` records the active roster. Each child node is keyed by a user ID with timestamps for auditing. The host is stored in the list with `isHost: true` for convenience.
- `userRaces/<uid>` denormalizes the relationship so dashboards can quickly list the races a user hosts or has joined.
- `joinRequests/<raceId>` allows self-service joins. Clients write their request and listen for approval; Cloud Functions or host-driven admin tools move approved users into the `participants` list and decline when capacity is full.

### Race lifecycle

1. **Create** – the host writes a new `races/<raceId>` document and mirrors an entry at `userRaces/<hostId>/<raceId>` with role `host`.
2. **Join** – when a race is `open` and below capacity, a user posts to `joinRequests/<raceId>/<uid>`. Server-side logic (Cloud Functions, admin client) validates capacity and moves approved users into `races/<raceId>/participants` and `userRaces/<uid>/<raceId>`.
3. **Leave** – the host (or automation running with elevated privileges) removes the user from `races/<raceId>/participants` and deletes the `userRaces` entry. Optionally, the user can set a `leave` flag inside their join request to trigger the removal.
4. **Close** – the host updates `status` to `in_progress` or `completed` to prevent new join requests from being approved.

### Participation restrictions

- A user cannot host a second race or join another race while they are already hosting an active (non-cancelled, non-completed) race.
- A user cannot submit or be approved for a new race if they are already participating in another active race.
- Participants may leave races that are still `open` or `cancelled`, but must remain in races that are `in_progress` or `completed`.
- Only the race host (or a level 2 admin) can cancel a race, which moves its status to `cancelled` and clears outstanding join requests.