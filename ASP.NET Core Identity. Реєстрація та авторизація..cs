using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. НАСТРОЙКА СЕРВИСОВ И IDENTITY
// ============================================================================

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("IdentityNotesBlogDb"));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    // Редирект после успешной авторизации на персональные заметки
    options.AccessDeniedPath = "/Notes/Index";
});

builder.Services.AddControllersWithViews();

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

app.Run();


// ============================================================================
// 2. СУЩНОСТИ И БАЗА ДАННЫХ
// ============================================================================

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public List<Note> Notes { get; set; } = new();
    public List<BlogPost> BlogPosts { get; set; } = new();
}

public class Note
{
    public int Id { get; set; }
    [Required] public string Title { get; set; } = string.Empty;
    [Required] public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
}

public class BlogPost
{
    public int Id { get; set; }
    [Required] public string Title { get; set; } = string.Empty;
    [Required] public string Description { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }
}

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
}


// ============================================================================
// 3. VIEW MODELS
// ============================================================================

public class RegisterViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    public string? FullName { get; set; }
}

public class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
}

public class EditProfileViewModel
{
    public string? FullName { get; set; }
    [EmailAddress] public string Email { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)] public string CurrentPassword { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string NewPassword { get; set; } = string.Empty;
}

public class ProfileViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public List<BlogPost> Posts { get; set; } = new();
}


// ============================================================================
// 4. КОНТРОЛЛЕРЫ
// ============================================================================

// --- Управление аккаунтом и профилем ---
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _db;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, AppDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
    }

    [HttpGet] public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new ApplicationUser { UserName = model.Email, Email = model.Email, FullName = model.FullName };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Notes"); // Редирект на личные заметки
            }
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        }
        return View(model);
    }

    [HttpGet] public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, false);
            if (result.Succeeded)
            {
                // После авторизации выполняем редирект на персональные заметки
                return RedirectToAction("Index", "Notes");
            }
            ModelState.AddModelError("", "Неверный логин или пароль.");
        }
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // Редактирование профиля
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> EditProfile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();
        return View(new EditProfileViewModel { FullName = user.FullName, Email = user.Email! });
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> EditProfile(EditProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        user.FullName = model.FullName;
        await _userManager.UpdateAsync(user);
        return RedirectToAction("Profile");
    }

    // Смена пароля
    [Authorize]
    [HttpGet] public IActionResult ChangePassword() => View();

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (result.Succeeded) return RedirectToAction("Profile");

        foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        return View(model);
    }

    // Профиль с информацией и публикациями
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var posts = await _db.BlogPosts.Where(p => p.AuthorId == user.Id).ToListAsync();
        return View(new ProfileViewModel { User = user, Posts = posts });
    }
}

// --- CRUD Нотаток (Персональные Заметки) ---
[Authorize]
public class NotesController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotesController(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        var notes = await _db.Notes.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).ToListAsync();
        return View(notes);
    }

    [HttpGet] public IActionResult Create() => View();

    [HttpPost]
    public async Task<IActionResult> Create(Note note)
    {
        var userId = _userManager.GetUserId(User);
        note.UserId = userId!;
        _db.Notes.Add(note);
        await _db.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _userManager.GetUserId(User);
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        return note != null ? View(note) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Note note)
    {
        var userId = _userManager.GetUserId(User);
        var existing = await _db.Notes.FirstOrDefaultAsync(n => n.Id == note.Id && n.UserId == userId);
        if (existing == null) return NotFound();

        existing.Title = note.Title;
        existing.Content = note.Content;
        await _db.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _userManager.GetUserId(User);
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (note != null)
        {
            _db.Notes.Remove(note);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Index");
    }
}

// --- Главная страница и Блог ---
public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var posts = await _db.BlogPosts.Include(p => p.Author).OrderByDescending(p => p.PublishedAt).ToListAsync();
        return View(posts);
    }

    [Authorize]
    [HttpGet] public IActionResult CreateBlogPost() => View();

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateBlogPost(BlogPost post)
    {
        var userId = _userManager.GetUserId(User);
        post.AuthorId = userId!;
        _db.BlogPosts.Add(post);
        await _db.SaveChangesAsync();
        return RedirectToAction("Index");
    }
}