using System.Net;
using System.Net.Sockets;
using System.Text;
using Quassel.Client.Application.Dcc;
using Quassel.Client.Domain;
using Quassel.Client.Infrastructure;
using QuasselGlow.ViewModels;

namespace Quassel.Client.Application.Tests;

public sealed class DccChatTests
{
    [Theory]
    [InlineData("\u0001DCC CHAT chat 1557997490 5060\u0001", "92.221.39.178")]
    [InlineData("DCC CHAT chat 92.221.39.178 5060", "92.221.39.178")]
    [InlineData("DCC CHAT chat 2001:db8::1 5060", "2001:db8::1")]
    [InlineData("DCC CHAT \"chat\" 2130706433 5060", "127.0.0.1")]
    [InlineData("\u0002\u0001DCC CHAT chat 1557997490 5060\u0001\u000f", "92.221.39.178")]
    public void Parse_RecognizesActiveChatOffers(string text, string expectedAddress)
    {
        var offer = Assert.IsType<DccChatOffer>(DccChatOfferParser.Parse(Message(text)));
        Assert.Equal("openmud", offer.Nick);
        Assert.Equal(expectedAddress, offer.Address.ToString());
        Assert.Equal(5060, offer.Port);
    }

    [Theory]
    [InlineData("DCC SEND chat 1557997490 5060")]
    [InlineData("DCC CHAT chat 1557997490 0")]
    [InlineData("DCC CHAT chat 1557997490 65536")]
    [InlineData("DCC CHAT chat 1557997490 -1")]
    [InlineData("DCC CHAT chat 4294967296 5060")]
    [InlineData("DCC CHAT chat 0 5060")]
    [InlineData("DCC CHAT chat 4294967295 5060")]
    [InlineData("DCC CHAT chat ff02::1 5060")]
    [InlineData("DCC CHAT chat example.com 5060")]
    [InlineData("DCC CHAT other 1557997490 5060")]
    [InlineData("You should type DCC CHAT chat 1557997490 5060")]
    [InlineData("\u0001DCC CHAT chat 1557997490 5060")]
    public void Parse_RejectsMalformedOrUnsupportedOffers(string text)
    {
        Assert.Null(DccChatOfferParser.Parse(Message(text)));
    }

    [Fact]
    public void BacklogOffers_AreVisibleButCannotBeAccepted()
    {
        var model = new MessageItemViewModel(Message("\u0001DCC CHAT chat 1557997490 5060\u0001", QuasselMessageFlags.Backlog));
        Assert.True(model.HasDccChatOffer);
        Assert.False(model.CanAcceptDccChat);
        Assert.DoesNotContain('\u2401', model.LineText);
        Assert.Null(DccChatOfferParser.Parse(Message("DCC CHAT chat 1557997490 5060", QuasselMessageFlags.Self)));
    }

    [Fact]
    public void TerminalEscapes_SplitAcrossReads_DoNotLeakIntoGameText()
    {
        var cleaner = new DccTextCleaner();
        Assert.Equal("Welcome ", cleaner.Clean("Welcome \x1B[3"));
        Assert.Equal("hero\nHP> ", cleaner.Clean("2mhero\x1B[0m\r\nHP> \x1B]0;window"));
        Assert.Equal("north", cleaner.Clean("title\x1B\\north\0\a"));
    }

