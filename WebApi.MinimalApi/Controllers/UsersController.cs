using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : Controller
{
    private readonly IMapper mapper;

    private readonly IUserRepository userRepository;

    // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
    public UsersController(IUserRepository userRepository, IMapper mapper)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
    }

    [HttpGet("{userId}", Name = nameof(GetUserById))]
    [Produces("application/json", "application/xml")]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var user = userRepository.FindById(userId);
        if (user == null) return NotFound();
        var userDto = mapper.Map<UserDto>(user);
        return Ok(userDto);
    }

    [HttpPost]
    [Produces("application/json", "application/xml")]
    public IActionResult CreateUser([FromBody] PostCreateUserDto? user)
    {
        if (user is null)
            return BadRequest();
        if (string.IsNullOrEmpty(user.Login) || !user.Login.All(char.IsLetterOrDigit))
        {
            ModelState.AddModelError("login", "Invalid login");
            return UnprocessableEntity(ModelState);
        }


        var userEntity = mapper.Map<UserEntity>(user);

        var createdUserEntity = userRepository.Insert(userEntity);
        return CreatedAtRoute(
            nameof(GetUserById),
            new { userId = createdUserEntity.Id },
            createdUserEntity.Id);
    }

    [HttpPut("{userId}")]
    [Produces("application/json", "application/xml")]
    public IActionResult UpsertUser([FromRoute] Guid userId, [FromBody] PutUpsertUserDto? user)
    {
        if (userId == Guid.Empty || user is null)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        var userEntity = mapper.Map(user, new UserEntity(userId));
        userRepository.UpdateOrInsert(userEntity, out var isInserted);
        if (isInserted)
        {
            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId = userEntity.Id },
                userEntity.Id);
        }

        return NoContent();
    }

    [HttpPatch("{userId}")]
    [Produces("application/json", "application/xml")]
    public IActionResult PartiallyUpdateUser([FromRoute] Guid userId, [FromBody] JsonPatchDocument<PutUpsertUserDto>? patchDoc)
    {
        if (patchDoc is null) return BadRequest();
        if (userId == Guid.Empty) return NotFound();
        var user = userRepository.FindById(userId);
        if (user == null) return NotFound();
        var initialUserDto = mapper.Map<PutUpsertUserDto>(user);
        patchDoc.ApplyTo(initialUserDto, ModelState);
        if (string.IsNullOrEmpty(initialUserDto.FirstName))
        {
            ModelState.AddModelError("firstName", "Invalid First Name");
            return UnprocessableEntity(ModelState);
        }
        if (string.IsNullOrEmpty(initialUserDto.LastName))
        {
            ModelState.AddModelError("lastName", "Invalid Last Name");
            return UnprocessableEntity(ModelState);
        }
        if (!TryValidateModel(initialUserDto) || !ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        var newUser = mapper.Map<UserEntity>(initialUserDto);
        userRepository.Insert(newUser);
        return NoContent();
    }
}