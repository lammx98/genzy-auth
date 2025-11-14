# Genzy Auth Service

A comprehensive authentication service for the Genzy microservices ecosystem, providing JWT-based authentication, OAuth2 integration (Google, Facebook), and refresh token management.

## Features

- ✅ **Local Authentication**: Email/password registration and login with SHA256 password hashing
- ✅ **OAuth2 Providers**: Google and Facebook login integration
- ✅ **JWT Tokens**: Stateless authentication with configurable expiry
- ✅ **Refresh Tokens**: Secure token rotation with automatic revocation
- ✅ **Token Management**: Logout and revocation endpoints
- ✅ **Swagger/OpenAPI**: Interactive API documentation
- ✅ **PostgreSQL**: Persistent storage for accounts and tokens
- ✅ **CORS**: Configured for frontend integration

## Tech Stack

- .NET 9.0
- ASP.NET Core Web API
- Entity Framework Core 9 + Npgsql
- JWT Bearer Authentication
- Google.Apis.Auth (Google OAuth)
- Facebook Graph API (Facebook OAuth)
- Swashbuckle (Swagger UI)

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=genzy-auth;Username=postgres;Password=yourpassword"
  },
  "JwtSettings": {
    "Secret": "your-secret-key-min-32-chars-long",
    "Issuer": "GenzyAuth",
    "Audience": "GenzyServices",
    "ExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  },
  "Authentication": {
    "Google": {
      "ClientId": "your-google-client-id.apps.googleusercontent.com",
      "ClientSecret": "your-google-client-secret"
    },
    "Facebook": {
      "AppId": "your-facebook-app-id",
      "AppSecret": "your-facebook-app-secret"
    }
  }
}
```

### OAuth Provider Setup

#### Google OAuth

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable Google+ API
4. Create OAuth 2.0 credentials
5. Add authorized redirect URIs:
   - `https://localhost:7000/signin-google` (dev)
   - Your production callback URL
6. Copy Client ID and Client Secret to appsettings

#### Facebook OAuth

1. Go to [Facebook Developers](https://developers.facebook.com/)
2. Create a new app or select existing
3. Add Facebook Login product
4. Configure OAuth redirect URIs:
   - `https://localhost:7000/signin-facebook` (dev)
   - Your production callback URL
5. Copy App ID and App Secret to appsettings

## Database Setup

```bash
# Install EF Core tools (if not already)
dotnet tool install --global dotnet-ef

# Create migration
dotnet ef migrations add InitialCreate --project genzy-auth.csproj

# Update database
dotnet ef database update --project genzy-auth.csproj
```

## Running the Service

```bash
# Development mode
set ASPNETCORE_ENVIRONMENT=Development
dotnet run --project genzy-auth.csproj

# With local config
set ASPNETCORE_ENVIRONMENT=Local
dotnet run --project genzy-auth.csproj
```

Default URL: `https://localhost:7000` (or as configured in `launchSettings.json`)

## API Endpoints

### Swagger UI
- **Development**: `https://localhost:7000/swagger`

### Authentication Endpoints

#### 1. Register (Local)
```http
POST /auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "fullName": "John Doe"
}
```

**Response:**
```json
{
  "token": "eyJhbGc...",
  "refreshToken": "8xQ3...",
  "email": "user@example.com",
  "fullName": "John Doe",
  "pictureUrl": null
}
```

#### 2. Login (Local)
```http
POST /auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Response:** Same as register

#### 3. External Login (Google/Facebook)
```http
POST /auth/external-login
Content-Type: application/json

{
  "provider": "Google",
  "token": "google-id-token-here"
}
```

**Google**: Send the ID token from Google Sign-In  
**Facebook**: Send the access token from Facebook Login

**Response:** Same as register

#### 4. Refresh Token
```http
POST /auth/refresh-token
Content-Type: application/json

"your-refresh-token-here"
```

**Response:**
```json
{
  "token": "new-jwt-token",
  "refreshToken": "new-refresh-token",
  "email": "user@example.com",
  "fullName": "John Doe",
  "pictureUrl": "https://..."
}
```

**Note:** Old refresh token is automatically revoked (token rotation)

#### 5. Logout
```http
POST /auth/logout
Authorization: Bearer your-jwt-token
Content-Type: application/json

"your-refresh-token-here"
```

**Response:**
```json
{
  "message": "Logged out successfully"
}
```

#### 6. Revoke Token
```http
POST /auth/revoke-token
Authorization: Bearer your-jwt-token
Content-Type: application/json

"refresh-token-to-revoke"
```

#### 7. Get Current User
```http
GET /auth/me
Authorization: Bearer your-jwt-token
```

**Response:**
```json
{
  "name": "John Doe",
  "email": "user@example.com"
}
```

### Google OAuth Flow (Browser)
```http
GET /auth/google-login?returnUrl=/dashboard
```
Redirects to Google consent screen, then back to `/auth/google-callback`

## Integration with Other Services

### 1. Add JWT Authentication

In your service's `Program.cs`:

```csharp
using Genzy.Base.Security.Jwt;

// Add JWT validation pointing to genzy-auth
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
```

### 2. Configure appsettings.json

```json
{
  "Jwt": {
    "Secret": "same-secret-as-genzy-auth",
    "Issuer": "GenzyAuth",
    "Audience": "GenzyServices",
    "ExpiryMinutes": 60
  }
}
```

### 3. Protect Endpoints

```csharp
[Authorize]
[HttpGet("protected")]
public IActionResult Protected()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var email = User.FindFirst(ClaimTypes.Email)?.Value;
    return Ok(new { userId, email });
}
```

### 4. Use UserContext (Recommended)

```csharp
using Genzy.Base.Security;

