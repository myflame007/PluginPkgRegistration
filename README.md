# Dataverse Plugin Registration Tool

Dataverse plugin registration tool for **NuGet-based (dependent assembly) plugins**.
Reads `CrmPluginRegistrationAttribute` and registers steps + images — like spkl, but for PluginPackages.

## Installation

```bash
# From NuGet (when published)
dotnet tool install --global Dataverse.PluginRegistration

# Or from local build
dotnet pack
dotnet tool install --global --add-source ./nupkg Dataverse.PluginRegistration
```

## Quick Start

```bash
# 1. In your plugin project directory (or repo root):
plugin-reg init                  # Creates pluginreg.json + scaffolds Attributes/

# 2. Create a .env file next to pluginreg.json (never commit this):
#    DATAVERSE_DEV_URL=https://yourorg.crm4.dynamics.com
#    DATAVERSE_APPID=51f81489-12ee-4a9e-aaae-a2591f45987d
#    DATAVERSE_REDIRECT_URI=http://localhost

# 3. Build your project (Debug configuration), then:
plugin-reg list                  # Dry-run: shows discovered steps (no connection needed)
plugin-reg register --env dev    # Deploy package + register steps
```

## Commands

| Command    | Description |
|------------|-------------|
| `init`     | Create a `pluginreg.json` config file in the current directory |
| `register` | Push NuGet package + register plugin steps in Dataverse |
| `list`     | List discovered steps from assembly (dry-run, no connection needed) |

## Configuration

### pluginreg.json

Commit this file to git. Use `${VAR}` placeholders for environment-specific values.

`plugin-reg init` auto-detects the project type and generates the correct DLL path:
- **Classic `.csproj`** (e.g. Dynamics/CRM projects): `bin\Debug\MyPlugin.dll`
- **SDK-style `.csproj`** (e.g. .NET 6+): `bin\Debug\net8.0\MyPlugin.dll`

```json
{
  "assemblies": [
    {
      "name": "MyPlugin",
      "path": "bin\\Debug\\MyPlugin.dll",
      "nupkgPath": "bin\\Debug\\MyPlugin.1.0.0.nupkg",
      "publisherPrefix": "pub",
      "solutionName": "MySolution_unmanaged"
    }
  ],
  "environments": {
    "dev": {
      "url": "${DATAVERSE_DEV_URL}",
      "authType": "OAuth",
      "appId": "${DATAVERSE_APPID}",
      "redirectUri": "${DATAVERSE_REDIRECT_URI}",
      "loginPrompt": "Auto"
    }
  }
}
```

### .env

**Never commit this file** — add `.env` to your `.gitignore`. Each developer creates their own copy locally.

```env
DATAVERSE_DEV_URL=https://myorg-dev.crm4.dynamics.com
DATAVERSE_LIVE_URL=https://myorg.crm4.dynamics.com
DATAVERSE_REDIRECT_URI=http://localhost

# AppId — use the Microsoft XRM Tooling app for local development (no own App Registration needed):
DATAVERSE_APPID=51f81489-12ee-4a9e-aaae-a2591f45987d
```

> **AppId note:** `51f81489-12ee-4a9e-aaae-a2591f45987d` is Microsoft's official "XRM Tooling" app,
> the same one used by PAC CLI, XrmToolBox, and spkl. It has Dataverse permissions pre-configured
> and works for interactive (browser) login out of the box — no Azure AD App Registration required.
> For CI/CD or service accounts, use a dedicated App Registration with a client secret instead.

## What it does

1. **Step 1/2 — Push Plugin Package**: Creates or updates the PluginPackage in Dataverse from the `.nupkg` file
2. **Step 2/2 — Register Steps**: Reads `CrmPluginRegistrationAttribute` from the DLL and syncs steps + images with change detection (only updates when something changed)

## Features

- Reads `CrmPluginRegistrationAttribute` via `MetadataLoadContext` (no assembly locking)
- NuGet package deployment (replaces `pac plugin push`)
- Step + image registration with **change detection** (CREATED / UPDATED / UNCHANGED)
- `.env` file support with `${VAR}` placeholder resolution
- Custom browser authentication page (German)
- Ctrl+C cancellation support

## CLI Options

```
plugin-reg register --env <name>              Use named environment from pluginreg.json
plugin-reg register --dll <path>              Override DLL path
plugin-reg register --connection <string>     Use raw connection string
plugin-reg register --config <path>           Custom config file path
plugin-reg register --assembly-name <name>    Override assembly name
```
