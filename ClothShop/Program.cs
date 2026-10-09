using ClothShop.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ClothingShopContext>();

var app = builder.Build();

//Вывод списка всей одежды в http://localhost:5072/api/closing/

app.MapGet("/api/closing", async (ClothingShopContext context) => Results.Json(context.Clothings));

//Выполняет поиск одежды по части названия http://localhost:5072/api/closing/футболка

app.MapGet("/api/closing/{name}", async (ClothingShopContext context, string name) =>
{
    var Name = name.ToLower();
    var Closing = await context.Clothings.Where(x => x.Name.ToLower().Contains(name)).ToListAsync();
    if (Closing.Count == 0) return Results.NotFound(new { message = "Такой одежды нет" });
    return Results.Json(Closing);
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

//app.MapPost("/api/users", async (ClothingShopContext context, User user) => {
//    // id теперь устанавливает БД
//    // добавляем пользователя в список
//    context.Users.Add(user);
//    await context.SaveChangesAsync();
//    return Results.Created($"/api/users/{user.Id}", user);
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
