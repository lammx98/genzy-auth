using Genzy.Auth.Data;
using Genzy.Auth.Models;
using Genzy.Base.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Genzy.Auth.Services;

public class AccountService(AppDbContext context)
{
    private readonly AppDbContext _context = context;

    public async Task<Account> CreateAsync(Account account, string? password = null)
    {
        account.Id = Guid.NewGuid().ToString();
        await _context.AddAsync(account);
        await _context.SaveChangesAsync();
        return account;
    }

    public async Task<Account> FindByEmailAsync(string email)
    {
        var account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(o => o.Email == email);
        if (account != null) return account;
        throw new NotFoundException(email);
    }
}
