# GreenLumaPresets

GreenLumaPresets is a WPF .NET 8 application designed to manage and manipulate presets for GreenLuma. This project provides a user-friendly interface to import, create, rename, and delete presets and their associated App IDs.

## Features

- **Create Presets**: Create new presets and add App IDs.
- **Edit Presets**: Rename and delete existing presets.
- **Manage App IDs**: Add, rename, and delete App IDs within presets.
- **Import Presets**: Import presets from the clipboard.
- **GreenLuma Integration**: Load app lists and restart Steam with GreenLuma.

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

- **Load App List**: Click the `Load Preset` button.
- **Restart Steam**: Click the `Load and Launch Steam` button.
