using EnergyApp.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        // คลาสรับข้อมูลจากหน้าเว็บ
        public class UpdateUserDto
        {
            public string DisplayName { get; set; } = string.Empty;
            public Guid? DepartmentId { get; set; }
            public string Role { get; set; } = Roles.User;
            public string Status { get; set; } = UserStatus.Pending;
            public List<string> AccessibleSystems { get; set; } = new();
            public List<string> AdminSystems { get; set; } = new();
        }

        // 1. ดึงรายชื่อ User ทั้งหมด (SuperAdmin เท่านั้น) — รองรับ Pagination + Search
        [Authorize(Roles = "SuperAdmin")]
        [HttpGet]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            pageSize = Math.Clamp(pageSize, 1, 200);
            page = Math.Max(1, page);

            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u =>
                    u.Email.ToLower().Contains(s) ||
                    (u.FirstName + " " + u.LastName).ToLower().Contains(s));
            }

            var total = await query.CountAsync();
            var users = await query
                .OrderBy(u => u.Status == UserStatus.Pending ? 0 : 1)
                .ThenBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    id = u.Id,
                    email = u.Email,
                    displayName = u.FirstName + " " + u.LastName,
                    departmentId = u.DepartmentId,
                    role = u.Role,
                    status = u.Status,
                    accessibleSystems = u.AccessibleSystems,
                    adminSystems = u.AdminSystems
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items = users });
        }

        // 2. อัปเดตข้อมูลและสิทธิ์ของ User (SuperAdmin เท่านั้น)
        [Authorize(Roles = "SuperAdmin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserDto request)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound(new { message = "ไม่พบผู้ใช้งานในระบบ" });

            // แยกชื่อ-นามสกุลกลับลงตาราง
            if (!string.IsNullOrWhiteSpace(request.DisplayName))
            {
                var parts = request.DisplayName.Trim().Split(' ', 2);
                user.FirstName = parts[0];
                user.LastName = parts.Length > 1 ? parts[1] : "";
            }

            user.DepartmentId = request.DepartmentId;
            user.Role = request.Role;
            user.Status = request.Status;
            user.AccessibleSystems = request.AccessibleSystems ?? new List<string>();
            user.AdminSystems = request.AdminSystems ?? new List<string>();

            await _context.SaveChangesAsync();
            return Ok(new { message = "อัปเดตข้อมูลสำเร็จ" });
        }

        // 3. รีเซ็ตรหัสผ่าน (SuperAdmin เท่านั้น)
        public class ResetPasswordDto
        {
            public string NewPassword { get; set; } = string.Empty;
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordDto req)
        {
            if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
                return BadRequest(new { message = "รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร" });

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound(new { message = "ไม่พบผู้ใช้งาน" });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "รีเซ็ตรหัสผ่านสำเร็จ" });
        }

        // 4. ลบ User (SuperAdmin เท่านั้น)
        [Authorize(Roles = "SuperAdmin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound(new { message = "ไม่พบผู้ใช้งาน" });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "ลบผู้ใช้งานสำเร็จ" });
        }
    }
}