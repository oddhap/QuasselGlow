using Avalonia.Media;
using QuasselGlow.Appearance;
using QuasselGlow.ViewModels;
using Quassel.Client.Domain;

namespace Quassel.Client.Application.Tests;

public sealed class NickColorPaletteTests
{
    [Fact]
    public void LargeRosterGetsUniquePastelsInBothThemesAndKeepsAssignments()
    {
        var light = new HashSet<Color>();
        var dark = new HashSet<Color>();
        for (var index = 0; index < 4096; index++)
        {
            var nick = $"palette-test-user-{index}";
            var lightBrush = Assert.IsAssignableFrom<ISolidColorBrush>(NickColorPalette.Resolve(nick, false));
            var darkBrush = Assert.IsAssignableFrom<ISolidColorBrush>(NickColorPalette.Resolve(nick, true));
            Assert.True(light.Add(lightBrush.Color), $"Light color repeated for {nick}");
            Assert.True(dark.Add(darkBrush.Color), $"Dark color repeated for {nick}");
            Assert.NotEqual(lightBrush.Color, darkBrush.Color);
            Assert.InRange(darkBrush.Color.ToHsl().L, 0.74, 0.86);
            Assert.InRange(darkBrush.Color.ToHsl().S, 0.28, 0.52);
            Assert.Same(lightBrush, NickColorPalette.Resolve(nick.ToUpperInvariant(), false));
            Assert.Same(darkBrush, NickColorPalette.Resolve(nick, true));
        }
    }

    [Fact]
    public void RosterAndMessagesShareColorsForManyNicknames()
    {
        var info = new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Channel, 0, "#colors");
        var buffer = new BufferItemViewModel(info);
        var users = Enumerable.Range(0, 64).Select(index => new QuasselChannelUser($"color-user-{index}", "")).ToArray();
        buffer.ApplyChannelState(new QuasselChannelState(info.NetworkId, info.BufferName, "", users));
        foreach (var (user, index) in users.Select((user, index) => (user, index)))
            buffer.AddMessage(new QuasselMessage(new MsgId(index + 1), DateTimeOffset.Now, info,
                QuasselMessageType.Plain, "Hi", user.Nick + "!user@host", QuasselMessageFlags.None), false);

        foreach (var dark in new[] { false, true, false })
        {
            buffer.ConfigureDarkMode(dark);
            var colors = new HashSet<Color>();
            foreach (var message in buffer.Messages)
            {
                var color = Assert.IsAssignableFrom<ISolidColorBrush>(message.SenderBrush).Color;
                Assert.True(colors.Add(color));
                var user = buffer.ChannelUsers.Single(user => user.Nick == message.SenderDisplay);
                Assert.Equal(color, Assert.IsAssignableFrom<ISolidColorBrush>(user.NickBrush).Color);
            }
        }
    }
}
