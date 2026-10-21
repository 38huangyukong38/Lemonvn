using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LemonVN.Infrastructure.Uninstall;

namespace GalgameManager.Test.Services;

[TestFixture]
[NonParallelizable]
public sealed class UninstallSafetyTest
{
    private string _root = null!, _app = null!, _data = null!;
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(TestEnvironmentSetup.Root, "UninstallSynthetic", Guid.NewGuid().ToString("N"));
        _app = Path.Combine(_root, "程序 with spaces"); _data = Path.Combine(_root, "合成数据");
        Directory.CreateDirectory(_app);
        _files.Clear();
        foreach (string name in new[] { "LemonVN.exe", "Uninstall.exe", "LemonVN.dll", "UninstallWorker.ps1", "LICENSE", "NOTICE", "Assets/icon.png" })
        {
            byte[] bytes = Encoding.UTF8.GetBytes(name is "LemonVN.exe" or "Uninstall.exe" ? "Synthetic identical non-executable host" : "Synthetic owned file " + name);
            _files.Add(name, bytes);
            string path = Path.Combine(_app, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes);
        }
        SaveManifest();
    }
    private void SaveManifest(IEnumerable<object>? rows = null) => File.WriteAllText(Path.Combine(_app, "FILE_MANIFEST.json"),
        JsonSerializer.Serialize(new { application = "LemonVN", files = rows ?? _files.Select(pair =>
            (object)new { path = pair.Key, bytes = pair.Value.Length, sha256 = Convert.ToHexString(SHA256.HashData(pair.Value)) }) }));
    private void CreateData(string application = "LemonVN.Desktop")
    {
        Directory.CreateDirectory(Path.Combine(_data, "Settings")); Directory.CreateDirectory(Path.Combine(_data, "Covers"));
        Directory.CreateDirectory(Path.Combine(_data, "Backups"));
        File.WriteAllText(Path.Combine(_data, "Settings/settings.json"), JsonSerializer.Serialize(new { Application = application, FormatVersion = 1 }));
        File.WriteAllText(Path.Combine(_data, "lemonvn.db"), "Synthetic database sentinel, not a real collection");
        File.WriteAllText(Path.Combine(_data, "Covers/cover.jpg"), "Synthetic cover sentinel");
        File.WriteAllText(Path.Combine(_data, "Backups/backup.zip"), "Synthetic backup sentinel");
    }
    private UninstallPlan Plan(bool deleteData) => UninstallService.Prepare(_app, _data, deleteData) with
        { ParentProcessId = int.MaxValue, ParentStartedUtcTicks = 1 };

    [Test]
    public void AliasLaunch_UsesOriginalResourceIdentity_WithoutShellOrElevation()
    {
        ProcessStartInfo start = UninstallService.CreateUiStartInfo(_app);
        Assert.That(start.FileName, Is.EqualTo(Path.Combine(_app, "LemonVN.exe")));
        Assert.That(start.ArgumentList, Is.EqualTo(new[] { "--uninstall" }));
        Assert.That(start.WorkingDirectory, Is.EqualTo(_app));
        Assert.That(start.UseShellExecute, Is.False);
        Assert.That(start.Verb, Is.Empty);
        Assert.That(UninstallService.IsUninstallRequest(start.ArgumentList.ToArray()), Is.True);
        File.Delete(Path.Combine(_app, "LemonVN.exe"));
        Assert.Throws<FileNotFoundException>(() => UninstallService.CreateUiStartInfo(_app));
    }

    [TestCase("")]
    [TestCase("--uninstall-anything")]
    [TestCase("--UNINSTALL")]
    [TestCase("other --uninstall")]
    public void UninstallMode_RequiresAnExactSingleArgument(string argument) =>
        Assert.That(UninstallService.IsUninstallRequest(argument.Split(' ', StringSplitOptions.RemoveEmptyEntries)), Is.False);

    [Test]
    public void PreparingOrCancelling_AccessesNoDatabaseAndDeletesNothing()
    {
        UninstallPlan plan = Plan(false);
        Assert.That(plan.DeleteData, Is.False);
        Assert.That(Directory.Exists(_data), Is.False);
        Assert.That(plan.Files, Has.Count.EqualTo(_files.Count + 1));
        Assert.That(File.Exists(Path.Combine(_app, "LemonVN.exe")), Is.True);
        Assert.That(UninstallService.IsUninstallExecutable(Path.Combine(_app, "Uninstall.exe")), Is.True);
        Assert.That(UninstallService.IsUninstallExecutable(Path.Combine(_app, "LemonVN.exe")), Is.False);
    }

    [TestCase("../outside.txt")]
    [TestCase("dir/../../outside.txt")]
    [TestCase("C:/outside.txt")]
    [TestCase("/outside.txt")]
    [TestCase("dir\\outside.txt")]
    [TestCase("file.txt:stream")]
    [TestCase("dir/CON.txt")]
    [TestCase("dir/file.")]
    public void MaliciousManifestReference_IsRejectedBeforeAnyRemoval(string relative)
    {
        SaveManifest(new[] { new { path = relative, bytes = 0, sha256 = new string('A', 64) } });
        Assert.Throws<InvalidDataException>(() => Plan(false));
        Assert.That(File.Exists(Path.Combine(_app, "LemonVN.exe")), Is.True);
    }

    [Test]
    public void ChangedApplicationOrWrongDataIdentity_IsRejectedAndPreserved()
    {
        File.WriteAllText(Path.Combine(_app, "LemonVN.dll"), "Changed after extraction");
        Assert.Throws<InvalidDataException>(() => Plan(false));
        File.WriteAllBytes(Path.Combine(_app, "LemonVN.dll"), _files["LemonVN.dll"]);
        CreateData("AnotherApplication");
        Assert.Throws<InvalidDataException>(() => Plan(true));
        Assert.That(Plan(false).DeleteData, Is.False);
        Assert.That(File.ReadAllText(Path.Combine(_data, "lemonvn.db")), Does.StartWith("Synthetic"));
    }

    [Test]
    public void BroadRootsRepositoriesAndOverlappingData_AreRejected()
    {
        Assert.Throws<InvalidDataException>(() => UninstallService.ValidateRoot(Path.GetPathRoot(_app)!));
        Assert.Throws<InvalidDataException>(() => UninstallService.ValidateRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
        Assert.Throws<InvalidDataException>(() => UninstallService.Prepare(_app, _app, true));
        Directory.CreateDirectory(Path.Combine(_app, ".git"));
        Assert.Throws<InvalidDataException>(() => Plan(false));
        Assert.That(File.Exists(Path.Combine(_app, "LemonVN.exe")), Is.True);
    }

    [Test]
    public void ChangedWorkerAfterConfirmation_IsNeverLaunched()
    {
        UninstallPlan plan = Plan(false);
        File.WriteAllText(Path.Combine(_app, "UninstallWorker.ps1"), "changed helper");
        Assert.Throws<InvalidDataException>(() => UninstallService.LaunchConfirmed(plan));
        Assert.That(Directory.Exists(Path.Combine(Path.GetTempPath(), "LemonVN.Uninstall", plan.Nonce)), Is.False);
        Assert.That(File.Exists(Path.Combine(_app, "LemonVN.exe")), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task JunctionCannotRedirectApplicationRemoval_ToAnotherDirectory(bool insertedAfterConfirmation)
    {
        UninstallPlan approved = Plan(false);
        string outside = Path.Combine(_root, "untouched"); Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "sentinel.txt"), "Keep outside target");
        string link = Path.Combine(_app, "linked");
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell/v1.0/powershell.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment["PSModulePath"] = Path.Combine(Path.GetDirectoryName(start.FileName)!, "Modules");
        start.Environment["LEMONVN_LINK_TEST_PATH"] = link; start.Environment["LEMONVN_LINK_TEST_TARGET"] = outside;
        foreach (string value in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "$ErrorActionPreference='Stop';New-Item -ItemType Junction -Path $env:LEMONVN_LINK_TEST_PATH -Target $env:LEMONVN_LINK_TEST_TARGET|Out-Null" }) start.ArgumentList.Add(value);
        using Process process = Process.Start(start)!;
        Task<string> error = process.StandardError.ReadToEndAsync(), output = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.That(process.ExitCode, Is.Zero, await error);
        await output;
        try
        {
            if (insertedAfterConfirmation) Assert.That(await RunWorker(approved), Is.EqualTo(1));
            else Assert.Throws<InvalidDataException>(() => Plan(false));
            Assert.That(File.ReadAllText(Path.Combine(outside, "sentinel.txt")), Is.EqualTo("Keep outside target"));
        }
        finally { new DirectoryInfo(link).Delete(); } // Unlink only this owned test junction.
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RealWorker_RemovesOnlyApprovedSyntheticApplication_AndHonorsDataChoice(bool deleteData)
    {
        CreateData();
        string unknown = Path.Combine(_app, "my-personal-note.txt"); File.WriteAllText(unknown, "Keep unlisted file");
        string sibling = Path.Combine(_root, "PotatoGuard"); Directory.CreateDirectory(sibling);
        File.WriteAllText(Path.Combine(sibling, "lemonvn.db"), "Unrelated sentinel");
        UninstallPlan plan = Plan(deleteData);
        var dataBefore = Directory.GetFiles(_data, "*", SearchOption.AllDirectories).ToDictionary(p => p, p => File.ReadAllBytes(p));
        int code = await RunWorker(plan);
        Assert.That(code, Is.Zero);
        Assert.That(File.Exists(Path.Combine(_app, "LemonVN.exe")), Is.False);
        Assert.That(File.Exists(Path.Combine(_app, "Uninstall.exe")), Is.False);
        Assert.That(File.ReadAllText(unknown), Is.EqualTo("Keep unlisted file"));
        Assert.That(File.ReadAllText(Path.Combine(sibling, "lemonvn.db")), Is.EqualTo("Unrelated sentinel"));
        Assert.That(Directory.Exists(_data), Is.EqualTo(!deleteData));
        if (!deleteData)
            foreach ((string path, byte[] bytes) in dataBefore) Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task WorkerRejectsChangedPlanOrPayload_BeforeDeletingApplicationOrData(bool corruptPlanHash)
    {
        CreateData();
        UninstallPlan plan = Plan(true);
        if (!corruptPlanHash) File.WriteAllText(Path.Combine(_app, "LemonVN.dll"), "Modified after user confirmation");
        Assert.That(await RunWorker(plan, corruptPlanHash), Is.EqualTo(1));
        Assert.That(File.Exists(Path.Combine(_app, "LemonVN.exe")), Is.True);
        Assert.That(File.Exists(Path.Combine(_data, "lemonvn.db")), Is.True);
    }

    private static async Task<int> RunWorker(UninstallPlan plan, bool corruptHash = false)
    {
        string session = Path.Combine(Path.GetTempPath(), "LemonVN.Uninstall", plan.Nonce);
        Assert.That(Directory.Exists(session), Is.False);
        Directory.CreateDirectory(session);
        string planFile = Path.Combine(session, "plan.json"), script = Path.Combine(session, "worker.ps1");
        File.WriteAllBytes(planFile, JsonSerializer.SerializeToUtf8Bytes(plan));
        string source = FindRepositoryFile("GalgameManager/Infrastructure/Uninstall/UninstallWorker.ps1");
        File.WriteAllText(script, File.ReadAllText(source), new UTF8Encoding(true));
        string hash = corruptHash ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(planFile)));
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell/v1.0/powershell.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment["PSModulePath"] = Path.Combine(Path.GetDirectoryName(start.FileName)!, "Modules");
        foreach (string value in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", script, "-PlanPath", planFile, "-PlanSha256", hash, "-Quiet" }) start.ArgumentList.Add(value);
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(), errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        TestContext.Out.WriteLine(await output); TestContext.Out.WriteLine(await errors);
        return process.ExitCode;
    }
    private static string FindRepositoryFile(string relative)
    {
        for (DirectoryInfo? dir = new(TestContext.CurrentContext.TestDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "GalgameManager.sln"))) return Path.Combine(dir.FullName, relative);
        throw new DirectoryNotFoundException("Cannot locate the worker source.");
    }
}
