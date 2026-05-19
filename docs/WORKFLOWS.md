Build, test, and commit workflows for Skock. WSL2 environment — use `.exe` suffixed tools.

## Rust (sim + dockyard)

```
cargo.exe build -p sim --release       # build sim binary
cargo.exe build -p dockyard --release  # build dockyard binary
cargo.exe test                         # all Rust tests
cargo.exe test --test determinism      # determinism golden-hash tests only
cargo.exe test -p sim <test_name>      # single test by name
cargo.exe test -p dockyard             # dockyard unit tests
cargo.exe fmt                          # format
cargo.exe fmt --check                  # lint-only (CI)
```

## C# (client + tests)

```
dotnet.exe build client/skock.csproj                    # type-check (no Godot runtime)
dotnet.exe test client.tests/skock.tests.csproj         # all C# tests
dotnet.exe test client.tests/skock.tests.csproj --filter "FullyQualifiedName~<TestName>"
csharpier.exe format client/                            # format
csharpier.exe check client/                             # lint-only (CI)
```

## Make targets

```
make det       # cargo test --test determinism
make fmt       # cargo fmt + csharpier format
make fmt-check # cargo fmt --check + csharpier check
make win-all   # fmt + build sim + build godot + test rust + test C#
```

## Before committing

1. Compiles with no warnings (`cargo build` / `dotnet build`)?
2. Determinism tests pass (`make det`)?
3. Any new bare `unwrap()` in sim code?
4. Any name that made you pause — is it the right name?
5. Any function longer than ~40 lines — does it need splitting?
