using EnergyApp.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
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
            public string Role { get; set; } = "User";
            public string Status { get; set; } = "pending";
            public List<string> AccessibleSystems { get; set; } = new();
            public List<string> AdminSystems { get; set; } = new();
        }

        // 1. ดึงรายชื่อ User ทั้งหมด
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    id = u.Id,
                    email = u.Email,
                    displayName = u.FirstName + " " + u.LastName, // รวมชื่อ-สกุลให้ตรงกับ Vue
                    departmentId = u.DepartmentId,
                    role = u.Role,
                    status = u.Status,
                    accessibleSystems = u.AccessibleSystems,
                    adminSystems = u.AdminSystems
                })
                .OrderBy(u => u.status == "pending" ? 0 : 1) // ให้บัญชีรออนุมัติขึ้นก่อน
                .ThenBy(u => u.email)
                .ToListAsync();

            return Ok(users);
        }

        // 2. อัปเดตข้อมูลและสิทธิ์ของ User
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

        // 3. ลบ User
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