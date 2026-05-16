# C++ build tools — required for Rust's default MSVC toolchain on Windows
winget install Microsoft.VisualStudio.2022.BuildTools --override "--add Microsoft.VisualStudio.Workload.VCTools --includeRecommended --quiet"

# Rust toolchain manager
winget install Rustlang.Rustup

# Install stable Rust toolchain (rustup ships with none by default)
rustup install stable
rustup default stable

# .NET SDK — required for Godot 4 C# scripting
winget install Microsoft.DotNet.SDK.8

# Godot 4 with .NET/C# support
winget install GodotEngine.GodotEngine.Mono

# Restore NuGet packages for the Godot C# project (MessagePack-CSharp etc.)
Push-Location "$PSScriptRoot/client"
dotnet restore
Pop-Location

# CSharpier — C# code formatter (used by make win-fmt and the pre-commit hook)
dotnet tool install csharpier -g

# Activate the project git hooks (formatter runs on commit)
git -C "$PSScriptRoot" config core.hooksPath .githooks
