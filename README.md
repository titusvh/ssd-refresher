# ssd-refresher

`ssd-refresher` is a .NET solution intended to refresh SSD data by controlled rewrite workflows.

## Branch strategy

- `main` = stable branch.
- `develop` = integration branch.
- Feature branches must target `develop`.

## Current status

This PR introduces only the project skeleton and CI pipeline:

- .NET 8 solution structure (`Core`, `Cli`, `Core.Tests`)
- basic CLI entrypoint
- baseline unit test setup (xUnit, Shouldly, Moq)
- GitHub Actions build/test workflow

Planned v1 scope is a **read-only scan + manifest** flow, but no SSD refresh functionality is implemented in this skeleton PR.
