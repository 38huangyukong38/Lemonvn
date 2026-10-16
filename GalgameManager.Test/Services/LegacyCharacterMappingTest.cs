using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.MapperProfile;
using PotatoVN.Client.Model;
using Gender = GalgameManager.Models.Gender;

namespace GalgameManager.Test.Services;

[TestFixture]
public class LegacyCharacterMappingTest
{
    [Test]
    public void OptionalOutgoingFields_KeepNullConstructorDefaults()
    {
        CharacterUpdateDto result = CharacterMapper.ToUpdate(new GalgameCharacter());
        Assert.Multiple(() =>
        {
            Assert.That(result.BloodType, Is.Null);
            Assert.That(result.Height, Is.Null);
            Assert.That(result.Weight, Is.Null);
            Assert.That(result.Id, Is.Zero);
            Assert.That(result.BirthYear, Is.Zero);
            Assert.That(result.BirthDay, Is.Zero);
        });
    }

    [Test]
    public void LegacyGameAuthentication_RemainsDisabledWithoutSharedCredentials()
    {
        Assert.ThrowsAsync<NotSupportedException>(async () =>
            await GalgameManager.Helpers.API.Ymgal.YmgalApi.GetAuthenticatedApiAsync());
    }

    [TestCase(true)]
    [TestCase(false)]
    public void IncomingCharacter_PreservesLegacyFieldAndDefaultSemantics(bool hasGender)
    {
        CharacterDto dto = new()
        {
            Id = 73, Name = "synthetic character", Relation = "supporting", Summary = "synthetic summary",
            Gender = hasGender ? (PotatoVN.Client.Model.Gender)2 : null,
            BirthYear = 2000, BirthMonth = 6, BirthDay = 12, BloodType = "A",
            Height = "170", Weight = "50", ThreeSize = "synthetic sizes",
            ImageUrl = "https://example.com/image.png", PreviewImageUrl = "https://example.com/preview.png"
        };
        GalgameCharacter result = CharacterMapper.FromDto(dto);
        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo(dto.Name));
            Assert.That(result.Relation, Is.EqualTo(dto.Relation));
            Assert.That(result.Summary, Is.EqualTo(dto.Summary));
            Assert.That(result.Gender, Is.EqualTo(hasGender ? Gender.Female : Gender.Unknown));
            Assert.That(result.BirthYear, Is.EqualTo(2000));
            Assert.That(result.BirthDay, Is.EqualTo(12));
            // BirthMon and BirthMonth were not conventionally matched by the old profile.
            Assert.That(result.BirthMon, Is.Null);
            Assert.That(result.BloodType, Is.EqualTo("A"));
            Assert.That(result.Height, Is.EqualTo("170"));
            Assert.That(result.Weight, Is.EqualTo("50"));
            Assert.That(result.BWH, Is.EqualTo(dto.ThreeSize));
            Assert.That(result.ImageUrl, Is.EqualTo(dto.ImageUrl));
            Assert.That(result.PreviewImageUrl, Is.EqualTo(dto.PreviewImageUrl));
            Assert.That(result.ImagePath, Is.EqualTo(Galgame.DefaultCharacterImagePath));
            Assert.That(result.Ids, Is.All.Null);
        });
    }

    [TestCase("73", 0)]
    [TestCase(null, 0)]
    public void OutgoingCharacter_PreservesIdentifierAndNullableDefaults(string? id, int expectedId)
    {
        GalgameCharacter source = new()
        {
            Name = "synthetic character", Relation = "supporting", Summary = "synthetic summary",
            Gender = Gender.Female, BirthYear = null, BirthMon = 6, BirthDay = 12,
            BloodType = "A", Height = "170", Weight = "50", BWH = "synthetic sizes"
        };
        source.Ids[(int)RssType.PotatoVn] = id;
        CharacterUpdateDto result = CharacterMapper.ToUpdate(source);
        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(expectedId));
            Assert.That(result.Name, Is.EqualTo(source.Name));
            Assert.That(result.Relation, Is.EqualTo(source.Relation));
            Assert.That(result.Summary, Is.EqualTo(source.Summary));
            Assert.That(result.Gender, Is.EqualTo((PotatoVN.Client.Model.Gender)2));
            Assert.That(result.BirthYear, Is.Zero);
            Assert.That(result.BirthMonth, Is.Zero);
            Assert.That(result.BirthDay, Is.EqualTo(12));
            Assert.That(result.BloodType, Is.EqualTo("A"));
            Assert.That(result.Height, Is.EqualTo("170"));
            Assert.That(result.Weight, Is.EqualTo("50"));
            // Measured with AutoMapper16: the optional DTO constructor kept
            // Id/ThreeSize defaults despite the old ForMember expressions.
            Assert.That(result.ThreeSize, Is.Null);
            Assert.That(result.ImageLoc, Is.Null);
            Assert.That(result.PreviewImageLoc, Is.Null);
        });
    }
}
