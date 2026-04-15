using EnergyApp.API.Data;
using EnergyApp.API.Models.Office;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers.Office
{
    [Authorize]
    [ApiController]
    [Route("api/v1/office/vehicle-departments")]
    public class VehicleDepartmentController : ControllerBase
    {
        private readonly AppDbContext _context;

        public VehicleDepartmentController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VehicleDepartment>>> GetAll()
        {
            var list = await _context.VehicleDepartments
                .OrderBy(d => d.Name)
                .ToListAsync();
            return Ok(list);
        }

        [HttpPost]
        public async Task<ActionResult<VehicleDepartment>> Create([FromBody] VehicleMasterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "กรุณากรอกชื่อหน่วยงาน" });

            var item = new VehicleDepartment { Name = dto.Name.Trim() };
            _context.VehicleDepartments.Add(item);
            await _context.SaveChangesAsync();
            return Ok(item);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<VehicleDepartment>> Update(int id, [FromBody] VehicleMasterDto dto)
        {
            var item = await _context.VehicleDepartments.FindAsync(id);
            if (item == null) return NotFound();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "กรุณากรอกชื่อหน่วยงาน" });

            item.Name = dto.Name.Trim();
            await _context.SaveChangesAsync();
            return Ok(item);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.VehicleDepartments.FindAsync(id);
            if (item == null) return NotFound();

            _context.VehicleDepartments.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public class VehicleMasterDto
    {
        public string Name { get; set; } = string.Empty;
    }
}
