namespace Chat.Shared;

public static class IndexHtml
{
    public const string Content = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>SignalR AOT Benchmark Demo Chat</title>
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <style>
    body { font-family: system-ui, sans-serif; max-width: 840px; margin: 2rem auto; padding: 0 1rem; }
    input { padding: .5rem; margin-right: .25rem; }
    button { padding: .55rem .8rem; }
    #messages { margin-top: 1rem; padding-left: 1.25rem; }
  </style>
</head>
<body>
  <h1>SignalR AOT Benchmark Demo Chat</h1>
  <p>This page is only for manual smoke testing. Use <code>scripts/run-benchmark.sh</code> for benchmark results.</p>
  <input id="user" value="browser-user" />
  <input id="message" value="hello from browser" size="40" />
  <button id="send">Send</button>
  <ol id="messages"></ol>

  <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@10/dist/browser/signalr.min.js"></script>
  <script>
    const messages = document.getElementById('messages');
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/chat', { transport: signalR.HttpTransportType.WebSockets })
      .build();

    connection.on('ReceiveMessage', message => {
      const item = document.createElement('li');
      item.textContent = `${message.user}: ${message.text}`;
      messages.appendChild(item);
    });

    document.getElementById('send').addEventListener('click', async () => {
      const payload = {
        senderId: -1,
        sequence: Date.now(),
        user: document.getElementById('user').value,
        text: document.getElementById('message').value,
        sentAtTimestamp: 0
      };
      await connection.invoke('SendMessage', payload);
    });

    connection.start().catch(err => {
      const item = document.createElement('li');
      item.textContent = `Connection failed: ${err}`;
      messages.appendChild(item);
    });
  </script>
</body>
</html>
""";
}
