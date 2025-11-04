
using System.Collections.Concurrent;

using System.Net;
using System.Net.WebSockets;
using System.Text;


using StardewModdingAPI;

namespace CommandWebUI
{
    public class Server
    {
        private readonly HttpListener listener;
        private readonly IMonitor monitor;
        private readonly ConcurrentBag<WebSocket> sockets = new();
        private string IndexPage;
        private string LoginPage;
        private bool running;
        public int Port;
        private readonly string AccessToken;

        private readonly WebSocketReader Reader;

        public Server(IMonitor monitor, WebSocketReader reader, int port, string acesstoken,ModConfig config)
        {
            AccessToken = acesstoken;
            Reader = reader;
            Port = port;
            this.monitor = monitor;
            listener = new HttpListener();
            var _uri = $"http://+:{Port}";
            listener.Prefixes.Add($"{_uri}/");

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods", "CommandWebUI", "index.html");
            IndexPage = File.ReadAllText(path);
            IndexPage = IndexPage.Replace("{{WEBSOCKET_URL}}", "/ws");

            var _loginPagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods", "CommandWebUI", "Login.html");
            LoginPage = File.ReadAllText(_loginPagePath);

            if (IndexPage == null)
            {
                IndexPage = "<h1>CommandWebUI</h1>";
            }
        }

        public void Start()
        {
            running = true;
            listener.Start();
            monitor.Log($"Server listening on http://0.0.0.0:{this.Port}/", LogLevel.Info);

            while (running)
            {
                try
                {
                    var ctx = listener.GetContext();
                    if (ctx.Request.IsWebSocketRequest)
                        _ = HandleWebSocketAsync(ctx);
                    else
                        _ = HandleHttpAsync(ctx);
                }
                catch (Exception ex)
                {
                    monitor.Log($"{ex}", LogLevel.Warn);
                }
            }
        }

        public void Stop()
        {
            running = false;
            listener.Stop();
            monitor.Log("Server stopped.", LogLevel.Info);
        }

        private async Task HandleHttpAsync(HttpListenerContext ctx)
        {
            
            if (ctx.Request.Url.AbsolutePath == "/index")

            {
                // 从查询参数中获取 token 
                var token = ctx.Request.QueryString["token"]; // 获取 URL 中的 token 参数
                                                              // 检查是否提供了正确的 token
                if (string.IsNullOrEmpty(token) || token != AccessToken) // 判断 token 是否为空且符合要求
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden; // 403 Forbidden
                    await using var writer = new StreamWriter(ctx.Response.OutputStream);
                    await writer.WriteAsync("Access denied: Invalid token."); // 提示访问被拒绝
                    await writer.FlushAsync();
                    ctx.Response.Close();
                    return;
                }
                // 如果 token 验证通过，返回页面内容
                ctx.Response.ContentType = "text/html; charset=utf-8";
                await using var responseWriter = new StreamWriter(ctx.Response.OutputStream);
                await responseWriter.WriteAsync($"{this.IndexPage}"); // 返回页面内容
                await responseWriter.FlushAsync();
                ctx.Response.Close();


            }
            else if(ctx.Request.Url.AbsolutePath == "/")
            {
                ctx.Response.ContentType = "text/html; charset=utf-8";
                await using var responseWriter = new StreamWriter(ctx.Response.OutputStream);
                await responseWriter.WriteAsync($"{this.LoginPage}"); // 返回页面内容
                await responseWriter.FlushAsync();
                ctx.Response.Close();
            }
        }
        private async Task HandleWebSocketAsync(HttpListenerContext ctx)
        {

            if (ctx.Request.Url.AbsolutePath != "/ws")
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                ctx.Response.Close();
                return;
            }
            try
            {
                var wsCtx = await ctx.AcceptWebSocketAsync(null);
                var ws = wsCtx.WebSocket;
                sockets.Add(ws);
                monitor.Log("client connected", LogLevel.Info);

                var buffer = new byte[1024];
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    // 接收网页端消息
                    string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    //作为控制台输入

                    //Console.WriteLine($"[WebSocket Input] {msg}");
                    Reader.PushInput(msg);
                }

                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                monitor.Log("client disconnected", LogLevel.Info);

            }
            catch (Exception ex)
            {
                monitor.Log($"{ex}", LogLevel.Warn);
            }
        }

        public async void Broadcast(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            foreach (var ws in sockets)
            {
                if (ws.State == WebSocketState.Open)
                    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}
