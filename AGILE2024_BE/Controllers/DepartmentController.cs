using AGILE2024_BE.Data;
using AGILE2024_BE.Models;
using AGILE2024_BE.Models.Identity;
using AGILE2024_BE.Models.Requests.DepartmentRequests;
using AGILE2024_BE.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AGILE2024_BE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize(Roles = RolesDef.Spravca)]
    public class DepartmentController : ControllerBase
    {
        private UserManager<ExtendedIdentityUser> userManager;
        private RoleManager<IdentityRole> roleManager;
        private IConfiguration config;
        private AgileDBContext dbContext;

        public DepartmentController(UserManager<ExtendedIdentityUser> um, IConfiguration co, RoleManager<IdentityRole> rm, AgileDBContext db)
        {
            this.userManager = um;
            this.config = co;
            this.roleManager = rm;
            this.dbContext = db;
        }

        [HttpGet("Departments")]
        [Authorize(Roles = RolesDef.Spravca + ", " + RolesDef.Veduci + ", " + RolesDef.Zamestnanec + "," + RolesDef.ShopAdmin)]
        public async Task<IActionResult> Departments()
        {
            var departments = await dbContext.Departments
            .Include(d => d.Organization)
            .ToListAsync();


            var departmentResponse = new List<DepartmentResponse>();

            foreach (var department in departments)
            {
                departmentResponse.Add(new DepartmentResponse
                {
                    Id = department.Id,
                    Name = department.Name,
                    Code = department.Code,
                    OrganizationId = department.Organization.Id,
                    OrganizationName = department.Organization?.Name,
                    Created = department.Created,
                    LastEdited = department.LastEdited,
                    Archived = department.Archived,
                    ParentDepartmentId = department.ParentDepartment?.Id,
                    ParentDepartmentName = department.ParentDepartment?.Name
                });
            }

            return Ok(departmentResponse);
        }



        [HttpGet("DepartmentsByOrganization/{organizationId}")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> GetDepartmentsByOrganization(Guid organizationId)
        {
            var departments = await dbContext.Departments
                .Where(d => d.Organization.Id == organizationId && d.Archived != true)
                .ToListAsync();

            var departmentResponse = departments.Select(d => new DepartmentResponse
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                OrganizationId = d.Organization?.Id,
                OrganizationName = d.Organization?.Name,
                Created = d.Created,
                LastEdited = d.LastEdited,
                Archived = d.Archived,
                ParentDepartmentId = d.ParentDepartment?.Id,
                ParentDepartmentName = d.ParentDepartment?.Name
            }).ToList();

            return Ok(departmentResponse);
        }


        [HttpPost("Create")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest createRequest)
        {
            
            using (var transaction = await dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    if (createRequest == null)
                    {
                        return BadRequest("Invalid data.");
                    }

                    var organization = await dbContext.Organizations
                        .FirstOrDefaultAsync(o => o.Id.ToString() == createRequest.Organization);

                    if (organization == null && createRequest.Organization != null)
                    {
                        return BadRequest($"Organizacia {createRequest.Organization} nexistuje.");
                    }

                    string supString = createRequest.superior.ToString();

                    var superior = await dbContext.Users
                        .FirstOrDefaultAsync(o => o.Id == createRequest.superior.ToString());

                    //TODO: Check if superior is not as superiori in different department else change department of emplyoee card
                    var superiorDepartment = await dbContext.Departments
                        .FirstOrDefaultAsync(d => d.Superior.Id == superior!.Id!);
                    if (superiorDepartment != null)
                    {
                        return BadRequest($"Vedúci oddelenia {superior.Id} je už priradený k inému oddeleniu");
                    }

                    var newDepartment = new Department
                    {
                        Id = Guid.NewGuid(),
                        Name = createRequest.Name,
                        Code = createRequest.Code,
                        Organization = organization,
                        Archived = createRequest.Archived,
                        Superior = superior,
                    };
                    dbContext.Departments.Add(newDepartment);

                    if (createRequest.Created != null)
                    {
                        newDepartment.Created = createRequest.Created;
                    }

                    var parentDepartment = await dbContext.Departments
                        .FirstOrDefaultAsync(o => o.Id.ToString() == createRequest.ParentDepartmentId);

                    if (parentDepartment != null && parentDepartment.Id == newDepartment.Id)
                    {
                        return BadRequest("Nadradené oddelenie nemôže byť rovnaké ako vytvárané");
                    }

                    if (parentDepartment != null)
                    {
                        if (await IsCycleAsync(parentDepartment, newDepartment))
                        {
                            return BadRequest("Nie je možné priradiť kvôli cyklu.");
                        }
                        newDepartment.ParentDepartment = parentDepartment;
                    }

                    await dbContext.SaveChangesAsync();

                    if (createRequest.ChildDepartments != null && createRequest.ChildDepartments.Any())
                    {
                        foreach (var childDepartmentId in createRequest.ChildDepartments)
                        {
                            var childDepartment = await dbContext.Departments
                                .FirstOrDefaultAsync(d => d.Id.ToString() == childDepartmentId);

                            if (childDepartment != null)
                            {
                                if (childDepartment.Id == newDepartment.Id)
                                {
                                    return BadRequest("Podradené oddelenie nemôže byť rovnaké ako vytvárané");
                                }

                                if (await IsCycleAsync(newDepartment, childDepartment))
                                {
                                    return BadRequest("Nie je možné priradiť kvôli cyklu.");
                                }

                                childDepartment.ParentDepartment = newDepartment;
                                dbContext.Departments.Update(childDepartment);
                            }
                        }

                        await dbContext.SaveChangesAsync();
                    }
                    //change employee card for superior
                    var superiorEmployeeCard = await dbContext.EmployeeCards
                        .Include(e => e.User)
                        .FirstOrDefaultAsync(e => e.User.Id == superior.Id);
                    superiorEmployeeCard.Department = newDepartment;
                    dbContext.EmployeeCards.Update(superiorEmployeeCard);
                    await dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    Console.Error.WriteLine(ex);

                    return StatusCode(StatusCodes.Status500InternalServerError, "Error creating new department record.");
                }
            }
        }

        private async Task<bool> IsAncestorAsync(Guid childId, Guid ancestorId)
        {
            var childDepartment = await dbContext.Departments
                .Include(d => d.ParentDepartment)
                .FirstOrDefaultAsync(d => d.Id == childId);
            var createdDepartment = await dbContext.Departments
                .FirstOrDefaultAsync(d => d.Id == ancestorId);
            var otec = createdDepartment.ParentDepartment;
            while (otec != null)
            {
                if (otec.Id == childDepartment.Id)
                {
                    return true;
                }

                otec = otec.ParentDepartment;
            }

            return false; 
        }

        private async Task<bool> IsCycleAsync(Department parentDepartment, Department existingDepartment)
        {
            var childDepartments = await PreOrderTraversalAsync(existingDepartment);

            foreach (var child in childDepartments)
            {
                if (child.Id == parentDepartment.Id)
                {
                    return true; 
                }
            }

            return false;
        }

        private async Task<List<Department>> PreOrderTraversalAsync(Department rootDepartment)
        {
            var result = new List<Department>();
            result.Add(rootDepartment);
            var childDepartments = await dbContext.Departments
                .Where(d => d.ParentDepartment.Id == rootDepartment.Id)
                .ToListAsync();
            foreach (var child in childDepartments)
            {
                var childResults = await PreOrderTraversalAsync(child);
                result.AddRange(childResults);
            }
            return result;
        }


        [HttpPut("Edit/{id}")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Edit(Guid id, [FromBody] EditDepartmentRequest editRequest)
        {
            try
            {
                if (editRequest == null)
                {
                    return BadRequest("Invalid data.");
                }

                var existingDepartment = await dbContext.Departments
                    .Include(d => d.Organization)
                    .Include(d => d.ParentDepartment)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (existingDepartment == null)
                {
                    return NotFound("Oddelenie sa nenašlo");
                }

                var superior = await dbContext.Users
                    .FirstOrDefaultAsync(s => s.Id == editRequest.SuperiorId);

                if (superior == null && editRequest.SuperiorId != null)
                {
                    return BadRequest($"Vedúci oddelenia {editRequest.SuperiorId} neexistuje.");
                }

                var organization = await dbContext.Organizations
                    .FirstOrDefaultAsync(o => o.Id.ToString() == editRequest.Organization);

                if (organization == null && editRequest.Organization != null)
                {
                    return BadRequest($"Organizácia {editRequest.Organization} neexistuje.");
                }

                //Check if superior is from department
                var superiorEmployeeCard = await dbContext.EmployeeCards
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.User.Id == superior.Id);
                if (superiorEmployeeCard == null || superiorEmployeeCard!.Department!.Id != existingDepartment.Id)
                    return BadRequest($"Vedúci oddelenia nie je priradený ku oddeleniu {editRequest.Id}");

                existingDepartment.Name = editRequest.Name;
                existingDepartment.Code = editRequest.Code;
                existingDepartment.Superior = superior;
                existingDepartment.Organization = organization ?? existingDepartment.Organization; 
                existingDepartment.Archived = editRequest.Archived;

                if (editRequest.Created != null)
                {
                    existingDepartment.Created = editRequest.Created;
                }

   
                if (!string.IsNullOrEmpty(editRequest.ParentDepartmentId))
                {
                    var parentDepartment = await dbContext.Departments
                        .FirstOrDefaultAsync(d => d.Id.ToString() == editRequest.ParentDepartmentId);

                    if (parentDepartment != null)
                    {
                        if (parentDepartment.Id == existingDepartment.Id)
                        {
                            return BadRequest("Oddelenie niemôže byť nadradené sebe samému");
                        }

                        if (await IsCycleAsync(parentDepartment, existingDepartment))
                        {
                            return BadRequest("Zmena oddelenia vytvorí cyklus");
                        }

                        existingDepartment.ParentDepartment = parentDepartment;
                    }
                    else {
                        existingDepartment.ParentDepartment = null;
                    }
                }

                if (editRequest.ChildDepartmentsId != null && editRequest.ChildDepartmentsId.Any())
                {
                    foreach (var childDepartmentId in editRequest.ChildDepartmentsId)
                    {
                        var childDepartment = await dbContext.Departments
                            .FirstOrDefaultAsync(d => d.Id.ToString() == childDepartmentId);

                        if (childDepartment != null)

                            if (childDepartment.Id == existingDepartment.Id)
                            {
                                return BadRequest("Oddelenie niemôže byť podradené sebe samému");
                            }
                        {
                            if (await IsAncestorAsync(childDepartment.Id, existingDepartment.Id))
                            {
                                return BadRequest("Nie je možné priradiť podradené oddelenie");
                            }

                            childDepartment.ParentDepartment = existingDepartment;
                            dbContext.Departments.Update(childDepartment);
                        }
                    }
                }
                else {
                    var currentChildDepartments = await dbContext.Departments
                        .Where(d => d.ParentDepartment.Id == existingDepartment.Id)
                        .ToListAsync();

                    foreach (var childDepartment in currentChildDepartments)
                    {
                        childDepartment.ParentDepartment = null;
                        dbContext.Departments.Update(childDepartment);
                    }
                }
                

                await dbContext.SaveChangesAsync();

                return Ok(new { message = "Zmeny sa uložili" });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error editing department: {ex.Message}");
            }
        }


        [HttpGet("{departmentId}")]
        [Authorize]
        public async Task<IActionResult> GetDepartmentById(Guid departmentId)
        {
            var department = await dbContext.Departments
            .Include(d => d.Organization)
            .Include(d => d.ParentDepartment)
            .Include(d => d.Superior)
            .FirstOrDefaultAsync(d => d.Id == departmentId);

            if (department == null)
            {
                return NotFound(new { message = "Oddelenie s týmto ID neexistuje." });
            }
            string superiorRole = "";
            if (department.Superior != null)
            {
                var superiorRoles = await userManager.GetRolesAsync(department.Superior);
                superiorRole = superiorRoles.FirstOrDefault() ?? "";
            }

            var childDepartments = await dbContext.Departments
                .Where(d => d.ParentDepartment.Id == departmentId)
                .Select(d => new { d.Id, d.Name }) 
                .ToListAsync();

            var departmentResponse = new DepartmentResponse
            {
                Id = department.Id,
                Name = department.Name,
                Code = department.Code,
                SuperiorId = department.Superior?.Id,
                SuperiorName = department.Superior != null
                ? $"{department.Superior.Name} {department.Superior.Surname}, {superiorRole}"
                : null,
                OrganizationId = department.Organization.Id,
                OrganizationName = department.Organization?.Name,
                Created = department.Created,
                LastEdited = department.LastEdited,
                Archived = department.Archived,
                ParentDepartmentId = department.ParentDepartment?.Id,
                ParentDepartmentName = department.ParentDepartment?.Name,
                ChildDepartments = childDepartments.Select(d => d.Name).ToList() 

            };

            return Ok(departmentResponse);
        }



        [HttpPut("Archive")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Archive([FromBody] ArchiveDepartmentRequest archiveRequest)
        {
            var department = await dbContext.Departments.FirstOrDefaultAsync(d => d.Id == archiveRequest.Id);

            if (department == null)
            {
                return NotFound(new { message = "Oddelenie sa nenašlo" });
            }

            department.Archived = archiveRequest.Archive;
            dbContext.Departments.Update(department);
            await dbContext.SaveChangesAsync();


            return Ok(new { message = "Oddelenie úspešne archivované" });
        }

        [HttpDelete("Delete")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Delete([FromQuery] Guid selectedDepartmentId)
        {

            var department = await dbContext.Departments.FirstOrDefaultAsync(d => d.Id == selectedDepartmentId);

            if (department == null)
            {
                return NotFound(new { message = "Oddelenie sa nenašlo" });
            }


            bool isReferencedInEmployeeCard = await dbContext.EmployeeCards
                               .AnyAsync(e => e.Department.Id == selectedDepartmentId);

            bool isReferencedInDepartment = await dbContext.Departments
                               .AnyAsync(e => e.ParentDepartment.Id == selectedDepartmentId);

            if (isReferencedInEmployeeCard || isReferencedInDepartment)
            {
                return BadRequest(new { message = "Nie je možné vymazať, oddeleniu sú priradení zamestnanci alebo oddelenia" });
            }

            dbContext.Departments.Remove(department);
            await dbContext.SaveChangesAsync();
            return Ok(new { message = "Oddelenie úspešne vymazané" });
        }

        [HttpGet("GetAllUnarchived")]
        public async Task<IActionResult> GetAllUnarchived()
        {
            var result = await dbContext.Departments.Where(x => x.Archived == false).ToListAsync();

            return Ok(result);
        }

        [HttpGet("DepartmentsWithEmployeeCount")]
        [Authorize(Roles = RolesDef.Spravca + ", " + RolesDef.Veduci + ", " + RolesDef.Zamestnanec + "," + RolesDef.ShopAdmin)]
        public async Task<IActionResult> GetDepartmentsWithEmployeeCount()
        {
            try
            {
                var departments = await dbContext.Departments
                    .Include(d => d.Organization)
                    .Include(d => d.ParentDepartment)
                    .ToListAsync();

                var employeeRoleId = await dbContext.Roles
                    .Where(r => r.Name == "Zamestnanec")
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync();



                var departmentsWithEmployeeCount = new List<object>();

                foreach (var department in departments)
                {
                    var employeeCount = await dbContext.EmployeeCards
                        .Where(e => e.Department.Id == department.Id)
                        .Where(e => dbContext.UserRoles
                    .Any(ur => ur.UserId == e.User.Id && ur.RoleId == employeeRoleId))
                .CountAsync();

                    departmentsWithEmployeeCount.Add(new
                    {
                        department.Id,
                        department.Name,
                        department.Code,
                        EmployeeCount = employeeCount
                    });
                }

                return Ok(departmentsWithEmployeeCount);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error fetching departments with employee count.");
            }
        }

        [HttpGet("GetUsers")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> GetUsers(string? departmentId)
        {
            List<UserIdentityResponse> userIdentityResponses = new List<UserIdentityResponse>();
            List<EmployeeCard> employees = new List<EmployeeCard>();
            if (string.IsNullOrEmpty(departmentId))
            {
                employees = await dbContext.EmployeeCards
                    .Include(e => e.User)
                    .Include(e => e.Department)
                    .Include(e => e.Department!.Superior)
                    .Where(e => e.User != e.Department!.Superior)
                    .OrderBy(e => e.User!.Name)
                    .ThenBy(e => e.User!.Surname )
                    .ToListAsync();
            }
            else 
            {
                employees = await dbContext.EmployeeCards
                    .Include(e => e.User)
                    .Where(e => e.Department!.Id.ToString() == departmentId)
                    .OrderBy(e => e.User!.Name)
                    .ThenBy(e => e.User!.Surname)
                    .ToListAsync();
            }
            foreach (var employee in employees)
            {
                var roles = await userManager.GetRolesAsync(employee!.User);

                userIdentityResponses.Add(new UserIdentityResponse
                {
                    id = employee!.User.Id,
                    FirstName = employee!.User.Name,
                    LastName = employee!.User.Surname,
                    TitleBefore = employee!.User.Title_before,
                    TitleAfter = employee!.User.Title_after,
                    Email = employee!.User.Email!,
                    Role = roles.FirstOrDefault()
                });
            }
            return Ok(userIdentityResponses);
        }

    }
}
