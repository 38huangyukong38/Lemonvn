using System.Diagnostics;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Helpers;
using GalgameManager.Enums;
using GalgameManager.Models.Sources;

namespace GalgameManager.Models.BgTasks;

/// <summary>
/// 游戏存档检测任务
/// <para>
/// 通过文件系统监听和启发式搜索来自动定位游戏的存档目录。
/// </para>
/// </summary>
public class GameSaveDetectorTask : BgTaskBase
{
    #region Properties & Fields
    public Galgame? Galgame { get; set; }
    public Guid? InstallationId { get; set; } // 要检测存档的安装实例Id
    public List<string> DetectedSavePaths { get; set; } = new();
    public List<string> MonitoredPaths { get; set; } = new();
    public bool IsMonitoring { get; set; }
    public int SaveOperationCount { get; set; }

    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly List<string> _candidatePaths = new();
    private readonly List<string> _pendingMonitorPaths = new(); // 待监听的路径
    private readonly Dictionary<string, DateTime> _pathFirstDetected = new(); // 路径首次检测时间
    private DateTime _monitorStartTime;
    private const int DELAY_SECONDS = 10; // 延迟10秒后开始监听不存在的路径
    private const int SAVE_COUNT_THRESHOLD = 3; // 需要检测到3次保存操作才开始监听
    private const double SIMILARITY_THRESHOLD = 0.8; // 相似度阈值

    // 缓存变体结果以提高性能
    private List<string>? _cachedVariants;
    /// <summary>
    /// 缓存的小写变体列表，用于高频的文件路径匹配，避免重复调用 ToLowerInvariant
    /// </summary>
    private List<string>? _cachedLowerVariants;
    /// <summary>
    /// 缓存的核心名称列表（游戏名，中文名，原名等），用于模糊匹配（Jaro-Winkler）
    /// </summary>
    private List<string>? _fuzzyMatchCoreVariants;
    private string _lastGameName = string.Empty;
    private GalgameAndPath? _installation; // 要检测存档的安装实例
    private string? InstallationPath => _installation?.Path; // 安装实例根目录

    #endregion

    #region Constructors & Lifecycle
    public override string Title => "GameSaveDetectorTask_Title".GetLocalized();

    public GameSaveDetectorTask() { } // For serialization

    /// <summary>
    /// 使用游戏的首选安装实例创建存档检测任务。
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    public GameSaveDetectorTask(Galgame game)
    {
        Galgame = game;
        _installation = game.PreferredLocalInstallation;
        InstallationId = _installation?.EntryId;
    }

    /// <summary>
    /// 使用明确安装实例创建存档检测任务。
    /// </summary>
    /// <param name="game">目标逻辑游戏</param>
    /// <param name="installation">要检测存档的安装实例</param>
    public GameSaveDetectorTask(Galgame game, GalgameAndPath installation)
    {
        Galgame = game;
        _installation = installation;
        InstallationId = installation.EntryId;
    }

    protected override Task RecoverFromJsonInternal()
    {
        _installation = Galgame?.SourceEntries.FirstOrDefault(e => e.EntryId == InstallationId);
        // 重新初始化候选路径
        InitializeCandidatePaths();
        return Task.CompletedTask;
    }

    protected async override Task RunInternal()
    {
        if (Galgame == null) return;

        // 1. 初始化阶段更新进度消息
        ChangeProgress(0, 1, "GameSaveDetector_Initializing".GetLocalized()); // "正在初始化存档检测..."

        // 0. 预计算优化：提前计算好所有变体的小写形式，供监听线程快速匹配
        PrecomputeVariants();

        // 2. 初始化搜索路径
        InitializeCandidatePaths();

        Debug.WriteLine($"[GameSaveDetector] 开始为游戏 '{Galgame.Name?.Value}' 检测存档路径");
        Debug.WriteLine($"[GameSaveDetector] 候选路径数量: {_candidatePaths.Count}");

        // 3. 启动监听
        Debug.WriteLine("[GameSaveDetector] 启动文件系统监听检测存档");
        await StartDelayedFileSystemMonitoring();

        // 4. 过滤和分析结果
        var finalPaths = FilterDetectedPaths();
        Debug.WriteLine($"[GameSaveDetector] 过滤后的候选路径数量: {finalPaths.Count}");

        // 5. 处理最终结果并更新UI
        ProcessDetectionResults(finalPaths);
        if (_installation?.Source is not null)
            App.GetService<IGalgameSourceCollectionService>().Save(_installation.Source);
        await App.GetService<IGalgameCollectionService>().SaveGalgameAsync(Galgame);
    }
    #endregion

    #region Result Processing
    /// <summary>
    /// 处理检测结果，选择最佳存档目录并通知用户
    /// </summary>
    private void ProcessDetectionResults(List<string> finalPaths)
    {
        if (finalPaths.Count > 0 && Galgame != null)
        {
            var saveDirectory = FindBestSaveDirectory(finalPaths);
            Debug.WriteLine($"[GameSaveDetector] 最终选择的存档目录: {saveDirectory}");

            if (!string.IsNullOrEmpty(saveDirectory))
            {
                if (_installation?.LocalConfig is { } config)
                    config.DetectedSavePath = GamePortablePath.Create(saveDirectory, InstallationPath);
                // 4. 【关键】成功后，将进度设为 1/1，并将消息设置为最终路径
                ChangeProgress(1, 1, "GameSaveDetector_Success".GetLocalized(saveDirectory)); // "成功检测到存档：{0}"
            }
            else
            {
                // 如果找不到最佳目录（虽然有文件），尝试使用第一个文件的父目录作为回退
                var fallbackDirectory = Path.GetDirectoryName(finalPaths[0]);
                if (!string.IsNullOrEmpty(fallbackDirectory))
                {
                    if (_installation?.LocalConfig is { } config)
                        config.DetectedSavePath = GamePortablePath.Create(fallbackDirectory, InstallationPath);
                    Debug.WriteLine($"[GameSaveDetector] 使用回退目录: {fallbackDirectory}");
                    ChangeProgress(1, 1, "GameSaveDetector_Success".GetLocalized(fallbackDirectory));
                }
                else
                {
                    // 虽然有文件但没确定目录（罕见）
                    ChangeProgress(1, 1, "GameSaveDetector_Failed".GetLocalized());
                }
            }
        }
        else
        {
            Debug.WriteLine("[GameSaveDetector] 未找到合适的存档目录");
            // 5. 失败处理
            ChangeProgress(1, 1, "GameSaveDetector_NotFound".GetLocalized()); // "未检测到存档变动"
        }
    }

