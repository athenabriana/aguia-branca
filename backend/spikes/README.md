# Spike B03 — EF Core MongoDB provider

Prova de conceito **descartável** (fora da solution). Valida os riscos do design §8 contra um MongoDB real em replica set.

```bash
cd backend && docker compose up -d mongo
dotnet run --project spikes/EfMongoSpike
```

Resultado (MongoDB 7 · `MongoDB.EntityFrameworkCore` 8.4.4 · EF Core 8.0.31): **14/15 ✅ — decisão GO**.
Detalhes e decisões derivadas: `.specs/features/sprint2/design.md` §8.2, ADR-002/003.

| Check | Resultado |
|---|---|
| a1/a2 CRUD, `_id` ObjectId, enum string, embutidos | ✅ |
| b1–b3 transação commit / rollback / rollback por exceção | ✅ |
| b4 `SaveChanges` atômico com violação de índice | ✅ (exceção = `MongoBulkWriteException`/`DuplicateKey`) |
| c1 concorrência otimista | ✅ |
| d1 filtro + ordenação + paginação | ✅ |
| d2 `Select` simples | ✅ |
| d3 `GroupBy` no servidor | ❌ esperado → agregar em memória (d4 ✅) |
| e1 Identity (`UserManager` + store customizado) | ✅ |
| f1 índices único / parcial / TTL via driver | ✅ |
| g1 camelCase (`SetElementName` + `HasElementName` em owned) | ✅ |
| g2 entidade sem atributos do driver (`HasConversion` p/ ObjectId) | ✅ |

`IdentityStore.cs` é a base do `MongoUserStore` da B08. Remover esta pasta antes de empacotar (D04).
