# Publishing `Qint` to NuGet

The package is **not** published to NuGet.org from CI or from this repo automatically —
the NuGet API key is held by the package owner. These are the exact steps to cut and push
a release. Run them from a clean checkout of the tag you intend to ship.

## Prerequisites

- .NET SDK 8.0 or newer (`dotnet --version`)
- A NuGet.org account that owns (or co-owns) the `Qint` package id
- A NuGet API key with **Push** scope for `Qint`
  (create at https://www.nuget.org/account/apikeys)

## 1. Set the version

The version lives in [`src/Qint/Qint.csproj`](src/Qint/Qint.csproj) as `<Version>`.
It is `0.1.0` today. Bump it (semver) for every release and commit the change:

```bash
# edit <Version> in src/Qint/Qint.csproj, then:
git commit -am "Release vX.Y.Z"
git tag vX.Y.Z
git push origin main --tags
```

## 2. Build and pack (Release)

```bash
dotnet build -c Release
dotnet test  -c Release
dotnet pack  src/Qint/Qint.csproj -c Release -o artifacts
```

This produces:

- `artifacts/Qint.0.1.0.nupkg`
- `artifacts/Qint.0.1.0.snupkg` (symbols; `README.md` and the MIT license are embedded)

Sanity-check the contents:

```bash
unzip -l artifacts/Qint.0.1.0.nupkg
```

## 3. Push to NuGet.org

```bash
dotnet nuget push artifacts/Qint.0.1.0.nupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json
```

The symbol package (`.snupkg`) is pushed automatically alongside the main package when it
sits in the same folder. NuGet.org validation and indexing usually take a few minutes.

## 4. Verify

```bash
# In a throwaway project:
dotnet new console -o /tmp/qint-smoke && cd /tmp/qint-smoke
dotnet add package Qint --version 0.1.0
```

Then confirm the listing at https://www.nuget.org/packages/Qint.

## Notes

- **Never commit the API key.** Pass it via the `NUGET_API_KEY` environment variable (or
  `--api-key` inline) only.
- To automate later, add a GitHub Actions workflow triggered on `v*` tags that runs
  `dotnet pack` and `dotnet nuget push`, with the key stored as the `NUGET_API_KEY`
  repository secret.
- The package id (`Qint`) is reserved on first successful push. Subsequent versions must
  come from an owner of that id.
