using AGILE2024_BE.Data;
using AGILE2024_BE.Models;
using AGILE2024_BE.Models.Identity;
using AGILE2024_BE.Models.Response;
using AGILE2024_BE.Models.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Web.Http.ModelBinding;

namespace AGILE2024_BE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrganizationController : ControllerBase
    {
        private UserManager<ExtendedIdentityUser> userManager;
        private RoleManager<IdentityRole> roleManager;
        private IConfiguration config;
        private AgileDBContext dbContext;

        public OrganizationController(UserManager<ExtendedIdentityUser> um, IConfiguration co, RoleManager<IdentityRole> rm, AgileDBContext db)
        {
            this.userManager = um;
            this.config = co;
            this.roleManager = rm;
            this.dbContext = db;
        }

        [HttpGet("GetUnarchaved")]
        public async Task<IActionResult> GetUnarchaved()
        {
            var organizations = dbContext.Organizations.Where(x => x.Archived == false).ToList();

            return Ok(organizations);
        }
        [HttpGet("Organizations")]
        [Authorize(Roles = RolesDef.Spravca + ", " + RolesDef.Veduci + ", " + RolesDef.Zamestnanec + "," + RolesDef.ShopAdmin)]
        public async Task<IActionResult> Organizations()
        {
            try
            {
                var organizations = await dbContext.Organizations.ToListAsync();

                if (organizations == null || organizations.Count == 0)
                {
                    return NoContent();
                }

                return Ok(organizations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Došlo k internej chybe pri načítavaní organizácií.");
            }
        }

        [HttpPut("Archive")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Archive([FromBody] ArchiveOrganizationRequest archiveRequest)
        {
            if (!ModelState.IsValid) return BadRequest("Nesprávny vstup.");

            var organization = await dbContext.Organizations.FirstOrDefaultAsync(x => x.Id == archiveRequest.Id);

            if (organization == null)
            {
                return NotFound("Organizácia nebola nájdená.");
            }

            if (organization.Archived == archiveRequest.Archive)
            {
                return Ok();
            }

            organization.Archived = !organization.Archived;
            organization.LastEdited = DateTime.Now;
            dbContext.Organizations.Update(organization);
            await dbContext.SaveChangesAsync();

            if (organization.Archived)
            {
                return Ok("Organizácia bola archivovaná.");
            }
            else
            {
                return Ok("Organizácia bola zarchivovaná.");
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Organization(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Neplatná organizácia.");
            }

            try
            {
                var organization = await dbContext.Organizations
                    .Include(o => o.Location)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (organization == null)
                {
                    return NotFound("Organizácia nebola nájdená.");
                }

                return Ok(organization);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Došlo k internej chybe pri načítavaní organizácie.");
            }
        }

        [HttpPut("Update/{id}")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Update(Guid id, [FromBody] RegisterOrganizationRequest registerOrganizationRequest)
        {
            if (registerOrganizationRequest == null)
            {
                return BadRequest("Neplatné dáta.");
            }

            var organization = await dbContext.Organizations.FirstOrDefaultAsync(x => x.Id == id);
            if (organization == null)
            {
                return NotFound($"Organizácia s ID {id} nebola nájdená.");
            }

            organization.Code = registerOrganizationRequest.Code;
            organization.Name = registerOrganizationRequest.Name;
            organization.LastEdited = DateTime.Now;

            dbContext.Update(organization);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Došlo k internej chybe pri uložení organizácie.");
            }

            return Ok(organization);
        }

        [HttpPost("Delete")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Delete([FromBody] Guid selectedId)
        {
            if (selectedId == Guid.Empty)
            {
                return BadRequest("Neplatná organizácia.");
            }

            var organization = await dbContext.Organizations.FirstOrDefaultAsync(x => x.Id == selectedId);

            if (organization == null)
            {
                return NotFound("Organizácia nebola nájdená.");
            }

            var departments = await dbContext.Departments.Where(x => x.Organization.Id == selectedId).ToListAsync();
            foreach (var item in departments)
            {
                item.Organization = null;
            }
            dbContext.Departments.UpdateRange(departments);
            dbContext.Organizations.Remove(organization);
            await dbContext.SaveChangesAsync();

            return Ok("Organizácia bola vymazaná.");
        }

        [HttpGet("UnarchivedOrganizations")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> UnarchivedOrganizations()
        {
            var organizations = await dbContext.Organizations
            .Include(o => o.RelatedDepartments)
            .Include(o => o.JobPositions)
            .Include(o => o.Location)
            .Where(o => !o.Archived)
            .ToListAsync();

            var organizationsResponse = new List<OrganizationResponse>();

            foreach (var organization in organizations)
            {
                organizationsResponse.Add(new OrganizationResponse
                {
                    Id = organization.Id,
                    Name = organization.Name,
                    Code = organization.Code,
                    LastEdited = organization.LastEdited,
                    Created = organization.Created,
                    Archived = organization.Archived,
                    LocationName = organization.Location?.Name

                });
            }

            return Ok(organizationsResponse);
        }
        
        [HttpPost("Register")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Register([FromBody] RegisterOrganizationRequest registerRequest)
        {
            if (string.IsNullOrWhiteSpace(registerRequest.Name) || string.IsNullOrWhiteSpace(registerRequest.Code))
            {
                return BadRequest("Názov a kód sú povinné.");
            }

            Organization organization = new Organization
            {
                Name = registerRequest.Name,
                Code = registerRequest.Code,
                Created = DateTime.Now,
                LastEdited = DateTime.Now,
                Archived = false
            };

            try
            {
                await dbContext.Organizations.AddAsync(organization);
                await dbContext.SaveChangesAsync();

                return Ok(new { Message = "Organizácia bola úspešne zaregistrovaná.", OrganizationId = organization.Id });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Chyba pri registrácii organizácie: {ex.Message}");
                return StatusCode(500, "Došlo k chybe pri registrácii organizácie.");
            }
        }
    }
}
