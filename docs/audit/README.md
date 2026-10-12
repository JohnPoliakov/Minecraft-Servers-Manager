These are historical probes for commit `1980935`, before the refactor.
Their reflection targets and .NET 9 assembly references do not match the new implementation.
For current regression checks use:

```powershell
dotnet run --project Tests/Msm.RegressionTests.csproj -c Release
```

See [implementation notes](../REFACTOR-IMPLEMENTATION.md).
