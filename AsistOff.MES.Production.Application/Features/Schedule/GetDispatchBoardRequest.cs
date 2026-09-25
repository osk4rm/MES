using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Schedule;

/// <summary>
/// Read-only shift-aware dispatch board over a caller-supplied date window.
/// Day buckets carry the active shifts with roster headcounts; order rows
/// cover Released/InProgress orders that are overdue, due inside the window
/// or have no due date. Computed from existing tables only (no migration).
/// </summary>
public record GetDispatchBoardRequest(DateOnly From, DateOnly To) : ITenantRequest<DispatchBoardResponse>;
