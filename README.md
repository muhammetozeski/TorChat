# TorChat

I got bored one weekend and wanted to write a chat program. TorChat is a decentralized P2P chat application that operates over the Tor network using `.onion` addresses.

## Features

- **P2P Tor Network:** Connects directly to peers using Tor Onion Services.
- **In-Memory Key Handling:** The Tor secret key is encrypted in RAM using Windows DPAPI or AES-GCM, and passed to Tor via the Control Port (`ADD_ONION`).
- **UI:** A standard WinForms interface with a dark mode toggle.
- **Identity & Profiles:** Generate new Onion addresses dynamically or import existing keys. Includes basic profile and online status support.

## Getting Started

### Prerequisites
- Windows OS
- .NET 10.0 Runtime (If using the framework-dependent build)

### Installation
1. Go to the [Releases](../../releases) page.
2. Download either the Portable version or the Framework-Dependent version.
3. Extract and run `Chat.exe`.
4. Wait for Tor to bootstrap to 100%.

### Usage
- On the first run, configure your secret key (generate a new one or load an existing Base64 Tor private key).
- Share your `.onion` address with peers.
- Enter a peer's `.onion` address and click Connect.

## Build Instructions

1. Clone the repository:
   ```bash
   git clone https://github.com/muhammetozeski/TorChat.git
   cd TorChat
   ```
2. Restore and Build:
   ```bash
   dotnet restore
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/Portable
   ```

## License
MIT License.
