using AGILE2024_BE.Helpers;
using AGILE2024_BE.Models.Identity;
using AGILE2024_BE.Models.Requests.User;
using AGILE2024_BE.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AGILE2024_BE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private UserManager<ExtendedIdentityUser> userManager;
        private IConfiguration config;

        public UserController(UserManager<ExtendedIdentityUser> um, IConfiguration co)
        {
            this.userManager = um;
            this.config = co;
        }

        [HttpGet("Me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            ExtendedIdentityUser? user = await userManager.FindByEmailAsync(User.Identity?.Name!);

            if (user == null)
            {
                return NotFound();
            }

            var roles = await userManager.GetRolesAsync(user);
            var userIdentityResponse = new UserIdentityResponse
            {
                id = user.Id,
                Email = user.Email!, // Email is never null
                FirstName = user.Name ?? string.Empty,
                LastName = user.Surname ?? string.Empty,
                TitleBefore = user.Title_before ?? string.Empty,
                TitleAfter = user.Title_after ?? string.Empty,
                Role = roles.FirstOrDefault(),
                ProfilePicLink = user.ProfilePicLink
            };

            return Ok(userIdentityResponse);
        }

        [HttpGet("Users")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Users()
        {
            var users = await userManager.Users.ToListAsync();
            var userIdentityResponses = new List<UserIdentityResponse>();

            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);

                userIdentityResponses.Add(new UserIdentityResponse
                {
                    id = user.Id,
                    FirstName = user.Name,
                    LastName = user.Surname,
                    TitleBefore = user.Title_before,
                    TitleAfter = user.Title_after,
                    Email = user.Email!,
                    Role = roles.FirstOrDefault()
                });
            }

            return Ok(userIdentityResponses);
        }
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginUserRequest loginRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            ExtendedIdentityUser? user = await userManager.FindByEmailAsync(loginRequest.Email);

            if (user == null || !(await userManager.CheckPasswordAsync(user, loginRequest.Password)))
            {
                return NotFound("Zadali ste nesprávne údaje!");
            }

            LoginResponse loginResponse = new LoginResponse()
            {
                NewJwtToken = await Auth.GenerateJWT(user, userManager, config),
                NewRefreshToken = Auth.GenerateRT()
            };

            user.RefreshToken = loginResponse.NewRefreshToken;
            user.RefreshTokenExpiry = DateTime.Now.AddMinutes(30);
            await userManager.UpdateAsync(user);

            return Ok(loginResponse);
        }

        [HttpPost("Register")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Register([FromBody] RegisterUserRequest registerRequest)
        {
            if (registerRequest.Password != registerRequest.ConfirmPassword)
            {
                return BadRequest("Heslá sa nezhodujú");
            }
            ExtendedIdentityUser newUser = new ExtendedIdentityUser()
            {
                UserName = registerRequest.Email,
                Email = registerRequest.Email,
                Name = registerRequest.Name,
                Surname = registerRequest.Surname,
                Title_before = registerRequest.TitleBefore,
                Title_after = registerRequest.TitleAfter
            };

            var result = await userManager.CreateAsync(newUser, registerRequest.Password);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            await userManager.AddToRoleAsync(newUser, registerRequest.Role);

            return Ok();
        }

        [HttpPost("Update")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Update([FromBody] UpdateUserRequest registerRequest)
        {
            if (registerRequest.Password != registerRequest.ConfirmPassword)
            {
                return BadRequest("Heslá sa nezhodujú");
            }
            // Check if the user exists by email
            var user = await userManager.FindByEmailAsync(registerRequest.Email);
            if (user == null)
            {
                return NotFound("User not found");
            }
            // Change user password if different
            if (!string.IsNullOrEmpty(registerRequest.Password))
            { 
                string resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                IdentityResult passwordChangeResult = await userManager.ResetPasswordAsync(user, resetToken, registerRequest.Password);
                if (!passwordChangeResult.Succeeded)
                {
                    return BadRequest(passwordChangeResult.Errors);
                }
            }
            // Update user properties
            user.UserName = registerRequest.Email;
            user.Email = registerRequest.Email;
            user.Name = registerRequest.Name;
            user.Surname = registerRequest.Surname;
            user.Title_before = registerRequest.TitleBefore;
            user.Title_after = registerRequest.TitleAfter;
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
            // Update the user in the database
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return StatusCode(500, "Failed to update user information.");
            }

            // Update user role if it has changed
            var currentRoles = await userManager.GetRolesAsync(user);
            if (currentRoles.Count == 0 || currentRoles[0] != registerRequest.Role)
            {
                // Remove existing roles
                await userManager.RemoveFromRolesAsync(user, currentRoles);

                // Add new role
                await userManager.AddToRoleAsync(user, registerRequest.Role);
            }
            return Ok("Užívateľ sa upravil");
        }

        [HttpDelete("Delete")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Delete(string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return NotFound("Používateľ neexistuje");
            }
            await userManager.DeleteAsync(user);
            return Ok("Používateľ sa vymazal");
        }

        [HttpGet("GetUser")]
        public async Task<UpdateUserRequest?> GetUser(string email)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return null;
            }
            var roles = await userManager.GetRolesAsync(user);
            UpdateUserRequest updateUserRequest = new UpdateUserRequest
            {
                Email = user.Email!,
                Name = user.Name,
                Surname = user.Surname,
                Password = "",
                ConfirmPassword = "",
                TitleBefore = user.Title_before,
                TitleAfter = user.Title_after,
                Role = roles.FirstOrDefault()
            };
            return updateUserRequest;
        }


        [HttpPost("ChangePassword")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (request.NewPassword != request.ConfirmNewPassword)
            {
                return BadRequest("Nové heslá sa nezhodujú");
            }

            var user = await userManager.FindByEmailAsync(User.Identity?.Name!);

            if (user == null)
            {
                return NotFound("Používateľ neexistuje");
            }

            var result = await userManager.ChangePasswordAsync(
                user,
                request.CurrentPassword,
                request.NewPassword
            );

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok("Heslo bolo úspešne zmenené");
        }


        public class ChangePasswordRequest
        {
            public string ConfirmNewPassword { get; set; } = "";
            public string CurrentPassword { get; set; } = "";
            public string NewPassword { get; set; } = "";
        }

    }
}
