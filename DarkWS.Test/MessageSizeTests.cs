using System.Net.WebSockets;
using System.Text;
using DarkWS.Test.Project;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DarkWS.Test;

public sealed class MessageSizeTests {
    [TestCase(false)]
    [TestCase(true)]
    public async Task ExactLimitIsAcceptedAndResetsForNextMessage(bool fragmented) {
        var socket = new TestWebSocket();
        if (fragmented) { socket.EnqueueReceive("ab", false); socket.EnqueueReceive("cde"); }
        else socket.EnqueueReceive("abcde");
        socket.EnqueueReceive("12345");
        using var connection = new WebSocketConnection(socket, new DefaultHttpContext(), null, 5);
        Assert.That(Encoding.UTF8.GetString((await connection.ReceiveMessageAsync()).Data), Is.EqualTo("abcde"));
        Assert.That(Encoding.UTF8.GetString((await connection.ReceiveMessageAsync()).Data), Is.EqualTo("12345"));
        Assert.That(socket.State, Is.EqualTo(WebSocketState.Open));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OversizeClosesBeforeEndOfMessage(bool fragmented) {
        var socket = new TestWebSocket();
        if (fragmented) { socket.EnqueueReceive("abc", false); socket.EnqueueReceive("def", false); }
        else socket.EnqueueReceive("abcdef", false);
        using var connection = new WebSocketConnection(socket, new DefaultHttpContext(), null, 5);
        var message = await connection.ReceiveMessageAsync();
        Assert.That(message.Data, Is.Empty);
        Assert.That(message.CloseStatus, Is.EqualTo(WebSocketCloseStatus.MessageTooBig));
        Assert.That(socket.LastOutputCloseStatus, Is.EqualTo(WebSocketCloseStatus.MessageTooBig));
        Assert.That(socket.State, Is.EqualTo(WebSocketState.CloseSent));
    }

    [Test]
    public async Task LimitCountsUtf8Bytes() {
        var socket = new TestWebSocket();
        socket.EnqueueReceive("яяя");
        using var connection = new WebSocketConnection(socket, new DefaultHttpContext(), null, 5);
        Assert.That((await connection.ReceiveMessageAsync()).CloseStatus, Is.EqualTo(WebSocketCloseStatus.MessageTooBig));
    }

    [Test]
    public async Task CloseDiscardsPartialMessage() {
        var socket = new TestWebSocket();
        socket.EnqueueReceive("abc", false);
        socket.EnqueueClose();
        using var connection = new WebSocketConnection(socket, new DefaultHttpContext(), null, 5);
        var message = await connection.ReceiveMessageAsync();
        Assert.That(message.Data, Is.Empty);
        Assert.That(message.CloseStatus, Is.EqualTo(WebSocketCloseStatus.NormalClosure));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void InvalidLimitIsRejectedAtRegistrationAndConstruction(int limit) {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddDarkWs(options => options.MaxMessageSizeBytes = limit));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WebSocketConnection(new TestWebSocket(), new DefaultHttpContext(), null, limit));
    }
}
