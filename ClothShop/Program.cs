using ClothShop;
using ClothShop.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ClothingShopContext>();

var app = builder.Build();

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



//app.MapPut("/api/users", async (UserDbContext context, User userData) => {
//    // получаем пользователя по id
//    var user = context.Users.FirstOrDefault(u => u.Id == userData.Id);
//    // если не найден, отправляем статусный код и сообщение об ошибке
//    if (user == null) return Results.NotFound(new { message = "Пользователь не найден" });

//    // если пользователь найден, изменяем его данные и отправляем обратно клиенту
//    user.LastName = userData.LastName;
//    user.FirstName = userData.FirstName;
//    user.MiddleName = userData.MiddleName;
//    await context.SaveChangesAsync();
//    return Results.Json(user);
//});



app.Run();
