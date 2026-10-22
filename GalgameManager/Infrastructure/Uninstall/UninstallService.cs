using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LemonVN.Infrastructure.Uninstall;

public sealed record UninstallFile(string Path, long Bytes, string Sha256);
public sealed record UninstallPlan(int FormatVersion, string Application, string Nonce, string ApplicationRoot,
    string DataRoot, bool DeleteData, IReadOnlyList<UninstallFile> Files, int ParentProcessId, long ParentStartedUtcTicks)
{
    public string PayloadPrefix { get; init; } = string.Empty;
}

// ZIP removal is deliberately separate from collection/backup/storage services.
// Preparing a plan only reads the distribution manifest; it never opens LiteDB.
public static class UninstallService
{
    public static bool IsUninstallExecutable(string? processPath) =>
        string.Equals(Path.GetFileName(processPath), "Uninstall.exe", StringComparison.OrdinalIgnoreCase);

    public static bool IsUninstallRequest(string[] args) =>
        args.Length == 1 && args[0] == "--uninstall";

    public static ProcessStartInfo CreateUiStartInfo(string applicationRoot)
    {
        string executable = Path.Combine(Path.GetFullPath(applicationRoot), "LemonVN.exe");
        if (!File.Exists(executable)) throw new FileNotFoundException("Extract the complete LemonVN ZIP before uninstalling.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = applicationRoot };
        start.ArgumentList.Add("--uninstall");
        return start;
    }

    public const string OrganizedLayout = "app-subdirectory-v1";

    public static string FindApplicationRoot(string runtimeDirectory)
    {
        string runtime = Path.TrimEndingDirectorySeparator(Path.GetFullPath(runtimeDirectory));
        if (!string.Equals(Path.GetFileName(runtime), "app", StringComparison.OrdinalIgnoreCase)) return runtime;
        string parent = Path.GetDirectoryName(runtime)!;
        string manifest = Path.Combine(parent, "FILE_MANIFEST.json");
        RejectLinks(manifest);
        if (!File.Exists(manifest) || new FileInfo(manifest).Length > 2 * 1024 * 1024) return runtime;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifest));
        JsonElement root = document.RootElement;
        return root.TryGetProperty("application", out var application) && application.GetString() == "LemonVN" &&
            root.TryGetProperty("layout", out var layout) && layout.GetString() == OrganizedLayout ? parent : runtime;
    }

    private static string ValidatePayloadPrefix(string prefix) => prefix is "" or "app/" ? prefix :
        throw new InvalidDataException("Unsupported application payload directory.");

    public static UninstallPlan Prepare(string applicationRoot, string dataRoot, bool deleteData)
    {
        string app = ValidateRoot(applicationRoot);
        string data = ValidateRoot(dataRoot);
        if (Inside(app, data) || Inside(data, app))
            throw new InvalidDataException("Application and data directories must not overlap.");
        string manifestPath = Path.Combine(app, "FILE_MANIFEST.json");
        RejectLinks(manifestPath);
        if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 2 * 1024 * 1024)
            throw new InvalidDataException("A complete extracted LemonVN ZIP with FILE_MANIFEST.json is required.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        JsonElement root = document.RootElement;
        if (root.GetProperty("application").GetString() != "LemonVN")
            throw new InvalidDataException("This is not a LemonVN distribution manifest.");
        string prefix = string.Empty;
        if (root.TryGetProperty("layout", out var layout))
        {
            if (layout.GetString() != OrganizedLayout) throw new InvalidDataException("Unsupported distribution layout.");
            prefix = "app/";
        }
        var files = new List<UninstallFile>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement row in root.GetProperty("files").EnumerateArray())
        {
            if (files.Count >= 4000) throw new InvalidDataException("Too many distribution files.");
            string name = row.GetProperty("path").GetString() ?? string.Empty;
            string full = ResolveOwnedFile(app, name);
            if (!names.Add(name)) throw new InvalidDataException("Duplicate distribution file.");
            long bytes = row.GetProperty("bytes").GetInt64();
            string hash = row.GetProperty("sha256").GetString() ?? string.Empty;
            if (bytes < 0 || bytes > 64 * 1024 * 1024 || !Regex.IsMatch(hash, "\\A[0-9A-Fa-f]{64}\\z"))
                throw new InvalidDataException("Invalid distribution fingerprint.");
            RejectLinks(full);
            if (File.Exists(full) && (new FileInfo(full).Length != bytes || Sha(full) != hash.ToUpperInvariant()))
                throw new InvalidDataException("An application file differs from the ZIP manifest; removal was not started.");
            files.Add(new(name, bytes, hash.ToUpperInvariant()));
        }
        foreach (string name in new[] { "LemonVN.exe", "Uninstall.exe", prefix + "LemonVN.exe", prefix + "LemonVN.dll", prefix + "UninstallWorker.ps1", "LICENSE", "NOTICE" })
            if (!names.Contains(name) || !File.Exists(ResolveOwnedFile(app, name)))
                throw new InvalidDataException("Required LemonVN distribution files are missing.");
        if (names.Contains("FILE_MANIFEST.json")) throw new InvalidDataException("The manifest cannot list itself.");
        files.Add(new("FILE_MANIFEST.json", new FileInfo(manifestPath).Length, Sha(manifestPath)));
        // Both root entries use the same reviewed host/launcher bytes.
        if (Sha(Path.Combine(app, "Uninstall.exe")) != Sha(Path.Combine(app, "LemonVN.exe")))
            throw new InvalidDataException("The uninstall entry point does not match this application.");
        RejectLinksInTree(app);
        if (deleteData) ValidateDataOwner(data);
        AssertApplicationClosed(app, deleteData);
        using var process = Process.GetCurrentProcess();
        return new(1, "LemonVN.Desktop", Guid.NewGuid().ToString("N"), app, data, deleteData,
            files.AsReadOnly(), process.Id, process.StartTime.ToUniversalTime().Ticks) { PayloadPrefix = prefix };
    }

    public static string ValidateRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal))
            throw new InvalidDataException("A local absolute directory is required.");
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var forbidden = new[] { Path.GetPathRoot(full), Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
        if (forbidden.Where(p => !string.IsNullOrWhiteSpace(p)).Any(p => Inside(p!, full)))
            throw new InvalidDataException("Refusing to remove a system/profile/shared directory or its ancestor.");
        if (File.Exists(Path.Combine(full, "GalgameManager.sln")) || File.Exists(Path.Combine(full, ".git")) || Directory.Exists(Path.Combine(full, ".git")))
            throw new InvalidDataException("Source repositories cannot be uninstalled.");
        RejectLinks(full);
        return full;
    }

    public static string ResolveOwnedFile(string root, string relative)
    {
        string[] parts = relative.Split('/');
        if (relative.Length == 0 || relative.Contains('\\') || relative.Contains(':') ||
            parts.Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith('.') || p.EndsWith(' ') ||
                p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Regex.IsMatch(p, @"\A(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?\z", RegexOptions.IgnoreCase)))
            throw new InvalidDataException("Unsafe distribution file reference.");
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase) || !Inside(full, root))
            throw new InvalidDataException("Distribution file leaves its application directory.");
        return full;
    }

    public static void ValidateDataOwner(string data)
    {
        ValidateRoot(data);
        if (!Directory.Exists(data)) return;
        RejectLinksInTree(data);
        if (!Directory.EnumerateFileSystemEntries(data).Any()) return;
        foreach (string name in new[] { "settings.json", "license-consent.json" })
        {
            string marker = Path.Combine(data, "Settings", name);
            if (!File.Exists(marker) || new FileInfo(marker).Length > 16 * 1024) continue;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(marker));
                if (doc.RootElement.TryGetProperty("Application", out JsonElement app) && app.GetString() == "LemonVN.Desktop" &&
                    doc.RootElement.TryGetProperty("FormatVersion", out JsonElement format) && format.GetInt32() == 1) return;
            }
            catch (JsonException) { /* A corrupt marker cannot authorize data deletion. */ }
        }
        throw new InvalidDataException("Local data identity is not recognized; choose to keep data and remove application files only.");
    }

    public static void RejectLinks(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked files or directories are not supported for uninstall.");
    }
    private static void RejectLinksInTree(string root)
    {
        if (!Directory.Exists(root)) return;
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out string? directory))
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked uninstall target.");
                if (Path.GetFileName(path) == ".git") throw new InvalidDataException("Git history must be preserved.");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
            }
    }
    public static void AssertApplicationClosed(string applicationRoot, bool deleteData)
    {
        foreach (Process process in Process.GetProcessesByName("LemonVN"))
        {
            using (process)
            {
                // The uninstall window now runs under LemonVN.exe for resource identity.
                if (process.Id == Environment.ProcessId) continue;
                if (deleteData) throw new InvalidOperationException("Close every LemonVN window before deleting shared local data.");
                try
                {
                    if (process.MainModule?.FileName is { } executable && Inside(executable, applicationRoot))
                        throw new InvalidOperationException("Close LemonVN before uninstalling this copy.");
                }
                catch (System.ComponentModel.Win32Exception) { throw new InvalidOperationException("Cannot verify a running LemonVN instance; close it first."); }
            }
        }
    }
    private static bool Inside(string child, string parent) => child.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
        child.StartsWith(Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static string Sha(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    // Call only after the WinUI confirmation. The built-in Windows PowerShell
    // helper runs after this process exits, so it can remove the EXE/DLLs safely.
    public static void LaunchConfirmed(UninstallPlan plan)
    {
        string systemPowerShell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        if (!File.Exists(systemPowerShell)) throw new FileNotFoundException("Windows PowerShell is unavailable; no files were removed.");
        string workerRelative = ValidatePayloadPrefix(plan.PayloadPrefix) + "UninstallWorker.ps1";
        string sourceWorker = ResolveOwnedFile(plan.ApplicationRoot, workerRelative);
        RejectLinks(sourceWorker);
        byte[] workerBytes = File.ReadAllBytes(sourceWorker);
        UninstallFile workerFingerprint = plan.Files.Single(file => file.Path == workerRelative);
        if (workerBytes.LongLength != workerFingerprint.Bytes || Convert.ToHexString(SHA256.HashData(workerBytes)) != workerFingerprint.Sha256)
            throw new InvalidDataException("The uninstall helper changed after confirmation; no removal was started.");
        string session = Path.Combine(Path.GetTempPath(), "LemonVN.Uninstall", plan.Nonce);
        RejectLinks(session);
        if (Directory.Exists(session)) throw new IOException("Uninstall session already exists.");
        Directory.CreateDirectory(session);
        string script = Path.Combine(session, "worker.ps1");
        string planPath = Path.Combine(session, "plan.json");
        try
        {
            File.WriteAllText(script, System.Text.Encoding.UTF8.GetString(workerBytes).TrimStart('\uFEFF'), new System.Text.UTF8Encoding(true));
            File.WriteAllBytes(planPath, JsonSerializer.SerializeToUtf8Bytes(plan));
            var start = new ProcessStartInfo(systemPowerShell) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            // Do not inherit PowerShell 7's incompatible/user module search path.
            start.Environment["PSModulePath"] = Path.Combine(Path.GetDirectoryName(systemPowerShell)!, "Modules");
            foreach (string value in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", script,
                "-PlanPath", planPath, "-PlanSha256", Sha(planPath) }) start.ArgumentList.Add(value);
            // Inherit the test-only data/temp root, never a PotatoVN path.
            using Process worker = Process.Start(start) ?? throw new IOException("Cannot start the uninstall helper.");
            // The helper writes a ready receipt only after parsing/validating its
            // parameters. Do not exit into a silently rejected execution policy.
            if (!SpinWait.SpinUntil(() => File.Exists(Path.Combine(session, "ready")) || worker.HasExited, TimeSpan.FromSeconds(10)) || worker.HasExited)
                throw new IOException("The uninstall helper could not start. No removal was performed; check the system script policy.");
        }
        catch
        {
            // Only this newly created session; never the install/data directory.
            foreach (string file in new[] { script, planPath }) if (File.Exists(file)) File.Delete(file);
            if (!Directory.EnumerateFileSystemEntries(session).Any()) Directory.Delete(session);
            throw;
        }
    }
}
