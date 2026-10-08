using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SupportService.Data;
using SupportService.DTOs;
using SupportService.Models;
using SupportService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<DbRequestMetrics>();
builder.Services.AddScoped<DbMetricsInterceptor>();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"));

    options.AddInterceptors(
        serviceProvider.GetRequiredService<DbMetricsInterceptor>());
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "fyodor_kartashov_auth";

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();


// --------------------
// Статические файлы
// --------------------

app.UseDefaultFiles();
app.UseStaticFiles();


// --------------------
// Авторизация
// --------------------

app.Use(async (context, next) =>
{
    var metrics =
        context.RequestServices.GetRequiredService<DbRequestMetrics>();

    metrics.Reset();

    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Db-Queries"] =
            metrics.QueryCount.ToString();

        context.Response.Headers["X-Db-Time-Ms"] =
            metrics.TotalMilliseconds.ToString(
                "F3",
                System.Globalization.CultureInfo.InvariantCulture);

        return Task.CompletedTask;
    });

    await next();
});

app.UseAuthentication();
app.UseAuthorization();


// --------------------
// Начальное наполнение
// --------------------

using (var scope = app.Services.CreateScope())
{
    var db =
        scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (args.Length >= 2 &&
        args[0].Equals(
            "seed",
            StringComparison.OrdinalIgnoreCase))
    {
        await DatabaseSeeder.SeedAsync(
            db,
            args[1]);

        return;
    }

    await DbSeeder.SeedAsync(db);
}


// --------------------
// Вход
// --------------------

app.MapPost("/api/login", async (
    LoginRequest request,
    AppDbContext db,
    HttpContext httpContext) =>
{
    var user = await db.Users
        .FirstOrDefaultAsync(
            x => x.Username == request.Username);

    if (user is null ||
        user.PasswordHash != request.Password)
    {
        return Results.Unauthorized();
    }

    var claims = new List<Claim>
    {
        new(
            ClaimTypes.NameIdentifier,
            user.Id.ToString()),

        new(
            ClaimTypes.Name,
            user.Username),

        new(
            "full_name",
            user.FullName),

        new(
            "is_employee",
            user.IsEmployee.ToString())
    };

    var identity = new ClaimsIdentity(
        claims,
        CookieAuthenticationDefaults.AuthenticationScheme);

    var principal =
        new ClaimsPrincipal(identity);

    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal);

    return Results.Ok(new
    {
        user.Id,
        user.Username,
        user.FullName,
        user.IsEmployee
    });
});


// --------------------
// Выход
// --------------------

app.MapPost("/api/logout", async (
    HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(
        CookieAuthenticationDefaults.AuthenticationScheme);

    return Results.Ok(new
    {
        message = "Logged out"
    });
})
.RequireAuthorization();


// --------------------
// Текущий пользователь
// --------------------

app.MapGet("/api/me", (
    HttpContext httpContext) =>
{
    var user = httpContext.User;

    return Results.Ok(new
    {
        id = user.FindFirstValue(
            ClaimTypes.NameIdentifier),

        username =
            user.Identity?.Name,

        fullName =
            user.FindFirstValue("full_name"),

        isEmployee =
            user.FindFirstValue("is_employee")
    });
})
.RequireAuthorization();


// --------------------
// Категории
// --------------------

app.MapGet("/api/categories", async (
    AppDbContext db) =>
{
    var categories = await db.Categories
        .AsNoTracking()
        .OrderBy(x => x.Id)
        .Select(x => new
        {
            x.Id,
            x.Name,
            x.ReactionTimeMinutes
        })
        .ToListAsync();

    return Results.Ok(categories);
})
.RequireAuthorization();


// --------------------
// Список заявок
// --------------------

