# Migração Firestore → MongoDB (`FirestoreMigrator`)

Ferramenta de linha de comando que lê os dados do **Firestore** (app da Sprint 1) e os grava no **MongoDB** do backend, no
formato exato que a API espera. É **idempotente** (pode rodar mais de uma vez sem duplicar), tem **`--dry-run`** e emite um
**relatório de conciliação**.

- Código: [`AguiaBranca.FirestoreMigrator`](AguiaBranca.FirestoreMigrator) · Testes: [`AguiaBranca.FirestoreMigrator.Tests`](AguiaBranca.FirestoreMigrator.Tests)
- Requisitos de origem: [spec R2-09](../../.specs/features/sprint2/spec.md)

## O que é migrado

| Firestore | MongoDB | Observações |
|---|---|---|
| `users/{uid}` | `users` | E-mail preservado; **senha nova** (temporária, veja abaixo); `badges` **recalculadas**; `points` → evento de abertura |
| `strategicGuidelines/{id}` | `guidelines` + `guidelineHistory` | 1 entrada `CREATED` por orientação (data de criação original) |
| `ideas/{id}` | `ideas` | `ice` embutido (o `score` é recalculado); `updatedAt` = `reviewedAt` ou `createdAt` |
| `projects/{id}` | `projects` | `version = 1`; valores monetários como `Decimal128` |
| `projects/{id}/updates/{uid}` | `projectUpdates` | `changes` convertidas para o formato tipado do servidor (`NUMBER`/`DATE`/`TEXT`) |
| — | `pointEvents` | 1 evento `MIGRATION` por usuário com saldo > 0, datado na criação do usuário (não entra no ranking do mês corrente) |

Ordem de escrita: `users → guidelines (+ histórico) → ideas → projects → projectUpdates → pointEvents`.

**Todos os IDs mudam** (Mongo usa `ObjectId`); o ID original fica em `legacyId` e **todas as referências** (`authorId`,
`guidelineId`, `originatingIdeaId`, `creatorManagerId`, `reviewerId`, `reporterId`, `responsibleId`, autor das atualizações) são
remapeadas. A tabela legado→ObjectId fica na coleção `migration_idmap` — é ela que torna a reexecução idempotente.

## Antes de rodar

1. **MongoDB de destino sem os dados de demonstração**: suba a API com `Seed__Enabled=false` (ou use um banco novo). Se um e-mail
   migrado já existir no destino como **outra** conta, o usuário é reportado como *conflito* e não é sobrescrito.
2. **Service account do Firestore** (fora do repositório!): Console do Firebase → Configurações do projeto → *Contas de serviço* →
   *Gerar nova chave privada*. Guarde o JSON fora do Git. Só precisa de leitura no Firestore (`roles/datastore.viewer`).
3. **.NET 8 SDK** (o mesmo do backend).

## Executando

Sempre comece com `--dry-run` (não escreve nada — nem o mapa de IDs):

```bash
dotnet run --project backend/tools/AguiaBranca.FirestoreMigrator -- \
  --firestore-project meu-projeto-firebase \
  --credentials /caminho/seguro/service-account.json \
  --mongo "mongodb://localhost:27017/?directConnection=true" \
  --database aguiabranca \
  --dry-run --report conciliacao-dryrun.json
```

Depois a execução real (a senha inicial dos usuários é obrigatória, ≥ 8 caracteres):

```bash
dotnet run --project backend/tools/AguiaBranca.FirestoreMigrator -- \
  --firestore-project meu-projeto-firebase \
  --credentials /caminho/seguro/service-account.json \
  --mongo "mongodb://localhost:27017/?directConnection=true" \
  --database aguiabranca \
  --temp-password "TroqueEstaSenha!2026" \
  --report conciliacao.json
```

PowerShell: troque `\` por `` ` `` (crase) no fim das linhas.

| Opção | Descrição |
|---|---|
| `--firestore-project` | Projeto de origem (obrigatório) |
| `--credentials` | JSON da service account. **Sem ele, usa o emulador** (`FIRESTORE_EMULATOR_HOST`) |
| `--mongo` / `--database` | Destino (a connection string é obrigatória; banco padrão `aguiabranca`) |
| `--temp-password` | Senha inicial de todos os usuários migrados (ou env `MIGRATION_TEMP_PASSWORD`) |
| `--dry-run` | Lê, valida e concilia **sem escrever** |
| `--report` | Grava o relatório de conciliação em JSON |
| `--timezone` | Fuso do "mês corrente" das badges (padrão `America/Sao_Paulo`) |

