# Copyright (c) Beztek Software Solutions. All rights reserved.

# Storage Facade — unit tests + optional live backends.
# Default: File provider only (no containers).
#
#   make test
#   make -- test --use-s3-container --use-smb-container --use-azure-container
#   make test use-s3-container use-smb-container use-azure-container
#   make containers-stop

SHELL := /bin/bash
.SHELLFLAGS := -euo pipefail -c

TESTS_PROJECT := StorageFacade.Tests/Beztek.Facade.Storage.Tests.csproj
SOLUTION := Beztek.Facade.Storage.sln

CONTAINER_CLI ?= $(shell command -v podman 2>/dev/null || command -v docker 2>/dev/null)

# GNU Make eats bare --flags; accept both --use-x-container and use-x-container.
USE_S3_CONTAINER ?= $(if $(filter --use-s3-container use-s3-container,$(MAKECMDGOALS)),1,)
USE_SMB_CONTAINER ?= $(if $(filter --use-smb-container use-smb-container,$(MAKECMDGOALS)),1,)
USE_AZURE_CONTAINER ?= $(if $(filter --use-azure-container use-azure-container,$(MAKECMDGOALS)),1,)
USE_GCS_CONTAINER ?= $(if $(filter --use-gcs-container use-gcs-container,$(MAKECMDGOALS)),1,)
# No local Alibaba OSS emulator — enables live against env-configured endpoint + ALIBABA_CLOUD_* keys.
USE_OSS_LIVE ?= $(if $(filter --use-oss-live use-oss-live,$(MAKECMDGOALS)),1,)

.PHONY: --use-s3-container use-s3-container \
	--use-smb-container use-smb-container \
	--use-azure-container use-azure-container \
	--use-gcs-container use-gcs-container \
	--use-oss-live use-oss-live
--use-s3-container use-s3-container \
--use-smb-container use-smb-container \
--use-azure-container use-azure-container \
--use-gcs-container use-gcs-container \
--use-oss-live use-oss-live:
	@:

# Images / names / ports (host → container)
S3MOCK_IMAGE ?= docker.io/adobe/s3mock:5.2.3
S3MOCK_NAME ?= storage-s3mock
# Avoid host 9000 (common MinIO / other stand-ins). s3mock listens on 9090 in-container.
S3MOCK_HOST_PORT ?= 19090

AZURITE_IMAGE ?= mcr.microsoft.com/azure-storage/azurite:3.37.0
AZURITE_NAME ?= storage-azurite
AZURITE_HOST_PORT ?= 10000

# Samba: container listens on 445; publish a high host port (no root). Client uses SMB__Port.
SAMBA_IMAGE ?= docker.io/dperson/samba:latest
SAMBA_NAME ?= storage-samba
SAMBA_USER ?= storage
SAMBA_PASSWORD ?= storage
SAMBA_SHARE ?= share
SAMBA_DOMAIN ?= WORKGROUP
SAMBA_HOST_PORT ?= 1445

GCS_IMAGE ?= docker.io/fsouza/fake-gcs-server:1.52.2
GCS_NAME ?= storage-fake-gcs
GCS_HOST_PORT ?= 4443
GCS_BUCKET ?= storage-live

# Bitbucket / pre-started services: wait only, do not create containers.
STORAGEFACADE_EXTERNAL_SERVICES ?=

LIVE_ENV_FILE := .live.env

COVERAGE_DIR := $(CURDIR)/coverage
ASSEMBLY_INCLUDE := [Beztek.Facade.Storage]*
# 0 = collect only (unit sits ~79%; live clears 85%). Override: COVERAGE_THRESHOLD=85
COVERAGE_THRESHOLD ?= 0
OPEN_CMD ?= open
# Unit-only by default. Clear for full suite after containers: COVERAGE_FILTER=
COVERAGE_FILTER ?= Category!=Live

