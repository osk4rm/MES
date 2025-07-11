namespace AsistOff.MES.Multitenancy.Error;

public static class Errors
{
    public static class Tenants
    {
        public static ErrorOr.Error NotFound =>
            ErrorOr.Error.NotFound(code: "Tenant.RepositoryGet", description: "Tenant not found");

        public static ErrorOr.Error CreateFailed =>
            ErrorOr.Error.Validation(code: "Common.RepositoryCreate", description: "Error creating an entity");

    }
}