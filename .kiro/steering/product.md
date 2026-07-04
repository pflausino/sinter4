# Product Summary

**SinterPrints** é um sistema de cadastro, controle e catálogo de arquivos de arte final. Ele organiza e facilita a busca por arquivos criados em ferramentas como CorelDRAW, Photoshop, Inkscape, Illustrator e outros softwares gráficos.

## Objetivo

Permitir que o usuário cadastre, categorize e localize rapidamente arquivos de arte final, eliminando a necessidade de busca manual em pastas e HDs.

## Funcionalidades Implementadas

- Cadastro de arquivos de arte final com metadados (nome, cliente, tipo de arquivo/software, número do disquete, data, número do arquivo)
- Listagem paginada com busca por texto (nome, cliente)
- Busca com suporte a unaccent (ignora acentos)
- CRUD completo (criar, editar, excluir registros)
- Autenticação via Firebase (login/logout, proteção de rotas e endpoints)

## Modelo de Dados Atual (FileRecord)

- `Id` (uuid) — Identificador único
- `Name` (string) — Nome do arquivo/trabalho
- `FileType` (enum) — Tipo/software: CorelDRAW, Photoshop, Illustrator, Inkscape, PDF, InDesign, PageMaker, JPEG, PNG, TIFF, EPS, LegacyOld, Unknown, Other
- `FlopDiskNumber` (int?) — Número do disquete/mídia de armazenamento
- `Date` (DateTime?) — Data do trabalho
- `Client` (string) — Nome do cliente
- `FileNumber` (string?) — Número do arquivo

## Funcionalidades Futuras (Planejadas)

- Catálogo visual com preview/thumbnail dos arquivos
- Tags personalizáveis para organização adicional
- Controle de versões dos arquivos
- Busca avançada por período (data inicial/final)

## Stack

- .NET 10 + Blazor Server (frontend e backend)
- PostgreSQL (persistência)
- Firebase Authentication (identidade e autenticação)
- Mono-repo