.PHONY: help
help:
	@echo "Storage Facade"
	@echo "  make test                 - unit + live File (no containers)"
	@echo "  make test-unit            - unit tests only (exclude Category=Live)"
	@echo "  make test-live            - live providers from $(LIVE_ENV_FILE) / env"
	@echo "  make test-azure           - unit + live File + Azure (Azurite container)"
	@echo "  make test-gcs             - unit + live File + GCS (fake-gcs-server)"
	@echo "  make -- test --use-s3-container --use-azure-container --use-smb-container --use-gcs-container"
	@echo "  make -- test --use-oss-live   - File + Alibaba OSS (needs OSS__Endpoint + ALIBABA_CLOUD_* env)"
	@echo "  make coverage             - Coverlet + terminal summary (unit by default)"
	@echo "  make coverage-html        - HTML report opened in a browser"
	@echo "  make coverage-check       - fail if line coverage < 85% (or COVERAGE_THRESHOLD)"
	@echo "  make containers-stop      - stop storage-* containers"
	@echo "  make print-live-env       - show $(LIVE_ENV_FILE)"
	@echo "  make tools                - restore local dotnet tools (reportgenerator)"
	@echo ""
	@echo "Coverage overrides: COVERAGE_FILTER=  COVERAGE_THRESHOLD=85"
	@echo ""
	@echo "Container CLI: $$(command -v podman 2>/dev/null || command -v docker 2>/dev/null || echo '(none)')"

.PHONY: _require-container-cli
_require-container-cli:
	@if [ -z "$(CONTAINER_CLI)" ]; then \
		echo "Neither podman nor docker is on PATH." >&2; \
		exit 1; \
	fi; \
	echo "Using container CLI: $(CONTAINER_CLI)"

.PHONY: _wait-tcp
_wait-tcp:
	@echo "Waiting for $(LABEL) on 127.0.0.1:$(PORT)…"
	@for i in $$(seq 1 $${WAIT_TCP_SECS:-30}); do \
		if (echo >/dev/tcp/127.0.0.1/$(PORT)) >/dev/null 2>&1; then \
			echo "$(LABEL) is up."; exit 0; \
		fi; \
		sleep 1; \
	done; \
	echo "$(LABEL) did not become ready in time on 127.0.0.1:$(PORT)." >&2; exit 1

# --- containers --------------------------------------------------------------

.PHONY: container-s3
ifeq ($(STORAGEFACADE_EXTERNAL_SERVICES),1)
container-s3:
	@$(MAKE) --no-print-directory _wait-tcp PORT=$(S3MOCK_HOST_PORT) LABEL=adobe/s3mock
else
container-s3: _require-container-cli
	@status=$$($(CONTAINER_CLI) inspect -f '{{.State.Status}}' $(S3MOCK_NAME) 2>/dev/null || true); \
	if [ "$$status" = "running" ]; then \
		echo "$(S3MOCK_NAME) already running"; \
	elif [ -n "$$status" ]; then \
		echo "Starting existing $(S3MOCK_NAME)…"; \
		$(CONTAINER_CLI) start $(S3MOCK_NAME) >/dev/null; \
	elif (echo >/dev/tcp/127.0.0.1/$(S3MOCK_HOST_PORT)) >/dev/null 2>&1; then \
		echo "Host port $(S3MOCK_HOST_PORT) is in use but $(S3MOCK_NAME) is missing." >&2; \
		echo "Stop the other process or override S3MOCK_HOST_PORT=…" >&2; \
		exit 1; \
	else \
		echo "Creating $(S3MOCK_NAME)…"; \
		$(CONTAINER_CLI) run -d --name $(S3MOCK_NAME) \
			-e initialBuckets=storage-live \
			-e COM_ADOBE_TESTING_S3MOCK_STORE_INITIAL_BUCKETS=storage-live \
			-e COM_ADOBE_TESTING_S3MOCK_STORE_REGION=us-east-1 \
			-p $(S3MOCK_HOST_PORT):9090 $(S3MOCK_IMAGE); \
	fi
	@echo "Waiting for adobe/s3mock on 127.0.0.1:$(S3MOCK_HOST_PORT)…"
	@for i in $$(seq 1 60); do \
		if curl -sf -o /dev/null "http://127.0.0.1:$(S3MOCK_HOST_PORT)/" 2>/dev/null; then \
			echo "s3mock is up."; exit 0; \
		fi; \
		if (echo >/dev/tcp/127.0.0.1/$(S3MOCK_HOST_PORT)) >/dev/null 2>&1; then \
			echo "s3mock is up (port open)."; exit 0; \
		fi; \
		sleep 1; \
	done; \
	echo "s3mock did not become ready in time." >&2; exit 1
endif

.PHONY: container-azure
ifeq ($(STORAGEFACADE_EXTERNAL_SERVICES),1)
container-azure:
	@$(MAKE) --no-print-directory _wait-tcp PORT=$(AZURITE_HOST_PORT) LABEL=Azurite
