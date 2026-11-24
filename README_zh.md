# FireflyFrp Windows 客户端

[English](README.md) | [简体中文](README_zh.md)

基于 **C# WPF / .NET 10** 的 FireflyFrp Windows 桌面客户端，提供：

- 登录 / 注册 / 自动登录 / 保存登录状态
- 顶部展示剩余流量、限速、并发上限
- 隧道卡片展示（TCP / UDP / HTTP / HTTPS）
- 开关启用/停止隧道
- 隧道在线后展示外网地址，点击复制
- 创建/删除隧道
- 最小化到托盘；关闭时选择“直接退出”或“最小化到托盘”，支持“下次不再提示”
- 内置 Windows `frpc.exe`，无需单独安装

---

## 1. 环境要求

- Windows 10 / 11 x64
- .NET 10 SDK（开发）或 .NET 10 Desktop Runtime（运行）
- 可访问 FireflyFrp 用户服务后端
- 后端已配置 frps 插件、管理员充值入口等

> 当前项目目标框架为 `net10.0-windows`，因为开发机安装了 .NET 10 SDK。  
> 如需 .NET 8，安装 .NET 8 SDK/Desktop targeting pack 后，把 `FireflyFrp.Client.csproj`
> 和测试项目的 `TargetFramework` 改为 `net8.0-windows` 即可。

---

## 2. 快速开始

```bash
cd FireflyFrp-Client
dotnet restore
dotnet build
dotnet run --project src/FireflyFrp.Client
```

首次启动显示登录窗口：

1. 输入用户服务地址（例如 `https://api.example.com`）；
2. 输入用户名和密码；
3. 可勾选“保存登录状态”；
4. 点击登录。

没有账号时，点击登录页下方的 **“没有账号？立即注册”** 切换到注册模式：

- 输入用户名、密码、确认密码；
- 点击“注册并登录”，注册成功后会自动登录；
- 新用户默认流量配额为 0，请联系管理员充值后才能开启隧道；
- 点击“已有账号？返回登录”可切回登录模式。

登录成功后进入主界面，顶部显示用户名、剩余流量和限速，下面展示隧道卡片。

登录后可点击右上角 **更多 → 修改密码**，输入原密码和新密码（8-72 位）。修改成功后所有登录会话会失效，客户端会停止本机隧道并返回登录页，使用新密码重新登录。

---

## 3. 配置文件 `client.config.json`

文件位于 exe 同目录（开发时在 `src/FireflyFrp.Client/client.config.json`，发布时复制到输出目录）。

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

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `serverUrl` | string | `""` | 用户服务 API 根地址，例如 `https://api.example.com`。留空时登录页显示服务端地址输入框。 |
| `lockServerUrl` | bool | `false` | 设为 `true` 且 `serverUrl` 非空时，登录页**不显示服务端地址**，固定使用 `serverUrl`。 |
| `frpcPath` | string | `""` | 为空时优先使用 `Resources\frpc\frpc.exe`；如果复制时没有带上该目录，会自动释放内置 frpc 到 `%LocalAppData%\FireflyFrp\bin\frpc.exe`。也可填绝对路径覆盖。 |
| `pollIntervalSeconds` | int | `3` | 主界面轮询用户信息的间隔（秒），用于刷新剩余流量、限速、隧道在线状态。 |
| `requestTimeoutSeconds` | int | `10` | API 请求超时（秒）。 |
| `useSystemProxy` | bool | `true` | 是否使用系统代理访问 API。设为 `false` 可绕过代理直连。 |
| `trustedCertificates` | list[string] | `[]` | 预置信任的 HTTPS 证书。可填证书文件路径（相对 exe 或绝对路径），或 SHA-256 指纹（如 `sha256:AB:CD:...`）。 |

### 托管部署示例

如果客户端只用于你自己的服务器，可以这样锁定地址：

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

这样登录页不显示服务端地址，用户只能登录该服务，也无法修改地址。

---

## 4. 登录与安全存储

