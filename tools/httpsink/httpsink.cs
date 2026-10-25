// Serveur HTTP minimal qui journalise chaque requête : dotnet run httpsink.cs -- <port> <fichier-journal>
using System.Net;
using System.Net.Sockets;
using System.Text;

var port = args.Length > 0 ? int.Parse(args[0]) : 8765;
var logPath = args.Length > 1 ? args[1] : "httpsink.log";
var listener = new TcpListener(IPAddress.Loopback, port);
listener.Start();
Console.WriteLine($"Écoute sur 127.0.0.1:{port}");

while (true)
{
    using var client = await listener.AcceptTcpClientAsync();
    using var stream = client.GetStream();
    var buffer = new byte[4096];
    var read = await stream.ReadAsync(buffer);
    var requestLine = Encoding.ASCII.GetString(buffer, 0, read).Split("\r\n")[0];
    var line = $"{DateTime.Now:HH:mm:ss.fff} {requestLine}";
    Console.WriteLine(line);
    await File.AppendAllTextAsync(logPath, line + Environment.NewLine);

    var body = "ok"u8.ToArray();
    var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
    await stream.WriteAsync(header);
    await stream.WriteAsync(body);
}
