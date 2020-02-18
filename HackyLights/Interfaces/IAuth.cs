using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace HackyLights.Interfaces
{
    public interface IAuth
    {
        Task<ClaimsPrincipal> ValidateTokenAsync(string value);
    }
}
