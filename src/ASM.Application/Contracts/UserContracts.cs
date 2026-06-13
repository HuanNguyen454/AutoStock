namespace ASM.Application.Contracts;

public record UserDto(
    Guid Id,
    string UserName,
    string FullName,
    string Email,
    string Role,
    bool IsActive);

public record CreateUserRequest(
    string UserName,
    string FullName,
    string Email,
    string Password,
    string Role);
