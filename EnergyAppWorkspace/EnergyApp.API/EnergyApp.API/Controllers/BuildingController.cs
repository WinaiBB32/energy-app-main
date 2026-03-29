using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;



namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class BuildingController : ControllerBase
    {
        private readonly AppDbContext _context;
        public BuildingController(AppDbContext context) {  _context = context; }

        public class BuildingDto
        {
            public string Name { get; set; } = string.Empty;
            public string Description {  get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Get() {
            var items = await _context.Buildings
                .Select(b => new { id = b.Id, name = b.Name, description = b.Location })
                .OrderBy(b => b.name).ToListAsync();
            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BuildingDto req)
        {
            var b = new Building { Name = req.Name, Location = req.Description };
            _context.Buildings.Add(b);
            await _context.SaveChangesAsync();
            return Ok(new { message = "เพิ่มอาคารสำเร็จ" });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] BuildingDto req)
        {
            var b = await _context.Buildings.FindAsync(id);
            if (b == null) return NotFound();
            b.Name = req.Name; b.Location = req.Description;
            await _context.SaveChangesAsync();
            return Ok(new { massage = "แก้ไขสำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var b = await _context.Buildings.FindAsync(id);
            if (b == null) return NotFound();
            _context.Buildings.Remove(b);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }

    }
}
