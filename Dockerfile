FROM nvidia/cuda:13.2.1-cudnn-devel-ubuntu24.04

RUN apt-get update && apt-get install -y \
    curl \
    build-essential \
    pkg-config \
    && rm -rf /var/lib/apt/lists/*

RUN curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y

ENV PATH="/root/.cargo/bin:${PATH}"
