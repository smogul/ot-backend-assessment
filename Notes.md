## Setup

### Option A: your own SQL Server and RabbitMQ (the README's setup)
1. Run `DatabaseGenerate.sql` on your SQL Server.
2. Make sure RabbitMQ is running on `localhost` with the login `guest/guest`.
3. Start both programs, each in its own terminal:
   ```
   dotnet run --project src/OT.Assessment.App
   dotnet run --project src/OT.Assessment.Consumer
   ```

The connection string adds `TrustServerCertificate=True` to the README's version, because `Microsoft.Data.SqlClient` encrypts connections by default and a local SQL Server uses a self-signed certificate.

### Option B: Docker for both
1. Start the containers: `docker compose up -d`
2. Create the database:
   ```
   docker exec ot-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P Local_Dev_Only_123 -C -i /scripts/DatabaseGenerate.sql
   ```
3. Start both programs with the Docker settings, each in its own terminal:
   ```
   dotnet run --project src/OT.Assessment.App --launch-profile Docker
   dotnet run --project src/OT.Assessment.Consumer --launch-profile Docker
   ```

The API listens on `http://localhost:5000`. Opening that address redirects to Swagger UI at `http://localhost:5000/swagger`, where all three endpoints can be tried with the Execute button. The POST endpoint already holds a valid example wager, so it works without typing anything.

On a Mac, turn off AirPlay Receiver first (System Settings > General > AirDrop & Handoff), because it uses port 5000.

### Running the tester
```
dotnet run --project test/OT.Assessment.Tester/OT.Assessment.Tester.csproj
```

### Checking the results
Run `scripts/Reconcile.sql` after the queue reaches zero. With Docker:
```
docker exec -i ot-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P Local_Dev_Only_123 -C -W < scripts/Reconcile.sql
```

## Message bus setup
RabbitMQ on `localhost:5672` with `guest/guest`. The API and the consumer both create the queues when they start, so they can be started in any order. The dashboard is at `http://localhost:15672`.

I chose RabbitMQ because this job's main rule is that an accepted wager must never be lost, and RabbitMQ gives that directly: durable queues, persistent messages, publisher confirms, per-message acknowledgements and dead lettering. Kafka's main strengths, replaying history and very high throughput across partitions, are not needed here. Redis would need more care to get the same durability.

## Architecture
The dependencies point inward, so the business rules know nothing about RabbitMQ, SQL Server or the web. The infrastructure could be replaced without changing the inner layers.

- `OT.Assessment.Domain`: the wager itself. Depends on nothing.
- `OT.Assessment.Application`: the jobs the system does, and the interfaces it needs.
- `OT.Assessment.Infrastructure`: the RabbitMQ and SQL Server code.
- `OT.Assessment.App`: the API.
- `OT.Assessment.Consumer`: the background worker.

The flow is: tester > API > RabbitMQ > consumer > SQL Server. The GET endpoints read SQL Server through stored procedures.

## Design decisions

**The API returns 202 only after RabbitMQ confirms the message.** Publishing without waiting would be slightly faster, but a crash could lose a wager the sender was told was accepted. Evidence: p99 latency stayed in single-digit milliseconds, and with RabbitMQ down the API returns 500 and not a false success.

**The API keeps a pool of 20 publishing channels.** One shared channel would make concurrent requests queue behind each other, and a new channel per request is slow. Each request borrows a channel and returns it.

**The consumer batches without a timer.** It waits for the first message, then takes whatever is already waiting, up to 500. Under light load batches are small and wagers are saved quickly. Under heavy load batches grow by themselves. The last few messages cannot get stuck waiting for a full batch.

**Saving goes through a table-valued parameter and one stored procedure.** One insert per wager makes a database trip per wager. `SqlBulkCopy` is fast, but it cannot look up player and game numbers or skip duplicates in one step.

**The consumer acknowledges only after the database commit.** A crash between the two means the messages are delivered again, never lost. That makes delivery "at least once", so saving has to cope with repeats.

**Saving skips wagers that are already stored.** `WagerId` is unique, and the stored procedure inserts only new ones, including when the same wager appears twice in one batch. Evidence: posting the same wager twice stores one row, and killing the consumer mid-run leaves no duplicates.

