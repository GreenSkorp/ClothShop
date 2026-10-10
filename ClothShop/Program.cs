using ClothShop;
using ClothShop.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

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
ClothingShopContext context = new ClothingShopContext();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = validation;
    });

builder.Services.AddAuthorization();

var hasher = new PasswordHasher<User>();

List<User> accounts = context.Users.ToList();


foreach (var account in accounts)
{
    account.PasswordHash = hasher.HashPassword(account, account.PasswordHash);
}

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

app.MapPost("/api/orders", async (ClothingShopContext context, CreateOrder co, ClaimsPrincipal user) =>
{
    //int userId = 1; // заглушка, после авторизации будет учитываться юзер
    var neworder = new Order
    {
        CreatedAt = DateTime.Now,
        StatusId = await context.OrderStatuses.Where(s => s.Name == "Создан").Select(s => s.Id).FirstAsync(),
        UserId = Convert.ToInt32(user.FindFirst("sub").Value),
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
}).RequireAuthorization();

//app.MapGet("/hash", (ClothingShopContext context) =>
//{
//    foreach (var user in context.Users.ToList())
//    {
//        user.PasswordHash = hasher.HashPassword(user, user.PasswordHash);
//    }

//    context.SaveChanges();
//    return Results.Ok(new
//    {
//        access_token = CreateToken(context.Users.First(i => i.Id == 1)),
//        token_type = "Bearer"
//    });
//}).AllowAnonymous();

//Получает список заказов определённого статуса

app.MapGet("/api/orders/{statusId}", async (ClothingShopContext context, int statusId) =>
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
}).RequireAuthorization();

//Получает список заказов определённого покупателя

app.MapGet("/api/orders/", async (ClothingShopContext context, ClaimsPrincipal user) =>
{
    var orders = await context.Orders
        .Where(o => o.User.Login == user.Identity.Name)
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
}).RequireAuthorization();

//Авторизация пользователя

app.MapPost("/api/auth/login", async (ClothingShopContext context, LoginRequest request) =>
{
    var user = await context.Users.Include(a => a.Role).FirstOrDefaultAsync(a => a.Login == request.Login);

    if (user == null || string.IsNullOrWhiteSpace(request.Password))
        return Results.Challenge();

    var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

    if (result == PasswordVerificationResult.Failed)
        return Results.Challenge();

    return Results.Ok(new
    {
        access_token = CreateToken(user),
        token_type = "Bearer"
    });
});

//Регистрация пользователя

app.MapPost("/api/reg/registr", async (ClothingShopContext context, RegRequest request) =>
{
    var user = await context.Users.FirstOrDefaultAsync(u => u.Login == request.Login);
    if (user != null) return Results.NotFound(new { message = "Логин занят" });

    var newuser = new User
    {
        Login = request.Login,
        PasswordHash = request.Password,
        Phone = request.Phone,
        DeliveryAddress = request.Address,
        RoleId = 2,
        Role = context.Roles.FirstOrDefault(r => r.Id==2)

    };
    newuser.PasswordHash = hasher.HashPassword(newuser, request.Password);
    await context.Users.AddAsync(newuser);
    context.SaveChanges();
    return Results.Created($"/api/users/{newuser.Id}", newuser);

}).AllowAnonymous();

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
}).RequireAuthorization("Manager");

//изменить количество товара определённого размера
//order-items
app.MapPut("/api/orderitems/{id}/quantity", async (ClothingShopContext context, int id, UpdateQuantity uq) =>
{
    var item = await context.OrderItems.FirstOrDefaultAsync(i => i.Id == id);
    if (item is null)
        return Results.NotFound(new { message = "Позиция заказа не найдена" });

    if (uq.Quantity <= 0)
        return Results.BadRequest(new { message = "Количество должно быть больше 0" });

    item.Quantity = uq.Quantity;
    await context.SaveChangesAsync();

    return Results.Json(new
    {
        item.Id,
        item.OrderId,
        item.ClothingSizeId,
        item.Quantity,
        item.UnitPrice
    });
}).RequireAuthorization("Manager");

//убрать товар из продажи

app.MapDelete("/api/closing/{id}", async (ClothingShopContext context, int id) =>
{
    var clothing = await context.Clothings.FirstOrDefaultAsync(c => c.Id == id);
    if (clothing is null)
        return Results.NotFound(new { message = "Товар не найден" });

    if (!clothing.IsAvailable)
        return Results.BadRequest(new { message = "Товар уже снят с продажи" });
    clothing.IsAvailable = false;
    await context.SaveChangesAsync();

    return Results.Json(new
    {
        clothing.Id,
        clothing.Name,
        clothing.IsAvailable
    });
}).RequireAuthorization("Manager");

//вернуть товар в продажу;

app.MapPut("/api/clothing/{id}/restore", async (ClothingShopContext context, int id) =>
{
    var clothing = await context.Clothings.FirstOrDefaultAsync(c => c.Id == id);
    if (clothing is null)
        return Results.NotFound(new { message = "Товар не найден" });

    if (clothing.IsAvailable)
        return Results.BadRequest(new { message = "Товар уже в продаже" });

    clothing.IsAvailable = true;
    await context.SaveChangesAsync();

    return Results.Json(new
    {
        clothing.Id,
        clothing.Name,
        clothing.IsAvailable
    });
}).RequireAuthorization("Manager");

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

