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
без групп. Её id — это claim `sid`, иначе id сессии ASP.NET, иначе новый случайный id.

Этот аутентификатор по умолчанию доверяет только HTTP-identity. Он не проверяет
команды `auth:<token>`: пока `HttpContext.User` аутентифицирован, любой непустой токен
проходит успешно с той же identity. После `logout` или неудачной `auth:` `HttpContext.User`
становится анонимным, поэтому каждая следующая `auth:` в этом соединении завершается отказом.
Зарегистрируйте собственный аутентификатор, чтобы проверять токены.

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

Защищённые действия проверяют `User.Identity.IsAuthenticated`, поэтому передавайте в сессию
principal с аутентифицированной identity (`ClaimsIdentity`, созданной с типом аутентификации).

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

Вызывайте `AddAuthenticator` один раз. Повторный вызов заменяет аутентификатор, а
регистрации типизированных сессий из предыдущих вызовов остаются.

Типизированные обработчики получают сессию через `HandlerBase<TSession>`, а также могут
внедрять её в конструкторы и scoped-сервисы:

```csharp
[Handler("message")]
public sealed class MessageHandler(MessageService messages) : HandlerBase<AppSession> {
    [Action("send")]
    public async Task<IResponse> SendAsync(MessageInput input) {
        var result = await messages.SendAsync(Session.UserId, input);
        await BroadcastToGroupAsync($"account:{Session.AccountId}", "message:created", result);
        return Ok(result);
    }
}
```

`HandlerBase.Session` и `IDarkWsContextAccessor.Session` при каждом обращении читают
текущую сессию соединения. `auth:` или `logout`, обработанные во время выполнения действия,
меняют то, что действие увидит дальше; после `logout` `HandlerBase.Session`
выбрасывает `InvalidOperationException`. Если действие должно работать с одной identity,
прочитайте сессию один раз в его начале.

`HttpContext.User` устанавливается в пользователя сессии, когда аутентификатор при upgrade
возвращает сессию, и остаётся без изменений, когда он возвращает null. Каждая `auth:` и
`logout` заменяют его: на пользователя новой сессии или на анонимный principal, когда
сессия очищается.

## Время жизни аутентификатора {#authenticator-lifetime}

Аутентификатор разрешается из области upgrade-запроса, которая живёт всё время
существования соединения. Один и тот же экземпляр обрабатывает upgrade и каждую команду
`auth:` в этом соединении.

Получайте короткоживущие зависимости, например `DbContext`, через `IServiceScopeFactory`.

## Аутентификация при upgrade {#upgrade-authentication}

При upgrade токен читается из параметра запроса `token`; изменить его можно через
`DarkWsOptions.AuthenticationQueryParameter`. Токен необязателен: без него
ваш аутентификатор принимает решение только по HTTP-запросу.

| Результат аутентификатора | Итог |
| --- | --- |
| Сессия | Соединение открывается аутентифицированным |
| `null` | Соединение открывается анонимным |
| Исключение | Исключение выходит из конечной точки; сокет не открывается |

Токены в URL могут попасть в логи прокси и журналы доступа. Предпочитайте короткоживущий
тикет подключения или открывайте сокет анонимно и аутентифицируйтесь через `auth:` (см.
[Безопасность](../security.md)).

## Команды аутентификации {#authentication-commands}

| Команда | Ответ при успехе | Ответ при ошибке |
| --- | --- | --- |
| `auth:<token>` | `auth:success` | `auth:failed` |
| `logout` | `logout:success` | Соединение завершается с ошибкой, если выход не удаётся выполнить |

- Успешная `auth:` заменяет сессию. `logout` всегда её очищает.
- Неудачная `auth:` (результат null, пустой токен или исключение, которое логируется
  как предупреждение) тоже очищает предыдущую сессию. Ответ всегда `auth:failed`;
  `DarkWsOptions.AuthenticationFailedError` его не меняет.
- Команды обрабатываются в цикле чтения соединения: сообщения, пришедшие после команды,
  диспетчеризуются после её завершения.
- Уже выполняющиеся действия при выходе не откатываются.

Оба клиента отправляют эти команды за вас: `authenticate()`/`logout()` в браузере
и `AuthenticateAsync`/`LogoutAsync` в .NET. .NET-клиент также умеет восстанавливать
сессию на каждом новом сокете через `AuthenticationTokenProvider`; браузерный клиент
заново читает опцию `query` при каждом подключении. См. страницы
[браузерного](../clients/browser.md) и [.NET](../clients/dotnet.md) клиентов.

## Реакция на изменение сессии {#react-to-session-changes}

`DarkWsMiddleware.OnAuthenticatedAsync` выполняется после каждой `auth:` и `logout`:
когда сессия заменена, очищена выходом или очищена неудачной `auth:`. Он получает
предыдущую сессию; текущая может быть null. Исключение, выброшенное из него, закрывает
соединение. См. [Middleware](pipeline.md#middleware).

## Изменение групп {#changing-groups}

Членство в группах читается из `IDarkWsSession.Groups` при добавлении соединения
и после каждой `auth:` и `logout`. Если ваше приложение меняет членство другим
способом, вызовите `Add(connection)` у внедрённого `ConnectionStorage`, чтобы обновить
индексы; см. [Рассылки](broadcasts.md).

## Ответственность приложения {#responsibilities-of-the-application}

DarkWS не проверяет, не отслеживает срок действия и не отзывает токены:

- Истечение срока действия или отзыв токена не закрывает существующее соединение. Контролируйте
  время жизни сессии и текущую авторизацию в приложении, например закрывая
  соединения из обработчика отзыва.
- DarkWS не ограничивает число попыток `auth:`; см. [Безопасность](../security.md).
