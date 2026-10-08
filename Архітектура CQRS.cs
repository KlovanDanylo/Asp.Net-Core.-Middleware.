using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Services.AddDbContext<StoryDbContext>(options =>
    options.UseInMemoryDatabase("CollaborativeStoryDb"));

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StoryDbContext>();
    db.Database.EnsureCreated();
    if (!db.UserProfiles.Any())
    {
        db.UserProfiles.Add(new UserProfile { Id = Guid.NewGuid(), Username = "Author1", Reputation = 10 });
        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public class Story
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid RootBranchId { get; set; }
}

public class Branch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryId { get; set; }
    public Guid? ParentBranchId { get; set; } 
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public List<Contribution> Contributions { get; set; } = new();
}

public enum ContributionStatus
{
    Pending,
    Accepted,
    Rejected
}

public class Contribution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ContributionStatus Status { get; set; } = ContributionStatus.Pending;
    public int VotesUp { get; set; } = 0;
    public int VotesDown { get; set; } = 0;
    public DateTime? PromotedAt { get; set; }
}

public class Vote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContributionId { get; set; }
    public Guid VoterId { get; set; }
    public int Value { get; set; } 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Round
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public DateTime StartAt { get; set; } = DateTime.UtcNow;
    public DateTime EndAt { get; set; }
    public bool IsClosed { get; set; } = false;
}

public class UserProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public int Reputation { get; set; } = 0;
}


public class StoryDbContext : DbContext
{
    public StoryDbContext(DbContextOptions<StoryDbContext> options) : base(options) { }

    public DbSet<Story> Stories => Set<Story>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
}


public record CreateStoryCommand(string Title, string Description) : IRequest<Guid>;

public class CreateStoryCommandValidator : AbstractValidator<CreateStoryCommand>
{
    public CreateStoryCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
    }
}

public class CreateStoryHandler : IRequestHandler<CreateStoryCommand, Guid>
{
    private readonly StoryDbContext _db;
    public CreateStoryHandler(StoryDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateStoryCommand request, CancellationToken cancellationToken)
    {
        var storyId = Guid.NewGuid();
        var rootBranch = new Branch
        {
            StoryId = storyId,
            Title = "Main Branch",
            ParentBranchId = null
        };

        var story = new Story
        {
            Id = storyId,
            Title = request.Title,
            Description = request.Description,
            RootBranchId = rootBranch.Id
        };

        _db.Stories.Add(story);
        _db.Branches.Add(rootBranch);
        await _db.SaveChangesAsync(cancellationToken);

        return storyId;
    }
}

public record AddContributionCommand(Guid BranchId, string Author, string Text) : IRequest<Guid>;

public class AddContributionCommandValidator : AbstractValidator<AddContributionCommand>
{
    public AddContributionCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Author).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MinimumLength(10);
    }
}

public class AddContributionHandler : IRequestHandler<AddContributionCommand, Guid>
{
    private readonly StoryDbContext _db;
    public AddContributionHandler(StoryDbContext db) => _db = db;

    public async Task<Guid> Handle(AddContributionCommand request, CancellationToken cancellationToken)
    {
        var branchExists = await _db.Branches.AnyAsync(b => b.Id == request.BranchId, cancellationToken);
        if (!branchExists) throw new ArgumentException("Ветка не найдена.");

        var contribution = new Contribution
        {
            BranchId = request.BranchId,
            Author = request.Author,
            Text = request.Text,
            Status = ContributionStatus.Pending
        };

        _db.Contributions.Add(contribution);
        await _db.SaveChangesAsync(cancellationToken);

        return contribution.Id;
    }
}

public record VoteContributionCommand(Guid ContributionId, Guid VoterId, int Value) : IRequest<bool>;

public class VoteContributionCommandValidator : AbstractValidator<VoteContributionCommand>
{
    public VoteContributionCommandValidator()
    {
        RuleFor(x => x.ContributionId).NotEmpty();
        RuleFor(x => x.VoterId).NotEmpty();
        RuleFor(x => x.Value).Must(v => v == 1 || v == -1).WithMessage("Значение голоса должно быть +1 или -1.");
    }
}

public class VoteContributionHandler : IRequestHandler<VoteContributionCommand, bool>
{
    private readonly StoryDbContext _db;
    private const int PromotionThreshold = 5; // Порог автопродвижения

    public VoteContributionHandler(StoryDbContext db) => _db = db;

