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
using AGILE2024_BE.Services;
using Microsoft.IdentityModel.Tokens;

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
        public async Task<IActionResult> GetRecievedRecognitions(Guid employeeId)
        {
            var exists = await dbContext.EmployeeCards
                .AnyAsync(ec => ec.Id == employeeId);

            if (!exists)
                return BadRequest("EmployeeCard neexistuje.");

            var data = await BaseRecognitionQuery()
                .Where(r => r.Recipients.Any(rec =>
                    rec.EmployeeCardId == employeeId &&
                    (rec.State == EnumRecognitionState.Schvalena ||
                     rec.State == EnumRecognitionState.SchvalenaSUpravou)
                ))
                .Select(r => new
                {
                    r.Id,
                    r.Predmet,
                    r.Text,
                    r.DateIn,
                    odmena = r.Odmena > 0 ? r.Odmena.ToString() : "-",

                    createdBy = new
                    {
                        id = r.createdBy.Id,
                        fullName = r.createdBy.User.Name + " " + r.createdBy.User.Surname
                    },

                    myState = r.Recipients
                        .Where(rec => rec.EmployeeCardId == employeeId)
                        .Select(rec => rec.State)
                        .FirstOrDefault(),

                })
                .OrderByDescending(r => r.DateIn)
                .ToListAsync();

            return Ok(data);
        }



        //**********************************************************************************
        // Ziskanie uznani pre daneho pouzivatela
        // Zoznam uznani
        //**********************************************************************************
        [HttpGet("GetSent/{employeeId}")]
        public async Task<IActionResult> GetSentRecognitions(Guid employeeId)
        {
            var exists = await dbContext.EmployeeCards
                .AnyAsync(ec => ec.Id == employeeId);

            if (!exists)
                return BadRequest("EmployeeCard neexistuje.");

            var data = await BaseRecognitionQuery()
                .Where(r => r.createdBy.Id == employeeId)
                .Select(r => new
                {
                    r.Id,
                    r.Predmet,
                    r.Text,
                    r.DateIn,
                    odmena = r.Odmena > 0 ? r.Odmena.ToString() : "-",

                    createdBy = new
                    {
                        id = r.createdBy.Id,
                        fullName = r.createdBy.User.Name + " " + r.createdBy.User.Surname
                    },

                    // 🔥 KAŽDÝ RECIPIENT MÁ VLASTNÝ STATE
                    recipients = r.Recipients.Select(q => new
                    {
                        id = q.EmployeeCardId,
                        fullName = q.EmployeeCard.User.Name + " " + q.EmployeeCard.User.Surname,
                        state = q.State
                    }),
                })
                .OrderByDescending(r => r.DateIn)
                .ToListAsync();

            return Ok(data);
        }



        //**********************************************************************************
        // Ziskanie uznani na schvalenie, veduci schvaluje uznania kde prijemcovia su vsetci z jeho oddelenia inac schvaluje admin
        // veduci z oddelenia z ktoreho je createdBy to schvaluje
        //**********************************************************************************
        [HttpGet("GetToBeApproved/{employeeId}")]
        public async Task<IActionResult> GetToBeApproved(Guid employeeId)
        {
            var employee = await dbContext.EmployeeCards
                .Include(e => e.Department)
                .FirstOrDefaultAsync(ec => ec.Id == employeeId);

            if (employee == null)
                return BadRequest("EmployeeCard neexistuje.");

            var departmentId = employee.Department.Id;

            var data = await BaseRecognitionQuery()
                .SelectMany(r => r.Recipients
                    .Where(rec =>
                        rec.State == EnumRecognitionState.Cakajuca &&
                        rec.EmployeeCard.Department.Id == departmentId
                    )
                    .Select(rec => new
                    {
                        recipientRecordId = rec.Id,
                        recognitionId = r.Id,
                        predmet = r.Predmet,
                        text = r.Text,
                        dateIn = r.DateIn,

                        createdBy = new
                        {
                            id = r.createdBy.Id,
                            fullName = r.createdBy.User.Name + " " + r.createdBy.User.Surname
                        },

                        recipient = new
                        {
                            id = rec.EmployeeCardId,
                            fullName = rec.EmployeeCard.User.Name + " " + rec.EmployeeCard.User.Surname,
                            state = rec.State,
                            odmena = r.Odmena
                        }
                    })
                )
                .OrderByDescending(x => x.dateIn)
                .ToListAsync();

            return Ok(data);
        }


        private IQueryable<Recognition> BaseRecognitionQuery()
        {
            return dbContext.Recognitions
                .AsNoTracking()
                .Include(r => r.createdBy)
                    .ThenInclude(cr => cr.User)
                .Include(r => r.Recipients)
                    .ThenInclude(rec => rec.EmployeeCard)
                        .ThenInclude(ec => ec.User);
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
                    Recipients = data.recipients.Select(r => new RecognitionRecipient
                    {
                        Id = Guid.NewGuid(),
                        EmployeeCardId = r.id
                    }).ToList()
                };

                //nastavenie stavu
                foreach (var recipient in recognition.Recipients)
                {
                    recipient.State = await GetInitialStateForRecipient(recognition, recipient);
                }

                dbContext.Recognitions.Add(recognition);
                await dbContext.SaveChangesAsync();

                // notifikacie
                var users = await GetSurveyRecipientUsersAsync(recognition);
                var notifications = new List<Notification>();

                // 🔥 1. NOTIFIKÁCIA PRE VEDÚCICH (iba ak existuje pending recipient)
                var hasPending = recognition.Recipients
                    .Any(r => r.State == EnumRecognitionState.Cakajuca);

                if (hasPending)
                {
                    var leaders = await GetLeadersFromCreatorDepartment(recognition);

                    foreach (var leader in leaders)
                    {
                        notifications.Add(new Notification
                        {
                            Id = Guid.NewGuid(),
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false,
                            NotificationType = EnumNotificationType.RecognitionCreated,
                            ReferencedItemId = recognition.Id,
                            Message = $"Uznanie čaká na schválenie: {recognition.Predmet}",
                            User = leader
                        });
                    }
                }


                //NOTIFIKÁCIE PRE RECIPIENTOV (KAŽDÝ PODĽA STAVU)
                foreach (var recipient in recognition.Recipients)
                {
                    if (recipient.State == EnumRecognitionState.Schvalena)
                    {
                        var user = await dbContext.EmployeeCards
                            .Include(e => e.User)
                            .Where(e => e.Id == recipient.EmployeeCardId)
                            .Select(e => e.User)
                            .FirstOrDefaultAsync();

                        if (user == null)
                            continue;


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
                    
                }

                if (!notifications.IsNullOrEmpty())
                {
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
                }


                return Ok(new { id = recognition.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Chyba pri vytváraní uznania: {ex.Message}");
            }
        }



        //**********************************************************************************
        // Nastavenie stavu uznania podla role
        //**********************************************************************************
        private async Task<EnumRecognitionState> GetInitialStateForRecipient(
            Recognition recognition,
            RecognitionRecipient recipient)
        {
            var creator = await dbContext.EmployeeCards
                .Include(e => e.User)
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == recognition.createdBy.Id);

            if (creator == null)
                return EnumRecognitionState.Cakajuca;

            var roles = await userManager.GetRolesAsync(creator.User);

            bool isEmployee = roles.Contains(RolesDef.Zamestnanec);
            bool isLeader = roles.Contains(RolesDef.Veduci);

            // zamestnanec
            if (isEmployee && !isLeader)
            {
                return recognition.Odmena > 0
                    ? EnumRecognitionState.Cakajuca
                    : EnumRecognitionState.Schvalena;
            }

            // vedúci
            if (isLeader)
            {
                var recipientEmployee = await dbContext.EmployeeCards
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Id == recipient.EmployeeCardId);

                if (recipientEmployee == null)
                    return EnumRecognitionState.Cakajuca;

                if (recognition.Odmena <= 0)
                    return EnumRecognitionState.Schvalena;

                return recipientEmployee.Department.Id == creator.Department.Id
                    ? EnumRecognitionState.Schvalena
                    : EnumRecognitionState.Cakajuca;
            }

            return EnumRecognitionState.Cakajuca;
        }


        //**********************************************************************************
        // Ziskanie vsetkych veducich pre toho co to vytvoril pre oddelenie
        //**********************************************************************************
        private async Task<List<ExtendedIdentityUser>> GetLeadersFromCreatorDepartment(Recognition recognition)
        {
            // creator s departmentom
            var creator = await dbContext.EmployeeCards
                .Include(e => e.User)
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == recognition.createdBy.Id);

            if (creator == null)
                return new List<ExtendedIdentityUser>();

            var departmentId = creator.Department.Id;

            // všetci zamestnanci z oddelenia
            var employees = await dbContext.EmployeeCards
                .Include(e => e.User)
                .Where(e => e.Department.Id == departmentId)
                .ToListAsync();

            var leaders = new List<ExtendedIdentityUser>();

            foreach (var emp in employees)
            {
                var roles = await userManager.GetRolesAsync(emp.User);

                if (roles.Contains(RolesDef.Veduci))
                {
                    leaders.Add(emp.User);
                }
            }

            return leaders
                .GroupBy(u => u.Id)
                .Select(g => g.First())
                .ToList();
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


        //**********************************************************************************
        // Vytvorenie uznania
        //**********************************************************************************
        [HttpPost("Approve")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec + "," + RolesDef.Spravca)]
        public async Task<IActionResult> ApproveRecognition([FromBody] RecognitioToApprove data)
        {
            var recipientRecord = await dbContext.Set<RecognitionRecipient>()
                .Include(r => r.EmployeeCard)
                    .ThenInclude(e => e.User)
                .Include(r => r.Recognition)
                .FirstOrDefaultAsync(r => r.Id == data.recipientRecordId);

            if (recipientRecord == null)
                return BadRequest("Záznam príjemcu neexistuje.");

            // Mapovanie state stringu → enum
            if (!Enum.TryParse<EnumRecognitionState>(data.state, out var newState))
                return BadRequest("Neplatný stav.");

            recipientRecord.State = newState;
            recipientRecord.dovod = data.dovod;

            if (data.odmena.HasValue)
                recipientRecord.Recognition.Odmena = data.odmena.Value;

            await dbContext.SaveChangesAsync();

            // Notifikácia iba ak je stav schválený
            if (newState == EnumRecognitionState.Schvalena || newState == EnumRecognitionState.SchvalenaSUpravou)
            {
                var user = recipientRecord.EmployeeCard.User;
                var recognition = recipientRecord.Recognition;

                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    NotificationType = EnumNotificationType.RecognitionCreated,
                    ReferencedItemId = recognition.Id,
                    Message = $"Vaše uznanie bolo schválené: {recognition.Predmet}",
                    User = user
                };

                dbContext.Notifications.Add(notification);
                await dbContext.SaveChangesAsync();

                var response = new NotificationResponse
                {
                    Id = notification.Id,
                    CreatedAt = notification.CreatedAt,
                    IsRead = notification.IsRead,
                    Message = notification.Message,
                    NotificationType = notification.NotificationType,
                    ReferencedItem = recognition.Id.ToString(),
                    Title = NotificationHelpers.GetNotificationTitle(notification.NotificationType)
                };

                await hubContext.Clients.User(user.Id)
                    .SendAsync("ReceiveNotification", response);
            }

            return Ok();
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


        public class RecognitionResponseDto
        {
            public Guid Id { get; set; }
            public string Predmet { get; set; }
            public string Text { get; set; }
            public DateTime DateIn { get; set; }
            public string Odmena { get; set; }

            public UserDto CreatedBy { get; set; }
            public List<UserDto> Recipients { get; set; }
            public EnumRecognitionState state { get; set; }
        }

        public class UserDto
        {
            public Guid Id { get; set; }
            public string FullName { get; set; }
        }


        public class RecognitioToApprove
        {
            public Guid recipientRecordId { get; set; }
            public Guid recognitionId { get; set; }
            public string state { get; set; }
            public int? odmena { get; set; }
            public string? dovod { get; set; }
        }

    }
}
