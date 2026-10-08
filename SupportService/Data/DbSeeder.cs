using Microsoft.EntityFrameworkCore;
using SupportService.Models;

namespace SupportService.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync())
            return;

        var users = new List<User>
        {
            new()
            {
                Username = "demo",
                PasswordHash = "demo",
                FullName = "Демонстрационный пользователь",
                IsEmployee = false
            },

            new()
            {
                Username = "employee",
                PasswordHash = "employee",
                FullName = "Сотрудник поддержки",
                IsEmployee = true
            }
        };

        var categories = new List<Category>
        {
            new()
            {
                Name = "Техническая проблема",
                ReactionTimeMinutes = 60
            },

            new()
            {
                Name = "Доступ к системе",
                ReactionTimeMinutes = 30
            },

            new()
            {
                Name = "Консультация",
                ReactionTimeMinutes = 120
            },

            new()
            {
                Name = "Ошибка данных",
                ReactionTimeMinutes = 45
            },

            new()
            {
                Name = "Другое",
                ReactionTimeMinutes = 180
            }
        };

        db.Users.AddRange(users);
        db.Categories.AddRange(categories);

        await db.SaveChangesAsync();
    }
}