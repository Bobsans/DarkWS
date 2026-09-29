---
sidebar_position: 8
title: Обновление до 5.0
---

# Обновление до 5.0

Все пакеты имеют общую версию. Обновляйте сервер, инстансы Redis и клиенты
одновременно. Полный список изменений — в
[changelog](https://github.com/Bobsans/DarkWS/blob/main/CHANGELOG.md).

## Регистрация сервера {#server-registration}

| 4.x | 5.0 |
| --- | --- |
| `services.AddDarkWsRedis(channel)` | `services.AddDarkWs().AddRedis(channel)` |
| `Configuration.AddDarkWs(services)` | `services.AddDarkWs()` |
| `Configuration.MapDarkWs(endpoints)` | `endpoints.MapDarkWs(pattern)` |
| `RedisConfiguration.AddDarkWsRedis(...)` | `services.AddDarkWs().AddRedis(channel)` |
| Наследник `DarkWsMiddleware` | Наследник `DarkWsConnectionHooks`, зарегистрированный через `AddConnectionHooks<T>()` |
| Повторные вызовы `AddAuthenticator` | Один вызов на общем builder; второй вызов выбрасывает исключение |
| `options.AuthenticationFailedError = …` | Удалите; неудачная текстовая аутентификация всегда отвечает `auth:failed` |

Хуки соединений больше не находятся сканированием сборок; регистрируйте каждый явно.

## Рассылки {#broadcasts}

`PublishAsync` с `BroadcastTarget` заменяет старые методы рассылки в
`IBroadcaster` и `HandlerBase`:

| 4.x | 5.0 |
| --- | --- |
| `BroadcastAsync(action, data)` | `PublishAsync(BroadcastTarget.All, action, data)` |
| `BroadcastToConnectionAsync(id, action, data)` | `PublishAsync(BroadcastTarget.Connection(id), action, data)` |
| `BroadcastToSessionAsync(id, action, data)` | `PublishAsync(BroadcastTarget.Session(id), action, data)` |
| `BroadcastToGroupAsync(group, action, data)` | `PublishAsync(BroadcastTarget.Group(group), action, data)` |
| `BroadcastToSelfAsync(action, data)` | `PublishAsync(Self, action, data)` |

Передавайте токен отмены позиционно после данных. Собственные реализации
`IBroadcaster` реализуют две перегрузки `PublishAsync`, принимающие токен.

В собственных backplane и тестах переименуйте `DarkWsBroadcast` в `BroadcastMessage`,
`DarkWsTarget` в `BroadcastTargetType`, а `Target`/`Groups` конверта — в
`TargetType`/`GroupNames`, затем перекомпилируйте. Формат передачи Redis не изменился,
поэтому узлы 4.x и 5.0 по-прежнему обмениваются сообщениями с одной целью. Прежде чем
использовать `Groups` или исключения, обновите все узлы на канале (см.
[Формат передачи Redis](server/redis.md#wire-format)).

Доставки Redis теперь выполняются параллельно: не полагайтесь на порядок рассылок в
пределах одного соединения.

## Соединения {#connections}

`ConnectionStorage` стал internal. Внедряйте `IDarkWsConnections`:

| 4.x | 5.0 |
| --- | --- |
| `storage.Add(connection)` после изменения групп | `connections.Refresh(connection)` |
| `storage.GetByConnection(id)` | `connections.Find(id)` |
| Фиктивные получатели в тестах | `DarkWsTestHost.CreateConnection` из `DarkWS.Testing` |

В собственных реализациях `IWebSocketConnection` удалите `WebSocket` и
`ReceiveMessageAsync`, реализуйте `Abort()` и принимайте `ReadOnlyMemory<byte>` в
`SendAsync`.

Записи конвертов передачи (`InputMessage`, `OkMessage`, `ErrorMessage` и другие)
стали internal. Код, который их сериализовал, должен использовать собственные записи с
документированными полями `id`, `action`, `data` и `error`.

## Изменения поведения {#behavior-changes}

- **`HttpContext` после завершения.** Обработчики, которые продолжают работать после
  `ConnectionAborted` и затем читают `HttpContext`, `Items`, заголовки или
  `AspNetSession`, получают `ObjectDisposedException`. Скопируйте значения заранее.
  `Session` это не затрагивает.
- **Занятые соединения.** Соединение, которое конвейеризует много медленных запросов,
  может получить `darkws:error:busy` по истечении `RequestQueueTimeout` (5 s).
  Повторите запрос или увеличьте `MaxConcurrentRequestsPerConnection` либо
  `RequestQueueTimeout`, оставляя таймаут меньше keep-alive и таймаутов pong на
  клиенте.
- **Флуд командами.** Четыре места в очереди зарезервированы для `auth:`/`logout`;
  команда, заставшая очередь заполненной, закрывает соединение с кодом 1008.
- **Ошибки аутентификации при upgrade** возвращают HTTP 401 вместо того, чтобы
  всплывать как ошибки сервера. Включите
  `AcceptAnonymousOnUpgradeAuthenticationException`, чтобы вместо этого принимать
  соединение анонимно.

## Браузерный клиент {#browser-client}

- Аутентификация и logout используют `controlTimeout` (30 s) вместо `requestTimeout`.
  Установите `controlTimeout` равным прежнему `requestTimeout`, чтобы сохранить старый
  дедлайн.
- `authenticate("")` отклоняется с `TypeError`; вызывайте вместо этого `logout()`.
- Сокет, не получивший `pong` в течение 30 секунд, закрывается и переподключается.
  Установите `pongTimeout: 0` для прежнего поведения и переименуйте `pingTimeout` в
  `pingInterval`.
- `connect()` больше не заменяет открытый сокет; вызовите `close()`, а затем
  `connect()`.
- Более 256 одновременных вызовов отклоняются с `RangeError`. Увеличьте
  `maxPendingRequests`, если нужно больше.

## Новое в 5.0 {#new-in-50}

- Объединения групп и исключения: `BroadcastTarget.Groups`, `ExceptConnection`,
  `ExceptSession`.
- [Фильтры действий, фильтры запросов](server/pipeline.md) и метаданные `DarkWsActionInfo`.
- `DarkWS.Testing` для [тестирования обработчиков](server/testing.md) без сокетов.
- `AllowedOrigins`, `RunActionsOnThreadPool`, `AllowNullPayloads`,
  `KeepSessionOnFailedAuthentication` и `AcceptAnonymousOnUpgradeAuthenticationException`.
- Браузер: `authenticationToken`, `sessionRestoreFailed`, повторы, `requestOptions`,
  `onAction`, `DarkWs.lazy()`, `isCurrentSocket` и `reconnectOnVisible`.
