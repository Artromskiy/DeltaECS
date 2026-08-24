# ADR-0001: Начальные допущения по хранению данных DeltaECS

> Статус: исторический документ. Это запись раннего storage baseline; актуальный
> публичный контракт и API описаны в `README.md`.

## Контекст
README требует тип-стираемого kernel с batched-операциями, SoA archetype/chunk и immediate structural transitions без смены archetype.

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
  - только dense-компоненты поддерживаются в этой версии ядра;
  - `Create/Destroy` и structural transitions завершаются немедленно через single/batch API `AddComponents` и `RemoveComponents`;
  - query-кеш кэширует соответствующие archetype и пересчитывается при появлении новых archetype;

## Последствия
- отдельный sparse storage path не входит в текущую модель dense-хранилища;
- переходы `add/remove` не имеют строгой версии/пулы транзакций как в step-6+, но поддерживают батчевую семантику;
- для простоты benchmark baseline использует локальный набор операций (dense итерация + create/destroy).
- `SchemaId` стабилен: повторная регистрация дедуплицирует полностью равный
  layout, а несовместимый повтор отклоняется.
