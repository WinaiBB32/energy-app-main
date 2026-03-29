using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class DepartmentController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DepartmentController(AppDbContext context)
        {
            _context = context;
        }

        // คลาสสำหรับรับข้อมูลจาก Vue
        public class DepartmentDto
        {
            public string Name { get; set; } = string.Empty;
        }

        // 1. GET: ดึงข้อมูลหน่วยงานทั้งหมด
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var departments = await _context.Departments
                .Select(d => new { d.Id, d.Name })
                .OrderBy(d => d.Name)
                .ToListAsync();

            return Ok(departments);
        }

        // 2. POST: สร้างหน่วยงานใหม่
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DepartmentDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "กรุณาระบุชื่อหน่วยงาน" });

            var dept = new Department
            {
                Name = request.Name,
                Code = "N/A" // กำหนดค่าเริ่มต้นให้กับฟิลด์ Code
            };

            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            return Ok(new { message = "เพิ่มหน่วยงานสำเร็จ", data = new { dept.Id, dept.Name } });
        }

        // 3. PUT: แก้ไขหน่วยงาน
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] DepartmentDto request)
        {
            var dept = await _context.Departments.FindAsync(id);
            if (dept == null) return NotFound(new { message = "ไม่พบหน่วยงานที่ต้องการแก้ไข" });

            dept.Name = request.Name;
            await _context.SaveChangesAsync();

            return Ok(new { message = "แก้ไขข้อมูลหน่วยงานสำเร็จ" });
        }

        // 4. DELETE: ลบหน่วยงาน
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var dept = await _context.Departments.FindAsync(id);
            if (dept == null) return NotFound(new { message = "ไม่พบหน่วยงานที่ต้องการลบ" });

            // เช็คก่อนว่ามีพนักงานในหน่วยงานนี้ไหม (เพื่อป้องกันข้อมูลพัง)
            var hasUsers = await _context.Users.AnyAsync(u => u.DepartmentId == id);
            if (hasUsers)
                return BadRequest(new { message = "ไม่สามารถลบได้ เนื่องจากมีผู้ใช้งานสังกัดอยู่ในหน่วยงานนี้" });

            _context.Departments.Remove(dept);
            await _context.SaveChangesAsync();

            return Ok(new { message = "ลบหน่วยงานสำเร็จ" });
        }
    }
}