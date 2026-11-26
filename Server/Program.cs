using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using WolfAndRabbits.Server.Game;
using WolfAndRabbits.Server.Protocol;

// Resolve build/runtime mode flag: Development (port 5000) vs Release (port 80)
string resolvedMode = ResolveRunMode(args);
bool isReleaseMode = string.Equals(resolvedMode, "Release", StringComparison.OrdinalIgnoreCase);
int defaultPort = isReleaseMode ? 80 : 5000;

var builderOptions = new WebApplicationOptions
{
    Args = args,
    EnvironmentName = isReleaseMode ? Environments.Production : Environments.Development
};
var builder = WebApplication.CreateBuilder(builderOptions);

// Bind port 5000 in Development, port 80 in Release (unless explicitly overridden by --urls or ASPNETCORE_URLS)
bool hasCliUrls = args.Any(a => a.StartsWith("--urls", StringComparison.OrdinalIgnoreCase));
string? envUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (!hasCliUrls)
{
    if (!string.IsNullOrWhiteSpace(envUrls))
    {
        builder.WebHost.UseUrls(envUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
    else
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{defaultPort}");
    }
}

builder.Services.AddSingleton<RoomManager>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseCors();

// Serve Unity WebGL build from wwwroot/ (supports .wasm, .data, Brotli .br, Gzip .gz, and HTTP fallback)
ConfigureWebGlStaticFiles(app);

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(15)
});

// Validate map & load configurable Wolf shop JSON on startup
var startupMap = MapDefinition.CreateDefaultForestMap();
var startupShop = WolfShopCatalog.Current;
app.Logger.LogInformation(
    "Wolf and Rabbits Server initialized. Mode={Mode} DefaultPort={Port} Map={W}x{H} Bases={Bases} WolfShopItems={ShopItems} MaxSlots={MaxSlots}",
    resolvedMode, defaultPort, startupMap.Width, startupMap.Height, startupMap.Bases.Count, startupShop.items.Count, startupShop.maxSlotItems);

app.MapGet("/api/wolf-shop", () => Results.Ok(WolfShopCatalog.Current));

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    game = "Wolf and Rabbits 3D",
    mode = resolvedMode,
    defaultPort,
    tickRateHz = GameConfig.TickRateHz,
    snapshotRateHz = GameConfig.TickRateHz / GameConfig.SnapshotEveryNTicks
}));

app.MapGet("/api/rooms", (RoomManager rooms) => Results.Ok(rooms.GetJoinableRooms()));

app.Map("/ws", async (HttpContext context, RoomManager rooms, ILoggerFactory loggerFactory) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket connection required.");
        return;
    }

    var logger = loggerFactory.CreateLogger("WebSocketSession");
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    string clientId = Guid.NewGuid().ToString("N")[..10];
    await using var client = new ClientConnection(clientId, socket);

    logger.LogInformation("Client connected: {ClientId}", clientId);
    client.Send(new WelcomeMessage { clientId = clientId });
    client.Send(new RoomListMessage { rooms = rooms.GetJoinableRooms() });

    var buffer = new byte[16 * 1024];
    using var ms = new MemoryStream();

    try
    {
        while (socket.State == WebSocketState.Open)
        {
            ms.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage && ms.Length < 64 * 1024);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            if (result.MessageType != WebSocketMessageType.Text)
                continue;

            string json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            HandleClientJson(client, json, rooms, logger);
        }
    }
    catch (OperationCanceledException)
    {
    }
    catch (WebSocketException)
    {
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "WebSocket error for client {ClientId}", clientId);
    }
    finally
    {
        rooms.LeaveCurrentRoom(client);
        logger.LogInformation("Client disconnected: {ClientId}", clientId);
        if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
        {
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch
            {
            }
        }
    }
});

app.Run();

