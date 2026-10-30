# LokiLogViewer

A self-hosted log viewer for [Grafana Loki](https://grafana.com/oss/loki/), built as a
Blazor WebAssembly app with an ASP.NET Core backend, in one repo. Search is deliberately
simple: a small, server-configured set of filter fields (e.g. application, namespace,
container) that you fill in and search - no query language, no boolean logic to learn.

## Contents

- [Architecture](#architecture)
- [Project layout](#project-layout)
- [Prerequisites](#prerequisites)
- [Running it](#running-it)
- [Configuring the Loki connection](#configuring-the-loki-connection)
- [Configuring the search fields](#configuring-the-search-fields)
- [Using the page](#using-the-page)
- [API reference](#api-reference)
- [Testing](#testing)
- [Known limitations](#known-limitations)

## Architecture

```
Browser (Blazor WebAssembly)
   |  HTTP, same origin
   v
ASP.NET Core server  ---->  Loki HTTP API
 (hosts + serves the        (label values / query_range)
  compiled WASM client,
  also exposes /api/logs)
```

The server is the only thing that talks to Loki. The client never calls Loki directly -
that keeps Loki's URL and credentials out of the browser and avoids CORS entirely, since
the client and the API it calls are served from the same origin.

| Project | Kind | Responsibility |
|---|---|---|
| `LokiLogViewer` | ASP.NET Core host | Serves the compiled Blazor client, proxies Loki, hosts `/api/logs/*` |
| `LokiLogViewer.Client` | Blazor WebAssembly | The single log-search page, runs in the browser |
| `LokiLogViewer.Shared` | Class library | The LogQL builder and the request/response DTOs both sides speak |
| `LokiLogViewer.Shared.Tests` | xUnit | Unit tests for the LogQL builder |

## Project layout

```
LokiLogViewer.sln
src/
  LokiLogViewer/                  Server (ASP.NET Core host)
    Loki/                         LokiClient, LokiOptions, raw Loki API DTOs
    Search/                       LogSearchService (runs the Loki query, merges/pages results)
    Endpoints/                    Minimal API endpoints under /api/logs
    Components/                   Server-rendered layout shell (App, MainLayout) and the error page
    wwwroot/                      Server-side static assets (app.css, logs.css)
  LokiLogViewer.Client/            Blazor WebAssembly client
    Pages/Logs.razor              The log viewer page (routed at "/")
    Components/                   SearchFieldRow (one configured filter field), AdHocFilterRow
                                   (one user-added filter), FieldsSidebar (the labels/JSON-fields
                                   column picker)
    Results/LogLineJson.cs        Tolerant JSON parsing of a log line's body, for the column picker
    Services/                     LogApiClient (typed HttpClient wrapper) + its exception type
  LokiLogViewer.Shared/            Shared between Server and Client
    Querying/LogQlBuilder.cs      Conditions -> one LogQL selector string (escaping + validation)
    Search/                       LogCondition, SearchFieldDefinition, LogQueryRequest/Response,
                                   LogEntry, LabelsRequest, UnixTime
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
| `DefaultLimit` | No (default `200`) | Not currently read by the UI (which sends its own limit), reserved for a future default |

**Loki itself has no built-in authentication** - per Grafana's docs, it relies entirely
on whatever sits in front of it (a reverse proxy, gateway, or Grafana Cloud's own
fronting layer). Which of the options above applies depends on how *your* Loki is
fronted; that's infrastructure knowledge specific to your deployment, not something
this app can detect.

## Configuring the search fields

The filter fields shown on the page come from a `SearchFields` section in
`appsettings.json` - a flat array, each entry naming a Loki label and how to display it:

```json
"SearchFields": [
  {
    "label": "applicationAcronym",
    "displayName": "Application",
    "values": ["uagaks", "ucsaks", "bmapks"]
  },
  { "label": "namespace", "displayName": "Namespace" },
  { "label": "container", "displayName": "Container" },
  { "label": "enterprise_system_acronym", "displayName": "Enterprise System" }
]
```

Add, remove, or rename fields here and the page picks it up automatically (fetched once
on load from `GET /api/logs/search-fields`) - no client code change needed. The default
four match the label combination every manually-built query in this project's own HAR
captures used to find "their app's logs".

An optional `values` array restricts a field to a fixed, known set - it renders as a
plain dropdown instead of a free-text box with live autocomplete from Loki. This is a
UI convenience only: the server accepts any value for that label regardless (including
one typed into an additional filter, below), so updating the list here never risks
locking out a value that legitimately exists in Loki but hasn't been added to config.

These 4 are a starting point, not a ceiling - see **Additional filters** below for
adding more from the page itself without touching config.

## Using the page

1. **Filters.** One input per configured field. Most are a plain text box with an
   autocomplete dropdown of real values for that label (fetched from Loki, scoped to
   whatever other fields already have a value - e.g. picking a namespace narrows the
   containers suggested next); a field configured with a fixed `values` list (see
   above) is a plain `<select>` instead. Leave a field blank to not filter on it;
   whatever's filled in - from the configured fields, additional filters, or both - is
   combined with a plain AND. At least one filter needs a value to search; it doesn't
   matter which source it came from.
2. **Additional filters.** Click **+** to add a row where both the field name and the
   value are free text with autocomplete (the field-name dropdown lists every label
   Loki knows about, scoped the same way). Use this for anything not worth permanently
   adding to `SearchFields` - a one-off filter, or a label nobody thought to configure.
   If an additional filter names the same label as a configured field, the additional
   filter's value wins.
3. **Since** (time range) and **Limit**, then **Search**. All of this - the configured
   fields, additional filters, time range, limit, and the Search button - lives in one
   compact toolbar row, not separate boxed sections, to leave as much room as possible
   for the results below.
4. **Results**, as a Time + Message table by default. The left sidebar lists every
   label and every JSON key discovered in the loaded log lines (parsing each line's body
   as JSON when it is JSON) as checkboxes - check one to add it as a column. A level
   badge (from a `level` or `detected_level` label) shows inline before the message when
   present. **Load more logs** fetches the next page using the previous page's cursor.

## API reference

Four endpoints under `/api/logs`:

| Endpoint | Body | Returns |
|---|---|---|
| `GET /api/logs/search-fields` | - | `SearchFieldDefinition[]` - the configured filter fields |
| `POST /api/logs/labels` | `LabelsRequest` (`context`: other filters' current values, `from`, `to`) | `string[]` of label names - backs the additional-filter field-name autocomplete |
| `POST /api/logs/labels/{name}/values` | `LabelsRequest` | `string[]` of values for that label |
| `POST /api/logs/query` | `LogQueryRequest` (`conditions`: every filled-in field from either source, `from`, `to`, `limit`, optional `cursor`) | `LogQueryResponse` (`entries`, `nextCursor`) |

The labels/query endpoints are POST rather than GET because the body carries a small
selector context, not just an id. Errors: an empty or invalid filter returns `400` with
`{ "error": "..." }`; an unreachable Loki returns `502`; a Loki timeout returns `504` -
all via a shared endpoint filter, so nothing leaks a raw stack trace to the client.

[`LogQlBuilder`](src/LokiLogViewer.Shared/Querying/LogQlBuilder.cs) turns the filled-in
conditions into one LogQL selector (`{label="value", ...}`) - Loki's own comma-separated
matchers are already an AND list, so this is just escaping and label-name validation,
nothing more elaborate. `LogSearchService` runs that one query, and merges + sorts its
results into one page (a single selector can still match more than one Loki stream).

## Testing

```
dotnet test tests/LokiLogViewer.Shared.Tests
```

Covers `LogQlBuilder`: a single condition, multiple conditions joined as an AND list,
value escaping, invalid label names, and the empty-conditions guard.

## Known limitations

- **Pagination at exact-timestamp ties.** Paging works by moving the time boundary to
  the oldest line's timestamp (nanosecond precision) - if multiple lines share that
  exact nanosecond across a page boundary, one could be skipped or repeated. Rare in
  practice, not handled specially.
- **Value autocomplete uses a fixed 24-hour lookback**, independent of whichever time
  range the search itself uses, to keep the picker simple.
- **No live tailing** - this is a query-and-page viewer, not a streaming tail.