    [Fact]
    public async Task TcpSession_ReceivesUnterminatedPrompts_AndSendsUtf8LinesWithoutIrcFraming()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var offer = new DccChatOffer("openmud", IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        await using var session = new DccChatSession();
        var prompt = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.TextReceived += text => prompt.TrySetResult(text);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.StateChanged += (state, _) => { if (state == DccChatState.Disconnected) closed.TrySetResult(); };

        var accept = listener.AcceptTcpClientAsync(timeout.Token).AsTask();
        await session.ConnectAsync(offer, timeout.Token);
        using var peer = await accept;
        await peer.GetStream().WriteAsync(Encoding.UTF8.GetBytes("HP> "), timeout.Token);
        Assert.Equal("HP> ", await prompt.Task.WaitAsync(timeout.Token));

        await session.SendLineAsync("/north blåbær", timeout.Token);
        var expected = Encoding.UTF8.GetBytes("/north blåbær\n");
        var actual = new byte[expected.Length];
        await peer.GetStream().ReadExactlyAsync(actual, timeout.Token);
        Assert.Equal(expected, actual); // No UTF-8 BOM, PRIVMSG wrapper or CR.
        await session.SendLineAsync("", timeout.Token);
        var blank = new byte[1];
        await peer.GetStream().ReadExactlyAsync(blank, timeout.Token);
        Assert.Equal((byte)'\n', blank[0]);
        await Assert.ThrowsAsync<ArgumentException>(() => session.SendLineAsync("look\nnorth"));

        peer.Close();
        await closed.Task.WaitAsync(timeout.Token);
    }

