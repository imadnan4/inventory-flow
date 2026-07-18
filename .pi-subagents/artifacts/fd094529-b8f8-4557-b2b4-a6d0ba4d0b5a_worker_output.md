Implemented database-enforced SKU conflict handling.

Changed files:
- `backend/src/InventoryFlow.Infrastructure/Products/EfProductCatalog.cs`
- `backend/tests/InventoryFlow.IntegrationTests/Api/ProductEndpointsTests.cs`

Validation:
- Full backend build, tests, formatting, frontend typecheck/lint/build/Prettier passed.
- 15 unit and 15 integration tests passed.
- No staged files.

Open risks/questions: None.

Recommended next step: Run final reviewer acceptance on the product slice.