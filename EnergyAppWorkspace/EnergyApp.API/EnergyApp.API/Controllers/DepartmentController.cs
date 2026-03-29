using EnergyApp.API.Data;
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

        // This [HttpGet] endpoint resolves the "405 Method Not Allowed" error
        // for all frontend components that need to fetch department data.
        [HttpGet]
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
                // It's good practice to log the exception details here
                return StatusCode(500, new { message = "An error occurred while fetching departments.", error = ex.Message });
            }
        }
    }
}