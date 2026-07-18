# Inventory Flow API

Set the database connection string and JWT signing secret outside tracked configuration before running the API:

```bash
cd backend/src/InventoryFlow.Api
dotnet user-secrets set "Jwt:SigningKey" "replace-with-at-least-32-random-bytes"
export ConnectionStrings__InventoryFlowDatabase='Server=localhost,1433;Database=InventoryFlow;User Id=sa;Password=<strong-password>;TrustServerCertificate=True'
```

Production deployments must supply `Jwt__SigningKey` from a secret manager and set exact `Cors__AllowedOrigins__0` values. The browser refresh cookie is `HttpOnly`, `SameSite=Strict`, and requires same-site SPA/API deployment. Apply migrations with `dotnet ef database update --project src/InventoryFlow.Infrastructure --startup-project src/InventoryFlow.Api` from this directory.
