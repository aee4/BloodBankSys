# Development Setup

1. Install the .NET SDK version compatible with `global.json`.
2. Clone the repository and checkout the approved development branch.
3. Run `dotnet restore`.
4. Run `dotnet build`.
5. Run `dotnet test`.
6. Start the web project with:

```bash
dotnet run --project src/BloodLink.Web/BloodLink.Web.csproj
```

Store real local secrets with user secrets or environment variables, not in source files.
