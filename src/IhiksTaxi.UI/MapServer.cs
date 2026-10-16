using System.Net;
using System.Net.Sockets;
using System.Text;

namespace IhiksTaxi.UI;

// WebView2 не умеет читать локальные файлы через fetch, поэтому офлайн-данные карты
// отдаём крошечным HTTP-сервером только на loopback-адресе 127.0.0.1
public sealed class MapServer : IDisposable
{
	private const int FirstPort = 5137;
	private const int LastPort = 5200;

	private readonly string _root;
	private readonly string _rootPrefix;
	private readonly TcpListener? _listener;

	public MapServer(string root)
	{
		_root = Path.GetFullPath(root);
		_rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar)
			? _root
			: _root + Path.DirectorySeparatorChar;
		_listener = Bind();

		if (_listener is null)
			return;

		var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
		IndexUrl = $"http://127.0.0.1:{port}/map/index.html";
		_listener.Start();
		_ = Task.Run(Loop);
	}

	public string? IndexUrl { get; }

	public bool HasMapPage => _listener is not null && File.Exists(Path.Combine(_root, "map", "index.html"));

	// текущий маршрут отдаётся страницей как /map/route.json
	public void SetRoute(string json) => _routeJson = json;

	private volatile string _routeJson = "{}";

	private static TcpListener? Bind()
	{
		for (var port = FirstPort; port <= LastPort; port++)
		{
			try
			{
				var listener = new TcpListener(IPAddress.Loopback, port);
				listener.Start();
				return listener;
			}
			catch (SocketException)
			{
				// порт занят — берём следующий
			}
		}

		return null;
	}

	private async Task Loop()
	{
		var listener = _listener!;
		while (true)
		{
			TcpClient client;
			try
			{
				client = await listener.AcceptTcpClientAsync();
			}
			catch
			{
				break;
			}

			_ = Task.Run(() => Serve(client));
		}
	}

	private void Serve(TcpClient client)
	{
		using (client)
		using (var stream = client.GetStream())
		{
			try
			{
				var request = ReadRequest(stream);
				if (request is null)
					return;

				var (method, path) = request.Value;
				var headOnly = method == "HEAD";
				if (method != "GET" && !headOnly)
				{
					Respond(stream, 405, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Method Not Allowed"), false);
					return;
				}

				if (path == "/map/route.json")
				{
					Respond(stream, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(_routeJson), headOnly);
					return;
				}

				if (path == "/")
					path = "/map/index.html";

				var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
				var full = Path.GetFullPath(Path.Combine(_root, relative));
				if (!full.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
				{
					Respond(stream, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not Found"), headOnly);
					return;
				}

				Respond(stream, 200, ContentType(full), File.ReadAllBytes(full), headOnly);
			}
			catch
			{
				// обрыв соединения клиентом не должен останавливать сервер
			}
		}
	}

	private static (string Method, string Path)? ReadRequest(NetworkStream stream)
	{
		var buffer = new byte[4096];
		var text = new StringBuilder();

		while (text.Length < 16384)
		{
			var read = stream.Read(buffer, 0, buffer.Length);
			if (read <= 0)
				break;

			text.Append(Encoding.ASCII.GetString(buffer, 0, read));
			if (text.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
				break;
		}

		var line = text.ToString().Split("\r\n", StringSplitOptions.None)[0];
		var parts = line.Split(' ');
		if (parts.Length < 2)
			return null;

		var path = parts[1];
		var query = path.IndexOf('?');
		if (query >= 0)
			path = path[..query];

		return (parts[0].ToUpperInvariant(), path);
	}

	private static void Respond(NetworkStream stream, int status, string contentType, byte[] body, bool headOnly)
	{
		var reason = status switch
		{
			200 => "OK",
			404 => "Not Found",
			405 => "Method Not Allowed",
			_ => "Error",
		};

		var header = $"HTTP/1.1 {status} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
		var headerBytes = Encoding.ASCII.GetBytes(header);
		stream.Write(headerBytes, 0, headerBytes.Length);

		if (!headOnly)
			stream.Write(body, 0, body.Length);

		stream.Flush();
	}

	private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
	{
		".html" => "text/html; charset=utf-8",
		".js" => "application/javascript; charset=utf-8",
		".css" => "text/css; charset=utf-8",
		".json" => "application/json; charset=utf-8",
		".geojson" => "application/json; charset=utf-8",
		_ => "application/octet-stream",
	};

	public void Dispose() => _listener?.Stop();
}
