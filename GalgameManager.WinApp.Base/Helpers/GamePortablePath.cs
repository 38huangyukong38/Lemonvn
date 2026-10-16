#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace GalgameManager.Helpers;

using TokenPathPair = (string Tag, string Path);

public readonly record struct TokenPathMapping
{
    private static readonly List<TokenPathPair> SysPaths;

    static TokenPathMapping()
    {
        SysPaths = [
            ( "%AppData%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ),
            ( "%LocalAppData%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) ),
            ( "%Documents%", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) ),
            ( "%UserProfile%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ),
        ];

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            SysPaths.Add(("%LocalLow%", localAppData.Replace("Local", "LocalLow")));
        }
    }

    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Root { get; init;  }

    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Tag { get; init; }

    private TokenPathPair ? GetRootMapping() =>
        string.IsNullOrWhiteSpace(Root) ? null : (Tag, Root);

    private IEnumerable<TokenPathPair> GetAllMappings()
    {
        foreach(TokenPathPair pathMapping in SysPaths)
            yield return pathMapping;

        if (GetRootMapping() is { } mRoot) yield return mRoot;
    }

    public IEnumerable<(string Tag, string Path)> GetTagToPathMapping() => GetAllMappings()
        .OrderByDescending(m => m.Tag.Length);
    public IEnumerable<(string Path, string Tag)> GetPathToTagMapping() => GetAllMappings()
        .OrderByDescending(m => m.Path.Length)
        .Select(m => (m.Path, m.Tag));
}

public record struct GamePortablePath
{
    [JsonInclude] public TokenPathMapping Mapping { get; init;  }
    [JsonInclude] public string? ParsedPath { get; init;  }

    private string? _cachedPath { get; set; }

    [return: NotNullIfNotNull(nameof(path))]
    public static GamePortablePath? Create(string? path, TokenPathMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        // 1. 展开任何可能的 tag
        path = _ToPath(path, mapping);

        // 2. 正则化相对路径
        path = _ToDisplay(path, mapping);

        return new GamePortablePath { ParsedPath = path, Mapping = mapping };
    }

    /// <summary>
    /// 专门用来初始化 GameRoot 这个特例。
    /// </summary>
    /// <param name="path">任意一个路径：%Documents% or C:\\114514 </param>
    /// <param name="gameRoot">游戏的根目录</param>
    /// <returns></returns>
    [return: NotNullIfNotNull(nameof(path))]
    public static GamePortablePath? Create(string? path, string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return Create(path, new TokenPathMapping { Root = gameRoot, Tag = "%GameRoot%", });
    }

    /// <summary>
    /// 返回伪绝对路径（含标签）
    /// </summary>
    [return: NotNullIfNotNull(nameof(ParsedPath))]
    public string? ToDisplay() => ParsedPath;

    [return: NotNullIfNotNull(nameof(anyPath))]
    private static string? _ToDisplay(string? anyPath, TokenPathMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(anyPath)) return null;
        foreach (var (fullpath, tag) in mapping.GetPathToTagMapping())
        {
            anyPath = anyPath.Replace(fullpath, tag, StringComparison.OrdinalIgnoreCase);
        }

        return anyPath;
    }

    /// <summary>
    /// 返回真实的绝对路径
    /// </summary>
    [return: NotNullIfNotNull(nameof(ParsedPath))]
    public override string? ToString() => ToPath();

    /// <summary>
    /// 返回真实的绝对路径
    /// </summary>
    [return: NotNullIfNotNull(nameof(gamePortablePath.ParsedPath))]
    public static implicit operator string?(GamePortablePath gamePortablePath) => gamePortablePath.ToPath();


    /// <summary>
    /// 返回真实的绝对路径
    /// </summary>
    [return: NotNullIfNotNull(nameof(ParsedPath))]
    public string? ToPath() => _cachedPath ??= _ToPath(ParsedPath, Mapping);

    [return: NotNullIfNotNull(nameof(anyPath))]
    private static string? _ToPath(string? anyPath, TokenPathMapping anyMapping)
    {
        if (anyPath is null) return null;
        var path = anyPath;

        foreach (var (tag, fullpath) in anyMapping.GetTagToPathMapping())
        {
            if (path.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
            {
                if (path.Length == tag.Length) return fullpath;

                var nextChar = path[tag.Length];
                if (nextChar == Path.DirectorySeparatorChar || nextChar == Path.AltDirectorySeparatorChar)
                {
                    var relative = path.Substring(tag.Length + 1);
                    return Path.Join(fullpath, relative);
                }
            }
        }

        return path;
    }

    public GamePortablePath? Relocated(string newBasePath) => Create(ToPath(), Mapping with
    {
        Root = newBasePath,
    });
}