**Códigos de saída:** `0` concluído e conciliado · `2` concluído com divergências · `1` erro de uso/execução.

### Testando com o emulador do Firestore

Com o [Firebase CLI](https://firebase.google.com/docs/emulator-suite) instalado e dados carregados no emulador:

```bash
export FIRESTORE_EMULATOR_HOST=localhost:8080
dotnet run --project backend/tools/AguiaBranca.FirestoreMigrator -- \
  --firestore-project demo-inovagab --mongo "mongodb://localhost:27017/?directConnection=true" --dry-run
```

## Relatório de conciliação

```text
== RELATÓRIO DE CONCILIAÇÃO ==
coleção           origem  inválidos  descartados  migrados  no destino  órfãs  avisos
users                  3          0            0         3           3      0       0
guidelines             2          0            0         2           2      0       0
ideas                  5          1            1         3           3      1       0
...
Violações de integridade referencial: 0
```

- **origem** = documentos lidos · **inválidos** = rejeitados na validação · **descartados** = conflitos de e-mail e itens sem
  autor/gestor no destino · **migrados** = enviados · **no destino** = conferidos no MongoDB depois da escrita (— em `--dry-run`).
- A conciliação fecha quando `origem − inválidos − descartados = migrados = no destino` e há **0 violações de integridade**.
- O JSON traz, além da tabela, cada ocorrência (`Invalid`, `OrphanReference`, `Warning`, `Conflict`) com o ID original.

### Regras de tratamento de dados ruins (a migração nunca aborta por um item)

| Situação | Resultado |
|---|---|
| Enum desconhecido (`role`, `division`, `status`, `stage`, `pillar`), número inválido/negativo, título/e-mail ausente | Item **descartado** e listado (`Invalid`) |
| Campo de enum **ausente** | Assume o padrão do app (ex.: divisão `CORPORATIVO`, status `SUBMETIDA`) |
| Data ausente | Assume o momento da migração + aviso |
| ICE fora de 1–10 | ICE descartado (a ideia migra sem ICE) + aviso |
| Referência **opcional** para ID inexistente (`guidelineId`, `reviewerId`, `reporterId`, `responsibleId`, `originatingIdeaId`) | Vira `null` + `OrphanReference` |
| Referência **obrigatória** inexistente (autor da ideia/orientação/atualização, gestor do projeto) | Item descartado + `OrphanReference` |
| Dois projetos apontando para a mesma ideia de origem | O 2º perde o vínculo (índice único parcial) + `OrphanReference` |
| E-mail duplicado na origem | O 2º é `Invalid` |

## Limitações intencionais

- **Senhas não migram.** O Firebase Auth guarda hashes em scrypt modificado, incompatíveis com o Identity. Todos os usuários
  ganham a mesma senha temporária (`--temp-password`); informe-a a cada um por um canal seguro. *(A API ainda não tem endpoint de
  troca de senha — hoje só o login/refresh/logout.)*
- **Reexecutar sobrescreve** os documentos migrados com o conteúdo da origem (é o que garante a idempotência). Para usuários já
  migrados atualiza só perfil, saldo e badges — **nunca a senha**. Não rode de novo depois de o sistema novo entrar em produção,
  a menos que queira descartar alterações feitas nele.
- **Badges** vêm do mesmo `BadgeEvaluator` do servidor (o app antigo nunca gravou badges).
- **Histórico de orientações:** só a entrada `CREATED` (o Firestore não guardava edições).

## Testes

```bash
dotnet test backend/tools/AguiaBranca.FirestoreMigrator.Tests
```

45 testes unitários (parsers, regras de tratamento, runner com origem/destino em memória: idempotência, dry-run, órfãs,
conflitos, pontos, badges, mapa de IDs salvo antes das gravações, CLI, conversão de tipos do SDK do Firestore).

Além disso, `Api.Tests/Migration/MigrationCompatibilityTests` (MongoDB real) migra um dataset fictício e usa **a API de
verdade** sobre os dados migrados: login com a senha temporária, leituras, dashboard, aprovar uma ideia migrada e editar um
projeto migrado — prova que o formato gravado é idêntico ao nativo.

> **Ainda não validado:** a leitura de um Firestore **real/emulador** (o `FirestoreSource`, fino, sobre o SDK oficial). Está
> coberto só pelos testes de conversão de tipos. Antes da migração definitiva, rode um `--dry-run` contra o projeto real (ou o
> emulador) e confira o relatório.
