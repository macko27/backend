using AGILE2024_BE.Data;
using AGILE2024_BE.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using AGILE2024_BE.Models.Enums;
using Microsoft.EntityFrameworkCore;
using AGILE2024_BE.Models.Recognition;
using AGILE2024_BE.Models;
using System.ComponentModel.DataAnnotations.Schema;
using static System.Runtime.InteropServices.JavaScript.JSType;
using AGILE2024_BE.Models.Survey;
using AGILE2024_BE.Services;

namespace AGILE2024_BE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RecognitionController : Controller
    {
        private UserManager<ExtendedIdentityUser> userManager;
        private RoleManager<IdentityRole> roleManager;
        private IConfiguration config;
        private AgileDBContext dbContext;
        private readonly IHubContext<NotificationHub> hubContext;

        public RecognitionController(UserManager<ExtendedIdentityUser> um, IConfiguration co, RoleManager<IdentityRole> rm, AgileDBContext db, IHubContext<NotificationHub> hubContext)
        {
            this.userManager = um;
            this.config = co;
            this.roleManager = rm;
            this.dbContext = db;
            this.hubContext = hubContext;
        }


        //**********************************************************************************
        // Ziskanie uznani pre daneho pouzivatela
        // Zoznam uznani
        //**********************************************************************************
        [HttpGet("GetRecieved/{employeeId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec + "," + RolesDef.Spravca)]
        public async Task<IActionResult> GetRecievedRecognitions(Guid employeeId)
        {
            var employeeCard = await dbContext.EmployeeCards
                .Include(ec => ec.User)
                .Include(ec => ec.Department)
                .FirstOrDefaultAsync(ec => ec.Id == employeeId);

            if (employeeCard == null)
                return BadRequest("EmployeeCard pre daného používateľa neexistuje.");

            var departmentId = employeeCard.Department?.Id;

            var recognitions = await dbContext.Recognitions
                .Include(r => r.createdBy)
                .Include(r => r.Recipients)
                    .ThenInclude(rec => rec.EmployeeCard)
                        .ThenInclude(ec => ec.User)
                .Where(r => r.Recipients.Any(rec => rec.EmployeeCardId == employeeCard.Id))
                .ToListAsync();


            var response = recognitions.Select(s => new
            {
                s.Id,
                s.Predmet,
                s.Text,
                createdById = s.createdBy.Id,
                recipients = s.Recipients.Select(q => new
                {
                    id = q.EmployeeCardId,
                    fullName = q.EmployeeCard.User.Name + " " + q.EmployeeCard.User.Surname
                })
            });

            return Ok(response);
        }



        //**********************************************************************************
        // Ziskanie uznani pre daneho pouzivatela
        // Zoznam uznani
        //**********************************************************************************
        [HttpGet("GetSent/{employeeId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec + "," + RolesDef.Spravca)]
        public async Task<IActionResult> GetSentRecognitions(Guid employeeId)
        {
            var employeeCard = await dbContext.EmployeeCards
                .Include(ec => ec.User)
                .Include(ec => ec.Department)
                .FirstOrDefaultAsync(ec => ec.Id == employeeId);

            if (employeeCard == null)
                return BadRequest("EmployeeCard pre daného používateľa neexistuje.");

            var departmentId = employeeCard.Department?.Id;

            var recognitions = await dbContext.Recognitions
                .Include(r => r.createdBy)
                .Include(r => r.Recipients)
                    .ThenInclude(rec => rec.EmployeeCard)
                        .ThenInclude(ec => ec.User)
                .Where(r => r.createdBy.Id == employeeCard.Id && r.anoPlatny == 1)
                .ToListAsync();


            var response = recognitions.Select(s => new
            {
                s.Id,
                s.Predmet,
                s.Text,
                createdById = s.createdBy.Id,
                recipients = s.Recipients.Select(q => new
                {
                    id = q.EmployeeCardId,
                    fullName = q.EmployeeCard.User.Name + " " + q.EmployeeCard.User.Surname
                })
            });

            return Ok(response);
        }




        //**********************************************************************************
        // Vytvorenie uznania
        //**********************************************************************************
        [HttpPost("Create")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec + "," + RolesDef.Spravca)]
        public async Task<IActionResult> CreateRecognition([FromBody] RecognitionRequest data)
        {
            if (data == null)
                return BadRequest("Neplatné dáta");

            var createdBy = await dbContext.EmployeeCards
                .FirstOrDefaultAsync(ec => ec.Id == data.createdById);

            if (createdBy == null)
            {
                return BadRequest("Vytvárajúci zamestnanec neexistuje.");
            }

            try
            {
                var recognition = new Recognition
                {
                    Id = Guid.NewGuid(),
                    Predmet = data.predmet,
                    Text = data.text,
                    Odmena = data.odmena,
                    createdBy = createdBy,
                    DateIn = DateTime.UtcNow,
                    anoPlatny = 1,
                    Recipients = data.recipients.Select(r => new RecognitionRecipient
                    {
                        Id = Guid.NewGuid(),
                        EmployeeCardId = r.id
                    }).ToList()
                };

                dbContext.Recognitions.Add(recognition);
                await dbContext.SaveChangesAsync();

                //notifikacie
                var users = await GetSurveyRecipientUsersAsync(recognition);
                var notifications = new List<Notification>();

                foreach (var user in users)
                {
                    notifications.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false,
                        NotificationType = EnumNotificationType.RecognitionCreated,
                        ReferencedItemId = recognition.Id,
                        Message = $"Dostali ste uznanie: {recognition.Predmet}",
                        User = user
                    });
                }

                dbContext.Notifications.AddRange(notifications);
                await dbContext.SaveChangesAsync();

                foreach (var notification in notifications)
                {
                    var response = new NotificationResponse
                    {
                        Id = notification.Id,
                        CreatedAt = notification.CreatedAt,
                        IsRead = notification.IsRead,
                        Message = notification.Message,
                        NotificationType = notification.NotificationType,
                        ReferencedItem = notification.ReferencedItemId.ToString(),
                        Title = NotificationHelpers.GetNotificationTitle(notification.NotificationType)
                    };

                    await hubContext.Clients.User(notification.User.Id)
                        .SendAsync("ReceiveNotification", response);
                }


                return Ok(new { id = recognition.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Chyba pri vytváraní uznania: {ex.Message}");
            }
        }



        //**********************************************************************************
        // Ziskanie vsetkych unikatnych prijemcov pre anketu
        //**********************************************************************************
        private async Task<List<ExtendedIdentityUser>> GetSurveyRecipientUsersAsync(Recognition recognition)
        {
            var userList = new List<ExtendedIdentityUser>();

            foreach (var r in recognition.Recipients)
            {
                var user = await dbContext.EmployeeCards
                        .Include(e => e.User)
                        .Where(e => e.Id == r.EmployeeCardId)
                        .Select(e => e.User)
                        .FirstOrDefaultAsync();

                if (user != null)
                    userList.Add(user);
            }

            // Odstránenie duplicít
            return userList
                .GroupBy(u => u.Id)
                .Select(g => g.First())
                .ToList();
        }



        //**********************************************************************************
        // Vyhľadávanie zamestnancov
        //**********************************************************************************
        [HttpGet("SearchRecipients")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec)]
        public async Task<IActionResult> SearchRecipients([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Ok(new List<object>());

            query = query.Trim().ToLower();


            var user = await userManager.GetUserAsync(User);
            var roles = await userManager.GetRolesAsync(user);

            bool isLeader = roles.Contains(RolesDef.Veduci);
            bool isEmployee = roles.Contains(RolesDef.Zamestnanec);


            // 1. Zamestnanci (meno a priezvisko obsahujú query)
            var employees = await dbContext.EmployeeCards
                .Include(e => e.User)
                .Where(e =>
                    (e.User.Name + " " + e.User.Surname).ToLower().Contains(query)
                )
                .Select(e => new
                {
                    Id = e.Id,
                    FullName = e.User.Name + " " + e.User.Surname,
                })
                .Take(20)
                .ToListAsync();

            return Ok(employees);
        }


        public class RecognitionRequest
        {
            public required string predmet { get; set; }
            public required string text { get; set; }
            public int odmena { get; set; }

            public Guid createdById { get; set; }
            public ICollection<RecognitionRecipientRequest>? recipients { get; set; } = new List<RecognitionRecipientRequest>();

        }

        public class RecognitionRecipientRequest
        {
            public Guid id { get; set; }

        }


        public class RecognitionDto
        {
            public Guid Id { get; set; }
            public required string Predmet { get; set; }
            public required string Text { get; set; }
            public int Odmena { get; set; }

            public Guid createdById { get; set; }
            public ICollection<RecognitionRecipient>? Recipients { get; set; } = new List<RecognitionRecipient>();

        }
    }
}
