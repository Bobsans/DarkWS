# DarkWS Domain Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a new standalone `DarkBoy.DarkWS` library and test project from the current `DimTim.DarkWS` source snapshot.

**Architecture:** Copy only the two tracked source directories from the clean source repository, then perform a hard public-identity rename in the new target repository. Keep runtime behavior, framework targets, package versions, and tests unchanged while removing the source-repository-only annotations dependency.

**Tech Stack:** .NET SDK, ASP.NET Core, NUnit, PowerShell, Git

**Spec:** `docs/superpowers/specs/2026-09-04-darkws-domain-migration-design.md`

## Global Constraints

- The migration is intentionally incompatible; do not add compatibility wrappers or type forwarding.
- Rename public package, assembly, and namespace identities to `DarkBoy.DarkWS` and `DarkBoy.DarkWS.Test`.
- Preserve package version `1.2.0` and target frameworks `net8.0;net9.0;net10.0`.
- Do not add a JetBrains annotations package or copy `JetBrains.Annotations.cs`.
- Do not modify or delete anything under `D:\Work\DimTim\DimTim.Sharp`.
- Do not update external consumers of `DimTim.DarkWS`.
- Initialize a new Git repository, but do not create commits or configure a remote.

---

### Task 1: Create the standalone project snapshot

**Files:**
- Create: `.gitignore`
- Create: `DarkBoy.DarkWS/**` from tracked `DimTim.DarkWS/**`
- Create: `DarkBoy.DarkWS.Test/**` from tracked `DimTim.DarkWS.Test/**`
- Create: `DarkBoy.DarkWS.sln`

**Interfaces:**
- Consumes: clean source commit `ccefd9d1a5cdf5dcbfa8c9c7afafba862857621a`
- Produces: two copied SDK-style projects and one solution in the target directory

- [ ] **Step 1: Reconfirm the source and target boundaries**

Run:

```powershell
ah --cwd 'D:\Work\DimTim\DimTim.Sharp' --json git status
ah --cwd 'D:\Work\DarkBoy\DarkWS' --json file tree . --depth 3
```

Expected: source is on `master` at `ccefd9d1` with no changes; target contains only the approved `docs/superpowers` files.

- [ ] **Step 2: Initialize the new repository and copy only tracked project files**

Run from `D:\Work\DarkBoy\DarkWS`:

```powershell
git init --initial-branch=main
Copy-Item 'D:\Work\DimTim\DimTim.Sharp\.gitignore' '.gitignore'
git -C 'D:\Work\DimTim\DimTim.Sharp' archive HEAD DimTim.DarkWS DimTim.DarkWS.Test | tar -xf - -C 'D:\Work\DarkBoy\DarkWS'
Rename-Item 'DimTim.DarkWS' 'DarkBoy.DarkWS'
Rename-Item 'DimTim.DarkWS.Test' 'DarkBoy.DarkWS.Test'
Rename-Item 'DarkBoy.DarkWS\DimTim.DarkWS.csproj' 'DarkBoy.DarkWS.csproj'
Rename-Item 'DarkBoy.DarkWS.Test\DimTim.DarkWS.Test.csproj' 'DarkBoy.DarkWS.Test.csproj'
```

Expected: the source repository remains untouched; the target contains only tracked project files under their new directory and project names.

- [ ] **Step 3: Create the solution and register both projects**

Run:

```powershell
dotnet new sln --name DarkBoy.DarkWS --format sln
dotnet sln DarkBoy.DarkWS.sln add DarkBoy.DarkWS\DarkBoy.DarkWS.csproj DarkBoy.DarkWS.Test\DarkBoy.DarkWS.Test.csproj
dotnet sln DarkBoy.DarkWS.sln list
```

Expected solution list:

```text
DarkBoy.DarkWS\DarkBoy.DarkWS.csproj
DarkBoy.DarkWS.Test\DarkBoy.DarkWS.Test.csproj
```

### Task 2: Apply the incompatible DarkBoy identity

**Files:**
- Modify: `DarkBoy.DarkWS/DarkBoy.DarkWS.csproj`
- Modify: `DarkBoy.DarkWS.Test/DarkBoy.DarkWS.Test.csproj`
- Modify: `DarkBoy.DarkWS/**/*.cs`
- Modify: `DarkBoy.DarkWS.Test/**/*.cs`
- Modify: `DarkBoy.DarkWS/readme.md`

**Interfaces:**
- Consumes: copied `DimTim.DarkWS` public types and existing NUnit integration tests
- Produces: `DarkBoy.DarkWS` package/assembly and `DarkBoy.DarkWS.*` namespaces with unchanged runtime behavior

