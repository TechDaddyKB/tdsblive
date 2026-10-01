using ExtensionSuite.Host;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class AccessControlTests
{
    [Fact]
    public void ConcurrentLoginLimitAndRotationAreAtomic()
    {
        var access = new AccessControl();
        var credential = new string('a', 64);
        access.SetAdminCredential(credential);
        var sessions = new string?[64];
        Parallel.For(0, sessions.Length, index => sessions[index] = access.TryCreateSession(credential));
        Assert.Equal(32, sessions.Count(value => value is not null));
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "tdsblive-session=" + sessions.First(value => value is not null);
        Assert.True(access.IsAuthenticated(context));
        access.SetAdminCredential(new string('b', 64));
        Assert.False(access.IsAuthenticated(context));
        Assert.Null(access.TryCreateSession(credential));
        Assert.NotNull(access.TryCreateSession(new string('b', 64)));
    }
}
