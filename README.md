# Macro Deck plugin template

A starting point for an out-of-process Macro Deck 3 plugin: one integration that starts, registers with
a host and does nothing else. No sample capabilities to delete, no example code to read around - add
what your plugin actually needs.

Looking for worked examples of each capability instead? The
[sample plugins repository](https://github.com/Macro-Deck-App/Macro-Deck-Sample-Plugins) has one
coherent plugin per area: actions and variables, a music player, a REST API with a multi-step config
flow, a virtual profile.

## Getting started

Either install the template and generate a project:

```bash
dotnet new install MacroDeck.Plugin.Templates::3.0.0-preview.2
```

```bash
dotnet new macrodeck-plugin -n Acme.LightControl --pluginId com.acme.light-control --pluginName "Acme Light Control"
```

| Parameter | Default | What it sets |
| --- | --- | --- |
| `-n`, `--name` | `MacroDeckPlugin` | The project, namespace, solution and the executable names in `manifest.json` |
| `--pluginId` | `com.example.my-plugin` | The manifest `id`: reverse-domain, lowercase, at least two dot-joined kebab segments |
| `--pluginName` | `My Plugin` | The display name Macro Deck shows |

Or clone this repository and rename by hand - the two are the same content. If you clone, change the
`id`, `name`, `version` and `description` in `src/MacroDeck.PluginTemplate/manifest.json`, then rename
the projects, the solution file and the namespace.

Either way, replace `Assets/icon.svg`. It is your plugin's icon: the manifest's `icon` path is the
single source of truth and the host reads that file directly, so there is no code to change.

## Requirements

- .NET SDK 10.0
- For the [full dev loop](#running-against-a-real-host): a running Macro Deck 3 host - either a
  development host from the [Macro Deck 3 repository](https://github.com/Macro-Deck-App/Macro-Deck-3),
  or an installed desktop app

You do **not** need Macro Deck installed to build, run or test a plugin - the
[CLI](#the-developer-cli) runs one against a disposable stub host.

## Quick start

```bash
dotnet build
```

```bash
dotnet test
```

```bash
dotnet tool install --global MacroDeck.Plugin.Cli --prerelease
```

```bash
macrodeck-plugin run --project src/MacroDeck.PluginTemplate
```

That launches the plugin against a disposable stub host, composing the environment exactly the way the
real supervisor does. Ctrl-C runs the documented shutdown sequence.

## Building against a local SDK build

> **Until `3.0.0-preview.2` is on nuget.org, this is not optional.** The template uses SDK surface that
> landed after `3.0.0-preview.1`, so a plain `dotnet build` fails until that release is published. Build
> against a locally packed SDK as described here until then.

The template tracks the SDK's *published* packages. While a change is still unreleased, pack the SDK
from a Macro Deck 3 checkout into this repository's `local-feed/` and build against that version:

```bash
dotnet pack MacroDeck.slnx -c Release -p:Version=3.0.0-local.1 -o <path-to-this-repo>/local-feed
```

```bash
dotnet build -p:MacroDeckSdkVersion=3.0.0-local.1
```

`NuGet.config` already lists `local-feed/` as a package source, and `MacroDeckSdkVersion` sets the
version for every Macro Deck package at once (see `Directory.Packages.props`). Nothing in the
repository pins the local version, so a plain `dotnet build` goes back to the published one.

Pick a version that cannot collide with a real release - `3.0.0-local.N` rather than reusing
`3.0.0-preview.2`, which would put a hand-built package into the global NuGet cache under the name of
a published one.

## How a plugin is put together

### Project layout

```
src/MacroDeck.PluginTemplate/
  Program.cs             the host builder - three lines and a RunAsync
  manifest.json          identity, icon and per-platform entrypoints
  PluginIntegration.cs   the integration: lifecycle and capability opt-ins
  Assets/icon.svg        the icon the manifest declares
tests/MacroDeck.PluginTemplate.Tests/
  PluginIntegrationTests.cs   the plugin builds and initializes
```

### The entry point

`MacroDeckPlugin.CreatePlugin(args)` wraps `WebApplication.CreateBuilder`, so everything an ASP.NET
Core application has is available - configuration, options binding, `IHttpClientFactory`, hosted
services, dependency injection:

```csharp
var plugin = MacroDeckPlugin.CreatePlugin(args)
    .UseMacroDeckLogging()
    .RegisterIntegration<PluginIntegration>()
    .Build();

await plugin.RunAsync();
```

`RegisterIntegration<T>()` is the one door: it registers the integration's actions plus a capability
handler for every SDK interface the type implements. The integration is built by DI, so it can take
`IHttpClientFactory`, `IOptions<T>`, Serilog's `ILogger`, `PluginMetadata` or `IPluginCatalogNotifier`
in its constructor. `UseMacroDeckLogging()` routes your log output to the host's log viewer.

Anything the container needs beyond that goes on `builder.Services` before `Build()`.

### The manifest

`manifest.json` is the plugin's identity, read from the content root at startup. `Build()` validates it
and fails fast on an invalid id, a missing name or version, or an unreadable icon.

```json
{
  "manifestVersion": 1,
  "id": "app.macro-deck.template",
  "name": "Macro Deck Plugin Template",
  "version": "1.0.0",
  "description": "A minimal Macro Deck 3 plugin.",
  "icon": "Assets/icon.svg",
  "entrypoints": {
    "win-x64": { "executable": "MacroDeck.PluginTemplate.exe" },
    "osx-arm64": { "executable": "MacroDeck.PluginTemplate" },
    "osx-x64": { "executable": "MacroDeck.PluginTemplate" },
    "linux-x64": { "executable": "MacroDeck.PluginTemplate" }
  }
}
```

Only `manifestVersion`, `id`, `name`, `version` and `entrypoints` are required. A manifest may also
declare `permissions`, `dependencies`, `conflicts`, `iconPacks`, `compatibility` and `files[]` -
`macrodeck-plugin inspect` reports all of them, and `pack` recomputes `files[]` for you.

`win-arm64` falls back to `win-x64` and `osx-arm64` falls back to `osx-x64`; there is no `"any"` key,
and `linux-musl-*` resolves no fallback at all.

### Capabilities

`PluginIntegration` implements `IPluginIntegration` - lifecycle and actions - and **opts into**
everything else by implementing that capability's interface. The host discovers each one by filtering
on the interface, so you only implement what you need. Identity and the icon are not on this list:
they come from the manifest.

| Capability | Interface |
| --- | --- |
| Actions | `IActionDefinition`, in `Actions` |
| Config flow | `IConfigFlowProvider` |
| Variables | `IVariableProvider` |
| Events | `IEventProvider` |
| Issues | `IIntegrationIssueProvider` |
| Music players | `IMusicPlayerProvider` |
| Weather | `IWeatherProvider` |
| Virtual profiles | `IProfileProvider` |

An integration that provides a config flow starts **disabled** until the user configures it; everything
else defaults to enabled.

Capability ids are namespaced by the host as `integrationId::localId`, so you declare provider-local
ids and never the qualified form.

The [sample plugins](https://github.com/Macro-Deck-App/Macro-Deck-Sample-Plugins) are the worked
examples for each of these.

## Running against a real host

The plugin registers itself with a running host rather than being launched by it, so the host has to
be up first. Self-registration only works against a host on the **same machine**: the plugin endpoints
are local-only by design.

### 1. Start the host and the UI

From your Macro Deck 3 checkout:

```bash
dotnet run --project host/src/MacroDeckHost
```

A development host listens on `7193` (public) and `5191` (trusted loopback). An installed desktop app
uses `8193` instead - use that port below if you are developing against one, and skip to step 3, since
it brings its own UI.

Then start the configuration UI, from `ui/angular`:

```bash
npm install && npm run start
```

It serves on `http://localhost:4200` and proxies to the host's loopback port. Connections over
loopback are implicitly admin, so no login is needed in development.

### 2. Create a Developer token

In the UI, go to **Developer Tools → Plugin tokens** and press **Create token**. Copy the plaintext
value - it is shown once.

### 3. Start the plugin

```bash
dotnet build
cd src/MacroDeck.PluginTemplate/bin/Debug/net10.0

MACRO_DECK_PLUGIN_MODE=SelfRegistering \
MACRO_DECK_PLUGIN_HOST_URL=http://127.0.0.1:7193 \
MACRO_DECK_PLUGIN_ENROLLMENT_TOKEN=<your token> \
./MacroDeck.PluginTemplate
```

On PowerShell:

```powershell
$env:MACRO_DECK_PLUGIN_MODE = "SelfRegistering"
$env:MACRO_DECK_PLUGIN_HOST_URL = "http://127.0.0.1:7193"
$env:MACRO_DECK_PLUGIN_ENROLLMENT_TOKEN = "<your token>"
.\MacroDeck.PluginTemplate.exe
```

Run it from the build output directory: the SDK reads `manifest.json` from the content root, and the
manifest's icon path is resolved against it. `macrodeck-plugin run --host-url ... --mode
self-registering --enrollment-token ...` does the same thing without the manual environment.

### Later runs

The enrollment token is only needed the first time. The plugin exchanges it for a secret and persists
that, so afterwards this is enough:

```bash
MACRO_DECK_PLUGIN_MODE=SelfRegistering \
MACRO_DECK_PLUGIN_HOST_URL=http://127.0.0.1:7193 \
./MacroDeck.PluginTemplate
```

The secret is stored per plugin id under the platform state directory:

| Platform | Location |
| --- | --- |
| Windows | `%LOCALAPPDATA%\MacroDeck\plugins\<pluginId>\credentials.json` |
| macOS | `~/Library/Application Support/MacroDeck/plugins/<pluginId>/credentials.json` |
| Linux | `$XDG_STATE_HOME/macro-deck/plugins/<pluginId>/credentials.json` |

Delete that file to force a fresh enrollment. `MACRO_DECK_PLUGIN_STATE_DIRECTORY` overrides the
location. Anything your plugin itself writes belongs under `MACRO_DECK_PLUGIN_DATA_DIRECTORY` - it is
the only writable location that survives an update or a rollback.

## The developer CLI

`macrodeck-plugin` validates, inspects, packs, runs and conformance-tests a plugin without Macro Deck
installed.

```bash
dotnet tool install --global MacroDeck.Plugin.Cli --prerelease
```

`--prerelease` is required while the 3.0 SDK is in preview: only preview versions are published, and
`dotnet tool install` picks stable ones by default. Drop it once 3.0 ships.

The tool needs the **ASP.NET Core shared framework**, not just the .NET runtime - its stub host is a
real Kestrel server.

| Command | What it does |
| --- | --- |
| `validate` | Checks a manifest, version directory or artifact against the real manifest reader, the JSON Schema, the permission vocabulary and declared file digests. |
| `inspect` | Reports what installing an artifact would find - entrypoints, permissions, dependencies, conflicts, compatibility, signature shape, size. |
| `pack` | Builds a `.macroDeckPlugin` artifact, validating the manifest first and recomputing `files[]` digests. |
| `run` | Launches the plugin the way the supervisor does, against a stub host or a real one. |
| `test` | Runs the conformance suite and writes a text, JSON or Markdown report. |

### Packing a release

```bash
dotnet build -c Release
```

```bash
macrodeck-plugin validate --manifest src/MacroDeck.PluginTemplate/bin/Release/net10.0/manifest.json
```

```bash
macrodeck-plugin pack --source src/MacroDeck.PluginTemplate/bin/Release/net10.0
```

```bash
macrodeck-plugin inspect --artifact <id>-<version>.macroDeckPlugin
```

`pack` validates before it writes, so a bad manifest never becomes an artifact. It discards whatever
`files[]` the source manifest declared and recomputes every digest from disk. It cannot sign anything:
sign *after* packing, against the packed manifest, or the digest will not match.

`--output` defaults to `<id>-<version>.macroDeckPlugin`; `--force` overwrites an existing file.

### Conformance

```bash
macrodeck-plugin test --project src/MacroDeck.PluginTemplate --report markdown --output conformance.md
```

The suite drives a real session against your plugin: capability contracts, invocation and cancellation
semantics, reconnect and resume behaviour, the reserved `/_macrodeck/*` endpoints, and logging limits.
Checks are Required or Recommended, each with a stable id (`MDC0401`, …) you can select with `--check`
or `--category`. A check can report `SKIP` with a reason when your plugin gives it nothing to observe -
which is most of them until you add capabilities.

Exit codes make it usable as a CI gate - `0` conformant, `1` the plugin is wrong, `2` usage error, `3`
input unreadable, `4` cancelled. `1` and `3` are deliberately distinct: a missing file is an
environment problem, not a verdict about the plugin.

## Testing

```bash
dotnet test
```

The test project references `MacroDeck.Plugin.Testing`, which provides a loopback test host, fakes and
assertions for testing a plugin without a running Macro Deck. `PluginTestHarness.Create` builds your
plugin from the same `Action<PluginHostBuilder>` `Program.cs` uses - no socket, no host, no built
executable - with a `ManualTimeProvider` for the clock and a `FakeIntegrationContext` you can seed and
assert against:

```csharp
await using var harness = PluginTestHarness.Create(builder => builder.RegisterIntegration<PluginIntegration>());
await harness.InitializeIntegrationsAsync();
```

Drive capabilities through the typed clients it exposes (`harness.Actions`, `harness.Variables`, …)
rather than calling an executor directly, so parameter binding is under test too.
`MacroDeckTestHost.HostAsync` puts the wire itself under test, and `MacroDeckTestHost.LaunchAsync`
runs a real child process.

The conformance suite above covers the protocol contract; these tests are for your own behaviour.

## Contributing to the template

The template repository's root *is* the `dotnet new` content, so changing the template is an ordinary
change to the plugin in `src/`. How the package is built and released is documented in
[`packaging/README.md`](https://github.com/Macro-Deck-App/Macro-Deck-Plugin-Template/blob/main/packaging/README.md).

## License

MIT - see [LICENSE](LICENSE). Macro Deck itself is licensed under Apache 2.0.

## Further reading

- [Plugin development docs](https://github.com/Macro-Deck-App/Macro-Deck-3/tree/main/docs/plugin-development)
- [Sample plugins](https://github.com/Macro-Deck-App/Macro-Deck-Sample-Plugins) - a worked example per capability
- [`plugin-hosting.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/plugin-hosting.md) - the builder API, registration modes, the artifact format and every `MACRO_DECK_PLUGIN_*` variable
- [`sdk-reference.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/sdk-reference.md) - every interface and record you build against
- [`cli.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/cli.md) - every CLI command and option
- [`testing-plugins.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/testing-plugins.md) - the test harness, the fakes and the manual clock
- [`conformance.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/conformance.md) - the conformance suite and its check ids
- [`analyzers.md`](https://github.com/Macro-Deck-App/Macro-Deck-3/blob/main/docs/plugin-development/analyzers.md) - the compile-time diagnostics
