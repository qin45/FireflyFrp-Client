# FireflyFrp Windows Client

[English](README.md) | [简体中文](README_zh.md)

A **C# WPF / .NET 10** desktop client for FireflyFrp. It provides:

- Login, registration, automatic login, and remembered sessions
- Remaining traffic, rate limit, and concurrent tunnel limit in the header
- Tunnel cards for TCP / UDP / HTTP / HTTPS tunnels
- Start and stop tunnels with a switch
- Public address displayed after a tunnel comes online, with one-click copy
- Create and delete tunnels
- Minimize-to-tray and configurable close behavior, including "don't ask again"
- Bundled Windows `frpc.exe`; no separate installation required

---

## 1. Requirements

- Windows 10 / 11 x64
- .NET 10 SDK (development) or .NET 10 Desktop Runtime (running the published build)
- Network access to a FireflyFrp user service
- A backend configured with the frps plugin and an admin top-up flow

> The project targets `net10.0-windows` because the development machine has the
> .NET 10 SDK. To use .NET 8, install the .NET 8 SDK/Desktop targeting pack and
> change `TargetFramework` to `net8.0-windows` in `FireflyFrp.Client.csproj` and
> the test project.

---

## 2. Quick Start

```bash
cd FireflyFrp-Client
dotnet restore
dotnet build
dotnet run --project src/FireflyFrp.Client
```

On first launch, the login window appears:

1. Enter the user service URL (for example `https://api.example.com`).
2. Enter the username and password.
3. Optionally check **Remember login**.
4. Click **Log in**.

To create an account, click **"No account? Register"** at the bottom of the
login page:

- Enter a username, password, and confirmation password.
- Click **"Register and log in"**; a successful registration logs in automatically.
- New users start with a 0 traffic quota. An administrator must top up the
  account before tunnels can be started.
- Click **"Already have an account? Back to login"** to return.

After login, the main window shows the username, remaining traffic, rate limit,
and tunnel cards.

To change the password, open **More → Change Password** (top-right), enter the
old and new passwords (8-72 characters), and confirm. A successful change
invalidates all login sessions; the client stops local tunnels and returns to
the login page so you can sign in with the new password.

---

## 3. Configuration File `client.config.json`

The file lives next to the executable. During development it is at
`src/FireflyFrp.Client/client.config.json`; publish copies it to the output
directory.

```json
{
  "serverUrl": "",
  "lockServerUrl": false,
  "frpcPath": "",
  "pollIntervalSeconds": 3,
  "requestTimeoutSeconds": 10,
  "useSystemProxy": true,
  "trustedCertificates": []
}
```

| Field | Type | Default | Description |
|---|---|---|---|
| `serverUrl` | string | `""` | User service API root, for example `https://api.example.com`. Leave empty to show the server URL field on the login page. |
| `lockServerUrl` | bool | `false` | When `true` and `serverUrl` is set, the login page hides the server address and uses `serverUrl` exclusively. |
| `frpcPath` | string | `""` | When empty, uses `Resources\frpc\frpc.exe`; if that directory is absent, the embedded frpc is extracted to `%LocalAppData%\FireflyFrp\bin\frpc.exe`. An absolute path overrides this. |
| `pollIntervalSeconds` | int | `3` | Polling interval for user information (seconds): traffic, rate limit, and tunnel state. |
| `requestTimeoutSeconds` | int | `10` | API request timeout in seconds. |
| `useSystemProxy` | bool | `true` | Use the system proxy for API requests. Set to `false` to connect directly. |
| `trustedCertificates` | list[string] | `[]` | Pre-trusted HTTPS certificates. Entries can be certificate file paths (relative to the executable or absolute) or SHA-256 fingerprints such as `sha256:AB:CD:...`. |

### Managed Deployment Example

If the client is only used with your own server, lock the address:

```json
{
  "serverUrl": "https://frp-api.example.com",
  "lockServerUrl": true,
  "frpcPath": "",
  "pollIntervalSeconds": 3,
  "requestTimeoutSeconds": 10,
  "useSystemProxy": true,
  "trustedCertificates": []
}
```