else
container-azure: _require-container-cli
	@status=$$($(CONTAINER_CLI) inspect -f '{{.State.Status}}' $(AZURITE_NAME) 2>/dev/null || true); \
	if [ "$$status" = "running" ]; then \
		echo "$(AZURITE_NAME) already running"; \
	elif [ -n "$$status" ]; then \
		echo "Starting existing $(AZURITE_NAME)…"; \
		$(CONTAINER_CLI) start $(AZURITE_NAME) >/dev/null; \
	elif (echo >/dev/tcp/127.0.0.1/$(AZURITE_HOST_PORT)) >/dev/null 2>&1; then \
		echo "Host port $(AZURITE_HOST_PORT) is in use but $(AZURITE_NAME) is missing." >&2; \
		echo "Stop the other process or override AZURITE_HOST_PORT=…" >&2; \
		exit 1; \
	else \
		echo "Creating $(AZURITE_NAME)…"; \
		$(CONTAINER_CLI) run -d --name $(AZURITE_NAME) \
			-p $(AZURITE_HOST_PORT):10000 \
			$(AZURITE_IMAGE) \
			azurite-blob --blobHost 0.0.0.0 --blobPort 10000 --skipApiVersionCheck; \
	fi
	@$(MAKE) --no-print-directory _wait-http URL=http://127.0.0.1:$(AZURITE_HOST_PORT)/$(AZURITE_ACCOUNT) LABEL=Azurite
endif

.PHONY: _wait-http
_wait-http:
	@echo "Waiting for $(LABEL) at $(URL)…"
	@for i in $$(seq 1 60); do \
		if curl -sf -o /dev/null "$(URL)" 2>/dev/null; then \
			echo "$(LABEL) is up."; exit 0; \
		fi; \
		if (echo >/dev/tcp/127.0.0.1/$$(echo "$(URL)" | sed -n 's|.*:\([0-9][0-9]*\).*|\1|p')) >/dev/null 2>&1; then \
			echo "$(LABEL) is up (port open)."; exit 0; \
		fi; \
		sleep 1; \
	done; \
	echo "$(LABEL) did not become ready in time at $(URL)." >&2; exit 1

.PHONY: container-smb
ifeq ($(STORAGEFACADE_EXTERNAL_SERVICES),1)
container-smb:
	@$(MAKE) --no-print-directory _wait-tcp PORT=$(SAMBA_HOST_PORT) LABEL=Samba
else
container-smb: _require-container-cli
	@status=$$($(CONTAINER_CLI) inspect -f '{{.State.Status}}' $(SAMBA_NAME) 2>/dev/null || true); \
	if [ "$$status" = "running" ]; then \
		echo "$(SAMBA_NAME) already running"; \
	elif [ -n "$$status" ]; then \
		echo "Starting existing $(SAMBA_NAME)…"; \
		$(CONTAINER_CLI) start $(SAMBA_NAME) >/dev/null; \
	elif (echo >/dev/tcp/127.0.0.1/$(SAMBA_HOST_PORT)) >/dev/null 2>&1; then \
		echo "Host port $(SAMBA_HOST_PORT) is in use but $(SAMBA_NAME) is missing." >&2; \
		echo "Stop the other process or override SAMBA_HOST_PORT=…" >&2; \
		exit 1; \
	else \
		echo "Creating $(SAMBA_NAME) (host $(SAMBA_HOST_PORT)→container 445)…"; \
		if ! $(CONTAINER_CLI) run -d --name $(SAMBA_NAME) \
			-p $(SAMBA_HOST_PORT):445 \
			$(SAMBA_IMAGE) \
			-u "$(SAMBA_USER);$(SAMBA_PASSWORD)" \
			-s "$(SAMBA_SHARE);/share;yes;no;no;$(SAMBA_USER)" \
			-p; then \
			echo "Failed to start Samba on host port $(SAMBA_HOST_PORT)." >&2; \
			exit 1; \
		fi; \
		sleep 2; \
		status=$$($(CONTAINER_CLI) inspect -f '{{.State.Status}}' $(SAMBA_NAME) 2>/dev/null || true); \
		if [ "$$status" != "running" ]; then \
			echo "$(SAMBA_NAME) exited after start (status=$$status)." >&2; \
			$(CONTAINER_CLI) logs $(SAMBA_NAME) 2>&1 | tail -40 >&2 || true; \
			$(CONTAINER_CLI) rm -f $(SAMBA_NAME) >/dev/null 2>&1 || true; \
			exit 1; \
		fi; \
	fi
	@$(MAKE) --no-print-directory _wait-tcp PORT=$(SAMBA_HOST_PORT) LABEL=Samba
