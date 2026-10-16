// LemonVN modification from the fixed PotatoVN baseline; see LICENSE, NOTICE and UPSTREAM_PROVENANCE.json.
using PotatoVN.Client.Model;
using Gender = GalgameManager.Models.Gender;

namespace GalgameManager.Models.MapperProfile;

// LemonVN modification: explicit compatibility mapping replaces AutoMapper.
// This legacy game path stays outside the LemonVN service/page allow-list.
public static class CharacterMapper
{
    public static GalgameCharacter FromDto(CharacterDto source) => new()
    {
        Name = source.Name,
        Relation = source.Relation,
        Summary = source.Summary,
        Gender = source.Gender.HasValue ? (Gender)(int)source.Gender.Value : Gender.Unknown,
        BirthYear = source.BirthYear,
        BirthDay = source.BirthDay,
        BloodType = source.BloodType,
        Height = source.Height,
        Weight = source.Weight,
        BWH = source.ThreeSize,
        ImageUrl = source.ImageUrl,
        PreviewImageUrl = source.PreviewImageUrl
    };

    public static CharacterUpdateDto ToUpdate(GalgameCharacter source)
    {
        CharacterUpdateDto result = new()
        {
            Name = source.Name,
            Relation = source.Relation,
            Summary = source.Summary,
            Gender = (PotatoVN.Client.Model.Gender)(int)source.Gender,
            BirthYear = source.BirthYear ?? 0,
            BirthDay = source.BirthDay ?? 0
        };
        // Optional generated DTO fields have null constructor defaults, even
        // though their property setters use non-nullable annotations.
        if (source.BloodType is { } bloodType) result.BloodType = bloodType;
        if (source.Height is { } height) result.Height = height;
        if (source.Weight is { } weight) result.Weight = weight;
        // Preserve measured AutoMapper16 constructor defaults: Id/BirthMonth=0,
        // ThreeSize/ImageLoc/PreviewImageLoc=null. Do not repair dormant game
        // synchronization semantics during LemonVN release preparation.
        return result;
    }
}
