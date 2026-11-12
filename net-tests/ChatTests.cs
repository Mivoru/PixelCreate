using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>The little chats people have among themselves are well formed: real people, lines by those in the chat.</summary>
public class ChatTests
{
    [Fact]
    public void EveryChat_IsBetweenRealPeople_AndOnlyTheySpeak()
    {
        Assert.NotEmpty(Chats.All);
        foreach (var chat in Chats.All)
        {
            Assert.True(chat.Between.Length >= 2, "a chat needs two people");
            Assert.All(chat.Between, id => Assert.True(People.Of(id) is not null, $"{id} in a chat has no entry in world/people.json"));
            Assert.All(chat.Lines, line => Assert.Contains(line.Speaker, chat.Between));
            Assert.True(chat.Lines.Length >= 2, "a chat is more than one line");
        }
    }
}
