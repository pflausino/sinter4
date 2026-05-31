# ============================================================
# SinterPrints — Comandos de desenvolvimento
# ============================================================
# Uso: make <comando>
#   make dev        → Sobe banco + API + Web (tudo junto)
#   make db         → Sobe apenas o PostgreSQL
#   make api        → Roda a API
#   make web        → Roda o Blazor
#   make build      → Build de toda a solution
#   make test       → Roda todos os testes
#   make migrate    → Aplica migrations no banco
#   make clean      → Limpa artefatos de build
# ============================================================

.PHONY: dev dev-stop db db-stop api web build test migrate clean restore logs

# --- Infraestrutura ---

db: ## Sobe o PostgreSQL via Docker
	docker compose up -d postgres
	@echo "✓ PostgreSQL rodando em localhost:5432"

db-stop: ## Para o PostgreSQL
	docker compose down

logs: ## Mostra logs do PostgreSQL
	docker compose logs -f postgres

# --- Aplicação ---

restore: ## Restaura dependências NuGet
	dotnet restore

build: ## Build da solution inteira
	dotnet build

api: db ## Roda a API (sobe o banco se necessário)
	dotnet run --project src/Api

web: ## Roda o Blazor frontend
	dotnet run --project src/Web

dev: db ## Sobe banco + API + Web em paralelo (Web com hot reload)
	@echo "Building..."
	@dotnet build
	@echo "Iniciando API e Web em paralelo..."
	@trap 'pkill -P $$$$; exit' INT TERM EXIT; \
		dotnet run --no-build --project src/Api & \
		dotnet watch --project src/Web --no-hot-reload & \
		wait

dev-stop: ## Mata processos órfãos do dev (API e Web)
	@pkill -f 'dotnet.*src/Web' 2>/dev/null || true
	@pkill -f 'dotnet.*src/Api' 2>/dev/null || true
	@pkill -f 'dotnet watch' 2>/dev/null || true
	@echo "✓ Processos encerrados"

# --- Banco de Dados ---

migrate: db ## Aplica migrations do EF Core
	dotnet ef database update --project src/Infrastructure --startup-project src/Api

migration: ## Cria nova migration (uso: make migration name=NomeDaMigration)
	dotnet ef migrations add $(name) --project src/Infrastructure --startup-project src/Api

# --- Testes ---

test: ## Roda todos os testes
	dotnet test

test-watch: ## Roda testes em modo watch
	dotnet watch test

# --- Limpeza ---

clean: ## Limpa artefatos de build
	dotnet clean
	@echo "✓ Build limpo"

# --- Help ---

help: ## Mostra esta ajuda
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-15s\033[0m %s\n", $$1, $$2}'

.DEFAULT_GOAL := help
