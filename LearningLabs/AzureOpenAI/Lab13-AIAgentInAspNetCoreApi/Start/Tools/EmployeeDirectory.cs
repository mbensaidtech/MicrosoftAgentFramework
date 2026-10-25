using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AIAgentWebApi.Tools;

/// <summary>
/// The tool of the agent: the profile of the signed-in user (mocked company directory).
/// The tool has no "user" parameter: the model cannot choose whose profile it reads. The user comes from the
/// validated token of the HTTP request, through the IServiceProvider given to the agent (as in Lab 03, scenario 3).
/// </summary>
public static class EmployeeDirectory
{
    private static readonly Dictionary<string, string> Profiles = new(StringComparer.Ordinal)
    {
        ["alice"] = "Alice Martin - Engineering team - office in Paris",
        ["bob"] = "Bob Durand - Sales team - office in Lyon"
    };

    [Description("Gets the profile of the signed-in user: name, team and office.")]
    public static string GetMyProfile(IServiceProvider services)
    {
        string? userId = services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User.FindFirst("sub")?.Value;
        return userId is not null && Profiles.TryGetValue(userId, out string? profile)
            ? profile
            : "No profile found for the signed-in user.";
    }
}
