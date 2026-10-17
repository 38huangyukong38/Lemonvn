using System.Text.Json;
using System.Text.Json.Serialization;
using LemonVN.Contracts.Services;

namespace LemonVN.Services;

/// <summary>Only local acknowledgement of displayed content, never publisher/legal approval.</summary>
public sealed class LicenseConsentService(IAppDataPathService paths)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public string FilePath => Path.Combine(paths.SettingsPath, "license-consent.json");

    public bool IsCurrentAccepted(LicenseTerms terms)
    {
        RejectLinkedRecord();
        if (!File.Exists(FilePath)) return false;
        if (new FileInfo(FilePath).Length > 4096) return false;
        try
        {
            var record = JsonSerializer.Deserialize<ConsentRecord>(File.ReadAllBytes(FilePath), JsonOptions);
            return record is { FormatVersion: 1 } && record.Application == AppIdentity.PackageName &&
                record.TermsHash == terms.ContentHash && record.AcceptedAtUtc > DateTimeOffset.UnixEpoch &&
                record.AcceptedAtUtc.Offset == TimeSpan.Zero;
        }
        catch (JsonException) { return false; } // Preserve an invalid/older record until an explicit new choice.
    }

    public async Task<bool> RunAsync(LicenseTerms terms, Func<Task<bool>> requestDecision, Func<Task> startBusiness)
    {
        if (!IsCurrentAccepted(terms))
        {
            if (!await requestDecision()) return false;
            RecordAcceptance(terms); // Storage failure must prevent business initialization.
        }
        await startBusiness();
        return true;
    }

    public void RecordAcceptance(LicenseTerms terms)
    {
        RejectLinkedRecord();
        Directory.CreateDirectory(paths.SettingsPath);
        string temporary = Path.Combine(paths.SettingsPath, $"license-consent.{Guid.NewGuid():N}.tmp");
        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new ConsentRecord
            {
                FormatVersion = 1, Application = AppIdentity.PackageName,
                TermsHash = terms.ContentHash, AcceptedAtUtc = DateTimeOffset.UtcNow
            });
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
            else File.Move(temporary, FilePath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private void RejectLinkedRecord()
    {
        foreach (FileSystemInfo item in new FileSystemInfo[] { new DirectoryInfo(paths.RootPath), new DirectoryInfo(paths.SettingsPath), new FileInfo(FilePath) })
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The local consent record must remain in the application data directory.");
    }

    private sealed class ConsentRecord
    {
        public required int FormatVersion { get; set; }
        public required string Application { get; set; }
        public required string TermsHash { get; set; }
        public required DateTimeOffset AcceptedAtUtc { get; set; }
    }
}