    [Fact]
    public async Task TcpSession_DisconnectCancelsPendingReceive()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        await using var session = new DccChatSession();
        var accept = listener.AcceptTcpClientAsync(timeout.Token).AsTask();
        await session.ConnectAsync(new DccChatOffer("bot", IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port), timeout.Token);
        using var peer = await accept;
        await session.DisconnectAsync().WaitAsync(timeout.Token);
        var buffer = new byte[1];
        Assert.Equal(0, await peer.GetStream().ReadAsync(buffer, timeout.Token));
    }

    [Fact]
    public async Task TcpSession_ExpiredOfferReportsError()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        await using var session = new DccChatSession(TimeSpan.FromSeconds(1));
        var states = new List<DccChatState>();
        session.StateChanged += (state, _) => states.Add(state);
        await session.ConnectAsync(new DccChatOffer("bot", IPAddress.Loopback, port));
        Assert.Equal([DccChatState.Connecting, DccChatState.Error], states);
    }

    [Fact]
    public async Task GameViewModel_HandlesSplitPromptsAndCommandHistory()
    {
        var session = new FakeDccSession();
        await using var model = new DccChatViewModel(new DccChatOffer("bot", IPAddress.Loopback, 5060), session, marshalToUiThread: false);
        Assert.False(model.SendCommand.CanExecute(null));
        Assert.Equal(0, session.ConnectCalls);
        await model.StartAsync();
        await model.StartAsync();
        Assert.Equal(1, session.ConnectCalls);
        session.Receive("\x1B[3");
        session.Receive("2mHP> ");
        Assert.Equal("HP> ", model.Output);

        model.Draft = "/look";
        await model.SendCommand.ExecuteAsync(null);
        Assert.Equal(["/look"], session.SentLines);
        Assert.Equal(string.Empty, model.Draft);
        model.Draft = "unsent";
        Assert.True(model.RecallHistory(previous: true));
        Assert.Equal("/look", model.Draft);
        Assert.True(model.RecallHistory(previous: false));
        Assert.Equal("unsent", model.Draft);
        await model.DisconnectCommand.ExecuteAsync(null);
        Assert.False(model.IsConnected);
        Assert.False(model.SendCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("!login oddi test-secret")]
    [InlineData("  !LOGIN\tuser\ttest-secret")]
    [InlineData("/login user test-secret")]
    public async Task LoginIsSentUnchangedButMaskedAndExcludedFromHistory(string login)
    {
        var session = new FakeDccSession();
        await using var model = new DccChatViewModel(new DccChatOffer("bot", IPAddress.Loopback, 5060), session, marshalToUiThread: false);
        await model.StartAsync();
        model.Draft = "!look";
        await model.SendCommand.ExecuteAsync(null);
        model.Draft = login;
        await model.SendCommand.ExecuteAsync(null);
        Assert.Equal(login, session.SentLines[^1]);
        Assert.DoesNotContain("test-secret", model.Output);
        Assert.DoesNotContain("test-secret", string.Concat(model.OutputSegments.Select(s => s.Text)));
        Assert.Contains("••••", model.Output);
        Assert.True(model.RecallHistory(previous: true));
        Assert.Equal("!look", model.Draft);
        Assert.False(model.RecallHistory(previous: true));
        Assert.True(model.RecallHistory(previous: false));
        Assert.Equal(string.Empty, model.Draft);
    }

    [Fact]
    public async Task GameOutputKeepsStyledTextAndPlainTranscriptInSyncAcrossReads()
    {
        var session = new FakeDccSession();
        await using var model = new DccChatViewModel(new DccChatOffer("bot", IPAddress.Loopback, 5060), session, marshalToUiThread: false);
        await model.StartAsync();
        session.Receive("\u00030");
        session.Receive("3\u0002Town");
        var oldSegments = model.OutputSegments;
        session.Receive(" Square\u000F\nHP> ");
        Assert.NotSame(oldSegments, model.OutputSegments);
        Assert.Equal("Town Square\nHP> ", model.Output);
        Assert.Equal(model.Output, string.Concat(model.OutputSegments.Select(s => s.Text)));
        Assert.True(model.OutputSegments[0].Style.Bold);
        Assert.Equal("#009300", model.OutputSegments[0].Style.Foreground);
    }

    [Fact]
    public async Task ScrollingHistoryDoesNotStoreAnUnsentLoginDraft()
    {
        var session = new FakeDccSession();
        await using var model = new DccChatViewModel(new DccChatOffer("bot", IPAddress.Loopback, 5060), session, marshalToUiThread: false);
        await model.StartAsync();
        model.Draft = "!look";
        await model.SendCommand.ExecuteAsync(null);
        model.Draft = "!login user test-secret";
        model.RecallHistory(previous: true);
        model.RecallHistory(previous: false);
        Assert.Equal(string.Empty, model.Draft);
    }

    [Fact]
    public async Task ScrollbackLimitTrimsTheSameTextFromStyledAndPlainOutput()
    {
        var session = new FakeDccSession();
        await using var model = new DccChatViewModel(new DccChatOffer("bot", IPAddress.Loopback, 5060), session, marshalToUiThread: false);
        await model.StartAsync();
        session.Receive("old line\n\u000303" + new string('x', 200_000) + "\u000F\nHP> ");
        Assert.True(model.Output.Length <= 200_000);
        Assert.DoesNotContain("old line", model.Output);
        Assert.EndsWith("HP> ", model.Output);
        Assert.Equal(model.Output, string.Concat(model.OutputSegments.Select(s => s.Text)));
        session.Receive("\u0002new line\u000F");
        Assert.EndsWith("HP> new line", model.Output);
        Assert.Equal(model.Output, string.Concat(model.OutputSegments.Select(s => s.Text)));
    }

    private static QuasselMessage Message(string text, QuasselMessageFlags flags = QuasselMessageFlags.None) => new(
        new MsgId(1), DateTimeOffset.Now,
        new QuasselBufferInfo(new BufferId(1), new NetworkId(1), QuasselBufferType.Query, 0, "openmud"),
        QuasselMessageType.Plain, text, "openmud!user@host", flags);

    private sealed class FakeDccSession : IDccChatSession
    {
        public event Action<string>? TextReceived;
        public event Action<DccChatState, string?>? StateChanged;
        public int ConnectCalls { get; private set; }
        public List<string> SentLines { get; } = [];
        public Task ConnectAsync(DccChatOffer offer, CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            StateChanged?.Invoke(DccChatState.Connected, null);
            return Task.CompletedTask;
        }
        public Task SendLineAsync(string text, CancellationToken cancellationToken = default)
        {
            SentLines.Add(text);
            return Task.CompletedTask;
        }
        public Task DisconnectAsync()
        {
            StateChanged?.Invoke(DccChatState.Disconnected, null);
            return Task.CompletedTask;
        }
        public void Receive(string text) => TextReceived?.Invoke(text);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
