using EnergyApp.API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- เชื่อมต่อ Database PostgreSQL ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 1. เพิ่มการตั้งค่า CORS ตรงนี้ ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173") // URL ของ Vue 3
              .AllowAnyHeader()   // อนุญาตให้ส่ง Header อะไรมาก็ได้ (เช่น Authorization Bearer)
              .AllowAnyMethod()   // อนุญาตทุก Method (GET, POST, PUT, DELETE)
              .AllowCredentials(); // อนุญาตให้ส่ง Cookie/Token ข้ามโดเมนได้
    });
});
// ---------------------------------

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// --- 2. เปิดใช้งาน CORS ตรงนี้ (ต้องอยู่ก่อน UseAuthorization เสมอ!) ---
app.UseCors("AllowVueApp");
// -----------------------------------------------------------

app.UseAuthorization();
app.MapControllers();
app.Run();