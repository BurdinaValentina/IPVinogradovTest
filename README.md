# Task Management API

RESTful API для управления задачами с событийной интеграцией через RabbitMQ. Проект реализован на .NET 9 с использованием Minimal APIs, Entity Framework Core и PostgreSQL.

## 🛠 Технологический стек

- **.NET 9** (ASP.NET Core Minimal APIs)
- **Entity Framework Core 9** (Npgsql provider)
- **PostgreSQL** (Основная база данных)
- **RabbitMQ** (Брокер сообщений, клиент версии 6.8.1 для максимальной стабильности)
- **NUnit + Microsoft.AspNetCore.Mvc.Testing** (Интеграционное тестирование)
- **EF Core InMemory** (Изолированное тестирование слоя данных)

## ✨ Ключевые особенности и архитектурные решения

### 1. Оптимистичная блокировка (Optimistic Concurrency)
Для предотвращения race condition при одновременном завершении задачи используется системная колонка PostgreSQL `xmin`. 
- Реализовано через `modelBuilder.Entity<TaskItem>().UseXminAsConcurrencyToken()`.
- Это наиболее эффективный и "нативный" способ для PostgreSQL, не требующий дополнительных триггеров или колонок в схеме БД.
- На уровне приложения также присутствует быстрая проверка флага `IsCompleted` (fast-fail) для улучшения UX и снижения нагрузки на БД.

### 2. Гарантированная доставка событий
Событие `TaskCompletedEvent` публикуется в RabbitMQ **строго после** успешного вызова `SaveChangesAsync()`. Это гарантирует, что в очередь не попадут сообщения о задачах, которые не были сохранены из-за ошибки или конфликта конкурентности.

### 3. Graceful Shutdown
Реализован корректный останов приложения через `IHostedService` (`RabbitMqShutdownService`). При получении сигнала остановки сервис гарантированно закрывает каналы и соединения с RabbitMQ, предотвращая потерю сообщений и утечки ресурсов.

### 4. Изолированное интеграционное тестирование
Тесты используют `WebApplicationFactory` с кастомной настройкой через `ConfigureTestServices`:
- Окружение переключается в `Testing`, что предотвращает регистрацию PostgreSQL провайдера на старте.
- `Npgsql` подменяется на `Microsoft.EntityFrameworkCore.InMemory`.
- Реальная реализация `IRabbitMqPublisher` заменяется на `MockRabbitMqPublisher`, что позволяет проверять факт публикации событий без поднятия реального брокера в тестах.
- Каждый тест получает свой экземпляр фабрики и уникальную InMemory базу данных для полной изоляции.

## 🚀 Быстрый старт

### Предварительные требования
1. [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
2. [PostgreSQL](https://www.postgresql.org/download/) (запущен и доступен)
3. [RabbitMQ](https://www.rabbitmq.com/docs/install-windows) (версии 3.13.x рекомендованы для стабильной работы с современными версиями Erlang)

### 1. Клонирование и восстановление зависимостей
```bash
git clone <repository-url>
cd TaskApi
dotnet restore