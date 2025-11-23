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

            if (data.Options.Count == 0 || data.Options.Count > 6)
            {
                return BadRequest("Anketa musí mať aspoň 1 a maximálne 6 možností.");
            }

            var survey = new Survey
            {
                Id = Guid.NewGuid(),
                name = data.name,
                question = data.question,
                info = data.info,
                status = data.status,
                createdBy = createdBy,
                end = data.end,
                Options = data.Options.Select(o => new SurveyOption
                {
                    Id = Guid.NewGuid(),
                    survey = null,
                    answer = o.Answer
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
                .Include(s => s.Options)
                .Where(s => s.createdBy.Department.Id == departmentId)
                .ToListAsync();

            var surveyResponses = surveys.Select(s => new
            {
                s.Id,
                s.name,
                s.question,
                s.info,
                status = s.status.ToString(),
                createdById = s.createdBy.Id,
                departmentId = s.createdBy.Department.Id,
                options = s.Options.Select(o => new { id = o.Id, answer = o.answer })
            });

            return Ok(surveyResponses);
        }
    }

    public class SurveyRequest
    {
        public string name { get; set; }
        public string question { get; set; }
        public string? info { get; set; }
        public EnumSurveyState status { get; set; }

        public Guid createdById { get; set; }
        public DateTime end { get; set; } = DateTime.Now;
        public ICollection<SurveyOptionRequest> Options { get; set; } = new List<SurveyOptionRequest>();
    }


    public class SurveyOptionRequest
    {
        public string Answer { get; set; }
    }
}
