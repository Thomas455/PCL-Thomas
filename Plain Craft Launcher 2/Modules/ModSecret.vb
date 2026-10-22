'由于包含加解密等安全信息，本文件中的部分代码已被删除

Friend Module ModSecret

    '标注 PCL 的不同分支，仅用于替换标记
    Public Const VersionBranchMain As String = "OpenSource"
    '在开源版的注册表与常规版的注册表隔离，以防数据冲突
    Public Const RegFolder As String = "PCLDebug"
    '用于微软登录的 ClientId（由 RefreshSecret 从环境变量或本地凭据文件读取）
    Public OAuthClientId As String = ""
    'CurseForge API Key（由 RefreshSecret 从环境变量或本地凭据文件读取）
    Public CurseForgeAPIKey As String = ""
    '用于匿名数据收集的腾讯云日志服务上报 URL，形如 https://{region}.cls.tencentcs.com/track?topic_id={topic_id}
    Public Const ClsBaseUrl As String = ""

#Region "本地凭据"

    ''' <summary>
    ''' 本地凭据文件路径：PCL\Secret.ini。
    ''' 这里只存放使用者自己的密钥，仅存在于本机，已在 .gitignore 中排除，不要提交到版本库。
    ''' 仓库中提供 Secret.ini.example 作为模板。
    ''' </summary>
    Friend ReadOnly Property SecretFile As String
        Get
            Return Paths.Base & "PCL\Secret.ini"
        End Get
    End Property

    ''' <summary>
    ''' 检查更新所用的 GitHub 仓库，格式为 “用户名/仓库名”。
    ''' </summary>
    Public UpdateRepo As String = "Meloong-Git/PCL"

    ''' <summary>
    ''' 从环境变量与本地凭据文件重新加载全部密钥与配置。
    ''' 优先级：环境变量 &gt; PCL\Secret.ini &gt; 编译期内置值（ModSecretBuild.vb）&gt; 内置默认值。
    ''' </summary>
    Friend Sub RefreshSecret()
        Try
            Dim MsId As String = Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID")
            If String.IsNullOrWhiteSpace(MsId) Then MsId = ReadIni(SecretFile, "MsClientId", "")
#If HAS_LOCAL_SECRET Then
            If String.IsNullOrWhiteSpace(MsId) Then MsId = ModSecretBuild.BuildMsClientId
#End If
            OAuthClientId = NormalizeSecret(MsId)

            Dim CfKey As String = Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY")
            If String.IsNullOrWhiteSpace(CfKey) Then CfKey = ReadIni(SecretFile, "CurseForgeAPIKey", "")
#If HAS_LOCAL_SECRET Then
            If String.IsNullOrWhiteSpace(CfKey) Then CfKey = ModSecretBuild.BuildCurseForgeKey
#End If
            CurseForgeAPIKey = NormalizeSecret(CfKey)

            Dim Repo As String = NormalizeSecret(ReadIni(SecretFile, "UpdateRepo", ""))
#If HAS_LOCAL_SECRET Then
            If Repo = "" Then Repo = NormalizeSecret(ModSecretBuild.BuildUpdateRepo)
