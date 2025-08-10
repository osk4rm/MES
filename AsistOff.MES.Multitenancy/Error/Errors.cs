using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Multitenancy.Error
{
    public static class Errors
    {
        public static class Tenants
        {
            public static void ThrowNotFound() =>
                throw new NotFoundException("Tenant not found");

            public static void ThrowCreateFailed() =>
                throw new RepositoryException("Error creating an entity");
        }
    }
}