- 勾选“保存登录状态”后，客户端使用 **Windows DPAPI（CurrentUser）** 加密保存：
  - `%AppData%\FireflyFrp\accounts\<accountKey>\auth.dat`
  - 内容：服务端地址、用户名、平台 token、过期时间
- **不会保存密码**。
- 下次启动自动读取并调用 `/api/v1/user` 恢复登录：
  - 401 → 清除保存状态，回到登录页；
  - 网络错误 → 保留状态，提示无法连接服务器。
- 退出登录/禁用账号/会话过期时清除 `auth.dat` 并停止所有本机隧道。

### 4.1 HTTPS 证书信任

使用 `https://` 服务端地址时，客户端会校验证书：

- 系统已信任的证书直接连接；
- 自签名等不可信证书会弹出证书详情，由用户选择“信任并继续”或取消；
- 选择信任后会保存到 `%AppData%\FireflyFrp\trusted-certs.json`，后续同一证书自动信任；
- 也可以在 `client.config.json` 中提前配置：

```json
{
  "serverUrl": "https://frp-api.example.com",
  "trustedCertificates": [
    "certs/server.crt",
    "sha256:AB:CD:EF:..."
  ]
}
```

证书文件路径相对 exe 目录或使用绝对路径。只应信任自己服务器的证书；信任未知证书会带来中间人攻击风险。

隧道启动配置同样使用 DPAPI 加密保存：

```
%AppData%\FireflyFrp\accounts\<accountKey>\tunnels\<tunnelId>.dat
```

运行时临时配置写入：

```
%LocalAppData%\FireflyFrp\run\<tunnelId>.toml
```

停止隧道或退出客户端时删除临时配置。日志位于：

```
%LocalAppData%\FireflyFrp\logs\
```

---

## 5. 主界面与隧道卡片

### 顶部

- 剩余流量（如 `8.5 GB`）
- 已用 / 总配额
- 当前限速（如 `10 Mbps`，0 显示“不限速”）
- 同时开启上限（如 `3 条`）
- 左上角只显示当前用户名，不显示服务端地址
- 新建隧道 / 刷新 / 更多
- 更多菜单：修改密码 / 浅色模式或深色模式 / 检查更新 / 开源软件许可证 / 退出登录
  - 主题项按当前主题显示“浅色模式”或“深色模式”，点击后切换
  - 主题默认跟随系统；切换后的选择会保存到 `%AppData%\FireflyFrp\settings.json` 的 `Theme` 字段（`system` / `light` / `dark`）
  - 检查更新：打开客户端仓库 Releases 页
  - 开源软件许可证：打开客户端仓库 LICENSE 页

### 隧道卡片

- 名称、类型徽标（TCP/UDP/HTTP/HTTPS）
- 本地目标 `127.0.0.1:22`
- 状态：在线 / 隧道已关闭（绿色）/ 启动中 / 停止中 / 其他客户端运行中 / 连接断开
- 主动退出隧道时不会显示 frpc 原始日志；异常退出才显示错误尾行
- 外网地址（仅在线时显示）：
  - TCP/UDP：`host:port`
  - HTTP/HTTPS：`http://domain` / `https://domain`
  - 点击“复制”写入剪贴板
- 隧道自身流量
- 开关：启用/停止
- “接管”：隧道正在其他客户端运行时，重置令牌并接管
- “删除”：删除隧道（在线时先断开）

### 启动/停止流程

- 启动：
  1. 检查剩余流量和该类型是否被服务端禁用；
  2. 读取本机保存的 frpc 配置；
  3. 没有配置时，确认后调用 `POST /api/v1/tunnels/{id}/token` 轮换令牌并获取新配置；
  4. 启动内置 `frpc.exe`；
  5. 轮询后端直到隧道在线。
- 停止：结束本机 frpc 进程，后端停止该隧道计费，其他隧道不受影响。

> 轮换令牌会使其地方已保存的旧令牌失效。隧道正在其他客户端运行时，需要通过“接管”确认。

---

## 6. 创建/删除隧道

