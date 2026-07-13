# Phase 1: Core Foundation & Provisioning - Technical Research

## 1. Stack & API Investigation

### Adoptium (Temurin) API v3
To obtain download links for Temurin OpenJDK binaries, we will use Adoptium's v3 redirect API. The stable redirect URL format is:
`https://api.adoptium.net/v3/binary/latest/{version}/ga/{os}/{arch}/{image_type}/hotspot/normal/eclipse`

- **Parameters**:
  - `{version}`: `8`, `11`, `17`, `21`, `25`
  - `{os}`: `linux` or `mac`
  - `{arch}`: `x64` or `aarch64` (for Apple Silicon)
  - `{image_type}`: `jdk`
- **Behavior**: The HTTP request will return a `307 Temporary Redirect` directing the client to the closest CDN mirror download link (e.g. `https://github.com/adoptium/temp-binaries/...`). The C# download manager must configure `HttpClientHandler.AllowAutoRedirect = true` (default in .NET HttpClient) to follow redirects.

### PocketMine-MP PHP Binaries
PocketMine PHP binaries are compiled and published under the `pmmp/PHP-Binaries` repository on GitHub.
- **Download Source**: Releases page assets.
- **Naming Convention**: `PHP-{version}-{os}-{arch}.tar.gz` (e.g., `PHP-8.2-Linux-x86_64.tar.gz` or `PHP-8.2-macOS-arm64.tar.gz`).
- **Extraction**: Must be extracted from `tar.gz` archive. The binary directory structure includes a `bin/` folder with `php` executable and libraries. We must programmatically assign executive permissions (`chmod +x`) to the extracted `bin/php` binary after decompression.

### Cross-Platform Secure Storage (D-01)
We will define an interface `ISecretStore` in `PocketMC.Core`.
- **macOS Implementation**: Accesses the native OS Keychain Services API. We will use P/Invoke calls to `security.framework` (specifically functions `SecItemAdd`, `SecItemCopyMatching`, `SecItemUpdate`, and `SecItemDelete`) with the service tag `PocketMC`.
- **Linux Implementation**: Accesses the GNOME Keyring / libsecret API using P/Invoke to `libsecret-1.so.0` (calling `secret_password_store_sync`, `secret_password_lookup_sync`, and `secret_password_clear_sync` with application attributes).
- **AES Fallback**: If P/Invoke loading fails or standard DBus/Keychain is not configured (e.g. headless CI environments), we fall back to local AES-256-GCM encryption. The key is derived using PBKDF2 from a combination of the host's system MAC address and the machine-id (`/etc/machine-id` or macOS IOPlatformUUID). The encrypted value is saved in settings.json under a dedicated encrypted block.

## 2. Directory Layout & Storage (D-03)

The application will default to the standard directory roots:
- Linux: `~/.config/PocketMC/`
- macOS: `~/Library/Application Support/PocketMC/`

The settings file is `settings.json` located at the root of the config directory. It will contain:
```json
{
  "version": 1,
  "customDataRoot": null,
  "downloadedRuntimes": {
    "java": {
      "21": "/path/to/extracted/java-21",
      "17": "/path/to/extracted/java-17"
    },
    "php": {
      "8.2": "/path/to/extracted/php-8.2"
    }
  }
}
```
If `customDataRoot` is populated, the directory initializer will redirect `Instances/`, `Backups/`, `Downloads/`, `Cache/`, and `Logs/` subfolders to that directory, while keeping `settings.json` itself at the default system location.

## 3. Pre-Launch Binary Validation (D-04)

Before starting any server instance, the provisioning system must validate the executable:
1. File existence checks at the registered path.
2. Executable permission bit verification.
3. Version output check by launching the process with version arguments:
   - Java: execute `java -version` and capture standard error (Java outputs version here) or standard output to parse the version string via regex.
   - PHP: execute `php -v` and capture stdout.
4. If version output does not match expected headers (e.g. missing executable or platform architecture mismatch), the manager blocks execution, alerts the UI service, and triggers a prompt to reinstall.

## 4. Validation Architecture

We will implement verification mechanisms to confirm all Phase 1 requirements:
- **Unit Tests**:
  - Test secret store operations using a mocked P/Invoke backend to verify libsecret, Keychain, and AES Fallback logic.
  - Test safe ZIP extraction (`SafeZipExtractor`) with path validation to ensure zip-slip vectors are blocked.
  - Test directory builder logic with custom root path overrides.
- **Integration Tests**:
  - Test Temurin API download redirects and verify package extraction.
  - Test PocketMine PHP binary download, extraction, permission setting, and executing `php -v` version parsing.

## 5. Pitfalls & Landmines

- **macOS Code Signing & Gatekeeper**: Executable runtimes downloaded via the Adoptium API or GitHub releases may trigger gatekeeper flags (`quarantine` attribute) on macOS. We must programmatically run `xattr -d com.apple.quarantine <file>` if download check fails, or prompt the user.
- **libsecret DBus Dependency**: On headless Linux servers or CI agents, DBus might not be running, causing native libsecret P/Invokes to fail. The fallback mechanism must catch these initialization exceptions gracefully and switch to the AES provider instantly without crashing the app.
- **Tar.gz permissions loss**: Extracting files using standard C# zip/tar libraries can drop Unix execution bits. We must verify that `chmod +x` is run explicitly on the php binary.
