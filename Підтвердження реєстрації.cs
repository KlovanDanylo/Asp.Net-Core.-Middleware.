using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseInMemoryDatabase("SocialAppDb"));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roles = { "User", "Moderator", "Admin" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var adminEmail = "admin@site.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        await userManager.CreateAsync(adminUser, "Admin123!");
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}

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


public class ApplicationUser : IdentityUser
{
    public List<Friendship> SentFriendRequests { get; set; } = new();
    public List<Friendship> ReceivedFriendRequests { get; set; } = new();
    public List<Post> Posts { get; set; } = new();
}

public enum PostVisibility
{
    Public,     
    Private,   
    ForFriends  
}

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public PostVisibility Visibility { get; set; } = PostVisibility.Public;
    public bool IsHidden { get; set; } = false; 
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
}

public enum FriendshipStatus
{
    Pending,
    Accepted,
    Rejected
}

public class Friendship
{
    public int Id { get; set; }
    public string RequesterId { get; set; } = string.Empty;
    public ApplicationUser? Requester { get; set; }

    public string ReceiverId { get; set; } = string.Empty;
    public ApplicationUser? Receiver { get; set; }

    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
}

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Friendship> Friendships => Set<Friendship>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Friendship>()
            .HasOne(f => f.Requester)
            .WithMany(u => u.SentFriendRequests)
            .HasForeignKey(f => f.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Friendship>()
            .HasOne(f => f.Receiver)
            .WithMany(u => u.ReceivedFriendRequests)
            .HasForeignKey(f => f.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}


public class RegisterViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class CreatePostViewModel
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public PostVisibility Visibility { get; set; } = PostVisibility.Public;
}

public class UserRoleViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CurrentRole { get; set; } = string.Empty;
    public bool IsLockedOut { get; set; }
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

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new ApplicationUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
        }
        return View(model);
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, false);
            if (result.Succeeded) return RedirectToAction("Index", "Home");
            ModelState.AddModelError("", "Неверный логин или пароль");
        }
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var currentUserId = _userManager.GetUserId(User);

        var friendIds = new List<string>();
        if (!string.IsNullOrEmpty(currentUserId))
        {
            friendIds = await _db.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == currentUserId || f.ReceiverId == currentUserId))
                .Select(f => f.RequesterId == currentUserId ? f.ReceiverId : f.RequesterId)
                .ToListAsync();
        }

        var posts = await _db.Posts
            .Include(p => p.User)
            .Where(p => !p.IsHidden)
            .Where(p =>
                p.Visibility == PostVisibility.Public ||
                (currentUserId != null && p.UserId == currentUserId) ||
                (p.Visibility == PostVisibility.ForFriends && friendIds.Contains(p.UserId))
            )
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(posts);
    }

    [Authorize]
    [HttpGet]
    public IActionResult CreatePost() => View();

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreatePost(CreatePostViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = _userManager.GetUserId(User);
            var post = new Post
            {
                Title = model.Title,
                Content = model.Content,
                Visibility = model.Visibility,
                UserId = userId!
            };
            _db.Posts.Add(post);
            await _db.SaveChangesAsync();
            return RedirectToAction("Index");
        }
        return View(model);
    }

    [Authorize(Roles = "Moderator,Admin")]
    [HttpPost]
    public async Task<IActionResult> ToggleHidePost(int id)
    {
        var post = await _db.Posts.FindAsync(id);
        if (post != null)
        {
            post.IsHidden = !post.IsHidden;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Index");
    }
}

[Authorize]
public class FriendsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public FriendsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpPost]
    public async Task<IActionResult> SendRequest(string receiverId)
    {
        var requesterId = _userManager.GetUserId(User);
        if (requesterId != receiverId && !_db.Friendships.Any(f => 
            (f.RequesterId == requesterId && f.ReceiverId == receiverId) ||
            (f.RequesterId == receiverId && f.ReceiverId == requesterId)))
        {
            _db.Friendships.Add(new Friendship
            {
                RequesterId = requesterId!,
                ReceiverId = receiverId,
                Status = FriendshipStatus.Pending
            });
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> AcceptRequest(int requestId)
    {
        var friendship = await _db.Friendships.FindAsync(requestId);
        if (friendship != null && friendship.ReceiverId == _userManager.GetUserId(User))
        {
            friendship.Status = FriendshipStatus.Accepted;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Index", "Home");
    }
}

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Index()
    {
        var users = _userManager.Users.ToList();
        var userRolesList = new List<UserRoleViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userRolesList.Add(new UserRoleViewModel
            {
                UserId = user.Id,
                Email = user.Email!,
                CurrentRole = roles.FirstOrDefault() ?? "None",
                IsLockedOut = await _userManager.IsLockedOutAsync(user)
            });
        }
        return View(userRolesList);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeRole(string userId, string newRole)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, newRole);
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> ToggleBlock(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            if (await _userManager.IsLockedOutAsync(user))
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            }
        }
        return RedirectToAction("Index");
    }
}