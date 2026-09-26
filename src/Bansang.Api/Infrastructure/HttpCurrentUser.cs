using Bansang.Application.Abstractions;

namespace Bansang.Api.Infrastructure;

/// <summary>ชั่วคราว: อ่านผู้ใช้จาก header X-User จนกว่าจะมีระบบ login/role</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string Header = "X-User";

    public string UserId
    {
        get
        {
            var value = accessor.HttpContext?.Request.Headers[Header].ToString();
            return string.IsNullOrWhiteSpace(value) ? "anonymous" : value.Trim()[..Math.Min(value.Trim().Length, 100)];
        }
    }
}