// In Program.cs
builder.Services.AddUserContext();
app.UseMiddleware<UserContextMiddleware>();

// In your service/controller
public class MyService(IUserContext userContext)
{
    public void DoSomething()
    {
        var userId = userContext.UserId;    // ulong from JWT NameIdentifier
        var email = userContext.Email;
        var roles = userContext.Roles;
    }
}
```

## Security Best Practices

1. **Secret Management**
   - Use environment variables or Azure Key Vault for secrets in production
   - Never commit secrets to source control
   - Rotate JWT secret periodically

2. **Token Expiry**
   - Keep JWT expiry short (15-60 minutes)
   - Use refresh tokens for extended sessions
   - Implement token rotation on refresh

3. **HTTPS Only**
   - Always use HTTPS in production
   - Configure HSTS headers

4. **Rate Limiting**
   - Add rate limiting middleware for login/register endpoints
   - Consider implementing account lockout after failed attempts

## Testing with jwt-sample

Generate test tokens using the jwt-sample tool:

```bash
cd tools/jwt-sample
dotnet run -- --userId 12345 --email test@example.com --role Admin

# Copy the token and use in Authorization header:
# Authorization: Bearer eyJhbGc...
```

## Architecture

```
┌─────────────┐
│   Client    │
│  (Browser/  │
│   Mobile)   │
└──────┬──────┘
       │
       │ 1. POST /auth/login or /auth/external-login
       ▼
┌─────────────────┐
│  genzy-auth     │
│  (Port 7000)    │
│                 │
│  - Register     │
│  - Login        │
│  - OAuth        │
│  - Refresh      │
│  - Logout       │
└────────┬────────┘
         │
         │ 2. Returns JWT + Refresh Token
         ▼
┌─────────────────┐
│   Other         │
│   Services      │
│                 │
│  - content      │
│  - progress     │
│  - ...          │
└─────────────────┘
    ▲
    │ 3. Client calls with Bearer token
    │    Services validate JWT locally
```

## Database Schema

### Accounts
```sql
CREATE TABLE accounts (
    id TEXT PRIMARY KEY,
    user_name TEXT NOT NULL,
    email TEXT NOT NULL,
    password_hash TEXT,
    full_name TEXT,
    avatar_url TEXT,
    provider TEXT,
    external_id TEXT
);
```

### RefreshTokens
```sql
CREATE TABLE refresh_tokens (
    id NUMERIC(20,0) PRIMARY KEY,
    token TEXT NOT NULL,
    account_id TEXT NOT NULL,
    expiry_date TIMESTAMP NOT NULL,
    is_revoked BOOLEAN NOT NULL,
    FOREIGN KEY (account_id) REFERENCES accounts(id)
);
```

## Troubleshooting

### Google Token Validation Fails
- Ensure the ID token is fresh (< 5 minutes old)
- Verify `ClientId` in appsettings matches Google Console
- Check token audience matches your ClientId

### Facebook Token Validation Fails
- Verify access token is valid (check expiry)
- Ensure Facebook app is not in development mode (or add test users)
- Request `email` permission in your frontend SDK

### Refresh Token Expired
- Check `RefreshTokenExpiryDays` in JwtSettings
- Expired tokens cannot be used; user must re-authenticate
- Consider implementing sliding expiration

## Future Enhancements

- [ ] Email verification flow
- [ ] Password reset functionality
- [ ] Account linking (merge providers)
- [ ] Background job to clean expired tokens
- [ ] Two-factor authentication (2FA)
- [ ] Rate limiting middleware
- [ ] Audit logging for auth events
- [ ] Additional providers (Microsoft, Apple, GitHub)

## License

MIT
