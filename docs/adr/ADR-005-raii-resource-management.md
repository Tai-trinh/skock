# ADR-005: RAII for Resource Management

Status: Accepted

## Context

Resources in this project — CUDA device memory, host memory, file handles, synchronisation primitives — must be explicitly released. Manual `cudaFree` / `delete` / `close` calls scattered across functions are easy to omit on early-return paths, after exceptions, or during refactoring. Every such omission is a silent leak or use-after-free that does not produce a compile error.

## Decision

Wrap every resource in a type whose destructor releases it (RAII). Prefer standard wrappers where they exist; write a minimal custom wrapper where they do not.

| Resource | Preferred wrapper |
|---|---|
| Heap memory | `std::unique_ptr<T>` / `std::vector<T>` |
| CUDA device memory | Custom `CudaBuffer<T>` (see below) or `std::unique_ptr<T, CudaDeleter>` |
| CUDA streams | Custom `CudaStream` wrapper |
| Any other handle | Single-purpose struct with destructor |

Minimal `CudaBuffer<T>` pattern:

```cpp
template<typename T>
struct CudaBuffer {
    T* ptr = nullptr;
    explicit CudaBuffer(size_t count) {
        cudaMalloc(&ptr, sizeof(T) * count);
    }
    ~CudaBuffer() { cudaFree(ptr); }

    CudaBuffer(const CudaBuffer&)            = delete;
    CudaBuffer& operator=(const CudaBuffer&) = delete;
};
```

Raw `cudaMalloc` / `cudaFree` pairs and bare `new` / `delete` are only acceptable inside the constructor and destructor of a dedicated RAII wrapper — never in application logic or kernel launch helpers.

## Rationale

- Destructors run unconditionally on scope exit, including exception paths and early returns, eliminating the most common causes of leaks.
- Ownership is explicit at the declaration site; there is no need to search for the matching `Free` call.
- Copy and move semantics can be precisely controlled (delete copy, allow move) to make double-free impossible at compile time.

## Consequences

- New code must manage CUDA allocations through RAII wrappers, not bare `cudaMalloc`/`cudaFree`.
- Existing `main.cu` and `main_equity.cu` use bare `cudaMalloc`/`cudaFree` and are grandfathered until touched; migrate when refactoring those functions.
- RAII wrappers live in `src/cuda_utils.h` alongside `wasErr`, keeping CUDA boilerplate in one place.
