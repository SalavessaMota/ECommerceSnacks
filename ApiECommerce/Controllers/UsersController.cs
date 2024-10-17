using Microsoft.AspNetCore.Mvc;
using ApiECommerce.Entities;
using ApiECommerce.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;

namespace ApiECommerce.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _appDbContext;
        private readonly IConfiguration _config;

        public UsersController(AppDbContext appDbContext, IConfiguration config)
        {
            _appDbContext = appDbContext;
            _config = config;
        }


        [HttpPost("[action]")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            var checkUser = await _appDbContext.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
            if (checkUser != null)
            {
                return BadRequest("There is already an user with that email.");
            }

            _appDbContext.Users.Add(user);
            await _appDbContext.SaveChangesAsync();
            return StatusCode(StatusCodes.Status201Created);
        }


        [HttpPost("[action]")]
        public async Task<IActionResult> Login([FromBody] User user)
        {
            var currentUser = await _appDbContext.Users.FirstOrDefaultAsync(u => u.Email == user.Email && u.Password == user.Password);

            if (currentUser == null)
            { 
                return NotFound("The user doesnt exist"); 
            }

            var key = _config["JWT:Key"] ?? throw new ArgumentNullException("JWT:Key", "JWT:Key cannot be null.");
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Email, user.Email!)
            };

            var token = new JwtSecurityToken(
                issuer: _config["JWT:Issuer"],
                audience: _config["JWT:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(10),
                signingCredentials: credentials);

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return new ObjectResult(new
            {
                AccessToken = jwt,
                TokenType = "bearer",
                UserId = currentUser.Id,
                UserName = currentUser.Name
            });
        }

        [Authorize]
        [HttpPost("uploaduserimage")]
        public async Task<IActionResult> UploadUserPhoto(IFormFile image)
        {
            var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var user = await _appDbContext.Users.FirstOrDefaultAsync(U => U.Email == userEmail);

            if (user == null) 
            {
                return NotFound("User not found");
            }

            if(image != null)
            {
                string uniqueFileName = $"{Guid.NewGuid().ToString()}_{image.FileName}";

                string filePath = Path.Combine("wwwroot/userimages", uniqueFileName);

                using(var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                user.UrlImage = $"/userimages/{uniqueFileName}";

                await _appDbContext.SaveChangesAsync();
                return Ok("Image uploaded successfully");
            }

            return BadRequest("No image uploaded");
        }


        [Authorize]
        [HttpGet("[action]")]
        public async Task<IActionResult> UserProfileImage()
        {
            //see if user is logged
            var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            //locate user
            var user = await _appDbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            if(user == null)
            {
                return NotFound("User not found");
            }

            var userImage = await _appDbContext.Users
                .Where(x => x.Email == userEmail)
                .Select(x => new
                {
                    x.UrlImage,
                })
                .SingleOrDefaultAsync();

            return Ok(userImage);
        }
    }
}
