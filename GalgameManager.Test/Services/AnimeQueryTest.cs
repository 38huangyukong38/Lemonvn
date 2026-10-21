using LemonVN.Application.Anime;
using LemonVN.Domain.Anime;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class AnimeQueryTest
{
    private readonly AnimeQueryService _query = new();

    [Test]
    public void AscendingAndDescending_CombineWithFilters_AndKeepMissingValuesLast([Values] AnimeSort sort, [Values] bool descending)
    {
        AnimeEntry a = Entry("A", rating: 1, watched: [1]);
        AnimeEntry b = Entry("B", rating: 9, watched: [1, 2, 3]);
        a.CreatedAtUtc = a.UpdatedAtUtc = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        b.CreatedAtUtc = b.UpdatedAtUtc = a.CreatedAtUtc.AddDays(1);
        AnimeEntry unknown = Entry("C", total: null);
        unknown.CreatedAtUtc = unknown.UpdatedAtUtc = a.CreatedAtUtc.AddDays(2);
        foreach (EpisodeRecord episode in a.Record.Episodes) episode.WatchedAtUtc = a.UpdatedAtUtc;
        foreach (EpisodeRecord episode in b.Record.Episodes) episode.WatchedAtUtc = b.UpdatedAtUtc;
        foreach (AnimeEntry entry in new[] { a, b, unknown }) entry.Record.Tags = ["Music"];
        var source = new[] { unknown, b, a, Entry("Excluded", WatchStatus.Dropped) };
        AnimeEntry[] expected = sort is AnimeSort.Rating or AnimeSort.Progress or AnimeSort.LastWatched
            ? descending ? [b, a, unknown] : [a, b, unknown]
            : descending ? [unknown, b, a] : [a, b, unknown];
        Assert.That(_query.Apply(source, new("original", WatchStatus.Watching, "MUSIC", sort, descending)), Is.EqualTo(expected));
        Assert.That(_query.Apply(source.Reverse(), new("original", WatchStatus.Watching, "MUSIC", sort, descending)), Is.EqualTo(expected));
    }
    private static AnimeEntry Entry(string title, WatchStatus status = WatchStatus.Watching, int? rating = null,
        int? total = 12, params int[] watched) => new()
    {
        Metadata = new() { Title = title, OriginalTitle = "Original " + title, TotalEpisodes = total },
        Record = new() { WatchStatus = status, PersonalRating = rating, Tags = ["音乐"],
            Episodes = (watched.Length == 0 && status == WatchStatus.Watching ? new[] { 1 } : watched)
                .Select(number => new EpisodeRecord { EpisodeNumber = number, IsWatched = true }).ToList() }
    };

    [Test]
    public void SearchStatusTagAndSort_CombineWithoutOverwritingEachOther()
    {
        AnimeEntry a = Entry("音乐 A", rating: 5);
        AnimeEntry b = Entry("音乐 B", rating: 9);
        var source = new[] { a, b, Entry("音乐 C", WatchStatus.Planned), Entry("其他") };
        Assert.That(_query.Apply(source, new("original 音乐", WatchStatus.Watching, "音乐", AnimeSort.Rating)), Is.EqualTo(new[] { b, a }));
        Assert.That(_query.Apply(source, new("MUSIC")), Is.Empty);
        a.Metadata.OriginalTitle = "MuSiC Academy";
        Assert.That(_query.Apply(source, new("music")), Is.EqualTo(new[] { a }));
    }

    [Test]
    public void RatingZeroPrecedesUnrated_AndTiesUseTitleThenGuid()
    {
        AnimeEntry zero = Entry("Zero", rating: 0);
        AnimeEntry unknown = Entry("A Unrated");
        AnimeEntry high = Entry("High", rating: 9);
        Assert.That(_query.Apply(new[] { unknown, zero, high }, new(Sort: AnimeSort.Rating)), Is.EqualTo(new[] { high, zero, unknown }));
        AnimeEntry duplicate = zero.Copy();
        duplicate.Id = Guid.NewGuid();
        var expected = new[] { zero, duplicate }.OrderBy(entry => entry.Id).ToList();
        Assert.That(_query.Apply(new[] { duplicate, zero }, new(Sort: AnimeSort.Title)), Is.EqualTo(expected));
    }

    [Test]
    public void ProgressUsesCountNotHighest_KnownBeforeUnknown_WithoutMutatingRecords()
    {
        AnimeEntry skipped = Entry("Skipped", total: 12, watched: [1, 2, 12]);
        AnimeEntry contiguous = Entry("Contiguous", total: 12, watched: [1, 2, 3, 4]);
        AnimeEntry unknown = Entry("Unknown", total: null, watched: [1, 2, 3, 4, 5]);
        Assert.That(_query.Apply(new[] { unknown, skipped, contiguous }, new(Sort: AnimeSort.Progress)), Is.EqualTo(new[] { contiguous, skipped, unknown }));
        Assert.That(skipped.Record.Episodes, Has.Count.EqualTo(3));
        Assert.That(skipped.Record.WatchStatus, Is.EqualTo(WatchStatus.Watching));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(100)]
    [TestCase(1000)]
    public void ScaleAndLongTitles_HaveStablePredictableResults(int count)
    {
        AnimeEntry[] entries = Enumerable.Range(0, count).Select(number => Entry($"作品 {number:D4}" + new string('长', 2000),
            number % 2 == 0 ? WatchStatus.Watching : WatchStatus.Planned)).ToArray();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        IReadOnlyList<AnimeEntry> result = _query.Apply(entries, new("作品", WatchStatus.Watching, "音乐", AnimeSort.Title));
        TestContext.Out.WriteLine($"Query {count} entries: {timer.ElapsedMilliseconds} ms; matches {result.Count}.");
        Assert.That(result, Has.Count.EqualTo((count + 1) / 2));
        Assert.That(_query.Apply(entries.Reverse(), new(Sort: AnimeSort.Title)), Is.EqualTo(_query.Apply(entries, new(Sort: AnimeSort.Title))));
    }

    [Test]
    public void DatesAndOptionalOriginalTitle_AreHandled()
    {
        AnimeEntry older = Entry("Older");
        AnimeEntry newer = Entry("Newer");
        older.Metadata.OriginalTitle = null;
        newer.UpdatedAtUtc = DateTime.UtcNow;
        older.CreatedAtUtc = newer.UpdatedAtUtc.AddDays(1);
        Assert.That(_query.Apply(new[] { older, newer }, new(Sort: AnimeSort.Updated))[0], Is.SameAs(newer));
        Assert.That(_query.Apply(new[] { older, newer }, new(Sort: AnimeSort.Added))[0], Is.SameAs(older));
        Assert.That(_query.Apply(new[] { older }, new("Older")), Has.Count.EqualTo(1));
    }

    [Test]
    public void Tags_AreTrimmedDeduplicatedAndFilteredCaseInsensitively()
    {
        AnimeEntry entry = Entry("Tags");
        entry.Record.Tags = AnimeTagRules.Parse("  音乐, 校园，Music;music\n  \n喜欢");
        Assert.That(entry.Record.Tags, Is.EqualTo(new[] { "音乐", "校园", "Music", "喜欢" }));
        Assert.That(_query.Apply(new[] { entry }, new(Tag: "MUSIC")), Has.Count.EqualTo(1));
        entry.Record.Tags = AnimeTagRules.Parse("音乐");
        Assert.That(_query.GetTags(new[] { entry }), Is.EqualTo(new[] { "音乐" }));
        entry.Record.Tags = AnimeTagRules.Parse("");
        Assert.That(entry.Record.Tags, Is.Empty);
        Assert.That(() => AnimeTagRules.Parse(new string('x', 65)), Throws.TypeOf<AnimeValidationException>());
        Assert.That(() => AnimeTagRules.Normalize(Enumerable.Range(0, 51).Select(number => number.ToString())), Throws.TypeOf<AnimeValidationException>());
    }
}
