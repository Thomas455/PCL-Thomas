# PCL（Plain Craft Launcher）代码结构解析

> **对象**：本仓库（PCL Thomas455 修改版 2.99.1.0），源码基线为上游 PCL `2.99.0.0` 开源版
> **提交**：`3b75656`（写作时 HEAD）
> **统计口径**：`Plain Craft Launcher 2/` 下全部 `.vb` / `.xaml`，排除 `bin/`、`obj/`
> **规模**：177 个 `.vb` / `.xaml` 源文件，约 4.9 万行（另有约 100 个图片、字体、jar 等资源文件）

---

## 目录

1. [项目总览](#一项目总览)
2. [解决方案与工程依赖](#二解决方案与工程依赖)
3. [目录结构与职责](#三目录结构与职责)
4. [启动流程与页面框架](#四启动流程与页面框架)
5. [加载器体系（核心设计）](#五加载器体系核心设计)
6. [基础设施层](#六基础设施层)
7. [网络层](#七网络层)
8. [业务模块层](#八业务模块层)
9. [UI 控件库](#九ui-控件库)
10. [设置系统与事件系统](#十设置系统与事件系统)
11. [关键调用链](#十一关键调用链)
12. [本分支改动索引](#十二本分支改动索引)
13. [构建与排错](#十三构建与排错)
14. [阅读路线建议](#十四阅读路线建议)
15. [附录](#附录)

---

## 一、项目总览

PCL 是一个 **.NET Framework 4.8 + WPF + VB.NET** 的 Minecraft: Java Edition 启动器。它不是"一个 VM 加几个页面"的简单应用，而是把**所有耗时操作都抽象成可组合的加载器（Loader）**，UI 只负责展示加载器状态。理解这一点，整个代码库就读通了。

### 架构分层

```
┌──────────────────────────────────────────────────────────────┐
│ 表现层   FormMain（主窗体/页面路由）                           │
│          Pages/*（页面）+ Controls/*（自绘控件库）              │
├──────────────────────────────────────────────────────────────┤
│ 交互层   ModMain（提示/弹窗/帮助/遥测）                        │
│          ModEvent（自定义事件）+ Settings/SettingService（设置）│
├──────────────────────────────────────────────────────────────┤
│ 业务层   ModLaunch（启动）  ModDownload（下载源）              │
│          ModDownloadLib（安装引擎）  ModMinecraft（实例模型）  │
│          ModModpack（整合包）  ModJava  ModCrash  ModWatcher   │
│          Resource*（社区资源）  ModMusic  ModSecret            │
├──────────────────────────────────────────────────────────────┤
│ 基础层   ModLoader（加载器/任务栏进度）  ModNet（HTTP/下载）    │
│          ModBase（INI/进程/线程/日志/颜色）  ModAnimation       │
│          ModValidate（校验规则）  MyBitmap（图片）              │
├──────────────────────────────────────────────────────────────┤
│ 依赖层   MeloongCore（netstandard2.0 工具库）                  │
│          MeloongCore.Wpf（net48 WPF 辅助）  PCLCS（C# 领域模型）│
└──────────────────────────────────────────────────────────────┘
```

### 三个工程的角色

| 工程 | 语言 / 框架 | 角色 |
|---|---|---|
| `Plain Craft Launcher 2` | VB.NET / WPF / net48 | 主程序。**几乎全部 UI 与业务逻辑都在这里** |
| `PCLCS` | C# / net48 | 领域模型与工具：`McVersion`、`McInstallManifest`、`Resource`、`WikiEntry`、`Java`、`LaunchUtils`，并内嵌 `JavaWrapper.jar`、`LwjglUnsafeAgent.jar`、`WikiEntries.txt`、`OptiFineHashes.json` |
| `MeloongCore` | C# / netstandard2.0 | 通用库：`FileUtils`、`PathUtils`、`RegistryUtils`、`CryptographyUtils`、扩展方法（`EndsWithF` / `StartsWithF` 等）、`LoaderBase` 之外的 Worker 原语 |
| `MeloongCore.Wpf` | C# / net48 | WPF 辅助：`RunInUi`/`RunInThread` 之类线程切换、UI 工具 |

> **关键点**：`PCLCS` 与两个 `MeloongCore` 的产物 **不是以 DLL 文件形式随 exe 分发的**，而是作为**嵌入资源**打包进 `Plain Craft Launcher 2.exe`，运行时由 `AppDomain.AssemblyResolve` 从资源中反序列化加载（见 `Application.xaml.vb` 的 `New()`）。所以发布出来只有一个 exe，这是 PCL 单文件分发的实现方式。

---

## 二、解决方案与工程依赖

`Plain Craft Launcher 2.sln` 包含 4 个工程：

```
Plain Craft Launcher 2.vbproj ─┬─> MeloongCore.csproj
   (VB.NET, net48, WinExe)     ├─> MeloongCore.Wpf.csproj
                               └─> PCLCS.csproj
```

### 三种构建配置

| 配置 | `DefineConstants` | `VersionCode` | 用途 |
|---|---|---|---|
| `Debug` | `SNAPSHOT` | 414（快照） | 本地调试，带 `#If SNAPSHOT` 分支 |
| `Snapshot` | `SNAPSHOT` | 414 | 官方快照版发布用 |
| `Release` | `RELEASE` | 413（正式版） | 正式发布（本分支的发布配置） |

三者都输出到同一个 `bin\` 目录（`OutputPath=bin\`，无 `$(Configuration)` 子目录），切换配置会互相覆盖，这是 PCL 的既定习惯。

### 单文件分发机制（重要）

`Application.xaml.vb` 的私有构造函数里做了两件事：

```vb
Static Prefixes As String() = {"NAudio", "Newtonsoft.Json", "Ookii.Dialogs.Wpf",
    "Imazen.WebP", "CacheCow.Common", ..., "PCLCS", "MeloongCore.Wpf", "MeloongCore"}
AddHandler AppDomain.CurrentDomain.AssemblyResolve, Function(...) ...
    ' 把 My.Resources.ResourceManager.GetObject(前缀) 的字节流 Assembly.Load 进来
```

- 注释明确写了：**"这里必须尽早调用，且不能使用任何库，否则加载 MeloongCore 就会崩溃"**——所以这段代码在类型初始化最早期执行。
- `Imazen.WebP` 是特例：加载它之前必须先把 `libwebp64.dll` 落到 `PathPure`（exe 所在目录），因此有 `ExtractLibwebp()`。
- vbproj 里对应的声明是 `<None Include="..\PCLCS\bin\PCLCS.dll"><LogicalName>PCLCS.dll</LogicalName>`，`LogicalName` 决定资源名。

**迁移/裁剪时注意**：删掉任何 `Prefixes` 中的条目会导致运行期 `FileNotFoundException`，而不是编译错误。

---

## 三、目录结构与职责

```
Plain Craft Launcher 2/
├── Application.xaml(.vb)     应用入口：协议/参数解析、DLL 动态加载、全局异常
├── FormMain.xaml(.vb)        主窗体：页面路由、标题栏、窗口缩放、退出流程
├── App.config / app.manifest 运行时配置与清单（DPI、管理员权限声明）
│
├── Modules/                  ── 无 UI 的逻辑层（最关键的部分）
│   ├── ModMain.vb            提示、弹窗、帮助文档、遥测、反馈、GPU 设置
│   ├── ModEvent.vb           自定义事件（可点击控件的行为分发）
│   ├── ModSecret.vb          凭据、主题配色、检查更新、赞助等级
│   ├── ModSecretBuild.vb     ★本分支新增：编译期内置凭据（gitignore）
│   ├── ModMusic.vb           背景音乐
│   ├── ModDevelop.vb         开发者模式入口
│   ├── Base/
│   │   ├── ModBase.vb        全局常量、INI 读写、进程/线程、颜色与几何、杂项工具
│   │   ├── ModLoader.vb      ★加载器体系：LoaderBase/Task/Combo/Worker + 任务栏进度
│   │   ├── ModNet.vb         HTTP 请求、文件下载、分片、镜像测速、端口工具
│   │   ├── ModAnimation.vb   动画 DSL（AaXxx 构造 + AniStart 播放）
│   │   ├── ModValidate.vb    输入校验规则集合
│   │   ├── MyBitmap.vb       图片解码（含 WebP）
│   │   └── PclLogger.vb      日志器（5 文件轮转，Alert/Toast 行为）
│   ├── Minecraft/
│   │   ├── ModMinecraft.vb   实例模型（McFolder/McInstance）、皮肤、库与资源索引
│   │   ├── ModLaunch.vb      ★启动流水线 + 四种登录方式（微软 6 步）
│   │   ├── ModDownload.vb    版本列表与加载器列表（Mojang/BMCLAPI 双源）
│   │   ├── ModModpack.vb     整合包识别与安装（8 种格式）
│   │   ├── ModJava.vb        Java 探测、匹配、下载
│   │   ├── ModCrash.vb       崩溃分析器（规则引擎）
│   │   └── ModWatcher.vb     游戏进程监控
│   ├── Resource/             社区资源（Mod/整合包/材质/光影/数据包）
│   │   ├── ResourceSearcher.vb   搜索编排（中文翻译 + 双平台请求）
│   │   ├── ResourceProject.vb    项目模型（CurseForge / Modrinth 统一）
│   │   ├── ResourceVersion.vb    版本/文件模型与下载地址解析
│   │   ├── LocalResourceFile.vb  本地文件模型（含哈希指纹）
│   │   └── LocalResourceLoaders.vb 本地扫描与在线更新检查
│   └── ThirdParty/DragHelper.vb  拖拽文件钩子
│
├── Pages/                    ── 页面（每个页面 = XAML + 同名 .xaml.vb）
│   ├── PageLaunch/           启动页（账号、版本选择、皮肤、各类登录子页）
│   ├── PageDownload/         下载页
│   │   ├── ModDownloadLib.vb ★安装引擎（2176 行，实际执行下载安装）
│   │   ├── PageDownloadInstall   安装游戏（版本选择 + 加载器）
│   │   ├── PageDownloadModify    修改已有实例
│   │   └── Resource/             资源搜索/详情/各资源类型页
│   ├── PageInstance/         实例管理（总览/设置/Mod/导出）
│   ├── PageSetup/            设置（启动/界面/系统/链接）+ Settings.vb + SettingService.vb
│   ├── PageOther/            更多（帮助/关于/百宝箱(已移除)/反馈/投票）
│   ├── PageLink/             联机（EasyTier，依赖官方配置）
│   ├── PageSpeedLeft/Right   下载源测速
│   └── PageSelectLeft/Right  选择页（首次使用引导）
│
├── Controls/                 ── 自绘控件库（不使用系统默认样式）
│   ├── MyButton / MyIconButton / MyIconTextButton / MyTextButton / MyExtraButton ...
│   ├── MyCard               卡片容器（标题、可折叠 CanSwap、自动高度）
│   ├── MyListItem           列表项（图标/标题/描述/右侧按钮/单选框）
│   ├── MyLoading            加载动画控件，绑定 LoaderBase 状态显示
│   ├── MyTextBox            带校验规则的输入框
│   ├── MySlider / MySliderDot / MyScrollBar / MyScrollViewer / MyComboBox
│   ├── MyPageLeft / MyPageRight  左右分栏页面基类（PageOnEnter/Exit 生命周期）
│   ├── MyMsg/{MyMsgText,MyMsgInput,MyMsgSelect}  弹窗内容控件
│   ├── MyResizer            无边框窗体的边缘拖拽缩放
│   ├── MyDropShadow         窗口阴影
│   ├── MyVirtualizingElement 虚拟化容器（长列表性能）
│   └── Behaviors/           LazyLoadBehavior（懒加载）、ClipboardInterceptor（剪贴板拦截）
│
├── Resources/               嵌入资源：字体、Help.zip、Custom.xaml、各 jar
├── Images/                  嵌入图片：图标、头像、方块、皮肤、主题预览
└── My Project/              AssemblyInfo.vb、Resources.Designer.vb、Settings.Designer.vb
```

---

## 四、启动流程与页面框架

### 4.1 应用启动顺序

```
Application.New()                  ← 注册 AssemblyResolve（必须最先）
  └─ Application_Startup
       ├─ 单实例检查（Mutex + FindWindow 唤醒已有窗口）
       ├─ 命令行参数：--update / --gpu / --memory / 文件拖入
       ├─ 环境检查（.NET Framework 版本 ≥ 528040）
       ├─ 字体加载修复（#3467）
       └─ FrmMain 初始化（三阶段）
```

`FrmMain` 的初始化分三个阶段（见 `FormMain.xaml.vb` 中 `RunInUi(Sub() ... "初始化")` 的分段注释）：

| 阶段 | 内容 |
|---|---|
| 第一阶段 | 窗口尺寸/位置恢复、主题刷新 `ThemeRefresh(...)`、`ThemeCheckAll(False)` |
| 第二阶段 | 页面框架搭建、左侧导航、标题栏按钮 |
| 第三阶段 | 加载器池启动：`JavaInit()`、`DlClientListMojangLoader.Start(1)`、`RunCountSub()`、`ServerLoader.Start()`、清理临时文件、`RefreshSecret()`、遥测 |

### 4.2 页面路由

`FormMain` 里定义了两个关键枚举：

```vb
Enum PageType        ' 一级页面
  Launch = 0, Download = 1, Link = 2, Setup = 3, Other = 4
End Enum

Enum PageSubType     ' 二级页面（数值即页内索引）
  ' 下载
  DownloadInstall = 0, DownloadMod = 2, DownloadPack = 3, DownloadDataPack = 4,
  DownloadResourcePack = 5, DownloadShader = 6
  ' 设置
  SetupLaunch = 0, SetupLink = 1, SetupUI = 2, SetupSystem = 3
  ' 更多
  OtherHelp = 0, OtherAbout = 1, OtherTest = 2
  ' 实例
  InstanceOverall = 0, InstanceSetup = 1, InstanceModify = 2, InstanceMod = 3,
  InstanceModDisabled = 4, InstanceExport = 5
End Enum
```

切换逻辑：`FrmMain.PageChange(PageType, PageSubType)` → 各 `PageXxxLeft` 左栏根据 `Tag` 值调用 `PageChange(Val(Tag))` → `PageGet(ID)` 返回**单例页面实例**（`FrmSetupUI`、`FrmOtherLeft`、`FrmOtherTest` 等都在 `ModMain.vb:391` 一带声明为共享字段）。

页面生命周期由 `MyPageRight` 提供：`PageOnEnter()` / `PageOnExit()` / `PageOnForceExit()`，配合 `AniStart` 做进场动画。

> **陷阱**：`Handles Me.Loaded` 会在每次切回页面时重复触发，所以 PCL 的页面普遍用 `Static Reloaded As Boolean` 做"仅首次"逻辑，例如 `PageSetupUI_Loaded`。

---

## 五、加载器体系（核心设计）

这是整个代码库最重要的抽象，位于 `Modules/Base/ModLoader.vb`。

### 5.1 类层次

```vb
Public MustInherit Class LoaderBase           ' ModLoader.vb:7   基类：状态机 + 进度 + 输出
Public Class LoaderTask                       ' ModLoader.vb:264 单个任务（输入 → 输出）
Public Class LoaderCombo                      ' ModLoader.vb:422 组合多个加载器，串行/并行
Public Class LoaderWorker                     ' ModLoader.vb:468 无输入的循环任务
Public Class LoaderDownload                   ' ModNet.vb:1312   下载专用加载器
```

状态枚举（`ModBase.vb`）：

```vb
Enum LoadState
  Waiting / Loading / Finished / Failed / Canceled
End Enum
```

### 5.2 典型用法

```vb
' 1) 声明（模块级单例，全局复用一个）
Public DlClientListLoader As New LoaderTask(Of String, DlClientListResult)(
    "DlClientList Main", AddressOf DlClientListMain)

' 2) 触发并等待
DlClientListLoader.WaitForExit(Id, IsForceRestart:=True)
Dim Output = DlClientListLoader.Output

' 3) 组合：按进度分段执行多个加载器
Dim Loaders As New List(Of LoaderBase)
Loaders.Add(New LoaderTask(...) With {.ProgressWeight = 0.5, .Block = False})
LoaderCombo.Run(Loaders)
```

### 5.3 为什么这么设计

- **UI 与逻辑解耦**：`MyLoading` 控件绑定 `LoaderBase`，页面不需要写"正在下载…"这类状态文字。
- **可组合**：安装一个 Forge 版本 = `加载版本列表 → 下载 json → 下载 libraries → 下载 assets → 安装加载器` 五个 `LoaderTask` 串成 `LoaderCombo`，各自带 `ProgressWeight`，总进度自动算。
- **可重入**：`ReloadTimeout`（如 `ServerLoader` 设 1 小时）让加载器结果可缓存复用。
- **可取消**：`LoaderBase.Cancel()` 会抛出 `OperationCanceledException`，调用方统一用 `ex.ThrowIfCanceled()` 识别。

---

## 六、基础设施层

### 6.1 `ModBase.vb`（1216 行）

| 类别 | 成员 | 说明 |
|---|---|---|
| 版本常量 | `VersionBaseName`、`VersionCode`、`VersionDisplay`、`BuildTypeDisplay` | 显示用版本名与构建类型 |
| 路径 | `PathExe`（exe 全路径）、`PathTemp`（缓存目录，可配置）、`Paths.Base` | |
| INI | `ReadIni` / `WriteIni` / `HasIniKey` / `DeleteIniKey` / `IniClearCache` | 带内存缓存；简写文件名会落到 `PCL\xxx.ini` |
| 进程 | `StartProcess`（自动处理长路径）、`RunAsAdmin`、`GetProgramArgument` | |
| 线程 | `RunInThread`、`RunInNewThread`、`RunInUi`、`RunInUiWait` | UI 线程切换的唯一入口 |
| 颜色/几何 | `MyColor`（HSL 构造 `FromHSL2`、运算符重载）、`MyRect` | 主题系统的基础 |
| 杂项 | `GetTimeMs`、`GetUuid`、`OpenWebsite`、`OpenExplorer`、`ClipboardSet`、`RandomOne`、`ExtractResources` | |
| 搜索 | `SearchSimilarity`、`SearchSimilarityWeighted` | 资源搜索的本地模糊匹配 |

### 6.2 `ModAnimation.vb`（1066 行）

PCL 的动画不是 WPF Storyboard，而是一套自建 DSL：

```vb
AniStart({
    AaOpacity(Control, 1, 200, 100),      ' 目标值 1，时长 200ms，延迟 100ms
    AaCode(Sub() DoSomething(), 130)      ' 延时执行代码
}, "动画组名")                             ' 同名组会自动打断上一组
```

- 全局开关 `AniControlEnabled`：大于 0 时动画被抑制（批量设置控件属性时用，避免动画风暴）。
- `AniSpeed` 倍速控制，`AaEaseInFluent` 等缓动类实现贝塞尔曲线。

### 6.3 `ModValidate.vb`（424 行）

输入校验规则，以 `Collection(Of Validate)` 挂在 `MyTextBox.ValidateRules` 上：`ValidateFolderName`、`ValidateHttp`、`ValidateInteger`、`ValidateLength`、`ValidateRegex`、`ValidateSame`……

### 6.4 `PclLogger.vb`

`Logger.Info/Warn/Error(ex, msg, LogBehavior.Alert|Toast)`，日志写入 `PCL\Log1.txt`～`Log5.txt` 轮转，`LogBehavior.Alert` 会在出错时弹窗并打开日志。

---

## 七、网络层

`Modules/Base/ModNet.vb`（1933 行），全部 HTTP 与下载能力都在这里。

### 7.1 请求族

| 方法 | 用途 |
|---|---|
| `NetRequestByClient` | 单次请求（HttpClient） |
| `NetRequestByClientRetry` | 失败重试（业务层最常用） |
| `NetRequestByClientMultiple` | 多线程分片请求 |
| `NetRequestByLoader` | 绑定 `LoaderBase` 的请求，可同步进度、支持多 URL 回退 |
| `NetDownloadByClient` | 直接下载文件 |
| `NetDownloadByLoader` | 带进度、带 `FileChecker` 校验、支持多 URL 的下载 |

### 7.2 双源与自动回退

PCL 的下载源（Mojang 官方 / BMCLAPI 镜像）不是硬编码切换，而是由 `DlSourceLoader`（`ModDownload.vb`）在启动时**并发探测**：

```vb
DlSourceLoader(Loader, {
    {DlClientListBmclapiLoader, 30},        ' 30ms 后启动备用源
    {DlClientListMojangLoader, 30 + 60}     ' 更晚启动
})
```

先返回的源胜出，并记录 `DlPreferMojang` 决定后续所有请求走哪个源（`DlSourceModGet` / `DlSourceAssetsGet` / `DlSourceLibraryGet` / `DlSourceLauncherOrMetaGet` 各自有优先级规则）。

### 7.3 请求头注入

所有请求最终都会经过 `ModSecret.SecretHeadersSign(Url, Req, SimulateBrowserHeaders)`：

- UA：`PCL2/{版本}.{构建类型}`（模拟浏览器时追加 Chrome UA）
- Referer：`http://{VersionCode}.open.pcl2.server/`
- `api.curseforge.com` / `forgecdn.net`：追加 `x-api-key`（本分支的 CurseForge Key 由此注入）

---

## 八、业务模块层

### 8.1 `ModMinecraft.vb`（2153 行）— 实例模型

核心类型：

```vb
Class McFolder          ' 一个 .minecraft 目录
Class McInstance        ' 一个版本（versions/<name>）
Enum McInstanceState    ' 实例健康状态
Class McLibEntry        ' 依赖库条目
Structure McSkinInfo    ' 皮肤信息
```

主要职责：

- `InitMcInstanceList*`：扫描 `versions/` 构建实例列表（有缓存 + 无缓存两条路径）
- `McJsonRuleCheck`：解析 MC json 里的 `rules`（OS/arch 条件）
- `McLibListGet*` / `McAssetsGetIndex*`：根据版本 json 推导需要的库与资源索引
- `McSkin*`：皮肤地址解析与下载

### 8.2 `ModLaunch.vb`（2239 行）— 启动流水线

`McLaunchStart(Options)` 是总入口，内部按序调用（见成员表）：

```
McLaunchPrecheck        正版购买劝导 / 环境检查
  → McLogin*Start       登录（Ms 6 步 / Authlib / Nide / Legacy / Server）
  → McLaunchJava        挑选或下载 Java
  → McLaunchArgumentMain + ArgumentsJVM/Game   拼装完整命令行
  → McLaunchNatives     解压 natives
  → McLaunchPrerun      自定义前置命令
  → McLaunchCustom      自定义启动命令
  → McLaunchRun         启动进程
  → McLaunchWait        等待退出（配合 ModWatcher）
  → McLaunchEnd         收尾（日志、退出码）
```

微软登录的 6 个步骤（`MsLoginStep1New` 起）：

| 步骤 | 端点 | 说明 |
|---|---|---|
| 1 | `login.microsoftonline.com/consumers/oauth2/v2.0/devicecode` | 设备码 |
| 2 | `user.auth.xboxlive.com` | Xbox Live 认证 |
| 3 | `xsts.auth.xboxlive.com/xsts/authorize` | XSTS 授权 |
| 4 | `api.minecraftservices.com/authentication/login_with_xbox` | 换 MC 令牌（**白名单校验点**） |
| 5 | `api.minecraftservices.com/minecraft/profile` | 取 UUID/皮肤 |
| 6 | 本地写账号 | 保存刷新令牌 |

### 8.3 `ModDownload.vb`（1389 行）+ `ModDownloadLib.vb`（2176 行）

两者分工容易混淆：

| 模块 | 位置 | 职责 |
|---|---|---|
| `ModDownload` | `Modules/Minecraft/` | **元数据**：版本列表、Forge/Fabric/NeoForge/OptiFine/LiteLoader 列表、下载源优先级 |
| `ModDownloadLib` | `Pages/PageDownload/` | **执行**：真正下载并落地文件，`McInstallRequest` 描述一次安装任务 |

安装一个原版版本的典型链路：

```
PageDownloadInstall.LoaderInit
  → DlClientListLoader（版本列表）
  → 用户选择版本
  → McInstallRequest（目标版本 / 加载器 / 是否安装整合包）
  → LoaderCombo：
       下载 version json → 解析 libraries → 下载 libraries（多线程分片）
       → 下载 client.jar → 下载 assets 索引 → 下载 assets 对象
       → （可选）安装 Forge/Fabric/NeoForge 加载器
  → 写入 versions/<name>/，刷新实例列表
```

### 8.4 其他业务模块

| 模块 | 行数 | 关键能力 |
|---|---|---|
| `ModModpack` | 866 | 识别并安装 8 种整合包：CurseForge / Modrinth / HMCL / MMC / MCBBS / LauncherPack / 普通压缩包 |
| `ModJava` | 584 | 扫描注册表与常见路径、按 MC 版本匹配 Java、缺失时引导下载（`JavaDownloadInfo`） |
| `ModCrash` | 1122 | `CrashAnalyzer` 规则引擎：`AnalyzeCrit1/2/3`、`AnalyzeStackKeyword`、`AnalyzeModName`，输出中文诊断 |
| `ModWatcher` | 394 | 监控游戏进程：启动、退出、崩溃检测（`MinecraftState`） |
| `ModMusic` | 347 | BGM 列表、随机/顺序播放、音量与暂停 |
| `ResourceSearcher` | 530 | 中文→英文搜索词转换、CurseForge/Modrinth 分页与去重、`SearchRequest`/`SearchResult` |
| `ResourceProject` | 711 | 两平台项目的统一模型、`WikiEntry` 本地词条、图标异步加载 |
| `ResourceVersion` | 427 | 版本文件、下载地址解析（含 CurseForge 的 `ParseCurseForgeDownloadUrls`） |
| `LocalResourceFile` | 277 | 本地 Mod/资源文件模型，`CurseForgeHash` / `ModrinthHash` 指纹、`HasUpdate` |
| `LocalResourceLoaders` | 345 | 扫描本地文件并在线检查更新 |
| `ModSecret` | 474 | 凭据、主题配色、检查更新、（本分支扩展） |
| `ModEvent` | 446 | `EventType` 12 种：打开网页/文件/帮助、执行命令、启动游戏、复制文本、刷新主页/页面/帮助、今日人品、内存优化、清理垃圾、弹出窗口 |
| `ModMain` | 1083 | `Hint`、`MyMsgBox*`、帮助文档解压、遥测、反馈、GPU 偏好设置 |

---

## 九、UI 控件库

PCL 完全不用系统默认控件样式，所有控件自绘（`Controls/`，约 30 个类）。

| 控件 | 职责 | 关键属性/机制 |
|---|---|---|
| `MyButton` | 主按钮 | `ColorType`（Normal/Highlight）、`Logo`（图标路径）、按下/悬停动画 |
| `MyIconButton` | 纯图标按钮 | `Logo`、`LogoScale` |
| `MyIconTextButton` | 图标+文字按钮（顶部导航用） | |
| `MyTextButton` / `MyExtraButton` / `MyExtraTextButton` | 文本/附加按钮变体 | |
| `MyRadioButton` / `MyRadioBox` | 单选按钮（后者带文字块） | `Checked`、`Changed` 路由事件 |
| `MyCheckBox` | 复选框 | `Change` 事件、`SettingService` 绑定 |
| `MyCard` | 卡片容器 | `Title`、`CanSwap`/`IsSwapped`（可折叠）、`TriggerForceResize()` |
| `MyListItem` | 列表项（PCL 最常用的控件） | `Title`/`Info`/`Logo`/`Type="RadioBox"`/`Buttons` 数组 |
| `MyLoading` | 加载动画 | `State` 绑定 `LoaderBase`；`ILoadingTrigger` 接口 |
| `MyTextBox` | 输入框 | `ValidateRules`、`HintText`、`Validate()` |
| `MySlider` | 滑块 | `MaxValue`、`ValueByKey`（步长）、`GetHintText` |
| `MyComboBox` / `MyComboBoxItem` | 下拉框 | `Tag` 携带枚举值 |
| `MyScrollViewer` / `MyScrollBar` | 滚动容器/滚动条 | 自绘、平滑滚动 |
| `MyPageLeft` / `MyPageRight` | 左右分栏页基类 | `PageOnEnter/Exit/ForceExit` |
| `MyImage` | 网络/本地图片 | 异步加载、占位图 |
| `MyDropShadow` | 窗口阴影 | `Placement` 枚举 |
| `MyResizer` | 窗体缩放 | Win32 消息钩子 |
| `MyVirtualizingElement` | 虚拟化容器 | 长列表只创建可视区元素 |
| `MyMenuItem` | 菜单项 | |
| `MySearchBox` | 搜索框 | |
| `MyHint` | 提示条 | `Themes` 枚举（蓝/绿/红） |
| `MyMsg/MyMsgText·Input·Select` | 弹窗内容 | 配合 `MyMsgBox*` 使用 |
| `Behaviors/LazyLoadBehavior` | 懒加载 | 滚动到可视区才渲染 |
| `Behaviors/ClipboardInterceptor` | 剪贴板拦截 | 防止游戏内复制被截获 |
| `Behaviors/Interfaces` | `IRefreshable`、`IMyRadio` | 控件契约 |

---

## 十、设置系统与事件系统

### 10.1 设置（`Settings.vb` + `SettingService.vb`）

```vb
Class Settings
  Shared ReadOnly Entries As Dictionary(Of String, Setting)
  ' 声明式注册：键、默认值、存储位置、是否加密、变更回调
  New Setting("UiLauncherTheme", 0, OnChanged:=AddressOf ThemeRefresh)
  New Setting("SystemSystemCache", "", Source:=Sources.Registry)
  New Setting("VersionAdvanceJvm", "", Source:=Sources.Instance)
End Class

Enum Sources
  Normal      ' PCL\Setup.ini
  Registry    ' HKEY_CURRENT_USER\Software\PCLDebug（本分支为 OpenSource 隔离）
  Instance    ' 每个实例的 PCL\Setup.ini
End Enum
```

UI 侧通过**附加属性**双向绑定，无需写事件处理：

```xaml
<local:MyCheckBox local:SettingService.Key="UiMusicAuto" Text="打开启动器自动开始播放" />
<local:MyTextBox  local:SettingService.Key="SystemSystemCache" HintText="默认" />
```

`SettingService.RefreshSettings(Me)` / `ResetSettings(Me)` 负责把值刷进控件、或把控件重置为默认。

### 10.2 事件（`ModEvent.vb`）

```xaml
<local:MyButton Text="赞助 PCL !"
    local:CustomEventService.EventType="打开网页"
    local:CustomEventService.EventData="https://meloong.com/afd/a/LTCat" />
```

`EventType` 共 12 种（打开网页/文件/帮助、执行命令、启动游戏、复制文本、刷新主页/页面/帮助、今日人品、内存优化、清理垃圾、弹出窗口）。
`EventSafetyConfirm` 对危险动作（执行命令）做二次确认；`GetAbsoluteUrls` 处理相对路径与占位符替换（如 `{pcl_branch}`）。

---

## 十一、关键调用链

### 11.1 启动游戏

```
用户点「启动游戏」BtnLaunch
  → PageLaunchLeft 收集选项（账号、版本、内存…）
  → McLaunchStart(McLaunchOptions)
      ├ McLaunchPrecheck()              正版劝导 / 目录检查
      ├ McLoginStart(...)               未登录则先登录
      ├ McLaunchJava()                  选 Java（版本不匹配则提示/下载）
      ├ McLaunchArgumentMain()          -cp、natives、主类、游戏参数
      ├ McLaunchNatives()               解压 natives 到临时目录
      ├ McLaunchRun()                   StartProcess(javaw)
      └ McLaunchWait()                  → ModWatcher 监控退出
```

### 11.2 安装游戏版本

```
PageDownloadInstall.Init
  → DlClientListLoader（双源并发取版本列表）
  → 选择版本 → PrepareSelectPage()
  → 选择加载器（Forge/Fabric/…，各自有 ListLoader）
  → McInstallRequest → ModDownloadLib 执行下载
  → 回调 LoadMinecraft_OnFinish → 刷新实例列表
```

### 11.3 搜索社区资源

```
PageResource 搜索框
  → ResourceSearcher.SearchRequest（Type/Tag/ModLoaders/GameVersion/Sources）
  → ResourceSearcher.Run
      ├ 中文关键词 → WikiEntry 词条 → 翻译成英文
      ├ GetCurseForgeAddress() → GET api.curseforge.com/v1/mods/search
      └ GetModrinthAddress()   → GET api.modrinth.com/v2/search
  → ResourceProject.ToResourceItem → MyResourceItem 列表项
```

### 11.4 检查更新（本分支改造）

```
BtnAboutUpdate / BtnSystemUpdate
  → ModSecret.UpdateCheckByButton()
      → GetLatestRelease()             GET api.github.com/repos/{UpdateRepo}/releases/latest
      → 比较 tag_name 与 VersionBaseName
      → GetReleaseExeUrl()             取 .exe 附件
      → NetDownloadByClient()          下载到 PCL\Plain Craft Launcher 2.exe
      → IsUpdateWaitingRestart = True
  → FrmMain.EndProgram(False)
      → EndProgramForce → UpdateRestart(False)
          → StartProcess(新 exe, "--update <pid> ""<旧>"" ""<新>"" True")
  → Application 启动时识别 --update
      → UpdateReplace(pid, old, new, restart)
          → 等旧进程退出 → 覆盖 → 重启
```

### 11.5 主题刷新（本分支补回预设表）

```
Settings.Set("UiLauncherTheme", id)
  → Setting.OnChanged → ThemeRefresh(id)
      ├ 读取 UiLauncherHue/Sat/Delta/Light（自定义滑块）
      ├ Select Case id：套用 15 套预设 HSL
      ├ 计算 Color1..Color8 / ColorBg0/1（MyColor.FromHSL2）
      └ 写入 Application.Current.Resources("ColorBrush1"…)
  → ThemeRefreshMain()
      → 在主线程刷新顶部渐变条与主页背景
```

### 11.6 凭据加载（本分支新增）

```
FrmMain 第三阶段 → RefreshSecret()
  → 环境变量 PCL_MS_CLIENT_ID / PCL_CURSEFORGE_API_KEY
  → 其次 PCL\Secret.ini
  → 再次 ModSecretBuild.Build*（编译期内置，#If HAS_LOCAL_SECRET）
  → 最后内置默认值
```

---

## 十二、本分支改动索引

> 与上游 `2.99.0.0` 的差异，全部可在 git 历史中逐条回滚。

| 文件 | 改动 |
|---|---|
| `Modules/ModSecret.vb` | ①`RestrictMcDownload = False` 恢复下载；②新增「本地凭据」区（`SecretFile`/`RefreshSecret`/`SaveSecret`）；③`ThemeRefresh` 补回主题→HSL 预设表；④`ThemeCheckAll/ThemeCheckOne/ThemeUnlock` 解除赞助校验；⑤「更新」区整体实现（`GetLatestRelease`/`UpdateCheckByButton`/`UpdateRestart`/`UpdateReplace`/`DownloadLatestPCL`） |
| `Modules/ModSecretBuild.vb` | **新增**，编译期内置凭据；已 gitignore，仓库只留 `.example` |
| `Modules/Base/ModBase.vb` | `VersionBaseName` → `2.99.1.0` |
| `Modules/Minecraft/ModLaunch.vb` | ①`MsLoginStep1New` 增加空 Client ID 守卫；②`MsLoginStep4` 的 403 区分「未获白名单授权」并给出申请入口 |
| `Modules/Minecraft/ModCrash.vb` | 内存不足建议中移除「更多 → 百宝箱」指引 |
| `Modules/Resource/ResourceSearcher.vb` | `Sources` 默认值按是否配置 CF Key 决定 |
| `Pages/PageDownload/Resource/PageResource.xaml.vb` | 无 Key 时剔除 CurseForge 源并提示一次 |
| `FormMain.xaml(.vb)` | ①窗口标题改名；②移除启动时的「开源版本说明」弹窗，改为 `RefreshSecret()` |
| `Application.xaml.vb` | 唤醒已有窗口的 `FindWindow` 标题同步新名称（保留旧标题回退） |
| `Pages/PageSetup/PageSetupUI.xaml(.vb)` | ①新增 `ThemeUnlockAll()` 并接入 `ThemeCheckAll`；②移除 Release 下的赞助遮罩；③移除「隐藏百宝箱」复选框与相关计数/互斥逻辑 |
| `Pages/PageSetup/PageSetupSystem.xaml(.vb)` | 凭据卡片先加后删（改为编译期内置后移除界面） |
| `Pages/PageOther/PageOtherLeft.xaml(.vb)` | 移除「百宝箱」入口及路由分支 |
| `Pages/PageOther/PageOtherAbout.xaml` | 新增 thomas455 署名行（含头像）、GitHub 主页与介绍页按钮、版权文字追加第三方声明 |
| `My Project/AssemblyInfo.vb` | 标题/产品名/版权/版本（2.99.1.0） |
| `Plain Craft Launcher 2.vbproj` | `ModSecretBuild.vb` 条件编译 + `HAS_LOCAL_SECRET` 常量；注册头像资源 |
| `.gitignore` | 排除 `Secret.ini`、`ModSecretBuild.vb` 等本地凭据文件 |

---

## 十三、构建与排错

### 13.1 前置条件

```bash
git clone --recurse-submodules https://github.com/Thomas455/PCL-Thomas.git
dotnet restore PCLCS/PCLCS.csproj        # 还原 PolySharp 等包
msbuild "Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj" -p:Configuration=Release
```

- 必须有 **MeloongCore 子模块**，否则 `MSB3202` / `NU1104`
- 必须有 **.NET Framework 4.8 目标包** + **Visual Studio 的 MSBuild**
- 产物在 `Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe`

### 13.2 常见错误对照

| 现象 | 原因 | 处理 |
|---|---|---|
| `MSB3202 / NU1104` 找不到 `MeloongCore\*.csproj` | 子模块为空 | `git submodule update --init --recursive` |
| 500+ 个 `BC30506`（Handles 子句要求 WithEvents） | 用 `dotnet build` 编译旧式 VB WPF 工程 | 改用 VS 的 `msbuild.exe` |
| `MSB3021 / MSB3027` 文件被占用 | 启动器正在运行，锁住 `bin\*.dll` | 先关闭进程再编译 |
| `BC32017: 应为逗号、")"…` | VB 源码里出现中文弯引号 `“ ”`（GBK 解析会吞掉引号） | 改成 `""` 或 `vbLQ/vbRQ` |
| 运行时报 `FileNotFoundException` 找不到 `PCLCS`/`MeloongCore` | `AssemblyResolve` 的 `Prefixes` 列表被改坏，或对应 `LogicalName` 资源缺失 | 检查 vbproj 的 `<None Include=... LogicalName=...>` |
| 改了 XAML 但界面没变 | 增量构建未重新生成 BAML | `-t:Rebuild` |

### 13.3 调试技巧

- 日志：`bin\PCL\Log1.txt`（`LogBehavior.Alert` 会直接弹窗并打开）
- 设置 `SystemDebugMode = True` 可打开调试页、显示更多日志等级
- `AniControlEnabled += 1` 可临时关闭动画观察布局
- 页面切换均有日志：`切换主要页面：Launch, -1`

---

## 十四、阅读路线建议

如果是第一次读这套代码，推荐顺序（每步都能独立跑起来验证）：

1. **`ModBase.vb`** —— 掌握 `RunInUi`/`ReadIni`/`StartProcess` 这些"万能工具"
2. **`ModLoader.vb`** —— 理解加载器状态机，这是全库的骨架
3. **`FormMain.xaml.vb` 的三阶段初始化** —— 知道程序启动时发生了什么
4. **`PageLaunchLeft/Right` + `ModLaunch.McLaunchStart`** —— 最核心的业务链路
5. **`ModNet.vb` 的 `NetDownloadByLoader`** —— 所有下载都经过它
6. **`ModMinecraft` 的实例模型** —— 理解 `McInstance` 后，实例管理、整合包、导出都不难看懂
7. **`ModDownloadLib`** —— 安装引擎，最复杂但也最有参考价值（分片下载 + 进度合并）
8. **控件库** —— 需要改 UI 时再看，`MyListItem` 与 `MyLoading` 优先

---

## 附录

### A. 源文件规模 Top 20

| 行数 | 文件 |
|---:|---|
| 2239 | `Modules/Minecraft/ModLaunch.vb` |
| 2176 | `Pages/PageDownload/ModDownloadLib.vb` |
| 2153 | `Modules/Minecraft/ModMinecraft.vb` |
| 1933 | `Modules/Base/ModNet.vb` |
| 1725 | `FormMain.xaml.vb` |
| 1389 | `Modules/Minecraft/ModDownload.vb` |
| 1216 | `Modules/Base/ModBase.vb` |
| 1199 | `Pages/PageDownload/PageDownloadInstall.xaml.vb` |
| 1122 | `Modules/Minecraft/ModCrash.vb` |
| 1116 | `Pages/PageLink/PageLinkMain.xaml.vb` |
| 1083 | `Modules/ModMain.vb` |
| 1066 | `Modules/Base/ModAnimation.vb` |
| 866 | `Modules/Minecraft/ModModpack.vb` |
| 839 | `Pages/PageInstance/PageInstanceMod.xaml.vb` |
| 837 | `Pages/PageLaunch/PageLaunchLeft.xaml.vb` |
| 791 | `Modules/Base/ModLoader.vb` |
| 784 | `Pages/PageInstance/PageInstanceExport.xaml.vb` |
| 727 | `Pages/PageDownload/PageDownloadModify.vb` |
| 711 | `Modules/Resource/ResourceProject.vb` |
| 682 | `Pages/PageOther/PageOtherAbout.xaml` |

### B. 枚举速查

| 枚举 | 位置 | 取值 |
|---|---|---|
| `BuildTypes` | `ModBase.vb` | `Snapshot=0 / Release=50 / Debug=100`（决定 `VersionCode` 与显示名） |
| `LoadState` | `ModBase.vb` | `Waiting / Loading / Finished / Failed / Canceled` |
| `ProcessReturnValues` | `ModBase.vb` | `Success=0 / Fail=1 / Exception=2 / Timeout=3 / Cancel=4 / TaskDone=5` |
| `PageType` | `FormMain.xaml.vb` | `Launch / Download / Link / Setup / Other` |
| `PageSubType` | `FormMain.xaml.vb` | 见 4.2 节 |
| `Sources` | `Settings.vb` | `Normal / Registry / Instance` |
| `EventType` | `ModEvent.vb` | 12 种自定义事件 |
| `HintType` | `ModMain.vb` | `Blue / Green / Red` |
| `McLoginType` | `ModLaunch.vb` | `Legacy=0 / Nide=2 / Auth=3 / Ms=5`（服务器登录另由 `McLoginServer` 处理） |
| `ResourcePlatforms` | `PCLCS/Constants.cs` | `CurseForge=1 / Modrinth=2 / Any=3`（`[Flags]`，**无 None**） |
| `McInstanceState` | `ModMinecraft.vb` | 实例健康状态 |
| `MinecraftState` | `ModWatcher.vb` | 游戏进程状态 |

### C. 命名约定

- `Mod*`：无 UI 的逻辑模块（`Module` 或 `Class`）
- `Page*`：页面类，一个页面 = `.xaml` + `.xaml.vb`
- `My*`：自绘控件或工具类
- `Dl*` / `Mc*`：下载与 Minecraft 领域前缀（`DlClientListLoader`、`McInstance`）
- `Frm*`：页面实例的共享字段（`FrmSetupUI`、`FrmOtherLeft`）
- 中文注释为主，关键类有 `''' <summary>` XML 文档注释

---

*本文档由代码扫描 + 人工校对生成，若与代码不符以代码为准。*