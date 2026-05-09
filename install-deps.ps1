# C++ build tools — required for Rust's default MSVC toolchain on Windows
winget install Microsoft.VisualStudio.2022.BuildTools --override "--add Microsoft.VisualStudio.Workload.VCTools --includeRecommended --quiet"

# Rust toolchain manager
winget install Rustlang.Rustup

# .NET SDK — required for Godot 4 C# scripting
winget install Microsoft.DotNet.SDK.8

# Godot 4 with .NET/C# support
winget install GodotEngine.GodotEngine.Mono
