using InventoryFlow.Application.Features.Warehouses;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace InventoryFlow.Infrastructure.Warehouses; public sealed class EfWarehouseCatalog(ApplicationDbContext db) : IWarehouseCatalog { public async Task<Warehouse> CreateAsync(Warehouse x, CancellationToken c) { db.Warehouses.Add(x); try { await db.SaveChangesAsync(c); return x; } catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 }) { throw new WarehouseNameConflictException(); } } public Task<IReadOnlyList<Warehouse>> ListActiveAsync(Guid w, CancellationToken c) => db.Warehouses.AsNoTracking().Where(x => x.WorkspaceId == w && x.ArchivedAtUtc == null).OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(c).ContinueWith(x => (IReadOnlyList<Warehouse>)x.Result, c); public Task<Warehouse?> FindAsync(Guid w, Guid id, CancellationToken c) => db.Warehouses.SingleOrDefaultAsync(x => x.WorkspaceId == w && x.Id == id, c); public async Task SaveChangesAsync(CancellationToken c) => await db.SaveChangesAsync(c); }
