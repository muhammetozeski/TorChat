# TorChat

![TorChat Logo](icon.ico)

TorChat is a modern, highly secure, and entirely decentralized P2P chat application operating over the Tor network. It leverages `.onion` addresses to ensure complete anonymity and privacy for both parties involved in the communication.

## Features

- **Decentralized P2P Network:** No central servers. Your device connects directly to your peers using Tor Onion Services.
- **In-Memory Encryption (RAM):** The Tor hidden service secret key is never written directly to the disk in plaintext. It is encrypted in RAM using DPAPI (Data Protection API) or AES-GCM (PBKDF2) and only decrypted for a split second when handed to the Tor Control Port.
- **Control Port Integration:** Tor is launched dynamically. Onion addresses and keys are securely passed to Tor via the Control Port using `ADD_ONION`.
- **Dynamic Identity Management:** Generate new identities (Onion addresses) on the fly, or import your existing keys. 
- **Modern UI/UX:** Clean, responsive WinForms UI with Dark Mode support and rich messaging features.
- **Peer Profiles:** View and edit profiles, bios, and see when your peers were last online.

## Security Architecture

1. **Tor Network:** All traffic is routed through the Tor network, ensuring end-to-end encryption, metadata stripping, and IP address masking.
2. **Ephemeral Key Management (`SecureRamKey`):**
   - Keys are loaded into memory and immediately protected using Windows `ProtectedData` (DPAPI) bound to the current user.
   - When Tor requires the key for the `ADD_ONION` command, it is decrypted into a temporary buffer, passed to Tor, and immediately zeroed out in memory.
3. **Storage Security:** 
   - Application settings and default secrets can be stored securely next to the executable using user-selected protection mechanisms (DPAPI or Type1 Password-based AES).

## Getting Started

### Prerequisites
- Windows OS (Tested on Windows 10/11)
- .NET 10.0 Runtime (If using the framework-dependent build)

### Installation
1. Head over to the [Releases](../../releases) tab.
2. Download either the **Portable** version (includes all dependencies, no installation required) or the **Framework-Dependent** version (requires .NET 10 installed).
3. Extract and run `Chat.exe`.
4. Wait for the Tor bootstrap process to reach 100%.

### Usage
- On the first run, TorChat will ask you to configure your secret key. You can generate a new one or load an existing Base64 ED25519-V3 Tor private key.
- Share your `.onion` address (found in your profile or at the bottom of the window) with your friends.
- Type their `.onion` address in the target box and hit **Connect**.
- Once connected, you can chat securely!

## Build Instructions

If you want to compile TorChat from source:

1. Clone this repository:
   ```bash
   git clone https://github.com/YOUR_USERNAME/TorChat.git
   cd TorChat
   ```
2. Restore dependencies:
   ```bash
   dotnet restore
   ```
3. Build and Publish:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/Portable
   ```

## License
MIT License.
