# ADR-003: CUDA Kernel Launch Wrappers

Status: Accepted

## Context

CUDA kernels are launched with `<<<gridSize, blockSize>>>` syntax, which requires the caller to compute a valid grid and block configuration. Inlining this boilerplate at every call site scatters occupancy tuning logic across the codebase and makes it easy to forget `cudaDeviceSynchronize`, error checks, or stream arguments.

## Decision

Every `__global__` kernel is paired with a host-side launch wrapper that owns the configuration and call site. Two naming patterns apply:

| Kernel | Wrapper | When to use |
|---|---|---|
| `xxxKernel` | `callXxxKernel` | Synchronous / no stream |
| `xxxAsync` | `callXxxAsync` | Takes a `cudaStream_t` argument |

This convention applies to kernels with **one-dimensional** launch configurations — both `gridSize` and `blockSize` are plain `int`, not `dim3`. Kernels that require 2D or 3D thread layouts are covered by a separate ADR.

The wrapper:
1. Calls `cudaOccupancyMaxPotentialBlockSize` to determine `blockSize` and `minGridSize`.
2. Computes `gridSize = (n + blockSize - 1) / blockSize`.
3. Launches the kernel with `<<<gridSize, blockSize>>>` (or `<<<gridSize, blockSize, 0, stream>>>` for async variants).
4. Takes raw CUDA pointers (e.g. `float* param1`) matching the kernel signature exactly — no host containers.

Example:

```cpp
__global__ void equityKernel(curandState* rng, uint64_t* hands, EquityResult* out, int numTrials, int n);

void callEquityKernel(curandState* rng, uint64_t* hands, EquityResult* out, int numTrials, int n) {
    int blockSize, minGridSize;
    cudaOccupancyMaxPotentialBlockSize(&minGridSize, &blockSize, equityKernel, 0, n);
    int gridSize = (n + blockSize - 1) / blockSize;
    equityKernel<<<gridSize, blockSize>>>(rng, hands, out, numTrials, n);
}

// Async variant:
__global__ void equityAsync(curandState* rng, uint64_t* hands, EquityResult* out, int numTrials, int n);

void callEquityAsync(curandState* rng, uint64_t* hands, EquityResult* out, int numTrials, int n, cudaStream_t stream) {
    int blockSize, minGridSize;
    cudaOccupancyMaxPotentialBlockSize(&minGridSize, &blockSize, equityAsync, 0, n);
    int gridSize = (n + blockSize - 1) / blockSize;
    equityAsync<<<gridSize, blockSize, 0, stream>>>(rng, hands, out, numTrials, n);
}
```

## Rationale

- Occupancy tuning belongs next to the kernel it applies to, not scattered across callers.
- Callers become a single readable line (`callEquityKernel(...)`) with no launch syntax visible.
- Stream variants are structurally distinct by name — `Async` suffix signals that the caller must manage synchronization.
- Raw pointer signatures keep the wrapper in the CUDA layer; higher-level code that holds `std::vector` or other containers performs its own `cudaMalloc`/`cudaMemcpy` before calling the wrapper.

## Consequences

- `main_equity.cu` and `main.cu` should be refactored to move their inline `<<<>>>` launches into `callXxx` wrappers — existing code is grandfathered until touched.
- The wrapper does not call `cudaDeviceSynchronize`; that remains the caller's responsibility so that multiple async launches can be pipelined before syncing.
- Error checking (`cudaGetLastError` after the launch) is the caller's responsibility or may be wrapped in a project-wide `checkCuda()` utility.
