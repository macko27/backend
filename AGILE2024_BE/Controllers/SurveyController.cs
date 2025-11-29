using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AGILE2024_BE.Data;
using AGILE2024_BE.Models.Identity;
using Microsoft.EntityFrameworkCore;
using AGILE2024_BE.Models.Enums;
using AGILE2024_BE.Models.Survey;
using AGILE2024_BE.Models;

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
        [Authorize(Roles = RolesDef.Veduci)]
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

            var survey = new Survey
            {
                Id = Guid.NewGuid(),
                name = data.name,
                info = data.info,
                status = data.status,
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
                    }).ToList()
                }).ToList()
            };

           
            dbContext.Surveys.Add(survey);
            await dbContext.SaveChangesAsync();


            return Ok(new { surveyId = survey.Id });
        }


        //**********************************************************************************
        // Ziskanie ankiet pre daneho pouzivatela
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
                .ThenInclude(ec => ec.Department)
                .Include(s => s.Questions)
                .Where(s => s.createdBy.Department.Id == departmentId)
                .ToListAsync();

            var surveyResponses = surveys.Select(s => new
            {
                s.Id,
                s.name,
                s.info,
                status = s.status.ToString(),
                createdById = s.createdBy.Id,
                departmentId = s.createdBy.Department.Id,
                questions = s.Questions.Select(o => new { id = o.Id, answer = o.question })
            });

            return Ok(surveyResponses);
        }


        //**********************************************************************************
        // Vyhľadávanie zamestnancov / oddelení pre výber príjemcov
        //**********************************************************************************
        [HttpGet("SearchRecipients")]
        [Authorize(Roles = RolesDef.Veduci)]
        public async Task<IActionResult> SearchRecipients([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Ok(new List<object>());

            query = query.Trim().ToLower();

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

    }


    public class OptionRequest
    {
        public string answer { get; set; } = "";
    }
}


