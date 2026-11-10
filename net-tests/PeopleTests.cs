using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>What people are like: how a tone lands with them, how they judge deeds, and that the file names real people.</summary>
public class PeopleTests
{
    [Fact]
    public void EveryoneInTheFile_IsSomeoneWhoSpeaks()
    {
        foreach (var id in People.All.Keys)
        {
            Assert.True(Story.Dialogues.ContainsKey(id), $"people.json describes '{id}', who has no dialogue");
        }
    }

    [Fact]
    public void ATone_LandsDifferentlyWithDifferentPeople()
    {
        // Elda warms to kindness and bristles at hardness; Kai is the other way round.
        Assert.True(People.Of("elda")!.Tones["compassionate"] > 0 && People.Of("elda")!.Tones["hard"] < 0);
        Assert.True(People.Of("kai")!.Tones["hard"] > People.Of("kai")!.Tones["compassionate"]);
    }

    [Fact]
    public void AnAnswersTone_CountsOnce()
    {
        var world = new WorldState();
        var option = Story.Options.First(o => People.Of(o.Npc) is { } p && p.Tones.GetValueOrDefault(o.Tone) != 0);
        var change = People.HearTone(world, option);
        Assert.NotEqual(0, change);
        Assert.Equal(change, world.TrustOf(option.Npc));
        Assert.Equal(0, People.HearTone(world, option));
        Assert.Equal(change, world.TrustOf(option.Npc));
    }

    [Fact]
    public void ADeed_IsJudgedWhenItHappens_AndNotAgainOnLoad()
    {
        var world = new WorldState();
        world.SetFlag("vosk.defied");
        var trust = world.TrustOf("nell");
        Assert.True(trust > 0, "Nell thinks better of someone who stood up to Vosk");

        // Reading the save back sets the flag again, in a fresh world: the trust comes from the save, not from a second judgement.
        var loaded = new WorldState();
        SavedWorld.From(world).Into(loaded);
        Assert.Equal(trust, loaded.TrustOf("nell"));
    }

    [Fact]
    public void TheDead_JudgeNothing()
    {
        var world = new WorldState();
        world.SetFate("nell", NpcFate.Dead);
        world.SetFlag("vosk.defied");
        Assert.Equal(0, world.TrustOf("nell"));
    }

    [Theory]
    [InlineData(-60, "hostile")]
    [InlineData(0, "a stranger")]
    [InlineData(15, "known")]
    [InlineData(45, "trusted")]
    [InlineData(80, "a friend")]
    public void Trust_HasAWord(int trust, string word) => Assert.Equal(word, People.TrustWord(trust));
}
