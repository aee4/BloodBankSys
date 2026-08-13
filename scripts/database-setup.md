# Database Setup

BloodLink uses SQL Server through Entity Framework Core.

The foundation phase intentionally does not include migrations because the shared entity model must be approved first. Database Developer 1 owns the migration chain.

Development connection string placeholder:

```json
"BloodLinkDatabase": "Server=(localdb)\\mssqllocaldb;Database=BloodLink_Development;Trusted_Connection=True;MultipleActiveResultSets=true"
```

Use user secrets or environment variables for any real credential-bearing connection string.
