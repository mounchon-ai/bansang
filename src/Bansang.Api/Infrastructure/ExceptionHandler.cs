using Bansang.Application.Abstractions;
using Bansang.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bansang.Api.Infrastructure;

/// <summary>แปลง exception เป็น ProblemDetails (RFC 9457) ให้ SPA อ่าน code ได้</summary>
public sealed class ExceptionHandler(IProblemDetailsService problems, ILogger<ExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, code, title, extra) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "not_found", ex.Message, null),
            InsufficientStockException ise => (StatusCodes.Status409Conflict, ise.Code, ex.Message,
                new Dictionary<string, object?> { ["shortageBase"] = ise.ShortageBase }),
            ConflictException ce => (StatusCodes.Status409Conflict, ce.Code, ex.Message, null),
            DomainException de => (StatusCodes.Status422UnprocessableEntity, de.Code, ex.Message, null),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg } =>
                (StatusCodes.Status409Conflict, "duplicate", $"ข้อมูลซ้ำ ({pg.ConstraintName})", null),
            BadHttpRequestException bad => (bad.StatusCode, "bad_request", ex.Message, null),
            _ => (0, "", "", (Dictionary<string, object?>?)null),
        };
        if (status == 0)
        {
            log.LogError(ex, "Unhandled exception");
            return false;
        }

        http.Response.StatusCode = status;
        var pd = new ProblemDetails { Status = status, Title = title, Type = $"https://bansang.local/errors/{code}" };
        pd.Extensions["code"] = code;
        foreach (var (k, v) in extra ?? []) pd.Extensions[k] = v;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = pd, Exception = ex });
    }
}
