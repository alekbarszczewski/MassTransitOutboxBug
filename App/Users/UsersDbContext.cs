using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace App.Users;

public class UsersDbContext : DbContext
{
    public UsersDbContext(DbContextOptions<UsersDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("users");
        
        builder.AddTransactionalOutboxEntities();
        
        base.OnModelCreating(builder);
    }
}