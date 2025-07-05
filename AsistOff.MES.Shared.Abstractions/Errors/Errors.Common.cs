using ErrorOr;

namespace AsistOff.MES.Shared.Abstractions.Errors
{
    public static class Errors
    {
        public static class Common
        {
            public static Error RepositoryDelete =>
                Error.Validation(code: "Common.RepositoryDelete", description: "Error deleting an entity");

            public static Error RepositoryUpdate =>
                Error.Validation(code: "Common.RepositoryDelete", description: "Error updating an entity");

        }
    }
}
