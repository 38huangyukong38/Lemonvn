using Refit;

namespace GalgameManager.Helpers.API.Bgm;

public interface IBgmApi
{
    [Get("/v0/subjects/{id}")]
    Task<GameDto> GetGameAsync(int id);

    [Get("/v0/subjects/{id}/persons")]
    Task<List<RelatedPersonDto>> GetGamePersonsAsync(int id);

    [Get("/v0/subjects/{id}/characters")]
    Task<List<RelatedCharacterDto>> GetGameCharactersAsync(int id);

    [Post("/v0/search/persons")]
    Task<Paged<PersonDto>> SearchPersonAsync([Body] SearchPersonPayload payload);

    [Get("/v0/persons/{id}")]
    Task<PersonDetailDto> GetPersonAsync(int id);
}