The login page then shows no server address, and users cannot change it.

---

## 4. Login and Secure Storage

- When **Remember login** is checked, the client encrypts and stores:
  - `%AppData%\FireflyFrp\accounts\<accountKey>\auth.dat`
  - Contents: server URL, username, platform token, and expiry time
- **Passwords are never stored.**
- On the next launch, the client reads the saved session and calls
  `/api/v1/user` to restore the login:
  - 401 → clears the saved session and returns to the login page;
  - Network error → keeps the session and reports that the server is unreachable.
- Logout, disabled accounts, and expired sessions clear `auth.dat` and stop all
  local tunnels.

### 4.1 HTTPS Certificate Trust

When using an `https://` server URL, the client validates the certificate:

- Certificates trusted by Windows connect without a prompt.
- Self-signed or otherwise untrusted certificates show a details dialog. The
  user can choose **Trust and continue** or cancel.
- A trusted certificate is stored in
  `%AppData%\FireflyFrp\trusted-certs.json` and trusted automatically on later
  connections.
- Certificates can also be preconfigured in `client.config.json`:

```json
{
  "serverUrl": "https://frp-api.example.com",
  "trustedCertificates": [
    "certs/server.crt",
    "sha256:AB:CD:EF:..."
  ]
}
```

Certificate file paths are relative to the executable unless absolute. Only
trust certificates from your own server; trusting an unknown certificate exposes
you to man-in-the-middle attacks.

Tunnel startup configurations are also encrypted with DPAPI:

```
%AppData%\FireflyFrp\accounts\<accountKey>\tunnels\<tunnelId>.dat
```

Runtime configuration files are written to:

```
%LocalAppData%\FireflyFrp\run\<tunnelId>.toml
```

They are deleted when a tunnel stops or the client exits. Logs are written to:

```
%LocalAppData%\FireflyFrp\logs\
```

---

## 5. Main Window and Tunnel Cards

### Header

- Remaining traffic (for example `8.5 GB`)
- Used / total quota
- Current rate limit (for example `10 Mbps`; 0 is displayed as "Unlimited")
- Concurrent tunnel limit (for example `3`)
- Only the current username is shown at the top-left; the server URL is hidden
- **New Tunnel** / **Refresh** / **More**
- More menu: **Change Password** / **Light mode** or **Dark mode** / **Check for Updates** / **Open-Source Licenses** / **Log out**
  - The theme item shows the opposite mode of the current theme and switches to it when clicked
  - The theme follows the system by default; the selected mode is saved as the `Theme` field (`system` / `light` / `dark`) in `%AppData%\FireflyFrp\settings.json`
  - Check for Updates opens the client repository's Releases page
  - Open-Source Licenses opens the client repository's LICENSE page

### Tunnel Cards

- Name and type badge (TCP/UDP/HTTP/HTTPS)
- Local target, for example `127.0.0.1:22`
- State: Online / Tunnel closed (green) / Starting / Stopping / Running in another client / Disconnected
- Stopping a tunnel intentionally does not show raw frpc logs; only abnormal exits show an error tail
- Public address, shown only while online:
  - TCP/UDP: `host:port`
  - HTTP/HTTPS: `http://domain` / `https://domain`
  - **Copy** writes the address to the clipboard
- Per-tunnel traffic
- Switch to start/stop
- **Take over**: when the tunnel is running in another client, reset the token and take it over
- **Delete**: remove the tunnel (disconnecting it first if online)

### Start/Stop Flow

- Start:
  1. Check remaining traffic and whether the tunnel type is enabled on the server.
  2. Load the local frpc configuration.
  3. If no configuration exists, confirm and call
     `POST /api/v1/tunnels/{id}/token` to rotate the token and download a new
     configuration.
  4. Start the bundled `frpc.exe`.
  5. Poll the backend until the tunnel is online.
- Stop: terminate the local frpc process; the backend stops billing for this
  tunnel without affecting other tunnels.

> Rotating the token invalidates any previously saved token. If the tunnel is
> running in another client, use **Take over** to confirm.

