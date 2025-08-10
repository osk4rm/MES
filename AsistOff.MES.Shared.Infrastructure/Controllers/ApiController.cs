using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AsistOff.MES.Shared.Infrastructure.Controllers
{
    [ApiController]
    [Authorize]
    public class ApiController : ControllerBase
    {
        // Controller is now simplified - exceptions are handled globally
    }
}
