using Microsoft.EntityFrameworkCore;

public sealed class Todo
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsDone { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AppDbContext : DbContext
{
    public DbSet<Todo> Todos => Set<Todo>();

    private readonly string _cs;
    public AppDbContext(string connectionString) => _cs = connectionString;

    protected override void OnConfiguring(DbContextOptionsBuilder b)
        => b.UseNpgsql(_cs);

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Todo>(e =>
        {
            e.ToTable("todos");
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(200).IsRequired();
            e.HasIndex(t => t.IsDone);
        });
    }
}

class Program
{
    static async Task Main()
    {
        var cs = Environment.GetEnvironmentVariable("CONNECTION_STRING")
                 ?? "Host=localhost;Port=5432;Database=appdb;Username=app;Password=app";

        Console.WriteLine("Connecting to PostgreSQL...");
        using var db = new AppDbContext(cs);

        await db.Database.EnsureCreatedAsync();

        var one = new Todo { Title = "Buy milk", IsDone = false };
        db.Todos.Add(one);
        await db.SaveChangesAsync();
        Console.WriteLine($"Added one: id={one.Id}");

        var batch = new[]
        {
            new Todo { Title = "Write report" },
            new Todo { Title = "Read book" },
            new Todo { Title = "Do workout" }
        };
        await db.Todos.AddRangeAsync(batch);
        await db.SaveChangesAsync();
        Console.WriteLine($"Added many: {batch.Length}");

        var toUpdateOne = await db.Todos.FirstAsync(t => t.Title == "Buy milk");
        toUpdateOne.IsDone = true;
        await db.SaveChangesAsync();
        Console.WriteLine($"Updated one: id={toUpdateOne.Id} -> IsDone=true");

        var toUpdateMany = await db.Todos
            .Where(t => t.Title.Contains("Read") || t.Title.Contains("Write"))
            .ToListAsync();
        toUpdateMany.ForEach(t => t.IsDone = true);
        await db.SaveChangesAsync();
        Console.WriteLine($"Updated many: {toUpdateMany.Count}");

        var toDeleteOne = await db.Todos.FirstOrDefaultAsync(t => t.Title == "Do workout");
        if (toDeleteOne != null)
        {
            db.Todos.Remove(toDeleteOne);
            await db.SaveChangesAsync();
            Console.WriteLine($"Deleted one: id={toDeleteOne.Id}");
        }

        var toDeleteMany = await db.Todos.Where(t => t.IsDone).ToListAsync();
        db.Todos.RemoveRange(toDeleteMany);
        await db.SaveChangesAsync();
        Console.WriteLine($"Deleted many (IsDone=true): {toDeleteMany.Count}");

        var total = await db.Todos.CountAsync();
        Console.WriteLine($"Count: {total}");

        await db.Todos.AddRangeAsync(
            new Todo { Title = "Task A", IsDone = false },
            new Todo { Title = "Task B", IsDone = false },
            new Todo { Title = "Task C", IsDone = true }
        );
        await db.SaveChangesAsync();

        var firstOpen = await db.Todos.Where(t => !t.IsDone).OrderBy(t => t.Id).FirstOrDefaultAsync();
        Console.WriteLine(firstOpen is null
            ? "First open: not found"
            : $"First open: id={firstOpen.Id}, title={firstOpen.Title}");

        var openList = await db.Todos.Where(t => !t.IsDone).OrderBy(t => t.Id).ToListAsync();
        Console.WriteLine($"Open items ({openList.Count}): " + string.Join(", ", openList.Select(t => $"#{t.Id}:{t.Title}")));

        Console.WriteLine("Done.");
    }
}
