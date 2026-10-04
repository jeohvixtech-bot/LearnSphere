using LearnSphere.API.Services;

namespace LearnSphere.Tests.Services;

/// <summary>
/// Free-text profanity check: catches stretched spellings, inflections and look-alike
/// characters, without flagging ordinary words that merely contain a listed one.
/// Same cases are mirrored by the client-side filter (profanity-filter.service.js).
/// </summary>
public class ProfanityFilterTests
{
    [Theory]
    [InlineData("fuck")]
    [InlineData("fuckk")]
    [InlineData("FUUUCK you")]
    [InlineData("what the fucking hell")]
    [InlineData("you fucker!")]
    [InlineData("f*ck")]
    [InlineData("fvck")]
    [InlineData("sh1t")]
    [InlineData("that was shitty")]
    [InlineData("b!tch")]
    [InlineData("bitches")]
    [InlineData("a$$hole")]
    [InlineData("dickhead")]
    [InlineData("douchebag")]
    [InlineData("(shit)")]
    public void Flags_profanity_and_variants(string text) =>
        Assert.True(ProfanityFilter.ContainsProfanity(text));

    [Theory]
    [InlineData("Hello, see you in class tomorrow")]
    [InlineData("I live in Essex near Sussex")]
    [InlineData("Scunthorpe")]
    [InlineData("seashell")]
    [InlineData("The therapist booked a session")]
    [InlineData("grapes and drapes")]
    [InlineData("shiitake mushrooms")]
    [InlineData("a prickly cactus pricked my finger")]
    [InlineData("our cocker spaniel")]
    [InlineData("cocky")]
    [InlineData("Charles Dickens")]
    [InlineData("assess the passage")]
    [InlineData("")]
    [InlineData(null)]
    public void Leaves_ordinary_text_alone(string? text) =>
        Assert.False(ProfanityFilter.ContainsProfanity(text));
}
