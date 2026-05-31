# Deploy — SinterPrints

## Estratégia

Deploy por SSH + Docker Compose. O código fonte é copiado para a VPS via SCP e o build da imagem Docker acontece diretamente na VPS.

```
GitHub (push main) → GitHub Actions → Testes → SCP código para VPS → docker compose build + up
```

Na VPS:
```
Caddy (443/SSL auto) → API Container (8080) → PostgreSQL Container (5432)
```

## Pré-requisitos na VPS (configuração manual)

- Docker e Docker Compose instalados
- Caddy instalado e configurado
- Diretório `/opt/apps/sinter-prints` criado com permissão para `pflausino`

### Diretório da aplicação

```bash
sudo mkdir -p /opt/apps/sinter-prints
sudo chown pflausino:pflausino /opt/apps/sinter-prints
```

### Caddy

Adicionar ao Caddyfile:

```
pflausino.xyz {
    reverse_proxy localhost:8080
}
```

Recarregar: `sudo systemctl reload caddy`

### Arquivo .env

Criar `/opt/apps/sinter-prints/.env`:

```env
POSTGRES_USER=sinterprints
POSTGRES_PASSWORD=sua-senha-forte-aqui
FIREBASE_PROJECT_ID=seu-projeto-firebase
```

O `docker compose` lê automaticamente o `.env` no mesmo diretório.

## GitHub Secrets

No repositório GitHub → **Settings → Secrets and variables → Actions**:

| Secret | Descrição |
|--------|-----------|
| `VPS_SSH_KEY` | Chave SSH privada para acessar a VPS |

As variáveis de ambiente de produção (PostgreSQL, Firebase) ficam no `.env` da VPS, não nos secrets do GitHub.

### Gerar chave SSH para deploy

```bash
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/sinterprints_deploy -N ""
ssh-copy-id -i ~/.ssh/sinterprints_deploy.pub pflausino@pflausino.xyz

# Copiar o conteúdo da chave PRIVADA para o secret VPS_SSH_KEY:
cat ~/.ssh/sinterprints_deploy
```

## Como funciona o deploy

1. Push na branch `main`
2. GitHub Actions roda os testes (`dotnet test`)
3. Copia `src/` e `docker-compose.prod.yml` para `/opt/apps/sinter-prints` via SCP
4. Conecta via SSH e roda `docker compose build` + `up -d`
5. Aplicação no ar em `https://pflausino.xyz`

## Comandos úteis na VPS

```bash
cd /opt/apps/sinter-prints

# Ver logs
docker compose -f docker-compose.prod.yml logs -f api

# Status dos containers
docker compose -f docker-compose.prod.yml ps

# Restart
docker compose -f docker-compose.prod.yml restart api

# Rebuild manual
docker compose -f docker-compose.prod.yml build
docker compose -f docker-compose.prod.yml up -d
```
