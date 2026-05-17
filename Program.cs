using BelgaAuthAPI.Data;
using BelgaAuthAPI.Services;
using BelgaAuthAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configura JSON para usar camelCase (compatível com KeyAuth)
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = false;
    });
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlite("Data Source=auth.db"));

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddHttpClient();

// Autenticação com Cookies para Painel Admin, Revendedor e Cliente
builder.Services.AddAuthentication("AdminCookie")
    .AddCookie("AdminCookie", options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/AdminLogout";
        options.AccessDeniedPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
    })
    .AddCookie("ResellerCookie", options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/ResellerLogout";
        options.AccessDeniedPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
    })
    .AddCookie("CustomerCookie", options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.AuthenticationSchemes.Add("AdminCookie");
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("IsAdmin", "true");
    });
    
    options.AddPolicy("ResellerOnly", policy =>
    {
        policy.AuthenticationSchemes.Add("ResellerCookie");
        policy.AuthenticationSchemes.Add("AdminCookie"); // Admin também pode acessar se necessário
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("IsReseller", "true");
    });

    options.AddPolicy("CustomerOnly", policy =>
    {
        policy.AuthenticationSchemes.Add("CustomerCookie");
        policy.RequireAuthenticatedUser();
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Proteção DDoS - Rate Limiting (Máximo de 150 requisições por minuto por IP)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    options.AddPolicy("IPLimit", context =>
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 150,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Muitas requisições. Por favor, aguarde um minuto e tente novamente." }, token);
        }
        else
        {
            context.HttpContext.Response.ContentType = "text/html";
            await context.HttpContext.Response.WriteAsync("<h1>429 - Muitas Requisicoes</h1><p>Por favor, aguarde um minuto e tente novamente.</p>", token);
        }
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseStaticFiles();

// Proteção de Cabeçalhos de Segurança (Security Headers)
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("Referrer-Policy", "no-referrer-when-downgrade");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    await next();
});

// Ativar Rate Limiting de proteção
app.UseRateLimiter();

app.UseRouting();
app.UseAuthentication(); // Adicionar autenticação
app.UseAuthorization();

// Map Controllers primeiro
app.MapControllers();

// Map Razor Pages
app.MapRazorPages();

// Rota para testar se a aplicação está funcionando
app.MapGet("/api/health", () => Results.Json(new { status = "ok", message = "BelgaAuth API is running" }));

// ⚠️ ENDPOINT TEMPORÁRIO - Listar todos os usuários (APENAS PARA DESENVOLVIMENTO)
app.MapGet("/api/debug/users", async (AuthDbContext db) =>
{
    var users = await db.Users.ToListAsync();
    var userList = users.Select(u => new
    {
        id = u.Id,
        username = u.Username,
        email = u.Email,
        passwordHash = u.PasswordHash, // Hash BCrypt - não é a senha real
        hwid = u.HWID,
        ip = u.IP,
        createdDate = u.CreatedDate,
        lastLogin = u.LastLogin,
        isBanned = u.IsBanned
    }).ToList();
    
    return Results.Json(new { 
        total = userList.Count, 
        users = userList,
        note = "As senhas estão em hash BCrypt. Para criar usuários no Render, use senhas novas."
    });
}).RequireAuthorization("AdminOnly");

// Endpoint de backup do banco de dados (sempre disponível para admin)
app.MapGet("/api/admin/backup", async (HttpContext context) =>
{
    var dbPath = "auth.db";
    if (!System.IO.File.Exists(dbPath))
    {
        return Results.NotFound(new { message = "Database file not found" });
    }
    
    try
    {
        var bytes = await System.IO.File.ReadAllBytesAsync(dbPath);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return Results.File(bytes, "application/octet-stream", $"auth_backup_{timestamp}.db");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error creating backup: {ex.Message}");
    }
}).RequireAuthorization("AdminOnly");

// Rota padrão - redireciona para AdminLogin se não autenticado (Index.cshtml está em "/" mas requer autenticação)

