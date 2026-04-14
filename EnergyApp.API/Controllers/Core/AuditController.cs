using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuditController(AppDbContext context)
        {
            _context = context;
        }

        // คลาสรับข้อมูลเวลาส่งมาจาก Vue
        public class CreateAuditDto
        {
            public string Uid { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;
            public string Module { get; set; } = string.Empty;
            public string Detail { get; set; } = string.Empty;
            public string IpAddress { get; set; } = string.Empty;
            public string Browser { get; set; } = string.Empty;
            public string UserAgent { get; set; } = string.Empty;
        }

        // 1. POST: รับข้อมูลบันทึก Log 
        [HttpPost]
        public async Task<IActionResult> LogAction([FromBody] CreateAuditDto request)
        {
            var audit = new AuditLog
            {
                Uid = request.Uid ?? "Unknown",
                DisplayName = request.DisplayName ?? "-",
                Email = request.Email ?? "-",
                Role = request.Role ?? "user",
                Action = request.Action ?? "UNKNOWN",
                Module = request.Module ?? "-",
                Detail = request.Detail ?? "",
                IpAddress = request.IpAddress ?? "0.0.0.0",
                Browser = request.Browser ?? "Unknown",
                UserAgent = request.UserAgent ?? "-",
                CreatedAt = DateTime.UtcNow // บันทึกเวลาฝั่งเซิร์ฟเวอร์
            };

            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();

            return Ok(new { message = "บันทึก Log สำเร็จ" });
        }

        // 2. GET: สำหรับดึง Log ไปแสดงที่หน้า AuditLog.vue 
        [HttpGet]
        public async Task<IActionResult> GetLogs()
        {
            var logs = await _context.AuditLogs
                .OrderByDescending(a => a.CreatedAt)
                .Take(1000) // ดึงมาแค่ 1000 รายการล่าสุด ป้องกันโหลดช้า
                .ToListAsync();

            return Ok(logs);
        }
    }
}