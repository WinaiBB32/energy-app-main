using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class FuelReceiptController : ControllerBase
    {
        private readonly AppDbContext _context;
        public FuelReceiptController(AppDbContext context) { _context = context; }

        public class FuelReceiptDto
        {
            public Guid? DepartmentId { get; set; }
            public string EntriesJson { get; set; } = "[]";
            public decimal TotalAmount { get; set; }
            public string DeclarerName { get; set; } = string.Empty;
            public string DeclarerPosition { get; set; } = string.Empty;
            public string DeclarerDept { get; set; } = string.Empty;
            public string Note { get; set; } = string.Empty;
            public string RecordedByName { get; set; } = string.Empty;
            public string RecordedByUid { get; set; } = string.Empty;
        }

        // --- FuelReceiptEntryDto structure ---
        // [
        //   {
        //     "day": 1,
        //     "month": "เมษายน",
        //     "year": 2567,
        //     "detail": "...",
        //     "receiptNo": "...",
        //     "bookNo": "...",
        //     "amount": 123.45,
        //     "driverName": "...",
        //     "branch": "...",           // สาขา
        //     "liters": 12.345            // ปริมาณลิตร (ทศนิยม 3)
        //   }
        // ]

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] Guid? departmentId,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var query = _context.FuelReceipts.AsQueryable();

            if (departmentId.HasValue)
                query = query.Where(r => r.DepartmentId == departmentId);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return Ok(new { total, items });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var item = await _context.FuelReceipts.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FuelReceiptDto req)
        {
            // Validate EntriesJson structure
            var entries = System.Text.Json.JsonSerializer.Deserialize<List<DTOs.Office.FuelReceiptEntryDto>>(req.EntriesJson);
            if (entries == null || entries.Count == 0)
                return BadRequest("ต้องระบุข้อมูลรายการน้ำมันอย่างน้อย 1 รายการ");
            // Optionally: Validate branch and liters
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Branch))
                    return BadRequest("กรุณาระบุสาขา");
                if (entry.Liters <= 0)
                    return BadRequest("กรุณาระบุปริมาณลิตรให้ถูกต้อง (> 0)");
            }
            var record = new FuelReceipt
            {
                DepartmentId = req.DepartmentId,
                EntriesJson = req.EntriesJson,
                TotalAmount = req.TotalAmount,
                DeclarerName = req.DeclarerName,
                DeclarerPosition = req.DeclarerPosition,
                DeclarerDept = req.DeclarerDept,
                Note = req.Note,
                RecordedByName = req.RecordedByName,
                RecordedByUid = req.RecordedByUid
            };
            _context.FuelReceipts.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "บันทึกใบเสร็จเชื้อเพลิงสำเร็จ", id = record.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] FuelReceiptDto req)
        {
            var record = await _context.FuelReceipts.FindAsync(id);
            if (record == null) return NotFound();

            // Validate EntriesJson structure
            var entries = System.Text.Json.JsonSerializer.Deserialize<List<DTOs.Office.FuelReceiptEntryDto>>(req.EntriesJson);
            if (entries == null || entries.Count == 0)
                return BadRequest("ต้องระบุข้อมูลรายการน้ำมันอย่างน้อย 1 รายการ");
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Branch))
                    return BadRequest("กรุณาระบุสาขา");
                if (entry.Liters <= 0)
                    return BadRequest("กรุณาระบุปริมาณลิตรให้ถูกต้อง (> 0)");
            }

            record.DepartmentId = req.DepartmentId;
            record.EntriesJson = req.EntriesJson;
            record.TotalAmount = req.TotalAmount;
            record.DeclarerName = req.DeclarerName;
            record.DeclarerPosition = req.DeclarerPosition;
            record.DeclarerDept = req.DeclarerDept;
            record.Note = req.Note;
            record.RecordedByName = req.RecordedByName;
            record.RecordedByUid = req.RecordedByUid;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขใบเสร็จเชื้อเพลิงสำเร็จ" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.FuelReceipts.FindAsync(id);
            if (record == null) return NotFound();
            _context.FuelReceipts.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }
    }
}
