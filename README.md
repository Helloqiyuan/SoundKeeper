# SoundKeeper

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6.svg)](#系统要求)
[![Release](https://img.shields.io/badge/release-v1.0.0-brightgreen.svg)](CHANGELOG.md)

> 一个 Windows 纯托盘小工具：持续输出**人耳无法察觉**的极低幅度音频流，
> 让蓝牙耳机始终认为"有音频在播放"，从而避免因长时间无音频而自动休眠或断连。

无窗口、不占任务栏、无需管理员权限，双击即用。

[English](README.en.md) | 简体中文

---

## 目录

- [解决的问题](#解决的问题)
- [功能特性](#功能特性)
- [快速开始](#快速开始)
- [工作原理](#工作原理)
- [系统要求](#系统要求)
- [从源码构建](#从源码构建)
- [配置存储](#配置存储)
- [自定义信号强度](#自定义信号强度)
- [常见问题](#常见问题)
- [项目结构](#项目结构)
- [参与贡献](#参与贡献)
- [许可证](#许可证)

---

## 解决的问题

很多蓝牙耳机（尤其是降噪型号）为了省电，会在"一段时间没有音频数据"后自动进入休眠，
表现为必须重新播放声音才会"唤醒"，或者干脆断开连接、延迟明显。

常见的"循环播放静音文件"方案效果不稳定，原因是：

- 部分协议栈/固件会把**全零静音帧**直接判定为空闲（idle），不重置休眠计时器；
- 循环播放音频文件会引入解码、重采样与间隙，某些情况下反而造成流中断。

SoundKeeper 的做法是**在程序内实时生成严格非零的极小样本**并直接推送到音频设备，
让音频会话持续存在，耳机侧的休眠计时器不断被重置。

## 功能特性

- 🎧 **持续音频保活**：向默认输出设备推流约 -90 dBFS 的非零样本，人耳完全不可察觉
- 🖱️ **纯托盘操作**：右键菜单即可启停，双击图标快速切换，无任何窗口
- 🚀 **开机自启动**：一键注册，写入 `HKCU` 注册表，**无需管理员权限、不弹 UAC**
- 💾 **状态记忆**：记住上次的保活开关，下次启动自动恢复
- 🔄 **热插拔自愈**：耳机断开/重连、默认设备切换后自动重建音频流
- 🔒 **单实例保护**：重复启动不会出现多个托盘图标
- 🎨 **零资源依赖**：托盘图标由代码绘制（绿=运行中 / 灰=已停止），无需外部图片文件
- 🔍 **内置自检**：`--selftest` 一键验证音频链路与注册表读写
- 📦 **单文件发布**：自包含 exe，目标机器无需安装 .NET 运行时

## 快速开始

1. 从 [Releases](https://github.com/Helloqiyuan/SoundKeeper/releases) 下载 `SoundKeeper.exe`。
2. 双击运行。系统托盘区会出现一个**灰色圆点**图标（当前为已停止状态）。
3. **右键**该图标：

   | 菜单项 | 说明 |
   | --- | --- |
   | **音频保活** | 勾选 = 开始输出保活音频流（图标变**绿色**）；取消 = 停止推流并释放设备 |
   | **开机自动启动** | 勾选 = 写入开机自启项；取消 = 移除 |
   | **退出** | 停止音频流、清理托盘图标、结束进程 |

4. 点击「音频保活」，图标变绿即表示保活已生效。

> 提示：**双击**托盘图标等同于快速切换「音频保活」开关。

首次使用建议同时勾选「开机自动启动」，之后开机即静默进入托盘，无需手动操作。

## 工作原理

### 为什么能防止耳机休眠

蓝牙耳机在"一段时间没有音频数据"后进入休眠或断开。SoundKeeper 持续向默认输出设备
推流，使音频会话始终存在，耳机侧的计时器被不断重置，从而维持链路活跃。

### 为什么人耳听不到

输出样本值为 `1/32768`（float 范围 `[-1, 1]` 内的最小值），约合 **-90 dBFS**，
远低于任何人耳可闻阈值。同时程序使用**共享模式**输出，不影响其它程序正常播放声音。

### 为什么不能输出纯静音

部分蓝牙协议栈和耳机固件会把"全零静音帧"判定为空闲，从而不重置休眠计时器——
这正是很多"播放静音文件"方案失效的原因。因此这里必须输出**严格非零**的极小值。

### 关键技术细节

音频格式必须规整为纯 `IeeeFloat`：

```csharp
// 设备返回的通常是 WaveFormatExtensible（32bit float 包在 extensible 容器里），
// 直接交给 WasapiOut 会抛 "Must be already floating point"。
// 必须显式创建纯浮点格式。
var mixFormat = device.AudioClient.MixFormat;
var format = WaveFormat.CreateIeeeFloatWaveFormat(mixFormat.SampleRate, mixFormat.Channels);
```

采样率与声道数取自设备自身的 `MixFormat`，避免 WASAPI 插入重采样层导致流间断。

## 系统要求

| 项目 | 要求 |
| --- | --- |
| 操作系统 | Windows 10 / 11 (x64) |
| 运行时 | **无需安装**（发布版为自包含单文件） |
| 权限 | 普通用户即可，**不需要管理员权限** |
| 依赖 | [NAudio](https://github.com/naudio/NAudio) 2.2.1（已内嵌） |

## 从源码构建

需要 **.NET SDK 10**（或 8 及以上版本）。

```bash
# 还原依赖
dotnet restore -r win-x64

# 调试运行
dotnet run

# 发布单文件（自包含，脱离 .NET 环境也能运行）
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

产物路径：`bin\Release\net10.0-windows\win-x64\publish\SoundKeeper.exe`

### 命令行参数

| 参数 | 说明 |
| --- | --- |
| （无） | 正常启动，进入托盘 |
| `--silent` | 静默进入托盘（开机自启使用） |
| `--selftest` | 自检模式：验证音频链路与注册表读写，结果写入 exe 同目录 `selftest.log` 后退出 |

部署后如需确认音频链路正常，运行一次：

```bash
SoundKeeper.exe --selftest
```

查看 `selftest.log`，出现 `RESULT: OK` 即表示核心功能正常。

## 配置存储

所有设置保存在**当前用户**注册表下，因此无需管理员权限，也不会触发 UAC：

| 用途 | 注册表位置 | 值 |
| --- | --- | --- |
| 保活开关（记住上次状态） | `HKCU\Software\SoundKeeper` → `Enabled` | `1` / `0` |
| 开机自启动 | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `SoundKeeper` | `"<exe路径>" --silent` |

程序启动时读取 `Enabled`，若上次是开启状态则自动恢复保活。

> ⚠️ **移动程序位置后需重新勾选自启**：注册表记录的是绝对路径，移动 exe 后旧路径失效。
> 菜单会通过提示气泡提醒，取消再勾选一次即可刷新。

## 自定义信号强度

默认幅度约 -90 dBFS（`1/32768`），绝大多数蓝牙耳机足以保持链路活跃。

若你的耳机固件要求更强的信号能量（少见，表现为耳机仍会休眠），修改
`AudioKeepAlive.cs` 中创建 provider 的调用：

```csharp
// 默认（-90 dBFS，人耳完全不可闻）
new TinySampleProvider(format)

// 增强版（-60 dBFS，对信号能量敏感的固件可改用这个）
new TinySampleProvider(format, TinySampleProvider.StrongAmplitude)
```

`TinySampleProvider` 已内置两个预定义幅度常量：`DefaultAmplitude` 与 `StrongAmplitude`。

## 常见问题

<details>
<summary><b>勾选「音频保活」后耳机还是会休眠？</b></summary>

可能原因：

1. **耳机固件要求更强的信号能量**。尝试按[自定义信号强度](#自定义信号强度)
   改用 `StrongAmplitude`（-60 dBFS）重新编译。
2. **默认输出设备不是你的耳机**。本程序只作为"默认输出设备的一个音频源"，
   请确认系统默认播放设备已切换到耳机。
3. **耳机有独立的省电策略**。部分耳机（如某些 TWS）的自动关机是固件行为，
   与音频流无关，软件层面无法阻止。
</details>

<details>
<summary><b>会影响我听音乐/看视频吗？</b></summary>

不会。程序使用 WASAPI **共享模式**输出，与其他程序共存，且信号幅度低至 -90 dBFS，
人耳无法察觉。你可以在系统音量合成器中看到 SoundKeeper 的会话，属正常现象。
</details>

<details>
<summary><b>为什么任务栏里看不到窗口？</b></summary>

这是设计如此。程序不创建任何窗体，只通过 `NotifyIcon` 存在于系统托盘区。
如果托盘图标被折叠，点击托盘区的"显示隐藏的图标"箭头即可找到。
</details>

<details>
<summary><b>每次开机都会弹出 UAC 提示？</b></summary>

不应该。程序只写 `HKCU`（当前用户）注册表，清单中声明
`requestedExecutionLevel level="asInvoker"`，不请求管理员权限。
若出现 UAC 提示，请检查是否有安全软件对自启项做了额外拦截。
</details>

<details>
<summary><b>程序会不会占用很多 CPU / 内存？</b></summary>

不会。音频样本是恒定值，生成逻辑为简单赋值循环，CPU 占用极低；实测内存约 40–50 MB。
</details>

## 项目结构

```
SoundKeeper/
├── Program.cs                  # 入口：解析 --silent / --selftest，单实例保护
├── TrayApplicationContext.cs   # 托盘生命周期、右键菜单、状态刷新
├── AudioKeepAlive.cs           # NAudio 推流封装（含热插拔自愈）
├── TinySampleProvider.cs       # 极小非零样本生成器
├── AppSettings.cs              # 注册表持久化（HKCU）
├── TrayIconFactory.cs          # 代码绘制双态托盘图标
├── NativeMethods.cs            # Win32 互操作（释放图标句柄）
├── app.manifest                # 应用清单（asInvoker + 兼容性声明）
├── SoundKeeper.csproj          # 项目文件
├── NuGet.Config                # 包源配置
├── LICENSE                     # MIT
└── CHANGELOG.md                # 变更记录
```

## 参与贡献

欢迎提交 Issue 和 Pull Request。

1. Fork 本仓库
2. 创建特性分支：`git checkout -b feature/your-feature`
3. 提交变更：`git commit -m "feat: add some feature"`
4. 推送分支：`git push origin feature/your-feature`
5. 发起 Pull Request

提交前请确保：

- `dotnet build -c Release` 无错误、无警告
- 若修改了音频相关逻辑，运行 `SoundKeeper.exe --selftest` 确认输出 `RESULT: OK`

提交信息建议遵循 [Conventional Commits](https://www.conventionalcommits.org/zh-hans/) 规范。

## 许可证

本项目基于 [MIT License](LICENSE) 开源。

## 致谢

- [NAudio](https://github.com/naudio/NAudio) — 强大的 .NET 音频库，本项目的核心依赖