static void HandleClientJson(ClientConnection client, string json, RoomManager rooms, ILogger logger)
{
    ClientEnvelope? env;
    try
    {
        env = JsonSerializer.Deserialize<ClientEnvelope>(json);
    }
    catch (Exception ex)
    {
        logger.LogDebug(ex, "Invalid JSON from {ClientId}", client.ClientId);
        client.Send(new ErrorMessage { code = "bad_json", message = "Gói tin không hợp lệ." });
        return;
    }

    if (env == null || string.IsNullOrWhiteSpace(env.type))
        return;

    string reqId = env.requestId ?? string.Empty;

    switch (env.type)
    {
        case "list_rooms":
            client.Send(new RoomListMessage { rooms = rooms.GetJoinableRooms() });
            break;

        case "create_room":
            rooms.CreateRoom(client, env.playerName ?? "Chủ phòng", env.password);
            break;

        case "join_room":
        {
            var res = rooms.JoinRoom(client, env.roomCode, env.playerName ?? "Người chơi", env.password);
            if (!res.Success)
                client.Send(new ErrorMessage { code = res.Code, message = res.Message, requestId = reqId });
            break;
        }

        case "leave_room":
            rooms.LeaveCurrentRoom(client);
            client.Send(new RoomListMessage { rooms = rooms.GetJoinableRooms() });
            break;

        case "start_match":
        {
            var room = rooms.GetRoom(client.CurrentRoomCode);
            if (room == null)
            {
                client.Send(new ErrorMessage { code = "no_room", message = "Bạn chưa ở trong phòng.", requestId = reqId });
                break;
            }
            var res = room.TryStartMatch(client.ClientId);
            if (!res.Success)
                client.Send(new ErrorMessage { code = res.Code, message = res.Message, requestId = reqId });
            break;
        }

        case "return_to_room":
        {
            var room = rooms.GetRoom(client.CurrentRoomCode);
            if (room == null)
            {
                client.Send(new RoomListMessage { rooms = rooms.GetJoinableRooms() });
                break;
            }
            var res = room.TryReturnToWaitingRoom(client.ClientId);
            if (!res.Success)
                client.Send(new ErrorMessage { code = res.Code, message = res.Message, requestId = reqId });
            break;
        }

        case "move_input":
        {
            var room = rooms.GetRoom(client.CurrentRoomCode);
            room?.HandleMoveInput(client.ClientId, env.dirX, env.dirY);
            break;
        }

        case "build_request":
        {
            var room = rooms.GetRoom(client.CurrentRoomCode);
            if (room == null) break;
            var res = room.HandleBuild(client.ClientId, env.requestId, env.buildingType, env.tileX, env.tileY);
            if (!res.Success)
                client.Send(new ErrorMessage { code = res.Code, message = res.Message, requestId = reqId });
            break;
        }

        case "upgrade_request":
        {
            var room = rooms.GetRoom(client.CurrentRoomCode);
            if (room == null) break;
            var res = room.HandleUpgrade(client.ClientId, env.requestId, env.buildingId);
            if (!res.Success)
                client.Send(new ErrorMessage { code = res.Code, message = res.Message, requestId = reqId });
            break;
        }

        case "attack_request":
        {
            var room = rooms.GetRoom(client.CurrentRoomCode);
            if (room == null) break;
            var res = room.HandleAttack(client.ClientId, env.requestId);
            if (!res.Success && res.Code != "cooldown")
                client.Send(new ErrorMessage { code = res.Code, message = res.Message, requestId = reqId });
            break;
        }

        case "buy_item":
        {
            var room = rooms.GetRoom(client.CurrentRoomCode);
            if (room == null) break;
            var res = room.HandleBuyItem(client.ClientId, env.requestId, env.itemId);
            if (!res.Success)
                client.Send(new ErrorMessage { code = res.Code, message = res.Message, requestId = reqId });
            break;
        }
    }
}

