using ClothShop;
using ClothShop.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ClothingShopContext>();

var key = new SymmetricSecurityKey(
    RandomNumberGenerator.GetBytes(32));

var validation = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = "JwtDemo",

    ValidateAudience = true,
    ValidAudience = "JwtDemoApi",

    ValidateIssuerSigningKey = true,
    IssuerSigningKey = key,

    ValidateLifetime = true,
    ClockSkew = TimeSpan.Zero,

    NameClaimType = "name",
    RoleClaimType = "role"
};

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = validation;
    });

builder.Services.AddAuthorization();



var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

//Вывод списка всей одежды в http://localhost:5072/api/closing/

app.MapGet("/api/closing", async (ClothingShopContext context) => Results.Json(context.Clothings));

//Выполняет поиск одежды по части названия в http://localhost:5072/api/closing/футболка

app.MapGet("/api/closing/{name}", async (ClothingShopContext context, string name) =>
{
    var Name = name.ToLower();
    var Closing = await context.Clothings.Where(x => x.Name.ToLower().Contains(name)).ToListAsync();
    if (Closing.Count == 0) return Results.NotFound(new { message = "Такой одежды нет" });
    return Results.Json(Closing);
});

//По идее создаёт новый заказ

app.MapPost("/api/orders", async (ClothingShopContext context, CreateOrder co) =>
{
    int userId = 1; // заглушка, после авторизации будет учитываться юзер
    var neworder = new Order
    {
        CreatedAt = DateTime.Now,
        StatusId = await context.OrderStatuses.Where(s => s.Name == "Создан").Select(s => s.Id).FirstAsync(),
        UserId = userId,
        OrderItems = co.ItemsInOrder.Select(x => new OrderItem
        {
            ClothingSizeId = x.ClothingSizeId,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice
        }).ToList()
    };


    context.Orders.Add(neworder);
    await context.SaveChangesAsync();
    return Results.Created($"/api/orders/{neworder.Id}", new
    {
        neworder.Id,
        neworder.CreatedAt,
        neworder.StatusId,
        neworder.UserId,
        Items = neworder.OrderItems.Select(i => new
        {
            i.Id,
            i.ClothingSizeId,
            i.Quantity,
            i.UnitPrice
        })
    });
});

//Получает список заказов определённого статуса

app.MapGet("/api/orders", async (ClothingShopContext context, int statusId) =>
{
    var orders = await context.Orders.Where(x => x.StatusId == statusId).Include(x => x.OrderItems).Select(x => new
    {
        x.Id,
        x.CreatedAt,
        x.StatusId,
        x.UserId,
        Items = x.OrderItems.Select(i => new
        {
            i.Id,
            i.ClothingSizeId,
            i.Quantity,
            i.UnitPrice
        })
    }).ToListAsync();

    return Results.Json(orders);
});

//Получает список заказов определённого покупателя

app.MapGet("/api/orders/user/{userId}", async (ClothingShopContext context, int userId) =>
{
    var orders = await context.Orders
        .Where(o => o.UserId == userId)
        .Include(o => o.OrderItems)
        .Select(o => new
        {
            o.Id,
            o.CreatedAt,
            o.StatusId,
            o.UserId,
            Items = o.OrderItems.Select(i => new
            {
                i.Id,
                i.ClothingSizeId,
                i.Quantity,
                i.UnitPrice
            })
        })
        .ToListAsync();

    return Results.Json(orders);
});





//app.MapDelete("/api/users/{id}", async (ClothingShopContext context, int id) =>
//{
//    // получаем пользователя по id
//    User? user = context.Users.FirstOrDefault(u => u.Id == id);

//    // если не найден, отправляем статусный код и сообщение об ошибке
//    if (user == null) return Results.NotFound(new { message = "Пользователь не найден" });

//    // если пользователь найден, удаляем его
//    context.Users.Remove(user);
//    await context.SaveChangesAsync();
//    return Results.Json(user);
//});

//изменить статус существующего заказа

app.MapPut("/api/orders/{id}/status", async (ClothingShopContext context, int id, UpdateOrderStat uos) =>
{
    var order = context.Orders.FirstOrDefault(o => o.Id == id);
    if (order == null) return Results.NotFound(new { message = "Заказ не найден" });

    var statusExists = await context.OrderStatuses.AnyAsync(s => s.Id == uos.StatusId);
    if (!statusExists)
        return Results.BadRequest(new { message = "Такого статуса не существует" });

    order.StatusId = uos.StatusId;
    await context.SaveChangesAsync();

    return Results.Json(new
    {
        order.Id,
        order.CreatedAt,
        order.StatusId,
        order.UserId
    });
});

//зарегистрировать нового пользователя

app.MapPost("/api/auth/login", async (ClothingShopContext context, LoginRequest request, IPasswordHasher<User> hasher) =>
{
    var user = await context.Users.Include(a => a.Role).FirstOrDefaultAsync(a => a.Login == request.Login);

    if (user == null ||
        string.IsNullOrWhiteSpace(request.Password))
        return Results.Challenge();

    var result = hasher.VerifyHashedPassword(
        user, user.PasswordHash, request.Password);

    if (result == PasswordVerificationResult.Failed)
        return Results.Challenge();

    return Results.Ok(new
    {
        access_token = CreateToken(user),
        token_type = "Bearer"
    });
}).AllowAnonymous();

app.Run();

string CreateToken(User user)
{
    var claims = new[]
    {
        new Claim("sub", user.Id.ToString()),
        new Claim("name", user.Login),
        new Claim("role", user.Role.Name)
    };

    var token = new JwtSecurityToken(
        issuer: "JwtDemo",
        audience: "JwtDemoApi",
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(15),
        signingCredentials: new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256));

    return new JwtSecurityTokenHandler().WriteToken(token);
}

