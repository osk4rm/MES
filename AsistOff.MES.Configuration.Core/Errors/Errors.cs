using ErrorOr;

namespace AsistOff.MES.Configuration.Domain.Errors;

public static class Errors
{
    public static class Warehouses
    {
        public static Error NotFoundError => Error.NotFound("Warehouse not found");
        public static Error CannotAddWarehouse => Error.Conflict("Cannot add warehouse");
    }
}