static string ResolveRunMode(string[] args)
{
    foreach (var arg in args)
    {
        if (arg.Equals("--release", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--mode=release", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--mode=production", StringComparison.OrdinalIgnoreCase))
            return "Release";

        if (arg.Equals("--dev", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--development", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--mode=development", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--mode=dev", StringComparison.OrdinalIgnoreCase))
            return "Development";
    }

    string? gameEnv = Environment.GetEnvironmentVariable("GAME_ENV")
                   ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (!string.IsNullOrWhiteSpace(gameEnv))
    {
        if (gameEnv.Equals("Release", StringComparison.OrdinalIgnoreCase) ||
            gameEnv.Equals("Production", StringComparison.OrdinalIgnoreCase))
            return "Release";

        if (gameEnv.Equals("Development", StringComparison.OrdinalIgnoreCase) ||
            gameEnv.Equals("Dev", StringComparison.OrdinalIgnoreCase))
            return "Development";
    }

#if DEBUG
    return "Development";
#else
    return "Release";
#endif
}

static void ConfigureWebGlStaticFiles(WebApplication app)
{
    string webRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "wwwroot"));
    if (!Directory.Exists(webRoot)) return;

    string webRootPrefix = webRoot.EndsWith(Path.DirectorySeparatorChar)
        ? webRoot
        : webRoot + Path.DirectorySeparatorChar;
    string cacheDir = Path.Combine(Path.GetTempPath(), "wolf_and_rabbits_webgl_cache");
    var cacheLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

    // Fallback for plain HTTP (e.g. http://<vps-ip>) where browsers do not advertise Brotli ("br").
    // Serves pre-transcoded gzip (or raw) transparently so Unity WebGL Brotli builds still load over HTTP.
    app.Use(async (context, next) =>
    {
        if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) &&
            context.Request.Path.Value is { } reqPath &&
            reqPath.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
        {
            string acceptEncoding = context.Request.Headers.AcceptEncoding.ToString();
            bool acceptsBrotli = AcceptsEncoding(acceptEncoding, "br");

            if (!acceptsBrotli)
            {
                string relativePath = reqPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                string sourcePath = Path.GetFullPath(Path.Combine(webRoot, relativePath));
                if (sourcePath.StartsWith(webRootPrefix, StringComparison.Ordinal) && File.Exists(sourcePath))
                {
                    bool acceptsGzip = AcceptsEncoding(acceptEncoding, "gzip");
                    string innerName = Path.GetFileName(sourcePath)[..^3];

                    context.Response.Headers.Append("Vary", "Accept-Encoding");
                    SetInnerWebGlContentType(context.Response, innerName);

                    if (acceptsGzip)
                    {
                        string cachedGz = await EnsureGzipFallbackAsync(sourcePath, cacheDir, cacheLocks, context.RequestAborted);
                        context.Response.Headers.ContentEncoding = "gzip";
                        context.Response.ContentLength = new FileInfo(cachedGz).Length;
                        if (!HttpMethods.IsHead(context.Request.Method))
                        {
                            await context.Response.SendFileAsync(cachedGz, context.RequestAborted);
                        }
                        return;
                    }

                    if (!HttpMethods.IsHead(context.Request.Method))
                    {
                        await using var fs = File.OpenRead(sourcePath);
                        await using var brotli = new BrotliStream(fs, CompressionMode.Decompress);
                        await brotli.CopyToAsync(context.Response.Body, context.RequestAborted);
                    }
                    return;
                }
            }
        }

        await next();
    });

    var contentTypes = new FileExtensionContentTypeProvider();
    contentTypes.Mappings[".wasm"] = "application/wasm";
    contentTypes.Mappings[".data"] = "application/octet-stream";
    contentTypes.Mappings[".symbols.json"] = "application/json";
    contentTypes.Mappings[".br"] = "application/octet-stream";
    contentTypes.Mappings[".gz"] = "application/octet-stream";

    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = contentTypes,
        ServeUnknownFileTypes = true,
        OnPrepareResponse = ctx =>
        {
            string name = ctx.File.Name;
            if (name.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Context.Response.Headers.Append("Vary", "Accept-Encoding");
                ctx.Context.Response.Headers.ContentEncoding = "br";
                SetInnerWebGlContentType(ctx.Context.Response, name[..^3]);
            }
            else if (name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Context.Response.Headers.Append("Vary", "Accept-Encoding");
                ctx.Context.Response.Headers.ContentEncoding = "gzip";
                SetInnerWebGlContentType(ctx.Context.Response, name[..^3]);
            }
        }
    });
}

static bool AcceptsEncoding(string acceptEncodingHeader, string token)
{
    if (string.IsNullOrWhiteSpace(acceptEncodingHeader)) return false;
    foreach (var rawPart in acceptEncodingHeader.Split(','))
    {
        var part = rawPart.Trim();
        int semi = part.IndexOf(';');
        string encoding = (semi >= 0 ? part[..semi] : part).Trim();
        if (!encoding.Equals(token, StringComparison.OrdinalIgnoreCase)) continue;
        if (semi >= 0 && part[(semi + 1)..].Replace(" ", "").Contains("q=0", StringComparison.OrdinalIgnoreCase) &&
            !part[(semi + 1)..].Replace(" ", "").Contains("q=0.", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }
    return false;
}

static async Task<string> EnsureGzipFallbackAsync(
    string sourcePath,
    string cacheDir,
    ConcurrentDictionary<string, SemaphoreSlim> cacheLocks,
    CancellationToken ct)
{
    string hashKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sourcePath)))[..16];
    string targetPath = Path.Combine(cacheDir, $"{hashKey}_{Path.GetFileName(sourcePath)}.gz");

    DateTime sourceWriteTime = File.GetLastWriteTimeUtc(sourcePath);
    if (File.Exists(targetPath) && File.GetLastWriteTimeUtc(targetPath) >= sourceWriteTime)
        return targetPath;

    var gate = cacheLocks.GetOrAdd(sourcePath, _ => new SemaphoreSlim(1, 1));
    await gate.WaitAsync(ct);
    try
    {
        if (File.Exists(targetPath) && File.GetLastWriteTimeUtc(targetPath) >= sourceWriteTime)
            return targetPath;

        Directory.CreateDirectory(cacheDir);
        string tmpPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var src = File.OpenRead(sourcePath))
            await using (var brotli = new BrotliStream(src, CompressionMode.Decompress))
            await using (var dst = File.Create(tmpPath))
            await using (var gzip = new GZipStream(dst, CompressionLevel.Fastest))
            {
                await brotli.CopyToAsync(gzip, ct);
            }
            File.Move(tmpPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { }
            }
        }
        return targetPath;
    }
    finally
    {
        gate.Release();
    }
}

static void SetInnerWebGlContentType(HttpResponse response, string innerName)
{
    if (innerName.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
        response.ContentType = "application/wasm";
    else if (innerName.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        response.ContentType = "application/javascript";
    else if (innerName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        response.ContentType = "application/json";
    else if (innerName.EndsWith(".data", StringComparison.OrdinalIgnoreCase))
        response.ContentType = "application/octet-stream";
}
