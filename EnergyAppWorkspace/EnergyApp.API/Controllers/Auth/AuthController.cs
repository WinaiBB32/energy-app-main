using EnergyApp.API.Data;
using EnergyApp.API.DTOs;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")] // URL จะเป็น: /api/v1/auth
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto request)
        {
            // 1. เช็คว่ามี Email นี้ในระบบหรือยัง?
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest(new { message = "อีเมลนี้ถูกใช้งานแล้ว" });

            // 2. เข้ารหัสผ่าน
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // 3. ✨ โลจิกคนแรกเป็น SuperAdmin ✨
            // เช็คว่าในตาราง Users มีใครอยู่หรือยัง?
            bool isFirstUser = !await _context.Users.AnyAsync();
            string assignRole = isFirstUser ? "SuperAdmin" : "User";

            // 4. นำข้อมูลลง Database
            var user = new User
            {
                Email = request.Email,
                PasswordHash = passwordHash,
                FirstName = request.FirstName,
                LastName = request.LastName,
                DepartmentId = request.DepartmentId, // <--- เพิ่มตรงนี้
                Role = assignRole // <--- ระบบจะเลือกสิทธิ์ให้อัตโนมัติ
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"สมัครสมาชิกสำเร็จ! (สิทธิ์ของคุณคือ: {assignRole})" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto request)
        {
            // 1. ค้นหา User จาก Email
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            // 2. ถ้าไม่พบ User หรือ รหัสผ่านที่ถอดรหัสแล้วไม่ตรงกัน
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Unauthorized(new { message = "อีเมลหรือรหัสผ่านไม่ถูกต้อง" });

            // 3. รหัสผ่านถูกต้อง -> สร้าง JWT Token
            var token = GenerateJwtToken(user);

            // 4. ส่ง Token กลับไปให้ Vue 3 ใช้งาน
            return Ok(new
            {
                token = token,
                user = new { user.Id, user.Email, user.FirstName, user.LastName, user.Role }
            });
        }

        // ⚠️ API ลับสำหรับ Developer (ใช้เสร็จแล้วค่อยมาลบทิ้งทีหลังได้ครับ)
        [HttpGet("make-me-admin/{email}")]
        public async Task<IActionResult> MakeMeAdmin(string email)
        {
            // ค้นหา User จาก Email
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                return NotFound(new { message = "ไม่พบอีเมลนี้ในระบบ" });

            // จับอัปเกรดสิทธิ์ทันที
            user.Role = "SuperAdmin";
            await _context.SaveChangesAsync();

            return Ok(new { message = $"อัปเกรดบัญชี {email} เป็น SuperAdmin เรียบร้อยแล้ว!" });
        }

        // --- Helper Method สำหรับสร้าง JWT Token ---
        private string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtSettings:Secret"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // ข้อมูลที่จะฝังลงไปใน Token (ห้ามใส่รหัสผ่านเด็ดขาด)
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FirstName", user.FirstName)
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8), // Token มีอายุ 8 ชั่วโมง
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}