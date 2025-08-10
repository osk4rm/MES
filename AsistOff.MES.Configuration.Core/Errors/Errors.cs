using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Configuration.Domain.Errors;

public static class Errors
{
    public static class Warehouses
    {
        public static void ThrowNotFound() => 
            throw new NotFoundException("Warehouse not found");
            
        public static void ThrowCannotAdd() => 
            throw new ConflictException("Cannot add warehouse");
    }
}