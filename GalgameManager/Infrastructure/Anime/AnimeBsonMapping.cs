using System.Globalization;
using LemonVN.Domain.Anime;
using LiteDB;

namespace LemonVN.Infrastructure.Anime;

public static class AnimeBsonMapping
{
    public static BsonMapper Create()
    {
        var mapper = new BsonMapper { EnumAsInteger = true };
        mapper.RegisterType<DateOnly>(date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bson => DateOnly.ParseExact(bson.AsString, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        mapper.Entity<AnimeEntry>().Id(entry => entry.Id, autoId: false);
        mapper.Entity<AnimeRecord>().Ignore(record => record.WatchedCount);
        return mapper;
    }
}
