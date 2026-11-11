# GreenLumaPresets

GreenLumaPresets is a WPF .NET 8 application designed to manage and manipulate presets for GreenLuma. This project provides a user-friendly interface to import, create, rename, and delete presets and their associated App IDs.

![presets](https://github.com/user-attachments/assets/4e78ca32-f696-44a7-9164-4074519f0924)

## Features

- **Create Presets**: Create new presets and add App IDs.
- **Edit Presets**: Rename and delete existing presets.
- **Manage App IDs**: Add, rename, and delete App IDs within presets.
- **Import Presets**: Import a game and all its DLC from Steam (by AppID or by name), or AppIDs from the clipboard.
- **Validation**: Invalid and duplicate AppIDs are rejected or skipped on import.
- **GreenLuma Integration**: Load app lists, restart Steam, clear the app list and the Steam app cache.
- **GreenLuma Installation**: Install GreenLuma from a local archive or a URL (with optional SHA-256 verification), and uninstall it.
- **Auto-update**: Checks for new releases on startup.

## Requirements

- .NET 8.0
- Visual Studio 2022 or later

## Getting Started

### Prerequisites

Ensure you have the following installed:

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/)

### Installation

1. Clone the repository:

```
git clone https://github.com/AnsNihility/GreenLuma-Presets.git
cd GreenLumaPresets
```

2. Open the solution file `GreenLumaPresets.sln` in Visual Studio.

3. Restore the NuGet packages:

```
dotnet restore
```

4. Build the project:

```
dotnet build
```

### Running the Application

1. Start the application from Visual Studio by pressing `F5` or using the `dotnet run` command:

```
dotnet run --project GreenLumaPresets
```

## Usage

### Importing Presets

1. Click the `Import` button.
2. Select `Import from Clipboard` to import App IDs from the clipboard.

### Creating a New Preset

1. Click the `Create Preset` button.
2. A new preset named "New Preset" will be added to the list.

### Managing App IDs

- **Add App ID**: Click the `Create App ID` button.
- **Rename App ID**: Right-click on an App ID and select `Rename`.
- **Delete App ID**: Right-click on an App ID and select `Delete`.

### GreenLuma Integration

- **Load App List**: Click the `Load` button.
- **Restart Steam**: Click the `Load and launch Steam` button.

## Configuration

Settings are read from `appsettings.json`, next to the executable. The file is optional: copy `appsettings.example.json` to `appsettings.json` to customize them. Updates never overwrite it.

| Setting | Description |
|---|---|
| `GreenLumaArchiveUrl` | URL of a GreenLuma archive to download when installing. |
| `GreenLumaArchivePath` | Path to a local GreenLuma archive (relative to the executable, `GreenLuma.rar` by default). Takes precedence over the URL when the file exists. |
| `GreenLumaArchiveSha256` | Optional SHA-256 the archive must match before it is extracted into the Steam folder. Strongly recommended. |
| `MaxAppListSize` | Optional number of AppIDs supported by your GreenLuma version. Loading a bigger preset asks for confirmation. |

Presets, logs (7 days kept) and the download cache are stored in `%LocalAppData%\GreenLumaPresets`. A `presets.db` left next to the executable by an older version is copied there on first launch.

The app runs without administrator rights. Windows only asks for permission when adding the Windows Defender exclusion and when installing an update.

Uninstalling GreenLuma removes every file the installer extracted (listed in `GreenLumaPresets.installed.json` in the Steam folder). Files that already existed, such as Steam files overwritten by the archive, are never removed.

## Development

```
dotnet test GreenLumaPresets.sln
```

### Releasing

1. Bump `<Version>` in `GreenLumaPresets/GreenLumaPresets.csproj` and merge to `main`.
2. Start the **Release** workflow: either run it from the Actions tab on `main`, or push a tag matching the version (`git tag v1.4.0 && git push origin v1.4.0`).

The workflow runs the tests, builds `GreenLumaPresets-v<version>-win-x64.zip` with `scripts/release.ps1`, creates the GitHub release with the archive, then commits the regenerated `update.xml` (with the archive's SHA-256) to `main`. Clients pick up the update from there.

`./scripts/release.ps1` can also be run locally to build the archive.

## Disclaimer

GreenLuma alters how Steam checks licenses. Using it may violate the Steam Subscriber Agreement and put your account at risk. Use at your own risk.
