# LokiLogViewer

A self-hosted log viewer for [Grafana Loki](https://grafana.com/oss/loki/), built as a
Blazor WebAssembly app with an ASP.NET Core backend, in one repo. It lets you build a
filter out of nested **AND / OR / NOT** label conditions - not just a flat list of
`label=value` pairs - and browse the matching log lines with pagination.

It exists because Loki's own query language (LogQL) has no native OR or NOT across
labels, but a usable query builder needs both; see [How filters become LogQL](#how-filters-become-logql)
for how that gap is bridged.

## Contents

- [Architecture](#architecture)
- [Project layout](#project-layout)
- [Prerequisites](#prerequisites)
- [Running it](#running-it)
- [Configuring the Loki connection](#configuring-the-loki-connection)
- [Using the query builder](#using-the-query-builder)
- [API reference](#api-reference)
- [How filters become LogQL](#how-filters-become-logql)
- [Testing](#testing)
- [Known limitations](#known-limitations)

## Architecture

```
Browser (Blazor WebAssembly)
   |  HTTP, same origin
   v
ASP.NET Core server  ---->  Loki HTTP API
 (hosts + serves the        (labels / label values / query_range)
  compiled WASM client,
  also exposes /api/logs)
```

The server is the only thing that talks to Loki. The client never calls Loki directly -
that keeps Loki's URL and credentials out of the browser and avoids CORS entirely, since
the client and the API it calls are served from the same origin.

| Project | Kind | Responsibility |
|---|---|---|
| `LokiLogViewer` | ASP.NET Core host | Serves the compiled Blazor client, proxies Loki, hosts `/api/logs/*` |
| `LokiLogViewer.Client` | Blazor WebAssembly | The single log-search page and query builder UI, runs in the browser |
| `LokiLogViewer.Shared` | Class library | The query tree model, the LogQL builder, and the request/response DTOs both sides speak |
| `LokiLogViewer.Shared.Tests` | xUnit | Unit tests for the LogQL builder |

## Project layout

```
LokiLogViewer.sln
src/
  LokiLogViewer/                  Server (ASP.NET Core host)
    Loki/                         LokiClient, LokiOptions, raw Loki API DTOs
    Search/                       LogSearchService (orchestrates Loki calls, merges results)
    Endpoints/                    Minimal API endpoints under /api/logs
    Components/                   Server-rendered layout shell (App, MainLayout) and the error page
    wwwroot/                      Server-side static assets (app.css, logs.css)
  LokiLogViewer.Client/            Blazor WebAssembly client
    Pages/Logs.razor              The log viewer page (routed at "/")
    Components/                   QueryGroupEditor, QueryConditionRow (the recursive builder UI)
    Query/QueryNodeEditor.cs      Mutable editor model for the query tree, used only by the UI
    Services/                     LogApiClient (typed HttpClient wrapper) + its exception type
  LokiLogViewer.Shared/            Shared between Server and Client
    Querying/                     QueryNode tree + LogQlBuilder (AND/OR/NOT -> LogQL)
    Search/                       LogQueryRequest/Response, LogEntry, LabelsRequest, UnixTime
    Serialization/                Shared JsonSerializerOptions setup (enums as strings)
tests/
  LokiLogViewer.Shared.Tests/      Unit tests for LogQlBuilder
```

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A reachable Loki instance (or a Loki-compatible endpoint) to point it at

## Running it

```
dotnet run --project src/LokiLogViewer
```

This builds and serves both the API and the WASM client from one process. The URL it
listens on comes from `src/LokiLogViewer/Properties/launchSettings.json`
(`https://localhost:7146` / `http://localhost:5122` by default) - open that URL; the
log search page is the only page, at `/`.

Until [Loki is configured](#configuring-the-loki-connection), the app starts fine but
every search shows a clean "Could not reach Loki" error - that's the missing
`Loki:BaseUrl` being caught by startup validation, not a bug.

## Configuring the Loki connection

All Loki connection settings live under a `Loki` section (bound to `LokiOptions` in
[`src/LokiLogViewer/Loki/LokiOptions.cs`](src/LokiLogViewer/Loki/LokiOptions.cs)).
`BaseUrl` is required - the app fails fast at startup with a clear error if it's
missing, rather than surfacing a confusing connection failure later.

Set these with [`dotnet user-secrets`](https://learn.microsoft.com/aspnet/core/security/app-secrets)
from `src/LokiLogViewer` so real values never land in `appsettings.json` or git:

```
cd src/LokiLogViewer
dotnet user-secrets set "Loki:BaseUrl" "https://your-loki-host"
```

| Key | Required | Purpose |
|---|---|---|
| `BaseUrl` | Yes | Root URL of the Loki HTTP API (no trailing path) |
| `OrgId` | If multi-tenant | Sent as `X-Scope-OrgID` - Loki requires this in multi-tenant mode (`auth_enabled: true`, the default) |
| `BearerToken` | If your Loki/gateway needs it | Sent as `Authorization: Bearer <token>`. Takes priority over Basic auth if both are set |
| `BasicAuthUsername` / `BasicAuthPassword` | If your Loki/gateway needs it | Sent as HTTP Basic auth. This is Grafana Cloud Loki's scheme: username is the stack's Loki instance ID, password is an access policy token |
| `MaxBranches` | No (default `25`) | Caps how many LogQL queries one filter tree can expand into - see [below](#how-filters-become-logql) |
| `DefaultLimit` | No (default `200`) | Not currently read by the UI (which sends its own limit), reserved for a future default |

**Loki itself has no built-in authentication** - per Grafana's docs, it relies entirely
on whatever sits in front of it (a reverse proxy, gateway, or Grafana Cloud's own
fronting layer). Which of the options above applies depends on how *your* Loki is
fronted; that's infrastructure knowledge specific to your deployment, not something
this app can detect.

## Using the query builder

The page has three parts:

1. **The filter.** Start with one group that reads "Match ALL of the following:".
   Click **+ Add condition** to add a `field is value` row - the operator dropdown
   covers `is` / `is not` / `matches pattern` / `doesn't match pattern`, so negation is
   just a choice of operator rather than a separate control. Field and value inputs are
   autocomplete text boxes backed by Loki's `/labels` and `/label/{name}/values`, scoped
   to whatever other conditions are already set in the same group. Switch **ALL** to
   **ANY** to match any one of the conditions instead of all of them, and check
   **Exclude these matches** to invert a whole group. **+ Add a nested group (advanced)**
   is there for combining ALL/ANY logic (e.g. `namespace is prod AND (container is a OR
   container is b)`) but is deliberately out of the way for the common case, which is
   just a flat list of conditions.
2. **Time range and result limit**, then **Search logs**.
3. **Results**, with labels shown as small badges (colored when a `level` or
   `detected_level` label is present) and a **Load more logs** button that fetches the
   next page using the previous page's cursor.

## API reference

Three endpoints under `/api/logs`, all POST (even the read-only ones - the request
body is a filter tree or a small selector context, not a simple id):

| Endpoint | Body | Returns |
|---|---|---|
| `POST /api/logs/labels` | `LabelsRequest` (`context`: flat AND conditions, `from`, `to`) | `string[]` of label names |
| `POST /api/logs/labels/{name}/values` | `LabelsRequest` | `string[]` of values for that label |
| `POST /api/logs/query` | `LogQueryRequest` (`filter`: the AND/OR/NOT tree, `from`, `to`, `limit`, optional `cursor`) | `LogQueryResponse` (`entries`, `nextCursor`) |

Errors: a malformed or unsatisfiable filter tree returns `400` with `{ "error": "..." }`;
an unreachable Loki returns `502`; a Loki timeout returns `504` - all via a shared
endpoint filter, so nothing leaks a raw stack trace to the client.

## How filters become LogQL

Loki stream selectors (`{label="value", ...}`) only support **AND** between matchers -
there's no way to express OR or NOT across labels in a single selector. To still offer
a real AND/OR/NOT builder, [`LogQlBuilder`](src/LokiLogViewer.Shared/Querying/LogQlBuilder.cs)
does this in two steps:

1. **Push NOT down to the leaves** (De Morgan's laws) - `NOT(A AND B)` becomes
   `NOT(A) OR NOT(B)`, `NOT(A OR B)` becomes `NOT(A) AND NOT(B)`, and `NOT` on a single
   condition just flips its operator (`=` &harr; `!=`, `=~` &harr; `!~`).
2. **Convert to disjunctive normal form** (an OR of AND-only clauses). Each clause maps
   directly to one LogQL selector, since Loki's own comma-separated matchers are
   already an AND.

`LogSearchService` then runs one Loki query per clause **in parallel**, merges the
results, de-duplicates lines that matched more than one clause, sorts by timestamp, and
returns one page.

A deeply nested tree can expand combinatorially (each OR multiplies the branch count),
so `MaxBranches` (default 25) caps this - past the limit, the query is rejected with a
"simplify your query" error instead of firing an unbounded number of Loki queries.

## Testing

```
dotnet test tests/LokiLogViewer.Shared.Tests
```

Covers `LogQlBuilder`: AND/OR/NOT combinations, De Morgan push-down, DNF distribution
over nested groups, duplicate-branch de-duplication, value escaping, and the
too-complex-query guard.

## Known limitations

- **Pagination at exact-timestamp ties.** Paging works by moving the time boundary to
  the oldest line's timestamp (nanosecond precision) - if multiple lines share that
  exact nanosecond across a page boundary, one could be skipped or repeated. Rare in
  practice, not handled specially.
- **Label/value pickers use a fixed 24-hour lookback**, independent of whichever time
  range the search itself uses, to avoid threading the search range through every
  condition row.
- **No live tailing** - this is a query-and-page viewer, not a streaming tail.
