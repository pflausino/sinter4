# Configuração do Firebase para Desenvolvimento

## Pré-requisitos

- .NET 10 SDK instalado
- Projeto Firebase criado no [Firebase Console](https://console.firebase.google.com/)
- Arquivo JSON da Service Account baixado do Firebase Console

## 1. Baixar a Service Account

1. Acesse o [Firebase Console](https://console.firebase.google.com/)
2. Vá em **Configurações do Projeto → Contas de serviço**
3. Clique em **Gerar nova chave privada**
4. Salve o arquivo JSON em um local seguro **fora do repositório**, por exemplo:
   ```
   ~/.firebase/sinterprints-sa.json
   ```

> **Nunca** coloque esse arquivo dentro do repositório. Ele contém a chave privada da service account.

## 2. Inicializar User Secrets nos projetos

```bash
dotnet user-secrets init --project src/Api
dotnet user-secrets init --project src/Web
```

## 3. Configurar os secrets

**Projeto Api:**

```bash
dotnet user-secrets set "Firebase:ProjectId" "seu-project-id" --project src/Api
dotnet user-secrets set "Firebase:ServiceAccountPath" "/caminho/absoluto/para/sinterprints-sa.json" --project src/Api
```

**Projeto Web** (precisa apenas da ApiKey para autenticação client-side):

```bash
dotnet user-secrets set "Firebase:ApiKey" "sua-api-key" --project src/Web
```

## 4. Onde encontrar cada valor

| Chave | Onde encontrar |
|-------|---------------|
| `Firebase:ProjectId` | Firebase Console → Configurações do Projeto → Geral → ID do projeto |
| `Firebase:ServiceAccountPath` | Caminho local onde você salvou o JSON no passo 1 |
| `Firebase:ApiKey` | Firebase Console → Configurações do Projeto → Geral → Chave de API da Web |

## 5. Como funciona no código

O `FirebaseInitializer` (em `src/Infrastructure/Auth/`) lê o path configurado e inicializa o SDK:

```csharp
var credential = GoogleCredential.FromFile(serviceAccountPath);
FirebaseApp.Create(new AppOptions { Credential = credential });
```

Isso é chamado automaticamente na inicialização da aplicação. O SDK do Firebase Admin cuida do resto (validação de tokens, gerenciamento de usuários, etc.).

## 6. Verificar a configuração

Para listar os secrets configurados:

```bash
dotnet user-secrets list --project src/Api
```

Saída esperada:

```
Firebase:ServiceAccountPath = /Users/seu-usuario/.firebase/sinterprints-sa.json
Firebase:ProjectId = sinterprints-abc123
```

## Produção

Em produção, use variáveis de ambiente no lugar dos user-secrets:

```bash
export Firebase__ProjectId="seu-project-id"
export Firebase__ServiceAccountPath="/caminho/para/service-account.json"
```

> O separador de seção em variáveis de ambiente é `__` (duplo underscore), não `:`.

Alternativas para produção:
- Montar o arquivo da service account via secret do orquestrador (Docker Secrets, Kubernetes Secrets)
- Usar um gerenciador de secrets (Azure Key Vault, AWS Secrets Manager, GCP Secret Manager)
- Em ambientes Google Cloud, usar a credencial padrão do ambiente (ADC) sem precisar de arquivo

## Segurança

- **Nunca** commite o arquivo `service-account.json` no repositório
- **Nunca** coloque valores reais em `appsettings.json` — os placeholders devem permanecer vazios
- O `.gitignore` já ignora arquivos de service account fora do projeto
- Os user-secrets ficam em `~/.microsoft/usersecrets/` e não entram no Git
