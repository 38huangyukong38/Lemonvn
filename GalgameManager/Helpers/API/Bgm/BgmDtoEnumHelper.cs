using GalgameManager.Enums;

namespace GalgameManager.Helpers.API.Bgm;

public static class BgmDtoEnumHelper
{
    public static Career ToCareer(this PersonCareerDto career)
    {
        return career switch
        {
            PersonCareerDto.mangaka or PersonCareerDto.artist or PersonCareerDto.illustrator => Career.Painter,
            PersonCareerDto.writer => Career.Writer,
            PersonCareerDto.producer => Career.Producer,
            PersonCareerDto.seiyu => Career.Seiyu,
            _ => Career.Unknown
        };
    }
}