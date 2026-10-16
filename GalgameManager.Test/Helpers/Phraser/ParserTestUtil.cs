using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

public static class ParserTestUtil
{
    public static void CheckStaff(Staff? result, RssType feed, string expectedId, 
        string? expectedDescription = null,
        string? expectedJapaneseName = null, string? expectedEnglishName = null)
    {
        Assert.That(result, Is.Not.Null, "Parse Failed: staff is null");
        Assert.That(result.Ids[(int)feed], Is.EqualTo(expectedId),
            $"Id is not equal, expected: {expectedId}, actual: {result.Ids[(int)feed]}");
        if (!string.IsNullOrEmpty(expectedDescription) && result.Description?.Contains(expectedDescription) is not true)
            Assert.Fail($"Description is not equal, expected: {expectedDescription}, actual: {result.Description}");
        if (!string.IsNullOrEmpty(expectedJapaneseName))
            Assert.That(result.JapaneseName, Is.EqualTo(expectedJapaneseName),
                $"JapaneseName is not equal, expected: {expectedJapaneseName}, actual: {result.JapaneseName}");
        if (!string.IsNullOrEmpty(expectedEnglishName))
            Assert.That(result.EnglishName, Is.EqualTo(expectedEnglishName),
                $"EnglishName is not equal, expected: {expectedEnglishName}, actual: {result.EnglishName}");
    }

    public static void CheckGame(
        Galgame? game,
        string expectedId,
        string? expectedName = null, string? expectedCnName = null,
        string? expectedDeveloper = null,
        IGalCharacterPhraser? characterPhraser = null, string[]? expectedCharacterNames = null)
    {
        Assert.That(game, Is.Not.Null, "Parse Failed: game is null");

        if (!string.IsNullOrEmpty(expectedName))
        {
            Assert.That(game!.Name.Value, Is.EqualTo(expectedName),
                $"Name is not equal. Expected: {expectedName}, Actual: {game.Name}");
        }

        Assert.That(game!.Id, Is.EqualTo(expectedId),
            $"Id is not equal. Expected: {expectedId}, Actual: {game.Id}");

        if (!string.IsNullOrEmpty(expectedCnName))
        {
            Assert.That(game.CnName, Is.EqualTo(expectedCnName),
                $"CnName is not equal. Expected: {expectedCnName}, Actual: {game.CnName}");
        }
        
        if (!string.IsNullOrEmpty(expectedDeveloper))
        {
            Assert.That(game.Developer.Value, Is.EqualTo(expectedDeveloper),
                $"Developer is not equal. Expected: {expectedDeveloper}, Actual: {game.Developer.Value}");
        }

        if (expectedCharacterNames is { Length: > 0 })
        {
            Assert.That(characterPhraser, Is.Not.Null,
                "characterPhraser is null, Set characterPhraser to check characters.");
            foreach (var expectedCharacterName in expectedCharacterNames)
            {
                var hasCharacter = game.Characters.Any(
                    c => characterPhraser.GetGalgameCharacter(c).Result?.Name == expectedCharacterName);
                Assert.That(hasCharacter, Is.True,
                    $"Expected character [{expectedCharacterName}] was not found in game.Characters.");
            }
        }
    }
}