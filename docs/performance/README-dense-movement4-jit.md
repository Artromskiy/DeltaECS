# Dense Movement4 JIT: Hot-Loop Review

Профиль: `ecs-next`, `Movement4`, Release AArch64 JIT.

## Главное

В настоящем slot-loop нет `blr`, `bhs` или object/`Array[]` lookup. Для одной
entity тело цикла содержит 29 инструкций:

| Инструкция | Количество | Роль |
|---|---:|---|
| `sbfiz` | 1 | Масштабирование slot index для 4-байтовых значений |
| `add` | 12 | Адресная арифметика и вычисления |
| `ldr` | 10 | Чтение A/B/C/D и checksum |
| `str` | 3 | Запись A/B/C |
| `asr` | 1 | Деление среднего на 2 |
| `sub` | 1 | Reverse slot decrement |
| `tbz` | 1 | Предсказуемая проверка окончания slot-loop |

Главный перспективный вариант — заранее подготовленные advancing refs для
каждой component-column, обновляемые только при смене chunk. Тогда в slot-loop
можно убрать повторное масштабирование индекса и часть адресной арифметики.

## Участки кода и assembly

| Операция | Исходный код | Assembly | Оценка |
|---|---|---|---|
| Reverse slot-loop | [MicroBenchmarkImplementations.cs:149](../../benchmarks/DeltaECS.MicroBenchmarks/MicroBenchmarkImplementations.cs:149) | [`sub` + `tbz`](../../artifacts/jit-disasm/ecs-next-movement4-full.txt:130918) | Одна предсказуемая ветка; это не bounds-check |
| Получение A/B/C/D | [MicroBenchmarkImplementations.cs:151](../../benchmarks/DeltaECS.MicroBenchmarks/MicroBenchmarkImplementations.cs:151) | [`sbfiz` и четыре адресных `add`](../../artifacts/jit-disasm/ecs-next-movement4-full.txt:130890) | P1-кандидат: advancing refs |
| Чтение и checksum | [MicroBenchmarkImplementations.cs:158](../../benchmarks/DeltaECS.MicroBenchmarks/MicroBenchmarkImplementations.cs:158) | Десять `ldr` и три `str` начинаются здесь: [`ldr`](../../artifacts/jit-disasm/ecs-next-movement4-full.txt:130895) | Часть чтений повторяется из-за checksum |
| Row resolution | [MicroBenchmarkImplementations.cs:145](../../benchmarks/DeltaECS.MicroBenchmarks/MicroBenchmarkImplementations.cs:145) | `Array[] → physicalRow → data ref` на chunk: [chunk setup](../../artifacts/jit-disasm/ecs-next-movement4-full.txt:130845) | Не выполняется для каждой entity |
| Write tracking | [QuerySlots.cs:64](../../src/DeltaECS/QuerySlots.cs:64) | Stores версий до slot-loop: [store](../../artifacts/jit-disasm/ecs-next-movement4-full.txt:130856) | Внутри entity-loop ветки нет |
| `Ref<T>` | [QueryAccess.cs:204](../../src/DeltaECS/QueryAccess.cs:204) | Заинлайнен в адресную арифметику | `Ref<T>(int)` JIT не улучшил |
| Query setup/validation | [MicroBenchmarkImplementations.cs:133](../../benchmarks/DeltaECS.MicroBenchmarks/MicroBenchmarkImplementations.cs:133) | `blr` находятся до цикла: [пример](../../artifacts/jit-disasm/ecs-next-movement4-full.txt:130675) | Не считать hot-loop overhead |
| Prologue/epilogue | — | `ldp/stp`: [prologue](../../artifacts/jit-disasm/ecs-next-movement4-full.txt:130650) | Не относится к entity throughput |

## Проверенные эксперименты

| Вариант | JIT-результат | Решение |
|---|---|---|
| P1: пакет прямых row references на chunk | JIT и счётчики без изменений; 100k регрессировал | Отклонён |
| P2: `Ref<T>(int index)` вместо передачи `QuerySlots` | Scalar replacement сделал код побайтно идентичным | Отклонён |

Передача `QuerySlots` по значению уже устраняется JIT. Простое изменение
сигнатуры не создаёт нового выигрыша.

## Следующий осмысленный эксперимент

Сделать trusted dense iterator с четырьмя внутренними advancing refs:

1. при переходе на chunk один раз получить базовые refs компонентных рядов;
2. в slot-loop читать текущие refs;
3. после `MoveNext()` сдвигать refs на предыдущий элемент;
4. сохранить внешний safe API и запрет structural mutation во время scope.

Отдельно можно проверить хранение вычисленных `a`, `b`, `c`, `d` в локальных
значениях перед checksum. Это уменьшит повторные `ldr`, но является оптимизацией
benchmark/user kernel, а не ECS storage path, поэтому её нельзя смешивать с
оценкой row-access API.
