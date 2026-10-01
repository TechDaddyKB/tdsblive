using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ExtensionSuite.Core;
using Microsoft.AspNetCore.Antiforgery;

namespace ExtensionSuite.Host;

public sealed class AccessControl
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> sessions = new();
    private readonly object sessionSync = new();
    private byte[]? adminHash;
    public bool HasAdminCredential { get { lock (sessionSync) return adminHash is not null; } }

    public void SetAdminCredential(string credential)
    {
        if (credential.Length < 32) throw new ArgumentException("Admin credentials require at least 32 characters.");
        lock (sessionSync)
        {
            adminHash = SHA256.HashData(Encoding.UTF8.GetBytes(credential));
            sessions.Clear();
        }
    }

    public bool ValidateCredential(string candidate)
    {
        lock (sessionSync) return ValidateCredentialLocked(candidate);
    }

    private bool ValidateCredentialLocked(string candidate) => adminHash is not null && candidate.Length <= 1024 &&
        CryptographicOperations.FixedTimeEquals(adminHash, SHA256.HashData(Encoding.UTF8.GetBytes(candidate)));

    public string? TryCreateSession(string credential)
    {
        lock (sessionSync)
        {
            if (!ValidateCredentialLocked(credential)) return null;
            foreach (var expired in sessions.Where(item => item.Value <= DateTimeOffset.UtcNow)) sessions.TryRemove(expired.Key, out _);
            if (sessions.Count >= 32) return null;
            var session = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            sessions[session] = DateTimeOffset.UtcNow.AddHours(8);
            return session;
        }
    }

    public bool IsAuthenticated(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.Ordinal)) return ValidateCredential(authorization[7..]);
        lock (sessionSync) return context.Request.Cookies.TryGetValue("tdsblive-session", out var session) &&
            sessions.TryGetValue(session, out var expires) && expires > DateTimeOffset.UtcNow;
    }

    public void RevokeSession(HttpContext context)
    {
        lock (sessionSync)
            if (context.Request.Cookies.TryGetValue("tdsblive-session", out var session)) sessions.TryRemove(session, out _);
        context.Response.Cookies.Delete("tdsblive-session");
    }
}

public sealed class RequestSecurity(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ApplicationConfiguration configuration, AccessControl access, IAntiforgery antiforgery)
    {
        var request = context.Request;
        if (request.ContentLength > 65536)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        var host = request.Host.Host.Trim('[', ']');
        var loopbackHost = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);
        var configuredHost = host.Equals(configuration.Server.Host, StringComparison.OrdinalIgnoreCase);
        var allowedHost = configuration.Server.AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase);
        if (!loopbackHost && !(configuration.Server.EnableLan && (configuredHost || allowedHost)))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        var origin = request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin) && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            uri.Scheme != request.Scheme || !uri.Authority.Equals(request.Host.Value, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (request.Headers["Sec-Fetch-Site"] == "cross-site")
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        var localPeer = context.Connection.RemoteIpAddress is { } peer && IPAddress.IsLoopback(peer);
        // TestServer has no network peer; it must still use a loopback Host.
        var local = localPeer || context.Connection.RemoteIpAddress is null && loopbackHost;
        var login = request.Path == "/api/auth/login" || request.Path == "/api/auth/csrf" || request.Path == "/login" || request.Path.StartsWithSegments("/editor/assets");
        if (!local && (!configuration.Server.EnableLan || !access.IsAuthenticated(context)) && !login)
        {
            if (request.Path == "/editor") { context.Response.Redirect("/login"); return; }
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        if (context.WebSockets.IsWebSocketRequest && string.IsNullOrEmpty(origin))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method))
        {
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers.CacheControl = "no-store";
        await next(context);
    }
}
