BUILD_DIR := bin
OUTPUT_NAME := Server
CS_PROJ := Server/Server.csproj
LAUNCHER_NAME := ipk24chat-server
SERVER_DLL := $(BUILD_DIR)/$(OUTPUT_NAME).dll

all: build launcher

build:
	dotnet build $(CS_PROJ) -o $(BUILD_DIR)

launcher:
	@echo '#!/bin/bash' > $(LAUNCHER_NAME)
	@echo 'dotnet $(BUILD_DIR)/$(OUTPUT_NAME).dll "$$@"' >> $(LAUNCHER_NAME)
	@chmod +x $(LAUNCHER_NAME)
	@chmod +x $(SERVER_DLL) 

clean:
	rm -rf $(BUILD_DIR)
	rm -f $(LAUNCHER_NAME)