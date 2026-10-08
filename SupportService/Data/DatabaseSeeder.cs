using Microsoft.EntityFrameworkCore;
using SupportService.Models;

namespace SupportService.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        string mode)
    {
        bool isSmall = mode.ToLower() == "small";
        bool isWork = mode.ToLower() == "work";

        if (!isSmall && !isWork)
        {
            throw new ArgumentException(
                "Режим должен быть small или work.");
        }

        int userCount = isSmall ? 50 : 3000;
        int categoryCount = isSmall ? 5 : 10;
        int ticketCount = isSmall ? 300 : 50000;
        int commentCount = isSmall ? 900 : 150000;

        Console.WriteLine(
            $"Наполнение: {mode}");

        Console.WriteLine(
            $"Пользователи: {userCount}");

        Console.WriteLine(
            $"Категории: {categoryCount}");

        Console.WriteLine(
            $"Заявки: {ticketCount}");

        Console.WriteLine(
            $"Комментарии: {commentCount}");

        var random = new Random(42);

        // Полностью очищаем операционные данные.
        await db.Database.ExecuteSqlRawAsync("""
            TRUNCATE TABLE
                fyodor_kartashov.comments,
                fyodor_kartashov.tickets,
                fyodor_kartashov.categories,
                fyodor_kartashov.users
            RESTART IDENTITY CASCADE;
            """);

        // --------------------
        // Пользователи
        // --------------------

        var users = new List<User>();

        users.Add(new User
        {
            Username = "demo",
            PasswordHash = "demo",
            FullName = "Демонстрационный пользователь",
            IsEmployee = false
        });

        users.Add(new User
        {
            Username = "employee",
            PasswordHash = "employee",
            FullName = "Сотрудник поддержки",
            IsEmployee = true
        });

        for (int i = 3; i <= userCount; i++)
        {
            bool isEmployee = i % 10 == 0;

            users.Add(new User
            {
                Username = $"user{i}",
                PasswordHash = "password",
                FullName = isEmployee
                    ? $"Сотрудник поддержки {i}"
                    : $"Пользователь {i}",
                IsEmployee = isEmployee
            });
        }

        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        Console.WriteLine(
            $"Создано пользователей: {userCount}");

        // --------------------
        // Категории
        // --------------------

        var allCategories = new[]
        {
            new { Name = "Техническая проблема", Minutes = 60 },
            new { Name = "Доступ к системе", Minutes = 30 },
            new { Name = "Консультация", Minutes = 120 },
            new { Name = "Ошибка данных", Minutes = 45 },
            new { Name = "Другое", Minutes = 180 },

            new { Name = "Оборудование", Minutes = 90 },
            new { Name = "Программное обеспечение", Minutes = 75 },
            new { Name = "Сеть", Minutes = 40 },
            new { Name = "Учётная запись", Minutes = 25 },
            new { Name = "Безопасность", Minutes = 20 }
        };

        var categories = allCategories
            .Take(categoryCount)
            .Select(x => new Category
            {
                Name = x.Name,
                ReactionTimeMinutes = x.Minutes
            })
            .ToList();

        db.Categories.AddRange(categories);
        await db.SaveChangesAsync();

        Console.WriteLine(
            $"Создано категорий: {categoryCount}");

        // --------------------
        // Заявки
        // --------------------

        const int batchSize = 2000;

        var baseTime =
            new DateTime(
                2026,
                1,
                1,
                8,
                0,
                0,
                DateTimeKind.Utc);

        int createdTickets = 0;

        while (createdTickets < ticketCount)
        {
            int currentBatch =
                Math.Min(
                    batchSize,
                    ticketCount - createdTickets);

            var tickets =
                new List<Ticket>(currentBatch);

            for (int j = 0; j < currentBatch; j++)
            {
                int number =
                    createdTickets + j + 1;

                string status =
                    (number % 10) switch
                    {
                        0 => "closed",
                        1 or 2 or 3 => "in_progress",
                        _ => "new"
                    };

                var createdAt =
                    baseTime.AddMinutes(number * 3L);

                tickets.Add(new Ticket
                {
                    Title =
                        $"Заявка №{number}",

                    Description =
                        $"Описание проблемы для заявки №{number}",

                    Status = status,

                    CreatedAt = createdAt,

                    ClosedAt =
                        status == "closed"
                            ? createdAt.AddHours(3)
                            : null,

                    UserId =
                        random.Next(1, userCount + 1),

                    CategoryId =
                        random.Next(1, categoryCount + 1)
                });
            }

            db.Tickets.AddRange(tickets);
            await db.SaveChangesAsync();

            createdTickets += currentBatch;

            db.ChangeTracker.Clear();

            Console.WriteLine(
                $"Заявки: {createdTickets}/{ticketCount}");
        }

        // --------------------
        // Комментарии
        // --------------------

        int createdComments = 0;

        while (createdComments < commentCount)
        {
            int currentBatch =
                Math.Min(
                    batchSize,
                    commentCount - createdComments);

            var comments =
                new List<Comment>(currentBatch);

            for (int j = 0; j < currentBatch; j++)
            {
                int globalNumber =
                    createdComments + j;

                int ticketId =
                    (globalNumber / 3) + 1;

                int commentPosition =
                    globalNumber % 3;

                DateTime ticketCreatedAt =
                    baseTime.AddMinutes(ticketId * 3L);

                if (commentPosition == 0)
                {
                    // Комментарий пользователя.
                    comments.Add(new Comment
                    {
                        TicketId = ticketId,
                        UserId =
                            random.Next(1, userCount + 1),

                        Text =
                            $"Дополнительная информация по заявке {ticketId}.",

                        CreatedAt =
                            ticketCreatedAt.AddMinutes(2)
                    });
                }
                else if (commentPosition == 1)
                {
                    // Первый ответ сотрудника.
                    // Иногда быстрый, иногда намеренно
                    // превышает норматив.
                    int responseMinutes =
                        ticketId % 5 == 0
                            ? 240
                            : random.Next(5, 25);

                    comments.Add(new Comment
                    {
                        TicketId = ticketId,
                        UserId = 2,

                        Text =
                            $"Ответ сотрудника по заявке {ticketId}.",

                        CreatedAt =
                            ticketCreatedAt.AddMinutes(
                                responseMinutes)
                    });
                }
                else
                {
                    // Следующее сообщение пользователя.
                    comments.Add(new Comment
                    {
                        TicketId = ticketId,

                        UserId =
                            random.Next(1, userCount + 1),

                        Text =
                            $"Уточнение по заявке {ticketId}.",

                        CreatedAt =
                            ticketCreatedAt.AddMinutes(250)
                    });
                }
            }

            db.Comments.AddRange(comments);
            await db.SaveChangesAsync();

            createdComments += currentBatch;

            db.ChangeTracker.Clear();

            Console.WriteLine(
                $"Комментарии: {createdComments}/{commentCount}");
        }

        Console.WriteLine();
        Console.WriteLine("Наполнение завершено.");
        Console.WriteLine(
            $"users      = {await db.Users.CountAsync()}");
        Console.WriteLine(
            $"categories = {await db.Categories.CountAsync()}");
        Console.WriteLine(
            $"tickets    = {await db.Tickets.CountAsync()}");
        Console.WriteLine(
            $"comments   = {await db.Comments.CountAsync()}");
    }
}