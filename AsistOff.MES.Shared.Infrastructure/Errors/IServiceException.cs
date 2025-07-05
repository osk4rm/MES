using System.Net;

namespace AsistOff.MES.Shared.Infrastructure.Errors
{
    public interface IServiceException
    {
        public HttpStatusCode StatusCode { get; }

        public string ErrorMessage { get; }

    }
}