---

## 6. Creating and Deleting Tunnels

- **New Tunnel** dialog:
  - The type list only contains types allowed by the backend's
    `enabled_tunnel_types`.
  - Enter the name, local IP, and local port.
  - On success, the frpc configuration is encrypted and saved locally, and the
    tunnel list is refreshed.
- Delete:
  - Requires confirmation.
  - An online tunnel is disconnected first.
  - The locally stored tunnel configuration is removed.

---

## 7. Close Behavior and Tray

### Closing the Window

Using the window X button or Alt+F4:

- By default, a dialog offers:
  - **Exit directly (stop all tunnels)**
  - **Minimize to tray (keep tunnels running)**
  - **Don't ask again**
- After checking **Don't ask again**, the selected action is used on subsequent
  closes without another prompt.
- Tray menu → **Close behavior** can reset it to:
  - Ask every time
  - Minimize to tray
  - Exit directly

### Tray Menu

- Show main window (double-clicking the tray icon also works)
- New tunnel
- Stop all tunnels
- Close behavior
- Exit (stops all tunnels and exits)

Minimizing to the tray shows a notification; tunnels keep running.

---

## 8. Icon

The icon is used only for the executable, window, taskbar, and tray. It is not
shown on the login page or main window.

```
FireflyFrp-Client/图标.ico                 # the only icon source (multi-size ico)
```

Both the csproj `<ApplicationIcon>` and the WPF `Resource` point to `图标.ico`.
The tray service uses `Assets/Icons/app.ico`, linked from `图标.ico`. To change
the icon, replace `图标.ico` and rebuild.

---

## 9. Build and Publish

```bash
cd FireflyFrp-Client
dotnet restore
dotnet build
dotnet test

dotnet publish src/FireflyFrp.Client -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o publish
```

The publish directory contains:

- `FireflyFrp.exe` (single-file, self-contained)
- `client.config.json`
- `Resources\frpc\frpc.exe`
- Other runtime files

`frpc.exe` is also embedded in `FireflyFrp.exe`: if only the exe and
`client.config.json` are copied to another folder or computer, the first tunnel
start extracts it to `%LocalAppData%\FireflyFrp\bin\frpc.exe`. If
`Resources\frpc\frpc.exe` exists, that external file is used first, which makes
it possible to upgrade frpc without repackaging the client.

---

## 10. FAQ

| Symptom | Resolution |
|---|---|
| Cannot connect to the server | Check `serverUrl`, the network, and the HTTPS certificate; confirm the user service is running. |
| The server certificate is untrusted | Verify the server identity and click **Trust and continue**; or preconfigure the certificate file or SHA-256 fingerprint in `client.config.json` under `trustedCertificates`. |
| Login has expired | Log in again, or check the backend session expiry. |
| Not enough traffic | Ask an administrator to top up the account (`remaining_traffic_bytes`). |
| At most 3 active tunnels | Stop another tunnel first; the backend's `max_active_tunnels` controls this. |
| Running in another client | Click **Take over**, confirm, then rotate the token and start local frpc. |
| `frpc.exe` is missing | Check `frpcPath` or the publish directory's `resources\frpc\frpc.exe`. |
| A tunnel type is unavailable | HTTP/HTTPS may be disabled on the server; `enabled_tunnel_types` from `GET /api/v1/user` controls the choices. |
| The client disappears after closing | It may be configured to minimize to the tray; double-click the tray icon to restore it. |
| Restore the close prompt | Tray menu → Close behavior → Ask every time. |

---

## 11. Known Limitations

- Windows only (WPF); no macOS or Linux client.
- Supports only `tcp` / `udp` / `http` / `https` allowed by the backend; STCP,
  XTCP, and SUDP are not supported.
- Whether tunnels keep running after closing the window depends on the close
  behavior; **Exit directly** stops all tunnels.
- Token rotation invalidates old tokens by design.
- The client does not store passwords; automatic login uses a DPAPI-protected
  platform token.
- Code signing is recommended for production releases to avoid SmartScreen
  warnings.
