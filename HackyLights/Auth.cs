using HackyLights.Interfaces;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace HackyLights
{
    public class Auth : IAuth
    {
        private readonly string ISSUER = "https://sts.windows.net/94381a35-03a2-46b4-b050-3782b51a11e5/";
        private readonly string AUDIENCE = "api://60d05312-3ee8-4aca-a078-9fc018f4a7b9";
        private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

        public Auth()
        {
            HttpDocumentRetriever documentRetriever = new HttpDocumentRetriever();
            documentRetriever.RequireHttps = ISSUER.StartsWith("https://");

            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{ISSUER}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                documentRetriever);
        }

        public async Task<ClaimsPrincipal> ValidateTokenAsync(string value)
        {
            var config = await _configurationManager.GetConfigurationAsync(CancellationToken.None);
            var issuer = ISSUER;
            var audience = AUDIENCE;

            var validationParameter = new TokenValidationParameters()
            {
                RequireSignedTokens = true,
                ValidAudience = audience,
                ValidateAudience = true,
                ValidIssuer = issuer,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                IssuerSigningKeys = config.SigningKeys
            };

            ClaimsPrincipal result = null;
            var tries = 0;

            while (result == null && tries <= 1)
            {
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    result = handler.ValidateToken(value, validationParameter, out var token);
                }
                catch (SecurityTokenSignatureKeyNotFoundException)
                {
                    // This exception is thrown if the signature key of the JWT could not be found.
                    // This could be the case when the issuer changed its signing keys, so we trigger a 
                    // refresh and retry validation.
                    _configurationManager.RequestRefresh();
                    tries++;
                }
                catch (SecurityTokenException)
                {
                    return null;
                }
            }

            return result;
        }
    }
}
