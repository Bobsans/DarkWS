# DarkWS Domain Migration

## Goal

Create a new standalone `DarkBoy.DarkWS` project from the current
`DimTim.DarkWS` and `DimTim.DarkWS.Test` source snapshot. The migration is
intentionally incompatible and does not preserve Git history or the old public
package and namespace identities.

## Target Layout

```text
DarkWS/
├── DarkBoy.DarkWS/
├── DarkBoy.DarkWS.Test/
├── DarkBoy.DarkWS.sln
└── .gitignore
```

The target directory becomes a new Git repository. No commit or remote is
created as part of the migration.

## Identity Changes

- Rename the production project and package to `DarkBoy.DarkWS`.
- Rename the test project to `DarkBoy.DarkWS.Test`.
- Replace `DimTim.DarkWS` namespaces and imports with `DarkBoy.DarkWS`.
- Replace `DimTim.DarkWS.Test` namespaces and imports with
  `DarkBoy.DarkWS.Test`.
- Replace DimTim ownership metadata with DarkBoy metadata and remove the old
  repository URL until a new remote URL exists.
- Preserve the current package version and target frameworks.

No compatibility wrappers, type forwarding, or duplicate packages are added.

## Dependencies

The production project currently compiles a repository-level
`JetBrains.Annotations.cs` file that is outside both projects. Its only uses are
the optional `PublicAPI` and `UsedImplicitly` annotations. Remove those
annotations and the external compile item instead of copying the vendored file
or adding a new package dependency.

The test project continues to reference the production project and keeps its
existing NUnit and ASP.NET Core test dependencies.

## Source Preservation

The source repository at `D:\Work\DimTim\DimTim.Sharp` remains unchanged. The
migration copies the current source snapshot and performs all renaming only in
`D:\Work\DarkBoy\DarkWS`.

Existing consumers of the old `DimTim.DarkWS` package are outside this task and
remain unchanged. They will continue using the old package until explicitly
migrated.

## Verification

1. Confirm the source repository is still clean.
2. Confirm no `DimTim` identifiers or old project paths remain in the target.
3. Restore, build, and run tests for `net8.0`, `net9.0`, and `net10.0`.
4. Confirm generated build artifacts are ignored by the new repository.

## Rollback

Rollback is deleting the newly created target contents before they are
published. The original source repository is retained and is not modified.
