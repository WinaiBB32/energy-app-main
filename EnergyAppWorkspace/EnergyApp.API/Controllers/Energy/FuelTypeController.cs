using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class FuelTypeController : ControllerBase
    {
        private readonly AppDbContext _context;
        public FuelTypeController(AppDbContext context) { _context = context; }

        public class FuelTypeDto
        {
            public string Name { get; set; } = string.Empty;
            public string Severity { get; set; } = "secondary";
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return Ok(await _context.FuelTypes.OrderBy(f => f.CreatedAt).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FuelTypeDto req)
        {
            var f = new FuelType { Name = req.Name, Severity = req.Severity };
            _context.FuelTypes.Add(f);
            await _context.SaveChangesAsync();
            return Ok(new { message = "เพิ่มสำเร็จ" });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] FuelTypeDto req)
        {
            var f = await _context.FuelTypes.FindAsync(id);
            if (f == null) return NotFound();
            f.Name = req.Name; f.Severity = req.Severity;
            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขสำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var f = await _context.FuelTypes.FindAsync(id);
            if (f == null) return NotFound();
            _context.FuelTypes.Remove(f);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
    }
}