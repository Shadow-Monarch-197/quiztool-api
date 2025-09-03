using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using quizTool.Models;
using quizTool.Services;                       
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;             
using System.Text;

namespace quizTool.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly QuizTool_Dbcontext _DbContext;
        private readonly IEmailSender _email;

        //new
        private readonly ILogger<UserController> _log;
        private readonly IHostEnvironment _env;
        private readonly IConfiguration _cfg; // NEW

        // HARD-CODED OTP (TEST ONLY) — change/remove for prod
            private const string TEST_OTP = "111111"; // HARD-CODED OTP (TEST ONLY)

        //public UserController(QuizTool_Dbcontext dbContext, IEmailSender email)
        //{
        //    _DbContext = dbContext;
        //    _email = email;
        //}

        //new
        public UserController(QuizTool_Dbcontext dbContext, IEmailSender email, ILogger<UserController> log, IHostEnvironment env,IConfiguration cfg)
        {
            _DbContext = dbContext;
            _email = email;
            _log = log;
            _env = env;
            _cfg = cfg;
        }


        [AllowAnonymous]
        [HttpPost("LoginUser")]
        public async Task<IActionResult> LoginUser([FromBody] LoginModel userobj)
        {
            if (userobj == null || string.IsNullOrEmpty(userobj.email) || string.IsNullOrEmpty(userobj.password))
                return BadRequest(new { message = "Email and password are required." });

            var user = await _DbContext.Users
                .FirstOrDefaultAsync(u => u.email == userobj.email && u.password == userobj.password);

            if (user == null) return Unauthorized(new { message = "Invalid email or password." });

            var token = CreateJWT(user);

            return Ok(new LoginResponseModel
            {
                userId = user.userid,
                name = user.name,
                email = user.email,
                role = user.role,
                token = token
            });
        }


        [AllowAnonymous]
        [HttpPost("RegisterUser")]
        public async Task<IActionResult> RegisterUser([FromBody] UserDataModel newUser)
        {
            if (string.IsNullOrWhiteSpace(newUser.email) ||
                string.IsNullOrWhiteSpace(newUser.password) ||
                string.IsNullOrWhiteSpace(newUser.name))
            {
                return BadRequest(new { message = "Name, Email, and Password are required." });
            }

            var exists = await _DbContext.Users.AnyAsync(u => u.email == newUser.email);
            if (exists) return Conflict(new { message = "User with this email already exists." });

            newUser.role = "basic";
            newUser.createddate = DateTime.UtcNow;

            _DbContext.Users.Add(newUser);
            await _DbContext.SaveChangesAsync();

            return Ok(new { message = "User registered successfully." });
        }


        [Authorize(Roles = "admin")]
        [HttpGet("GetAllUsers")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _DbContext.Users
                .Select(u => new { u.userid, u.name, u.email, u.role, u.createddate })
                .ToListAsync();

            return Ok(users);
        }


        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            return Ok(new
            {
                name = User.Identity?.Name,
                roles = User.Claims.Where(c => c.Type == "role" || c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray(),
                claims = User.Claims.Select(c => new { c.Type, c.Value }).ToArray()
            });
        }




        //[AllowAnonymous]
        //[HttpPost("LoginStep1")]
        //public async Task<IActionResult> LoginStep1([FromBody] LoginModel body)
        //{
        //    if (body == null || string.IsNullOrWhiteSpace(body.email) || string.IsNullOrWhiteSpace(body.password))
        //        return BadRequest(new { message = "Email and password are required." });

        //    var user = await _DbContext.Users
        //        .FirstOrDefaultAsync(u => u.email == body.email && u.password == body.password);

        //    if (user == null) return Unauthorized(new { message = "Invalid email or password." });

        //    var challengeId = await CreateAndSendOtpAsync(user.email, OtpPurpose.Login, TimeSpan.FromMinutes(10));
        //    return Ok(new { challengeId, message = "OTP sent to your email." });
        //}

        //new

        [AllowAnonymous]
        [HttpPost("LoginStep1")]
        public async Task<IActionResult> LoginStep1([FromBody] LoginModel body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.email) || string.IsNullOrWhiteSpace(body.password))
                return BadRequest(new { message = "Email and password are required." });

            var user = await _DbContext.Users
                .FirstOrDefaultAsync(u => u.email == body.email && u.password == body.password);
            if (user == null) return Unauthorized(new { message = "Invalid email or password." });

            var (challengeId, devOtp) = await CreateAndSendOtpAsync(user.email, OtpPurpose.Login, TimeSpan.FromMinutes(10));
            return Ok(new { challengeId, message = devOtp != null ? "OTP ready (dev)" : "OTP sent", devOtp });
        }


        [AllowAnonymous]
        [HttpPost("LoginStep2")]
        public async Task<IActionResult> LoginStep2([FromBody] LoginStep2Dto dto)
        {
            var email = (dto.email ?? "").Trim().ToLower();
            var rec = await _DbContext.OtpCodes.FirstOrDefaultAsync(o => o.Id == dto.challengeId && o.Email == email && o.Purpose == OtpPurpose.Login);
            if (rec == null) return BadRequest(new { message = "Invalid challenge." });

            if (rec.ExpiresAt < DateTime.UtcNow) return Unauthorized(new { message = "OTP expired." });
            if (rec.ConsumedAt != null) return Unauthorized(new { message = "OTP already used." });
            if (rec.Attempts >= 5) return Unauthorized(new { message = "Too many attempts." });

            rec.Attempts++;
            // var ok = rec.CodeHash == Sha256(dto.otp ?? "");

            // HARD-CODED OTP (TEST ONLY): allow TEST_OTP to pass without hash match
            var provided = (dto.otp ?? "").Trim();
            var ok = rec.CodeHash == Sha256(provided) || provided == TEST_OTP; // HARD-CODED OTP (TEST ONLY)

            if (!ok) { await _DbContext.SaveChangesAsync(); return Unauthorized(new { message = "Incorrect OTP." }); }

            rec.ConsumedAt = DateTime.UtcNow;
            await _DbContext.SaveChangesAsync();

            var user = await _DbContext.Users.FirstOrDefaultAsync(u => u.email == email);
            if (user == null) return Unauthorized(new { message = "User no longer exists." });

            var token = CreateJWT(user);
            return Ok(new LoginResponseModel
            {
                userId = user.userid,
                name = user.name,
                email = user.email,
                role = user.role,
                token = token
            });
        }



        [AllowAnonymous]
        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotStartDto dto)
        {
            var email = (dto.email ?? "").Trim().ToLower();
            var exists = await _DbContext.Users.AnyAsync(u => u.email == email);
            if (exists)
            {
                await CreateAndSendOtpAsync(email, OtpPurpose.ResetPassword, TimeSpan.FromMinutes(10));
            }
            return Ok(new { message = "If the email exists, an OTP has been sent." });
        }


        [AllowAnonymous]
        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var email = (dto.email ?? "").Trim().ToLower();
            var rec = await _DbContext.OtpCodes
                .Where(o => o.Email == email && o.Purpose == OtpPurpose.ResetPassword && o.ConsumedAt == null)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (rec == null) return BadRequest(new { message = "No active reset request. Please request a new OTP." });
            if (rec.ExpiresAt < DateTime.UtcNow) return Unauthorized(new { message = "OTP expired." });
            if (rec.Attempts >= 5) return Unauthorized(new { message = "Too many attempts." });

            rec.Attempts++;
            // var ok = rec.CodeHash == Sha256(dto.otp ?? "");

            ar provided = (dto.otp ?? "").Trim();
            var ok = rec.CodeHash == Sha256(provided) || provided == TEST_OTP; // HARD-CODED OTP (TEST ONLY)
            
            if (!ok) { await _DbContext.SaveChangesAsync(); return Unauthorized(new { message = "Incorrect OTP." }); }

            var user = await _DbContext.Users.FirstOrDefaultAsync(u => u.email == email);
            if (user == null) return NotFound(new { message = "User not found." });

            user.password = dto.newPassword;
            rec.ConsumedAt = DateTime.UtcNow;

            await _DbContext.SaveChangesAsync();
            return Ok(new { message = "Password updated." });
        }


           private string CreateJWT(UserDataModel user)
        {
            // Read from config/user-secrets
            var secret   = _cfg["Jwt:Secret"]   ?? throw new Exception("Missing Jwt:Secret");
            var issuer   = _cfg["Jwt:Issuer"];
            var audience = _cfg["Jwt:Audience"];

            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, user.role ?? "basic"),
                new Claim(ClaimTypes.Name, user.email ?? string.Empty)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = credentials,
                Issuer = string.IsNullOrWhiteSpace(issuer) ? null : issuer,
                Audience = string.IsNullOrWhiteSpace(audience) ? null : audience
            };

            var handler = new JwtSecurityTokenHandler();
            var token   = handler.CreateToken(tokenDescriptor);
            return handler.WriteToken(token);
        }

        private static string RandomOtp(int len = 6)
        {
            var bytes = new byte[len];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            var sb = new StringBuilder(len);
            foreach (var b in bytes) sb.Append((char)('0' + b % 10));
            return sb.ToString();
        }

        private static string Sha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }

        //private async Task<int> CreateAndSendOtpAsync(string email, OtpPurpose purpose, TimeSpan ttl)
        //{
        //    var otp = RandomOtp(6);
        //    var rec = new OtpCode
        //    {
        //        Email = email.Trim().ToLower(),
        //        Purpose = purpose,
        //        CodeHash = Sha256(otp),
        //        ExpiresAt = DateTime.UtcNow.Add(ttl)
        //    };
        //    _DbContext.OtpCodes.Add(rec);
        //    await _DbContext.SaveChangesAsync();

        //    var subject = purpose == OtpPurpose.Login ? "Your login OTP" : "Password reset OTP";
        //    var body = $@"<p>Your one-time code is:</p>
        //                  <h2 style=""letter-spacing:4px;margin:0"">{otp}</h2>
        //                  <p>This code expires in {ttl.TotalMinutes} minutes.</p>";

        //    await _email.SendAsync(email, subject, body);
        //    return rec.Id;
        //}

        //new
        private async Task<(int id, string? devOtp)> CreateAndSendOtpAsync(string email, OtpPurpose purpose, TimeSpan ttl)
        {
            var otp = RandomOtp(6);
            var rec = new OtpCode
            {
                Email = email.Trim().ToLower(),
                Purpose = purpose,
                CodeHash = Sha256(otp),
                ExpiresAt = DateTime.UtcNow.Add(ttl)
            };
            _DbContext.OtpCodes.Add(rec);
            await _DbContext.SaveChangesAsync();

            var subject = purpose == OtpPurpose.Login ? "Your login OTP" : "Password reset OTP";
            var body = $@"<p>Your one-time code is:</p>
                  <h2 style=""letter-spacing:4px;margin:0"">{otp}</h2>
                  <p>This code expires in {ttl.TotalMinutes} minutes.</p>";

            // fire-and-forget with 5s timeout so the HTTP request never hangs
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            _ = Task.Run(async () =>
            {
                try { await _email.SendAsync(email, subject, body, cts.Token); }
                catch (Exception ex) { _log.LogWarning(ex, "Email send failed for {Email}", email); }
            });

            // show OTP in dev or when email is disabled (optional)
            var echo = _env.IsDevelopment();
            return (rec.Id, echo ? otp : null);
        }

    }
}
