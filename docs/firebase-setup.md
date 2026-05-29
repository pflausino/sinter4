# Configuração do Firebase para Desenvolvimento

## Pré-requisitos

- .NET 10 SDK instalado
- Projeto Firebase criado no [Firebase Console](https://console.firebase.google.com/)
- Arquivo JSON da Service Account baixado do Firebase Console (Configurações do Projeto → Contas de serviço → Gerar nova chave privada)

## Configuração via User Secrets

Os valores de configuração do Firebase **nunca devem ser commitados no controle de versão**. Em ambiente de desenvolvimento, utilize o `dotnet user-secrets` para armazená-los de forma segura.

### 1. Inicializar User Secrets nos projetos

```bash
dotnet user-secrets init --project src/Api
dotnet user-secrets init --project src/Web
```

### 2. Configurar as chaves do Firebase

**Projeto Api** (precisa de todas as chaves):

```bash
dotnet user-secrets set "Firebase:ProjectId" "seu-project-id" --project src/Api
dotnet user-secrets set "Firebase:ServiceAccountPath" "/caminho/absoluto/para/service-account.json" --project src/Api
dotnet user-secrets set "Firebase:ApiKey" "sua-api-key" --project src/Api
```

**Projeto Web** (precisa apenas da ApiKey):

```bash
dotnet user-secrets set "Firebase:ApiKey" "sua-api-key" --project src/Web
```

### 3. Onde encontrar cada valor

| Chave | Onde encontrar |
|-------|---------------|
| `Firebase:ProjectId` | Firebase Console → Configurações do Projeto → Geral → ID do projeto |
| `Firebase:ServiceAccountPath` | Caminho local do arquivo JSON baixado (ex: `~/.firebase/sinterprints-sa.json`) |
| `Firebase:ApiKey` | Firebase Console → Configurações do Projeto → Geral → Chave de API da Web |

### 4. Alternativa via variáveis de ambiente

Também é possível configurar via variáveis de ambiente (útil para CI/CD e produção):

```bash
export Firebase__ProjectId="seu-project-id"
export Firebase__ServiceAccountPath="/caminho/para/service-account.json"
export Firebase__ApiKey="sua-api-key"
```

> **Importante:** O separador de seção em variáveis de ambiente é `__` (duplo underscore), não `:`.

## Segurança

- **Nunca** commite o arquivo `service-account.json` no repositório
- **Nunca** coloque valores reais em `appsettings.json` — os placeholders devem permanecer vazios
- O `.gitignore` já deve ignorar arquivos `*.json` de service account fora do projeto
- Em produção, utilize variáveis de ambiente ou um gerenciador de secrets (Azure Key Vault, AWS Secrets Manager, etc.)