    public async Task<bool> Handle(VoteContributionCommand request, CancellationToken cancellationToken)
    {
        var contribution = await _db.Contributions.FindAsync(new object[] { request.ContributionId }, cancellationToken);
        if (contribution == null || contribution.Status != ContributionStatus.Pending) return false;

        var existingVote = await _db.Votes
            .FirstOrDefaultAsync(v => v.ContributionId == request.ContributionId && v.VoterId == request.VoterId, cancellationToken);

        if (existingVote != null)
        {
            if (existingVote.Value == request.Value) return true; 

            if (existingVote.Value == 1) contribution.VotesUp--;
            else contribution.VotesDown--;

            existingVote.Value = request.Value;
        }
        else
        {
            _db.Votes.Add(new Vote
            {
                ContributionId = request.ContributionId,
                VoterId = request.VoterId,
                Value = request.Value
            });
        }

        if (request.Value == 1) contribution.VotesUp++;
        else contribution.VotesDown++;

        if (contribution.VotesUp - contribution.VotesDown >= PromotionThreshold)
        {
            contribution.Status = ContributionStatus.Accepted;
            contribution.PromotedAt = DateTime.UtcNow;

            var authorProfile = await _db.UserProfiles.FirstOrDefaultAsync(u => u.Username == contribution.Author, cancellationToken);
            if (authorProfile != null)
            {
                authorProfile.Reputation += 10;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public record ForkBranchCommand(Guid ParentBranchId, string Title) : IRequest<Guid>;

public class ForkBranchHandler : IRequestHandler<ForkBranchCommand, Guid>
{
    private readonly StoryDbContext _db;
    public ForkBranchHandler(StoryDbContext db) => _db = db;

    public async Task<Guid> Handle(ForkBranchCommand request, CancellationToken cancellationToken)
    {
        var parentBranch = await _db.Branches.FindAsync(new object[] { request.ParentBranchId }, cancellationToken);
        if (parentBranch == null) throw new ArgumentException("Родительская ветка не найдена.");

        var newBranch = new Branch
        {
            StoryId = parentBranch.StoryId,
            ParentBranchId = parentBranch.Id,
            Title = request.Title
        };

        _db.Branches.Add(newBranch);
        await _db.SaveChangesAsync(cancellationToken);

        return newBranch.Id;
    }
}

public record GetStoryQuery(Guid StoryId) : IRequest<StoryDetailsDto?>;

public record StoryDetailsDto(Guid Id, string Title, string Description, List<BranchDto> Branches);
public record BranchDto(Guid Id, string Title, Guid? ParentBranchId, List<ContributionDto> Contributions);
public record ContributionDto(Guid Id, string Author, string Text, string Status, int VotesUp, int VotesDown);

public class GetStoryHandler : IRequestHandler<GetStoryQuery, StoryDetailsDto?>
{
    private readonly StoryDbContext _db;
    public GetStoryHandler(StoryDbContext db) => _db = db;

    public async Task<StoryDetailsDto?> Handle(GetStoryQuery request, CancellationToken cancellationToken)
    {
        var story = await _db.Stories.FindAsync(new object[] { request.StoryId }, cancellationToken);
        if (story == null) return null;

        var branches = await _db.Branches
            .Where(b => b.StoryId == request.StoryId)
            .Include(b => b.Contributions)
            .ToListAsync(cancellationToken);

        var branchDtos = branches.Select(b => new BranchDto(
            b.Id,
            b.Title,
            b.ParentBranchId,
            b.Contributions.Select(c => new ContributionDto(
                c.Id, c.Author, c.Text, c.Status.ToString(), c.VotesUp, c.VotesDown
            )).ToList()
        )).ToList();

        return new StoryDetailsDto(story.Id, story.Title, story.Description, branchDtos);
    }
}


[ApiController]
[Route("api/[controller]")]
public class StoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public StoriesController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> CreateStory([FromBody] CreateStoryCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetStory), new { id }, id);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStory(Guid id)
    {
        var result = await _mediator.Send(new GetStoryQuery(id));
        return result != null ? Ok(result) : NotFound();
    }

    [HttpPost("contributions")]
    public async Task<IActionResult> AddContribution([FromBody] AddContributionCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(new { ContributionId = id });
    }

    [HttpPost("vote")]
    public async Task<IActionResult> Vote([FromBody] VoteContributionCommand command)
    {
        var success = await _mediator.Send(command);
        return success ? Ok("Голос принят") : BadRequest("Не удалось проголосовать");
    }

    [HttpPost("branches/fork")]
    public async Task<IActionResult> ForkBranch([FromBody] ForkBranchCommand command)
    {
        var branchId = await _mediator.Send(command);
        return Ok(new { BranchId = branchId });
    }
}