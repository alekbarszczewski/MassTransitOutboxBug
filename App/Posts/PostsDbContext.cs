using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace App.Posts;

public class PostsDbContext : DbContext
{
    public PostsDbContext(DbContextOptions<PostsDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("posts");
        
        builder.AddTransactionalOutboxEntities();
        
        base.OnModelCreating(builder);
    }
}