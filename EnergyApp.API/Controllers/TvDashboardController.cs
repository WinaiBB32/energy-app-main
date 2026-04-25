using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class TvDashboardController : ControllerBase
    {
        private readonly AppDbContext _context;
        public TvDashboardController(AppDbContext context) { _context = context; }

        public class TvDashboardDto
        {
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public bool IsActive { get; set; } = true;
            public int RefreshIntervalSeconds { get; set; } = 60;
            public int SlideDurationSeconds { get; set; } = 10;
            public List<TvDashboardWidgetDto> Widgets { get; set; } = new();
        }

        public class TvDashboardWidgetDto
        {
            public string WidgetType { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public int SortOrder { get; set; }
            public bool IsVisible { get; set; } = true;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var dashboards = await _context.TvDashboards
                .Include(d => d.Widgets)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new
                {
                    d.Id,
                    d.Name,
                    d.Description,
                    d.IsActive,
                    d.RefreshIntervalSeconds,
                    d.SlideDurationSeconds,
                    d.CreatedAt,
                    d.UpdatedAt,
                    WidgetCount = d.Widgets.Count(w => w.IsVisible)
                })
                .ToListAsync();

            return Ok(dashboards);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var dashboard = await _context.TvDashboards
                .Include(d => d.Widgets.OrderBy(w => w.SortOrder))
                .FirstOrDefaultAsync(d => d.Id == id);

            if (dashboard == null) return NotFound(new { message = "ไม่พบ Dashboard" });

            return Ok(new
            {
                dashboard.Id,
                dashboard.Name,
                dashboard.Description,
                dashboard.IsActive,
                dashboard.RefreshIntervalSeconds,
                dashboard.SlideDurationSeconds,
                dashboard.CreatedAt,
                dashboard.UpdatedAt,
                Widgets = dashboard.Widgets.Select(w => new
                {
                    w.Id,
                    w.WidgetType,
                    w.Label,
                    w.SortOrder,
                    w.IsVisible
                })
            });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TvDashboardDto req)
        {
            var dashboard = new TvDashboard
            {
                Name = req.Name,
                Description = req.Description,
                IsActive = req.IsActive,
                RefreshIntervalSeconds = Math.Max(10, req.RefreshIntervalSeconds),
                SlideDurationSeconds = Math.Max(5, req.SlideDurationSeconds),
            };

            for (int i = 0; i < req.Widgets.Count; i++)
            {
                var w = req.Widgets[i];
                dashboard.Widgets.Add(new TvDashboardWidget
                {
                    WidgetType = w.WidgetType,
                    Label = w.Label,
                    SortOrder = w.SortOrder,
                    IsVisible = w.IsVisible
                });
            }

            _context.TvDashboards.Add(dashboard);
            await _context.SaveChangesAsync();

            return Ok(new { message = "สร้าง Dashboard สำเร็จ", id = dashboard.Id });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] TvDashboardDto req)
        {
            var dashboard = await _context.TvDashboards
                .Include(d => d.Widgets)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (dashboard == null) return NotFound(new { message = "ไม่พบ Dashboard" });

            dashboard.Name = req.Name;
            dashboard.Description = req.Description;
            dashboard.IsActive = req.IsActive;
            dashboard.RefreshIntervalSeconds = Math.Max(10, req.RefreshIntervalSeconds);
            dashboard.SlideDurationSeconds = Math.Max(5, req.SlideDurationSeconds);
            dashboard.UpdatedAt = DateTime.UtcNow;

            _context.TvDashboardWidgets.RemoveRange(dashboard.Widgets);
            dashboard.Widgets.Clear();

            foreach (var w in req.Widgets)
            {
                dashboard.Widgets.Add(new TvDashboardWidget
                {
                    WidgetType = w.WidgetType,
                    Label = w.Label,
                    SortOrder = w.SortOrder,
                    IsVisible = w.IsVisible
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "อัปเดต Dashboard สำเร็จ" });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var dashboard = await _context.TvDashboards
                .Include(d => d.Widgets)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (dashboard == null) return NotFound(new { message = "ไม่พบ Dashboard" });

            _context.TvDashboards.Remove(dashboard);
            await _context.SaveChangesAsync();

            return Ok(new { message = "ลบ Dashboard สำเร็จ" });
        }

        /// <summary>
        /// ดึงข้อมูลสรุปสำหรับแสดงบนจอ TV (ไม่ต้องล็อกอิน เพื่อรองรับ kiosk mode)
        /// </summary>
        [AllowAnonymous]
        [HttpGet("{id}/summary")]
        public async Task<IActionResult> GetSummary(Guid id)
        {
            var dashboard = await _context.TvDashboards
                .Include(d => d.Widgets)
                .FirstOrDefaultAsync(d => d.Id == id && d.IsActive);

            if (dashboard == null) return NotFound(new { message = "ไม่พบ Dashboard หรือไม่ได้เปิดใช้งาน" });

            var visibleWidgets = dashboard.Widgets
                .Where(w => w.IsVisible)
                .OrderBy(w => w.SortOrder)
                .ToList();

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            var widgetResults = new List<object>();

            foreach (var widget in visibleWidgets)
            {
                object data;
                try
                {
                    switch (widget.WidgetType)
                    {
                        case "electricity":
                            var elecKwh = await _context.ElectricityRecord
                                .Where(r => r.Type == "PEA_BILL" && r.BillingCycle >= monthStart && r.BillingCycle < monthEnd)
                                .SumAsync(r => (decimal?)r.PeaUnitUsed) ?? 0;
                            var elecAmount = await _context.ElectricityRecord
                                .Where(r => r.Type == "PEA_BILL" && r.BillingCycle >= monthStart && r.BillingCycle < monthEnd)
                                .SumAsync(r => (decimal?)r.PeaAmount) ?? 0;
                            data = new { totalKwh = elecKwh, totalAmount = elecAmount };
                            break;

                        case "solar":
                            var solarWh = await _context.ElectricityRecord
                                .Where(r => r.Type == "SOLAR_PRODUCTION" && r.RecordDate >= monthStart && r.RecordDate < monthEnd)
                                .SumAsync(r => (decimal?)r.ProductionWh) ?? 0;
                            var solarSavings = await _context.ElectricityRecord
                                .Where(r => r.Type == "SOLAR_PRODUCTION" && r.RecordDate >= monthStart && r.RecordDate < monthEnd)
                                .SumAsync(r => (decimal?)r.ToHomeWh) ?? 0;
                            data = new { totalProductionKwh = Math.Round(solarWh / 1000m, 2), totalSelfUseKwh = Math.Round(solarSavings / 1000m, 2) };
                            break;

                        case "water":
                            var waterUnit = await _context.WaterRecords
                                .Where(r => r.BillingCycle >= monthStart && r.BillingCycle < monthEnd)
                                .SumAsync(r => (decimal?)r.WaterUnitUsed) ?? 0;
                            var waterAmount = await _context.WaterRecords
                                .Where(r => r.BillingCycle >= monthStart && r.BillingCycle < monthEnd)
                                .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;
                            data = new { totalUnit = waterUnit, totalAmount = waterAmount };
                            break;

                        case "fuel":
                            var fuelLiters = await _context.FuelRecords
                                .Where(r => r.RefuelDate >= monthStart && r.RefuelDate < monthEnd)
                                .SumAsync(r => (decimal?)r.Liters) ?? 0;
                            var fuelAmount = await _context.FuelRecords
                                .Where(r => r.RefuelDate >= monthStart && r.RefuelDate < monthEnd)
                                .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;
                            data = new { totalLiters = fuelLiters, totalAmount = fuelAmount };
                            break;

                        case "maintenance":
                            var pendingStatuses = new[] { RepairRequestStatus.New, RepairRequestStatus.Assigned };
                            var activeStatuses = new[] {
                            RepairRequestStatus.InProgress,
                            RepairRequestStatus.NeedSupervisorReview,
                            RepairRequestStatus.ReturnedToTechnician,
                            RepairRequestStatus.WaitingDepartmentExternalProcurement,
                            RepairRequestStatus.WaitingCentralExternalProcurement,
                            RepairRequestStatus.ExternalScheduled,
                            RepairRequestStatus.ExternalInProgress
                        };
                            var pendingCount = await _context.ServiceRequests
                                .CountAsync(r => pendingStatuses.Contains(r.Status));
                            var inProgressCount = await _context.ServiceRequests
                                .CountAsync(r => activeStatuses.Contains(r.Status));
                            data = new { pendingCount, inProgressCount };
                            break;

                        case "meeting":
                            var meetingUsage = await _context.MeetingRecords
                                .Where(r => r.RecordMonth >= monthStart && r.RecordMonth < monthEnd)
                                .SumAsync(r => (int?)r.UsageCount) ?? 0;
                            var roomCount = await _context.MeetingRecords
                                .Where(r => r.RecordMonth >= monthStart && r.RecordMonth < monthEnd)
                                .Select(r => r.RoomId)
                                .Distinct()
                                .CountAsync();
                            data = new { totalUsageCount = meetingUsage, roomCount };
                            break;

                        case "postal":
                            var incomingTotal = await _context.PostalRecords
                                .Where(r => r.RecordMonth >= monthStart && r.RecordMonth < monthEnd)
                                .SumAsync(r => (int?)r.IncomingTotalMail) ?? 0;
                            var outgoingTotal = await _context.PostalRecords
                                .Where(r => r.RecordMonth >= monthStart && r.RecordMonth < monthEnd)
                                .SumAsync(r => r.NormalMail + r.RegisteredMail + r.EmsMail);
                            data = new { incomingTotal, outgoingTotal };
                            break;

                        case "saraban":
                            var receivedCount = await _context.SarabanStats
                                .Where(r => r.RecordMonth >= monthStart && r.RecordMonth < monthEnd)
                                .SumAsync(r => (int?)r.ReceivedCount) ?? 0;
                            var forwardedCount = await _context.SarabanStats
                                .Where(r => r.RecordMonth >= monthStart && r.RecordMonth < monthEnd)
                                .SumAsync(r => (int?)r.ForwardedCount) ?? 0;
                            data = new { receivedCount, forwardedCount };
                            break;

                        default:
                            data = new { };
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // widget query ล้มเหลว (เช่น ตารางยังไม่มีใน dev) → คืนค่าว่าง ไม่หยุด widget อื่น
                    data = new { error = ex.Message };
                }

                widgetResults.Add(new
                {
                    widget.WidgetType,
                    widget.Label,
                    widget.SortOrder,
                    Data = data
                });
            }

            return Ok(new
            {
                dashboard.Id,
                dashboard.Name,
                dashboard.Description,
                dashboard.RefreshIntervalSeconds,
                dashboard.SlideDurationSeconds,
                GeneratedAt = now,
                Widgets = widgetResults
            });
        }
    }
}
