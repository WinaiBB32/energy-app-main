using EnergyApp.API.Data;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class SparePartController : ControllerBase
    {
        private const string MaintenanceTechnicianPermission = "maintenance:technician";
        private const string MaintenanceCentralExternalPermission = "maintenance:adminbuilding:central";

        private readonly AppDbContext _context;

        public SparePartController(AppDbContext context)
        {
            _context = context;
        }

        public class SparePartCreateDto
        {
            public string PartCode { get; set; } = string.Empty;
            public string PartName { get; set; } = string.Empty;
            public string Unit { get; set; } = "pcs";
            public decimal ReorderPoint { get; set; }
        }

        public class StockReceiveDto
        {
            public Guid SparePartId { get; set; }
            public decimal Quantity { get; set; }
            public string Note { get; set; } = string.Empty;
        }

        public class IssueRequestItemDto
        {
            public Guid SparePartId { get; set; }
            public decimal QtyRequested { get; set; }
        }

        public class IssueRequestCreateDto
        {
            public Guid? ServiceRequestId { get; set; }
            public string Note { get; set; } = string.Empty;
            public List<IssueRequestItemDto> Items { get; set; } = new();
        }

        public class IssueRequestApproveItemDto
        {
            public Guid SparePartId { get; set; }
            public decimal QtyApproved { get; set; }
        }

        public class IssueRequestApproveDto
        {
            public string Note { get; set; } = string.Empty;
            public List<IssueRequestApproveItemDto> Items { get; set; } = new();
        }

        public class IssueRequestRejectDto
        {
            public string Reason { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetParts([FromQuery] bool activeOnly = true)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);
            if (!CanViewInventoryAction(actor, scope))
                return Forbid();

            var query = _context.SpareParts.AsQueryable();
            if (activeOnly)
                query = query.Where(x => x.IsActive);

            var items = await query
                .OrderBy(x => x.PartCode)
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePart([FromBody] SparePartCreateDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);

            if (!CanStockAdminAction(actor, scope))
                return Forbid();

            if (string.IsNullOrWhiteSpace(req.PartCode) || string.IsNullOrWhiteSpace(req.PartName))
                return BadRequest(new { message = "กรุณาระบุรหัสและชื่ออะไหล่" });

            var exists = await _context.SpareParts.AnyAsync(x => x.PartCode == req.PartCode);
            if (exists)
                return BadRequest(new { message = "รหัสอะไหล่นี้มีอยู่แล้ว" });

            var part = new SparePart
            {
                PartCode = req.PartCode.Trim(),
                PartName = req.PartName.Trim(),
                Unit = string.IsNullOrWhiteSpace(req.Unit) ? "pcs" : req.Unit.Trim(),
                ReorderPoint = req.ReorderPoint,
                QuantityOnHand = 0
            };

            _context.SpareParts.Add(part);
            await _context.SaveChangesAsync();

            return Ok(new { message = "เพิ่มรายการอะไหล่สำเร็จ", id = part.Id });
        }

        [HttpPost("receive")]
        public async Task<IActionResult> ReceiveStock([FromBody] StockReceiveDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);

            if (!CanStockAdminAction(actor, scope))
                return Forbid();

            if (req.Quantity <= 0)
                return BadRequest(new { message = "จำนวนรับเข้าต้องมากกว่า 0" });

            var part = await _context.SpareParts.FindAsync(req.SparePartId);
            if (part == null) return NotFound();

            part.QuantityOnHand += req.Quantity;
            part.UpdatedAt = DateTime.UtcNow;

            _context.SparePartTransactions.Add(new SparePartTransaction
            {
                SparePartId = part.Id,
                TxType = "receive",
                Quantity = req.Quantity,
                ReferenceType = "manual_receive",
                ReferenceId = string.Empty,
                RequestedByUid = actor.Uid,
                ApprovedByUid = actor.Uid,
                Note = req.Note
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = "รับอะไหล่เข้าคลังสำเร็จ", quantityOnHand = part.QuantityOnHand });
        }

        [HttpGet("issue-requests")]
        public async Task<IActionResult> GetIssueRequests([FromQuery] string? status)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);
            if (!CanViewInventoryAction(actor, scope))
                return Forbid();

            var query = _context.SparePartIssueRequests
                .Include(x => x.Items)
                .AsQueryable();

            if (!CanStockAdminAction(actor, scope))
                query = query.Where(x => x.RequestedByUid == actor.Uid);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(x => x.Status == status);

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost("issue-requests")]
        public async Task<IActionResult> CreateIssueRequest([FromBody] IssueRequestCreateDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);

            if (!CanIssueRequestAction(actor, scope))
                return Forbid();

            if (req.Items.Count == 0)
                return BadRequest(new { message = "กรุณาระบุรายการอะไหล่ที่ต้องการเบิก" });

            var request = new SparePartIssueRequest
            {
                RequestNo = BuildIssueRequestNo(),
                ServiceRequestId = req.ServiceRequestId,
                RequestedByUid = actor.Uid,
                RequestedByName = actor.DisplayName,
                Note = req.Note,
                Status = UserStatus.Pending
            };

            foreach (var item in req.Items)
            {
                request.Items.Add(new SparePartIssueRequestItem
                {
                    SparePartId = item.SparePartId,
                    QtyRequested = item.QtyRequested,
                    QtyApproved = 0
                });
            }

            _context.SparePartIssueRequests.Add(request);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ส่งคำขอเบิกอะไหล่สำเร็จ", id = request.Id, requestNo = request.RequestNo });
        }

        [HttpPost("issue-requests/{id}/approve")]
        public async Task<IActionResult> ApproveIssueRequest(Guid id, [FromBody] IssueRequestApproveDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);

            if (!CanStockAdminAction(actor, scope))
                return Forbid();

            var issueRequest = await _context.SparePartIssueRequests
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (issueRequest == null) return NotFound();
            if (issueRequest.Status != UserStatus.Pending)
                return BadRequest(new { message = "คำขอนี้ไม่ได้อยู่ในสถานะรออนุมัติ" });

            foreach (var approvedItem in req.Items)
            {
                var line = issueRequest.Items.FirstOrDefault(x => x.SparePartId == approvedItem.SparePartId);
                if (line == null) continue;

                var part = await _context.SpareParts.FindAsync(approvedItem.SparePartId);
                if (part == null)
                    return BadRequest(new { message = "พบอะไหล่ในคำขอที่ไม่มีอยู่ในคลัง" });

                if (approvedItem.QtyApproved < 0)
                    return BadRequest(new { message = "จำนวนอนุมัติต้องไม่ติดลบ" });

                if (approvedItem.QtyApproved > part.QuantityOnHand)
                    return BadRequest(new { message = $"สต็อกไม่พอสำหรับอะไหล่ {part.PartCode}" });

                line.QtyApproved = approvedItem.QtyApproved;
                part.QuantityOnHand -= approvedItem.QtyApproved;
                part.UpdatedAt = DateTime.UtcNow;

                if (approvedItem.QtyApproved > 0)
                {
                    _context.SparePartTransactions.Add(new SparePartTransaction
                    {
                        SparePartId = part.Id,
                        TxType = "issue",
                        Quantity = approvedItem.QtyApproved,
                        ReferenceType = "issue_request",
                        ReferenceId = issueRequest.Id.ToString(),
                        RequestedByUid = issueRequest.RequestedByUid,
                        ApprovedByUid = actor.Uid,
                        Note = req.Note
                    });
                }
            }

            issueRequest.Status = UserStatus.Approved;
            issueRequest.ApprovedByUid = actor.Uid;
            issueRequest.ApprovedByName = actor.DisplayName;
            issueRequest.ApprovedAt = DateTime.UtcNow;
            issueRequest.Note = string.IsNullOrWhiteSpace(req.Note) ? issueRequest.Note : req.Note;

            await _context.SaveChangesAsync();
            return Ok(new { message = "อนุมัติเบิกอะไหล่สำเร็จ" });
        }

        [HttpPost("issue-requests/{id}/reject")]
        public async Task<IActionResult> RejectIssueRequest(Guid id, [FromBody] IssueRequestRejectDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);

            if (!CanStockAdminAction(actor, scope))
                return Forbid();

            var issueRequest = await _context.SparePartIssueRequests.FindAsync(id);
            if (issueRequest == null) return NotFound();
            if (issueRequest.Status != UserStatus.Pending)
                return BadRequest(new { message = "คำขอนี้ไม่ได้อยู่ในสถานะรออนุมัติ" });

            issueRequest.Status = UserStatus.Rejected;
            issueRequest.ApprovedByUid = actor.Uid;
            issueRequest.ApprovedByName = actor.DisplayName;
            issueRequest.RejectReason = req.Reason;
            issueRequest.ApprovedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "ปฏิเสธคำขอเบิกอะไหล่แล้ว" });
        }

        private static string BuildIssueRequestNo()
        {
            return $"SR-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        private static bool CanAccess(string actorRole, params string[] allowedRoles)
        {
            var normalized = actorRole.Trim().ToLowerInvariant();
            return allowedRoles.Contains(normalized);
        }

        private sealed class PermissionScope
        {
            public HashSet<string> AdminSystems { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private static bool HasPermission(PermissionScope scope, string permission)
            => scope.AdminSystems.Contains(permission);

        private static bool CanStockAdminAction(ActorContext actor, PermissionScope scope)
            =>
                CanAccess(actor.Role, "superadmin", "adminbuilding") ||
                HasPermission(scope, MaintenanceCentralExternalPermission);

        private static bool CanViewInventoryAction(ActorContext actor, PermissionScope scope)
            =>
                HasPermission(scope, MaintenanceTechnicianPermission) ||
                CanStockAdminAction(actor, scope);

        private static bool CanIssueRequestAction(ActorContext actor, PermissionScope scope)
            =>
                CanAccess(actor.Role, "superadmin", "technician") ||
                HasPermission(scope, MaintenanceTechnicianPermission) ||
                CanStockAdminAction(actor, scope);

        private async Task<PermissionScope> GetPermissionScopeAsync(ActorContext actor)
        {
            var scope = new PermissionScope();

            if (!Guid.TryParse(actor.Uid, out var actorGuid))
                return scope;

            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == actorGuid)
                .Select(u => new { u.AdminSystems })
                .FirstOrDefaultAsync();

            if (user == null)
                return scope;

            scope.AdminSystems = (user.AdminSystems ?? new List<string>())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return scope;
        }

        private ActorContext? GetActorContext()
        {
            var uid = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? string.Empty;

            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var firstName = User.FindFirstValue("FirstName") ?? string.Empty;
            var email = User.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? User.FindFirstValue(ClaimTypes.Email)
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(role))
                return null;

            var displayName = string.IsNullOrWhiteSpace(firstName) ? email : firstName;
            return new ActorContext(uid, role, displayName);
        }

        private sealed record ActorContext(string Uid, string Role, string DisplayName);
    }
}
