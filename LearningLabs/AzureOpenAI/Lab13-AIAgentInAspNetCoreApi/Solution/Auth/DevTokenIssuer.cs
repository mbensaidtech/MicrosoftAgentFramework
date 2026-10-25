using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AIAgentWebApi.Auth;

/// <summary>
/// Development token issuer: it stands in for a real identity provider (Microsoft Entra ID, for example) so that the lab
/// runs without any setup. It signs bearer tokens (JWT) for the lab users with a random key, valid until the program stops.
/// Never issue tokens yourself in production: validate the tokens of your identity provider instead.
/// </summary>
public sealed class DevTokenIssuer
{
    private const string Issuer = "https://localhost/lab13-dev-issuer";
    private const string Audience = "lab13-agent-api";

    private readonly SymmetricSecurityKey _signingKey = new(RandomNumberGenerator.GetBytes(32));

    /// <summary>What the API checks in every token: issuer, audience, signature and expiry.</summary>
    public TokenValidationParameters ValidationParameters => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = _signingKey
    };

    /// <summary>Creates a token whose "sub" claim (the user id) is <paramref name="userId"/>, valid for 30 minutes.</summary>
    public string CreateToken(string userId) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = Issuer,
        Audience = Audience,
        Subject = new ClaimsIdentity([new Claim("sub", userId)]),
        Expires = DateTime.UtcNow.AddMinutes(30),
        SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
    });
}