app.MapGet("/api/tickets", async (
    int? page,
    int? size,
    string? status,
    int? categoryId,
    AppDbContext db) =>
{
    var currentPage =
        page.GetValueOrDefault(1);

    var pageSize =
        size.GetValueOrDefault(20);

    if (currentPage < 1)
    {
        currentPage = 1;
    }

    if (pageSize < 1)
    {
        pageSize = 20;
    }

    if (pageSize > 100)
    {
        pageSize = 100;
    }

    var query = db.Tickets
        .AsNoTracking()
        .AsQueryable();

    if (!string.IsNullOrWhiteSpace(status))
    {
        query = query.Where(
            x => x.Status == status);
    }

    if (categoryId.HasValue)
    {
        query = query.Where(
            x => x.CategoryId == categoryId.Value);
    }

    var total =
        await query.CountAsync();

    var items = await query
        .OrderByDescending(x => x.CreatedAt)
        .Skip((currentPage - 1) * pageSize)
        .Take(pageSize)
        .Select(x => new
        {
            x.Id,
            x.Title,
            x.Status,
            x.CreatedAt,
            x.ClosedAt,

            User = new
            {
                x.User.Id,
                x.User.Username,
                x.User.FullName
            },

            Category = new
            {
                x.Category.Id,
                x.Category.Name,
                x.Category.ReactionTimeMinutes
            },

            CommentsCount =
                x.Comments.Count
        })
        .ToListAsync();

    return Results.Ok(new
    {
        page = currentPage,
        size = pageSize,
        total,
        items
    });
})
.RequireAuthorization();


// --------------------
// Карточка заявки
// --------------------

app.MapGet("/api/tickets/{id:int}", async (
    int id,
    AppDbContext db) =>
{
    var ticket = await db.Tickets
        .AsNoTracking()
        .Include(x => x.User)
        .Include(x => x.Category)
        .Include(x => x.Comments)
            .ThenInclude(x => x.User)
        .FirstOrDefaultAsync(
            x => x.Id == id);

    if (ticket is null)
    {
        return Results.NotFound(new
        {
            message = "Ticket not found"
        });
    }

    var firstEmployeeComment =
        ticket.Comments
            .Where(
                x => x.User.IsEmployee)
            .OrderBy(
                x => x.CreatedAt)
            .FirstOrDefault();

    double? reactionTimeMinutes = null;

    if (firstEmployeeComment is not null)
    {
        reactionTimeMinutes =
            TicketRules.CalculateReactionTimeMinutes(
                ticket.CreatedAt,
                firstEmployeeComment.CreatedAt);
    }

    var referenceTime =
        firstEmployeeComment?.CreatedAt
        ?? DateTime.UtcNow;

    var slaViolated =
        TicketRules.IsSlaViolated(
            ticket.CreatedAt,
            referenceTime,
            ticket.Category.ReactionTimeMinutes);

    return Results.Ok(new
    {
        ticket.Id,
        ticket.Title,
        ticket.Description,
        ticket.Status,
        ticket.CreatedAt,
        ticket.ClosedAt,

        User = new
        {
            ticket.User.Id,
            ticket.User.Username,
            ticket.User.FullName
        },

        Category = new
        {
            ticket.Category.Id,
            ticket.Category.Name,
            ticket.Category.ReactionTimeMinutes
        },

        ReactionTimeMinutes =
            reactionTimeMinutes,

        SlaViolated =
            slaViolated,

        Comments = ticket.Comments
            .OrderBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Text,
                x.CreatedAt,

                User = new
                {
                    x.User.Id,
                    x.User.Username,
                    x.User.FullName,
                    x.User.IsEmployee
                }
            })
    });
})
.RequireAuthorization();


// --------------------
// Создание заявки
// --------------------

app.MapPost("/api/tickets", async (
    CreateTicketRequest request,
    AppDbContext db,
    HttpContext httpContext) =>
{
    if (string.IsNullOrWhiteSpace(
        request.Title))
    {
        return Results.BadRequest(new
        {
            message =
                "Title is required"
        });
    }

    if (string.IsNullOrWhiteSpace(
        request.Description))
    {
        return Results.BadRequest(new
        {
            message =
                "Description is required"
        });
    }

    var categoryExists =
        await db.Categories.AnyAsync(
            x => x.Id ==
                 request.CategoryId);

    if (!categoryExists)
    {
        return Results.NotFound(new
        {
            message =
                "Category not found"
        });
    }

    var userIdString =
        httpContext.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

    if (!int.TryParse(
        userIdString,
        out var userId))
    {
        return Results.Unauthorized();
    }

    var ticket = new Ticket
    {
        Title =
            request.Title.Trim(),

        Description =
            request.Description.Trim(),

        Status = "new",

        CreatedAt =
            DateTime.UtcNow,

        UserId =
            userId,

        CategoryId =
            request.CategoryId
    };

    db.Tickets.Add(ticket);

    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/tickets/{ticket.Id}",
        new
        {
            ticket.Id,
            ticket.Title,
            ticket.Status,
            ticket.CreatedAt,
            ticket.UserId,
            ticket.CategoryId
        });
})
.RequireAuthorization();