    #endregion

    #region Candidate Initialization
    /// <summary>
    /// 预计算游戏名称变体并缓存小写版本
    /// </summary>
    private void PrecomputeVariants()
    {
        if (Galgame == null) return;
        var allGeneratedVariants = GenerateAllVariants(Galgame);

        // 缓存所有生成的小写变体
        _cachedLowerVariants = allGeneratedVariants
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v.ToLowerInvariant())
            .Distinct()
            .ToList();

        // 缓存核心名称用于模糊匹配
        // 核心名称包括：游戏名、中文名、原名、开发商、分类
        var coreNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Galgame.Name?.Value is { } name) coreNames.Add(name);
        if (!string.IsNullOrEmpty(Galgame.ChineseName)) coreNames.Add(Galgame.ChineseName!);
        if (Galgame.OriginalName?.Value is { } original) coreNames.Add(original);
        if (Galgame.Developer?.Value is { } developer) coreNames.Add(developer);
        if (Galgame.Categories != null)
        {
            foreach (var category in Galgame.Categories)
            {
                if (!string.IsNullOrEmpty(category.Name)) coreNames.Add(category.Name);
            }
        }

        _fuzzyMatchCoreVariants = coreNames
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v.ToLowerInvariant())
            .Distinct()
            .ToList();

        Debug.WriteLine($"[GameSaveDetector] 预计算了 {_cachedLowerVariants.Count} 个小写变体用于快速匹配");
        Debug.WriteLine($"[GameSaveDetector] 预计算了 {_fuzzyMatchCoreVariants.Count} 个核心变体用于模糊匹配 (Jaro-Winkler)");
    }

    /// <summary>
    /// 初始化所有可能的存档候选路径
    /// </summary>
    private void InitializeCandidatePaths()
    {
        if (Galgame == null) return;

        Debug.WriteLine("[GameSaveDetector] 初始化候选路径");
        _candidatePaths.Clear();

        // 分解为多个步骤添加路径，提高代码可读性
        AddGameInstallPath();
        AddStandardUserPaths();
        AddHeuristicPaths();

        Debug.WriteLine($"[GameSaveDetector] 最终候选路径数量: {_candidatePaths.Count}");
    }

    /// <summary>
    /// 辅助函数：添加路径到候选列表（自动去重）
    /// </summary>
    private void AddCandidatePath(string path)
    {
        if (!string.IsNullOrEmpty(path) && !_candidatePaths.Contains(path))
        {
            _candidatePaths.Add(path);
        }
    }

    private void AddGameInstallPath()
    {
        // 游戏安装目录（如果是本地的）
        if (!string.IsNullOrEmpty(InstallationPath))
        {
            AddCandidatePath(InstallationPath);
            Debug.WriteLine($"[GameSaveDetector] 添加游戏安装目录: {InstallationPath}");
        }
    }

    private void AddStandardUserPaths()
    {
        // 用户文档目录
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        AddCandidatePath(documentsPath);
        AddCandidatePath(Path.Combine(documentsPath, "My Games"));
        AddCandidatePath(Path.Combine(documentsPath, "Saved Games"));

        // 用户主目录下的Saved Games目录
        var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        AddCandidatePath(userProfilePath);
        AddCandidatePath(Path.Combine(userProfilePath, "Saved Games"));

        // AppData 目录
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var localLowPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Replace("Local", "LocalLow");

        AddCandidatePath(appDataPath);
        AddCandidatePath(localAppDataPath);
        AddCandidatePath(localLowPath);
    }

    private void AddHeuristicPaths()
    {
        // 获取当前程序路径以排除 PotatoVN 相关路径
        var currentAppPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        Debug.WriteLine($"[GameSaveDetector] 当前程序路径: {currentAppPath}");

        // 基于游戏名称和开发者的启发式路径
        var gameKeywords = ExtractGameKeywords();
        Debug.WriteLine($"[GameSaveDetector] 提取到的关键词数量: {gameKeywords.Count}");

        var basePaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        foreach (var keyword in gameKeywords)
        {
            if (string.IsNullOrEmpty(keyword)) continue;

            foreach (var basePath in basePaths)
            {
                var combinedPath = Path.Combine(basePath, keyword);
                // 检查路径是否包含 PotatoVN 或在当前程序路径下
                if (!ShouldExcludePath(combinedPath, currentAppPath))
                {
                    AddCandidatePath(combinedPath);
                    // 这里保持原来的Debug信息
                    if (basePath.Contains("AppData"))
                        Debug.WriteLine($"[GameSaveDetector] 添加AppData/LocalAppData路径: {combinedPath}");
                    else
                        Debug.WriteLine($"[GameSaveDetector] 添加文档路径: {combinedPath}");
                }
            }
        }
    }

    /// <summary>
    /// 判断是否应该强制监听该路径（跳过排除检查）
    /// </summary>
    /// <param name="path">要检查的路径</param>
    /// <returns>是否应该强制监听</returns>
    private bool ShouldForceMonitorPath(string path)
    {
        if (string.IsNullOrEmpty(path) || InstallationPath == null)
            return false;

        var gameLocalPath = InstallationPath.ToLowerInvariant();
        var targetPath = path.ToLowerInvariant();

        // 如果是游戏根目录本身，强制监听
        if (targetPath == gameLocalPath)
        {
            Debug.WriteLine($"[GameSaveDetector] 强制监听游戏根目录: {path}");
            return true;
        }

        return false;
    }

    private bool ShouldExcludePath(string targetPath, string currentAppPath)
    {
        if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(currentAppPath))
            return false;

        // 修复：如果路径在游戏目录下，不进行排除检查
        if (InstallationPath != null && targetPath.StartsWith(InstallationPath, StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine($"[GameSaveDetector] 路径在游戏目录下，跳过排除: {targetPath}");
            return false;
        }

        // 首先检查是否应该强制监听
        if (ShouldForceMonitorPath(targetPath))
        {
            Debug.WriteLine($"[GameSaveDetector] 强制监听路径，跳过排除: {targetPath}");
            return false;
        }

        // 使用常量检查是否应该排除此路径
        var shouldExclude = SaveDetectionConstants.ShouldExcludePath(targetPath.AsSpan(), currentAppPath);

        if (shouldExclude)
        {
            Debug.WriteLine($"[GameSaveDetector] 排除路径: {targetPath}");
        }

        return shouldExclude;
    }

    private List<string> ExtractGameKeywords()
    {
        var keywords = new List<string>();
        if (Galgame == null) return keywords;

        // 从游戏名称提取关键字
        if (Galgame.Name?.Value is { } name) keywords.Add(name);
        if (!string.IsNullOrEmpty(Galgame.ChineseName)) keywords.Add(Galgame.ChineseName!);
        if (Galgame.OriginalName?.Value is { } original) keywords.Add(original);

        // 从开发者名称提取关键字
        if (!string.IsNullOrEmpty(Galgame.Developer?.Value)) keywords.Add(Galgame.Developer.Value);

        // 从分类中提取关键字
        if (Galgame.Categories != null)
        {
            foreach (var category in Galgame.Categories)
            {
                if (category.Name != null) keywords.Add(category.Name);
            }
        }

        // 添加开发者变体，特别是针对 ASa Project 的各种可能性
        AddDeveloperVariants(keywords);
        return keywords;
    }

    private void AddDeveloperVariants(List<string> keywords)
    {
        // 重构为使用新的变体生成系统
        var allVariants = GenerateAllVariants(Galgame!);
        foreach (var variant in allVariants)
        {
            if (!string.IsNullOrEmpty(variant) && !keywords.Contains(variant))
            {
                keywords.Add(variant);
            }
        }
    }

    #endregion

    #region Variant Generation System
    // ========================================================================
    // 变体生成系统 (Variant Generation System)
    // ========================================================================

    /// <summary>
    /// 生成游戏相关的所有变体关键词（带缓存）
    /// </summary>
    /// <param name="game">游戏对象</param>
    /// <returns>包含所有变体的列表</returns>
    private List<string> GenerateAllVariants(Galgame game)
    {
        if (game == null) return new List<string>();

        // 创建游戏唯一标识符
        var gameIdentifier = $"{game.Name?.Value}_{game.ChineseName}_{game.OriginalName?.Value}_{game.Developer?.Value}";

        // 检查缓存
        if (_cachedVariants != null && _lastGameName == gameIdentifier)
        {
            Debug.WriteLine($"[GameSaveDetector] 使用缓存变体，数量: {_cachedVariants.Count}");
            return _cachedVariants;
        }

        var allVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Debug.WriteLine($"[GameSaveDetector] 开始为游戏 '{game.Name?.Value}' 生成变体");

        // 1. 游戏名称变体
        GenerateNameVariants(game.Name?.Value, allVariants);
        GenerateNameVariants(game.ChineseName, allVariants);
        GenerateNameVariants(game.OriginalName?.Value, allVariants);

        // 2. 开发者变体
        GenerateDeveloperVariants(game.Developer?.Value, allVariants);

        // 3. 分类变体
        if (game.Categories != null)
        {
            foreach (var category in game.Categories)
            {
                if (!string.IsNullOrEmpty(category.Name))
                {
                    GenerateNameVariants(category.Name, allVariants);
                }
            }
        }

        // 缓存结果
        _cachedVariants = allVariants.ToList();
        _lastGameName = gameIdentifier;

        Debug.WriteLine($"[GameSaveDetector] 总共生成了 {allVariants.Count} 个变体（已缓存）");
        return _cachedVariants;
    }

    /// <summary>
    /// 生成名称的所有变体
    /// </summary>
    private void GenerateNameVariants(string? name, HashSet<string> variants)
    {
        if (string.IsNullOrEmpty(name)) return;

        Debug.WriteLine($"[GameSaveDetector] 为名称 '{name}' 生成变体");

        // 原始名称
        variants.Add(name);

        // 大小写变体
        variants.Add(name.ToLowerInvariant());
        variants.Add(name.ToUpperInvariant());

        // 首字母大写
        if (name.Length > 0)
        {
            var titleCase = char.ToUpperInvariant(name[0]) + name.Substring(1).ToLowerInvariant();
            variants.Add(titleCase);
        }

        // 空格和分隔符变体
        GenerateSeparatorVariants(name, variants);

        // 常见缩写和简化变体
        GenerateAbbreviationVariants(name, variants);

        // 特殊字符处理
        GenerateSpecialCharacterVariants(name, variants);
    }

    /// <summary>
    /// 生成分隔符变体（空格、下划线、连字符等）- 使用常量
    /// </summary>
    private void GenerateSeparatorVariants(string name, HashSet<string> variants)
    {
        foreach (var currentSep in SaveDetectionConstants.CurrentSeparators)
        {
            if (!name.Contains(currentSep)) continue;

            var parts = name.Split(currentSep, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            // 为每个分隔符生成交替版本
            foreach (var newSep in SaveDetectionConstants.Separators)
            {
                if (newSep == currentSep) continue;
                if (newSep == '\0') continue; // 跳过空分隔符

                var variant = string.Join(newSep.ToString(), parts);
                variants.Add(variant);
            }

            // 无分隔符版本
            var noSepVariant = string.Join("", parts);
            variants.Add(noSepVariant);

            // 驼峰命名版本
            var camelCaseVariant = string.Join("", parts.Select((part, index) =>
                index == 0 ? part.ToLowerInvariant() :
                (part.Length > 0 ? char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant() : part)));
            variants.Add(camelCaseVariant);
        }
    }

    /// <summary>
    /// 生成缩写和简化变体 - 使用常量
    /// </summary>
    private void GenerateAbbreviationVariants(string name, HashSet<string> variants)
    {
        // 提取首字母缩写
        var words = name.Split(SaveDetectionConstants.CurrentSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            var abbreviation = new string(words.Select(word =>
                string.IsNullOrEmpty(word) ? ' ' : char.ToUpperInvariant(word[0])).ToArray());
            variants.Add(abbreviation);
            variants.Add(abbreviation.ToLowerInvariant());
        }

        // 使用常量进行词汇简化
        SaveDetectionConstants.ApplyWordSimplifications(name, variants);
    }

    /// <summary>
    /// 生成特殊字符变体 - 使用常量
    /// </summary>
    private void GenerateSpecialCharacterVariants(string name, HashSet<string> variants)
    {
        // 使用常量进行日语字符的罗马音变体
        SaveDetectionConstants.ApplyJapaneseConversions(name, variants);

        // 使用常量进行数字变体
        var numberVariants = SaveDetectionConstants.ApplyNumberConversions(name);
        foreach (var variant in numberVariants)
        {
            variants.Add(variant);
        }
    }

    /// <summary>
    /// 生成开发者特定的变体
    /// </summary>
    private void GenerateDeveloperVariants(string? developer, HashSet<string> variants)
    {
        if (string.IsNullOrEmpty(developer)) return;

        Debug.WriteLine($"[GameSaveDetector] 为开发者 '{developer}' 生成变体");

        // 基础名称变体
        GenerateNameVariants(developer, variants);

        // 使用常量获取开发者特定变体
        var developerSpecificVariants = SaveDetectionConstants.GetDeveloperVariants(developer);

        foreach (var variant in developerSpecificVariants)
        {
            variants.Add(variant);
        }
    }

    #endregion

    #region File System Monitoring
    // ========================================================================
    // 文件系统监听 (File System Monitoring)
    // ========================================================================

    private async Task StartDelayedFileSystemMonitoring()
    {
        IsMonitoring = true;
        Debug.WriteLine("[GameSaveDetector] 开始延迟文件系统监听");

        // 首先监听已存在的路径
        StartMonitoringForExistingPaths();

        // 监听最多2分钟，包含延迟机制
        var maxMonitorTime = TimeSpan.FromMinutes(2);
        var earlyStopThreshold = 3;
        var confidenceThreshold = 2;

        _monitorStartTime = DateTime.Now;
        Debug.WriteLine($"[GameSaveDetector] 开始文件系统监听，最长监听时间: {maxMonitorTime.TotalMinutes} 分钟");

        while (IsMonitoring && (DateTime.Now - _monitorStartTime) < maxMonitorTime)
        {
            Debug.WriteLine($"[GameSaveDetector] 当前检测到 {DetectedSavePaths.Count} 个潜在存档文件");

            // 6.任务进行中（不确定的进度），Message 显示当前捕获数量
            var count = DetectedSavePaths.Count;
            ChangeProgress(0, 1, "GameSaveDetector_Monitoring".GetLocalized(count));

            // 检查是否有路径可以开始监听
            CheckAndStartPendingMonitors();

            if (ShouldStopEarly(DetectedSavePaths, earlyStopThreshold, confidenceThreshold))
            {
                Debug.WriteLine("[GameSaveDetector] 达到早停条件，停止监听");
                break;
            }

            await Task.Delay(1000); // 每秒检查一次
        }

        StopFileSystemMonitoring();
    }

    private void StartMonitoringForExistingPaths()
    {
        var currentAppPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";

        foreach (var path in _candidatePaths)
        {
            // 修复：如果是游戏目录或其子目录，不进行排除检查
            var shouldSkipExcludeCheck = ShouldForceMonitorPath(path);

            // 早期排除：避免对系统路径创建监听器（但跳过游戏相关目录）
            if (!shouldSkipExcludeCheck && SaveDetectionConstants.ShouldExcludePath(path, currentAppPath))
            {
                Debug.WriteLine($"[GameSaveDetector] 跳过排除路径的监听: {path}");
                continue;
            }

            if (Directory.Exists(path))
            {
                CreateFileSystemWatcher(path);
                Debug.WriteLine($"[GameSaveDetector] 开始监听路径: {path}");
            }
            else
            {
                _pendingMonitorPaths.Add(path);
                Debug.WriteLine($"[GameSaveDetector] 路径不存在，加入待监听列表: {path}");
            }
        }

        Debug.WriteLine($"[GameSaveDetector] 立即监听 {_watchers.Count} 个已存在路径，{_pendingMonitorPaths.Count} 个路径待监听");
    }

    private void CheckAndStartPendingMonitors()
    {
        var pathsToMonitor = new List<string>(4); // 预分配容量提高性能
        var currentTime = DateTime.Now;
        var currentAppPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";

        // 使用更高效的遍历方式，避免ToList()的内存分配
        for (var i = _pendingMonitorPaths.Count - 1; i >= 0; i--)
        {
            var pendingPath = _pendingMonitorPaths[i];

            if (Directory.Exists(pendingPath))
            {
                // 修复：使用统一的路径排除逻辑（包含强制监听检查）
                if (ShouldExcludePath(pendingPath, currentAppPath))
                {
                    _pendingMonitorPaths.RemoveAt(i);
                    Debug.WriteLine($"[GameSaveDetector] 跳过排除路径的延迟监听: {pendingPath}");
                    continue;
                }

                if (!_pathFirstDetected.TryGetValue(pendingPath, out var detectedTime))
                {
                    _pathFirstDetected[pendingPath] = currentTime;
                    Debug.WriteLine($"[GameSaveDetector] 路径首次出现，加入延迟列表: {pendingPath}");
                }
                else if ((currentTime - detectedTime).TotalSeconds >= DELAY_SECONDS)
                {
                    pathsToMonitor.Add(pendingPath);
                    _pendingMonitorPaths.RemoveAt(i);
                    _pathFirstDetected.Remove(pendingPath);
                    Debug.WriteLine($"[GameSaveDetector] 延迟时间到，准备监听路径: {pendingPath}");
                }
            }
        }

        // 检查是否有路径因为检测到保存操作而需要提前开始监听
        if (SaveOperationCount >= SAVE_COUNT_THRESHOLD)
        {
            for (var i = _pendingMonitorPaths.Count - 1; i >= 0; i--)
            {
                var pendingPath = _pendingMonitorPaths[i];

                if (Directory.Exists(pendingPath))
                {
                    pathsToMonitor.Add(pendingPath);
                    _pendingMonitorPaths.RemoveAt(i);
                    _pathFirstDetected.Remove(pendingPath);
                    Debug.WriteLine($"[GameSaveDetector] 检测到足够的保存操作，提前监听路径: {pendingPath}");
                }
            }
        }

        // 批量创建监听器以提高效率
        foreach (var path in pathsToMonitor)
        {
            CreateFileSystemWatcher(path);
        }
    }

    private void CreateFileSystemWatcher(string path)
    {
        try
        {
            Debug.WriteLine($"[GameSaveDetector] 设置监听器监控路径: {path}");
            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                // 减少缓冲区大小以提高响应速度
                InternalBufferSize = 4096,
                // 通知所有变化
                NotifyFilter = NotifyFilters.FileName |
                              NotifyFilters.LastWrite |
                              NotifyFilters.Size |
                              NotifyFilters.Attributes |
                              NotifyFilters.CreationTime
            };

            // 监听所有相关事件
            watcher.Created += OnFileSystemChanged;
            watcher.Changed += OnFileSystemChanged;
            watcher.Renamed += OnFileSystemChanged;

            _watchers.Add(watcher);
            MonitoredPaths.Add(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameSaveDetector] 监听路径 {path} 失败: {ex.Message}");
        }
    }

    private void StopFileSystemMonitoring()
    {
        IsMonitoring = false;
        Debug.WriteLine($"[GameSaveDetector] 停止文件系统监听，清理 {_watchers.Count} 个监听器");

        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                Debug.WriteLine("[GameSaveDetector] 成功清理一个文件系统监听器");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameSaveDetector] 清理监听器时出错: {ex.Message}");
            }
        }

        _watchers.Clear();
        MonitoredPaths.Clear();

        // 清理缓存以释放内存
        _cachedVariants = null;
        _cachedLowerVariants = null;
        _pathFirstDetected.Clear();
        _pendingMonitorPaths.Clear();

        Debug.WriteLine("[GameSaveDetector] 文件系统监听已完全停止，缓存已清理");
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        if (!IsMonitoring) return;

        try
        {
            Debug.WriteLine($"[GameSaveDetector] 检测到文件系统变化: {e.ChangeType} - {e.FullPath}");

            // 处理重命名事件
            if (e is RenamedEventArgs renamedArgs)
            {
                Debug.WriteLine($"[GameSaveDetector] 文件重命名: {renamedArgs.OldFullPath} -> {renamedArgs.FullPath}");
                HandleFileChange(renamedArgs.FullPath);
            }
            else
            {
                // 检查文件是否可能是存档文件
                HandleFileChange(e.FullPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameSaveDetector] 处理文件系统变化时出错: {ex.Message}");
        }
    }

    #endregion

    #region File Detection Logic
    /// <summary>
    /// 处理文件变更，判断是否加入检测到的存档列表
    /// </summary>
    private void HandleFileChange(string path)
    {
        if (IsPotentialSaveFile(path))
        {
            lock (DetectedSavePaths)
            {
                if (!DetectedSavePaths.Contains(path))
                {
                    DetectedSavePaths.Add(path);
                    SaveOperationCount++;
                    Debug.WriteLine($"[GameSaveDetector] 发现新的存档文件: {path}");
                }
            }
        }
    }

    private bool IsPotentialSaveFile(string filePath)
    {
        try
        {
            // 全程使用 Span 避免内存分配 (Zero-Allocation)
            ReadOnlySpan<char> filePathSpan = filePath.AsSpan();
            ReadOnlySpan<char> fileNameSpan = Path.GetFileName(filePathSpan);
            ReadOnlySpan<char> directorySpan = Path.GetDirectoryName(filePathSpan);

            // Debug 输出无法避免 ToString，但在 Release 模式下或不开启 Debug 时影响较小
            // 也可以选择仅在特定条件下输出
            Debug.WriteLine($"[GameSaveDetector] 检查文件: {fileNameSpan.ToString()}");

            // 修复：首先检查是否在游戏目录下（游戏目录下的文件优先考虑）
            // StartsWith 对比 Span 和 String
            if (InstallationPath != null &&
                filePathSpan.StartsWith(InstallationPath.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[GameSaveDetector] 文件在游戏目录下: {filePath}");
                return true;
            }

            // 早期排除：检查文件路径是否在排除列表中
            // 注意：ShouldExcludePath 目前可能还需要 String，因为需要和 currentAppPath 对比
            // 如果能优化 ShouldExcludePath 更好，暂时保持 string 调用，但它是基于 StartsWith 的，相对快
            var currentAppPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            if (SaveDetectionConstants.ShouldExcludePath(filePathSpan, currentAppPath))
            {
                return false;
            }

            // 检查文件扩展名
            ReadOnlySpan<char> extensionSpan = Path.GetExtension(filePathSpan);
            if (extensionSpan.Length > 0)
            {
                // 去掉点号
                var extNoDot = extensionSpan.Slice(1);
                if (SaveDetectionConstants.IsSaveFileExtension(extNoDot))
                {
                    Debug.WriteLine($"[GameSaveDetector] 扩展名匹配: {fileNameSpan.ToString()}");
                    return true;
                }
            }

            // 检查文件名是否包含存档相关关键词
            if (SaveDetectionConstants.ContainsSaveKeyword(fileNameSpan))
            {
                Debug.WriteLine($"[GameSaveDetector] 文件名关键词匹配: {fileNameSpan.ToString()}");
                return true;
            }

            // 使用启发式关键字匹配 (传入 Span)
            if (MatchesHeuristicKeywords(directorySpan))
            {
                Debug.WriteLine($"[GameSaveDetector] 目录启发式匹配: {directorySpan.ToString()}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameSaveDetector] 检查文件出错: {filePath}, 错误: {ex.Message}");
            return false;
        }
    }

    private bool MatchesHeuristicKeywords(ReadOnlySpan<char> pathSpan)
    {
        // 0. 基础检查
        if (Galgame == null || pathSpan.IsEmpty) return false;

        // 1 & 2. 检查汉化和存档后缀
        if (CheckSuffixList(pathSpan, SaveDetectionConstants.ChineseLocalizationSuffixes)) return true;
        if (CheckSuffixList(pathSpan, SaveDetectionConstants.SaveDirectorySuffixPatterns)) return true;

        // 3. 游戏特定变体匹配 (Contains 检查)
        // 优化：使用 Span.IndexOf 避免 ToLower
        if (_cachedLowerVariants != null)
        {
            foreach (var variant in _cachedLowerVariants)
            {
                // MemoryExtensions.IndexOf (Span, String, StringComparison)
                if (pathSpan.IndexOf(variant, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.WriteLine($"[GameSaveDetector] 游戏变体包含匹配: '{variant}'");
                    return true;
                }
            }
        }

        // 4. 字符串相似度匹配 (路径分段)
        // 只有当前面都失败时才执行这个昂贵的操作
        if (_fuzzyMatchCoreVariants != null && _fuzzyMatchCoreVariants.Count > 0)
        {
            return CheckFuzzyMatch(pathSpan);
        }

        return false;
    }

    // 辅助方法：检查后缀列表 (Span 优化版)
    private bool CheckSuffixList(ReadOnlySpan<char> pathSpan, IEnumerable<string> suffixes)
    {
        foreach (var suffix in suffixes)
        {
            // 1. 直接检查 EndsWith (Span版本)
            if (pathSpan.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[GameSaveDetector] 后缀匹配 (EndsWith): '{suffix}'");
                return true;
            }

            // 2. 检查分隔符组合 (避免拼接字符串，使用 IndexOf)
            // 我们查找 suffix 在 path 中的位置
            int index;

            // 注意：pathSpan 在每次 Slice 后长度变化，这里我们需要在原始 pathSpan 上循环查找
            // 为了简单，我们使用 while loop 配合 Slice

            var tempSpan = pathSpan;
            int absoluteIndex = 0;

            while ((index = tempSpan.IndexOf(suffix, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                // index 是相对于 tempSpan 的位置
                int actualIndex = absoluteIndex + index;

                // 检查前一个字符
                if (actualIndex > 0)
                {
                    char prevChar = pathSpan[actualIndex - 1];
                    if (prevChar == '_' || prevChar == '-' || prevChar == '.')
                    {
                        Debug.WriteLine($"[GameSaveDetector] 后缀匹配 (Contains分隔符): '{suffix}'");
                        return true;
                    }
                }

                // 继续查找：从本次匹配位置之后开始
                int nextStart = index + 1;
                if (nextStart >= tempSpan.Length) break;

                tempSpan = tempSpan.Slice(nextStart);
                absoluteIndex += nextStart;
            }
        }
        return false;
    }

    // 辅助方法：模糊匹配 (Span 优化版)
    private bool CheckFuzzyMatch(ReadOnlySpan<char> pathSpan)
    {
        int start = 0;

        // 手动遍历路径分割符
        for (int i = 0; i <= pathSpan.Length; i++)
        {
            // 当遇到分隔符 或 字符串结束时
            if (i == pathSpan.Length || pathSpan[i] == Path.DirectorySeparatorChar || pathSpan[i] == Path.AltDirectorySeparatorChar)
            {
                if (i > start) // 确保段不为空
                {
                    // 获取当前段
                    ReadOnlySpan<char> segmentSpan = pathSpan.Slice(start, i - start);

                    // 使用无分配的 JaroWinkler 实现
                    if (_fuzzyMatchCoreVariants != null)
                    {
                        foreach (var coreVariant in _fuzzyMatchCoreVariants)
                        {
                            if (JaroWinkler(segmentSpan, coreVariant) > SIMILARITY_THRESHOLD)
                            {
                                Debug.WriteLine($"[GameSaveDetector] 模糊匹配成功: '{segmentSpan.ToString()}' ~= '{coreVariant}'");
                                return true;
                            }
                        }
                    }
                }
                start = i + 1;
            }
        }
        return false;
    }


    /// <summary>
    /// 高性能、无分配（小字符串）的 Jaro-Winkler 相似度计算
    /// </summary>
    /// <param name="s1">输入字符串 Span (任意大小写)</param>
    /// <param name="s2">比较基准字符串 (必须预先转为小写)</param>
    private static double JaroWinkler(ReadOnlySpan<char> s1, string s2)
    {
        if (s1.IsEmpty || string.IsNullOrEmpty(s2)) return 0;

        int n = s1.Length;
        int m = s2.Length;
        int matchWindow = Math.Max(n, m) / 2 - 1;

        // 对于路径片段，通常都很短，使用 stackalloc 避免堆分配
        // 如果超过 256 字符（极罕见），回退到数组
        Span<bool> s1Matches = n <= 256 ? stackalloc bool[n] : new bool[n];
        Span<bool> s2Matches = m <= 256 ? stackalloc bool[m] : new bool[m];

        int matches = 0;
        int transpositions = 0;

        for (int i = 0; i < n; i++)
        {
            int start = Math.Max(0, i - matchWindow);
            int end = Math.Min(i + matchWindow + 1, m);

            char c1 = char.ToLowerInvariant(s1[i]);

            for (int j = start; j < end; j++)
            {
                if (s2Matches[j]) continue;
                // s2 假定已经是小写
                if (c1 != s2[j]) continue;

                s1Matches[i] = true;
                s2Matches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0;

        int k = 0;
        for (int i = 0; i < n; i++)
        {
            if (!s1Matches[i]) continue;
            while (!s2Matches[k]) k++;
            if (char.ToLowerInvariant(s1[i]) != s2[k]) transpositions++;
            k++;
        }

        double jaro = (matches / (double)n + matches / (double)m + (matches - transpositions / 2.0) / matches) / 3.0;

        // Winkler prefix scale (standard is 0.1, max 4 chars)
        int prefix = 0;
        int maxPrefix = Math.Min(4, Math.Min(n, m));
        for (int i = 0; i < maxPrefix; i++)
        {
            if (char.ToLowerInvariant(s1[i]) == s2[i]) prefix++;
            else break;
        }

        return jaro + prefix * 0.1 * (1 - jaro);
    }

    #endregion

    #region Analysis & Filtering
    private bool ShouldStopEarly(List<string> detectedPaths, int fileThreshold, int confidenceThreshold)
    {
        if (detectedPaths.Count < fileThreshold)
        {
            Debug.WriteLine($"[GameSaveDetector] 未达到早停文件阈值: {detectedPaths.Count} < {fileThreshold}");
            return false;
        }

        // 统计每个目录的文件数量（优化性能）
        var directoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in detectedPaths)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                var normalizedDir = directory.TrimEnd('\\', '/');
                if (directoryCounts.ContainsKey(normalizedDir))
                    directoryCounts[normalizedDir]++;
                else
                    directoryCounts[normalizedDir] = 1;
            }
        }

        // 记录目录统计信息
        Debug.WriteLine($"[GameSaveDetector] 目录文件统计: {string.Join(", ", directoryCounts.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");

        // 如果有目录包含足够的文件，可以早停
        var shouldStop = directoryCounts.Any(kvp => kvp.Value >= confidenceThreshold);
        if (shouldStop)
        {
            var topDir = directoryCounts.OrderByDescending(kvp => kvp.Value).First();
            Debug.WriteLine($"[GameSaveDetector] 达到早停条件，目录 '{topDir.Key}' 包含 {topDir.Value} 个文件 (阈值: {confidenceThreshold})");
        }
        return shouldStop;
    }

    private List<string> FilterDetectedPaths()
    {
        var filteredPaths = new List<string>();
        lock (DetectedSavePaths)
        {
            // 去重和排序
            var uniquePaths = DetectedSavePaths.Distinct().ToList();
            Debug.WriteLine($"[GameSaveDetector] 过滤前路径数量: {DetectedSavePaths.Count}, 去重后: {uniquePaths.Count}");

            // 按文件大小和修改时间排序（最新的和较大的文件更可能是存档）
            var fileInfoList = uniquePaths.Select(path => new
            {
                Path = path,
                Info = new FileInfo(path),
                Score = CalculateSaveFileScore(path)
            })
            .OrderByDescending(x => x.Score)
            .Take(10) // 只返回前10个最可能的路径
            .ToList();

            Debug.WriteLine("[GameSaveDetector] 过滤后保留前10个最高评分路径:");
            foreach (var file in fileInfoList)
            {
                Debug.WriteLine($"[GameSaveDetector] - {file.Path} (评分: {file.Score:F1})");
            }

            filteredPaths.AddRange(fileInfoList.Select(x => x.Path));
        }
        return filteredPaths;
    }

    private double CalculateSaveFileScore(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var score = 0.0;

            // 文件大小适中的文件更可能是存档（1KB - 10MB）
            if (fileInfo.Length > 1024 && fileInfo.Length < 10 * 1024 * 1024) score += 30;
            else if (fileInfo.Length > 100 && fileInfo.Length < 100 * 1024 * 1024) score += 20;

            // 最近修改的文件更可能是存档
            var timeDiff = DateTime.Now - fileInfo.LastWriteTime;
            if (timeDiff.TotalMinutes < 10) score += 40;
            else if (timeDiff.TotalHours < 1) score += 30;
            else if (timeDiff.TotalDays < 1) score += 20;

            // 路径匹配启发式关键字
            if (MatchesHeuristicKeywords(filePath)) score += 30;

            return score;
        }
        catch
        {
            return 0;
        }
    }

    private string FindBestSaveDirectory(List<string> paths)
    {
        if (paths.Count == 0) return string.Empty;

        Debug.WriteLine($"[GameSaveDetector] 开始分析 {paths.Count} 个路径以找到最佳存档目录");

        // 优化：优先使用缓存的小写变体，避免重复计算 ToLowerInvariant
        var allVariants = GenerateAllVariants(Galgame!);
        var matchVariants = _cachedLowerVariants ?? allVariants.Select(v => v.ToLowerInvariant()).Distinct().ToList();

        var directoryScores = new Dictionary<string, DirectoryScoreInfo>();
        var currentAppPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";

        foreach (var path in paths)
        {
            var directory = Path.GetDirectoryName(path);
            // 如果路径本身就是目录（例如匹配到了游戏名的文件夹），则直接使用该路径，而不是取父级
            // 这是一个关键修复：防止像 "...\Roaming\妄想complete！" 这种正确路径被切成 "...\Roaming"
            if (Directory.Exists(path) && !File.Exists(path))
            {
                directory = path;
            }

            if (string.IsNullOrEmpty(directory)) continue;
            if (SaveDetectionConstants.ShouldExcludePath(directory.AsSpan(), currentAppPath)) continue;

            var normalizedDir = directory.ToLowerInvariant().TrimEnd('\\', '/');
            if (!directoryScores.ContainsKey(normalizedDir))
                directoryScores[normalizedDir] = new DirectoryScoreInfo { Directory = directory };

            var scoreInfo = directoryScores[normalizedDir];

            // 1. 基础分：稍微降低，不要让文件多的垃圾目录占据优势
            scoreInfo.TotalScore += 2;
            scoreInfo.FileCount++;

            // 2. 文件质量分
            scoreInfo.TotalScore += CalculateSaveFileScore(path) * 0.1;

            // 3. 核心修改：变体匹配评分 (这是最重要的指标)
            // 我们单独计算目录名的匹配程度，而不是整个长路径
            var variantScore = CalculateDirectoryVariantMatchScore(directory, matchVariants);
            scoreInfo.TotalScore += variantScore;

            if (variantScore > 0)
            {
                scoreInfo.MatchedVariants.AddRange(GetMatchedVariants(directory, matchVariants));
            }

            // 4. 路径结构微调
            scoreInfo.TotalScore += SaveDetectionConstants.GetPathStructureScore(directory.AsSpan());
        }

        if (directoryScores.Count == 0) return string.Empty;

        // 输出调试信息并选择最佳
        foreach (var kvp in directoryScores.OrderByDescending(kvp => kvp.Value.TotalScore))
        {
            var info = kvp.Value;
            Debug.WriteLine($"[GameSaveDetector] - {info.Directory} 总分: {info.TotalScore:F1} (匹配变体: {string.Join(",", info.MatchedVariants)})");
        }

        return directoryScores.OrderByDescending(kvp => kvp.Value.TotalScore).First().Value.Directory;
    }

    /// <summary>
    /// 计算目录路径与变体的匹配评分 (重命名以更准确反映用途)
    /// </summary>
    /// <param name="directory">目录路径</param>
    /// <param name="allLowerVariantsForContains">预先转换为小写的全量变体列表（用于Contains匹配）</param>
    private double CalculateDirectoryVariantMatchScore(string directory, List<string> allLowerVariantsForContains)
    {
        if (string.IsNullOrEmpty(directory) || allLowerVariantsForContains == null || allLowerVariantsForContains.Count == 0) return 0;

        var totalScore = 0.0;

        // 优化：使用 Path.GetFileName 获取最后一级目录名
        var dirName = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(dirName)) dirName = directory; // Fallback

        // 为了精确匹配比较（Case Insensitive），我们还是需要一个 string 或者 Span 比较
        var dirNameSpan = dirName.AsSpan();

        // 我们需要 directoryLower 仅用于 contains 检查，但我们可以用 IndexOf 替代
        var directorySpan = directory.AsSpan();

        // --- 精确/包含匹配 (使用所有生成的变体) ---
        foreach (var variantLower in allLowerVariantsForContains)
        {
            if (string.IsNullOrEmpty(variantLower)) continue;

            double scoreForThisVariant = 0;

            // 策略 A: 目录名完全等于游戏名/变体名 (最高优先级)
            // 使用 Equals 忽略大小写比较 Span 和 String
            if (dirNameSpan.Equals(variantLower.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                scoreForThisVariant = 500; // 极高分，几乎确信
                Debug.WriteLine($"[GameSaveDetector] 完美目录名匹配: {variantLower}");
            }
            // 策略 B: 路径中包含独立的游戏名目录
            else
            {
                // 使用 Span 优化的 IndexOf
                int index = directorySpan.IndexOf(variantLower, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    // 检查前后字符是否为分隔符或边界
                    bool startOk = (index == 0) || (directory[index - 1] == Path.DirectorySeparatorChar) || (directory[index - 1] == Path.AltDirectorySeparatorChar);
                    int end = index + variantLower.Length;
                    bool endOk = (end == directory.Length) || (directory[end] == Path.DirectorySeparatorChar) || (directory[end] == Path.AltDirectorySeparatorChar);

                    if (startOk && endOk)
                    {
                        scoreForThisVariant = 100; // 高分
                    }
                    else
                    {
                        scoreForThisVariant = 20; // 包含但不是独立段
                    }
                }
            }

            totalScore = Math.Max(totalScore, scoreForThisVariant);
        }

        // --- 模糊匹配 (使用核心变体进行Jaro-Winkler) ---
        if (_fuzzyMatchCoreVariants != null)
        {
            // 使用我们优化的无分配 JaroWinkler
            foreach (var coreVariant in _fuzzyMatchCoreVariants)
            {
                if (string.IsNullOrEmpty(coreVariant)) continue;
                // 注意：JaroWinkler 内部会处理大小写
                if (JaroWinkler(dirNameSpan, coreVariant) > 0.8)
                {
                    // 模糊匹配得分作为额外加分
                    totalScore = Math.Max(totalScore, 400);
                    Debug.WriteLine($"[GameSaveDetector] 模糊目录名匹配 (核心变体): {dirName} ~= {coreVariant}");
                }
            }
        }

        return totalScore;
    }

    /// <summary>
    /// 获取匹配的变体列表
    /// </summary>
    /// <param name="directory">目录路径</param>
    /// <param name="lowerVariants">预先转换为小写的变体列表</param>
    private List<string> GetMatchedVariants(string directory, List<string> lowerVariants)
    {
        var matched = new List<string>();
        if (string.IsNullOrEmpty(directory) || lowerVariants == null) return matched;

        var directoryLower = directory.ToLowerInvariant();
        foreach (var variantLower in lowerVariants)
        {
            if (!string.IsNullOrEmpty(variantLower) && directoryLower.Contains(variantLower))
            {
                matched.Add(variantLower);
            }
        }

        return matched;
    }

    /// <summary>
    /// 目录评分信息
    /// </summary>
    private class DirectoryScoreInfo
    {
        public string Directory { get; set; } = string.Empty;
        public double TotalScore { get; set; }
        public int FileCount { get; set; }
        public List<string> MatchedVariants { get; set; } = new();
    }

    #endregion
}