**A failed save waits 5 seconds and tries again, without limit.** Simple, and it loses nothing during a database outage. The downside is that a wager the database permanently rejects would block the queue, so the API's validation matches the column limits. See production improvements.

**Unreadable messages go to a dead letter queue.** One bad message should not stop the good ones, and it is kept for inspection instead of being thrown away.

**`CasinoWager` uses a bigint clustered key, not the GUID `WagerId`.** Random GUIDs would insert into the middle of the table and fragment it. New bigint rows go at the end. `WagerId` keeps a separate unique constraint.

**Amounts use `DECIMAL(28,15)`.** The tester sends up to 15 decimal places, and a floating point type would change them. Evidence: the stored total matches the tester's total to the last digit. Anything past 15 decimal places is rounded by SQL Server.

**Dates use `DATETIME2(7)` in UTC.** This keeps the sender's full precision, and sorting does not depend on time zones.

**Read committed snapshot isolation is on.** Readers see the last committed version instead of waiting for writers, so both GET endpoints stay quick during ingestion.

**One covering index serves both GET endpoints.** Key: `PlayerKey, CreatedDateUtc DESC, CasinoWagerKey DESC`, including `WagerId, GameKey, Amount`. Every extra index slows inserts, and nothing filters by game, so there is no index on `GameKey`.

**Players, providers and games have their own tables.** This avoids repeating names on every wager. A game belongs to a provider because the same game name appears under different providers in the tester's data.

**Some fields are not stored.** `sessionData` is about a quarter of the payload and no endpoint uses it. The other identifiers are kept for tracing a wager back to its source.

**`pageSize` and `count` are capped at 100.** This stops one request asking for everything. An unknown player gets an empty list, because a player with no wagers is a valid answer and not an error.

**There is no login, HTTPS redirect or rate limit.** The tester counts anything other than a 2xx response as a failure. These are listed under production improvements.

## Test results

|smogul@DATSs-Laptop ot-backend-assessment % dotnet run --project test/OT.Assessment.Tester/OT.Assessment.Tester.csproj
Sending casino wagers to http://localhost:5000/api/player/casinowager
  _   _   ____                        _                        __   
 | \ | | | __ )    ___    _ __ ___   | |__     ___   _ __     / /_  
 |  \| | |  _ \   / _ \  | '_ ` _ \  | '_ \   / _ \ | '__|   | '_ \ 
 | |\  | | |_) | | (_) | | | | | | | | |_) | |  __/ | |      | (_) |
 |_| \_| |____/   \___/  |_| |_| |_| |_.__/   \___| |_|       \___/ 
                                                                    
Welcome to the NBomber community!
Documentation: https://nbomber.com/docs/getting-started/overview
Blog: https://nbomber.com/blog
Roadmap: https://nbomber.com/docs/getting-started/roadmap
GitHub Discussion: https://github.com/PragmaticFlow/NBomber/discussions
GitHub Issues: https://github.com/PragmaticFlow/NBomber/issues
Docker Hub: https://hub.docker.com/u/nbomberdocker
LinkedIn: https://www.linkedin.com/company/nbomber
Slack: https://nbomberworkspace.slack.com

14:27:03 [WRN] Server GC mode is not enabled. For better performance and more accurate load test results, enable it 
by adding <ServerGarbageCollection>true</ServerGarbageCollection> to your .csproj file, or #:property ServerGarbageCollection=true to the top of your .cs file script. Docs: https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector#workstation-vs-server
14:27:03 [INF] NBomber "6.7.1" started a new session: "2026-11-06_12-27-03_d84ce41c"
14:27:03 [INF] NBomber started as single node
14:27:03 [INF] License validation....
14:27:03 [WRN] THIS VERSION IS FREE ONLY FOR PERSONAL USE. You can't use it for an organization.
14:27:03 [INF] Reports folder: "/Users/smogul/Downloads/ot-backend-assessment/test/OT.Assessment.Tester/bin/Debug/net10.0/reports/20
26-11-06_12-27-03_d84ce41c"
14:27:03 [INF] Plugin loaded: "HttpMetricsPlugin"
14:27:03 [INF] Reporting sinks: no reporting sinks were loaded
14:27:03 [INF] Starting init...
14:27:03 [INF] Start init plugin: "HttpMetricsPlugin"
14:27:03 [INF] Target scenarios: "casino_wager_submission"
14:27:03 [INF] Init finished
14:27:03 [INF] Starting bombing...
14:27:31 [INF] Waiting on scenarios completion...
14:27:34 [INF] Stopping scenarios...
14:27:34 [INF] Calculating final statistics...

