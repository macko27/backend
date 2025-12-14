using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AGILE2024_BE.Data;
using AGILE2024_BE.Models.Identity;
using Microsoft.EntityFrameworkCore;
using AGILE2024_BE.Models.Enums;
using AGILE2024_BE.Models.Survey;
using AGILE2024_BE.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace AGILE2024_BE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SurveyController : Controller
    {
        private UserManager<ExtendedIdentityUser> userManager;
        private RoleManager<IdentityRole> roleManager;
        private IConfiguration config;
        private AgileDBContext dbContext;

        public SurveyController(UserManager<ExtendedIdentityUser> um, IConfiguration co, RoleManager<IdentityRole> rm, AgileDBContext db)
        {
            this.userManager = um;
            this.config = co;
            this.roleManager = rm;
            this.dbContext = db;
        }

        //**********************************************************************************
        // Vytvorenie ankety
        //**********************************************************************************
        [HttpPost("Create")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec)]
        public async Task<IActionResult> CreateSurvey([FromBody] SurveyRequest data)
        {

            var createdBy = await dbContext.EmployeeCards
                .FirstOrDefaultAsync(ec => ec.Id == data.createdById);

            if (createdBy == null)
            {
                return BadRequest("Vytvárajúci zamestnanec neexistuje.");
            }

            if (data.questions.Count == 0)
            {
                return BadRequest("Anketa musí mať aspoň 1 otázku");
            }

            EnumSurveyState calculatedStatus;
            var now = DateTime.Now;
            if (data.start <= now && now <= data.end)
            {
                calculatedStatus = EnumSurveyState.Aktívna; // 0
            }
            else
            {
                calculatedStatus = EnumSurveyState.Neaktívna; // 3
            }

            var survey = new Survey
            {
                Id = Guid.NewGuid(),
                name = data.name,
                info = data.info,
                status = calculatedStatus,
                SurveyType = data.surveyType,
                createdBy = createdBy,
                start = data.start,
                end = data.end,
                Recipients = data.recipients.Select(r => new Recipient
                {
                    Id = Guid.NewGuid(),
                    EmployeeCardId = r.id,
                    Type = r.type,
                }).ToList(),
                Questions = data.questions.Select(q => new SurveyQuestion
                {
                    Id = Guid.NewGuid(),
                    question = q.question,
                    Options = q.options.Select(o => new SurveyOption
                    {
                        Id = Guid.NewGuid(),
                        Answer = o.answer
                    }).ToList(),
                    answerType = q.answerType
                }).ToList(),
                anoPlatny = 1
            };

           
            dbContext.Surveys.Add(survey);
            await dbContext.SaveChangesAsync();


            return Ok(new { surveyId = survey.Id });
        }


        //**********************************************************************************
        // Ziskanie ankiet pre daneho pouzivatela
        // Zoznam ankiet
        //**********************************************************************************
        [HttpGet("GetByEmployee/{employeeId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec)]
        public async Task<IActionResult> GetSurveysByEmployee(Guid employeeId)
        {
            var employeeCard = await dbContext.EmployeeCards
                .Include(ec => ec.User)
                .Include(ec => ec.Department)
                .FirstOrDefaultAsync(ec => ec.Id == employeeId);

            if (employeeCard == null)
                return BadRequest("EmployeeCard pre daného používateľa neexistuje.");

            var departmentId = employeeCard.Department?.Id;

            var surveys = await dbContext.Surveys
                .Include(s => s.createdBy)
                .Include(s => s.Recipients)
                .Include(s => s.Questions)
                .Where(s =>
                    s.anoPlatny == 1 &&
                    (s.status == EnumSurveyState.Aktívna || s.status == EnumSurveyState.Uzavretá) &&
                    s.createdBy.Id != employeeCard.Id &&
                    (
                        s.Recipients.Any(r => r.Type == "employee" && r.EmployeeCardId == employeeCard.Id)
                        ||
                        s.Recipients.Any(r => r.Type == "department" && r.EmployeeCardId == departmentId)
                    )
                )
                .ToListAsync();

            var response = surveys.Select(s => new
            {
                s.Id,
                s.name,
                s.info,
                status = s.status.ToString(),
                createdById = s.createdBy.Id,
                start = s.start,
                end = s.end,
                questions = s.Questions.Select(q => new { id = q.Id, question = q.question })
            });

            return Ok(response);
        }


        //**********************************************************************************
        // Vratenie ankiet ktore vytvoril dany veduci podla id veduceho
        // Moje ankety
        //**********************************************************************************
        [HttpGet("GetMySurveys/{employeeId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec)]
        public async Task<IActionResult> GetMySurveys(Guid employeeId)
        {
            var employee = await dbContext.EmployeeCards
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
                return BadRequest("EmployeeCard neexistuje.");

            var surveys = await dbContext.Surveys
                .Include(s => s.Recipients)
                .Include(s => s.Questions)
                .Where(s => s.createdBy.Id == employee.Id && s.anoPlatny == 1)
                .ToListAsync();

            var response = surveys.Select(s => new
            {
                s.Id,
                s.name,
                s.info,
                status = s.status.ToString(),
                createdById = s.createdBy.Id,
                start = s.start,
                end = s.end,
                recipients = s.Recipients.Select(r => new { r.Id, r.Type, r.EmployeeCardId }),
                questions = s.Questions.Select(q => new
                {
                    id = q.Id,
                    question = q.question,
                    options = q.Options.Select(o => new { id = o.Id, answer = o.Answer })
                })
            });

            return Ok(response);
        }

        //**********************************************************************************
        // Vyhľadávanie zamestnancov / oddelení pre výber príjemcov
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
                    id = e.Id,
                    name = e.User.Name + " " + e.User.Surname,
                    type = "employee"
                })
                .Take(20)
                .ToListAsync();

            if (isEmployee && !isLeader)
            {
                return Ok(employees);
            }


            // 2. Oddelenia (názov oddelenia obsahuje query)
            var departments = await dbContext.Departments
                .Include(d => d.EmployeeCards)
                .Where(d => d.Name.ToLower().Contains(query))
                .Select(d => new
                {
                    id = d.Id,
                    name = d.Name,
                    type = "department",
                    count = d.EmployeeCards.Count
                })
                .Take(20)
                .ToListAsync();

            // 3. Spojenie výsledkov
            var result = employees.Cast<object>()
                .Concat(departments)
                .ToList();

            return Ok(result);
        }



        //**********************************************************************************
        // Detail ankety podľa ID
        //**********************************************************************************
        [HttpGet("GetDetail/{surveyId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec)]
        public async Task<IActionResult> GetSurveyDetail(Guid surveyId)
        {
            var survey = await dbContext.Surveys
                .Include(s => s.createdBy)
                .Include(s => s.Recipients)
                .Include(s => s.Questions)
                    .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(s => s.Id == surveyId && s.anoPlatny == 1);

            if (survey == null)
                return NotFound("Anketa neexistuje.");

            var totalRecipients = await CalculateTotalRecipientsAsync(survey);

            // neskôr doplníš keď budeš mať votes tabuľku
            var totalVotes = await GetTotalThatSubmittedVote(survey);

            var response = new
            {
                id = survey.Id,
                name = survey.name,
                info = survey.info,
                type = survey.SurveyType,
                status = survey.status.ToString(),
                start = survey.start,
                end = survey.end,
                createdBy = survey.createdBy.Id,
                totalRecipients = totalRecipients,
                totalVotes = totalVotes,
                questions = survey.Questions.Select(q => new
                {
                    id = q.Id,
                    question = q.question,
                    options = q.Options.Select(o => new
                    {
                        id = o.Id,
                        answer = o.Answer
                    }),
                    answerType = q.answerType
                })
            };

            return Ok(response);
        }


        private async Task<int> GetTotalThatSubmittedVote(Survey survey)
        {
            var total = await dbContext.SurveyAnswers
                .Where(a => a.survey.Id == survey.Id)
                .Select(a => a.user.Id)
                .Distinct() 
                .CountAsync();

            return total;
        }


        private async Task<int> CalculateTotalRecipientsAsync(Survey survey)
        {
            int total = 0;

            foreach (var r in survey.Recipients)
            {
                switch (r.Type)
                {
                    case "employee":
                        total += 1;
                        break;

                    case "department":
                        var departmentId = r.EmployeeCardId;

                        var deptCount = await dbContext.EmployeeCards
                            .Where(ec => ec.Department.Id == departmentId)
                            .CountAsync();

                        total += deptCount;

                        break;

                    default:
                        break;
                }
            }

            return total;
        }


        //**********************************************************************************
        // Detail ankety podľa ID
        //**********************************************************************************
        [HttpDelete("{surveyId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec)]
        public async Task<IActionResult> DeleteSurvey(Guid surveyId)
        {
            var survey = await dbContext.Surveys
                .Include(s => s.createdBy) // aby sme mali createdBy.Id
                .FirstOrDefaultAsync(s => s.Id == surveyId);

            if (survey == null)
                return NotFound(new { message = "Anketa neexistuje." });

            var userId = this.userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var employeeCard = await dbContext.EmployeeCards
                .FirstOrDefaultAsync(ec => ec.User.Id == userId);

            if (employeeCard == null)
                return Unauthorized();

            // Kontrola, či som autor ankety
            if (survey.createdBy.Id != employeeCard.Id)
                return Forbid("Nemôžete vymazať anketu, ktorú ste nevytvorili.");

            // Soft delete: nastav AnoPlatny = 0
            survey.anoPlatny = 0;

            try
            {
                await dbContext.SaveChangesAsync();
                return Ok(new { message = "Anketa bola úspešne vymazaná (soft delete)." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Chyba pri vymazávaní ankety.", error = ex.Message });
            }
        }


        //**********************************************************************************
        // Získanie názvu oddelenia pre vedúceho / zamestnanca
        //**********************************************************************************
        [HttpGet("GetMyDepartment/{employeeId}")]
        [Authorize(Roles = RolesDef.Veduci)]
        public async Task<IActionResult> GetMyDepartment(Guid employeeId)
        {
            // Načítanie EmployeeCard vrátane Department
            var employee = await dbContext.EmployeeCards
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.User.Id == employeeId.ToString());


            if (employee == null)
                return BadRequest("EmployeeCard neexistuje.");

            if (employee.Department == null)
                return BadRequest("Employee nemá priradené žiadne oddelenie.");

            // Vrátenie názvu oddelenia
            return Ok(new
            {
                Id = employee.Department.Id,
                departmentName = employee.Department.Name,
                type = "department",
            });
        }



        //**********************************************************************************
        // Odoslanie hlasovania používateľa
        //**********************************************************************************
        [HttpPost("SubmitVote/{surveyId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec)]
        public async Task<IActionResult> SubmitVote(Guid surveyId, [FromBody] VoteRequest data)
        {
            var userId = this.userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();


            var employee = await dbContext.EmployeeCards
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.User.Id == userId);
            if (employee == null)
                return Unauthorized("Používateľ nemá EmployeeCard.");


            var survey = await dbContext.Surveys
                .Include(s => s.Recipients)
                .Include(s => s.Questions)
                    .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(s => s.Id == surveyId && s.anoPlatny == 1);
            if (survey == null)
                return NotFound("Anketa neexistuje.");


            // kontrola času
            var now = DateTime.Now;
            if (!(survey.start <= now && now <= survey.end))
                return BadRequest("Anketa už nie je aktívna.");


            // kontrola či už hlasoval
            bool alreadyVoted = await dbContext.SurveyAnswers
            .AnyAsync(a =>
                a.user.Id == employee.Id &&
                a.survey.Id == survey.Id
            );
            if (alreadyVoted)
                return BadRequest("Už ste hlasovali v tejto ankete.");


            using var transaction = await dbContext.Database.BeginTransactionAsync();

            // uloženie odpovedí
            foreach (var item in data.answers)
            {
                var questionId = Guid.Parse(item.Key);
                var selectedOptionIds = item.Value.Select(Guid.Parse).ToList();

                var question = survey.Questions.FirstOrDefault(q => q.Id == questionId);
                if (question == null) continue;

                var answer = new SurveyAnswer
                {
                    Id = Guid.NewGuid(),
                    survey = survey,
                    question = question,
                    user = employee
                };

                answer.selectedOptions = selectedOptionIds.Select(oId => new SurveyOptionAnswer
                {
                    Id = Guid.NewGuid(),
                    OptionId = oId,
                    SurveyAnswer = answer
                }).ToList();

                dbContext.SurveyAnswers.Add(answer);
            }


            await dbContext.SaveChangesAsync();

            // prepočet po uložení hlasov
            var totalRecipients = await CalculateTotalRecipientsAsync(survey);
            var totalVotes = await GetTotalThatSubmittedVote(survey);

            if (totalRecipients <= totalVotes)
            {
                survey.status = EnumSurveyState.Uzavretá;
                await dbContext.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            return Ok(new { message = "Hlas bol uložený." });
        }

        //**********************************************************************************
        // Ziskanie odpovedi ak uz som hlasoval
        //**********************************************************************************
        [HttpPost("GetVotes/{surveyId}")]
        [Authorize(Roles = RolesDef.Veduci + "," + RolesDef.Zamestnanec)]
        public async Task<IActionResult> GetVotes(Guid surveyId)
        {
            var userId = this.userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var employee = await dbContext.EmployeeCards
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.User.Id == userId);
            if (employee == null)
                return Unauthorized("Používateľ nemá EmployeeCard.");

            var votes = await dbContext.SurveyAnswers
                .Where(a => a.survey.Id == surveyId && a.user.Id == employee.Id)
                .Include(a => a.question)
                .Include(a => a.survey)
                .Include(a => a.selectedOptions)
                .ToListAsync();

            if (votes == null || !votes.Any())
                return NotFound("Používateľ ešte nehlasoval v tejto ankete.");

            // Preformátovať do slovníka: { questionId: [optionId, ...] }
            var result = votes.ToDictionary(
                v => v.question.Id.ToString(),
                v => v.selectedOptions.Select(o => o.OptionId.ToString()).ToList()
            );

            return Ok(result);
        }

    }

    public class SurveyRequest
    {
        public string name { get; set; }
        public string? info { get; set; }
        public EnumSurveyState status { get; set; }

        public Guid createdById { get; set; }
        public DateTime start { get; set; } = DateTime.Now;
        public DateTime end { get; set; } = DateTime.Now;
        public string surveyType { get; set; } = "anonymous";
        public ICollection<RecipientRequest>? recipients { get; set; } = new List<RecipientRequest>();

        public ICollection<QuestionRequest> questions { get; set; } = new List<QuestionRequest>();
    }

    public class RecipientRequest
    {
        public Guid id { get; set; }
        public string? name { get; set; }
        public string? type { get; set; }
       
    }

    public class QuestionRequest
    {
        public string? question { get; set; }
        public ICollection<OptionRequest> options { get; set; } = new List<OptionRequest>();
        public String answerType { get; set; }

    }


    public class OptionRequest
    {
        public string answer { get; set; } = "";
    }


    public class VoteRequest
    {
        //id otazky a ku tomu id vsetkych zvolenych odpovedi
        public Dictionary<string, List<string>> answers { get; set; }
    }

}


