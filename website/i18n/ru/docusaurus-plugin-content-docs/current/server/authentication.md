---
sidebar_position: 2
title: Аутентификация и сессии
---

# Аутентификация и сессии

Сессия DarkWS — это неизменяемый объект, привязанный к соединению. Она содержит
пользователя, id сессии, используемый для [рассылок по сессии](broadcasts.md), и группы,
в которые входит соединение. Соединение без сессии анонимно и может вызывать только
действия с `[AllowAnonymous]`.

Сессия устанавливается в двух местах:

1. **При upgrade.** Когда WebSocket открывается, аутентификатор получает HTTP-запрос
   и параметр запроса `token`.
2. **Командой.** Клиент отправляет по открытому сокету текстовую команду `auth:<token>`,
   чтобы войти или обновить учётные данные, и `logout`, чтобы выйти, — без
   переподключения.

## По умолчанию: identity ASP.NET {#default-the-aspnet-identity}

Без настройки DarkWS использует identity ASP.NET Core из upgrade-запроса.
Если `HttpContext.User` аутентифицирован (через cookie, bearer или любую другую схему, которая
выполняется до `MapDarkWs`), соединение получает `AspNetDarkWsSession` с этим пользователем и
без групп. Её id — это claim `sid`, иначе id сессии ASP.NET, иначе случайный id,
стабильный в пределах соединения.

Этот аутентификатор по умолчанию доверяет только HTTP-identity. Он не проверяет
команды `auth:<token>`: пока пользователь аутентифицирован, любой токен проходит успешно
с той же identity. Зарегистрируйте собственный аутентификатор, чтобы проверять токены.

## Типизированные сессии {#typed-sessions}

Определите сессию с данными вашей предметной области:

```csharp
using System.Security.Claims;
using DarkWS.Abstractions;

public sealed record AppSession(
    string Id,
    ClaimsPrincipal User,
    Guid AccountId,
    Guid UserId
) : IDarkWsSession {
    public IReadOnlyCollection<string> Groups =>
        [$"account:{AccountId}", $"user:{UserId}"];
}
```

Реализуйте `IDarkWsAuthenticator`. Верните сессию, чтобы принять соединение, или null, чтобы
оставить его анонимным:

```csharp
public sealed class AppAuthenticator(TokenValidator tokens) : IDarkWsAuthenticator {
    public async ValueTask<IDarkWsSession?> AuthenticateAsync(
        HttpContext context, string? token, CancellationToken cancellationToken) {
        if (string.IsNullOrEmpty(token)) return null;
        var ticket = await tokens.ValidateAsync(token, cancellationToken);
        if (ticket is null) return null;
        return new AppSession(ticket.SessionId, ticket.Principal, ticket.AccountId, ticket.UserId);
    }
}
```

Зарегистрируйте оба один раз в builder DarkWS:

```csharp
builder.Services
    .AddDarkWs()
    .AddHandlersFromAssemblyContaining<Program>()
    .AddAuthenticator<AppAuthenticator, AppSession>();
```

Повторный вызов `AddAuthenticator` выбрасывает `InvalidOperationException` до изменения
каких-либо регистраций.

Типизированные обработчики получают сессию через `HandlerBase<TSession>`, а также могут
внедрять её в конструкторы и scoped-сервисы:

```csharp
[Handler("message")]
public sealed class MessageHandler(MessageService messages) : HandlerBase<AppSession> {
    [Action("send")]
    public async Task<IResponse> SendAsync(MessageInput input) {
        var result = await messages.SendAsync(Session.UserId, input);
        await PublishAsync(BroadcastTarget.Group($"account:{Session.AccountId}"), "message:created", result);
        return Ok(result);
    }
}
```

Действие видит ту сессию, с которой оно было авторизовано. `auth:` или
`logout`, пришедшие во время выполнения действия, её не меняют;
`IWebSocketConnection.Session` всегда показывает актуальное значение.

`HttpContext.User` следует решению аутентификатора: он заменяется при
аутентификации и выходе, а отклонённый upgrade оставляет его анонимным.

