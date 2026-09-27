using AsistOff.MES.Shared.Infrastructure.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Gateway.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ErrorsController : ControllerBase
    {
        // Explicit opt-out from the global fallback authorization policy
        // (issue #351): the exception-handler re-execution path must render
        // the sanitized ProblemDetails for anonymous callers too
        // (GlobalExceptionHandler only exposes IServiceException messages,
        // generic 500 otherwise), so challenging on the error path would mask
        // the real error behind an auth challenge. This controller carries no
        // tenant or user data — only the sanitized error envelope.
        [AllowAnonymous]
        [Route("/error")]
        public IActionResult Error()
        {
            var exception = HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;

            var (statusCode, message) = exception switch
            {
                IServiceException serviceException => ((int)serviceException.StatusCode, serviceException.ErrorMessage),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occured.")
            };
            return Problem(statusCode: statusCode, title: message);
        }
    }
}
