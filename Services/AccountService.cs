using System.Security.Cryptography;
using System.Text;
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
        
        if (!string.IsNullOrEmpty(password))
        {
            account.PasswordHash = HashPassword(password);
        }
        
        await _context.AddAsync(account);
        await _context.SaveChangesAsync();
        return account;
    }

    public async Task<Account?> FindByEmailAsync(string email)
    {
        return await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(o => o.Email == email);
    }

    public async Task<Account?> FindByIdAsync(string id)
    {
        return await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Account?> FindByExternalIdAsync(string provider, string externalId)
    {
        return await _context.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Provider == provider && o.ExternalId == externalId);
    }

    public bool VerifyPassword(Account account, string password)
    {
        if (string.IsNullOrEmpty(account.PasswordHash) || string.IsNullOrEmpty(password))
            return false;

        return account.PasswordHash == HashPassword(password);
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
