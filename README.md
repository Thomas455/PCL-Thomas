<!--
  README 由 thomas455 与 DeepSeek Harness（deepseek-v4.1-flash-1017）协作编写。
  修改时间：2026-10-21
-->

[![](https://img.shields.io/badge/powered_by-dsh-4D6BFE?style=flat-square&logo=deepseek&logoColor=white)](https://github.com/deepseek-ai/deepseek-harness)

<p align="center">
  <sub><i>✦ 本分支由 <b>thomas455</b> 维护 · 修改过程由 <b>DeepSeek Harness</b> 协作完成 · 2026-10-21 ✦</i></sub>
</p>

<img src="docs/avatar.jpg" width="110" align="right" alt="thomas455" />

# Plain Craft Launcher · Thomas 修改版

> **第三方修改版声明**：本仓库是**第三方基于 PCL 独立进行二次创作的产物**，与 PCL 官方（龙腾猫跃 / 成都瓜皮龙科技有限公司）无关，不代表官方立场，也未经官方背书。
> 上游项目：[Meloong-Git/PCL](https://github.com/Melong-Git/PCL)

**修改者**：thomas455 · [主页](https://thomas455.github.io) · [GitHub](https://github.com/Thomas455)

<br/>

## 这个仓库是什么

PCL（Plain Craft Launcher）是一款 Minecraft: Java Edition 启动器。本仓库是它的一个**个人维护的修改分支**，目的很简单：让开源发布的这份源码能正常编译，并把几个被移除的功能恢复回来，供自己日常使用。

本分支基于上游 `2.99.0.0` 版本的源码（除换行符外与上游逐字节一致），所有改动都记录在 git 提交历史里，可逐条查看与回滚。

## 修改内容

### 1. 修复编译失败

上游仓库的 `MeloongCore` 子模块在 zip 下载后是空目录，直接编译会报：

```
MSB3202: 未找到项目文件 MeloongCore\Shared\MeloongCore.csproj
NU1104: 找不到项目 MeloongCore\Wpf\MeloongCore.Wpf.csproj
```

补全子模块后还需注意：**必须用 Visual Studio 自带的 MSBuild**，用 `dotnet build` 会得到 500+ 个 `BC30506` 错误（旧式 VB.NET + WPF 工程不支持 SDK 版 MSBuild）。

### 2. 恢复 Minecraft 游戏下载

上游用 `RestrictMcDownload = True` 下线了下载功能，但下载链路本身完整保留。本分支将其置为 `False`，恢复：

- 原版 / Forge / Fabric / NeoForge / OptiFine / LiteLoader 安装
- 整合包安装（拖入 zip / mrpack / rar）
- 实例修改中的 MC 版本切换
- 整合包详情页的「安装」按钮

下载源未做任何改动，仍为 BMCLAPI 与 Mojang 官方双源。

### 3. 恢复主题个性化

上游删除了「主题 ID → 配色」的映射表，并用赞助校验锁死了绝大部分主题。本分支：

- 补回 15 个主题的配色预设（HSL）
- 解除主题的赞助 / 成就解锁限制，隐藏灰色占位与赞助遮罩
- 自定义主题的四个滑块（色调 / 饱和度 / 亮度 / 色调渐变）重新接回渲染

### 4. 本地凭据文件（新增）

新增 `PCL\Secret.ini`，用于存放自己的密钥，**只存本机、不进版本库**：

```ini
MsClientId:你的微软应用 Client ID
CurseForgeAPIKey:你的 CurseForge API Key
UpdateRepo:Thomas455/PCL-Thomas
```

- 优先级：环境变量（`PCL_MS_CLIENT_ID` / `PCL_CURSEFORGE_API_KEY`）> `Secret.ini` > 内置默认值
- 已加入 `.gitignore`，仓库里只提交模板 [`Secret.ini.example`](Secret.ini.example)
- 设置页新增「凭据与更新源」卡片，可直接填写并保存

### 5. CurseForge 优雅降级

未配置 API Key 时，资源搜索自动只用 Modrinth 源并提示一次，不再撞 403；配置后 CurseForge 搜索 / 下载 / 整合包全部可用。

### 6. 检查更新改为自建仓库

上游的更新走官方签名 CDN。本分支改为查询自己 GitHub 仓库的 Release：

- 以 Release 的 `tag_name` 作为版本号，与程序内 `VersionBaseName` 比较
- 有 exe 附件时：下载 → 通过 `--update` 协议等待旧进程退出 → 覆盖 → 重启
- 无附件时：跳转 Release 页面
- 整合包导出用的「下载最新版 PCL」也改用该仓库

### 7. 其他

- 移除每次启动弹出的「开源版本说明」对话框
- 正版登录的 403 错误改为准确提示（区分「未获 Minecraft 白名单授权」与网络问题）

## 正版登录的已知限制

自建的 Azure 应用可以走通 **OAuth → Xbox Live → XSTS**，但最后一步 `api.minecraftservices.com/authentication/login_with_xbox` 会返回：

```
403 Invalid app registration
```

原因是 **Mojang 对可调用 Minecraft 登录接口的 Client ID 设有白名单，新注册的 Entra 应用默认不在名单内**，这不是配置问题（改 API 权限、重定向 URI、公开客户端流都无效）。

要使用正版登录只能向 Mojang 提交白名单申请：<https://help.minecraft.net/hc/en-us/articles/16254801392141>

在获批之前，离线登录可以正常游玩，但无法加入开启正版验证（`online-mode=true`）的服务器。

## 已知不可用功能

| 功能 | 原因 |
|---|---|
| 百宝箱 | 上游已删除整个页面源码 |
| 联机（EasyTier） | 依赖官方服务器下发的签名配置 |
| 启动器公告 / 联网通知 | 同上，官方自建服务 |
| 正版登录 | 见上节，需 Mojang 白名单 |

## 如何构建

```bash
# 1. 克隆（记得带上子模块）
git clone --recurse-submodules https://github.com/Thomas455/PCL-Thomas.git

# 2. 还原 NuGet 包
dotnet restore PCLCS/PCLCS.csproj

# 3. 用 Visual Studio 的 MSBuild 编译（不要用 dotnet build）
msbuild "Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj" -p:Configuration=Release
```

**环境要求**：Visual Studio（含 .NET 桌面开发 / VB 工作负载）、.NET Framework 4.8 目标包。

产物：`Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe`

发新版时记得同步改 `Plain Craft Launcher 2\Modules\Base\ModBase.vb` 里的 `VersionBaseName`，并让 Release 的 tag 与之对应，否则会一直提示更新。

## 致谢与署名

- **PCL 原作者：龙腾猫跃**（[GitHub](https://github.com/LTCat) / [赞助](https://meloong.com/afd/a/LTCat)）—— 本项目的全部基础工作都属于他
- 下载镜像源：[bangbang93](https://github.com/bangbang93)（BMCLAPI）
- 第三方组件版权见程序「关于」页面中的《许可与版权声明》

## 许可

本项目沿用上游的 [LICENCE](LICENCE)（《PCL 分发有限许可》与《PCL 存储库合理使用指南》），请在使用、修改、分发前阅读原文。

## 免责声明

**非 MINECRAFT 官方产品。未经 MOJANG 或 MICROSOFT 批准，也不与 MOJANG 或 MICROSOFT 关联。**