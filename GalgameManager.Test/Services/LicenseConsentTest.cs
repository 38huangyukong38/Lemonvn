using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LemonVN;
using LemonVN.Services;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class LicenseConsentTest
{
    private string _root = null!;
    private string _application = null!;
    private AppDataPathService _paths = null!;
    private LicenseConsentService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(TestEnvironmentSetup.Root, "LicenseConsent", Guid.NewGuid().ToString("N"));
        _application = Path.Combine(_root, "Application");
        _paths = new AppDataPathService(Path.Combine(_root, "LocalData"));
        _service = new LicenseConsentService(_paths);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(_application, LicenseTerms.SdkRelativePath))!);
        string repository = RepositoryRoot();
        foreach (string relative in new[] { LicenseTerms.SdkRelativePath, LicenseTerms.AppSdkRelativePath, LicenseTerms.WinRtMitRelativePath, LicenseTerms.WindowsMlRelativePath })
        {
            string target = Path.Combine(_application, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(repository, "docs", relative), target);
        }
        File.WriteAllText(Path.Combine(_application, "SDK_END_USER_TERMS_DRAFT.md"), "Synthetic SDK draft for isolated tests.\nNot publisher approval.");
        File.WriteAllText(Path.Combine(_application, "LICENSE"), "Synthetic Apache-2.0 source notice.");
    }

    [Test]
    public void CheckingFirstLaunch_DoesNotCreateDataOrOpenDatabase()
    {
        Assert.That(_service.IsCurrentAccepted(LicenseTerms.Load(_application)), Is.False);
        Assert.That(Directory.Exists(_paths.RootPath), Is.False);
    }

    [Test]
    public async Task Decline_DoesNotInitializeBusinessOrCreateAnyLocalData()
    {
        bool started = false;
        bool accepted = await _service.RunAsync(LicenseTerms.Load(_application), () => Task.FromResult(false), () =>
        {
            started = true;
            return Task.CompletedTask;
        });
        Assert.Multiple(() =>
        {
            Assert.That(accepted, Is.False);
            Assert.That(started, Is.False);
            Assert.That(Directory.Exists(_paths.RootPath), Is.False);
        });
    }

    [Test]
    public async Task Accept_SavesOnlyMinimalUtcAcknowledgementBeforeBusinessStarts()
    {
        var terms = LicenseTerms.Load(_application);
        DateTimeOffset before = DateTimeOffset.UtcNow;
        bool started = false;
        Assert.That(await _service.RunAsync(terms, () => Task.FromResult(true), () =>
        {
            Assert.That(_service.IsCurrentAccepted(terms), Is.True);
            started = true;
            return Task.CompletedTask;
        }), Is.True);
        using JsonDocument record = JsonDocument.Parse(File.ReadAllText(_service.FilePath));
        var document = record.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(started, Is.True);
            Assert.That(document.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(
                new[] { "FormatVersion", "Application", "TermsHash", "AcceptedAtUtc" }));
            Assert.That(document.GetProperty("Application").GetString(), Is.EqualTo(AppIdentity.PackageName));
            Assert.That(document.GetProperty("TermsHash").GetString(), Is.EqualTo(terms.ContentHash));
            Assert.That(document.GetProperty("AcceptedAtUtc").GetDateTimeOffset(), Is.InRange(before, DateTimeOffset.UtcNow));
            Assert.That(document.GetProperty("AcceptedAtUtc").GetDateTimeOffset().Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(File.Exists(_paths.DatabasePath), Is.False);
            Assert.That(Directory.Exists(_paths.BackupsPath), Is.False);
            Assert.That(Directory.GetFiles(_paths.SettingsPath), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task SameTermsOnRestart_SkipsPromptAndDoesNotRewriteAcknowledgement()
    {
        var terms = LicenseTerms.Load(_application);
        _service.RecordAcceptance(terms);
        byte[] previous = File.ReadAllBytes(_service.FilePath);
        bool prompted = false, started = false;
        var restarted = new LicenseConsentService(_paths);
        Assert.That(await restarted.RunAsync(LicenseTerms.Load(_application), () =>
        {
            prompted = true;
            return Task.FromResult(false);
        }, () => { started = true; return Task.CompletedTask; }), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(prompted, Is.False);
            Assert.That(started, Is.True);
            Assert.That(File.ReadAllBytes(_service.FilePath), Is.EqualTo(previous));
        });
    }

    [Test]
    public async Task ChangedTerms_RequireNewChoice_AndDeclinePreservesPreviousRecordAndDatabase()
    {
        _service.RecordAcceptance(LicenseTerms.Load(_application));
        File.WriteAllText(_paths.DatabasePath, "Synthetic existing database sentinel, not user data.");
        byte[] oldRecord = File.ReadAllBytes(_service.FilePath), oldDatabase = File.ReadAllBytes(_paths.DatabasePath);
        File.AppendAllText(Path.Combine(_application, "SDK_END_USER_TERMS_DRAFT.md"), "\nChanged material condition.");
        var changed = LicenseTerms.Load(_application);
        bool prompted = false, started = false;
        Assert.That(await _service.RunAsync(changed, () => { prompted = true; return Task.FromResult(false); },
            () => { started = true; return Task.CompletedTask; }), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(prompted, Is.True);
            Assert.That(started, Is.False);
            Assert.That(File.ReadAllBytes(_service.FilePath), Is.EqualTo(oldRecord));
            Assert.That(File.ReadAllBytes(_paths.DatabasePath), Is.EqualTo(oldDatabase));
        });
        _service.RecordAcceptance(changed);
        Assert.That(_service.IsCurrentAccepted(changed), Is.True);
    }

    [Test]
    public void FormattingOnlyLineEndingsAndBom_DoNotChangeTermsHash()
    {
        string file = Path.Combine(_application, "SDK_END_USER_TERMS_DRAFT.md");
        var original = LicenseTerms.Load(_application);
        string text = File.ReadAllText(file).Replace("\n", "\r\n");
        File.WriteAllText(file, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        Assert.That(LicenseTerms.Load(_application).ContentHash, Is.EqualTo(original.ContentHash));
    }

    [TestCase("missing")]
    [TestCase("tampered")]
    [TestCase("oversized")]
    public void InvalidLicenseDocuments_FailClosedWithoutTouchingCollection(string defect)
    {
        Directory.CreateDirectory(_paths.RootPath);
        File.WriteAllText(_paths.DatabasePath, "Synthetic collection sentinel.");
        string sdk = Path.Combine(_application, LicenseTerms.SdkRelativePath);
        if (defect == "missing") File.Delete(sdk);
        else if (defect == "tampered") File.AppendAllText(sdk, "Synthetic tamper.");
        else File.WriteAllBytes(sdk, new byte[1024 * 1024 + 1]);
        Assert.That(() => LicenseTerms.Load(_application), Throws.Exception);
        Assert.That(File.ReadAllText(_paths.DatabasePath), Is.EqualTo("Synthetic collection sentinel."));
        Assert.That(File.Exists(_service.FilePath), Is.False);
    }

    [TestCase(LicenseTerms.AppSdkRelativePath, false)]
    [TestCase(LicenseTerms.AppSdkRelativePath, true)]
    [TestCase(LicenseTerms.WinRtMitRelativePath, false)]
    [TestCase(LicenseTerms.WinRtMitRelativePath, true)]
    [TestCase(LicenseTerms.WindowsMlRelativePath, false)]
    [TestCase(LicenseTerms.WindowsMlRelativePath, true)]
    public void MissingOrChangedAdditionalLicense_FailsClosedAndPreservesCollection(string relative, bool tamper)
    {
        Directory.CreateDirectory(_paths.RootPath);
        File.WriteAllText(_paths.DatabasePath, "Synthetic collection sentinel.");
        string document = Path.Combine(_application, relative);
        if (tamper) File.AppendAllText(document, "Synthetic tamper.");
        else File.Delete(document);
        Assert.That(() => LicenseTerms.Load(_application), Throws.Exception);
        Assert.That(File.ReadAllText(_paths.DatabasePath), Is.EqualTo("Synthetic collection sentinel."));
        Assert.That(File.Exists(_service.FilePath), Is.False);
    }

    [TestCase("format")]
    [TestCase("identity")]
    [TestCase("hash")]
    [TestCase("time")]
    [TestCase("unknown")]
    [TestCase("malformed")]
    [TestCase("oversized")]
    public void InvalidOrForeignRecord_DoesNotImplyConsent_AndIsPreserved(string defect)
    {
        var terms = LicenseTerms.Load(_application);
        _service.RecordAcceptance(terms);
        var record = JsonNode.Parse(File.ReadAllText(_service.FilePath))!.AsObject();
        switch (defect)
        {
            case "format": record["FormatVersion"] = 2; break;
            case "identity": record["Application"] = "Other.Application"; break;
            case "hash": record["TermsHash"] = new string('0', 64); break;
            case "time": record["AcceptedAtUtc"] = DateTimeOffset.MinValue; break;
            case "unknown": record["UnexpectedField"] = "synthetic"; break;
        }
        string content = defect switch { "malformed" => "{broken", "oversized" => new string(' ', 4097), _ => record.ToJsonString() };
        File.WriteAllText(_service.FilePath, content);
        Assert.That(_service.IsCurrentAccepted(terms), Is.False);
        Assert.That(File.ReadAllText(_service.FilePath), Is.EqualTo(content));
    }

    [Test]
    public void FailedAtomicSave_DoesNotStartBusinessOrReplaceExistingRecord()
    {
        _service.RecordAcceptance(LicenseTerms.Load(_application));
        byte[] previous = File.ReadAllBytes(_service.FilePath);
        File.AppendAllText(Path.Combine(_application, "SDK_END_USER_TERMS_DRAFT.md"), "\nNew condition.");
        FileStream? held = null;
        bool started = false;
        try
        {
            Assert.ThrowsAsync<IOException>(async () => await _service.RunAsync(LicenseTerms.Load(_application), () =>
            {
                held = new FileStream(_service.FilePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return Task.FromResult(true);
            }, () => { started = true; return Task.CompletedTask; }));
        }
        finally { held?.Dispose(); }
        Assert.Multiple(() =>
        {
            Assert.That(started, Is.False);
            Assert.That(File.ReadAllBytes(_service.FilePath), Is.EqualTo(previous));
            Assert.That(Directory.GetFiles(_paths.SettingsPath, "*.tmp"), Is.Empty);
            Assert.That(File.Exists(_paths.DatabasePath), Is.False);
        });
    }

    [Test]
    public void ReadingOriginalSdk_DoesNotPermitEditingOriginalBytes()
    {
        var terms = LicenseTerms.Load(_application);
        byte[] original = File.ReadAllBytes(Path.Combine(_application, LicenseTerms.SdkRelativePath));
        using var stream = terms.OpenSdkOriginal();
        Assert.That(stream.CanWrite, Is.False);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        Assert.That(copy.ToArray(), Is.EqualTo(original));
    }

    [TestCase("SDK_END_USER_TERMS_DRAFT.md")]
    [TestCase("LICENSE")]
    public void BlankApplicableTerms_AreRejected(string name)
    {
        File.WriteAllText(Path.Combine(_application, name), " \r\n ");
        Assert.Throws<InvalidDataException>(() => LicenseTerms.Load(_application));
        Assert.That(Directory.Exists(_paths.RootPath), Is.False);
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "GalgameManager.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot find repository test fixtures.");
    }
}