// ⚠️ IMPORTANTE: Copiar users.data para auth.db no startup apenas se o banco não existir (Evita perda de dados)
if (System.IO.File.Exists("users.data") && !System.IO.File.Exists("auth.db"))
{
    Console.WriteLine("=== Copiando users.data para auth.db (Seeding inicial) ===");
    System.IO.File.Copy("users.data", "auth.db", true);
    Console.WriteLine("✓ Banco de dados configurado com 14 usuários!");
}

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    
    // ⚠️ IMPORTANTE: NÃO DELETAR O BANCO EM DESENVOLVIMENTO
    // Comentado para preservar os 14 usuários existentes
    var isDevMode = app.Environment.IsDevelopment();
    
    try
    {
        if (isDevMode)
        {
            // ⚠️ COMENTADO: Não deletar o banco para preservar os 14 usuários
            // db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
            Console.WriteLine("✅ Banco de dados verificado (modo desenvolvimento - preservando dados existentes)!");
        }
        else
        {
            // Em produção: apenas cria se não existir
            db.Database.EnsureCreated();
            Console.WriteLine("✅ Banco de dados verificado/criado com sucesso!");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Aviso ao inicializar banco: {ex.Message}");
        // Se falhar, tenta apenas criar se não existir
        try
        {
            db.Database.EnsureCreated();
        }
        catch (Exception ex2)
        {
            Console.WriteLine($"❌ Erro crítico ao criar banco: {ex2.Message}");
        }
    }

    // Garantir que as colunas novas existem no SQLite (migração automática silenciosa)
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN LastHWIDReset TEXT NULL;"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN HasPaused INTEGER NOT NULL DEFAULT 0;"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Subscriptions ADD COLUMN IsPaused INTEGER NOT NULL DEFAULT 0;"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Subscriptions ADD COLUMN PausedAt INTEGER NULL;"); } catch { }

    // Migrações do sistema de créditos
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Resellers ADD COLUMN Credits INTEGER NOT NULL DEFAULT 0;"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Resellers ADD COLUMN TotalCreditsEver INTEGER NOT NULL DEFAULT 0;"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN PasswordPlain TEXT NULL;"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Resellers ADD COLUMN PasswordPlain TEXT NULL;"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS CreditTransactions (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        ResellerId INTEGER NOT NULL,
        Amount INTEGER NOT NULL,
        Type TEXT NOT NULL,
        Description TEXT,
        CreatedDate TEXT NOT NULL,
        FOREIGN KEY (ResellerId) REFERENCES Resellers(Id)
    );"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS PaymentOrders (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        ResellerId INTEGER NOT NULL,
        PixCorrelationId TEXT,
        AmountCents INTEGER NOT NULL,
        Credits INTEGER NOT NULL,
        Status TEXT NOT NULL DEFAULT 'pending',
        QrCodeUrl TEXT,
        PixCopyPaste TEXT,
        CreatedDate TEXT NOT NULL,
        PaidAt TEXT,
        FOREIGN KEY (ResellerId) REFERENCES Resellers(Id)
    );"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE PaymentOrders ADD COLUMN CouponCode TEXT NULL;"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS Coupons (
        Code TEXT PRIMARY KEY,
        DiscountPercentage INTEGER NOT NULL DEFAULT 0,
        BonusPercentage INTEGER NOT NULL DEFAULT 0,
        MaxUses INTEGER,
        UsesCount INTEGER NOT NULL DEFAULT 0,
        IsActive INTEGER NOT NULL DEFAULT 1,
        CreatedDate TEXT NOT NULL,
        ExpirationDate TEXT
    );"); } catch { }

    // Seed initial data se não existir
    if (!db.Applications.Any())
    {
        db.Applications.Add(new Application
        {
            Name = "Marcosdelax89's Application",
            OwnerId = "Cn531Y1cND",
            Secret = "9026857324acfa914c0e10ca98af9c4abf72bbf463231a06b65c5a220c963cda",
            Version = "1.3",
            TotalUsers = 0,
            OnlineUsers = 0,
            TotalLicenses = 0
        });
        db.SaveChanges();
    }

    // Criar usuário admin padrão se não existir
    if (!db.Users.Any(u => u.Username == "admin"))
    {
        db.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            PasswordPlain = "admin123",
            Email = "admin@belgaauth.com",
            CreatedDate = DateTime.UtcNow,
            IsBanned = false,
            IsAdmin = true // Permitir acesso ao painel admin
        });
        db.SaveChanges();
        Console.WriteLine("✅ Usuário admin criado! Username: admin | Password: admin123");
        Console.WriteLine("⚠️  ALTERE A SENHA DEPOIS DO PRIMEIRO LOGIN!");
    }
    else
    {
        // Garantir que o usuário admin existente tenha permissão
        var adminUser = db.Users.FirstOrDefault(u => u.Username == "admin");
        if (adminUser != null && !adminUser.IsAdmin)
        {
            adminUser.IsAdmin = true;
            db.SaveChanges();
            Console.WriteLine("✅ Permissão de admin adicionada ao usuário 'admin'");
        }
    }
}

// Configuração para produção - aceita qualquer host e porta
// Use variáveis de ambiente PORT ou ASPNETCORE_URLS para configurar
// Exemplo: set PORT=8080 ou set ASPNETCORE_URLS=http://0.0.0.0:8080
// Configuração de porta - usa variável de ambiente ou padrão 5000
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";

// Em desenvolvimento, usa localhost; em produção, usa 0.0.0.0
var isDevelopment = app.Environment.IsDevelopment();
var host = isDevelopment 
    ? $"http://localhost:{port}" 
    : Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? $"http://0.0.0.0:{port}";

Console.WriteLine($"🚀 BelgaAuth iniciando em: {host}");
Console.WriteLine($"📱 Acesse: http://localhost:{port}");
if (isDevelopment)
{
    Console.WriteLine($"🌐 Admin: http://localhost:{port}/Login?type=admin");
    Console.WriteLine($"🏪 Revendedor: http://localhost:{port}/ResellerLogin");
}

app.Run(host);

