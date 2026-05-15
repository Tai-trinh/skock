MOUNTS = -v $$(pwd):/app

.PHONY: install-win install-docker docker-image docker-run fmt-check det win-build win-build-godot start_godot

install-win:
	powershell.exe -ExecutionPolicy Bypass -File install-deps.ps1

install-docker:
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

docker-image:
	docker build -t skock .

docker-run:
	docker run --rm -it --gpus all $(MOUNTS) -w /app skock bash

det:
	cargo test --test determinism

win-build:
	cargo.exe build -p sim --release

win-build-godot:
	dotnet.exe build client/skock.csproj

start_godot:
	powershell.exe -ExecutionPolicy Bypass -File start-godot.ps1 -ProjectPath "$$(wslpath -w $$(pwd)/client/project.godot)"

fmt-check:
	cargo fmt --check