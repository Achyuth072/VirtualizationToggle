# VirtualizationToggle

> **Disclaimer**: This is **vibe coded** — I have no experience in .NET. It was built to solve a specific personal pain point.

The primary reason for building this tool is that undervolting with **ThrottleStop** does not work when the **Virtual Machine Platform** is enabled on Windows 11.

This creates a conflict between:
*   **Work/Productivity**: Needing WSL2, WSA, or Docker (requires Virtualization).
*   **Gaming**: Needing ThrottleStop for undervolts (requires Virtualization disabled).

Manually toggling these features to switch between working and gaming is a hassle. This project solves that problem with a simple system tray toggle.

## Features

- **System Tray Integration**: Runs quietly in the background with a system tray icon.
- **Visual Status Indicator**:
  - 🟢 **Green Icon**: Virtualization is Enabled (Ready for WSL2, WSA, Docker).
  - 🔴 **Red Icon**: Virtualization is Disabled (Ready for ThrottleStop, etc.).
- **One-Click Toggle**: Easily switch state via the context menu.
- **Auto-Restart Scheduling**: Option to schedule a restart immediately or after a delay to apply changes.
- **Run at Startup**: Built-in option to launch automatically when you log in.
  - *Note*: This feature uses **Windows Task Scheduler** to create a task with "Highest Privileges". This allows the app to start with Administrator rights without triggering a UAC prompt every time you boot.

## Prerequisites

- **Operating System**: Windows 10 or Windows 11.
- **Runtime**: [.NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0).
- **Privileges**: Must be run as **Administrator** to modify system features.

## Installation & Usage

1.  **Download/Build**: Download the latest release or build the project from source.
2.  **Run**: Execute `VirtualizationToggle.exe`.
    - *Note*: You must run the application as Administrator.
3.  **Interact**:
    - Locate the icon in your system tray.
    - Right-click to open the menu.
    - Select **Toggle Virtualization** to change the state.
    - Select **Run at Windows Startup** to enable auto-start.

## How It Works

The application uses PowerShell commands to manage Windows Optional Features.

- **To Enable**:
  ```powershell
  Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-Hypervisor -NoRestart
  Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -NoRestart
  ```

- **To Disable**:
  ```powershell
  Disable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-Hypervisor -NoRestart
  Disable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -NoRestart
  ```

Changes to these features require a system restart to take effect. The application handles this by offering to restart immediately or schedule it for later.