#End If
            UpdateRepo = If(Repo = "", "Meloong-Git/PCL", Repo)
        Catch ex As Exception
            Logger.Warn(ex, "读取本地凭据失败")
        End Try
    End Sub

    ''' <summary>
    ''' 把凭据写入本地凭据文件（不写注册表，不进版本库），并立即生效。
    ''' </summary>
    Friend Sub SaveSecret(MsClientId As String, CurseForgeKey As String, Repo As String)
        Try
            Dim Folder As String = Paths.Base & "PCL\"
            If Not IO.Directory.Exists(Folder) Then IO.Directory.CreateDirectory(Folder)
            WriteIni(SecretFile, "MsClientId", If(MsClientId, "").Trim())
            WriteIni(SecretFile, "CurseForgeAPIKey", If(CurseForgeKey, "").Trim())
            WriteIni(SecretFile, "UpdateRepo", If(Repo, "").Trim())
        Finally
            RefreshSecret()
        End Try
    End Sub

    ''' <summary>
    ''' 去掉复制粘贴时容易带上的空格、引号与换行。
    ''' </summary>
    Private Function NormalizeSecret(Value As String) As String
        Return If(Value, "").Replace(vbCr, "").Replace(vbLf, "").Trim().Trim(""""c).Trim("'"c).Trim()
    End Function

#End Region

#Region "网络鉴权"

    '是否禁用 MC 下载功能（同时影响：下载页安装游戏、整合包安装、实例修改中的 MC 版本切换、
    'ModPack 详情页的安装按钮、拖入整合包时的处理）。
    '本分支解除该限制，恢复正常下载链路（下载源仍为 BMCLAPI / Mojang 官方，未做任何改动）。
    Public Const RestrictMcDownload As Boolean = False
    Friend Function SecretCdnSign(UrlWithMark As String) As String
        If Not UrlWithMark.EndsWithF("{CDN}") Then Return UrlWithMark
        Return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20")
    End Function
    ''' <summary>
    ''' 设置 Headers 的 UA、Referer。
    ''' </summary>
    Friend Sub SecretHeadersSign(Url As String, ByRef Req As HttpRequestMessage, Optional SimulateBrowserHeaders As Boolean = False)
        If Not Req.Headers.UserAgent.Any Then
            If Url.Contains("baidupcs.com") OrElse Url.Contains("baidu.com") Then
                Req.Headers.Add("User-Agent", "LogStatistic")  '#4951
            ElseIf SimulateBrowserHeaders Then
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)} Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36")
            Else
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)}")
            End If
        End If
        If Not SimulateBrowserHeaders Then Req.Headers.Add("Referer", $"http://{VersionCode}.open.pcl2.server/")
        If Url.Contains("api.curseforge.com") OrElse Url.Contains("forgecdn.net") Then Req.Headers.Add("x-api-key", CurseForgeAPIKey)
    End Sub

#End Region

#Region "主题"

    Public Color1 As New MyColor(52, 61, 74)
    Public Color2 As New MyColor(11, 91, 203)
    Public Color3 As New MyColor(19, 112, 243)
    Public Color4 As New MyColor(72, 144, 245)
    Public Color5 As New MyColor(150, 192, 249)
    Public Color6 As New MyColor(213, 230, 253)
    Public Color7 As New MyColor(222, 236, 253)
    Public Color8 As New MyColor(234, 242, 254)
    Public ColorBg0 As New MyColor(150, 192, 249)
    Public ColorBg1 As New MyColor(190, Color7)
    Public ColorGray1 As New MyColor(64, 64, 64)
    Public ColorGray2 As New MyColor(115, 115, 115)
    Public ColorGray3 As New MyColor(140, 140, 140)
    Public ColorGray4 As New MyColor(166, 166, 166)
    Public ColorGray5 As New MyColor(204, 204, 204)
    Public ColorGray6 As New MyColor(235, 235, 235)
    Public ColorGray7 As New MyColor(240, 240, 240)
    Public ColorGray8 As New MyColor(245, 245, 245)
    Public ColorSemiTransparent As New MyColor(1, Color8)

    Public ThemeNow As Integer = -1
    Public ColorHue As Integer = 210, ColorSat As Integer = 85, ColorLightAdjust As Integer = 0, ColorHueTopbarDelta As OneOf(Of Integer, Integer()) = 0
    Public ThemeDontClick As Integer = 0

    Public Sub ThemeRefresh(Optional NewTheme As Integer = -1)
        Try
            If ThemeNow = NewTheme AndAlso NewTheme >= 0 Then Return
            If NewTheme >= 0 Then ThemeNow = NewTheme

            '先取自定义主题的滑块值，再按主题 ID 套用预设配色
            ColorHue = Settings.Get(Of Integer)("UiLauncherHue")
            ColorSat = Settings.Get(Of Integer)("UiLauncherSat")
            ColorHueTopbarDelta = Settings.Get(Of Integer)("UiLauncherDelta")
            ColorLightAdjust = Settings.Get(Of Integer)("UiLauncherLight")
            Select Case If(ThemeNow >= 0, ThemeNow, Settings.Get(Of Integer)("UiLauncherTheme"))
                Case 0 '龙猫蓝
                    ColorHue = 210 : ColorSat = 85 : ColorHueTopbarDelta = 0 : ColorLightAdjust = 0
                Case 1 '甜柠青
                    ColorHue = 174 : ColorSat = 72 : ColorHueTopbarDelta = 0 : ColorLightAdjust = 0
                Case 2 '小草绿
                    ColorHue = 110 : ColorSat = 62 : ColorHueTopbarDelta = 0 : ColorLightAdjust = 0
                Case 3 '菠萝黄
                    ColorHue = 45 : ColorSat = 85 : ColorHueTopbarDelta = 0 : ColorLightAdjust = 0
                Case 4 '橡木棕
                    ColorHue = 25 : ColorSat = 45 : ColorHueTopbarDelta = 0 : ColorLightAdjust = 0
                Case 5 '玄素黑
                    ColorHue = 210 : ColorSat = 0 : ColorHueTopbarDelta = 0 : ColorLightAdjust = -12
                Case 6 '铁杆粉
                    ColorHue = 330 : ColorSat = 70 : ColorHueTopbarDelta = 0 : ColorLightAdjust = 0
                Case 7 '神秘紫
                    ColorHue = 265 : ColorSat = 60 : ColorHueTopbarDelta = 0 : ColorLightAdjust = 0
                Case 8 '秋仪金
                    ColorHue = 35 : ColorSat = 78 : ColorHueTopbarDelta = 0 : ColorLightAdjust = 0
                Case 9 '活跃橙
                    ColorHue = 20 : ColorSat = 85 : ColorHueTopbarDelta = 0 : ColorLightAdjust = 0
                Case 10 '跳票红
                    ColorHue = 355 : ColorSat = 75 : ColorHueTopbarDelta = 0 : ColorLightAdjust = 0
                Case 11 '极客蓝
                    ColorHue = 195 : ColorSat = 92 : ColorHueTopbarDelta = 0 : ColorLightAdjust = 0
                Case 12 '滑稽彩
                    ColorHue = 50 : ColorSat = 100 : ColorHueTopbarDelta = 120 : ColorLightAdjust = 0
                Case 13 '欧皇彩
                    ColorHue = 300 : ColorSat = 95 : ColorHueTopbarDelta = 90 : ColorLightAdjust = 0
                Case 14 '自定义：沿用上面读取的滑块值
            End Select

            Color1 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.2, 25 + ColorLightAdjust * 0.3)
            Color2 = New MyColor().FromHSL2(ColorHue, ColorSat, 45 + ColorLightAdjust)
            Color3 = New MyColor().FromHSL2(ColorHue, ColorSat, 55 + ColorLightAdjust)
            Color4 = New MyColor().FromHSL2(ColorHue, ColorSat, 65 + ColorLightAdjust)
            Color5 = New MyColor().FromHSL2(ColorHue, ColorSat, 80 + ColorLightAdjust * 0.4)
            Color6 = New MyColor().FromHSL2(ColorHue, ColorSat, 91 + ColorLightAdjust * 0.1)
            Color7 = New MyColor().FromHSL2(ColorHue, ColorSat, 95)
            Color8 = New MyColor().FromHSL2(ColorHue, ColorSat, 97)
            ColorBg0 = Color4 * 0.4 + Color5 * 0.4 + ColorGray4 * 0.2
            ColorBg1 = New MyColor(190, Color7)

            ColorSemiTransparent = New MyColor(1, Color8)
            Application.Current.Resources("ColorBrush1") = New SolidColorBrush(Color1)
            Application.Current.Resources("ColorBrush2") = New SolidColorBrush(Color2)
            Application.Current.Resources("ColorBrush3") = New SolidColorBrush(Color3)
            Application.Current.Resources("ColorBrush4") = New SolidColorBrush(Color4)
            Application.Current.Resources("ColorBrush5") = New SolidColorBrush(Color5)
            Application.Current.Resources("ColorBrush6") = New SolidColorBrush(Color6)
            Application.Current.Resources("ColorBrush7") = New SolidColorBrush(Color7)
            Application.Current.Resources("ColorBrush8") = New SolidColorBrush(Color8)
            Application.Current.Resources("ColorBrushBg0") = New SolidColorBrush(ColorBg0)
            Application.Current.Resources("ColorBrushBg1") = New SolidColorBrush(ColorBg1)
            Application.Current.Resources("ColorObject1") = CType(Color1, Color)
            Application.Current.Resources("ColorObject2") = CType(Color2, Color)
            Application.Current.Resources("ColorObject3") = CType(Color3, Color)
            Application.Current.Resources("ColorObject4") = CType(Color4, Color)
            Application.Current.Resources("ColorObject5") = CType(Color5, Color)
            Application.Current.Resources("ColorObject6") = CType(Color6, Color)
            Application.Current.Resources("ColorObject7") = CType(Color7, Color)
            Application.Current.Resources("ColorObject8") = CType(Color8, Color)
            Application.Current.Resources("ColorObjectBg0") = CType(ColorBg0, Color)
            Application.Current.Resources("ColorObjectBg1") = CType(ColorBg1, Color)
            ThemeRefreshMain()
        Catch ex As Exception
            Logger.Error(ex, "刷新主题颜色失败", LogBehavior.Toast)
        End Try
    End Sub
    Public Sub ThemeRefreshMain()
        RunInUi(
        Sub()
            If Not FrmMain.IsLoaded Then Return
            '顶部条背景
            Dim Brush = New LinearGradientBrush With {.EndPoint = New Point(1, 0), .StartPoint = New Point(0, 0)}
            Dim Deltas = ColorHueTopbarDelta.Switch(Function(d) New Integer() {-d, 0, d}, Function(d) d)
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue + Deltas(0), ColorSat, 48 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue + Deltas(1), ColorSat, 54 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue + Deltas(2), ColorSat, 48 + ColorLightAdjust)})
            FrmMain.PanTitle.Background = Brush
            FrmMain.PanTitle.Background.Freeze()
            '主页面背景
            If Settings.Get(Of Boolean)("UiBackgroundColorful") Then
                Brush = New LinearGradientBrush With {.EndPoint = New Point(0.1, 1), .StartPoint = New Point(0.9, 0)}
                Brush.GradientStops.Add(New GradientStop With {.Offset = -0.1, .Color = New MyColor().FromHSL2(ColorHue - 15, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.4, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1.1, .Color = New MyColor().FromHSL2(ColorHue + 15, ColorSat * 0.8, 91)})
                FrmMain.PanForm.Background = Brush
            Else
                FrmMain.PanForm.Background = New MyColor(245, 245, 245)
            End If
            FrmMain.PanForm.Background.Freeze()
        End Sub)
    End Sub
    ''' <summary>
    ''' 本分支已解除主题的赞助 / 成就解锁限制：全部主题直接可用。
    ''' EffectSetup 为 True 时才会同步 UI（隐藏灰色占位与解锁提示）。
    ''' </summary>
    Friend Sub ThemeCheckAll(EffectSetup As Boolean)
        Try
            If EffectSetup AndAlso FrmSetupUI IsNot Nothing Then FrmSetupUI.ThemeUnlockAll()
        Catch ex As Exception
            Logger.Warn(ex, "解锁主题时出错")
        End Try
    End Sub
    Friend Function ThemeCheckOne(Id As Integer) As Boolean
        Return True
    End Function
    Friend Function ThemeUnlock(Id As Integer, Optional ShowDoubleHint As Boolean = True, Optional UnlockHint As String = Nothing) As Boolean
        If ShowDoubleHint AndAlso Not String.IsNullOrEmpty(UnlockHint) Then Hint(UnlockHint, HintType.Green, False)
        Return True
    End Function

#End Region

#Region "更新"

    ''' <summary>
    ''' 已下载、等待重启后替换当前程序的新版本文件。
    ''' </summary>
    Friend ReadOnly Property UpdateNewFile As String
        Get
            Return Paths.Base & "PCL\Plain Craft Launcher 2.exe"
        End Get
    End Property

    Friend IsUpdateWaitingRestart As Boolean = False

    ''' <summary>
    ''' 从 GitHub 仓库获取最新 Release。失败时抛出异常。
    ''' </summary>
    Private Function GetLatestRelease(Repo As String) As JObject
        Try
            Return NetRequestByClientRetry($"https://api.github.com/repos/{Repo}/releases/latest", RequireJson:=True).DeserializeJson()
        Catch ex As HttpRequestCodeException
            '把常见的 HTTP 错误翻译成能看懂的话
            Select Case ex.StatusCode
                Case HttpStatusCode.NotFound
                    Throw New Exception($"更新源 {Repo} 还没有发布任何 Release，或者该仓库是私有的（检查更新需要公开仓库）。")
                Case HttpStatusCode.Forbidden, CType(429, HttpStatusCode)
                    Throw New Exception("GitHub 接口访问次数超限（匿名每小时 60 次/IP），稍后会自动改用备用方式。")
                Case Else
                    Throw
            End Select
        End Try
    End Function

    ''' <summary>
    ''' 从 Release 的附件中挑选可用于自动更新的文件（优先 exe）。没有则返回 Nothing。
    ''' </summary>
    Private Function GetReleaseExeUrl(Release As JObject) As String
        For Each Asset As JToken In If(Release?.Item("assets"), New JArray())
            Dim Name As String = If(Asset?("name")?.ToString, "")
            If Name.EndsWithF(".exe") Then Return Asset("browser_download_url")?.ToString
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' 一次更新检查的结果。
    ''' </summary>
    Private Structure UpdateInfo
        Public Tag As String
        Public Body As String
        Public ExeUrl As String
        Public PageUrl As String
        Public FromApi As Boolean
    End Structure

    ''' <summary>
    ''' 用 releases.atom 取最新 tag。这条路不消耗 GitHub API 的匿名配额（60 次/小时/IP），
    ''' 用于 API 被限流或拦截时兜底。
    ''' </summary>
    Private Function GetLatestTagByAtom(Repo As String) As String
        Try
            Dim Xml As String = NetRequestByClientRetry($"https://github.com/{Repo}/releases.atom")
            '取第一个 entry 里的 /releases/tag/xxx
            Dim M = System.Text.RegularExpressions.Regex.Match(Xml, "/releases/tag/([^""<>/\s]+)")
            If M.Success Then Return M.Groups(1).Value
        Catch ex As Exception
            Logger.Warn(ex, "通过 atom 获取最新版本号失败")
        End Try
        Return ""
    End Function

    ''' <summary>
    ''' 用 expanded_assets 页面取附件的下载地址（同样不走 API）。
    ''' </summary>
    Private Function GetReleaseExeUrlByHtml(Repo As String, Tag As String) As String
        Try
            Dim Html As String = NetRequestByClientRetry($"https://github.com/{Repo}/releases/expanded_assets/{Tag}")
            Dim M = System.Text.RegularExpressions.Regex.Match(Html, "href=""([^""]*/releases/download/[^""]+\.exe)""")
            If M.Success Then Return "https://github.com" & M.Groups(1).Value
        Catch ex As Exception
            Logger.Warn(ex, "通过网页获取更新附件地址失败")
        End Try
        Return Nothing
    End Function

    ''' <summary>
    ''' 一条更新源。
    ''' </summary>
    Private Structure UpdateSource
        Public Platform As String 'github / gitcode
        Public Repo As String
        Public ReadOnly Property Display As String
            Get
                Return $"{Platform}:{Repo}"
            End Get
        End Property
    End Structure

    ''' <summary>
    ''' 解析 UpdateRepo 配置。支持多条，用 ; 或 , 分隔：
    ''' "github:Thomas455/PCL-Thomas;gitcode:Thomas455/PCL-Thomas"
    ''' 不带前缀时默认按 GitHub 处理（兼容旧配置）。
    ''' </summary>
    Private Function ParseUpdateSources() As List(Of UpdateSource)
        Dim Result As New List(Of UpdateSource)
        For Each Raw As String In If(UpdateRepo, "").Split({";"c, ","c}, StringSplitOptions.RemoveEmptyEntries)
            Dim S As String = Raw.Trim()
            If S = "" Then Continue For
            Dim Platform As String = "github"
            Dim Repo As String = S
            Dim Idx As Integer = S.IndexOf(":"c)
            If Idx > 0 Then
                Platform = S.Substring(0, Idx).Trim().ToLowerInvariant()
                Repo = S.Substring(Idx + 1).Trim()
            End If
            If Platform <> "github" AndAlso Platform <> "gitcode" Then
                Logger.Warn($"忽略未知的更新源平台：{Platform}（仅支持 github / gitcode）")
                Continue For
            End If
            Repo = Repo.Trim("/"c)
            If Repo <> "" Then Result.Add(New UpdateSource With {.Platform = Platform, .Repo = Repo})
        Next
        Return Result
    End Function

    ''' <summary>
    ''' 获取更新信息：按配置顺序依次尝试各更新源，第一个成功的胜出。
    ''' </summary>
    Private Function GetUpdateInfo() As UpdateInfo
        Dim Sources As List(Of UpdateSource) = ParseUpdateSources()
        If Sources.Count = 0 Then
            Throw New Exception("尚未配置更新源，请在 Modules\ModSecretBuild.vb 或 PCL\Secret.ini 中填写" &
                                """github:用户名/仓库名"" 或 ""gitcode:用户名/仓库名""")
        End If
        '遍历所有源，取版本号最高的那个，避免某个镜像落后时误判为“已是最新”
        Dim Best As UpdateInfo = New UpdateInfo
        Dim BestVersion As Version = Nothing
        Dim HasBest As Boolean = False
        Dim Errors As New List(Of String)
        For Each Src In Sources
            Try
                Dim Info As UpdateInfo = GetUpdateInfoFrom(Src)
                Dim V As Version = Nothing
                If Not Version.TryParse(If(Info.Tag, "").Trim().TrimStart("v"c, "V"c), V) Then
                    Errors.Add($"{Src.Display}：无法解析版本号（{Info.Tag}）")
                    Continue For
                End If
                Logger.Info($"更新源 {Src.Display} 报告版本 {V}")
                If Not HasBest OrElse V > BestVersion Then
                    Best = Info
                    BestVersion = V
                    HasBest = True
                End If
            Catch ex As Exception
                ex.ThrowIfCanceled()
                Errors.Add($"{Src.Display}：{ex.Message}")
                Logger.Warn(ex, $"更新源 {Src.Display} 不可用")
            End Try
        Next
        If Not HasBest Then
            Throw New Exception("所有更新源都不可用：" & vbCrLf & String.Join(vbCrLf, Errors))
        End If
        Return Best
    End Function

    Private Function GetUpdateInfoFrom(Src As UpdateSource) As UpdateInfo
        If Src.Platform = "gitcode" Then Return GetUpdateInfoFromGitCode(Src.Repo)
        Return GetUpdateInfoFromGitHub(Src.Repo)
    End Function

    ''' <summary>
    ''' GitHub：先 API，被限流时退回 releases.atom（不消耗 API 配额）。
    ''' </summary>
    Private Function GetUpdateInfoFromGitHub(Repo As String) As UpdateInfo
        Try
            Dim Release As JObject = GetLatestRelease(Repo)
            Return New UpdateInfo With {
                .Tag = If(Release?("tag_name")?.ToString, ""),
                .Body = If(Release?("body")?.ToString, ""),
                .ExeUrl = GetReleaseExeUrl(Release),
                .PageUrl = If(Release?("html_url")?.ToString, ""),
                .FromApi = True}
        Catch ex As Exception
            ex.ThrowIfCanceled()
            'API 受限（常见于共享出口 IP，匿名额度被同 IP 的其他请求用光）时改用网页方式
            Logger.Warn(ex, "GitHub API 不可用，改用 releases.atom 检查更新")
            Dim Tag As String = GetLatestTagByAtom(Repo)
            If Tag = "" Then
                Throw New Exception("GitHub 接口与网页方式都未能获取版本号。原始错误：" & ex.Message, ex)
            End If
            Return New UpdateInfo With {
                .Tag = Tag,
                .Body = "",
                .ExeUrl = GetReleaseExeUrlByHtml(Repo, Tag),
                .PageUrl = $"https://github.com/{Repo}/releases/tag/{Tag}",
                .FromApi = False}
        End Try
    End Function

    ''' <summary>
    ''' GitCode：用 /api/v5/repos/{owner}/{repo}/releases 列表（公共仓库无需 token，
    ''' 限额 400 次/分钟），取 created_at 最新的一条。
    ''' </summary>
    Private Function GetUpdateInfoFromGitCode(Repo As String) As UpdateInfo
        Dim Json As JArray = CType(NetRequestByClientRetry(
            $"https://api.gitcode.com/api/v5/repos/{Repo}/releases", RequireJson:=True).DeserializeJson(), JArray)
        If Json Is Nothing OrElse Json.Count = 0 Then
            Throw New Exception($"GitCode 仓库 {Repo} 还没有发布任何 Release")
        End If
        Dim Release As JObject = Json.Cast(Of JObject)().
            OrderByDescending(Function(j) If(j?("created_at")?.ToString, "")).First()
        Dim Tag As String = If(Release?("tag_name")?.ToString, "")
        Return New UpdateInfo With {
            .Tag = Tag,
            .Body = If(Release?("body")?.ToString, ""),
            .ExeUrl = GetReleaseExeUrl(Release),
            .PageUrl = $"https://gitcode.com/{Repo}/releases/tag/{Tag}",
            .FromApi = True}
    End Function

    ''' <summary>
    ''' 检查更新。更新源为本地凭据文件中配置的 GitHub 仓库。
    ''' </summary>
    Friend Sub UpdateCheckByButton()
        Hint("正在检查更新……")
        RunInNewThread(
        Sub()
            Try
                Dim Info As UpdateInfo = GetUpdateInfo()
                Dim Tag As String = Info.Tag.Trim()
                If Tag = "" Then Throw New Exception("更新源没有返回版本号")
                Dim NewVersion As Version = Nothing
                If Not Version.TryParse(Tag.TrimStart("v"c, "V"c), NewVersion) Then Throw New Exception($"无法识别更新源返回的版本号：{Tag}")
                If NewVersion <= New Version(VersionBaseName) Then
                    RunInUi(Sub() MyMsgBox($"当前已是最新版本（{VersionBaseName}）。", "检查更新"))
                    Return
                End If
                '整理更新说明
                Dim BodyText As String = Info.Body.Trim().Replace(vbCrLf, vbLf).Replace(vbLf, vbCrLf)
                If BodyText.Length > 800 Then BodyText = BodyText.Substring(0, 800) & vbCrLf & "……"
                Dim Message As String = $"发现新版本 {Tag}，当前版本 {VersionBaseName}。"
                If BodyText <> "" Then Message &= vbCrLf & vbCrLf & BodyText
                Dim ExeUrl As String = Info.ExeUrl
                Dim PageUrl As String = Info.PageUrl
                '询问用户
                Dim Choice As Integer = 2
                Dim Waiter As New ManualResetEventSlim(False)
                RunInUi(
                Sub()
                    Choice = If(String.IsNullOrEmpty(ExeUrl),
                        MyMsgBox(Message & vbCrLf & vbCrLf & "该版本没有提供可直接下载的 exe，是否打开下载页面？", "检查更新", "打开页面", "取消"),
                        MyMsgBox(Message & vbCrLf & vbCrLf & "是否现在下载并安装？", "检查更新", "下载并安装", "取消"))
                    Waiter.Set()
                End Sub)
                Waiter.Wait()
                If Choice <> 1 Then Return
                '没有 exe 附件时改为打开 Release 页面
                If String.IsNullOrEmpty(ExeUrl) Then
                    If PageUrl <> "" Then RunInUi(Sub() OpenWebsite(PageUrl))
                    Return
                End If
                '下载新版本
                Hint("正在下载新版本……")
                Dim Folder As String = Paths.Base & "PCL\"
                If Not IO.Directory.Exists(Folder) Then IO.Directory.CreateDirectory(Folder)
                NetDownloadByClient(ExeUrl, UpdateNewFile, SimulateBrowserHeaders:=True)
                IsUpdateWaitingRestart = True
                '询问是否重启
                Dim Restart As Integer = 2
                Dim Waiter2 As New ManualResetEventSlim(False)
                RunInUi(
                Sub()
                    Restart = MyMsgBox("新版本已下载完成，重启启动器后生效。是否立即重启？", "更新完成", "立即重启", "稍后重启")
                    Waiter2.Set()
                End Sub)
                Waiter2.Wait()
                If Restart = 1 Then RunInUi(Sub() FrmMain.EndProgram(False))
            Catch ex As Exception
                ex.ThrowIfCanceled()
                Logger.Error(ex, "检查更新失败", LogBehavior.Toast)
            End Try
        End Sub, "UpdateCheck")
    End Sub

    ''' <summary>
    ''' 启动新版本进程完成文件替换。由程序退出流程调用。
    ''' </summary>
    Public Sub UpdateRestart(TriggerRestartAndByEnd As Boolean)
        Try
            Dim NewFile As String = UpdateNewFile
            If Not FileUtils.Exists(NewFile) Then Return
            '由新版本自身作为更新器：--update <旧进程 PID> "<旧文件>" "<新文件>" <替换后是否重启>
            Dim Arguments As String = $"--update {Process.GetCurrentProcess().Id} ""{PathExe}"" ""{NewFile}"" True"
            StartProcess(NewFile, Arguments)
            Logger.Info($"已启动更新程序：{NewFile} {Arguments}")
        Catch ex As Exception
            Logger.Error(ex, "启动更新程序失败", LogBehavior.Toast)
        End Try
    End Sub

    ''' <summary>
    ''' 由新版本进程调用：等待旧版本退出后用 NewFileName 覆盖 OldFileName，并按需重启。
    ''' </summary>
    Public Sub UpdateReplace(ProcessId As Integer, OldFileName As String, NewFileName As String, TriggerRestart As Boolean)
        Try
            '等待旧版本退出
            Try
                Dim OldProcess As Process = Process.GetProcessById(ProcessId)
                If Not OldProcess.WaitForExit(30000) Then Logger.Warn("等待旧版本退出超时，仍尝试替换文件")
            Catch ex As ArgumentException
                '旧进程已经不在了
            End Try
            Thread.Sleep(200)
            '替换文件
            FileUtils.Copy(NewFileName, OldFileName)
            Logger.Info($"更新完成：{NewFileName} → {OldFileName}")
            '清理残留
            If Not String.Equals(NewFileName, OldFileName, StringComparison.OrdinalIgnoreCase) Then
                Try
                    FileUtils.Delete(NewFileName)
                Catch ex As Exception
                    Logger.Warn(ex, "清理更新残留文件失败")
                End Try
            End If
            If TriggerRestart Then StartProcess(OldFileName)
        Catch ex As Exception
            Logger.Error(ex, "替换更新文件失败")
        End Try
    End Sub

    ''' <summary>
    ''' 确保 PathTemp/Latest.exe 是更新仓库中最新的 PCL，它会被用于整合包打包。
    ''' </summary>
    Friend Sub DownloadLatestPCL(Optional LoaderToSyncProgress As LoaderBase = Nothing)
        Try
            '按配置顺序找一个提供了 exe 附件的更新源
            Dim ExeUrl As String = Nothing
            Dim LastEx As Exception = Nothing
            For Each Src In ParseUpdateSources()
                Try
                    Dim Info As UpdateInfo = GetUpdateInfoFrom(Src)
                    If Not String.IsNullOrEmpty(Info.ExeUrl) Then
                        ExeUrl = Info.ExeUrl
                        Exit For
                    End If
                Catch ex As Exception
                    ex.ThrowIfCanceled()
                    LastEx = ex
                    Logger.Warn(ex, $"更新源 {Src.Display} 不可用")
                End Try
            Next
            If String.IsNullOrEmpty(ExeUrl) Then
                Throw New Exception("所有更新源都没有提供 exe 附件" & If(LastEx Is Nothing, "", "：" & LastEx.Message), LastEx)
            End If
            NetDownloadByLoader(ExeUrl, PathTemp & "Latest.exe", LoaderToSyncProgress, SimulateBrowserHeaders:=True)
        Catch ex As Exception
            Throw New Exception("下载最新的 PCL 正式版失败，请检查 Modules\ModSecretBuild.vb 或 PCL\Secret.ini 中的更新源配置", ex)
        End Try
    End Sub

#End Region

#Region "联网配置"

    ''' <summary>
    ''' 联网获取的配置信息。
    ''' 若获取失败或仍在获取中，可能为 Nothing。
    ''' </summary>
    Public ServerConfig As JObject

    Public ServerLoader As New LoaderTask(Of Integer, Integer)("PCL 配置更新", Sub() Logger.Info("该版本中不包含更新通知功能……"), Priority:=ThreadPriority.BelowNormal) With
        {.ReloadTimeout = 1000 * 60 * 60} '超时 1 小时

#End Region

#Region "赞助等级"

    Public ReadOnly Property CurrentRank As DonationRank
        Get
            Return DonationRank.None
        End Get
    End Property

    Public Sub InputPotatoCode(IsUpdating As Boolean)
    End Sub
    Friend Sub GeneratePotatoCode()
    End Sub

    ''' <summary>
    ''' 获取设备识别码。
    ''' </summary>
    Friend Function GetIdentify() As String
        Return "0000-0000-0000-0000"
    End Function

#End Region

End Module
