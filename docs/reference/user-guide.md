# Guia do Usuário

Este guia explica como usar o SinterPrints no dia a dia: entrar no sistema, encontrar fichas e manter o cadastro atualizado.

## Conceito: a ficha de arquivo

Cada trabalho de arte final é registrado como uma **ficha**. Uma ficha guarda:

| Campo | Obrigatório | Descrição |
| --- | --- | --- |
| Nome | Sim | Nome do trabalho/arquivo (até 200 caracteres). Ex.: "Logo Empresa XYZ". |
| Tipo de Arquivo | Sim | Software ou formato: CorelDRAW, Photoshop, Illustrator, Inkscape, PDF, InDesign, PageMaker, JPEG, PNG, TIFF, EPS, LegacyOld, Unknown, Other. |
| Cliente | Sim | Nome do cliente (até 200 caracteres). |
| Data | Sim | Data do trabalho. |
| Número do Arquivo | Sim | Número inteiro de 0 a 99.999.999.999. Fichas antigas sem número usam `0`. |
| Número do Disquete | Não | Número da mídia física (disquete, CD, HD) onde o arquivo está guardado. |

## 1. Entrar no sistema

1. Acesse o endereço do sistema. A tela de login abre em `/` ou `/login`.
2. Informe **e-mail** e **senha** e clique em **Entrar**.
3. Em caso de erro, a mensagem indica o motivo:
   - "Email ou senha incorretos."
   - "Sua conta está desativada. Contate o administrador."
   - "Muitas tentativas. Tente novamente mais tarde."

A sessão é renovada automaticamente enquanto você usa o sistema. Se a renovação falhar, você volta para a tela de login.

Para sair, use **Sair** no rodapé do menu lateral.

> As contas são criadas pelo administrador no Firebase. Não há cadastro de usuário dentro do sistema.

## 2. Menu

| Item | Para que serve |
| --- | --- |
| Home | Tela inicial de boas-vindas. |
| Arquivos | Lista de fichas: busca, ordenação, cadastro, edição e exclusão. |
| Health | Mostra se a API e o banco de dados estão respondendo. |

## 3. Encontrar fichas

Abra **Arquivos**. A lista mostra o total de registros e carrega mais 50 fichas automaticamente conforme você rola a página.

### Buscar

1. Digite no campo **"Buscar por nome do arquivo ou cliente..."**.
2. Pressione **Enter** ou clique em **Buscar**.

Como a busca funciona:

- Procura no **nome** e no **cliente** ao mesmo tempo.
- **Ignora acentos e maiúsculas**: `joao` encontra "João", `CAFE` encontra "Café".
- Encontra **partes de palavras**: `log` encontra "Logo" e "Catálogo".
- **Vários termos** precisam aparecer todos: `logo padaria` encontra fichas que tenham "logo" **e** "padaria" (em nome ou cliente).
- Limite de 200 caracteres.

Para voltar à lista completa, clique em **✕ Limpar busca**.

### Ordenar

- **Computador**: clique no título da coluna (Nome, Tipo, Nº Arquivo, Cliente, Data, Nº Disquete). Clique de novo para inverter a ordem.
- **Celular**: use o seletor **"Ordenar por"** e o botão ↑/↓ ao lado.

Ao abrir a página, a lista vem ordenada pelo **Nº Arquivo, do maior para o menor** (os mais recentes primeiro).

### Escolher colunas

Clique no botão de **filtro (funil)** ao lado da busca e marque ou desmarque as colunas. Pelo menos uma coluna fica sempre visível. Por padrão, "Nº Disquete" vem oculta.

## 4. Cadastrar uma ficha

1. Em **Arquivos**, clique em **+ Nova Ficha**.
2. Preencha os campos. Pressione **Enter** para pular para o próximo campo. No último, Enter salva.
3. Clique em **Salvar Ficha**.

Depois de salvar, aparece a mensagem de sucesso e o formulário é limpo para o próximo cadastro.

**Dica: cadeado do Número do Arquivo.** Ao cadastrar várias fichas seguidas do mesmo arquivo, clique no cadeado 🔓 ao lado de "Número do Arquivo" para travá-lo (🔒). O número fica preenchido depois de salvar, e os outros campos são limpos.

## 5. Editar uma ficha

Na lista, clique no lápis ✏️ (ou em **Editar** no celular), altere os campos e salve. Se a ficha não existir mais, o sistema avisa.

## 6. Excluir uma ficha

Clique na lixeira 🗑️ (ou em **Excluir** no celular) e confirme. **A exclusão é permanente e não pode ser desfeita.**

## Problemas comuns

| Situação | O que fazer |
| --- | --- |
| "Erro ao carregar fichas" | Verifique a conexão e clique em **Tentar novamente**. Consulte a página **Health**. |
| "Não foi possível completar a busca" | Tente novamente. Se persistir, avise o suporte. |
| Fui mandado para o login sozinho | A sessão expirou e não pôde ser renovada. Entre novamente. |
| Mensagem de validação no formulário | Corrija o campo indicado. Por exemplo, nome ou cliente em branco ou data vazia. |
