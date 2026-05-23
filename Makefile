MOUNTS = -v $$(pwd):/app
DISPLAY_ENV = \
	-e DISPLAY=$$DISPLAY \
	-e WAYLAND_DISPLAY=$$WAYLAND_DISPLAY \
	-e XDG_RUNTIME_DIR=/mnt/wslg/runtime-dir \
	-e PULSE_SERVER=$$PULSE_SERVER
DISPLAY_MOUNTS = \
	-v /tmp/.X11-unix:/tmp/.X11-unix \
	-v /mnt/wslg:/mnt/wslg

.PHONY: install-win install-docker docker-image docker-run fmt fmt-check det win-fmt win-build-sim win-build-rust win-build-godot install-hooks win-start-godot docker-engine-start docker-build-sim docker-build-godot win-test-godot win-test-cs install-comfyui install-comfyui-extras

install-docker:
	sudo apt-get install -y x11-xserver-utils
	sudo apt-get update
	sudo apt-get install -y ca-certificates curl gnupg lsb-release
	sudo install -m 0755 -d /etc/apt/keyrings
	curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
	sudo chmod a+r /etc/apt/keyrings/docker.gpg
	echo "deb [arch=$$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $$(. /etc/os-release && echo $$VERSION_CODENAME) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
	sudo apt-get update
	sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
	sudo usermod -aG docker $$USER
	curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg
	curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list
	sudo apt-get update
	sudo apt-get install -y nvidia-container-toolkit
	sudo nvidia-ctk runtime configure --runtime=docker
	sudo service docker restart
	@echo "Done. Re-login or run: newgrp docker"

docker-engine-start:
	sudo service docker start

docker-image:
	docker build -t skock .

docker-run:
	docker run --rm -it --gpus all $(MOUNTS) $(DISPLAY_ENV) $(DISPLAY_MOUNTS) \
		-w /app skock bash

docker-build-sim:
	docker run --rm $(MOUNTS) -w /app skock cargo build -p sim --release

docker-build-godot:
	docker run --rm $(MOUNTS) -w /app skock dotnet build client/skock.csproj

install-hooks:
	git config core.hooksPath .githooks

fmt:
	cargo fmt
	csharpier format client/

fmt-check:
	cargo fmt --check
	csharpier check client/

det:
	cargo test --test determinism

## Windows targets:

install-win:
	powershell.exe -ExecutionPolicy Bypass -File scripts/install-deps.ps1

win-test-cs:
	dotnet.exe test client.tests/skock.tests.csproj

win-build-sim:
	cargo.exe build -p sim --release

win-build-rust: win-build-sim
	cargo.exe build -p dockyard --release
	cargo.exe build -p catalog --release
	cargo.exe build -p admiral --release

win-test-rust:
	cargo.exe test

win-build-godot:
	dotnet.exe build client/skock.csproj

win-fmt:
	cargo.exe fmt
	cargo.exe clippy
	csharpier.exe format client/

win-start-godot:
	powershell.exe -ExecutionPolicy Bypass -File scripts/start-godot.ps1 -ProjectPath "$$(wslpath -w $$(pwd)/client/project.godot)"

win-all: win-fmt win-build-rust win-build-godot win-test-rust win-test-cs

install-comfyui:
	cmd.exe /c start "" powershell.exe -NoExit -ExecutionPolicy Bypass -File "$$(wslpath -w $$(pwd)/scripts/install-comfyui.ps1)"

install-comfyui-extras:
	cmd.exe /c start "" powershell.exe -NoExit -ExecutionPolicy Bypass -File "$$(wslpath -w $$(pwd)/scripts/install-comfyui-extras.ps1)"

comfyui:
	cmd.exe /c start "" /d "$$(wslpath -w $$(pwd)/ComfyUI_windows_portable)" "$$(wslpath -w $$(pwd)/ComfyUI_windows_portable/run_nvidia_gpu.bat)"