## Время жизни аутентификатора {#authenticator-lifetime}

- Аутентификатор для upgrade разрешается из области upgrade-запроса, которая
  живёт всё время существования соединения.
- Каждая команда `auth:` разрешает аутентификатор из новой области.

Если аутентификатор используется при upgrade, получайте короткоживущие зависимости, например
`DbContext`, через `IServiceScopeFactory`.

## Аутентификация при upgrade {#upgrade-authentication}

При upgrade токен читается из параметра запроса `token`; изменить его можно через
`DarkWsOptions.AuthenticationQueryParameter`. Токен необязателен: без него
ваш аутентификатор принимает решение только по HTTP-запросу.

| Результат аутентификатора | Итог |
| --- | --- |
| Сессия | Соединение открывается аутентифицированным |
| `null` | Соединение открывается анонимным |
| Исключение | Логируется как предупреждение; HTTP 401, сокет не открывается |

Установите `AcceptAnonymousOnUpgradeAuthenticationException = true`, чтобы принимать сокет
анонимно, когда аутентификатор выбрасывает исключение. Отмена запроса и отмена при завершении
работы никогда не считаются ошибкой аутентификации.

Токены в URL могут попасть в логи прокси и журналы доступа. Предпочитайте короткоживущий
тикет подключения или открывайте сокет анонимно и аутентифицируйтесь через `auth:` (см.
[Безопасность](../security.md#tokens-in-urls)).

## Команды аутентификации {#authentication-commands}

| Команда | Ответ при успехе | Ответ при ошибке |
| --- | --- | --- |
| `auth:<token>` | `auth:success` | `auth:failed` |
| `logout` | `logout:success` | Соединение завершается с ошибкой, если выход не удаётся выполнить |

- Успешная `auth:` заменяет сессию. `logout` всегда её очищает.
- По умолчанию неудачная `auth:` (отказ, пустой токен или исключение) тоже очищает
  предыдущую сессию. Установите `KeepSessionOnFailedAuthentication = true`, чтобы сохранить
  текущую сессию, principal и индексы групп; ответ по-прежнему `auth:failed`.
  Используйте это, только если хост по-прежнему контролирует срок действия сохранённой сессии.
- Команды сохраняют свой порядок относительно запросов в соединении.
- Уже выполняющиеся действия при выходе не откатываются.

Оба клиента отправляют эти команды за вас: `authenticate()`/`logout()` в браузере
и `AuthenticateAsync`/`LogoutAsync` в .NET. Они также умеют восстанавливать сессию при
каждом переподключении; см. страницы [браузерного](../clients/browser.md#authentication) и
[.NET](../clients/dotnet.md#authentication) клиентов.

## Реакция на изменение сессии {#react-to-session-changes}

`DarkWsConnectionHooks.OnAuthenticatedAsync` выполняется после того, как сессия заменена,
очищена выходом или очищена неудачной `auth:`. Он получает предыдущую сессию;
текущая может быть null. Он не вызывается, когда
`KeepSessionOnFailedAuthentication` оставляет сессию без изменений. См.
[Фильтры и хуки](pipeline.md#connection-hooks).

## Изменение групп {#changing-groups}

Членство в группах читается из `IDarkWsSession.Groups` при добавлении соединения
и после каждой повторной аутентификации. Если ваше приложение меняет членство другим
способом, вызовите `Refresh(connection)` у внедрённого `IDarkWsConnections`; см.
[Рассылки](broadcasts.md#connections-and-groups).

## Ответственность приложения {#responsibilities-of-the-application}

DarkWS не проверяет, не отслеживает срок действия и не отзывает токены:

- Истечение срока действия или отзыв токена не закрывает существующее соединение. Контролируйте
  время жизни сессии и текущую авторизацию в приложении, например закрывая
  соединения из обработчика отзыва.
- DarkWS не ограничивает число попыток `auth:`; см.
  [Безопасность](../security.md#authentication-attempts).
