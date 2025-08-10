using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Shared.Abstractions.Errors
{
    public static class Errors
    {
        public static class Common
        {
            public static void ThrowRepositoryDeleteError()
                => throw new RepositoryException("Error deleting an entity");

            public static void ThrowRepositoryUpdateError()
                => throw new RepositoryException("Error updating an entity");
        }
    }
}
