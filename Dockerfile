FROM nvidia/cuda:13.2.1-cudnn-devel-ubuntu24.04

ENV DEBIAN_FRONTEND=noninteractive
ENV GODOT_VERSION=4.6.2

RUN apt-get update && apt-get install -y \
    curl wget unzip \
    build-essential pkg-config \
    # .NET 8 SDK (for Godot C# scripting)
    dotnet-sdk-8.0 \
    # X11 display
    libx11-6 libxcursor1 libxinerama1 libxrandr2 libxrender1 libxi6 libxext6 libxfixes3 \
    libxkbcommon0 \
    # Wayland fallback
    libwayland-client0 libwayland-cursor0 libwayland-egl1 \
    # OpenGL + EGL + Mesa software renderer
    libgl1 libgles2 libglu1-mesa libegl1 libgl1-mesa-dri \
    # Wayland decorations
    libdecor-0-0 \
    # Audio
    libasound2t64 libpulse0 \
    # Font rendering
    libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

# Rust toolchain
RUN curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y
ENV PATH="/root/.cargo/bin:${PATH}"

# Godot 4 Mono (Linux x86_64)
RUN wget -q \
    "https://github.com/godotengine/godot/releases/download/${GODOT_VERSION}-stable/Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64.zip" \
    -O /tmp/godot.zip \
    && unzip /tmp/godot.zip -d /opt/ \
    && ln -s \
    "/opt/Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64/Godot_v${GODOT_VERSION}-stable_mono_linux.x86_64" \
    /usr/local/bin/godot \
    && rm /tmp/godot.zip
