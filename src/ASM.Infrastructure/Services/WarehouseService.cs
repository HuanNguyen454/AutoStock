using ASM.Application.Contracts;
using ASM.Application.Interfaces;
using ASM.Domain.Entities;
using ASM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ASM.Infrastructure.Services;

public class WarehouseService(
    AppDbContext dbContext,
    ICurrentUserService currentUser) : IWarehouseService
{
    public async Task<IReadOnlyCollection<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Warehouses
            .Where(x => x.TenantId == currentUser.TenantId)
            .OrderBy(x => x.Name)
            .Select(x => new WarehouseDto(x.Id, x.Name, x.Code, x.Address))
            .ToListAsync(cancellationToken);
    }

    public async Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var warehouse = new Warehouse
        {
            TenantId = currentUser.TenantId,
            Name = request.Name,
            Code = request.Code,
            Address = request.Address
        };

        dbContext.Warehouses.Add(warehouse);
        await SaveAuditAsync("CreateWarehouse", nameof(Warehouse), warehouse.Id, warehouse.Name, cancellationToken);
        return new WarehouseDto(warehouse.Id, warehouse.Name, warehouse.Code, warehouse.Address);
    }

    public async Task<AreaDto> AddAreaAsync(CreateAreaRequest request, CancellationToken cancellationToken)
    {
        await EnsureWarehouseInTenantAsync(request.WarehouseId, cancellationToken);
        var area = new Area
        {
            TenantId = currentUser.TenantId,
            WarehouseId = request.WarehouseId,
            Name = request.Name
        };
        dbContext.Areas.Add(area);
        await SaveAuditAsync("CreateArea", nameof(Area), area.Id, area.Name, cancellationToken);
        return new AreaDto(area.Id, area.WarehouseId, area.Name);
    }

    public async Task<RackDto> AddRackAsync(CreateRackRequest request, CancellationToken cancellationToken)
    {
        var area = await dbContext.Areas.FirstOrDefaultAsync(
            x => x.Id == request.AreaId && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Không tìm thấy khu vực.");

        var rack = new Rack
        {
            TenantId = currentUser.TenantId,
            AreaId = area.Id,
            Name = request.Name
        };
        dbContext.Racks.Add(rack);
        await SaveAuditAsync("CreateRack", nameof(Rack), rack.Id, rack.Name, cancellationToken);
        return new RackDto(rack.Id, rack.AreaId, rack.Name);
    }

    public async Task<SlotDto> AddSlotAsync(CreateSlotRequest request, CancellationToken cancellationToken)
    {
        var rack = await dbContext.Racks.FirstOrDefaultAsync(
            x => x.Id == request.RackId && x.TenantId == currentUser.TenantId,
            cancellationToken) ?? throw new InvalidOperationException("Không tìm thấy rack.");

        var slot = new Slot
        {
            TenantId = currentUser.TenantId,
            RackId = rack.Id,
            Name = request.Name,
            IsOccupied = false
        };
        dbContext.Slots.Add(slot);
        await SaveAuditAsync("CreateSlot", nameof(Slot), slot.Id, slot.Name, cancellationToken);
        return new SlotDto(slot.Id, slot.RackId, slot.Name, slot.IsOccupied);
    }

    public async Task<WarehouseLayoutDto?> GetLayoutAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var warehouse = await dbContext.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == warehouseId && x.TenantId == currentUser.TenantId, cancellationToken);
        if (warehouse is null)
        {
            return null;
        }

        var areas = await dbContext.Areas
            .AsNoTracking()
            .Where(x => x.WarehouseId == warehouseId && x.TenantId == currentUser.TenantId)
            .Include(x => x.Racks)
            .ThenInclude(x => x.Slots)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return new WarehouseLayoutDto(
            new WarehouseDto(warehouse.Id, warehouse.Name, warehouse.Code, warehouse.Address),
            areas.Select(area => new AreaDetailDto(
                area.Id,
                area.Name,
                area.Racks
                    .OrderBy(r => r.Name)
                    .Select(rack => new RackDetailDto(
                        rack.Id,
                        rack.Name,
                        rack.Slots
                            .OrderBy(slot => slot.Name)
                            .Select(slot => new SlotDto(slot.Id, slot.RackId, slot.Name, slot.IsOccupied))
                            .ToList()))
                    .ToList()))
            .ToList());
    }

    private async Task EnsureWarehouseInTenantAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Warehouses.AnyAsync(
            x => x.Id == warehouseId && x.TenantId == currentUser.TenantId,
            cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("Không tìm thấy kho.");
        }
    }

    private async Task SaveAuditAsync(string action, string entityName, Guid entityId, string detail, CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            TenantId = currentUser.TenantId,
            PerformedByUserId = currentUser.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Detail = detail
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
