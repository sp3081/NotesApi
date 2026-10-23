using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace NotesApi.Tests.Helpers;

public static class ControllerExtensions
{
    // Simulates a request made with a valid token for the given user id,
    // the same "sub" claim the real JWT carries.
    public static T AsUser<T>(this T controller, int userId) where T : ControllerBase
    {
        var identity = new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
            authenticationType: "Test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }
}
