using AGILE2024_BE.Data;
using AGILE2024_BE.Models;
using AGILE2024_BE.Models.Identity;
using AGILE2024_BE.Models.Requests;
using AGILE2024_BE.Models.Requests.EmployeeCardRequests;
using AGILE2024_BE.Models.Requests.JobPositionRequests;

using AGILE2024_BE.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Reflection.Emit;
using AGILE2024_BE.Models.Recognition;
using System.Globalization;

namespace AGILE2024_BE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize(Roles = RolesDef.Spravca)]
    public class EmployeeCardController : ControllerBase
    {
        private UserManager<ExtendedIdentityUser> userManager;
        private RoleManager<IdentityRole> roleManager;
        private IConfiguration config;
        private AgileDBContext dbContext;

        public EmployeeCardController(UserManager<ExtendedIdentityUser> um, IConfiguration co, RoleManager<IdentityRole> rm, AgileDBContext db)
        {
            this.userManager = um;
            this.config = co;
            this.roleManager = rm;
            this.dbContext = db;
        }

        [HttpPost("Update")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Update([FromBody] UpdateEmployeeCardRequest employeeCardRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await dbContext.Users.FindAsync(employeeCardRequest.UserId);
            var employeeCard = await dbContext.EmployeeCards
                .FindAsync(employeeCardRequest.Id);

            var level = await dbContext.Levels.FindAsync(employeeCardRequest.Level);
            var location = await dbContext.Locations.FindAsync(employeeCardRequest.Location);
            var department = await dbContext.Departments.FindAsync(employeeCardRequest.Department);
            var contractType = await dbContext.ContractTypes.FindAsync(employeeCardRequest.ContractType);

            employeeCard.Birthdate = DateTime.ParseExact(
                employeeCardRequest.Birth,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture
            );
            employeeCard.ContractType = contractType;
            employeeCard.Location = location;
            employeeCard.Department = department;
            employeeCard.WorkPercentage = employeeCardRequest.WorkTime;
            employeeCard.Level = level;
            employeeCard.StartWorkDate = DateTime.ParseExact(
                employeeCardRequest.StartWorkDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture
            );
            employeeCard.LastEdited = DateTime.Now;

            var oldPoints = employeeCard.PointsBalance;
            var newPoints = employeeCardRequest.points;

            var rozdiel = newPoints - oldPoints;

            employeeCard.PointsBalance = newPoints;

            if (rozdiel != 0)
            {
                dbContext.PointsTransactions.Add(new PointsTransaction
                {
                    Id = Guid.NewGuid(),
                    EmployeeCardId = employeeCard.Id,
                    Points = rozdiel,
                    Type = "ADJUSTMENT",
                    Description = $"Manuálna úprava bodov adminom ({oldPoints} → {newPoints})",
                    CreatedAt = DateTime.UtcNow,
                    IsPositive = rozdiel > 0
                });
            }

            dbContext.EmployeeCards.Update(employeeCard);

            await dbContext.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("EmployeeCards")]
        //[Authorize(Roles = RolesDef.Veduci)]
        public async Task<IActionResult> EmployeeCards()
        {
            ExtendedIdentityUser? user = await userManager.FindByEmailAsync(User.Identity?.Name!);
            var superiorId = user.Id;

            if (string.IsNullOrEmpty(superiorId))
            {
                return Unauthorized("Nedokázali sme získať informácie o prihlásenom používateľovi.");
            }
            // Získanie zamestnancov, ktorí majú rolu zamestnanec a patria do oddelenia so zadaným superiorId
            var employeeCards = await dbContext.EmployeeCards
            .Include(ec => ec.User)
            .Where(ec => ec.Department != null)
            .Join(
                dbContext.UserRoles,
                ec => ec.User.Id,
                ur => ur.UserId,
                (ec, ur) => new { EmployeeCard = ec, UserRole = ur }
            )
            .Join(
                dbContext.Roles,
                combined => combined.UserRole.RoleId,
                r => r.Id,
                (combined, role) => new { EmployeeCard = combined.EmployeeCard, Role = role, Department = combined.EmployeeCard.Department }
            )
            .Where(joined => joined.Role.Name == RolesDef.Zamestnanec)
            //.Where(joined => joined.Department.Superior.id == superiorId.ToString()) // Filtrovanie podľa SuperiorId v Department
            .Select(joined => new EmployeeCardResponse
            {
                EmployeeId = joined.EmployeeCard.Id,
                Email = joined.EmployeeCard.User.Email,
                Name = joined.EmployeeCard.User.Name ?? string.Empty,
                TitleBefore = joined.EmployeeCard.User.Title_before ?? string.Empty,
                TitleAfter = joined.EmployeeCard.User.Title_after ?? string.Empty,
                Department = joined.Department.Name ?? "N/A",
                DepartmentId = joined.Department.Id.ToString() ?? string.Empty,
                Surname = joined.EmployeeCard.User.Surname ?? string.Empty,
                MiddleName = joined.EmployeeCard.User.MiddleName
            })
                .ToListAsync();

            return Ok(employeeCards);
        }

        [HttpPost("Deactivate/{userId}")]
        public async Task<IActionResult> Deactivate(string userId)
        {
            var employeeCard = await dbContext.EmployeeCards.FirstAsync(x => x.User.Id == userId);

            employeeCard.Archived = !employeeCard.Archived;

            dbContext.EmployeeCards.Update(employeeCard);

            await dbContext.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("GetUserByEmployeeCard")]
        public async Task<IActionResult> GetUserByEmployeeCard(string employeeCardId)
        {
            // Assuming you have a DbContext or repository to access EmployeeCard and User data
            var employeeCard = await dbContext.EmployeeCards
                .Include(ec => ec.User) // Include the related user
                .FirstOrDefaultAsync(ec => ec.Id.ToString() == employeeCardId
                );

            if (employeeCard == null || employeeCard.User == null)
            {
                return NotFound("Employee card or user not found.");
            }

            // Get the user information from the User object associated with the EmployeeCard
            var user = employeeCard.User;
            var roles = await userManager.GetRolesAsync(user);

            var userIdentityResponse = new UserIdentityResponse
            {
                id = user.Id,
                Email = user.Email!,
                FirstName = user.Name ?? string.Empty,
                LastName = user.Surname ?? string.Empty,
                TitleBefore = user.Title_before ?? string.Empty,
                TitleAfter = user.Title_after ?? string.Empty,
                Role = roles.FirstOrDefault(),
                ProfilePicLink = user.ProfilePicLink
            };

            return Ok(userIdentityResponse);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var employeeCard = await dbContext.EmployeeCards.Include(x => x.User).ToListAsync();

            return Ok(employeeCard);
        }



        [HttpGet("GetAllEmployeesExceptMe")]
        public async Task<IActionResult> GetAllEmployeesExceptMe()
        {
            var userId = this.userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var employeesZamestnanec = await userManager.GetUsersInRoleAsync(RolesDef.Zamestnanec);
            var employeesVeduci = await userManager.GetUsersInRoleAsync(RolesDef.Veduci);

            var employees = employeesZamestnanec
                .Concat(employeesVeduci)
                .Where(u => u.Id != userId)
                .ToList();

            var employeeCards = await dbContext.EmployeeCards
                .Include(ec => ec.User)
                .Where(ec => employees.Select(e => e.Id).Contains(ec.User.Id))
                .Select(ec => new EmployeeCardResponse
                {
                    EmployeeId = ec.Id,
                    Email = ec.User.Email,
                    Name = ec.User.Name ?? string.Empty,
                    TitleBefore = ec.User.Title_before ?? string.Empty,
                    TitleAfter = ec.User.Title_after ?? string.Empty,
                    Department = ec.Department.Name ?? "N/A",
                    Surname = ec.User.Surname ?? string.Empty,
                    MiddleName = ec.User.MiddleName
                })
                .ToListAsync();

            return Ok(employeeCards);
        }



        [HttpGet("GetAllEmployees")]
        public async Task<IActionResult> GetAllEmployees()
        {
            var employeesZamestnanec = await userManager.GetUsersInRoleAsync(RolesDef.Zamestnanec);
            var employeesVeduci = await userManager.GetUsersInRoleAsync(RolesDef.Veduci);
            var employees = employeesZamestnanec.Concat(employeesVeduci).ToList();
            var employeeCards = await dbContext.EmployeeCards
            .Include(ec => ec.User)
            .Where(ec => employees.Contains(ec.User))
            .Select(ec => new EmployeeCardResponse
            {
                EmployeeId = ec.Id,
                Email = ec.User.Email,
                Name = ec.User.Name ?? string.Empty,
                TitleBefore = ec.User.Title_before ?? string.Empty,
                TitleAfter = ec.User.Title_after ?? string.Empty,
                Department = ec.Department.Name ?? "N/A",
                Surname = ec.User.Surname ?? string.Empty,
                MiddleName = ec.User.MiddleName
            }).ToListAsync();

            return Ok(employeeCards);
        }

        [HttpGet("GetEmployeesWithouSuperiors")]
        public async Task<IActionResult> GetEmployeesWithouSuperiors()
        {
            var employeesZamestnanec = await userManager.GetUsersInRoleAsync(RolesDef.Zamestnanec);
            var employeeCards = await dbContext.EmployeeCards
            .Include(ec => ec.User)
            .Where(ec => employeesZamestnanec.Contains(ec.User))
            .Select(ec => new EmployeeCardResponse
            {
                EmployeeId = ec.Id,
                Email = ec.User.Email,
                Name = ec.User.Name ?? string.Empty,
                TitleBefore = ec.User.Title_before ?? string.Empty,
                TitleAfter = ec.User.Title_after ?? string.Empty,
                Department = ec.Department.Name ?? "N/A",
                Organization = ec.Department.Organization.Name,
                JobPosition = ec.Level.JobPosition.Name,
                Surname = ec.User.Surname ?? string.Empty,
                MiddleName = ec.User.MiddleName,
                FullName = ec.User.Name + " " + ec.User.Surname

            }).ToListAsync();

            return Ok(employeeCards);
        }


        [HttpGet("GetEmployeesInTeam")]
        public async Task<IActionResult> GetEmployeesInTeam()
        {
            var user = await userManager.FindByEmailAsync(User.Identity?.Name!);

            if (user == null)
            {
                return Unauthorized("User not found.");
            }
            var employeesZamestnanec = await userManager.GetUsersInRoleAsync(RolesDef.Zamestnanec);
            var employeesVeduci = await userManager.GetUsersInRoleAsync(RolesDef.Veduci);
            var employeesSpravca = await userManager.GetUsersInRoleAsync(RolesDef.Spravca);

            var allEmployees = employeesZamestnanec.Concat(employeesVeduci).Concat(employeesSpravca).Distinct().ToList();


            var employeeCards = await dbContext.EmployeeCards
                .Include(ec => ec.User)
                .Include(ec => ec.Level)
                .ThenInclude(l => l.JobPosition)
                .Include(ec => ec.Department)
                .ThenInclude(d => d.Organization)
                .Where(ec => employeesZamestnanec.Contains(ec.User) || employeesVeduci.Contains(ec.User))
                .ToListAsync();
            var userDepartmentId = employeeCards
                .FirstOrDefault(ec => ec.User.Id == user.Id && employeesVeduci.Contains(ec.User))
                ?.Department?.Id;

            if (userDepartmentId == null)
            {
                return NotFound("User's department (lead) not found.");
            }

            var employeesInSameDepartment = employeeCards
                .Where(ec => ec.Department != null && ec.Department.Id == userDepartmentId && employeesZamestnanec.Contains(ec.User))
                //.Where(ec => ec.Department.Id == userDepartmentId && allEmployees.Contains(ec.User))
                .Select(ec => new EmployeeCardResponse
                {
                    EmployeeId = ec.Id,
                    Email = ec.User.Email,
                    Name = ec.User.Name ?? string.Empty,
                    TitleBefore = ec.User.Title_before ?? string.Empty,
                    TitleAfter = ec.User.Title_after ?? string.Empty,
                    Department = ec.Department.Name ?? "N/A",
                    Organization = ec.Department.Organization.Name,
                    JobPosition = ec.Level.JobPosition.Name,
                    Surname = ec.User.Surname ?? string.Empty,
                    MiddleName = ec.User.MiddleName,
                    FullName = ec.User.Name + " " + ec.User.Surname

                }).ToList();

            return Ok(employeesInSameDepartment);
        }

        [HttpGet("GetCurrentSuperiorDepartmentTeam")]
        [Authorize]
        public async Task<IActionResult> GetCurrentSuperiorDepartmentTeam()
        {
            var user = await userManager.FindByEmailAsync(User.Identity?.Name!);
            var employee = await dbContext.EmployeeCards
                .Include(e => e.User)
                .Include(e => e.Department)
                    .ThenInclude(d => d.EmployeeCards)
                        .ThenInclude(ec => ec.User)
                .Include(e => e.Department.Organization)
                .Include(e => e.Level)
                    .ThenInclude(l => l.JobPosition)
                .FirstOrDefaultAsync(x => x.User == user);

            if (employee == null)
                return NotFound("Employee not found");

            var team = employee.Department.EmployeeCards.Select(ec => new
            {
                employeeId = ec.Id,
                userId = ec.User.Id,
                name = ec.User.Name + " " + ec.User.Surname,
                department = ec.Department.Name
            }).ToList();

            return Ok(team);
        }

      [HttpGet("GetEmployeeCardLoggedIn")]
        public async Task<IActionResult> GetEmployeeCardLoggedIn()
        {
            //ExtendedIdentityUser? user = await userManager.FindByEmailAsync(User.Identity?.Name!);

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return Unauthorized();
            }

            var loggedEmployee = await dbContext.EmployeeCards
                .Include(ec => ec.User)
                .Include(ec => ec.Level)
                .ThenInclude(l => l.JobPosition)
                .Include(ec => ec.Department)
                .ThenInclude(d => d.Organization)
                .Where(ec => ec.User.Id == user.Id).FirstOrDefaultAsync();
            if (loggedEmployee == null)
                return NotFound("Zamestnanec nenájdený.");

            var employyeCard = new EmployeeCardResponse
            {
                EmployeeId = loggedEmployee.Id,
                Email = loggedEmployee.User.Email,
                Name = loggedEmployee.User.Name ?? string.Empty,
                TitleBefore = loggedEmployee.User.Title_before ?? string.Empty,
                TitleAfter = loggedEmployee.User.Title_after ?? string.Empty,
                Department = loggedEmployee.Department.Name ?? "N/A",
                Organization = loggedEmployee.Department.Organization.Name,
                JobPosition = loggedEmployee.Level.JobPosition.Name,
                Surname = loggedEmployee.User.Surname ?? string.Empty,
                MiddleName = loggedEmployee.User.MiddleName
            };

            return Ok(employyeCard);
        }

    }
}