// --------------------
// Добавление комментария
// --------------------

app.MapPost(
    "/api/tickets/{id:int}/comments",
    async (
        int id,
        CreateCommentRequest request,
        AppDbContext db,
        HttpContext httpContext) =>
{
    if (string.IsNullOrWhiteSpace(
        request.Text))
    {
        return Results.BadRequest(new
        {
            message =
                "Comment text is required"
        });
    }

    var ticketExists =
        await db.Tickets.AnyAsync(
            x => x.Id == id);

    if (!ticketExists)
    {
        return Results.NotFound(new
        {
            message =
                "Ticket not found"
        });
    }

    var userIdString =
        httpContext.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

    if (!int.TryParse(
        userIdString,
        out var userId))
    {
        return Results.Unauthorized();
    }

    var comment = new Comment
    {
        TicketId = id,
        UserId = userId,
        Text = request.Text.Trim(),
        CreatedAt = DateTime.UtcNow
    };

    db.Comments.Add(comment);

    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/tickets/{id}",
        new
        {
            comment.Id,
            comment.TicketId,
            comment.UserId,
            comment.Text,
            comment.CreatedAt
        });
})
.RequireAuthorization();


// --------------------
// Изменение статуса заявки
// --------------------

app.MapPut(
    "/api/tickets/{id:int}/status",
    async (
        int id,
        UpdateTicketStatusRequest request,
        AppDbContext db,
        HttpContext httpContext) =>
{
    var isEmployeeString =
        httpContext.User.FindFirstValue(
            "is_employee");

    if (!bool.TryParse(
            isEmployeeString,
            out var isEmployee)
        || !isEmployee)
    {
        return Results.Forbid();
    }

    if (!TicketRules.IsStatusAllowed(
        request.Status))
    {
        return Results.BadRequest(new
        {
            message =
                "Allowed statuses: new, in_progress, closed"
        });
    }

    var ticket =
        await db.Tickets
            .FirstOrDefaultAsync(
                x => x.Id == id);

    if (ticket is null)
    {
        return Results.NotFound(new
        {
            message =
                "Ticket not found"
        });
    }

    ticket.Status =
        request.Status;

    if (request.Status == "closed")
    {
        ticket.ClosedAt =
            DateTime.UtcNow;
    }
    else
    {
        ticket.ClosedAt = null;
    }

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        ticket.Id,
        ticket.Status,
        ticket.ClosedAt
    });
})
.RequireAuthorization();


// --------------------
// Сводка
// --------------------

app.MapGet("/api/summary", async (
    AppDbContext db) =>
{
    var now =
        DateTime.UtcNow;

    var categories =
        await db.Categories
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(category => new
            {
                category.Id,
                category.Name,
                category.ReactionTimeMinutes,

                OpenTickets =
                    category.Tickets.Count(
                        ticket =>
                            ticket.Status
                            != "closed")
            })
            .ToListAsync();

    var tickets =
        await db.Tickets
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Comments)
                .ThenInclude(x => x.User)
            .ToListAsync();

    var totalTickets =
        tickets.Count;

    var violatedTickets =
        tickets.Count(ticket =>
        {
            var firstEmployeeComment =
                ticket.Comments
                    .Where(
                        comment =>
                            comment.User
                                .IsEmployee)
                    .OrderBy(
                        comment =>
                            comment.CreatedAt)
                    .FirstOrDefault();

            var referenceTime =
                firstEmployeeComment?.CreatedAt
                ?? now;

            return TicketRules.IsSlaViolated(
                ticket.CreatedAt,
                referenceTime,
                ticket.Category
                    .ReactionTimeMinutes);
        });

    var violationRate =
        totalTickets == 0
            ? 0
            : (double)violatedTickets
              / totalTickets;

    return Results.Ok(new
    {
        TotalTickets =
            totalTickets,

        ViolatedTickets =
            violatedTickets,

        ViolationRate =
            violationRate,

        Categories =
            categories
    });
})
.RequireAuthorization();


app.Run();

public partial class Program
{
}