───────────────────────────────────────────── test info ─────────────────────────────────────────────

test suite: nbomber_default_test_suite_name
test name: nbomber_default_test_name
session id: 2026-11-06_12-27-03_d84ce41c

────────────────────────────────────────── scenario stats ───────────────────────────────────────────

scenario: casino_wager_submission
  - duration: 00:00:28

load simulations: 
  - iterations_for_inject, rate: 500, interval: 00:00:02, iterations: 7000

┌─────────────────────────┬─────────────────────────────────────────────────────────────────────────┐
│      scenario and steps │ ok stats                                                                │
├─────────────────────────┼─────────────────────────────────────────────────────────────────────────┤
│           scenario name │ casino_wager_submission                                                 │
│                requests │ total = 7000, ok = 7000, fail = 0                                       │
│           RPS (req/sec) │ total = 250/s, ok = 250/s, fail = 0/s                                   │
│            latency (ms) │ min = 0.68, mean = 1.9, max = 67.25, StdDev = 3.21                      │
│ latency percentile (ms) │ p50 = 1.28, p75 = 1.6, p95 = 3.31, p99 = 20.48                          │
│      data transfer (KB) │ min = 0.978, mean = 1.060, max = 1.169, throughput = 265.085 KB/s,      │
│                         │ total = 7.248 MB                                                        │
└─────────────────────────┴─────────────────────────────────────────────────────────────────────────┘

status codes for scenario: casino_wager_submission
┌─────────────┬───────┬─────────┐
│ status code │ count │ message │
├─────────────┼───────┼─────────┤
│    Accepted │ 7000  │         │
└─────────────┴───────┴─────────┘



## Challenges
- The tester sends `Username` and `Duration` with capital letters, while the README shows `duration` in lowercase. The API's JSON settings ignore case, and a unit test protects this.
- Testing found that an amount too big for the database column got a 202, and the consumer then retried it forever while good wagers waited behind it. The API now rejects those amounts with a 400.
- Testing found that a RabbitMQ restart could crash the consumer, because signing for messages failed on the closed connection. The consumer now logs it and carries on. The messages are delivered again and the duplicates are skipped.
- On an Apple Silicon Mac, port 5000 is used by AirPlay Receiver, and SQL Server only runs through emulation.

## Production improvements
- **Bad batches:** split a batch that keeps failing, so one wager the database rejects goes to the dead letter queue and does not block the rest.
- **Security:** add login, HTTPS and rate limiting, move secrets to a vault, and stop using the `guest` account.
- **Monitoring:** add health checks, metrics and alerts for queue depth, dead letters and failed saves.
- **Publishing during an outage:** retry briefly in the API, so a short RabbitMQ restart causes fewer failed requests.
- **More consumers:** lock the player, provider and game lookups in the stored procedure, or concurrent first inserts will hit the unique constraints and have to retry.
- **Top spenders:** keep a running total per player, because adding up every wager gets slower as the table grows.
- **Data growth:** partition or archive old wagers.
- **High availability:** use quorum queues and a SQL Server availability group.
- **Testing:** add automated integration tests against real RabbitMQ and SQL Server containers.
- **Username changes:** the first username seen for a player is kept, so a real system would need a rule for updates.

## AI assistance
Claude Code assisted to generate the project scaffolding, the RabbitMQ and SQL plumbing, the code comments helped detail these notes. I wrote `DatabaseGenerate.sql`, the `.Net 10 Clean Architecture`logic. I also reviewed the Claude code and its unit tests, ran the load test and failure tests myself (the results above are from my runs), and I am able to explain each decision.