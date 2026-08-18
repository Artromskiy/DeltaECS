# ADR-0001: Начальные допущения по шагам 1–5 ядра DeltaECS

## Контекст
README требует тип-стираемого kernel с batched-операциями, SoA archetype/chunk, queue-based структурных переходов и overlay-tag mask-индексом без смены archetype.

## Решение для этой поставки
- Реализована версия ядра, где:
  - сущности идентифицируются как `Entity(Index, Generation)` и маппятся в `EntityRecord`;
  - `ComponentId` + `ComponentLayout` управляют всеми типами;
  - data-часть хранится как dense-строки archetype/chunk: `byte[]` сохраняется
    как legacy baseline, а новый backend использует прямые `Array[]` CLR-массивы
    без row-wrapper классов;
  - Type-backed ArrayRows layouts допускают struct с managed-полями; Type
    используется только при холодном создании массива и не участвует в
    ComponentId identity или query hot loop;
  - один CLR element type может иметь несколько виртуальных ComponentId, но
    их физические массивы всегда независимы;
  - только dense-компоненты поддерживаются в шагах 1–5;
  - `Create/Destroy` и structural transitions работают через батч-очередь (`QueueAddComponents`, `QueueRemoveComponents`, `PlaybackTransitions`);
  - query-кеш кэширует соответствующие archetype и пересчитывается при появлении новых archetype;
  - overlay-теги хранятся в sparse-структуре `tag -> chunk -> bitset` и применяются через маски в query.

## Последствия
- `Stream` и `Overlay` как компоненты в layouts пока не инжектируются в dense-хранилище;
- переходы `add/remove` не имеют строгой версии/пулы транзакций как в step-6+, но поддерживают батчевую семантику;
- для простоты benchmark baseline использует локальный набор операций (dense итерация + create/destroy).
- `SchemaId` стабилен: повторная регистрация дедуплицирует полностью равный
  layout, а несовместимый повтор отклоняется.