endif

.PHONY: container-gcs
ifeq ($(STORAGEFACADE_EXTERNAL_SERVICES),1)
container-gcs:
	@$(MAKE) --no-print-directory _wait-tcp PORT=$(GCS_HOST_PORT) LABEL=fake-gcs-server
else
container-gcs: _require-container-cli
	@status=$$($(CONTAINER_CLI) inspect -f '{{.State.Status}}' $(GCS_NAME) 2>/dev/null || true); \
	if [ "$$status" = "running" ]; then \
		echo "$(GCS_NAME) already running"; \
	elif [ -n "$$status" ]; then \
		echo "Starting existing $(GCS_NAME)…"; \
		$(CONTAINER_CLI) start $(GCS_NAME) >/dev/null; \
	elif (echo >/dev/tcp/127.0.0.1/$(GCS_HOST_PORT)) >/dev/null 2>&1; then \
		echo "Host port $(GCS_HOST_PORT) is in use but $(GCS_NAME) is missing." >&2; \
		echo "Stop the other process or override GCS_HOST_PORT=…" >&2; \
		exit 1; \
	else \
		echo "Creating $(GCS_NAME)…"; \
		$(CONTAINER_CLI) run -d --name $(GCS_NAME) \
			-p $(GCS_HOST_PORT):4443 \
			$(GCS_IMAGE) \
			-scheme http -port 4443 -external-url "http://127.0.0.1:$(GCS_HOST_PORT)"; \
	fi
	@$(MAKE) --no-print-directory _wait-http URL=http://127.0.0.1:$(GCS_HOST_PORT)/storage/v1/ LABEL=fake-gcs-server
endif

.PHONY: ensure-live-containers
ensure-live-containers:
	@if [ "$(USE_S3_CONTAINER)" = "1" ]; then $(MAKE) --no-print-directory container-s3; fi
	@if [ "$(USE_AZURE_CONTAINER)" = "1" ]; then $(MAKE) --no-print-directory container-azure; fi
	@if [ "$(USE_SMB_CONTAINER)" = "1" ]; then $(MAKE) --no-print-directory container-smb; fi
	@if [ "$(USE_GCS_CONTAINER)" = "1" ]; then $(MAKE) --no-print-directory container-gcs; fi
	@if [ "$(USE_OSS_LIVE)" = "1" ]; then \
		if [ -z "$${OSS__Endpoint:-}" ] && [ -z "$${OSS_ENDPOINT:-}" ]; then \
			echo "USE_OSS_LIVE requires OSS__Endpoint (and ALIBABA_CLOUD_ACCESS_KEY_ID/SECRET) in the environment." >&2; \
			echo "There is no local Alibaba OSS emulator." >&2; \
			exit 1; \
		fi; \
		if [ -z "$${ALIBABA_CLOUD_ACCESS_KEY_ID:-}" ] && [ -z "$${OSS_ACCESS_KEY_ID:-}" ]; then \
			echo "USE_OSS_LIVE requires ALIBABA_CLOUD_ACCESS_KEY_ID / ALIBABA_CLOUD_ACCESS_KEY_SECRET (or OSS_ACCESS_KEY_*)." >&2; \
			exit 1; \
		fi; \
		echo "OSS live: using OSS__Endpoint from environment (credentials from ALIBABA_CLOUD_* / OSS_*)."; \
	fi

.PHONY: containers-stop
containers-stop: _require-container-cli
	@$(CONTAINER_CLI) stop $(S3MOCK_NAME) $(AZURITE_NAME) $(SAMBA_NAME) $(GCS_NAME) 2>/dev/null || true
	@$(CONTAINER_CLI) rm $(S3MOCK_NAME) $(AZURITE_NAME) $(SAMBA_NAME) $(GCS_NAME) 2>/dev/null || true
	@echo "Stopped local storage-* containers (if any)."

# --- live env ----------------------------------------------------------------

# Well-known Azurite account key (public, not a secret).
AZURITE_ACCOUNT ?= devstoreaccount1
AZURITE_KEY ?= Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==

