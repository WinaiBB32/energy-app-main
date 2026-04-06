using EnergyApp.API.Data;
using EnergyApp.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class SystemMonitorController : ControllerBase
    {
        private static readonly DateTime StartedAtUtc = DateTime.UtcNow;
        private readonly AppDbContext _context;
        private readonly IHostEnvironment _environment;
        private readonly ISystemErrorLogStore _errorLogStore;

        public SystemMonitorController(
            AppDbContext context,
            IHostEnvironment environment,
            ISystemErrorLogStore errorLogStore)
        {
            _context = context;
            _environment = environment;
            _errorLogStore = errorLogStore;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var process = Process.GetCurrentProcess();
            var dbStopwatch = Stopwatch.StartNew();

            bool dbHealthy;
            string? dbError = null;
            try
            {
                dbHealthy = await _context.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                dbHealthy = false;
                dbError = ex.Message;
            }
            finally
            {
                dbStopwatch.Stop();
            }

            var uptimeSeconds = Math.Max(0, (long)(DateTime.UtcNow - StartedAtUtc).TotalSeconds);

            return Ok(new
            {
                generatedAtUtc = DateTime.UtcNow,
                api = new
                {
                    status = "healthy",
                    environment = _environment.EnvironmentName,
                    machineName = Environment.MachineName,
                    processId = process.Id,
                    workingSetMb = Math.Round(process.WorkingSet64 / 1024d / 1024d, 2),
                    threadCount = process.Threads.Count,
                    runtime = Environment.Version.ToString(),
                    uptimeSeconds
                },
                database = new
                {
                    status = dbHealthy ? "healthy" : "unhealthy",
                    canConnect = dbHealthy,
                    responseTimeMs = dbStopwatch.ElapsedMilliseconds,
                    provider = _context.Database.ProviderName,
                    error = dbError
                },
                frontend = new
                {
                    note = "Frontend diagnostics should be collected in browser and sent to this dashboard."
                },
                errors = new
                {
                    count24h = _errorLogStore.CountSince(DateTime.UtcNow.AddHours(-24)),
                    recent = _errorLogStore.GetRecent(20)
                }
            });
        }
    }
}
