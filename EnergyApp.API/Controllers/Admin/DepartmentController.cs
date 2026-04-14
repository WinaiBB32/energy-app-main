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

        [HttpGet]
        [HttpGet("all")]
        public async Task<IActionResult> GetDepartments()
        {
            try
            {
                var departments = await _context.Departments
                    .OrderBy(d => d.Code)
                    .ToListAsync();

                return Ok(departments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching departments.", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateDepartment([FromBody] DepartmentDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "กรุณากรอกชื่อหน่วยงาน" });

            var department = new Department
            {
                Name = request.Name,
                Code = request.Name
            };

            _context.Departments.Add(department);
            await _context.SaveChangesAsync();

            return Ok(department);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDepartment(Guid id, [FromBody] DepartmentDto request)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null)
                return NotFound(new { message = "ไม่พบหน่วยงาน" });

            department.Name = request.Name;
            await _context.SaveChangesAsync();

            return Ok(department);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDepartment(Guid id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null)
                return NotFound(new { message = "ไม่พบหน่วยงาน" });

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();

            return Ok(new { message = "ลบหน่วยงานสำเร็จ" });
        }
    }

    public class DepartmentDto
    {
        public string Name { get; set; } = string.Empty;
    }
}