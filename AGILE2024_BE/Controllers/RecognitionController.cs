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
using Azure.Storage.Blobs;
using System.ComponentModel.DataAnnotations.Schema;

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



        [HttpGet("GetDetail/{recognitionId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec + "," + RolesDef.Spravca + "," + RolesDef.ShopAdmin)]
        public async Task<IActionResult> GetRecognitionDetail(Guid recognitionId)
        {
            var recognition = await dbContext.Recognitions
                .Include(r => r.createdBy)
                    .ThenInclude(cr => cr.User)
                .Include(r => r.Recipients)
                    .ThenInclude(rec => rec.EmployeeCard)
                        .ThenInclude(ec => ec.User)
                .Include(r => r.Attachments)
                .FirstOrDefaultAsync(r => r.Id == recognitionId);

            if (recognition == null)
                return NotFound("Uznanie neexistuje.");

            var response = new RecognitionResponseDto
            {
                Id = recognition.Id,
                Predmet = recognition.Predmet,
                Text = recognition.Text,
                DateIn = recognition.DateIn,
                Odmena = recognition.Odmena.ToString(),
                CreatedBy = new UserDto
                {
                    Id = recognition.createdBy.Id,
                    FullName = recognition.createdBy.User.Name + " " + recognition.createdBy.User.Surname
                },
                Recipients = recognition.Recipients.Select(r => new UserDto
                {
                    Id = r.EmployeeCardId,
                    FullName = r.EmployeeCard.User.Name + " " + r.EmployeeCard.User.Surname
                }).ToList(),
                state = recognition.Recipients.FirstOrDefault()?.State ?? EnumRecognitionState.Cakajuca
            };

            return Ok(response);
        }




        //**********************************************************************************
        // Ziskanie uznani pre daneho pouzivatela
        // Zoznam uznani
        //**********************************************************************************
        [HttpGet("GetRecieved/{employeeId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec + "," + RolesDef.Spravca + "," + RolesDef.ShopAdmin)]
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

                    recipients = r.Recipients
                        .Where(rec => rec.EmployeeCardId == employeeId)
                        .Select(rec => new
                        {
                            id = rec.EmployeeCardId,
                            state = rec.State
                        })

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
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec + "," + RolesDef.Spravca + "," + RolesDef.ShopAdmin)]
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
                .Include(e => e.User)
                .FirstOrDefaultAsync(ec => ec.Id == employeeId);

            if (employee == null)
                return BadRequest("EmployeeCard neexistuje.");

            var roles = await userManager.GetRolesAsync(employee.User);
            bool isLeader = roles.Contains(RolesDef.Veduci);
            bool isAdmin = roles.Contains(RolesDef.Spravca);

            var recognitions = await BaseRecognitionQuery()
                .Where(r => r.Recipients.Any(rec => rec.State == EnumRecognitionState.Cakajuca))
                .ToListAsync();

            var result = new List<object>();

            foreach (var r in recognitions)
            {
                foreach (var rec in r.Recipients.Where(rec =>
                    rec.State == EnumRecognitionState.Cakajuca &&
                    rec.EmployeeCardId != employeeId)) // nech nevidím seba
                {
                    var recipient = await dbContext.EmployeeCards
                        .Include(e => e.User)
                        .FirstOrDefaultAsync(e => e.Id == rec.EmployeeCardId);

                    if (recipient == null)
                        continue;

                    var recipientRoles = await userManager.GetRolesAsync(recipient.User);
                    bool recipientIsLeader = recipientRoles.Contains(RolesDef.Veduci);

                    bool canSee =
                        (isLeader && !recipientIsLeader) || // vedúci → zamestnanci
                        (isAdmin && recipientIsLeader);     // admin → vedúci

                    if (!canSee)
                        continue;

                    result.Add(new
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
                            fullName = recipient.User.Name + " " + recipient.User.Surname,
                            state = rec.State,
                            odmena = r.Odmena
                        }
                    });
                }
            }

            return Ok(result);
        }



        //**********************************************************************************
        // Ziskanie uznani pre daneho pouzivatela
        // Zoznam uznani
        //**********************************************************************************
        [HttpGet("GetTeamMembers/{employeeId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Spravca)]
        public async Task<IActionResult> GetTeamMembers(Guid employeeId)
        {
            var employee = await dbContext.EmployeeCards
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
                return BadRequest("EmployeeCard neexistuje.");

            var departmentId = employee.Department.Id;

            var teamMembers = await dbContext.EmployeeCards
                .Include(e => e.User)
                .Where(e => e.Department.Id == departmentId && e.Id != employeeId)
                .Select(e => new
                {
                    id = e.Id,
                    fullName = e.User.Name + " " + e.User.Surname,
                    pointsBalance = e.PointsBalance
                })
                .OrderBy(e => e.fullName)
                .ToListAsync();

            return Ok(teamMembers);
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
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec + "," + RolesDef.Spravca + "," + RolesDef.ShopAdmin)]
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

                    //udelenie bodov
                    if (recipient.State == EnumRecognitionState.Schvalena || recipient.State == EnumRecognitionState.SchvalenaSUpravou)
                    {
                        var employeeCard = await dbContext.EmployeeCards
                            .FirstOrDefaultAsync(e => e.Id == recipient.EmployeeCardId);

                        if (employeeCard != null)
                        {
                            employeeCard.PointsBalance += recognition.Odmena;

                            dbContext.PointsTransactions.Add(new PointsTransaction
                            {
                                Id = Guid.NewGuid(),
                                EmployeeCardId = employeeCard.Id,
                                Points = recognition.Odmena,
                                Type = "Uznanie",
                                Description = $"{recognition.Predmet}",
                                CreatedAt = DateTime.UtcNow,
                                RecognitionId = recognition.Id,
                                IsPositive = true
                            });
                        }
                    }
                }

                dbContext.Recognitions.Add(recognition);
                await dbContext.SaveChangesAsync();

                // notifikacie
                var users = await GetSurveyRecipientUsersAsync(recognition);
                var notifications = new List<Notification>();

                //NOTIFIKÁCIA PRE VEDÚCICH (iba ak existuje pending recipient)
                var hasPending = recognition.Recipients
                    .Any(r => r.State == EnumRecognitionState.Cakajuca);

                if (hasPending)
                {
                    var leaders = await GetApprovers(recognition);

                    foreach (var leader in leaders)
                    {
                        notifications.Add(new Notification
                        {
                            Id = Guid.NewGuid(),
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false,
                            NotificationType = EnumNotificationType.RecognitionApproval,
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
        // Vytvorenie uznania s prilohami
        //**********************************************************************************
        [HttpPost("CreateWithFiles")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec + "," + RolesDef.Spravca + "," + RolesDef.ShopAdmin)]
        public async Task<IActionResult> CreateRecognitionWithFiles([FromForm] RecognitionWithFilesRequest data)
        {
            if (data == null)
                return BadRequest("Neplatné dáta");

            // 1️⃣ Vytvor uznanie (bez príloh) - použijeme logiku z CreateRecognition
            var recognitionRequest = new RecognitionRequest
            {
                predmet = data.predmet,
                text = data.text,
                odmena = data.odmena,
                createdById = data.createdById,
                recipients = data.recipients.Select(r => new RecognitionRecipientRequest { id = r }).ToList()
            };

            // Zavoláme existujúcu logiku
            IActionResult createResult = await CreateRecognition(recognitionRequest);
            if (createResult is not OkObjectResult okResult)
                return createResult;


            //ukladanie suborov
            Guid recognitionId = ((dynamic)okResult.Value).id;
            var recognition = await dbContext.Recognitions
                .Include(r => r.Recipients)
                .FirstOrDefaultAsync(r => r.Id == recognitionId);

            if (recognition == null)
                return StatusCode(500, "Chyba pri načítaní vytvoreného uznania.");


            if (data.files != null && data.files.Count > 0)
            {
                if (data.files.Count > 3)
                    return BadRequest("Maximálne 3 prílohy.");

                BlobServiceClient client = new(config.GetSection("Blob")["BlobConnect"]);
                var container = client.GetBlobContainerClient("recognition-files");


                foreach (var file in data.files)
                {
                    if (file.Length > 10 * 1024 * 1024)
                        return BadRequest($"Súbor {file.FileName} je väčší ako 10MB.");

                    var fileName = $"{Guid.NewGuid()}.{file.FileName.Split('.').Last()}";
                    var blobClient = container.GetBlobClient(fileName);

                    await blobClient.UploadAsync(file.OpenReadStream(), true);

                    //ak su attachments null nastavime novy list

                    var attachment = new RecognitionAttachment
                    {
                        FileName = file.FileName,
                        FileUrl = blobClient.Uri.ToString(),
                        RecognitionId = recognition.Id
                    };

                    dbContext.RecognitionAttachment.Add(attachment);
                }

                //dbContext.RecognitionAttachments.AddRange(attachments);
                await dbContext.SaveChangesAsync();
            }

            return Ok(new { id = recognition.Id });
        
        }



        //**********************************************************************************
        // ziskanie priloh 
        //**********************************************************************************
        [HttpGet("GetAttachments/{recognitionId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec + "," + RolesDef.Spravca + "," + RolesDef.ShopAdmin)]
        public async Task<IActionResult> GetAttachments(Guid recognitionId)
        {
            var attachments = await dbContext.RecognitionAttachment
                .Where(a => a.RecognitionId == recognitionId)
                .Select(a => new
                {
                    id = a.Id,
                    fileName = a.FileName
                })
                .ToListAsync();

            return Ok(attachments);
        }



        //**********************************************************************************
        // stiahnutie priloh 
        //**********************************************************************************
        [HttpGet("DownloadAttachment/{attachmentId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec + "," + RolesDef.Spravca + "," + RolesDef.ShopAdmin)]
        public async Task<IActionResult> DownloadAttachment(Guid attachmentId)
        {
            var attachment = await dbContext.RecognitionAttachment
                .FirstOrDefaultAsync(a => a.Id == attachmentId);

            if (attachment == null)
                return NotFound("Príloha neexistuje.");

            BlobServiceClient blobServiceClient = new(config.GetSection("Blob")["BlobConnect"]);
            var container = blobServiceClient.GetBlobContainerClient("recognition-files");

            var blobName = new Uri(attachment.FileUrl).Segments.Last();
            var blobClient = container.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
                return NotFound("Súbor neexistuje v úložisku.");

            var stream = await blobClient.OpenReadAsync();

            return File(stream, "application/octet-stream", attachment.FileName);
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
                .FirstOrDefaultAsync(e => e.Id == recognition.createdBy.Id);

            if (creator == null)
                return EnumRecognitionState.Cakajuca;

            var creatorRoles = await userManager.GetRolesAsync(creator.User);
            bool creatorIsLeader = creatorRoles.Contains(RolesDef.Veduci);

            var recipientEmployee = await dbContext.EmployeeCards
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == recipient.EmployeeCardId);

            if (recipientEmployee == null)
                return EnumRecognitionState.Cakajuca;

            var recipientRoles = await userManager.GetRolesAsync(recipientEmployee.User);
            bool recipientIsLeader = recipientRoles.Contains(RolesDef.Veduci);

            //HLAVNÁ LOGIKA

            // vedúci → zamestnanec = auto schválené
            if (creatorIsLeader && !recipientIsLeader)
            {
                return EnumRecognitionState.Schvalena;
            }

            // vedúci → vedúci = admin schvaľuje
            if (creatorIsLeader && recipientIsLeader)
            {
                return EnumRecognitionState.Cakajuca;
            }

            // zamestnanec → ktokoľvek = vedúci schvaľuje
            return recognition.Odmena > 0
                ? EnumRecognitionState.Cakajuca
                : EnumRecognitionState.Schvalena;
        }



        private async Task<List<ExtendedIdentityUser>> GetApprovers(Recognition recognition)
        {
            var approvers = new List<ExtendedIdentityUser>();

            foreach (var recipient in recognition.Recipients
                .Where(r => r.State == EnumRecognitionState.Cakajuca))
            {
                var employee = await dbContext.EmployeeCards
                    .Include(e => e.User)
                    .FirstOrDefaultAsync(e => e.Id == recipient.EmployeeCardId);

                if (employee == null)
                    continue;

                var roles = await userManager.GetRolesAsync(employee.User);

                bool isLeader = roles.Contains(RolesDef.Veduci);

                if (!isLeader)
                {
                    //recipient je zamestnanec → schvaľuje vedúci
                    var leaders = await GetLeadersFromRecipientDepartments(recognition);
                    approvers.AddRange(leaders);
                }
                else
                {
                    //recipient je vedúci → schvaľuje správca
                    var admins = await userManager.GetUsersInRoleAsync(RolesDef.Spravca);
                    approvers.AddRange(admins);
                }
            }

            return approvers
                .GroupBy(u => u.Id)
                .Select(g => g.First())
                .ToList();
        }




        //**********************************************************************************
        // Ziskanie vsetkych veducich pre toho co to vytvoril pre oddelenie
        //**********************************************************************************
        private async Task<List<ExtendedIdentityUser>> GetLeadersFromRecipientDepartments(Recognition recognition)
        {
            // Všetci pending recipienti
            var pendingRecipients = recognition.Recipients
                .Where(r => r.State == EnumRecognitionState.Cakajuca)
                .ToList();

            foreach (var r in pendingRecipients)
            {
                r.EmployeeCard = await dbContext.EmployeeCards
                        .Include(e => e.Department)
                        .Include(e => e.User)
                        .FirstOrDefaultAsync(e => e.Id == r.EmployeeCardId);
            }

            var leaders = new List<ExtendedIdentityUser>();

            var groupedByDepartment = pendingRecipients.GroupBy(r => r.EmployeeCard.Department.Id);

            foreach (var group in groupedByDepartment)
            {
                var departmentId = group.Key;

                var employeesInDept = await dbContext.EmployeeCards
                    .Include(e => e.User)
                    .Where(e => e.Department.Id == departmentId)
                    .ToListAsync();

                foreach (var emp in employeesInDept)
                {
                    var roles = await userManager.GetRolesAsync(emp.User);
                    if (roles.Contains(RolesDef.Veduci))
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
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec + "," + RolesDef.Spravca + "," + RolesDef.ShopAdmin)]
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
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Spravca)]
        public async Task<IActionResult> ApproveRecognition([FromBody] RecognitioToApprove data)
        {
            var recipientRecord = await dbContext.Set<RecognitionRecipient>()
                .Include(r => r.EmployeeCard)
                    .ThenInclude(e => e.User)
                .Include(r => r.Recognition)
                .FirstOrDefaultAsync(r => r.Id == data.recipientRecordId);

            if (recipientRecord == null)
                return BadRequest("Záznam príjemcu neexistuje.");

            //schvalovat sa moze iba cakajuca odmena
            if (recipientRecord.State != EnumRecognitionState.Cakajuca)
                return BadRequest("Uznanie už bolo schválené alebo zamietnuté, odmena sa nedá meniť.");

            // Mapovanie state stringu → enum
            if (!Enum.TryParse<EnumRecognitionState>(data.state, out var newState))
                return BadRequest("Neplatný stav.");

            recipientRecord.State = newState;
            recipientRecord.dovod = data.dovod;

            if (data.odmena.HasValue)
                recipientRecord.Recognition.Odmena = data.odmena.Value;

            await dbContext.SaveChangesAsync();

            // Notifikácia iba ak je stav schválený
            if (newState == EnumRecognitionState.Schvalena || newState == EnumRecognitionState.SchvalenaSUpravou || newState == EnumRecognitionState.Zamietnuta)
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


            //pripocitanie bodov
            if ((newState == EnumRecognitionState.Schvalena || newState == EnumRecognitionState.SchvalenaSUpravou))
            {
                var employeeCard = await dbContext.EmployeeCards
                    .FirstOrDefaultAsync(e => e.Id == recipientRecord.EmployeeCardId);

                var points = recipientRecord.Recognition.Odmena;

                if (employeeCard != null && recipientRecord.Recognition != null)
                {
                    employeeCard.PointsBalance += points;
                    dbContext.PointsTransactions.Add(new PointsTransaction
                    {
                        Id = Guid.NewGuid(),
                        EmployeeCardId = employeeCard.Id,
                        Points = points,
                        Type = "Uznanie",
                        Description = $"{recipientRecord.Recognition.Predmet}",
                        CreatedAt = DateTime.UtcNow,
                        RecognitionId = recipientRecord.Recognition.Id,
                        IsPositive = true
                    });
                }

                await dbContext.SaveChangesAsync();
            }

            return Ok();
        }


        //**********************************************************************************
        // Historia bodov
        //**********************************************************************************
        [HttpGet("GetPointsHistory/{employeeId}")]
        public async Task<IActionResult> GetPointsHistory(Guid employeeId)
        {
            var data = await dbContext.PointsTransactions
                .Where(p => p.EmployeeCardId == employeeId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    id = p.Id,
                    date = p.CreatedAt,
                    points = p.Points,
                    type = p.Type,
                    description = p.Description,
                    recognitionId = p.RecognitionId,
                    isPositive = p.IsPositive
                })
                .ToListAsync();

            return Ok(data);
        }



        //**********************************************************************************
        // Ziskanie stavu bodov pre pouzivatela
        //**********************************************************************************
        [HttpGet("GetPointsBalance/{employeeId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec + "," + RolesDef.Spravca + "," + RolesDef.ShopAdmin)]
        public async Task<IActionResult> GetPointsBalance(Guid employeeId)
        {
            var employee = await dbContext.EmployeeCards
                .FirstOrDefaultAsync(ec => ec.Id == employeeId);

            if (employee is null)
                return BadRequest("EmployeeCard neexistuje.");

            return Ok(employee.PointsBalance);
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



        public class RecognitionWithFilesRequest
        {
            public string predmet { get; set; }
            public string text { get; set; }
            public int odmena { get; set; }
            public Guid createdById { get; set; }

            public List<Guid> recipients { get; set; } = new();

            public List<IFormFile>? files { get; set; }
        }

    }
}
