using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. НАСТРОЙКА СЕРВИСОВ, IDENTITY И SIGNALR
// ============================================================================

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("SignalRChatDb"));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 4;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR(); // Добавление службы SignalR

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Маппинг SignalR хаба
app.MapHub<ChatHub>("/chatHub");

app.Run();


// ============================================================================
// 2. БАЗА ДАННЫХ И МОДЕЛИ
// ============================================================================

public class ApplicationUser : IdentityUser { }

public class ChatMessage
{
    public int Id { get; set; }
    public string SenderUsername { get; set; } = string.Empty;
    public string? ReceiverUsername { get; set; } // Null если публичное сообщение
    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsPrivate => !string.IsNullOrEmpty(ReceiverUsername);
}

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
}


// ============================================================================
// 3. SIGNALR HUB (Обработка сообщений и онлайн-пользователей)
// ============================================================================

[Authorize]
public class ChatHub : Hub
{
    private readonly AppDbContext _db;
    // Потокобезопасный словарь онлайн-пользователей: Key = Username, Value = ConnectionId
    private static readonly ConcurrentDictionary<string, string> OnlineUsers = new();

    public ChatHub(AppDbContext db)
    {
        _db = db;
    }

    public override async Task OnConnectedAsync()
    {
        var username = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            OnlineUsers[username] = Context.ConnectionId;
            // Уведомляем всех об обновлении списка пользователей онлайн
            await Clients.All.SendAsync("UpdateUserList", OnlineUsers.Keys.ToList());
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = Context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            OnlineUsers.TryRemove(username, out _);
            await Clients.All.SendAsync("UpdateUserList", OnlineUsers.Keys.ToList());
        }
        await base.OnDisconnectedAsync(exception);
    }

    // Отправка публичного сообщения всем
    public async Task SendPublicMessage(string message)
    {
        var sender = Context.User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrEmpty(sender)) return;

        var chatMessage = new ChatMessage
        {
            SenderUsername = sender,
            Text = message
        };

        _db.ChatMessages.Add(chatMessage);
        await _db.SaveChangesAsync();

        await Clients.All.SendAsync("ReceiveMessage", chatMessage.Id, sender, message, false, chatMessage.SentAt.ToString("HH:mm"));
    }

    // Отправка личного сообщения
    public async Task SendPrivateMessage(string receiverUsername, string message)
    {
        var sender = Context.User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrEmpty(sender)) return;

        var chatMessage = new ChatMessage
        {
            SenderUsername = sender,
            ReceiverUsername = receiverUsername,
            Text = message
        };

        _db.ChatMessages.Add(chatMessage);
        await _db.SaveChangesAsync();

        // Отправляем получателю (если онлайн)
        if (OnlineUsers.TryGetValue(receiverUsername, out var receiverConnId))
        {
            await Clients.Client(receiverConnId).SendAsync("ReceiveMessage", chatMessage.Id, sender, message, true, chatMessage.SentAt.ToString("HH:mm"));
        }

        // Отправляем копию себе
        await Clients.Caller.SendAsync("ReceiveMessage", chatMessage.Id, sender, $"[Лично для {receiverUsername}]: {message}", true, chatMessage.SentAt.ToString("HH:mm"));
    }

    // Удаление сообщения
    public async Task DeleteMessage(int messageId)
    {
        var username = Context.User?.Identity?.Name;
        var msg = await _db.ChatMessages.FindAsync(messageId);

        if (msg != null && (msg.SenderUsername == username))
        {
            _db.ChatMessages.Remove(msg);
            await _db.SaveChangesAsync();

            // Уведомляем клиентов об удалении
            await Clients.All.SendAsync("MessageDeleted", messageId);
        }
    }
}


// ============================================================================
// 4. VIEW MODELS & CONTROLLERS
// ============================================================================

public class RegisterViewModel
{
    [Required] public string Username { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required] public string Username { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
}

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet] public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new ApplicationUser { UserName = model.Username };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }
            foreach (var err in result.Errors) ModelState.AddModelError("", err.Description);
        }
        return View(model);
    }

    [HttpGet] public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, false, false);
            if (result.Succeeded) return RedirectToAction("Index", "Home");
            ModelState.AddModelError("", "Неверный логин или пароль");
        }
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }
}

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;

    [Authorize]
    public async Task<IActionResult> Index()
    {
        var username = User.Identity?.Name;
        // Загружаем сообщения из истории (публичные + свои личные)
        var messages = await _db.ChatMessages
            .Where(m => m.ReceiverUsername == null || m.ReceiverUsername == username || m.SenderUsername == username)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        return View(messages);
    }
}