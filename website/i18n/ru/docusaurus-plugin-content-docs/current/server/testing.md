---
sidebar_position: 7
title: Тестирование обработчиков
---

# Тестирование обработчиков

`DarkWS.Testing` запускает обработчики без HTTP-сервера, WebSocket и Redis. Пакет
не зависит от тестового фреймворка, поэтому работает с NUnit, xUnit, MSTest и чем угодно ещё.

```bash
dotnet add package DarkWS.Testing
```

## Вызов действия через конвейер {#invoke-an-action-through-the-pipeline}

`DarkWsTestHost` собирает провайдер сервисов с вашими регистрациями DarkWS и вызывает
зарегистрированные действия точно так же, как это делает сервер:

```csharp
using System.Text.Json;
using DarkWS.Testing;
using Microsoft.Extensions.DependencyInjection;

await using var host = new DarkWsTestHost(builder => {
    builder.AddHandlersFromAssemblyContaining<MessageHandler>();
    builder.Services.AddScoped<IMessageStore, FakeMessageStore>();
});

var connection = host.CreateConnection(); // anonymous
await host.InvokeAsync(connection, "message:echo",
    JsonSerializer.SerializeToElement("hello"), requestId: "1");

using var response = JsonDocument.Parse(connection.SentMessages.Single());
Assert.That(response.RootElement.GetProperty("data").GetString(), Is.EqualTo("hello"));
```

`InvokeAsync` использует настоящие регистрацию, авторизацию, привязку JSON, инициализаторы
области, фильтры действий, преобразование ошибок, сериализацию и асинхронное освобождение
области. Каждый вызов создаёт новую область сообщения. Необязательный второй аргумент
конструктора настраивает `DarkWsOptions`, например `options => options.AllowNullPayloads = true`.
`host.Services` — корневой провайдер сервисов.

## Аутентифицированные вызовы {#authenticated-calls}

Передайте сессию в `CreateConnection`. Её principal должен быть аутентифицирован
(`Identity.IsAuthenticated` равно true):

```csharp
var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "42")], "test"));
var connection = host.CreateConnection(new AppSession("session-1", user, accountId, userId));
```

Сессия тестового подключения фиксируется при его создании.

## Рассылки {#broadcasts}

`host.Broadcasts` записывает каждую опубликованную рассылку с её целью и JSON-данными.
Настоящий broadcaster также направляет их подключениям, созданным хостом, поэтому их
`SentMessages` содержат и ответы, и конверты рассылок (`id` равен `"@"`):

```csharp
var sender = host.CreateConnection(aliceSession);
var receiver = host.CreateConnection(bobSession); // same account group

await host.InvokeAsync(sender, "message:send",
    JsonSerializer.SerializeToElement(new { text = "hi" }), requestId: "1");

Assert.That(host.Broadcasts.Single().Action, Is.EqualTo("message:created"));
Assert.That(receiver.SentMessages, Has.Count.EqualTo(1));
```

Чтобы проверить выбор получателей, создайте несколько подключений с разными сессиями и группами.
Захваченные массивы байтов — это копии; считайте их доступными только для чтения.

## Модульный тест обработчика напрямую {#unit-test-a-handler-directly}

Чтобы вызвать метод обработчика без диспетчера, инициализируйте его контекст из области:

```csharp
await using var scope = host.CreateScope(connection);
var handler = new MessageHandler(new FakeMessageStore());
scope.Initialize(handler);

var result = handler.Echo("hello");
await result.WriteResultAsync(new ResponseContext(connection, "direct", new DarkWsOptions()));
```

`scope.Services` разрешает зависимости конструктора и зарегистрированные обработчики. Прямая
инициализация задаёт подключение, сессию, отмену, сервисы и broadcaster, но пропускает
регистрацию, авторизацию, инициализаторы области и фильтры, а метаданные действия равны
null. Используйте `InvokeAsync`, когда эти проверки важны. Не освобождайте область, пока
не проверите или не запишете результат.

## Что хост не покрывает {#what-the-host-does-not-cover}

Хост всегда использует изолированный in-memory backplane, даже если ваша конфигурация
регистрирует Redis. Он не запускает hosted-сервисы, HTTP middleware, обмены
аутентификации, хуки жизненного цикла подключения, транспортные очереди и таймауты, а
также ограничения фреймов. Покрывайте это интеграционными тестами с настоящим сервером,
например с помощью `WebApplicationFactory` и `DarkWS.Client`.

Настраивайте получателей до вызова параллельных действий, а перед освобождением хоста
дожидайтесь завершения всех вызовов и освобождайте созданные вручную области.
