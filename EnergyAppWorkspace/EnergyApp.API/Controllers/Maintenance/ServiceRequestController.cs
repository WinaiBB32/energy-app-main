using EnergyApp.API.Data;
using EnergyApp.API.Hubs;
using EnergyApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EnergyApp.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class ServiceRequestController : ControllerBase
    {
        private const string MaintenanceTechnicianPermission = "maintenance:technician";
        private const string MaintenanceSupervisorPermission = "maintenance:supervisor";
        private const string MaintenanceDepartmentExternalPermission = "maintenance:adminbuilding";
        private const string MaintenanceCentralExternalPermission = "maintenance:adminbuilding:central";

        private readonly AppDbContext _context;
        private readonly IHubContext<MaintenanceHub> _hubContext;

        public ServiceRequestController(AppDbContext context, IHubContext<MaintenanceHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public class ServiceRequestDto
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Priority { get; set; } = "medium";
            public string Status { get; set; } = RepairRequestStatus.New;
            public string BuildingName { get; set; } = string.Empty;
            public string LocationDetail { get; set; } = string.Empty;
            public string AssetNumber { get; set; } = string.Empty;
            public bool IsCentralAsset { get; set; }
            public string Extension { get; set; } = string.Empty;
            public string RequesterName { get; set; } = string.Empty;
            public string RequesterEmail { get; set; } = string.Empty;
            public string RequesterUid { get; set; } = string.Empty;
            public string RequesterDepartmentCode { get; set; } = string.Empty;
            public string RequesterDepartmentName { get; set; } = string.Empty;
            public string AssignedTo { get; set; } = string.Empty;
            public string TechnicianUid { get; set; } = string.Empty;
            public string TechnicianName { get; set; } = string.Empty;
            public string SupervisorUid { get; set; } = string.Empty;
            public string SupervisorName { get; set; } = string.Empty;
            public string AdminOfficerUid { get; set; } = string.Empty;
            public string AdminOfficerName { get; set; } = string.Empty;
            public string TechnicianDiagnosis { get; set; } = string.Empty;
            public string TechnicianAction { get; set; } = string.Empty;
            public string EscalationReason { get; set; } = string.Empty;
            public bool? SupervisorCanRepairInHouse { get; set; }
            public string SupervisorReason { get; set; } = string.Empty;
            public string SupervisorRepairPlan { get; set; } = string.Empty;
            public string SupervisorExternalAdvice { get; set; } = string.Empty;
            public string ExternalVendorName { get; set; } = string.Empty;
            public DateTime? ExternalScheduledAt { get; set; }
            public DateTime? ExternalCompletedAt { get; set; }
            public string ExternalResult { get; set; } = string.Empty;
            public string Note { get; set; } = string.Empty;
        }

        public class AssignTechnicianDto
        {
            public string TechnicianUid { get; set; } = string.Empty;
            public string TechnicianName { get; set; } = string.Empty;
        }

        public class TechnicianCompleteDto
        {
            public string Diagnosis { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;
            public string Note { get; set; } = string.Empty;
        }

        public class TechnicianEscalateDto
        {
            public string EscalationReason { get; set; } = string.Empty;
            public string Diagnosis { get; set; } = string.Empty;
        }

        public class SupervisorReviewDto
        {
            public bool CanRepairInHouse { get; set; }
            public string Reason { get; set; } = string.Empty;
            public string RepairPlan { get; set; } = string.Empty;
            public string ExternalAdvice { get; set; } = string.Empty;
        }

        public class ExternalProgressDto
        {
            public string VendorName { get; set; } = string.Empty;
            public DateTime? ScheduledAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public string Result { get; set; } = string.Empty;
            public bool CloseAfterComplete { get; set; }
        }

        public class RequesterEditDto
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string BuildingName { get; set; } = string.Empty;
            public string LocationDetail { get; set; } = string.Empty;
            public string Extension { get; set; } = string.Empty;
        }

        public class RequesterExternalDto
        {
            public string Reason { get; set; } = string.Empty;
        }

        public class RequesterCancelDto
        {
            public string Reason { get; set; } = string.Empty;
        }

        public class CloseRequestDto
        {
            public string Note { get; set; } = string.Empty;
        }

        public class TimelineReadDto
        {
            public DateTime? CreatedAt { get; set; }
        }

        public class TechnicianStartDto
        {
            public string? Note { get; set; }
        }

        public class ChatMessageDto
        {
            public string Text { get; set; } = string.Empty;
            public string SenderName { get; set; } = string.Empty;
            public string SenderEmail { get; set; } = string.Empty;
            public string SenderId { get; set; } = string.Empty;
            public string SenderRole { get; set; } = string.Empty;
        }

        public class NotificationItemDto
        {
            public Guid RequestId { get; set; }
            public string WorkOrderNo { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public bool IsUnread { get; set; }
            public DateTime CreatedAt { get; set; }
            public string Path { get; set; } = string.Empty;
        }

        public class RealtimeNotificationDto
        {
            public Guid RequestId { get; set; }
            public string WorkOrderNo { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public bool IsUnread { get; set; }
            public DateTime CreatedAt { get; set; }
            public string Path { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] string? status,
            [FromQuery] string? category,
            [FromQuery] string? requesterUid,
            [FromQuery] string? departmentCode,
            [FromQuery] string? assetNumber,
            [FromQuery] string? technicianUid,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var query = _context.ServiceRequests.AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

            if (!string.IsNullOrEmpty(category))
                query = query.Where(r => r.Category == category);

            if (!string.IsNullOrEmpty(requesterUid))
                query = query.Where(r => r.RequesterUid == requesterUid);

            if (!string.IsNullOrEmpty(departmentCode))
                query = query.Where(r => r.RequesterDepartmentCode == departmentCode);

            if (!string.IsNullOrEmpty(assetNumber))
                query = query.Where(r => r.AssetNumber == assetNumber);

            if (!string.IsNullOrEmpty(technicianUid))
                query = query.Where(r => r.TechnicianUid == technicianUid);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            await ApplyDisplayNamesToRequestsAsync(items);

            return Ok(new { total, items });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var item = await _context.ServiceRequests.FindAsync(id);
            if (item == null) return NotFound();

            await ApplyDisplayNamesToRequestsAsync(new[] { item });
            return Ok(item);
        }

        [HttpGet("{id}/messages")]
        public async Task<IActionResult> GetMessages(Guid id)
        {
            var messages = await _context.ChatMessages
                .Where(m => m.ServiceRequestId == id)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            await ApplyDisplayNamesToMessagesAsync(messages);
            return Ok(messages);
        }

        [HttpPost("{id}/messages")]
        public async Task<IActionResult> AddMessage(Guid id, [FromBody] ChatMessageDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var request = await _context.ServiceRequests.FindAsync(id);
            if (request == null) return NotFound();

            var message = new ChatMessage
            {
                ServiceRequestId = id,
                Text = req.Text,
                SenderName = actor.DisplayName,
                SenderEmail = actor.Email,
                SenderId = actor.Uid,
                SenderRole = actor.Role,
                IsRead = false,
                ReadAt = null,
                ReadById = string.Empty,
            };
            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"request:{id}")
                .SendAsync("ChatMessageCreated", new
                {
                    requestId = id,
                    messageId = message.Id,
                    createdAt = message.CreatedAt
                });

            await PublishRealtimeNotificationAsync(
                request,
                "chat",
                $"ข้อความใหม่จาก {actor.DisplayName}",
                true,
                message.CreatedAt);

            return Ok(new { message = "ส่งข้อความสำเร็จ", id = message.Id });
        }

        [HttpPut("{id}/messages/read")]
        public async Task<IActionResult> MarkMessagesRead(Guid id)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var request = await _context.ServiceRequests.FindAsync(id);
            if (request == null) return NotFound();

            var canRead = IsPrivileged(actor.Role) || actor.Uid == request.RequesterUid || actor.Uid == request.TechnicianUid;
            if (!canRead)
                return Forbid();

            var unreadMessages = await _context.ChatMessages
                .Where(m => m.ServiceRequestId == id && !m.IsRead && m.SenderId != actor.Uid)
                .ToListAsync();

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
                message.ReadById = actor.Uid;
            }

            if (unreadMessages.Count > 0)
            {
                await _context.SaveChangesAsync();
                await _hubContext.Clients
                    .Group($"request:{id}")
                    .SendAsync("MessagesMarkedRead", new
                    {
                        requestId = id,
                        readerId = actor.Uid,
                        readAt = DateTime.UtcNow
                    });
            }

            return Ok(new { updated = unreadMessages.Count });
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications([FromQuery] int take = 20)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);

            var safeTake = take <= 0 ? 20 : Math.Min(take, 100);
            var normalizedRole = actor.Role.Trim().ToLowerInvariant();
            var isSuperAdmin = normalizedRole == "superadmin";
            var hasTechnicianPermission = HasPermission(scope, MaintenanceTechnicianPermission);
            var hasSupervisorPermission = HasPermission(scope, MaintenanceSupervisorPermission);
            var hasDepartmentExternalPermission = HasPermission(scope, MaintenanceDepartmentExternalPermission);
            var hasCentralExternalPermission = HasPermission(scope, MaintenanceCentralExternalPermission);
            var actorDepartmentKeys = scope.DepartmentKeys
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            IQueryable<ServiceRequest> scopedRequests = _context.ServiceRequests.AsNoTracking();

            if (!isSuperAdmin)
            {
                if (normalizedRole == "technician" || hasTechnicianPermission)
                {
                    // แจ้งเฉพาะช่างที่รับงานนั้น
                    scopedRequests = scopedRequests.Where(r => r.TechnicianUid == actor.Uid);
                }
                else if (normalizedRole == "supervisor" || hasSupervisorPermission)
                {
                    // แจ้งหัวหน้าที่เกี่ยวข้องกับงานที่ต้องพิจารณา
                    scopedRequests = scopedRequests.Where(r =>
                        r.Status == RepairRequestStatus.NeedSupervisorReview ||
                        r.SupervisorUid == actor.Uid);
                }
                else if (normalizedRole == "adminbuilding" || hasCentralExternalPermission)
                {
                    // ธุรการส่วนกลางแจ้งเตือนเฉพาะตอนมีคิวส่วนกลาง
                    scopedRequests = scopedRequests.Where(r =>
                        r.Status == RepairRequestStatus.WaitingCentralExternalProcurement ||
                        r.Status == RepairRequestStatus.ExternalScheduled ||
                        r.Status == RepairRequestStatus.ExternalInProgress);
                }
                else if (normalizedRole == "admin" || hasDepartmentExternalPermission)
                {
                    // ธุรการหน่วยงาน/กอง เห็นเฉพาะคิวจ้างช่างนอกของหน่วยงานตัวเอง
                    scopedRequests = scopedRequests.Where(r =>
                        !r.IsCentralAsset &&
                        r.Status == RepairRequestStatus.WaitingDepartmentExternalProcurement &&
                        actorDepartmentKeys.Contains(r.RequesterDepartmentCode));
                }
                else
                {
                    // ผู้แจ้ง/หน่วยงานที่แจ้งซ่อม
                    scopedRequests = scopedRequests.Where(r =>
                        r.RequesterUid == actor.Uid ||
                        actorDepartmentKeys.Contains(r.RequesterDepartmentCode));
                }
            }

            var unreadMessagesQuery =
                from m in _context.ChatMessages.AsNoTracking()
                join r in scopedRequests on m.ServiceRequestId equals r.Id
                where !m.IsRead && m.SenderId != actor.Uid
                select new { m, r };

            var unreadChatCount = await unreadMessagesQuery.CountAsync();
            var unreadMessageItems = await unreadMessagesQuery
                .OrderByDescending(x => x.m.CreatedAt)
                .Take(safeTake)
                .Select(x => new NotificationItemDto
                {
                    RequestId = x.r.Id,
                    WorkOrderNo = x.r.WorkOrderNo,
                    Title = x.r.Title,
                    Type = "chat",
                    Message = $"ข้อความใหม่จาก {x.m.SenderName}",
                    Status = x.r.Status,
                    IsUnread = true,
                    CreatedAt = x.m.CreatedAt,
                    Path = $"/maintenance/service/{x.r.Id}"
                })
                .ToListAsync();

            var timelineCandidates = await scopedRequests
                .Where(r => r.UpdatedAt.HasValue && r.UpdatedAt.Value > r.CreatedAt)
                .OrderByDescending(r => r.UpdatedAt)
                .Select(r => new NotificationItemDto
                {
                    RequestId = r.Id,
                    WorkOrderNo = r.WorkOrderNo,
                    Title = r.Title,
                    Type = "timeline",
                    Message = $"สถานะงานอัปเดตเป็น {r.Status}",
                    Status = r.Status,
                    IsUnread = false,
                    CreatedAt = r.UpdatedAt ?? r.CreatedAt,
                    Path = $"/maintenance/service/{r.Id}"
                })
                .ToListAsync();

            var timelineReadMarkers = await _context.AuditLogs
                .AsNoTracking()
                .Where(a => a.Uid == actor.Uid && a.Action == "maintenance.timeline.read")
                .Select(a => new { a.Detail, a.CreatedAt })
                .ToListAsync();

            var latestReadByRequest = timelineReadMarkers
                .Select(x => new
                {
                    RequestId = Guid.TryParse(x.Detail, out var requestId) ? requestId : Guid.Empty,
                    x.CreatedAt
                })
                .Where(x => x.RequestId != Guid.Empty)
                .GroupBy(x => x.RequestId)
                .ToDictionary(g => g.Key, g => g.Max(x => x.CreatedAt));

            var timelineUnreadItems = timelineCandidates
                .Where(item => !latestReadByRequest.TryGetValue(item.RequestId, out var readAt) || item.CreatedAt > readAt)
                .ToList();

            var timelineUpdateCount = timelineUnreadItems.Count;
            var timelineItems = timelineUnreadItems
                .Take(safeTake)
                .ToList();

            var items = unreadMessageItems
                .Concat(timelineItems)
                .OrderByDescending(x => x.CreatedAt)
                .Take(safeTake)
                .ToList();

            return Ok(new
            {
                unreadChatCount,
                timelineUpdateCount,
                total = unreadChatCount + timelineUpdateCount,
                items
            });
        }

        [HttpPut("{id}/timeline/read")]
        public async Task<IActionResult> MarkTimelineRead(Guid id, [FromBody] TimelineReadDto? req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var request = await _context.ServiceRequests
                .AsNoTracking()
                .Where(r => r.Id == id)
                .Select(r => new { r.Id, r.UpdatedAt, r.CreatedAt })
                .FirstOrDefaultAsync();

            if (request == null) return NotFound();

            var existed = await _context.AuditLogs
                .AnyAsync(a =>
                    a.Uid == actor.Uid &&
                    a.Action == "maintenance.timeline.read" &&
                    a.Detail == id.ToString() &&
                    a.CreatedAt >= (request.UpdatedAt ?? request.CreatedAt));

            if (!existed)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    Uid = actor.Uid,
                    DisplayName = actor.DisplayName,
                    Email = actor.Email,
                    Role = actor.Role,
                    Action = "maintenance.timeline.read",
                    Module = "maintenance",
                    Detail = id.ToString(),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    Browser = Request.Headers.UserAgent.ToString(),
                    UserAgent = Request.Headers.UserAgent.ToString(),
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "อ่านการแจ้งเตือนไทม์ไลน์แล้ว" });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ServiceRequestDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var record = new ServiceRequest
            {
                WorkOrderNo = BuildWorkOrderNo(),
                Title = req.Title,
                Description = req.Description,
                Category = req.Category,
                Priority = req.Priority,
                Status = req.Status,
                BuildingName = req.BuildingName,
                LocationDetail = req.LocationDetail,
                AssetNumber = req.AssetNumber,
                IsCentralAsset = req.IsCentralAsset,
                Extension = req.Extension,
                RequesterName = string.IsNullOrWhiteSpace(req.RequesterName) ? actor.DisplayName : req.RequesterName,
                RequesterEmail = string.IsNullOrWhiteSpace(req.RequesterEmail) ? actor.Email : req.RequesterEmail,
                RequesterUid = string.IsNullOrWhiteSpace(req.RequesterUid) ? actor.Uid : req.RequesterUid,
                RequesterDepartmentCode = req.RequesterDepartmentCode,
                RequesterDepartmentName = req.RequesterDepartmentName,
                AssignedTo = req.AssignedTo,
                TechnicianUid = req.TechnicianUid,
                TechnicianName = req.TechnicianName,
                SupervisorUid = req.SupervisorUid,
                SupervisorName = req.SupervisorName,
                AdminOfficerUid = req.AdminOfficerUid,
                AdminOfficerName = req.AdminOfficerName,
                TechnicianDiagnosis = req.TechnicianDiagnosis,
                TechnicianAction = req.TechnicianAction,
                EscalationReason = req.EscalationReason,
                SupervisorCanRepairInHouse = req.SupervisorCanRepairInHouse,
                SupervisorReason = req.SupervisorReason,
                SupervisorRepairPlan = req.SupervisorRepairPlan,
                SupervisorExternalAdvice = req.SupervisorExternalAdvice,
                ExternalVendorName = req.ExternalVendorName,
                ExternalScheduledAt = req.ExternalScheduledAt,
                ExternalCompletedAt = req.ExternalCompletedAt,
                ExternalResult = req.ExternalResult,
                Note = req.Note
            };
            _context.ServiceRequests.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "สร้างใบแจ้งซ่อมสำเร็จ", id = record.Id, workOrderNo = record.WorkOrderNo });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] ServiceRequestDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var record = await _context.ServiceRequests.FindAsync(id);
            if (record == null) return NotFound();

            var canEdit = IsPrivileged(actor.Role);
            if (!canEdit)
                return Forbid();

            record.Title = req.Title;
            record.Description = req.Description;
            record.Category = req.Category;
            record.Priority = req.Priority;
            record.Status = req.Status;
            record.BuildingName = req.BuildingName;
            record.LocationDetail = req.LocationDetail;
            record.AssetNumber = req.AssetNumber;
            record.IsCentralAsset = req.IsCentralAsset;
            record.Extension = req.Extension;
            record.RequesterName = req.RequesterName;
            record.RequesterEmail = req.RequesterEmail;
            record.RequesterUid = req.RequesterUid;
            record.RequesterDepartmentCode = req.RequesterDepartmentCode;
            record.RequesterDepartmentName = req.RequesterDepartmentName;
            record.AssignedTo = req.AssignedTo;
            record.TechnicianUid = req.TechnicianUid;
            record.TechnicianName = req.TechnicianName;
            record.SupervisorUid = req.SupervisorUid;
            record.SupervisorName = req.SupervisorName;
            record.AdminOfficerUid = req.AdminOfficerUid;
            record.AdminOfficerName = req.AdminOfficerName;
            record.TechnicianDiagnosis = req.TechnicianDiagnosis;
            record.TechnicianAction = req.TechnicianAction;
            record.EscalationReason = req.EscalationReason;
            record.SupervisorCanRepairInHouse = req.SupervisorCanRepairInHouse;
            record.SupervisorReason = req.SupervisorReason;
            record.SupervisorRepairPlan = req.SupervisorRepairPlan;
            record.SupervisorExternalAdvice = req.SupervisorExternalAdvice;
            record.ExternalVendorName = req.ExternalVendorName;
            record.ExternalScheduledAt = req.ExternalScheduledAt;
            record.ExternalCompletedAt = req.ExternalCompletedAt;
            record.ExternalResult = req.ExternalResult;
            record.Note = req.Note;
            record.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "แก้ไขใบแจ้งซ่อมสำเร็จ" });
        }

        [HttpPut("{id}/requester-cancel")]
        public async Task<IActionResult> RequesterCancel(Guid id, [FromBody] RequesterCancelDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var record = await _context.ServiceRequests.FindAsync(id);
            if (record == null) return NotFound();

            if (!IsRequesterActor(record, actor))
                return Forbid();

            var canCancel = record.Status == RepairRequestStatus.New || record.Status == RepairRequestStatus.Assigned;
            if (!canCancel)
                return BadRequest(new { message = "ยกเลิกใบงานได้เฉพาะก่อนช่างเริ่มรับงาน" });

            record.Status = RepairRequestStatus.Closed;
            record.ClosedAt = DateTime.UtcNow;
            record.ClosedByUid = actor.Uid;
            record.ClosedByName = actor.DisplayName;
            record.UpdatedAt = DateTime.UtcNow;

            var reason = string.IsNullOrWhiteSpace(req.Reason) ? "-" : req.Reason.Trim();
            record.Note = $"{(string.IsNullOrWhiteSpace(record.Note) ? string.Empty : record.Note + "\n")}[ผู้แจ้ง] ยกเลิกใบงาน: {reason}";

            AddSystemTimelineMessage(record.Id, "ผู้แจ้งยกเลิกใบงาน");

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"request:{record.Id}")
                .SendAsync("RequestTimelineChanged", new
                {
                    requestId = record.Id,
                    status = record.Status,
                    updatedAt = record.UpdatedAt ?? DateTime.UtcNow
                });

            await PublishRealtimeNotificationAsync(
                record,
                "timeline",
                $"สถานะงานอัปเดตเป็น {record.Status}",
                false,
                record.UpdatedAt ?? DateTime.UtcNow);

            return Ok(new { message = "ยกเลิกใบงานเรียบร้อยแล้ว" });
        }

        [HttpPut("{id}/assign-technician")]
        public async Task<IActionResult> AssignTechnician(Guid id, [FromBody] AssignTechnicianDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);

            if (!CanSupervisorAction(actor, scope) && !CanCentralExternalAction(actor, scope))
                return Forbid();

            if (string.IsNullOrWhiteSpace(req.TechnicianUid))
                return BadRequest(new { message = "กรุณาระบุช่างที่รับงาน" });

            var record = await _context.ServiceRequests.FindAsync(id);
            if (record == null) return NotFound();

            record.TechnicianUid = req.TechnicianUid;
            record.TechnicianName = req.TechnicianName;
            record.AssignedTo = req.TechnicianUid;
            record.Status = RepairRequestStatus.Assigned;
            record.UpdatedAt = DateTime.UtcNow;

            AddSystemTimelineMessage(record.Id, "งานถูกมอบหมายให้ช่างรับผิดชอบ");

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"request:{record.Id}")
                .SendAsync("RequestTimelineChanged", new
                {
                    requestId = record.Id,
                    status = record.Status,
                    updatedAt = record.UpdatedAt ?? DateTime.UtcNow
                });

            await PublishRealtimeNotificationAsync(
                record,
                "timeline",
                $"สถานะงานอัปเดตเป็น {record.Status}",
                false,
                record.UpdatedAt ?? DateTime.UtcNow);

            return Ok(new { message = "มอบหมายช่างสำเร็จ" });
        }

        [HttpPut("{id}/technician-complete")]
        public async Task<IActionResult> TechnicianComplete(Guid id, [FromBody] TechnicianCompleteDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);

            if (!CanTechnicianAction(actor, scope))
                return Forbid();

            var record = await _context.ServiceRequests.FindAsync(id);
            if (record == null) return NotFound();

            var allowedForComplete = record.Status == RepairRequestStatus.InProgress ||
                                     record.Status == RepairRequestStatus.ReturnedToTechnician;
            if (!allowedForComplete)
                return BadRequest(new { message = "สามารถบันทึกผลซ่อมได้เฉพาะงานที่กำลังดำเนินการหรือถูกส่งกลับจากหัวหน้าเท่านั้น" });

            var isSuperAdmin = CanAccess(actor.Role, "superadmin");
            if (!isSuperAdmin && !string.IsNullOrWhiteSpace(record.TechnicianUid) && record.TechnicianUid != actor.Uid)
                return Forbid();

            record.TechnicianDiagnosis = req.Diagnosis;
            record.TechnicianAction = req.Action;
            record.Note = req.Note;
            record.Status = RepairRequestStatus.Resolved;
            record.UpdatedAt = DateTime.UtcNow;

            AddSystemTimelineMessage(record.Id, "ช่างซ่อมเสร็จแล้ว รอการปิดงาน");

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"request:{record.Id}")
                .SendAsync("RequestTimelineChanged", new
                {
                    requestId = record.Id,
                    status = record.Status,
                    updatedAt = record.UpdatedAt ?? DateTime.UtcNow
                });

            await PublishRealtimeNotificationAsync(
                record,
                "timeline",
                $"สถานะงานอัปเดตเป็น {record.Status}",
                false,
                record.UpdatedAt ?? DateTime.UtcNow);

            return Ok(new { message = "ช่างปิดงานซ่อมสำเร็จ" });
        }

        [HttpPut("{id}/technician-start")]
        public async Task<IActionResult> TechnicianStart(Guid id, [FromBody] TechnicianStartDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);

            if (!CanTechnicianAction(actor, scope))
                return Forbid();

            var record = await _context.ServiceRequests.FindAsync(id);
            if (record == null) return NotFound();

            var canStart = record.Status == RepairRequestStatus.New ||
                           record.Status == RepairRequestStatus.Assigned ||
                           record.Status == RepairRequestStatus.ReturnedToTechnician;
            if (!canStart)
                return BadRequest(new { message = "สถานะงานนี้ไม่สามารถกดรับงานได้" });

            var isSuperAdmin = CanAccess(actor.Role, "superadmin");
            if (!isSuperAdmin && !string.IsNullOrWhiteSpace(record.TechnicianUid) && record.TechnicianUid != actor.Uid)
                return Forbid();

            if (string.IsNullOrWhiteSpace(record.TechnicianUid))
            {
                record.TechnicianUid = actor.Uid;
                record.TechnicianName = actor.DisplayName;
                record.AssignedTo = actor.Uid;
            }

            if (!string.IsNullOrWhiteSpace(req.Note))
            {
                record.Note = req.Note.Trim();
            }

            record.Status = RepairRequestStatus.InProgress;
            record.UpdatedAt = DateTime.UtcNow;

            AddSystemTimelineMessage(record.Id, "ช่างรับงานและเริ่มดำเนินการ");

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"request:{record.Id}")
                .SendAsync("RequestTimelineChanged", new
                {
                    requestId = record.Id,
                    status = record.Status,
                    updatedAt = record.UpdatedAt ?? DateTime.UtcNow
                });

            await PublishRealtimeNotificationAsync(
                record,
                "timeline",
                $"สถานะงานอัปเดตเป็น {record.Status}",
                false,
                record.UpdatedAt ?? DateTime.UtcNow);

            return Ok(new { message = "ช่างรับงานเรียบร้อย" });
        }

        [HttpPut("{id}/technician-escalate")]
        public async Task<IActionResult> TechnicianEscalate(Guid id, [FromBody] TechnicianEscalateDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);

            if (!CanTechnicianAction(actor, scope))
                return Forbid();

            var record = await _context.ServiceRequests.FindAsync(id);
            if (record == null) return NotFound();

            if (record.Status != RepairRequestStatus.InProgress)
                return BadRequest(new { message = "สามารถส่งหัวหน้าตรวจสอบได้เฉพาะงานที่กำลังดำเนินการเท่านั้น" });

            var isSuperAdmin = CanAccess(actor.Role, "superadmin");
            if (!isSuperAdmin && !string.IsNullOrWhiteSpace(record.TechnicianUid) && record.TechnicianUid != actor.Uid)
                return Forbid();

            record.TechnicianDiagnosis = req.Diagnosis;
            record.EscalationReason = req.EscalationReason;
            record.Status = RepairRequestStatus.NeedSupervisorReview;
            record.UpdatedAt = DateTime.UtcNow;

            AddSystemTimelineMessage(
                record.Id,
                "ช่างส่งงานให้หัวหน้าตรวจสอบเพื่อพิจารณาแนวทางซ่อม");

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"request:{record.Id}")
                .SendAsync("RequestTimelineChanged", new
                {
                    requestId = record.Id,
                    status = record.Status,
                    updatedAt = record.UpdatedAt ?? DateTime.UtcNow
                });

            await PublishRealtimeNotificationAsync(
                record,
                "timeline",
                $"สถานะงานอัปเดตเป็น {record.Status}",
                false,
                record.UpdatedAt ?? DateTime.UtcNow);

            return Ok(new
            {
                message = "ส่งเรื่องไปหน้าตรวจสอบงานโดยหัวหน้าช่างแล้ว"
            });
        }

        [HttpPut("{id}/supervisor-review")]
        public async Task<IActionResult> SupervisorReview(Guid id, [FromBody] SupervisorReviewDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);

            if (!CanSupervisorAction(actor, scope))
                return Forbid();

            var record = await _context.ServiceRequests.FindAsync(id);
            if (record == null) return NotFound();

            record.SupervisorUid = actor.Uid;
            record.SupervisorName = actor.DisplayName;
            record.SupervisorCanRepairInHouse = req.CanRepairInHouse;
            record.SupervisorReason = req.Reason;
            record.SupervisorRepairPlan = req.RepairPlan;
            record.SupervisorExternalAdvice = req.ExternalAdvice;

            if (req.CanRepairInHouse)
            {
                record.Status = RepairRequestStatus.ReturnedToTechnician;
                AddSystemTimelineMessage(record.Id, "หัวหน้างานส่งกลับให้ช่างดำเนินการต่อ");
            }
            else
            {
                record.Status = record.IsCentralAsset
                    ? RepairRequestStatus.WaitingCentralExternalProcurement
                    : RepairRequestStatus.WaitingDepartmentExternalProcurement;

                if (record.Status == RepairRequestStatus.WaitingCentralExternalProcurement)
                    AddSystemTimelineMessage(record.Id, "หัวหน้างานส่งเรื่องให้ธุรการจ้างช่างนอก");
                else
                    AddSystemTimelineMessage(record.Id, "หัวหน้างานประเมินว่าซ่อมภายในไม่ได้ และส่งกลับหน่วยงาน/กอง");
            }

            record.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"request:{record.Id}")
                .SendAsync("RequestTimelineChanged", new
                {
                    requestId = record.Id,
                    status = record.Status,
                    updatedAt = record.UpdatedAt ?? DateTime.UtcNow
                });

            await PublishRealtimeNotificationAsync(
                record,
                "timeline",
                $"สถานะงานอัปเดตเป็น {record.Status}",
                false,
                record.UpdatedAt ?? DateTime.UtcNow);

            return Ok(new { message = "บันทึกผลพิจารณาหัวหน้างานสำเร็จ" });
        }

        [HttpPut("{id}/external-progress")]
        public async Task<IActionResult> ExternalProgress(Guid id, [FromBody] ExternalProgressDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);

            var record = await _context.ServiceRequests.FindAsync(id);
            if (record == null) return NotFound();

            var canCentralExternal = CanCentralExternalAction(actor, scope);
            var canDepartmentExternal = CanDepartmentExternalAction(actor, scope);

            if (record.IsCentralAsset)
            {
                if (!canCentralExternal)
                    return Forbid();
            }
            else
            {
                if (!canDepartmentExternal)
                    return Forbid();

                if (!string.IsNullOrWhiteSpace(record.RequesterDepartmentCode) &&
                    !IsDepartmentMatch(scope, record.RequesterDepartmentCode))
                    return Forbid();
            }

            record.AdminOfficerUid = actor.Uid;
            record.AdminOfficerName = actor.DisplayName;
            record.ExternalVendorName = req.VendorName;
            record.ExternalScheduledAt = req.ScheduledAt ?? record.ExternalScheduledAt;
            record.ExternalCompletedAt = req.CompletedAt ?? record.ExternalCompletedAt;
            record.ExternalResult = req.Result;

            if (req.CompletedAt.HasValue)
            {
                record.Status = req.CloseAfterComplete ? RepairRequestStatus.Closed : RepairRequestStatus.Resolved;
                AddSystemTimelineMessage(record.Id, req.CloseAfterComplete
                    ? "งานช่างนอกเสร็จสิ้นและปิดงาน"
                    : "งานช่างนอกซ่อมเสร็จ");
            }
            else if (req.ScheduledAt.HasValue)
            {
                record.Status = RepairRequestStatus.ExternalScheduled;
                AddSystemTimelineMessage(record.Id, record.IsCentralAsset
                    ? "ธุรการส่วนกลางนัดหมายช่างนอกแล้ว"
                    : "หน่วยงาน/กองนัดหมายช่างนอกแล้ว");
            }
            else
            {
                record.Status = RepairRequestStatus.ExternalInProgress;
                AddSystemTimelineMessage(record.Id, record.IsCentralAsset
                    ? "งานช่างนอก (ส่วนกลาง) กำลังดำเนินการ"
                    : "งานช่างนอกของหน่วยงาน/กองกำลังดำเนินการ");
            }

            if (record.Status == RepairRequestStatus.Closed)
            {
                record.ClosedAt = DateTime.UtcNow;
                record.ClosedByUid = actor.Uid;
                record.ClosedByName = actor.DisplayName;
            }

            record.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"request:{record.Id}")
                .SendAsync("RequestTimelineChanged", new
                {
                    requestId = record.Id,
                    status = record.Status,
                    updatedAt = record.UpdatedAt ?? DateTime.UtcNow
                });

            await PublishRealtimeNotificationAsync(
                record,
                "timeline",
                $"สถานะงานอัปเดตเป็น {record.Status}",
                false,
                record.UpdatedAt ?? DateTime.UtcNow);

            return Ok(new { message = "อัปเดตความคืบหน้าการซ่อมภายนอกสำเร็จ" });
        }

        [HttpPut("{id}/requester-edit")]
        public async Task<IActionResult> RequesterEdit(Guid id, [FromBody] RequesterEditDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var record = await _context.ServiceRequests.FindAsync(id);
            if (record == null) return NotFound();

            if (!IsRequesterActor(record, actor))
                return Forbid();

            var canEditBeforeTechnicianStart =
                record.Status == RepairRequestStatus.New ||
                record.Status == RepairRequestStatus.Assigned;

            if (!canEditBeforeTechnicianStart)
                return BadRequest(new { message = "ช่างรับงานแล้ว ผู้แจ้งงานไม่สามารถแก้ไขใบงานได้" });

            record.Title = req.Title;
            record.Description = req.Description;
            record.BuildingName = req.BuildingName;
            record.LocationDetail = req.LocationDetail;
            record.Extension = req.Extension;
            record.UpdatedAt = DateTime.UtcNow;

            AddSystemTimelineMessage(record.Id, "ผู้แจ้งแก้ไขรายละเอียดใบงาน");

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"request:{record.Id}")
                .SendAsync("RequestTimelineChanged", new
                {
                    requestId = record.Id,
                    status = record.Status,
                    updatedAt = record.UpdatedAt ?? DateTime.UtcNow
                });

            return Ok(new { message = "แก้ไขใบงานเรียบร้อยแล้ว" });
        }

        // requester-external ถูกยกเลิกตาม workflow ใหม่
        // ผู้แจ้งไม่มีสิทธิ์ส่งเรื่องจ้างช่างนอกเอง หัวหน้างานเป็นผู้ตัดสินใจและธุรการเป็นผู้ดำเนินการ
        [HttpPut("{id}/requester-external")]
        public IActionResult RequesterExternal(Guid id, [FromBody] RequesterExternalDto req)
            => BadRequest(new { message = "การส่งเรื่องจ้างช่างนอกโดยผู้แจ้งไม่อยู่ใน workflow ปัจจุบัน กรุณาติดต่อหัวหน้างาน" });

        [HttpPut("{id}/close")]
        public async Task<IActionResult> CloseByWorkflow(Guid id, [FromBody] CloseRequestDto req)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            var scope = await GetPermissionScopeAsync(actor);

            var record = await _context.ServiceRequests.FindAsync(id);
            if (record == null) return NotFound();

            var normalizedRole = actor.Role.Trim().ToLowerInvariant();
            var isSuperAdmin = normalizedRole == "superadmin";

            var isTechnicianOwner =
                normalizedRole == "technician" &&
                (string.IsNullOrWhiteSpace(record.TechnicianUid) || record.TechnicianUid == actor.Uid);

            var isRequesterOwner = record.RequesterUid == actor.Uid;
            var isDepartmentAdmin = normalizedRole == "admin";
            var isAdminBuilding = normalizedRole == "adminbuilding";
            var hasDepartmentExternalPermission = HasPermission(scope, MaintenanceDepartmentExternalPermission);
            var hasCentralExternalPermission = HasPermission(scope, MaintenanceCentralExternalPermission);

            var canTechnicianClose = !record.ExternalCompletedAt.HasValue && !record.IsCentralAsset &&
                (record.Status == RepairRequestStatus.InProgress || record.Status == RepairRequestStatus.Resolved);

            var canDepartmentClose = !record.IsCentralAsset &&
                (record.Status == RepairRequestStatus.ExternalInProgress ||
                 record.Status == RepairRequestStatus.ExternalScheduled ||
                 record.Status == RepairRequestStatus.Resolved);

            var canCentralClose = record.IsCentralAsset &&
                (record.Status == RepairRequestStatus.ExternalInProgress || record.Status == RepairRequestStatus.ExternalScheduled || record.Status == RepairRequestStatus.Resolved);

            var allowed = isSuperAdmin
                || (canTechnicianClose && isTechnicianOwner)
                || (canDepartmentClose && (isDepartmentAdmin || hasDepartmentExternalPermission))
                || (canCentralClose && (isAdminBuilding || hasCentralExternalPermission));

            if (!allowed)
                return Forbid();

            record.Status = RepairRequestStatus.Closed;
            record.ClosedAt = DateTime.UtcNow;
            record.ClosedByUid = actor.Uid;
            record.ClosedByName = actor.DisplayName;
            record.UpdatedAt = DateTime.UtcNow;

            var note = string.IsNullOrWhiteSpace(req.Note) ? "-" : req.Note.Trim();
            record.Note = $"{(string.IsNullOrWhiteSpace(record.Note) ? string.Empty : record.Note + "\n")}[ปิดงาน] โดย {actor.DisplayName}: {note}";

            AddSystemTimelineMessage(record.Id, $"ปิดงานโดย {actor.DisplayName}");

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"request:{record.Id}")
                .SendAsync("RequestTimelineChanged", new
                {
                    requestId = record.Id,
                    status = record.Status,
                    updatedAt = record.UpdatedAt ?? DateTime.UtcNow
                });

            await PublishRealtimeNotificationAsync(
                record,
                "timeline",
                $"สถานะงานอัปเดตเป็น {record.Status}",
                false,
                record.UpdatedAt ?? DateTime.UtcNow);

            return Ok(new { message = "ปิดงานเรียบร้อยแล้ว" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var actor = GetActorContext();
            if (actor == null) return Unauthorized();

            if (!IsPrivileged(actor.Role))
                return Forbid();

            var record = await _context.ServiceRequests.FindAsync(id);
            if (record == null) return NotFound();
            _context.ServiceRequests.Remove(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "ลบสำเร็จ" });
        }

        [HttpGet("asset/{assetNumber}/history")]
        public async Task<IActionResult> GetAssetHistory(string assetNumber)
        {
            var normalized = assetNumber.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return BadRequest(new { message = "กรุณาระบุเลขสินทรัพย์" });

            var items = await _context.ServiceRequests
                .Where(x => x.AssetNumber == normalized)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.WorkOrderNo,
                    x.Title,
                    x.Status,
                    x.Priority,
                    x.BuildingName,
                    x.LocationDetail,
                    x.TechnicianName,
                    x.TechnicianDiagnosis,
                    x.TechnicianAction,
                    x.SupervisorReason,
                    x.SupervisorRepairPlan,
                    x.SupervisorExternalAdvice,
                    x.ExternalVendorName,
                    x.ExternalScheduledAt,
                    x.ExternalCompletedAt,
                    x.ExternalResult,
                    x.ClosedAt,
                    x.CreatedAt,
                    x.UpdatedAt
                })
                .ToListAsync();

            return Ok(new { assetNumber = normalized, total = items.Count, items });
        }

        private static string BuildWorkOrderNo()
        {
            return $"WO-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        private async Task<PermissionScope> GetPermissionScopeAsync(ActorContext actor)
        {
            var scope = new PermissionScope();

            if (!Guid.TryParse(actor.Uid, out var actorGuid))
                return scope;

            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == actorGuid)
                .Select(u => new
                {
                    u.DepartmentId,
                    DepartmentCode = u.Department != null ? u.Department.Code : string.Empty,
                    u.AdminSystems
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return scope;

            scope.DepartmentId = user.DepartmentId?.ToString() ?? string.Empty;
            scope.DepartmentCode = user.DepartmentCode ?? string.Empty;
            scope.DepartmentKeys = new[] { scope.DepartmentId, scope.DepartmentCode }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            scope.AdminSystems = (user.AdminSystems ?? new List<string>())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return scope;
        }

        private static bool HasPermission(PermissionScope scope, string permission)
            => scope.AdminSystems.Contains(permission);

        private static bool CanTechnicianAction(ActorContext actor, PermissionScope scope)
            => CanAccess(actor.Role, "superadmin", "technician") || HasPermission(scope, MaintenanceTechnicianPermission);

        private static bool CanSupervisorAction(ActorContext actor, PermissionScope scope)
            => CanAccess(actor.Role, "superadmin", "supervisor", "admin") || HasPermission(scope, MaintenanceSupervisorPermission);

        private static bool CanDepartmentExternalAction(ActorContext actor, PermissionScope scope)
            => CanAccess(actor.Role, "superadmin", "admin") || HasPermission(scope, MaintenanceDepartmentExternalPermission);

        private static bool CanCentralExternalAction(ActorContext actor, PermissionScope scope)
            => CanAccess(actor.Role, "superadmin", "adminbuilding") || HasPermission(scope, MaintenanceCentralExternalPermission);

        private sealed class PermissionScope
        {
            public string DepartmentId { get; set; } = string.Empty;
            public string DepartmentCode { get; set; } = string.Empty;
            public HashSet<string> DepartmentKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> AdminSystems { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsDepartmentMatch(PermissionScope scope, string requesterDepartmentCode)
        {
            if (string.IsNullOrWhiteSpace(requesterDepartmentCode))
                return false;

            return scope.DepartmentKeys.Contains(requesterDepartmentCode.Trim());
        }

        private static bool CanAccess(string actorRole, params string[] allowedRoles)
        {
            var normalized = actorRole.Trim().ToLowerInvariant();
            return allowedRoles.Contains(normalized);
        }

        private static bool IsPrivileged(string actorRole)
        {
            return CanAccess(actorRole, "superadmin", "adminbuilding", "supervisor");
        }

        private static bool IsRequesterActor(ServiceRequest record, ActorContext actor)
        {
            if (!string.IsNullOrWhiteSpace(record.RequesterUid) && record.RequesterUid == actor.Uid)
                return true;

            if (!string.IsNullOrWhiteSpace(record.RequesterEmail) &&
                !string.IsNullOrWhiteSpace(actor.Email) &&
                string.Equals(record.RequesterEmail.Trim(), actor.Email.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private async Task PublishRealtimeNotificationAsync(
            ServiceRequest request,
            string type,
            string message,
            bool isUnread,
            DateTime createdAt)
        {
            var targetGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "role:superadmin"
            };

            if (!string.IsNullOrWhiteSpace(request.RequesterUid))
                targetGroups.Add($"user:{request.RequesterUid}");

            if (!string.IsNullOrWhiteSpace(request.RequesterDepartmentCode))
                targetGroups.Add($"department:{request.RequesterDepartmentCode}");

            if (!string.IsNullOrWhiteSpace(request.TechnicianUid))
                targetGroups.Add($"user:{request.TechnicianUid}");

            if (!string.IsNullOrWhiteSpace(request.SupervisorUid))
                targetGroups.Add($"user:{request.SupervisorUid}");

            if (request.Status == RepairRequestStatus.NeedSupervisorReview)
                targetGroups.Add("role:supervisor");

            if (request.Status == RepairRequestStatus.WaitingCentralExternalProcurement)
                targetGroups.Add("role:adminbuilding");

            if (request.Status == RepairRequestStatus.WaitingDepartmentExternalProcurement)
            {
                if (!string.IsNullOrWhiteSpace(request.RequesterDepartmentCode))
                    targetGroups.Add($"department:{request.RequesterDepartmentCode}");
                targetGroups.Add("role:admin");
            }

            if (targetGroups.Count == 0)
                return;

            var payload = new RealtimeNotificationDto
            {
                RequestId = request.Id,
                WorkOrderNo = request.WorkOrderNo,
                Title = request.Title,
                Type = type,
                Message = message,
                Status = request.Status,
                IsUnread = isUnread,
                CreatedAt = createdAt,
                Path = $"/maintenance/service/{request.Id}"
            };

            await _hubContext.Clients.Groups(targetGroups.ToList()).SendAsync("MaintenanceNotification", payload);
        }

        private void AddSystemTimelineMessage(Guid requestId, string detail)
        {
            _context.ChatMessages.Add(new ChatMessage
            {
                ServiceRequestId = requestId,
                Text = $"[timeline] {detail}",
                SenderName = "ระบบ",
                SenderEmail = "system@energyapp.local",
                SenderId = "system",
                SenderRole = "system",
                IsRead = true,
                ReadAt = DateTime.UtcNow,
                ReadById = "system",
            });
        }

        private ActorContext? GetActorContext()
        {
            var uid = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? string.Empty;

            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var fullName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            var firstName = User.FindFirstValue("FirstName") ?? string.Empty;
            var email = User.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? User.FindFirstValue(ClaimTypes.Email)
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(role))
                return null;

            var displayName = FirstNonEmpty(
                NormalizeDisplayName(fullName),
                NormalizeDisplayName(firstName),
                DeriveNameFromEmail(email),
                uid);

            return new ActorContext(uid, role, displayName, email);
        }

        private async Task ApplyDisplayNamesToRequestsAsync(IEnumerable<ServiceRequest> requests)
        {
            var requestList = requests.ToList();
            if (requestList.Count == 0)
                return;

            var userMap = await BuildUserDisplayNameMapAsync(
                requestList
                    .SelectMany(r => new[]
                    {
                        r.RequesterUid,
                        r.TechnicianUid,
                        r.SupervisorUid,
                        r.AdminOfficerUid,
                        r.ClosedByUid,
                    }));

            foreach (var r in requestList)
            {
                r.RequesterName = ResolveDisplayName(userMap, r.RequesterUid, r.RequesterName, r.RequesterEmail);
                r.TechnicianName = ResolveDisplayName(userMap, r.TechnicianUid, r.TechnicianName, null);
                r.SupervisorName = ResolveDisplayName(userMap, r.SupervisorUid, r.SupervisorName, null);
                r.AdminOfficerName = ResolveDisplayName(userMap, r.AdminOfficerUid, r.AdminOfficerName, null);
                r.ClosedByName = ResolveDisplayName(userMap, r.ClosedByUid, r.ClosedByName, null);
            }
        }

        private async Task ApplyDisplayNamesToMessagesAsync(IEnumerable<ChatMessage> messages)
        {
            var messageList = messages.ToList();
            if (messageList.Count == 0)
                return;

            var userMap = await BuildUserDisplayNameMapAsync(messageList.Select(m => m.SenderId));

            foreach (var m in messageList)
            {
                if (string.Equals(m.SenderRole, "system", StringComparison.OrdinalIgnoreCase))
                    continue;

                m.SenderName = ResolveDisplayName(userMap, m.SenderId, m.SenderName, m.SenderEmail);
            }
        }

        private async Task<Dictionary<string, string>> BuildUserDisplayNameMapAsync(IEnumerable<string?> rawUids)
        {
            var guidIds = rawUids
                .Where(uid => !string.IsNullOrWhiteSpace(uid))
                .Select(uid => Guid.TryParse(uid, out var guid) ? guid : Guid.Empty)
                .Where(guid => guid != Guid.Empty)
                .Distinct()
                .ToList();

            if (guidIds.Count == 0)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var users = await _context.Users
                .AsNoTracking()
                .Where(u => guidIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email
                })
                .ToListAsync();

            return users.ToDictionary(
                u => u.Id.ToString(),
                u => FirstNonEmpty(
                    NormalizeDisplayName($"{u.FirstName} {u.LastName}"),
                    DeriveNameFromEmail(u.Email),
                    u.Id.ToString()),
                StringComparer.OrdinalIgnoreCase);
        }

        private static string ResolveDisplayName(
            IReadOnlyDictionary<string, string> userDisplayNames,
            string? uid,
            string? preferredName,
            string? fallbackEmail)
        {
            if (!string.IsNullOrWhiteSpace(uid) && userDisplayNames.TryGetValue(uid, out var fromUserMap))
                return fromUserMap;

            return FirstNonEmpty(
                NormalizeDisplayName(preferredName),
                DeriveNameFromEmail(fallbackEmail),
                "-");
        }

        private static string NormalizeDisplayName(string? value)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return string.Empty;

            return trimmed.Contains('@') ? string.Empty : trimmed;
        }

        private static string DeriveNameFromEmail(string? email)
        {
            var trimmed = (email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return string.Empty;

            var localPart = trimmed.Split('@')[0];
            return string.IsNullOrWhiteSpace(localPart) ? string.Empty : localPart;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }

        private sealed record ActorContext(string Uid, string Role, string DisplayName, string Email);
    }
}