.PHONY: live-env-file
live-env-file:
	@rm -f $(LIVE_ENV_FILE)
	@providers="file"; \
	{ echo "# Generated by make — do not commit"; } > $(LIVE_ENV_FILE); \
	if [ "$(USE_S3_CONTAINER)" = "1" ]; then \
		providers="$$providers,s3"; \
		endpoint="http://127.0.0.1:$(S3MOCK_HOST_PORT)"; \
		{ \
			echo "S3__BucketName=storage-live"; \
			echo "S3__Region=us-east-1"; \
			echo "S3__AccessKeyId=test"; \
			echo "S3__SecretAccessKey=test"; \
			echo "S3__ServiceUrl=$$endpoint"; \
		} >> $(LIVE_ENV_FILE); \
		echo "Live env: S3 → $$endpoint"; \
	fi; \
	if [ "$(USE_AZURE_CONTAINER)" = "1" ]; then \
		providers="$$providers,azure"; \
		uri="http://127.0.0.1:$(AZURITE_HOST_PORT)/$(AZURITE_ACCOUNT)"; \
		conn="DefaultEndpointsProtocol=http;AccountName=$(AZURITE_ACCOUNT);AccountKey=$(AZURITE_KEY);BlobEndpoint=$$uri;"; \
		{ \
			echo "AZURE__BlobServiceUri=$$uri"; \
			echo "AZURE__AccountKey='$(AZURITE_KEY)'"; \
			echo "AZURE__ContainerName=storage-live"; \
			echo "AZURE__ConnectionString='$$conn'"; \
		} >> $(LIVE_ENV_FILE); \
		echo "Live env: Azure/Azurite → $$uri (container storage-live)"; \
	fi; \
	if [ "$(USE_SMB_CONTAINER)" = "1" ]; then \
		providers="$$providers,smb"; \
		{ \
			echo "SMB__Host=127.0.0.1"; \
			echo "SMB__Port=$(SAMBA_HOST_PORT)"; \
			echo "SMB__Share=$(SAMBA_SHARE)"; \
			echo "SMB__Username=$(SAMBA_USER)"; \
			echo "SMB__Password=$(SAMBA_PASSWORD)"; \
			echo "SMB__Domain=$(SAMBA_DOMAIN)"; \
		} >> $(LIVE_ENV_FILE); \
		echo "Live env: SMB → //127.0.0.1:$(SAMBA_HOST_PORT)/$(SAMBA_SHARE)"; \
	fi; \
	if [ "$(USE_GCS_CONTAINER)" = "1" ]; then \
		providers="$$providers,gcs"; \
		uri="http://127.0.0.1:$(GCS_HOST_PORT)/storage/v1/"; \
		{ \
			echo "GCS__BucketName=$(GCS_BUCKET)"; \
			echo "GCS__ServiceUri=$$uri"; \
		} >> $(LIVE_ENV_FILE); \
		echo "Live env: GCS → $$uri (bucket $(GCS_BUCKET), unauthenticated emulator)"; \
	fi; \
	if [ "$(USE_OSS_LIVE)" = "1" ]; then \
		providers="$$providers,oss"; \
		endpoint="$${OSS__Endpoint:-$${OSS_ENDPOINT:-}}"; \
		bucket="$${OSS__BucketName:-$${OSS_BUCKET:-storage-live}}"; \
		{ \
			echo "OSS__Endpoint=$$endpoint"; \
			echo "OSS__BucketName=$$bucket"; \
		} >> $(LIVE_ENV_FILE); \
		echo "Live env: OSS → $$endpoint / $$bucket (credentials from process env)"; \
	fi; \
	echo "STORAGEFACADE_LIVE_PROVIDERS=$$providers" >> $(LIVE_ENV_FILE); \
	echo "Live providers: $$providers"

.PHONY: print-live-env
print-live-env:
	@if [ -s "$(LIVE_ENV_FILE)" ]; then cat "$(LIVE_ENV_FILE)"; else echo "(no $(LIVE_ENV_FILE) — run make live-env-file or make test with container flags)"; fi

define load-live-env
	set -a; \
	if [ -s "$(LIVE_ENV_FILE)" ]; then . "./$(LIVE_ENV_FILE)"; fi; \
	set +a
endef

# --- test targets ------------------------------------------------------------