- “新建隧道”对话框：
  - 类型下拉框只显示后端 `enabled_tunnel_types` 中允许的类型；
  - 输入名称、本地 IP、本地端口；
  - 成功后自动加密保存 frpc 配置并刷新列表。
- 删除：
  - 二次确认；
  - 在线隧道会先断开；
  - 删除本地保存的隧道配置。

---

## 7. 关闭行为与托盘

### 关闭窗口

点击窗口 X 或 Alt+F4：

- 默认弹出对话框：
  - **直接退出（停止所有隧道）**
  - **最小化到托盘（隧道继续运行）**
  - 复选框 **下次不再提示**
- 勾选“下次不再提示”后，下次直接执行所选行为，不再弹窗。
- 托盘菜单 → “关闭行为”可改回：
  - 每次询问
  - 最小化到托盘
  - 直接退出

### 托盘菜单

- 显示主窗口（双击托盘图标同样效果）
- 新建隧道
- 停止所有隧道
- 关闭行为
- 退出（停止所有隧道后退出）

最小化到托盘会显示气泡提示；隧道保持运行。

---

## 8. 图标

图标只用于 exe / 窗口 / 任务栏 / 托盘，不在登录页和主界面显示。

```
FireflyFrp-Client/图标.ico                 # 唯一的图标源文件（多尺寸 ico）
```

csproj 的 `<ApplicationIcon>` 和 WPF `Resource` 都指向 `图标.ico`；托盘服务使用
`Assets/Icons/app.ico`（由 `图标.ico` 链接而来）。如需替换图标，直接替换
`图标.ico` 后重新构建即可。

---

## 9. 构建与发布

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

发布目录包含：

- `FireflyFrp.exe`（单文件自包含）
- `client.config.json`
- `Resources\frpc\frpc.exe`
- 运行所需的其他文件

`frpc.exe` 同时内嵌在 `FireflyFrp.exe` 中：如果只复制 exe 和 `client.config.json` 到其他路径或电脑，首次启动隧道时会自动释放到 `%LocalAppData%\FireflyFrp\bin\frpc.exe`。如果 `Resources\frpc\frpc.exe` 存在，则优先使用外部文件，便于不重新打包就升级 frpc。

---

## 10. 常见问题

| 现象 | 处理 |
|---|---|
| 无法连接服务器 | 检查 `serverUrl`、网络、HTTPS 证书；确认用户服务已启动。 |
| 提示服务器证书不受信任 | 确认服务器身份后点击“信任并继续”；也可在 `client.config.json` 的 `trustedCertificates` 预置证书文件或 SHA-256 指纹。 |
| 登录已失效 | 重新登录；或检查后端 session 过期时间。 |
| 流量不足 | 管理员在用户服务中充值（`remaining_traffic_bytes`）。 |
| 最多同时开启 3 条 | 先停止其他隧道；上限由后端 `max_active_tunnels` 控制。 |
| 其他客户端运行中 | 点击“接管”，确认后轮换令牌并启动本机 frpc。 |
| 找不到 frpc.exe | 检查 `frpcPath` 或发布目录 `resources\frpc\frpc.exe`。 |
| 隧道类型不可选 | 服务端禁用了 HTTP/HTTPS；`GET /api/v1/user` 的 `enabled_tunnel_types` 决定可选项。 |
| 关闭后客户端不见了 | 可能被设置为最小化到托盘；双击托盘图标恢复。 |
| 想恢复关闭询问 | 托盘菜单 → 关闭行为 → 每次询问。 |

---

## 11. 已知限制

- Windows 专用（WPF）；没有 macOS/Linux 版本。
- 只支持后端允许的 `tcp` / `udp` / `http` / `https`，不支持 STCP/XTCP/SUDP。
- 隧道关闭窗口后是否继续运行取决于“关闭行为”；选择“直接退出”会停止所有隧道。
- 轮换令牌会使旧令牌失效，这是服务端令牌安全模型决定的。
- 客户端不保存密码；自动登录依赖 DPAPI 保存的平台 token。
- 生产发布建议代码签名，避免 SmartScreen 提示。