- [ ] **Step 1: Prove the copied snapshot still contains the old identity**

Run:

```powershell
rg -n 'DimTim\.DarkWS|Dim-Tim|dimtiminc' DarkBoy.DarkWS DarkBoy.DarkWS.Test
```

Expected: matches in namespaces/imports, project metadata, README, and the test project reference.

- [ ] **Step 2: Rename namespaces and imports mechanically**

For every tracked `*.cs` file under the two target project directories, replace exactly:

```text
DimTim.DarkWS.Test -> DarkBoy.DarkWS.Test
DimTim.DarkWS      -> DarkBoy.DarkWS
```

Do the longer test namespace replacement first. Do not replace other `DimTim.*` identifiers.

- [ ] **Step 3: Make the production project standalone**

Update `DarkBoy.DarkWS/DarkBoy.DarkWS.csproj` to retain its SDK settings, version, target frameworks, README packing, and ASP.NET Core framework reference, with these exact metadata values:

```xml
<Title>DarkBoy.DarkWS</Title>
<Authors>DarkBoy</Authors>
<Copyright>DarkBoy</Copyright>
```

Remove the old `<RepositoryUrl>` element and remove the entire `<Compile Include="..\JetBrains.Annotations.cs">` item group. The project filename supplies the new implicit package ID, assembly name, and root namespace.

- [ ] **Step 4: Remove the unused annotations dependency from source**

Apply these exact reductions:

```csharp
// Attributes.cs
[AttributeUsage(AttributeTargets.Class)]
public class HandlerAttribute(string name) : Attribute

[AttributeUsage(AttributeTargets.Method)]
public class ActionAttribute(string name) : Attribute
```

```csharp
// Messages.cs: all seven message records
[Serializable]
public record InputMessage(...);
```

Remove `using JetBrains.Annotations;` from both files. Do not change record signatures or runtime attributes.

- [ ] **Step 5: Point tests at the renamed project and update the README**

Set the test reference to:

```xml
<ProjectReference Include="..\DarkBoy.DarkWS\DarkBoy.DarkWS.csproj"/>
```

Change the first README line to:

```text
DarkBoy.DarkWS
```

- [ ] **Step 6: Verify the identity invariant**

Run:

```powershell
rg -n 'DimTim|dimtim' DarkBoy.DarkWS DarkBoy.DarkWS.Test
```

Expected: no matches.

### Task 3: Verify build, tests, isolation, and repository hygiene

**Files:**
- Verify: `DarkBoy.DarkWS.sln`
- Verify: `.gitignore`
- Verify: all target project files

**Interfaces:**
- Consumes: completed standalone solution
- Produces: evidence that all supported frameworks build and test without altering the source repository

- [ ] **Step 1: Restore and build all target frameworks**

Run:

```powershell
dotnet restore DarkBoy.DarkWS.sln
dotnet build DarkBoy.DarkWS.sln --no-restore
```

Expected: exit code `0`, with `DarkBoy.DarkWS` and `DarkBoy.DarkWS.Test` built for `net8.0`, `net9.0`, and `net10.0`.

- [ ] **Step 2: Run the existing integration tests on every target framework**

Run:

```powershell
dotnet test DarkBoy.DarkWS.Test\DarkBoy.DarkWS.Test.csproj --framework net8.0 --no-build
dotnet test DarkBoy.DarkWS.Test\DarkBoy.DarkWS.Test.csproj --framework net9.0 --no-build
dotnet test DarkBoy.DarkWS.Test\DarkBoy.DarkWS.Test.csproj --framework net10.0 --no-build
```

Expected for each command: exit code `0` and all existing NUnit tests pass.

- [ ] **Step 3: Verify source preservation and target hygiene**

Run:

```powershell
ah --cwd 'D:\Work\DimTim\DimTim.Sharp' --json git status
git status --short
git check-ignore DarkBoy.DarkWS\bin DarkBoy.DarkWS\obj DarkBoy.DarkWS.Test\bin DarkBoy.DarkWS.Test\obj
```

Expected: source remains clean at the same commit; target changes are uncommitted; all four generated directories are ignored.

- [ ] **Step 4: Review the complete target change set**

Run:

```powershell
git status --short
rg -n 'DimTim|dimtim' DarkBoy.DarkWS DarkBoy.DarkWS.Test
dotnet sln DarkBoy.DarkWS.sln list
```

Expected: only intended new project files and documentation are present, the old identity search is empty, and both renamed projects are in the solution. Do not commit or configure a remote.