.PHONY: restore
restore:
	dotnet restore $(SOLUTION)

.PHONY: build
build: restore
	dotnet build $(SOLUTION) --no-restore

.PHONY: test-unit
test-unit: build
	dotnet test $(TESTS_PROJECT) --no-build --filter "Category!=Live"

.PHONY: test-azure
test-azure:
	@$(MAKE) --no-print-directory test USE_AZURE_CONTAINER=1

.PHONY: test-gcs
test-gcs:
	@$(MAKE) --no-print-directory test USE_GCS_CONTAINER=1

.PHONY: test-live
test-live: build
	@$(load-live-env); \
	export STORAGEFACADE_LIVE_PROVIDERS="$${STORAGEFACADE_LIVE_PROVIDERS:-file}"; \
	echo "STORAGEFACADE_LIVE_PROVIDERS=$$STORAGEFACADE_LIVE_PROVIDERS"; \
	dotnet test $(TESTS_PROJECT) --no-build --filter "Category=Live"

.PHONY: test
test: ensure-live-containers live-env-file build
	@$(load-live-env); \
	export STORAGEFACADE_LIVE_PROVIDERS="$${STORAGEFACADE_LIVE_PROVIDERS:-file}"; \
	echo "STORAGEFACADE_LIVE_PROVIDERS=$$STORAGEFACADE_LIVE_PROVIDERS"; \
	dotnet test $(TESTS_PROJECT) --no-build

.PHONY: ci-test
ci-test:
	@$(MAKE) --no-print-directory test \
		USE_S3_CONTAINER=1 \
		USE_AZURE_CONTAINER=1 \
		USE_SMB_CONTAINER=1 \
		STORAGEFACADE_EXTERNAL_SERVICES=1 \
		S3MOCK_HOST_PORT=9090 \
		AZURITE_HOST_PORT=10000

.PHONY: tools
tools:
	dotnet tool restore

.PHONY: coverage
coverage: tools
	@rm -rf "$(COVERAGE_DIR)"
	@mkdir -p "$(COVERAGE_DIR)"
	dotnet test $(TESTS_PROJECT) \
		$(if $(COVERAGE_FILTER),--filter "$(COVERAGE_FILTER)",) \
		/p:CollectCoverage=true \
		/p:CoverletOutputFormat=cobertura \
		/p:CoverletOutput="$(COVERAGE_DIR)/" \
		/p:Include='$(ASSEMBLY_INCLUDE)' \
		/p:Threshold=$(COVERAGE_THRESHOLD) \
		/p:ThresholdType=line
	@dotnet reportgenerator \
		"-reports:$(COVERAGE_DIR)/coverage.cobertura.xml" \
		"-targetdir:$(COVERAGE_DIR)/report" \
		-reporttypes:TextSummary \
		-verbosity:Warning
	@cat "$(COVERAGE_DIR)/report/Summary.txt"

.PHONY: coverage-html
coverage-html: coverage
	@dotnet reportgenerator \
		"-reports:$(COVERAGE_DIR)/coverage.cobertura.xml" \
		"-targetdir:$(COVERAGE_DIR)/html" \
		-reporttypes:Html \
		-verbosity:Warning
	@echo "opening $(COVERAGE_DIR)/html/index.html"
	@$(OPEN_CMD) "$(COVERAGE_DIR)/html/index.html" 2>/dev/null \
		|| xdg-open "$(COVERAGE_DIR)/html/index.html" 2>/dev/null \
		|| true

.PHONY: coverage-check
coverage-check: coverage
	@line=$$(sed -n 's/^[[:space:]]*Line coverage:[[:space:]]*\([0-9.]*\)%.*/\1/p' "$(COVERAGE_DIR)/report/Summary.txt" | head -1); \
	want="$(COVERAGE_THRESHOLD)"; \
	if [ -z "$$line" ]; then echo "could not parse line coverage from Summary.txt"; exit 1; fi; \
	if [ "$$want" = "0" ]; then want=85; fi; \
	awk -v got="$$line" -v w="$$want" 'BEGIN { \
		if (got+0 < w+0) { printf "FAIL: line coverage %.1f%% < %s%%\n", got, w; exit 1 } \
		printf "OK: line coverage %.1f%% >= %s%%\n", got, w; exit 0 }'

.PHONY: clean
clean:
	dotnet clean $(SOLUTION)
	rm -rf "$(COVERAGE_DIR)"
